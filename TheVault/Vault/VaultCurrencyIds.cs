using System;
using System.Collections.Generic;

namespace TheVault.Vault
{
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
        public const string PrefixTicket = "ticket_";
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

        private static readonly Dictionary<string, ResolvedCurrencyId> ResolvedCache =
            new Dictionary<string, ResolvedCurrencyId>(StringComparer.Ordinal);
        private static readonly object ResolvedCacheLock = new object();

        /// <summary>
        /// Splits a full currency id into its kind and short id, cached per id.
        /// Hot paths (Inventory.GetAmount / HasEnough postfixes, HUD refresh) call this instead of
        /// re-running prefix checks, Substring and Enum.TryParse on every call (AZR-732).
        /// </summary>
        public static ResolvedCurrencyId Resolve(string fullId)
        {
            if (string.IsNullOrEmpty(fullId))
                return ResolvedCurrencyId.None;

            lock (ResolvedCacheLock)
            {
                if (ResolvedCache.TryGetValue(fullId, out var cached))
                    return cached;

                var resolved = ResolveUncached(fullId);
                ResolvedCache[fullId] = resolved;
                return resolved;
            }
        }

        private static ResolvedCurrencyId ResolveUncached(string fullId)
        {
            if (TryStripPrefix(fullId, PrefixSeasonal, out var rest))
            {
                return Enum.TryParse<SeasonalTokenType>(rest, out var token)
                    ? new ResolvedCurrencyId(CurrencyIdKind.Seasonal, rest, token)
                    : ResolvedCurrencyId.None;
            }
            if (TryStripPrefix(fullId, PrefixCommunity, out rest))
                return new ResolvedCurrencyId(CurrencyIdKind.Community, rest, default);
            if (TryStripPrefix(fullId, PrefixSpecial, out rest))
                return new ResolvedCurrencyId(CurrencyIdKind.Special, rest, default);
            if (TryStripPrefix(fullId, PrefixKey, out rest))
                return new ResolvedCurrencyId(CurrencyIdKind.Key, rest, default);
            if (TryStripPrefix(fullId, PrefixTicket, out rest))
                return new ResolvedCurrencyId(CurrencyIdKind.Ticket, rest, default);
            if (TryStripPrefix(fullId, PrefixOrb, out rest))
                return new ResolvedCurrencyId(CurrencyIdKind.Orb, rest, default);
            if (TryStripPrefix(fullId, PrefixCustom, out rest))
                return new ResolvedCurrencyId(CurrencyIdKind.Custom, rest, default);
            return ResolvedCurrencyId.None;
        }

        public static bool TryStripPrefix(string fullId, string prefix, out string remainder)
        {
            remainder = null;
            if (string.IsNullOrEmpty(fullId) || string.IsNullOrEmpty(prefix)) return false;
            if (!fullId.StartsWith(prefix, StringComparison.Ordinal)) return false;
            remainder = fullId.Substring(prefix.Length);
            return true;
        }
    }

    public enum CurrencyIdKind
    {
        None,
        Seasonal,
        Community,
        Special,
        Key,
        Ticket,
        Orb,
        Custom
    }

    /// <summary>A full currency id split into kind + short id (and the parsed token for seasonal ids).</summary>
    public readonly struct ResolvedCurrencyId
    {
        public static readonly ResolvedCurrencyId None = new ResolvedCurrencyId(CurrencyIdKind.None, null, default);

        public ResolvedCurrencyId(CurrencyIdKind kind, string shortId, SeasonalTokenType seasonalToken)
        {
            Kind = kind;
            ShortId = shortId;
            SeasonalToken = seasonalToken;
        }

        public CurrencyIdKind Kind { get; }
        public string ShortId { get; }
        public SeasonalTokenType SeasonalToken { get; }
    }
}
