# Mod Overhaul Execution Plan

**Status (2026-07-05):** The original overhaul is **complete**. Phases 1–4 (cross-mod
foundation contracts, per-mod stability/hardening, maintainability/perf, release polish)
shipped as the 2026-06 → 2026-07 releases (every mod at 2.x/3.x/4.x); the contracts now
live in [`MOD_LIFECYCLE_AND_LOGGING_CONTRACT.md`](MOD_LIFECYCLE_AND_LOGGING_CONTRACT.md)
and [`ATOMIC_SAVE_POLICY.md`](ATOMIC_SAVE_POLICY.md), and the `overhaul/*` branches are
retired.

This document is now the **feature and new-mod roadmap**. It consolidates:

- the repo review of 2026-07-05 (feature + new-mod suggestions),
- the surviving parked items from the old Deferred Backlog (none had shipped; several
  are folded into larger ideas below), and
- the still-open ideas from
  [`reviews/mod-review-on-hold-2026-07-04.md`](reviews/mod-review-on-hold-2026-07-04.md).

Codebase/refactor work is tracked separately in
[`reviews/mod-review-foundation-active-2026-07-04.md`](reviews/mod-review-foundation-active-2026-07-04.md)
— this plan only lists foundation items as **gates** where a feature depends on one.

## Work Ownership Rules

- One feature (or one tightly coupled set) per branch: `feature/<mod>-<slug>` for mod
  features, `newmod/<slug>` for new mods, `refactor/<slug>` for foundation items.
- Put shared utilities/contracts/tooling changes in their own branch and merge it
  before the mod branches that need it.
- Keep PRs scoped to one concern and one branch.

## Foundation Gates

From the active foundation queue — features below reference these:

| Gate | Item | Status | Unblocks |
|------|------|--------|----------|
| 1.1 | CharacterSaveStore (SharedUtilities) | **Done** | Any new per-character save (all new mods) |
| 1.2 | Shared Todo integration client | **Done** | Todo-pushing features, Season/festival templates |
| 1.3 | IconCache + UiStyle consolidation | Pending | Large new UIs (ledgers, trackers, config hub) |
| 1.5 | ReflectionProbe helper | **Done** (AZR-254 / #145) | Reflection-heavy mods (Atlas/NPC positions, health panels) |

## Feature Roadmap — Existing Mods

### The Vault
- **Transaction ledger tab** — per-character day-stamped history of deposits,
  withdrawals, auto-deposits, and vault-backed shop spends, with daily net totals.
  Highest impact-per-effort in the suite; the interception points already exist.
- Quick-deposit hotkey ("bank all coins now").
- Per-currency auto-deposit thresholds ("keep 5,000 in bag, bank the rest").
- Optional shared "family vault" across characters (Steam-scoped identity).

### Senpai's Chest
- **Global "Where is my item?" search** — type an item name, list every chest
  (label + area) holding it; reuses the existing rule-scan pass. Pair with a
  one-click "deposit all matching" from the player inventory.
- Rule presets export/import (JSON snippets) so players can share sorting templates.
- Craft-pull missing ingredients from labeled Smart Chests.
- Chest-network overview window.

### S.M.U.T.
- **"Obtainable now" awareness** — flag needed items catchable/minable in the current
  season/weather; push "last chance — season ends in N days" to the Almanac briefing.
- **Sell-guard** — warn/block shipping museum-needed items (highest player value in
  the on-hold review).
- "Where to find" hints per missing item.
- Per-hall completion ETA in the Almanac panel.

### Sun Haven Todo
- Recurring tasks (daily/weekly/seasonal resets).
- Game-calendar due dates with overdue highlighting; surface overdue in the Almanac
  briefing.
- **Festival prep template pack** (absorbs the parked "Festival prep helper").
- Quick-add hotkey without opening the full window. *(Gate: 1.2 for cross-mod pushes.)*

### Gifting Assistant
- **Shopping-list bridge** — when a loved/liked gift isn't in the bag, show its source
  (shop, crop, craft) and, via Senpai's Chest, whether one is sitting in a chest.
- "Buyable today" badge when a suggested gift is in current shop stock.
- Auto-roster builder ("keep everyone below X hearts").

### Crop Optimizer
- **Farm heatmap** (un-parked from the old backlog).
- **Profit planner** — rank plantable seeds by gold/day/tile given days left in the
  season; sort the HUD by profit-per-day.
- Season-end warning for crops that cannot mature before the season change.
- Sprinkler/water coverage overlay.
- Seed shopping list from the planner.

### Haven's Respec
- **Build export/import** — serialize a skill allocation to a shareable string, apply
  after reset (absorbs the skills half of the parked "Loadout Manager").
- Dry-run planner / respec simulator on top of `SkillResetService` snapshot/undo.
- Respec history log (when, tree, cost) on `ResetSnapshot`.

### Haven's Birthright
- On-screen **ability cooldown bar** for active racial abilities.
- In-game tuning UI (sliders instead of cfg editing).
- Per-character config profiles via `CharacterFingerprint`.

### Trinket Fortune
- **Pity meter** — show current boosted drop chance vs. vanilla and which trinkets
  remain; expose as an Almanac provider.

### Haven's Almanac
- **Weather + festival provider** — tomorrow's weather, days until next festival;
  natural host for S.M.U.T. season warnings and Todo due dates.
- Week-ahead planner (birthdays + crop ETAs + todos).
- Copy briefing to clipboard; panel reorder/hide via config.

### Birthday Reminder
- Season-at-a-glance calendar grid.
- Gift-given history log per NPC.

### Haven Dev Tools
- Harmony patch inspector (patches grouped by mod).
- Save-file viewer for suite JSON/vault files.
- Reflection-health panel / mod health dashboard (absorbs the "Mod Health Dashboard"
  standalone idea). *(Gate: 1.5.)*

### Faster Races
- Leave stable. Only revisit for separate mount/boat multipliers if the game exposes
  them.

## New Mod Roadmap

Ranked by leverage on existing suite code and integrations:

1. **Haven's Ledger** (economy tracker) — end-of-day income/expense summary, shipping
   history, per-category earnings, best-earner rankings. Integrates with The Vault
   (balances), Crop Optimizer (projections), Almanac (daily summary provider).
   Absorbs the parked "Shipping forecast + 5-day average" Almanac add-on.
   *(Gates: 1.1 done; 1.3 for UI.)*
2. **Haven's Atlas** (map & NPC finder) — live NPC positions/zone list, custom pins,
   "route for today". Absorbs the parked **NPC Finder Overlay** and **Route Planner
   Board**, and the "NPC Compass" review idea. Killer feature: click an ungifted NPC
   in the Gifting Assistant roster, get pointed to them. *(Gate: 1.5.)*
3. **Recipe Sage** (cooking/crafting tracker) — uncooked/uncrafted recipe checklist,
   ingredient lists, "can cook now" from bag + chest contents via Senpai's Chest.
   Highest code reuse (S.M.U.T. UI + save patterns).
4. **Ranch Hand** (animal & pet tracker) — daily petting/feeding checklist, happiness
   and produce-ready at a glance; Almanac briefing + morning todo. (= "Ranch Roster".)
5. **Angler's Almanac** (fishing companion) — season/weather/location availability for
   uncaught fish, catch log, "catchable today and still needed" wired to S.M.U.T. and
   Trinket Fortune. Informational only — no minigame automation. (= "Fishing Log /
   Tackle Advisor".)
6. **Haven's Wardrobe** (outfit/equipment presets) — save and hotkey-swap
   equipment/clothing sets; the equipment half of the parked "Loadout Manager".

Second tier (keep parked until a first-tier slot opens):

- **Cellar/Preserve Tracker** — fermentation/aging HUD mirroring Crop Optimizer.
- **Mine Companion** — per-visit floor/ore tracker + S.M.U.T. banner on floor enter.
- **Suite Config Hub** — one window for all suite BepInEx toggles + per-character
  profiles. *(Gate: 1.3.)*
- **Museum Wishlist Overlay** — "museum needed" badge in shop/NPC gift UIs via
  S.M.U.T. `DonationManager.HasDonatedByGameId` (may land as a S.M.U.T. feature
  instead of a standalone).

Folded into existing-mod features (no longer standalone): Skill Point Planner →
Haven's Respec dry-run; Mod Health Dashboard → Haven Dev Tools; Season Prep
Assistant → Sun Haven Todo templates + Almanac week-ahead planner.

## Suggested Sequencing

1. **Now (no gates):** Vault transaction ledger; Senpai's Chest item search;
   Trinket Fortune pity meter; Crop Optimizer season-end warning.
2. **After foundation 1.2:** Todo recurring tasks + due dates; festival templates;
   Gifting Assistant shopping-list bridge.
3. **After foundation 1.3:** Haven's Ledger (first new mod); S.M.U.T. sell-guard +
   obtainable-now; Almanac weather/festival provider.
4. **After foundation 1.5:** Haven's Atlas; Dev Tools health panels.
5. **Then:** Recipe Sage, Ranch Hand, Angler's Almanac, Haven's Wardrobe as capacity
   allows.

## Per-Branch Acceptance Checklist

- Builds for touched mod(s) pass locally.
- No new lint issues in touched files.
- Lifecycle teardown and character/scene transitions tested.
- Logging remains readable in default config (debug noise behind toggles).
- New per-character saves go through `CharacterSaveStore` and follow
  [`ATOMIC_SAVE_POLICY.md`](ATOMIC_SAVE_POLICY.md).
- Changelog entries follow player-facing vs maintainer split policy.
