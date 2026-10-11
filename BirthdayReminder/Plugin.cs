using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BirthdayReminder.Data;
using BirthdayReminder.Integration;
using BirthdayReminder.UI;
using HarmonyLib;
using SunhavenMods.Shared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace BirthdayReminder
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.azraelgodking.sunhaventodo", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static ConfigFile ConfigFile { get; private set; }

        // Static references
        private static BirthdayManager _staticManager;
        private static BirthdayHUD _staticHUD;
        private static GameObject _hudObject;
        private static float _staticHUDPositionX = -1f;
        private static float _staticHUDPositionY = -1f;

        // Cross-mod integration
        private static TodoIntegration _todoIntegration;

        // Configuration
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _hudPositionX;
        private ConfigEntry<float> _hudPositionY;
        private ConfigEntry<KeyCode> _toggleKey;
        private ConfigEntry<bool> _showGiftHints;
        private ConfigEntry<bool> _useNativeNotifications;
        private ConfigEntry<bool> _debugMode;
        private ConfigEntry<bool> _checkForUpdates;
        private ConfigEntry<float> _uiScale;

        // Static config for UI scale (used when recreating HUD)
        private static float _staticUIScale = 1f;
        private static bool _staticShowGiftHints = true;

        // Character tracking for save switching
        private static string _currentCharacterName;

        // Instance references
        private BirthdayManager _manager;
        private BirthdayHUD _hud;
        private Harmony _harmony;
        private bool _applicationQuitting;

        // Hook tracking - reset when returning to main menu
        private static bool _overnightHooked = false;
        private static UnityAction _overnightCallback;

        // Persistent runner for hotkey handling across scene transitions
        private static GameObject _persistentRunner;
        private static PersistentRunner _persistentRunnerComponent;

        // Static config values for PersistentRunner access
        private static KeyCode _staticToggleKey = KeyCode.B;
        private static bool _staticDebugMode = false;
        private static bool _staticUseNativeNotifications = true;
        private static bool _staticEnabled = true;

        // Cached reflection for native notifications
        private static Type _notificationStackType;
        private static PropertyInfo _notificationStackInstance;
        private static MethodInfo _sendNotificationMethod;
        private static bool _notificationSystemInitialized = false;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigFile = CreateNamedConfig();
            SunhavenMods.Shared.ConfigFileHelper.ReplacePluginConfig(this, ConfigFile, Log.LogWarning);

            Log.LogInfo($"Loading {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}");

            BindConfiguration();
            if (!_enabled.Value)
            {
                Log.LogInfo($"{PluginInfo.PLUGIN_NAME} is disabled in config. Skipping initialization.");
                ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                    ModHealthIntegrationSummary.Build(
                        ("DevTools", SuitePluginGuids.DevTools),
                        ("Todo", SuitePluginGuids.SunhavenTodo)),
                    mode: "disabled");
                return;
            }
            CreatePersistentRunner();
            InitializeManager();
            ApplyPatches();
            InitializeIntegrations();

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
                    ("Todo", SuitePluginGuids.SunhavenTodo)));
        }

        private void BindConfiguration()
        {
            _enabled = ConfigFile.Bind(
                "General",
                "Enabled",
                true,
                "Enable birthday reminders"
            );
            _staticEnabled = _enabled.Value;
            _enabled.SettingChanged += (_, _) => _staticEnabled = _enabled.Value;

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

            _toggleKey = ConfigFile.Bind(
                "Hotkeys",
                "ToggleKey",
                KeyCode.B,
                "Key to toggle the birthday HUD (with Ctrl)"
            );
            _staticToggleKey = _toggleKey.Value;
            _toggleKey.SettingChanged += (_, _) => _staticToggleKey = _toggleKey.Value;

            _showGiftHints = ConfigFile.Bind(
                "General",
                "ShowGiftHints",
                true,
                "Show gift preferences in the birthday reminder"
            );
            _staticShowGiftHints = _showGiftHints.Value;
            _showGiftHints.SettingChanged += (_, _) => _staticShowGiftHints = _showGiftHints.Value;

            _useNativeNotifications = ConfigFile.Bind(
                "General",
                "UseNativeNotifications",
                true,
                "Show birthday notifications using the game's native notification system"
            );

            _debugMode = ConfigFile.Bind(
                "Debug",
                "DebugMode",
                false,
                "Enable debug mode for testing (Ctrl+Shift+B to add test birthday)"
            );
            _staticDebugMode = _debugMode.Value;
            _debugMode.SettingChanged += (_, _) => _staticDebugMode = _debugMode.Value;

            _staticUseNativeNotifications = _useNativeNotifications.Value;
            _useNativeNotifications.SettingChanged += (_, _) => _staticUseNativeNotifications = _useNativeNotifications.Value;

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
                    "Scale factor for the birthday HUD (1.0 = default, 1.5 = 50% larger)",
                    new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 2.5f)
                )
            );
            _staticUIScale = Mathf.Clamp(_uiScale.Value, 0.5f, 2.5f);
            _uiScale.SettingChanged += (_, _) =>
            {
                _staticUIScale = Mathf.Clamp(_uiScale.Value, 0.5f, 2.5f);
                _staticHUD?.SetScale(_staticUIScale);
            };

            LocalizationBootstrap.BindForceEnglish(ConfigFile);
        }

        private static ConfigFile CreateNamedConfig()
        {
            return ConfigFileHelper.CreateNamedConfig(
                PluginInfo.PLUGIN_GUID,
                "BirthdayReminder.cfg",
                message => Log?.LogWarning(message)
            );
        }

        private void CreatePersistentRunner()
        {
            if (_persistentRunner != null && _persistentRunnerComponent != null) return;

            _persistentRunner = new GameObject("BirthdayReminder_PersistentRunner");
            DontDestroyOnLoad(_persistentRunner);
            _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
            SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
            _persistentRunnerComponent = _persistentRunner.AddComponent<PersistentRunner>();
            Log.LogInfo("[PersistentRunner] Created");
        }

        private void InitializeManager()
        {
            _manager = new BirthdayManager();
            _staticManager = _manager;
        }

        private void InitializeIntegrations()
        {
            try
            {
                // Check if SunhavenTodo is loaded
                if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.azraelgodking.sunhaventodo"))
                {
                    _todoIntegration = new TodoIntegration(_staticManager);
                }
                else
                {
                    Log.LogInfo("[Integrations] SunhavenTodo not found - birthday todos disabled");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[Integrations] Error initializing: {ex.Message}");
            }
        }

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
                        var patchMethod = AccessTools.Method(typeof(PlayerPatches), nameof(PlayerPatches.OnPlayerInitialized));
                        _harmony.Patch(initMethod, postfix: new HarmonyMethod(patchMethod));
                        Log.LogInfo("Applied player initialization patch");
                    }
                    else
                        Log.LogWarning("Could not find Player.InitializeAsOwner — birthday player hook inactive");
                }
                else
                    Log.LogWarning("Could not find Wish.Player — birthday player hook inactive");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to apply player init patch: {ex.Message}");
            }

            try
            {
                var npcAIType = AccessTools.TypeByName("Wish.NPCAI");
                var itemType = AccessTools.TypeByName("Wish.Item");
                if (npcAIType != null && itemType != null)
                {
                    var giftMethod = AccessTools.Method(npcAIType, "Gift", new[] { itemType });
                    if (giftMethod != null)
                    {
                        var patchMethod = AccessTools.Method(typeof(GiftPatches), nameof(GiftPatches.OnGiftGiven));
                        _harmony.Patch(giftMethod, postfix: new HarmonyMethod(patchMethod));
                        Log.LogInfo("Applied gift tracking patch (NPCAI.Gift)");
                    }
                    else
                        Log.LogWarning("Could not find NPCAI.Gift(Item) — gift tracking disabled");
                }
                else
                    Log.LogWarning("Could not find NPCAI or Wish.Item — gift tracking disabled");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to apply gift patch — gift tracking disabled: {ex.Message}");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogDebug($"[BirthdayReminder] Scene loaded: {scene.name}");

            // Check for specific main menu scene (Sun Haven uses "MainMenu")
            if (scene.name == "MainMenu" || scene.name == "Bootstrap")
            {
                Log.LogInfo("[BirthdayReminder] Main menu detected - hiding HUD and resetting state");
                _staticHUD?.Hide();
                _currentCharacterName = null;
                return;
            }

            EnsureUIComponentsExist();
        }

        /// <summary>
        /// Reset the overnight hook so it can be re-established (call when returning from main menu)
        /// </summary>
        public static void ResetOvernightHook()
        {
            OvernightHookUtility.TryUnhookOvernightEvent(
                ref _overnightHooked,
                ref _overnightCallback,
                GetSingletonInstance,
                message => Log?.LogInfo($"[BirthdayReminder] {message}"),
                message => Log?.LogWarning($"[BirthdayReminder] {message}"));
        }

        /// <summary>
        /// Try to hook into UIHandler.OnCompleteOvernight event and DayCycle.OnDayStart
        /// </summary>
        public static void TryHookOvernightEvent()
        {
            OvernightHookUtility.TryHookOvernightEvent(
                ref _overnightHooked,
                ref _overnightCallback,
                OnOvernightComplete,
                GetSingletonInstance,
                message => Log?.LogInfo(message),
                message => Log?.LogWarning(message)
            );
        }

        /// <summary>
        /// Called when the overnight transition completes (wake up)
        /// </summary>
        private static void OnOvernightComplete()
        {
            Log?.LogInfo("[BirthdayReminder] Day started - checking for birthdays");

            // Check for birthdays
            _staticManager?.CheckTodaysBirthdays();

            // Show HUD if there are birthdays, hide if not
            if (_staticManager != null && _staticManager.HasBirthdays)
            {
                EnsureUIComponentsExist();
                _staticHUD?.Show();

                // Send native game notifications for each birthday
                SendAllBirthdayNotifications();
            }
            else
            {
                _staticHUD?.Hide();
                Log?.LogInfo("[BirthdayReminder] No birthdays today - HUD hidden");
            }
        }

        public static void EnsureUIComponentsExist()
        {
            try
            {
                // Recreate PersistentRunner if destroyed
                if (_persistentRunner == null || _persistentRunnerComponent == null)
                {
                    Log?.LogInfo("[EnsureUI] Recreating PersistentRunner...");
                    _persistentRunner = new GameObject("BirthdayReminder_PersistentRunner");
                    UnityEngine.Object.DontDestroyOnLoad(_persistentRunner);
                    _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
                    SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
                    _persistentRunnerComponent = _persistentRunner.AddComponent<PersistentRunner>();
                    Log?.LogInfo("[EnsureUI] PersistentRunner recreated");
                }

                if (_staticHUD == null)
                {
                    Log?.LogInfo("[EnsureUI] Creating BirthdayHUD...");
                    _hudObject = new GameObject("BirthdayReminder_HUD");
                    UnityEngine.Object.DontDestroyOnLoad(_hudObject);

                    _staticHUD = _hudObject.AddComponent<BirthdayHUD>();
                    _staticHUD.Initialize(_staticManager);
                    _staticHUD.SetScale(_staticUIScale);

                    // Restore saved position if valid
                    if (_staticHUDPositionX >= 0 && _staticHUDPositionY >= 0)
                    {
                        _staticHUD.SetPosition(_staticHUDPositionX, _staticHUDPositionY);
                    }

                    // Wire up position persistence
                    _staticHUD.OnPositionChanged = (x, y) =>
                    {
                        _staticHUDPositionX = x;
                        _staticHUDPositionY = y;
                        Instance?._hudPositionX?.SetSerializedValue(x.ToString());
                        Instance?._hudPositionY?.SetSerializedValue(y.ToString());
                    };

                    Log?.LogInfo("[EnsureUI] BirthdayHUD created");
                }

                if (Instance != null)
                {
                    Instance._hud = _staticHUD;
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnsureUI] Error: {ex.Message}");
            }
        }

        public static BirthdayManager GetManager() => _staticManager;
        public static BirthdayHUD GetHUD() => _staticHUD;
        public static TodoIntegration GetTodoIntegration() => _todoIntegration;
        public static bool ShowGiftHints => _staticShowGiftHints;
        public static KeyCode StaticToggleKey => _staticToggleKey;
        public static bool StaticDebugMode => _staticDebugMode;
        public static bool StaticUseNativeNotifications => _staticUseNativeNotifications;
        public static bool StaticEnabled => _staticEnabled;

        /// <summary>
        /// Initialize the native notification system via reflection
        /// </summary>
        private static void InitializeNotificationSystem()
        {
            if (_notificationSystemInitialized) return;
            _notificationSystemInitialized = true;

            try
            {
                // Get NotificationStack singleton
                _notificationStackType = AccessTools.TypeByName("Wish.NotificationStack");
                if (_notificationStackType == null)
                {
                    Log?.LogDebug("[Notifications] NotificationStack type not found");
                    return;
                }

                // Get SingletonBehaviour<NotificationStack>.Instance
                var singletonType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                if (singletonType != null)
                {
                    var genericType = singletonType.MakeGenericType(_notificationStackType);
                    _notificationStackInstance = genericType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                }

                // Get SendNotification method - signature: SendNotification(string, int, int, bool, bool)
                _sendNotificationMethod = AccessTools.Method(_notificationStackType, "SendNotification",
                    new[] { typeof(string), typeof(int), typeof(int), typeof(bool), typeof(bool) });

                if (_sendNotificationMethod != null)
                {
                    Log?.LogInfo("[Notifications] Native notification system initialized");
                }
                else
                {
                    Log?.LogDebug("[Notifications] SendNotification method not found");
                }
            }
            catch (Exception ex)
            {
                Log?.LogDebug($"[Notifications] Error initializing: {ex.Message}");
            }
        }

        /// <summary>
        /// Send a native game notification for a birthday
        /// </summary>
        /// <param name="npcName">Name of the NPC</param>
        /// <param name="itemId">Optional item ID to show (0 for default)</param>
        public static void SendBirthdayNotification(string npcName, int itemId = 0)
        {
            if (!_staticUseNativeNotifications) return;

            InitializeNotificationSystem();

            if (_sendNotificationMethod == null || _notificationStackInstance == null)
            {
                Log?.LogDebug("[Notifications] Cannot send - system not available");
                return;
            }

            try
            {
                var instance = _notificationStackInstance.GetValue(null);
                if (instance == null)
                {
                    Log?.LogDebug("[Notifications] NotificationStack instance is null");
                    return;
                }

                // Send notification: (title, itemId, amount, showInChat, playSound)
                string message = $"It's {npcName}'s birthday today!";
                _sendNotificationMethod.Invoke(instance, new object[] { message, itemId, 1, false, true });
                Log?.LogInfo($"[Notifications] Sent birthday notification for {npcName}");
            }
            catch (Exception ex)
            {
                Log?.LogDebug($"[Notifications] Error sending notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Send birthday notifications for all NPCs with birthdays today
        /// </summary>
        public static void SendAllBirthdayNotifications()
        {
            if (!_staticUseNativeNotifications) return;
            if (_staticManager == null || !_staticManager.HasBirthdays) return;

            foreach (var birthday in _staticManager.TodaysBirthdays)
            {
                if (!birthday.HasBeenGifted)
                {
                    SendBirthdayNotification(birthday.NPCName);
                }
            }
        }

        /// <summary>
        /// Get singleton instance using reflection
        /// </summary>
        private static object GetSingletonInstance(Type targetType)
        {
            try
            {
                // Try to find SingletonBehaviour<T> base class
                var singletonBaseType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                if (singletonBaseType != null)
                {
                    var genericType = singletonBaseType.MakeGenericType(targetType);
                    var instanceProp = AccessTools.Property(genericType, "Instance");
                    return instanceProp?.GetValue(null);
                }

                // Alternative: look for static Instance property directly
                var directInstanceProp = AccessTools.Property(targetType, "Instance");
                return directInstanceProp?.GetValue(null);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[BirthdayReminder] GetSingletonInstance: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Manually trigger birthday check (for testing)
        /// </summary>
        public static void CheckBirthdays()
        {
            _staticManager?.CheckTodaysBirthdays();
        }

        /// <summary>
        /// Called when character changes (save switch)
        /// </summary>
        public static void OnCharacterChanged(string newCharacterName)
        {
            if (_currentCharacterName != newCharacterName)
            {
                Log?.LogInfo($"[BirthdayReminder] Character changed: {_currentCharacterName ?? "None"} -> {newCharacterName}");
                _currentCharacterName = newCharacterName;

                // Reset birthday tracking for new character
                _staticManager?.ResetForNewCharacter(newCharacterName);

                // Reset cross-mod integration tracking
                _todoIntegration?.Reset();

                // Hide HUD until we check birthdays
                _staticHUD?.Hide();
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _todoIntegration?.Dispose();
            _todoIntegration = null;

            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
                Log?.LogInfo($"[Lifecycle] Plugin OnDestroy during expected teardown (scene: {sceneName})");
            else
                Log?.LogWarning($"[Lifecycle] Plugin OnDestroy outside expected teardown (scene: {sceneName})");

            // Leave patches applied. UnpatchSelf here dropped player, gift, and day hooks when this
            // component was destroyed during scene cleanup, so Birthday Reminder stayed dead until
            // restart (AZR-437). Same rule as The Vault and Gifting Assistant.
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }
    }

    /// <summary>
    /// Player patches for initialization
    /// </summary>
    public static class PlayerPatches
    {
        private static string _lastCharacterName;

        [HarmonyWrapSafe]
        public static void OnPlayerInitialized(object __instance)
        {
            try
            {
                Plugin.Log?.LogInfo("[PlayerPatches] Player initialized - checking character...");

                // Get character name
                string characterName = GetCharacterName(__instance);

                // Detect character change (save switch) or returning from main menu
                if (_lastCharacterName != characterName)
                {
                    Plugin.Log?.LogInfo($"[PlayerPatches] Character: {characterName}");
                    Plugin.OnCharacterChanged(characterName);
                    _lastCharacterName = characterName;
                }

                // Ensure UI components exist (recreates if destroyed)
                Plugin.EnsureUIComponentsExist();

                // Reset and re-hook the overnight event (it gets lost when returning from main menu)
                Plugin.ResetOvernightHook();
                Plugin.TryHookOvernightEvent();

                // Check birthdays when player loads in
                Plugin.GetManager()?.CheckTodaysBirthdays();

                // Show HUD if birthdays found
                var manager = Plugin.GetManager();
                if (manager != null && manager.HasBirthdays)
                {
                    Plugin.Log?.LogInfo($"[PlayerPatches] Found {manager.TodaysBirthdays.Count} birthdays - showing HUD");
                    Plugin.GetHUD()?.Show();
                }
                else
                {
                    Plugin.Log?.LogInfo("[PlayerPatches] No birthdays found today - HUD will not show automatically");
                    Plugin.Log?.LogInfo("[PlayerPatches] Use Ctrl+Shift+B to add a test birthday for testing");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in OnPlayerInitialized: {ex.Message}");
            }
        }

        private static string GetCharacterName(object player)
        {
            string name = GameSaveCharacterName.TryGetCurrent(
                logWarning: msg => Plugin.Log?.LogWarning($"Failed to get character name: {msg}"));
            if (!string.IsNullOrEmpty(name))
                return name;

            try
            {
                if (player != null)
                {
                    var nameProp = AccessTools.Property(player.GetType(), "playerName");
                    var playerName = nameProp?.GetValue(player) as string;
                    if (!string.IsNullOrEmpty(playerName))
                        return playerName;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Failed to get player name fallback: {ex.Message}");
            }

            return "Unknown";
        }
    }

    /// <summary>
    /// Gift tracking patches - patches NPCAI.Gift(Item) since AddFriendship was removed
    /// </summary>
    public static class GiftPatches
    {
        /// <summary>
        /// Track when a gift is given (postfix on NPCAI.Gift)
        /// </summary>
        [HarmonyWrapSafe]
        public static void OnGiftGiven(object __instance, object __0)
        {
            try
            {
                // __instance is the NPCAI, __0 is the Item
                if (__instance == null) return;

                var npcType = __instance.GetType();
                var nameProp = AccessTools.Property(npcType, "OriginalName")
                               ?? AccessTools.Property(npcType, "ActualNPCName")
                               ?? AccessTools.Property(npcType, "NPCName")
                               ?? AccessTools.Property(npcType, "npcName")
                               ?? AccessTools.Property(npcType, "Name");

                string npcName = nameProp?.GetValue(__instance)?.ToString();
                if (string.IsNullOrEmpty(npcName)) return;
                string normalizedNpcName = NormalizeNpcName(npcName);
                if (string.IsNullOrEmpty(normalizedNpcName)) return;

                var manager = Plugin.GetManager();
                if (manager == null || !manager.HasBirthdays) return;

                var birthdayInfo = manager.TodaysBirthdays.Find(b =>
                    string.Equals(NormalizeNpcName(b.NPCName), normalizedNpcName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(b.NPCName, npcName, StringComparison.OrdinalIgnoreCase) ||
                    (b.NPCName != null && b.NPCName.Contains("+" + npcName)));
                if (birthdayInfo != null && !birthdayInfo.HasBeenGifted)
                {
                    manager.MarkGifted(normalizedNpcName);
                    manager.TryRecordLovedGift(normalizedNpcName, __0);
                    Plugin.Log?.LogInfo($"[BirthdayReminder] Marked {normalizedNpcName} as gifted on their birthday!");

                    Plugin.GetTodoIntegration()?.OnGiftGiven(normalizedNpcName);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Error tracking gift: {ex.Message}");
            }
        }

        internal static string NormalizeNpcName(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return "";

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parts = new List<string>();
            foreach (var segment in npcName.Split('+'))
            {
                var trimmed = segment.Trim();
                if (trimmed.Length == 0 || !seen.Add(trimmed))
                    continue;
                parts.Add(trimmed);
            }

            if (parts.Count == 0)
                return npcName.Trim();
            if (parts.Count == 1)
                return parts[0];

            return string.Join("+", parts);
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
        }

        private void CheckHotkeys()
        {
            if (!Plugin.StaticEnabled)
                return;
            if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                return;

            bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool altPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            // Toggle HUD: Ctrl+B
            if (ctrlPressed && !shiftPressed && !altPressed && Input.GetKeyDown(Plugin.StaticToggleKey))
            {
                Plugin.Log?.LogInfo("[PersistentRunner] Ctrl+B pressed - toggling HUD");
                Plugin.EnsureUIComponentsExist();
                Plugin.GetHUD()?.Toggle();
            }

            // Debug: Ctrl+Shift+B - Add test birthday (documented in DebugMode config)
            if (Plugin.StaticDebugMode && ctrlPressed && shiftPressed && !altPressed && Input.GetKeyDown(Plugin.StaticToggleKey))
            {
                Plugin.EnsureUIComponentsExist();
                Plugin.GetManager()?.DebugAddTestBirthday("Test NPC", "Loves: Diamonds, Gold");
            }

            // Debug: Ctrl+Shift+R - Refresh/recheck birthdays
            if (Plugin.StaticDebugMode && ctrlPressed && shiftPressed && Input.GetKeyDown(KeyCode.R))
            {
                Plugin.Log?.LogInfo("[PersistentRunner] Ctrl+Shift+R pressed - Manual refresh");
                Plugin.EnsureUIComponentsExist();
                Plugin.GetManager()?.ManualRefresh();
                Plugin.GetHUD()?.Show();
            }

            // Debug: Ctrl+Shift+C - Clear all birthdays
            if (Plugin.StaticDebugMode && ctrlPressed && shiftPressed && Input.GetKeyDown(KeyCode.C))
            {
                Plugin.Log?.LogInfo("[PersistentRunner] Ctrl+Shift+C pressed - Clearing birthdays");
                Plugin.GetManager()?.DebugClearBirthdays();
                Plugin.GetHUD()?.Hide();
            }

            // Debug: Ctrl+Shift+A - Load ALL birthdays
            if (Plugin.StaticDebugMode && ctrlPressed && shiftPressed && Input.GetKeyDown(KeyCode.A))
            {
                Plugin.Log?.LogInfo("[PersistentRunner] Ctrl+Shift+A pressed - loading all birthdays");
                Plugin.EnsureUIComponentsExist();
                Plugin.GetManager()?.DebugLoadAllBirthdays();
            }

            // Debug: Ctrl+Shift+L - Dump Lynn's NPC info
            if (Plugin.StaticDebugMode && ctrlPressed && shiftPressed && Input.GetKeyDown(KeyCode.L))
            {
                Plugin.Log?.LogInfo("[PersistentRunner] Ctrl+Shift+L pressed - dumping Lynn's NPC info...");
                Plugin.EnsureUIComponentsExist();
                Plugin.GetManager()?.DebugDumpNPCInfo("Lynn");
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
