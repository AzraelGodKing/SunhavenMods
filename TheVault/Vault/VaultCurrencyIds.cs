using System;

namespace TheVault.Vault
{
    /// <summary>
    /// Prefix buckets for a full vault currency id (<c>seasonal_Spring</c>, <c>special_doubloon</c>, …).
    /// </summary>
    public enum VaultCurrencyKind
    {
        Seasonal,
        Community,
        Key,
        Special,
        Orb,
        Custom
    }

    /// <summary>
    /// Canonical prefixes and built-in currency ids (UI, HUD, saves, patches).
    /// </summary>
    public static class VaultCurrencyIds
    {
        public const string PrefixSeasonal = "seasonal_";
        public const string PrefixCommunity = "community_";
        public const string PrefixKey = "key_";
        public const string PrefixSpecial = "special_";
        public const string PrefixOrb = "orb_";
        public const string PrefixCustom = "custom_";

        public const string SeasonalSpring = PrefixSeasonal + "Spring";
        public const string SeasonalSummer = PrefixSeasonal + "Summer";
        public const string SeasonalFall = PrefixSeasonal + "Fall";
        public const string SeasonalWinter = PrefixSeasonal + "Winter";

        public const string KeyCopper = PrefixKey + "copper";
        public const string KeyIron = PrefixKey + "iron";
        public const string KeyAdamant = PrefixKey + "adamant";
        public const string KeyMithril = PrefixKey + "mithril";
        public const string KeySunite = PrefixKey + "sunite";
        public const string KeyGlorite = PrefixKey + "glorite";
        public const string KeyKingsLostMine = PrefixKey + "kingslostmine";

        public const string SpecialCommunityToken = PrefixSpecial + "communitytoken";
        public const string SpecialDoubloon = PrefixSpecial + "doubloon";
        public const string SpecialBlackBottleCap = PrefixSpecial + "blackbottlecap";
        public const string SpecialRedCarnivalTicket = PrefixSpecial + "redcarnivalticket";
        public const string SpecialCandyCornPieces = PrefixSpecial + "candycornpieces";
        public const string SpecialManaShard = PrefixSpecial + "manashard";

        public static readonly string[] AllSeasonalFullIds =
        {
            SeasonalSpring, SeasonalSummer, SeasonalFall, SeasonalWinter
        };

        public static readonly string[] AllKeyFullIds =
        {
            KeyCopper, KeyIron, KeyAdamant, KeyMithril, KeySunite, KeyGlorite, KeyKingsLostMine
        };

        public static readonly string[] AllSpecialFullIds =
        {
            SpecialCommunityToken, SpecialDoubloon, SpecialBlackBottleCap,
            SpecialRedCarnivalTicket, SpecialCandyCornPieces, SpecialManaShard
        };

        public static string FullCustom(string shortId) => PrefixCustom + shortId;

        public static string FullSeasonalFromTokenName(string tokenName) => PrefixSeasonal + tokenName;

        public static bool TryStripPrefix(string fullId, string prefix, out string remainder)
        {
            remainder = null;
            if (string.IsNullOrEmpty(fullId) || string.IsNullOrEmpty(prefix)) return false;
            if (!fullId.StartsWith(prefix, StringComparison.Ordinal)) return false;
            remainder = fullId.Substring(prefix.Length);
            return true;
        }

        /// <summary>
        /// Maps a full currency id to its storage bucket. Does not match the unused <c>ticket_</c> prefix
        /// (special currencies are stored under <see cref="PrefixSpecial"/>; the Tickets dict is unchanged).
        /// </summary>
        public static bool TryDispatch(string fullId, out VaultCurrencyKind kind, out string remainder)
        {
            kind = default;
            remainder = null;
            if (string.IsNullOrEmpty(fullId)) return false;

            if (TryStripPrefix(fullId, PrefixSeasonal, out remainder))
            {
                kind = VaultCurrencyKind.Seasonal;
                return true;
            }
            if (TryStripPrefix(fullId, PrefixCommunity, out remainder))
            {
                kind = VaultCurrencyKind.Community;
                return true;
            }
            if (TryStripPrefix(fullId, PrefixKey, out remainder))
            {
                kind = VaultCurrencyKind.Key;
                return true;
            }
            if (TryStripPrefix(fullId, PrefixSpecial, out remainder))
            {
                kind = VaultCurrencyKind.Special;
                return true;
            }
            if (TryStripPrefix(fullId, PrefixOrb, out remainder))
            {
                kind = VaultCurrencyKind.Orb;
                return true;
            }
            if (TryStripPrefix(fullId, PrefixCustom, out remainder))
            {
                kind = VaultCurrencyKind.Custom;
                return true;
            }

            remainder = null;
            return false;
        }
    }
}
