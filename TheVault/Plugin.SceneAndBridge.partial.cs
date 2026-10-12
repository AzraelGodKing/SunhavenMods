using TheVault.Modding;

namespace TheVault
{
    /// <summary><see cref="VaultModApiBridge.OnVaultLoaded"/> wire-up (split from <see cref="Plugin"/> for maintainability).</summary>
    public partial class Plugin
    {
        private static void OnVaultDataLoaded()
        {
            try
            {
                VaultModApiBridge.NotifyVaultLoaded();
            }
            catch (System.Exception ex)
            {
                Log?.LogWarning($"[The Vault] Failed to raise OnVaultLoaded bridge event: {ex.Message}");
            }
        }
    }
}
