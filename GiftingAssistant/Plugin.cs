using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GiftingAssistant.Config;
using GiftingAssistant.Data;
using GiftingAssistant.Game;
using GiftingAssistant.Integration;
using GiftingAssistant.Patches;
using GiftingAssistant.UI;
using HarmonyLib;
using SunhavenMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GiftingAssistant
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.azraelgodking.squirrelsbirthdayreminder", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.sunhaventodo", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static ConfigFile ConfigFile { get; private set; }

        // Static state that survives plugin/scene teardown.
        private static GiftRosterManager _staticManager;
        private static GiftRosterSaveSystem _staticSaveSystem;
        private static GiftingWindow _staticWindow;
        private static GameObject _persistentRunner;
        private static PersistentRunner _persistentRunnerComponent;
        private static BirthdayIntegration _birthdayIntegration;
        private static TodoIntegration _todoIntegration;

        private static KeyCode _staticToggleKey = KeyCode.G;
        private static bool _staticRequireCtrl = true;
        private static bool _staticShowPossession = true;
        private static float _staticUIScale = 1f;
        private static bool _staticEnabled = true;
        private static bool _staticAlmanacIntegrationEnabled;

        private static bool _overnightHooked;
        private static UnityEngine.Events.UnityAction _overnightCallback;
        private static string _lastTodoRefreshDateKey = "";
        private static bool _applicationQuitting;
        private static bool _wasInMenuScene = true;
        private static float _lastAutoSaveTime;
        private static float _staticAutoSaveInterval = 60f;
        private static bool _staticAutoSave = true;

        private GiftingAssistantConfig _config;
        private Harmony _harmony;

        public static KeyCode StaticToggleKey => _staticToggleKey;
        public static bool StaticRequireCtrl => _staticRequireCtrl;
        public static bool StaticEnabled => _staticEnabled;
        public static bool StaticAlmanacIntegrationEnabled => _staticAlmanacIntegrationEnabled;

        public static string GetOpenShortcutDisplay()
        {
            string key = _staticToggleKey.ToString();
            return _staticRequireCtrl ? $"Ctrl+{key}" : key;
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigFile = CreateNamedConfig();
            ConfigFileHelper.ReplacePluginConfig(this, ConfigFile, Log.LogWarning);

            _config = new GiftingAssistantConfig(ConfigFile);
            _staticEnabled = _config.Enabled.Value;
            if (!_config.Enabled.Value)
            {
                Log.LogInfo($"{PluginInfo.PLUGIN_NAME} disabled in config.");
                ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                    ModHealthIntegrationSummary.Build(
                        ("DevTools", SuitePluginGuids.DevTools),
                        ("Birthday", SuitePluginGuids.BirthdayReminder),
                        ("Todo", SuitePluginGuids.SunhavenTodo),
                        ("Almanac", SuitePluginGuids.HavensAlmanac)),
                    mode: "disabled");
                return;
            }

            Log.LogInfo($"Loading {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}");

            BindConfigEvents();
            IconCache.Initialize(Log);

            LocalizationBootstrap.BindForceEnglish(ConfigFile);
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            LocalizationBootstrap.Init(PluginInfo.PLUGIN_GUID, _harmony, Log, Assembly.GetExecutingAssembly());
            ModLocalization.LanguageChanged += OnLanguageChanged;

            _birthdayIntegration = new BirthdayIntegration();
            _todoIntegration = new TodoIntegration
            {
                IsEnabled = _config.ReminderMode.Value == GiftingAssistant.Config.GiftReminderMode.PushToTodo
            };
            _staticAlmanacIntegrationEnabled = _config.UseAlmanacIntegration.Value;

            CreatePersistentRunner();
            InitializeManagers();
            ApplyPatches();

            SceneManager.sceneLoaded += OnSceneLoaded;

            if (_config.CheckForUpdates.Value)
            {
                VersionChecker.CheckForUpdate(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_VERSION, Log,
                    result => result.NotifyUpdateAvailable(Log));
            }

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} loaded successfully!");
            ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                ModHealthIntegrationSummary.Build(
                    ("DevTools", SuitePluginGuids.DevTools),
                    ("Birthday", SuitePluginGuids.BirthdayReminder),
                    ("Todo", SuitePluginGuids.SunhavenTodo),
                    ("Almanac", SuitePluginGuids.HavensAlmanac)));
        }

        private void BindConfigEvents()
        {
            _staticToggleKey = _config.ToggleKey.Value;
            _config.ToggleKey.SettingChanged += (_, _) => _staticToggleKey = _config.ToggleKey.Value;

            _staticRequireCtrl = _config.RequireCtrl.Value;
            _config.RequireCtrl.SettingChanged += (_, _) => _staticRequireCtrl = _config.RequireCtrl.Value;

            _staticShowPossession = _config.ShowInventoryPossession.Value;
            _config.ShowInventoryPossession.SettingChanged += (_, _) =>
            {
                _staticShowPossession = _config.ShowInventoryPossession.Value;
                _staticWindow?.SetShowPossession(_staticShowPossession);
            };

            _staticUIScale = Mathf.Clamp(_config.UIScale.Value, 0.5f, 2.5f);
            _config.UIScale.SettingChanged += (_, _) =>
            {
                _staticUIScale = Mathf.Clamp(_config.UIScale.Value, 0.5f, 2.5f);
                _staticWindow?.SetScale(_staticUIScale);
            };

            _config.ReminderMode.SettingChanged += (_, _) =>
            {
                if (_todoIntegration != null)
                    _todoIntegration.IsEnabled = _config.ReminderMode.Value == GiftingAssistant.Config.GiftReminderMode.PushToTodo;
            };

            _config.UseAlmanacIntegration.SettingChanged += (_, _) =>
                _staticAlmanacIntegrationEnabled = _config.UseAlmanacIntegration.Value;

            _staticAutoSaveInterval = Mathf.Max(5f, _config.AutoSaveInterval.Value);
            _config.AutoSaveInterval.SettingChanged += (_, _) =>
                _staticAutoSaveInterval = Mathf.Max(5f, _config.AutoSaveInterval.Value);

            _staticAutoSave = _config.AutoSave.Value;
            _config.AutoSave.SettingChanged += (_, _) => _staticAutoSave = _config.AutoSave.Value;
        }

        private static ConfigFile CreateNamedConfig()
        {
            return ConfigFileHelper.CreateNamedConfig(
                PluginInfo.PLUGIN_GUID,
                "GiftingAssistant.cfg",
                message => Log?.LogWarning(message));
        }

        private void CreatePersistentRunner()
        {
            if (_persistentRunner != null && _persistentRunnerComponent != null)
                return;

            _persistentRunner = new GameObject("GiftingAssistant_PersistentRunner");
            DontDestroyOnLoad(_persistentRunner);
            _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
            SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
            _persistentRunnerComponent = _persistentRunner.AddComponent<PersistentRunner>();
            Log.LogInfo("[PersistentRunner] Created");
        }

        private void InitializeManagers()
        {
            _staticManager = new GiftRosterManager();
            _staticSaveSystem = new GiftRosterSaveSystem(_staticManager);
        }

        private void ApplyPatches()
        {
            try
            {
                var playerType = AccessTools.TypeByName("Wish.Player");
                var initMethod = playerType != null ? AccessTools.Method(playerType, "InitializeAsOwner") : null;
                if (initMethod != null)
                {
                    var patch = AccessTools.Method(typeof(PlayerLoadPatch), nameof(PlayerLoadPatch.OnPlayerInitialized));
                    _harmony.Patch(initMethod, postfix: new HarmonyMethod(patch));
                    Log.LogInfo("Applied player initialization patch");
                }
                else
                {
                    Log.LogWarning("Could not find Wish.Player.InitializeAsOwner - roster auto-load disabled");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to apply player init patch: {ex.Message}");
            }

            try
            {
                var npcAIType = AccessTools.TypeByName("Wish.NPCAI");
                var itemType = AccessTools.TypeByName("Wish.Item");
                var giftMethod = (npcAIType != null && itemType != null)
                    ? AccessTools.Method(npcAIType, "Gift", new[] { itemType })
                    : null;
                if (giftMethod != null)
                {
                    var patch = AccessTools.Method(typeof(GiftPatch), nameof(GiftPatch.OnGiftGiven));
                    _harmony.Patch(giftMethod, postfix: new HarmonyMethod(patch));
                    Log.LogInfo("Applied gift tracking patch (NPCAI.Gift)");
                }
                else
                {
                    Log.LogWarning("Could not find NPCAI.Gift(Item) - instant gift tracking disabled");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to apply gift patch: {ex.Message}");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool isMenuScene = scene.name == "MainMenu" || scene.name == "Bootstrap";
            if (isMenuScene)
            {
                OvernightHookUtility.TryUnhookOvernightEvent(
                    ref _overnightHooked,
                    ref _overnightCallback,
                    type =>
                    {
                        try
                        {
                            var singletonBaseType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                            if (singletonBaseType != null)
                            {
                                var genericType = singletonBaseType.MakeGenericType(type);
                                return genericType.GetProperty("Instance")?.GetValue(null);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[GiftingAssistant] TryUnhookOvernight resolver failed: {ex.Message}");
                        }
                        return null;
                    },
                    msg => Log?.LogInfo(msg),
                    msg => Log?.LogWarning(msg));

                if (!_wasInMenuScene)
                {
                    SaveData();
                    PlayerLoadPatch.ResetForMenu();
                }
                _wasInMenuScene = true;
                return;
            }

            _wasInMenuScene = false;
            EnsureUIComponentsExist();
        }

        public static void EnsureUIComponentsExist()
        {
            try
            {
                LocalizationBootstrap.EnsureInitialized(
                    PluginInfo.PLUGIN_GUID,
                    Instance?._harmony,
                    Log,
                    typeof(Plugin).Assembly);

                if (_persistentRunner == null || _persistentRunnerComponent == null)
                {
                    _persistentRunner = new GameObject("GiftingAssistant_PersistentRunner");
                    UnityEngine.Object.DontDestroyOnLoad(_persistentRunner);
                    _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
                    SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
                    _persistentRunnerComponent = _persistentRunner.AddComponent<PersistentRunner>();
                }

                if (_staticWindow == null)
                {
                    var uiObject = new GameObject("GiftingAssistant_UI");
                    UnityEngine.Object.DontDestroyOnLoad(uiObject);
                    // No HideFlags here: HideAndDontSave suppresses OnGUI, which breaks the window.
                    _staticWindow = uiObject.AddComponent<GiftingWindow>();
                    _staticWindow.Initialize(_staticManager, _birthdayIntegration, _todoIntegration);
                    _staticWindow.SetScale(_staticUIScale);
                    _staticWindow.SetShowPossession(_staticShowPossession);
                    Log?.LogInfo("[EnsureUI] GiftingWindow created");
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnsureUI] Error: {ex.Message}");
            }
        }

        public void LoadDataForCharacter(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
            {
                Log.LogWarning("Cannot load roster: no character name");
                return;
            }

            var data = _staticSaveSystem.Load(characterName);
            _staticManager.LoadForCharacter(characterName, data);
            _staticWindow?.InvalidateNpcIndex();
            GiftGameData.ScheduleNpcCacheWarm();
            Log.LogInfo($"Loaded gift roster for character: {characterName}");
            TryHookOvernight();
            _todoIntegration?.ResetTracking();
            QueueEnsureGiftTodos();
        }

        public static void SaveData()
        {
            if (_staticManager == null || !_staticManager.IsDirty)
                return;
            _staticSaveSystem?.Save();
        }

        public static void MarkNpcGifted(string npcName)
        {
            if (_staticManager == null || string.IsNullOrEmpty(npcName))
                return;

            npcName = Game.GiftGameData.NormalizeNpcName(npcName);
            if (string.IsNullOrEmpty(npcName))
                return;

            if (_staticManager.Contains(npcName))
            {
                _staticManager.SetManualGifted(npcName, true);
                GiftGameData.SetGiftedToday(npcName, true);
                _staticWindow?.MarkGiftedSortDirty();
                QueueCompleteGiftTodo(npcName, true);
                return;
            }

            foreach (var entry in _staticManager.GetEntries())
            {
                if (string.Equals(Game.GiftGameData.NormalizeNpcName(entry.NpcName), npcName, StringComparison.OrdinalIgnoreCase))
                {
                    _staticManager.SetManualGifted(entry.NpcName, true);
                    GiftGameData.SetGiftedToday(entry.NpcName, true);
                    _staticWindow?.MarkGiftedSortDirty();
                    QueueCompleteGiftTodo(entry.NpcName, true);
                    return;
                }
            }
        }

        /// <summary>Queues gift-task completion sync (deferred flush).</summary>
        public static void QueueCompleteGiftTodo(string npcName, bool completed)
        {
            if (_todoIntegration == null || !_todoIntegration.CanPushTodos)
                return;
            _todoIntegration.QueueComplete(npcName, completed);
        }

        /// <summary>Queues removal of one NPC's gift task (deferred flush).</summary>
        public static void QueueRemoveGiftTodo(string npcName)
        {
            if (_todoIntegration == null || !_todoIntegration.CanPushTodos)
                return;
            _todoIntegration.QueueRemove(npcName);
        }

        /// <summary>Queues ensure-all gift tasks (character load).</summary>
        public static void QueueEnsureGiftTodos()
        {
            if (_todoIntegration == null || !_todoIntegration.CanPushTodos)
                return;
            _todoIntegration.QueueEnsureAll();
        }

        /// <summary>Queues a single NPC gift task (+Todo button).</summary>
        public static void QueuePublishGiftTodo(GiftRosterEntry entry)
        {
            if (entry == null || _todoIntegration == null || !_todoIntegration.CanPushTodos)
                return;
            _todoIntegration.QueuePublish(entry);
        }

        /// <summary>Deferred work: NPC cache warm, todo flush. Call once per frame.</summary>
        internal static void ProcessDeferredWork()
        {
            GiftGameData.ProcessDeferredNpcCache();
            _staticWindow?.NotifyNpcCacheReady();
            _todoIntegration?.ProcessPending();
        }

        public static void TryHookOvernight()
        {
            OvernightHookUtility.TryHookOvernightEvent(
                ref _overnightHooked,
                ref _overnightCallback,
                OnNewDay,
                type =>
                {
                    try
                    {
                        var singletonBaseType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                        if (singletonBaseType != null)
                        {
                            var genericType = singletonBaseType.MakeGenericType(type);
                            return genericType.GetProperty("Instance")?.GetValue(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GiftingAssistant] TryHookOvernight resolver failed: {ex.Message}");
                    }
                    return null;
                },
                msg => Log?.LogInfo(msg),
                msg => Log?.LogWarning(msg));
        }

        private static void OnNewDay()
        {
            if (_staticManager == null)
                return;

            string dateKey = GetCurrentDateKey();
            _staticManager.ResetDailyGifted(dateKey);
            GiftGameData.InvalidateCache();
            GiftGameData.ScheduleNpcCacheWarm();
            GiftGameData.RefreshGiftedTodayCache();
            _staticWindow?.InvalidateGiftedTodaySort();
            Log?.LogInfo("[GiftingAssistant] New day - reset daily gifted flags.");

            RefreshDailyTodos(dateKey);
            SaveData();
        }

        /// <summary>
        /// On a new in-game day, wipe our previous gift tasks and re-add a fresh one per rostered NPC.
        /// Guarded by the date key so it only runs once per day even if the overnight hook double-fires.
        /// </summary>
        private static void RefreshDailyTodos(string dateKey)
        {
            if (_todoIntegration == null || !_todoIntegration.CanPushTodos)
                return;

            if (!string.IsNullOrEmpty(dateKey) &&
                string.Equals(_lastTodoRefreshDateKey, dateKey, StringComparison.Ordinal))
                return;

            _lastTodoRefreshDateKey = dateKey ?? "";
            _todoIntegration?.ResetTracking();
            _todoIntegration?.QueueRefreshDaily();
        }

        private static string GetCurrentDateKey()
        {
            try
            {
                var dayCycleType = AccessTools.TypeByName("Wish.DayCycle");
                if (dayCycleType == null)
                    return "";

                int year = AccessTools.Property(dayCycleType, "Year")?.GetValue(null) is int y ? y : 0;
                int day = AccessTools.Property(dayCycleType, "MonthDay")?.GetValue(null) is int d ? d : 0;

                string season = "";
                var singletonBaseType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                if (singletonBaseType != null)
                {
                    var genericType = singletonBaseType.MakeGenericType(dayCycleType);
                    var instance = genericType.GetProperty("Instance")?.GetValue(null);
                    if (instance != null)
                        season = AccessTools.Property(dayCycleType, "Season")?.GetValue(instance)?.ToString() ?? "";
                }

                return $"{year}_{season}_{day}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GiftingAssistant] GetCurrentDateKey failed: {ex.Message}");
                return "";
            }
        }

        public static void ToggleUI()
        {
            EnsureUIComponentsExist();
            _staticWindow?.Toggle();
        }

        public static GiftRosterManager GetManager() => _staticManager;

        internal static void TickAutoSave()
        {
            if (_staticManager == null || !_staticManager.IsDirty)
                return;
            if (!_staticAutoSave)
                return;
            if (Time.unscaledTime - _lastAutoSaveTime < _staticAutoSaveInterval)
                return;
            SaveData();
            _lastAutoSaveTime = Time.unscaledTime;
        }

        private static void OnLanguageChanged(string _)
        {
            _staticWindow?.RefreshLocalization();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ModLocalization.LanguageChanged -= OnLanguageChanged;
            ModLocalization.Shutdown();
            SaveData();

            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
                Log?.LogInfo($"[Lifecycle] Plugin OnDestroy during expected teardown (scene: {sceneName})");
            else
                Log?.LogWarning($"[Lifecycle] Plugin OnDestroy outside expected teardown (scene: {sceneName})");
            // Patches intentionally left applied so OnPlayerInitialized keeps working across reloads.
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
            SaveData();
        }
    }

    /// <summary>
    /// Survives scene/plugin teardown to handle the open hotkey and periodic autosave.
    /// </summary>
    public class PersistentRunner : MonoBehaviour
    {
        private void Update()
        {
            CheckHotkeys();
            Plugin.TickAutoSave();
            Plugin.ProcessDeferredWork();
        }

        private void CheckHotkeys()
        {
            if (!Plugin.StaticEnabled)
                return;
            if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                return;

            bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (Input.GetKeyDown(Plugin.StaticToggleKey) && ctrlPressed == Plugin.StaticRequireCtrl)
                Plugin.ToggleUI();
        }

        private void OnDestroy()
        {
            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
                Plugin.Log?.LogInfo("[PersistentRunner] OnDestroy during app quit/menu unload (expected).");
            else
                Plugin.Log?.LogWarning("[PersistentRunner] OnDestroy outside quit/menu (unexpected).");
        }
    }
}
