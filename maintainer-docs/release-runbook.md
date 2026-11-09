# SunhavenMod Release Runbook (Draft)

This runbook is for manual release operations while CI changes are staged.

## 1) Start from Fresh Branch

- Branch from `main`.
- Use naming by purpose:
  - `mod/{mod-name}-initial-release`
  - `feature/{feature-description}`
  - `fix/{bug-description}`
  - `refactor/{description}`
  - `chore/{description}`

## 2) Update Version + Metadata

- Use build/version script when bumping:
  - `pwsh ./scripts/version/pre-push-build.ps1 -Mod <modkey> -Bump patch`
- Confirm updates in:
  - `docs/versions.json`
  - `<Mod>/Plugin.cs` or `<Mod>/PluginInfo.cs`
  - `<Mod>/thunderstore/manifest.json`

## 3) Validate References + Drift

- `pwsh ./scripts/build/check-sunhaven-refs.ps1 -SunHavenPath "<path>"`
- `pwsh ./scripts/version/verify-version-consistency.ps1`

## 4) Build Verification

- Build mod:
  - `pwsh ./scripts/version/pre-push-build.ps1 -Mod <modkey> -BuildOnly`
- Confirm mod loads in local Sun Haven test profile.

## 5) Prepare Commit

- Review diffs and ensure no game DLLs are included.
- Do not commit:
  - base-game DLLs
  - `bin/`, `obj/`, `.pdb`

## 6) Push and Trigger Release Workflow

- Push branch and open PR.
- After merge, trigger release workflow with correct inputs:
  - mod selection
  - GitHub release toggle
  - Thunderstore/Nexus publish toggles

## 7) Post-Release Verification

- Confirm GitHub release/tag exists and has expected zip (or was skipped as already tagged).
- Confirm Thunderstore version matches release version (or skip-same-version).
- Confirm Nexus upload (if enabled) or soft skip when Nexus already has this version — that skip must not fail the matrix for other mods.
- Confirm changelog text and version are correct.

## 8) Rollback / Hotfix

- Create `fix/{bug-description}` branch.
- Bump patch version.
- Re-run validation and release steps.
