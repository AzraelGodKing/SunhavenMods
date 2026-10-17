# Version Bump Rules

Version numbers are automatically bumped when PRs are merged into `main`, based on the source branch name.

## Branch naming

| Branch prefix | Bump type | Example (from 1.2.3) |
|---------------|-----------|----------------------|
| `feat/*` | Minor (PATCH → 0) | 1.2.3 → 1.3.0 |
| `fix/*` | Patch | 1.2.3 → 1.2.4 |
| `release/*` | Major (MINOR & PATCH → 0) | 1.2.3 → 2.0.0 |
| `major/*` | Major | 1.2.3 → 2.0.0 |

Branches that do not match these prefixes (e.g. `main`, `UI_Update`) do not trigger a version bump.

## Examples

- `feat/add-museum-filter` — minor bump
- `fix/search-bar-focus` — patch bump
- `release/2.0.0` — major bump
- `major/breaking-api` — major bump

## Per-mod bumping

Only mods whose files were changed in the PR get a version bump. For example, if you merge `feat/add-todo` and only change files under `SenpaisChest/`, only Senpai's Chest is bumped.

Changes under `SharedUtilities/` or other non-mod paths do not trigger bumps.

## Updated files

For each affected mod, the workflow updates:

- `docs/versions.json`
- `{ModDir}/PluginInfo.cs` or `{ModDir}/Plugin.cs`
- `{ModDir}/thunderstore/manifest.json`
