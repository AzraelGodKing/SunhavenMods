# Haven's Mirror

- **Nexus Mods:** [Haven's Mirror](https://www.nexusmods.com/sunhaven/mods/538) ([files tab](https://www.nexusmods.com/sunhaven/mods/538?tab=files); CI `nexus_file_group_id` **8035296**)
- **Thunderstore:** [HavensMirror](https://thunderstore.io/c/sun-haven/p/AzraelGodKing/HavensMirror/)

Drop-in PNG bust portraits for your farmer during dialogue, with an optional forced look for NPC portraits and seasonal outfits.

## Version

Published in [`docs/versions.json`](../docs/versions.json). Player-facing store text: [`thunderstore/README.md`](thunderstore/README.md).

## Default Behavior

- Drop PNG bust portraits into `BepInEx/plugins/HavensMirror/gallery/<CharacterName>/` (folders are created from your saves) or `gallery/_shared/`.
- Expected files: `spring.png`, `summer.png`, `autumn.png`, `winter.png`, `vows.png`, `shore.png`, `costume.png`.
- Empty character folders are ignored; missing slots reuse the first PNG found.
- Reload with **Ctrl+F8** (configurable).
- Optional `ForcedLook` overrides NPC dialogue bust season / special outfit.

## Config

File: `Sun Haven/BepInEx/config/HavensMirror.cfg`

## Notes

- This README is for repo maintainers.
- Independent suite implementation inspired by the *idea* of Nexus Sun Haven #329 (Self Portrait) — original code, names, assets, and layout.
- Shipped release notes: repo root [`CHANGELOG.md`](../CHANGELOG.md), [`docs/versions.json`](../docs/versions.json).
