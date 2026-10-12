using System;
using System.Collections.Generic;

namespace TheVault.Vault
{
    /// <summary>
    /// Core manager for vault operations. Handles all currency transactions
    /// and provides the API for other systems to check/modify vault values.
    /// </summary>
    public class VaultManager
    {
        private VaultData _vaultData;
        private Dictionary<string, CurrencyDefinition> _currencyDefinitions;
        private bool _isDirty;

        /// <summary>
        /// Event fired when any vault value changes
        /// </summary>
        public event Action<string, int, int> OnCurrencyChanged;

        /// <summary>
        /// Event fired when vault is loaded
        /// </summary>
        public event Action OnVaultLoaded;

        public VaultManager()
        {
            _vaultData = new VaultData();
            _currencyDefinitions = new Dictionary<string, CurrencyDefinition>();
            _isDirty = false;
            InitializeCurrencyDefinitions();
        }

        /// <summary>
        /// Initialize known currency definitions with their game item IDs.
        /// </summary>
        private void InitializeCurrencyDefinitions()
        {
            // Seasonal Tokens - Actual Sun Haven item IDs
            RegisterCurrency(new CurrencyDefinition("token_spring", "Spring Token", CurrencyCategory.SeasonalToken, 18020));
            RegisterCurrency(new CurrencyDefinition("token_summer", "Summer Token", CurrencyCategory.SeasonalToken, 18021));
            RegisterCurrency(new CurrencyDefinition("token_fall", "Fall Token", CurrencyCategory.SeasonalToken, 18023));
            RegisterCurrency(new CurrencyDefinition("token_winter", "Winter Token", CurrencyCategory.SeasonalToken, 18022));

            // Keys - Actual Sun Haven item IDs
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.KeyCopper, "Copper Key", CurrencyCategory.Key, 1251));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.KeyIron, "Iron Key", CurrencyCategory.Key, 1252));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.KeyAdamant, "Adamant Key", CurrencyCategory.Key, 1253));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.KeyMithril, "Mithril Key", CurrencyCategory.Key, 1254));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.KeySunite, "Sunite Key", CurrencyCategory.Key, 1255));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.KeyGlorite, "Glorite Key", CurrencyCategory.Key, 1256));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.KeyKingsLostMine, "King's Lost Mine Key", CurrencyCategory.Key, 1257));

            // Special currencies (includes Community Token)
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.SpecialCommunityToken, "Community Token", CurrencyCategory.Special, 18013));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.SpecialDoubloon, "Doubloon", CurrencyCategory.Special, 60014));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.SpecialBlackBottleCap, "Black Bottle Cap", CurrencyCategory.Special, 60013));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.SpecialRedCarnivalTicket, "Red Carnival Ticket", CurrencyCategory.Special, 18012));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.SpecialCandyCornPieces, "Candy Corn Pieces", CurrencyCategory.Special, 18016));
            RegisterCurrency(new CurrencyDefinition(VaultCurrencyIds.SpecialManaShard, "Mana Shard", CurrencyCategory.Special, 18015));

            Plugin.Log?.LogInfo($"Initialized {_currencyDefinitions.Count} currency definitions");
        }

        /// <summary>
        /// Register a new currency definition
        /// </summary>
        public void RegisterCurrency(CurrencyDefinition definition)
        {
            _currencyDefinitions[definition.Id] = definition;
        }

        /// <summary>
        /// Get a currency definition by its ID
        /// </summary>
        public CurrencyDefinition GetCurrencyDefinition(string currencyId)
        {
            return _currencyDefinitions.TryGetValue(currencyId, out var def) ? def : null;
        }

        /// <summary>
        /// Get currencies by category
        /// </summary>
        public IEnumerable<CurrencyDefinition> GetCurrenciesByCategory(CurrencyCategory category)
        {
            foreach (var kvp in _currencyDefinitions)
            {
                if (kvp.Value.Category == category)
                    yield return kvp.Value;
            }
        }

        /// <summary>
        /// Load vault data (called by save system)
        /// </summary>
        public void LoadVaultData(VaultData data)
        {
            _vaultData = data ?? new VaultData();
            int fixes = VaultDataSanitizer.Sanitize(_vaultData);
            if (fixes > 0)
                Plugin.Log?.LogWarning($"[Vault] Sanitized vault data ({fixes} correction(s)); negative or missing fields were normalized.");
            _isDirty = false;
            Plugin.Log?.LogInfo($"Vault data loaded for player: {_vaultData.PlayerName}");
            OnVaultLoaded?.Invoke();
        }

        /// <summary>
        /// Get current vault data for saving
        /// </summary>
        public VaultData GetVaultData()
        {
            _vaultData.LastSaved = DateTime.Now;
            return _vaultData;
        }

        /// <summary>
        /// Check if vault has unsaved changes
        /// </summary>
        public bool IsDirty => _isDirty;

        /// <summary>
        /// Mark vault as saved
        /// </summary>
        public void MarkClean()
        {
            _isDirty = false;
        }

        /// <summary>
        /// Set the player name for this vault
        /// </summary>
        public void SetPlayerName(string playerName)
        {
            _vaultData.PlayerName = playerName;
            _isDirty = true;
        }

        #region Seasonal Tokens

        public int GetSeasonalTokens(SeasonalTokenType type)
        {
            return _vaultData.SeasonalTokens.TryGetValue(type, out int count) ? count : 0;
        }

        public bool AddSeasonalTokens(SeasonalTokenType type, int amount)
        {
            if (amount < 0) return false;

            int oldValue = GetSeasonalTokens(type);
            _vaultData.SeasonalTokens[type] = oldValue + amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke(VaultCurrencyIds.FullSeasonalFromTokenName(type.ToString()), oldValue, oldValue + amount);
            Plugin.Log?.LogInfo($"Added {amount} {type} tokens. New total: {_vaultData.SeasonalTokens[type]}");
            return true;
        }

        public bool RemoveSeasonalTokens(SeasonalTokenType type, int amount)
        {
            if (amount < 0) return false;

            int current = GetSeasonalTokens(type);
            if (current < amount) return false;

            int oldValue = current;
            _vaultData.SeasonalTokens[type] = current - amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke(VaultCurrencyIds.FullSeasonalFromTokenName(type.ToString()), oldValue, current - amount);
            Plugin.Log?.LogInfo($"Removed {amount} {type} tokens. New total: {_vaultData.SeasonalTokens[type]}");
            return true;
        }

        #endregion

        #region Community Tokens

        public int GetCommunityTokens(string tokenId)
        {
            return _vaultData.CommunityTokens.TryGetValue(tokenId, out int count) ? count : 0;
        }

        public bool AddCommunityTokens(string tokenId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(tokenId)) return false;

            int oldValue = GetCommunityTokens(tokenId);
            _vaultData.CommunityTokens[tokenId] = oldValue + amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke(VaultCurrencyIds.PrefixCommunity + tokenId, oldValue, oldValue + amount);
            return true;
        }

        public bool RemoveCommunityTokens(string tokenId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(tokenId)) return false;

            int current = GetCommunityTokens(tokenId);
            if (current < amount) return false;

            int oldValue = current;
            _vaultData.CommunityTokens[tokenId] = current - amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke($"community_{tokenId}", oldValue, current - amount);
            return true;
        }

        #endregion

        #region Keys

        public int GetKeys(string keyId)
        {
            return _vaultData.Keys.TryGetValue(keyId, out int count) ? count : 0;
        }

        public bool AddKeys(string keyId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(keyId)) return false;

            int oldValue = GetKeys(keyId);
            _vaultData.Keys[keyId] = oldValue + amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke(VaultCurrencyIds.PrefixKey + keyId, oldValue, oldValue + amount);
            Plugin.Log?.LogInfo($"Added {amount} {keyId} keys. New total: {_vaultData.Keys[keyId]}");
            return true;
        }

        public bool RemoveKeys(string keyId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(keyId)) return false;

            int current = GetKeys(keyId);
            if (current < amount) return false;

            int oldValue = current;
            _vaultData.Keys[keyId] = current - amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke(VaultCurrencyIds.PrefixKey + keyId, oldValue, current - amount);
            return true;
        }

        #endregion

        #region Special (uses Tickets storage for backwards compatibility)

        public int GetSpecial(string specialId)
        {
            return _vaultData.Tickets.TryGetValue(specialId, out int count) ? count : 0;
        }

        public bool AddSpecial(string specialId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(specialId)) return false;

            int oldValue = GetSpecial(specialId);
            _vaultData.Tickets[specialId] = oldValue + amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke(VaultCurrencyIds.PrefixSpecial + specialId, oldValue, oldValue + amount);
            return true;
        }

        public bool RemoveSpecial(string specialId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(specialId)) return false;

            int current = GetSpecial(specialId);
            if (current < amount) return false;

            int oldValue = current;
            _vaultData.Tickets[specialId] = current - amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke(VaultCurrencyIds.PrefixSpecial + specialId, oldValue, current - amount);
            return true;
        }

        #endregion

        #region Orbs

        public int GetOrbs(string orbId)
        {
            return _vaultData.Orbs.TryGetValue(orbId, out int count) ? count : 0;
        }

        public bool AddOrbs(string orbId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(orbId)) return false;

            int oldValue = GetOrbs(orbId);
            _vaultData.Orbs[orbId] = oldValue + amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke($"orb_{orbId}", oldValue, oldValue + amount);
            return true;
        }

        public bool RemoveOrbs(string orbId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(orbId)) return false;

            int current = GetOrbs(orbId);
            if (current < amount) return false;

            int oldValue = current;
            _vaultData.Orbs[orbId] = current - amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke(VaultCurrencyIds.PrefixOrb + orbId, oldValue, current - amount);
            return true;
        }

        #endregion

        #region Custom Currencies

        public int GetCustomCurrency(string currencyId)
        {
            return _vaultData.CustomCurrencies.TryGetValue(currencyId, out int count) ? count : 0;
        }

        public bool AddCustomCurrency(string currencyId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(currencyId)) return false;

            int oldValue = GetCustomCurrency(currencyId);
            _vaultData.CustomCurrencies[currencyId] = oldValue + amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke(VaultCurrencyIds.FullCustom(currencyId), oldValue, oldValue + amount);
            return true;
        }

        public bool RemoveCustomCurrency(string currencyId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(currencyId)) return false;

            int current = GetCustomCurrency(currencyId);
            if (current < amount) return false;

            int oldValue = current;
            _vaultData.CustomCurrencies[currencyId] = current - amount;
            _isDirty = true;

            OnCurrencyChanged?.Invoke(VaultCurrencyIds.FullCustom(currencyId), oldValue, current - amount);
            return true;
        }

        #endregion

        #region Generic Currency Operations

        /// <summary>
        /// Generic method to get any currency by its full ID
        /// </summary>
        public int GetCurrency(string fullCurrencyId)
        {
            if (!VaultCurrencyIds.TryDispatch(fullCurrencyId, out var kind, out var remainder))
                return 0;

            switch (kind)
            {
                case VaultCurrencyKind.Seasonal:
                    return Enum.TryParse<SeasonalTokenType>(remainder, out var tokenType)
                        ? GetSeasonalTokens(tokenType)
                        : 0;
                case VaultCurrencyKind.Community:
                    return GetCommunityTokens(remainder);
                case VaultCurrencyKind.Key:
                    return GetKeys(remainder);
                case VaultCurrencyKind.Special:
                    return GetSpecial(remainder);
                case VaultCurrencyKind.Orb:
                    return GetOrbs(remainder);
                case VaultCurrencyKind.Custom:
                    return GetCustomCurrency(remainder);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Credits vault currency. Returns false when the id is unrecognized so callers can roll back inventory.
        /// </summary>
        public bool AddCurrency(string fullCurrencyId, int amount)
        {
            if (string.IsNullOrEmpty(fullCurrencyId) || amount <= 0)
                return false;
            if (!VaultCurrencyIds.TryDispatch(fullCurrencyId, out var kind, out var remainder))
            {
                Plugin.Log?.LogWarning($"AddCurrency: unrecognized currency id '{fullCurrencyId}'");
                return false;
            }

            switch (kind)
            {
                case VaultCurrencyKind.Seasonal:
                    if (!Enum.TryParse<SeasonalTokenType>(remainder, out var tokenType))
                    {
                        Plugin.Log?.LogWarning($"AddCurrency: invalid seasonal type '{remainder}' in '{fullCurrencyId}'");
                        return false;
                    }
                    return AddSeasonalTokens(tokenType, amount);
                case VaultCurrencyKind.Community:
                    return AddCommunityTokens(remainder, amount);
                case VaultCurrencyKind.Key:
                    return AddKeys(remainder, amount);
                case VaultCurrencyKind.Special:
                    return AddSpecial(remainder, amount);
                case VaultCurrencyKind.Orb:
                    return AddOrbs(remainder, amount);
                case VaultCurrencyKind.Custom:
                    return AddCustomCurrency(remainder, amount);
                default:
                    return false;
            }
        }

        public bool RemoveCurrency(string fullCurrencyId, int amount)
        {
            if (string.IsNullOrEmpty(fullCurrencyId) || amount <= 0)
                return false;
            if (!VaultCurrencyIds.TryDispatch(fullCurrencyId, out var kind, out var remainder))
                return false;

            switch (kind)
            {
                case VaultCurrencyKind.Seasonal:
                    return Enum.TryParse<SeasonalTokenType>(remainder, out var tokenType)
                        && RemoveSeasonalTokens(tokenType, amount);
                case VaultCurrencyKind.Community:
                    return RemoveCommunityTokens(remainder, amount);
                case VaultCurrencyKind.Key:
                    return RemoveKeys(remainder, amount);
                case VaultCurrencyKind.Special:
                    return RemoveSpecial(remainder, amount);
                case VaultCurrencyKind.Orb:
                    return RemoveOrbs(remainder, amount);
                case VaultCurrencyKind.Custom:
                    return RemoveCustomCurrency(remainder, amount);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Generic method to check if player has enough of any currency
        /// </summary>
        public bool HasCurrency(string fullCurrencyId, int amount)
        {
            return GetCurrency(fullCurrencyId) >= amount;
        }

        /// <summary>
        /// Get a summary of all non-zero currencies for display
        /// </summary>
        public Dictionary<string, int> GetAllNonZeroCurrencies()
        {
            var result = new Dictionary<string, int>();

            foreach (var kvp in _vaultData.SeasonalTokens)
            {
                if (kvp.Value > 0)
                    result[VaultCurrencyIds.FullSeasonalFromTokenName(kvp.Key.ToString())] = kvp.Value;
            }

            foreach (var kvp in _vaultData.CommunityTokens)
            {
                if (kvp.Value > 0)
                    result[VaultCurrencyIds.PrefixCommunity + kvp.Key] = kvp.Value;
            }

            foreach (var kvp in _vaultData.Keys)
            {
                if (kvp.Value > 0)
                    result[VaultCurrencyIds.PrefixKey + kvp.Key] = kvp.Value;
            }

            foreach (var kvp in _vaultData.Tickets)
            {
                if (kvp.Value > 0)
                    result[VaultCurrencyIds.PrefixSpecial + kvp.Key] = kvp.Value;
            }

            foreach (var kvp in _vaultData.Orbs)
            {
                if (kvp.Value > 0)
                    result[VaultCurrencyIds.PrefixOrb + kvp.Key] = kvp.Value;
            }

            foreach (var kvp in _vaultData.CustomCurrencies)
            {
                if (kvp.Value > 0)
                    result[VaultCurrencyIds.FullCustom(kvp.Key)] = kvp.Value;
            }

            return result;
        }

        /// <summary>
        /// Merge <see cref="VaultData.CustomCurrencies"/> keys that have no <see cref="CurrencyDefinition"/> (debug / orphan detection).
        /// Keys in <paramref name="keyedByFullCurrencyId"/> use the <c>custom_</c> prefix.
        /// </summary>
        public void MergeOrphanCustomBalances(Dictionary<string, int> keyedByFullCurrencyId)
        {
            if (keyedByFullCurrencyId == null || _vaultData?.CustomCurrencies == null) return;

            foreach (var kvp in _vaultData.CustomCurrencies)
            {
                string full = VaultCurrencyIds.FullCustom(kvp.Key);
                if (!keyedByFullCurrencyId.ContainsKey(full))
                    keyedByFullCurrencyId[full] = kvp.Value;
            }
        }

        /// <summary>
        /// HUD debug: every registered custom id plus any save-only custom keys, with balances (including 0).
        /// </summary>
        public void FillDebugCustomHud(Dictionary<string, int> dest)
        {
            if (dest == null) return;
            dest.Clear();
            if (_vaultData == null) return;

            var temp = new Dictionary<string, int>();

            foreach (var def in GetCurrenciesByCategory(CurrencyCategory.Custom))
            {
                string full = VaultCurrencyIds.FullCustom(def.Id);
                temp[full] = GetCustomCurrency(def.Id);
            }

            if (_vaultData.CustomCurrencies != null)
            {
                foreach (var kvp in _vaultData.CustomCurrencies)
                {
                    string full = VaultCurrencyIds.FullCustom(kvp.Key);
                    if (!temp.ContainsKey(full))
                        temp[full] = kvp.Value;
                }
            }

            var keys = new List<string>(temp.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string k in keys)
                dest[k] = temp[k];
        }

        /// <summary>
        /// Human-readable dump of every vault store (for the in-game Debug tab).
        /// </summary>
        public List<string> BuildDebugVaultReport()
        {
            var lines = new List<string>();
            if (_vaultData == null)
            {
                lines.Add("(no vault data)");
                return lines;
            }

            lines.Add($"PlayerName={_vaultData.PlayerName ?? ""}");
            lines.Add($"LastSaved={_vaultData.LastSaved:yyyy-MM-dd HH:mm:ss}");
            lines.Add($"Version={_vaultData.Version}");
            lines.Add("");

            lines.Add("[SeasonalTokens]");
            foreach (SeasonalTokenType t in Enum.GetValues(typeof(SeasonalTokenType)))
            {
                int v = _vaultData.SeasonalTokens != null && _vaultData.SeasonalTokens.TryGetValue(t, out int c) ? c : 0;
                lines.Add($"  {t} = {v}");
            }

            lines.Add("");
            lines.Add("[CommunityTokens]");
            AppendSortedStringDict(lines, _vaultData.CommunityTokens);

            lines.Add("");
            lines.Add("[Keys]");
            AppendSortedStringDict(lines, _vaultData.Keys);

            lines.Add("");
            lines.Add("[Tickets / special_* backing store]");
            AppendSortedStringDict(lines, _vaultData.Tickets);

            lines.Add("");
            lines.Add("[Orbs]");
            AppendSortedStringDict(lines, _vaultData.Orbs);

            lines.Add("");
            lines.Add("[CustomCurrencies]");
            AppendSortedStringDict(lines, _vaultData.CustomCurrencies);

            return lines;
        }

        private static void AppendSortedStringDict(List<string> lines, Dictionary<string, int> dict)
        {
            if (dict == null || dict.Count == 0)
            {
                lines.Add("  (empty)");
                return;
            }

            var keys = new List<string>(dict.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string k in keys)
                lines.Add($"  {k} = {dict[k]}");
        }

        #endregion
    }
}
