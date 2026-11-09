using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SunhavenMods.Shared;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wish;

namespace FasterRaces
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log { get; private set; }
        internal static ConfigEntry<bool> EnableMod;
        internal static ConfigEntry<float> SpeedBonusPercent;
        private const float MinSpeedBonusPercent = 0f;
        private const float MaxSpeedBonusPercent = 300f;

        // Per-race overrides. -1 means "use global SpeedBonusPercent".
        internal static readonly Dictionary<string, ConfigEntry<float>> RaceSpeedOverrides
            = new Dictionary<string, ConfigEntry<float>>();

        private static readonly string[] KnownRaces =
        {
            "Human", "Elf", "Angel", "Demon",
            "FireElemental", "WaterElemental", "MagmaElemental", "Shade"
        };

        private Harmony _harmony;
        private bool _applicationQuitting;

        private void Awake()
        {
            Log = Logger;
            var configFile = ConfigFileHelper.CreateNamedConfig(
                PluginInfo.PLUGIN_GUID,
                "FasterRaces.cfg",
                message => Log?.LogWarning(message));
            ConfigFileHelper.ReplacePluginConfig(this, configFile, Log.LogWarning);
            EnableMod = configFile.Bind("General", "Enabled", true,
                "Enable Faster Races movement speed bonus. When enabled, Haven's Birthright will not apply its own movement speed bonuses to avoid double speed.");

            SpeedBonusPercent = configFile.Bind(
                "General",
                "SpeedBonusPercent",
                25f,
                new ConfigDescription(
                    "Global percentage bonus to movement speed (e.g. 25 = +25%). Used for any race that does not have a per-race override set.",
                    new AcceptableValueRange<float>(MinSpeedBonusPercent, MaxSpeedBonusPercent)
                )
            );

            foreach (var race in KnownRaces)
            {
                var entry = configFile.Bind(
                    "PerRace",
                    $"{race}SpeedBonusPercent",
                    -1f,
                    new ConfigDescription(
                        $"Speed bonus % for {race}. Set to -1 to use the global SpeedBonusPercent.",
                        new AcceptableValueRange<float>(-1f, MaxSpeedBonusPercent)
                    )
                );
                RaceSpeedOverrides[race] = entry;
            }

            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            var playerType = typeof(Player);
            var getStat = AccessTools.Method(playerType, "GetStat", new[] { typeof(Wish.StatType) });
            var postfix = AccessTools.Method(typeof(SpeedPatch), nameof(SpeedPatch.Postfix));
            if (getStat != null && postfix != null)
            {
                _harmony.Patch(getStat, postfix: new HarmonyMethod(postfix));
                Log.LogInfo("Patched Player.GetStat for movement speed");
            }
            else
            {
                if (getStat == null)
                    Log.LogWarning("[Reflect] Player.GetStat(StatType) not found - speed bonus inactive. Check game version.");
                if (postfix == null)
                    Log.LogWarning("[Reflect] SpeedPatch.Postfix not found - internal error.");
            }

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded. Global speed bonus: {SpeedBonusPercent.Value}%");
            ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                ModHealthIntegrationSummary.Build(
                    ("DevTools", SuitePluginGuids.DevTools),
                    ("Birthright", SuitePluginGuids.HavensBirthright)),
                mode: EnableMod.Value ? "startup" : "disabled");
        }

        /// <summary>
        /// Compatibility hook for other mods (e.g. Haven's Birthright):
        /// true when this mod is enabled and would apply a positive speed bonus
        /// for the current race (or any configured override when player is unavailable).
        /// </summary>
        public static bool IsSpeedBonusActive
        {
            get
            {
                if (EnableMod == null || !EnableMod.Value)
                    return false;

                string raceName = SpeedPatch.GetCurrentRaceName();
                if (!string.IsNullOrEmpty(raceName))
                    return GetEffectiveSpeedBonus(raceName) > 0f;

                if (SpeedBonusPercent != null && SpeedBonusPercent.Value > 0f)
                    return true;

                foreach (var entry in RaceSpeedOverrides.Values)
                {
                    if (entry.Value > 0f)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Maps game SubRace strings to per-race config keys in KnownRaces.
        /// </summary>
        internal static string NormalizeRaceName(string rawRaceName)
        {
            if (string.IsNullOrEmpty(rawRaceName))
                return rawRaceName;

            if (RaceSpeedOverrides.ContainsKey(rawRaceName))
                return rawRaceName;

            // SubRace enum may use alternate spellings (e.g. spaces, generic Elemental).
            switch (rawRaceName.Replace(" ", ""))
            {
                case "Fire":
                case "FireElement":
                    return "FireElemental";
                case "Water":
                case "WaterElement":
                    return "WaterElemental";
                case "Magma":
                case "MagmaElement":
                    return "MagmaElemental";
                case "ShadeElement":
                    return "Shade";
                default:
                    return rawRaceName;
            }
        }

        /// <summary>
        /// Returns the effective speed bonus % for the given race name.
        /// Falls back to global if no per-race override is configured.
        /// </summary>
        internal static float GetEffectiveSpeedBonus(string raceName)
        {
            raceName = NormalizeRaceName(raceName);
            if (!string.IsNullOrEmpty(raceName)
                && RaceSpeedOverrides.TryGetValue(raceName, out var entry)
                && entry.Value >= 0f)
            {
                return entry.Value;
            }
            return SpeedBonusPercent?.Value ?? 0f;
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void OnDestroy()
        {
            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
                Log?.LogInfo($"[Lifecycle] Plugin OnDestroy during expected teardown (scene: {sceneName})");
            else
                Log?.LogWarning($"[Lifecycle] Plugin OnDestroy outside expected teardown (scene: {sceneName})");

            // Leave patches applied. UnpatchSelf here dropped the movement-speed postfix when this
            // component was destroyed during scene cleanup, so Faster Races stayed dead until
            // restart. Same rule as Birthday Reminder (AZR-437), The Vault, and Gifting Assistant.
        }

        public static class SpeedPatch
        {
            // Cache the SubRace property for fast per-frame access.
            private static PropertyInfo _subRaceProp;
            private static bool _subRaceChecked;
            private static bool _loggedRaceResolveFailure;

            /// <summary>Runs after Haven's Birthright StatPatches.ModifyGetStat (higher Harmony priority = later postfix).</summary>
            [HarmonyPriority(300)]
            public static void Postfix(Wish.StatType stat, ref float __result)
            {
                if (stat != Wish.StatType.Movespeed)
                    return;
                if (EnableMod == null || !EnableMod.Value)
                    return;

                string raceName = GetCurrentRaceName();
                float pct = Mathf.Clamp(Plugin.GetEffectiveSpeedBonus(raceName), MinSpeedBonusPercent, MaxSpeedBonusPercent);
                if (pct <= 0f)
                    return;

                __result *= (1f + pct / 100f);
            }

            internal static string GetCurrentRaceName()
            {
                try
                {
                    var player = Player.Instance;
                    if (player == null) return null;

                    if (!_subRaceChecked)
                    {
                        _subRaceChecked = true;
                        _subRaceProp = player.GetType().GetProperty("SubRace",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    }

                    var subRace = _subRaceProp?.GetValue(player);
                    return subRace?.ToString();
                }
                catch (Exception ex)
                {
                    if (!_loggedRaceResolveFailure)
                    {
                        _loggedRaceResolveFailure = true;
                        Plugin.Log?.LogDebug($"[FasterRaces] GetCurrentRaceName failed: {ex.Message}");
                    }
                    return null;
                }
            }
        }
    }
}
