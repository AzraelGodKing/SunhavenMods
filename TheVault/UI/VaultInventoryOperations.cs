using System;
using HarmonyLib;
using TheVault.Patches;
using TheVault.Vault;
using UnityEngine;
using Wish;

namespace TheVault.UI
{
    /// <summary>
    /// Deposit / withdraw operations for vault windows.
    /// </summary>
    public static class VaultInventoryOperations
    {
        public static void WithdrawToInventory(VaultManager vaultManager, string currencyId, int amount,
            Action<string, bool> setStatus)
        {
            int current = vaultManager.GetCurrency(currencyId);
            if (current < amount)
            {
                setStatus?.Invoke($"Not enough in vault (have {current}, need {amount}).", true);
                Plugin.Log?.LogWarning($"Not enough {currencyId} in vault (have {current}, need {amount})");
                return;
            }

            int itemId = ItemPatches.GetItemForCurrency(currencyId);
            if (itemId < 0)
            {
                setStatus?.Invoke("This currency cannot be turned into an inventory item.", true);
                Plugin.Log?.LogWarning($"No item mapping found for currency {currencyId}");
                return;
            }

            if (Player.Instance == null)
            {
                setStatus?.Invoke("Player not ready — try again in-game.", true);
                Plugin.Log?.LogWarning("Player instance not found");
                return;
            }

            var inventory = Player.Instance.PlayerInventory ?? Player.Instance.Inventory;
            if (inventory == null)
            {
                setStatus?.Invoke("Inventory not available.", true);
                Plugin.Log?.LogWarning("Player inventory not found");
                return;
            }

            try
            {
                ItemPatches.IsWithdrawing = true;
                ItemPatches.StartWithdrawing(itemId);

                try
                {
                    RemoveCurrency(vaultManager, currencyId, amount);

                    var addItemMethod = AccessTools.Method(inventory.GetType(), "AddItem",
                        new[] { typeof(int), typeof(int), typeof(bool) });

                    if (addItemMethod != null)
                    {
                        addItemMethod.Invoke(inventory, new object[] { itemId, amount, true });
                        Plugin.Log?.LogInfo($"Withdrew {amount} of {currencyId} (itemId={itemId}) to inventory");
                        setStatus?.Invoke($"Withdrew {amount} to inventory.", false);
                    }
                    else
                    {
                        var simpleAddMethod = AccessTools.Method(inventory.GetType(), "AddItem", new[] { typeof(int) });
                        if (simpleAddMethod != null)
                        {
                            for (int i = 0; i < amount; i++)
                                simpleAddMethod.Invoke(inventory, new object[] { itemId });
                            Plugin.Log?.LogInfo($"Withdrew {amount} of {currencyId} (itemId={itemId}) to inventory (simple method)");
                            setStatus?.Invoke($"Withdrew {amount} to inventory.", false);
                        }
                        else
                        {
                            Plugin.Log?.LogError("Could not find AddItem method on inventory");
                            AddCurrencyBack(vaultManager, currencyId, amount);
                            setStatus?.Invoke("Could not add to inventory — vault balance unchanged.", true);
                        }
                    }
                }
                finally
                {
                    ItemPatches.IsWithdrawing = false;
                    ItemPatches.StopWithdrawing(itemId);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error withdrawing to inventory: {ex.Message}");
                ItemPatches.StopWithdrawing(itemId);
                AddCurrencyBack(vaultManager, currencyId, amount);
                setStatus?.Invoke("Withdraw failed — see BepInEx log. Vault balance restored.", true);
            }
        }

        public static void DepositFromInventory(VaultManager vaultManager, string currencyId, int amount,
            Action<string, bool> setStatus)
        {
            int itemId = ItemPatches.GetItemForCurrency(currencyId);
            if (itemId < 0)
            {
                setStatus?.Invoke("Deposit needs an inventory item mapping for this currency.", true);
                return;
            }

            int inBag = ItemPatches.GetRawInventoryCount(itemId);
            if (inBag < amount)
            {
                setStatus?.Invoke($"Only {inBag} in your inventory (bag).", true);
                return;
            }

            if (ItemPatches.DepositItemToVault(itemId, amount))
            {
                setStatus?.Invoke($"Deposited {amount} into the vault.", false);
                Plugin.Log?.LogInfo($"[Vault UI] Deposited {amount} of item {itemId} as {currencyId}");
            }
            else
                setStatus?.Invoke("Deposit failed — see BepInEx log.", true);
        }

        public static void RemoveCurrency(VaultManager vaultManager, string currencyId, int amount)
        {
            if (currencyId.StartsWith("seasonal_"))
            {
                string typeName = currencyId.Substring("seasonal_".Length);
                if (Enum.TryParse<SeasonalTokenType>(typeName, out var tokenType))
                    vaultManager.RemoveSeasonalTokens(tokenType, amount);
            }
            else if (currencyId.StartsWith("community_"))
                vaultManager.RemoveCommunityTokens(currencyId.Substring("community_".Length), amount);
            else if (currencyId.StartsWith("key_"))
                vaultManager.RemoveKeys(currencyId.Substring("key_".Length), amount);
            else if (currencyId.StartsWith("special_"))
                vaultManager.RemoveSpecial(currencyId.Substring("special_".Length), amount);
            else if (currencyId.StartsWith("orb_"))
                vaultManager.RemoveOrbs(currencyId.Substring("orb_".Length), amount);
            else if (currencyId.StartsWith("custom_"))
                vaultManager.RemoveCustomCurrency(currencyId.Substring("custom_".Length), amount);
        }

        private static void AddCurrencyBack(VaultManager vaultManager, string currencyId, int amount)
        {
            if (currencyId.StartsWith("seasonal_"))
            {
                string typeName = currencyId.Substring("seasonal_".Length);
                if (Enum.TryParse<SeasonalTokenType>(typeName, out var tokenType))
                    vaultManager.AddSeasonalTokens(tokenType, amount);
            }
            else if (currencyId.StartsWith("community_"))
                vaultManager.AddCommunityTokens(currencyId.Substring("community_".Length), amount);
            else if (currencyId.StartsWith("key_"))
                vaultManager.AddKeys(currencyId.Substring("key_".Length), amount);
            else if (currencyId.StartsWith("special_"))
                vaultManager.AddSpecial(currencyId.Substring("special_".Length), amount);
            else if (currencyId.StartsWith("orb_"))
                vaultManager.AddOrbs(currencyId.Substring("orb_".Length), amount);
            else if (currencyId.StartsWith("custom_"))
                vaultManager.AddCustomCurrency(currencyId.Substring("custom_".Length), amount);
        }
    }
}
