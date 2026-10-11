using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SunhavenMods.Shared;
using SunhavenTodo.Data;
using SunhavenTodo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunhavenTodo
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static ConfigFile ConfigFile { get; private set; }

        // Static references that survive plugin destruction
        private static TodoManager _staticTodoManager;
        private static TodoSaveSystem _staticSaveSystem;
        private static TodoUI _staticTodoUI;
        private static TodoHUD _staticTodoHUD;
        private static GameObject _persistentRunner;
        private static PersistentRunner _persistentRunnerComponent;
        private static KeyCode _staticToggleKey = KeyCode.T;
        private static bool _staticRequireCtrl = true;
        private static bool _staticAutoSave = true;
        private static float _staticAutoSaveInterval = 60f;
        private static bool _staticHUDEnabled = true;
        private static float _staticHUDPositionX = -1f;
        private static float _staticHUDPositionY = -1f;
        private static KeyCode _staticHUDToggleKey = KeyCode.H;
        private static float _staticUIScale = 1f;

        // Configuration
        private ConfigEntry<KeyCode> _toggleKey;
        private ConfigEntry<bool> _requireCtrl;
        private ConfigEntry<bool> _autoSave;
        private ConfigEntry<float> _autoSaveInterval;
        private ConfigEntry<bool> _hudEnabled;
        private ConfigEntry<float> _hudPositionX;
        private ConfigEntry<float> _hudPositionY;
        private ConfigEntry<KeyCode> _hudToggleKey;
        private ConfigEntry<bool> _checkForUpdates;
        private ConfigEntry<float> _uiScale;

        // Instance references
        private TodoManager _todoManager;
        private TodoSaveSystem _saveSystem;
        private TodoUI _todoUI;
        private TodoHUD _todoHUD;

        // State
        private Harmony _harmony;
        private static float _lastAutoSaveTime;
        private bool _isDataLoaded;
        private string _loadedCharacterName;
        private bool _wasInMenuScene = true;
        private static bool _applicationQuitting;
        private static bool _overnightHooked;
        private static UnityEngine.Events.UnityAction _overnightCallback;

        // Static access for patches and hotkeys
        public static KeyCode StaticToggleKey => _staticToggleKey;
        public static bool StaticRequireCtrl => _staticRequireCtrl;
        public static KeyCode StaticHUDToggleKey => _staticHUDToggleKey;
        public static bool StaticHUDEnabled => _staticHUDEnabled;

        /// <summary>Returns the display string for the open-list shortcut (e.g. "Ctrl+T" or "J").</summary>
        public static string GetOpenListShortcutDisplay()
        {
            var key = StaticToggleKey.ToString();
            return StaticRequireCtrl ? $"Ctrl+{key}" : key;
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigFile = CreateNamedConfig();
            SunhavenMods.Shared.ConfigFileHelper.ReplacePluginConfig(this, ConfigFile, Log.LogWarning);

            Log.LogInfo($"Loading {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}");
            IconCache.Initialize(Log);

            BindConfiguration();
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            InitLocalization();
            CreatePersistentRunner();
            InitializeManagers();
            ApplyPatches();

            SceneManager.sceneLoaded += OnSceneLoaded;

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
        }

        private void BindConfiguration()
        {
            _toggleKey = ConfigFile.Bind(
                "Hotkeys",
                "ToggleKey",
                KeyCode.T,
                "Key to toggle the Todo List window"
            );
            _staticToggleKey = _toggleKey.Value;
            _toggleKey.SettingChanged += (_, _) => _staticToggleKey = _toggleKey.Value;

            _requireCtrl = ConfigFile.Bind(
                "Hotkeys",
                "RequireCtrl",
                true,
                "Require Ctrl to be held when pressing the toggle key"
            );
            _staticRequireCtrl = _requireCtrl.Value;
            _requireCtrl.SettingChanged += (_, _) => _staticRequireCtrl = _requireCtrl.Value;

            _autoSave = ConfigFile.Bind(
                "Saving",
                "AutoSave",
                true,
                "Automatically save the todo list periodically"
            );
            _staticAutoSave = _autoSave.Value;
            _autoSave.SettingChanged += (_, _) => _staticAutoSave = _autoSave.Value;

            _autoSaveInterval = ConfigFile.Bind(
                "Saving",
                "AutoSaveInterval",
                60f,
                "Auto-save interval in seconds"
            );
            _staticAutoSaveInterval = _autoSaveInterval.Value;
            _autoSaveInterval.SettingChanged += (_, _) => _staticAutoSaveInterval = _autoSaveInterval.Value;

            // HUD Configuration
            _hudEnabled = ConfigFile.Bind(
                "HUD",
                "Enabled",
                true,
                "Show the movable HUD panel with top 5 urgent tasks"
            );
            _staticHUDEnabled = _hudEnabled.Value;
            _hudEnabled.SettingChanged += (_, _) =>
            {
                _staticHUDEnabled = _hudEnabled.Value;
                _staticTodoHUD?.SetEnabled(_staticHUDEnabled);
            };

            _hudPositionX = ConfigFile.Bind(
                "HUD",
                "PositionX",
                -1f,
                "HUD X position (-1 for default)"
            );
            _staticHUDPositionX = _hudPositionX.Value;

            _hudPositionY = ConfigFile.Bind(
                "HUD",
                "PositionY",
                -1f,
                "HUD Y position (-1 for default)"
            );
            _staticHUDPositionY = _hudPositionY.Value;

            _hudToggleKey = ConfigFile.Bind(
                "Hotkeys",
                "HUDToggleKey",
                KeyCode.H,
                "Key to toggle the HUD panel (with Ctrl if RequireCtrl is enabled)"
            );
            _staticHUDToggleKey = _hudToggleKey.Value;
            _hudToggleKey.SettingChanged += (_, _) => _staticHUDToggleKey = _hudToggleKey.Value;

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
                    "Scale factor for the Todo list and HUD (1.0 = default, 1.5 = 50% larger)",
                    new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 2.5f)
                )
            );
            _staticUIScale = Mathf.Clamp(_uiScale.Value, 0.5f, 2.5f);
            _uiScale.SettingChanged += (_, _) =>
            {
                _staticUIScale = Mathf.Clamp(_uiScale.Value, 0.5f, 2.5f);
                _staticTodoUI?.SetScale(_staticUIScale);
                _staticTodoHUD?.SetScale(_staticUIScale);
            };
        }

        private static ConfigFile CreateNamedConfig()
        {
            string configPath = Path.Combine(Paths.ConfigPath, "SunhavenTodo.cfg");
            string legacyPath = Path.Combine(Paths.ConfigPath, $"{PluginInfo.PLUGIN_GUID}.cfg");
            try
            {
                if (!File.Exists(configPath) && File.Exists(legacyPath))
                    File.Copy(legacyPath, configPath);
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[Config] Migration to SunhavenTodo.cfg failed: {ex.Message}");
            }
            return new ConfigFile(configPath, true);
        }

        private void CreatePersistentRunner()
        {
            if (_persistentRunner != null && _persistentRunnerComponent != null) return;

            _persistentRunner = new GameObject("SunhavenTodo_PersistentRunner");
            DontDestroyOnLoad(_persistentRunner);
            _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
            SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
            _persistentRunnerComponent = _persistentRunner.AddComponent<PersistentRunner>();
            Log.LogInfo("[PersistentRunner] Created");
        }

        private void InitializeManagers()
        {
            _todoManager = new TodoManager();
            _staticTodoManager = _todoManager;

            _saveSystem = new TodoSaveSystem(_todoManager);
            _staticSaveSystem = _saveSystem;

            _todoManager.OnTodosChanged += OnTodosChanged;
        }

        private void InitLocalization()
        {
            LocalizationBootstrap.BindForceEnglish(ConfigFile);
            LocalizationBootstrap.Init(PluginInfo.PLUGIN_GUID, _harmony, Log, Assembly.GetExecutingAssembly());
        }

        private void ApplyPatches()
        {
            try
            {
                // Patch player initialization to load data per character
                var playerType = AccessTools.TypeByName("Wish.Player");
                if (playerType != null)
                {
                    var initMethod = AccessTools.Method(playerType, "InitializeAsOwner");
                    if (initMethod != null)
                    {
                        var patchMethod = AccessTools.Method(typeof(PlayerPatches), nameof(PlayerPatches.OnPlayerInitialized));
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

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool isMenuScene = IsMenuScene(scene.name);

            if (isMenuScene)
            {
                // DayCycle singleton is gone on main menu — unhook both event sources so re-entry cannot dual-fire.
                SunhavenMods.Shared.OvernightHookUtility.TryUnhookOvernightEvent(
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
                            System.Diagnostics.Debug.WriteLine($"[SunhavenTodo] TryUnhookOvernight resolver failed: {ex.Message}");
                        }
                        return null;
                    },
                    msg => Log?.LogInfo(msg),
                    msg => Log?.LogWarning(msg));

                if (!_wasInMenuScene)
                {
                    SaveData();
                    PlayerPatches.ResetForMenu();
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

                // Recreate PersistentRunner if destroyed
                if (_persistentRunner == null || _persistentRunnerComponent == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating PersistentRunner...");
                    _persistentRunner = new GameObject("SunhavenTodo_PersistentRunner");
                    UnityEngine.Object.DontDestroyOnLoad(_persistentRunner);
                    _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
                    SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
                    _persistentRunnerComponent = _persistentRunner.AddComponent<PersistentRunner>();
                    Log?.LogInfo("[EnsureUI] PersistentRunner recreated");
                }

                // Recreate TodoUI if destroyed
                if (_staticTodoUI == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating TodoUI...");
                    var uiObject = new GameObject("SunhavenTodo_UI");
                    UnityEngine.Object.DontDestroyOnLoad(uiObject);
                    // NOTE: Do NOT use HideFlags.HideAndDontSave on TodoUI!
                    // That flag prevents Unity's OnGUI from being called, which breaks the UI rendering.
                    // Only PersistentRunner needs HideFlags (it only uses Update, not OnGUI).

                    _staticTodoUI = uiObject.AddComponent<TodoUI>();
                    _staticTodoUI.Initialize(_staticTodoManager);
                    _staticTodoUI.SetScale(_staticUIScale);
                    Log?.LogInfo("[EnsureUI] TodoUI recreated");
                }

                // Recreate TodoHUD if destroyed
                if (_staticTodoHUD == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating TodoHUD...");
                    var hudObject = new GameObject("SunhavenTodo_HUD");
                    UnityEngine.Object.DontDestroyOnLoad(hudObject);

                    _staticTodoHUD = hudObject.AddComponent<TodoHUD>();
                    _staticTodoHUD.Initialize(_staticTodoManager);
                    _staticTodoHUD.SetScale(_staticUIScale);
                    _staticTodoHUD.SetEnabled(_staticHUDEnabled);

                    // Restore saved position if valid
                    if (_staticHUDPositionX >= 0 && _staticHUDPositionY >= 0)
                    {
                        _staticTodoHUD.SetPosition(_staticHUDPositionX, _staticHUDPositionY);
                    }

                    // Wire up position persistence
                    _staticTodoHUD.OnPositionChanged = (x, y) =>
                    {
                        _staticHUDPositionX = x;
                        _staticHUDPositionY = y;
                        Instance?._hudPositionX?.SetSerializedValue(x.ToString());
                        Instance?._hudPositionY?.SetSerializedValue(y.ToString());
                    };

                    Log?.LogInfo("[EnsureUI] TodoHUD recreated");
                }

                // Update instance reference if we have an instance
                if (Instance != null)
                {
                    Instance._todoUI = _staticTodoUI;
                    Instance._todoHUD = _staticTodoHUD;
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnsureUI] Error: {ex.Message}");
            }
        }

        private void OnTodosChanged()
        {
            // Mark for auto-save in 5 seconds
            _lastAutoSaveTime = Time.unscaledTime - _staticAutoSaveInterval + 5f;
        }

        public void LoadDataForCharacter(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
            {
                Log.LogWarning("Cannot load data: No character name");
                return;
            }

            // Save previous character's data if switching
            if (_isDataLoaded && _loadedCharacterName != characterName)
            {
                SaveData();
            }

            var data = _staticSaveSystem.Load(characterName);
            _staticTodoManager.LoadForCharacter(characterName, data);
            _isDataLoaded = true;
            _loadedCharacterName = characterName;
            Log.LogInfo($"Loaded todo list for character: {characterName}");
            TryHookOvernight();
        }

        public static void SaveData()
        {
            if (_staticTodoManager == null || !_staticTodoManager.IsDirty)
                return;
            _staticSaveSystem?.Save();
        }

        public static void TryHookOvernight()
        {
            SunhavenMods.Shared.OvernightHookUtility.TryHookOvernightEvent(
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
                        System.Diagnostics.Debug.WriteLine($"[SunhavenTodo] TryHookOvernight instance resolver failed: {ex.Message}");
                    }
                    return null;
                },
                msg => Log?.LogInfo(msg),
                msg => Log?.LogWarning(msg));
        }

        private static void OnNewDay()
        {
            if (_staticTodoManager == null) return;
            _staticTodoManager.ResetRecurringTodos(Data.RecurInterval.Daily);
            Log?.LogInfo("[Todo] Reset daily recurring tasks.");

            try
            {
                // DayCycle exposes MonthDay but no Week property; weekly reset uses day-of-month heuristic.
                var dayCycleType = AccessTools.TypeByName("Wish.DayCycle");
                if (dayCycleType != null)
                {
                    var monthDayProp = AccessTools.Property(dayCycleType, "MonthDay");
                    int day = monthDayProp != null ? (int)monthDayProp.GetValue(null) : 0;
                    if (day > 0)
                    {
                        // First day of each 7-day block in the season (days 1, 8, 15, 22).
                        if ((day - 1) % 7 == 0)
                        {
                            _staticTodoManager.ResetRecurringTodos(Data.RecurInterval.Weekly);
                            Log?.LogInfo("[Todo] Reset weekly recurring tasks.");
                        }
                        if (day == 1)
                        {
                            _staticTodoManager.ResetRecurringTodos(Data.RecurInterval.Seasonal);
                            Log?.LogInfo("[Todo] Reset seasonal recurring tasks.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SunhavenTodo] OnNewDay recurring reset check failed: {ex.Message}");
            }

            SaveData();
        }

        public static void ToggleUI()
        {
            EnsureUIComponentsExist();
            _staticTodoUI?.Toggle();
        }

        public static TodoManager GetTodoManager() => _staticTodoManager;
        public static TodoUI GetTodoUI() => _staticTodoUI;
        public static TodoHUD GetTodoHUD() => _staticTodoHUD;

        public static void ToggleHUD()
        {
            EnsureUIComponentsExist();
            _staticTodoHUD?.Toggle();
        }

        /// <summary>
        /// Shows the sticky HUD if HUD is enabled in config. Does not toggle off.
        /// </summary>
        public static void ShowHUD()
        {
            EnsureUIComponentsExist();
            if (!_staticHUDEnabled || _staticTodoHUD == null)
                return;
            _staticTodoHUD.SetEnabled(true);
        }

        internal static void TickAutoSave()
        {
            if (!_staticAutoSave || _staticTodoManager == null || !_staticTodoManager.IsDirty)
                return;

            if (Time.unscaledTime - _lastAutoSaveTime < _staticAutoSaveInterval)
                return;

            SaveData();
            _lastAutoSaveTime = Time.unscaledTime;
        }

        private static bool IsMenuScene(string sceneName) =>
            sceneName == "MainMenu" || sceneName == "Bootstrap" || sceneName == "Menu";

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_todoManager != null)
                _todoManager.OnTodosChanged -= OnTodosChanged;
            ModLocalization.Shutdown();

            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
                Log.LogInfo($"Plugin OnDestroy during expected teardown (scene: {sceneName})");
            else
                Log.LogWarning($"[CRITICAL] Plugin OnDestroy outside expected teardown (scene: {sceneName})");

            SaveData();
            // NOTE: Do NOT call _harmony?.UnpatchSelf() here!
            // Patches must survive plugin destruction so OnPlayerInitialized
            // can trigger EnsureUIComponentsExist() on character reload.
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
            SaveData();
        }
    }

    /// <summary>
    /// Persistent runner that survives game cleanup for hotkey detection.
    /// Uses HideFlags.HideAndDontSave to survive Unity cleanup.
    /// </summary>
    public class PersistentRunner : MonoBehaviour
    {
        private void Update()
        {
            CheckHotkeys();
            Plugin.TickAutoSave();
        }

        private void CheckHotkeys()
        {
            var todoUI = Plugin.GetTodoUI();
            bool todoOpen = todoUI != null && todoUI.IsVisible;

            if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                return;

            bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool togglePressed = Input.GetKeyDown(Plugin.StaticToggleKey);
            bool hudTogglePressed = Input.GetKeyDown(Plugin.StaticHUDToggleKey);

            // Full-window toggle: only when the big Todo window is closed (otherwise use Escape).
            if (!todoOpen && togglePressed && (ctrlPressed == Plugin.StaticRequireCtrl))
                Plugin.ToggleUI();

            // HUD toggle: always (so you can show/hide the sticky while the full list is open).
            if (hudTogglePressed && (ctrlPressed == Plugin.StaticRequireCtrl))
                Plugin.ToggleHUD();
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
    /// Harmony patches for game integration
    /// </summary>
    public static class PlayerPatches
    {
        private static bool _isDataLoaded = false;
        private static string _loadedCharacterName = null;

        public static void OnPlayerInitialized(object __instance)
        {
            try
            {
                Plugin.EnsureUIComponentsExist();

                string characterName = GetCurrentCharacterName(__instance);

                if (_isDataLoaded && _loadedCharacterName != characterName)
                {
                    Plugin.SaveData();
                    ResetState();
                }

                if (!string.IsNullOrEmpty(characterName))
                {
                    Plugin.Instance?.LoadDataForCharacter(characterName);
                    _isDataLoaded = true;
                    _loadedCharacterName = characterName;
                }
                else
                {
                    Plugin.Log?.LogWarning("Skipping todo load because character name is unavailable.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in OnPlayerInitialized: {ex.Message}");
            }
        }

        private static string GetCurrentCharacterName(object player)
        {
            string name = GameSaveCharacterName.TryGetCurrent(
                logWarning: msg => Plugin.Log?.LogWarning($"Failed to get character name: {msg}"));
            if (!string.IsNullOrEmpty(name))
                return name;

            if (!string.IsNullOrEmpty(_loadedCharacterName))
                Plugin.Log?.LogWarning("Character name lookup failed; refusing to reuse previous character identity.");

            return null;
        }

        private static void ResetState()
        {
            _isDataLoaded = false;
            _loadedCharacterName = null;
        }

        internal static void ResetForMenu()
        {
            ResetState();
        }
    }
}
