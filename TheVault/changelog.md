# The Vault - Changelog

## Version 4.3.1

- **Maintenance:** One currency-prefix dispatcher (`VaultCurrencyIds.TryDispatch`) for vault add/remove/get. Inventory merge uses `GetCurrency`. Unused `ticket_` dispatch and `GetTickets`/`AddTickets`/`RemoveTickets` aliases removed; `Tickets`/`Orbs`/`CommunityTokens` dicts and `CurrencyCategory` values are unchanged. Shared Steam ID lookup caches successes only (debug logs). Menu `SaveAndReset` is no longer fired twice on scene load.

## Version 4.3.0

- **Save format (AZR-243):** New saves use CSVAULT3: a random per-vault salt, a fresh IV per write, PBKDF2-SHA256 with 100,000 iterations, and an HMAC-SHA256 integrity check verified before decrypting. A corrupted or edited file fails cleanly and falls back to the backup. CSVAULT2 and older files still load and are rewritten as CSVAULT3 on load.
- **Fix (AZR-242):** CSVAULT3 keys use the character name only, so they no longer depend on Steamworks load timing. For older files, a failed Steam ID lookup is retried instead of latched for the session, and the player-portable key is always tried.

## Version 4.2.2

- **Fix (AZR-438):** The vault window consumes mouse input. Inventory slots under the cursor no longer pick up or throw items while the window is open.

## Version 4.2.1

- **Fix (AZR-359):** Window scale follows `Screen.height/1080` when `ScaleWithResolution` is on, so 1440p and ultrawide stay readable. `AutoHighResLayout` raises the default scale once on those displays.
- Removed unused door, shop, save, and inventory hooks that were never registered.

## Version 4.2.0

### Steam Deck compatibility V1 (AZR-348)

- **Touch targets:** Currency row toggle and -1/-5/-10 withdraw buttons use a ≥44px minimum (scale with WindowScale). Row height 52; window width 560.
- **Amount chips:** When a currency is selected, **1 / 5 / 10 / Max** chips set the qty field (Max = vault balance). Deposit/Withdraw/chips use ≥44px control height.
- **Controller open:** Hold **LB** + press **Y** (`JoystickButton4` held + `JoystickButton3` down) toggles the vault via `ToggleMainVaultWindow`. Configurable: `[UI] ControllerOpenEnabled` (default true), `ControllerOpenModifier`, `ControllerOpenButton`. Settings tab has an Enable toggle + hint; F8 AltToggle still works for Steam Input keyboard layers.
- **Controller close:** Escape still closes; also tries game cancel (`UICancel` / `Cancel` / `Close`) so Steam Input **B** can dismiss the vault. No global Harmony block of UICancel.
- **Deck layout defaults:** `[Display] AutoDeckLayout` (default true) once raises WindowScale / HUD Scale to at least **1.35** on Deck-like resolutions (1280×800 / 1280×720 or ≤1366×800 ~16:10). `[Display] DeckLayoutApplied` prevents re-fighting user prefs.

## Version 4.1.3

- **Fix (AZR-240):** Legacy vault recovery skips the plaintext `CSVAULT2` header (shared `VaultCryptography`) so key-mismatch recovery can succeed again.
- **Fix (AZR-238):** After a failed load, saving is blocked until you confirm **Start Fresh** in the Vault UI — so a surviving `.backup` is not overwritten by an empty in-memory vault.

## Version 4.1.2

- **Fix (AZR-234):** Tab buttons use owned textures on every IMGUI interaction state (including focused), so switching categories cannot fall back to the skin atlas "zoomed text" background.

## Version 4.1.1

- **Fix:** `HasEnough` combines bag + vault; deposit/auto-deposit rolls back inventory when vault credit fails; menu `ResetState` clears auto-deposit guard flags.
- **Fix:** Load restores from `.backup` when the primary save file is missing or unreadable (crash during atomic save), instead of silently starting an empty vault. Tries candidates in order canonical → backup → legacy; quarantines corrupt primary when backup/legacy loads; resets re-encryption flag per candidate.
- **Fix (pt-BR):** Re-applied native-speaker Portuguese (Brazil) strings with correct UTF-8 encoding.
- **Fix:** `GetVaultAmount` supports registered `custom_` currencies in inventory merge paths.
- **Fix:** Shop currency deduction runs only after prefix-approved vault purchases.
- **Fix:** HUD recreated when destroyed without recreating the full vault window.
- **Fix:** Encryption mode and secret-gift hash logs downgraded to debug.

## Version 4.0.1

- **Fix:** Vault UI and HUD icons display again — `VaultUI`/`VaultHUD` now resolve `IconCache` to `SunhavenMods.Shared.IconCache` (C# namespace shadowing had them reading the removed legacy cache while load/register used shared). Removed duplicate `TheVault/UI/IconCache.cs`.
- **Performance:** Icon loading on character enter uses shared `SunhavenMods.Shared.IconCache` only (no duplicate legacy cache load). Vault HUD rebuilds row layout only when currency totals or HUD scale/density change. Character-context sync fallback runs ~0.75s instead of every frame; repeated `CurrentCharacter` fallback warning logs once per session.

## Version 2.0.0
- Added item icons from the game to Vault UI and HUD
- Icons are loaded from the game's database and cached for performance
- Fallback to text abbreviations while icons are loading or unavailable

## Version 1.0.4
- Added persistent HUD bar showing vault currency totals (toggle with F7)
- HUD displays abbreviated currency names with counts, hides when main vault is open
- Configurable HUD position (TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight)
- Moved Community Tokens to Special category for better organization
- Added input blocking when Vault or Debug UI is open (prevents game interaction)
- Added Steam Deck support with F8 as alternative keybind (no Ctrl required)

## Version 1.0.3
- Added Candy Corn Pieces support
- Added Mana Shard support
- Renamed "Tickets" category to "Special" to better reflect the variety of currencies
- All special currencies now use unified "special_" prefix internally

## Version 1.0.2
- Added Red Carnival Ticket support
- Renamed "Pirate" category to "Tickets" to accommodate all ticket types
- Improved Debug UI with dropdown menus for all vault currencies
- Debug UI now includes quick amount buttons for easier testing

## Version 1.0.1
- Fixed item duplication bug when withdrawing currencies from vault
- Added support for Pirate Currencies:
  - Doubloon
  - Black Bottle Cap

## Version 1.0.0
- Initial release
- Auto-deposit system for currencies when picked up
- Seamless shop integration (vault currencies used automatically)
- Door and chest integration for keys
- Per-character vault storage
- Auto-save every 5 minutes
- Supported currencies:
  - Seasonal Tokens (Spring, Summer, Fall, Winter)
  - Community Tokens
  - Keys (Copper, Iron, Adamant, Mithril, Sunite, Glorite, King's Lost Mine)
- Vault UI with Ctrl+V hotkey
- Quick withdraw buttons (-1, -5, -10)
- Per-currency auto-deposit toggles
