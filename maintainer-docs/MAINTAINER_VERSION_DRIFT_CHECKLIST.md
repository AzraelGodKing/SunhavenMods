# Maintainer Version Drift Checklist

Use this checklist before any release prep, documentation refresh, or compatibility pass.

## Global Verification

- Confirm each mod's current version in `docs/versions.json`.
- Verify each mod's version constant (`PluginInfo.cs` or inline `PLUGIN_VERSION`) matches `docs/versions.json`.
- Verify each Thunderstore manifest version matches `docs/versions.json`.
- Verify root `README.md` mod table and per-mod summary text match current versions and hotkeys.
- Verify each touched mod README has a changelog update entry when behavior changed.
- Keep root `CHANGELOG.md` player-facing only (gameplay/behavior/perf-impacting changes players care about).
- Put internal CI/build/devops/script/refactor notes in `MAINTAINER_CHANGELOG.md` instead of root `CHANGELOG.md`.
- Verify `builds/README.md` lists the correct required artifact set for each releasable mod.

## Mod-by-Mod File Checklist

### BirthdayReminder

- `BirthdayReminder/PluginInfo.cs`
- `BirthdayReminder/thunderstore/manifest.json`
- `BirthdayReminder/thunderstore/README.md`

### FasterRaces

- `FasterRaces/PluginInfo.cs`
- `FasterRaces/thunderstore/manifest.json`
- `FasterRaces/thunderstore/README.md`

### HavensAlmanac

- `HavensAlmanac/PluginInfo.cs`
- `HavensAlmanac/thunderstore/manifest.json`
- `HavensAlmanac/thunderstore/README.md`

### HavensBirthright

- `HavensBirthright/Plugin.cs`
- `HavensBirthright/thunderstore/manifest.json`
- `HavensBirthright/README.md`
- `HavensBirthright/thunderstore/README.md`

### HavensRespec

- `HavensRespec/PluginInfo.cs`
- `HavensRespec/thunderstore/manifest.json`
- `HavensRespec/README.md`
- `HavensRespec/thunderstore/README.md`

### HavensMirror

- `HavensMirror/PluginInfo.cs`
- `HavensMirror/thunderstore/manifest.json`
- `HavensMirror/README.md`
- `HavensMirror/thunderstore/README.md`

### SunhavenTodo

- `SunhavenTodo/PluginInfo.cs`
- `SunhavenTodo/Plugin.cs` (hotkey defaults)
- `SunhavenTodo/README.md`
- `SunhavenTodo/thunderstore/manifest.json`
- `SunhavenTodo/thunderstore/README.md`

### SunHavenMuseumUtilityTracker

- `SunHavenMuseumUtilityTracker/Plugin.cs`
- `SunHavenMuseumUtilityTracker/README.md`
- `SunHavenMuseumUtilityTracker/thunderstore/manifest.json`
- `SunHavenMuseumUtilityTracker/thunderstore/README.md`

### SenpaisChest

- `SenpaisChest/PluginInfo.cs`
- `SenpaisChest/SenpaisChest.csproj` (build dependencies)
- `SenpaisChest/thunderstore/manifest.json`
- `SenpaisChest/thunderstore/README.md`

### TheVault

- `TheVault/TheVault.csproj`
- `TheVault/README.md`
- `TheVault/thunderstore/manifest.json`
- `TheVault/thunderstore/README.md`
- `TheVault.Abstractions/TheVault.Abstractions.csproj`

### HavenDevTools

- `HavenDevTools/Plugin.cs`
- `HavenDevTools/thunderstore/manifest.json`
- `HavenDevTools/thunderstore/README.md`

### TrinketFortune

- `TrinketFortune/Plugin.cs`
- `TrinketFortune/thunderstore/manifest.json`
- `TrinketFortune/thunderstore/README.md`

## Final Sanity Pass

- Run `scripts/version/pre-push-build.ps1 -All` without version bump.
- Ensure no version was incremented unless explicitly requested by the owner.
- Confirm root `CHANGELOG.md` entries describe user-facing behavior changes, not only internal refactors.
- Confirm internal-only notes were recorded in `MAINTAINER_CHANGELOG.md` when applicable.
