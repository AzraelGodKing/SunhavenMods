# Changelog

Notes for **players and release readers**. Per-mod blurbs and upstream metadata are also in [`docs/versions.json`](docs/versions.json). Maintainer-only tooling and CI changes are in [`MAINTAINER_CHANGELOG.md`](MAINTAINER_CHANGELOG.md).

---

## Unreleased

**Senpai's Chest** — scan logs
- Per-item chest-scan traces no longer appear at Info; the scan-complete summary is unchanged.

**Maintenance releases: The Vault 4.3.1, Crop Optimizer 2.3.1, Haven's Birthright 3.1.4, Haven Dev Tools 2.1.3, Haven's Almanac 2.2.3, Museum Utility Tracker 3.1.3, A Squirrel's Birthday Reminder 2.1.4, Sun Haven Todo 2.1.3, Senpai's Chest 3.1.4, Gifting Assistant 1.1.4**
- **Cleanup:** Unused and unreachable code removed across these mods. Nothing you use changes, and saves are untouched.
- **Museum Utility Tracker:** The hidden F10/F11 debug keys are gone. F11 clashed with the Haven Dev Tools overlay.
- **A Squirrel's Birthday Reminder:** The duplicate Ctrl+Alt+B/A debug hotkeys are gone. Ctrl+Shift+B/A still work when `DebugMode` is on.
- **Crop Optimizer:** The startup health log no longer lists Todo and Birthday integrations that never ran.
- **Haven Dev Tools:** The startup log and the ToggleKey setting no longer say the debug window needs authorization.

**Sunhaven Todo** — unused API cleanup
- Removed unused `ShowUI` / `HideUI` / `IsCharacterDataLoaded`, `ClearData`, the string `AddTodo` overload, `GetTodosByPriority`, `SearchTodos`, and `TodoSaveSystem.Delete`. Suite mods already go through `TodoSoftClient` (`GetTodoManager` + `AddTodo(TodoItem)`).
**Haven's Mirror** — AZR-450
**Haven's Mirror (1.0.1 → 1.0.2)** — AZR-450
- **Fix:** Your portrait now stays on screen after you pick a dialogue option, instead of vanishing for the rest of the conversation. It still closes with the NPC's portrait when the conversation ends. Reported by AnnaBanana4411.

**The Vault (4.2.2 → 4.3.0)** — AZR-242, AZR-243
- **Save format:** Vault files are now saved as CSVAULT3. Each vault gets its own random salt and a fresh IV on every save, and an integrity check (HMAC-SHA256) rejects a corrupted or edited file instead of loading half of it. Older vault files still load and are rewritten in the new format right away.
- **Fix:** The save key no longer depends on whether Steam finished loading before the mod started, so a vault saved in one session is not unreadable in the next.

**Senpai's Chest 3.1.3, Haven's Birthright 3.1.3, Museum Utility Tracker 3.1.2, A Squirrel's Birthday Reminder 2.1.3, Sunhaven Todo 2.1.2, Haven Dev Tools 2.1.2, Haven's Almanac 2.2.2, Faster Races 2.1.2, Trinket Fortune 2.1.3, Crop Optimizer 2.2.3, Haven's Respec 2.1.4, Gifting Assistant 1.1.3, Haven's Mirror 1.0.1** — AZR-257
- **Update checks:** One download of the version list per game launch, shared by every installed mod, instead of one request per mod. The list is read with a real JSON parser, and an installed or published version that can't be compared now reports a failed check instead of "up to date". Checks point at the renamed `SunhavenMods` repository.
- **Haven's Almanac:** A relationship value that can't be read is now logged once instead of silently showing 0.

**Senpai's Chest, A Squirrel's Birthday Reminder, Gifting Assistant, Crop Optimizer** — AZR-255
- **Sun Haven Todo integration:** All four mods now talk to Sun Haven Todo through one shared client. If a Todo update changes something they rely on, each mod logs one clear warning and turns its Todo tasks off instead of failing silently.
- **Senpai's Chest:** Museum donation tasks wait until your character's todo list has loaded. Before, a chest scan that ran first could mark an item as tracked without creating its task.

**Website / feedback** — AZR-251, AZR-252
- **Fix:** The Mod Hub feedback page posted to a server that no longer exists. It now links to each mod's report form on [azraelsmods.com](https://www.azraelsmods.com/sun-haven/).

**Crop Optimizer (2.2.3 → 2.3.0)** — AZR-440
- **New:** Crops that can be mana infused but aren't yet get purple corner brackets, like the dry (yellow) and unfertilized (green) ones. They show at any growth stage once you've unlocked Mana Infusion, and don't need a tool held. Turn off with `HighlightUninfusedManaCrops` under `[Highlights]`. Requested by Achenar459.

**The Vault (4.2.1 → 4.2.2)** — AZR-438
- **Fix:** Clicks on the vault window no longer pass through to the inventory. Item slots under the window no longer pick up or throw items.

**A Squirrel's Birthday Reminder (2.1.1 → 2.1.2)** — AZR-437
- **Fix:** Scene cleanup no longer unpatches this mod, so it keeps running when Gifting Assistant is installed. Shared day-start hooks append to the existing listener list instead of replacing it. A failed player-init patch no longer skips gift tracking.

**Gifting Assistant (1.1.1 → 1.1.2)** — AZR-437
- **Fix:** Player-load and gift patches are isolated so an exception in this mod cannot skip Birthday Reminder's hooks on the same game methods.

## 2026-10-10

**Haven's Respec (2.1.2 → 2.1.3)** — AZR-356
- **Fix:** Respec now rolls back Max Mana from refunded skill nodes (e.g. Mental Focus, Town Spirit). Deactivate passes the live node rank into `SetActive`, and leftover permanent mana / character progress is cleared (Undo restores it).

**Trinket Fortune (2.1.1 → 2.1.2)** — AZR-357
- **Fix:** Museum-item bias patching is more resilient to game updates — discovers alternate `RandomItem` names, tries postfix if prefix fails, and falls back to FishingRod call-site patches when `Utilities.RandomItem` cannot be hooked.

**Senpai's Chest (3.1.1 → 3.1.2)** — AZR-358
- **Fix:** Config window layout heights scale with UIScale so 1.5×+ no longer overflows a fixed window.
- **Fix:** Chest name labels use constant pixel canvas positioning (no ultrawide sideways drift).
- **QoL:** `ScaleWithResolution` (default on), `AutoHighResLayout` for 1440p/ultrawide, UIScale range up to 3.0.

**The Vault (4.2.0 → 4.2.1)** — AZR-359
- **Fix:** WindowScale is multiplied by `Screen.height/1080` when `ScaleWithResolution` is on so 1440p/ultrawide stays readable.
- **QoL:** `AutoHighResLayout` bumps default scale on high-res / ultrawide once; settings slider shows raw→effective scale.

**The Vault** — dead-code cleanup

- Deleted `DoorPatches.cs` (never wired to Harmony).
- Deleted `ShopPatches.cs` and its `PatchShopBuyItem` registration (`_vaultPurchaseRequirements` was never populated; all hooks were no-ops).
- Removed `GameSavePatches.OnGameSaveLoad` / `OnSetCurrentCharacter` (never registered or called).
- Removed `PlayerPatches.ResetVaultLoaded` / `ForceVaultReload` (zero callers; aliases for `ResetState`).
- Removed `ItemPatches.OnItemPickedUp` / `OnItemPickedUpSingle` / `OnInventoryAddItemWithNotify` (never registered as Harmony targets).
- Removed `VaultSaveSystem.ExportVault` / `ImportVault` / `DeleteSave` / `GetAllSavedPlayers` (zero callers).
- Removed `VaultManager.GetCurrencyByGameItemId` / `GetAllCurrencies` (zero callers).
- Removed `CurrencyDefinition.Description` / `IconPath` (never read; not serialized).
- Removed `SaveLoadPatches.OnGameLoaded` and its `GameSave.LoadGame` patch registration (only logged).
- Deleted `README_BREAKING_CALLOUT.in.md` (unreferenced template).
- Cleaned stale commented-out code and duplicate summary in touched files.

---

## 2026-09-27

**The Vault (4.2.0)** — Steam Deck V1 / AZR-348 (**4.1.3 → 4.2.0**)

- **Touch-sized controls:** Row toggle and quick withdraw buttons enforce a ~44px minimum; wider window / taller rows so they fit.
- **Amount chips:** 1 / 5 / 10 / Max under the qty field for trackpad-friendly withdraw/deposit.
- **Open:** **F8** (AltToggle) or controller **LB+Y** (Xbox layout; `[UI] ControllerOpenEnabled` / Modifier / Button).
- **Close:** Escape plus game cancel (`UICancel` / Cancel / Close) when the vault is open.
- **Auto Deck layout:** Once on Deck-like resolutions, WindowScale and HUD Scale bump to ≥1.35 unless already customized (`AutoDeckLayout` / `DeckLayoutApplied` in Display).
- **Docs (GitHub Pages / `docs/` hub):** Steam Deck Ready copy — F8 or LB+Y open, touch UI, chips, B/cancel close; not full D-pad nav. Hub announce, Vault card chips, comparison Controller Support (`Deck`), Vault Parameters (`AutoDeckLayout` / `DeckLayoutApplied`), and search-index keywords.

**Haven's Mirror (1.0.0)** — new suite mod

- Drop PNG bust portraits into `BepInEx/plugins/HavensMirror/gallery/<CharacterName>/` (or `gallery/_shared/`).
- File names: `spring.png`, `summer.png`, `autumn.png`, `winter.png`, `vows.png`, `shore.png`, `costume.png`.
- Your custom bust appears in dialogue when portraits are present.
- Optional `ForcedLook` overrides NPC dialogue busts and seasonal outfits.
- Reload portraits in-game with **Ctrl+F8** (configurable).
- Independent AzraelGodKing implementation inspired by the idea of Nexus Sun Haven #329 (Self Portrait) — not a port of that mod.
- **HOWTO.txt** (written to `gallery/_shared/` on first launch) rewritten in plain steps: what the mod does, where images go, exact file names, and how to reload.
- **Save-slot folders:** when the game runs, scans `GameSave` character saves and creates empty `gallery/<CharacterName>/` folders automatically. Empty folders are ignored until you drop PNGs; `_shared/` remains the fallback.
- **Fixed (AZR-346):** Survive early BepInEx `OnDestroy` via `PersistentRunner`; Harmony patches and gallery sync stay alive so dialogue busts keep working.
- **Release prep:** Suite release pipeline + Cloudflare site page ready; Nexus listing [mod 538](https://www.nexusmods.com/sunhaven/mods/538) (`nexus_file_group_id` **8035296**) and Thunderstore [`HavensMirror`](https://thunderstore.io/c/sun-haven/p/AzraelGodKing/HavensMirror/) wired in `docs/versions.json` for CI uploads and download-nav.
- **Fix (AZR-346):** Thunderstore package icon resized to the required **256×256** PNG (was 512×512).
- **Docs:** Haven's Mirror hub page now uses the gold oval portrait theme, seasonal file cards, and a gallery folder map instead of a plain shared-styles shell.

---

## 2026-09-25

**The Vault (4.1.3)**

- **Legacy decrypt (AZR-240):** Recovery no longer feeds the plaintext `CSVAULT2` header into AES. Shared `VaultCryptography` skips the header and tries legacy keys, so key-mismatch recovery can succeed again.
- **Failed load (AZR-238):** If every on-disk candidate fails, the empty in-memory vault **cannot autosave** until you click **Start Fresh** in the Vault UI — so a surviving `.backup` is not overwritten.

**The Vault (4.1.2)**

- **Fix (AZR-234):** Category tab buttons keep owned backgrounds on focus so switching tabs cannot replace the window chrome with a zoomed skin-atlas overlay.

**Website / feedback desk**

- **Fix (AZR-103):** Feedback intake now targets Sunhaven Mods + Todo via `LINEAR_PROJECT_ID` / `LINEAR_STATE_ID` (wrangler defaults), with clearer Linear auth vs ID error messages. Still requires a valid `LINEAR_API_TOKEN` secret on the Pages project.

---

## 2026-09-22

**Sun Haven 3.1.3b**

- **Trinket Fortune:** Fishing museum-item bias applies again. HarmonyX could not patch `Utilities.RandomItem<int>` after the 3.1.3b launch (generic `ref T __result` failed IL compile), so the plugin aborted during load.

---

## 2026-09-21

**Suite reliability (ticket batch)**

- **Saves:** Atomic save helper no longer deletes the only copy of your data if a write fails mid-rotate; corrupt/truncated JSON is rejected so backups can load.
- **The Vault:** Tab switching no longer breaks the IMGUI window background (owned textures on all focus states; no shared whiteTexture / skin.window atlas).
- **S.M.U.T.:** Removed a forked icon cache that could leak textures; uses the shared IconCache.
- **Crop Optimizer:** Reflection failures log once per call-site instead of failing silently after a game update.

---

## 2026-08-30

**Docs hub**

- **New:** [Feedback](https://www.azraelsmods.com/sun-haven/) ticket desk on the site — file a bug or feature request without leaving the docs. The floating Report Bug button on every guide now opens that page.

**Crop Optimizer (2.2.2)**

- **Fix:** Nel'Vari and Withergate HUD totals now use orb and ticket shop prices. Those farms store harvest value on `orbSellPrice` / `ticketSellPrice` with gold at 0, so the overlay previously showed crop count (and hover names) with a blank overall price.

---

## 2026-08-10

**Haven's Birthright (3.1.2)**

- **Fix:** Infernal Forge `Player.AddMana` Harmony prefix now uses parameter names `mana` / `overCapAmount` so HarmonyX can patch after the game signature change (was looking for `amount` and failing the patch).

---

## 2026-07-28

**Suite patch release (all mods)**

- Patch bumps across the suite for the bug-risk audit fixes. Per-mod notes in `docs/versions.json`.

**Suite bug-risk audit fixes**

- **Haven's Respec:** Profession reset now fails closed — any mid-walk exception rolls back from the pre-reset snapshot instead of charging gold for a half-wiped tree.
- **The Vault:** `HasEnough` now treats bag + vault as a combined balance; auto-deposit/deposit paths roll inventory back if vault credit fails; menu reset clears auto-deposit guard flags.
- **Senpai's Chest:** Smart-chest transfers only remove what `AddItem` actually accepted (no item loss on failed moves).
- **Haven's Birthright:** Removed double-stacked racial Defense on `ReceiveDamage` (Defense already comes from `GetStat`); guarded Tidal Blessing / Infernal Forge / Divine Ward against zero max HP/mana; safer Tidal Blessing logging.
- **Shared overnight hooks:** `OvernightHookUtility.TryUnhookOvernightEvent` removes DayCycle and UIHandler listeners before re-hook (Almanac, Birthday Reminder, Todo, Gifting Assistant) to stop duplicate morning briefings/resets.
- **Birthday Reminder:** Character-switch `TodoIntegration.Reset` removes tracked birthday todos so they are not orphaned/duplicated.
- **Sun Haven Todo:** Character-name lookup no longer falls back to the previous character's name on failure.
- **S.M.U.T.:** Wishlist badges destroy and leave the host dictionaries on disable; dead Unity keys are pruned on refresh.
- **Trinket Fortune:** Fish bias respects drop-level gates when the loot type exposes them.

**Sun Haven Todo**

- **Fix:** Todo HUD caches reusable `GUIStyle` instances instead of allocating them every `OnGUI` frame.
- **Fix:** Menu save-on-exit detection includes the `Menu` scene alongside `MainMenu` and `Bootstrap`.
- **Fix:** Exposed `IsCharacterDataLoaded` for cross-mod integrations; documented weekly recurring reset heuristic (`DayCycle.MonthDay` has no `Week` property).

**Gifting Assistant**

- **Fix:** `HasGivenGiftToday` refreshes the gifted-today cache once when stale instead of returning false.
- **Fix:** Todo integration skips redundant `LoadDataForCharacter` when Sun Haven Todo's manager (or `IsCharacterDataLoaded` flag) already has data for the active character.

**S.M.U.T. (Sun Haven Museum Utility Tracker)**

- **Fix:** Icon cache always extracts sprite regions via `ExtractSpriteTexture` instead of storing shared atlas textures.

**A Squirrel's Birthday Reminder**

- **Fix:** Gift-tracking patch failure no longer calls `UnpatchSelf()` and removes player-init hooks.
- **Fix:** `ShowGiftHints` config now gates HUD gift-hint labels.
- **Fix:** Character name resolution uses `characterName` consistently with player-init patches.
- **Fix:** Todo integration keys NPCs with normalized names so fuzzy/composite names complete correctly.
- **Fix:** Date staleness checks skip when no save is loaded (avoids spurious main-menu refreshes).
- **Fix:** Gift tracking uses case-insensitive NPC name matching.

**Faster Races**

- **Fix:** `IsSpeedBonusActive` now respects per-race overrides and current SubRace so Haven's Birthright does not double-apply movement speed when global bonus is 0.
- **Fix:** SubRace names are normalized to per-race config keys (e.g. alternate elemental spellings).

**The Vault**

- **Fix:** `GetVaultAmount` now reads registered `custom_` currencies for inventory merge paths.
- **Fix:** Shop postfix deducts vault currency only when the purchase prefix approved it.
- **Fix:** `EnsureUIComponentsExist` recreates `VaultHUD` when destroyed independently of `VaultUI`.
- **Fix:** Reduced encryption and secret-gift hash log verbosity.

**Senpai's Chest**

- **Fix:** Player init no longer reloads save data when the same character is already active this session (matches scene-load guard); museum todo integration resets only on character change.
- **Fix:** Removed periodic `FindObjectsOfType<Chest>` label scan — labels are ensured via existing `OnEnable` / `SetMeta` patches.
- **Fix:** Chest label interaction text is appended on a new line instead of replacing the game's default prompt.

**Trinket Fortune**

- **Fix:** `PickBiasedFishingMuseumItem` builds the unowned list with a pre-sized loop instead of LINQ.
- **Fix:** Aquarium progress prefers S.M.U.T. donation save stats when available, falling back to game save progress.

**Crop Optimizer**

- **Fix:** After character load clears the forecast, existing crops are rescanned once so the HUD is populated immediately.
- **Fix:** F3 HUD toggle defers while typing in chat, console, or UI text fields (`TextInputFocusGuard`).
- **Chore:** Removed unused todo/birthday integration field construction from the plugin entry point.

**Haven's Almanac**

- **Fix:** Daily briefing shows only after overnight/sleep (not on every player init).
- **Fix:** Birthday provider copies `TodaysBirthdays` into its own list instead of holding a live reference.
- **Fix:** Vault provider `HasBriefingContent` matches `DrawBriefingSection` when stored currencies exist.

**Haven Dev Tools**

- **Fix:** `SpawnItem` returns false when `AddItem` invoke fails or reports failure.
- **Fix:** Log viewer caches a reusable `GUIStyle` for log lines instead of allocating per entry.

**Haven's Birthright**

- **Fix:** Infernal Forge no longer blocks explicit mana restores (food/consumables) while active — only passive regen ticks (`AddMana` amount 0) are suppressed.
- **Fix:** Amari Dog Loyalty Aura now counts max-friendship NPCs and applies bonuses to Defense, melee damage, magic damage, and max HP only.
- **Fix:** `StatFrameCache` marks the cache invalid on update errors instead of falsely reporting valid data.

**Haven's Respec**

- **Fix:** Reset/undo hotkeys defer while typing in chat, console, or UI text fields (`TextInputFocusGuard`).
- **Fix:** Persistent runner recreation reattaches its component when the GameObject survives but the MonoBehaviour was destroyed.

---

## 2026-07-11

**Suite release (minor bumps)**

- Versioned suite mods for this branch: Dev Tools / Respec / SMUT / Almanac / Vault / Todo / Birthday / Birthright / Senpai's Chest / Faster Races / Trinket Fortune / Crop Optimizer / Gifting Assistant. See `docs/versions.json` changelog lines per mod.

**S.M.U.T. (Sun Haven Museum Utility Tracker)**

- **Fix (perf):** Museum wishlist overlay no longer tanks FPS after Sun Haven 3.1.2. LateUpdate work is limited to real shop slots / gift inventory, museum membership uses a HashSet + cached badge results, and badges only update when the item/visibility changes.

**Suite mods (localization)**

- **Fix (pt-BR):** Re-applied the native-speaker Portuguese (Brazil) review with correct UTF-8 encoding (previous import had mojibake such as `AniversÃ¡rios`). 389 strings across Birthday Reminder, Crop Optimizer, Haven Dev Tools, Haven's Almanac, Haven's Respec, S.M.U.T., Senpai's Chest, Sun Haven Todo, and The Vault.

---

## 2026-07-05
## 2026-07-07

**Suite mods (localization)**

- **Portuguese (Brazil):** Updated 389 `pt-BR` strings across Birthday Reminder, Crop Optimizer, Haven Dev Tools, Haven's Almanac, Haven's Respec, S.M.U.T., Senpai's Chest, Sun Haven Todo, and The Vault from a native-speaker review.

---

## 2026-07-04

**Haven Dev Tools**

- **Steam ID lock removed:** F11 debug window, F6 overlay, and FPS counter are available to all users without Steam ID authorization. Removed the authorization hash generator from the Utility tab.
- **Mod Health dashboard:** New top-level **Health** tab (F11) aggregates suite mod diagnostics, shared-code revision skew warnings, version checks, and per-mod detail dumps. Tab bar is now **Health | Tools | Suite | Extensions** (Azrael's Mods renamed **Suite**). Version checker moved from Tools → Utility into Health.
- **FPS counter:** Compact corner HUD showing smoothed FPS (color-coded green/yellow/red). Enabled by default via `[Overlay] ShowFpsCounter`; position via `FpsCounterPosition` (TopLeft, TopRight, BottomLeft, BottomRight). F11 → Tools → Perf tab adds toggles for the corner counter and F6 overlay stats, plus a 3-second min/max FPS range.
- **Fix:** Tools → Currencies no longer runs full reflection and inventory scans every IMGUI frame; balances refresh twice per second (Refresh button for immediate update).
- **Suite integration:** All suite mods report startup health to the **Health** tab. **Suite** adds panels for Crop Optimizer, Faster Races, Haven's Respec, and Gifting Assistant when installed.
- **Respec simulator:** Suite → Haven's Respec panel dry-runs a profession reset, shows projected refund/cost, and lets you **Apply reset** or **Revert preview** without charging until confirmed. Requires Haven's Respec installed and the Skills panel opened at least once in the current session.
- **Fix:** Suite tab re-detects installed mods after the full BepInEx chain loads so Haven's Respec (and other late-loading suite mods) show up reliably.
- **Fix:** Respec simulator panel reads `IsReady` / `ProfessionCount` as API properties (not methods) so the UI no longer stuck on "Open the Skills panel".
- **Fix:** Suite tab no longer resolves the wrong mod `Plugin` type when calling GetManager/GetTodoManager (namespace-qualified reflection).
- **Fix:** Trinket Fortune Suite panel uses built-in status summary (`GetDevToolsSummary`) instead of a missing optional DevTools panel class.

- **Mod Health bridge:** When Haven Dev Tools is installed, the dashboard shows a short Mod Health summary and points to Dev Tools → Health for full diagnostics. No Mod Health section when Dev Tools is absent.

**Sun Haven Todo** / **The Vault**

- **Support diagnostics:** Startup `[Health]` log line with version, shared-code revision, and detected companion mods (feeds Dev Tools → **Health** tab).

**Senpai's Chest** / **Birthday Reminder** / **S.M.U.T.** / **Haven's Birthright** / **Crop Optimizer** / **Faster Races** / **Haven's Respec** / **Gifting Assistant** / **Trinket Fortune**

- **Support diagnostics:** Same startup `[Health]` line and Dev Tools Suite tab integration when Haven Dev Tools is installed.

**S.M.U.T. (Sun Haven Museum Utility Tracker)**

- **Museum wishlist overlay:** Shop items and gift-inventory slots show a small orange "needed" badge when the item is still undonated for the museum. Toggle under `[WishlistOverlay]` in `SunHavenMuseumUtilityTracker.cfg`.
- **Museum wishlist badge:** Shop/gift overlay now shows an **M** (museum / S.M.U.T. marker) with gold-trim styling instead of the first letter of "Needed".
- **Museum wishlist tooltip:** Hovering a marked shop or gift item appends “Needed for the museum” to the vanilla tooltip. Badge size reduced.

**Haven's Respec**

- **DevTools API:** Exposes `RespecDevToolsApi` for dry-run profession reset preview (simulate, apply, revert). In-game Simulate button removed until Respec ships its own UI.

**Crop Optimizer**

- **Fix:** Hover tooltip no longer scans every crop in the save every frame when you are off the farm (town, sheds, etc.). Crop lists are scoped to the active scene, empty scenes are cached longer, and stable mouse positions reuse a "no crop under cursor" result instead of running a full scan.

---

## 2026-07-03

**Haven's Almanac**

- **Relationships dashboard:** Built-in expandable section (Ctrl+F5 dashboard) listing every NPC's friendship hearts from the game's save data, with vanilla-style heart sprites (gold filled hearts, silver milestone slot for romanceable NPCs), dating/marriage badges, and ungifted-today markers. Morning briefing highlights NPCs you have not gifted yet. Hearts align on the same row as each NPC name. Fully localized for all supported languages. Configure under `[Relationships]` in `HavensAlmanac.cfg`.

**Gifting Assistant**

- **Relationship hearts:** Roster rows and the Add NPC picker show friendship level inline on one row with the same heart sprites as Haven's Almanac instead of Unicode heart text. Hearts are vertically centered with the NPC name.

---

## 2026-07-02

**Senpai's Chest**

- **Fix:** Smart-chest rules are no longer deleted when traveling between areas. Rules are not removed on `Chest.OnDisable` (Sun Haven fires that during every area change). Removal is only via the **Remove Smart Chest** UI button. `InitializeAsOwner` no longer reloads from disk on every map load, and save refuses to overwrite a non-empty file with empty in-memory state after a successful load unless you cleared all rules yourself.
- **Fix:** Smart-chest config no longer calls `PlayerInput.DisableInput` while open, so CheatEnabler / Quantum Console (`~`) and in-game chat work with the config window up. Backspace/Cancel is still swallowed only while typing in Senpai's Chest search fields (`[UI] BlockInputWhenTypingInConfig`, default true).

**Haven Dev Tools**

- **NPC Relationships tab (F11 → Tools):** Edit NPC heart points, dating/marriage flags, romance cycles, and primary spouse in the dev window. Use **CheatEnabler** (or vanilla dev cheats) for Quantum Console commands such as `/setrelationship`.
- **Marriable tab (F11 → Tools):** Select multiple romanceable NPCs and marry them at once when **Ultra Polygamy** is loaded and its `MarryPlayer` patch is active; blocked otherwise (single-marriage remains on the Relationships tab).
- **UI:** F11 debug window is resizable (drag bottom-right corner); size persists in `[DebugWindow] Width` / `Height`. Header bar with close button; list panels scale with window size.
- **Fix:** F11 debug window no longer pauses the game by default (`[DebugWindow] PauseGameWhenDebugOpen = false`). Console tab accepts Enter and focuses the input field when opened.

**Crop Optimizer**

- **Field highlights:** Uses bundled tile-selection art from `Assets/tile_selection_sheet.png` (same look as vanilla tool hover; green corner brackets for fertilizer). Falls back to cloning `Tool._selection` if the sheet is missing. Toggle in `CropOptimizer.cfg` under `[Highlights]`.
- **Fix:** Field highlights now detect the equipped watering can / fertilizer via Sun Haven 3.1 `Player.UseItem` (the old inventory slot probe never matched).
- **Fix:** Field highlight sprites now use the correct 36×36 regions from `tile_selection_sheet.png` (not equal 27px slices); corner frames use 9-slice borders matching vanilla `Tool._selection`.
- **Fix:** Crop scene cache no longer wipes the forecast when `Wish.Crop` is unavailable or backfills forecast rows with placeholder values; highlight prototype resolution retries until assets or a live tool selection are available; field highlights are torn down and recreated on HUD canvas rebuild; farm tile center fallback uses grid cell Z half-size instead of preserving stale Z; bundled sheet load failures release the temporary texture.
- **Fix:** HUD crop count and projected sell value now backfill from live scene crops after load (Harmony growth hooks alone missed already-planted crops); empty scene scans no longer clear the forecast when crop objects exist but fail a transient presence check.

---

## 2026-06-29

**The Vault**

- **Fix:** Vault load now restores from `.backup` when the primary save is missing or unreadable (e.g. after a crash during save), matching the atomic-save policy used by other mods.

**S.M.U.T. (Sun Haven Museum Utility Tracker)**

- **Fix:** Donation save load now checks `.bak` even when the primary file is missing, preventing silent data loss after an interrupted save.

**Haven's Birthright**

- **Fix:** Shop racial discount restores the original listing price after each purchase so repeated buys from the same shop row cannot stack discounts.

**Haven Dev Tools**

- **Fix:** Mod update checker reads each mod's installed version from BepInEx at runtime instead of a hardcoded table, so update reports stay accurate between releases.

**Haven's Respec**

- **Fix:** **RESET ALL** now sits in the left sidebar directly under **Reset** (same styled button stack as Undo), instead of overlapping the skill tier headers. Toggle with `UI.EnableResetAll` (default on).

**Maintainer tooling**

- **Scripts:** Reorganized under `scripts/` into `matrix/`, `stats/`, `version/`, `build/`, `localization/`, and `archive/` with shared helpers. CI and docs paths updated; see [`scripts/README.md`](scripts/README.md) and [`MAINTAINER_CHANGELOG.md`](MAINTAINER_CHANGELOG.md).

---

## 2026-06-28

**Current versions** ([`docs/versions.json`](docs/versions.json)): The Vault **4.0.1**, Haven's Birthright / Senpai's Chest / S.M.U.T. **3.0.1**, Sun Haven Todo / Birthday Reminder / Haven's Almanac / Haven Dev Tools / Faster Races / Trinket Fortune / Crop Optimizer / Haven's Respec **2.0.1**, Gifting Assistant **1.0.0**.

**Haven's Respec**

- **Fix:** Reset buttons no longer disappear after main menu → game transitions. Harmony patches and controller hooks now survive plugin `OnDestroy` during scene loads (matching The Vault / Sun Haven Todo). Added a `PersistentRunner` for hotkeys and `EnsureModReady()` on scene load to re-wire hooks if the BepInEx plugin MonoBehaviour is recreated.

**Docs (GitHub Pages)**

- **Fix blank white pages:** Restored mod and index HTML after a batch-edit corruption emptied several files. Removed the `mod-subpage.css` white-wash layer that stacked opaque white cards over per-mod themes. Gifting Assistant docs page restored.
- **Gifting Assistant hub listing:** Re-added Gifting Assistant to the notice board (13 contracts), Diplomat's Kit, Guild Master's Arsenal, FAQ synergy bullets, and Arsenal Comparison column. Announcement banner points to the custom gift-wrap docs page. Cross-links on Todo, Birthday Reminder, and Haven's Almanac mod pages.
- **Almanac integration docs:** Rewrote &ldquo;Integration with Haven&rsquo;s Almanac&rdquo; sections on every mod page (HUD / dashboard / briefing surfaces per data provider). Added Gifting Assistant to the Almanac supported-mod list; noted Respec alongside Faster Races and Trinket Fortune as not yet integrated.
- **versions.json:** Added **Gifting Assistant** **1.0.0** release metadata (`com.azraelgodking.giftingassistant`); Nexus [`/mods/507`](https://www.nexusmods.com/sunhaven/mods/507) (`nexus_file_group_id` **7597922**) and Thunderstore [`GiftingAssistant`](https://thunderstore.io/c/sun-haven/p/AzraelGodKing/GiftingAssistant/) URLs.
- **Release workflows:** Added `giftingassistant` to the **Release & Publish** and **Test — Self-hosted Sunhaven runner** mod picker (single-mod releases).
- **Mod README versions:** Every mod `README.md` now states the published version from `docs/versions.json` (fixed stale **The Vault** `3.3.2` and **S.M.U.T.** `2.2.7` headers). Added root `README.md` for Haven Dev Tools and Trinket Fortune.

**Gifting Assistant**

- **Thunderstore publish:** Shortened `docs/versions.json` description to 236 characters (Thunderstore manifest limit is 256; the previous text was 257).
- **PR #74 review fixes:** ReminderMode legacy migration (`UseTodoIntegration` false → RosterOnly); autosave uses `Saving > AutoSaveInterval` from config (60s default); sort guard uses try/finally; shared `GameSaveCharacterName` helper; Almanac dashboard drops unreachable gifted branch; `GiftSuggestionResolver` formatting; pt-BR **Normal** priority translation.
- **Gifted toggle:** The row **Gifted** checkbox no longer adds or updates Sun Haven Todo tasks — only **+Todo** pushes reminders. In-game gifts still auto-complete matching todos when you give the gift in the world.
- **Hotkey toggle:** **Ctrl + G** (or configured toggle) now closes the gifting window when it is already open; previously the hotkey only opened it and Escape was required to dismiss. Added full `Localization/strings.json` coverage for all 16 supported game languages (da, de, es, fr, it, ja, ko, nl, pt, pt-BR, ru, sv, zh-CN, zh-TW, uk). Shared terminology aligned with Sun Haven Todo (Cancel, priority labels), Birthday Reminder (Gifts), and repo-wide UI strings where keys overlap.
- **Gift icon display split:** Roster rows show **all** selected preferred gift icons (capped at four with a +N overflow). Sun Haven Todo tasks pick **one random** icon from preferred gifts (or random loved/liked when none are set); the pick refreshes on **+Todo**, a new in-game day, or when the window reopens. Todo descriptions still list all suggested gift names.
- **+Todo replace:** Clicking **+Todo** on a roster row now removes any existing Gifting Assistant gift task for that NPC (active or completed) and adds a fresh one with current preferred gifts and icon — no need to delete the old todo manually first.
- **Performance:** Cached NPC reflection (`TryGetValue`, name accessors) at init; gifted-today flags now refresh on window open, day change, and in-game gifts instead of per OnGUI frame — roster stats, rows, and sort use the cache.
- **Performance (click freeze fix):** Major UI/Todo overhaul so button clicks no longer stall the game. **+Todo**, gifted toggle, roster add/remove, and **Gifts** now only flip flags or enqueue work; heavy paths (full `NPCManager` scan, todo reflection, `GetAllTodos` scan, disk save, gift-name resolution) run on the next `Update` tick via batched `TodoIntegration.ProcessPending` and deferred `GiftGameData` cache warm. Todo bridge caches context + todo IDs (Birthday Reminder pattern) for O(1) complete/remove instead of scanning every task per click. Gift selector and NPC picker build lists off the IMGUI thread and draw rows/icons incrementally with cached item names.
- **Todo integration fix (scene transitions):** Fixed gift todos never being pushed/completed with repeated `Todo integration: plugin instance not found` warnings while Sun Haven Todo was installed and loaded. Sun Haven Todo's BepInEx plugin MonoBehaviour is destroyed on scene transitions, so `PluginInfo.Instance` compared equal to null via Unity's overloaded operator and the integration bailed even though Sun Haven Todo's static `TodoManager` was still alive. The bridge now resolves the `SunhavenTodo.Plugin` type directly (instance-independent) and calls the static `GetTodoManager()`/`SaveData()` without the live instance — mirroring A Squirrel's Birthday Reminder's robust integration. The live instance is only used for the rare `LoadDataForCharacter` fallback, which is skipped when the manager already reports data loaded. Failure warnings are now logged once per cause instead of on every attempt.
- **Todo integration on by default:** `Integrations > ReminderMode` now defaults to **PushToTodo** (was RosterOnly) so a fresh install shows the per-row **+Todo** button and pushes gift tasks automatically when Sun Haven Todo is installed; it still falls back to roster-only when Sun Haven Todo is not installed. Legacy `UseTodoIntegration` migration is unchanged.
- **Todo button fix:** Fixed the per-row **+Todo** button not appearing when the integration was active. The button is gated on `CanPushTodos` (enabled + Sun Haven Todo loaded); with the opt-in default previously off it never showed on fresh installs. Also hardened the Sun Haven Todo availability check against soft-dependency load order (re-checks while not yet loaded, caches once detected) so the button reliably appears without re-probing every frame.
- **Todo auto-complete sync:** Gifting an NPC in-game now marks the matching Sun Haven Todo task complete (and un-completes if the game flag is cleared). The window **Gifted** toggle updates the roster only — it does not add or complete todos.
- **Todo daily refresh:** When `Integrations > ReminderMode = PushToTodo`, every new in-game day removes the previous gift tasks and re-adds a fresh one per rostered NPC (guarded so it runs once per day). Tasks are also ensured on character load so they appear without waiting for a day rollover.
- **Todo gift icons:** Gift tasks now carry the NPC's primary suggested gift as the task icon (`TodoItem.IconItemId`, via the established cross-mod contract) and list the preferred gift name(s) in the task description.
- **Per-NPC preferred gifts:** Each roster row has a **Gifts** button that opens a selector of the NPC's loved/liked gifts with checkboxes; selected gifts are persisted per character. When set, the row and the Todo task show only the chosen gifts (instead of the full loved/liked lists) to cut clutter, falling back to loved-then-liked when nothing is selected.
- **Gift selector UX:** Removed the pinned **Selected (N)** summary strip from the preferred-gift picker; selection is shown only by row checkmarks (and a subtle gold row highlight). Fixed garbled row layout where localization keys could leak beside item names — checkmarks now render inside a fixed-width checkbox column so icon and name no longer overlap.
- **Almanac integration:** When Haven's Almanac is installed and `Integrations > UseAlmanacIntegration` is enabled (default on), shares gift roster progress with the Almanac HUD (`pending / roster` summary), dashboard (remaining NPCs with priority), and daily briefing (pending count plus high/urgent breakdown).
- **Config:** `Integrations > UseAlmanacIntegration` now defaults to **true** (was false); set to false in `GiftingAssistant.cfg` to keep roster data private.
- **Todo integration:** Fixed **+Todo** not creating entries in Sun Haven Todo — ensures Sun Haven Todo character data is loaded before add, uses the string `AddTodo(title, description, priority, category)` overload (same reflection path as Crop Optimizer), verifies the todo appears in the active list, and logs warnings when add/save fails instead of reporting success on a silent no-op.
- **Todo integration:** **+Todo** is hidden unless Sun Haven Todo is installed *and* `Integrations > UseTodoIntegration` is enabled (default off — own gift roster/priority when off).
- **Tracking mode UX:** Replaced the opaque boolean `UseTodoIntegration` with `Integrations > ReminderMode` (**RosterOnly** / **PushToTodo**). The gifting window shows an active tracking line ("Daily gift roster" vs "Daily roster + Sun Haven Todo"), stats read "On roster" instead of "Tracked", and PushToTodo without Sun Haven Todo installed shows a clear fallback hint. Legacy `UseTodoIntegration = true` migrates automatically.
- **NPC picker:** Fixed duplicate composite names (e.g. `Anne+Anne`) in the add-NPC list; names are normalized the same way as Birthday Reminder, and gift-status lookup resolves roster names against game dictionary keys.
- **Relationship hearts:** Roster rows show each NPC's friendship level as a heart bar (from `CharacterData.Relationships`, 5 points per heart — same tiers as the in-game Relationship HUD). Values refresh when the window opens, not every frame; unknown NPCs show `?`. The Add NPC picker shows the same heart bar beside each candidate name (cache refreshes when the picker opens).

**The Vault**

- **Fix:** Vault UI and HUD icons display again — removed duplicate `TheVault/UI/IconCache.cs`; C# namespace shadowing had `VaultUI`/`VaultHUD` read the legacy cache while `Plugin`/`PlayerPatches` loaded and registered the shared `SunhavenMods.Shared.IconCache`.
- **Performance:** Player icon load now routes through shared `SunhavenMods.Shared.IconCache` only (removed duplicate legacy load path). Vault HUD rebuilds row layout only when currency totals or HUD scale/density change (integer hash fingerprint instead of per-refresh string join). Character-sync fallback throttled to ~0.75s; repeated `CurrentCharacter` fallback warning logs once per session.

**Senpai's Chest**

- **Performance:** Routine `[Scan]` log lines (start, per-chest scan, no-op complete) downgraded to LogDebug; item moves and errors remain LogInfo/LogError.

**Localization (UI mods)**

- Shared layer: `SharedUtilities/ModLocalization.cs`, `LanguageChangeWatcher.cs`, `LocalizationBootstrap.cs`; `Directory.Build.targets` adds `I2Localization.dll` for UI projects.
- **`[Localization] ForceEnglish`** (default `false`) on every localized mod — keep mod UI in English and ignore Sun Haven's in-game language. Toggle live in BepInEx Configuration Manager.
- All nine UI mods ship `Localization/strings.json` (16 languages: en, da, de, es, fr, it, ja, ko, nl, pt, pt-BR, ru, sv, zh-CN, zh-TW, uk) as embedded resources; `ModLocalization.T()` in HUD/windows; live refresh when the game language changes (Haven's Respec refreshes TMP labels explicitly).
- PRs run `scripts/localization/validate-localization.ps1`. Optional per-language overrides: `BepInEx/config/<ModFolder>/lang/<code>.json`.
- Machine translation via MyMemory: run one mod and one language per invocation to avoid rate limits, e.g. `scripts/localization/fill-localization-languages.ps1 -Translate -ForceRetranslate -Mod HavensRespec -Language fr`. Full matrix: `scripts/localization/translate-all-localization.ps1 -ForceRetranslate` (long-running; 30s pause between passes by default). Progress audit: `scripts/localization/audit-untranslated.ps1`.
- **Localization fill:** Full MyMemory pass for all nine UI mods (`-ForceRetranslate`) to replace English placeholder copies with per-language strings; Senpai's Chest bullet glyphs normalized to UTF-8 `•`.
- **Haven's Almanac:** Dashboard provider sections (birthday, vault, todo, museum, chest, birthright, crop optimizer, dev tools, mod health) now use `ModLocalization` keys.
- **Crop Optimizer:** TMP HUD title and tooltip toggle labels refresh when the game language changes.
- **Crop Optimizer:** Fixed startup crash when The Vault is not installed (`TheVault.Abstractions` load failure); optional Vault integration now uses reflection like Todo/Birthday integrations.
- **Small-mod localization sweep:** Filled remaining English-copy gaps in Crop Optimizer, Senpai's Chest, The Vault, Sun Haven Todo, Haven's Respec, S.M.U.T., and Haven's Almanac; fixed Crop Optimizer Rich Text placeholders (`{0}`, `<color={n}>`) and Japanese `XPH`/`XRT` artifacts across those mods.
- **PR #72 review fixes:** Removed stray `HavenDevTools/manifest.json` and root translation scratch files; normalized broken `{0}` format tokens (Japanese `XPH`/`XRT`, French NBSP-in-specifier); split Birthday Reminder `birthday.gift.universal` into `universalLoved` / `universalLiked`.
- **Follow-up polish:** Haven Dev Tools debug window caches IMGUI toolbar/race label arrays (no per-frame allocations); Crop Optimizer hover tooltip and The Vault door patches use `ModLocalization` for remaining hardcoded English strings.
- **HUD localization fix:** Fixed Crop Optimizer and Sun Haven Todo HUDs showing raw localization keys (e.g. `crop.hud.title`, `todo.hud.title`) after plugin teardown. `ModLocalization.Shutdown()` no longer clears loaded string tables when persistent UI survives, and `LocalizationBootstrap` uses the mod assembly reliably when loading embedded `strings.json`.
- **The Vault:** Fixed `IconCache` type ambiguity in `PlayerPatches` (build blocker for Almanac dependency chain).

**Haven's Almanac**

- **Gifting Assistant integration:** When Gifting Assistant is installed and its `Integrations > UseAlmanacIntegration` is enabled, the Almanac HUD, dashboard, and daily briefing show gift roster progress (pending count, priority breakdown, remaining NPC list).

**Sun Haven Todo**

- **HUD:** The sticky task panel header includes a close (**X**) control that hides the HUD until you press the HUD toggle hotkey again (your config’s **HUDToggleKey**, often **Ctrl + H**). This does not change the **HUD → Enabled** setting in the config file.
- **HUD:** While the full Todo window is open, a **Sticky** button appears in its header when the sticky panel is hidden so you can bring it back without closing the list. The **HUD toggle hotkey** also works while the big window is open (the main **ToggleKey** for the full list still only applies when that window is closed—use **Escape** to close it).

---

## 2026-06-26

**Bugfix release:** Every mod received a **patch** semver bump so store metadata, DLLs, and [`docs/versions.json`](docs/versions.json) stay aligned. Current versions: The Vault **4.0.1**, Haven's Birthright **3.0.1**, Senpai's Chest **3.0.1**, S.M.U.T. **3.0.1**, Sun Haven Todo **2.0.1**, A Squirrel's Birthday Reminder **2.0.1**, Haven's Almanac **2.0.1**, Haven Dev Tools **2.0.1**, Faster Races **2.0.1**, Trinket Fortune **2.0.1**, Crop Optimizer **2.0.1**, Haven's Respec **2.0.1**.

- **Live language refresh:** Fixed a startup error (`Parameter "languageName" not found`) that prevented the shared `LanguageChangeWatcher` from patching the game's `SetLanguageAndCode`. The Harmony postfix parameter names now match the original method's casing, so the language-change hook registers and mod UIs re-localize correctly when you switch the in-game language.
- **Crop Optimizer:** Fixed stale crop instance IDs surviving across character/save loads. The crop instance registry is now cleared on character load, so reused Unity instance IDs from a previous save can no longer collide with unrelated new crops and report wrong growth state.

**Gifting Assistant** (new mod, **1.0.0**)

- **Daily gift routine:** New standalone mod with an in-game window (**Ctrl + G** by default) to build a per-character NPC gift roster, view loved/liked gift options with item icons, track who you've already gifted today, optionally show whether suggested gifts are in your bag, and sort by priority (Low / Normal / High / Urgent). Soft integrations with Birthday Reminder (birthday badge) and Sun Haven Todo (add reminder task).

---

## 2026-05-03

**Senpai's Chest**

- **Wildcard rules:** Wildcard/glob matching (`*` / `?`) now lives in **Manage Groups** by default (patterns stored with each group, then applied through **By Group** rules). Optional UI toggle `SeparateWildcardRuleInUI` exposes a standalone **By wildcard name** rule when explicitly enabled.

**Patch release:** Every mod in the repo received a **patch** semver bump so store metadata, DLLs, and [`docs/versions.json`](docs/versions.json) stay aligned after the cross-cutting review. Current versions: The Vault **3.3.2**, Haven's Birthright **2.2.2**, Senpai's Chest **2.6.2**, S.M.U.T. **2.4.2**, Sun Haven Todo **1.4.2**, A Squirrel's Birthday Reminder **1.4.2**, Haven's Almanac **1.4.2**, Haven Dev Tools **1.2.2**, Faster Races **1.4.2**, Trinket Fortune **1.2.2**, Crop Optimizer **1.4.2**, Haven's Respec **1.3.2**.

- **Maintainers:** Nexus “detailed description” BBCode sources were renamed per mod folder: [`docs/NexusMods-BBCode-Index.txt`](docs/NexusMods-BBCode-Index.txt) (`NexusMods-<ModFolder>-BBCode.txt`).

---

## 2026-05-02

Cross-cutting hardening and documentation (see also maintainer log).

**Shared code**

- **IconCache:** Per-entry texture ownership; eviction and clear only destroy textures the mod allocated — full-atlas game sprites are never destroyed. RenderTexture copy path always releases temp RTs.
- **VersionChecker:** Logger scoped to the check runner (no cross-mod static clobbering). Version compare ignores SemVer prerelease/`+build` tails for ordering.
- **GUIStyleHelper / MinimalJsonParser / VaultCryptography:** Safer gradient height edge case; UTF-16 surrogate pairs in JSON escapes; docs clarify tamper-resistant local storage vs confidentiality.

**The Vault**

- **Saves:** Filenames use a Steam or per-machine suffix to avoid collisions; legacy bare-name files migrate on load. Unreadable vault files are **quarantined** (`.corrupt-*.bak`) before starting an empty in-memory vault; see mod logs if you need to restore. **Load path:** invalid character names no longer leave a prior character’s vault state “loaded” for a new save; the plugin only marks the vault loaded when load succeeds. **Quarantine UX:** the HUD and vault window surface a one-time notice when a corrupt file was quarantined.
- **API:** `IsVaultManagerAvailable` / `IsVaultDataLoadedForCurrentSession`; `IsVaultReady` kept as a legacy alias for “manager exists.”
- **UI / patches:** Vault UI disposes generated IMGUI textures on rebuild and destroy; vault load trigger uses the same lock as other character-load paths; noisy hot-path logging reduced to Debug where appropriate; `PersistentUpdateRunner` lives in its own file; `VaultModApiBridge` subscription contract documented for soft dependencies.

**Haven's Birthright**

- If an essential Harmony patch fails to apply, the mod sets a fail-closed flag so stat/combat postfixes do not run half-patched.
- **Faster Races:** explicit Harmony priority on `Player.GetStat` (Birthright before Faster Races); coordination with movement speed no longer treats “Faster Races enabled at 0%” as an active speed bonus.

**Haven Dev Tools**

- Comments clarify Steam ID hashing is a client-side convenience gate, not a security boundary.

**Trinket Fortune**

- Nexus detailed-description BBCode refreshed ([`TrinketFortune/NexusMods-TrinketFortune-BBCode.txt`](TrinketFortune/NexusMods-TrinketFortune-BBCode.txt)): `BepInEx/config/TrinketFortune.cfg` (with legacy GUID migration), `MaxBonusChancePercent`, standalone-safe / optional S.M.U.T. and Haven Dev Tools, Thunderstore / Nexus / GitHub links.

**Repository**

- Reusable workflow setup; concurrency on release/test workflows. Stray `manifest.json` files under mod dirs are rejected by the version verifier. Policy files (license, security, contributing, code of conduct) and shared-code strategy doc. Almanac: no committed game decompiles — use local-only `_refs/` per `.gitignore`.
- **Docs:** Root [`README.md`](README.md), this changelog, and [`MAINTAINER_CHANGELOG.md`](MAINTAINER_CHANGELOG.md) were rewritten — concise hub layout, versions aligned with [`docs/versions.json`](docs/versions.json), long-running docs-site diary removed from the README in favor of links.
- **Mod matrix / versions:** `verify-version-consistency.py` also requires every `jsonKey` in `scripts/matrix/mod-matrix.json` to exist in `docs/versions.json`. [`docs/ATOMIC_SAVE_POLICY.md`](docs/ATOMIC_SAVE_POLICY.md) documents temp-file behavior for competing save systems.

---

## 2026-05-01

- Thunderstore manifests normalized (no UTF-8 BOM) for strict tooling.
- Thunderstore READMEs: removed stale “Unreleased” blocks from several mods.
- **The Vault:** clearer destroy-path logging for expected vs unexpected teardown.
- **Sunhaven Todo:** confirmed dirty-save flags on all task mutation paths after save guard work.

---

## 2026-04-29

- Release workflows: Nexus preflight and zip path resolution hardened; Almanac Nexus skip removed where listing exists.

---

## 2026-04-28

Lifecycle and teardown pass across multiple mods: duplicate handler prevention, menu-transition deduplication, Harmony unpatch on shutdown where applicable, debug-level diagnostics for reflection fallbacks. **Senpai's Chest:** smarter persistence and scan logging toggle. **The Vault / Todo / S.M.U.T. / Crop Optimizer / Trinket Fortune / Birthright / Faster Races / Respec / Birthday / Dev Tools / Almanac:** see git history for per-file detail.

---

## 2026-04-21

Performance and stability: UI texture disposal in hot paths, IconCache bounded eviction, fewer allocations in patches and helpers, IconCache eviction fallback fixes, ItemSearch race fix, vault validation and player-context locking, VersionChecker regex caching, silent catches logged at Debug in shared helpers.

---

## Earlier releases

Summaries for older shipped work (vault HUD, mod hub, Crop Optimizer, Haven's Respec, CI migration to self-hosted builds, docs site) live in git history and in each mod’s folder README / [`docs/versions.json`](docs/versions.json) changelog fields.
