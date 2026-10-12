# Haven's Almanac

- **Nexus Mods:** [Haven's Almanac](https://www.nexusmods.com/sunhaven/mods/501) ([files tab](https://www.nexusmods.com/sunhaven/mods/501?tab=files))
- **Thunderstore:** [HavensAlmanac](https://thunderstore.io/c/sun-haven/p/AzraelGodKing/HavensAlmanac/)

Haven's Almanac is the Sun Haven mod hub — one place to see what your other
AzraelGodKing mods are telling you today. It ships a compact always-on HUD,
an expandable dashboard, and a morning briefing that pops up after you wake
up so you don't forget today's tasks, birthdays, or museum windows.

## Version

**2.0.1** — published in [`docs/versions.json`](../docs/versions.json). Player-facing store text: [`thunderstore/README.md`](thunderstore/README.md).

## Features

- **Compact HUD (`F4`)** — one-line summary per installed companion mod,
  draggable, position persists across sessions.
- **Full Dashboard (`Ctrl+F5`)** — expandable window with detailed sections
  for every registered provider.
- **Daily Briefing** — fires after player-initialize and on `DayCycle.OnDayStart`
  (with `UIHandler.OnCompleteOvernight` as a fallback) with whatever is
  actually noteworthy today — never filler.
- **Optional auto-dismiss** — set `DailyBriefing → AutoDismissSeconds` to a
  positive number and the briefing closes itself after that many seconds,
  with a live countdown under the Dismiss button.
- **UI Scale** — `Display → UIScale` (0.5–2.5) applies to all three windows.
- **Soft dependencies** — each integration is gated on
  `Chainloader.PluginInfos.ContainsKey(guid)`; missing mods are just skipped.
- **Configuration Manager friendly** — binds to a friendly
  `BepInEx/config/HavensAlmanac.cfg` file but rewires `BaseUnityPlugin.Config`
  via reflection so every entry is editable from the in-game Configuration
  Manager UI.

## Supported Mods

| Companion mod | What the Almanac shows |
|---|---|
| Sun Haven Todo | Active task count, completion %, high-priority tasks |
| Birthday Reminder | Today's NPC birthdays and ungifted count |
| S.M.U.T. (Museum Tracker) | Museum donation progress + near-completion nudge |
| Senpai's Chest | Configured smart chest count |
| The Vault | Stored currency types |
| Haven's Birthright | Current race and active racial bonuses |
| Haven Dev Tools | Player name |
| Crop Optimizer | Live crop forecast summary |
| Gifting Assistant | Daily gift roster progress, pending NPCs, and priority breakdown |

The built-in **Mod Health** provider is always on and surfaces
`SunhavenMods.Shared.VersionChecker` telemetry in the dashboard.

## Config

`BepInEx/config/HavensAlmanac.cfg` (auto-migrated from the legacy
`com.azraelgodking.havensalmanac.cfg` on first load of 1.0.5+).

- `Hotkeys → DashboardToggleKey` / `DashboardRequireCtrl` — default `Ctrl+F5`
- `Hotkeys → HUDToggleKey` — default `F4`
- `HUD → Enabled` / `HUD → PositionX` / `HUD → PositionY`
- `DailyBriefing → Enabled` / `DailyBriefing → AutoDismissSeconds`
- `Updates → CheckForUpdates`
- `Display → UIScale`

## Changelog

### 2026-05-02 (maintainer notes — see root CHANGELOG for shipped highlights)

- **Lifecycle:** Added expected-teardown plugin shutdown diagnostics, explicit scene-handler cleanup, and Harmony unpatch on destroy.
- **Stability:** Main-menu transitions now reset overnight hook state so it re-binds cleanly on the next character load.
- **Perf:** `ModHealthDataProvider` now caches reflected snapshot property handles by snapshot runtime type, removing repeated `GetProperty` lookups each refresh.
- **Release metadata:** `docs/versions.json` includes Nexus mod page [`/mods/501`](https://www.nexusmods.com/sunhaven/mods/501) and `nexus_file_group_id` **7326779** (Files tab → API / file group id) for CI uploads and download stats.
- **Build:** Integration DLL references now use `ProjectReference` to sibling mods (`SunhavenTodo`, `BirthdayReminder`, `SunHavenMuseumUtilityTracker`, `SenpaisChest`, `TheVault`, `HavensBirthright`, `HavenDevTools`, `CropOptimizer`) so `dotnet build` works without copying plugins into `builds/` first. `BirthdayReminder` and `SenpaisChest` were updated the same way for their Todo / museum soft-deps.
- **Briefing: `AutoDismissSeconds` is now actually honored.** The config
  entry has been bound since 1.0.x but `DailyBriefing.cs` never read it, so
  the window sat until you pressed the Dismiss button or Escape. Now the
  window snapshots `Time.unscaledTime` when it opens, ticks a countdown in
  `Update`, and auto-hides when the configured threshold elapses. A small
  "Auto-dismissing in Ns (Xs total)" label appears under the Dismiss button
  so you can see it coming. Unscaled time is used deliberately so the
  countdown keeps moving during overnight fades and UI pauses. `0` still
  means "wait for manual dismiss".
- **Briefing: window no longer opens when there's nothing to say.** The old
  gate counted any provider with `IsReady`, but the always-registered Mod
  Health provider is always ready and never emits briefing content — so the
  window would open to a "Nothing noteworthy today. Have a great day!"
  shell every morning. Added `IModDataProvider.HasBriefingContent` so each
  provider opts in honestly (Mod Health / Vault / Birthright / Chest /
  DevTools / CropOptimizer always return `false`; Todo/Birthday/Museum
  return true only when they actually have something to show). The briefing
  window now gates on `AlmanacDataAggregator.HasAnyBriefingContent` and
  stays closed on quiet days.
- **Briefing: no more orphan section headers.** Providers whose
  `DrawBriefingSection` would return false used to get their
  `{icon} {name}` title drawn anyway, leaving empty stubs under "Good
  Morning!". The briefing now skips every provider with
  `!HasBriefingContent` before emitting the title, and isolates each section
  behind `TryDrawProviderSection` which balances its `GUILayout.BeginVertical`
  using a `try/finally` + `verticalOpened` flag — eliminating the latent
  double-`EndVertical` in the old catch block that could corrupt the IMGUI
  layout stack if a provider threw *after* the inner `EndVertical` ran.
- **Museum briefing: tightened to only fire when near completion.** The
  Museum provider's `DrawBriefingSection` used to return `true`
  unconditionally once the donation manager loaded, so the briefing pinged
  museum progress every single morning — duplicating what the HUD already
  shows. It now only surfaces when within 5 items of completion (using the
  same threshold for `HasBriefingContent` and `DrawBriefingSection` so the
  two can never disagree). Routine daily progress stays on the HUD where
  the persistent state belongs.
- **HUD / Dashboard / Plugin startup: "No supported mods" messaging now
  actually reachable.** Since the Mod Health provider is always registered,
  `_aggregator.InstalledModCount` is never 0 — so the warning log on
  `Plugin.Awake`, the HUD "No mods detected" line, and the Dashboard "No
  mods detected" placeholder were all dead code. Added
  `AlmanacDataAggregator.IntegrationModCount` (provider count excluding Mod
  Health) and swapped all three call sites to use it, plus clearer copy
  listing every companion mod the user could install to light up real data.
- **Docs: config-file path corrected + Crop Optimizer added to supported
  mods list.** `docs/HavensAlmanac/HavensAlmanac.html` had the legacy
  `com.azraelgodking.havensalmanac.cfg` in the Configuration note; now says
  `HavensAlmanac.cfg` with a note about the 1.0.5+ auto-migration. The
  Supported Mods grid and the thunderstore README's bullet list both gained
  a Crop Optimizer card/entry to match what `Plugin.InitializeIntegrations`
  actually registers.
- **Chest provider: reflection handles are now cached.** `ChestDataProvider`
  used to resolve `SenpaisChest.Plugin`, `GetManager`, `GetSaveData`, and
  the `ChestConfigs` member on every 5-second refresh. The provider now
  caches all four (`Type`, `MethodInfo`, `PropertyInfo`/`FieldInfo`) after
  the first successful probe and reuses them for subsequent refreshes, with
  a defensive re-resolve if the manager or save-data concrete type ever
  changes mid-session. Keeps the behavior identical but drops the per-tick
  reflection cost to near-zero.
- **Repo: `_refs/` for local reference only.** Game-derived dnSpy/ILSpy dumps
  are **not** committed (`.gitignore`); maintainers may place optional local
  notes or decompiles there for editor grep. Real code uses reflection only.
- **Docs: Haven's Almanac page + hub card + site search updated for the
  1.0.6 shipped state.** `docs/HavensAlmanac/HavensAlmanac.html` breadcrumb
  date ticked from "Updated Mar 2026" to "Updated Apr 2026"; the Daily
  Briefing feature card now reflects the "only opens when noteworthy" gate
  and mentions the auto-dismiss countdown; the `AutoDismissSeconds` config
  row notes the under-Dismiss countdown label; and a new **Built-in: Mod
  Health** section has been added below Supported Mods explaining what
  the always-on telemetry panel shows and that each mod needs to call
  `VersionChecker.CheckForUpdate` to appear. `docs/index.html` announcement
  banner moved off the ancient `v1.0.3` text + `announce-almanac-v103`
  dismiss key (people who dismissed the v1.0.3 banner were missing every
  refresh since) to `v1.0.6` messaging highlighting Crop Optimizer, quieter
  briefing, and cross-mod Mod Health — new dismiss key
  `announce-almanac-v106-modhealth` so the refresh actually shows up. The
  Haven's Almanac mod card's feature chip bumped from "7 Mod Integrations"
  → "8 Mod Integrations" (Crop Optimizer was added back in the branch but
  the chip hadn't been refreshed) and "Daily Briefing" → "Smart Daily
  Briefing" to match the new gating. `docs/search-index.json` entry gained
  `modhealth version checker telemetry auto-dismiss almanac` keywords so
  Ctrl+K / site-search surfaces the page for those terms.
- **Mod Health: now aggregates telemetry across every loaded mod, not just
  Haven's Almanac.** Each mod compiles its own copy of
  `SunhavenMods.Shared.VersionChecker` into its DLL (via the
  `<Compile Include="..\SharedUtilities\VersionChecker.cs">` link pattern +
  suppressed CS0436), which means every mod has its own private static
  `HealthByPluginGuid` dictionary holding exactly one entry: itself. The
  old `ModHealthDataProvider` only queried Haven's Almanac's copy, so the
  dashboard's Mod Health panel always showed `1 checked / 0 issues` with a
  single row for the Almanac itself — even though 7 other mods were
  successfully running their own version checks on startup.
  `ModHealthDataProvider` now reflects over every loaded assembly, finds
  each copy of `SunhavenMods.Shared.VersionChecker`, grabs the private
  static `HealthByPluginGuid` field, and merges every `(guid, snapshot)`
  into one view — keeping the freshest `LastCheckUtc` and max
  `ExceptionCount` per GUID. Results are cached keyed on
  `AppDomain.CurrentDomain.GetAssemblies().Length` (assemblies don't churn
  after chainload, but it's cheap to re-scan if one ever does), and
  friendly display names come from `Chainloader.PluginInfos[guid].Metadata.Name`
  so rows read as `"Sun Haven Todo"` / `"The Vault"` / etc. instead of raw
  GUIDs. Mods that don't call `VersionChecker.CheckForUpdate` still won't
  appear (Crop Optimizer currently doesn't register a self-snapshot — that
  will be fixed in a separate Crop Optimizer branch).

## Links

- [Documentation](https://www.azraelsmods.com/sun-haven/havens-almanac/)
- [Thunderstore](https://thunderstore.io/c/sun-haven/p/AzraelGodKing/HavensAlmanac/)
- [Report Bugs on Discord](https://discord.gg/Vwh2y7qMXv)
