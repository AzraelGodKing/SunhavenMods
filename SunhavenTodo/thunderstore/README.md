# Sun Haven Todo

**Version 2.1.3** — In-game task tracker for farming goals, quests, and daily tasks. Categories, priorities, per-character saves, parchment-style UI. Default hotkey: **Ctrl+T**.

**1.1.7:** Museum-detected tasks now show destination hall (`The Hall of Gems`, `The Hall of Culture`, or `Aquarium`) and render the associated item icon in both Todo UI and HUD. Long task titles now wrap and task rows expand in both Todo UI and HUD so text is no longer cut off. Runtime autosave was moved to the hidden persistent keepalive runner, so Todo survives plugin lifecycle cleanup without relying on `Plugin.Update`. Config file now uses `BepInEx/config/SunhavenTodo.cfg` (auto-migrates values from the legacy GUID-named config on first load).

## Features

- **Task Management** - Create, edit, and complete tasks
- **Categories** - Organize tasks by type (farming, quests, etc.)
- **Priority Levels** - Mark important tasks
- **Per-Character Saves** - Separate todo lists for each save file
- **Parchment Theme** - Beautiful UI that fits the game's aesthetic

## Usage

Press the configured hotkey to open the todo list UI.

## Installation

1. Install BepInEx 5.x
2. Place `SunhavenTodo.dll` in `BepInEx/plugins/`
3. Launch the game. Open todo: **Ctrl+T** by default (configurable).

## Configuration

Edit `BepInEx/config/SunhavenTodo.cfg` to customize settings.

## Links

- [Nexus Mods](https://www.nexusmods.com/sunhaven/mods/491)
- [Documentation](https://www.azraelsmods.com/sun-haven/sunhaven-todo/)
- [Report Bugs on Discord](https://discord.gg/Vwh2y7qMXv)
