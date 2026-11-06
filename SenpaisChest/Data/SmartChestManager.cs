#pragma warning disable CS0436 // Type conflicts with imported type - we explicitly use SunhavenMods.Shared types
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SunhavenMods.Shared;
using UnityEngine;
using Wish;

namespace SenpaisChest.Data
{
    public class SmartChestManager
    {
        private readonly Dictionary<string, SmartChestData> _smartChests = new Dictionary<string, SmartChestData>();
        private readonly Dictionary<string, ItemGroup> _groups = new Dictionary<string, ItemGroup>(StringComparer.OrdinalIgnoreCase);
        private string _characterName = "";
        private bool _isDirty;

        // Cache for ItemCategory lookups (since ItemSellInfo doesn't expose it)
        private readonly Dictionary<int, string> _categoryCache = new Dictionary<int, string>();

        // Reflection cache
        private static FieldInfo _chestDataField;
        private static object _notificationStackInstance;
        private static MethodInfo _sendNotificationMethod;
        private static MethodInfo _databaseGetDataMethod;
        private static bool _reflectionInitialized;

        // Museum donation check cache (S.M.U.T. integration via reflection)
        private static bool _museumReflectionInitialized;
        private static bool _museumModAvailable;
        private static MethodInfo _getDonationManagerMethod;
        private static MethodInfo _hasDonatedByGameIdMethod;
        private static MethodInfo _findByGameItemIdMethod;
        private static PropertyInfo _isLoadedProperty;

        public bool IsDirty => _isDirty;

        public bool UserClearedAllRulesThisSession { get; private set; }

        public void LoadData(SmartChestSaveData data)
        {
            _smartChests.Clear();
            _groups.Clear();
            _categoryCache.Clear();
            UserClearedAllRulesThisSession = false;

            if (data == null)
                return;

            _characterName = data.CharacterName;

            foreach (var chest in data.Chests)
            {
                _smartChests[chest.ChestId] = chest;
            }

            if (data.Groups != null)
            {
                foreach (var g in data.Groups)
                {
                    if (!string.IsNullOrWhiteSpace(g.Name) && g.ItemIds != null)
                        _groups[g.Name] = g;
                }
            }

            Plugin.Log?.LogInfo($"Loaded {_smartChests.Count} smart chest(s), {_groups.Count} group(s)");
        }

        public SmartChestSaveData GetSaveData()
        {
            return new SmartChestSaveData
            {
                CharacterName = _characterName,
                Chests = _smartChests.Values.ToList(),
                Groups = _groups.Values.ToList()
            };
        }

        public IReadOnlyList<ItemGroup> GetGroups() => _groups.Values.ToList();

        public ItemGroup GetGroup(string name)
        {
            return !string.IsNullOrEmpty(name) && _groups.TryGetValue(name, out var g) ? g : null;
        }

        public ItemGroup GetOrCreateGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var key = name.Trim();
            if (_groups.TryGetValue(key, out var existing))
                return existing;
            var g = new ItemGroup { Name = key, ItemIds = new List<int>() };
            _groups[key] = g;
            _isDirty = true;
            return g;
        }

        public void RemoveGroup(string name)
        {
            if (!string.IsNullOrEmpty(name) && _groups.Remove(name.Trim()))
                _isDirty = true;
        }

        public void SetCharacterName(string name)
        {
            _characterName = name;
        }

        public void MarkClean()
        {
            _isDirty = false;
        }

        public void MarkDirty()
        {
            _isDirty = true;
        }

        public SmartChestData GetOrCreateSmartChest(string chestId, string chestName)
        {
            if (_smartChests.TryGetValue(chestId, out var existing))
            {
                existing.ChestName = chestName;
                return existing;
            }

            var data = new SmartChestData(chestId, chestName);
            _smartChests[chestId] = data;
            _isDirty = true;
            return data;
        }

        public bool IsSmartChest(string chestId)
        {
            return _smartChests.ContainsKey(chestId)
                && _smartChests[chestId].IsEnabled
                && _smartChests[chestId].Rules.Count > 0;
        }

        /// <summary>
        /// Returns how many other registered smart chests share the same ChestName
        /// as the given chest (excludes the source chest itself).
        /// </summary>
        public int CountSameNameChests(string chestId)
        {
            if (!_smartChests.TryGetValue(chestId, out var source)) return 0;
            int count = 0;
            foreach (var kvp in _smartChests)
            {
                if (kvp.Key == chestId) continue;
                if (string.Equals(kvp.Value.ChestName, source.ChestName, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Copies the rule set of the source chest to every other registered smart
        /// chest that shares the same ChestName. Returns the number of chests updated.
        /// </summary>
        public int CopyRulesToSameNameChests(string sourceChestId)
        {
            if (!_smartChests.TryGetValue(sourceChestId, out var source) || source.Rules.Count == 0)
                return 0;

            int updated = 0;
            foreach (var kvp in _smartChests)
            {
                if (kvp.Key == sourceChestId) continue;
                if (!string.Equals(kvp.Value.ChestName, source.ChestName, StringComparison.OrdinalIgnoreCase))
                    continue;
                kvp.Value.Rules.Clear();
                foreach (var rule in source.Rules)
                    kvp.Value.Rules.Add(CloneRule(rule));
                updated++;
            }
            if (updated > 0) _isDirty = true;
            return updated;
        }

        private static SmartChestRule CloneRule(SmartChestRule r) => new SmartChestRule
        {
            Type = r.Type,
            ItemId = r.ItemId,
            CategoryName = r.CategoryName,
            ItemTypeName = r.ItemTypeName,
            PropertyName = r.PropertyName,
            GroupName = r.GroupName,
            NamePattern = r.NamePattern,
        };

        /// <returns>True if a saved entry existed and was removed.</returns>
        public bool RemoveSmartChest(string chestId, bool userInitiated = false)
        {
            if (_smartChests.Remove(chestId))
            {
                _isDirty = true;
                if (userInitiated && _smartChests.Count == 0)
                    UserClearedAllRulesThisSession = true;
                return true;
            }

            return false;
        }

        public static string GetChestId(Chest chest)
        {
            if (chest == null)
                return null;

            var decoration = chest as Decoration;
            if (decoration == null)
                return null;

            var pos = decoration.Position;
            // Round to integers so IDs are stable across save/load (float precision can differ)
            int x = (int)Math.Round((double)pos.x);
            int y = (int)Math.Round((double)pos.y);
            int z = (int)Math.Round((double)pos.z);
            return $"{x}_{y}_{z}";
        }

        #region Reflection Helpers

        /// <summary>
        /// Gets the display name for a chest using the cached Chest.data field.
        /// </summary>
        public static string GetChestName(Chest chest)
        {
            try
            {
                InitReflection();
                if (_chestDataField != null)
                {
                    var chestData = _chestDataField.GetValue(chest) as ChestData;
                    if (chestData != null && !string.IsNullOrEmpty(chestData.name))
                        return chestData.name;
                }
            }
            catch (Exception ex) { Plugin.Log?.LogDebug($"[SmartChestManager] GetChestName: {ex.Message}"); }
            return "Chest";
        }

        private static void InitReflection()
        {
            if (_reflectionInitialized)
                return;

            try
            {
                // Cache Chest.data field
                _chestDataField = typeof(Chest).GetField("data",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                // Cache Database.GetData<ItemData> via reflection
                var databaseType = AccessTools.TypeByName("Wish.Database") ?? AccessTools.TypeByName("Database");
                if (databaseType != null)
                {
                    var getDataGeneric = databaseType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "GetData" && m.IsGenericMethod && m.GetParameters().Length == 3);
                    if (getDataGeneric != null)
                    {
                        _databaseGetDataMethod = getDataGeneric.MakeGenericMethod(typeof(ItemData));
                    }
                }

                _reflectionInitialized = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Failed to initialize reflection: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the full ItemSellInfo for an item by ID. Delegates to shared ItemSearch.
        /// </summary>
        private static ItemSellInfo GetItemSellInfo(int itemId)
        {
            return ItemSearch.GetItemSellInfo(itemId);
        }

        private static void SendNotification(string message)
        {
            try
            {
                if (_notificationStackInstance == null || (_notificationStackInstance is UnityEngine.Object obj && obj == null))
                {
                    var stackType = SunhavenMods.Shared.ReflectionHelper.FindWishType("NotificationStack");
                    _notificationStackInstance = stackType != null ? SunhavenMods.Shared.ReflectionHelper.GetSingletonInstance(stackType) : null;
                    if (_notificationStackInstance != null)
                    {
                        _sendNotificationMethod = _notificationStackInstance.GetType()
                            .GetMethod("SendNotification", new[] { typeof(string), typeof(int), typeof(int), typeof(bool), typeof(bool) });
                    }
                }

                _sendNotificationMethod?.Invoke(_notificationStackInstance,
                    new object[] { message, 0, 0, true, false });
            }
            catch (Exception ex) { Plugin.Log?.LogDebug($"[SmartChestManager] SendNotification: {ex.Message}"); }
        }

        private bool IsChestInUse(Chest chest)
        {
            try
            {
                InitReflection();
                if (_chestDataField != null)
                {
                    var chestData = _chestDataField.GetValue(chest) as ChestData;
                    return chestData?.inUse ?? false;
                }
            }
            catch (Exception ex) { Plugin.Log?.LogDebug($"[SmartChestManager] IsChestInUse: {ex.Message}"); }
            return false;
        }

        #endregion

        #region Scan Engine

        public void ExecuteScan(int maxItemsPerScan, bool enableNotifications)
        {
            if (_smartChests.Count == 0)
            {
                Plugin.Log?.LogDebug("[Scan] No smart chests configured, skipping scan");
                return;
            }

            InitReflection();

            var inventories = ChestManager.inventories;
            var associatedChests = ChestManager.associatedChests;

            if (inventories == null || inventories.Count == 0)
            {
                Plugin.Log?.LogDebug("[Scan] No inventories loaded, skipping scan");
                return;
            }

            if (associatedChests == null || associatedChests.Count == 0)
            {
                Plugin.Log?.LogDebug("[Scan] No associated chests found, skipping scan");
                return;
            }

            Plugin.Log?.LogDebug($"[Scan] Starting scan: {_smartChests.Count} smart chest(s), {associatedChests.Count} associated chest(s)");

            int totalMoved = 0;
            var modifiedChests = new HashSet<Chest>();

            // Build a lookup from chestId to (Inventory, Chest)
            var chestLookup = new Dictionary<string, KeyValuePair<Inventory, Chest>>();
            foreach (var kvp in associatedChests)
            {
                var chestId = GetChestId(kvp.Value);
                if (chestId != null && !chestLookup.ContainsKey(chestId))
                {
                    chestLookup[chestId] = new KeyValuePair<Inventory, Chest>(kvp.Key, kvp.Value);
                }
            }

            Plugin.Log?.LogDebug($"[Scan] Built chest lookup: {chestLookup.Count} unique chests");

            // Rule removal is via SmartChestUI "Remove Smart Chest" only. Chest.OnDisable fires on area
            // changes and cannot be used to detect pickup reliably in Sun Haven.

            foreach (var smartChestEntry in _smartChests)
            {
                if (totalMoved >= maxItemsPerScan)
                    break;

                var smartData = smartChestEntry.Value;
                if (!smartData.IsEnabled || smartData.Rules.Count == 0)
                {
                    Plugin.Log?.LogDebug($"[Scan] Skipping '{smartData.ChestName}' (enabled={smartData.IsEnabled}, rules={smartData.Rules.Count})");
                    continue;
                }

                if (!chestLookup.TryGetValue(smartData.ChestId, out var targetPair))
                {
                    Plugin.Log?.LogWarning($"[Scan] Smart chest '{smartData.ChestName}' (id={smartData.ChestId}) not found in loaded chests");
                    continue;
                }

                var targetInventory = targetPair.Key;
                var targetChest = targetPair.Value;

                if (IsChestInUse(targetChest))
                {
                    Plugin.Log?.LogDebug($"[Scan] Target chest '{smartData.ChestName}' is in use, skipping");
                    continue;
                }

                Plugin.Log?.LogDebug($"[Scan] Scanning for items matching '{smartData.ChestName}' ({smartData.Rules.Count} rules)");

                int sourcesScanned = 0;
                foreach (var sourceEntry in chestLookup)
                {
                    if (totalMoved >= maxItemsPerScan)
                        break;

                    if (sourceEntry.Key == smartData.ChestId)
                        continue;

                    var sourceInventory = sourceEntry.Value.Key;
                    var sourceChest = sourceEntry.Value.Value;

                    if (IsChestInUse(sourceChest))
                        continue;

                    if (IsSmartChest(sourceEntry.Key))
                        continue;

                    sourcesScanned++;
                    int moved = TransferMatchingItems(sourceInventory, sourceChest,
                        targetInventory, targetChest, smartData.Rules,
                        maxItemsPerScan - totalMoved);

                    if (moved > 0)
                    {
                        totalMoved += moved;
                        modifiedChests.Add(sourceChest);
                        modifiedChests.Add(targetChest);
                    }
                }

                Plugin.Log?.LogDebug($"[Scan] Scanned {sourcesScanned} source chests for '{smartData.ChestName}'");
            }

            // Phase 2: Eject non-matching items from smart chests
            foreach (var smartChestEntry in _smartChests)
            {
                if (totalMoved >= maxItemsPerScan)
                    break;

                var smartData = smartChestEntry.Value;
                if (!smartData.IsEnabled || smartData.Rules.Count == 0)
                    continue;

                if (!chestLookup.TryGetValue(smartData.ChestId, out var smartPair))
                    continue;

                var smartInventory = smartPair.Key;
                var smartChest = smartPair.Value;

                if (IsChestInUse(smartChest))
                    continue;

                var smartItems = smartInventory.Items;
                if (smartItems == null)
                    continue;

                int slotCount = Mathf.Min(smartInventory.maxSlots, smartItems.Count);

                for (int i = slotCount - 1; i >= 0 && totalMoved < maxItemsPerScan; i--)
                {
                    var slotData = smartItems[i];
                    if (slotData.id <= 0 || slotData.amount <= 0)
                        continue;

                    // Item matches this chest's rules — it belongs here
                    if (MatchesAnyRule(slotData.id, smartData.Rules))
                        continue;

                    var sellInfo = GetItemSellInfo(slotData.id);
                    var itemName = sellInfo?.name ?? $"Item {slotData.id}";

                    // Try to find another smart chest that wants this item
                    bool ejected = false;
                    foreach (var otherEntry in _smartChests)
                    {
                        if (otherEntry.Key == smartData.ChestId)
                            continue;
                        if (!otherEntry.Value.IsEnabled || otherEntry.Value.Rules.Count == 0)
                            continue;
                        if (!MatchesAnyRule(slotData.id, otherEntry.Value.Rules))
                            continue;
                        if (!chestLookup.TryGetValue(otherEntry.Value.ChestId, out var destPair))
                            continue;
                        if (IsChestInUse(destPair.Value))
                            continue;

                        int amountToAccept;
                        if (!destPair.Key.CanAcceptItem(slotData.item, slotData.amount, out amountToAccept) || amountToAccept <= 0)
                            continue;

                        Plugin.Log?.LogInfo($"[Scan] Ejecting {itemName} x{amountToAccept} from '{smartData.ChestName}' to smart chest '{otherEntry.Value.ChestName}'");
                        TransferItemData(smartInventory, i, destPair.Key, slotData.item, amountToAccept, slotData.id);
                        modifiedChests.Add(smartChest);
                        modifiedChests.Add(destPair.Value);
                        totalMoved++;
                        ejected = true;
                        break;
                    }

                    if (ejected)
                        continue;

                    // No matching smart chest — send to any normal chest with space
                    foreach (var normalEntry in chestLookup)
                    {
                        if (IsSmartChest(normalEntry.Key))
                            continue;
                        if (IsChestInUse(normalEntry.Value.Value))
                            continue;

                        int amountToAccept;
                        if (!normalEntry.Value.Key.CanAcceptItem(slotData.item, slotData.amount, out amountToAccept) || amountToAccept <= 0)
                            continue;

                        Plugin.Log?.LogInfo($"[Scan] Ejecting {itemName} x{amountToAccept} from '{smartData.ChestName}' to normal chest");
                        TransferItemData(smartInventory, i, normalEntry.Value.Key, slotData.item, amountToAccept, slotData.id);
                        modifiedChests.Add(smartChest);
                        modifiedChests.Add(normalEntry.Value.Value);
                        totalMoved++;
                        break;
                    }
                }
            }

            // Persist changes for all modified chests
            foreach (var chest in modifiedChests)
            {
                try
                {
                    chest.SaveMeta();
                    chest.SendNewMeta(((Decoration)chest).meta);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"Error saving chest meta: {ex.Message}");
                }
            }

            if (totalMoved > 0)
            {
                Plugin.Log?.LogInfo($"[Scan] Complete: sorted {totalMoved} item stack(s)");
                if (enableNotifications)
                {
                    SendNotification("Senpai's Chest: Sorted");
                }
            }
            else
            {
                Plugin.Log?.LogDebug("[Scan] Complete: no items to sort");
            }
        }

        private int TransferMatchingItems(Inventory sourceInv, Chest sourceChest,
            Inventory targetInv, Chest targetChest,
            List<SmartChestRule> rules, int maxItems)
        {
            int moved = 0;
            var sourceItems = sourceInv.Items;
            if (sourceItems == null)
                return 0;

            int count = Mathf.Min(sourceInv.maxSlots, sourceItems.Count);

            for (int i = count - 1; i >= 0 && moved < maxItems; i--)
            {
                var slotData = sourceItems[i];
                if (slotData.id <= 0 || slotData.amount <= 0)
                    continue;

                if (!MatchesAnyRule(slotData.id, rules))
                    continue;

                int amountToAccept;
                if (!targetInv.CanAcceptItem(slotData.item, slotData.amount, out amountToAccept))
                {
                    Plugin.Log?.LogDebug($"[Scan] Target cannot accept item {slotData.id} (amount={slotData.amount})");
                    continue;
                }

                if (amountToAccept <= 0)
                    continue;

                var sellInfo = GetItemSellInfo(slotData.id);
                var itemName = sellInfo?.name ?? $"Item {slotData.id}";
                Plugin.Log?.LogInfo($"[Scan] Moving {itemName} x{amountToAccept}");

                TransferItemData(sourceInv, i, targetInv, slotData.item, amountToAccept, slotData.id);
                moved++;
            }

            return moved;
        }

        private void TransferItemData(Inventory sourceInv, int sourceSlot,
            Inventory targetInv, Item item, int amount, int itemId)
        {
            if (sourceInv == null || targetInv == null || item == null || amount <= 0 || itemId <= 0)
                return;

            // Only remove what the target actually accepted — never drop items on a failed AddItem.
            // Wish.Item exposes ID() / slot ids, not a public `id` field.
            int targetBefore = targetInv.GetAmount(itemId);
            targetInv.AddItem(item, amount, false);
            int added = targetInv.GetAmount(itemId) - targetBefore;
            if (added <= 0)
            {
                Plugin.Log?.LogWarning($"[Scan] AddItem accepted 0 of item {itemId}; skipping source remove");
                return;
            }

            int toRemove = Mathf.Min(added, amount);
            sourceInv.RemoveItemAt(sourceSlot, toRemove);
            if (toRemove < amount)
                Plugin.Log?.LogWarning($"[Scan] Partial transfer of item {itemId}: added {toRemove}/{amount}");
        }

        #endregion

        #region Rule Matching

        private bool MatchesAnyRule(int itemId, List<SmartChestRule> rules)
        {
            foreach (var rule in rules)
            {
                if (MatchesRule(itemId, rule))
                    return true;
            }
            return false;
        }

        private bool MatchesRule(int itemId, SmartChestRule rule)
        {
            switch (rule.Type)
            {
                case RuleType.ByItemId:
                    return itemId == rule.ItemId;

                case RuleType.ByItemType:
                    var sellInfo = GetItemSellInfo(itemId);
                    if (sellInfo == null) return false;
                    return sellInfo.itemType.ToString() == rule.ItemTypeName;

                case RuleType.ByProperty:
                    if (rule.PropertyName == "isNotDonated")
                        return IsMuseumItemNotDonated(itemId);

                    var info = GetItemSellInfo(itemId);
                    if (info == null) return false;
                    switch (rule.PropertyName)
                    {
                        case "isGem": return info.isGem;
                        case "isForageable": return info.isForageable;
                        case "isAnimalProduct": return info.isAnimalProduct;
                        case "isMeal": return info.isMeal;
                        case "isFruit": return info.isFruit;
                        case "isArtisanryItem": return info.isArtisanryItem;
                        case "isPotion": return info.isPotion;
                        default: return false;
                    }

                case RuleType.ByCategory:
                    return MatchesCategory(itemId, rule.CategoryName);

                case RuleType.ByGroup:
                    return MatchesGroup(itemId, rule.GroupName);

                case RuleType.ByNamePattern:
                    var sn = GetItemSellInfo(itemId);
                    string displayName = sn?.name ?? ItemSearch.GetItemName(itemId) ?? "";
                    return ItemNamePatternMatch.Matches(displayName, rule.NamePattern);

                default:
                    return false;
            }
        }

        private bool MatchesCategory(int itemId, string categoryName)
        {
            if (string.Equals(categoryName, "Undonated Items", StringComparison.OrdinalIgnoreCase))
                return IsMuseumItemNotDonated(itemId);

            if (_categoryCache.TryGetValue(itemId, out var cached))
            {
                return cached == categoryName;
            }

            // Use reflection to call Database.GetData<ItemData>
            bool matched = false;
            try
            {
                if (_databaseGetDataMethod != null)
                {
                    Action<ItemData> callback = itemData =>
                    {
                        if (itemData != null)
                        {
                            _categoryCache[itemId] = itemData.category.ToString();
                            matched = itemData.category.ToString() == categoryName;
                        }
                    };

                    _databaseGetDataMethod.Invoke(null, new object[] { itemId, callback, null });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Failed to lookup category for item {itemId}: {ex.Message}");
            }

            return matched;
        }

        private bool MatchesGroup(int itemId, string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) return false;
            var g = GetGroup(groupName);
            if (g == null) return false;

            if (g.ItemIds != null && g.ItemIds.Contains(itemId))
                return true;

            if (g.NamePatterns != null && g.NamePatterns.Count > 0)
            {
                var info = GetItemSellInfo(itemId);
                string displayName = info?.name ?? ItemSearch.GetItemName(itemId) ?? "";
                for (int i = 0; i < g.NamePatterns.Count; i++)
                {
                    if (ItemNamePatternMatch.Matches(displayName, g.NamePatterns[i]))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if an item is a museum item that hasn't been donated yet.
        /// Uses reflection to access S.M.U.T. (soft dependency).
        /// </summary>
        private static bool IsMuseumItemNotDonated(int itemId)
        {
            if (!_museumReflectionInitialized)
            {
                _museumReflectionInitialized = true;
                try
                {
                    var pluginType = AccessTools.TypeByName("SunHavenMuseumUtilityTracker.Plugin");
                    if (pluginType != null)
                    {
                        _getDonationManagerMethod = pluginType.GetMethod("GetDonationManager",
                            BindingFlags.Public | BindingFlags.Static);

                        var contentType = AccessTools.TypeByName("SunHavenMuseumUtilityTracker.Data.MuseumContent");
                        if (contentType != null)
                        {
                            _findByGameItemIdMethod = contentType.GetMethod("FindByGameItemId",
                                BindingFlags.Public | BindingFlags.Static);
                        }

                        _museumModAvailable = _getDonationManagerMethod != null && _findByGameItemIdMethod != null;

                        if (_museumModAvailable)
                            Plugin.Log?.LogInfo("[Museum] S.M.U.T. integration available for isNotDonated rule");
                        else
                            Plugin.Log?.LogWarning("[Museum] S.M.U.T. found but missing expected methods");
                    }
                    else
                    {
                        Plugin.Log?.LogInfo("[Museum] S.M.U.T. not installed — isNotDonated rule will not match any items");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[Museum] Failed to initialize S.M.U.T. reflection: {ex.Message}");
                }
            }

            if (!_museumModAvailable)
                return false;

            try
            {
                // Check if it's a museum item
                var museumItem = _findByGameItemIdMethod.Invoke(null, new object[] { itemId });
                if (museumItem == null)
                    return false;

                // Get the donation manager (re-fetch each time in case it was recreated)
                var donationManager = _getDonationManagerMethod.Invoke(null, null);
                if (donationManager == null)
                    return false;

                // Cache the HasDonatedByGameId method if needed
                if (_hasDonatedByGameIdMethod == null)
                {
                    _hasDonatedByGameIdMethod = donationManager.GetType().GetMethod("HasDonatedByGameId",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (_hasDonatedByGameIdMethod == null)
                        return false;

                    // Also get IsLoaded property
                    _isLoadedProperty = donationManager.GetType().GetProperty("IsLoaded",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Check if donation data is loaded
                if (_isLoadedProperty != null)
                {
                    var isLoaded = (bool)_isLoadedProperty.GetValue(donationManager);
                    if (!isLoaded)
                        return false;
                }

                // Check if NOT donated
                var isDonated = (bool)_hasDonatedByGameIdMethod.Invoke(donationManager, new object[] { itemId });
                return !isDonated;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[SmartChestManager] IsMuseumItemNotDonated({itemId}): {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}