using System;
using System.Collections.Generic;
using System.Linq;

namespace CropOptimizer.Data
{
    internal sealed class CropForecast
    {
        private readonly Dictionary<int, CropState> _cropStateByInstanceId = new Dictionary<int, CropState>();
        private CropShopValue _runningProjectedSell;

        internal readonly struct CropState
        {
            public CropState(float nextHarvestEtaHours, float qualityMultiplier, CropShopValue projectedSell, int itemId, bool sellLookupComplete)
            {
                NextHarvestEtaHours = nextHarvestEtaHours;
                QualityMultiplier = qualityMultiplier;
                ProjectedSell = projectedSell;
                ItemId = itemId;
                SellLookupComplete = sellLookupComplete;
            }

            public float NextHarvestEtaHours { get; }
            public float QualityMultiplier { get; }
            public CropShopValue ProjectedSell { get; }
            public int ProjectedSellGold => ProjectedSell.Gold;
            public int ItemId { get; }
            public bool SellLookupComplete { get; }
        }

        public readonly struct CropTypeSummary
        {
            public CropTypeSummary(int itemId, CropShopValue total, int cropCount)
            {
                ItemId = itemId;
                Total = total;
                CropCount = cropCount;
            }

            public int ItemId { get; }
            public CropShopValue Total { get; }
            public int TotalGold => Total.Gold;
            public int CropCount { get; }
        }

        public void UpdateCropState(int cropInstanceId, float nextHarvestEtaHours, float qualityMultiplier, CropShopValue projectedSell, int itemId = 0, bool sellLookupComplete = false)
        {
            if (_cropStateByInstanceId.TryGetValue(cropInstanceId, out CropState prev))
                Subtract(prev.ProjectedSell);
            _cropStateByInstanceId[cropInstanceId] = new CropState(nextHarvestEtaHours, qualityMultiplier, projectedSell, itemId, sellLookupComplete);
            Add(projectedSell);
        }

        public IReadOnlyDictionary<int, CropState> Snapshot()
        {
            return _cropStateByInstanceId;
        }

        public CropShopValue GetProjectedShopValue() => _runningProjectedSell;

        public bool TryGetState(int cropInstanceId, out CropState state)
        {
            return _cropStateByInstanceId.TryGetValue(cropInstanceId, out state);
        }

        public bool RemoveCropState(int cropInstanceId)
        {
            if (!_cropStateByInstanceId.TryGetValue(cropInstanceId, out CropState previous))
                return false;

            Subtract(previous.ProjectedSell);
            return _cropStateByInstanceId.Remove(cropInstanceId);
        }

        /// <summary>
        /// Returns the top <paramref name="count"/> crop types ranked by total projected shop value,
        /// aggregated across all tracked instances. Types with itemId == 0 are skipped.
        /// </summary>
        public List<CropTypeSummary> GetTopCropsByValue(int count = 5)
        {
            var byType = new Dictionary<int, (CropShopValue total, int cropCount)>();
            foreach (var kvp in _cropStateByInstanceId)
            {
                var state = kvp.Value;
                if (state.ItemId <= 0) continue;
                if (!byType.TryGetValue(state.ItemId, out var acc))
                    acc = (default, 0);
                byType[state.ItemId] = (AddValues(acc.total, state.ProjectedSell), acc.cropCount + 1);
            }
            return byType
                .Select(kvp => new CropTypeSummary(kvp.Key, kvp.Value.total, kvp.Value.cropCount))
                .OrderByDescending(s => Rank(s.Total))
                .Take(count)
                .ToList();
        }

        public void Clear()
        {
            _cropStateByInstanceId.Clear();
            _runningProjectedSell = default;
        }

        public void PruneExcept(HashSet<int> liveIds)
        {
            if (liveIds == null || liveIds.Count == 0)
            {
                Clear();
                return;
            }

            var stale = new List<int>();
            foreach (int id in _cropStateByInstanceId.Keys)
            {
                if (!liveIds.Contains(id))
                    stale.Add(id);
            }

            for (int i = 0; i < stale.Count; i++)
                RemoveCropState(stale[i]);
        }

        private void Add(CropShopValue value)
        {
            _runningProjectedSell = AddValues(_runningProjectedSell, value);
        }

        private void Subtract(CropShopValue value)
        {
            _runningProjectedSell = new CropShopValue(
                Math.Max(0, _runningProjectedSell.Gold - Math.Max(0, value.Gold)),
                Math.Max(0, _runningProjectedSell.Orbs - Math.Max(0, value.Orbs)),
                Math.Max(0, _runningProjectedSell.Tickets - Math.Max(0, value.Tickets)));
        }

        private static CropShopValue AddValues(CropShopValue a, CropShopValue b)
        {
            return new CropShopValue(
                a.Gold + Math.Max(0, b.Gold),
                a.Orbs + Math.Max(0, b.Orbs),
                a.Tickets + Math.Max(0, b.Tickets));
        }

        private static long Rank(CropShopValue value)
        {
            return (long)value.Gold + value.Orbs + value.Tickets;
        }
    }
}
