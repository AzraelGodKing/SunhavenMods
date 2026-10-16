#pragma warning disable CS0436 // Type conflicts with imported type - we explicitly use SunhavenMods.Shared types
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SenpaisChest.Config;
using SenpaisChest.Data;
using SenpaisChest.Integration;
using SenpaisChest.UI;
using SunhavenMods.Shared;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Wish;

namespace SenpaisChest
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.azraelgodking.sunhavenmuseumutilitytracker", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.azraelgodking.sunhaventodo", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static ConfigFile ConfigFile { get; private set; }

        // Static references that survive Plugin destruction
        private static SmartChestManager _staticManager;
        private static SmartChestSaveSystem _staticSaveSystem;
        private static SmartChestUI _staticUI;
        private static SmartChestConfig _staticConfig;

        // Cross-mod integration
        private static MuseumTodoIntegration _museumTodoIntegration;

        // Track the chest the player is currently interacting with
        internal static Chest CurrentInteractingChest;

        /// <summary>
        /// Character we've already loaded from disk this play session. Scene changes (farm → town) must not
        /// call LoadData again or we overwrite unsaved UI edits (auto-save is only every 5 minutes).
        /// </summary>
        private static string _gameplaySessionCharacter;

        private Harmony _harmony;
        private SmartChestManager _manager;
        private SmartChestSaveSystem _saveSystem;
        private SmartChestUI _ui;
        private SmartChestConfig _config;

        // PersistentRunner
        private static GameObject _persistentRunner;
        private static SmartChestPersistentRunner _updateRunner;

        // Scene tracking
        private bool _wasInMenuScene = true;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigFile = CreateNamedConfig();
            SunhavenMods.Shared.ConfigFileHelper.ReplacePluginConfig(this, ConfigFile, Log.LogWarning);

            Log.LogInfo($"Loading {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}");

            CreatePersistentRunner();

            try
            {
                // Initialize config
                _config = new SmartChestConfig();
                _config.Initialize(ConfigFile);
                _staticConfig = _config;
                if (_config.TryApplyHighResLayoutDefaults())
                    Log.LogInfo($"[Senpai's Chest] Auto high-res layout applied (UIScale={_config.UIScale.Value:F2}, {Screen.width}x{Screen.height})");
                void ApplyUiScale() => _staticUI?.SetScale(_config.GetEffectiveUIScale());
                _config.UIScale.SettingChanged += (_, _) => ApplyUiScale();
                _config.ScaleWithResolution.SettingChanged += (_, _) => ApplyUiScale();

                // Initialize manager and save system
                _manager = new SmartChestManager();
                _saveSystem = new SmartChestSaveSystem(_manager);
                _staticManager = _manager;
                _staticSaveSystem = _saveSystem;

                // Create UI (separate GameObject, no HideFlags — needs OnGUI)
                var uiObject = new GameObject("SenpaisChest_UI");
                DontDestroyOnLoad(uiObject);
                _ui = uiObject.AddComponent<SmartChestUI>();
                _ui.Initialize(_manager);
                _ui.SetScale(_config.GetEffectiveUIScale());
                _staticUI = _ui;

                // Apply Harmony patches
                _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                LocalizationBootstrap.BindForceEnglish(ConfigFile);
                LocalizationBootstrap.Init(PluginInfo.PLUGIN_GUID, _harmony, Log);
                ApplyPatches();

                // Initialize cross-mod integrations
                InitializeIntegrations();

                // Subscribe to scene loading
                SceneManager.sceneLoaded += OnSceneLoaded;

                // Check for updates
                if (_config.CheckForUpdates.Value)
                {
                    SunhavenMods.Shared.VersionChecker.CheckForUpdate(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_VERSION, Log,
                        result => result.NotifyUpdateAvailable(Log));
                }

                Log.LogInfo($"{PluginInfo.PLUGIN_NAME} loaded successfully!");
                ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                    ModHealthIntegrationSummary.Build(
                        ("DevTools", SuitePluginGuids.DevTools),
                        ("SMUT", SuitePluginGuids.Smut),
                        ("Todo", SuitePluginGuids.SunhavenTodo)));
                Log.LogInfo($"Press {(_config.RequireCtrlModifier.Value ? "Ctrl+" : "")}{_config.ToggleKey.Value} to configure a chest while interacting with it");
                if (_config.EnableChestLabels.Value)
                    Log.LogInfo("Chest Labels enabled - labels shown above Chest and BankChest (not Hoppers/Animal Feeders)");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to load {PluginInfo.PLUGIN_NAME}: {ex}");
            }
        }

        private void CreatePersistentRunner()
        {
            if (_persistentRunner != null)
            {
                Log.LogDebug("PersistentRunner already exists");
                return;
            }

            _persistentRunner = new GameObject("SenpaisChest_PersistentRunner");
            DontDestroyOnLoad(_persistentRunner);
            _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
            SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
            _updateRunner = _persistentRunner.AddComponent<SmartChestPersistentRunner>();

            Log.LogDebug("Created hidden PersistentRunner");
        }

        private static ConfigFile CreateNamedConfig()
        {
            string configPath = Path.Combine(Paths.ConfigPath, "SenpaisChest.cfg");
            string legacyPath = Path.Combine(Paths.ConfigPath, $"{PluginInfo.PLUGIN_GUID}.cfg");
            try
            {
                if (!File.Exists(configPath) && File.Exists(legacyPath))
                    File.Copy(legacyPath, configPath);
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[Config] Migration to SenpaisChest.cfg failed: {ex.Message}");
            }
            return new ConfigFile(configPath, true);
        }

        public static void EnsureUIComponentsExist()
        {
            try
            {
                if (_persistentRunner == null || _updateRunner == null)
                {
                    Log?.LogDebug("[EnsureUI] Recreating PersistentRunner...");
                    _persistentRunner = new GameObject("SenpaisChest_PersistentRunner");
                    UnityEngine.Object.DontDestroyOnLoad(_persistentRunner);
                    _persistentRunner.hideFlags = HideFlags.HideAndDontSave;
                    SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_persistentRunner);
                    _updateRunner = _persistentRunner.AddComponent<SmartChestPersistentRunner>();
                }

                if (_staticUI == null)
                {
                    Log?.LogDebug("[EnsureUI] Recreating SmartChestUI...");
                    var uiObject = new GameObject("SenpaisChest_UI");
                    UnityEngine.Object.DontDestroyOnLoad(uiObject);
                    _staticUI = uiObject.AddComponent<SmartChestUI>();
                    _staticUI.Initialize(_staticManager);
                    _staticUI.SetScale(_staticConfig != null ? _staticConfig.GetEffectiveUIScale() : 1f);
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnsureUI] Error recreating UI: {ex.Message}");
            }
        }

        private void InitializeIntegrations()
        {
            try
            {
                _museumTodoIntegration?.Dispose();
                _museumTodoIntegration = null;

                var pluginInfos = BepInEx.Bootstrap.Chainloader.PluginInfos;
                bool hasSmut = pluginInfos.ContainsKey("com.azraelgodking.sunhavenmuseumutilitytracker");
                bool hasTodo = pluginInfos.ContainsKey("com.azraelgodking.sunhaventodo");

                if (hasSmut && hasTodo)
                {
                    _museumTodoIntegration = new MuseumTodoIntegration();
                }
                else
                {
                    if (!hasSmut) Log.LogDebug("[Integrations] S.M.U.T. not found");
                    if (!hasTodo) Log.LogDebug("[Integrations] SunhavenTodo not found");
                    Log.LogDebug("[Integrations] Museum todo integration disabled (requires both S.M.U.T. and Todo)");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[Integrations] Error initializing: {ex.Message}");
            }
        }

        #region Harmony Patches

        private void ApplyPatches()
        {
            try
            {
                // Player.InitializeAsOwner → load character data
                PatchMethod(typeof(Player), "InitializeAsOwner",
                    typeof(Plugin), nameof(OnPlayerInitialized));

                // Chest.Interact → track current chest
                PatchMethod(typeof(Chest), "Interact",
                    typeof(Plugin), nameof(OnChestInteract),
                    new[] { typeof(int) });

                // Chest.EndInteract → block close while config UI is open
                var endInteractOriginal = AccessTools.Method(typeof(Chest), "EndInteract", new[] { typeof(int) });
                if (endInteractOriginal != null)
                {
                    var prefix = new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(OnChestEndInteract_Prefix)));
                    _harmony.Patch(endInteractOriginal, prefix: prefix);
                    Log.LogDebug("Patched Chest.EndInteract (prefix)");
                }
                else
                {
                    Log.LogWarning("Could not find method Chest.EndInteract");
                }

                // UIHandler.OpenUI → add "Senpai's Chest" button to chest panel when opened
                var uiHandlerType = AccessTools.TypeByName("Wish.UIHandler");
                var iExternalType = AccessTools.TypeByName("Wish.IExternalUIHandler");
                if (uiHandlerType != null && iExternalType != null)
                {
                    var openUIMethod = AccessTools.Method(uiHandlerType, "OpenUI", new[] { typeof(GameObject), typeof(Transform), typeof(bool), typeof(bool), iExternalType });
                    if (openUIMethod != null)
                    {
                        var openUIPostfix = new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(OnUIHandlerOpenUI_Postfix)));
                        _harmony.Patch(openUIMethod, postfix: openUIPostfix);
                        Log.LogDebug("Patched UIHandler.OpenUI (postfix)");
                    }
                    else
                    {
                        Log.LogWarning("Could not find method UIHandler.OpenUI");
                    }
                }
                else
                {
                    Log.LogWarning("Could not find Wish.UIHandler or Wish.IExternalUIHandler for chest button integration");
                }

                // Input.GetKeyDown and GetKey → block Backspace from reaching game when Senpai's Chest config is open
                var inputGetKeyDown = AccessTools.Method(typeof(UnityEngine.Input), "GetKeyDown", new[] { typeof(UnityEngine.KeyCode) });
                if (inputGetKeyDown != null)
                {
                    _harmony.Patch(inputGetKeyDown, prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(OnInputGetKeyDown_Prefix))));
                    Log.LogDebug("Patched Input.GetKeyDown (prefix)");
                }
                var inputGetKey = AccessTools.Method(typeof(UnityEngine.Input), "GetKey", new[] { typeof(UnityEngine.KeyCode) });
                if (inputGetKey != null)
                {
                    _harmony.Patch(inputGetKey, prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(OnInputGetKey_Prefix))));
                    Log.LogDebug("Patched Input.GetKey (prefix)");
                }
                var inputGetButtonDown = AccessTools.Method(typeof(UnityEngine.Input), "GetButtonDown", new[] { typeof(string) });
                if (inputGetButtonDown != null)
                {
                    _harmony.Patch(inputGetButtonDown, prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(OnInputGetButtonDown_Prefix))));
                    Log.LogDebug("Patched Input.GetButtonDown (prefix)");
                }

                // Wish.PlayerInput.GetButtonDown(string) → the game uses Rewired via this custom class,
                // and the string overload bypasses the AllowInput/DisableInput system entirely.
                // OnUICancel calls PlayerInput.GetButtonDown("UICancel") which maps Backspace to close
                // the chest. We block it here so Backspace works in the search field without closing the chest.
                var wishPlayerInputType = AccessTools.TypeByName("Wish.PlayerInput");
                if (wishPlayerInputType != null)
                {
                    var getButtonDownStr = AccessTools.Method(wishPlayerInputType, "GetButtonDown", new[] { typeof(string) });
                    if (getButtonDownStr != null)
                    {
                        _harmony.Patch(getButtonDownStr, prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(OnPlayerInputGetButtonDown_Prefix))));
                        Log.LogDebug("Patched PlayerInput.GetButtonDown(string) (prefix)");
                    }
                    else
                    {
                        Log.LogWarning("Could not find method PlayerInput.GetButtonDown(string)");
                    }
                }
                else
                {
                    Log.LogWarning("Could not find Wish.PlayerInput type for UICancel blocking");
                }

                // Chest Labels (labels above chests - enabled via config)
                ApplyChestLabelPatches();

                Log.LogDebug("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to apply patches: {ex}");
            }
        }

        private void ApplyChestLabelPatches()
        {
            var patchType = typeof(SenpaisChest.ChestLabels.ChestLabelPatch);
            var setMetaPostfix = AccessTools.Method(patchType, "SetMeta_Postfix");
            var onEnablePostfix = AccessTools.Method(patchType, "OnEnable_Postfix");
            var interactionPostfix = AccessTools.Method(patchType, "InteractionPoint_Postfix");

            foreach (var methodName in new[] { "SetMeta", "ReceiveNewMeta", "SaveMeta" })
            {
                var target = AccessTools.Method(typeof(Chest), methodName);
                if (target != null)
                {
                    _harmony.Patch(target, postfix: new HarmonyMethod(setMetaPostfix));
                    Log.LogDebug($"Patched Chest.{methodName} (labels)");
                }
            }

            var onEnable = AccessTools.Method(typeof(Chest), "OnEnable");
            if (onEnable != null)
            {
                _harmony.Patch(onEnable, postfix: new HarmonyMethod(onEnablePostfix));
                Log.LogDebug("Patched Chest.OnEnable (labels)");
            }

            var getInteractionPoint = AccessTools.Method(typeof(Chest), "get_InteractionPoint");
            if (getInteractionPoint != null)
            {
                _harmony.Patch(getInteractionPoint, postfix: new HarmonyMethod(interactionPostfix));
                Log.LogDebug("Patched Chest.get_InteractionPoint (labels)");
            }
        }

        private void PatchMethod(Type targetType, string methodName,
            Type patchType, string patchMethodName, Type[] parameters = null)
        {
            var original = parameters != null
                ? AccessTools.Method(targetType, methodName, parameters)
                : AccessTools.Method(targetType, methodName);

            if (original == null)
            {
                Log.LogWarning($"Could not find method {targetType.Name}.{methodName}");
                return;
            }

            var postfix = AccessTools.Method(patchType, patchMethodName);
            _harmony.Patch(original, postfix: new HarmonyMethod(postfix));
            Log.LogDebug($"Patched {targetType.Name}.{methodName}");
        }

        private static void OnPlayerInitialized(Player __instance)
        {
            try
            {
                if (__instance != Player.Instance)
                    return;

                EnsureUIComponentsExist();

                // Use GameSave.CurrentCharacter (static property, no SingletonBehaviour needed)
                string characterName = null;
                var currentChar = GameSave.CurrentCharacter;
                if (currentChar != null)
                {
                    characterName = currentChar.characterName;
                }

                if (string.IsNullOrEmpty(characterName))
                {
                    Log?.LogWarning("Player initialized but no character name found");
                    return;
                }

                Log?.LogInfo($"Player initialized: {characterName}");

                if (string.Equals(_gameplaySessionCharacter, characterName, StringComparison.Ordinal))
                {
                    Log?.LogDebug($"[SenpaisChest] Skip disk reload on player init — '{characterName}' already active");
                    return;
                }

                _museumTodoIntegration?.Reset();
                _staticManager?.SetCharacterName(characterName);

                var data = _staticSaveSystem?.Load(characterName);
                _staticManager?.LoadData(data);
                _gameplaySessionCharacter = characterName;
            }
            catch (Exception ex)
            {
                Log?.LogError($"Error in OnPlayerInitialized: {ex}");
            }
        }

        private static void OnChestInteract(Chest __instance, int interactType)
        {
            if (interactType != 0)
                return;

            CurrentInteractingChest = __instance;

            // Schedule adding "Senpai's Chest" button to the chest panel (we know a chest was just opened)
            try
            {
                var chestType = __instance.GetType();
                var uiField = chestType.GetField("ui", BindingFlags.NonPublic | BindingFlags.Instance);
                if (uiField != null && uiField.GetValue(__instance) is GameObject chestUi && chestUi != null)
                {
                    _pendingChestPanel = chestUi;
                    _pendingChest = __instance;
                    _pendingChestButtonFrames = 3; // Wait 3 frames for UI to be fully initialized
                    Log?.LogInfo($"[SenpaisChest] Scheduled embedded panel from Chest.Interact (will add in 3 frames) - UI active: {chestUi.activeInHierarchy}");
                }
            }
            catch (Exception ex)
            {
                Log?.LogDebug($"[SenpaisChest] Could not schedule chest button: {ex.Message}");
            }
        }

        private static bool OnInputGetKeyDown_Prefix(UnityEngine.KeyCode key, ref bool __result)
        {
            if (key != UnityEngine.KeyCode.Backspace)
                return true;
            if (!SmartChestConfig.StaticBlockInputWhenTyping)
                return true;
            if (!SmartChestUI.ShouldBlockGameInput(_staticUI))
                return true;
            __result = false;
            return false;
        }

        private static bool OnInputGetKey_Prefix(UnityEngine.KeyCode key, ref bool __result)
        {
            if (key != UnityEngine.KeyCode.Backspace)
                return true;
            if (!SmartChestConfig.StaticBlockInputWhenTyping)
                return true;
            if (!SmartChestUI.ShouldBlockGameInput(_staticUI))
                return true;
            __result = false;
            return false;
        }

        private static bool OnInputGetButtonDown_Prefix(string buttonName, ref bool __result)
        {
            if (string.IsNullOrEmpty(buttonName))
                return true;
            if (!buttonName.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
                return true;
            if (!SmartChestConfig.StaticBlockInputWhenTyping)
                return true;
            if (!SmartChestUI.ShouldBlockGameInput(_staticUI))
                return true;
            __result = false;
            return false;
        }

        /// <summary>
        /// Blocks Wish.PlayerInput.GetButtonDown(string) for "UICancel" when the user is typing in our search field.
        /// Only blocks when our UI has keyboard focus so the cheat enabler chat and other mods can receive input.
        /// </summary>
        private static bool OnPlayerInputGetButtonDown_Prefix(string button, ref bool __result)
        {
            if (!SmartChestConfig.StaticBlockInputWhenTyping)
                return true;
            if (!SmartChestUI.ShouldBlockGameInput(_staticUI))
                return true;
            if (button == "UICancel" || button == "Close")
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static bool OnChestEndInteract_Prefix(Chest __instance, int interactType)
        {
            if (CurrentInteractingChest == __instance && _staticUI != null && _staticUI.IsVisible)
            {
                return false;
            }

            // Allow chest to close normally and clean up
            if (CurrentInteractingChest == __instance)
            {
                CurrentInteractingChest = null;
                _staticUI?.Hide();
            }
            return true;
        }

        /// <summary>
        /// After the game opens a chest UI, schedule adding a "Senpai's Chest" button (deferred so ChestUI.Start has run).
        /// </summary>
        private static void OnUIHandlerOpenUI_Postfix(object __instance, GameObject ui, Transform parent, bool playAudio, bool animate, object externalUIHandler)
        {
            if (ui == null || externalUIHandler == null) return;
            if (!(externalUIHandler is Chest chest)) return;

            try
            {
                _pendingChestPanel = ui;
                _pendingChest = chest;
                _pendingChestButtonFrames = 3; // add panel after 3 frames so ChestUI.Start and layout are fully ready
                Log?.LogInfo($"[SenpaisChest] Scheduled embedded panel (will add in 3 frames) - UI active: {ui.activeInHierarchy}");
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[SenpaisChest] Failed to schedule embedded panel: {ex.Message}");
            }
        }

        private static GameObject _pendingChestPanel;
        private static Chest _pendingChest;
        private static int _pendingChestButtonFrames;

        internal static void ProcessPendingChestButton()
        {
            if (_pendingChestButtonFrames <= 0 || _pendingChestPanel == null || _pendingChest == null)
                return;
            _pendingChestButtonFrames--;
            if (_pendingChestButtonFrames != 0) return;

            var panel = _pendingChestPanel;
            var c = _pendingChest;
            
            Log?.LogInfo($"[SenpaisChest] Processing pending panel: panel={panel != null}, active={panel?.activeInHierarchy}, chest={c != null}");
            
            if (panel == null || !panel.activeInHierarchy)
            {
                Log?.LogInfo($"[SenpaisChest] Skip add panel: panel null or inactive (panel={panel != null}, active={panel?.activeInHierarchy})");
                _pendingChestPanel = null;
                _pendingChest = null;
                return;
            }

            // Verify chest UI components exist
            var canvas = panel.GetComponentInParent<Canvas>();
            Log?.LogInfo($"[SenpaisChest] Chest UI state - Canvas: {canvas != null}, Canvas active: {canvas?.gameObject.activeInHierarchy}, RenderMode: {canvas?.renderMode}");

            try
            {
                AddSenpaisChestNotePanel(panel, c);
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[SenpaisChest] Failed to add embedded panel: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _pendingChestPanel = null;
                _pendingChest = null;
            }
        }

        private const string SenpaisChestConfigPanelName = "SenpaisChest_ConfigPanel";

        /// <summary>
        /// Adds a note panel to the chest UI: "Press F9 to configure rules". Config opens only via F9 as a movable floating window.
        /// </summary>
        private static void AddSenpaisChestNotePanel(GameObject chestUiRoot, Chest chest)
        {
            if (chestUiRoot == null || !chestUiRoot.activeInHierarchy)
            {
                Log?.LogDebug($"[SenpaisChest] Cannot add panel: chestUiRoot is null or inactive (null={chestUiRoot == null}, active={chestUiRoot?.activeInHierarchy})");
                return;
            }
            
            // Check if panel already exists
            var existingPanel = chestUiRoot.transform.Find(SenpaisChestConfigPanelName);
            if (existingPanel != null)
            {
                Log?.LogInfo($"[SenpaisChest] Panel already exists, reusing - active: {existingPanel.gameObject.activeInHierarchy}");
                var ui = GetUI();
                if (ui != null)
                {
                    ui.ShowNoteForChest(chest, existingPanel.gameObject);
                }
                return;
            }

            // Find the Canvas that contains the chest UI (required for proper rendering)
            var canvas = chestUiRoot.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Log?.LogWarning("[SenpaisChest] Chest UI root is not under a Canvas, cannot create embedded panel");
                return;
            }

            // Parent the panel to the chest UI root so it sits at the bottom of the chest window,
            // below the inventory grids, instead of at the bottom of the full screen.
            Transform parentContainer = chestUiRoot.transform;

            try
            {
                var panelGo = new GameObject(SenpaisChestConfigPanelName);
                panelGo.transform.SetParent(parentContainer, false);
                panelGo.transform.SetAsLastSibling();
                panelGo.SetActive(true); // Ensure it's active

                var rt = panelGo.AddComponent<RectTransform>();
                // Anchor to bottom-center of chest UI; compact note: "Press F9 to configure rules"
                const float notePanelHeight = 34f;
                const float notePanelWidth = 340f;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(notePanelWidth, notePanelHeight);
                
                Log?.LogInfo($"[SenpaisChest] Panel RectTransform - anchorMin={rt.anchorMin}, anchorMax={rt.anchorMax}, sizeDelta={rt.sizeDelta}, rect={rt.rect}");

                // Ensure the panel doesn't block raycasts (so it doesn't interfere with chest UI interaction)
                var canvasGroup = panelGo.AddComponent<CanvasGroup>();
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                canvasGroup.alpha = 1f; // Ensure it's fully visible

                // Force layout update to ensure RectTransform is properly calculated
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

                // Verify the rect is valid before showing embedded UI
                if (rt.rect.width <= 0 || rt.rect.height <= 0)
                {
                    Log?.LogWarning($"[SenpaisChest] Panel rect is invalid (width={rt.rect.width}, height={rt.rect.height}), waiting one more frame");
                    // Schedule retry after one more frame
                    _pendingChestPanel = chestUiRoot;
                    _pendingChest = chest;
                    _pendingChestButtonFrames = 1;
                    return;
                }

                var ui = GetUI();
                if (ui != null)
                {
                    ui.ShowNoteForChest(chest, panelGo);
                    Log?.LogInfo($"[SenpaisChest] Added note panel (Press F9 to configure) - chestUiRoot active: {chestUiRoot.activeInHierarchy}, panel active: {panelGo.activeInHierarchy}, rect: {rt.rect}");
                }
                else
                {
                    Log?.LogWarning("[SenpaisChest] UI component is null, cannot show embedded panel");
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[SenpaisChest] Exception creating embedded panel: {ex}");
            }
        }

        #endregion

        #region Scene Management

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool isMenuScene = scene.name == "MainMenu" || scene.name == "Menu";

            if (isMenuScene && !_wasInMenuScene)
            {
                Log.LogInfo("Returned to menu, saving data...");
                _staticSaveSystem?.Save();
                _gameplaySessionCharacter = null;
            }
            else if (!isMenuScene)
            {
                // Only load from disk when the session character changes (e.g. picked "Load" from menu).
                // Reloading on every map transition wiped in-memory rules before the next auto-save.
                var characterName = GetCurrentCharacterName();
                if (!string.IsNullOrEmpty(characterName))
                {
                    if (string.Equals(_gameplaySessionCharacter, characterName, StringComparison.Ordinal))
                    {
                        Log?.LogDebug($"[SenpaisChest] Skip disk reload on scene '{scene.name}' — '{characterName}' already active");
                    }
                    else
                    {
                        _staticManager?.SetCharacterName(characterName);
                        var data = _staticSaveSystem?.Load(characterName);
                        _staticManager?.LoadData(data);
                        _gameplaySessionCharacter = characterName;
                        Log?.LogDebug($"[SenpaisChest] Loaded smart chest data from disk for '{characterName}' on scene load");
                    }
                }
            }

            _wasInMenuScene = isMenuScene;
        }

        #endregion

        #region Lifecycle

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _museumTodoIntegration?.Dispose();
            _museumTodoIntegration = null;

            // Do NOT unpatch Harmony — patches survive and enable reloads
            Log?.LogDebug("Plugin OnDestroy called — static references preserved");
            _staticSaveSystem?.Save();
        }

        private void OnApplicationQuit()
        {
            Log?.LogInfo("Application quitting — saving data");
            _staticSaveSystem?.Save();
        }

        #endregion

        #region Static Accessors (for PersistentRunner)

        internal static SmartChestManager GetManager() => _staticManager;
        internal static SmartChestSaveSystem GetSaveSystem() => _staticSaveSystem;

        /// <summary>Returns the current in-game character name so saves always write to the correct file.</summary>
        internal static string GetCurrentCharacterName()
        {
            try
            {
                var currentChar = GameSave.CurrentCharacter;
                return currentChar?.characterName;
            }
            catch (Exception ex)
            {
                Log?.LogDebug($"[SenpaisChest] GetCurrentCharacterName: {ex.Message}");
                return null;
            }
        }

        internal static SmartChestUI GetUI() => _staticUI;
        internal static SmartChestConfig GetConfig() => _staticConfig;
        internal static MuseumTodoIntegration GetMuseumTodoIntegration() => _museumTodoIntegration;

        #endregion
    }

    /// <summary>
    /// Hidden MonoBehaviour that survives game cleanup. Handles scan timer, hotkeys, and auto-save.
    /// </summary>
    public class SmartChestPersistentRunner : MonoBehaviour
    {
        private float _scanTimer;
        private float _autoSaveTimer;
        private const float AUTO_SAVE_INTERVAL = 300f; // 5 minutes
        private int _lastCountdownSecond = -1;

        private void Update()
        {
            Plugin.ProcessPendingChestButton();

            var config = Plugin.GetConfig();
            var manager = Plugin.GetManager();

            if (config == null || manager == null)
                return;

            float dt = Time.unscaledDeltaTime;
            float scanInterval = config.GetScanInterval();

            // Scan timer
            _scanTimer += dt;

            // Countdown logging: log each second from 10 down to 1
            float timeRemaining = scanInterval - _scanTimer;
            int secondsRemaining = (int)Math.Ceiling(timeRemaining);
            if (secondsRemaining >= 1 && secondsRemaining <= 10 && secondsRemaining != _lastCountdownSecond)
            {
                _lastCountdownSecond = secondsRemaining;
                if (SmartChestConfig.StaticEnableScanCountdownDebug)
                    Plugin.Log?.LogDebug($"[Scan] Next scan in {secondsRemaining}...");
            }

            if (_scanTimer >= scanInterval)
            {
                _scanTimer = 0f;
                _lastCountdownSecond = -1;
                try
                {
                    manager.ExecuteScan(
                        config.MaxItemsPerScan.Value,
                        config.EnableNotifications.Value);

                    // After scan, check for museum items in chests
                    Plugin.GetMuseumTodoIntegration()?.OnScanComplete();
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"Error during scan: {ex}");
                }
            }

            // Auto-save timer
            _autoSaveTimer += dt;
            if (_autoSaveTimer >= AUTO_SAVE_INTERVAL)
            {
                _autoSaveTimer = 0f;
                if (manager.IsDirty)
                {
                    Plugin.GetSaveSystem()?.Save();
                }
            }

            // Hotkey detection
            DetectHotkey(config);
        }

        private void DetectHotkey(SmartChestConfig config)
        {
            if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                return;

            var toggleKey = SmartChestConfig.StaticToggleKey;
            var requireCtrl = SmartChestConfig.StaticRequireCtrl;

            if (Input.GetKeyDown(toggleKey))
            {
                if (requireCtrl && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                    return;

                var ui = Plugin.GetUI();
                if (ui == null)
                    return;

                // Only toggle if player is interacting with a chest
                if (Plugin.CurrentInteractingChest != null)
                {
                    ui.ToggleForChest(Plugin.CurrentInteractingChest);
                }
            }
        }

        private void OnDestroy()
        {
            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
            {
                Plugin.Log?.LogDebug("[PersistentRunner] OnDestroy during app quit/menu unload (expected).");
            }
            else
            {
                Plugin.Log?.LogWarning("[PersistentRunner] OnDestroy outside quit/menu (unexpected).");
            }
        }
    }
}
