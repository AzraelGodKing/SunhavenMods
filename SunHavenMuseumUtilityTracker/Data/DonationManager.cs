using System;
using System.Collections.Generic;
using System.Linq;

namespace SunHavenMuseumUtilityTracker.Data
{
    /// <summary>
    /// Manages donation tracking and provides statistics.
    /// </summary>
    public class DonationManager
    {
        private static Dictionary<int, List<string>> _museumIdsByGameItemId;
        private DonationData _donationData;
        private string _currentCharacter;
        private bool _isDirty;

        public event Action OnDonationsChanged;
        public event Action OnDataLoaded;

        public bool IsDirty => _isDirty;
        public bool IsLoaded => _donationData != null;
        public string CurrentCharacter => _currentCharacter;

        public DonationManager()
        {
            _donationData = null;
            _currentCharacter = null;
        }

        public void LoadForCharacter(string characterName, DonationData data)
        {
            _currentCharacter = characterName;
            _donationData = data ?? new DonationData(characterName);
            _isDirty = false;
            OnDataLoaded?.Invoke();
            Plugin.Log?.LogInfo($"DonationManager: Loaded data for {characterName} with {_donationData.DonatedItemIds.Count} donated items");
        }

        public DonationData GetData()
        {
            return _donationData;
        }

        public void ClearDirty()
        {
            _isDirty = false;
        }

        /// <summary>
        /// Checks if an item has been donated.
        /// </summary>
        public bool HasDonated(string itemId)
        {
            return _donationData?.HasDonated(itemId) ?? false;
        }

        /// <summary>
        /// Checks if an item has been donated by game item ID.
        /// </summary>
        public bool HasDonatedByGameId(int gameItemId)
        {
            var museumIds = GetMuseumItemIdsForGameItem(gameItemId);
            if (museumIds.Count == 0)
                return false;

            foreach (var museumItemId in museumIds)
            {
                if (!HasDonated(museumItemId))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Marks an item as donated.
        /// </summary>
        public void MarkDonated(string itemId)
        {
            if (_donationData == null) return;
            if (_donationData.HasDonated(itemId)) return;

            _donationData.MarkDonated(itemId);
            _isDirty = true;
            OnDonationsChanged?.Invoke();

            var item = MuseumContent.FindById(itemId);
            Plugin.Log?.LogInfo($"DonationManager: Marked {item?.Name ?? itemId} as donated");
        }

        /// <summary>
        /// Marks an item as donated by game item ID.
        /// </summary>
        public void MarkDonatedByGameId(int gameItemId)
        {
            foreach (var museumItemId in GetMuseumItemIdsForGameItem(gameItemId))
            {
                MarkDonated(museumItemId);
            }
        }

        private static List<string> GetMuseumItemIdsForGameItem(int gameItemId)
        {
            if (_museumIdsByGameItemId == null)
            {
                _museumIdsByGameItemId = MuseumContent
                    .GetAllItems()
                    .GroupBy(item => item.GameItemId)
                    .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList());
            }

            return _museumIdsByGameItemId.TryGetValue(gameItemId, out var ids)
                ? ids
                : new List<string>();
        }

        /// <summary>
        /// Toggles the donation status of an item (for manual tracking).
        /// </summary>
        public void ToggleDonated(string itemId)
        {
            if (_donationData == null) return;

            if (_donationData.HasDonated(itemId))
            {
                _donationData.UnmarkDonated(itemId);
            }
            else
            {
                _donationData.MarkDonated(itemId);
            }

            _isDirty = true;
            OnDonationsChanged?.Invoke();
        }

        /// <summary>
        /// Gets donation statistics for a section.
        /// </summary>
        public (int donated, int total) GetSectionStats(MuseumSection section)
        {
            int total = 0;
            int donated = 0;

            foreach (var bundle in section.Bundles)
            {
                var bundleStats = GetBundleStats(bundle);
                total += bundleStats.total;
                donated += bundleStats.donated;
            }

            return (donated, total);
        }

        /// <summary>
        /// Gets donation statistics for a bundle.
        /// </summary>
        public (int donated, int total) GetBundleStats(MuseumBundle bundle)
        {
            int total = bundle.Items.Count;
            int donated = bundle.Items.Count(item => HasDonated(item.Id));
            return (donated, total);
        }

        /// <summary>
        /// Gets overall donation statistics.
        /// </summary>
        public (int donated, int total) GetOverallStats()
        {
            var allItems = MuseumContent.GetAllItems();
            int total = allItems.Count;
            int donated = allItems.Count(item => HasDonated(item.Id));
            return (donated, total);
        }

        /// <summary>
        /// Gets a list of items still needed for a bundle.
        /// </summary>
        public List<MuseumItem> GetNeededItems(MuseumBundle bundle)
        {
            return bundle.Items.Where(item => !HasDonated(item.Id)).ToList();
        }

        /// <summary>
        /// Gets a list of all items still needed.
        /// </summary>
        public List<MuseumItem> GetAllNeededItems()
        {
            return MuseumContent.GetAllItems().Where(item => !HasDonated(item.Id)).ToList();
        }

        /// <summary>
        /// Checks if a bundle is complete.
        /// </summary>
        public bool IsBundleComplete(MuseumBundle bundle)
        {
            return bundle.Items.All(item => HasDonated(item.Id));
        }

        /// <summary>
        /// Checks if a section is complete.
        /// </summary>
        public bool IsSectionComplete(MuseumSection section)
        {
            return section.Bundles.All(IsBundleComplete);
        }

        /// <summary>
        /// Gets completion percentage for a section.
        /// </summary>
        public float GetSectionCompletionPercent(MuseumSection section)
        {
            var stats = GetSectionStats(section);
            return stats.total > 0 ? (float)stats.donated / stats.total * 100f : 0f;
        }

        /// <summary>
        /// Gets overall completion percentage.
        /// </summary>
        public float GetOverallCompletionPercent()
        {
            var stats = GetOverallStats();
            return stats.total > 0 ? (float)stats.donated / stats.total * 100f : 0f;
        }
    }
}
