using HavensMirror.Config;
using HavensMirror.Gallery;
using SunhavenMods.Shared;
using UnityEngine;

namespace HavensMirror.Hotkeys
{
    /// <summary>
    /// DDOL runner that keeps gallery folder sync and reload hotkey alive after early
    /// BepInEx Plugin.OnDestroy (empty-scene teardown).
    /// </summary>
    public sealed class MirrorPersistentRunner : PersistentRunnerBase
    {
        private const float SyncIntervalSeconds = 5f;
        private const float SyncSlowIntervalSeconds = 30f;

        private float _nextScan;
        private int _passes;

        protected override string RunnerName => "MirrorPersistentRunner";

        protected override void OnUpdate()
        {
            if (MirrorOptions.Enabled == null || !MirrorOptions.Enabled.Value)
                return;

            CheckReloadHotkey();
            TickFolderSync();
        }

        protected override void OnGameTransition()
        {
            Plugin.ReloadGallery(notify: false);
        }

        protected override void Log(string message)
        {
            Plugin.Log?.LogInfo(message);
        }

        protected override void LogWarning(string message)
        {
            Plugin.Log?.LogWarning(message);
        }

        private void CheckReloadHotkey()
        {
            KeyCode key = MirrorOptions.ReloadKey?.Value ?? KeyCode.None;
            if (key == KeyCode.None)
                return;

            if (!UnityEngine.Input.GetKeyDown(key))
                return;

            KeyCode modifier = MirrorOptions.ReloadModifier?.Value ?? KeyCode.None;
            if (modifier != KeyCode.None && !IsModifierHeld(modifier))
                return;

            Plugin.Log?.LogInfo("[Gallery] Reload hotkey pressed.");
            Plugin.ReloadGallery(notify: MirrorOptions.NotifyOnReload?.Value ?? true);
        }

        /// <summary>
        /// Treat left/right variants of Ctrl, Shift, Alt and Cmd as the same key, so the default
        /// LeftControl also accepts Right Ctrl.
        /// </summary>
        private static bool IsModifierHeld(KeyCode modifier)
        {
            if (UnityEngine.Input.GetKey(modifier))
                return true;

            switch (modifier)
            {
                case KeyCode.LeftControl: return UnityEngine.Input.GetKey(KeyCode.RightControl);
                case KeyCode.RightControl: return UnityEngine.Input.GetKey(KeyCode.LeftControl);
                case KeyCode.LeftShift: return UnityEngine.Input.GetKey(KeyCode.RightShift);
                case KeyCode.RightShift: return UnityEngine.Input.GetKey(KeyCode.LeftShift);
                case KeyCode.LeftAlt: return UnityEngine.Input.GetKey(KeyCode.RightAlt);
                case KeyCode.RightAlt: return UnityEngine.Input.GetKey(KeyCode.LeftAlt);
                case KeyCode.LeftCommand: return UnityEngine.Input.GetKey(KeyCode.RightCommand);
                case KeyCode.RightCommand: return UnityEngine.Input.GetKey(KeyCode.LeftCommand);
                default: return false;
            }
        }

        private void TickFolderSync()
        {
            if (Time.unscaledTime < _nextScan)
                return;

            _nextScan = Time.unscaledTime + SyncIntervalSeconds;
            _passes++;

            int created = SaveSlotFolderSync.EnsureCharacterFolders();
            if (created > 0)
                Plugin.Log?.LogInfo($"[Gallery] Save-slot sync created {created} folder(s).");

            // After many quiet passes, slow down (still catch new characters / new saves).
            if (_passes > 24 && created == 0)
                _nextScan = Time.unscaledTime + SyncSlowIntervalSeconds;
        }
    }
}
