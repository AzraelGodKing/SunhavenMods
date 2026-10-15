# Crop Optimizer

- **Thunderstore:** [CropOptimizer](https://thunderstore.io/c/sun-haven/p/AzraelGodKing/CropOptimizer/)
- **Nexus Mods:** [Crop Optimizer](https://www.nexusmods.com/sunhaven/mods/500) ([files tab](https://www.nexusmods.com/sunhaven/mods/500?tab=files))

Crop Optimizer adds field-level harvest forecasting, a lightweight crop HUD, and soft integrations with The Vault and Haven's Almanac.

## Version

**2.3.0** — published in [`docs/versions.json`](../docs/versions.json). Player-facing store text: [`thunderstore/README.md`](thunderstore/README.md).

## Features

- Tracks crop growth updates through a single Harmony patch on crop growth.
- HUD summary for tracked crops and projected sell value.
- **Field highlights:** bundled `Assets/tile_selection_sheet.png` (vanilla selection frames); yellow corners for dry tiles, purple corners for mana crops that are not infused yet, green corners for unfertilized; see `[Highlights]` in config.
- Optional The Vault integration via reflection when The Vault is installed (no hard dependency on `TheVault.Abstractions.dll`).
- Optional Haven's Almanac data surface for crop count and projected sell value.

## Integration Badges

| Integration | Status |
|---|---|
| Haven's Almanac | Compatible |
| The Vault | Soft dependency |

## Config

`BepInEx/config/CropOptimizer.cfg`

- `Enabled`
- `HUD.Enabled`
- `HUD.Scale`
- `HUD.PositionX` / `HUD.PositionY` (updated when you drag the HUD window)
- `HUD.ToggleKey`
- `Debug.DebugLogging`

## Roadmap (later)

- Sun Haven-themed item icons inside the tooltip (reuse the actual `ItemData.icon` sprite).
- Optional shortcut to open the Vault / journal filtered by the hovered crop.

## Changelog

### 2026-10-15 (docs)
- Removed Todo and Birthday integration claims. Crop Optimizer integrates with The Vault and Haven's Almanac only; those Todo/Birthday hooks never ran.

### 2026-10-10 (2.3.0)
- **Added (AZR-440):** Purple corner highlights on crops that can be mana infused but are not yet (requested by Achenar459). Uses the game's own Infuse check: mana-infusable seed, not infused, not dead, and Mana Infusion unlocked on the character. Shown at any growth stage, since an uninfused mana crop has to be infused before it can be harvested. Toggle with `Highlights.HighlightUninfusedManaCrops` (default on). Infusing needs no tool, so `OnlyWhenHoldingTool` does not hide these. When a tile is also dry, the dry highlight wins.

### 2026-08-30 (2.2.2)
- **Fixed (AZR-92):** Projected sell on the HUD (and hover) now includes **orbs** (Nel'Vari) and **tickets** (Withergate). Those harvests use `ItemSellInfo.orbSellPrice` / `ticketSellPrice` with gold at 0, so the farm total looked missing even though crop count and hover names already worked.

### 2026-05-02 (maintainer notes)
- **Lifecycle:** Improved plugin teardown diagnostics to distinguish expected menu/quit destroys from unexpected runtime destruction.
- **Debug:** Replaced silent HUD exception catches with debug logs for session-state probing and config flush failures.
- **Build:** Restored `SharedUtilities/VersionChecker.cs` link in `CropOptimizer.csproj` and fixed public data-surface accessibility (`CropOptimizerDataProvider.GetTopCrops` now returns a public DTO shape). Also restored `CropForecast.RemoveCropState(...)` used by lifecycle cleanup hooks.
- **Correctness:** Forecast entries are now removed when crops hit lifecycle end hooks (harvest/destroy paths), preventing stale crop counts and projected gold totals from lingering after crops disappear.
- **Safety:** `CropForecast.Snapshot()` now returns a read-only wrapper instead of the mutable backing dictionary, protecting running totals from accidental external mutation.
- **Performance:** Hover crop lookup now prefers a live crop registry populated from growth/lifecycle hooks, with scene-wide crop discovery used as periodic reconciliation instead of the only source of truth.
- **Nexus:** Canonical listing is [mod 500](https://www.nexusmods.com/sunhaven/mods/500) in [`docs/versions.json`](../docs/versions.json) (Haven's Almanac is [mod 501](https://www.nexusmods.com/sunhaven/mods/501)). `nexus_file_group_id` **7320911** remains for CI uploads.
- **Thunderstore:** Package `website_url` in `thunderstore/manifest.json` now points at the [Crop Optimizer listing](https://thunderstore.io/c/sun-haven/p/AzraelGodKing/CropOptimizer/); root README lists the same canonical link. `thunderstore/README.md` version line aligned with `docs/versions.json`.
- **Build:** Vault integration uses reflection at runtime; Crop Optimizer no longer references `TheVault.Abstractions` at compile time (optional Vault features activate only when The Vault is installed).
- **Performance:** Reduced per-frame work that could tank FPS on large farms: projected sell total is now maintained incrementally in `CropForecast` (no full dictionary scan each frame); gameplay camera resolution no longer calls `FindObjectsOfType<Camera>()` every frame when `Camera.main` is unset (cached + cooldown, invalidated on transitions); crop instance cache refresh slowed slightly; hover closest-crop search skips full O(n) scans while the pointer is stable within a short window; tooltip card content rebuilds are throttled when hovering the same crop; tile coord lookup tries `Wish.Crop` grid position / `WorldToCell` before the expensive nearest-`farmingData`-key scan.
- **Performance:** `CropHUD` no longer reapplies tooltip uGUI/`TMP` (`ApplyContent`) every frame — only when the tooltip text actually refreshes (~220 ms throttle or crop change); HUD headline stats refresh only when projected gold or tracked-count changes (`SetTooltipEnabled` still follows live cfg edits via a cheap bool compare).
- **Docs:** Added a dedicated Crop Optimizer docs page at [`docs/CropOptimizer/CropOptimizer.html`](../docs/CropOptimizer/CropOptimizer.html) — field-journal parchment theme with a sprout-green + harvest-gold palette and a twilight-field dark mode, matching the existing mod hub style. Covers HUD / hover tooltip features, a tooltip-anatomy grid mapping each row to its underlying game state, the full `CropOptimizer.cfg` reference, soft-integration notes (Sunhaven Todo / Birthday Reminder / The Vault / Haven's Almanac), and compatibility notes. A `crop-theme` card with a pulsing sprout-green "New!" badge was added to the mod hub (`docs/index.html`), and the page was registered in `docs/search-index.json` so it appears in `Ctrl+K` site search.
- **Fixed:** Configs now appear in BepInEx Configuration Manager. Each mod binds to a custom `ConfigFile` (e.g. `CropOptimizer.cfg`) rather than the default per-GUID file, and Configuration Manager's scanner only reads each plugin's inherited `BaseUnityPlugin.Config` property — so it never saw any of our entries. Added `ConfigFileHelper.ReplacePluginConfig` which rewires that inherited property to the custom file via reflection right after Awake, so the live config UI now picks up every `Config.Bind(...)` call without changing any config file names or paths.
- **Hover tooltip toggle on the HUD:** Added a small speech-bubble button in the top-right of the HUD header that flips the `HUD.HoverTooltip` config value with a single click. Gold bubble icon when tooltips are on, dimmed with a diagonal slash when off. Hover highlight + pressed-state feedback so it feels like a real button. Click writes the config value and forces a `ConfigFile.Save()` so the state survives a game restart. The view also re-reads the config each frame, so manual edits to `CropOptimizer.cfg` stay in sync with the button. A small "Toggle Crop Tooltips (on|off)" hover label appears below the button on mouse-over — implemented as a separate uGUI child of the button (not the `CropTooltipView`), so this button-tooltip is always shown regardless of the `HUD.HoverTooltip` state the button itself controls.
- **UI:** Replaced the IMGUI HUD and tooltip with a Sun Haven-themed uGUI design — wood-frame panel on a warm parchment body, TextMeshPro typography (uses the game's default TMP font), drag handle on the header bar. The hover card now has a crop-quality accent stripe (normal / silver / gold), procedural 16×16 pixel icons for sprout / water / fertilizer / mana / coin / clock / tile / check (no asset files — textures are generated at runtime), and colored stat rows (water turns blue when watered, brown when hoed-dry; fertilizer turns green when applied). Tooltip auto-sizes to its content and clamps inside the screen. Implemented with a dedicated `ScreenSpaceOverlay` canvas owned by the `CropHUD` runner so it survives scene transitions; UI is rebuilt on game-transition to pick up the current TMP font.
- **Fixed:** Hover tooltip previously rendered hundreds of pixels off-screen — the positioning math mixed the canvas's local coords (origin at pivot, usually center) with anchored-position coords (origin at top-left for the tooltip's anchor), so at the screen center the tooltip landed in the corner and near any edge it blew out of the viewport. Now converts the cursor's canvas-local point into top-left space before clamping and flips the card left/up when the default below-right placement would overflow.
- **Fixed:** HUD and tooltip panel interiors looked muddy because the drop-shadow sprite was parented to the panel; in uGUI children render *on top of* their parent, so the dark-tinted shadow was covering the parchment fill (`SetAsFirstSibling` only orders among siblings). Removed the shadow for now — the wood frame stands on its own. A proper shadow can come back later as a sibling layer behind the panel.
- **Readability:** Switched to a dark card look for both HUD and hover tooltip — wood-colored border around a near-black fill (~94 % opacity) instead of the parchment 9-slice, so cream/gold text stays readable over any farm tile. Rebuilt both panels using two nested solid Images (outer = border, inner inset 3 px = fill) which also dodges the 9-slice stretched-center artifacts that were rendering a ghost panel across the body rows. Row text bumped to 15 pt, icons to 18 px, header to 17 pt bold gold; accent numbers (ETA, gold, "Watered") now pop in warm gold / sky-blue, secondary notes (tile coord, "(cached)", extras) sit in muted cream.
- **Experimental:** HUD hover tooltip near the mouse lists crop item name (when resolvable), guessed water/fertilizer fields from `Wish.Crop` reflection, and growth ETA (live read or cached forecast). Toggle with `HUD.HoverTooltip` / tune `HUD.HoverTooltipMaxWorldDistance` in `CropOptimizer.cfg`. Uses a gameplay-camera fallback when `Camera.main` is unset, ray/plane mouse projection for orthographic 2D (fixes empty tooltips), XY distance to crops, default hover radius 5 world units; tooltip still draws when the summary HUD is hidden (F3).
- **Experimental:** Hover tooltip now also shows the **quality tier** (Normal / Silver / Gold + sell multiplier), **growth stage** (current / total with grown percent when available), **projected sell value** (`~Xg at shop`), **tile coord + farming state** via reflected `Wish.TileManager` (watered / hoed), and **item-panel extras** from `ItemData` (season, regrows, seed, museum / bundle flags — only when present). Falls back to reflected `Wish.Crop` water/fertilizer fields if the tile-state lookup fails.
- **Fixed:** Tooltip now reads the real `Wish.Crop` members confirmed via the one-time member dump: `Fertilized` / `ManaInfused` / `FullyGrown` / `PercentGrown` / `DaysLeft` (all properties, not fields — the previous field-only probes missed them). Fertilizer status is now reported directly from `Wish.Crop.Fertilized` (no more `?`), growth shows true `PercentGrown`, and "Ready now" is reported when `FullyGrown` is true.
- **Tile coord:** Uses `Wish.Crop.Position` (Vector3Int) as the source of truth for the farming tile instead of `transform.position` → `WorldToCell`, which could map crops to the wrong cell on orthographic grids. `TileManager` watered/hoed state now probes that coord and its 3×3 neighborhood.
- **Tile coord (v2):** Diagnostics revealed that neither `Wish.Crop.Position` nor `Grid.WorldToCell` aligned with `TileManager.farmingData` keys on the user's farm (key space and world space are offset by ~25 units on Y). Replaced the primary tile lookup with a **nearest-`farmingData`-key search** from the crop's world position, bounded to 64 world units. Since crops only grow on hoed/watered soil, the closest key is guaranteed to be the crop's real tile regardless of the tilemap's origin/scale transform. `WorldToCell` + `Wish.Crop.Position` remain as fallbacks.
- **Debug:** `[HoverDebug] Tile probe …` now logs the nearest `farmingData` key + its value and up to 3 sample keys, so any remaining coord-system mismatch (e.g. world-int vs cell-int) is visible in one line.
- **Water (sibling-scan fallback):** In-game testing showed `farmingData[tile] = Hoed` for tiles that are visibly watered — water state lives in a sibling structure on `TileManager`, not inside `farmingData`. `ProbeTile` now (a) takes live `IsWatered`/`IsHoed` results as ground truth, and (b) when `IsWatered` returns false, scans every `TileManager` field whose name contains `water` for `IDictionary`/`ICollection` membership of the tile coord (e.g. `wateredTiles` HashSet). Tooltip falls back to the old wording only when neither source reports watered.
- **Water (waterTileMap probe — definitive):** Deeper diagnostics revealed that `TileManager` exposes a dedicated `Tilemap waterTileMap` that owns the wet-soil sprites, and that `TileManager.IsWatered(Vector2Int)` and `wateredTiles` (a short `List<Vector2Int>` of queued waterings) do not match the full wet-soil set. The tooltip now queries `waterTileMap.HasTile(waterTileMap.WorldToCell(crop.transform.position))` directly — the water tilemap's own grid → cell mapping — which is the ground truth the game renders from. Hoed/dry/other states remain via the `farmingData` + `IsHoed` path.
- **Debug (v3):** `[HoverDebug] Tile probe …` now also logs the actual `IsWatered(tile)` / `IsHoed(tile)` / `IsHoedOrWatered(tile)` return values, `farmingData[tile]`, and every `TileManager` member whose name contains `water` (fields with counts / tile-membership inline, plus matching properties and method signatures), so the real water source can be pinned in one hover.
- **Log:** Replaced `HarmonyLib.AccessTools.Field/Property` member probes with silent `Type.GetField/GetProperty` in hover/forecast reflection helpers, so missing field names no longer flood the BepInEx log with HarmonyX Warnings.
- **Debug:** With `Debug.DebugLogging = true`, the first hovered crop logs its `Wish.Crop` public+private fields and properties one time (`[HoverDebug] ...`) so real member names (growth, quality, water, fertilizer) can be pinned down without dnSpy.
- **Build:** Project no longer references Sunhaven Todo or Birthday Reminder DLLs; those integrations call into optional mods via reflection, matching other slim mods (only game + BepInEx + TheVault.Abstractions for Vault types).
- Crop growth forecasting now reads reflected crop stage/timing, quality, and item sell data instead of placeholder values.
- **Fixed:** Harmony now patches real `Wish.Crop` entry points (`SetCropSprite`, `SetMeta`, `Water`, `Grow`) — the previous `UpdateGrowth`/`GrowCrop` targets do not exist in Sun Haven, so no crop data was ever recorded.
- **Fixed:** Projected sell value uses harvest item id from `Crop._cropItem` / nested item id (not `Decoration.id`); lookups prefer `ItemInfoDatabase` / `ItemSellInfo.sellPrice`, then `ItemDatabase`, with `ItemData.sellPrice` read as float where applicable.
- HUD hidden until a character save is active (main menu / pre-load no longer shows the panel).
- Draggable IMGUI HUD; position saved to `HUD.PositionX` / `HUD.PositionY`.
- HUD now shows only live-backed metrics (removed permanent `n/a` placeholder lines).
- Vault projected-value registration now hooks the Vault-loaded bridge event instead of running too early during plugin `Awake`.
- Crop forecast cache now clears on character load via a `GameSave.LoadCharacter` postfix patch.
- HUD runner now uses shared `PersistentRunnerBase` scene-survival behavior.
