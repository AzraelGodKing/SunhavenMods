#!/usr/bin/env python3
"""Upload one zip to the mods website bucket and record it in downloads.json.

Same layout the site links: <game>/<slug>/<version>/<slug>-<version>.zip
plus a .sha256 file. downloads.json at the bucket root is the hosted-version record.

Requires CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID unless --dry-run.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import random
import re
import shutil
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timezone
from pathlib import Path

# Website slugs follow the mod folder name, except this Sun Haven package.
SLUG_OVERRIDES = {"SunHavenMuseumUtilityTracker": "smut"}


def slug_from_mod_dir(mod_dir: str) -> str:
    if mod_dir in SLUG_OVERRIDES:
        return SLUG_OVERRIDES[mod_dir]
    slug = re.sub(r"([a-z0-9])([A-Z])", r"\1-\2", mod_dir)
    slug = re.sub(r"[^a-zA-Z0-9]+", "-", slug).strip("-").lower()
    return slug

BUCKET = "azraels-mods"
MANIFEST_KEY = "downloads.json"
MANIFEST_ATTEMPTS = 5
MISSING_KEY = "The specified key does not exist"


def file_name(slug: str, version: str) -> str:
    return f"{slug}-{version}.zip"


def object_key(game: str, slug: str, version: str) -> str:
    return f"{game}/{slug}/{version}/{file_name(slug, version)}"


def record_upload(manifest: dict | None, entry: dict) -> dict:
    files = dict((manifest or {}).get("files") or {})
    files[f"{entry['game']}/{entry['slug']}"] = {
        "version": entry["version"],
        "file": file_name(entry["slug"], entry["version"]),
        "sha256": entry["sha256"],
        "bytes": entry["bytes"],
        "uploadedAt": entry["uploadedAt"],
    }
    return {"updatedAt": entry["uploadedAt"], "files": files}


def is_recorded(manifest: dict | None, entry: dict) -> bool:
    files = (manifest or {}).get("files") or {}
    current = files.get(f"{entry['game']}/{entry['slug']}") or {}
    return current.get("version") == entry["version"] and current.get("sha256") == entry["sha256"]


def wrangler(args: list[str]) -> subprocess.CompletedProcess[str]:
    command = ["npx", "--yes", "wrangler@4", "r2", "object", *args]
    return subprocess.run(command, check=False, text=True, capture_output=True)


def read_manifest(path: Path, run=wrangler) -> dict | None:
    """The bucket's downloads.json, an empty one if it does not exist yet, or None if it could not be read."""
    path.unlink(missing_ok=True)
    got = run(["get", f"{BUCKET}/{MANIFEST_KEY}", "--file", str(path), "--remote"])
    if got.returncode != 0:
        if MISSING_KEY in (got.stdout or "") + (got.stderr or ""):
            return {"updatedAt": None, "files": {}}
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None


def publish_manifest(root: Path, entry: dict, run=wrangler, sleep=time.sleep) -> bool:
    """Add this release to downloads.json and confirm it is still there afterwards.

    Releases that run at the same time each rewrite the whole file, so a parallel
    job can drop this entry. Re-read after a pause and write again until it stays.
    """
    path = root / "downloads.json"
    sleep(random.uniform(0, 10))
    for attempt in range(1, MANIFEST_ATTEMPTS + 1):
        manifest = read_manifest(path, run)
        if manifest is None:
            print(f"Could not read {MANIFEST_KEY} (attempt {attempt}); it was left as it is.", file=sys.stderr)
        elif is_recorded(manifest, entry):
            return True
        else:
            path.write_text(json.dumps(record_upload(manifest, entry), indent=2) + "\n", encoding="utf-8")
            put = run(
                [
                    "put",
                    f"{BUCKET}/{MANIFEST_KEY}",
                    "--file",
                    str(path),
                    "--remote",
                    "--content-type",
                    "application/json; charset=utf-8",
                    "--cache-control",
                    "public, max-age=60",
                ]
            )
            if put.returncode != 0:
                detail = ((put.stdout or "") + "\n" + (put.stderr or "")).strip()
                print(detail or f"wrangler failed ({put.returncode})", file=sys.stderr)
        sleep(random.uniform(5, 15))
    return is_recorded(read_manifest(path, run), entry)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--game", required=True)
    parser.add_argument("--slug", default="")
    parser.add_argument("--mod-dir", default="", help="Mod folder name. Used when --slug is omitted.")
    parser.add_argument("--version", required=True)
    parser.add_argument("--zip", required=True, dest="zip_path")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    slug = args.slug or slug_from_mod_dir(args.mod_dir)
    if not slug:
        print("Pass --slug or --mod-dir", file=sys.stderr)
        return 1

    zip_path = Path(args.zip_path)
    if not zip_path.is_file():
        print(f"Missing zip {zip_path}", file=sys.stderr)
        return 1

    data = zip_path.read_bytes()
    sha256 = hashlib.sha256(data).hexdigest()
    key = object_key(args.game, slug, args.version)
    uploaded_at = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"
    print(f"{'would upload' if args.dry_run else 'uploading'} {BUCKET}/{key}")
    print(f"sha256 {sha256} ({len(data)} bytes)")
    if args.dry_run:
        return 0

    token = os.environ.get("CLOUDFLARE_API_TOKEN") or ""
    account = os.environ.get("CLOUDFLARE_ACCOUNT_ID") or ""
    if not token or not account:
        print(
            "::warning::CLOUDFLARE_API_TOKEN or CLOUDFLARE_ACCOUNT_ID is missing. "
            "The website download was not updated.",
            file=sys.stderr,
        )
        return 0
    if shutil.which("npx") is None:
        print("npx is required to upload with wrangler", file=sys.stderr)
        return 1

    entry = {
        "game": args.game,
        "slug": slug,
        "version": args.version,
        "sha256": sha256,
        "bytes": len(data),
        "uploadedAt": uploaded_at,
    }
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        checksum_path = root / "file.sha256"
        checksum_path.write_text(f"{sha256}  {file_name(slug, args.version)}\n", encoding="utf-8")

        uploads = [
            ["put", f"{BUCKET}/{key}", "--file", str(zip_path), "--remote", "--content-type", "application/zip"],
            [
                "put",
                f"{BUCKET}/{key}.sha256",
                "--file",
                str(checksum_path),
                "--remote",
                "--content-type",
                "text/plain; charset=utf-8",
            ],
        ]
        for item in uploads:
            result = wrangler(item)
            if result.returncode != 0:
                detail = (result.stdout + "\n" + result.stderr).strip()
                print(detail or f"wrangler failed ({result.returncode})", file=sys.stderr)
                return result.returncode or 1
        if not publish_manifest(root, entry):
            print(
                f"::warning::{key} was uploaded but {MANIFEST_KEY} does not record it. "
                "The website still finds the zip when mod.json names this version.",
                file=sys.stderr,
            )
            return 0
    print(f"recorded {args.game}/{slug} {args.version}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
