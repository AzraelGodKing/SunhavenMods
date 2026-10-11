using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using TheVault.Integration;
using TheVault.Modding;
using TheVault.Patches;
using TheVault.UI;
using TheVault.Vault;
using HarmonyLib;
using SunhavenMods.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wish;

namespace TheVault
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public partial class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static ConfigFile ConfigFile { get; private set; }

        // Static references that survive Plugin destruction
        // (Unity's null-conditional returns null for destroyed MonoBehaviours)
        private static VaultManager _staticVaultManager;
        private static VaultSaveSystem _staticSaveSystem;
        private static VaultUI _staticVaultUI;
        private static VaultHUD _staticVaultHUD;

        // Static config values for PersistentRunner to use for hotkey detection
        internal static KeyCode StaticToggleKey = KeyCode.V;
        internal static bool StaticRequireCtrl = true;
        internal static KeyCode StaticAltToggleKey = KeyCode.F8;
        internal static bool StaticControllerOpenEnabled = true;
        internal static KeyCode StaticControllerOpenModifier = KeyCode.JoystickButton4;
        internal static KeyCode StaticControllerOpenButton = KeyCode.JoystickButton3;
        internal static KeyCode StaticHUDToggleKey = KeyCode.F7;
        internal static KeyCode StaticQuickConvertKey = KeyCode.F6;
        private const float MinWindowScale = 0.5f;
        private const float MaxWindowScale = 3.0f;

        private Harmony _harmony;
        private VaultManager _vaultManager;
        private VaultSaveSystem _saveSystem;
        private VaultUI _vaultUI;
        private VaultHUD _vaultHUD;

        // Configuration
        private ConfigEntry<KeyCode> _toggleKey;
        private ConfigEntry<bool> _requireCtrlModifier;
        private ConfigEntry<KeyCode> _altToggleKey;
        private ConfigEntry<bool> _controllerOpenEnabled;
        private ConfigEntry<KeyCode> _controllerOpenModifier;
        private ConfigEntry<KeyCode> _controllerOpenButton;
        private ConfigEntry<bool> _enableHUD;
        private ConfigEntry<string> _hudPosition;
        private ConfigEntry<float> _hudPositionX;
        private ConfigEntry<float> _hudPositionY;
        private ConfigEntry<float> _hudScale;
        private ConfigEntry<bool> _hudCompactMode;
        private ConfigEntry<string> _hudDensity;
        private ConfigEntry<KeyCode> _hudToggleKey;
        private ConfigEntry<KeyCode> _quickConvertKey;
        private ConfigEntry<string> _quickConvertTable;
        private ConfigEntry<float> _windowScale;
        private ConfigEntry<bool> _scaleWithResolution;
        private ConfigEntry<bool> _autoDeckLayout;
        private ConfigEntry<bool> _deckLayoutApplied;
        private ConfigEntry<bool> _autoHighResLayout;
        private ConfigEntry<bool> _highResLayoutApplied;
        private ConfigEntry<bool> _enableAutoSave;
        private ConfigEntry<float> _autoSaveInterval;
        private ConfigEntry<bool> _checkForUpdates;

        /// <summary>Last applied <see cref="_hudPosition"/>; when it changes, saved pixel HUD coords are cleared.</summary>
        private string _hudPositionConfigBaseline;

        /// <summary>
        /// Full vault inspector (zeros, Debug tab, full HUD). Controlled by Haven Dev Tools config / UI, not TheVault.cfg.
        /// </summary>
        private static bool _debugFullVaultInspector;
        private bool _wasInMenuScene = true;
        private bool _applicationQuitting;

        // Separate persistent object that survives game's UIHandler.UnloadGame cleanup
        private static GameObject _persistentRunner;
        private static PersistentUpdateRunner _updateRunner;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigFile = CreateNamedConfig();
            SunhavenMods.Shared.ConfigFileHelper.ReplacePluginConfig(this, ConfigFile, Log.LogWarning);

            // NOTE: DontDestroyOnLoad on this gameObject doesn't help because
            // the game's UIHandler.UnloadGame explicitly destroys UI objects.
            // We use a separate hidden persistent runner instead.

            Log.LogInfo($"Loading {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}");
            VaultSaveCompatibility.WarnIfBreakingRelease(Log);

            // Create a hidden persistent runner that survives the game's cleanup
            CreatePersistentRunner();

            try
            {
                // Initialize configuration
                InitializeConfig();
                SubscribeConfigChanged();
                TryApplyDeckLayoutDefaults();
                TryApplyHighResLayoutDefaults();

                // Initialize vault system
                // Store in both instance and static fields so they survive Plugin destruction
                _vaultManager = new VaultManager();
                _saveSystem = new VaultSaveSystem(_vaultManager);
                _staticVaultManager = _vaultManager;
                _staticSaveSystem = _saveSystem;
                _saveSystem.SetAutoSaveIntervalSeconds(Mathf.Max(10f, _autoSaveInterval.Value));
                _vaultManager.OnVaultLoaded += OnVaultDataLoaded;

                // Create UI GameObject (RectTransform stretched to full screen for uGUI Canvas)
                var uiObject = CreateTheVaultUiHost();
                DontDestroyOnLoad(uiObject);
                _vaultUI = uiObject.AddComponent<VaultUI>();
                _vaultUI.Initialize(_vaultManager);
                _vaultUI.SetScale(GetValidatedWindowScale());
                _vaultUI.SetToggleKey(_toggleKey.Value, _requireCtrlModifier.Value);
                _vaultUI.SetAltToggleKey(_altToggleKey.Value);
                _staticVaultUI = _vaultUI;

                // Store config values for PersistentRunner hotkey detection
                StaticToggleKey = _toggleKey.Value;
                StaticRequireCtrl = _requireCtrlModifier.Value;
                StaticAltToggleKey = _altToggleKey.Value;
                StaticControllerOpenEnabled = _controllerOpenEnabled.Value;
                StaticControllerOpenModifier = _controllerOpenModifier.Value;
                StaticControllerOpenButton = _controllerOpenButton.Value;
                StaticHUDToggleKey = _hudToggleKey.Value;
                StaticQuickConvertKey = _quickConvertKey.Value;

                // Create HUD for persistent display
                _vaultHUD = uiObject.AddComponent<VaultHUD>();
                _vaultHUD.Initialize(_vaultManager);
                _vaultHUD.SetEnabled(_enableHUD.Value);
                ApplyVaultHudPlacementFromConfig(_vaultHUD);
                WireVaultHudPositionPersistence(_vaultHUD);
                _vaultHUD.SetScale(Mathf.Clamp(_hudScale.Value, 0.5f, 3.0f));
                _vaultHUD.SetHudDensity(GetResolvedHudDensity());
                _staticVaultHUD = _vaultHUD;

                VaultModApiBridge.Instance = new VaultModApiAdapter();

                // Initialize icon cache for UI icons
                SunhavenMods.Shared.IconCache.Initialize(Log);
                RegisterIconCacheCurrencies();

                // Register item-to-currency mappings for deposit/withdraw
                RegisterItemMappings();

                // Apply Harmony patches
                _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                LocalizationBootstrap.BindForceEnglish(ConfigFile);
                LocalizationBootstrap.Init(PluginInfo.PLUGIN_GUID, _harmony, Log);
                ApplyPatches();

                // Patch GameSave class for character loading detection
                PatchGameSave();

                // Subscribe to scene loading as a backup trigger for vault loading
                // This is more reliable than patching game-specific methods that may not exist
                SceneManager.sceneLoaded += OnSceneLoaded;
                Log.LogInfo("Subscribed to SceneManager.sceneLoaded for vault loading");

                // Check for updates
                if (_checkForUpdates.Value)
                {
                    VersionChecker.CheckForUpdate(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_VERSION, Log,
                        result => result.NotifyUpdateAvailable(Log));
                }

                Log.LogInfo($"{PluginInfo.PLUGIN_NAME} loaded successfully!");
                ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                    ModHealthIntegrationSummary.Build(
                        ("DevTools", SuitePluginGuids.DevTools),
                        ("Almanac", SuitePluginGuids.HavensAlmanac)));
                Log.LogInfo(
                    $"Press {(_requireCtrlModifier.Value ? "Ctrl+" : "")}{_toggleKey.Value}, {_altToggleKey.Value}, or controller LB+Y (JoystickButton4+JoystickButton3) to open the vault");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to load {PluginInfo.PLUGIN_NAME}: {ex}");
            }
        }

        /// <summary>
        /// Creates a hidden GameObject that persists across scene loads AND survives
        /// the game's UIHandler.UnloadGame cleanup that destroys regular GameObjects.
        /// </summary>
        private void CreatePersistentRunner()
        {
            if (_persistentRunner != null)
            {
                Log.LogInfo("PersistentRunner already exists");
                return;
            }

            // Create a new hidden GameObject
            _persistentRunner = new GameObject("TheVault_PersistentRunner");

            // Mark it to survive scene changes
            DontDestroyOnLoad(_persistentRunner);

            // Hide it from the game's cleanup routines and hierarchy
            _persistentRunner.hideFlags = HideFlags.HideAndDontSave;

            SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);

            // Add the update runner component
            _updateRunner = _persistentRunner.AddComponent<PersistentUpdateRunner>();

            Log.LogInfo("Created hidden PersistentRunner that survives game cleanup");
        }

        private static ConfigFile CreateNamedConfig()
        {
            return ConfigFileHelper.CreateNamedConfig(
                PluginInfo.PLUGIN_GUID,
                "TheVault.cfg",
                message => Log?.LogWarning(message)
            );
        }

        /// <summary>
        /// uGUI needs a <see cref="RectTransform"/> that spans the overlay; a plain Transform clips to a tiny default rect.
        /// </summary>
        private static GameObject CreateTheVaultUiHost()
        {
            var go = new GameObject("TheVault_UI", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            return go;
        }

        /// <summary>
        /// Ensures UI components exist and recreates them if they were destroyed by the game's cleanup.
        /// Called from PlayerPatches when a character loads.
        /// </summary>
        public static void EnsureUIComponentsExist()
        {
            try
            {
                // Check if PersistentRunner was destroyed and recreate it
                if (_persistentRunner == null || _updateRunner == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating PersistentRunner...");
                    _persistentRunner = new GameObject("TheVault_PersistentRunner");
                    UnityEngine.Object.DontDestroyOnLoad(_persistentRunner);
                    _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
                    SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
                    _updateRunner = _persistentRunner.AddComponent<PersistentUpdateRunner>();
                    Log?.LogInfo("[EnsureUI] PersistentRunner recreated");
                }

                // Check if VaultUI was destroyed and recreate it
                if (_staticVaultUI == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating VaultUI...");
                    var uiObject = CreateTheVaultUiHost();
                    UnityEngine.Object.DontDestroyOnLoad(uiObject);
                    // NOTE: Do NOT use HideFlags.HideAndDontSave on VaultUI!
                    // That flag prevents Unity's OnGUI from being called, which breaks the UI rendering.
                    // Only PersistentRunner needs HideFlags (it only uses Update, not OnGUI).

                    _staticVaultUI = uiObject.AddComponent<VaultUI>();
                    _staticVaultUI.Initialize(_staticVaultManager);
                    float windowScale = Instance != null ? Instance.GetValidatedWindowScale() : 1f;
                    _staticVaultUI.SetScale(windowScale);
                    _staticVaultUI.SetToggleKey(StaticToggleKey, StaticRequireCtrl);
                    _staticVaultUI.SetAltToggleKey(StaticAltToggleKey);

                    _staticVaultHUD = uiObject.AddComponent<VaultHUD>();
                    _staticVaultHUD.Initialize(_staticVaultManager);
                    if (Instance != null)
                    {
                        Instance._vaultUI = _staticVaultUI;
                        // uGUI disabled
                        Instance._vaultHUD = _staticVaultHUD;
                        _staticVaultHUD.SetEnabled(Instance._enableHUD.Value);
                        Instance.ApplyVaultHudPlacementFromConfig(_staticVaultHUD);
                        Instance.WireVaultHudPositionPersistence(_staticVaultHUD);
                        _staticVaultHUD.SetScale(Mathf.Clamp(Instance._hudScale.Value, 0.5f, 3.0f));
                        _staticVaultHUD.SetHudDensity(GetResolvedHudDensity());
                    }

                    Log?.LogInfo("[EnsureUI] VaultUI and VaultHUD recreated");
                }
                else if (_staticVaultHUD == null && _staticVaultUI != null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating VaultHUD...");
                    _staticVaultHUD = _staticVaultUI.gameObject.AddComponent<VaultHUD>();
                    _staticVaultHUD.Initialize(_staticVaultManager);
                    if (Instance != null)
                    {
                        Instance._vaultHUD = _staticVaultHUD;
                        _staticVaultHUD.SetEnabled(Instance._enableHUD.Value);
                        Instance.ApplyVaultHudPlacementFromConfig(_staticVaultHUD);
                        Instance.WireVaultHudPositionPersistence(_staticVaultHUD);
                        _staticVaultHUD.SetScale(Mathf.Clamp(Instance._hudScale.Value, 0.5f, 3.0f));
                        _staticVaultHUD.SetHudDensity(GetResolvedHudDensity());
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnsureUI] Error recreating UI: {ex.Message}");
            }
        }

        private void InitializeConfig()
        {
            _toggleKey = ConfigFile.Bind(
                "UI",
                "ToggleKey",
                KeyCode.V,
                "Key to toggle the vault UI"
            );

            _requireCtrlModifier = ConfigFile.Bind(
                "UI",
                "RequireCtrlModifier",
                true,
                "Require Ctrl key to be held when pressing toggle key"
            );

            _altToggleKey = ConfigFile.Bind(
                "UI",
                "AltToggleKey",
                KeyCode.F8,
                "Alternative key to toggle vault UI (no modifier required). Useful for Steam Deck / Steam Input keyboard layers (F8 still works)."
            );

            _controllerOpenEnabled = ConfigFile.Bind(
                "UI",
                "ControllerOpenEnabled",
                true,
                "Enable controller combo to open/close the vault (Steam Deck / Xbox layout). Default combo: hold LB + press Y."
            );

            _controllerOpenModifier = ConfigFile.Bind(
                "UI",
                "ControllerOpenModifier",
                KeyCode.JoystickButton4,
                "Controller button held for vault open (Unity Xbox layout: JoystickButton4 = LB). JoystickButton* aggregates all joysticks."
            );

            _controllerOpenButton = ConfigFile.Bind(
                "UI",
                "ControllerOpenButton",
                KeyCode.JoystickButton3,
                "Controller button pressed while modifier is held to toggle vault (Unity Xbox layout: JoystickButton3 = Y)."
            );

            _enableHUD = ConfigFile.Bind(
                "HUD",
                "EnableHUD",
                true,
                "Show a persistent HUD bar displaying vault currency totals"
            );

            _hudPosition = ConfigFile.Bind(
                "HUD",
                "Position",
                "TopLeft",
                "Anchor when not using a custom drag position. Changing this clears PositionX/PositionY. Drag the HUD top strip to save a custom position (like Sun Haven Todo)."
            );

            _hudPositionX = ConfigFile.Bind(
                "HUD",
                "PositionX",
                -1f,
                "HUD left edge in pixels after dragging (-1 = use Position anchor only)"
            );

            _hudPositionY = ConfigFile.Bind(
                "HUD",
                "PositionY",
                -1f,
                "HUD top edge in pixels after dragging (-1 = use Position anchor only)"
            );

            _hudScale = ConfigFile.Bind(
                "HUD",
                "Scale",
                1.25f,
                new BepInEx.Configuration.ConfigDescription(
                    "Scale factor for the HUD bar (1.0 = smaller, 1.25 = new default, 2.0 = very large). Use a dot as decimal separator in the config file (e.g. 1.25).",
                    new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 3.0f))
            );

            _hudCompactMode = ConfigFile.Bind(
                "HUD",
                "CompactMode",
                false,
                "Legacy: when Density is Normal and this is true, HUD uses Compact spacing. Prefer [HUD] Density."
            );

            _hudDensity = ConfigFile.Bind(
                "HUD",
                "Density",
                "Normal",
                new ConfigDescription(
                    "HUD bar density: Normal, Compact, or Minimal.",
                    new AcceptableValueList<string>(new[] { "Normal", "Compact", "Minimal" }))
            );

            _windowScale = ConfigFile.Bind(
                "Display",
                "WindowScale",
                1.0f,
                new BepInEx.Configuration.ConfigDescription(
                    "Scale factor for the main Vault window (1.0 = default, 1.5 = 50% larger). Multiplied by resolution factor when ScaleWithResolution is on.",
                    new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 3.0f)
                )
            );

            _scaleWithResolution = ConfigFile.Bind(
                "Display",
                "ScaleWithResolution",
                true,
                "When true, WindowScale is multiplied by Screen.height/1080 so 1440p/ultrawide/4K stays readable without huge manual scale values (AZR-359)."
            );

            _autoDeckLayout = ConfigFile.Bind(
                "Display",
                "AutoDeckLayout",
                true,
                "Apply larger UI scale once on Deck-like resolutions (1280×800 / 1280×720 or similar 16:10). Set false to never auto-bump WindowScale / HUD Scale."
            );

            _deckLayoutApplied = ConfigFile.Bind(
                "Display",
                "DeckLayoutApplied",
                false,
                "Internal flag: set true after AutoDeckLayout has applied once so we do not overwrite user prefs on later launches. Reset to false to re-apply."
            );

            _autoHighResLayout = ConfigFile.Bind(
                "Display",
                "AutoHighResLayout",
                true,
                "Once on high-res / ultrawide (height ≥1440 or width ≥2560), bump WindowScale to at least 1.5 if still near default (AZR-359)."
            );

            _highResLayoutApplied = ConfigFile.Bind(
                "Display",
                "HighResLayoutApplied",
                false,
                "Internal flag: set true after AutoHighResLayout has applied once. Reset to false to re-apply."
            );

            _hudToggleKey = ConfigFile.Bind(
                "HUD",
                "ToggleKey",
                KeyCode.F7,
                "Key to toggle the HUD display on/off"
            );

            _quickConvertKey = ConfigFile.Bind(
                "Hotkeys",
                "QuickConvertKey",
                KeyCode.F6,
                "Key to trigger quick token/key conversion using [QuickConvert] ExchangeTable"
            );

            _quickConvertTable = ConfigFile.Bind(
                "QuickConvert",
                "ExchangeTable",
                "seasonal_Winter->key_copper:10:1;key_copper->seasonal_Winter:1:10",
                "Conversion rules: from->to:fromAmount:toAmount;from->to:... (example seasonal_Winter->key_copper:10:1)"
            );

            _enableAutoSave = ConfigFile.Bind(
                "Saving",
                "EnableAutoSave",
                true,
                "Automatically save vault data periodically"
            );

            _autoSaveInterval = ConfigFile.Bind(
                "Saving",
                "AutoSaveInterval",
                300f,
                "Auto-save interval in seconds (default: 5 minutes)"
            );

            _checkForUpdates = ConfigFile.Bind(
                "Updates",
                "CheckForUpdates",
                true,
                "Check for mod updates on startup"
            );

            _hudPositionConfigBaseline = _hudPosition.Value;
        }

        /// <summary>
        /// Subscribe to config changes so that when the user edits config in-game (e.g. ConfigurationManager or our Settings panel),
        /// we immediately update static state and UI.
        /// </summary>
        private void SubscribeConfigChanged()
        {
            if (ConfigFile != null)
                ConfigFile.SettingChanged += OnAnyConfigSettingChanged;
        }

        private void OnAnyConfigSettingChanged(object sender, SettingChangedEventArgs args)
        {
            try
            {
                // Live-apply only our own entries to avoid unrelated plugin config noise.
                if (args?.ChangedSetting == null || args.ChangedSetting.ConfigFile != ConfigFile)
                    return;
                ApplyConfigToState();
            }
            catch (Exception ex)
            {
                Log?.LogError($"[The Vault] Config change handler failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply current config values to static state and UI (no file reload).
        /// </summary>
        private void ApplyConfigToState()
        {
            try
            {
                float validatedWindowScale = GetValidatedWindowScale();
                StaticToggleKey = _toggleKey.Value;
                StaticRequireCtrl = _requireCtrlModifier.Value;
                StaticAltToggleKey = _altToggleKey.Value;
                StaticControllerOpenEnabled = _controllerOpenEnabled.Value;
                StaticControllerOpenModifier = _controllerOpenModifier.Value;
                StaticControllerOpenButton = _controllerOpenButton.Value;
                StaticHUDToggleKey = _hudToggleKey.Value;
                StaticQuickConvertKey = _quickConvertKey.Value;
                _staticSaveSystem?.SetAutoSaveIntervalSeconds(Mathf.Max(10f, _autoSaveInterval.Value));

                var vaultUI = GetVaultUI();
                if (vaultUI != null)
                {
                    vaultUI.SetScale(validatedWindowScale);
                    vaultUI.SetToggleKey(StaticToggleKey, StaticRequireCtrl);
                    vaultUI.SetAltToggleKey(StaticAltToggleKey);
                }

                var vaultHUD = GetVaultHUD();
                if (vaultHUD != null)
                {
                    vaultHUD.SetEnabled(_enableHUD.Value);
                    var posVal = _hudPosition.Value;
                    if (!string.Equals(posVal, _hudPositionConfigBaseline, StringComparison.OrdinalIgnoreCase))
                    {
                        _hudPositionX.SetSerializedValue("-1");
                        _hudPositionY.SetSerializedValue("-1");
                        vaultHUD.ClearCustomHudPlacement();
                    }

                    _hudPositionConfigBaseline = posVal;
                    vaultHUD.SetPosition(ParseHUDPosition(posVal));
                    if (_hudPositionX.Value >= 0f && _hudPositionY.Value >= 0f)
                        vaultHUD.RestoreHudPixelPosition(_hudPositionX.Value, _hudPositionY.Value);

                    vaultHUD.SetScale(Mathf.Clamp(_hudScale.Value, 0.5f, 3.0f));
                    vaultHUD.SetHudDensity(GetResolvedHudDensity());
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[The Vault] ApplyConfigToState failed: {ex.Message}");
            }
        }

        /// <summary>Apply window scale to the vault UI.</summary>
        public void ApplyVaultWindowScaleToUi()
        {
            float s = GetValidatedWindowScale();
            _staticVaultUI?.SetScale(s);
        }

        // --- Config getters/setters for in-game Settings UI (values persist to config file) ---
        public static KeyCode GetConfigToggleKey() => Instance?._toggleKey?.Value ?? KeyCode.V;
        public static void SetConfigToggleKey(KeyCode k) { if (Instance?._toggleKey != null) Instance._toggleKey.Value = k; }
        public static bool GetConfigRequireCtrl() => Instance?._requireCtrlModifier?.Value ?? true;
        public static void SetConfigRequireCtrl(bool v) { if (Instance?._requireCtrlModifier != null) Instance._requireCtrlModifier.Value = v; }
        public static KeyCode GetConfigAltToggleKey() => Instance?._altToggleKey?.Value ?? KeyCode.F8;
        public static void SetConfigAltToggleKey(KeyCode k) { if (Instance?._altToggleKey != null) Instance._altToggleKey.Value = k; }
        public static bool GetConfigControllerOpenEnabled() => Instance?._controllerOpenEnabled?.Value ?? true;
        public static void SetConfigControllerOpenEnabled(bool v) { if (Instance?._controllerOpenEnabled != null) Instance._controllerOpenEnabled.Value = v; }
        public static KeyCode GetConfigControllerOpenModifier() => Instance?._controllerOpenModifier?.Value ?? KeyCode.JoystickButton4;
        public static void SetConfigControllerOpenModifier(KeyCode k) { if (Instance?._controllerOpenModifier != null) Instance._controllerOpenModifier.Value = k; }
        public static KeyCode GetConfigControllerOpenButton() => Instance?._controllerOpenButton?.Value ?? KeyCode.JoystickButton3;
        public static void SetConfigControllerOpenButton(KeyCode k) { if (Instance?._controllerOpenButton != null) Instance._controllerOpenButton.Value = k; }
        public static bool GetConfigHUDEnabled() => Instance?._enableHUD?.Value ?? true;
        public static void SetConfigHUDEnabled(bool v) { if (Instance?._enableHUD != null) Instance._enableHUD.Value = v; }
        public static float GetConfigHUDScale() => Instance?._hudScale?.Value ?? 1f;
        public static void SetConfigHUDScale(float v) { if (Instance?._hudScale != null) Instance._hudScale.Value = Mathf.Clamp(v, 0.5f, 3.0f); }
        public static bool GetConfigHUDCompactMode() => Instance?._hudCompactMode?.Value ?? false;
        public static void SetConfigHUDCompactMode(bool v) { if (Instance?._hudCompactMode != null) Instance._hudCompactMode.Value = v; }

        public static string GetConfigHudDensity() => Instance?._hudDensity?.Value ?? "Normal";
        public static void SetConfigHudDensity(string v)
        {
            if (Instance?._hudDensity == null) return;
            if (string.IsNullOrWhiteSpace(v)) v = "Normal";
            Instance._hudDensity.Value = v;
        }

        /// <summary>
        /// Effective HUD density: <see cref="_hudDensity"/>, with legacy <see cref="_hudCompactMode"/> when density is Normal.
        /// </summary>
        public static VaultHudDensity GetResolvedHudDensity()
        {
            if (Instance == null) return VaultHudDensity.Normal;
            string raw = Instance._hudDensity?.Value?.Trim() ?? "Normal";
            if (string.Equals(raw, "Minimal", StringComparison.OrdinalIgnoreCase))
                return VaultHudDensity.Minimal;
            if (string.Equals(raw, "Compact", StringComparison.OrdinalIgnoreCase))
                return VaultHudDensity.Compact;
            if (Instance._hudCompactMode != null && Instance._hudCompactMode.Value)
                return VaultHudDensity.Compact;
            return VaultHudDensity.Normal;
        }
        public static float GetConfigWindowScale() => Instance?.GetValidatedWindowScale() ?? 1f;
        public static float GetConfigWindowScaleRaw() => Instance?._windowScale != null
            ? Instance.ClampWindowScaleValue(Instance._windowScale.Value)
            : 1f;
        public static bool GetConfigScaleWithResolution() => Instance?._scaleWithResolution?.Value ?? true;
        public static void SetConfigScaleWithResolution(bool v)
        {
            if (Instance?._scaleWithResolution != null)
                Instance._scaleWithResolution.Value = v;
        }
        public static void SetConfigWindowScale(float v)
        {
            if (Instance?._windowScale != null)
                Instance._windowScale.Value = Instance.ClampWindowScaleValue(v);
        }

        private float GetValidatedWindowScale()
        {
            if (_windowScale == null)
                return 1f;

            float clamped = ClampWindowScaleValue(_windowScale.Value);
            if (!Mathf.Approximately(_windowScale.Value, clamped))
                _windowScale.Value = clamped;
            float factor = GetResolutionScaleFactor();
            return ClampWindowScaleValue(clamped * factor);
        }

        private float GetResolutionScaleFactor()
        {
            if (_scaleWithResolution == null || !_scaleWithResolution.Value)
                return 1f;
            if (Screen.height <= 0)
                return 1f;
            // 1080p → 1.0, 1440p → ~1.33, 2160p → 2.0 (clamped).
            return Mathf.Clamp(Screen.height / 1080f, 1f, 2f);
        }

        private float ClampWindowScaleValue(float value)
        {
            return Mathf.Clamp(value, MinWindowScale, MaxWindowScale);
        }

        internal static void TryApplyHighResLayoutDefaults()
        {
            Instance?.ApplyHighResLayoutDefaultsIfNeeded();
        }

        private void ApplyHighResLayoutDefaultsIfNeeded()
        {
            try
            {
                if (_autoHighResLayout == null || _highResLayoutApplied == null || _windowScale == null)
                    return;
                if (!_autoHighResLayout.Value || _highResLayoutApplied.Value)
                    return;
                if (Screen.width <= 0 || Screen.height <= 0)
                    return;
                if (!LooksLikeHighResOrUltrawide())
                    return;

                const float highResFloor = 1.5f;
                float win = ClampWindowScaleValue(_windowScale.Value);
                // Only bump when still near factory default so we don't overwrite intentional prefs.
                if (win <= 1.05f)
                    _windowScale.Value = ClampWindowScaleValue(highResFloor);

                if (_hudScale != null && _hudScale.Value <= 1.26f)
                    _hudScale.Value = Mathf.Clamp(Mathf.Max(_hudScale.Value, highResFloor), 0.5f, 3.0f);

                _highResLayoutApplied.Value = true;
                ApplyConfigToState();
                Log?.LogInfo(
                    $"[The Vault] Auto high-res layout applied (WindowScale={_windowScale.Value:F2}, HUD Scale={_hudScale?.Value:F2}, {Screen.width}x{Screen.height})");
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[The Vault] Auto high-res layout failed: {ex.Message}");
            }
        }

        private static bool LooksLikeHighResOrUltrawide()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (h >= 1440)
                return true;
            if (w >= 2560 && h >= 1080)
                return true;
            if (h > 0 && (float)w / h >= 2.0f && w >= 2560)
                return true;
            return false;
        }

        /// <summary>
        /// Once per install (while AutoDeckLayout and !DeckLayoutApplied): raise WindowScale / HUD
        /// Scale to at least 1.35 on Deck-like resolutions. Safe to call repeatedly; no-ops after applied
        /// or when Screen size is not ready yet.
        /// </summary>
        internal static void TryApplyDeckLayoutDefaults()
        {
            Instance?.ApplyDeckLayoutDefaultsIfNeeded();
        }

        private void ApplyDeckLayoutDefaultsIfNeeded()
        {
            try
            {
                if (_autoDeckLayout == null || _deckLayoutApplied == null || _windowScale == null || _hudScale == null)
                    return;
                if (!_autoDeckLayout.Value || _deckLayoutApplied.Value)
                    return;
                if (Screen.width <= 0 || Screen.height <= 0)
                    return;
                if (!LooksLikeDeckResolution())
                    return;

                const float deckScale = 1.35f;
                float win = ClampWindowScaleValue(_windowScale.Value);
                if (win < deckScale)
                    _windowScale.Value = ClampWindowScaleValue(deckScale);

                // Only bump HUD when still near factory default (1.25) or below.
                float hud = _hudScale.Value;
                if (hud <= 1.26f)
                    _hudScale.Value = Mathf.Clamp(Mathf.Max(hud, deckScale), 0.5f, 3.0f);

                _deckLayoutApplied.Value = true;
                ApplyConfigToState();
                Log?.LogInfo(
                    $"[The Vault] Auto Deck layout applied (WindowScale={_windowScale.Value:F2}, HUD Scale={_hudScale.Value:F2}, {Screen.width}x{Screen.height})");
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[The Vault] Auto Deck layout failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Steam Deck-like resolutions:
        /// - Native Deck LCD / OLED Game Mode: 1280×800
        /// - Docked / external 720p: 1280×720
        /// - Other small 16:10-ish panels: width ≤1366 and height ≤800 with aspect ≈16:10 (1.5–1.7)
        /// </summary>
        private static bool LooksLikeDeckResolution()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w == 1280 && (h == 720 || h == 800))
                return true;
            if (w <= 1366 && h <= 800 && h > 0)
            {
                float aspect = (float)w / h;
                if (aspect >= 1.5f && aspect <= 1.7f)
                    return true;
            }
            return false;
        }

        public static bool GetConfigDebugFullVaultInspector() => _debugFullVaultInspector;

        /// <summary>
        /// Sets full vault inspector mode (intended for Haven Dev Tools; runtime only, no longer written to TheVault.cfg).
        /// </summary>
        public static void SetConfigDebugFullVaultInspector(bool v) => _debugFullVaultInspector = v;

        /// <summary>
        /// Register currency-to-item mappings for IconCache (used by VaultUI/VaultHUD).
        /// </summary>
        private void RegisterIconCacheCurrencies()
        {
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.SeasonalSpring, ItemIds.SpringToken);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.SeasonalSummer, ItemIds.SummerToken);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.SeasonalFall, ItemIds.FallToken);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.SeasonalWinter, ItemIds.WinterToken);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.KeyCopper, ItemIds.CopperKey);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.KeyIron, ItemIds.IronKey);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.KeyAdamant, ItemIds.AdamantKey);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.KeyMithril, ItemIds.MithrilKey);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.KeySunite, ItemIds.SuniteKey);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.KeyGlorite, ItemIds.GloriteKey);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.KeyKingsLostMine, ItemIds.KingsLostMineKey);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.SpecialCommunityToken, ItemIds.CommunityToken);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.SpecialDoubloon, ItemIds.Doubloon);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.SpecialBlackBottleCap, ItemIds.BlackBottleCap);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.SpecialRedCarnivalTicket, ItemIds.RedCarnivalTicket);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.SpecialCandyCornPieces, ItemIds.CandyCornPieces);
            SunhavenMods.Shared.IconCache.RegisterCurrency(VaultCurrencyIds.SpecialManaShard, ItemIds.ManaShard);
        }

        /// <summary>
        /// Register mappings between Sun Haven item IDs and vault currency IDs.
        /// Auto-deposit is enabled so items are automatically converted to vault currency when picked up.
        /// Item IDs are defined in ItemIds.cs for maintainability.
        /// </summary>
        private void RegisterItemMappings()
        {
            // Enable auto-deposit globally
            ItemPatches.AutoDepositEnabled = true;

            // Seasonal Tokens - auto-deposit enabled
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.SpringToken, "seasonal_Spring", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.SummerToken, "seasonal_Summer", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.WinterToken, "seasonal_Winter", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.FallToken, "seasonal_Fall", autoDeposit: true);

            // Keys - auto-deposit enabled
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.CopperKey, "key_copper", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.IronKey, "key_iron", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.AdamantKey, "key_adamant", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.MithrilKey, "key_mithril", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.SuniteKey, "key_sunite", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.GloriteKey, "key_glorite", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.KingsLostMineKey, "key_kingslostmine", autoDeposit: true);

            // Special currencies - auto-deposit enabled
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.CommunityToken, "special_communitytoken", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.Doubloon, "special_doubloon", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.BlackBottleCap, "special_blackbottlecap", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.RedCarnivalTicket, "special_redcarnivalticket", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.CandyCornPieces, "special_candycornpieces", autoDeposit: true);
            ItemPatches.RegisterItemCurrencyMapping(ItemIds.ManaShard, "special_manashard", autoDeposit: true);

            // Eagerly build pickup hot-path cache so first pickup doesn't lag
            ItemPatches.InitializePickupCache();

            Log.LogInfo("Registered item-to-currency mappings with auto-deposit enabled");
        }

        private void ApplyPatches()
        {
            try
            {
                // Patch player initialization for loading vault data
                var playerType = typeof(Player);

                PatchMethod(playerType, "InitializeAsOwner",
                    typeof(PlayerPatches), "OnPlayerInitialized");

                // Patch save/load for vault persistence (game uses GameSave.SaveGame / LoadGame, not SaveLoadManager)
                PatchGameSaveSaveLoad();

                // Patch return to menu for state reset (MainMenuController.HomeMenu when entering main menu)
                PatchMethod(typeof(MainMenuController), "HomeMenu",
                    typeof(SaveLoadPatches), "OnReturnToMenu");

                var titleType = AccessTools.TypeByName("Wish.TitleScreen");
                if (titleType != null)
                {
                    PatchMethod(titleType, "Start",
                        typeof(SaveLoadPatches), "OnReturnToMenu");
                }

                // Patch inventory AddItem for auto-deposit (GetAmount/HasEnough/RemoveItem too)
                PatchItemPickup();

                // Block inventory clicks that pass through the IMGUI vault window (AZR-438).
                VaultPointerGuard.Apply(_harmony);

                // Log results
                var patchedMethods = _harmony.GetPatchedMethods();
                int count = 0;
                foreach (var method in patchedMethods)
                {
                    Log.LogInfo($"Patched: {method.DeclaringType?.Name}.{method.Name}");
                    count++;
                }
                Log.LogInfo($"Total methods patched: {count}");
            }
            catch (Exception ex)
            {
                Log.LogError($"Harmony patching failed: {ex}");
            }
        }

        private void PatchMethod(Type targetType, string methodName, Type patchType, string patchMethodName, Type[] parameters = null)
        {
            try
            {
                var original = parameters == null
                    ? AccessTools.Method(targetType, methodName)
                    : AccessTools.Method(targetType, methodName, parameters);

                if (original == null)
                {
                    Log.LogWarning($"Could not find method {targetType.Name}.{methodName}. Available methods: {DescribeAvailableMethods(targetType)}");
                    return;
                }

                var postfix = AccessTools.Method(patchType, patchMethodName);
                if (postfix == null)
                {
                    Log.LogWarning($"Could not find patch method {patchType.Name}.{patchMethodName}");
                    return;
                }

                _harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                Log.LogInfo($"Successfully patched {targetType.Name}.{methodName}");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to patch {targetType.Name}.{methodName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch GameSave for character load detection (LoadCharacter). Save/Load triggers are in PatchGameSaveSaveLoad.
        /// </summary>
        private void PatchGameSave()
        {
            try
            {
                var gameSaveType = typeof(GameSave);
                var postfix = AccessTools.Method(typeof(GameSavePatches), "OnLoadCharacterAny");
                if (postfix == null)
                {
                    Log.LogWarning("Could not find GameSavePatches.OnLoadCharacterAny");
                    return;
                }

                var loadCharacterMethods = AccessTools.GetDeclaredMethods(gameSaveType)
                    .Where(m => string.Equals(m.Name, "LoadCharacter", StringComparison.Ordinal))
                    .ToList();

                if (loadCharacterMethods.Count == 0)
                {
                    Log.LogWarning("No GameSave.LoadCharacter overloads found to patch");
                    return;
                }

                foreach (var method in loadCharacterMethods)
                {
                    _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                    var signature = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                    Log.LogInfo($"Patched GameSave.LoadCharacter({signature})");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error patching GameSave: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch GameSave.SaveGame and LoadGame for vault persistence (game has no SaveLoadManager).
        /// </summary>
        private void PatchGameSaveSaveLoad()
        {
            try
            {
                var gameSaveType = typeof(GameSave);
                var saveGameMethod = AccessTools.Method(gameSaveType, "SaveGame");
                if (saveGameMethod != null)
                {
                    var postfix = AccessTools.Method(typeof(SaveLoadPatches), "OnGameSaved");
                    if (postfix != null)
                    {
                        _harmony.Patch(saveGameMethod, postfix: new HarmonyMethod(postfix));
                        Log.LogInfo("Patched GameSave.SaveGame");
                    }
                }

            }
            catch (Exception ex)
            {
                Log.LogError($"Error patching GameSave save/load: {ex.Message}");
            }
        }

        private void PatchItemPickup()
        {
            var inventoryType = typeof(Inventory);
            var invMethods = inventoryType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var itemType = typeof(Item);

            var addItemMethod = AccessTools.Method(inventoryType, "AddItem",
                new[] { itemType, typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool) });

            if (addItemMethod != null)
            {
                var prefix = AccessTools.Method(typeof(ItemPatches), "OnInventoryAddItemObjectPrefix");
                var postfix = AccessTools.Method(typeof(ItemPatches), "OnInventoryAddItemObjectPostfix");
                if (prefix != null)
                {
                    _harmony.Patch(addItemMethod,
                        prefix: new HarmonyMethod(prefix),
                        postfix: postfix != null ? new HarmonyMethod(postfix) : null);
                    Log.LogInfo($"Patched {inventoryType.Name}.AddItem(Item,...) for auto-deposit");
                }
                else if (postfix != null)
                {
                    _harmony.Patch(addItemMethod, postfix: new HarmonyMethod(postfix));
                    Log.LogInfo($"Patched {inventoryType.Name}.AddItem(Item,...) postfix only");
                }
            }
            else
            {
                Log.LogWarning("Could not find AddItem(Item,int,int,bool,bool,bool)");
                foreach (var m in invMethods)
                {
                    if (m.Name != "AddItem") continue;
                    var parameters = m.GetParameters();
                    if (parameters.Length > 0 && parameters[0].ParameterType == itemType)
                    {
                        var prefix = AccessTools.Method(typeof(ItemPatches), "OnInventoryAddItemObjectPrefix");
                        var postfix = AccessTools.Method(typeof(ItemPatches), "OnInventoryAddItemObjectPostfix");
                        if (prefix != null)
                        {
                            _harmony.Patch(m,
                                prefix: new HarmonyMethod(prefix),
                                postfix: postfix != null ? new HarmonyMethod(postfix) : null);
                            Log.LogInfo($"Patched {inventoryType.Name}.{m.Name} for auto-deposit");
                            break;
                        }
                        if (postfix != null)
                        {
                            _harmony.Patch(m, postfix: new HarmonyMethod(postfix));
                            Log.LogInfo($"Patched {inventoryType.Name}.{m.Name} postfix only");
                            break;
                        }
                    }
                }
            }

            // 3.1.x: AddItem(int, int, bool). Skip the (int, int) AccessTools probe — it logs a HarmonyX miss.
            var addItemIntMethod = inventoryType.GetMethod("AddItem",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(int), typeof(int), typeof(bool) }, null)
                ?? inventoryType.GetMethod("AddItem",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(int), typeof(int) }, null);
            if (addItemIntMethod != null)
            {
                var postfix = AccessTools.Method(typeof(ItemPatches), "OnInventoryAddItem");
                if (postfix != null)
                {
                    _harmony.Patch(addItemIntMethod, postfix: new HarmonyMethod(postfix));
                    Log.LogInfo($"Successfully patched {inventoryType.Name}.AddItem for auto-deposit");
                }
            }

            // Patch GetAmount to include vault amounts - makes shops see vault currency
            var getAmountMethod = AccessTools.Method(inventoryType, "GetAmount", new[] { typeof(int) });
            if (getAmountMethod != null)
            {
                var postfix = AccessTools.Method(typeof(ItemPatches), "OnInventoryGetAmount");
                if (postfix != null)
                {
                    _harmony.Patch(getAmountMethod, postfix: new HarmonyMethod(postfix));
                    Log.LogInfo($"Successfully patched {inventoryType.Name}.GetAmount for vault integration");
                }
            }
            else
            {
                Log.LogWarning("Could not find Inventory.GetAmount method");
            }

            // Patch HasEnough to check vault - shops and any bag-based cost that uses HasEnough see vault balance
            var hasEnoughMethod = AccessTools.Method(inventoryType, "HasEnough", new[] { typeof(int), typeof(int) });
            if (hasEnoughMethod != null)
            {
                var postfix = AccessTools.Method(typeof(ItemPatches), "OnInventoryHasEnough");
                if (postfix != null)
                {
                    _harmony.Patch(hasEnoughMethod, postfix: new HarmonyMethod(postfix));
                    Log.LogInfo($"Successfully patched {inventoryType.Name}.HasEnough for vault integration");
                }
            }
            else
            {
                Log.LogWarning("Could not find Inventory.HasEnough method");
            }

            // Patch RemoveItem to deduct from vault when inventory is insufficient
            var removeItemMethod = AccessTools.Method(inventoryType, "RemoveItem", new[] { typeof(int), typeof(int), typeof(int) });
            if (removeItemMethod != null)
            {
                var prefix = AccessTools.Method(typeof(ItemPatches), "OnInventoryRemoveItemPrefix");
                var postfix = AccessTools.Method(typeof(ItemPatches), "OnInventoryRemoveItemPostfix");
                if (prefix != null && postfix != null)
                {
                    _harmony.Patch(removeItemMethod,
                        prefix: new HarmonyMethod(prefix),
                        postfix: new HarmonyMethod(postfix));
                    Log.LogInfo($"Successfully patched {inventoryType.Name}.RemoveItem for vault integration");
                }
            }
            else
            {
                Log.LogWarning("Could not find Inventory.RemoveItem method");
            }
        }

        private static string DescribeAvailableMethods(Type targetType)
        {
            if (targetType == null)
                return "<no target type>";

            try
            {
                var methods = targetType
                    .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
                    .Select(m =>
                    {
                        string paramSig = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));
                        return $"{m.Name}({paramSig})";
                    })
                    .Distinct()
                    .OrderBy(s => s);

                return string.Join("; ", methods);
            }
            catch (Exception ex)
            {
                return $"<failed to enumerate methods: {ex.Message}>";
            }
        }

        private void ApplyVaultHudPlacementFromConfig(VaultHUD hud)
        {
            hud.SetPosition(ParseHUDPosition(_hudPosition.Value));
            if (_hudPositionX.Value >= 0f && _hudPositionY.Value >= 0f)
                hud.RestoreHudPixelPosition(_hudPositionX.Value, _hudPositionY.Value);
        }

        private void WireVaultHudPositionPersistence(VaultHUD hud)
        {
            hud.OnPositionChanged = (x, y) =>
            {
                _hudPositionX.SetSerializedValue(x.ToString(System.Globalization.CultureInfo.InvariantCulture));
                _hudPositionY.SetSerializedValue(y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            };
        }

        private static VaultHUD.HUDPosition ParseHUDPosition(string position)
        {
            return position?.ToLower() switch
            {
                "topleft" => VaultHUD.HUDPosition.TopLeft,
                "topcenter" => VaultHUD.HUDPosition.TopCenter,
                "topright" => VaultHUD.HUDPosition.TopRight,
                "bottomleft" => VaultHUD.HUDPosition.BottomLeft,
                "bottomcenter" => VaultHUD.HUDPosition.BottomCenter,
                "bottomright" => VaultHUD.HUDPosition.BottomRight,
                _ => VaultHUD.HUDPosition.TopLeft
            };
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
            // Save vault data on quit
            Log.LogInfo("Application quitting - saving vault data");
            _saveSystem?.ForceSave();
        }

        private void OnDisable()
        {
            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
            {
                Log.LogInfo($"[Lifecycle] Plugin OnDisable during expected teardown (scene: {sceneName})");
            }
            else
            {
                Log.LogWarning("[CRITICAL] Plugin OnDisable called outside expected teardown.");
                Log.LogWarning($"[CRITICAL] Current scene: {sceneName}");
                Log.LogWarning($"[CRITICAL] Stack trace: {Environment.StackTrace}");
            }
        }

        private void OnDestroy()
        {
            VaultModApiBridge.Instance = null;
            if (_vaultManager != null)
                _vaultManager.OnVaultLoaded -= OnVaultDataLoaded;
            if (ConfigFile != null)
                ConfigFile.SettingChanged -= OnAnyConfigSettingChanged;

            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
            {
                Log.LogInfo($"[Lifecycle] Plugin OnDestroy during expected teardown (scene: {sceneName})");
            }
            else
            {
                Log.LogWarning("[CRITICAL] Plugin OnDestroy called outside expected teardown.");
                Log.LogWarning($"[CRITICAL] Current scene: {sceneName}");
                Log.LogWarning($"[CRITICAL] Stack trace: {Environment.StackTrace}");
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // IMPORTANT: Do NOT unpatch Harmony here!
            // Harmony patches are global and will continue working even after this MonoBehaviour is destroyed.
            // If we unpatch, the LoadCharacter and InitializeAsOwner hooks stop working,
            // which breaks character switching entirely.
            // Only unpatch in OnApplicationQuit when the game is actually closing.
            // _harmony?.UnpatchSelf(); // REMOVED - this was breaking character switching!

            _saveSystem?.ForceSave();
        }

        #region Public API

        /// <summary>
        /// Get the vault manager instance
        /// </summary>
        public static VaultManager GetVaultManager()
        {
            // Use static field which survives Plugin destruction
            return _staticVaultManager;
        }

        /// <summary>
        /// Get the save system instance
        /// </summary>
        public static VaultSaveSystem GetSaveSystem()
        {
            // Use static field which survives Plugin destruction
            return _staticSaveSystem;
        }

        /// <summary>
        /// Get the vault UI instance
        /// </summary>
        public static VaultUI GetVaultUI()
        {
            // Use static field which survives Plugin destruction
            return _staticVaultUI;
        }

        /// <summary>Whether the active main vault window is open.</summary>
        public static bool IsMainVaultPanelVisible()
        {
            return _staticVaultUI != null && _staticVaultUI.IsVisible;
        }

        /// <summary>Open or close the vault using whichever UI mode config selected.</summary>
        public static void ToggleMainVaultWindow()
        {
            _staticVaultUI?.Toggle();
        }

        /// <summary>
        /// Open the vault UI
        /// </summary>
        public static void OpenVault()
        {
            _staticVaultUI?.Show();
        }

        /// <summary>
        /// Close the vault UI
        /// </summary>
        public static void CloseVault()
        {
            _staticVaultUI?.Hide();
        }

        /// <summary>
        /// Load vault data for a player. Returns false if the name was invalid or no save system exists — vault state is unchanged.
        /// </summary>
        public static bool LoadVaultForPlayer(string playerName)
        {
            if (_staticSaveSystem == null)
                return false;
            return _staticSaveSystem.Load(playerName);
        }

        /// <summary>
        /// True if the last <see cref="VaultSaveSystem.Load"/> quarantined an unreadable file (diagnostics / UI).
        /// </summary>
        public static bool LastVaultLoadQuarantinedCorruptFile =>
            _staticSaveSystem != null && _staticSaveSystem.LastLoadQuarantinedCorruptFile;

        /// <summary>
        /// True when load recovered no vault data and saves are blocked until <see cref="ConfirmStartFreshVault"/>.
        /// </summary>
        public static bool VaultLoadFailedNoRecoverableData =>
            _staticSaveSystem != null && _staticSaveSystem.LoadFailedNoRecoverableData;

        /// <summary>
        /// Player explicitly abandons the on-disk backup and allows the empty in-memory vault to save (AZR-238).
        /// </summary>
        public static void ConfirmStartFreshVault()
        {
            _staticSaveSystem?.ConfirmStartFreshVault();
        }

        /// <summary>
        /// Force save vault data
        /// </summary>
        public static void SaveVault()
        {
            // Use static field which survives Plugin destruction
            _staticSaveSystem?.ForceSave();
        }

        /// <summary>
        /// Get the vault HUD instance
        /// </summary>
        public static VaultHUD GetVaultHUD()
        {
            // Use static field which survives Plugin destruction
            return _staticVaultHUD;
        }

        /// <summary>
        /// Toggle the vault HUD visibility
        /// </summary>
        public static void ToggleHUD()
        {
            // Use static field which survives Plugin destruction
            _staticVaultHUD?.Toggle();
        }

        internal static void TickAutoSave()
        {
            if (Instance?._enableAutoSave == null || !Instance._enableAutoSave.Value)
                return;

            _staticSaveSystem?.CheckAutoSave();
        }

        internal static void TryQuickConvertHotkey()
        {
            Instance?.TryQuickConvert();
        }

        private void TryQuickConvert()
        {
            var manager = GetVaultManager();
            if (manager == null)
                return;

            var rules = ParseQuickConvertRules(_quickConvertTable?.Value);
            foreach (var rule in rules)
            {
                int available = manager.GetCurrency(rule.FromCurrencyId);
                if (available < rule.FromAmount)
                    continue;

                if (!TryMutateCurrency(manager, rule.FromCurrencyId, rule.FromAmount, add: false))
                    continue;
                if (!TryMutateCurrency(manager, rule.ToCurrencyId, rule.ToAmount, add: true))
                {
                    TryMutateCurrency(manager, rule.FromCurrencyId, rule.FromAmount, add: true);
                    continue;
                }

                Log?.LogInfo($"[QuickConvert] {rule.FromAmount} {rule.FromCurrencyId} -> {rule.ToAmount} {rule.ToCurrencyId}");
                return;
            }
        }

        private static List<QuickConvertRule> ParseQuickConvertRules(string table)
        {
            var rules = new List<QuickConvertRule>();
            if (string.IsNullOrWhiteSpace(table))
                return rules;

            foreach (string rawEntry in table.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string entry = rawEntry.Trim();
                int arrow = entry.IndexOf("->", StringComparison.Ordinal);
                int colonA = entry.IndexOf(':');
                int colonB = colonA >= 0 ? entry.IndexOf(':', colonA + 1) : -1;
                if (arrow <= 0 || colonA <= arrow || colonB <= colonA)
                    continue;

                string from = entry.Substring(0, arrow).Trim();
                string to = entry.Substring(arrow + 2, colonA - (arrow + 2)).Trim();
                if (!int.TryParse(entry.Substring(colonA + 1, colonB - colonA - 1), out int fromAmount) || fromAmount <= 0)
                    continue;
                if (!int.TryParse(entry.Substring(colonB + 1), out int toAmount) || toAmount <= 0)
                    continue;
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                    continue;

                rules.Add(new QuickConvertRule(from, to, fromAmount, toAmount));
            }

            return rules;
        }

        private static bool TryMutateCurrency(VaultManager manager, string currencyId, int amount, bool add)
        {
            if (manager == null || string.IsNullOrWhiteSpace(currencyId) || amount <= 0)
                return false;

            if (currencyId.StartsWith(VaultCurrencyIds.PrefixSeasonal, StringComparison.Ordinal))
            {
                string token = currencyId.Substring(VaultCurrencyIds.PrefixSeasonal.Length);
                if (!Enum.TryParse(token, out SeasonalTokenType type))
                    return false;
                return add ? manager.AddSeasonalTokens(type, amount) : manager.RemoveSeasonalTokens(type, amount);
            }
            if (currencyId.StartsWith(VaultCurrencyIds.PrefixCommunity, StringComparison.Ordinal))
            {
                string id = currencyId.Substring(VaultCurrencyIds.PrefixCommunity.Length);
                return add ? manager.AddCommunityTokens(id, amount) : manager.RemoveCommunityTokens(id, amount);
            }
            if (currencyId.StartsWith(VaultCurrencyIds.PrefixKey, StringComparison.Ordinal))
            {
                string id = currencyId.Substring(VaultCurrencyIds.PrefixKey.Length);
                return add ? manager.AddKeys(id, amount) : manager.RemoveKeys(id, amount);
            }
            if (currencyId.StartsWith(VaultCurrencyIds.PrefixSpecial, StringComparison.Ordinal))
            {
                string id = currencyId.Substring(VaultCurrencyIds.PrefixSpecial.Length);
                return add ? manager.AddSpecial(id, amount) : manager.RemoveSpecial(id, amount);
            }
            if (currencyId.StartsWith(VaultCurrencyIds.PrefixOrb, StringComparison.Ordinal))
            {
                string id = currencyId.Substring(VaultCurrencyIds.PrefixOrb.Length);
                return add ? manager.AddOrbs(id, amount) : manager.RemoveOrbs(id, amount);
            }
            if (currencyId.StartsWith(VaultCurrencyIds.PrefixCustom, StringComparison.Ordinal))
            {
                string id = currencyId.Substring(VaultCurrencyIds.PrefixCustom.Length);
                return add ? manager.AddCustomCurrency(id, amount) : manager.RemoveCustomCurrency(id, amount);
            }

            return false;
        }

        private readonly struct QuickConvertRule
        {
            public QuickConvertRule(string fromCurrencyId, string toCurrencyId, int fromAmount, int toAmount)
            {
                FromCurrencyId = fromCurrencyId;
                ToCurrencyId = toCurrencyId;
                FromAmount = fromAmount;
                ToAmount = toAmount;
            }

            public string FromCurrencyId { get; }
            public string ToCurrencyId { get; }
            public int FromAmount { get; }
            public int ToAmount { get; }
        }

        #endregion
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.azraelgodking.thevault";
        public const string PLUGIN_NAME = "The Vault";
        public const string PLUGIN_VERSION = "4.3.0";
    }
}
