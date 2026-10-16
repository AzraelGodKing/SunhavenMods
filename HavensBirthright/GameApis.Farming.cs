using SunhavenMods.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HavensBirthright
{
    /// <summary>
    /// Wish Crop/DayCycle types, TileManager / farming tile reflection, and inventory bulk helpers for Infernal Forge scan.
    /// </summary>
    internal static partial class GameApis
    {
        internal static Type CropType { get; private set; }
        internal static Type DayCycleType { get; private set; }

        internal static Type TileManagerType;
        internal static object TileManagerInstance;
        internal static MethodInfo IsWateredMethod;
        internal static MethodInfo WaterTileMethod;
        internal static FieldInfo FarmingDataField;
        internal static FieldInfo FarmingTileMapField;
        internal static MethodInfo WorldToCellMethod;
        internal static object GridInstance;
        internal static MethodInfo GridCellToWorldMethod;

        private static bool _wishCachesInitialized;

        private static MethodInfo _cachedGetAmountMethod;
        private static MethodInfo _cachedRemoveItemMethod;
        private static bool _inventoryMethodsCached;

        internal static void EnsureWishCachesInitialized()
        {
            if (_wishCachesInitialized)
                return;
            _wishCachesInitialized = true;

            try
            {
                CropType = ReflectionHelper.FindWishType("Crop");
                DayCycleType = ReflectionHelper.FindWishType("DayCycle");

                if (CropType != null)
                    Plugin.Log.LogInfo("[GameApis] Found Crop type");
                if (DayCycleType != null)
                    Plugin.Log.LogInfo("[GameApis] Found DayCycle type");

                CacheTileManager();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[GameApis] Wish cache init failed: {ex.Message}");
            }
        }

        internal static void ResetWishCachesInitializedFlag()
        {
            _wishCachesInitialized = false;
        }

        private static void CacheTileManager()
        {
            try
            {
                TileManagerType = ReflectionHelper.FindWishType("TileManager");
                if (TileManagerType == null)
                {
                    Plugin.Log.LogWarning("[GameApis] TileManager type not found");
                    return;
                }

                IsWateredMethod = TileManagerType.GetMethod("IsWatered", new[] { typeof(Vector2Int) });
                WaterTileMethod = TileManagerType.GetMethod("Water", new[] { typeof(Vector2Int), typeof(short) });

                FarmingDataField = TileManagerType.GetField("farmingData",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FarmingTileMapField = TileManagerType.GetField("farmingTileMap",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                Plugin.Log.LogInfo($"[GameApis] TileManager cached — " +
                    $"IsWatered:{IsWateredMethod != null}, Water:{WaterTileMethod != null}, " +
                    $"farmingData:{FarmingDataField != null}");

                TryGetTileManagerInstance();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[GameApis] TileManager cache failed: {ex.Message}");
            }
        }

        internal static void TryGetTileManagerInstance()
        {
            if (TileManagerInstance != null)
            {
                if (TileManagerInstance is UnityEngine.Object unityObj && unityObj == null)
                {
                    Plugin.Log?.LogInfo("[GameApis] TileManager instance stale — clearing");
                    TileManagerInstance = null;
                }
                else
                    return;
            }

            if (TileManagerType == null) return;

            try
            {
                TileManagerInstance = ReflectionHelper.GetSingletonInstance(TileManagerType);
                if (TileManagerInstance != null)
                {
                    Plugin.Log?.LogInfo("[GameApis] TileManager instance acquired");

                    if (WorldToCellMethod == null && FarmingTileMapField != null)
                    {
                        try
                        {
                            var tilemap = FarmingTileMapField.GetValue(TileManagerInstance);
                            if (tilemap != null)
                            {
                                WorldToCellMethod = tilemap.GetType().GetMethod("WorldToCell", new[] { typeof(Vector3) });
                                Plugin.Log?.LogInfo($"[GameApis] WorldToCell: {(WorldToCellMethod != null ? "ok" : "missing")}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log?.LogWarning($"[GameApis] WorldToCell cache failed: {ex.Message}");
                        }
                    }

                    if (GridInstance == null && FarmingTileMapField != null)
                    {
                        try
                        {
                            var tilemap = FarmingTileMapField.GetValue(TileManagerInstance);
                            if (tilemap != null)
                            {
                                var layoutGridProp = tilemap.GetType().GetProperty("layoutGrid");
                                GridInstance = layoutGridProp?.GetValue(tilemap);
                                if (GridInstance != null)
                                {
                                    GridCellToWorldMethod = GridInstance.GetType().GetMethod("CellToWorld",
                                        new[] { typeof(Vector3Int) });
                                    Plugin.Log?.LogInfo($"[GameApis] Grid CellToWorld: {(GridCellToWorldMethod != null ? "ok" : "missing")}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log?.LogWarning($"[GameApis] Grid cache failed: {ex.Message}");
                        }
                    }
                }
                else
                    Plugin.Log?.LogInfo("[GameApis] TileManager singleton null");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[GameApis] TileManager instance lookup failed: {ex.Message}");
            }
        }

        private static void ResetFarmingCaches()
        {
            TileManagerInstance = null;
            WorldToCellMethod = null;
            GridInstance = null;
            GridCellToWorldMethod = null;
            _cachedGetAmountMethod = null;
            _cachedRemoveItemMethod = null;
            _inventoryMethodsCached = false;
            Plugin.Log?.LogDebug("[GameApis] Farming + inventory reflection reset");
        }

        /// <summary>TileManager/inventory scan state — use on scene transitions without clearing notification stack.</summary>
        internal static void ResetFarmingTileAndInventoryScanState()
        {
            ResetFarmingCaches();
            ResetWishCachesInitializedFlag();
        }

        internal static void CacheInventoryMethods(object inventory)
        {
            try
            {
                var invType = inventory.GetType();
                _cachedGetAmountMethod = invType.GetMethod("GetAmount", new[] { typeof(int) })
                    ?? invType.GetMethod("GetItemAmount", new[] { typeof(int) });
                _cachedRemoveItemMethod = invType.GetMethod("RemoveAll", new[] { typeof(int) });

                Plugin.Log.LogInfo($"[GameApis] Inventory methods — GetAmount:{_cachedGetAmountMethod?.Name ?? "null"}, Remove:{_cachedRemoveItemMethod?.Name ?? "null"}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[GameApis] CacheInventoryMethods failed: {ex.Message}");
            }
        }

        internal static void EnsureInventoryMethodsCached(object inventory)
        {
            if (!_inventoryMethodsCached)
            {
                CacheInventoryMethods(inventory);
                _inventoryMethodsCached = true;
            }
        }

        internal static int GetInventoryAmount(object inventory, int itemId)
        {
            try
            {
                if (_cachedGetAmountMethod != null)
                {
                    var result = _cachedGetAmountMethod.Invoke(inventory, new object[] { itemId });
                    if (result is int count)
                        return count;
                }

                var amount = ReflectionHelper.InvokeMethod(inventory, "GetAmount", itemId);
                if (amount is int amt)
                    return amt;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[GameApis] GetInventoryAmount failed for {itemId}: {ex.Message}");
            }
            return 0;
        }

        internal static bool RemoveInventoryItem(object inventory, int itemId, int amount)
        {
            try
            {
                if (_cachedRemoveItemMethod == null)
                    return false;

                int currentCount = GetInventoryAmount(inventory, itemId);
                if (currentCount < amount)
                    return false;

                int leftover = currentCount - amount;
                _cachedRemoveItemMethod.Invoke(inventory, new object[] { itemId });

                if (leftover > 0)
                    AddInventoryItem(inventory, itemId, leftover);

                return true;
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                Plugin.Log?.LogWarning($"[GameApis] RemoveInventoryItem: {inner.Message}");
                return false;
            }
        }

        internal static void AddInventoryItem(object inventory, int itemId, int amount)
        {
            try
            {
                var addMethod = GetAddItemIntMethod(inventory);
                if (addMethod != null)
                    InvokeAddItem(addMethod, inventory, itemId, amount, false);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[GameApis] AddInventoryItem failed: {ex.Message}");
            }
        }

        /// <summary>Full save/load reset: notifications, item reflection, TileManager, and Wish type re-init.</summary>
        internal static void ResetAllApiCaches()
        {
            ResetItemAndNotificationCaches();
            ResetFarmingTileAndInventoryScanState();
        }
    }
}
