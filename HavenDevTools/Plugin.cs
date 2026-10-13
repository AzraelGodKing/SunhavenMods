using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HavenDevTools.Config;
using HavenDevTools.Services;
using HavenDevTools.UI;
using SunhavenMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HavenDevTools
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.azraelgodking.thevault", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.sunhavenmuseumutilitytracker", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.havensbirthright", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.senpaischest", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.squirrelsbirthdayreminder", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.sunhaventodo", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.trinketfortune", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.havensrespec", BepInDependency.DependencyFlags.SoftDependency)]
    // NOTE: Do NOT add BepInDependency on HavensAlmanac - it creates a cyclic dep (Almanac already depends on HavenDevTools).
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static ConfigFile ConfigFile { get; private set; }

        // Static references that survive Plugin destruction
        private static ItemInspector _staticItemInspector;
        private static CurrencyTracker _staticCurrencyTracker;
        private static BundleInspector _staticBundleInspector;
        private static RaceModifierTracker _staticRaceModifierTracker;
        private static DebugWindow _staticDebugWindow;
        private static DebugOverlay _staticDebugOverlay;
        private static FpsCounterOverlay _staticFpsCounterOverlay;
        private static CommandConsole _staticCommandConsole;
        private static LogViewerPanel _staticLogViewer;

        // Persistent runner
        private static GameObject _persistentRunner;
        private static PersistentRunner _persistentRunnerComponent;

        // Static config values for PersistentRunner hotkey detection
        internal static KeyCode StaticToggleKey = KeyCode.F11;
        internal static KeyCode StaticOverlayToggleKey = KeyCode.F6;

        private static string _currentPlayerName;

        // Detected mods
        public static bool HasTheVault { get; private set; }
        public static bool HasSMUT { get; private set; }
        public static bool HasHavensBirthright { get; private set; }
        public static bool HasSenpaisChest { get; private set; }
        public static bool HasBirthdayReminder { get; private set; }
        public static bool HasSunhavenTodo { get; private set; }
        public static bool HasHavensAlmanac { get; private set; }
        public static bool HasTrinketFortune { get; private set; }
        public static bool HasCropOptimizer { get; private set; }
        public static bool HasFasterRaces { get; private set; }
        public static bool HasHavensRespec { get; private set; }
        public static bool HasGiftingAssistant { get; private set; }
        public static bool HasUltraPolygamy => UltraPolygamyHelper.IsAvailable;

        private Harmony _harmony;
        private bool _applicationQuitting;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigFile = CreateNamedConfig();
            SunhavenMods.Shared.ConfigFileHelper.ReplacePluginConfig(this, ConfigFile, Log.LogWarning);

            Log.LogInfo($"Loading {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}");

            try
            {
                ModConfig.Initialize(ConfigFile);

                StaticToggleKey = ModConfig.ToggleKey.Value;
                StaticOverlayToggleKey = ModConfig.OverlayToggleKey.Value;

                CreatePersistentRunner();

                DetectInstalledMods();
                ModConfig.SyncTheVaultFullVaultInspectorToPlugin();

                _staticItemInspector = new ItemInspector();
                _staticCurrencyTracker = new CurrencyTracker();
                _staticBundleInspector = new BundleInspector();
                _staticRaceModifierTracker = new RaceModifierTracker();
                _staticCommandConsole = new CommandConsole();
                _staticLogViewer = new LogViewerPanel();

                CreateUIComponents();

                _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                LocalizationBootstrap.BindForceEnglish(ConfigFile);
                LocalizationBootstrap.Init(PluginInfo.PLUGIN_GUID, _harmony, Log);
                PatchPlayerInit();

                SceneManager.sceneLoaded += OnSceneLoaded;

                if (ModConfig.CheckForUpdates.Value)
                {
                    VersionChecker.CheckForUpdate(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_VERSION, Log,
                        result => result.NotifyUpdateAvailable(Log));
                }

                Log.LogInfo($"{PluginInfo.PLUGIN_NAME} loaded successfully!");
                ReportStartupHealth();

                Log.LogInfo($"Press {ModConfig.ToggleKey.Value} to open the debug window");
                Log.LogInfo($"Press {ModConfig.OverlayToggleKey.Value} to toggle the overlay");
                Log.LogInfo($"Detected mods - TheVault: {HasTheVault}, SMUT: {HasSMUT}, Birthright: {HasHavensBirthright}, SenpaisChest: {HasSenpaisChest}, Birthday: {HasBirthdayReminder}, Todo: {HasSunhavenTodo}, Almanac: {HasHavensAlmanac}, TrinketFortune: {HasTrinketFortune}");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to load {PluginInfo.PLUGIN_NAME}: {ex}");
            }
        }

        private static ConfigFile CreateNamedConfig()
        {
            return ConfigFileHelper.CreateNamedConfig(
                PluginInfo.PLUGIN_GUID,
                "HavenDevTools.cfg",
                message => Log?.LogWarning(message)
            );
        }

        private void CreatePersistentRunner()
        {
            if (_persistentRunner != null && _persistentRunnerComponent != null) return;

            _persistentRunner = new GameObject("HavenDevTools_PersistentRunner");
            DontDestroyOnLoad(_persistentRunner);
            _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
            SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
            _persistentRunnerComponent = _persistentRunner.AddComponent<PersistentRunner>();
            Log.LogInfo("[PersistentRunner] Created hidden persistent runner");
        }

        private void CreateUIComponents()
        {
            var uiObject = new GameObject("HavenDevTools_UI");
            DontDestroyOnLoad(uiObject);
            // NOTE: Do NOT use HideFlags.HideAndDontSave on UI objects!
            // That flag prevents Unity's OnGUI from being called.

            _staticDebugWindow = uiObject.AddComponent<DebugWindow>();
            _staticDebugOverlay = uiObject.AddComponent<DebugOverlay>();
            _staticFpsCounterOverlay = uiObject.AddComponent<FpsCounterOverlay>();

            Log.LogInfo("UI components created");
        }

        /// <summary>
        /// Ensure UI components exist (recreate if destroyed by game cleanup).
        /// Called from PersistentRunner or when player initializes.
        /// </summary>
        public static void EnsureUIComponentsExist()
        {
            try
            {
                // Check if PersistentRunner was destroyed and recreate it
                if (_persistentRunner == null || _persistentRunnerComponent == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating PersistentRunner...");
                    _persistentRunner = new GameObject("HavenDevTools_PersistentRunner");
                    UnityEngine.Object.DontDestroyOnLoad(_persistentRunner);
                    _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
                    SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
                    _persistentRunnerComponent = _persistentRunner.AddComponent<PersistentRunner>();
                    Log?.LogInfo("[EnsureUI] PersistentRunner recreated");
                }

                // Check if UI was destroyed and recreate it
                if (_staticDebugWindow == null || _staticDebugOverlay == null || _staticFpsCounterOverlay == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating UI components...");
                    var uiObject = new GameObject("HavenDevTools_UI");
                    UnityEngine.Object.DontDestroyOnLoad(uiObject);

                    _staticDebugWindow = uiObject.AddComponent<DebugWindow>();
                    _staticDebugOverlay = uiObject.AddComponent<DebugOverlay>();
                    _staticFpsCounterOverlay = uiObject.AddComponent<FpsCounterOverlay>();

                    Log?.LogInfo("[EnsureUI] UI components recreated");
                }

                // Ensure services exist
                if (_staticItemInspector == null) _staticItemInspector = new ItemInspector();
                if (_staticCurrencyTracker == null) _staticCurrencyTracker = new CurrencyTracker();
                if (_staticBundleInspector == null) _staticBundleInspector = new BundleInspector();
                if (_staticRaceModifierTracker == null) _staticRaceModifierTracker = new RaceModifierTracker();
                if (_staticCommandConsole == null) _staticCommandConsole = new CommandConsole();
                if (_staticLogViewer == null) _staticLogViewer = new LogViewerPanel();
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnsureUI] Error recreating components: {ex.Message}");
            }
        }

        /// <summary>
        /// Re-scan suite mods. Safe to call after chainload finishes (e.g. Suite tab draw, first Update).
        /// Awake-time detection misses plugins that load later in the BepInEx chain.
        /// </summary>
        public static void RefreshInstalledMods()
        {
            HasTheVault = IsSuitePluginLoaded(SuitePluginGuids.TheVault, "TheVault");
            HasSMUT = IsSuitePluginLoaded(SuitePluginGuids.Smut, "SunHavenMuseumUtilityTracker");
            HasHavensBirthright = IsSuitePluginLoaded(SuitePluginGuids.HavensBirthright, "HavensBirthright");
            HasSenpaisChest = IsSuitePluginLoaded(SuitePluginGuids.SenpaisChest, "SenpaisChest");
            HasBirthdayReminder = IsSuitePluginLoaded(SuitePluginGuids.BirthdayReminder, "BirthdayReminder");
            HasSunhavenTodo = IsSuitePluginLoaded(SuitePluginGuids.SunhavenTodo, "SunhavenTodo");
            HasHavensAlmanac = IsSuitePluginLoaded(SuitePluginGuids.HavensAlmanac, "HavensAlmanac");
            HasTrinketFortune = IsSuitePluginLoaded(SuitePluginGuids.TrinketFortune, "TrinketFortune");
            HasCropOptimizer = IsSuitePluginLoaded(SuitePluginGuids.CropOptimizer, "CropOptimizer");
            HasFasterRaces = IsSuitePluginLoaded(SuitePluginGuids.FasterRaces, "FasterRaces");
            HasHavensRespec = IsSuitePluginLoaded(SuitePluginGuids.HavensRespec, "HavensRespec");
            HasGiftingAssistant = IsSuitePluginLoaded(SuitePluginGuids.GiftingAssistant, "GiftingAssistant");
        }

        private static bool IsSuitePluginLoaded(string pluginGuid, string assemblyName)
        {
            if (Chainloader.PluginInfos != null
                && !string.IsNullOrEmpty(pluginGuid)
                && Chainloader.PluginInfos.ContainsKey(pluginGuid))
            {
                return true;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void DetectInstalledMods()
        {
            RefreshInstalledMods();
            Log.LogInfo($"Mod detection complete - TheVault: {HasTheVault}, SMUT: {HasSMUT}, Birthright: {HasHavensBirthright}, SenpaisChest: {HasSenpaisChest}, Birthday: {HasBirthdayReminder}, Todo: {HasSunhavenTodo}, Almanac: {HasHavensAlmanac}, TrinketFortune: {HasTrinketFortune}, CropOptimizer: {HasCropOptimizer}, FasterRaces: {HasFasterRaces}, Respec: {HasHavensRespec}, GiftingAssistant: {HasGiftingAssistant}, UltraPolygamy: {HasUltraPolygamy}");
        }

        private void ReportStartupHealth()
        {
            ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                $"Suite tabs: {ModHealthIntegrationSummary.Build(
                    ("Vault", SuitePluginGuids.TheVault),
                    ("SMUT", SuitePluginGuids.Smut),
                    ("Birthright", SuitePluginGuids.HavensBirthright),
                    ("SenpaisChest", SuitePluginGuids.SenpaisChest),
                    ("Birthday", SuitePluginGuids.BirthdayReminder),
                    ("Todo", SuitePluginGuids.SunhavenTodo),
                    ("Almanac", SuitePluginGuids.HavensAlmanac),
                    ("TrinketFortune", SuitePluginGuids.TrinketFortune),
                    ("CropOptimizer", SuitePluginGuids.CropOptimizer),
                    ("FasterRaces", SuitePluginGuids.FasterRaces),
                    ("Respec", SuitePluginGuids.HavensRespec),
                    ("GiftingAssistant", SuitePluginGuids.GiftingAssistant))}");
        }

        private void PatchPlayerInit()
        {
            try
            {
                var playerType = typeof(Wish.Player);
                var initMethod = AccessTools.Method(playerType, "InitializeAsOwner");

                if (initMethod != null)
                {
                    var postfix = AccessTools.Method(typeof(Plugin), nameof(OnPlayerInitialized));
                    _harmony.Patch(initMethod, postfix: new HarmonyMethod(postfix));
                    Log.LogInfo("Patched Player.InitializeAsOwner");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to patch player init: {ex.Message}");
            }
        }

        private static void OnPlayerInitialized()
        {
            Log?.LogInfo("[HavenDevTools] Player initialized, ensuring UI exists...");
            EnsureUIComponentsExist();
            UpdatePlayerContext();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogDebug($"[SceneChange] Scene loaded: '{scene.name}'");

            string sceneLower = scene.name.ToLowerInvariant();
            if (sceneLower.Contains("menu") || sceneLower.Contains("title"))
            {
                Log.LogDebug($"Menu scene detected: {scene.name}");
                _currentPlayerName = null;
            }
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
                Log?.LogInfo($"[Lifecycle] Plugin OnDestroy during expected teardown (scene: {sceneName})");
            else
                Log?.LogWarning($"[Lifecycle] Plugin OnDestroy outside expected teardown (scene: {sceneName})");

            // NOTE: Do NOT call _harmony?.UnpatchSelf() here!
            // Patches must survive plugin destruction so OnPlayerInitialized
            // can trigger EnsureUIComponentsExist() on character reload.
        }

        #region Player context

        private static void UpdatePlayerContext()
        {
            try
            {
                if (Wish.Player.Instance != null)
                {
                    var playerNameProp = Wish.Player.Instance.GetType().GetProperty("PlayerName",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (playerNameProp != null)
                    {
                        _currentPlayerName = playerNameProp.GetValue(Wish.Player.Instance) as string;
                    }

                    if (string.IsNullOrEmpty(_currentPlayerName))
                    {
                        _currentPlayerName = Wish.Player.Instance.name;
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.LogDebug($"[HavenDevTools] Player name fallback: {ex.Message}");
                _currentPlayerName = "Unknown";
            }
        }

        #endregion

        #region Public API

        public static string CurrentPlayerName => _currentPlayerName;

        public static ItemInspector GetItemInspector() => _staticItemInspector;
        public static CurrencyTracker GetCurrencyTracker() => _staticCurrencyTracker;
        public static BundleInspector GetBundleInspector() => _staticBundleInspector;
        public static RaceModifierTracker GetRaceModifierTracker() => _staticRaceModifierTracker;

        public static CommandConsole GetCommandConsole() => _staticCommandConsole;
        public static LogViewerPanel GetLogViewer() => _staticLogViewer;

        public static void ToggleDebugWindow()
        {
            _staticDebugWindow?.Toggle();
        }

        public static void ToggleDebugOverlay()
        {
            _staticDebugOverlay?.Toggle();
        }

        #endregion
    }

    /// <summary>
    /// Persistent runner that survives game cleanup.
    /// Uses HideFlags.HideAndDontSave to avoid being destroyed by game's UIHandler.UnloadGame.
    /// </summary>
    public class PersistentRunner : MonoBehaviour
    {
        private float _heartbeatTimer = 0f;
        private int _heartbeatCount = 0;
        private const float HEARTBEAT_INTERVAL = 30f;
        private bool _syncedTheVaultInspectorOnce;

        private void Awake()
        {
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            Plugin.Log?.LogInfo("[PersistentRunner] Awake - hidden persistent runner active");
        }

        private void Update()
        {
            // HavenDevTools may load before other suite plugins; refresh detection once chainload is done.
            if (!_syncedTheVaultInspectorOnce)
            {
                _syncedTheVaultInspectorOnce = true;
                Plugin.RefreshInstalledMods();
                ModConfig.SyncTheVaultFullVaultInspectorToPlugin();
            }

            CheckHotkeys();

            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= HEARTBEAT_INTERVAL)
            {
                _heartbeatTimer = 0f;
                _heartbeatCount++;
                Plugin.Log?.LogDebug($"[PersistentRunner Heartbeat #{_heartbeatCount}] Player: {Plugin.CurrentPlayerName ?? "none"}");
            }
        }

        private void CheckHotkeys()
        {
            try
            {
                if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                    return;

                // Check for debug window toggle key
                if (Input.GetKeyDown(Plugin.StaticToggleKey))
                {
                    Plugin.ToggleDebugWindow();
                }

                // Check for overlay toggle key
                if (Input.GetKeyDown(Plugin.StaticOverlayToggleKey))
                {
                    Plugin.ToggleDebugOverlay();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[PersistentRunner] Hotkey error: {ex.Message}");
            }
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

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.azraelgodking.havendevtools";
        public const string PLUGIN_NAME = "Haven Dev Tools";
        public const string PLUGIN_VERSION = "2.1.3";
    }
}
