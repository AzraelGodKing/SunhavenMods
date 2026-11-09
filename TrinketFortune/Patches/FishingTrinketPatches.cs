using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Wish;

namespace TrinketFortune.Patches
{
    /// <summary>
    /// Harmony patches for fishing loot / trinket bias.
    /// - Utilities.RandomItem: bias bonus museum items (5% fishing drop) toward unowned
    /// - RandomFishArray.RandomItem: bias main fish spawn toward unowned aquarium fish
    /// </summary>
    public static class FishingTrinketPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            bool museumPatched = TryPatchUtilitiesRandomItem(harmony);
            TryPatchRandomFishArray(harmony);
            if (!museumPatched)
                TryPatchFishingRodMuseumCallSites(harmony);
        }

        private static bool TryPatchUtilitiesRandomItem(Harmony harmony)
        {
            // Patch bonus museum item selection (FishingRod.fishingMuseumItems.RandomItem<int>()).
            // HarmonyX cannot bind a generic prefix's `ref T __result` to closed RandomItem<int>
            // (IL compile: "Cannot assign method return type System.Int32 to __result type").
            var candidates = FindUtilitiesRandomItemIntMethods();
            if (candidates.Count == 0)
            {
                Plugin.Log.LogWarning(
                    "Could not find Utilities.RandomItem<int> for museum item bias (game update?). " +
                    "Will try FishingRod call-site fallbacks.");
                return false;
            }

            Exception lastError = null;
            foreach (var randomItemMethod in candidates)
            {
                try
                {
                    var prefix = new HarmonyMethod(typeof(FishingTrinketPatches), nameof(RandomItemInt_Prefix));
                    harmony.Patch(randomItemMethod, prefix: prefix);
                    Plugin.Log.LogDebug($"Patched {DescribeMethod(randomItemMethod)} for fishing museum items");
                    return true;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Plugin.Log?.LogDebug($"Prefix patch failed for {DescribeMethod(randomItemMethod)}: {ex.Message}");
                }

                // Postfix fallback — some HarmonyX builds accept ref result only as postfix.
                try
                {
                    var postfix = new HarmonyMethod(typeof(FishingTrinketPatches), nameof(RandomItemInt_Postfix));
                    harmony.Patch(randomItemMethod, postfix: postfix);
                    Plugin.Log.LogDebug($"Patched {DescribeMethod(randomItemMethod)} (postfix) for fishing museum items");
                    return true;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Plugin.Log?.LogDebug($"Postfix patch failed for {DescribeMethod(randomItemMethod)}: {ex.Message}");
                }
            }

            Plugin.Log.LogError($"Failed to patch Utilities.RandomItem<int>: {lastError?.Message}");
            return false;
        }

        private static List<MethodInfo> FindUtilitiesRandomItemIntMethods()
        {
            var found = new List<MethodInfo>();
            Type utilitiesType = null;
            try
            {
                utilitiesType = typeof(Utilities);
            }
            catch
            {
                utilitiesType = AccessTools.TypeByName("Wish.Utilities");
            }

            if (utilitiesType == null)
                return found;

            foreach (var mi in utilitiesType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (mi == null)
                    continue;

                // Accept RandomItem / GetRandomItem / PickRandom (game renames).
                string name = mi.Name ?? string.Empty;
                bool nameMatch =
                    name.Equals("RandomItem", StringComparison.Ordinal)
                    || name.Equals("GetRandomItem", StringComparison.Ordinal)
                    || name.Equals("PickRandom", StringComparison.Ordinal)
                    || name.EndsWith("RandomItem", StringComparison.Ordinal);
                if (!nameMatch)
                    continue;

                MethodInfo closed = null;
                try
                {
                    if (mi.IsGenericMethodDefinition)
                    {
                        var parms = mi.GetParameters();
                        if (parms.Length < 1 || parms.Length > 2)
                            continue;
                        var p0 = parms[0].ParameterType;
                        if (!p0.IsGenericType)
                            continue;
                        var def = p0.GetGenericTypeDefinition();
                        if (def != typeof(IList<>) && def != typeof(List<>) && def != typeof(IEnumerable<>))
                            continue;
                        closed = mi.MakeGenericMethod(typeof(int));
                    }
                    else if (mi.IsGenericMethod)
                    {
                        closed = mi;
                    }
                    else
                    {
                        var parms = mi.GetParameters();
                        if (parms.Length >= 1
                            && parms[0].ParameterType == typeof(IList<int>)
                            && mi.ReturnType == typeof(int))
                        {
                            closed = mi;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogDebug($"RandomItem close failed for {name}: {ex.Message}");
                }

                if (closed != null && !found.Contains(closed))
                    found.Add(closed);
            }

            return found;
        }

        private static void TryPatchRandomFishArray(Harmony harmony)
        {
            MethodInfo randomFishMethod = null;
            try
            {
                randomFishMethod = typeof(RandomFishArray).GetMethod(
                    nameof(RandomFishArray.RandomItem),
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(float) },
                    null);
            }
            catch
            {
                // fall through to name scan
            }

            if (randomFishMethod == null)
            {
                var t = AccessTools.TypeByName("Wish.RandomFishArray") ?? typeof(RandomFishArray);
                foreach (var mi in AccessTools.GetDeclaredMethods(t))
                {
                    if (mi == null || !mi.Name.Contains("RandomItem"))
                        continue;
                    var parms = mi.GetParameters();
                    if (parms.Length == 1 && parms[0].ParameterType == typeof(float))
                    {
                        randomFishMethod = mi;
                        break;
                    }
                }
            }

            if (randomFishMethod == null)
            {
                Plugin.Log.LogWarning("Could not find RandomFishArray.RandomItem");
                return;
            }

            try
            {
                harmony.Patch(
                    randomFishMethod,
                    postfix: new HarmonyMethod(typeof(FishingTrinketPatches), nameof(RandomFishArray_RandomItem_Postfix)));
                Plugin.Log.LogDebug($"Patched {DescribeMethod(randomFishMethod)} for fish bias");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to patch RandomFishArray.RandomItem: {ex.Message}");
            }
        }

        /// <summary>
        /// Last-resort: postfix instance methods on FishingRod that return int and likely pick
        /// museum trinket IDs, so bias still applies if Utilities.RandomItem was renamed away.
        /// </summary>
        private static void TryPatchFishingRodMuseumCallSites(Harmony harmony)
        {
            Type rodType;
            try
            {
                rodType = typeof(FishingRod);
            }
            catch
            {
                rodType = AccessTools.TypeByName("Wish.FishingRod");
            }

            if (rodType == null)
                return;

            int patched = 0;
            foreach (var mi in rodType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (mi == null || mi.IsSpecialName || mi.ReturnType != typeof(int))
                    continue;
                string n = mi.Name ?? string.Empty;
                if (n.IndexOf("Museum", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("Trinket", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("Bonus", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                try
                {
                    harmony.Patch(mi, postfix: new HarmonyMethod(typeof(FishingTrinketPatches), nameof(FishingRodInt_Postfix)));
                    Plugin.Log.LogDebug($"Patched FishingRod.{mi.Name} as museum-bias fallback");
                    patched++;
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogDebug($"FishingRod.{mi.Name} fallback patch failed: {ex.Message}");
                }
            }

            if (patched == 0)
                Plugin.Log?.LogWarning("No FishingRod museum call-site fallbacks patched.");
        }

        /// <summary>
        /// Closed prefix for <c>Utilities.RandomItem&lt;int&gt;</c>. HarmonyX needs a concrete
        /// <c>ref int __result</c> — an open generic <c>ref T __result</c> fails IL compile.
        /// Parameter name <c>list</c> matches Wish.Utilities.RandomItem.
        /// </summary>
        private static bool RandomItemInt_Prefix(IList<int> list, ref int __result)
        {
            return !TryBiasMuseumList(list, ref __result);
        }

        private static void RandomItemInt_Postfix(IList<int> list, ref int __result)
        {
            TryBiasMuseumList(list, ref __result);
        }

        private static void FishingRodInt_Postfix(ref int __result)
        {
            if (!Config.Enabled.Value)
                return;
            var items = FishingRod.fishingMuseumItems;
            if (items == null || items.Count == 0)
                return;
            // Only rewrite when vanilla already picked something from the museum table.
            if (!items.Contains(__result))
                return;
            int pick = DonationHelper.PickBiasedFishingMuseumItem();
            if (pick != 0)
                __result = pick;
        }

        private static bool TryBiasMuseumList(IList<int> list, ref int __result)
        {
            if (!Config.Enabled.Value)
                return false;
            if (list == null || !ReferenceEquals(list, FishingRod.fishingMuseumItems))
                return false;

            int pick = DonationHelper.PickBiasedFishingMuseumItem();
            if (pick == 0)
                return false;

            __result = pick;
            return true;
        }

        private static void RandomFishArray_RandomItem_Postfix(RandomFishArray __instance, float level, ref FishData __result)
        {
            var biased = DonationHelper.PickBiasedFish(__instance, level);
            if (biased != null)
                __result = biased;
        }

        private static string DescribeMethod(MethodInfo mi)
        {
            if (mi == null)
                return "(null)";
            try
            {
                return mi.DeclaringType?.FullName + "." + mi.ToString();
            }
            catch
            {
                return mi.Name;
            }
        }
    }
}
