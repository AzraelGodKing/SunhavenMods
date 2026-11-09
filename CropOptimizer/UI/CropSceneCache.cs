using System.Collections.Generic;
using CropOptimizer.Data;
using CropOptimizer.Patches;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CropOptimizer.UI
{
    /// <summary>Shared crop scene scan with registry / forecast reconciliation.</summary>
    internal static class CropSceneCache
    {
        private static UnityEngine.Object[] _cachedCrops = System.Array.Empty<UnityEngine.Object>();
        private static float _nextRefreshTime;
        private static int _cachedSceneHandle = -1;
        private static System.Type _cropType;

        // AZR-733: reused per refresh instead of allocating new collections every 1.5s.
        private static readonly HashSet<int> _liveIds = new HashSet<int>();
        private static readonly List<Object> _presentCrops = new List<Object>();

        // AZR-733: farm tile -> crop, rebuilt with each refresh, so hover lookups are O(1)
        // instead of a per-crop tile check across the whole farm.
        private static readonly Dictionary<Vector2Int, Component> _cropsByFarmTile = new Dictionary<Vector2Int, Component>();

        /// <summary>
        /// Crop whose resolved farm tile is <paramref name="farmTile"/>, from the last scene refresh.
        /// Crops whose tile cannot be resolved are not indexed; callers keep a positional fallback.
        /// </summary>
        public static bool TryGetCropAtFarmTile(Vector2Int farmTile, out Component crop)
        {
            if (_cropsByFarmTile.TryGetValue(farmTile, out crop) && crop != null && CropPresence.IsPresent(crop))
                return true;
            crop = null;
            return false;
        }

        /// <summary>When the active scene has no crops, avoid hammering <c>FindObjectsOfType</c> off-farm.</summary>
        private const float EmptySceneCacheRefreshSeconds = 4f;

        public static void Invalidate()
        {
            _cachedCrops = System.Array.Empty<UnityEngine.Object>();
            _nextRefreshTime = 0f;
            _cachedSceneHandle = -1;
            _cropsByFarmTile.Clear();
        }

        public static bool HasActiveSceneCrops(float refreshIntervalSeconds)
        {
            UnityEngine.Object[] crops = GetCrops(refreshIntervalSeconds);
            return crops != null && crops.Length > 0;
        }

        public static UnityEngine.Object[] GetCrops(float refreshIntervalSeconds)
        {
            float now = Time.unscaledTime;
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.handle != _cachedSceneHandle)
            {
                _cachedSceneHandle = activeScene.handle;
                _cachedCrops = System.Array.Empty<UnityEngine.Object>();
                _nextRefreshTime = 0f;
                _cropsByFarmTile.Clear();
            }

            if (now < _nextRefreshTime)
                return _cachedCrops;

            if (_cropType == null)
                _cropType = AccessTools.TypeByName("Wish.Crop");
            if (_cropType == null)
            {
                _cachedCrops = System.Array.Empty<UnityEngine.Object>();
                _nextRefreshTime = now + EmptySceneCacheRefreshSeconds;
                return _cachedCrops;
            }

            var discovered = Object.FindObjectsOfType(_cropType);
            var liveIds = _liveIds;
            var presentCrops = _presentCrops;
            liveIds.Clear();
            presentCrops.Clear();
            _cropsByFarmTile.Clear();

            for (int i = 0; i < discovered.Length; i++)
            {
                if (discovered[i] is not Component crop || !CropPresence.IsPresent(crop))
                    continue;
                if (crop.gameObject.scene != activeScene)
                    continue;

                CropInstanceRegistry.Register(crop);
                liveIds.Add(crop.GetInstanceID());
                presentCrops.Add(crop);
                if (GameFarmCoords.TryGetCropFarmTile(crop, out Vector2Int farmTile) && !_cropsByFarmTile.ContainsKey(farmTile))
                    _cropsByFarmTile[farmTile] = crop;
            }

            PruneStale(liveIds, discovered.Length);
            ReconcileForecast(presentCrops);
            _cachedCrops = presentCrops.ToArray();
            _nextRefreshTime = now + (presentCrops.Count == 0
                ? EmptySceneCacheRefreshSeconds
                : Mathf.Max(0.1f, refreshIntervalSeconds));
            return _cachedCrops;
        }

        private static void ReconcileForecast(List<Object> presentCrops)
        {
            CropForecast forecast = Plugin.Instance?._forecast;
            if (forecast == null || presentCrops == null || presentCrops.Count == 0)
                return;

            for (int i = 0; i < presentCrops.Count; i++)
            {
                if (presentCrops[i] is not Component crop || !CropPresence.IsTrackable(crop))
                    continue;

                int id = crop.GetInstanceID();
                if (!forecast.TryGetState(id, out CropForecast.CropState state)
                    || state.ItemId <= 0
                    || !state.SellLookupComplete)
                    CropGrowthPatch.SyncCropToForecast(crop);
            }
        }

        private static void PruneStale(HashSet<int> liveIds, int discoveredCount)
        {
            // Empty scan with no Wish.Crop instances — drop stale rows (left the farm / no crops).
            // When instances exist but fail IsPresent, keep forecast until a successful pass.
            if (liveIds == null || liveIds.Count == 0)
            {
                if (discoveredCount == 0)
                {
                    CropInstanceRegistry.PruneExcept(liveIds);
                    Plugin.Instance?._forecast?.PruneExcept(liveIds);
                }
                return;
            }

            CropInstanceRegistry.PruneExcept(liveIds);
            Plugin.Instance?._forecast?.PruneExcept(liveIds);
        }
    }
}
