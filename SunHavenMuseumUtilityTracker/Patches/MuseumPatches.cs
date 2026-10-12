using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using SunHavenMuseumUtilityTracker.Data;

namespace SunHavenMuseumUtilityTracker.Patches
{
    /// <summary>
    /// Reads museum bundle completion from GameSave world progress and syncs the tracker.
    /// The game only stores bundle completion; individual donations are tracked manually
    /// or via HungryMonster patches.
    /// </summary>
    public static class MuseumPatches
    {
        // GameSave progress access (reflection only — avoids compile-time Sirenix dependency from GameSave)
        private static Type _gameSaveRuntimeType;
        private static Func<object> _gameSaveInstanceGetter;
        private static Func<object, bool> _currentWorldChecker;
        private static Func<object, string, int> _getProgressIntWorld;
        private static Func<object, string, bool> _getProgressBoolWorld;

        /// <summary>
        /// Sync our tracker with the game's existing museum progress (bundle-level only).
        /// NOTE: This only syncs COMPLETED bundles, not individual items.
        /// </summary>
        public static void SyncWithGameProgress(bool verboseLogging = true)
        {
            try
            {
                var manager = Plugin.GetDonationManager();
                if (manager == null)
                {
                    if (verboseLogging)
                        Plugin.Log?.LogWarning("[SYNC] DonationManager is null");
                    return;
                }

                if (verboseLogging)
                    Plugin.Log?.LogInfo("[SYNC] ======= Starting sync with game progress =======");

                if (!TryGetGameSaveForWorldProgress(out _))
                {
                    if (verboseLogging)
                        Plugin.Log?.LogWarning("[SYNC] Game progress not available - cannot sync (world not loaded yet)");
                    return;
                }

                if (verboseLogging)
                {
                    Plugin.Log?.LogInfo("[SYNC] NOTE: The game only stores BUNDLE completion, not individual items.");
                    Plugin.Log?.LogInfo("[SYNC] This will mark ALL items in completed bundles as donated.");
                }

                int bundlesSynced = SyncCompletedBundles(manager, verboseLogging);

                if (verboseLogging)
                {
                    Plugin.Log?.LogInfo($"[SYNC] Synced {bundlesSynced} completed bundles");
                    Plugin.Log?.LogInfo("[SYNC] ======= Sync complete =======");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[SYNC] Error syncing with game progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the donation count for a bundle from the game's progress system.
        /// Returns -1 if unable to read.
        /// </summary>
        public static int GetBundleDonationCount(string progressKey)
        {
            try
            {
                if (!TryGetGameSaveForWorldProgress(out var gs))
                    return -1;

                return _getProgressIntWorld(gs, progressKey + "complete");
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Check if a bundle is marked as complete in the game's progress system.
        /// Matches in-game behavior: progress bool OR donated slot count &gt;= required (see HungryMonster / MuseumCurator.GetMuseumProgress).
        /// </summary>
        public static bool IsBundleCompleteInGame(string progressKey)
        {
            var items = MuseumContent.GetItemsInBundle(progressKey);
            return IsBundleCompleteInGame(progressKey, items.Count);
        }

        private static bool IsBundleCompleteInGame(string progressKey, int requiredSlots)
        {
            try
            {
                if (!TryGetGameSaveForWorldProgress(out var gs))
                    return false;

                if (_getProgressBoolWorld(gs, progressKey))
                    return true;

                if (requiredSlots <= 0)
                    return false;

                int filled = _getProgressIntWorld(gs, progressKey + "complete");
                return filled >= requiredSlots;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[MuseumPatches] IsBundleCompleteInGame({progressKey}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// World-scoped museum progress requires CurrentWorld (host world or client's loaded world).
        /// </summary>
        private static bool TryGetGameSaveForWorldProgress(out object gs)
        {
            gs = null;
            if (!EnsureGameSaveProgressDelegates())
                return false;

            try
            {
                gs = _gameSaveInstanceGetter?.Invoke();
                if (gs == null || !_currentWorldChecker(gs))
                {
                    gs = null;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[MuseumPatches] TryGetGameSaveForWorldProgress: {ex.Message}");
                gs = null;
                return false;
            }
        }

        private static bool EnsureGameSaveProgressDelegates()
        {
            if (_getProgressIntWorld != null && _getProgressBoolWorld != null &&
                _gameSaveInstanceGetter != null && _currentWorldChecker != null)
                return true;

            try
            {
                _gameSaveRuntimeType = Type.GetType("Wish.GameSave, SunHaven.Core")
                    ?? AccessTools.TypeByName("Wish.GameSave")
                    ?? AccessTools.TypeByName("GameSave");

                if (_gameSaveRuntimeType == null)
                    return false;

                _gameSaveInstanceGetter = CreateGameSaveInstanceGetter(_gameSaveRuntimeType);
                if (_gameSaveInstanceGetter == null)
                    return false;

                var currentWorldProp = _gameSaveRuntimeType.GetProperty("CurrentWorld", BindingFlags.Public | BindingFlags.Instance);
                if (currentWorldProp == null)
                    return false;

                _currentWorldChecker = gs => gs != null && currentWorldProp.GetValue(gs) != null;

                var intM = _gameSaveRuntimeType.GetMethod("GetProgressIntWorld", new[] { typeof(string) });
                var boolM = _gameSaveRuntimeType.GetMethod("GetProgressBoolWorld", new[] { typeof(string) });
                if (intM == null || boolM == null)
                    return false;

                _getProgressIntWorld = (obj, key) => (int)intM.Invoke(obj, new object[] { key });
                _getProgressBoolWorld = (obj, key) => (bool)boolM.Invoke(obj, new object[] { key });
                Plugin.Log?.LogInfo("[MuseumPatches] GameSave world progress delegates ready");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[MuseumPatches] EnsureGameSaveProgressDelegates: {ex.Message}");
                return false;
            }
        }

        private static Func<object> CreateGameSaveInstanceGetter(Type gameSaveType)
        {
            var inst = gameSaveType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (inst != null)
                return () => inst.GetValue(null);

            var singletonOpen = AccessTools.TypeByName("SingletonBehaviour`1");
            if (singletonOpen == null)
                return null;

            var closed = singletonOpen.MakeGenericType(gameSaveType);
            var p = closed.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (p == null)
                return null;

            return () => p.GetValue(null);
        }

        /// <summary>
        /// Checks all known bundle progress keys and marks their items as donated if complete.
        /// </summary>
        private static int SyncCompletedBundles(DonationManager manager, bool verboseLogging)
        {
            int bundlesSynced = 0;

            var bundleIds = MuseumContent.GetAllBundleIds();
            if (verboseLogging)
                Plugin.Log?.LogInfo($"[SYNC] Checking {bundleIds.Count} bundles for completion...");

            foreach (var bundleId in bundleIds)
            {
                try
                {
                    var items = MuseumContent.GetItemsInBundle(bundleId);
                    int gameDonationCount = GetBundleDonationCount(bundleId);
                    bool isComplete = IsBundleCompleteInGame(bundleId, items.Count);

                    if (verboseLogging)
                        Plugin.Log?.LogDebug($"[SYNC] Bundle '{bundleId}': {gameDonationCount}/{items.Count} donated, complete={isComplete}");

                    if (isComplete)
                    {
                        if (verboseLogging)
                            Plugin.Log?.LogInfo($"[SYNC] Bundle '{bundleId}' is COMPLETE, marking all items...");

                        int markedCount = 0;
                        foreach (var item in items)
                        {
                            if (!manager.HasDonated(item.Id))
                            {
                                manager.MarkDonated(item.Id);
                                markedCount++;
                            }
                        }

                        if (markedCount > 0)
                        {
                            if (verboseLogging)
                                Plugin.Log?.LogInfo($"[SYNC] Marked {markedCount} items from bundle '{bundleId}'");
                            bundlesSynced++;
                        }
                    }
                    else if (gameDonationCount > 0)
                    {
                        if (verboseLogging)
                            Plugin.Log?.LogInfo($"[SYNC] Bundle '{bundleId}' has {gameDonationCount}/{items.Count} donations (not complete)");
                    }
                }
                catch (Exception ex)
                {
                    if (verboseLogging)
                        Plugin.Log?.LogDebug($"[SYNC] Error checking bundle '{bundleId}': {ex.Message}");
                }
            }

            return bundlesSynced;
        }
    }

    /// <summary>
    /// Harmony patches for HungryMonster (museum bundle donations).
    /// Tracks items when they're donated to museum bundles in real-time.
    /// </summary>
    public static class HungryMonsterPatches
    {
        private static bool _isHooked = false;

        /// <summary>
        /// Apply patches to HungryMonster for real-time donation tracking.
        /// Call from Plugin.ApplyPatches().
        /// </summary>
        public static void ApplyPatches(Harmony harmony)
        {
            if (_isHooked) return;

            try
            {
                var hungryMonsterType = AccessTools.TypeByName("Wish.HungryMonster") ?? AccessTools.TypeByName("HungryMonster");
                if (hungryMonsterType == null)
                {
                    Plugin.Log?.LogWarning("[HungryMonsterPatches] Could not find HungryMonster type");
                    return;
                }

                // Patch Chest.SaveInventory (declared method) - HarmonyX expects patching base, not override
                var chestType = AccessTools.TypeByName("Wish.Chest") ?? AccessTools.TypeByName("Chest");
                if (chestType != null)
                {
                    var saveInventoryMethod = AccessTools.DeclaredMethod(chestType, "SaveInventory");
                    if (saveInventoryMethod == null)
                        saveInventoryMethod = AccessTools.Method(chestType, "SaveInventory");
                    if (saveInventoryMethod != null)
                    {
                        var postfix = AccessTools.Method(typeof(HungryMonsterPatches), nameof(OnSaveInventory));
                        harmony.Patch(saveInventoryMethod, postfix: new HarmonyMethod(postfix));
                        Plugin.Log?.LogInfo("[HungryMonsterPatches] Patched Chest.SaveInventory (HungryMonster filter in postfix)");
                    }
                    else
                        Plugin.Log?.LogWarning("[HungryMonsterPatches] Could not find Chest.SaveInventory");
                }
                else
                    Plugin.Log?.LogWarning("[HungryMonsterPatches] Could not find Chest type");

                _isHooked = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[HungryMonsterPatches] Error applying patches: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after HungryMonster.SaveInventory() - tracks donated items.
        /// </summary>
        public static void OnSaveInventory(object __instance)
        {
            try
            {
                var instanceType = __instance.GetType();

                // Check if this is a museum bundle (bundleType == MuseumBundle)
                var bundleTypeField = instanceType.GetField("bundleType", BindingFlags.Public | BindingFlags.Instance);
                if (bundleTypeField == null) return;

                var bundleType = bundleTypeField.GetValue(__instance);
                if (bundleType == null) return;

                // BundleType.MuseumBundle = 3
                int bundleTypeValue = (int)bundleType;
                if (bundleTypeValue != 3) return; // Not a museum bundle

                // Get the progress key for this bundle
                var progressTokenField = instanceType.GetField("progressTokenWhenFull", BindingFlags.Public | BindingFlags.Instance);
                if (progressTokenField == null) return;

                var progressToken = progressTokenField.GetValue(__instance);
                if (progressToken == null) return;

                // Get progressID from the Progress object
                var progressIdField = progressToken.GetType().GetField("progressID", BindingFlags.Public | BindingFlags.Instance);
                if (progressIdField == null) return;

                string progressKey = progressIdField.GetValue(progressToken) as string;
                if (string.IsNullOrEmpty(progressKey)) return;

                // Get the selling inventory
                var inventoryField = instanceType.GetField("sellingInventory", BindingFlags.Public | BindingFlags.Instance);
                if (inventoryField == null) return;

                var inventory = inventoryField.GetValue(__instance);
                if (inventory == null) return;

                // Get Items property from inventory
                var itemsProperty = inventory.GetType().GetProperty("Items", BindingFlags.Public | BindingFlags.Instance);
                if (itemsProperty == null) return;

                var items = itemsProperty.GetValue(inventory) as IEnumerable;
                if (items == null) return;

                // Find the corresponding bundle in our content
                var bundle = MuseumContent.FindBundleByProgressKey(progressKey);
                if (bundle == null)
                {
                    Plugin.Log?.LogDebug($"[HungryMonsterPatches] No bundle found for progress key: {progressKey}");
                    return;
                }

                var manager = Plugin.GetDonationManager();
                if (manager == null) return;

                int trackedCount = 0;

                // Iterate through inventory items and mark them as donated
                foreach (var slotData in items)
                {
                    var slotDataType = slotData.GetType();
                    var itemField = slotDataType.GetField("item", BindingFlags.Public | BindingFlags.Instance);
                    if (itemField == null) continue;

                    var item = itemField.GetValue(slotData);
                    if (item == null) continue;

                    // Get item ID
                    var idMethod = item.GetType().GetMethod("ID", BindingFlags.Public | BindingFlags.Instance);
                    if (idMethod == null) continue;

                    int gameItemId = (int)idMethod.Invoke(item, null);

                    var museumItem = MuseumContent.FindByGameItemIdInBundle(gameItemId, progressKey);
                    if (museumItem != null && !manager.HasDonated(museumItem.Id))
                    {
                        manager.MarkDonated(museumItem.Id);
                        trackedCount++;
                        Plugin.Log?.LogInfo($"[HungryMonsterPatches] Auto-tracked donation: {museumItem.Name} (ID: {gameItemId})");
                    }
                }

                if (trackedCount > 0)
                {
                    Plugin.Log?.LogInfo($"[HungryMonsterPatches] Tracked {trackedCount} new donations for bundle '{bundle.Name}'");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[HungryMonsterPatches] Error tracking donation: {ex.Message}");
            }
        }
    }
}
