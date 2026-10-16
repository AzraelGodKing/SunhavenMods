# SunhavenMod scripts

Maintainer tooling for version sync, mod matrix, stats, build checks, and localization. Structured for a future move to a dedicated tooling repository.

## Layout

| Folder | Purpose |
|--------|---------|
| [`lib/`](lib/) | Shared helpers (`RepoPaths.ps1`, `paths.js`) |
| [`matrix/`](matrix/) | Mod wiring SSOT and docs hub generator |
| [`stats/`](stats/) | Nexus / Thunderstore download stats |
| [`version/`](version/) | Version bump, sync, and CI consistency checks |
| [`build/`](build/) | Self-hosted runner preflight (game DLL refs) |
| [`localization/`](localization/) | `strings.json` validation, translation, audits |

`py -3` alone isn't reliable on every machine: on a box that was ever registered as a self-hosted GitHub Actions runner, the `py` launcher's entries can still point at a stale runner tool-cache path (e.g. `C:\actions-runner\_work\_tool\Python\3.14.0\x64\python.exe`) that no longer exists, and plain `python` on PATH may resolve to the Windows Store stub under `%LocalAppData%\Microsoft\WindowsApps\` instead of a real interpreter. `scripts/lib/Resolve-Python.ps1` validates every candidate (runs it and checks `sys.version_info[0] == 3`) before using it, so both failure modes are skipped automatically.

Override locally if you still need to force a specific interpreter:

```powershell
$env:PYTHON = 'C:\Users\you\AppData\Local\Programs\Python\Python311\python.exe'
.\scripts\version\verify-version-consistency.ps1
```

Resolution order: `$env:PYTHON` / `$env:PYTHON3` → (on GitHub Actions) `python3`/`python` from PATH → standard install folders (`%LocalAppData%\Programs\Python`, `Program Files`, newest version first) → validated `py -0p` entries → `python3`/`python` on PATH. See [`lib/Resolve-Python.ps1`](lib/Resolve-Python.ps1).

## Common commands

From repo root:

```powershell
# Sync plugin/manifest/docs versions from docs/versions.json (no dotnet build)
.\scripts\version\pre-push-build.ps1 -All -SyncOnly

# Verify versions.json ↔ PLUGIN_VERSION ↔ thunderstore manifest
python scripts/version/verify-version-consistency.py

# Regenerate docs/mod-matrix.json after editing matrix/
npm run sync-mod-matrix

# Refresh download stats cache
npm run stats

# CI localization gate (all mods with Localization/strings.json, including Gifting Assistant)
.\scripts\localization\validate-localization.ps1

# Translation audit (% cells still matching English)
.\scripts\localization\audit-untranslated.ps1
.\scripts\localization\audit-untranslated.ps1 -Mod TheVault -ShowPerLanguage

# Full MyMemory pass (long-running; optional log file)
.\scripts\localization\translate-all-localization.ps1 -ForceRetranslate -LogPath scripts\translate-run.log

# Apply a native-speaker pt-BR review markdown in-place (does not reformat strings.json)
python scripts/localization/apply-ptbr-review.py path/to/pt-BR-translation-review.md .
```

## CI wiring

| Workflow | Scripts |
|----------|---------|
| `release-publish.yml` | `matrix/sync-docs-mod-matrix.js`, `version/verify-version-consistency.py`, `version/set-versions-changelog.py`, `version/pre-push-build.ps1`, `version/stage-version-sync-files.py` |
| `sync-mod-versions.yml` | `version/pre-push-build.ps1`, `version/verify-version-consistency.py`, `version/stage-version-sync-files.py` |
| `build-mods.yml` | `build/check-sharedutilities-links.mjs`, `localization/validate-localization.ps1` |
| `live-stats.yml` | `stats/fetch-stats.js` (npm) |

## Mod matrix

Edit [`matrix/mod-matrix.json`](matrix/mod-matrix.json), then run `npm run sync-mod-matrix`. Localization scripts discover mods automatically when `Localization/strings.json` exists — no separate mod list to maintain.

## Cache and logs

`scripts/.gitignore` excludes `.cache/`, `*.log`, and translation run logs. MyMemory cache defaults to `scripts/.cache/translation-cache.json`.
