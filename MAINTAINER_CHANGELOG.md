# Maintainer changelog

Internal engineering log: **CI**, **release automation**, **scripts**, **docs infrastructure**, and **non–player-visible** observability. **Do not** duplicate full player-facing release notes here — those belong in [`CHANGELOG.md`](CHANGELOG.md), which is linked for cross-reference.

---

## 2026-10-11

- **Crop Optimizer unused-code cleanup:** Deleted never-constructed `BirthdayIntegration` / `TodoIntegration`, plus their Todo/Birthday `BepInDependency` soft-deps and `ModHealthIntegrationSummary` entries so the health log no longer claims integrations that do not run. Dropped the unused `TodoSoftClient` / `TodoManagerReflection` links from `CropOptimizer.csproj`. Removed unused `CropInstanceRegistry.Unregister` / `IsKnownActive` and `CropTileReflection.MeasureFarmTileScreenPixels`. Vault integration is unchanged. No mod version bump.
- **Gifting Assistant dead-code cleanup:** Removed unused `Plugin.GetWindow`, `GiftGameData.FormatRelationshipHearts` / `GetMaxHeartsForPoints` (UI uses `RelationshipHeartRules.GetMaxHearts`), `GiftingAssistantAlmanacData.CountPendingByPriority` (Almanac counts from `GetSortedRosterEntries`), and `PlayerInventoryReader.HasItem`. Dropped unused `PersistentRunnerBase.cs` and `SceneHelpers.cs` Compile links from `GiftingAssistant.csproj` (still linked by Birthright, Crop Optimizer, and Haven's Mirror). No player-facing change; no version bump.
- **Shared config helper:** The Vault, Haven Dev Tools, Crop Optimizer, and Haven's Birthright now call `ConfigFileHelper.CreateNamedConfig` (same wrapper as Almanac / Gifting) instead of private copies that matched the helper. Dropped unused `AllowUnsafeBlocks` from TheVault, HavensAlmanac, HavensBirthright, and HavenDevTools csproj files (no `unsafe` in the repo).
- **HavenDevTools leftover auth lock:** Startup log and ToggleKey config description no longer say the debug window requires authorization. Almanac `DevToolsDataProvider` always uses the authorized path; removed unreachable `almanac.provider.devtools.notAuthorized` locale keys. Deleted unused `Plugin.IsAuthorized`.

## 2026-10-10

- **SharedUtilities `VaultCsvault3.cs` (AZR-243):** CSVAULT3 format kept free of Unity/BepInEx so `TheVault.Tests` links it directly. PBKDF2-HMAC-SHA256 is implemented in-file (tested against `Rfc2898DeriveBytes`) so it does not depend on Mono's SHA-256 PBKDF2 support. Derived keys are cached per character for the session, so only the first load or save pays the 100,000-iteration cost. `SharedCodeRevision` bumped to `2026.10.10`.
- **SharedUtilities baseline props (AZR-256):** Every mod csproj imports `SharedUtilities/SharedUtilities.Baseline.props` (ConfigFileHelper, SharedCodeRevision, ModDiagnostics, ModHealthReport, ModHealthIntegrationSummary, ReflectionHelper, ReflectionProbe) and lists only its optional shared files. `build-mods.yml` runs `scripts/build/check-sharedutilities-links.mjs` in the setup job, so a shared file linked by no project fails CI.
- **VersionChecker (AZR-257):** `versions.json` is parsed with `MinimalJsonParser` (linked into Haven's Birthright and Haven's Mirror, which lacked it). The download is coordinated through `AppDomain` data so each mod's linked copy reuses one request per launch (30-minute success cache, 1-minute failure backoff). `TryCompareVersions` returns null for unparseable versions. `SharedCodeRevision` bumped to `2026.10.10`.
- **Empty catch stragglers (AZR-254 follow-up):** Haven's Almanac `GameRelationshipReader.ReadRelationshipPoints` now reports through `ReflectionProbe.LogOnce`. The Vault's per-frame close-key probes in `VaultUI` keep swallowing but say why.
- **Shared Todo client (AZR-255):** `SharedUtilities/TodoManagerReflection.cs` (no BepInEx/Unity) binds every Todo member the suite uses (`AddTodo(TodoItem)`, `RemoveTodo`, `ToggleComplete`, `GetTodoById`, `GetAllTodos`, `GetActiveTodos`, `GetData`, `CurrentCharacter`, and the `TodoItem` properties) and exposes `TodoDraft` / `TodoSnapshot` instead of Todo types. `TodoSoftClient` finds the plugin through the Chainloader, loads the right character's list (moved from Gifting Assistant), and saves. Gifting Assistant, Birthday Reminder, Senpai's Chest, and Crop Optimizer use it. Birthday Reminder and Senpai's Chest no longer reference `SunhavenTodo.csproj`, and Birthday Reminder drops its CS0436 suppression. `SunhavenTodo/Tests/TodoManagerReflectionTests.cs` binds the client to the real `TodoManager`, so a Todo rename fails CI. The four clients went from 1,348 to 898 lines, all mod-specific. Haven's Almanac's read-only dashboard still compiles against Todo.
- **Dead Pages site removed (AZR-251, AZR-252):** The `azrael-sunhaven-website` Pages project no longer exists, so `cloudflare-site/` (marketing HTML and the `/api/feedback` Linear intake function), `wrangler.toml`, `.dev.vars.example`, `docs/feedback.js`, and the `cf:pages:*` npm scripts are gone. Reports go through the azraelsmods.com Worker's `/api/report` (mod-website repo), which rejects disallowed or missing Origins before doing any work and requires a server-verified Turnstile token, the controls those tickets recommended. `docs/feedback.html` is now a static list of links to each mod's `#report-form`.
- **Thunderstore duplicate is a skip:** The release gate was calling `thunderstore.io/c/sun-haven/api/v1/package/...`, which 404s, so an already-published version looked unpublished and the upload failed with "Package of the same namespace, name and version already exists". The gate now reads `api/experimental/package` `latest.version_number`. A 400 for that duplicate exits the mod successfully. An empty cached Thunderstore version is not reused.
- **Nexus BBCode:** Removed the `[i](vX.Y.Z)[/i]` header from every `NexusMods-*-BBCode.txt`. `pre-push-build.ps1` no longer rewrites those files on a version bump.
- **Gifting Assistant Nexus paste:** Added `GiftingAssistant/NexusMods-GiftingAssistant-BBCode.txt` (v1.1.1) and listed it in `maintainer-docs/NexusMods-BBCode-Index.txt`. The version sync script already updates `NexusMods-<ModFolder>-BBCode.txt` from the matrix, so this file is picked up on the next bump.
- **Public links:** Replaced `azraelgodking.github.io/SunhavenMod` documentation and Thunderstore `website_url` links with [azraelsmods.com/sun-haven](https://www.azraelsmods.com/sun-haven/) mod pages. Every mod's Thunderstore `website_url`, Thunderstore README, and Nexus BBCode now include that mod page so the store fields can be pasted as-is. In-game update checks now read `docs/versions.json` from the raw `main` branch. Feedback CORS allowlist origin is `https://www.azraelsmods.com`.

- **CI workflows rebuilt on the RimWorld layout:** `build-mods.yml` (PR/`main` compile gate on the self-hosted runner), `release-publish.yml` (dispatch bump → build → GitHub / Thunderstore / Nexus), `reusable-release-mod.yml`, and `live-stats.yml` (Thunderstore + Nexus counts force-pushed to the `stats` branch as `live.json`, no commit on `main`). Removed `build-release-publish.yml`, `update-stats.yml`, `reusable-mod-matrix-setup.yml`, and the empty `workflows/deprecated` folder. Kept `sync-mod-versions.yml` and `remove-stale-github-releases.yml`. No GitHub Pages deploy: the public site stays on Cloudflare (`cloudflare-site/`). Cloudflare stats read `stats/live.json`, then `docs/data/stats-cache.json` on `main`.
- **AZR-437 / AZR-438 / AZR-439** (`fix/azr-437-438-439-sunhaven-bugs`): Vault pointer guard blocks `ItemIcon`/`Slot.OnPointerDown` and `PlayerInput.GetMouseButton*` while the cursor is over the vault window, plus a screen-space hit target. Birthday Reminder no longer calls `UnpatchSelf` on destroy; `OvernightHookUtility` combines day-start delegates instead of replacing the field when a `UnityAction` cast fails. Gifting Assistant and Birthday Reminder gift/player postfixes are `[HarmonyWrapSafe]`. AZR-439: seasonal counts are exact ints through sanitize/migrate (locked by test); no `/100` floor in the 4.2.1 save path. The Feb 2026 report predates that save format. Patch bumps: The Vault 4.2.1→4.2.2, Birthday Reminder 2.1.1→2.1.2, Gifting Assistant 1.1.1→1.1.2.

## 2026-10-09

- **Repo hygiene — orphaned files:** Removed `pt-BR-translation-review.md` (finished review artifact), `repo-management-review-2026-07-02.md` (completed audit), `SunhavenTodo/README.txt` (superseded by `README.md`), and `SunhavenTodo/Tests/TestResults/todo.trx` (committed test-run output with machine/user name). Added `**/TestResults/` to `.gitignore`.
- **Cleanup — deduplicate character-name reflection:** BirthdayReminder (`PlayerPatches.GetCharacterName`) and SunhavenTodo (`PlayerPatches.GetCurrentCharacterName`) replaced private reflection walks with linked `SharedUtilities/GameSaveCharacterName.cs` (same pattern as GiftingAssistant / HavensMirror). BirthdayReminder retains its `player.playerName` fallback in the caller. No behavior change; no mod version bumps.
- **HavenDevTools code-quality:** PersistentRunner heartbeat log demoted from `LogInfo` to `LogDebug` (MOD_LIFECYCLE_AND_LOGGING_CONTRACT §3). Removed dead `devtools.auth.*` locale keys (auth UI removed, `IsAuthorized` hardcoded true). Deleted narrating comments in `Awake()`.

## 2026-10-08

- **AZR-356 / 357 / 358 / 359** (`cursor/azr-356-359-sunhaven-fixes-0135`): One-branch Sunhaven ticket batch. Respec `SkillManaRollback` + `SetActive(..., nodeAmount)`; Trinket Fortune multi-strategy Harmony discovery; Senpai's Chest resolution-aware UIScale + ConstantPixelSize labels; Vault `ScaleWithResolution` / `AutoHighResLayout`. Player notes in `CHANGELOG.md`. Applied patch bumps: Respec 2.1.2→2.1.3, Trinket 2.1.1→2.1.2, Chest 3.1.1→3.1.2, Vault 4.2.0→4.2.1.

## 2026-10-04

- **Website downloads:** Release & Publish uploads the packed zip to `downloads.azraelsmods.com` and records the version in `downloads.json`. Uses `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID`. A missing token does not fail GitHub, Nexus, or Thunderstore.

## 2026-10-02

- **Website catalog:** Added `mod.json` in each published mod folder (name, version, description, GitHub zip, Nexus, Thunderstore). Generated from `docs/versions.json` and existing release assets. No mod version bumps.

## 2026-09-27

- **AZR-348 — The Vault Steam Deck V1:** Touch-sized row controls (≥44px), amount chips (1/5/10/Max), **LB+Y** controller open (`[UI] ControllerOpenEnabled` / Modifier=JoystickButton4 / Button=JoystickButton3), Escape + `UICancel`/`Cancel`/`Close` to dismiss, `[Display] AutoDeckLayout` / `DeckLayoutApplied` one-shot 1.35 scale on Deck-like resolutions. Docs/hub honesty (no D-pad nav claim). **Ship bump 4.1.3 → 4.2.0** (Az yes). Player notes in `CHANGELOG.md`.
- **AZR-348 — GitHub Pages / `docs/` hub:** Hub announce → Vault 4.2.0 Deck; Vault card chips + comparison `Deck`; Vault Parameters rows (`AutoDeckLayout` / `DeckLayoutApplied`); search-index Deck keywords. No Cloudflare site edits for this work.
- **AZR-347 — Haven's Mirror docs page:** Replaced the thin shared-styles shell with a gold/plum portrait theme (`icon.png` hero, seasonal slot cards, folder map, numbered install steps, dark mode). Docs-only; no mod version bump.
- **AZR-346 — Haven's Mirror Thunderstore icon:** CI packs `HavensMirror/thunderstore/icon.png`; resized **512×512 → 256×256** (RGBA) so Thunderstore publish no longer rejects with “Invalid icon dimensions”. Mirrored same file at `docs/HavensMirror/icon.png` (identical source copy for the hub).
- **AZR-346 — Haven's Mirror Nexus + Thunderstore URLs:** `docs/versions.json` → Nexus [`/mods/538`](https://www.nexusmods.com/sunhaven/mods/538), `nexus_file_group_id` **8035296**, Thunderstore [`HavensMirror`](https://thunderstore.io/c/sun-haven/p/AzraelGodKing/HavensMirror/). README + Nexus BBCode + download-nav.
- **AZR-346 — Haven's Mirror release prep:** Cloudflare site page + stats/presentation maps (`havensmirror` → `havens-mirror`); Discord tag→dir case; deploy `DEFAULT_MODS`; Thunderstore `website_url` → docs hub. Player notes in `CHANGELOG.md`.
- **AZR-346 — Haven's Mirror:** New suite mod scaffold (`HavensMirror/`), matrix + `docs/versions.json` entry at **1.0.0**, hub page, workflow dispatch option `havensmirror`, `SuitePluginGuids.HavensMirror`. Player notes in `CHANGELOG.md`.
- **AZR-346 — save-slot gallery folders:** `SaveSlotFolderSync` reads `Wish.GameSave` save-slot `characterData.characterName` (plus `GameSaveCharacterName` for the active character), creates empty `gallery/<name>/` via `CharacterSaveStore` sanitization; empty folders skipped for display; HOWTO rewritten for the install → scan → drop PNGs story.
- **AZR-346 — early OnDestroy:** `MirrorPersistentRunner` (DDOL / PersistentRunnerBase) owns folder sync + reload hotkey; Plugin OnDestroy only Disposes Shelf + UnpatchSelf on expected teardown so dialogue busts survive empty-scene BepInEx teardown.
- **AZR-346 — player bust Present:** Stop treating vanilla `isRefreshBust` (always true from `NPCAI.Interact`) as hide; Present on bust setters, Hide on `CancelDialogue`; fix `NotificationStack.SendNotification` arity for Ctrl+F8 toast.

## 2026-09-26

- **Cleanup:** Removed **Sync Cloudflare site mirror** workflow (`sync-cloudflare-site-mirror.yml`). Pages deploy stays on `cloudflare-site/` via wrangler; no more push-mirror to `azrael-sunhaven-website`. No mod version bumps.

## 2026-09-25

- **Cleanup:** Removed unused **Test — Self-hosted Sunhaven runner** workflow (`release-self-hosted-sunhaven-runner.yml`). Last run was 2026-04; **Release & Publish** already builds on the self-hosted runner and covers dry-run / publish. No mod version bumps.
- **Release gate (RimWorld-style):** `scripts/version/release_version_gate.py` asks Nexus (page + files), GitHub, and Thunderstore for each mod’s current version. If ours is newer, that channel publishes; if the same (or Nexus already ahead), that channel is skipped with success so other mods in the matrix keep releasing. Wired into `reusable-release-mod.yml`. No mod version bumps.
- **Nexus upload parity:** `.github/actions/nexus-upload` pins upload-action beta.10 with `update_mod_version`, `archive_existing_version`, and `primary_mod_manager_download`; Changelog tab via `scripts/version/nexus_post_changelog.py` after a successful file upload (soft-fail).
- **CI fix:** `VaultSaveSystem` missing `System.Security.Cryptography` / `System.Text` usings (SHA256 / Encoding / StringBuilder) broke `thevault` and `havensalmanac` builds.
- **AZR-234 / AZR-103 PR** (`cursor/azr-234-103-sunhaven-todos-7024`): Vault tab buttons own all IMGUI focus states (follow-up to window chrome fix). Feedback Worker passes `LINEAR_PROJECT_ID` / `LINEAR_STATE_ID` into `issueCreate`, ships AzraelGodKing/Sunhaven Mods/Todo UUIDs in `wrangler.toml` `[vars]`, adds `.dev.vars.example`, and classifies Linear auth vs ID failures in 502 responses. Pages secret `LINEAR_API_TOKEN` still required for live create. **TheVault** `4.1.1` → `4.1.2` (patch).
- **AZR-238 / AZR-240 — The Vault save recovery:** `VaultSaveSystem` load/save now uses `SharedUtilities.VaultCryptography` (CSVAULT2 header skip + legacy keys). Failed-load path sets `LoadFailedNoRecoverableData` and blocks `Save()` until `ConfirmStartFreshVault()` (UI button). Added `VaultFailedLoadSavePolicy` + CSVAULT2 header unit tests. **TheVault** `4.1.2` → `4.1.3` (patch — data-loss / recovery).

## 2026-09-22

- **AZR-259 — Sun Haven 3.1.3b:** Trinket Fortune closed Harmony prefix for `Utilities.RandomItem<int>` (`ref int __result`); ApplyPatches isolated so `RandomFishArray.RandomItem` still patches if RandomItem fails. Vault item-id cache probes `Item.ID()` first; Crop Optimizer / Gifting Assistant optional AccessTools probes switched to `GetMethod`/`GetProperty` so HarmonyX does not warn on 3.1.3b misses (`Crop.Harvest`/`npcName`).
- **AZR-247 CI:** `CharacterSaveStore.SanitizeFileName` now uses a portable Windows invalid-char set (not `Path.GetInvalidFileNameChars()`). Linux runners were leaving `:` intact and trimming tabs before replace, so `SanitizeFileName_ReplacesInvalidCharacters` / `SanitizeFileName_ReplacesControlCharsBeforeTrim` failed.

## 2026-09-21

- **Ticket batch** (`feat/azr-239-247-suite-ticket-batch`): AZR-239 CharacterSaveStore safe atomic write + durable flush; AZR-244/245 strict MinimalJsonParser + control-char escapes; AZR-247 `dotnet test` job (net8) on PRs; AZR-234 Vault IMGUI background (blank style + `onNormal`/focus states + border clamp); AZR-246 delete SMUT IconCache fork; AZR-250 stats index degrade + timeouts; AZR-253 remove root `functions/` duplicate; AZR-258 README version check in verify script; AZR-254 ReflectionProbe + Crop empty-catch conversion; AZR-255 `TodoSoftClient` + Crop migration; AZR-256 Baseline.props + shared-link checker + link VaultCryptography; AZR-103 ops note (CF Pages `LINEAR_API_TOKEN` / `LINEAR_TEAM_ID`). `SharedCodeRevision` → `2026.09.21`.
- **Crop Optimizer compile:** Alias `SunhavenMods.Shared.ReflectionProbe` (and `UnityEngine.Object` in the highlight renderer) so UnityEngine name clashes do not fail `dotnet build`.

## 2026-08-30

- **AZR-99 — Docs hub Linear ticket desk:** Live Pages hub (`docs/feedback.html`) POSTs JSON to Cloudflare `/api/feedback`. Worker CORS allowlist: `https://azraelgodking.github.io` plus `localhost` / `127.0.0.1` (any port) for `docs:hub:dev`. Extra origins via optional `FEEDBACK_CORS_ORIGINS`. Required env: `LINEAR_API_TOKEN`, `LINEAR_TEAM_ID`. Optional: `FEEDBACK_RATE_WINDOW_SECONDS` (600), `FEEDBACK_RATE_MAX` (5), `LINEAR_BUG_LABEL_ID`, `LINEAR_FEATURE_LABEL_ID`. Default API origin on the form: `https://azrael-sunhaven-website.pages.dev` (override with `data-feedback-api-origin`, `?api=`, or `localStorage sunhavenmod-feedback-api-origin`). Marketing `cloudflare-site/` HTML teardown is a follow-up. No mod version bumps.

## 2026-08-10

- **Haven's Birthright 3.1.2:** `OnPlayerAddManaPrefix` args renamed to `mana` / `overCapAmount` to match `Wish.Player.AddMana` (HarmonyX name-bind failure was marking essential patches incomplete).

## 2026-07-28

- **Suite patch bumps:** All 13 suite mods `.x.x+1` for bug-risk audit release; `docs/versions.json` changelogs set per mod. Branch `fix/suite-patch-version-bumps`.
- **Suite bug-risk audit (branch `fix/suite-bug-risk-audit`):** Cross-mod pass of crash/corruption paths. Player-facing notes in `CHANGELOG.md`. `SharedCodeRevision` → `2026.07.28` for OvernightHookUtility `TryUnhookOvernightEvent` (rebuild all overnight consumers).
- **Respec:** `SkillResetService.ResetProfession` transactional — node walk + save zeroing in one try; `TryRestoreSnapshot` on failure before returning false.
- **Vault ItemPatches:** `AddCurrencyToVault` returns bool; deposit/auto-deposit rollback; `HasEnough` uses bag+vault; `ResetState` clears skip/auto-deposit/suppress flags.
- **Shared OvernightHookUtility:** Best-effort unhook from both DayCycle.OnDayStart and UIHandler.OnCompleteOvernight; Almanac/Birthday/Todo/Gifting menu + reset paths call it.

## 2026-07-11

- **Suite minor version bump:** All mods touched on this branch bumped Minor; `docs/versions.json` changelog lines updated; `SharedCodeRevision` → `2026.07.11`.
- **S.M.U.T. — wishlist LateUpdate lag (3.1.2):** `ItemImage`/`ItemIcon` LateUpdate postfixes were walking parent-name heuristics every frame and calling `FindByGameItemId` (full museum scan) + `IconCache.GetIcon`/`RefreshLabel` on every tick. Now: shop=`ShopItem` only, gift=`GiftMode` only (frame-cached); `HashSet` museum id index; badge need cache invalidated on donations; badge Apply only when id/visibility changes.
- **pt-BR apply script UTF-8 fix:** `scripts/localization/apply-ptbr-review.ps1` `ConvertTo-JsonString` was iterating UTF-8 bytes as chars (mojibake in JSON). Fixed to iterate Unicode code units; re-applied all 389 reviewed `pt-BR` strings.

## 2026-07-05

- **Haven Dev Tools — Steam ID lock removed:** Dropped SHA-256 Steam ID allowlist; `IsAuthorized` is always true; F11/F6/FPS counter no longer gated. Removed Utility-tab hash generator UI.
- **Mod Health Dashboard:** `SharedCodeRevision`, `ModDiagnostics`, `ModHealthReport`, `ModHealthAggregator` in SharedUtilities. Haven Dev Tools tab bar → **Health | Tools | Suite | Extensions**; Health tab aggregates suite diagnostics, shared-code skew banner, version checker (moved from Utility). Pilot `[Health]` startup reports: Dev Tools, Todo, Vault. Almanac `ModHealthBridgeProvider` registers only when Dev Tools is installed.
- **Suite-wide Dev Tools integration:** `ModHealthIntegrationSummary`, `SuitePluginGuids`, `ModDiagnostics.LogModStartup`; `LogStartupHealth` now registers reports for the Health dashboard. All 13 suite mods emit startup health; Suite tab extended with Crop Optimizer, Faster Races, Respec, Gifting Assistant panels.
- **Suite tab reflection fix:** `ReflectionHelper.FindType` now resolves namespace-qualified types before bare names; added `FindModPlugin`. Fixes GetManager/GetTodoManager resolving HavenDevTools.Plugin instead of target mod.
- **Haven Dev Tools — Currencies tab FPS:** `CurrencyTracker.GetSummary()` was invoked every IMGUI layout/repaint with uncached reflection (`AccessTools`, assembly scan, `GetMethod` per currency). Cached reflection handles; summary throttled to 0.5s; manual Refresh on Tools → Currencies.
- **Haven Dev Tools — respec simulator:** Suite tab Haven's Respec panel calls `HavensRespec.DevTools.RespecDevToolsApi` for dry-run reset preview (estimate, simulate, apply, revert).
- **Haven Dev Tools — Suite mod detection:** `RefreshInstalledMods()` uses `Chainloader.PluginInfos` + assembly fallback; called on first Update and when drawing Suite tab (fixes Respec missing when Dev Tools Awake runs before later plugins).
- **Haven Dev Tools — Respec simulator reflection:** `IsReady` and `ProfessionCount` are static properties on `RespecDevToolsApi`; panel now uses `GetStaticValue` instead of `InvokeStaticMethod`.
- **Haven's Respec — DevTools API:** `RespecDevToolsApi` + controller hooks; removed in-game Simulate button/dialog and `[UI] EnableSimulator` until Respec ships its own UI.
- **S.M.U.T. — museum wishlist overlay:** Harmony postfix on `ItemImage`/`ItemIcon` LateUpdate; orange UGUI badge when `DonationManager.HasDonatedByGameId` is false for a known museum item. Shop (`ShopItem`) and gift-inventory contexts; config `[WishlistOverlay] ShowShopBadges` / `ShowGiftBadges`. Badge label uses `smut.wishlist.badge` (**M**). Hover tooltip appends `smut.wishlist.tooltip`; smaller 14×14 badge.
## 2026-07-07

- **Localization — pt-BR review import:** Applied 389 native-speaker `pt-BR` corrections from `pt-BR-translation-review.md` across 9 suite mods. Added `scripts/localization/apply-ptbr-review.ps1` (and `.py`) to import review markdown in-place without reformatting `strings.json`.

## 2026-07-04

- **CharacterSaveStore — load failures:** `LoadWithBackup` wraps `tryDeserialize` in try/catch and forwards exceptions via `onReadFailure`; added unit test. Fixed `WriteAtomicCore` finally so failed writes delete `.tmp` when `deleteTempInFinally` is true (regression from merged `#90`). `WriteAtomic` / `WriteAtomicBytes` throw `ArgumentNullException` on null content instead of returning false. `LoadTextWithBackup` treats empty primary as a successful read (`primary != null`), matching `LoadWithBackup`.
- **CharacterSaveStore (foundation 1.1):** Added `SharedUtilities/CharacterSaveStore.cs` (sanitize path, atomic `.tmp` → `.bak`/`.backup` rotate write, backup fallback load). Migrated Todo, Gifting Assistant, SMUT, Senpais Chest, Birthday Reminder favorites, The Vault save bytes, and SMUT `PlayerPatches` sanitize. NUnit coverage in `SunhavenTodo.Tests/CharacterSaveStoreTests.cs`. **Fix:** `WriteAtomicCore` no longer swallows I/O exceptions — real failures propagate to each mod's outer `catch` for diagnosable logs. **Review follow-up:** restore replace-then-trim sanitize order; `GetAbsenceReason` + `onReadFailure` on load helpers; FavoriteGiftStore path inside try; remove dead Senpai's Chest loader; Vault `PlayerPatches`/`SecretGifts` save paths consolidated.
- **Mod review triage:** Foundation queue ([`maintainer-docs/reviews/mod-review-foundation-active-2026-07-04.md`](maintainer-docs/reviews/mod-review-foundation-active-2026-07-04.md)); deferred features/new mods ([`maintainer-docs/reviews/mod-review-on-hold-2026-07-04.md`](maintainer-docs/reviews/mod-review-on-hold-2026-07-04.md)). Moved `mod-review-2026-07-*.md` under `maintainer-docs/reviews/`.
- **Crop Optimizer — off-farm FPS:** `CropSceneCache.GetCrops()` filters to `SceneManager.GetActiveScene()`; caches empty scene results for 4s; `CropHoverQuery` fast path caches hover misses (not only hits); `CropHUD.UpdateHoverTooltip()` skips work when the active scene has no crops.

---

## 2026-07-03

- **Haven's Almanac — Relationships dashboard:** Built-in `RelationshipDataProvider` reads `characterData.Relationships`, `NPCManager._npcs`, and `GameSave` dating/marriage flags via Harmony reflection; IMGUI heart grid uses bundled `Assets/Relationships/` sprites (5-wide layout, gold fill / silver milestone slot); `[Relationships]` config section; dashboard opens without companion mods via `HasDashboardSections`. **Refactor:** heart rules/loader/renderer moved to `SharedUtilities/` (`RelationshipHeartLayout.Dashboard` vs `.Compact`); relationship provider strings translated for all 16 locales.
- **Gifting Assistant — heart sprites:** Uses shared `RelationshipHeartRenderer` with `RelationshipHeartLayout.Compact`; single inline strip beside each name.

---

## 2026-07-02 (continued)

- **Haven Dev Tools — NPC relationships:** New **Relationships** Tools sub-tab (`NpcRelationshipEditor`) with hearts, date/platonic/marry/divorce, skip-to-cycle, set primary spouse, and reset-all-hearts. Dev console aliases: `relationship.set`, `relationship.marry`, `relationship.divorce`, `relationship.cycle`. Cheat commands: use **CheatEnabler** (removed in-mod `ForceEnableCheats` / Quantum Console patches).
- **Haven Dev Tools — Marriable tab:** Multi-select romanceable NPCs and marry via `MarryNpcPolygamy` (no divorce-first). Gated on `UltraPolygamyHelper`: BepInEx GUID `vurawnica.sunhaven.polygamy` plus Harmony prefix on `NPCAI.MarryPlayer`. Status line in F11 header (`Poly: Yes/No`).
- **Haven Dev Tools — resizable window:** `DebugWindowLayout` + corner grip; `[DebugWindow] Width` / `Height` config persistence; header bar, mod status on two rows, flexible scroll/list heights in tabs.
- **Field highlights:** `Assets/tile_selection_sheet.png` (3× 36px sprites in one row) via `TileSelectionAssetLoader`; yellow 9-slice corners for dry, green 9-slice corners for unfertilized; probes vanilla sprite border/ppu when available. **Fix:** `CropSceneCache` no longer clears forecast on failed type lookup or backfills bogus ETA/gold; **reconciles** missing forecast rows via `CropGrowthPatch.SyncCropToForecast` for trackable scene crops; skips prune when `FindObjectsOfType` returned instances that failed `IsPresent`; caches `Wish.Crop` type after first resolve; `CropHUD` periodic scene scan drives reconciliation; `GameSelectionHighlightRenderer` retries prototype resolution after early-session misses (1s backoff between attempts); `CropHUD.RebuildCanvas` destroys/recreates highlight renderer; do not skip highlight sync for farm tile `(0,0)`; `TileSelectionAssetLoader` destroys the temp texture on load failure; `HeldItemProbe` caches keyword arrays for name matching. **Packaging:** `CropOptimizer/thunderstore/manifest.json` re-saved UTF-8 without BOM.
- **Haven Dev Tools — input:** `[DebugWindow] PauseGameWhenDebugOpen` default **false**. Built-in console tab: auto-focus + Enter/history keys.
- **Senpai's Chest — CheatEnabler compat:** Removed `PlayerInput.DisableInput` / `AddPauseObject` on config open (`SmartChestUI.BlockGameInput`); was blocking `QuantumConsole.CanOpen`. `ShouldBlockGameInput` yields when QC or uGUI text fields are active; `TextInputFocusGuard` exposes `IsQuantumConsoleActive` / `IsUnityUiTextInputFocused`.
- **Community mod — Ultra Polygamy:** 3.1 port of `vurawnica.sunhaven.polygamy` v0.0.5 under `CommunityMods/UltraPolygamy/` (original behavior; Harmony signature + decompiler fixes only). **Gitignored** and **excluded** from `mod-matrix.json` / release CI. See [`CommunityMods/README.md`](CommunityMods/README.md). **`RequestSleep` fix:** T1/guest beds open sleep dialog directly (`ignoreDialogueOnGoing` + clear stuck dialogue); marriage-bed poly menu unchanged; post-cutscene path still defers to vanilla `StartSleep`.

---

## 2026-06-29

- **Mod review fixes (save + compat):** The Vault `VaultSaveSystem.Load()` tries `.backup` when primary/legacy saves are missing or unreadable; SMUT `DonationSaveSystem.Load()` matches SenpaisChest/Todo backup-first pattern; HavensBirthright shop discount restores listing price in `BuyItem` postfixes; HavenDevTools version checker uses `Chainloader.PluginInfos` instead of compiled-in version tuples. See [`maintainer-docs/reviews/mod-review-2026-07-02.md`](maintainer-docs/reviews/mod-review-2026-07-02.md).
- **Vault load (PR review):** Candidate order canonical → backup → legacy; quarantine corrupt primary when a later candidate loads; reset `_needsReEncryption` per candidate try.
- **Haven's Respec:** **RESET ALL** moved from the top of the skill grid to the left sidebar under **Reset** (`RespecButtonInjector` row layout); `UI.EnableResetAll` restored (default on).
- **Cloudflare mirror:** [`sync-cloudflare-site-mirror.yml`](.github/workflows/sync-cloudflare-site-mirror.yml) authenticates git clone/push with host-scoped `Authorization: Basic` (`x-access-token`) instead of `Bearer`, which GitHub’s git HTTPS rejects and caused `could not read Username for 'https://github.com'` in CI.
- **Version verifier:** [`scripts/version/verify-version-consistency.py`](scripts/version/verify-version-consistency.py) uses [`scripts/lib/Resolve-Python.ps1`](scripts/lib/Resolve-Python.ps1) in the PowerShell wrapper so local dev skips broken `py` launcher entries (e.g. stale self-hosted Actions tool cache) while CI still picks up `setup-python` / runner `python3`.
- **pre-push-build:** `-Mod` accepts mod matrix keys or project folder names/paths (e.g. `.\HavensRespec\` → `havensrespec`).

---

## 2026-06-28

- **Release workflows:** Added `giftingassistant` to the mod `options` list in [`build-release-publish.yml`](.github/workflows/build-release-publish.yml) and [`release-self-hosted-sunhaven-runner.yml`](.github/workflows/release-self-hosted-sunhaven-runner.yml) so single-mod dispatches can target Gifting Assistant (matrix / `mod=all` already included it via `scripts/matrix/mod-matrix.json`).

---

## 2026-05-03

- **`release-self-hosted-sunhaven-runner.yml`:** **`package_step.outputs.zip_path`** is the single source of truth for dry-run artifact upload, GitHub Release assets, Thunderstore `file`, and Nexus preflight fallback; **`test_discord`** packaging now emits `zip_path`; **preflight-success** guards all Nexus upload/retry/backoff steps; **`ignore_discord_notify`** workflow input matches **Release & Publish**; lightweight **Package diagnostics** step before dry-run completion / publishes.
- **`build-release-publish.yml`:** Comments clarify that `builds/` paths are **ephemeral on the runner** (stage → upload-artifact → download → package), not a committed tree — same idea as [`builds/README.md`](builds/README.md).
- **Nexus BBCode files:** Renamed from `NexusMods-BBCode.txt` to **`NexusMods-<ModFolder>-BBCode.txt`** in each project; [`scripts/version/pre-push-build.ps1`](scripts/version/pre-push-build.ps1) and [`scripts/version/stage-version-sync-files.py`](scripts/version/stage-version-sync-files.py) updated; hub list in [`docs/NexusMods-BBCode-Index.txt`](docs/NexusMods-BBCode-Index.txt).
- **Senpai's Chest:** Glob rules on item display names (`*` / `?`), `RuleType.ByNamePattern` + JSON `NamePattern`; group data now stores `NamePatterns` and `By Group` matches item IDs or wildcard patterns. New UI config: `SeparateWildcardRuleInUI` (default false; wildcard editing in Manage Groups unless explicitly separated).
- **Version bump:** `.\scripts\pre-push-build.ps1 -All -Bump patch` — patch bump for all twelve mods (`docs/versions.json`, manifests, Nexus BBCode headers, doc badges, `HavenDevTools` `DebugWindow` tuples, rebuild).

---

## 2026-05-02

- **Workflows:** Added [`reusable-mod-matrix-setup.yml`](.github/workflows/reusable-mod-matrix-setup.yml); **concurrency** groups on **Release & Publish** and **Test — Self-hosted** to reduce racing publishes.
- **Version verifier:** [`scripts/version/verify-version-consistency.py`](scripts/version/verify-version-consistency.py) scans for stray `manifest.json` under each mod directory; **matrix keys** in `scripts/matrix/mod-matrix.json` must exist in `docs/versions.json`; PowerShell script delegates to Python.
- **Mod matrix:** [`scripts/matrix/mod-matrix.json`](scripts/matrix/mod-matrix.json) includes `statsDomId`; [`sync-docs-mod-matrix.js`](scripts/matrix/sync-docs-mod-matrix.js) regenerates [`docs/mod-matrix.json`](docs/mod-matrix.json); [`fetch-stats.js`](scripts/stats/fetch-stats.js) resolves stats IDs from the matrix; reusable matrix setup runs the generator and **`git diff --exit-code`** on `docs/mod-matrix.json`.
- **Sync workflow:** [`sync-mod-versions.yml`](.github/workflows/sync-mod-versions.yml) stages allowlisted paths via [`scripts/version/stage-version-sync-files.py`](scripts/version/stage-version-sync-files.py) (no broad `git add -A` / full-repo `-u`).
- **Release / mirror:** Self-hosted test workflow: Nexus **fail-closed** when publish requested but no upload succeeded; Nexus `upload-action` **v1.0.0-beta.4** aligned with main release workflow; [`sync-cloudflare-site-mirror.yml`](.github/workflows/sync-cloudflare-site-mirror.yml) uses `http.extraHeader` auth instead of token-in-URL; [`update-stats.yml`](.github/workflows/update-stats.yml) falls back to `github.token` when `ADMIN_PUSH_TOKEN` is unset.
- **Build:** [`Directory.Build.props`](Directory.Build.props) — `Nullable` annotations, `TreatWarningsAsErrors` when `GITHUB_ACTIONS` is set; `LangVersion` latest, `Deterministic`, `ContinuousIntegrationBuild`. **Senpai's Chest** suppresses **CS0436** (linked `SceneRootSurvivor` + `SunHavenMuseumUtilityTracker` reference embeds the same type).
- **Policy / governance:** Root [`LICENSE`](LICENSE), [`CONTRIBUTING.md`](CONTRIBUTING.md), [`SECURITY.md`](SECURITY.md), [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md), [`docs/SHARED_CODE_STRATEGY.md`](docs/SHARED_CODE_STRATEGY.md).
- **Hygiene:** Removed duplicate root `HavenDevTools/manifest.json`; `.gitignore` includes **`Decompiled/`** (alongside legacy typo dir); **removed** confusing duplicate `.github/workflows/deprecated/build-release-publish.yml`.

**2026-05-24**

- **Release & Publish (Nexus zip missing after Thunderstore):** With the release jobs finally running, mods failed at **Nexus — resolve and verify zip** with `dist/ directory does not exist` ([run #142](https://github.com/AzraelGodKing/SunhavenMod/actions/runs/27860763313)). Cause: `GreenTF/upload-thunderstore-package@v4.3` is a Docker container action that resets the workspace and wipes the uncommitted `dist/` tree, so the later Nexus step finds no zip. Fix in [`reusable-release-mod.yml`](.github/workflows/reusable-release-mod.yml): `package_step` now stages a copy of the zip in `$RUNNER_TEMP/release-pkg` (outside the workspace), and the Nexus preflight restores `dist/<zip>` from that stage if the workspace was reset.
- **Release & Publish (release stage rewrite):** Root cause of the long-standing "release skipped / 0 steps" — in this repo **any job that transitively depends on the matrix `build` job is auto-skipped unless its `if` begins with `always()`**. Proof from [run #139](https://github.com/AzraelGodKing/SunhavenMod/actions/runs/27860386455): all 12 `release` jobs `skipped` (0 steps) while `Build complete` and the `always()` `Combined release report` ran. Removed `build-gate` and the twelve explicit per-mod caller jobs; replaced with a **single matrix `release` job** (`if: always() && … && needs.build.result == 'success'`, `strategy.matrix.mod: fromJson(needs.setup.outputs.matrix)`) calling [`reusable-release-mod.yml`](.github/workflows/reusable-release-mod.yml). Same matrix the `build` job already fans out on, so `mod=all` → all mods and single-mod → one. `release_aggregate_report` now `needs: [release]`. Removed obsolete `scripts/generate-reusable-release-workflow.py`.
- **Release & Publish:** (superseded) Replaced matrix `release` job (skipped with 0 steps on `mod=all` after matrix `build` — [run #137](https://github.com/AzraelGodKing/SunhavenMod/actions/runs/27860163768)) with twelve explicit jobs calling the reusable workflow; granted `contents: write` per caller after nested `release-mod` was rejected at startup ([run #138](https://github.com/AzraelGodKing/SunhavenMod/actions/runs/27860332770)).
- **Release & Publish:** Fixed release skipped on dispatch: boolean `build_only == false` never matched (GitHub passes `'false'` string); use `!= 'true'` for version/release gates ([run #136](https://github.com/AzraelGodKing/SunhavenMod/actions/runs/27859681812)).
- **Hygiene:** Removed stray `HavenDevTools/manifest.json` again (canonical Thunderstore metadata is `HavenDevTools/thunderstore/manifest.json` only).
- **Localization:** Fixed `scripts/fix-localization-format-specifiers.ps1` — PowerShell `StartsWith([char]0xFEFF)` is always true, which stripped the root `{` and broke five `strings.json` files in CI; use explicit UTF-8 read without the bogus BOM strip.
- **Follow-up:** `DebugWindow` caches IMGUI toolbar/race arrays; localized Crop Optimizer hover tooltip and The Vault `DoorPatches` user-facing strings; `scripts/add-followup-localization-keys.ps1` helper for new keys.
- **Docs:** Rewrote root [`README.md`](README.md), [`CHANGELOG.md`](CHANGELOG.md), and this file — scoped maintainer vs player notes; [`docs/VERSION_AND_RELEASE.md`](docs/VERSION_AND_RELEASE.md) clarifies Python for version checks and hub matrix regen; [`docs/ATOMIC_SAVE_POLICY.md`](docs/ATOMIC_SAVE_POLICY.md) for save temp semantics.
- **README:** Per-mod `### Unreleased` headers converted to dated maintainer sections where applicable.

---

## 2026-04-28

- Established split: `CHANGELOG.md` = players; this file = maintainers.

---

## 2026-04-22 — 2026-04-26 (summary)

- Cloudflare / Pages / mirror workflows and site UX iterations; feedback endpoint and logging shape for triage.
- Hub telemetry presentation (Nexus aggregate display, formatting).

---

## Older entries

Before 2026-04-22, use `git log` and archived workflow README sections for fine-grained history. Large docs-site and CI narratives that previously lived only in the root README were retired in favor of this file + [`CHANGELOG.md`](CHANGELOG.md).
