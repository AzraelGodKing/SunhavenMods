using System;
using BepInEx.Configuration;
using UnityEngine;

namespace HavensAlmanac.Config
{
    public static class AlmanacConfig
    {
        // Config entries
        public static ConfigEntry<KeyCode> DashboardToggleKey { get; private set; }
        public static ConfigEntry<bool> DashboardRequireCtrl { get; private set; }
        public static ConfigEntry<KeyCode> HUDToggleKey { get; private set; }
        public static ConfigEntry<bool> HUDEnabled { get; private set; }
        public static ConfigEntry<float> HUDPositionX { get; private set; }
        public static ConfigEntry<float> HUDPositionY { get; private set; }
        public static ConfigEntry<bool> BriefingEnabled { get; private set; }
        public static ConfigEntry<float> BriefingAutoDismissSeconds { get; private set; }
        public static ConfigEntry<bool> CheckForUpdates { get; private set; }
        public static ConfigEntry<float> UIScale { get; private set; }

        public static ConfigEntry<bool> RelationshipsEnabled { get; private set; }
        public static ConfigEntry<bool> RelationshipsRomanceOnly { get; private set; }
        public static ConfigEntry<RelationshipSortMode> RelationshipsSortMode { get; private set; }
        public static ConfigEntry<bool> RelationshipsShowGiftBadge { get; private set; }

        // Static values for PersistentRunner access
        internal static KeyCode StaticDashboardToggleKey = KeyCode.F5;
        internal static bool StaticDashboardRequireCtrl = true;
        internal static KeyCode StaticHUDToggleKey = KeyCode.F4;
        internal static bool StaticHUDEnabled = true;
        internal static float StaticHUDPositionX = -1f;
        internal static float StaticHUDPositionY = -1f;
        internal static bool StaticBriefingEnabled = true;
        internal static float StaticBriefingAutoDismiss = 0f;
        internal static float StaticUIScale = 1f;

        private static EventHandler _dashboardToggleKeyChanged;
        private static EventHandler _dashboardRequireCtrlChanged;
        private static EventHandler _hudToggleKeyChanged;
        private static EventHandler _hudEnabledChanged;
        private static EventHandler _briefingEnabledChanged;
        private static EventHandler _briefingAutoDismissChanged;
        private static EventHandler _uiScaleChanged;

        public static void Initialize(ConfigFile config)
        {
            DashboardToggleKey = config.Bind(
                "Hotkeys", "DashboardToggleKey", KeyCode.F5,
                "Key to toggle the full dashboard");
            StaticDashboardToggleKey = DashboardToggleKey.Value;
            _dashboardToggleKeyChanged = (_, _) => StaticDashboardToggleKey = DashboardToggleKey.Value;
            DashboardToggleKey.SettingChanged += _dashboardToggleKeyChanged;

            DashboardRequireCtrl = config.Bind(
                "Hotkeys", "DashboardRequireCtrl", true,
                "Require Ctrl modifier for dashboard toggle");
            StaticDashboardRequireCtrl = DashboardRequireCtrl.Value;
            _dashboardRequireCtrlChanged = (_, _) => StaticDashboardRequireCtrl = DashboardRequireCtrl.Value;
            DashboardRequireCtrl.SettingChanged += _dashboardRequireCtrlChanged;

            HUDToggleKey = config.Bind(
                "Hotkeys", "HUDToggleKey", KeyCode.F4,
                "Key to toggle HUD visibility");
            StaticHUDToggleKey = HUDToggleKey.Value;
            _hudToggleKeyChanged = (_, _) => StaticHUDToggleKey = HUDToggleKey.Value;
            HUDToggleKey.SettingChanged += _hudToggleKeyChanged;

            HUDEnabled = config.Bind(
                "HUD", "Enabled", true,
                "Show the compact HUD");
            StaticHUDEnabled = HUDEnabled.Value;
            _hudEnabledChanged = (_, _) => StaticHUDEnabled = HUDEnabled.Value;
            HUDEnabled.SettingChanged += _hudEnabledChanged;

            HUDPositionX = config.Bind(
                "HUD", "PositionX", -1f,
                "HUD X position (-1 for default)");
            StaticHUDPositionX = HUDPositionX.Value;

            HUDPositionY = config.Bind(
                "HUD", "PositionY", -1f,
                "HUD Y position (-1 for default)");
            StaticHUDPositionY = HUDPositionY.Value;

            BriefingEnabled = config.Bind(
                "DailyBriefing", "Enabled", true,
                "Show daily briefing when you wake up");
            StaticBriefingEnabled = BriefingEnabled.Value;
            _briefingEnabledChanged = (_, _) => StaticBriefingEnabled = BriefingEnabled.Value;
            BriefingEnabled.SettingChanged += _briefingEnabledChanged;

            BriefingAutoDismissSeconds = config.Bind(
                "DailyBriefing", "AutoDismissSeconds", 0f,
                "Auto-dismiss briefing after this many seconds (0 to require manual dismiss)");
            StaticBriefingAutoDismiss = BriefingAutoDismissSeconds.Value;
            _briefingAutoDismissChanged = (_, _) => StaticBriefingAutoDismiss = BriefingAutoDismissSeconds.Value;
            BriefingAutoDismissSeconds.SettingChanged += _briefingAutoDismissChanged;

            CheckForUpdates = config.Bind(
                "Updates", "CheckForUpdates", true,
                "Check for mod updates on startup");

            UIScale = config.Bind(
                "Display", "UIScale", 1f,
                new BepInEx.Configuration.ConfigDescription(
                    "Scale factor for Almanac HUD, Dashboard, and Daily Briefing (1.0 = default)",
                    new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 2.5f)
                ));
            StaticUIScale = Mathf.Clamp(UIScale.Value, 0.5f, 2.5f);
            _uiScaleChanged = (_, _) =>
            {
                StaticUIScale = Mathf.Clamp(UIScale.Value, 0.5f, 2.5f);
                Plugin.Instance?.ApplyUIScaleToAllUI();
            };
            UIScale.SettingChanged += _uiScaleChanged;

            RelationshipsEnabled = config.Bind(
                "Relationships", "Enabled", true,
                "Show the built-in Relationships dashboard section (native game heart data).");
            RelationshipsRomanceOnly = config.Bind(
                "Relationships", "RomanceOnly", false,
                "Only list romanceable NPCs in the Relationships section.");
            RelationshipsSortMode = config.Bind(
                "Relationships", "SortBy", RelationshipSortMode.Name,
                "Sort order for the Relationships dashboard list.");
            RelationshipsShowGiftBadge = config.Bind(
                "Relationships", "ShowUngiftedBadge", true,
                "Show an ungifted-today marker on NPC rows (uses NPCAI.gaveGiftForDay).");
        }

        public static void UnsubscribeSettingChanged()
        {
            if (DashboardToggleKey != null && _dashboardToggleKeyChanged != null)
                DashboardToggleKey.SettingChanged -= _dashboardToggleKeyChanged;
            if (DashboardRequireCtrl != null && _dashboardRequireCtrlChanged != null)
                DashboardRequireCtrl.SettingChanged -= _dashboardRequireCtrlChanged;
            if (HUDToggleKey != null && _hudToggleKeyChanged != null)
                HUDToggleKey.SettingChanged -= _hudToggleKeyChanged;
            if (HUDEnabled != null && _hudEnabledChanged != null)
                HUDEnabled.SettingChanged -= _hudEnabledChanged;
            if (BriefingEnabled != null && _briefingEnabledChanged != null)
                BriefingEnabled.SettingChanged -= _briefingEnabledChanged;
            if (BriefingAutoDismissSeconds != null && _briefingAutoDismissChanged != null)
                BriefingAutoDismissSeconds.SettingChanged -= _briefingAutoDismissChanged;
            if (UIScale != null && _uiScaleChanged != null)
                UIScale.SettingChanged -= _uiScaleChanged;

            _dashboardToggleKeyChanged = null;
            _dashboardRequireCtrlChanged = null;
            _hudToggleKeyChanged = null;
            _hudEnabledChanged = null;
            _briefingEnabledChanged = null;
            _briefingAutoDismissChanged = null;
            _uiScaleChanged = null;
        }
    }
}
