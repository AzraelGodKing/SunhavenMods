using System;
using HarmonyLib;
using Wish;

namespace GiftingAssistant.Game
{
    /// <summary>
    /// Reads how many of an item the player currently carries in their bag.
    /// Uses Player.Instance.PlayerInventory.GetAmount(itemId).
    ///
    /// Caveat: if The Vault is installed it augments Inventory.GetAmount with vault currency,
    /// so for registered vault currencies this count can include vault holdings. v1 accepts this
    /// and reports the bag count the game exposes.
    /// </summary>
    public static class PlayerInventoryReader
    {
        public static int GetAmount(int itemId)
        {
            if (itemId <= 0)
                return 0;

            try
            {
                var player = Player.Instance;
                object inventory = player?.PlayerInventory;
                if (inventory == null)
                    return 0;

                if (inventory is Inventory bag)
                    return bag.GetAmount(itemId);

                var getAmount = AccessTools.Method(inventory.GetType(), "GetAmount", new[] { typeof(int) });
                if (getAmount == null)
                    return 0;
                return getAmount.Invoke(inventory, new object[] { itemId }) is int amount ? amount : 0;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[PlayerInventoryReader] GetAmount({itemId}): {ex.Message}");
                return 0;
            }
        }
    }
}
