using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SunhavenMods.Shared;
using UnityEngine;
using Wish;

namespace HavenDevTools.Services
{
    /// <summary>
    /// Service for tracking and displaying currency balances (integrates with The Vault)
    /// </summary>
    public class CurrencyTracker
    {
        private const float SummaryRefreshIntervalSeconds = 0.5f;

        // Known currency item IDs in Sun Haven
        public static readonly Dictionary<string, int> CurrencyItemIds = new Dictionary<string, int>
        {
            // Seasonal Tokens
            { "Spring Token", 18020 },
            { "Summer Token", 18021 },
            { "Fall Token", 18023 },
            { "Winter Token", 18022 },

            // Keys
            { "Copper Key", 1251 },
            { "Iron Key", 1252 },
            { "Adamant Key", 1253 },
            { "Mithril Key", 1254 },
            { "Sunite Key", 1255 },
            { "Glorite Key", 1256 },
            { "King's Lost Mine Key", 1257 },

            // Special
            { "Community Token", 18013 },
            { "Doubloon", 60014 },
            { "Black Bottle Cap", 60013 },
            { "Red Carnival Ticket", 18012 },
            { "Candy Corn Pieces", 18016 },
            { "Mana Shard", 18015 }
        };

        private PropertyInfo _gameSaveCoinsProperty;
        private PropertyInfo _singletonInstanceProperty;
        private PropertyInfo _currentSaveProperty;
        private FieldInfo _currentSaveCoinsField;
        private PropertyInfo _playerOrbsProperty;
        private PropertyInfo _playerTicketsProperty;
        private MethodInfo _inventoryGetAmountMethod;
        private Type _vaultPluginType;
        private MethodInfo _vaultGetVaultManagerMethod;
        private MethodInfo _vaultGetAllNonZeroCurrenciesMethod;
        private bool _goldFallbackResolved;
        private bool _playerPropertiesResolved;
        private bool _vaultReflectionResolved;

        private CurrencySummary _cachedSummary;
        private float _lastSummaryRefreshTime = float.NegativeInfinity;

        public CurrencyTracker()
        {
            Plugin.Log?.LogInfo("[CurrencyTracker] Initialized");
        }

        /// <summary>
        /// Get player's inventory count for a currency item
        /// </summary>
        public int GetInventoryAmount(int itemId)
        {
            try
            {
                if (Player.Instance?.Inventory == null) return 0;

                var inventory = Player.Instance.Inventory;
                if (_inventoryGetAmountMethod == null)
                {
                    _inventoryGetAmountMethod = AccessTools.Method(
                        inventory.GetType(),
                        "GetAmount",
                        new[] { typeof(int) });
                }

                if (_inventoryGetAmountMethod != null)
                {
                    return (int)_inventoryGetAmountMethod.Invoke(inventory, new object[] { itemId });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[CurrencyTracker] Error getting inventory amount: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Get all vault currencies (requires The Vault mod)
        /// </summary>
        public Dictionary<string, int> GetAllVaultCurrencies()
        {
            var result = new Dictionary<string, int>();

            if (!Plugin.HasTheVault) return result;

            try
            {
                if (!EnsureVaultReflection()) return result;

                var vaultManager = _vaultGetVaultManagerMethod.Invoke(null, null);
                if (vaultManager == null) return result;

                if (_vaultGetAllNonZeroCurrenciesMethod != null)
                {
                    var currencies = _vaultGetAllNonZeroCurrenciesMethod.Invoke(vaultManager, null) as Dictionary<string, int>;
                    if (currencies != null)
                    {
                        return currencies;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[CurrencyTracker] Error getting all vault currencies: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get player's gold amount (stored in GameSave.Coins)
        /// </summary>
        public int GetGold()
        {
            try
            {
                EnsureGoldReflection();

                if (_gameSaveCoinsProperty != null)
                {
                    return (int)_gameSaveCoinsProperty.GetValue(null);
                }

                if (_singletonInstanceProperty != null && _currentSaveProperty != null && _currentSaveCoinsField != null)
                {
                    var instance = _singletonInstanceProperty.GetValue(null);
                    if (instance != null)
                    {
                        var currentSave = _currentSaveProperty.GetValue(instance);
                        if (currentSave != null)
                        {
                            return Convert.ToInt32(_currentSaveCoinsField.GetValue(currentSave));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[CurrencyTracker] Error getting gold: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Get player's orb/ticket amounts
        /// </summary>
        public int GetOrbs()
        {
            try
            {
                if (Player.Instance == null) return 0;

                EnsurePlayerProperties();
                if (_playerOrbsProperty != null)
                {
                    return (int)_playerOrbsProperty.GetValue(Player.Instance);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[CurrencyTracker] Error getting orbs: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Get player's tickets
        /// </summary>
        public int GetTickets()
        {
            try
            {
                if (Player.Instance == null) return 0;

                EnsurePlayerProperties();
                if (_playerTicketsProperty != null)
                {
                    return (int)_playerTicketsProperty.GetValue(Player.Instance);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[CurrencyTracker] Error getting tickets: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Get a summary of all currencies. Results are cached and refreshed at most twice per second.
        /// </summary>
        public CurrencySummary GetSummary(bool forceRefresh = false)
        {
            if (!forceRefresh
                && _cachedSummary != null
                && Time.unscaledTime - _lastSummaryRefreshTime < SummaryRefreshIntervalSeconds)
            {
                return _cachedSummary;
            }

            _cachedSummary = BuildSummary();
            _lastSummaryRefreshTime = Time.unscaledTime;
            return _cachedSummary;
        }

        private CurrencySummary BuildSummary()
        {
            var summary = new CurrencySummary
            {
                Gold = GetGold(),
                Orbs = GetOrbs(),
                Tickets = GetTickets(),
                InventoryCurrencies = new Dictionary<string, int>(),
                VaultCurrencies = new Dictionary<string, int>()
            };

            foreach (var kvp in CurrencyItemIds)
            {
                int amount = GetInventoryAmount(kvp.Value);
                if (amount > 0)
                {
                    summary.InventoryCurrencies[kvp.Key] = amount;
                }
            }

            if (Plugin.HasTheVault)
            {
                summary.VaultCurrencies = GetAllVaultCurrencies();
            }

            return summary;
        }

        private void EnsureGoldReflection()
        {
            if (_gameSaveCoinsProperty != null || _goldFallbackResolved)
                return;

            var gameSaveType = AccessTools.TypeByName("Wish.GameSave");
            if (gameSaveType != null)
            {
                _gameSaveCoinsProperty = gameSaveType.GetProperty("Coins", BindingFlags.Public | BindingFlags.Static);
            }

            if (_gameSaveCoinsProperty != null)
                return;

            var singletonType = AccessTools.TypeByName("SingletonBehaviour`1");
            if (singletonType != null && gameSaveType != null)
            {
                var genericType = singletonType.MakeGenericType(gameSaveType);
                _singletonInstanceProperty = genericType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (_singletonInstanceProperty != null)
                {
                    var instance = _singletonInstanceProperty.GetValue(null);
                    if (instance != null)
                    {
                        _currentSaveProperty = instance.GetType().GetProperty("CurrentSave");
                        if (_currentSaveProperty != null)
                        {
                            var currentSave = _currentSaveProperty.GetValue(instance);
                            if (currentSave != null)
                            {
                                _currentSaveCoinsField = currentSave.GetType().GetField(
                                    "coins",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            }
                        }
                    }
                }
            }

            _goldFallbackResolved = true;
        }

        private void EnsurePlayerProperties()
        {
            if (_playerPropertiesResolved || Player.Instance == null)
                return;

            var playerType = Player.Instance.GetType();
            _playerOrbsProperty = playerType.GetProperty("Orbs", BindingFlags.Public | BindingFlags.Instance);
            _playerTicketsProperty = playerType.GetProperty("Tickets", BindingFlags.Public | BindingFlags.Instance);
            _playerPropertiesResolved = true;
        }

        private bool EnsureVaultReflection()
        {
            if (_vaultReflectionResolved)
                return _vaultGetVaultManagerMethod != null;

            _vaultReflectionResolved = true;
            _vaultPluginType = ReflectionHelper.FindModPlugin("TheVault");
            if (_vaultPluginType == null)
                return false;

            _vaultGetVaultManagerMethod = _vaultPluginType.GetMethod(
                "GetVaultManager",
                BindingFlags.Public | BindingFlags.Static);
            if (_vaultGetVaultManagerMethod == null)
                return false;

            var vaultManager = _vaultGetVaultManagerMethod.Invoke(null, null);
            if (vaultManager == null)
                return false;

            _vaultGetAllNonZeroCurrenciesMethod = vaultManager.GetType().GetMethod("GetAllNonZeroCurrencies");
            return _vaultGetAllNonZeroCurrenciesMethod != null;
        }
    }

    public class CurrencySummary
    {
        public int Gold { get; set; }
        public int Orbs { get; set; }
        public int Tickets { get; set; }
        public Dictionary<string, int> InventoryCurrencies { get; set; }
        public Dictionary<string, int> VaultCurrencies { get; set; }
    }
}
