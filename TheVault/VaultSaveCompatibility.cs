using BepInEx.Logging;
using Wish;

namespace TheVault
{
    /// <summary>
    /// Flip <see cref="ThisReleaseMayBreakVaultSaves"/> to true only when shipping a build that cannot
    /// migrate vault data forward, following <c>maintainer-docs/ATOMIC_SAVE_POLICY.md</c>.
    /// </summary>
    public static class VaultSaveCompatibility
    {
        /// <summary>Must match the release process in maintainer-docs/ATOMIC_SAVE_POLICY.md before setting true.</summary>
        public const bool ThisReleaseMayBreakVaultSaves = false;

        public static void WarnIfBreakingRelease(ManualLogSource log)
        {
            if (!ThisReleaseMayBreakVaultSaves || log == null)
                return;

            log.LogWarning(
                "================================================================================\n" +
                "The Vault — VAULT SAVE COMPATIBILITY WARNING\n" +
                "This release may NOT preserve your existing vault data. Do not continue unless\n" +
                "you have backed up the paths listed in the mod README (ATOMIC_SAVE_POLICY.md).\n" +
                "================================================================================");

            try
            {
                if (NotificationStack.Instance != null)
                {
                    NotificationStack.Instance.SendNotification(
                        "The Vault: This update may break vault saves — check README / BepInEx log and back up first.");
                }
            }
            catch
            {
                /* non-fatal */
            }
        }
    }
}
