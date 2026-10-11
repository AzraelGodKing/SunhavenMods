# Senpai's Chest

**Version 3.1.4** — Smart chests with configurable item rules for hands-free storage organization in Sun Haven. Chest labels, rules by item/category/type/property/group, per-character saves.

**2.2.4:** Chest labels now anchor from chest sprite world bounds (top-center), fixing cases where labels appeared offset instead of directly above the chest. Museum-detected Todo tasks now include destination hall and item icon metadata for richer Todo/HUD display. Added a new **By Category** option: **Undonated Items** (museum-needed items via S.M.U.T), and the category now only appears when S.M.U.T. is installed. Adding a rule now auto-enables Smart Chest by default (configurable via `AutoEnableSmartChestOnRuleAdd`). Config file now uses `BepInEx/config/SenpaisChest.cfg` (auto-migrates values from the legacy GUID-named config on first load).

**2.2.2:** No per-scene disk reload that overwrote in-memory config. **Rule deletion** only when a chest **actually leaves the world** (picked up / destroyed): `Chest.OnDisable` plus guards for **application quit** and **scene teardown** (active scene replaced). Scene-discard suppression now tracks replaced scene handles long enough for slower transitions, so area changes do not delete rules. Sorts never delete saved rules; no scan-time “orphan” wipe.

**Disaster recovery:** Per-character saves live in `BepInEx/config/SenpaisChest/Saves/<CharacterName>_smartchests.json`. Each save renames the previous file to `.bak`. If your rules were overwritten with an empty file, exit the game and try restoring the `.bak` (copy it over the `.json`, or merge the `Chests` array from the backup).

## Features

- **Chest Labels** — Labels above chests (Chest, Large Wooden Chest, BankChest). Configurable visibility. Excludes Hoppers and Animal Feeders.
- **Smart Chests** — Mark any chest as a Smart Chest and define rules for which items it collects
- **Automatic Sorting** — Smart Chests periodically scan nearby chests and pull matching items (scan interval configurable)
- **Flexible Rules** — Filter items by:
  - **By Item** — Specific items (search by name)
  - **By Category** — Equip, Use, Craftable, Monster, Furniture, Quest, Undonated Items
  - **By Item Type** — Normal, Armor, Food, Fish, Crop, Watering Can, Animal, Pet, Tool
  - **By Property** — Gems, Forageables, Animal Products, Meals, Fruits, Artisanry Items, Potions, Museum (Not Donated)
- **By Group** — Your own custom subcategories (e.g. Flowers, Vegetables). Create groups in Manage Groups, add items via search, add wildcard patterns (`*`, `?`), then attach to chests with one click.
- **Per-Character Saves** — Each character's Smart Chest settings are saved separately
- **Multiplayer Safe** — Skips chests that other players are using

## Usage

1. Open any chest
2. Press **F9** (or your configured key) to open the Smart Chest configuration
3. Enable **Smart Chest** and add rules for items this chest should collect
4. Close the config and the chest
5. Matching items will be pulled from nearby chests automatically on each scan

## Configuration

Edit `BepInEx/config/SenpaisChest.cfg`:

| Setting | Default | Description |
|---------|---------|-------------|
| EnableChestLabels | true | Show labels above chests |
| LabelVisibility | Visible | Chest labels: Visible, OnHover, or Hidden |
| IconVisibility | Visible | Item icons in labels: Visible, OnHover, or Hidden |
| ScanInterval | 60 | Seconds between scans (min: 10) |
| EnableNotifications | true | Show notifications when items are moved |
| MaxItemsPerScan | 50 | Max item stacks moved per scan (reduces lag) |
| ToggleKey | F9 | Key to open config UI while chest is open |
| RequireCtrlModifier | false | Require Ctrl held with toggle key |
| AutoEnableSmartChestOnRuleAdd | true | Automatically enable Smart Chest when adding a rule |
| CheckForUpdates | true | Check for mod updates on startup |

## Notes

- Smart Chests do not pull from other Smart Chests (prevents loops)
- Chests in use by another player are skipped
- Configuration is tied to chest position; moving a chest resets its config

## Tips — Fine-Grained Sorting

The game's ItemType "Crop" includes flowers, vegetables, and all crop types. Use **By Group** for the easiest way to separate them:

1. Open a chest, press F9, and click **Manage Groups**
2. Create a group (e.g. "Flowers"), search for items, and add them
3. Use **By Group** when adding rules to attach the group to any chest with one click
4. Reuse the same group across multiple chests—edit it once to update all of them

Wildcard tips:
- In **Manage Groups**, you can add patterns like `Tomato*`, `*Ore`, `Gold ???`.
- Use **+IDs** / **Add Matches to IDs** to materialize current wildcard matches as fixed item IDs.

## Links

- [Nexus Mods](https://www.nexusmods.com/sunhaven/mods/496)
- [Documentation](https://www.azraelsmods.com/sun-haven/senpais-chest/)
- [Report Bugs / Discord](https://discord.gg/Vwh2y7qMXv)
