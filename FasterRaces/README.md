# Faster Races

Configurable movement speed bonus for Sun Haven.

## Version

Published in [`docs/versions.json`](../docs/versions.json). Player-facing store text: [`thunderstore/README.md`](thunderstore/README.md).

## Default Behavior

- Adds a configurable speed bonus (default `+25%`).
- Applies only when `Enabled = true` in `FasterRaces.cfg`.
- Integrates with Haven's Birthright to avoid double speed stacking.

## Config

File: `Sun Haven/BepInEx/config/FasterRaces.cfg`

- `Enabled` (default `true`)
- `SpeedBonusPercent` (default `25`, clamped to `0`-`300`)

## Notes

- This README is intentionally short for repo maintainers.
- Player-facing details live in `thunderstore/README.md`.
- **Harmony:** `Player.GetStat` postfix uses **HarmonyPriority 300** so it runs **after** Haven's Birthright (**100**) — racial bonuses apply first, then Faster Races multiplies movement speed.

## Changelog

Shipped release notes: repo root [`CHANGELOG.md`](../CHANGELOG.md), [`docs/versions.json`](../docs/versions.json).

### 2026 (maintainer backlog — shipped bits tracked in CHANGELOG)

- **Build:** Added missing `using System;` in `Plugin.cs` so `Exception` in the race-name reflection guard compiles cleanly.
- **Lifecycle:** Expected-teardown `OnDestroy` logging and Harmony cleanup for plugin unload/quit paths.
- **Debug:** One-time BepInEx debug logging for reflection fallbacks.
