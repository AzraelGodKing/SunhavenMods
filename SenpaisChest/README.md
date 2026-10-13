# Senpai's Chest

Smart chest automation and sorting rules for Sun Haven.

## Version

Published in [`docs/versions.json`](../docs/versions.json). Player-facing store text: [`thunderstore/README.md`](thunderstore/README.md).

## Default Behavior

- Configure smart-chest rules by item, category, type, property, or group.
- Manage Groups now supports wildcard patterns (`*`, `?`) and can expand matches into item IDs.
- Supports chest labels and periodic scan-based sorting.
- Optional integration with S.M.U.T. and SunhavenTodo for museum workflows.

## Config

File: `Sun Haven/BepInEx/config/SenpaisChest.cfg`

## Notes

- This README is intentionally short for repository contributors.
- Full player-facing documentation is in `thunderstore/README.md`.

## Changelog

### 2026-07-02 (maintainer notes)

- **Fixed:** Smart-chest rules no longer deleted on area change. `Chest.OnDisable` no longer removes rules (only the **Remove Smart Chest** UI does). `InitializeAsOwner` skips disk reload when the same character is already active. Save will not overwrite a non-empty JSON with empty runtime state after a successful load unless the user cleared all rules via UI.

### 2026-05-03 (maintainer notes)

- **Groups + wildcard:** Manage Groups now supports wildcard patterns (`*`, `?`, case-insensitive) in addition to explicit item IDs.
- **Pattern expansion:** Added actions to convert pattern matches into concrete IDs in the group ("+IDs" per pattern and "Add Matches to IDs" from input).
- **UI behavior:** Wildcards are group-managed by default via **By Group** rules; optional config `UI.SeparateWildcardRuleInUI` can expose a standalone wildcard rule UI.

### 2026-05-02 (maintainer notes)

- **Fixed:** Added save protection in `SmartChestSaveSystem` to prevent overwriting an existing non-empty smart-chest JSON with an empty in-memory state before any successful character load in the current session.
- **Fixed:** `Plugin` now unsubscribes from `SceneManager.sceneLoaded` / `activeSceneChanged` and disposes museum integration handlers during teardown to prevent duplicate callbacks across reloads.
- **Fixed:** `MuseumTodoIntegration` now detaches its `OnDonationsChanged` subscription on dispose to avoid duplicate event handling and lingering references.
- **Changed:** UI-triggered saves now honor manager dirty state, reducing unnecessary writes and lowering risk of persisting transient empty runtime state.
- **Changed:** `[Scan] Next scan in ...` countdown logs are now optional Debug-level output, controlled by new config flag `Debug.EnableScanCountdownDebugLog`.
- **Changed:** "Copy Rules to All" now requires a second click confirmation within a short timeout to reduce accidental bulk overwrites.
- **Build:** Excluded `Tests/**/*.cs` from `SenpaisChest.csproj` compile items so NUnit test sources are not compiled into the runtime mod assembly.
- **Build:** Soft-dependency references to `SunHavenMuseumUtilityTracker` and `SunhavenTodo` now use `ProjectReference` to those sibling projects instead of `..\builds\...\*.dll`, matching other mods in the repo.
- **Fixed:** Configs now appear in BepInEx Configuration Manager. The mod binds entries to a custom `ConfigFile` (`SenpaisChest.cfg`) rather than the default per-GUID file, and Configuration Manager only scans each plugin's inherited `BaseUnityPlugin.Config` property — so it never saw our entries. `ConfigFileHelper.ReplacePluginConfig` now rewires that inherited property to the custom file via reflection right after Awake, so the live config UI picks up every `Config.Bind(...)` call without changing the config file name or path.
- Reworked chest-label rendering to use a screen overlay anchor so labels stay above chests.
- Hide chest labels while chest UI is open.
- Restrict chest labels to chest item/deco id `10110` only.
- Implement chest-label decoration ID allowlist (`10110` seeded) for easy future expansion.
- Add config-backed `ChestLabels.LabeledChestDecorationIds` list for runtime control of labeled chest IDs.
