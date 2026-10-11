using System.Collections.Generic;
using UnityEngine;

namespace CropOptimizer.Data
{
    internal static class CropInstanceRegistry
    {
        private static readonly HashSet<int> ActiveCropIds = new HashSet<int>();

        public static void Register(Object cropObject)
        {
            if (cropObject == null)
                return;
            ActiveCropIds.Add(cropObject.GetInstanceID());
        }

        public static void UnregisterById(int instanceId)
        {
            if (instanceId != 0)
                ActiveCropIds.Remove(instanceId);
        }

        public static void PruneExcept(HashSet<int> liveIds)
        {
            if (liveIds == null || liveIds.Count == 0)
            {
                ActiveCropIds.Clear();
                return;
            }

            var stale = new List<int>();
            foreach (int id in ActiveCropIds)
            {
                if (!liveIds.Contains(id))
                    stale.Add(id);
            }

            for (int i = 0; i < stale.Count; i++)
                ActiveCropIds.Remove(stale[i]);
        }

        // Unity reuses GetInstanceID() values after objects are destroyed/recreated
        // (e.g. switching save files), so stale IDs must be dropped on character load
        // to avoid colliding with unrelated new crops.
        public static void Clear()
        {
            ActiveCropIds.Clear();
        }
    }
}
