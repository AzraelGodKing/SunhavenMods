using System;
using SunhavenMods.Shared;
using TheVault.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheVault
{
    /// <summary>
    /// A separate MonoBehaviour that runs on a hidden GameObject.
    /// This survives the game's UIHandler.UnloadGame cleanup because:
    /// 1. It's marked DontDestroyOnLoad
    /// 2. It's hidden from Unity's hierarchy (HideFlags)
    /// 3. It's not a child of any game object the cleanup knows about
    /// </summary>
    public class PersistentUpdateRunner : MonoBehaviour
    {
        private string _lastKnownScene = "";
        private bool _wasInMenuScene = true;
        private float _sceneCheckTimer = 0f;
        private float _heartbeatTimer = 0f;
        private float _characterSyncTimer = 0f;
        private int _heartbeatCount = 0;

        private const float SCENE_CHECK_INTERVAL = 0.5f;
        private const float HEARTBEAT_INTERVAL = 30f;
        private const float CHARACTER_SYNC_INTERVAL = 0.75f;

        private void Awake()
        {
            // Hide this object from the game's cleanup routines
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            Plugin.Log?.LogDebug("[PersistentRunner] Created hidden persistent runner");
        }

        private void Update()
        {
            Plugin.TickAutoSave();

            // Deck layout needs a valid Screen size; Awake may run before that.
            Plugin.TryApplyDeckLayoutDefaults();

            // Poll for menu scene changes
            _sceneCheckTimer += Time.deltaTime;
            if (_sceneCheckTimer >= SCENE_CHECK_INTERVAL)
            {
                _sceneCheckTimer = 0f;
                CheckForMenuSceneChange();
            }

            // Heartbeat (debug — long sessions should not spam Info)
            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= HEARTBEAT_INTERVAL)
            {
                _heartbeatTimer = 0f;
                _heartbeatCount++;
                Plugin.Log?.LogDebug(
                    $"[PersistentRunner Heartbeat #{_heartbeatCount}] Scene: {_lastKnownScene}, VaultLoaded: {PlayerPatches.IsVaultLoaded}, Character: {PlayerPatches.LoadedCharacterName ?? "none"}");
            }

            // Handle hotkey detection for Vault UI (since VaultUI might be destroyed)
            CheckHotkeys();

            // Drain auto-deposit notifications off the pickup path (reduces lag)
            if (PlayerPatches.IsVaultLoaded)
                ItemPatches.DrainAutoDepositNotifications();

            // Character-switch survival fallback: if any hook missed, detect and correct active vault.
            _characterSyncTimer += Time.deltaTime;
            if (_characterSyncTimer >= CHARACTER_SYNC_INTERVAL)
            {
                _characterSyncTimer = 0f;
                PlayerPatches.TrySynchronizeCharacterContext();
            }
        }

        private void CheckHotkeys()
        {
            try
            {
                if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                    return;

                if (Plugin.GetVaultUI() == null) return;

                // Check for vault toggle key (with modifier)
                bool modifierHeld = !Plugin.StaticRequireCtrl ||
                    Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                if (modifierHeld && Input.GetKeyDown(Plugin.StaticToggleKey))
                {
                    Plugin.ToggleMainVaultWindow();
                }

                // Check for alternative toggle key (no modifier - for Steam Deck)
                if (Plugin.StaticAltToggleKey != KeyCode.None && Input.GetKeyDown(Plugin.StaticAltToggleKey))
                {
                    Plugin.ToggleMainVaultWindow();
                }

                // Controller open/toggle (default LB+Y = JoystickButton4 held + JoystickButton3 down)
                if (Plugin.StaticControllerOpenEnabled
                    && Plugin.StaticControllerOpenModifier != KeyCode.None
                    && Plugin.StaticControllerOpenButton != KeyCode.None
                    && Input.GetKey(Plugin.StaticControllerOpenModifier)
                    && Input.GetKeyDown(Plugin.StaticControllerOpenButton))
                {
                    Plugin.ToggleMainVaultWindow();
                }

                // Check for HUD toggle key
                if (Input.GetKeyDown(Plugin.StaticHUDToggleKey))
                {
                    var vaultHUD = Plugin.GetVaultHUD();
                    vaultHUD?.Toggle();
                }

                if (Plugin.StaticQuickConvertKey != KeyCode.None && Input.GetKeyDown(Plugin.StaticQuickConvertKey))
                {
                    Plugin.TryQuickConvertHotkey();
                }

            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[PersistentRunner] Hotkey error: {ex.Message}");
            }
        }

        private void CheckForMenuSceneChange()
        {
            try
            {
                var activeScene = SceneManager.GetActiveScene();
                string sceneName = activeScene.name;

                if (sceneName != _lastKnownScene)
                {
                    Plugin.Log?.LogDebug($"[PersistentRunner] Scene changed: '{_lastKnownScene}' -> '{sceneName}'");
                    _lastKnownScene = sceneName;

                    string sceneLower = sceneName.ToLowerInvariant();
                    bool isMenuScene = sceneLower.Contains("menu") || sceneLower.Contains("title");

                    if (isMenuScene && !_wasInMenuScene)
                    {
                        Plugin.Log?.LogDebug($"[PersistentRunner] Menu scene detected: {sceneName}");
                        PlayerPatches.SaveAndReset();
                    }

                    _wasInMenuScene = isMenuScene;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[PersistentRunner] Error: {ex.Message}");
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
