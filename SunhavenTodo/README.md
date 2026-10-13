# Sunhaven Todo

An in-game to-do list for Sun Haven with per-character saves, categories, priorities, and a parchment-styled UI.

## Version

Published in [`docs/versions.json`](../docs/versions.json). Player-facing store text: [`thunderstore/README.md`](thunderstore/README.md).

## Features

- **In-Game Todo List**: Add, edit, complete, and delete tasks without leaving the game
- **Per-Character Lists**: Each character has their own separate todo file
- **Categories & Priorities**: Organize tasks by category and urgency
- **Search & Filters**: Search titles/descriptions, filter by category, and hide completed items
- **Progress Stats**: See totals and completion percentage at a glance
- **Auto-Save**: Saves automatically on a timer and on game exit

## Controls

| Action | Key |
|--------|-----|
| Toggle Todo List | `Ctrl + T` |
| Close Window | `Escape` |
| Toggle HUD (sticky panel) | `Ctrl + H` (see config: `HUDToggleKey`, `RequireCtrl`) |
| Close HUD | **X** on the HUD header |
| Reopen HUD | HUD toggle hotkey (works even while the full Todo window is open), or **Sticky** in that window’s header when the sticky panel was hidden |

## Installation

1. Install BepInEx 5.x for Sun Haven
2. Build or download `SunhavenTodo.dll`
3. Copy `SunhavenTodo.dll` to `Sun Haven/BepInEx/plugins/SunhavenTodo/`
4. Launch the game

## Configuration

After first launch, edit the config file at:
`Sun Haven/BepInEx/config/SunhavenTodo.cfg`

| Setting | Default | Description |
|---------|---------|-------------|
| ToggleKey | T | Key to open/close the todo list |
| RequireCtrl | true | Require Ctrl to be held with ToggleKey |
| AutoSave | true | Automatically save the todo list |
| AutoSaveInterval | 60 | Auto-save interval (seconds) |

## Data Storage

Todo lists are saved per character under:
`Sun Haven/BepInEx/config/com.azraelgodking.sunhaventodo/`

Each file is named like:
`<CharacterName>_todos.json`

Character names are sanitized to avoid invalid filename characters.

## Notes

- The window is draggable by the header
- The UI pauses player input while the todo list is open

## Changelog

### 2.0.1

- **HUD:** Close (**X**) on the sticky header hides the HUD; reopen with the HUD toggle hotkey or the **Sticky** button in the full Todo window when the panel is hidden.

### 2026-05-02 (maintainer notes)

- **Build:** Excluded `Tests/**/*.cs` from `SunhavenTodo.csproj` compile items so NUnit test sources are not compiled into the runtime mod assembly, and linked `SharedUtilities/OvernightHookUtility.cs` so `Plugin.TryHookOvernight()` compiles in normal mod builds.
- **Lifecycle:** Added teardown unsubscription for scene/manager handlers and changed destroy logging to treat menu/quit teardown as expected.
- **Stability:** Added a gameplay -> menu transition save/reset path so dirty todo data is flushed once and character patch runtime state is reset cleanly between sessions.

## Links

- [Nexus Mods](https://www.nexusmods.com/sunhaven/mods/491)
- [Documentation](https://www.azraelsmods.com/sun-haven/sunhaven-todo/)
- [Discord — bugs & discussion](https://discord.gg/Vwh2y7qMXv)

## License

Feel free to use, modify, and distribute this mod.

## Credits

- Created by AzraelGodKing
- Built with BepInEx and Harmony
