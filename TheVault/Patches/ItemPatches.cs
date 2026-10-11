using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using SunhavenMods.Shared;
using TheVault.Vault;
using Wish;

namespace TheVault.Patches
{
    /// <summary>
    /// Patches for item interactions.
    /// Handles automatic deposit of currency items into vault when picked up,
    /// and item spawning when withdrawing from vault.
    /// </summary>
    public static class ItemPatches
    {
        /// <summary>
        /// Maps game item IDs to vault currency types.
        /// Format: gameItemId -> (currencyId, autoDeposit)
        /// </summary>
        private static Dictionary<int, (string currencyId, bool autoDeposit)> _itemToCurrency
            = new Dictionary<int, (string, bool)>();

        /// <summary>
        /// Maps vault currency types to game item IDs (for withdrawing)
        /// </summary>
        private static Dictionary<string, int> _currencyToItem
            = new Dictionary<string, int>();

        /// <summary>
        /// Whether auto-deposit is enabled globally
        /// </summary>
        public static bool AutoDepositEnabled { get; set; } = false;

        /// <summary>
        /// Global flag: we are withdrawing items to inventory (bypass auto-deposit).
        /// </summary>
        public static bool IsWithdrawing { get; set; } = false;

        /// <summary>
        /// Item IDs currently being withdrawn (bypass auto-deposit for these).
        /// </summary>
        private static readonly HashSet<int> _withdrawingItemIds = new HashSet<int>();

        /// <summary>
        /// Cached reflection for fast GetItemId (runs on every pickup - must be fast).
        /// </summary>
        private static bool _itemIdReflectionCached = false;
        private static FieldInfo _cachedItemIdField;
        private static PropertyInfo _cachedItemIdProperty;
        private static MethodInfo _cachedItemIdMethod;
        /// <summary>Delegate for ID() method - avoids MethodInfo.Invoke overhead on every pickup.</summary>
        private static Func<object, int> _cachedGetItemIdDelegate;
        private static bool _canResolveItemId;
        private static bool _loggedMissingItemIdResolver;

        private enum InventoryRemoveKind { None, IntIntInt, IntInt, IntIntBool, RemoveAmount }

        private struct InventoryRemovePlan
        {
            public InventoryRemoveKind Kind;
            public MethodInfo Method;
        }

        private static readonly Dictionary<Type, InventoryRemovePlan> _inventoryRemoveByType = new Dictionary<Type, InventoryRemovePlan>();
        private const BindingFlags InstanceMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object _itemInfoDatabaseInstance;
        private static FieldInfo _allItemSellInfosField;
        private static int _lastCurrencyLookupItemId = -1;
        private static string _lastCurrencyLookupCurrencyId;
        private static bool _lastCurrencyLookupValid;

        /// <summary>
        /// Check if auto-deposit is enabled for a currency.
        /// </summary>
        public static bool IsAutoDepositEnabled(string currencyId)
        {
            int itemId = GetItemForCurrency(currencyId);
            if (itemId < 0) return false;
            return AutoDepositEnabled && _itemToCurrency.TryGetValue(itemId, out var m) && m.autoDeposit;
        }

        /// <summary>
        /// Toggle auto-deposit for a currency.
        /// </summary>
        public static void ToggleAutoDeposit(string currencyId)
        {
            int itemId = GetItemForCurrency(currencyId);
            if (itemId < 0 || !_itemToCurrency.TryGetValue(itemId, out var m)) return;
            _itemToCurrency[itemId] = (m.currencyId, !m.autoDeposit);
        }

        /// <summary>
        /// Mark that we are withdrawing this item (bypass auto-deposit).
        /// </summary>
        public static void StartWithdrawing(int itemId)
        {
            _withdrawingItemIds.Add(itemId);
        }

        /// <summary>
        /// Clear withdrawing state for this item.
        /// </summary>
        public static void StopWithdrawing(int itemId)
        {
            _withdrawingItemIds.Remove(itemId);
        }

        /// <summary>
        /// Reset state when returning to menu.
        /// </summary>
        public static void ResetState()
        {
            IsWithdrawing = false;
            _withdrawingItemIds.Clear();
            _skipObjectAddItemPostfixOnce = false;
            _isProcessingAutoDeposit = false;
            _suppressVaultRemoveHookDepth = 0;
            _skipVaultInGetAmount = false;
        }

        /// <summary>
        /// True if <paramref name="inv"/> is the player's main bag (<see cref="Player.PlayerInventory"/> or <see cref="Player.Inventory"/>).
        /// Auto-deposit and vault merges must not run on chests or other inventories — those flows often retry on the player bag and would double-credit the vault.
        /// </summary>
        public static bool IsPlayerMainInventory(object inv)
        {
            if (inv == null || Player.Instance == null) return false;
            var p = Player.Instance;
            if (ReferenceEquals(inv, p.PlayerInventory)) return true;
            return p.Inventory != null && ReferenceEquals(inv, p.Inventory);
        }

        /// <summary>
        /// Resolve the player's primary bag for remove/add helpers (UI and patches use PlayerInventory; some game paths use Inventory).
        /// </summary>
        private static object GetPlayerBagForMutation()
        {
            var p = Player.Instance;
            if (p == null) return null;
            return p.PlayerInventory ?? (object)p.Inventory;
        }

        /// <summary>
        /// Register a mapping between a game item and vault currency.
        /// </summary>
        /// <param name="gameItemId">The item's ID in Sun Haven's item database</param>
        /// <param name="currencyId">The vault currency ID</param>
        /// <param name="autoDeposit">Whether to auto-deposit when picked up</param>
        public static void RegisterItemCurrencyMapping(int gameItemId, string currencyId, bool autoDeposit = false)
        {
            _itemToCurrency[gameItemId] = (currencyId, autoDeposit);
            _currencyToItem[currencyId] = gameItemId;
            if (_lastCurrencyLookupValid && _lastCurrencyLookupItemId == gameItemId)
                _lastCurrencyLookupCurrencyId = currencyId;
            Plugin.Log?.LogInfo($"Registered item-currency mapping: Item {gameItemId} <-> {currencyId}");
        }

        /// <summary>
        /// Manually sweep the player's inventory and auto-deposit all matching items into the vault.
        /// Respects per-currency auto-deposit toggles.
        /// </summary>
        public static void ForceAutoDepositAll()
        {
            try
            {
                Plugin.Log?.LogInfo("[Sweep] Sweep Inventory button pressed");

                if (!AutoDepositEnabled)
                {
                    Plugin.Log?.LogInfo("[Sweep] Auto-deposit is disabled; sweep skipped");
                    return;
                }

                // Use same player/inventory as VaultUI (Player.Instance.PlayerInventory = real bag, not PrimaryInventory)
                if (Player.Instance == null)
                {
                    Plugin.Log?.LogWarning("[Sweep] Player.Instance is null");
                    return;
                }

                var inventory = Player.Instance.PlayerInventory;
                if (inventory == null)
                {
                    Plugin.Log?.LogWarning("[Sweep] PlayerInventory is null");
                    return;
                }

                int totalDeposited = 0;

                // Iterate over all registered mappings and deposit anything currently in inventory.
                // Use raw inventory count (exclude vault) so we don't try to remove more than is in the bag.
                foreach (var kvp in _itemToCurrency)
                {
                    int itemId = kvp.Key;
                    var mapping = kvp.Value;

                    // Respect per-currency auto-deposit toggle
                    if (!mapping.autoDeposit)
                        continue;

                    int amount;
                    try
                    {
                        _skipVaultInGetAmount = true;
                        try
                        {
                            if (inventory is Inventory invBag)
                                amount = invBag.GetAmount(itemId);
                            else
                            {
                                var getAmountMethod = AccessTools.Method(inventory.GetType(), "GetAmount", new[] { typeof(int) });
                                if (getAmountMethod == null)
                                    continue;
                                amount = (int)(getAmountMethod.Invoke(inventory, new object[] { itemId }) ?? 0);
                            }
                        }
                        finally
                        {
                            _skipVaultInGetAmount = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _skipVaultInGetAmount = false;
                        Plugin.Log?.LogDebug($"[Sweep] GetAmount({itemId}) failed: {ex.Message}");
                        continue;
                    }

                    if (amount <= 0)
                        continue;

                    if (DepositItemToVault(itemId, amount))
                    {
                        totalDeposited += amount;
                    }
                }

                Plugin.Log?.LogInfo($"[Sweep] Auto-deposited a total of {totalDeposited} items from inventory into the vault");

                // Show in-game notification feedback
                ShowSweepNotification(totalDeposited);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[Sweep] Error during ForceAutoDepositAll: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the game's notification when the Sweep button is used.
        /// </summary>
        private static void ShowSweepNotification(int totalDeposited)
        {
            try
            {
                string text = totalDeposited > 0
                    ? $"Swept {totalDeposited} item{(totalDeposited == 1 ? "" : "s")} to the vault"
                    : "No vault currencies in inventory";
                SendTextNotification(text);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[Sweep] ShowSweepNotification: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a text-only notification via the game's NotificationStack.
        /// </summary>
        private static void SendTextNotification(string text)
        {
            try
            {
                var stackType = ReflectionHelper.FindWishType("NotificationStack");
                if (stackType == null) return;
                var inst = ReflectionHelper.GetSingletonInstance(stackType);
                var sendMethod = AccessTools.Method(stackType, "SendNotification", new[] { typeof(string), typeof(int), typeof(int), typeof(bool), typeof(bool) });
                if (inst != null && sendMethod != null)
                    sendMethod.Invoke(inst, new object[] { text, 0, 0, false, false });
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[ItemPatches] SendTextNotification: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the vault currency ID for a game item
        /// </summary>
        public static string GetCurrencyForItem(int gameItemId)
        {
            if (_lastCurrencyLookupValid && gameItemId == _lastCurrencyLookupItemId)
                return _lastCurrencyLookupCurrencyId;

            string currencyId = _itemToCurrency.TryGetValue(gameItemId, out var mapping) ? mapping.currencyId : null;
            _lastCurrencyLookupItemId = gameItemId;
            _lastCurrencyLookupCurrencyId = currencyId;
            _lastCurrencyLookupValid = true;
            return currencyId;
        }

        /// <summary>
        /// Get the game item ID for a vault currency
        /// </summary>
        public static int GetItemForCurrency(string currencyId)
        {
            return _currencyToItem.TryGetValue(currencyId, out int itemId) ? itemId : -1;
        }

        /// <summary>
        /// Stack count in the player bag only (temporarily skips vault merging in GetAmount patches).
        /// </summary>
        public static int GetRawInventoryCount(int itemId)
        {
            if (Player.Instance?.PlayerInventory == null) return 0;
            object inventory = Player.Instance.PlayerInventory;
            try
            {
                _skipVaultInGetAmount = true;
                try
                {
                    if (inventory is Inventory invBag)
                        return invBag.GetAmount(itemId);

                    var getAmountMethod = AccessTools.Method(inventory.GetType(), "GetAmount", new[] { typeof(int) });
                    if (getAmountMethod == null) return 0;
                    object r = getAmountMethod.Invoke(inventory, new object[] { itemId });
                    return r != null ? (int)r : 0;
                }
                finally
                {
                    _skipVaultInGetAmount = false;
                }
            }
            catch (Exception ex)
            {
                _skipVaultInGetAmount = false;
                Plugin.Log?.LogDebug($"[ItemPatches] GetRawInventoryCount: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Check if an item should be auto-deposited
        /// </summary>
        public static bool ShouldAutoDeposit(int gameItemId)
        {
            if (!AutoDepositEnabled) return false;
            return _itemToCurrency.TryGetValue(gameItemId, out var mapping) && mapping.autoDeposit;
        }

        /// <summary>
        /// Deposit items from player inventory into vault.
        /// </summary>
        /// <param name="gameItemId">The game item ID to deposit</param>
        /// <param name="amount">Amount to deposit</param>
        /// <returns>True if successful</returns>
        public static bool DepositItemToVault(int gameItemId, int amount)
        {
            if (amount <= 0) return false;

            string currencyId = GetCurrencyForItem(gameItemId);
            if (string.IsNullOrEmpty(currencyId))
            {
                Plugin.Log?.LogWarning($"No currency mapping for item {gameItemId}");
                return false;
            }

            var vaultManager = Plugin.GetVaultManager();
            if (vaultManager == null) return false;

            // Remove from inventory; suppress vault remove hook to avoid accidental vault deductions.
            BeginSuppressVaultRemoveHook();
            try
            {
                if (!RemoveItemFromInventory(gameItemId, amount))
                {
                    Plugin.Log?.LogWarning($"Failed to remove {amount} of item {gameItemId} from inventory");
                    return false;
                }
            }
            finally
            {
                EndSuppressVaultRemoveHook();
            }

            // Add to vault — roll inventory back if currency id is unrecognized / add fails.
            if (!AddCurrencyToVault(vaultManager, currencyId, amount))
            {
                BeginSuppressVaultRemoveHook();
                try
                {
                    AddItemToInventory(gameItemId, amount);
                }
                finally
                {
                    EndSuppressVaultRemoveHook();
                }
                Plugin.Log?.LogWarning($"Failed to credit {amount} {currencyId} to vault; restored inventory item {gameItemId}");
                return false;
            }
            Plugin.Log?.LogInfo($"Deposited {amount} of item {gameItemId} as {currencyId}");
            return true;
        }

        /// <summary>
        /// Withdraw currency from vault and spawn as items.
        /// </summary>
        /// <param name="currencyId">The vault currency ID</param>
        /// <param name="amount">Amount to withdraw</param>
        /// <returns>True if successful</returns>
        public static bool WithdrawCurrencyToInventory(string currencyId, int amount)
        {
            if (amount <= 0) return false;

            var vaultManager = Plugin.GetVaultManager();
            if (vaultManager == null) return false;

            // Check if player has enough
            if (!vaultManager.HasCurrency(currencyId, amount))
            {
                Plugin.Log?.LogWarning($"Not enough {currencyId} in vault");
                return false;
            }

            int gameItemId = GetItemForCurrency(currencyId);
            if (gameItemId < 0)
            {
                Plugin.Log?.LogWarning($"No item mapping for currency {currencyId}");
                return false;
            }

            // Remove from vault
            if (!RemoveCurrencyFromVault(vaultManager, currencyId, amount))
            {
                return false;
            }

            // Add to inventory
            if (!AddItemToInventory(gameItemId, amount))
            {
                // Rollback vault change
                AddCurrencyToVault(vaultManager, currencyId, amount);
                Plugin.Log?.LogWarning($"Failed to add item {gameItemId} to inventory, rolled back vault");
                return false;
            }

            Plugin.Log?.LogInfo($"Withdrew {amount} {currencyId} as item {gameItemId}");
            return true;
        }

        /// <summary>
        /// Postfix patch for Inventory.AddItem(int item, int amount, bool sendNotification).
        /// Parameter names must match the game method exactly for Harmony binding.
        /// </summary>
        public static void OnInventoryAddItem(object __instance, int item, int amount, bool sendNotification)
        {
            int itemId = item;
            try
            {
                if (!IsPlayerMainInventory(__instance)) return;
                if (_isProcessingAutoDeposit || IsWithdrawing || _withdrawingItemIds.Contains(itemId)) return;
                if (!ShouldAutoDeposit(itemId))
                {
                    return;
                }

                string currencyId = GetCurrencyForItem(itemId);
                if (string.IsNullOrEmpty(currencyId))
                {
                    return;
                }

                var vaultManager = Plugin.GetVaultManager();
                if (vaultManager == null)
                {
                    return;
                }

                _isProcessingAutoDeposit = true;
                try
                {
                    // Remove from inventory - try inventory instance first, then Player fallback
                    BeginSuppressVaultRemoveHook();
                    bool removed;
                    try
                    {
                        removed = TryRemoveFromInventory(__instance, itemId, amount) || RemoveItemFromInventory(itemId, amount);
                    }
                    finally
                    {
                        EndSuppressVaultRemoveHook();
                    }
                    if (!removed)
                    {
                        Plugin.Log?.LogWarning($"Failed to remove {amount} of item {itemId} from inventory for auto-deposit");
                        return;
                    }

                    if (!AddCurrencyToVault(vaultManager, currencyId, amount))
                    {
                        AddItemToInventory(itemId, amount);
                        Plugin.Log?.LogWarning($"Auto-deposit failed to credit {currencyId}; restored {amount} of item {itemId}");
                    }
                }
                finally
                {
                    _isProcessingAutoDeposit = false;
                }
            }
            catch (Exception ex)
            {
                _isProcessingAutoDeposit = false;
                Plugin.Log?.LogError($"Error in OnInventoryAddItem: {ex.Message}");
            }
        }

        /// <summary>
        /// Flag to prevent recursive auto-deposit when we're adding items back to inventory during withdraw
        /// </summary>
        private static bool _isProcessingAutoDeposit = false;
        /// <summary>
        /// Guard flag: when object AddItem PREFIX handles auto-deposit and skips original,
        /// ignore the matching POSTFIX to avoid double-deposit.
        /// </summary>
        private static bool _skipObjectAddItemPostfixOnce = false;
        private static int _suppressVaultRemoveHookDepth = 0;

        private static void BeginSuppressVaultRemoveHook() => _suppressVaultRemoveHookDepth++;
        private static void EndSuppressVaultRemoveHook() => _suppressVaultRemoveHookDepth = Math.Max(0, _suppressVaultRemoveHookDepth - 1);
        private static bool IsVaultRemoveHookSuppressed() => _suppressVaultRemoveHookDepth > 0;

        /// <summary>
        /// PREFIX patch for Inventory.AddItem(Item, int, int, bool, bool, bool).
        /// Called by Wish.Pickup.goToPlayer() when picking up items from the ground.
        /// Returns false to SKIP the original - item never enters inventory, goes straight to vault.
        /// </summary>
        public static bool OnInventoryAddItemObjectPrefix(object __instance, object item, int amount, int slot, bool sendNotification, bool specialItem, bool superSecretCheck)
        {
            try
            {
                if (_isProcessingAutoDeposit) return true;
                if (!IsPlayerMainInventory(__instance)) return true;

                int itemId = GetItemId(item);
                if (itemId < 0) return true;

                if (IsWithdrawing || _withdrawingItemIds.Contains(itemId)) return true;

                if (!ShouldAutoDeposit(itemId)) return true;

                string currencyId = GetCurrencyForItem(itemId);
                if (string.IsNullOrEmpty(currencyId)) return true;

                var vaultManager = Plugin.GetVaultManager();
                if (vaultManager == null) return true;

                _isProcessingAutoDeposit = true;
                try
                {
                    if (!AddCurrencyToVault(vaultManager, currencyId, amount))
                    {
                        Plugin.Log?.LogWarning($"Auto-deposit skipped: could not credit {amount} {currencyId} for item {itemId}");
                        return true; // Let original AddItem run — nothing credited yet
                    }
                    if (sendNotification)
                        EnqueueAutoDepositNotification(currencyId, amount);
                    _skipObjectAddItemPostfixOnce = true;
                    return false; // Skip original - item does NOT go to inventory
                }
                finally
                {
                    _isProcessingAutoDeposit = false;
                }
            }
            catch (Exception ex)
            {
                _isProcessingAutoDeposit = false;
                _skipObjectAddItemPostfixOnce = false;
                Plugin.Log?.LogError($"Error in OnInventoryAddItemObjectPrefix: {ex.Message}\n{ex.StackTrace}");
                return true; // Let original run on error
            }
        }

        /// <summary>
        /// POSTFIX patch for Inventory.AddItem(Item, int, int, bool, bool, bool).
        /// Backup path - only runs if PREFIX returned true (e.g. withdraw, or non-auto-deposit item).
        /// </summary>
        public static void OnInventoryAddItemObjectPostfix(object __instance, object item, int amount, int slot, bool sendNotification, bool specialItem, bool superSecretCheck)
        {
            try
            {
                if (_skipObjectAddItemPostfixOnce)
                {
                    _skipObjectAddItemPostfixOnce = false;
                    return;
                }

                // Prevent recursive calls when we're doing withdrawals or other operations
                if (_isProcessingAutoDeposit) return;
                if (!IsPlayerMainInventory(__instance)) return;

                // Get the item ID from the Item object
                int itemId = GetItemId(item);
                if (IsWithdrawing || _withdrawingItemIds.Contains(itemId)) return;
                if (!ShouldAutoDeposit(itemId))
                {
                    // Not registered for auto-deposit
                    return;
                }

                string currencyId = GetCurrencyForItem(itemId);
                if (string.IsNullOrEmpty(currencyId)) return;
                var vaultManager = Plugin.GetVaultManager();
                if (vaultManager == null) return;

                _isProcessingAutoDeposit = true;
                try
                {
                    // The item is now in inventory (and notification was shown if sendNotification was true)
                    // Now remove it from inventory and add to vault

                    // Remove from inventory - try inventory instance first, then Player fallback
                    BeginSuppressVaultRemoveHook();
                    bool removed;
                    try
                    {
                        removed = TryRemoveFromInventory(__instance, itemId, amount) || RemoveItemFromInventory(itemId, amount);
                    }
                    finally
                    {
                        EndSuppressVaultRemoveHook();
                    }
                    if (!removed) return;
                    if (!AddCurrencyToVault(vaultManager, currencyId, amount))
                    {
                        // Inventory already removed — restore bag so currency is not destroyed.
                        AddItemToInventory(itemId, amount);
                        Plugin.Log?.LogWarning($"Auto-deposit postfix failed to credit {currencyId}; restored {amount} of item {itemId} to inventory");
                    }
                }
                finally
                {
                    _isProcessingAutoDeposit = false;
                }
            }
            catch (Exception ex)
            {
                _isProcessingAutoDeposit = false;
                Plugin.Log?.LogError($"Error in OnInventoryAddItemObjectPostfix: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Call from plugin startup to build the GetItemId cache before any pickup (avoids lag on first pickup).
        /// </summary>
        public static void InitializePickupCache()
        {
            EnsureItemIdCache();
            if (!_canResolveItemId)
                Plugin.Log?.LogWarning("[ItemPatches] Item ID resolver is unavailable; pickup object patch paths will be skipped.");
        }

        /// <summary>
        /// Initialize cached reflection for GetItemId. Runs once per game session.
        /// </summary>
        private static void EnsureItemIdCache()
        {
            if (_itemIdReflectionCached) return;
            lock (typeof(ItemPatches))
            {
                if (_itemIdReflectionCached) return;
                try
                {
                    // Wish.Item exposes ID() (Sun Haven 3.1+). Probe that first — AccessTools
                    // Field/Property for id/_id/Id/ItemID logs HarmonyX misses on every launch.
                    var itemType = typeof(Item);
                    const BindingFlags idFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    _cachedItemIdMethod = itemType.GetMethod("ID", idFlags, null, Type.EmptyTypes, null);
                    if (_cachedItemIdMethod != null)
                    {
                        try
                        {
                            var typedDelegate = (Func<Item, int>)Delegate.CreateDelegate(typeof(Func<Item, int>), _cachedItemIdMethod);
                            _cachedGetItemIdDelegate = obj =>
                            {
                                if (obj is Item wishItem)
                                    return typedDelegate(wishItem);
                                return -1;
                            };
                        }
                        catch
                        {
                            _cachedGetItemIdDelegate = null;
                        }
                    }
                    else
                    {
                        _cachedItemIdField = itemType.GetField("id", idFlags) ?? itemType.GetField("_id", idFlags);
                        _cachedItemIdProperty = itemType.GetProperty("id", idFlags)
                            ?? itemType.GetProperty("Id", idFlags)
                            ?? itemType.GetProperty("ItemID", idFlags);
                    }
                    _canResolveItemId = _cachedGetItemIdDelegate != null || _cachedItemIdField != null || _cachedItemIdProperty != null;
                    _itemIdReflectionCached = true;
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogDebug($"[ItemPatches] EnsureItemIdCache: {ex.Message}");
                    _canResolveItemId = false;
                    _itemIdReflectionCached = true;
                }
            }
        }

        /// <summary>
        /// Get item ID using cached reflection. Fast path for pickup hot path.
        /// </summary>
        private static int GetItemId(object item)
        {
            if (item == null) return -1;
            if (item is Item wishItem)
                return wishItem.ID();
            EnsureItemIdCache();
            if (!_canResolveItemId)
            {
                if (!_loggedMissingItemIdResolver)
                {
                    Plugin.Log?.LogWarning("[ItemPatches] Missing item ID resolver; skipping object-based pickup handling.");
                    _loggedMissingItemIdResolver = true;
                }
                return -1;
            }
            try
            {
                // Fast path: delegate call (no reflection invoke)
                if (_cachedGetItemIdDelegate != null)
                    return _cachedGetItemIdDelegate(item);
                if (_cachedItemIdField != null)
                {
                    var r = _cachedItemIdField.GetValue(item);
                    if (r is int id) return id;
                }
                if (_cachedItemIdProperty != null)
                {
                    var r = _cachedItemIdProperty.GetValue(item);
                    if (r is int id) return id;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[ItemPatches] GetItemId: {ex.Message}");
            }
            return -1;
        }

        #region Inventory Operations

        private static InventoryRemovePlan ResolveInventoryRemovePlan(Type invType)
        {
            if (_inventoryRemoveByType.TryGetValue(invType, out var cached))
                return cached;

            var plan = new InventoryRemovePlan { Kind = InventoryRemoveKind.None, Method = null };

            var m = invType.GetMethod("RemoveItem", InstanceMemberFlags, null, new[] { typeof(int), typeof(int), typeof(int) }, null);
            if (m != null)
            {
                plan.Kind = InventoryRemoveKind.IntIntInt;
                plan.Method = m;
            }
            else
            {
                m = invType.GetMethod("RemoveItem", InstanceMemberFlags, null, new[] { typeof(int), typeof(int) }, null);
                if (m != null)
                {
                    plan.Kind = InventoryRemoveKind.IntInt;
                    plan.Method = m;
                }
                else
                {
                    m = invType.GetMethod("RemoveItem", InstanceMemberFlags, null, new[] { typeof(int), typeof(int), typeof(bool) }, null);
                    if (m != null)
                    {
                        plan.Kind = InventoryRemoveKind.IntIntBool;
                        plan.Method = m;
                    }
                    else
                    {
                        m = invType.GetMethod("RemoveItemAmount", InstanceMemberFlags, null, new[] { typeof(int), typeof(int) }, null);
                        if (m != null)
                        {
                            plan.Kind = InventoryRemoveKind.RemoveAmount;
                            plan.Method = m;
                        }
                    }
                }
            }

            _inventoryRemoveByType[invType] = plan;
            return plan;
        }

        private static bool InvokeInventoryRemove(in InventoryRemovePlan plan, object inventory, int itemId, int amount)
        {
            if (plan.Method == null) return false;
            try
            {
                object result;
                switch (plan.Kind)
                {
                    case InventoryRemoveKind.IntIntInt:
                        result = plan.Method.Invoke(inventory, new object[] { itemId, amount, 0 });
                        break;
                    case InventoryRemoveKind.IntInt:
                        result = plan.Method.Invoke(inventory, new object[] { itemId, amount });
                        break;
                    case InventoryRemoveKind.IntIntBool:
                        result = plan.Method.Invoke(inventory, new object[] { itemId, amount, false });
                        break;
                    case InventoryRemoveKind.RemoveAmount:
                        result = plan.Method.Invoke(inventory, new object[] { itemId, amount });
                        break;
                    default:
                        return false;
                }

                if (result is bool b) return b;
                if (result is System.Collections.ICollection coll) return coll.Count > 0;
                return plan.Kind == InventoryRemoveKind.IntIntInt && result != null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[ItemPatches] Inventory remove invoke failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try to remove an item from a specific inventory instance.
        /// Tries multiple RemoveItem signatures since Sun Haven's API may vary (resolved once per inventory type).
        /// </summary>
        private static bool TryRemoveFromInventory(object inventory, int itemId, int amount)
        {
            if (inventory == null) return false;
            var plan = ResolveInventoryRemovePlan(inventory.GetType());
            return InvokeInventoryRemove(plan, inventory, itemId, amount);
        }

        /// <summary>
        /// Remove an item from player inventory.
        /// Needs to interact with Sun Haven's inventory system.
        /// </summary>
        private static bool RemoveItemFromInventory(int itemId, int amount)
        {
            try
            {
                object player = Player.Instance;
                if (player == null)
                {
                    var localPlayerProperty = typeof(Player).GetProperty("LocalPlayer", BindingFlags.Public | BindingFlags.Static);
                    if (localPlayerProperty != null)
                        player = localPlayerProperty.GetValue(null);
                }
                if (player == null)
                {
                    Plugin.Log?.LogWarning("Player (Instance and LocalPlayer) is null");
                    return false;
                }

                var bag = GetPlayerBagForMutation();
                if (bag != null)
                    return TryRemoveFromInventory(bag, itemId, amount);

                // Fallback: resolve inventory field once (avoids HarmonyX spam from failed Player.RemoveItem probes)
                var playerType = player.GetType();
                var inventoryField =
                    playerType.GetField("playerInventory", InstanceMemberFlags)
                    ?? playerType.GetField("_playerInventory", InstanceMemberFlags)
                    ?? playerType.GetField("inventory", InstanceMemberFlags)
                    ?? playerType.GetField("_inventory", InstanceMemberFlags);
                var inventoryProperty =
                    playerType.GetProperty("PlayerInventory", InstanceMemberFlags)
                    ?? playerType.GetProperty("Inventory", InstanceMemberFlags);

                object inventory = inventoryField != null ? inventoryField.GetValue(player) : null;
                if (inventory == null && inventoryProperty != null)
                    inventory = inventoryProperty.GetValue(player);

                if (inventory != null)
                    return TryRemoveFromInventory(inventory, itemId, amount);

                Plugin.Log?.LogWarning("Could not find player inventory");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error removing item from inventory: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Add an item to player inventory.
        /// Needs to interact with Sun Haven's inventory system.
        /// </summary>
        private static bool AddItemToInventory(int itemId, int amount)
        {
            try
            {
                var player = Player.Instance;
                if (player == null) return false;

                var inventory = GetPlayerBagForMutation();
                if (inventory != null)
                {
                    if (inventory is Inventory inv)
                    {
                        inv.AddItem(itemId, amount, true);
                        return true;
                    }
                    var invAddMethod = AccessTools.Method(inventory.GetType(), "AddItem", new[] { typeof(int), typeof(int), typeof(bool) });
                    if (invAddMethod != null)
                    {
                        invAddMethod.Invoke(inventory, new object[] { itemId, amount, true });
                        return true;
                    }
                    var invAddMethod5 = AccessTools.Method(inventory.GetType(), "AddItem", new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool) });
                    if (invAddMethod5 != null)
                    {
                        invAddMethod5.Invoke(inventory, new object[] { itemId, amount, 0, true, true });
                        return true;
                    }
                }

                var addMethod = AccessTools.Method(typeof(Player), "AddItemToInventory", new[] { typeof(int), typeof(int) });
                if (addMethod != null)
                {
                    var result = addMethod.Invoke(player, new object[] { itemId, amount });
                    return result is bool b && b;
                }

                Plugin.Log?.LogWarning("Could not find a method to add items to inventory");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error adding item to inventory: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Vault Operations

        /// <summary>
        /// Credits vault currency. Returns false when the id is unrecognized so callers can roll back inventory.
        /// </summary>
        private static bool AddCurrencyToVault(VaultManager vaultManager, string currencyId, int amount)
        {
            if (vaultManager == null || string.IsNullOrEmpty(currencyId) || amount <= 0)
                return false;

            if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixSeasonal, out string suffix))
            {
                if (Enum.TryParse<SeasonalTokenType>(suffix, out var tokenType))
                {
                    vaultManager.AddSeasonalTokens(tokenType, amount);
                    return true;
                }
                Plugin.Log?.LogWarning($"AddCurrencyToVault: invalid seasonal type '{suffix}' in '{currencyId}'");
                return false;
            }
            if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixCommunity, out suffix))
            {
                vaultManager.AddCommunityTokens(suffix, amount);
                return true;
            }
            if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixSpecial, out suffix))
            {
                vaultManager.AddSpecial(suffix, amount);
                return true;
            }
            if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixKey, out suffix))
            {
                vaultManager.AddKeys(suffix, amount);
                return true;
            }
            if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixTicket, out suffix))
            {
                vaultManager.AddTickets(suffix, amount);
                return true;
            }
            if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixOrb, out suffix))
            {
                vaultManager.AddOrbs(suffix, amount);
                return true;
            }
            if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixCustom, out suffix))
            {
                vaultManager.AddCustomCurrency(suffix, amount);
                return true;
            }

            Plugin.Log?.LogWarning($"AddCurrencyToVault: unrecognized currency id '{currencyId}'");
            return false;
        }

        private static bool RemoveCurrencyFromVault(VaultManager vaultManager, string currencyId, int amount)
        {
            if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixSeasonal, out string suffix))
            {
                if (Enum.TryParse<SeasonalTokenType>(suffix, out var tokenType))
                {
                    return vaultManager.RemoveSeasonalTokens(tokenType, amount);
                }
            }
            else if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixCommunity, out suffix))
            {
                return vaultManager.RemoveCommunityTokens(suffix, amount);
            }
            else if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixSpecial, out suffix))
            {
                return vaultManager.RemoveSpecial(suffix, amount);
            }
            else if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixKey, out suffix))
            {
                return vaultManager.RemoveKeys(suffix, amount);
            }
            else if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixTicket, out suffix))
            {
                return vaultManager.RemoveTickets(suffix, amount);
            }
            else if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixOrb, out suffix))
            {
                return vaultManager.RemoveOrbs(suffix, amount);
            }
            else if (TryStripPrefix(currencyId, VaultCurrencyIds.PrefixCustom, out suffix))
            {
                return vaultManager.RemoveCustomCurrency(suffix, amount);
            }

            return false;
        }

        private static bool TryStripPrefix(string id, string prefix, out string suffix)
        {
            if (id.StartsWith(prefix, StringComparison.Ordinal))
            {
                suffix = id.Substring(prefix.Length);
                return true;
            }
            suffix = null;
            return false;
        }

        #endregion

        #region Inventory Integration Patches (HasEnough, GetAmount, RemoveItem)

        /// <summary>
        /// Get the vault amount for a game item ID.
        /// Returns 0 if the item is not a registered currency.
        /// </summary>
        public static int GetVaultAmount(int itemId)
        {
            string currencyId = GetCurrencyForItem(itemId);
            if (string.IsNullOrEmpty(currencyId)) return 0;

            var vaultManager = Plugin.GetVaultManager();
            if (vaultManager == null) return 0;

            if (currencyId.StartsWith(VaultCurrencyIds.PrefixSeasonal))
            {
                string typeName = currencyId.Substring(VaultCurrencyIds.PrefixSeasonal.Length);
                if (Enum.TryParse<SeasonalTokenType>(typeName, out var tokenType))
                {
                    return vaultManager.GetSeasonalTokens(tokenType);
                }
            }
            else if (currencyId.StartsWith(VaultCurrencyIds.PrefixCommunity))
            {
                return vaultManager.GetCommunityTokens(currencyId.Substring(VaultCurrencyIds.PrefixCommunity.Length));
            }
            else if (currencyId.StartsWith(VaultCurrencyIds.PrefixSpecial))
            {
                return vaultManager.GetSpecial(currencyId.Substring(VaultCurrencyIds.PrefixSpecial.Length));
            }
            else if (currencyId.StartsWith(VaultCurrencyIds.PrefixKey))
            {
                return vaultManager.GetKeys(currencyId.Substring(VaultCurrencyIds.PrefixKey.Length));
            }
            else if (currencyId.StartsWith(VaultCurrencyIds.PrefixTicket))
            {
                return vaultManager.GetTickets(currencyId.Substring(VaultCurrencyIds.PrefixTicket.Length));
            }
            else if (currencyId.StartsWith(VaultCurrencyIds.PrefixOrb))
            {
                return vaultManager.GetOrbs(currencyId.Substring(VaultCurrencyIds.PrefixOrb.Length));
            }
            else if (currencyId.StartsWith(VaultCurrencyIds.PrefixCustom, StringComparison.Ordinal))
            {
                return vaultManager.GetCustomCurrency(currencyId.Substring(VaultCurrencyIds.PrefixCustom.Length));
            }

            return 0;
        }

        /// <summary>
        /// Check if the item is a registered vault currency.
        /// </summary>
        public static bool IsVaultCurrency(int itemId)
        {
            return _itemToCurrency.ContainsKey(itemId);
        }

        /// <summary>
        /// Flag to temporarily skip vault addition in GetAmount (used by RemoveItem prefix)
        /// </summary>
        private static bool _skipVaultInGetAmount = false;

        /// <summary>
        /// POSTFIX for Inventory.GetAmount(int id) -> int
        /// Adds vault amount to inventory amount for registered currencies.
        /// Only adds vault when __instance is the player's inventory (not chests).
        /// Crafting table iterates over player + nearby chests; adding vault to every
        /// inventory would double-count (60 * N inventories = wrong total).
        /// </summary>
        public static void OnInventoryGetAmount(object __instance, int id, ref int __result)
        {
            try
            {
                // Skip vault addition if we're getting raw inventory count
                if (_skipVaultInGetAmount) return;

                if (!IsVaultCurrency(id)) return;

                if (!IsPlayerMainInventory(__instance)) return;

                int vaultAmount = GetVaultAmount(id);
                if (vaultAmount > 0)
                {
                    int originalResult = __result;
                    __result += vaultAmount;
                    Plugin.Log?.LogDebug($"GetAmount for item {id}: inventory={originalResult}, vault={vaultAmount}, total={__result}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in OnInventoryGetAmount: {ex.Message}");
            }
        }

        /// <summary>
        /// POSTFIX for Inventory.HasEnough(int id, int amount) -> bool
        /// Returns true if inventory + vault has enough for registered currencies.
        /// </summary>
        public static void OnInventoryHasEnough(object __instance, int id, int amount, ref bool __result)
        {
            try
            {
                if (!IsPlayerMainInventory(__instance)) return;

                // If already has enough in inventory, no need to check vault
                if (__result) return;

                // Only check vault for registered currencies
                if (!IsVaultCurrency(id)) return;

                // Combine bag + vault. __result is false here, so bag alone is short —
                // but bag+vault may still cover the full amount (e.g. bag 5 + vault 5 for need 10).
                int bagAmount = GetRawInventoryCount(id);
                int vaultAmount = GetVaultAmount(id);
                if (bagAmount + vaultAmount >= amount)
                {
                    __result = true;
                    Plugin.Log?.LogDebug($"HasEnough for item {id}: bag={bagAmount}, vault={vaultAmount}, need={amount} - TRUE");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in OnInventoryHasEnough: {ex.Message}");
            }
        }

        /// <summary>
        /// PREFIX for Inventory.RemoveItem(int id, int amount, int slot) -> List
        /// Stores the inventory amount before removal so postfix knows how much to take from vault.
        /// Only runs for player's inventory (not chests) since vault is player-stored.
        /// </summary>
        public static void OnInventoryRemoveItemPrefix(object __instance, int id, int amount, int slot, ref int __state)
        {
            __state = -1;
            try
            {
                if (IsVaultRemoveHookSuppressed() || _isProcessingAutoDeposit || IsWithdrawing) return;
                if (!IsVaultCurrency(id)) return;
                if (!IsPlayerMainInventory(__instance)) return;

                // Raw bag count only — GetAmount postfix adds vault; using it here made __state include vault so postfix never deducted.
                _skipVaultInGetAmount = true;
                try
                {
                    if (__instance is Inventory inv)
                    {
                        __state = inv.GetAmount(id);
                    }
                    else
                    {
                        var invType = __instance.GetType();
                        var getAmountMethod = AccessTools.Method(invType, "GetAmount", new[] { typeof(int) });
                        if (getAmountMethod != null)
                        {
                            __state = (int)(getAmountMethod.Invoke(__instance, new object[] { id }) ?? 0);
                        }
                    }
                }
                finally
                {
                    _skipVaultInGetAmount = false;
                }

                if (__state >= 0)
                    Plugin.Log?.LogInfo($"RemoveItem prefix: item {id}, rawInventory={__state}, requestedAmount={amount}");
            }
            catch (Exception ex)
            {
                _skipVaultInGetAmount = false; // Ensure flag is reset on error
                Plugin.Log?.LogError($"Error in OnInventoryRemoveItemPrefix: {ex.Message}");
            }
        }

        /// <summary>
        /// POSTFIX for Inventory.RemoveItem(int id, int amount, int slot) -> List
        /// If player inventory didn't have enough, deduct the remainder from vault.
        /// Only runs for player's inventory (not chests).
        /// </summary>
        public static void OnInventoryRemoveItemPostfix(object __instance, int id, int amount, int slot, int __state)
        {
            try
            {
                if (IsVaultRemoveHookSuppressed() || _isProcessingAutoDeposit || IsWithdrawing) return;
                if (!IsVaultCurrency(id)) return;
                if (!IsPlayerMainInventory(__instance)) return;
                if (__state < 0) return;

                int inventoryHadBefore = __state;

                // If inventory had enough, nothing to do with vault
                if (inventoryHadBefore >= amount)
                {
                    Plugin.Log?.LogInfo($"RemoveItem postfix: item {id}, inventory had enough ({inventoryHadBefore} >= {amount})");
                    return;
                }

                // Calculate how much should come from vault
                int fromVault = amount - inventoryHadBefore;

                string currencyId = GetCurrencyForItem(id);
                if (string.IsNullOrEmpty(currencyId)) return;

                var vaultManager = Plugin.GetVaultManager();
                if (vaultManager == null) return;

                int vaultAmount = GetVaultAmount(id);
                if (fromVault > 0 && vaultAmount >= fromVault)
                {
                    // Deduct from vault
                    bool removed = RemoveCurrencyFromVault(vaultManager, currencyId, fromVault);
                    Plugin.Log?.LogInfo($"RemoveItem postfix: deducted {fromVault} of item {id} from vault (success={removed})");
                }
                else if (fromVault > 0)
                {
                    Plugin.Log?.LogWarning($"RemoveItem postfix: vault has {vaultAmount} but need {fromVault} more");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in OnInventoryRemoveItemPostfix: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Deferred notification queue so we don't run the game's heavy notification UI on the pickup path.
        /// Drained once per frame from PersistentRunner.
        /// </summary>
        private static readonly List<(int itemId, int amount)> _autoDepositNotifyQueue = new List<(int, int)>();
        private static readonly object _notifyQueueLock = new object();
        private static readonly Dictionary<int, int> _mergeBuffer = new Dictionary<int, int>();

        /// <summary>Enqueue a notification to show later (keeps pickup path fast).</summary>
        public static void EnqueueAutoDepositNotification(string currencyId, int amount)
        {
            int itemId = GetItemForCurrency(currencyId);
            if (itemId < 0) return;
            lock (_notifyQueueLock)
            {
                _autoDepositNotifyQueue.Add((itemId, amount));
            }
        }

        /// <summary>Call once per frame from PersistentRunner. Shows at most one batched notification per frame to avoid lag.</summary>
        public static void DrainAutoDepositNotifications()
        {
            lock (_notifyQueueLock)
            {
                if (_autoDepositNotifyQueue.Count == 0) return;

                // Merge by itemId
                _mergeBuffer.Clear();
                foreach (var t in _autoDepositNotifyQueue)
                {
                    if (!_mergeBuffer.TryGetValue(t.itemId, out int a)) a = 0;
                    _mergeBuffer[t.itemId] = a + t.amount;
                }
                _autoDepositNotifyQueue.Clear();

                // Show only the first one this frame; re-queue the rest for next frame
                bool first = true;
                foreach (var kvp in _mergeBuffer)
                {
                    if (first)
                    {
                        ShowAutoDepositNotificationImmediate(kvp.Key, kvp.Value);
                        first = false;
                    }
                    else
                        _autoDepositNotifyQueue.Add((kvp.Key, kvp.Value));
                }
            }
        }

        /// <summary>
        /// Shows the native game pickup notification for auto-deposited items.
        /// Called off the hot path (from DrainAutoDepositNotifications).
        /// </summary>
        private static void ShowAutoDepositNotificationImmediate(int itemId, int amount)
        {
            try
            {
                string itemName = TryGetItemName(itemId);
                if (string.IsNullOrWhiteSpace(itemName))
                    itemName = "Vault";
                NotificationStack.Instance?.SendNotification(itemName, itemId, amount, false, false);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[ItemPatches] ShowAutoDepositNotificationImmediate: {ex.Message}");
            }
        }

        private static string TryGetItemName(int itemId)
        {
            try
            {
                if (_itemInfoDatabaseInstance == null || (_itemInfoDatabaseInstance is UnityEngine.Object uo && uo == null))
                {
                    var itemDbType = ReflectionHelper.FindWishType("ItemInfoDatabase");
                    _itemInfoDatabaseInstance = ReflectionHelper.GetSingletonInstance(itemDbType);
                    _allItemSellInfosField = _itemInfoDatabaseInstance?.GetType().GetField("allItemSellInfos", BindingFlags.Public | BindingFlags.Instance);
                }

                if (_itemInfoDatabaseInstance == null || _allItemSellInfosField == null)
                    return null;

                var dict = _allItemSellInfosField.GetValue(_itemInfoDatabaseInstance) as System.Collections.IDictionary;
                if (dict == null || !dict.Contains(itemId))
                    return null;

                object itemSellInfo = dict[itemId];
                if (itemSellInfo == null)
                    return null;

                var nameProp = itemSellInfo.GetType().GetProperty("name", InstanceMemberFlags);
                if (nameProp != null)
                    return nameProp.GetValue(itemSellInfo) as string;

                var nameField = itemSellInfo.GetType().GetField("name", InstanceMemberFlags);
                if (nameField != null)
                    return nameField.GetValue(itemSellInfo) as string;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[ItemPatches] TryGetItemName({itemId}): {ex.Message}");
            }
            return null;
        }

    }
}
