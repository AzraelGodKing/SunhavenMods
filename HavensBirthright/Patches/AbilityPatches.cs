using HavensBirthright.Abilities;
using HarmonyLib;
using SunhavenMods.Shared;
using System;
using System.Collections.Generic;
using Wish;

namespace HavensBirthright.Patches
{
    /// <summary>
    /// Patches for active racial abilities that intercept inventory operations.
    /// Currently implements:
    /// - Fire Elemental's Infernal Forge (auto-smelt ore into bars on pickup)
    /// </summary>
    public static class AbilityPatches
    {
        // ===================== ORE → BAR MAPPING =====================

        /// <summary>
        /// Hardcoded ore ID → bar ID mapping from game's item database.
        /// Ore IDs: 1100-1108, Bar IDs: 1200-1207
        /// </summary>
        internal static readonly Dictionary<int, int> OreToBarMap = new Dictionary<int, int>
        {
            { 1100, 1200 }, // Copper Ore → Copper Bar
            { 1101, 1201 }, // Iron Ore → Iron Bar
            { 1102, 1202 }, // Adamant Ore → Adamant Bar
            { 1103, 1203 }, // Mithril Ore → Mithril Bar
            { 1104, 1204 }, // Sunite Ore → Sunite Bar
            { 1105, 1205 }, // Gold Ore → Gold Bar
            { 1107, 1206 }, // Glorite Ore → Glorite Bar
            { 1108, 1207 }, // Elven Steel Ore → Elven Steel Bar
        };

        // ===================== TIERED MANA COSTS =====================

        /// <summary>
        /// Per-ore mana cost as percentage of max mana per bar smelted.
        /// Scales from cheap (copper) to expensive (elven steel).
        /// </summary>
        internal static readonly Dictionary<int, float> OreManaCostMap = new Dictionary<int, float>
        {
            { 1100, 1f },   // Copper Ore — 1% max mana
            { 1101, 2f },   // Iron Ore — 2%
            { 1105, 3f },   // Gold Ore — 3%
            { 1102, 4f },   // Adamant Ore — 4%
            { 1103, 5f },   // Mithril Ore — 5%
            { 1104, 6f },   // Sunite Ore — 6%
            { 1107, 7f },   // Glorite Ore — 7%
            { 1108, 8f },   // Elven Steel Ore — 8%
        };

        internal static void SendGameNotification(string text, int id = 0, int amount = 0, bool unique = false, bool error = false)
            => GameApis.SendGameNotification(text, id, amount, unique, error);

        /// <summary>Legacy split-reset: both map to <see cref="GameApis"/> item/notification caches.</summary>
        internal static void ResetNotificationCache() => GameApis.ResetItemAndNotificationCaches();

        internal static void ResetReflectionCache()
        {
        }

        // ===================== MANA REGEN BLOCK PATCH =====================

        /// <summary>
        /// PREFIX patch for Player.AddMana(float mana, float overCapAmount).
        /// Blocks ALL mana regeneration while Infernal Forge is toggled ON.
        /// Only applies to Fire Elemental (Infernal Forge ability) — must NOT block
        /// mana for other races (e.g. Cat Amari food/drink/fishing mana restore).
        /// Returns false to skip the original AddMana method entirely (passive regen only).
        /// Explicit restores (food, consumables) pass a positive mana value and are allowed through.
        /// Parameter names must match Wish.Player.AddMana or HarmonyX fails to patch.
        /// </summary>
        public static bool OnPlayerAddManaPrefix(float mana, float overCapAmount)
        {
            if (!RacialConfig.EnableRacialBonuses.Value) return true;
            if (!AbilityConfig.EnableInfernalForge.Value) return true;
            // Must be Fire Elemental (or generic Elemental) — Infernal Forge is Fire-only
            var manager = Plugin.GetRacialBonusManager();
            if (manager == null) return true;
            var race = manager.GetPlayerRace();
            if (!race.HasValue) return true;
            Race infernalRace = ElementalVariantResolver.ResolveElementalAbilityRace(race.Value);
            bool infernalManaLockRace = infernalRace == Race.FireElemental || infernalRace == Race.Elemental;
            if (!infernalManaLockRace) return true; // Never block mana for Water Elemental, other races, etc.
            if (!ActiveAbilityManager.IsRuntimeEnabled(ActiveAbilityManager.InfernalForge)) return true;
            if (mana > 0f) return true; // Allow explicit mana restores (consumables, food, abilities)
            return false; // Block passive regen ticks (mana 0)
        }
    }
}
