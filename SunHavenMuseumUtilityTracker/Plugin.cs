using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SunhavenMods.Shared;
using SunHavenMuseumUtilityTracker.Data;
using SunHavenMuseumUtilityTracker.Patches;
using SunHavenMuseumUtilityTracker.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunHavenMuseumUtilityTracker
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        // Static references for access from patches
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static ConfigFile ConfigFile { get; private set; }

        // Static references that survive plugin destruction
        private static DonationManager _staticDonationManager;
        private static DonationSaveSystem _staticSaveSystem;
        private static MuseumTrackerUI _staticTrackerUI;
        private static GameObject _persistentRunner;
        private static PersistentRunner _persistentRunnerComponent;

        // Instance references
        private DonationManager _donationManager;
        private DonationSaveSystem _saveSystem;
        private MuseumTrackerUI _trackerUI;
        private Harmony _harmony;
        private bool _wasInMenuScene = true;
        private static bool _applicationQuitting;

        // Configuration
        private ConfigEntry<KeyCode> _toggleKey;
        private ConfigEntry<bool> _requireCtrl;
        private ConfigEntry<KeyCode> _altToggleKey;
        private ConfigEntry<bool> _checkForUpdates;
        private ConfigEntry<float> _uiScale;
        private ConfigEntry<bool> _wishlistShopBadges;
        private ConfigEntry<bool> _wishlistGiftBadges;

        // Static config for PersistentRunner
        public static KeyCode StaticToggleKey { get; private set; }
        public static bool StaticRequireCtrl { get; private set; }
        public static KeyCode StaticAltToggleKey { get; private set; }
        public static float StaticUIScale { get; private set; } = 1f;
        public static bool StaticWishlistShopBadges { get; private set; } = true;
        public static bool StaticWishlistGiftBadges { get; private set; } = true;

        public DonationManager DonationManager => _donationManager;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigFile = CreateNamedConfig();
            SunhavenMods.Shared.ConfigFileHelper.ReplacePluginConfig(this, ConfigFile, Log.LogWarning);

            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loading...");

            // Bind configuration
            BindConfiguration();

            SunhavenMods.Shared.IconCache.Initialize(Logger);

            // Create persistent runner first
            CreatePersistentRunner();

            // Initialize managers
            _donationManager = new DonationManager();
            _staticDonationManager = _donationManager;
            MuseumWishlistOverlayPatches.SubscribeDonationRefresh();

            _saveSystem = new DonationSaveSystem(_donationManager);
            _staticSaveSystem = _saveSystem;

            // Create UI
            CreateUIComponents();

            // Apply Harmony patches
            ApplyPatches();

            // Subscribe to scene changes
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Check for updates
            if (_checkForUpdates.Value)
            {
                VersionChecker.CheckForUpdate(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_VERSION, Log,
                    result => result.NotifyUpdateAvailable(Log));
            }

            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} loaded successfully!");
            ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                ModHealthIntegrationSummary.Build(
                    ("DevTools", SuitePluginGuids.DevTools),
                    ("SenpaisChest", SuitePluginGuids.SenpaisChest),
                    ("Almanac", SuitePluginGuids.HavensAlmanac)));
            Logger.LogInfo($"Press {(_requireCtrl.Value ? "Ctrl+" : "")}{_toggleKey.Value} or {_altToggleKey.Value} to open the tracker");
        }

        private void BindConfiguration()
        {
            _toggleKey = ConfigFile.Bind(
                "Hotkeys",
                "ToggleKey",
                KeyCode.C,
                "Key to toggle the Museum Tracker window"
            );

            _requireCtrl = ConfigFile.Bind(
                "Hotkeys",
                "RequireCtrl",
                true,
                "Require Ctrl to be held when pressing the toggle key"
            );

            _altToggleKey = ConfigFile.Bind(
                "Hotkeys",
                "AltToggleKey",
                KeyCode.F7,
                "Alternative key to toggle the Museum Tracker (no modifier required). Useful for Steam Deck."
            );

            _checkForUpdates = ConfigFile.Bind(
                "Updates",
                "CheckForUpdates",
                true,
                "Check for mod updates on startup"
            );

            _uiScale = ConfigFile.Bind(
                "Display",
                "UIScale",
                1f,
                new BepInEx.Configuration.ConfigDescription(
                    "Scale factor for the tracker window (1.0 = default, 1.5 = 50% larger)",
                    new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 2.5f)
                )
            );

            _wishlistShopBadges = ConfigFile.Bind(
                "WishlistOverlay",
                "ShowShopBadges",
                true,
                "Show a museum-needed badge on shop items that are still undonated"
            );

            _wishlistGiftBadges = ConfigFile.Bind(
                "WishlistOverlay",
                "ShowGiftBadges",
                true,
                "Show a museum-needed badge on inventory items while gifting to NPCs"
            );

            // Set static values for PersistentRunner
            StaticToggleKey = _toggleKey.Value;
            StaticRequireCtrl = _requireCtrl.Value;
            StaticAltToggleKey = _altToggleKey.Value;
            StaticUIScale = Mathf.Clamp(_uiScale.Value, 0.5f, 2.5f);
            StaticWishlistShopBadges = _wishlistShopBadges.Value;
            StaticWishlistGiftBadges = _wishlistGiftBadges.Value;

            // Listen for config changes
            _toggleKey.SettingChanged += (_, _) =>
            {
                StaticToggleKey = _toggleKey.Value;
                _trackerUI?.SetToggleKey(_toggleKey.Value, _requireCtrl.Value);
            };
            _requireCtrl.SettingChanged += (_, _) =>
            {
                StaticRequireCtrl = _requireCtrl.Value;
                _trackerUI?.SetToggleKey(_toggleKey.Value, _requireCtrl.Value);
            };
            _altToggleKey.SettingChanged += (_, _) =>
            {
                StaticAltToggleKey = _altToggleKey.Value;
            };
            _uiScale.SettingChanged += (_, _) =>
            {
                StaticUIScale = Mathf.Clamp(_uiScale.Value, 0.5f, 2.5f);
                _trackerUI?.SetScale(StaticUIScale);
            };
            _wishlistShopBadges.SettingChanged += (_, _) => StaticWishlistShopBadges = _wishlistShopBadges.Value;
            _wishlistGiftBadges.SettingChanged += (_, _) => StaticWishlistGiftBadges = _wishlistGiftBadges.Value;

            LocalizationBootstrap.BindForceEnglish(ConfigFile);
        }

        private static ConfigFile CreateNamedConfig()
        {
            string configPath = Path.Combine(Paths.ConfigPath, "SunHavenMuseumUtilityTracker.cfg");
            string legacyPath = Path.Combine(Paths.ConfigPath, $"{PluginInfo.PLUGIN_GUID}.cfg");
            try
            {
                if (!File.Exists(configPath) && File.Exists(legacyPath))
                    File.Copy(legacyPath, configPath);
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[Config] Migration to SunHavenMuseumUtilityTracker.cfg failed: {ex.Message}");
            }
            return new ConfigFile(configPath, true);
        }

        private void CreatePersistentRunner()
        {
            if (_persistentRunner != null && _persistentRunnerComponent != null) return;

            _persistentRunner = new GameObject("MuseumTracker_PersistentRunner");
            DontDestroyOnLoad(_persistentRunner);
            _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
            SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
            _persistentRunnerComponent = _persistentRunner.AddComponent<PersistentRunner>();
            Logger.LogInfo("[PersistentRunner] Created");
        }

        private void CreateUIComponents()
        {
            var uiObject = new GameObject("MuseumTracker_UI");
            DontDestroyOnLoad(uiObject);

            _trackerUI = uiObject.AddComponent<MuseumTrackerUI>();
            _trackerUI.Initialize(_donationManager);
            _trackerUI.SetScale(StaticUIScale);
            _trackerUI.SetToggleKey(_toggleKey.Value, _requireCtrl.Value);
            _staticTrackerUI = _trackerUI;

            Logger.LogInfo("UI components created");
        }

        /// <summary>
        /// Ensure UI components exist (recreate if destroyed by game cleanup).
        /// Called from PlayerPatches when a character loads.
        /// </summary>
        public static void EnsureUIComponentsExist()
        {
            try
            {
                // Check if PersistentRunner was destroyed and recreate it
                if (_persistentRunner == null || _persistentRunnerComponent == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating PersistentRunner...");
                    _persistentRunner = new GameObject("MuseumTracker_PersistentRunner");
                    UnityEngine.Object.DontDestroyOnLoad(_persistentRunner);
                    _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
                    SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
                    _persistentRunnerComponent = _persistentRunner.AddComponent<PersistentRunner>();
                    Log?.LogInfo("[EnsureUI] PersistentRunner recreated");
                }

                // Check if TrackerUI was destroyed and recreate it
                if (_staticTrackerUI == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating TrackerUI...");
                    var uiObject = new GameObject("MuseumTracker_UI");
                    UnityEngine.Object.DontDestroyOnLoad(uiObject);
                    // NOTE: Do NOT use HideFlags.HideAndDontSave on TrackerUI!
                    // That flag prevents Unity's OnGUI from being called, which breaks the UI rendering.
                    // Only PersistentRunner needs HideFlags (it only uses Update, not OnGUI).

                    _staticTrackerUI = uiObject.AddComponent<MuseumTrackerUI>();
                    _staticTrackerUI.Initialize(_staticDonationManager);
                    _staticTrackerUI.SetScale(StaticUIScale);
                    _staticTrackerUI.SetToggleKey(StaticToggleKey, StaticRequireCtrl);

                    Log?.LogInfo("[EnsureUI] TrackerUI recreated");
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnsureUI] Error recreating UI: {ex.Message}");
            }
        }

        private void ApplyPatches()
        {
            try
            {
                _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                LocalizationBootstrap.Init(PluginInfo.PLUGIN_GUID, _harmony, Log);

                // Patch Player.InitializeAsOwner for data loading
                var playerType = typeof(Wish.Player);
                var initMethod = AccessTools.Method(playerType, "InitializeAsOwner");
                if (initMethod != null)
                {
                    var patchMethod = AccessTools.Method(typeof(PlayerPatches), "OnPlayerInitialized");
                    _harmony.Patch(initMethod, postfix: new HarmonyMethod(patchMethod));
                    Logger.LogInfo("Patched Player.InitializeAsOwner");
                }
                else
                {
                    Logger.LogWarning("Could not find Player.InitializeAsOwner");
                }

                // Patch GameSave.LoadCharacter for character name extraction
                var gameSaveType = typeof(Wish.GameSave);
                var loadCharMethod = AccessTools.Method(gameSaveType, "LoadCharacter", new[] { typeof(int) });
                if (loadCharMethod != null)
                {
                    var patchMethod = AccessTools.Method(typeof(GameSavePatches), "OnLoadCharacter");
                    _harmony.Patch(loadCharMethod, postfix: new HarmonyMethod(patchMethod));
                    Logger.LogInfo("Patched GameSave.LoadCharacter");
                }

                // Patch HungryMonster for real-time donation tracking
                HungryMonsterPatches.ApplyPatches(_harmony);

                MuseumWishlistOverlayPatches.ApplyPatches(_harmony);

                Logger.LogInfo("Harmony patches applied");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying patches: {ex.Message}");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.LogInfo($"[SceneChange] Scene loaded: '{scene.name}'");

            string sceneName = scene.name.ToLowerInvariant();
            bool isMenuScene = sceneName.Contains("menu") || sceneName.Contains("title") || sceneName.Contains("mainmenu");
            if (isMenuScene)
            {
                if (!_wasInMenuScene)
                {
                    Logger.LogInfo($"Menu scene transition detected: {scene.name}");
                    PlayerPatches.SaveAndReset();
                }
                _wasInMenuScene = true;
                return;
            }

            _wasInMenuScene = false;
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
            Logger.LogInfo("Application quitting, saving data...");
            _saveSystem?.ForceSave();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
                Logger.LogInfo($"Plugin OnDestroy during expected teardown (scene: {sceneName})");
            else
                Logger.LogWarning($"[CRITICAL] Plugin OnDestroy outside expected teardown (scene: {sceneName})");

            _saveSystem?.ForceSave();
        }

        #region Public API

        public static DonationManager GetDonationManager() => _staticDonationManager;

        public static void SaveData()
        {
            _staticSaveSystem?.ForceSave();
        }

        public static void LoadDataForPlayer(string playerName)
        {
            var data = _staticSaveSystem?.Load(playerName);
            _staticDonationManager?.LoadForCharacter(playerName, data);
        }

        public static void ToggleUI()
        {
            _staticTrackerUI?.Toggle();
        }

        internal static void TickAutoSave()
        {
            if (!PlayerPatches.IsDataLoaded)
                return;

            _staticSaveSystem?.CheckAutoSave();
        }

        #endregion
    }

    /// <summary>
    /// Persistent runner that survives game cleanup.
    /// </summary>
    public class PersistentRunner : MonoBehaviour
    {
        private string _lastScene = "";
        private float _worldSyncTimer = 0f;
        private const float WORLD_SYNC_INTERVAL = 5f;

        private void Update()
        {
            Plugin.TickAutoSave();
            CheckHotkeys();
            CheckSceneChange();
            SyncWorldProgress();
        }

        private void CheckHotkeys()
        {
            if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                return;

            bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool togglePressed = Input.GetKeyDown(Plugin.StaticToggleKey);
            bool altTogglePressed = Input.GetKeyDown(Plugin.StaticAltToggleKey);

            // Check main toggle key (with optional Ctrl modifier)
            if (togglePressed && (ctrlPressed == Plugin.StaticRequireCtrl))
            {
                Plugin.ToggleUI();
            }
            // Check alt toggle key (no modifier required)
            else if (altTogglePressed)
            {
                Plugin.ToggleUI();
            }
        }

        private void CheckSceneChange()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene != _lastScene)
            {
                Plugin.Log?.LogDebug($"[PersistentRunner] Scene changed: '{_lastScene}' -> '{currentScene}'");
                _lastScene = currentScene;
            }
        }

        /// <summary>
        /// Senpai's Chest-style background refresh loop:
        /// periodically pull world progress so co-op players converge without opening the UI.
        /// </summary>
        private void SyncWorldProgress()
        {
            _worldSyncTimer += Time.unscaledDeltaTime;
            if (_worldSyncTimer < WORLD_SYNC_INTERVAL)
                return;

            _worldSyncTimer = 0f;

            if (!PlayerPatches.IsDataLoaded)
                return;

            var manager = Plugin.GetDonationManager();
            if (manager == null || !manager.IsLoaded)
                return;

            var (before, _) = manager.GetOverallStats();
            MuseumPatches.SyncWithGameProgress(verboseLogging: false);
            var (after, _) = manager.GetOverallStats();

            if (after <= before)
                return;

            Plugin.SaveData();
            Plugin.Log?.LogInfo($"[PersistentRunner] Background world sync marked {after - before} items");
        }

        private void OnDestroy()
        {
            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
            {
                Plugin.Log?.LogInfo("[PersistentRunner] OnDestroy during app quit/menu unload (expected).");
            }
            else
            {
                Plugin.Log?.LogWarning("[PersistentRunner] OnDestroy outside quit/menu (unexpected).");
            }
        }
    }

    /// <summary>
    /// Plugin information.
    /// </summary>
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.azraelgodking.sunhavenmuseumutilitytracker";
        public const string PLUGIN_NAME = "Sun Haven Museum Utility Tracker";
        public const string PLUGIN_VERSION = "3.1.3";
    }
}
