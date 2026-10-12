using System;
using System.Reflection;
using CropOptimizer.Data;
using CropOptimizer.Patches;
using HarmonyLib;
using SunhavenMods.Shared;
using UnityEngine;
using ReflectionProbe = SunhavenMods.Shared.ReflectionProbe;

namespace CropOptimizer.UI
{
    /// <summary>
    /// Experimental: find <see cref="Wish.Crop"/> near the mouse in world space for hover tooltips.
    /// </summary>
    internal static class CropHoverQuery
    {
        private static Type _cropType;

        private static Camera _cachedGameplayCamera;
        private static float _nextGameplayCameraSearchTime;

        private static Vector3 _lastHoverMouseScreen;
        private static Component _lastHoverCrop;
        private static float _nextHoverFullScanTime;

        /// <summary>How often we refresh the crop list from the scene (large farms: avoid hammering <c>FindObjectsOfType</c>).</summary>
        internal const float CropCacheRefreshSeconds = 1.5f;

        /// <summary>When the mouse is stable, skip the O(n) closest-crop scan for a short window.</summary>
        private const float HoverRescanMinInterval = 0.055f;

        private const float MouseMoveSkipScanPxSq = 9f;

        /// <summary>After a successful camera search, wait before scanning all cameras again (avoids per-frame <c>FindObjectsOfType&lt;Camera&gt;</c>).</summary>
        private const float GameplayCameraSearchCooldown = 2f;

        private static Type CropType => _cropType ??= AccessTools.TypeByName("Wish.Crop");

        /// <summary>
        /// Prefer <see cref="Camera.main"/>; many scenes never tag the farm camera as MainCamera.
        /// </summary>
        public static Camera ResolveGameplayCamera()
        {
            if (Camera.main != null && Camera.main.enabled)
            {
                _cachedGameplayCamera = Camera.main;
                return Camera.main;
            }

            if (_cachedGameplayCamera != null && _cachedGameplayCamera.enabled && _cachedGameplayCamera.gameObject.activeInHierarchy)
                return _cachedGameplayCamera;

            float now = Time.unscaledTime;
            if (now < _nextGameplayCameraSearchTime)
                return _cachedGameplayCamera;

            Camera[] cams = UnityEngine.Object.FindObjectsOfType<Camera>();
            Camera best = null;
            foreach (Camera c in cams)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                    continue;
                if (best == null || c.depth > best.depth)
                    best = c;
            }

            _cachedGameplayCamera = best;
            // Retry sooner when no camera yet (e.g. load-in); back off when we have one.
            _nextGameplayCameraSearchTime = now + (best != null ? GameplayCameraSearchCooldown : 0.25f);
            return best;
        }

        /// <summary>Call on scene/game transitions so we do not keep a stale camera reference.</summary>
        public static void InvalidateGameplayCameraCache()
        {
            _cachedGameplayCamera = null;
            _nextGameplayCameraSearchTime = 0f;
        }

        /// <summary>Clears hover fast-path state (e.g. after load) so we never reference destroyed crops.</summary>
        public static void InvalidateHoverAssist()
        {
            _lastHoverCrop = null;
            _nextHoverFullScanTime = 0f;
        }

        /// <summary>
        /// Maps mouse to the XY plane at <paramref name="planeZ"/> (crop layer). Raw
        /// <see cref="Camera.ScreenToWorldPoint"/> with <c>mouse.z == 0</c> is wrong for orthographic cameras.
        /// </summary>
        public static bool TryMouseWorldOnPlane(Camera camera, float planeZ, out Vector3 world)
        {
            world = default;
            if (camera == null)
                return false;

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));
            if (plane.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }

            // Fallback: orthographic depth hack when ray is parallel to the farm plane
            Vector3 mp = Input.mousePosition;
            mp.z = Mathf.Max(0.01f, Mathf.Abs(camera.transform.position.z));
            world = camera.ScreenToWorldPoint(mp);
            world.z = planeZ;
            return true;
        }

        public static bool TryGetClosestCropNearMouse(Camera camera, float maxWorldDistance, out Component crop)
        {
            crop = null;
            if (camera == null)
                return false;

            UnityEngine.Object[] crops = CropSceneCache.GetCrops(CropCacheRefreshSeconds);
            if (crops == null || crops.Length == 0)
                return false;

            float planeZ = 0f;
            foreach (UnityEngine.Object o in crops)
            {
                if (o is Component mb && mb != null)
                {
                    planeZ = mb.transform.position.z;
                    break;
                }
            }

            if (!TryMouseWorldOnPlane(camera, planeZ, out Vector3 worldOnPlane))
                return false;

            Vector2Int mouseFarmTile = GameFarmCoords.GetMouseFarmTile();
            Vector3 mouseScreen = Input.mousePosition;
            float now = Time.unscaledTime;
            float maxSq = maxWorldDistance * maxWorldDistance;
            Vector2 mouse2 = new Vector2(worldOnPlane.x, worldOnPlane.y);

            bool mouseStable = (mouseScreen - _lastHoverMouseScreen).sqrMagnitude <= MouseMoveSkipScanPxSq;
            _lastHoverMouseScreen = mouseScreen;

            if (mouseStable && now < _nextHoverFullScanTime)
            {
                if (_lastHoverCrop != null)
                {
                    Component last = _lastHoverCrop;
                    if (last != null
                        && CropPresence.IsPresent(last)
                        && GameFarmCoords.IsCropOnFarmTile(last, mouseFarmTile))
                    {
                        crop = last;
                        return true;
                    }
                }
                else
                {
                    crop = null;
                    return false;
                }
            }

            _nextHoverFullScanTime = now + HoverRescanMinInterval;

            Component best = null;
            float bestSq = maxSq;

            foreach (UnityEngine.Object o in crops)
            {
                if (o is not Component mb || mb == null || !CropPresence.IsPresent(mb))
                    continue;

                if (GameFarmCoords.IsCropOnFarmTile(mb, mouseFarmTile))
                {
                    crop = mb;
                    _lastHoverCrop = mb;
                    return true;
                }

                Vector2 p = new Vector2(mb.transform.position.x, mb.transform.position.y);
                float sq = (p - mouse2).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = mb;
                }
            }

            if (best == null)
            {
                _lastHoverCrop = null;
                return false;
            }

            crop = best;
            _lastHoverCrop = best;
            return true;
        }

        private static bool _dumpedCropMembers;
        private static bool _loggedTileProbe;

        private static void LogTileDebugOnce(Component crop, Vector2Int tile)
        {
            if (_loggedTileProbe) return;
            _loggedTileProbe = true;
            try { Plugin.Log?.LogInfo(CropTileReflection.BuildDebugSnapshot(crop, tile)); } catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropHoverQuery.cs:235", __ex, Plugin.Log); }
        }

        /// <summary>When <c>Debug.DebugLogging</c> is on, log <see cref="Wish.Crop"/>'s public+private
        /// fields/props once so we can hardcode the right names for water / growth / quality.</summary>
        private static void DumpCropMembersOnce(object cropInstance)
        {
            if (_dumpedCropMembers || cropInstance == null)
                return;
            _dumpedCropMembers = true;

            try
            {
                var cfg = Plugin.Instance?.Config as BepInEx.Configuration.ConfigFile;
                bool debug = Plugin.Instance != null && IsDebugLogEnabled();
                if (!debug)
                    return;

                var log = Plugin.Log;
                if (log == null)
                    return;

                Type t = cropInstance.GetType();
                log.LogInfo($"[HoverDebug] Dumping members of {t.FullName}");
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                foreach (var fi in t.GetFields(flags))
                    log.LogInfo($"[HoverDebug]   field  {fi.FieldType.Name} {fi.Name}");
                foreach (var pi in t.GetProperties(flags))
                    log.LogInfo($"[HoverDebug]   prop   {pi.PropertyType.Name} {pi.Name}");
            }
            catch (Exception __ex) { ReflectionProbe.LogOnce("CropOptimizer/UI/CropHoverQuery.cs:265", __ex, Plugin.Log); }
        }

        private static bool IsDebugLogEnabled()
        {
            try
            {
                return Plugin.Instance != null && Plugin.IsDebugLoggingEnabled;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Builds a structured <see cref="TooltipContent"/> for the uGUI hover card.</summary>
        public static TooltipContent BuildTooltipContent(Component crop, CropForecast forecast)
        {
            if (crop == null) return null;

            object inst = crop;
            DumpCropMembersOnce(inst);

            var content = new TooltipContent();

            int itemId = 0;
            string cropTitle = ModLocalization.T("crop.tooltip.crop");
            if (CropGrowthPatch.TryGetTooltipHarvestItemId(inst, out itemId) && itemId > 0)
            {
                if (CropGrowthPatch.TryGetItemDisplayName(itemId, out string name) && !string.IsNullOrWhiteSpace(name))
                    cropTitle = name;
                else
                    cropTitle = ModLocalization.T("crop.tooltip.itemId", itemId);
            }
            content.Title = cropTitle;

            bool fullyGrown = false;
            CropGrowthPatch.TryGetTooltipFullyGrown(inst, out fullyGrown);
            if (fullyGrown) content.HeaderTag = ModLocalization.T("crop.tooltip.headerTag.ready");

            // Rich-text accent colors are tuned to read on the dark panel fill:
            //   #F7D982 = warm gold (numbers / emphasis), #B8A078 = muted cream (secondary notes).
            const string accentGold = "#F7D982";
            const string mutedCream = "#B8A078";

            if (CropGrowthPatch.TryGetTooltipQualityInfo(inst, out string qualityLabel, out float qMul) && !string.IsNullOrEmpty(qualityLabel))
            {
                content.QualityColor = QualityColorFor(qualityLabel);
                content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Quality, content.QualityColor,
                    ModLocalization.T("crop.tooltip.quality", qualityLabel, mutedCream, qMul)));
            }

            if (CropGrowthPatch.TryGetTooltipGrowthStageInfo(inst, out string stageText, out _) && !string.IsNullOrEmpty(stageText))
                content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Sprout, UiStyle.Sprout, ModLocalization.T("crop.tooltip.growth", stageText)));

            if (fullyGrown)
                content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Ready, UiStyle.Fertilizer,
                    ModLocalization.T("crop.tooltip.readyNow", accentGold)));
            else if (CropGrowthPatch.TryGetTooltipEtaHours(inst, out float liveEta, out bool reflOk) && reflOk)
                content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Clock, UiStyle.Clock,
                    ModLocalization.T("crop.tooltip.readyIn", accentGold, Mathf.Max(0f, liveEta))));
            else if (forecast != null && forecast.TryGetState(crop.GetInstanceID(), out CropForecast.CropState st))
                content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Clock, UiStyle.Clock,
                    ModLocalization.T("crop.tooltip.readyInCached", accentGold, Mathf.Max(0f, st.NextHarvestEtaHours), mutedCream)));
            else
                content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Clock, UiStyle.Clock,
                    ModLocalization.T("crop.tooltip.etaUnknown", mutedCream)));

            if (CropGrowthPatch.TryGetTooltipProjectedShopValue(inst, out CropShopValue shop, out _))
            {
                if (shop.Gold > 0)
                    content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Coin, UiStyle.Coin,
                        CropSellDisplay.FormatTooltipLine(accentGold, shop.Gold, "crop.tooltip.projectedGold")));
                if (shop.Orbs > 0)
                    content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Coin, UiStyle.Coin,
                        CropSellDisplay.FormatTooltipLine(accentGold, shop.Orbs, "crop.tooltip.projectedOrbs")));
                if (shop.Tickets > 0)
                    content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Coin, UiStyle.Coin,
                        CropSellDisplay.FormatTooltipLine(accentGold, shop.Tickets, "crop.tooltip.projectedTickets")));
            }

            // Water (from the water tilemap; fallback to "?" if nothing resolves).
            Vector2Int tile = default;
            bool haveTile = CropTileReflection.TryGetTileCoordForCrop(crop, out tile);
            string tileState = null;
            if (haveTile)
            {
                tileState = CropTileReflection.DescribeFarmingTileState(tile, crop.transform.position, true, out _);
                if (IsDebugLogEnabled()) LogTileDebugOnce(crop, tile);
            }

            (string waterText, Color32 waterColor) = DescribeWaterState(tileState);
            content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Water, waterColor, waterText));

            if (CropGrowthPatch.TryGetTooltipFertilized(inst, out bool fertilized))
            {
                string label = fertilized ? ModLocalization.T("crop.tooltip.fertilized") : ModLocalization.T("crop.tooltip.notFertilized");
                content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Fertilizer,
                    fertilized ? UiStyle.Fertilizer : (Color32)new Color32(0x9A, 0x88, 0x60, 0xFF), label));
            }

            if (CropGrowthPatch.TryGetTooltipManaInfused(inst, out bool manaInfused) && manaInfused)
                content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Mana, UiStyle.Mana, ModLocalization.T("crop.tooltip.manaInfused")));

            if (haveTile)
                content.Rows.Add(RowSpec.Make(UiStyle.IconKind.Tile, UiStyle.Tile,
                    ModLocalization.T("crop.tooltip.tile", mutedCream, tile.x, tile.y)));

            if (itemId > 0)
            {
                var extras = new System.Collections.Generic.List<string>();
                CropGrowthPatch.AppendItemExtraLines(itemId, extras);
                if (extras.Count > 0)
                    content.Extras = string.Join(" · ", extras);
            }

            return content;
        }

        private static Color32 QualityColorFor(string label)
        {
            if (string.IsNullOrEmpty(label)) return UiStyle.QualityNormal;
            string l = label.ToLowerInvariant();
            if (l.Contains("gold") || l.Contains("iridium")) return UiStyle.QualityGold;
            if (l.Contains("silver")) return UiStyle.QualitySilver;
            return UiStyle.QualityNormal;
        }

        private static (string text, Color32 color) DescribeWaterState(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return (ModLocalization.T("crop.water.unknown"), UiStyle.Water);
            string r = raw.ToLowerInvariant();
            if (r.Contains("water"))
                return (ModLocalization.T("crop.water.watered"), UiStyle.Water);
            if (r.Contains("hoed"))
                return (ModLocalization.T("crop.water.hoedDry"), new Color32(0xC9, 0xA0, 0x70, 0xFF));
            return (ModLocalization.T("crop.water.label", raw), UiStyle.Water);
        }
    }
}
