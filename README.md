# Sun Haven Mods

BepInEx plugins for [Sun Haven](https://store.steampowered.com/app/1432860/Sun_Haven/) — vault, QoL, integrations, and docs. **Published versions and download links** live in [`docs/versions.json`](docs/versions.json) (also drives the [mod hub](https://www.azraelsmods.com/sun-haven/)). Each mod folder also has a `mod.json` for the public mod website.

## Mods (versions match `docs/versions.json`)

| Mod | Folder | Version |
|-----|--------|---------|
| Senpai's Chest | [`SenpaisChest/`](SenpaisChest/) | 3.1.4 |
| Sun Haven Todo | [`SunhavenTodo/`](SunhavenTodo/) | 2.1.3 |
| Sun Haven Museum Utility Tracker (S.M.U.T.) | [`SunHavenMuseumUtilityTracker/`](SunHavenMuseumUtilityTracker/) | 3.1.3 |
| The Vault | [`TheVault/`](TheVault/) | 4.3.1 |
| Haven's Birthright | [`HavensBirthright/`](HavensBirthright/) | 3.1.4 |
| Haven's Almanac | [`HavensAlmanac/`](HavensAlmanac/) | 2.2.3 |
| A Squirrel's Birthday Reminder | [`BirthdayReminder/`](BirthdayReminder/) | 2.1.4 |
| Haven Dev Tools | [`HavenDevTools/`](HavenDevTools/) | 2.1.3 |
| Trinket Fortune | [`TrinketFortune/`](TrinketFortune/) | 2.1.3 |
| Faster Races | [`FasterRaces/`](FasterRaces/) | 2.1.2 |
| Crop Optimizer | [`CropOptimizer/`](CropOptimizer/) | 2.3.1 |
| Haven's Respec | [`HavensRespec/`](HavensRespec/) | 2.1.4 |
| Gifting Assistant | [`GiftingAssistant/`](GiftingAssistant/) | 1.1.4 |
| Haven's Mirror | [`HavensMirror/`](HavensMirror/) | 1.0.2 |

Per-mod READMEs and Thunderstore packages describe features, hotkeys, and config in detail.

## Installation

1. Install [BepInEx 5.x](https://docs.bepinex.dev/articles/user_guide/installation/index.html) for Sun Haven.
2. Download from [Thunderstore](https://thunderstore.io/c/sun-haven/) / [Nexus](https://www.nexusmods.com/sunhaven) / [GitHub Releases](https://github.com/AzraelGodKing/SunhavenMod/releases), or build from this repo.
3. Copy the mod DLL (and any packaged files) into `Sun Haven/BepInEx/plugins/` (subfolders allowed).

## Build from source

Requires a local Sun Haven install (or CI paths) so game assemblies resolve. Optional env: `SUNHAVEN_PATH` overrides `Directory.Build.props`.

```bash
dotnet build <ModDir>/<ModName>.csproj
```

Artifacts land under `bin/Debug/net48/` (or Release). Local builds may copy into your game’s BepInEx plugins folder when configured.

Multi-mod solution (subset of mods): [`HavensBirthright/HavensBirthright.sln`](HavensBirthright/HavensBirthright.sln).

## Repository layout

| Path | Purpose |
|------|---------|
| `<ModName>/` | One folder per mod + `thunderstore/` packaging |
| [`SharedUtilities/`](SharedUtilities/) | Shared source linked into mods (VersionChecker, IconCache, etc.) |
| [`TheVault.Abstractions/`](TheVault.Abstractions/) | Compile-time API surface for soft-dependent mods |
| [`scripts/`](scripts/) | Maintainer tooling: mod matrix, version sync, stats, build checks, localization (see [`scripts/README.md`](scripts/README.md)) |
| [`docs/`](docs/) | `versions.json`, mod guides (not a hosted site) |
| [`maintainer-docs/`](maintainer-docs/) | Internal engineering docs (not published) — release process, compatibility contract, lifecycle/save policies |
| [`.github/workflows/`](.github/workflows/) | CI, release, stats, site mirror |

The `builds/` directory is **ephemeral** (CI/local staging); do not commit it — see [`builds/README.md`](builds/README.md).

## Changelogs and contributing

- **Players:** [`CHANGELOG.md`](CHANGELOG.md)
- **Maintainers:** [`MAINTAINER_CHANGELOG.md`](MAINTAINER_CHANGELOG.md) (CI, scripts, infra)

[`CONTRIBUTING.md`](CONTRIBUTING.md) · [`SECURITY.md`](SECURITY.md) · [`LICENSE`](LICENSE)

## Links

- [Site](https://www.azraelsmods.com/sun-haven/)
- [Version & release process](maintainer-docs/VERSION_AND_RELEASE.md)
- [Discord](https://discord.gg/Vwh2y7qMXv)