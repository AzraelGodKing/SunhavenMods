using System.Globalization;
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

        public static void Initialize(ConfigFile config)
        {
            DashboardToggleKey = config.Bind(
                "Hotkeys", "DashboardToggleKey", KeyCode.F5,
                "Key to toggle the full dashboard");
            StaticDashboardToggleKey = DashboardToggleKey.Value;
            DashboardToggleKey.SettingChanged += (_, _) => StaticDashboardToggleKey = DashboardToggleKey.Value;

            DashboardRequireCtrl = config.Bind(
                "Hotkeys", "DashboardRequireCtrl", true,
                "Require Ctrl modifier for dashboard toggle");
            StaticDashboardRequireCtrl = DashboardRequireCtrl.Value;
            DashboardRequireCtrl.SettingChanged += (_, _) => StaticDashboardRequireCtrl = DashboardRequireCtrl.Value;

            HUDToggleKey = config.Bind(
                "Hotkeys", "HUDToggleKey", KeyCode.F4,
                "Key to toggle HUD visibility");
            StaticHUDToggleKey = HUDToggleKey.Value;
            HUDToggleKey.SettingChanged += (_, _) => StaticHUDToggleKey = HUDToggleKey.Value;

            HUDEnabled = config.Bind(
                "HUD", "Enabled", true,
                "Show the compact HUD");
            StaticHUDEnabled = HUDEnabled.Value;
            HUDEnabled.SettingChanged += (_, _) => StaticHUDEnabled = HUDEnabled.Value;

            HUDPositionX = config.Bind(
                "HUD", "PositionX", -1f,
                "HUD X position (-1 for default)");
            StaticHUDPositionX = ReadInvariantFloat(HUDPositionX);

            HUDPositionY = config.Bind(
                "HUD", "PositionY", -1f,
                "HUD Y position (-1 for default)");
            StaticHUDPositionY = ReadInvariantFloat(HUDPositionY);

            BriefingEnabled = config.Bind(
                "DailyBriefing", "Enabled", true,
                "Show daily briefing when you wake up");
            StaticBriefingEnabled = BriefingEnabled.Value;
            BriefingEnabled.SettingChanged += (_, _) => StaticBriefingEnabled = BriefingEnabled.Value;

            BriefingAutoDismissSeconds = config.Bind(
                "DailyBriefing", "AutoDismissSeconds", 0f,
                "Auto-dismiss briefing after this many seconds (0 to require manual dismiss)");
            StaticBriefingAutoDismiss = BriefingAutoDismissSeconds.Value;
            BriefingAutoDismissSeconds.SettingChanged += (_, _) => StaticBriefingAutoDismiss = BriefingAutoDismissSeconds.Value;

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
            UIScale.SettingChanged += (_, _) =>
            {
                StaticUIScale = Mathf.Clamp(UIScale.Value, 0.5f, 2.5f);
                Plugin.Instance?.ApplyUIScaleToAllUI();
            };

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

        /// <summary>
        /// Persist a dragged HUD position with invariant decimals so comma-decimal
        /// locales write "1234.5" instead of "1234,5" (which BepInEx cannot reload).
        /// </summary>
        internal static void SaveHudPosition(float x, float y)
        {
            StaticHUDPositionX = x;
            StaticHUDPositionY = y;
            WriteInvariantFloat(HUDPositionX, x);
            WriteInvariantFloat(HUDPositionY, y);
        }

        private static float ReadInvariantFloat(ConfigEntry<float> entry)
        {
            if (entry == null)
                return -1f;

            string raw = entry.GetSerializedValue();
            if (!string.IsNullOrWhiteSpace(raw) &&
                float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return parsed;

            return entry.Value;
        }

        private static void WriteInvariantFloat(ConfigEntry<float> entry, float value)
        {
            entry?.SetSerializedValue(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
