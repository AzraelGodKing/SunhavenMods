using System;
using BepInEx.Configuration;
using System.Reflection;
using UnityEngine;
using HavenDevTools.UI;
using SunhavenMods.Shared;

namespace HavenDevTools.Config
{
    public static class ModConfig
    {
        // Hotkeys
        public static ConfigEntry<KeyCode> ToggleKey { get; private set; }
        public static ConfigEntry<KeyCode> OverlayToggleKey { get; private set; }

        // Overlay Settings
        public static ConfigEntry<bool> ShowOverlayOnStart { get; private set; }
        public static ConfigEntry<string> OverlayPosition { get; private set; }
        public static ConfigEntry<bool> ShowPerformance { get; private set; }
        public static ConfigEntry<bool> ShowFpsCounter { get; private set; }
        public static ConfigEntry<string> FpsCounterPosition { get; private set; }

        // Log Viewer
        public static ConfigEntry<int> MaxLogEntries { get; private set; }
        public static ConfigEntry<string> LogLevelFilter { get; private set; }

        // Updates
        public static ConfigEntry<bool> CheckForUpdates { get; private set; }

        /// <summary>
        /// The Vault: full inspector (zeros in tabs, Debug dump tab, full HUD). Pushed to TheVault.Plugin at runtime.
        /// </summary>
        public static ConfigEntry<bool> TheVaultFullVaultInspector { get; private set; }

        private static bool _theVaultInspectorHooked;
        private static EventHandler _theVaultInspectorChanged;
        private static Type _cachedTheVaultPluginType;
        private static MethodInfo _cachedSetTheVaultFullInspector;

        /// <summary>
        /// When true, pauses the player while the F11 debug window is open. When false (default), other UI and chat stay usable.
        /// </summary>
        public static ConfigEntry<bool> PauseGameWhenDebugOpen { get; private set; }

        public static ConfigEntry<float> DebugWindowWidth { get; private set; }
        public static ConfigEntry<float> DebugWindowHeight { get; private set; }

        public static void SaveDebugWindowSize(float width, float height)
        {
            if (DebugWindowWidth != null)
                DebugWindowWidth.Value = width;
            if (DebugWindowHeight != null)
                DebugWindowHeight.Value = height;
        }

        public static void Initialize(ConfigFile config)
        {
            // Hotkeys
            ToggleKey = config.Bind(
                "Hotkeys",
                "ToggleKey",
                KeyCode.F11,
                "Key to toggle the debug window"
            );

            OverlayToggleKey = config.Bind(
                "Hotkeys",
                "OverlayToggleKey",
                KeyCode.F6,
                "Key to toggle the debug overlay"
            );

            // Overlay Settings
            ShowOverlayOnStart = config.Bind(
                "Overlay",
                "ShowOnStart",
                false,
                "Show the debug overlay when the game starts"
            );

            OverlayPosition = config.Bind(
                "Overlay",
                "Position",
                "TopRight",
                "Overlay position: TopLeft, TopRight, BottomLeft, BottomRight"
            );

            ShowPerformance = config.Bind(
                "Overlay",
                "ShowPerformance",
                true,
                "Show FPS and memory usage in the debug overlay"
            );

            ShowFpsCounter = config.Bind(
                "Overlay",
                "ShowFpsCounter",
                true,
                "Show a compact FPS counter in a screen corner (independent of the F6 overlay)"
            );

            FpsCounterPosition = config.Bind(
                "Overlay",
                "FpsCounterPosition",
                "TopLeft",
                "FPS counter position: TopLeft, TopRight, BottomLeft, BottomRight"
            );

            // Log Viewer
            MaxLogEntries = config.Bind(
                "LogViewer",
                "MaxLogEntries",
                500,
                "Maximum number of log entries to keep in the in-game log viewer"
            );

            LogLevelFilter = config.Bind(
                "LogViewer",
                "LogLevelFilter",
                "Info",
                "Minimum log level to display: Debug, Info, Warning, Error"
            );

            // Updates
            CheckForUpdates = config.Bind(
                "Updates",
                "CheckForUpdates",
                true,
                "Check for mod updates on startup"
            );

            TheVaultFullVaultInspector = config.Bind(
                "The Vault",
                "FullVaultInspector",
                false,
                "When true: The Vault lists every defined currency (including 0), adds the Debug tab with a raw vault dump, and the HUD shows all slots. Same as the former [Debug] FullVaultInspector in TheVault.cfg (moved here)."
            );

            PauseGameWhenDebugOpen = config.Bind(
                "DebugWindow",
                "PauseGameWhenDebugOpen",
                false,
                "Pause the player while the F11 debug window is open. Leave false so in-game chat and other UI stay usable with the window open."
            );

            DebugWindowWidth = config.Bind(
                "DebugWindow",
                "Width",
                DebugWindowLayout.DefaultWidth,
                "Debug window width in pixels (drag bottom-right corner to resize in-game)."
            );

            DebugWindowHeight = config.Bind(
                "DebugWindow",
                "Height",
                DebugWindowLayout.DefaultHeight,
                "Debug window height in pixels (drag bottom-right corner to resize in-game)."
            );

            if (!_theVaultInspectorHooked)
            {
                _theVaultInspectorChanged = (_, __) => SyncTheVaultFullVaultInspectorToPlugin();
                TheVaultFullVaultInspector.SettingChanged += _theVaultInspectorChanged;
                _theVaultInspectorHooked = true;
            }

            global::HavenDevTools.Plugin.Log?.LogInfo("Configuration initialized");
        }

        public static void UnsubscribeSettingChanged()
        {
            if (TheVaultFullVaultInspector != null && _theVaultInspectorChanged != null)
                TheVaultFullVaultInspector.SettingChanged -= _theVaultInspectorChanged;
            _theVaultInspectorChanged = null;
            _theVaultInspectorHooked = false;
        }

        /// <summary>
        /// Push <see cref="TheVaultFullVaultInspector"/> to The Vault plugin (reflection; no compile reference).
        /// Call after mod detection so <see cref="Plugin.HasTheVault"/> is accurate.
        /// </summary>
        public static void SyncTheVaultFullVaultInspectorToPlugin()
        {
            try
            {
                if (!global::HavenDevTools.Plugin.HasTheVault || TheVaultFullVaultInspector == null) return;

                _cachedTheVaultPluginType ??= ReflectionHelper.FindModPlugin("TheVault");
                if (_cachedTheVaultPluginType == null) return;

                _cachedSetTheVaultFullInspector ??= _cachedTheVaultPluginType.GetMethod(
                    "SetConfigDebugFullVaultInspector",
                    BindingFlags.Public | BindingFlags.Static);
                if (_cachedSetTheVaultFullInspector == null) return;

                _cachedSetTheVaultFullInspector.Invoke(null, new object[] { TheVaultFullVaultInspector.Value });
            }
            catch (Exception ex)
            {
                global::HavenDevTools.Plugin.Log?.LogDebug($"[ModConfig] SyncTheVaultFullVaultInspectorToPlugin: {ex.Message}");
            }
        }

        public static OverlayPositionType GetOverlayPosition()
        {
            return ParseOverlayPosition(OverlayPosition.Value);
        }

        public static OverlayPositionType GetFpsCounterPosition()
        {
            return ParseOverlayPosition(FpsCounterPosition.Value);
        }

        private static OverlayPositionType ParseOverlayPosition(string value)
        {
            return value?.ToLower() switch
            {
                "topleft" => OverlayPositionType.TopLeft,
                "topright" => OverlayPositionType.TopRight,
                "bottomleft" => OverlayPositionType.BottomLeft,
                "bottomright" => OverlayPositionType.BottomRight,
                _ => OverlayPositionType.TopRight
            };
        }
    }

    public enum OverlayPositionType
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
