using System;
using System.Collections.Generic;
using TheVault.Vault;
using UnityEngine;

namespace TheVault.UI
{
    /// <summary>
    /// Shared list/label/formatting for vault windows.
    /// </summary>
    public static class VaultUiShared
    {
        public static Dictionary<string, int> GetCurrenciesForCategory(VaultManager vaultManager, CurrencyCategory category)
        {
            var result = new Dictionary<string, int>();
            if (vaultManager == null) return result;

            bool showAllBalances = Plugin.GetConfigDebugFullVaultInspector();

            foreach (var currency in vaultManager.GetCurrenciesByCategory(category))
            {
                string keyId = currency.Id;
                int amount = 0;

                switch (category)
                {
                    case CurrencyCategory.SeasonalToken:
                        string tokenName = keyId.Replace("token_", "");
                        tokenName = char.ToUpper(tokenName[0]) + tokenName.Substring(1);
                        if (Enum.TryParse<SeasonalTokenType>(tokenName, out var tokenType))
                            amount = vaultManager.GetSeasonalTokens(tokenType);
                        if (showAllBalances || amount != 0)
                            result[$"seasonal_{tokenName}"] = amount;
                        break;

                    case CurrencyCategory.CommunityToken:
                        string communityId = keyId.Replace("token_", "");
                        amount = vaultManager.GetCommunityTokens(communityId);
                        if (showAllBalances || amount != 0)
                            result[$"community_{communityId}"] = amount;
                        break;

                    case CurrencyCategory.Key:
                        string keyName = keyId.Replace("key_", "");
                        amount = vaultManager.GetKeys(keyName);
                        if (showAllBalances || amount != 0)
                            result[$"key_{keyName}"] = amount;
                        break;

                    case CurrencyCategory.Special:
                        string specialId = keyId.Replace("special_", "");
                        amount = vaultManager.GetSpecial(specialId);
                        if (showAllBalances || amount != 0)
                            result[$"special_{specialId}"] = amount;
                        break;

                    case CurrencyCategory.Orb:
                        string orbId = keyId.Replace("orb_", "");
                        amount = vaultManager.GetOrbs(orbId);
                        if (showAllBalances || amount != 0)
                            result[$"orb_{orbId}"] = amount;
                        break;

                    case CurrencyCategory.Custom:
                        amount = vaultManager.GetCustomCurrency(keyId);
                        if (showAllBalances || amount != 0)
                            result[$"custom_{keyId}"] = amount;
                        break;
                }
            }

            if (category == CurrencyCategory.Custom && showAllBalances)
                vaultManager.MergeOrphanCustomBalances(result);

            if (category == CurrencyCategory.Custom && result.Count > 1)
            {
                var keys = new List<string>(result.Keys);
                keys.Sort(StringComparer.Ordinal);
                var ordered = new Dictionary<string, int>(keys.Count);
                foreach (string k in keys)
                    ordered[k] = result[k];
                return ordered;
            }

            return result;
        }

        public static string GetDisplayName(VaultManager vaultManager, string currencyId)
        {
            if (currencyId.StartsWith("seasonal_"))
                return currencyId.Substring("seasonal_".Length) + " Token";
            if (currencyId.StartsWith("community_"))
            {
                string id = currencyId.Substring("community_".Length);
                return id == "community" ? "Community Token" : "Community " + CapitalizeFirst(id);
            }
            if (currencyId.StartsWith("key_"))
                return FormatKeyName(currencyId.Substring("key_".Length));
            if (currencyId.StartsWith("special_"))
                return FormatSpecialName(currencyId.Substring("special_".Length));
            if (currencyId.StartsWith("orb_"))
                return CapitalizeFirst(currencyId.Substring("orb_".Length)) + " Orb";
            if (currencyId.StartsWith("custom_") && vaultManager != null)
            {
                string shortId = currencyId.Substring("custom_".Length);
                var def = vaultManager.GetCurrencyDefinition(shortId);
                if (def != null && !string.IsNullOrEmpty(def.DisplayName))
                    return def.DisplayName;
                return CapitalizeFirst(shortId.Replace("_", " "));
            }

            return currencyId;
        }

        public static string GetCurrencyIcon(string currencyId)
        {
            if (currencyId.StartsWith("seasonal_"))
            {
                string season = currencyId.Substring("seasonal_".Length).ToLower();
                return season switch
                {
                    "spring" => "[Sp]",
                    "summer" => "[Su]",
                    "fall" => "[Fa]",
                    "winter" => "[Wi]",
                    _ => "[T]"
                };
            }
            if (currencyId.StartsWith("community_")) return "[C]";
            if (currencyId.StartsWith("key_")) return "[K]";
            if (currencyId.StartsWith("special_"))
            {
                string specialName = currencyId.Substring("special_".Length).ToLower();
                return specialName switch
                {
                    "communitytoken" => "[C]",
                    "doubloon" => "[D]",
                    "blackbottlecap" => "[B]",
                    "redcarnivalticket" => "[R]",
                    "candycornpieces" => "[CC]",
                    "manashard" => "[M]",
                    _ => "[S]"
                };
            }
            if (currencyId.StartsWith("orb_")) return "[O]";
            if (currencyId.StartsWith("custom_")) return "[+]";
            return "[?]";
        }

        public static string FormatNumber(int value)
        {
            if (value >= 1000000)
            {
                float millions = value / 1000000f;
                if (millions >= 10f || millions == (int)millions)
                    return $"{(int)millions}M";
                return $"{millions:0.#}M";
            }
            if (value >= 1000)
            {
                float thousands = value / 1000f;
                if (thousands >= 10f || thousands == (int)thousands)
                    return $"{(int)thousands}K";
                return $"{thousands:0.#}K";
            }
            return value.ToString();
        }

        private static string CapitalizeFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        private static string FormatKeyName(string keyName)
        {
            return keyName switch
            {
                "copper" => "Copper Key",
                "iron" => "Iron Key",
                "adamant" => "Adamant Key",
                "mithril" => "Mithril Key",
                "sunite" => "Sunite Key",
                "glorite" => "Glorite Key",
                "kingslostmine" => "King's Lost Mine Key",
                _ => CapitalizeFirst(keyName) + " Key"
            };
        }

        private static string FormatSpecialName(string specialName)
        {
            return specialName switch
            {
                "communitytoken" => "Community Token",
                "doubloon" => "Doubloon",
                "blackbottlecap" => "Black Bottle Cap",
                "redcarnivalticket" => "Red Carnival Ticket",
                "candycornpieces" => "Candy Corn Pieces",
                "manashard" => "Mana Shard",
                _ => CapitalizeFirst(specialName)
            };
        }
    }
}
