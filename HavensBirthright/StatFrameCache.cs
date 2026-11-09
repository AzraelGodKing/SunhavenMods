using HavensBirthright.Abilities;
using SunhavenMods.Shared;
using System;
using UnityEngine;
using Wish;

namespace HavensBirthright
{
    /// <summary>
    /// Per-frame values consumed by <see cref="Patches.StatPatches"/> to avoid reflection on every <c>GetStat</c> call.
    /// </summary>
    internal static class StatFrameCache
    {
        private static float _cachedHPRatio = 1f;
        private static string _cachedSeason;
        private static bool _cachedIsDaytime = true;
        private static bool _cachedIsInMine;
        private static bool _cachedIsSpring;
        private static bool _cachedIsSummer;
        private static bool _cachedIsWinter;
        private static float _cachedQuickLearnerBonus;
        private static bool _cacheValid;
        private static string _lastSceneName;
        private static bool _lastIsInMine;
        private static object _lastSeasonValue;

        public static float CachedHPRatio => _cachedHPRatio;
        public static string CachedSeason => _cachedSeason;
        public static bool CachedIsDaytime => _cachedIsDaytime;
        public static bool CachedIsInMine => _cachedIsInMine;
        public static bool CachedIsSpring => _cachedIsSpring;
        public static bool CachedIsSummer => _cachedIsSummer;
        public static bool CachedIsWinter => _cachedIsWinter;
        public static float CachedQuickLearnerBonus => _cachedQuickLearnerBonus;
        public static bool IsCacheValid => _cacheValid;

        public static void Reset()
        {
            _cacheValid = false;
            _cachedHPRatio = 1f;
            _cachedSeason = null;
            _cachedIsDaytime = true;
            _cachedIsInMine = false;
            _cachedIsSpring = false;
            _cachedIsSummer = false;
            _cachedIsWinter = false;
            _cachedQuickLearnerBonus = 0f;
            _lastSceneName = null;
            _lastIsInMine = false;
            _lastSeasonValue = null;
        }

        public static void Update(Race race, Type dayCycleType)
        {
            try
            {
                _cachedIsInMine = ComputeIsInMine();

                if (dayCycleType != null)
                {
                    var dayCycleInstance = ReflectionHelper.GetSingletonInstance(dayCycleType);
                    if (dayCycleInstance != null)
                    {
                        float hour = ReflectionHelper.TryGetValue<float>(dayCycleInstance, "Hour", -1f);
                        if (hour < 0)
                            hour = ReflectionHelper.TryGetValue<float>(dayCycleInstance, "CurrentHour", -1f);
                        if (hour < 0)
                            hour = ReflectionHelper.TryGetValue<float>(dayCycleInstance, "currentHour", -1f);
                        if (hour < 0)
                        {
                            int hourInt = ReflectionHelper.TryGetValue<int>(dayCycleInstance, "Hour", -1);
                            if (hourInt >= 0) hour = hourInt;
                        }

                        _cachedIsDaytime = hour < 0 || (hour >= 6f && hour < 18f);

                        var season = ReflectionHelper.GetInstanceValue(dayCycleInstance, "Season");
                        if (season == null)
                            season = ReflectionHelper.GetInstanceValue(dayCycleInstance, "season");
                        if (season == null)
                            season = ReflectionHelper.GetInstanceValue(dayCycleInstance, "CurrentSeason");

                        // Season only changes a few times per in-game year; skip ToString + IndexOf when unchanged (AZR-729).
                        if (season == null || !season.Equals(_lastSeasonValue))
                        {
                            _lastSeasonValue = season;
                            _cachedSeason = season?.ToString();
                            _cachedIsSpring = _cachedSeason != null && _cachedSeason.IndexOf("Spring", StringComparison.OrdinalIgnoreCase) >= 0;
                            _cachedIsSummer = _cachedSeason != null && _cachedSeason.IndexOf("Summer", StringComparison.OrdinalIgnoreCase) >= 0;
                            _cachedIsWinter = _cachedSeason != null && _cachedSeason.IndexOf("Winter", StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                    }
                }

                var player = Player.Instance;
                if (player != null)
                {
                    float maxHP = player.MaxHealth;
                    if (maxHP > 0)
                    {
                        float currentHP = ReflectionHelper.TryGetValue<float>(player, "health", maxHP);
                        _cachedHPRatio = currentHP / maxHP;
                    }
                    else
                    {
                        _cachedHPRatio = 1f;
                    }

                    if (race == Race.Human && AbilityConfig.EnableQuickLearner != null && AbilityConfig.EnableQuickLearner.Value)
                        _cachedQuickLearnerBonus = CalculateQuickLearnerBonus(player);
                    else
                        _cachedQuickLearnerBonus = 0f;
                }

                _cacheValid = true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[StatFrameCache] Update error: {ex.Message}");
                _cacheValid = false;
            }
        }

        private static bool ComputeIsInMine()
        {
            string scene = SceneHelpers.GetCurrentSceneName();
            if (scene == _lastSceneName) return _lastIsInMine;
            _lastSceneName = scene;
            string lower = scene.ToLowerInvariant();
            _lastIsInMine = lower.Contains("mine") || lower.Contains("dungeon") ||
                            lower.Contains("cave") || lower.Contains("underground") ||
                            lower.Contains("nelvari") || lower.Contains("withergate");
            return _lastIsInMine;
        }

        private static float CalculateQuickLearnerBonus(Player player)
        {
            try
            {
                int threshold = AbilityConfig.QuickLearnerSkillThreshold.Value;
                int qualifyingSkills = 0;

                float farmingLevel = ReflectionHelper.TryGetValue<float>(player, "FarmingSkillLevel", 0f);
                float miningLevel = ReflectionHelper.TryGetValue<float>(player, "MiningSkillLevel", 0f);
                float fishingLevel = ReflectionHelper.TryGetValue<float>(player, "FishingSkillLevel", 0f);
                float explorationLevel = ReflectionHelper.TryGetValue<float>(player, "ExplorationSkillLevel", 0f);

                if (farmingLevel >= threshold) qualifyingSkills++;
                if (miningLevel >= threshold) qualifyingSkills++;
                if (fishingLevel >= threshold) qualifyingSkills++;
                if (explorationLevel >= threshold) qualifyingSkills++;

                try
                {
                    var professions = ReflectionHelper.GetInstanceValue(player, "Professions");
                    if (professions != null)
                    {
                        var craftingType = ReflectionHelper.FindWishType("ProfessionType");
                        if (craftingType != null)
                        {
                            var craftingEnum = Enum.Parse(craftingType, "Crafting");
                            if (professions is System.Collections.IDictionary dict && dict.Contains(craftingEnum))
                            {
                                var profession = dict[craftingEnum];
                                int level = ReflectionHelper.TryGetValue<int>(profession, "level", 0);
                                if (level >= threshold) qualifyingSkills++;
                            }
                        }
                    }
                }
                catch { /* skip crafting */ }

                float bonus = qualifyingSkills * AbilityConfig.QuickLearnerBonusPerSkill.Value;
                return Mathf.Min(bonus, AbilityConfig.QuickLearnerMaxBonus.Value);
            }
            catch
            {
                return 0f;
            }
        }
    }
}
