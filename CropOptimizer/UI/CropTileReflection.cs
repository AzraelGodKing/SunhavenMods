using System;
using System.Collections;
using System.Reflection;
using SunhavenMods.Shared;
using UnityEngine;
using ReflectionProbe = SunhavenMods.Shared.ReflectionProbe;

namespace CropOptimizer.UI
{
    /// <summary>
    /// Lazy-resolves <c>Wish.TileManager</c> and <c>farmingData</c> so the hover tooltip can report
    /// tile-level state (watered / hoed) and tile coordinates. Mirrors the pattern in
    /// <c>HavensBirthright.GameApis.Farming</c>.
    /// </summary>
    internal static class CropTileReflection
    {
        private static bool _initialized;
        private static Type _tileManagerType;
        private static object _tileManagerInstance;
        private static MethodInfo _isWateredMethod;
        private static MethodInfo _isHoedMethod;
        private static MethodInfo _isHoedOrWateredMethod;
        private static FieldInfo _farmingDataField;
        private static FieldInfo _farmingTileMapField;
        private static FieldInfo _waterTileMapField;
        private static FieldInfo _wateredTilesField;
        private static MethodInfo _worldToCellMethod;
        private static MethodInfo _waterWorldToCellMethod;
        private static MethodInfo _waterHasTileMethod;

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;
            _initialized = true;

            try
            {
                _tileManagerType = ReflectionHelper.FindWishType("TileManager");
                if (_tileManagerType == null)
                    return;

                const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                _isWateredMethod = _tileManagerType.GetMethod("IsWatered", new[] { typeof(Vector2Int) });
                _isHoedMethod = _tileManagerType.GetMethod("IsHoed", new[] { typeof(Vector2Int) });
                _isHoedOrWateredMethod = _tileManagerType.GetMethod("IsHoedOrWatered", new[] { typeof(Vector2Int) });
                _farmingDataField = _tileManagerType.GetField("farmingData", instanceFlags);
                _farmingTileMapField = _tileManagerType.GetField("farmingTileMap", instanceFlags);
                _waterTileMapField = _tileManagerType.GetField("waterTileMap", instanceFlags);
                _wateredTilesField = _tileManagerType.GetField("wateredTiles", instanceFlags);
            }
            catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:60", __ex, Plugin.Log); }
        }

        private static object ResolveInstance()
        {
            if (_tileManagerType == null)
                return null;
            if (_tileManagerInstance is UnityEngine.Object uo && uo == null)
                _tileManagerInstance = null;
            if (_tileManagerInstance == null)
            {
                try { _tileManagerInstance = ReflectionHelper.GetSingletonInstance(_tileManagerType); } catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:73", __ex, Plugin.Log); }
            }

            if (_tileManagerInstance != null && _worldToCellMethod == null && _farmingTileMapField != null)
            {
                try
                {
                    var tilemap = _farmingTileMapField.GetValue(_tileManagerInstance);
                    if (tilemap != null)
                    {
                        Type tilemapType = tilemap.GetType();
                        _worldToCellMethod = tilemapType.GetMethod("WorldToCell", new[] { typeof(Vector3) });
                    }
                }
                catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:96", __ex, Plugin.Log); }
            }

            return _tileManagerInstance;
        }

        public static bool TryGetTileCoordForCrop(Component crop, out Vector2Int tile)
        {
            tile = default;
            if (crop == null)
                return false;

            EnsureInitialized();
            object tm = ResolveInstance();
            Vector3 pos = crop.transform.position;

            // Prefer O(1) sources before scanning all farmingData keys (large farms: key scan is costly per hover).
            if (CropOptimizer.Patches.CropGrowthPatch.TryGetCropGridPosition(crop, out Vector2Int cropTile))
            {
                tile = cropTile;
                return true;
            }

            // Fallback: game's WorldToCell on the farming tilemap.
            if (tm != null)
            {
                try
                {
                    if (_worldToCellMethod != null && _farmingTileMapField != null)
                    {
                        var tilemap = _farmingTileMapField.GetValue(tm);
                        if (tilemap != null)
                        {
                            object cell = _worldToCellMethod.Invoke(tilemap, new object[] { pos });
                            if (cell is Vector3Int v)
                            {
                                tile = new Vector2Int(v.x, v.y);
                                return true;
                            }
                        }
                    }
                }
                catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:248", __ex, Plugin.Log); }
            }

            // Last resort: nearest farmingData key to world position (handles offset tilemap / key space).
            if (tm != null && _farmingDataField != null &&
                TryFindNearestFarmingKey(tm, pos, out Vector2Int nearest, maxSqDist: 64f * 64f))
            {
                tile = nearest;
                return true;
            }

            tile = new Vector2Int(Mathf.RoundToInt(pos.x - 0.5f), Mathf.RoundToInt(pos.y - 0.5f));
            return true;
        }

        /// <summary>
        /// Scan <c>farmingData</c> for the Vector2Int key closest (by squared XY distance) to the crop's
        /// world position. Tilemap grids may use non-trivial origin/scale, but the relative order of keys
        /// always matches world space, so the nearest key is the crop's real tile.
        /// </summary>
        private static bool TryFindNearestFarmingKey(object tm, Vector3 worldPos, out Vector2Int tile, float maxSqDist)
        {
            tile = default;
            try
            {
                var dict = _farmingDataField.GetValue(tm) as IDictionary;
                if (dict == null || dict.Count == 0)
                    return false;

                float bestSq = maxSqDist;
                bool found = false;
                foreach (var k in dict.Keys)
                {
                    if (k is Vector2Int v)
                    {
                        float dx = v.x - worldPos.x;
                        float dy = v.y - worldPos.y;
                        float sq = dx * dx + dy * dy;
                        if (sq < bestSq)
                        {
                            bestSq = sq;
                            tile = v;
                            found = true;
                        }
                    }
                }
                return found;
            }
            catch
            {
                return false;
            }
        }

        public static string DescribeFarmingTileState(Vector2Int tile)
        {
            return DescribeFarmingTileState(tile, Vector3.zero, false, out _);
        }

        public static string DescribeFarmingTileState(Vector2Int tile, out Vector2Int matchedTile)
        {
            return DescribeFarmingTileState(tile, Vector3.zero, false, out matchedTile);
        }

        /// <summary>True when the crop's farming tile reads as watered on the water tilemap / TileManager.</summary>
        public static bool IsCropTileWatered(Component crop)
        {
            if (crop == null)
                return true;

            if (!TryGetTileCoordForCrop(crop, out Vector2Int tile))
                return true;

            string state = DescribeFarmingTileState(tile, crop.transform.position, true, out _);
            return string.Equals(state, "watered", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Uses <paramref name="worldPosForWaterMap"/> (when <paramref name="haveWorldPos"/> is true) to
        /// probe <c>TileManager.waterTileMap.HasTile(...)</c> directly — the water tilemap is the real
        /// source of truth for "is this soil wet right now?" and may use a different grid than farmingData.
        /// </summary>
        public static string DescribeFarmingTileState(Vector2Int tile, Vector3 worldPosForWaterMap, bool haveWorldPos, out Vector2Int matchedTile)
        {
            matchedTile = tile;

            EnsureInitialized();
            object tm = ResolveInstance();

            // 1. First-class water signal: waterTileMap.HasTile(waterTileMap.WorldToCell(worldPos)).
            //    The water tilemap owns the wet-soil sprites and uses its own grid, so we go through it
            //    directly instead of trusting IsWatered(Vector2Int) which depends on a shared coord system.
            if (haveWorldPos && tm != null && TryWaterTileMapHasTile(tm, worldPosForWaterMap))
                return "watered";

            if (tm == null)
                return null;

            try
            {
                string result = ProbeTile(tm, tile);
                if (result != null)
                {
                    matchedTile = tile;
                    return result;
                }

                foreach (Vector2Int off in SearchOffsets)
                {
                    Vector2Int t2 = new Vector2Int(tile.x + off.x, tile.y + off.y);
                    result = ProbeTile(tm, t2);
                    if (result != null)
                    {
                        matchedTile = t2;
                        return result;
                    }
                }
            }
            catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:398", __ex, Plugin.Log); }

            return null;
        }

        private static bool TryWaterTileMapHasTile(object tm, Vector3 worldPos)
        {
            try
            {
                if (_waterTileMapField == null) return false;
                object waterMap = _waterTileMapField.GetValue(tm);
                if (waterMap == null) return false;

                if (_waterWorldToCellMethod == null)
                    _waterWorldToCellMethod = waterMap.GetType().GetMethod("WorldToCell", new[] { typeof(Vector3) });
                if (_waterHasTileMethod == null)
                    _waterHasTileMethod = waterMap.GetType().GetMethod("HasTile", new[] { typeof(Vector3Int) });
                if (_waterWorldToCellMethod == null || _waterHasTileMethod == null)
                    return false;

                object cellObj = _waterWorldToCellMethod.Invoke(waterMap, new object[] { worldPos });
                if (cellObj is not Vector3Int cell) return false;

                object has = _waterHasTileMethod.Invoke(waterMap, new object[] { cell });
                return has is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        // 3×3 neighborhood minus (0,0) — covered by the exact probe first.
        private static readonly Vector2Int[] SearchOffsets =
        {
            new Vector2Int(-1,  0), new Vector2Int(1,  0),
            new Vector2Int( 0, -1), new Vector2Int(0,  1),
            new Vector2Int(-1, -1), new Vector2Int(-1, 1),
            new Vector2Int( 1, -1), new Vector2Int( 1, 1),
        };

        private static int NextOffset(int current) => current == -1 ? 0 : current == 0 ? 1 : 2;

        private static string ProbeTile(object tm, Vector2Int tile)
        {
            bool watered = false;
            bool hoed = false;

            // First, ground truth from IsWatered / IsHoed / IsHoedOrWatered.
            if (_isWateredMethod != null)
            {
                try { watered = (bool)_isWateredMethod.Invoke(tm, new object[] { tile }); } catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:451", __ex, Plugin.Log); }
            }
            if (_isHoedMethod != null)
            {
                try { hoed = (bool)_isHoedMethod.Invoke(tm, new object[] { tile }); } catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:455", __ex, Plugin.Log); }
            }

            // Second, scan sibling "water"-looking dicts/sets on TileManager for tile membership.
            // This covers builds where watered state lives outside farmingData (e.g. wateredTiles HashSet).
            if (!watered)
            {
                try { watered = ScanWaterMembersForTile(tm, tile); } catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:462", __ex, Plugin.Log); }
            }

            if (watered) return "watered";
            if (hoed) return "hoed (dry)";

            if (_isHoedOrWateredMethod != null)
            {
                try { if ((bool)_isHoedOrWateredMethod.Invoke(tm, new object[] { tile })) return "hoed or watered"; }
                catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:471", __ex, Plugin.Log); }
            }

            if (_farmingDataField != null)
            {
                try
                {
                    var dict = _farmingDataField.GetValue(tm) as IDictionary;
                    if (dict != null && dict.Contains(tile))
                    {
                        object raw = dict[tile];
                        if (raw != null)
                        {
                            string s = raw.ToString();
                            if (s.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0 || s == "3") return "watered";
                            if (s.Equals("Hoed", StringComparison.OrdinalIgnoreCase) || s == "2") return "hoed (dry)";
                            return s;
                        }
                    }
                }
                catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:491", __ex, Plugin.Log); }
            }
            return null;
        }

        /// <summary>
        /// Scan TileManager's fields for any dictionary or set whose name contains "water", and check
        /// whether it contains this tile. Used as a fallback when <c>IsWatered</c> returns false for tiles
        /// that are visibly watered (some builds store water in a sibling structure).
        /// </summary>
        private static bool ScanWaterMembersForTile(object tm, Vector2Int tile)
        {
            if (tm == null || _tileManagerType == null) return false;
            const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            foreach (FieldInfo fi in _tileManagerType.GetFields(f))
            {
                string n = fi.Name;
                if (n.IndexOf("water", StringComparison.OrdinalIgnoreCase) < 0) continue;
                object val;
                try { val = fi.GetValue(tm); } catch { continue; }
                if (val == null) continue;

                if (val is IDictionary d && d.Contains(tile))
                    return true;
                if (val is ICollection col)
                {
                    // HashSet<Vector2Int> / List<Vector2Int>
                    foreach (object entry in col)
                    {
                        if (entry is Vector2Int v && v == tile)
                            return true;
                    }
                }
            }
            return false;
        }

        public static string BuildDebugSnapshot(Component crop, Vector2Int tile)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[HoverDebug] Tile probe for crop at {crop?.transform?.position} → tile={tile}");
            object tm = ResolveInstance();
            sb.Append($", tm={(tm != null ? tm.GetType().Name : "null")}");

            // Live method invocations on the ACTUAL resolved tile — "yes/no" (exists) vs true/false (returned)
            sb.Append(", IsWatered(tile)=" + InvokeBoolSafe(_isWateredMethod, tm, tile));
            sb.Append(", IsHoed(tile)=" + InvokeBoolSafe(_isHoedMethod, tm, tile));
            sb.Append(", IsHoedOrWatered(tile)=" + InvokeBoolSafe(_isHoedOrWateredMethod, tm, tile));

            // waterTileMap.HasTile probe against the crop's world pos (via its own WorldToCell).
            if (tm != null && crop != null)
            {
                sb.Append(", waterTileMap.HasTile(worldPos)=" +
                    (TryWaterTileMapHasTile(tm, crop.transform.position) ? "true" : "false"));
            }

            try
            {
                if (tm != null && _farmingDataField != null)
                {
                    var dict = _farmingDataField.GetValue(tm) as IDictionary;
                    sb.Append($", farmingData.count={(dict != null ? dict.Count : -1)}");
                    if (dict != null)
                    {
                        object v = dict.Contains(tile) ? dict[tile] : null;
                        sb.Append($", farmingData[tile]={(v == null ? "<missing>" : v.ToString())}");
                    }
                }
            }
            catch (Exception ex) { sb.Append($", farmingDataErr={ex.Message}"); }

            // Scan TileManager for other "water"-looking members so we can locate the real source
            try
            {
                if (tm != null)
                {
                    sb.Append(", waterMembers=[");
                    bool any = false;
                    const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                    foreach (FieldInfo fi in _tileManagerType.GetFields(f))
                    {
                        string n = fi.Name;
                        if (!ContainsAny(n, "water", "Watered", "Watering")) continue;
                        if (any) sb.Append(','); any = true;
                        sb.Append($"{fi.FieldType.Name} {n}");
                        try
                        {
                            object val = fi.GetValue(tm);
                            if (val is ICollection col) sb.Append($"(count={col.Count})");
                            if (val is IDictionary d2 && d2.Contains(tile))
                                sb.Append($"(contains tile, val={d2[tile]})");
                        }
                        catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropTileReflection.cs:583", __ex, Plugin.Log); }
                    }
                    foreach (PropertyInfo pi in _tileManagerType.GetProperties(f))
                    {
                        string n = pi.Name;
                        if (!ContainsAny(n, "water", "Watered", "Watering")) continue;
                        if (any) sb.Append(','); any = true;
                        sb.Append($"{pi.PropertyType.Name} {n}(prop)");
                    }
                    foreach (MethodInfo mi in _tileManagerType.GetMethods(f))
                    {
                        string n = mi.Name;
                        if (!ContainsAny(n, "water", "Watered", "Watering")) continue;
                        if (mi.IsSpecialName) continue;
                        if (any) sb.Append(','); any = true;
                        var pars = mi.GetParameters();
                        sb.Append($"{mi.ReturnType.Name} {n}(");
                        for (int i = 0; i < pars.Length; i++)
                        {
                            if (i > 0) sb.Append(',');
                            sb.Append(pars[i].ParameterType.Name);
                        }
                        sb.Append(")");
                    }
                    sb.Append(']');
                }
            }
            catch (Exception ex) { sb.Append($", waterScanErr={ex.Message}"); }

            return sb.ToString();
        }

        private static string InvokeBoolSafe(MethodInfo method, object tm, Vector2Int tile)
        {
            if (method == null) return "<no-method>";
            if (tm == null) return "<no-tm>";
            try
            {
                object r = method.Invoke(tm, new object[] { tile });
                return r is bool b ? (b ? "true" : "false") : $"<{r}>";
            }
            catch (Exception ex) { return $"<err:{ex.GetType().Name}>"; }
        }

        private static bool ContainsAny(string s, params string[] needles)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (string n in needles)
                if (s.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
