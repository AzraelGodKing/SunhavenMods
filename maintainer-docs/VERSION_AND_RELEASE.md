# Versioning and release (single pipeline)

This repo uses **one release version per mod** in [`docs/versions.json`](../docs/versions.json). GitHub Actions, Thunderstore, and Nexus all read that file. The compiled DLL must report the **same** version (`PLUGIN_VERSION` / BepInEx metadata), or players and update checks will disagree with the store.

## Order of operations (required)

### One-shot release (recommended)

Run **Release & Publish** on GitHub Actions with:

1. **`bump_version`**: `patch`, `minor`, or `major` (or `none` to ship current `versions.json` as-is)
2. **`release_changelog`**: optional text written to `versions.json` before the bump
3. **`mod`**: `all` or a single mod key
4. Publish toggles as needed

The workflow runs **Version → verify → build → release** in one dispatch. The version job bumps `docs/versions.json`, syncs plugin/manifest/docs via `pre-push-build.ps1 -SyncOnly`, commits, and pushes; build/release checkout that commit.

Requires `ADMIN_PUSH_TOKEN` (or a branch policy that allows `GITHUB_TOKEN` pushes) when bumping on `main`.

### Manual path (still supported)

1. **Edit release metadata** for the mod(s) you will ship:
   - In `docs/versions.json`: bump **`version`**, update **`changelog`** (and any `vault_save_breaking` / banner fields for The Vault when needed).

2. **Propagate the version everywhere** the maintainer script knows about (plugin source, Thunderstore manifest, readmes, etc.):
   - From the repo root on **Windows** (PowerShell), either:
     - **Bump and sync:**  
       `.\scripts\version\pre-push-build.ps1 -Mod <modkey> -Bump patch`  
       (use `minor` or `major` when appropriate), **or**
     - **Sync only** (version already edited in `versions.json`):  
       `.\scripts\version\pre-push-build.ps1 -Mod <modkey>`  

   Mod keys match the workflow picker: `senpaischest`, `havensbirthright`, `sunhavenmuseumutilitytracker`, `squirrelsbirthdayreminder`, `sunhaventodo`, `thevault`, `havendevtools`, `havensalmanac`, `fasterraces`, `trinketfortune`, `cropoptimizer`, `havensrespec`, `giftingassistant`, etc. Use `-All` for every mod (see script help).

3. **Commit and push** to the branch your workflows use (usually `main`).

4. **CI check:** **Release & Publish** runs [`scripts/version/verify-version-consistency.py`](../scripts/version/verify-version-consistency.py) in the setup job. If `versions.json`, `PLUGIN_VERSION`, and `thunderstore/manifest.json` disagree, the workflow fails before building.

5. **Publish:** run **Release & Publish** with **`bump_version` = `none`**. Toggle GitHub Release / Thunderstore / Nexus as needed.

## Why version must come before the release build

- The **per-channel release gate** compares `docs/versions.json` to GitHub tags, Thunderstore, and Nexus (page + files API). Thunderstore is read from `https://thunderstore.io/api/experimental/package/<namespace>/<name>/` (`latest.version_number`). Each channel is skipped independently when our version is not newer. A Thunderstore upload that returns "package already exists" is the same skip: the job succeeds so the rest of the matrix keeps releasing. Same-version Nexus is a soft skip for the same reason. Shipping without bumping skips every requested channel.
- The **DLL** must be built from sources that already contain the new `PLUGIN_VERSION`, or the artifact version and the published metadata will not match.

## Local checks

If you only need to validate consistency (for example before pushing), use either entry point (both end up on the same Python script):

**PowerShell wrapper** (locates `python` / `py` and runs the Python script — **Python 3.6+ must be installed and on PATH**):

```powershell
.\scripts\verify-version-consistency.ps1
```

**Direct Python:**

```bash
python3 scripts/version/verify-version-consistency.py
```

The Python verifier requires Python 3.6+.

## Mod matrix ownership

Structural mod metadata now lives in [`scripts/matrix/mod-matrix.json`](../scripts/matrix/mod-matrix.json). This is the single source of truth for mod wiring used by scripts and workflows (mod keys, directories, plugin file path, csproj path, Thunderstore package name, docs page path, docs label aliases).

When adding a new mod:

1. Add one entry to `scripts/matrix/mod-matrix.json`.
2. Add the mod's release metadata entry in [`docs/versions.json`](../docs/versions.json).
3. Regenerate the hub copy (do not hand-edit `docs/mod-matrix.json`):
   - `npm run sync-mod-matrix` (runs `scripts/matrix/sync-docs-mod-matrix.js`). CI also checks that `docs/mod-matrix.json` matches the generator.

Required matrix fields per row: `modKey`, `jsonKey`, `modDir`, `pluginFile`, `dllName`, `csproj`, `thunderstoreName`, `readmePath`, `indexDataName`, `docsPagePath`, `extraCsprojPaths`.

## Automatic sync on merge (optional drift repair)

The workflow [`.github/workflows/sync-mod-versions.yml`](../.github/workflows/sync-mod-versions.yml) runs on every push to `main`. It executes `.\scripts\version\pre-push-build.ps1 -All -SyncOnly` (no `dotnet build`), then `python3 scripts/version/verify-version-consistency.py`. If any tracked file was behind `docs/versions.json`, it commits and pushes with message `chore: sync mod versions from versions.json [skip ci]` so the job does not re-trigger in a loop.

**Source of truth remains `docs/versions.json`.** The workflow only copies those values into plugin sources and store metadata; it does not invent new semver bumps.

## Related files

| Piece | Role |
|--------|------|
| `docs/versions.json` | Source of truth for semver, changelog, store links |
| `scripts/version/release_version_gate.py` | Per-channel GH/Thunderstore/Nexus version gate (skip-if-same, no fail) |
| `scripts/version/nexus_post_changelog.py` | POST Nexus Changelog tab after a successful file upload |
| `.github/actions/nexus-upload` | File upload with page version bump, archive previous, Vortex primary |
| `scripts/version/pre-push-build.ps1` | Bump/sync version across plugin, manifests, docs (`-SyncOnly` for CI without game DLLs) |
| `scripts/version/verify-version-consistency.py` | CI + local guard: JSON vs plugin vs manifest |
| `scripts/version/verify-version-consistency.ps1` | Windows helper that invokes `verify-version-consistency.py` (Python required) |
| `scripts/version/stage-version-sync-files.py` | Used by `sync-mod-versions.yml` to `git add` only paths `pre-push-build.ps1` may touch (avoids broad `git add -A`) |
| `.github/workflows/sync-mod-versions.yml` | After merge: align tracked files with `versions.json` if needed |
| `.github/workflows/build-mods.yml` | PR and `main` compile gate (localization, tests, self-hosted DLL build) |
| `.github/workflows/release-publish.yml` | Dispatch: optional bump → build → GitHub / Thunderstore / Nexus |
| `.github/workflows/live-stats.yml` | Hourly Thunderstore + Nexus counts on the `stats` branch (`live.json`) |
| [`maintainer-docs/ATOMIC_SAVE_POLICY.md`](ATOMIC_SAVE_POLICY.md) | Documents temp-file / on-failure behavior for The Vault vs Senpai's Chest vs Todo saves |
| [`maintainer-docs/NexusMods-BBCode-Index.txt`](NexusMods-BBCode-Index.txt) | Lists each mod’s `NexusMods-<ModFolder>-BBCode.txt` for paste into Nexus (BBCode mode) |
