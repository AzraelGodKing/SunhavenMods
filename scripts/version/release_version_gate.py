#!/usr/bin/env python3
"""Compare docs/versions.json version to GitHub, Thunderstore, and Nexus.

Per-channel gates (RimWorld-style): skip only the channel that already has
this version. Same-version Nexus does not fail the job or block GitHub /
Thunderstore when those still need a ship.

Stdlib only. Run from repo root.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

SEMVER = re.compile(r"(\d+(?:\.\d+)*)")
UNFETCHED = "unfetched"
NONE = "none"
GAME = "sunhaven"


def normalize_version(value: str) -> str:
    text = (value or "").strip()
    if text in {UNFETCHED, NONE}:
        return ""
    text = re.sub(r"^[vV]", "", text)
    match = SEMVER.search(text)
    return match.group(1) if match else text


def version_key(value: str) -> tuple[int, ...]:
    parts: list[int] = []
    for piece in normalize_version(value).split("."):
        try:
            parts.append(int(piece))
        except ValueError:
            parts.append(0)
    return tuple(parts)


def already_published(ours: str, remote: str) -> bool:
    """True when our version is not newer than the remote version."""
    ours_n = normalize_version(ours)
    remote_n = normalize_version(remote)
    if not ours_n or not remote_n:
        return False
    if ours_n == remote_n:
        return True
    try:
        return version_key(ours_n) <= version_key(remote_n)
    except (TypeError, ValueError):
        return False


def display_ver(value: str) -> str:
    return value if value else NONE


def write_github_output(path: Path, values: dict[str, str]) -> None:
    with path.open("a", encoding="utf-8") as fh:
        for key, value in values.items():
            if "\n" in value:
                fh.write(f"{key}<<EOF\n{value}\nEOF\n")
            else:
                fh.write(f"{key}={value}\n")


def truthy(value: str) -> bool:
    return value.strip().lower() in {"1", "true", "yes"}


def http_json(url: str, headers: dict[str, str]) -> tuple[int, object]:
    # Send a named agent: with urllib's default "Python-urllib/x", every
    # Thunderstore lookup came back "unfetched" in run #5.
    req = urllib.request.Request(
        url, headers={"User-Agent": "SunhavenMods-release-gate", **headers}
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            body = resp.read().decode("utf-8")
            return resp.status, json.loads(body) if body else {}
    except urllib.error.HTTPError as exc:
        raw = exc.read().decode("utf-8", errors="replace")
        try:
            parsed: object = json.loads(raw) if raw else {}
        except json.JSONDecodeError:
            parsed = {}
        return exc.code, parsed
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
        return 0, {}


def nexus_mod_id(nexus_url: str) -> str:
    match = re.search(r"/mods/(\d+)/?$", (nexus_url or "").strip())
    return match.group(1) if match else ""


def github_latest_and_tag(
    api_url: str, repo: str, ts_name: str, ours: str, token: str
) -> tuple[str, bool]:
    """Return (latest_version, exact_tag_exists)."""
    if not token or not repo or not ts_name:
        return UNFETCHED, False
    status, body = http_json(
        f"{api_url.rstrip('/')}/repos/{repo}/releases?per_page=100",
        {
            "Accept": "application/vnd.github+json",
            "Authorization": f"Bearer {token}",
        },
    )
    if status != 200 or not isinstance(body, list):
        return UNFETCHED, False
    prefix = f"{ts_name}-v"
    tag = f"{prefix}{ours}"
    best_ver = ""
    best_key: tuple[int, ...] | None = None
    tag_hit = False
    for item in body:
        if not isinstance(item, dict):
            continue
        name = str(item.get("tag_name") or "")
        if name == tag:
            tag_hit = True
        if not name.startswith(prefix):
            continue
        ver = name[len(prefix) :]
        if not normalize_version(ver):
            continue
        key = version_key(ver)
        if best_key is None or key > best_key:
            best_key = key
            best_ver = normalize_version(ver)
    return (best_ver or NONE), tag_hit


def thunderstore_latest(community: str, namespace: str, name: str) -> str:
    """Latest version_number, or none when the package does not exist.

    The community v1 URL (.../c/<community>/api/v1/package/<ns>/<name>/) is 404
    for these packages, which used to look like "unpublished" and made the
    upload fail with "package already exists". The experimental package
    endpoint returns latest.version_number. `community` is unused; callers
    still pass it.
    """
    del community
    if not namespace or not name:
        return UNFETCHED
    status, body = http_json(
        f"https://thunderstore.io/api/experimental/package/{namespace}/{name}/",
        {"Accept": "application/json"},
    )
    if status == 404:
        return NONE
    if status != 200 or not isinstance(body, dict):
        return UNFETCHED
    latest = body.get("latest")
    if not isinstance(latest, dict):
        return NONE
    return display_ver(str(latest.get("version_number") or ""))


def nexus_page_version(mod_id: str, api_key: str) -> str:
    if not mod_id or not api_key:
        return UNFETCHED
    status, body = http_json(
        f"https://api.nexusmods.com/v1/games/{GAME}/mods/{mod_id}.json",
        {"accept": "application/json", "apikey": api_key},
    )
    if status != 200 or not isinstance(body, dict):
        return UNFETCHED
    return display_ver(str(body.get("version") or ""))


def nexus_file_version(mod_id: str, api_key: str) -> str:
    if not mod_id or not api_key:
        return UNFETCHED
    status, body = http_json(
        f"https://api.nexusmods.com/v1/games/{GAME}/mods/{mod_id}/files.json",
        {"accept": "application/json", "apikey": api_key},
    )
    if status != 200 or not isinstance(body, dict):
        return UNFETCHED
    files = body.get("files") or []
    matched: list[tuple[int, str]] = []
    for item in files:
        if not isinstance(item, dict):
            continue
        version = str(item.get("version") or "")
        if not version:
            continue
        matched.append((int(item.get("uploaded_timestamp") or 0), version))
    if not matched:
        return NONE
    matched.sort()
    return display_ver(matched[-1][1])


def load_cache(path: Path, ttl: int) -> dict[str, str] | None:
    if not path.is_file() or ttl <= 0:
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    if not isinstance(data, dict):
        return None
    checked = int(data.get("checked_at_epoch") or 0)
    if checked <= 0 or (int(time.time()) - checked) >= ttl:
        return None
    return {
        "github_version": str(data.get("github_version") or ""),
        "thunderstore_version": str(data.get("thunderstore_version") or ""),
        "nexus_version": str(data.get("nexus_version") or ""),
        "nexus_page_version": str(data.get("nexus_page_version") or ""),
        "nexus_file_version": str(data.get("nexus_file_version") or ""),
        "github_tag_hit": str(data.get("github_tag_hit") or "false"),
    }


def save_cache(path: Path, values: dict[str, str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "checked_at_epoch": int(time.time()),
        "github_version": values.get("github_version", ""),
        "thunderstore_version": values.get("thunderstore_version", ""),
        "nexus_version": values.get("nexus_version", ""),
        "nexus_page_version": values.get("nexus_page_version", ""),
        "nexus_file_version": values.get("nexus_file_version", ""),
        "github_tag_hit": values.get("github_tag_hit", "false"),
    }
    path.write_text(json.dumps(payload) + "\n", encoding="utf-8")


def evaluate(
    *,
    version: str,
    thunderstore_name: str,
    nexus_url: str,
    repo: str,
    api_url: str,
    github_token: str,
    nexus_key: str,
    community: str,
    namespace: str,
    create_github: bool,
    publish_thunderstore: bool,
    publish_nexus: bool,
    has_nexus_target: bool,
    cache_file: Path | None,
    cache_ttl: int,
) -> dict[str, str]:
    cache_used = False
    cached = load_cache(cache_file, cache_ttl) if cache_file else None
    # An empty Thunderstore version is a failed lookup, not "unpublished".
    # Reuse of that cache made a second run upload a package that already existed.
    if (
        cached is not None
        and publish_thunderstore
        and not normalize_version(cached.get("thunderstore_version") or "")
    ):
        cached = None
    if cached is not None:
        cache_used = True
        gh_v = cached["github_version"]
        ts_v = cached["thunderstore_version"]
        page_v = cached.get("nexus_page_version") or NONE
        file_v = cached.get("nexus_file_version") or NONE
        nx_v = cached["nexus_version"] or (
            page_v if page_v not in {NONE, UNFETCHED, ""} else file_v
        )
        tag_hit = cached.get("github_tag_hit") == "true"
    else:
        gh_v, tag_hit = github_latest_and_tag(
            api_url, repo, thunderstore_name, version, github_token
        )
        ts_v = thunderstore_latest(community, namespace, thunderstore_name)
        mod_id = nexus_mod_id(nexus_url)
        page_v = nexus_page_version(mod_id, nexus_key) if mod_id else NONE
        file_v = nexus_file_version(mod_id, nexus_key) if mod_id else NONE
        nx_v = page_v if page_v not in {NONE, UNFETCHED} else file_v
        if cache_file is not None:
            save_cache(
                cache_file,
                {
                    "github_version": gh_v if gh_v not in {UNFETCHED} else "",
                    "thunderstore_version": ts_v if ts_v not in {UNFETCHED} else "",
                    "nexus_version": nx_v if nx_v not in {UNFETCHED, NONE} else "",
                    "nexus_page_version": page_v,
                    "nexus_file_version": file_v,
                    "github_tag_hit": "true" if tag_hit else "false",
                },
            )

    # Prefer exact release tag when we fetched live; fall back to latest == ours.
    github_hit = tag_hit or already_published(version, gh_v)
    ts_hit = already_published(version, ts_v)
    nexus_hit = False
    if has_nexus_target and nexus_mod_id(nexus_url):
        nexus_hit = already_published(version, page_v) or already_published(version, file_v)

    github_work = create_github and not github_hit
    ts_work = publish_thunderstore and not ts_hit
    nexus_work = publish_nexus and has_nexus_target and not nexus_hit
    has_work = github_work or ts_work or nexus_work

    sources: list[str] = []
    if github_hit:
        sources.append("github")
    if ts_hit:
        sources.append("thunderstore")
    if nexus_hit:
        sources.append("nexus")

    return {
        "version": version,
        "about_version": version,
        "github_version": gh_v if gh_v else NONE,
        "thunderstore_version": ts_v if ts_v else NONE,
        "nexus_version": nx_v if nx_v else NONE,
        "nexus_page_version": page_v if page_v else NONE,
        "nexus_file_version": file_v if file_v else NONE,
        "github_unchanged": "true" if github_hit else "false",
        "thunderstore_unchanged": "true" if ts_hit else "false",
        "nexus_unchanged": "true" if nexus_hit else "false",
        "has_work": "true" if has_work else "false",
        # Compat: true only when nothing left to ship for requested channels.
        "version_unchanged": "false" if has_work else "true",
        "version_match_sources": ",".join(sources),
        "upstream_github_version": "" if gh_v in {UNFETCHED, NONE} else gh_v,
        "upstream_thunderstore_version": "" if ts_v in {UNFETCHED, NONE} else ts_v,
        "upstream_nexus_version": "" if nx_v in {UNFETCHED, NONE} else nx_v,
        "upstream_cache_used": "true" if cache_used else "false",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", required=True)
    parser.add_argument("--thunderstore-name", required=True)
    parser.add_argument("--nexus-url", default="")
    parser.add_argument("--has-nexus-target", default="false")
    parser.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY", ""))
    parser.add_argument("--api-url", default=os.environ.get("GITHUB_API_URL", "https://api.github.com"))
    parser.add_argument("--community", default="sun-haven")
    parser.add_argument("--namespace", default="AzraelGodKing")
    parser.add_argument("--create-github-release", default="true")
    parser.add_argument("--publish-thunderstore", default="false")
    parser.add_argument("--publish-nexus", default="false")
    parser.add_argument("--dry-run", default="false")
    parser.add_argument("--release-anyway", default="false")
    parser.add_argument("--cache-file", default="")
    parser.add_argument("--cache-ttl", type=int, default=21600)
    parser.add_argument("--github-output", action="store_true")
    args = parser.parse_args()

    bypass = truthy(args.dry_run) or truthy(args.release_anyway)
    if args.thunderstore_name == "TestDiscord":
        bypass = True

    if bypass:
        values = {
            "version": args.version,
            "about_version": args.version,
            "github_version": NONE,
            "thunderstore_version": NONE,
            "nexus_version": NONE,
            "nexus_page_version": NONE,
            "nexus_file_version": NONE,
            "github_unchanged": "false",
            "thunderstore_unchanged": "false",
            "nexus_unchanged": "false",
            "has_work": "true",
            "version_unchanged": "false",
            "version_match_sources": "",
            "upstream_github_version": "",
            "upstream_thunderstore_version": "",
            "upstream_nexus_version": "",
            "upstream_cache_used": "false",
        }
    else:
        github_token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN") or ""
        nexus_key = os.environ.get("NEXUS_API_KEY") or os.environ.get("NEXUSMODS_API_KEY") or ""
        cache_path = Path(args.cache_file) if args.cache_file else None
        values = evaluate(
            version=args.version,
            thunderstore_name=args.thunderstore_name,
            nexus_url=args.nexus_url,
            repo=args.repo,
            api_url=args.api_url,
            github_token=github_token,
            nexus_key=nexus_key,
            community=args.community,
            namespace=args.namespace,
            create_github=truthy(args.create_github_release),
            publish_thunderstore=truthy(args.publish_thunderstore),
            publish_nexus=truthy(args.publish_nexus),
            has_nexus_target=truthy(args.has_nexus_target),
            cache_file=cache_path,
            cache_ttl=args.cache_ttl,
        )

    if args.github_output:
        out = os.environ.get("GITHUB_OUTPUT")
        if not out:
            print("GITHUB_OUTPUT is not set", file=sys.stderr)
            return 1
        write_github_output(Path(out), values)
    else:
        for key, value in values.items():
            print(f"{key}={value}")

    print(
        f"version={values['version']} GitHub={values['github_version']} "
        f"Thunderstore={values['thunderstore_version']} "
        f"Nexus={values['nexus_version']} "
        f"(page {values['nexus_page_version']}, file {values['nexus_file_version']}) "
        f"has_work={values['has_work']}",
        file=sys.stderr,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
