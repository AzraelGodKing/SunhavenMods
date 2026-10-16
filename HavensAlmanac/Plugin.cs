using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HavensAlmanac.Config;
using HavensAlmanac.Data;
using HavensAlmanac.UI;
using SunhavenMods.Shared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace HavensAlmanac
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.azraelgodking.sunhaventodo", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.squirrelsbirthdayreminder", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.sunhavenmuseumutilitytracker", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.senpaischest", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.thevault", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.havensbirthright", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.havendevtools", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.cropoptimizer", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.giftingassistant", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static ConfigFile ConfigFile { get; private set; }

        // Static references
        private static AlmanacDataAggregator _staticAggregator;
        private static AlmanacHUD _staticHUD;
        private static AlmanacDashboard _staticDashboard;
        private static DailyBriefing _staticBriefing;

        // Persistent runner
        private static GameObject _persistentRunner;
        private static AlmanacPersistentRunner _persistentRunnerComponent;

        // Overnight hook tracking
        private static bool _overnightHooked;
        private static UnityAction _overnightCallback;

        private Harmony _harmony;
        private bool _applicationQuitting;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigFile = CreateNamedConfig();
            SunhavenMods.Shared.ConfigFileHelper.ReplacePluginConfig(this, ConfigFile, Log.LogWarning);

            Log.LogInfo($"Loading {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}");

            AlmanacConfig.Initialize(ConfigFile);
            LocalizationBootstrap.BindForceEnglish(ConfigFile);
            CreatePersistentRunner();

            _staticAggregator = new AlmanacDataAggregator();
            InitializeIntegrations();
            CreateUIComponents();
            ApplyPatches();

            SceneManager.sceneLoaded += OnSceneLoaded;

            if (AlmanacConfig.CheckForUpdates.Value)
            {
                VersionChecker.CheckForUpdate(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_VERSION, Log,
                    result => result.NotifyUpdateAvailable(Log));
            }

            int integrationCount = _staticAggregator.IntegrationModCount;
            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} loaded with {integrationCount} integration{(integrationCount == 1 ? string.Empty : "s")}");

            ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                $"{integrationCount} providers, {ModHealthIntegrationSummary.Build(
                    ("DevTools", SuitePluginGuids.DevTools),
                    ("Todo", SuitePluginGuids.SunhavenTodo),
                    ("Birthday", SuitePluginGuids.BirthdayReminder),
                    ("SMUT", SuitePluginGuids.Smut),
                    ("SenpaisChest", SuitePluginGuids.SenpaisChest),
                    ("Vault", SuitePluginGuids.TheVault),
                    ("Birthright", SuitePluginGuids.HavensBirthright),
                    ("CropOptimizer", SuitePluginGuids.CropOptimizer),
                    ("GiftingAssistant", SuitePluginGuids.GiftingAssistant))}");

            // IntegrationModCount excludes Mod Health bridge and built-in Relationships.
            if (integrationCount == 0)
                Log.LogWarning("No supported companion mods detected. Haven's Almanac is most useful alongside SunhavenTodo, Birthday Reminder, Museum Tracker, Senpai's Chest, The Vault, Haven's Birthright, Haven Dev Tools, Crop Optimizer, or Gifting Assistant.");
        }

        private static ConfigFile CreateNamedConfig()
        {
            return ConfigFileHelper.CreateNamedConfig(
                PluginInfo.PLUGIN_GUID,
                "HavensAlmanac.cfg",
                message => Log?.LogWarning(message)
            );
        }

        private void CreatePersistentRunner()
        {
            if (_persistentRunner != null && _persistentRunnerComponent != null) return;

            _persistentRunner = new GameObject("HavensAlmanac_PersistentRunner");
            DontDestroyOnLoad(_persistentRunner);
            _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
            SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
            _persistentRunnerComponent = _persistentRunner.AddComponent<AlmanacPersistentRunner>();
            Log.LogInfo("[PersistentRunner] Created");
        }

        private void InitializeIntegrations()
        {
            var pluginInfos = BepInEx.Bootstrap.Chainloader.PluginInfos;

            _staticAggregator.RegisterProvider(new Integration.RelationshipDataProvider());

            if (pluginInfos.ContainsKey("com.azraelgodking.havendevtools"))
                _staticAggregator.RegisterProvider(new Integration.ModHealthBridgeProvider());

            TryRegisterProvider(pluginInfos, "com.azraelgodking.sunhaventodo",
                () => new Integration.TodoDataProvider(), "SunhavenTodo");

            TryRegisterProvider(pluginInfos, "com.azraelgodking.squirrelsbirthdayreminder",
                () => new Integration.BirthdayDataProvider(), "BirthdayReminder");

            TryRegisterProvider(pluginInfos, "com.azraelgodking.sunhavenmuseumutilitytracker",
                () => new Integration.MuseumDataProvider(), "S.M.U.T.");

            TryRegisterProvider(pluginInfos, "com.azraelgodking.senpaischest",
                () => new Integration.ChestDataProvider(), "SenpaisChest");

            TryRegisterProvider(pluginInfos, "com.azraelgodking.thevault",
                () => new Integration.VaultDataProvider(), "TheVault");

            TryRegisterProvider(pluginInfos, "com.azraelgodking.havensbirthright",
                () => new Integration.BirthrightDataProvider(), "HavensBirthright");

            TryRegisterProvider(pluginInfos, "com.azraelgodking.havendevtools",
                () => new Integration.DevToolsDataProvider(), "HavenDevTools");

            TryRegisterProvider(pluginInfos, "com.azraelgodking.cropoptimizer",
                () => new Integration.CropOptimizerDataProvider(), "CropOptimizer");

            TryRegisterProvider(pluginInfos, "com.azraelgodking.giftingassistant",
                () => new Integration.GiftingAssistantDataProvider(), "GiftingAssistant");
        }

        private void TryRegisterProvider(
            System.Collections.Generic.Dictionary<string, BepInEx.PluginInfo> pluginInfos,
            string guid, Func<IModDataProvider> factory, string displayName)
        {
            try
            {
                if (pluginInfos.ContainsKey(guid))
                {
                    _staticAggregator.RegisterProvider(factory());
                    Log.LogInfo($"[Integration] {displayName} detected and registered");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[Integration] Failed to load {displayName} provider: {ex.Message}");
            }
        }

        private void CreateUIComponents()
        {
            try
            {
                // HUD
                var hudObj = new GameObject("HavensAlmanac_HUD");
                DontDestroyOnLoad(hudObj);
                _staticHUD = hudObj.AddComponent<AlmanacHUD>();
                _staticHUD.Initialize(_staticAggregator);
                _staticHUD.SetScale(AlmanacConfig.StaticUIScale);
                WireHudPositionPersistence();

                // Dashboard
                var dashObj = new GameObject("HavensAlmanac_Dashboard");
                DontDestroyOnLoad(dashObj);
                _staticDashboard = dashObj.AddComponent<AlmanacDashboard>();
                _staticDashboard.Initialize(_staticAggregator);
                _staticDashboard.SetScale(AlmanacConfig.StaticUIScale);

                // Daily Briefing
                var briefObj = new GameObject("HavensAlmanac_Briefing");
                DontDestroyOnLoad(briefObj);
                _staticBriefing = briefObj.AddComponent<DailyBriefing>();
                _staticBriefing.Initialize(_staticAggregator);
                _staticBriefing.SetScale(AlmanacConfig.StaticUIScale);
            }
            catch (Exception ex)
            {
                Log.LogError($"[UI] Error creating UI components: {ex}");
            }
        }

        /// <summary>Called when Display/UIScale config changes; applies scale to all UI components.</summary>
        public void ApplyUIScaleToAllUI()
        {
            float scale = AlmanacConfig.StaticUIScale;
            _staticHUD?.SetScale(scale);
            _staticDashboard?.SetScale(scale);
            _staticBriefing?.SetScale(scale);
        }

        public static void EnsureUIComponentsExist()
        {
            try
            {
                if (_persistentRunner == null || _persistentRunnerComponent == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating PersistentRunner...");
                    _persistentRunner = new GameObject("HavensAlmanac_PersistentRunner");
                    UnityEngine.Object.DontDestroyOnLoad(_persistentRunner);
                    _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
                    SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
                    _persistentRunnerComponent = _persistentRunner.AddComponent<AlmanacPersistentRunner>();
                }

                if (_staticHUD == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating HUD...");
                    var hudObj = new GameObject("HavensAlmanac_HUD");
                    UnityEngine.Object.DontDestroyOnLoad(hudObj);
                    _staticHUD = hudObj.AddComponent<AlmanacHUD>();
                    _staticHUD.Initialize(_staticAggregator);
                    _staticHUD.SetScale(AlmanacConfig.StaticUIScale);
                    WireHudPositionPersistence();
                }

                if (_staticDashboard == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating Dashboard...");
                    var dashObj = new GameObject("HavensAlmanac_Dashboard");
                    UnityEngine.Object.DontDestroyOnLoad(dashObj);
                    _staticDashboard = dashObj.AddComponent<AlmanacDashboard>();
                    _staticDashboard.Initialize(_staticAggregator);
                    _staticDashboard.SetScale(AlmanacConfig.StaticUIScale);
                }

                if (_staticBriefing == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating Briefing...");
                    var briefObj = new GameObject("HavensAlmanac_Briefing");
                    UnityEngine.Object.DontDestroyOnLoad(briefObj);
                    _staticBriefing = briefObj.AddComponent<DailyBriefing>();
                    _staticBriefing.Initialize(_staticAggregator);
                    _staticBriefing.SetScale(AlmanacConfig.StaticUIScale);
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnsureUI] Error: {ex.Message}");
            }
        }

        #region Harmony Patches

        private void ApplyPatches()
        {
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            LocalizationBootstrap.Init(PluginInfo.PLUGIN_GUID, _harmony, Log);

            try
            {
                var playerType = AccessTools.TypeByName("Wish.Player");
                if (playerType != null)
                {
                    var initMethod = AccessTools.Method(playerType, "InitializeAsOwner");
                    if (initMethod != null)
                    {
                        var patchMethod = AccessTools.Method(typeof(Plugin), nameof(OnPlayerInitialized));
                        _harmony.Patch(initMethod, postfix: new HarmonyMethod(patchMethod));
                        Log.LogInfo("Applied player initialization patch");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to apply patches: {ex.Message}");
            }
        }

        private static void OnPlayerInitialized(object __instance)
        {
            try
            {
                Log?.LogInfo("[Almanac] Player initialized");

                EnsureUIComponentsExist();

                // Refresh all data
                _staticAggregator?.RefreshAll();

                // Reset and re-hook overnight event
                ResetOvernightHook();
                TryHookOvernightEvent();
            }
            catch (Exception ex)
            {
                Log?.LogError($"Error in OnPlayerInitialized: {ex.Message}");
            }
        }

        #endregion

        #region Overnight Hook

        public static void ResetOvernightHook()
        {
            OvernightHookUtility.TryUnhookOvernightEvent(
                ref _overnightHooked,
                ref _overnightCallback,
                ResolveSingletonInstance,
                message => Log?.LogInfo($"[Almanac] {message}"),
                message => Log?.LogWarning($"[Almanac] {message}"));
        }

        public static void TryHookOvernightEvent()
        {
            OvernightHookUtility.TryHookOvernightEvent(
                ref _overnightHooked,
                ref _overnightCallback,
                OnOvernightComplete,
                ResolveSingletonInstance,
                message => Log?.LogInfo(message),
                message => Log?.LogWarning(message)
            );
        }

        private static void OnOvernightComplete()
        {
            Log?.LogInfo("[Almanac] Day started - refreshing data and showing briefing");

            _staticAggregator?.RefreshAll();
            _staticBriefing?.ShowBriefing();
        }

        #endregion

        #region Scene Management

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu" || scene.name == "Bootstrap")
            {
                Log.LogInfo("[Almanac] Main menu detected - hiding UI");
                _staticHUD?.Hide();
                _staticDashboard?.Hide();
                _staticBriefing?.Hide();
                ResetOvernightHook();
                return;
            }

            EnsureUIComponentsExist();
        }

        private static void WireHudPositionPersistence()
        {
            if (_staticHUD == null)
                return;

            _staticHUD.OnPositionChanged = AlmanacConfig.SaveHudPosition;
        }

        private static object ResolveSingletonInstance(Type targetType)
        {
            if (targetType == null)
                return null;

            var singletonBaseType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
            if (singletonBaseType == null)
                return null;

            var genericType = singletonBaseType.MakeGenericType(targetType);
            var instanceProp = AccessTools.Property(genericType, "Instance");
            return instanceProp?.GetValue(null);
        }

        #endregion

        #region Static Accessors

        internal static AlmanacDataAggregator GetDataAggregator() => _staticAggregator;
        internal static AlmanacHUD GetAlmanacHUD() => _staticHUD;
        internal static AlmanacDashboard GetAlmanacDashboard() => _staticDashboard;
        internal static DailyBriefing GetDailyBriefing() => _staticBriefing;

        #endregion

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

            _harmony?.UnpatchSelf();
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }
    }

    /// <summary>
    /// Hidden MonoBehaviour that survives game cleanup. Handles hotkey detection.
    /// </summary>
    public class AlmanacPersistentRunner : MonoBehaviour
    {
        private void Update()
        {
            DetectHotkeys();
        }

        private void DetectHotkeys()
        {
            if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                return;

            bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            // Dashboard toggle: Ctrl+F5 (or just F5 if ctrl not required)
            if (Input.GetKeyDown(AlmanacConfig.StaticDashboardToggleKey))
            {
                if (!AlmanacConfig.StaticDashboardRequireCtrl || ctrlPressed)
                {
                    Plugin.EnsureUIComponentsExist();
                    Plugin.GetAlmanacDashboard()?.Toggle();
                }
            }

            // HUD toggle: F4
            if (Input.GetKeyDown(AlmanacConfig.StaticHUDToggleKey))
            {
                Plugin.EnsureUIComponentsExist();
                Plugin.GetAlmanacHUD()?.Toggle();
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
}
