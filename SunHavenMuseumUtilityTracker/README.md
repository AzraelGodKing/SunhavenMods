# Sun Haven Museum Utility Tracker (S.M.U.T.)

**Version 3.0.1**

Track museum donations across **Hall of Gems**, **Hall of Culture**, and the **Aquarium** in [Sun Haven](https://store.steampowered.com/app/1432860/Sun_Haven/). Lists use the game’s real item IDs (from `Wish.ItemID` and official bundle layouts), so icons, search, and cross-mod features stay aligned with your save.

---

## Features

| | |
|--|--|
| **Three halls** | Gems, Culture, and Aquarium bundles in one warm, parchment-style tracker |
| **Progress** | Per-section and per-bundle counts; overall completion; optional in-game bundle stats next to headers |
| **Icons** | Item icons from game data when available |
| **Search & filter** | Find by name; toggle “needed only” to hide completed rows |
| **Per-character saves** | Progress is stored separately for each character |
| **Hotkeys** | Default **Ctrl+C** to open/close; **F7** alternate toggle (no modifier, e.g. Steam Deck) |
| **Display** | Configurable UI scale (50%–250%) |
| **Sync with game** | One-click import: marks every item in **completed** bundles using world save progress |
| **Live donations** | Donating at the museum chest can auto-mark the matching tracker row (see *How tracking works* below) |

Other mods can read donation state via `Plugin.GetDonationManager()` (e.g. **Senpai’s Chest**, **Sun Haven Todo**, **Trinket Fortune**, **Haven’s Almanac**, **Haven Dev Tools**), when those integrations are installed.

---

## How tracking works

- The vanilla game records **bundle-level** progress (counts and completion flags), not a full checklist of “which exact slots” you filled in an incomplete bundle.
- **S.M.U.T.** keeps a **checklist** in its own save file. You can always toggle rows manually.
- When you **donate through the museum**, the mod watches the museum chest flow and can **auto-mark** the donated item in the correct **bundle-scoped** slot (important when the same game item appears in more than one bundle, e.g. seasonal crops).
- **Sync with Game** reads `GameSave` world progress and, for each bundle the game treats as **done**, marks **all** items in that bundle in the tracker. It does not reconstruct partial bundles item-by-item (the game doesn’t expose that reliably).
- After major list updates (e.g. culture/aquarium IDs finalized), old saves may still have **placeholder row keys** from older builds. Use **Sync** or re-check affected bundles if counts look wrong.

---

## Museum coverage

### Hall of Gems

Mana, Money, Golden, Bars, Gems, Nel’Vari Mines, Withergate Mines — donation quantities match in-game bundle requirements where applicable.

### Hall of Culture

Spring / Summer / Fall / Winter crops, Nel’Vari & Withergate crops, Flowers, Foraging, Exploration, Combat, Alchemy, Nel’Vari Temple — full static lists with real IDs.

### Aquarium

Fishing (treasures), seasonal fish tanks, Nel’Vari fish tank, Withergate fish tank, large fish tank — full static fish lists (no runtime resolver).

---

## Controls

| Action | Default |
|--------|---------|
| Open / close tracker | **Ctrl+C** (if `Require Ctrl` is on) |
| Open / close (alternate) | **F7** — no modifier |
| Close while focused | **Escape** |
| Mark item donated / needed | Click the checkbox |
| Expand / collapse bundle | Click the bundle header |
| **Sync with Game** | Button in the tracker window |

---

## Installation

1. Install **[BepInEx 5.x](https://github.com/BepInEx/BepInEx)** for Sun Haven.
2. Get **`SunHavenMuseumUtilityTracker.dll`** from [Nexus](https://www.nexusmods.com/sunhaven/mods/490), [Thunderstore](https://thunderstore.io/c/sun-haven/p/AzraelGodKing/SMUT/), or your build output.
3. Place the DLL under:

   `Sun Haven/BepInEx/plugins/`  

   (a subfolder such as `SunHavenMuseumUtilityTracker/` is fine.)

4. Launch the game; configure hotkeys and scale in the generated config if you like.

**BepInEx GUID (for soft dependencies):** `com.azraelgodking.sunhavenmuseumutilitytracker`

---

## Configuration

File (after first run):

`Sun Haven/BepInEx/config/SunHavenMuseumUtilityTracker.cfg`

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| Hotkeys | `ToggleKey` | `C` | Key used with Ctrl (if required) to toggle the window |
| Hotkeys | `RequireCtrl` | `true` | Require Ctrl when using `ToggleKey` |
| Hotkeys | `AltToggleKey` | `F7` | Second toggle key — **no** modifier |
| Updates | `CheckForUpdates` | `true` | Check for a newer release on startup |
| Display | `UIScale` | `1.0` | Window scale (`0.5`–`2.5`) |

---

## Save data

Per-character JSON:

`Sun Haven/BepInEx/config/SunHavenMuseumUtilityTracker/Saves/<CharacterName>_donations.json`

Backups may appear beside the primary file after failed loads. Keep these if you rely on manual progress between game patches.

---

## UI reference

- **Section tabs** — Gems / Culture / Aquarium  
- **Bundle rows** — show local progress; when available, **[Game: n]** reflects the game’s donated-count for that bundle  
- **Rarity tint** — Common → Legendary (gray → gold accents)  
- **Footer** — reminder of toggle keys and that Sync applies to **completed** bundles only  

---

## Links

- [Nexus Mods](https://www.nexusmods.com/sunhaven/mods/490)  
- [Thunderstore — SMUT](https://thunderstore.io/c/sun-haven/p/AzraelGodKing/SMUT/)  
- [Documentation](https://www.azraelsmods.com/sun-haven/smut/)  
- [Discord — bugs & discussion](https://discord.gg/Vwh2y7qMXv)  

---

## Version history

- **3.0.1** — Repo release alignment (`docs/versions.json`). Patch release: shared localization language-change refresh fix (Harmony postfix parameter casing on `SetLanguageAndCode`).
- **2026-05-02 (maintainer notes)** — Lifecycle hardening pass: menu transition reset now runs once per actual gameplay → menu transition, duplicate reset paths were deduplicated, and plugin teardown logging now treats menu/quit shutdown as expected while unsubscribing scene handlers on destroy.
- **2.2.7** — Co-op freshness: switched to a Senpai's Chest-style background refresh loop that syncs world museum progress every few seconds, so each player's tracker stays aligned even if the window is closed. Runtime autosave now runs solely from the hidden keepalive runner (no `Plugin.Update` dependency). Config file now uses `SunHavenMuseumUtilityTracker.cfg` with first-run migration from the legacy GUID-based filename.
- **2.2.5** — Hall of Culture: full static lists with real `Wish.ItemID` + wiki ordering; bundle-scoped lookup and `HasDonatedByGameId` across duplicate rows (e.g. Pepper in Spring and Summer). Aquarium: all tanks use static fish data; removed runtime `AquariumFishResolver`. **Note:** older saves may use placeholder culture row IDs — re-check or Sync.  
- **2.2.4** — Reliable `GameSave` world progress; bundle completion aligned with `MuseumCurator`; season fish icons; placeholders where icons missing; persist save after Sync.  
- **2.2.3** — Thunderstore packaging iteration.  
- **2.2.1** — Manual tracking, search, filter, polished UI, per-character saves.  
- **1.0.0** — Initial public release.  

---

## License

Feel free to use, modify, and distribute this mod.

## Credits

- **Author:** AzraelGodKing  
- **Stack:** [BepInEx](https://github.com/BepInEx/BepInEx), [Harmony](https://github.com/pardeike/Harmony)  
