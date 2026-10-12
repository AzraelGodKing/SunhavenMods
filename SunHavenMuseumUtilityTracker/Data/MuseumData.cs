using System;
using System.Collections.Generic;

namespace SunHavenMuseumUtilityTracker.Data
{
    /// <summary>
    /// Represents a section of the museum (Hall of Gems, Hall of Culture, Aquarium).
    /// </summary>
    [Serializable]
    public class MuseumSection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<MuseumBundle> Bundles { get; set; } = new List<MuseumBundle>();

        public MuseumSection() { }

        public MuseumSection(string id, string name, string description = "")
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Represents a bundle of related items within a museum section.
    /// </summary>
    [Serializable]
    public class MuseumBundle
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string SectionId { get; set; }
        public List<MuseumItem> Items { get; set; } = new List<MuseumItem>();

        public MuseumBundle() { }

        public MuseumBundle(string id, string name, string sectionId, string description = "")
        {
            Id = id;
            Name = name;
            SectionId = sectionId;
            Description = description;
        }
    }

    /// <summary>
    /// Represents an individual item that can be donated to the museum.
    /// </summary>
    [Serializable]
    public class MuseumItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string BundleId { get; set; }
        public int GameItemId { get; set; }
        public string Description { get; set; }
        public ItemRarity Rarity { get; set; }

        public MuseumItem() { }

        public MuseumItem(string id, string name, string bundleId, int gameItemId, ItemRarity rarity = ItemRarity.Common, string description = "")
        {
            Id = id;
            Name = name;
            BundleId = bundleId;
            GameItemId = gameItemId;
            Rarity = rarity;
            Description = description;
        }
    }

    /// <summary>
    /// Item rarity for visual distinction.
    /// </summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// Tracks which items have been donated by the player.
    /// </summary>
    [Serializable]
    public class DonationData
    {
        public string CharacterName { get; set; }
        public HashSet<string> DonatedItemIds { get; set; } = new HashSet<string>();
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public DonationData() { }

        public DonationData(string characterName)
        {
            CharacterName = characterName;
        }

        public bool HasDonated(string itemId)
        {
            return DonatedItemIds.Contains(itemId);
        }

        public void MarkDonated(string itemId)
        {
            DonatedItemIds.Add(itemId);
            LastUpdated = DateTime.Now;
        }

        public void UnmarkDonated(string itemId)
        {
            DonatedItemIds.Remove(itemId);
            LastUpdated = DateTime.Now;
        }
    }

    /// <summary>
    /// Wrapper for Unity JSON serialization (parallel arrays).
    /// </summary>
    [Serializable]
    public class DonationDataWrapper
    {
        public string CharacterName;
        public List<string> DonatedItemIds = new List<string>();
        public string LastUpdated;

        public DonationDataWrapper() { }

        public DonationDataWrapper(DonationData data)
        {
            CharacterName = data.CharacterName;
            DonatedItemIds = new List<string>(data.DonatedItemIds);
            LastUpdated = data.LastUpdated.ToString("o");
        }

        public DonationData ToData()
        {
            var data = new DonationData
            {
                CharacterName = CharacterName,
                DonatedItemIds = new HashSet<string>(DonatedItemIds)
            };

            if (DateTime.TryParse(LastUpdated, out var dt))
                data.LastUpdated = dt;

            return data;
        }
    }
}
