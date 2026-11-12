using System;
using System.Collections.Generic;
using CropOptimizer.Data;
using CropOptimizer.Patches;
using UnityEngine;
using Wish;

namespace CropOptimizer.UI
{
    internal enum CropHighlightKind
    {
        NeedsWater,
        NeedsFertilizer,
        NeedsManaInfusion
    }

    internal readonly struct CropHighlightTarget
    {
        public readonly Vector3 Center;
        public readonly Vector2Int Tile;
        public readonly CropHighlightKind Kind;

        public CropHighlightTarget(Vector3 center, Vector2Int tile, CropHighlightKind kind)
        {
            Center = center;
            Tile = tile;
            Kind = kind;
        }
    }

    /// <summary>Finds crop tiles that still need water, mana infusion, or fertilizer.</summary>
    internal static class CropFieldHighlightScanner
    {
        private const int MaxTargets = 600;
        private const float CacheRefreshSeconds = 0.85f;
        private const string ManaInfusionProgressId = "ManaInfusion";
        private static readonly List<CropHighlightTarget> _scratch = new List<CropHighlightTarget>(128);

        public static IReadOnlyList<CropHighlightTarget> Scan(bool includeDry, bool includeUnfertilized, bool includeUninfused)
        {
            _scratch.Clear();
            includeUninfused = includeUninfused && IsManaInfusionUnlocked();
            if (!includeDry && !includeUnfertilized && !includeUninfused)
                return _scratch;

            UnityEngine.Object[] crops = CropSceneCache.GetCrops(CacheRefreshSeconds);
            if (crops == null || crops.Length == 0)
                return _scratch;

            foreach (UnityEngine.Object o in crops)
            {
                if (_scratch.Count >= MaxTargets)
                    break;
                if (o is not Component crop || !CropPresence.IsPresent(crop))
                    continue;

                object inst = crop;
                if (!GameFarmCoords.TryGetCropFarmTile(crop, out Vector2Int farmTile))
                    continue;

                Vector3 center = GameFarmCoords.GetSelectionWorldPosition(farmTile);

                if (includeDry && !CropTileReflection.IsCropTileWatered(crop))
                {
                    _scratch.Add(new CropHighlightTarget(center, farmTile, CropHighlightKind.NeedsWater));
                    continue;
                }

                if (includeUninfused && NeedsManaInfusion(crop))
                {
                    _scratch.Add(new CropHighlightTarget(center, farmTile, CropHighlightKind.NeedsManaInfusion));
                    continue;
                }

                if (includeUnfertilized
                    && (!CropGrowthPatch.TryGetTooltipFullyGrown(inst, out bool fullyGrown) || !fullyGrown)
                    && CropGrowthPatch.TryGetTooltipFertilized(inst, out bool fertilized)
                    && !fertilized)
                    _scratch.Add(new CropHighlightTarget(center, farmTile, CropHighlightKind.NeedsFertilizer));
            }

            return _scratch;
        }

        /// <summary>Same test the game uses for the crop's "Infuse" interaction (<c>Crop.InteractionText</c>).</summary>
        private static bool NeedsManaInfusion(Component crop)
        {
            if (crop is not Crop c)
                return false;

            CropSaveData data = c.data;
            if (data == null || data.dead || data.manaInfused)
                return false;

            SeedData seed = c.SeedData;
            return seed != null && seed.manaInfusable;
        }

        private static bool IsManaInfusionUnlocked()
        {
            try
            {
                GameSave save = GameSave.Instance;
                return save != null && save.GetProgressBoolCharacter(ManaInfusionProgressId);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[CropFieldHighlightScanner] Mana infusion unlock check failed: {ex.Message}");
                return false;
            }
        }
    }
}
