using System;
using System.Collections.Generic;
using GiftingAssistant.Game;

namespace GiftingAssistant.Data
{
    /// <summary>
    /// Lightweight data surface consumed by Haven's Almanac integration.
    /// Gated by <see cref="Plugin.StaticAlmanacIntegrationEnabled"/>.
    /// </summary>
    public static class GiftingAssistantAlmanacData
    {
        public readonly struct RosterEntrySnapshot
        {
            public RosterEntrySnapshot(string npcName, GiftPriority priority, bool isGiftedToday)
            {
                NpcName = npcName;
                Priority = priority;
                IsGiftedToday = isGiftedToday;
            }

            public string NpcName { get; }
            public GiftPriority Priority { get; }
            public bool IsGiftedToday { get; }
        }

        public static bool IsIntegrationEnabled => Plugin.StaticAlmanacIntegrationEnabled;

        public static bool TryGetSummary(out string summary, out int rosterCount, out int pendingCount)
        {
            summary = "";
            rosterCount = 0;
            pendingCount = 0;

            if (!IsIntegrationEnabled)
                return false;

            var manager = Plugin.GetManager();
            if (manager == null || string.IsNullOrEmpty(manager.CurrentCharacter))
                return false;

            var entries = BuildSortedSnapshots(manager);
            rosterCount = entries.Count;
            foreach (var entry in entries)
            {
                if (!entry.IsGiftedToday)
                    pendingCount++;
            }

            summary = rosterCount == 0
                ? "No roster"
                : $"{pendingCount} pending / {rosterCount} roster";
            return true;
        }

        public static List<RosterEntrySnapshot> GetSortedRosterEntries(int maxCount = 0)
        {
            var manager = Plugin.GetManager();
            if (!IsIntegrationEnabled || manager == null || string.IsNullOrEmpty(manager.CurrentCharacter))
                return new List<RosterEntrySnapshot>();

            var entries = BuildSortedSnapshots(manager);
            if (maxCount > 0 && entries.Count > maxCount)
                entries = entries.GetRange(0, maxCount);
            return entries;
        }

        private static List<RosterEntrySnapshot> BuildSortedSnapshots(GiftRosterManager manager)
        {
            var snapshots = new List<RosterEntrySnapshot>();
            foreach (var entry in manager.GetEntries())
            {
                if (entry == null || string.IsNullOrEmpty(entry.NpcName))
                    continue;
                snapshots.Add(new RosterEntrySnapshot(entry.NpcName, entry.Priority, IsGiftedToday(entry)));
            }

            snapshots.Sort((a, b) =>
            {
                if (a.IsGiftedToday != b.IsGiftedToday)
                    return a.IsGiftedToday ? 1 : -1;
                int pri = ((int)b.Priority).CompareTo((int)a.Priority);
                if (pri != 0)
                    return pri;
                return string.Compare(a.NpcName, b.NpcName, StringComparison.OrdinalIgnoreCase);
            });

            return snapshots;
        }

        private static bool IsGiftedToday(GiftRosterEntry entry)
        {
            if (entry == null)
                return false;
            if (entry.ManualGiftedToday)
                return true;
            return GiftGameData.HasGivenGiftToday(entry.NpcName);
        }
    }
}
