using System;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using CropOptimizer.Config;
using CropOptimizer.Data;
using CropOptimizer.Integration;
using CropOptimizer.Patches;
using CropOptimizer.UI;
using HarmonyLib;
using SunhavenMods.Shared;
using System.Reflection;
using UnityEngine;

namespace CropOptimizer
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.azraelgodking.thevault", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log { get; private set; }
        public static Plugin Instance { get; private set; }
        public static bool IsDebugLoggingEnabled => Instance?._config?.DebugLogging?.Value == true;

        private Harmony _harmony;
        private CropOptimizerConfig _config;
        internal CropForecast _forecast;
        private CropHUD _hud;
        private VaultIntegration _vaultIntegration;
        private bool _hudVisible = true;
        private bool _applicationQuitting;
        private bool _isVaultLoadedEventSubscribed;
        private EventInfo _vaultLoadedEventInfo;
        private Delegate _vaultLoadedHandler;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            var namedConfig = CreateNamedConfig();
            SunhavenMods.Shared.ConfigFileHelper.ReplacePluginConfig(this, namedConfig, Log.LogWarning);
            _config = new CropOptimizerConfig(namedConfig);
            if (!_config.Enabled.Value)
            {
                Log.LogInfo($"{PluginInfo.PLUGIN_NAME} disabled in config.");
                ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                    ModHealthIntegrationSummary.Build(
                        ("DevTools", SuitePluginGuids.DevTools),
                        ("Vault", SuitePluginGuids.TheVault)),
                    mode: "disabled");
                return;
            }

            _forecast = new CropForecast();
            _vaultIntegration = new VaultIntegration();

            LocalizationBootstrap.BindForceEnglish(namedConfig);
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            LocalizationBootstrap.Init(PluginInfo.PLUGIN_GUID, _harmony, Log, Assembly.GetExecutingAssembly());
            ModLocalization.LanguageChanged += OnLanguageChanged;
            CropGrowthPatch.Apply(_harmony, _forecast);
            CharacterLoadPatch.Apply(_harmony, _forecast);

            _hud = PersistentRunnerBase.CreateRunner<CropHUD>();
            _hud.Initialize(_forecast);
            _hud.SetPlacement(_config.HudPositionX.Value, _config.HudPositionY.Value);
            _hud.SetScale(_config.HudScale.Value);
            _hud.SetVisible(_config.HudEnabled.Value);
            _hudVisible = _config.HudEnabled.Value;
            _hud.PlacementChanged += OnCropHudPlacementChanged;
            _hud.SetHoverConfig(_config.HoverTooltipEnabled, _config.HoverTooltipMaxWorldDistance);
            _hud.SetHighlightConfig(_config);

            TrySubscribeVaultLoaded();

            if (_config.CheckForUpdates.Value)
            {
                SunhavenMods.Shared.VersionChecker.CheckForUpdate(
                    PluginInfo.PLUGIN_GUID,
                    PluginInfo.PLUGIN_VERSION,
                    Log,
                    result => result.NotifyUpdateAvailable(Log));
            }

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded");
            ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                ModHealthIntegrationSummary.Build(
                    ("DevTools", SuitePluginGuids.DevTools),
                    ("Vault", SuitePluginGuids.TheVault)));
        }

        private void OnDestroy()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
                Log?.LogInfo($"[Lifecycle] Plugin OnDestroy during expected teardown (scene: {sceneName})");
            else
                Log?.LogWarning($"[Lifecycle] Plugin OnDestroy outside expected teardown (scene: {sceneName})");

            if (_hud != null)
                _hud.PlacementChanged -= OnCropHudPlacementChanged;

            _harmony?.UnpatchSelf();

            if (_isVaultLoadedEventSubscribed && _vaultLoadedEventInfo != null && _vaultLoadedHandler != null)
            {
                _vaultLoadedEventInfo.RemoveEventHandler(null, _vaultLoadedHandler);
                _isVaultLoadedEventSubscribed = false;
                _vaultLoadedEventInfo = null;
                _vaultLoadedHandler = null;
            }

            ModLocalization.LanguageChanged -= OnLanguageChanged;
            ModLocalization.Shutdown();
        }

        private static void OnLanguageChanged(string _)
        {
            Instance?._hud?.RefreshLocalization();
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void OnCropHudPlacementChanged(float x, float y)
        {
            if (_config == null)
                return;
            _config.HudPositionX.Value = x;
            _config.HudPositionY.Value = y;
        }

        private void Update()
        {
            if (_config == null || _hud == null)
                return;

            if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                return;

            if (_config.ToggleHudKey.Value != KeyCode.None && Input.GetKeyDown(_config.ToggleHudKey.Value))
            {
                _hudVisible = !_hudVisible;
                _hud.SetVisible(_hudVisible);
            }
        }

        public static string GetHudSummary()
        {
            if (Instance?._forecast == null)
                return "Not ready";
            return $"Crops: {Instance._forecast.Snapshot().Count}, Value: {CropOptimizer.UI.CropSellDisplay.FormatPlain(Instance._forecast.GetProjectedShopValue())}";
        }

        internal static System.Collections.Generic.List<CropForecast.CropTypeSummary> GetTopCrops(int count = 5)
        {
            return Instance?._forecast?.GetTopCropsByValue(count)
                ?? new System.Collections.Generic.List<CropForecast.CropTypeSummary>();
        }

        private void TrySubscribeVaultLoaded()
        {
            if (_vaultIntegration == null || !_vaultIntegration.IsAvailable)
                return;

            try
            {
                var bridgeType = VaultReflection.GetBridgeType();
                if (bridgeType == null)
                {
                    Log?.LogDebug("[CropOptimizer] The Vault is installed but TheVault.Abstractions was not found; skipping Vault integration.");
                    return;
                }

                var evt = bridgeType.GetEvent("OnVaultLoaded", BindingFlags.Public | BindingFlags.Static);
                if (evt == null)
                {
                    Log?.LogDebug("[CropOptimizer] Vault bridge OnVaultLoaded event not found; skipping subscription.");
                    return;
                }

                var method = GetType().GetMethod(nameof(OnVaultLoaded), BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null)
                    return;

                var handler = Delegate.CreateDelegate(evt.EventHandlerType, this, method, false);
                if (handler == null)
                {
                    Log?.LogWarning("[CropOptimizer] Vault OnVaultLoaded handler could not be bound.");
                    return;
                }

                evt.AddEventHandler(null, handler);
                _isVaultLoadedEventSubscribed = true;
                _vaultLoadedEventInfo = evt;
                _vaultLoadedHandler = handler;

                var instance = VaultReflection.GetBridgeInstance(bridgeType);
                if (VaultReflection.IsVaultReady(instance))
                    _vaultIntegration.TryRegisterProjectedValueCurrency();
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[CropOptimizer] Failed to subscribe to Vault OnVaultLoaded: {ex.Message}");
            }
        }

        private void OnVaultLoaded()
        {
            try
            {
                _vaultIntegration?.TryRegisterProjectedValueCurrency();
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[CropOptimizer] Vault currency registration failed on OnVaultLoaded: {ex.Message}");
            }
        }

        private static ConfigFile CreateNamedConfig()
        {
            string configPath = Path.Combine(Paths.ConfigPath, "CropOptimizer.cfg");
            string legacyPath = Path.Combine(Paths.ConfigPath, $"{PluginInfo.PLUGIN_GUID}.cfg");
            try
            {
                if (!File.Exists(configPath) && File.Exists(legacyPath))
                    File.Copy(legacyPath, configPath);
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[Config] Migration to CropOptimizer.cfg failed: {ex.Message}");
            }
            return new ConfigFile(configPath, true);
        }
    }
}
