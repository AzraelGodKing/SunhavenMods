# Mod review — Foundation (active)

**Status:** In progress (2026-07-04)  
**Scope:** Codebase improvements from [`mod-review-2026-07-04.md`](mod-review-2026-07-04.md) §1 only.  
**Deferred work:** [`mod-review-on-hold-2026-07-04.md`](mod-review-on-hold-2026-07-04.md)

Work these in order where possible — later items build on earlier extractions.

| # | Item | Priority | Status | Notes |
|---|------|----------|--------|-------|
| 1.1 | **CharacterSaveStore** in SharedUtilities | Highest | Done | Todo, Gifting Assistant, SMUT, Senpai's Chest, Birthday Reminder favorites, The Vault (sanitize + atomic bytes); SMUT PlayerPatches |
| 1.2 | **Shared Todo integration client** | High | Done | Replace 4 diverged copies; mirror TheVault.Abstractions / VaultModApiBridge pattern |
| 1.3 | **IconCache + UiStyle consolidation** | High | Pending | SMUT → shared IconCache; CropOptimizer + GiftingAssistant → shared UiStyle superset |
| 1.4 | **Vault CSVAULT3 crypto hardening** | Medium | Done | Random IV/salt per save + HMAC; legacy decrypt chain for migration |
| 1.5 | **ReflectionProbe helper** | Medium | Pending | Log first failure per call-site; targets CropTileReflection / CropHoverQuery silent catches |
| 1.6 | **Birthright fallback log throttle** | Low | Pending | `BirthrightRunner.cs:435` + audit other `FindObjectsOfType` fallback sites |
| 1.7 | **IMGUI style allocation audit** | Low | Pending | Confirm no per-frame `new GUIStyle` in SmartChestUI / GiftingWindow OnGUI paths |
| 1.8 | **Grow test surface** | Medium | Pending | NUnit for GiftSuggestionResolver, BonusTransferRules, CostService, MinimalJsonParser, RelationshipHeartRules |
| 1.9 | **JSON layer standardization** | Medium | Pending | Shared MinimalJsonParser + writer; keep per-mod DTOs |
| 1.10 | **Repo hygiene** | Low | In progress | Review docs under `maintainer-docs/reviews/`; verify `.gitignore` for `bin`/`obj`; audit dead `docs/styles.css` |

## Already landed (related)

- Shared **RelationshipHeart** utilities (partial 1.3)
- Crop Optimizer off-farm hover perf (related to 1.7 spirit)
- Haven Dev Tools FPS counter style cache (related to 1.7 spirit)

## Branch convention

One foundation item (or tightly coupled pair) per branch, e.g. `refactor/character-save-store`, `refactor/todo-integration-client`.
