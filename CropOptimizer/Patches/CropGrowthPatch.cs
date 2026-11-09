using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CropOptimizer.Data;
using CropOptimizer.UI;
using HarmonyLib;
using SunhavenMods.Shared;
using UnityEngine;
using ReflectionProbe = SunhavenMods.Shared.ReflectionProbe;

namespace CropOptimizer.Patches
{
    internal static class CropGrowthPatch
    {
        private static CropForecast _forecast;
        private static bool _growthMembersResolved;
        private static MemberInfo _daysLeftMember;
        private static MemberInfo _percentGrownMember;
        private static MemberInfo _fullyGrownMember;
        private static MemberInfo _currentStageMember;
        private static MemberInfo _totalGrowthMember;
        private static MemberInfo _growthDaysMember;
        private static MemberInfo _fertilizedMember;
        private static MemberInfo _manaInfusedMember;
        private static MemberInfo _cropPositionMember;

        private static bool _itemMembersResolved;
        private static Type _itemDatabaseType;
        private static MethodInfo _itemDatabaseGetItemMethod;
        private static object _itemDatabaseItemsContainer;
        private static MethodInfo _dictTryGetValueMethod;
        private static PropertyInfo _dictIndexerProperty;
        private static PropertyInfo _itemInfoDatabaseInstanceProperty;
        private static FieldInfo _allItemSellInfosField;

        private static bool _qualityMembersResolved;
        private static MemberInfo _cropDataMember;
        private static MemberInfo _qualityMember;

        private static bool _loggedEtaFallback;
        private static bool _loggedSellFallback;
        private static bool _loggedQualityFallback;

        public static void Apply(Harmony harmony, CropForecast forecast)
        {
            _forecast = forecast;
            Type cropType = AccessTools.TypeByName("Wish.Crop");
            if (cropType == null)
            {
                Plugin.Log?.LogWarning("[CropGrowthPatch] Wish.Crop not found");
                return;
            }

            // Sun Haven's Wish.Crop does not expose UpdateGrowth/GrowCrop; growth runs through
            // SetMeta/SetCropSprite/Water/Grow. Patch every resolved entry point once.
            var methodsToPatch = new List<MethodInfo>();
            void TryAdd(MethodInfo m)
            {
                if (m != null)
                    methodsToPatch.Add(m);
            }

            TryAdd(AccessTools.Method(cropType, "SetCropSprite", Type.EmptyTypes));
            var decorationDataType = AccessTools.TypeByName("Wish.DecorationPositionData");
            if (decorationDataType != null)
                TryAdd(AccessTools.Method(cropType, "SetMeta", new[] { decorationDataType }));
            TryAdd(cropType.GetMethod("Water", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
                ?? AccessTools.Method(cropType, "Water", Type.EmptyTypes));
            TryAdd(cropType.GetMethod("Grow", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float) }, null)
                ?? AccessTools.Method(cropType, "Grow", new[] { typeof(float) }));
            // Legacy / alternate builds — use GetMethod so HarmonyX does not warn on 3.1.3b misses
            TryAdd(cropType.GetMethod("UpdateGrowth", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null));
            TryAdd(cropType.GetMethod("GrowCrop", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null));

            methodsToPatch = methodsToPatch
                .GroupBy(m => m.MetadataToken)
                .Select(g => g.First())
                .ToList();

            if (methodsToPatch.Count == 0)
            {
                Plugin.Log?.LogError("[CropGrowthPatch] No Crop methods found to patch — forecast will stay empty.");
                return;
            }

            var postfix = AccessTools.Method(typeof(CropGrowthPatch), nameof(OnAfterCropGrowth));
            if (postfix == null)
            {
                Plugin.Log?.LogWarning("[CropGrowthPatch] Postfix method not found");
                return;
            }

            int patched = 0;
            foreach (var method in methodsToPatch)
            {
                try
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                    patched++;
                    Plugin.Log?.LogInfo($"[CropGrowthPatch] Patched {cropType.Name}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[CropGrowthPatch] Skipped {method.Name}: {ex.Message}");
                }
            }

            if (patched == 0)
                Plugin.Log?.LogError("[CropGrowthPatch] No Harmony patches applied — check game version compatibility.");

            PatchLifecycleMethods(harmony, cropType);
        }

        /// <summary>One-time scan of crops already in the world (e.g. after character load clears the forecast).</summary>
        internal static void RescanExistingCrops()
        {
            if (_forecast == null)
                return;

            try
            {
                Type cropType = AccessTools.TypeByName("Wish.Crop");
                if (cropType == null)
                    return;

                var crops = UnityEngine.Object.FindObjectsOfType(cropType);
                if (crops == null || crops.Length == 0)
                    return;

                int scanned = 0;
                foreach (var crop in crops)
                {
                    if (crop == null)
                        continue;
                    OnAfterCropGrowth(crop);
                    scanned++;
                }

                Plugin.Log?.LogInfo($"[CropGrowthPatch] Rescanned {scanned} existing crop(s) after character load.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[CropGrowthPatch] Existing crop rescan failed: {ex.Message}");
            }
        }

        private static void OnAfterCropGrowth(object __instance)
        {
            SyncCropToForecast(__instance);
        }

        /// <summary>Harmony postfix and scene reconciliation share the same forecast write path.</summary>
        internal static void SyncCropToForecast(object cropInstance)
        {
            if (cropInstance == null || _forecast == null)
                return;

            try
            {
                var unityObj = cropInstance as UnityEngine.Object;
                if (unityObj == null)
                    return;

                int id = unityObj.GetInstanceID();
                CropInstanceRegistry.Register(unityObj);
                float etaHours = TryResolveEtaHours(cropInstance, out bool etaResolved) ? Mathf.Max(0f, _resolvedEtaHoursCache) : 24f;
                float qualityMultiplier = TryResolveQualityMultiplier(cropInstance, out bool qualityResolved) ? _resolvedQualityMultiplierCache : 1f;
                CropShopValue projectedSell = TryResolveProjectedShopValue(cropInstance, qualityMultiplier, out bool sellResolved)
                    ? _resolvedShopValueCache
                    : default;
                TryGetHarvestItemId(cropInstance, out int harvestItemId);

                if (!etaResolved && !_loggedEtaFallback)
                {
                    _loggedEtaFallback = true;
                    Plugin.Log?.LogDebug("[CropGrowthPatch] ETA reflection fallback active; using default 24h for unresolved crops.");
                }
                if (!qualityResolved && !_loggedQualityFallback)
                {
                    _loggedQualityFallback = true;
                    Plugin.Log?.LogDebug("[CropGrowthPatch] Quality reflection fallback active; using default multiplier 1.0.");
                }
                if (!sellResolved && !_loggedSellFallback)
                {
                    _loggedSellFallback = true;
                    Plugin.Log?.LogDebug("[CropGrowthPatch] Sell value reflection fallback active; using default 0 for unresolved crops.");
                }

                _forecast.UpdateCropState(id, etaHours, qualityMultiplier, projectedSell, harvestItemId, sellResolved);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[CropGrowthPatch] Update failed: {ex.Message}");
            }
        }

        private static void PatchLifecycleMethods(Harmony harmony, Type cropType)
        {
            var lifecycleNames = new[]
            {
                "DestroyCrop",
                "OnDestroyed",
                "ApplyRegrowStateAfterHarvest",
                "Harvest",
                "RemoveCrop",
                "Die",
                "OnDestroy"
            };

            var lifecyclePostfix = AccessTools.Method(typeof(CropGrowthPatch), nameof(OnAfterCropLifecycleEnd));
            if (lifecyclePostfix == null)
                return;

            foreach (var methodName in lifecycleNames)
            {
                try
                {
                    var lifecycleMethod = cropType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (lifecycleMethod == null)
                        continue;
                    harmony.Patch(lifecycleMethod, postfix: new HarmonyMethod(lifecyclePostfix));
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogDebug($"[CropGrowthPatch] Lifecycle patch skipped for {methodName}: {ex.Message}");
                }
            }
        }

        private static void OnAfterCropLifecycleEnd(object __instance)
        {
            if (__instance is not UnityEngine.Object unityObj)
                return;

            int instanceId = unityObj.GetInstanceID();
            CropInstanceRegistry.UnregisterById(instanceId);
            _forecast?.RemoveCropState(instanceId);
            CropSceneCache.Invalidate();
            CropHoverQuery.InvalidateHoverAssist();
        }

        private static float _resolvedEtaHoursCache;
        private static float _resolvedQualityMultiplierCache;
        private static CropShopValue _resolvedShopValueCache;

        /// <summary>Live read for UI (hover); safe to call outside Harmony postfix (no shared static ETA cache).</summary>
        internal static bool TryGetTooltipEtaHours(object cropInstance, out float etaHours, out bool resolvedFromReflection)
        {
            return TryComputeEtaHours(cropInstance, out etaHours, out resolvedFromReflection);
        }

        internal static bool TryGetTooltipHarvestItemId(object cropInstance, out int itemId)
        {
            return TryGetHarvestItemId(cropInstance, out itemId);
        }

        internal static bool TryGetItemDisplayName(int itemId, out string displayName)
        {
            displayName = null;
            if (itemId <= 0)
                return false;
            try
            {
                if (!TryGetItemDataObject(itemId, out object itemData) || itemData == null)
                    return false;
                Type t = itemData.GetType();
                var member = FindMember(t, "displayName", "name", "itemName", "Name", "localizedName", "LocalizedName");
                if (member == null)
                    return false;
                object raw = GetMemberValue(itemData, member);
                if (raw == null)
                    return false;
                displayName = raw.ToString();
                return !string.IsNullOrWhiteSpace(displayName);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Tier label ("Normal"/"Silver"/"Gold"/etc.) plus the sell multiplier used by the forecast.</summary>
        internal static bool TryGetTooltipQualityInfo(object cropInstance, out string label, out float multiplier)
        {
            label = "Normal";
            multiplier = 1f;
            if (cropInstance == null)
                return false;

            try
            {
                EnsureQualityMembers(cropInstance.GetType());
                object qualityValue = null;
                if (!TryReadMemberValue(cropInstance, _qualityMember, out qualityValue) || qualityValue == null)
                {
                    if (!TryReadMemberValue(cropInstance, _cropDataMember, out object cropDataObj) || cropDataObj == null)
                        return false;
                    if (!TryReadMemberValue(cropDataObj, _qualityMember, out qualityValue) || qualityValue == null)
                        return false;
                }

                multiplier = MapQualityMultiplier(qualityValue);
                label = FormatQualityLabel(qualityValue, multiplier);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatQualityLabel(object qualityValue, float multiplier)
        {
            if (qualityValue != null)
            {
                string text = qualityValue.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (text.IndexOf("gold", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "Gold";
                    if (text.IndexOf("silver", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "Silver";

                    if (TryConvertToInt(qualityValue, out int code))
                    {
                        switch (code)
                        {
                            case 0: return "Normal";
                            case 1: return "Silver";
                            case 2: return "Gold";
                            default: return code > 0 ? $"Tier {code}" : "Normal";
                        }
                    }

                    return text;
                }
            }

            if (multiplier >= 1.99f) return "Gold";
            if (multiplier >= 1.49f) return "Silver";
            return "Normal";
        }

        /// <summary>
        /// Stage text ("stage N / M") and a 0..1 grown ratio. Ratio is best-effort; ETA is more reliable.
        /// </summary>
        internal static bool TryGetTooltipGrowthStageInfo(object cropInstance, out string stageText, out float grownRatio)
        {
            stageText = null;
            grownRatio = -1f;
            if (cropInstance == null)
                return false;

            try
            {
                EnsureGrowthMembers(cropInstance.GetType());

                // Wish.Crop exposes PercentGrown (Single, 0..1). Prefer that when available.
                if (TryReadNumericMember(cropInstance, _percentGrownMember, out float percent))
                {
                    grownRatio = Mathf.Clamp01(percent);
                    bool haveStageForHint = TryReadNumericMember(cropInstance, _currentStageMember, out float stageHint);
                    stageText = haveStageForHint
                        ? $"{grownRatio * 100f:0}% (stage {Mathf.Max(0f, stageHint):0.#})"
                        : $"{grownRatio * 100f:0}%";
                    return true;
                }

                bool haveStage = TryReadNumericMember(cropInstance, _currentStageMember, out float currentStage);
                bool haveTotal = TryReadNumericMember(cropInstance, _totalGrowthMember, out float totalGrowth);

                if (haveStage && haveTotal && totalGrowth > 0f)
                {
                    float cur = Mathf.Max(0f, currentStage);
                    float tot = Mathf.Max(cur, totalGrowth);
                    grownRatio = Mathf.Clamp01(cur / tot);
                    stageText = $"stage {cur:0.#} / {tot:0.#} ({grownRatio * 100f:0}%)";
                    return true;
                }

                if (haveStage)
                {
                    stageText = $"stage {Mathf.Max(0f, currentStage):0.#}";
                    return true;
                }
            }
            catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/Patches/CropGrowthPatch.cs:385", __ex, Plugin.Log); }
            return false;
        }

        internal static bool TryGetTooltipProjectedShopValue(object cropInstance, out CropShopValue value, out float qualityMultiplier)
        {
            value = default;
            qualityMultiplier = 1f;
            if (cropInstance == null)
                return false;

            try
            {
                if (TryGetTooltipQualityInfo(cropInstance, out _, out float qm))
                    qualityMultiplier = qm;

                if (!TryGetHarvestItemId(cropInstance, out int itemId) || itemId <= 0)
                    return false;

                if (!TryGetShopValue(itemId, out CropShopValue baseValue))
                    return false;

                value = baseValue.Scaled(qualityMultiplier);
                return value.HasAny;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Best-effort item-panel extras for hover (season/regrows/seed/bundle). Missing fields are silently skipped.</summary>
        internal static void AppendItemExtraLines(int itemId, System.Collections.Generic.List<string> lines)
        {
            if (itemId <= 0 || lines == null)
                return;

            try
            {
                if (!TryGetItemDataObject(itemId, out object itemData) || itemData == null)
                    return;
                Type t = itemData.GetType();

                string season = ReadStringLike(itemData, t, "seasons", "Seasons", "season", "Season", "plantableSeasons");
                if (!string.IsNullOrWhiteSpace(season))
                    lines.Add($"Season: {season}");

                bool? regrows = ReadBool(itemData, t, "regrowable", "Regrowable", "regrows", "Regrows", "isRegrowable");
                if (regrows.HasValue)
                    lines.Add($"Regrows: {(regrows.Value ? "yes" : "no")}");

                bool? isSeed = ReadBool(itemData, t, "isSeed", "IsSeed", "seedItem", "SeedItem");
                if (isSeed == true)
                    lines.Add("Type: Seed");

                bool? museum = ReadBool(itemData, t, "isMuseum", "IsMuseum", "museumItem", "museumDonation");
                if (museum == true)
                    lines.Add("Museum: yes");

                bool? bundle = ReadBool(itemData, t, "isBundleItem", "bundleItem", "inBundle", "IsBundleItem");
                if (bundle == true)
                    lines.Add("Bundle: yes");
            }
            catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/Patches/CropGrowthPatch.cs:459", __ex, Plugin.Log); }
        }

        private static string ReadStringLike(object instance, Type t, params string[] names)
        {
            foreach (var name in names)
            {
                var m = FindMember(t, name);
                if (m == null) continue;
                object raw = GetMemberValue(instance, m);
                if (raw == null) continue;
                if (raw is System.Collections.IEnumerable en && !(raw is string))
                {
                    var parts = new System.Collections.Generic.List<string>();
                    foreach (var e in en)
                    {
                        if (e == null) continue;
                        parts.Add(e.ToString());
                    }
                    if (parts.Count > 0)
                        return string.Join(", ", parts);
                }
                else
                {
                    string s = raw.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
            return null;
        }

        private static bool? ReadBool(object instance, Type t, params string[] names)
        {
            foreach (var name in names)
            {
                var m = FindMember(t, name);
                if (m == null) continue;
                object raw = GetMemberValue(instance, m);
                if (raw == null) continue;
                if (raw is bool b) return b;
                if (raw is int i) return i != 0;
                if (raw is float f) return Math.Abs(f) > 0.0001f;
            }
            return null;
        }

        private static bool TryComputeEtaHours(object cropInstance, out float etaHours, out bool resolved)
        {
            resolved = false;
            etaHours = 24f;
            if (cropInstance == null)
                return false;

            try
            {
                EnsureGrowthMembers(cropInstance.GetType());

                if (TryReadBoolMember(cropInstance, _fullyGrownMember, out bool fullyGrown) && fullyGrown)
                {
                    etaHours = 0f;
                    resolved = true;
                    return true;
                }

                float daysLeft;
                if (TryReadNumericMember(cropInstance, _daysLeftMember, out daysLeft))
                {
                    etaHours = Mathf.Max(0f, daysLeft * 24f);
                    resolved = true;
                    return true;
                }

                float currentStage;
                float totalGrowth;
                if (TryReadNumericMember(cropInstance, _currentStageMember, out currentStage) &&
                    TryReadNumericMember(cropInstance, _totalGrowthMember, out totalGrowth))
                {
                    etaHours = Mathf.Max(0f, (totalGrowth - currentStage) * 24f);
                    resolved = true;
                    return true;
                }

                float growthDays;
                if (TryReadNumericMember(cropInstance, _growthDaysMember, out growthDays))
                {
                    etaHours = Mathf.Max(0f, growthDays * 24f);
                    resolved = true;
                    return true;
                }
            }
            catch
            {
                // per-value fallback handled by caller
            }
            return false;
        }

        internal static bool TryReadBoolMember(object instance, MemberInfo member, out bool value)
        {
            value = false;
            if (!TryReadMemberValue(instance, member, out object raw) || raw == null)
                return false;
            if (raw is bool b) { value = b; return true; }
            if (raw is int i) { value = i != 0; return true; }
            if (raw is float f) { value = Math.Abs(f) > 0.0001f; return true; }
            return false;
        }

        /// <summary>Direct reads of <c>Wish.Crop</c> boolean flags (Fertilized / ManaInfused).</summary>
        internal static bool TryGetTooltipFertilized(object cropInstance, out bool fertilized)
        {
            fertilized = false;
            if (cropInstance == null) return false;
            EnsureGrowthMembers(cropInstance.GetType());
            return TryReadBoolMember(cropInstance, _fertilizedMember, out fertilized);
        }

        internal static bool TryGetTooltipManaInfused(object cropInstance, out bool manaInfused)
        {
            manaInfused = false;
            if (cropInstance == null) return false;
            EnsureGrowthMembers(cropInstance.GetType());
            return TryReadBoolMember(cropInstance, _manaInfusedMember, out manaInfused);
        }

        internal static bool TryGetTooltipFullyGrown(object cropInstance, out bool fullyGrown)
        {
            fullyGrown = false;
            if (cropInstance == null) return false;
            EnsureGrowthMembers(cropInstance.GetType());
            return TryReadBoolMember(cropInstance, _fullyGrownMember, out fullyGrown);
        }

        /// <summary>Crop's reported tile/grid coord (Wish.Crop.Position, Vector3Int). Source of truth, no raycast needed.</summary>
        internal static bool TryGetCropGridPosition(object cropInstance, out Vector2Int tile)
        {
            tile = default;
            if (cropInstance == null) return false;
            EnsureGrowthMembers(cropInstance.GetType());
            if (!TryReadMemberValue(cropInstance, _cropPositionMember, out object raw) || raw == null)
                return false;
            if (raw is Vector3Int v3)
            {
                tile = new Vector2Int(v3.x, v3.y);
                return true;
            }
            if (raw is Vector2Int v2)
            {
                tile = v2;
                return true;
            }
            return false;
        }

        private static bool TryResolveEtaHours(object cropInstance, out bool resolved)
        {
            resolved = false;
            _resolvedEtaHoursCache = 24f;
            if (cropInstance == null)
                return false;
            if (!TryComputeEtaHours(cropInstance, out float eta, out resolved) || !resolved)
                return false;
            _resolvedEtaHoursCache = eta;
            return true;
        }

        private static bool TryResolveQualityMultiplier(object cropInstance, out bool resolved)
        {
            resolved = false;
            _resolvedQualityMultiplierCache = 1f;
            if (cropInstance == null)
                return false;

            try
            {
                EnsureQualityMembers(cropInstance.GetType());
                object qualityValue = null;
                if (!TryReadMemberValue(cropInstance, _qualityMember, out qualityValue) || qualityValue == null)
                {
                    if (!TryReadMemberValue(cropInstance, _cropDataMember, out object cropDataObj) || cropDataObj == null)
                        return false;
                    if (!TryReadMemberValue(cropDataObj, _qualityMember, out qualityValue) || qualityValue == null)
                        return false;
                }

                _resolvedQualityMultiplierCache = MapQualityMultiplier(qualityValue);
                resolved = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveProjectedShopValue(object cropInstance, float qualityMultiplier, out bool resolved)
        {
            resolved = false;
            _resolvedShopValueCache = default;
            if (cropInstance == null)
                return false;

            try
            {
                EnsureItemPriceCaches();
                if (!TryGetHarvestItemId(cropInstance, out int itemId) || itemId <= 0)
                    return false;

                if (!TryGetShopValue(itemId, out CropShopValue baseValue))
                    return false;

                _resolvedShopValueCache = baseValue.Scaled(qualityMultiplier);
                resolved = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureGrowthMembers(Type cropType)
        {
            if (_growthMembersResolved || cropType == null)
                return;
            _growthMembersResolved = true;
            // Sun Haven exposes these as properties on Wish.Crop: DaysLeft (Int32), PercentGrown (Single),
            // FullyGrown (Boolean), Fertilized (Boolean), ManaInfused (Boolean), Position (Vector3Int).
            _daysLeftMember = FindMember(cropType, "DaysLeft", "daysLeft", "remainingDays", "daysRemaining");
            _percentGrownMember = FindMember(cropType, "PercentGrown", "percentGrown");
            _fullyGrownMember = FindMember(cropType, "FullyGrown", "fullyGrown");
            _currentStageMember = FindMember(cropType, "_stage", "stage", "currentGrowthStage", "growthStage");
            _totalGrowthMember = FindMember(cropType, "totalGrowthTime", "daysToGrow");
            _growthDaysMember = FindMember(cropType, "growthDays");
            _fertilizedMember = FindMember(cropType, "Fertilized", "fertilized");
            _manaInfusedMember = FindMember(cropType, "ManaInfused", "manaInfused");
            _cropPositionMember = FindMember(cropType, "Position", "position");
        }

        private static void EnsureQualityMembers(Type cropType)
        {
            if (_qualityMembersResolved || cropType == null)
                return;
            _qualityMembersResolved = true;
            _qualityMember = FindMember(cropType, "quality", "cropQuality", "_quality");
            _cropDataMember = FindMember(cropType, "data", "Data", "_data");
            if (_qualityMember == null)
            {
                if (_cropDataMember != null && TryReadMemberType(_cropDataMember, out Type dataType))
                    _qualityMember = FindMember(dataType, "quality", "cropQuality", "_quality");
            }
        }

        /// <summary>
        /// Resolves ItemDatabase / ItemInfoDatabase accessors once. Harvest item id comes from
        /// <see cref="TryGetHarvestItemId"/> (Crop._cropItem.id), not Decoration.id.
        /// </summary>
        private static void EnsureItemPriceCaches()
        {
            if (_itemMembersResolved)
                return;
            _itemMembersResolved = true;

            _itemDatabaseType = AccessTools.TypeByName("Wish.ItemDatabase");
            if (_itemDatabaseType != null)
            {
                _itemDatabaseGetItemMethod = _itemDatabaseType.GetMethod("GetItem", AllMemberFlags, null, new[] { typeof(int) }, null)
                                         ?? _itemDatabaseType.GetMethod("GetItemData", AllMemberFlags, null, new[] { typeof(int) }, null);

                var itemsField = _itemDatabaseType.GetField("Items", AllMemberFlags)
                               ?? _itemDatabaseType.GetField("items", AllMemberFlags)
                               ?? _itemDatabaseType.GetField("_items", AllMemberFlags);
                if (itemsField != null)
                {
                    _itemDatabaseItemsContainer = itemsField.GetValue(null);
                    if (_itemDatabaseItemsContainer != null)
                    {
                        Type containerType = _itemDatabaseItemsContainer.GetType();
                        _dictTryGetValueMethod = containerType.GetMethod("TryGetValue", BindingFlags.Instance | BindingFlags.Public);
                        _dictIndexerProperty = containerType.GetProperty("Item", BindingFlags.Instance | BindingFlags.Public);
                    }
                }
            }

            var itemInfoDatabaseType = AccessTools.TypeByName("Wish.ItemInfoDatabase");
            if (itemInfoDatabaseType != null)
            {
                _itemInfoDatabaseInstanceProperty = itemInfoDatabaseType.GetProperty("Instance", AllMemberFlags)
                                                   ?? itemInfoDatabaseType.GetProperty("instance", AllMemberFlags);
                _allItemSellInfosField = itemInfoDatabaseType.GetField("allItemSellInfos", AllMemberFlags);
                if (_itemInfoDatabaseInstanceProperty == null)
                {
                    var singletonType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                    if (singletonType != null)
                    {
                        Type constructed = singletonType.MakeGenericType(itemInfoDatabaseType);
                        _itemInfoDatabaseInstanceProperty = constructed.GetProperty("Instance", AllMemberFlags)
                                                            ?? constructed.GetProperty("instance", AllMemberFlags);
                    }
                }
            }
        }

        /// <summary>Best-effort item row for tooltips; prefers <c>ItemDatabase.GetItem</c> when available.</summary>
        private static bool TryGetItemDataObject(int itemId, out object itemData)
        {
            itemData = null;
            try
            {
                EnsureItemPriceCaches();

                if (_itemDatabaseGetItemMethod != null)
                {
                    itemData = _itemDatabaseGetItemMethod.Invoke(null, new object[] { itemId });
                    if (itemData != null)
                        return true;
                }

                if (_itemDatabaseItemsContainer != null)
                {
                    if (_dictTryGetValueMethod != null)
                    {
                        var args = new object[] { itemId, null };
                        bool found = (bool)_dictTryGetValueMethod.Invoke(_itemDatabaseItemsContainer, args);
                        if (found)
                        {
                            itemData = args[1];
                            if (itemData != null)
                                return true;
                        }
                    }
                    else if (_dictIndexerProperty != null)
                    {
                        itemData = _dictIndexerProperty.GetValue(_itemDatabaseItemsContainer, new object[] { itemId });
                        if (itemData != null)
                            return true;
                    }
                }

                if (_itemInfoDatabaseInstanceProperty != null && _allItemSellInfosField != null)
                {
                    object instance = _itemInfoDatabaseInstanceProperty.GetValue(null);
                    object dictObj = instance != null ? _allItemSellInfosField.GetValue(instance) : null;
                    if (dictObj is System.Collections.IDictionary idict && idict.Contains(itemId))
                    {
                        itemData = idict[itemId];
                        if (itemData != null)
                            return true;
                    }
                }
            }
            catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/Patches/CropGrowthPatch.cs:813", __ex, Plugin.Log); }

            return false;
        }

        private static bool TryGetHarvestItemId(object cropInstance, out int itemId)
        {
            itemId = 0;
            if (cropInstance == null)
                return false;
            try
            {
                object cropItem = ReflectionHelper.GetInstanceValue(cropInstance, "_cropItem")
                    ?? ReflectionHelper.GetInstanceValue(cropInstance, "cropItem")
                    ?? ReflectionHelper.GetInstanceValue(cropInstance, "item");
                if (cropItem == null)
                    return false;

                object rawId = ReflectionHelper.GetInstanceValue(cropItem, "id");
                if (rawId == null)
                    rawId = ReflectionHelper.GetInstanceValue(cropItem, "ID");

                if (rawId == null)
                    return false;
                return TryConvertToInt(rawId, out itemId) && itemId > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads gold, orbs, and tickets. Nel'Vari / Withergate produce often has gold 0 with
        /// <c>orbSellPrice</c> or <c>ticketSellPrice</c> set on <c>ItemSellInfo</c> / <c>ItemData</c>.
        /// </summary>
        private static bool TryGetShopValue(int itemId, out CropShopValue value)
        {
            value = default;
            try
            {
                EnsureItemPriceCaches();
                CropShopValue acc = default;
                bool found = false;

                if (_itemInfoDatabaseInstanceProperty != null && _allItemSellInfosField != null)
                {
                    object instance = _itemInfoDatabaseInstanceProperty.GetValue(null);
                    object dictObj = instance != null ? _allItemSellInfosField.GetValue(instance) : null;
                    if (dictObj is System.Collections.IDictionary idict && idict.Contains(itemId))
                    {
                        acc = CropShopValue.MergePreferNonZero(acc, ExtractShopValue(idict[itemId]));
                        found = true;
                    }
                }

                if (_itemDatabaseGetItemMethod != null)
                {
                    object itemData = _itemDatabaseGetItemMethod.Invoke(null, new object[] { itemId });
                    if (itemData != null)
                    {
                        acc = CropShopValue.MergePreferNonZero(acc, ExtractShopValue(itemData));
                        found = true;
                    }
                }

                if (_itemDatabaseItemsContainer != null)
                {
                    object itemData = null;
                    if (_dictTryGetValueMethod != null)
                    {
                        var args = new object[] { itemId, null };
                        bool foundInDict = (bool)_dictTryGetValueMethod.Invoke(_itemDatabaseItemsContainer, args);
                        if (foundInDict)
                            itemData = args[1];
                    }
                    else if (_dictIndexerProperty != null)
                    {
                        itemData = _dictIndexerProperty.GetValue(_itemDatabaseItemsContainer, new object[] { itemId });
                    }
                    if (itemData != null)
                    {
                        acc = CropShopValue.MergePreferNonZero(acc, ExtractShopValue(itemData));
                        found = true;
                    }
                }

                value = acc;
                return found;
            }
            catch
            {
                return false;
            }
        }

        private static CropShopValue ExtractShopValue(object itemData)
        {
            if (itemData == null)
                return default;

            int gold = ExtractCurrencyAmount(itemData, "sellPrice", "sellGold");
            int orbs = ExtractCurrencyAmount(itemData, "orbSellPrice", "orbsSellPrice", "sellOrbs");
            int tickets = ExtractCurrencyAmount(itemData, "ticketSellPrice", "ticketsSellPrice", "sellTickets");
            return new CropShopValue(gold, orbs, tickets);
        }

        private static int ExtractCurrencyAmount(object itemData, params string[] memberNames)
        {
            if (itemData == null || memberNames == null)
                return 0;

            var member = FindMember(itemData.GetType(), memberNames);
            if (member == null)
                return 0;

            object raw = GetMemberValue(itemData, member);
            if (raw == null)
                return 0;

            try
            {
                if (raw is float f)
                    return Mathf.Max(0, Mathf.RoundToInt(f));
                if (raw is double d)
                    return Mathf.Max(0, Mathf.RoundToInt((float)d));
                return Mathf.Max(0, Mathf.RoundToInt(Convert.ToSingle(raw)));
            }
            catch
            {
                return 0;
            }
        }

        private static float MapQualityMultiplier(object qualityValue)
        {
            if (qualityValue == null)
                return 1f;

            string text = qualityValue.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (text.IndexOf("gold", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 2f;
                if (text.IndexOf("silver", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 1.5f;
            }

            if (TryConvertToInt(qualityValue, out int code))
            {
                if (code >= 2) return 2f;
                if (code == 1) return 1.5f;
            }

            return 1f;
        }

        private const BindingFlags AllMemberFlags =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.FlattenHierarchy;

        /// <summary>Silent member probe; avoids HarmonyX's AccessTools logging every miss at Warning.</summary>
        private static MemberInfo FindMember(Type type, params string[] names)
        {
            if (type == null || names == null)
                return null;
            for (Type cur = type; cur != null; cur = cur.BaseType)
            {
                foreach (var name in names)
                {
                    try
                    {
                        var field = cur.GetField(name, AllMemberFlags);
                        if (field != null) return field;
                    }
                    catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/Patches/CropGrowthPatch.cs:991", __ex, Plugin.Log); }
                    try
                    {
                        var prop = cur.GetProperty(name, AllMemberFlags);
                        if (prop != null) return prop;
                    }
                    catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/Patches/CropGrowthPatch.cs:997", __ex, Plugin.Log); }
                }
            }
            return null;
        }

        private static bool TryReadMemberType(MemberInfo member, out Type type)
        {
            type = null;
            if (member is FieldInfo fi)
            {
                type = fi.FieldType;
                return type != null;
            }
            if (member is PropertyInfo pi)
            {
                type = pi.PropertyType;
                return type != null;
            }
            return false;
        }

        private static bool TryReadMemberValue(object instance, MemberInfo member, out object value)
        {
            value = null;
            if (instance == null || member == null)
                return false;
            value = GetMemberValue(instance, member);
            return value != null;
        }

        private static object GetMemberValue(object instance, MemberInfo member)
        {
            try
            {
                if (member is FieldInfo fi)
                    return fi.GetValue(instance);
                if (member is PropertyInfo pi)
                    return pi.GetValue(instance, null);
            }
            catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/Patches/CropGrowthPatch.cs:1037", __ex, Plugin.Log); }
            return null;
        }

        private static bool TryReadNumericMember(object instance, MemberInfo member, out float value)
        {
            value = 0f;
            if (!TryReadMemberValue(instance, member, out object raw))
                return false;
            try
            {
                value = Convert.ToSingle(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadIntMember(object instance, MemberInfo member, out int value)
        {
            value = 0;
            if (!TryReadMemberValue(instance, member, out object raw))
                return false;
            return TryConvertToInt(raw, out value);
        }

        private static bool TryConvertToInt(object raw, out int value)
        {
            value = 0;
            if (raw == null)
                return false;
            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
