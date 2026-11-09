using System;
using System.Collections.Generic;

namespace GiftingAssistant.Data
{
    public class GiftRosterManager
    {
        private GiftRosterData _data;
        private string _currentCharacter;
        private bool _isDirty;

        public event Action OnRosterChanged;
        public event Action OnDataLoaded;

        public bool IsDirty => _isDirty;
        public string CurrentCharacter => _currentCharacter;

        public void LoadForCharacter(string characterName, GiftRosterData data)
        {
            _currentCharacter = characterName;
            _data = data ?? new GiftRosterData(characterName);
            _isDirty = false;
            OnDataLoaded?.Invoke();
        }

        public GiftRosterData GetData() => _data;

        public void MarkClean() => _isDirty = false;

        public IReadOnlyList<GiftRosterEntry> GetEntries()
        {
            return _data?.Entries ?? (IReadOnlyList<GiftRosterEntry>)Array.Empty<GiftRosterEntry>();
        }

        public bool Contains(string npcName)
        {
            if (_data?.Entries == null || string.IsNullOrEmpty(npcName))
                return false;
            return _data.Entries.Exists(e => string.Equals(e.NpcName, npcName, StringComparison.OrdinalIgnoreCase));
        }

        public GiftRosterEntry GetEntry(string npcName)
        {
            if (_data?.Entries == null || string.IsNullOrEmpty(npcName))
                return null;
            return _data.Entries.Find(e => string.Equals(e.NpcName, npcName, StringComparison.OrdinalIgnoreCase));
        }

        public void AddNpc(string npcName, GiftPriority priority = GiftPriority.Normal)
        {
            if (_data == null || string.IsNullOrEmpty(npcName) || Contains(npcName))
                return;

            _data.Entries.Add(new GiftRosterEntry(npcName, priority));
            Touch();
        }

        public void RemoveNpc(string npcName)
        {
            if (_data?.Entries == null)
                return;
            if (_data.Entries.RemoveAll(e => string.Equals(e.NpcName, npcName, StringComparison.OrdinalIgnoreCase)) > 0)
                Touch();
        }

        public void SetPriority(string npcName, GiftPriority priority)
        {
            var entry = GetEntry(npcName);
            if (entry == null || entry.Priority == priority)
                return;
            entry.Priority = priority;
            Touch();
        }

        public void SetManualGifted(string npcName, bool gifted)
        {
            var entry = GetEntry(npcName);
            if (entry == null || entry.ManualGiftedToday == gifted)
                return;
            entry.ManualGiftedToday = gifted;
            Touch();
        }

        /// <summary>Toggles whether an item is one of this NPC's preferred gifts.</summary>
        public void TogglePreferredGift(string npcName, int itemId)
        {
            var entry = GetEntry(npcName);
            if (entry == null || itemId <= 0)
                return;

            entry.PreferredGiftIds = entry.PreferredGiftIds ?? new List<int>();
            if (entry.PreferredGiftIds.Contains(itemId))
                entry.PreferredGiftIds.Remove(itemId);
            else
                entry.PreferredGiftIds.Add(itemId);
            Touch();
        }

        public bool IsPreferredGift(string npcName, int itemId)
        {
            var entry = GetEntry(npcName);
            return entry?.PreferredGiftIds != null && entry.PreferredGiftIds.Contains(itemId);
        }

        public void ClearPreferredGifts(string npcName)
        {
            var entry = GetEntry(npcName);
            if (entry?.PreferredGiftIds == null || entry.PreferredGiftIds.Count == 0)
                return;
            entry.PreferredGiftIds.Clear();
            Touch();
        }

        /// <summary>
        /// Clears manual gifted flags for a new day. Returns true if anything changed.
        /// </summary>
        public bool ResetDailyGifted(string newDateKey)
        {
            if (_data == null)
                return false;

            bool alreadyResetToday = !string.IsNullOrEmpty(newDateKey) &&
                                     string.Equals(_data.LastResetDateKey, newDateKey, StringComparison.Ordinal);
            if (alreadyResetToday)
                return false;

            bool changed = false;
            foreach (var entry in _data.Entries)
            {
                if (entry.ManualGiftedToday)
                {
                    entry.ManualGiftedToday = false;
                    changed = true;
                }
            }

            _data.LastResetDateKey = newDateKey ?? "";
            if (changed || !alreadyResetToday)
                Touch();
            return changed;
        }

        private void Touch()
        {
            if (_data != null)
                _data.LastUpdated = DateTime.Now;
            _isDirty = true;
            OnRosterChanged?.Invoke();
        }
    }
}
