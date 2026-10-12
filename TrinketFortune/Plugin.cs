using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using SunhavenMods.Shared;
using TrinketFortune.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrinketFortune
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log { get; private set; }
        private Harmony _harmony;
        private bool _applicationQuitting;

        private void Awake()
        {
            Log = Logger;
            var namedConfig = ConfigFileHelper.CreateNamedConfig(
                PluginInfo.PLUGIN_GUID,
                "TrinketFortune.cfg",
                message => Log?.LogWarning(message));
            ConfigFileHelper.ReplacePluginConfig(this, namedConfig, Log.LogWarning);
            TrinketFortune.Config.Bind(namedConfig);

            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            try
            {
                FishingTrinketPatches.ApplyPatches(_harmony);
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Harmony patches failed (game version drift?): {ex}");
            }

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded. Fishing loot bias active when S.M.U.T. is installed.");
            ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                ModHealthIntegrationSummary.Build(
                    ("DevTools", SuitePluginGuids.DevTools),
                    ("SMUT", SuitePluginGuids.Smut)));
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void OnDestroy()
        {
            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
                Log?.LogInfo($"[Lifecycle] Plugin OnDestroy during expected teardown (scene: {sceneName})");
            else
                Log?.LogWarning($"[Lifecycle] Plugin OnDestroy outside expected teardown (scene: {sceneName})");

            // Leave patches applied. UnpatchSelf here dropped fishing loot hooks when this
            // component was destroyed during scene cleanup, so Trinket Fortune stayed dead until
            // restart. Same rule as Birthday Reminder (AZR-437), The Vault, and Gifting Assistant.
        }

        /// <summary>
        /// Lightweight status for Haven Dev Tools → Suite tab (soft integration).
        /// </summary>
        public static string GetDevToolsSummary()
        {
            if (TrinketFortune.Config.Enabled == null)
                return "Not ready";

            bool smutInstalled = Chainloader.PluginInfos != null
                && Chainloader.PluginInfos.ContainsKey(SuitePluginGuids.Smut);
            string integration = DonationHelper.IsAvailable
                ? "S.M.U.T. donations linked"
                : smutInstalled
                    ? "S.M.U.T. installed (load a save for donation data)"
                    : "standalone mode";

            if (!TrinketFortune.Config.Enabled.Value)
                return $"Disabled in config | {integration}";

            float aquariumPct = DonationHelper.GetAquariumProgressPercent();
            return $"Aquarium: {aquariumPct:F1}% | Bonus {TrinketFortune.Config.MuseumProgressBonusPercent.Value}% per 10% | " +
                   $"Min {TrinketFortune.Config.MinimumMuseumProgress.Value:P0} | Cap {TrinketFortune.Config.MaxBonusChancePercent.Value}% | {integration}";
        }

        public static class PluginInfo
        {
            public const string PLUGIN_GUID = "com.azraelgodking.trinketfortune";
            public const string PLUGIN_NAME = "Trinket Fortune";
            public const string PLUGIN_VERSION = "2.1.3";
        }
    }
}
