# Haven's Respec

- **Documentation:** [Haven's Respec](https://www.azraelsmods.com/sun-haven/havens-respec/)

Haven's Respec adds a safe, configurable way to reset your Sun Haven skill trees.

Every profession tab gets a styled **Reset** button. Clicking it brings up a confirmation
dialog summarising how many points you'll get back and how much (if anything) the reset
costs, and on confirm the full tree is cleared and your points are returned. The most
recent reset can be undone per-profession during the same session.

## Version

Published in [`docs/versions.json`](../docs/versions.json). Player-facing store text: [`thunderstore/README.md`](thunderstore/README.md).

## Features

- **Clean-room reset.** Walks the live `SkillNode` instances the game placed in
  `Skills._professionNodeDictionary` instead of hardcoding `{Combat1a..Exploration10d}`
  strings — so it survives Sun Haven updates that add rows, rename nodes, or change which
  columns a profession uses.
- **Confirmation dialog** (uGUI, warm wood-on-dark theme matching the Crop Optimizer UI).
- **Undo** — per profession, session-only. The snapshot is taken immediately before every
  reset and restores exactly the allocation you had.
- **Cost model** — `None` (default), `Gold`, or `Gems`, with a configurable cost-per-point.
- **Shift-skip** — hold `Shift` while clicking the Reset button to bypass the confirmation
  dialog (toggleable).
- **Optional hotkeys** for `ResetCurrentTab` and `Undo` (both unbound by default).
- **Full Configuration Manager visibility.** Config lives in
  `BepInEx/config/HavensRespec.cfg` and Haven's Respec rewires `BaseUnityPlugin.Config`
  via reflection so every setting is editable live, in-game, through the
  BepInEx Configuration Manager UI.

## Config (`BepInEx/config/HavensRespec.cfg`)

| Section | Key | Default | Notes |
| --- | --- | --- | --- |
| `General` | `Enabled` | `true` | Master switch. |
| `UI` | `InjectButtons` | `true` | Adds the styled Reset button to every skill tab. |
| `UI` | `RequireConfirmation` | `true` | Show a confirmation dialog. |
| `UI` | `ShiftSkipsConfirmation` | `true` | Hold Shift to bypass the dialog. |
| `UI` | `EnableUndo` | `true` | Show an Undo button after a reset. |
| `UI` | `EnableResetAll` | `true` | Show **Reset All** below **Reset** on each skill tab; one confirm resets every profession. |
| `Cost` | `Mode` | `None` | `None` / `Gold` / `Gems`. |
| `Cost` | `GoldPerPoint` | `100` | Used when `Mode = Gold`. |
| `Cost` | `GemsPerPoint` | `1` | Used when `Mode = Gems`. |
| `Hotkeys` | `ResetCurrentTab` | `None` | KeyCode, unbound by default. |
| `Hotkeys` | `Undo` | `None` | KeyCode, unbound by default. |
| `Debug` | `DebugLogging` | `false` | Extra logging. |

## Compatibility

- **Multiplayer:** this mod only rewrites your own `Profession.Nodes`. Other players in
  the session keep their cached copy of your allocation in `NetworkGamePlayer.multiplayerNodes`
  until the next re-sync; reopen the Skills panel (or reconnect) to force a refresh. Do not
  run a reset while another player is actively using a mid-combat skill you allocated — use
  the standard "town only" respec etiquette.
- **Other skill mods:** Haven's Birthright does not touch the skill-point system. If you run
  a third-party mod that grants stat bonuses based on node hashes, those bonuses will be
  cleared until you re-allocate into the same node.
- **Save compatibility:** reads and writes only `Profession.Nodes` + `Skills.skillPointsUsed`
  — the exact fields the game itself touches in `UpdateProfession`. No save format changes.

## Inspired by — not derived from

The community had a single maintained option for this feature historically; its author stopped
maintaining it. Haven's Respec is a clean-room reimplementation — **no source reuse**. The
public decompiled reference was used only to understand the problem shape (Harmony target,
which game fields are involved), not for any code. See the changelog for the specific
correctness and UX upgrades over the prior implementation.

## Changelog

### 2.1.3

- **Fix (AZR-356):** Respec rolls back Max Mana from refunded nodes (Mental Focus / Town Spirit and other mana skills). `SetActive` receives the live node rank; leftover permanent mana and matching character progress counters are cleared; Undo restores them.

### 2.1.2

- **Fix:** Confirm and Cancel on the respec prompt now work, so the Skills window is no longer stuck. The skill UI never delivered clicks to those buttons; the prompt reads the mouse itself. Shift-click is unchanged. Escape or a click on the dark backdrop still cancels.
- **UI:** **RESET ALL** sits below **Reset** in the left sidebar (Undo moves down when shown). Removes the old top-center placement that overlapped tier headers. Reset-all confirm dialog grows with longer copy so footer buttons no longer cover the undo note.

### 2026-05-02 (maintainer notes)

- **Lifecycle:** Harmony patches and controller hooks survive scene transitions (main menu ↔ game); a persistent runner keeps hotkeys alive when the BepInEx plugin MonoBehaviour is destroyed. Full unpatch only on application quit.
- **Cost preflight accuracy:** affordability checks now use an exact live-node refund estimate (with safe fallback), reducing false "cannot afford" blocks caused by stale point counters.
- **Reset All hardening:** reset-all now preflights total affordability and executes as a rollback-safe sequence; if any profession reset fails, previously reset professions are automatically undone in reverse order.
- **Undo depth:** undo now supports multi-level per-profession history (stacked snapshots) instead of only the latest reset.
- **Release metadata:** `docs/versions.json` includes Nexus mod page `https://www.nexusmods.com/sunhaven/mods/502`, `nexus_file_group_id` **7326839** (Files tab → API / file group id) for CI Nexus uploads, and the Thunderstore URL for `download-nav` on the doc page.
- **Copy:** Removed named references to a specific legacy skill-reset mod; docs and `RespecCostMode` comments now refer generically to other community mods.
- **Docs:** [`docs/HavensRespec/HavensRespec.html`](../docs/HavensRespec/HavensRespec.html) now loads [`docs/HavensAlmanac/almanac-style.css`](../docs/HavensAlmanac/almanac-style.css) for the shared book / parchment layout; [`docs/HavensRespec/respec-style.css`](../docs/HavensRespec/respec-style.css) adds copper accents, fixes the configuration table (wrapper vs. `<table>`), and styles compatibility note cards and install steps.
- **Thunderstore:** `thunderstore/manifest.json` `website_url` points at the [mod page](https://www.azraelsmods.com/sun-haven/havens-respec/) (canonical with `docs/HavensRespec/HavensRespec.html`).
- **Initial release scaffold** of Haven's Respec v1.0.0:
  - Harmony postfix on `Skills.SetupProfession(ProfessionType, SkillTree, SkillTreeAsset)`
    injects a styled Reset (and optional Undo) button onto every profession tab.
  - `SkillResetService` enumerates the live `Skills._professionNodeDictionary` to get the
    current node name list (no hardcoded `{Prof}{1..10}{a|b|c|d}` loop), zeroes every entry
    in `Profession.Nodes`, resets `Skills.skillPointsUsed[profession]` and
    `Skills.numActiveNodes[profession]`, and calls `Skills.EnablePanelWithAvailableSkillPoint`
    plus a best-effort private `UpdateProfession(...)` invocation for immediate UI refresh.
  - Session-only per-profession undo stack: `ResetSnapshot` captures `Nodes` + point counters
    immediately before every reset; Undo restores them and clears the snapshot.
  - Post-reset UI refresh: `TryRefreshProfessionPanel` only invokes the private
    `Skills.UpdateProfession(profession, panel, image, asset)` for the profession that was
    actually reset, deliberately **not** calling `Skills.EnablePanelWithAvailableSkillPoint()`
    — that helper walks `ProfessionType` in enum order (Combat → Farming → Fishing → Mining →
    Exploration) and force-enables the first panel with unspent points, which after a reset
    was yanking the open tab to whichever profession ranked first in the enum (almost always
    Farming). Now the tab you opened stays open.
  - `CostService` computes the reset price (`None` / `Gold` / `Gems`), checks the player's
    balance via `GameSave.Coins` / `GameSave.Tickets`, and deducts via `Player.AddMoney` /
    `Player.AddTickets` with a negative amount. Missing API → refuse reset (never free-ride
    a failed deduction).
  - `ConfirmResetDialog` uGUI modal with scrim, wood border, parchment-accented typography,
    and hover/pressed tint states on both the Cancel and Reset buttons. The dialog root is a
    full-screen `RectTransform` (not a plain `Transform` under the canvas — that broke stretch
    layout and could desync visuals from raycasts), with a **nested** `Canvas`
    (`overrideSorting`, `sortingOrder = parent + 200`, matching `renderMode` / `worldCamera` /
    `planeDistance`) plus its own `GraphicRaycaster` so the scrim and buttons sit above sibling
    UI and reliably receive clicks. Title, body, and card border use `raycastTarget = false`
    so they cannot steal hits from the buttons.
  - `RespecButtonInjector` ships a layered wood-plaque look — drop shadow → tintable fill →
    top-half gloss highlight → fixed gold border ring → all-caps letter-spaced TMP label.
    Hover / press now nudge only `Image.color` + `localScale` (1.04× hover lift, 0.97× press
    sink) instead of rebaking a sprite per pointer event, so feedback is instant and
    allocation-free. Row-0 button sits `135u` below and `14u` left of `_skillPointsTMP` —
    clear of the Skill Points caption, the "{Profession} EXP" label and the EXP bar, and
    visually balanced in the sidebar column. `I2.Loc.Localize` is stripped from the label
    so the game's translator doesn't overwrite the English text with a lookup miss. The
    label intentionally does **not** clone `fontSharedMaterial` for an outline — TMP's
    shared material hasn't resolved by the time `SetupProfession` fires, so
    `new Material(null)` would throw `ArgumentNullException` and abort the whole
    `BuildButton`. Bold cream on the dark fill is legible without it.
  - Config lives in `BepInEx/config/HavensRespec.cfg` via
    `SunhavenMods.Shared.ConfigFileHelper` — the mod also calls `ReplacePluginConfig` so
    every setting is live-editable from the BepInEx Configuration Manager UI in-game.
  - Optional `ResetCurrentTabHotkey` / `UndoHotkey` KeyCode bindings (unbound by default) are
    wired in `Plugin.Update` and resolve the active tab by asking which profession panel
    GameObject is currently active.
  - Build + publish plumbing: GitHub Actions matrix entry, `scripts/version/pre-push-build.ps1`
    tracking, `docs/versions.json` entry, mod card on the Sun Haven docs hub, and a dedicated
    `docs/HavensRespec/HavensRespec.html` page.
  - Repo hygiene: root `.gitignore` now ignores `builds/` staging outputs. CI workflows still
    use `builds/<ModDir>/` during each run, but those DLLs are treated as ephemeral artifacts
    (download/upload/package inputs), not tracked source files.
  - Cost correctness hardening:
    - `CostService.ReadBalance` now fails closed (`-1`) on reflection errors so `CanAfford`
      refuses paid resets when wallet verification is unavailable.
    - `RespecController.PerformReset` now charges by the **actual** refunded points returned by
      `ResetProfession` (post node-walk), not the preflight estimate. If affordability/deduction
      fails at actual cost time, the reset is automatically rolled back via undo snapshot.
    - Undo now refunds the exact charged amount/currency. `ResetSnapshot` carries
      `ChargedCost` + `ChargedCostMode`, set only after a successful deduction, and
      `PerformUndo` applies `CostService.TryRefund(...)` after restoring nodes.
  - Cleanup / perf pass:
    - Implemented `EnableResetAll`: adds a top-level **RESET ALL** button with one
      confirmation dialog, then runs per-profession reset flow in sequence.
    - Hotkey tab detection now uses cached `_activeSkills` via `RespecController`
      instead of `FindObjectOfType<Skills>()` every keypress.
    - Removed silent catches in `TryGetActiveProfessionTab`, `CaptureSnapshot`,
      and `CollectNodeHashes`; failures now emit debug logs with context.
    - `CollectNodeHashes` now deduplicates with `HashSet<int>` (replacing
      `List.Contains` O(n²) checks).
    - Consolidated profession panel field mapping into
      `Services/ProfessionUiMap.cs` and reused it from both UI and services.
    - Confirmation dialog hover no longer regenerates rounded sprites per pointer
      event; it now updates `Image.color` only (matching button injector behavior).
