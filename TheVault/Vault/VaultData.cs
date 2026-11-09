using System;
using System.Collections.Generic;

namespace TheVault.Vault
{
    /// <summary>
    /// Serializable data class that stores all vault currency values.
    /// This represents numeric counts of tokens, keys, and other collectibles
    /// instead of actual inventory items.
    /// </summary>
    [Serializable]
    public class VaultData
    {
        /// <summary>
        /// Version number for save compatibility
        /// </summary>
        public int Version { get; set; } = 2;

        /// <summary>
        /// Seasonal tokens (Spring, Summer, Fall, Winter festival tokens)
        /// </summary>
        public Dictionary<SeasonalTokenType, int> SeasonalTokens { get; set; }

        /// <summary>
        /// Community/event tokens
        /// </summary>
        public Dictionary<string, int> CommunityTokens { get; set; }

        /// <summary>
        /// Keys for various doors/areas (key name -> count)
        /// </summary>
        public Dictionary<string, int> Keys { get; set; }

        /// <summary>
        /// Generic currencies that can be extended (currency ID -> count)
        /// </summary>
        public Dictionary<string, int> CustomCurrencies { get; set; }

        /// <summary>
        /// Tickets (museum, carnival, etc.)
        /// </summary>
        public Dictionary<string, int> Tickets { get; set; }

        /// <summary>
        /// Orbs and magical collectibles
        /// </summary>
        public Dictionary<string, int> Orbs { get; set; }

        /// <summary>
        /// Last save timestamp
        /// </summary>
        public DateTime LastSaved { get; set; }

        /// <summary>
        /// Player name this vault belongs to (for multi-save support)
        /// </summary>
        public string PlayerName { get; set; }

        public VaultData()
        {
            SeasonalTokens = new Dictionary<SeasonalTokenType, int>();
            CommunityTokens = new Dictionary<string, int>();
            Keys = new Dictionary<string, int>();
            CustomCurrencies = new Dictionary<string, int>();
            Tickets = new Dictionary<string, int>();
            Orbs = new Dictionary<string, int>();
            LastSaved = DateTime.Now;
            PlayerName = "";

            // Initialize seasonal tokens to 0
            foreach (SeasonalTokenType tokenType in Enum.GetValues(typeof(SeasonalTokenType)))
            {
                SeasonalTokens[tokenType] = 0;
            }
        }
    }

    /// <summary>
    /// Types of seasonal festival tokens
    /// </summary>
    public enum SeasonalTokenType
    {
        Spring,
        Summer,
        Fall,
        Winter,
        Anniversary,
        Special
    }

    /// <summary>
    /// Represents a currency type for UI display and operations
    /// </summary>
    public class CurrencyDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public CurrencyCategory Category { get; set; }

        /// <summary>
        /// The item ID in Sun Haven's item database that corresponds to this currency.
        /// Used for deposit/withdraw conversions.
        /// </summary>
        public int GameItemId { get; set; }

        public CurrencyDefinition(string id, string displayName, CurrencyCategory category, int gameItemId = -1)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            GameItemId = gameItemId;
        }
    }

    /// <summary>
    /// Categories for organizing currencies in the UI
    /// </summary>
    public enum CurrencyCategory
    {
        SeasonalToken,
        CommunityToken,
        Key,
        Special,
        Orb,
        Custom
    }
}
