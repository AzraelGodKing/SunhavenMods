using BepInEx.Bootstrap;
using HavensBirthright.Abilities;
using HavensBirthright;
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Wish;

namespace HavensBirthright.Patches
{
    /// <summary>
    /// Unified patch for Player.GetStat to handle all stat modifications:
    /// - Base racial bonuses
    /// - Active ability effects (Blood Rage, Quick Learner, Tailwind, etc.)
    /// - Drawback penalties
    /// - Conditional synergies (time-of-day, season, HP threshold)
    /// </summary>
    public static class StatPatches
    {
        // Reentrancy guard: prevents infinite recursion when our patch calls game methods
        // (like player.MaxHealth or FarmingSkillLevel) that internally call GetStat again.
        private static bool _isApplyingBonuses = false;

        // Per-frame cache for all stats to avoid stutter when game polls GetStat
        // repeatedly (e.g. Amari Cat + scythe). Only used when not reentrant.
        private static int _cachedFrame = -1;
        private static Dictionary<StatType, float> _perFrameStatCache;

        /// <summary>Stats that are polled very frequently during attacks; skipping only these for Amari Cat removes stutter while keeping damage/crit/dodge.</summary>
        private static bool IsAttackSpeedStat(StatType stat)
        {
            return stat == StatType.AttackSpeed || stat == StatType.SpellAttackSpeed;
        }

        private const string FasterRacesGuid = "com.azraelgodking.fasterraces";
        private static int _fasterRacesStateFrame = -1;
        private static bool _fasterRacesSpeedActive;

        /// <summary>
        /// True when Faster Races is loaded and currently applying a positive speed bonus.
        /// We intentionally avoid skipping HB speed bonuses when Faster Races is installed but disabled.
        /// </summary>
        private static bool IsFasterRacesSpeedBonusActive()
        {
            if (_fasterRacesStateFrame == Time.frameCount)
                return _fasterRacesSpeedActive;

            _fasterRacesStateFrame = Time.frameCount;
            _fasterRacesSpeedActive = false;

            if (Chainloader.PluginInfos == null || !Chainloader.PluginInfos.TryGetValue(FasterRacesGuid, out var pluginInfo))
                return false;

            // Safe fallback: if reflection fails, assume Faster Races speed bonus is NOT active
            // so HavensBirthright keeps its own movement speed bonuses.
            _fasterRacesSpeedActive = false;

            var instance = pluginInfo?.Instance;
            if (instance == null)
                return _fasterRacesSpeedActive;

            var pluginType = instance.GetType();

            try
            {
                // Must match FasterRaces.Plugin.IsSpeedBonusActive (enabled + positive global bonus only).
                // Do not fall back to EnableMod alone — that mis-detects "active" at 0% and strips HB move speed.
                var activeProperty = pluginType.GetProperty("IsSpeedBonusActive", BindingFlags.Public | BindingFlags.Static);
                if (activeProperty != null && activeProperty.PropertyType == typeof(bool))
                {
                    _fasterRacesSpeedActive = (bool)activeProperty.GetValue(null, null);
                    return _fasterRacesSpeedActive;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[StatPatches] FasterRaces reflection failed; defaulting IsSpeedBonusActive=false. {ex.Message}");
            }

            return _fasterRacesSpeedActive;
        }

        /// <summary>
        /// Postfix patch for Player.GetStat - modifies stat values based on racial bonuses,
        /// active abilities, drawbacks, and conditional synergies.
        /// Runs before Faster Races (lower Harmony priority = earlier postfix): racial bonuses apply first, then FR multiplies Movespeed.
        /// </summary>
        [HarmonyPriority(100)]
        public static void ModifyGetStat(StatType stat, ref float __result)
        {
            if (Plugin.CriticalBirthrightHarmonyIncomplete)
                return;

            if (_isApplyingBonuses)
                return;

            // Don't apply bonuses until StatFrameCache has a valid frame (BirthrightRunner drives Update).
            if (!StatFrameCache.IsCacheValid)
                return;

            // Per-frame cache: check before any config/manager/race so cache hits are minimal cost.
            int frame = Time.frameCount;
            if (frame != _cachedFrame)
            {
                _cachedFrame = frame;
                _perFrameStatCache = _perFrameStatCache ?? new Dictionary<StatType, float>();
                _perFrameStatCache.Clear();
            }
            if (_perFrameStatCache.TryGetValue(stat, out float cached))
            {
                __result = cached;
                return;
            }

            if (!RacialConfig.EnableRacialBonuses.Value)
                return;

            var manager = Plugin.GetRacialBonusManager();
            if (manager == null)
                return;

            var race = manager.GetPlayerRace();
            if (!race.HasValue)
                return;

            // Optional: skip only attack speed bonuses for Amari Cat to eliminate stutter (e.g. scythe); damage, crit, dodge still apply.
            if (race.Value == Race.AmariCat && RacialConfig.AmariCatReduceCombatStutter != null &&
                RacialConfig.AmariCatReduceCombatStutter.Value && IsAttackSpeedStat(stat))
            {
                _perFrameStatCache[stat] = __result;
                return;
            }

            _isApplyingBonuses = true;
            try
            {
                // Apply base racial bonuses
                ApplyBaseBonuses(stat, ref __result, manager);

                // Apply active ability effects
                if (AbilityConfig.EnableActiveAbilities != null && AbilityConfig.EnableActiveAbilities.Value)
                {
                    ApplyActiveAbilities(stat, ref __result, manager, race.Value);
                }

                // Apply drawbacks
                if (AbilityConfig.EnableRacialDrawbacks != null && AbilityConfig.EnableRacialDrawbacks.Value)
                {
                    ApplyDrawbacks(stat, ref __result, race.Value);
                }

                // Apply conditional synergies
                if (AbilityConfig.EnableConditionalSynergies != null && AbilityConfig.EnableConditionalSynergies.Value)
                {
                    ApplyConditionalSynergies(stat, ref __result, race.Value);
                }

                _perFrameStatCache[stat] = __result;
            }
            finally
            {
                _isApplyingBonuses = false;
            }
        }

        /// <summary>
        /// Base racial bonus application (existing system)
        /// </summary>
        private static void ApplyBaseBonuses(StatType stat, ref float __result, RacialBonusManager manager)
        {
            switch (stat)
            {
                // === COMBAT STATS ===
                case StatType.AttackDamage:
                    if (manager.HasBonus(BonusType.MeleeStrength))
                        __result = manager.ApplyBonus(__result, BonusType.MeleeStrength);
                    break;

                case StatType.SpellDamage:
                    if (manager.HasBonus(BonusType.MagicPower))
                        __result = manager.ApplyBonus(__result, BonusType.MagicPower);
                    break;

                case StatType.Crit:
                    if (manager.HasBonus(BonusType.CriticalChance))
                    {
                        float bonus = manager.GetBonusValue(BonusType.CriticalChance);
                        __result += bonus / 100f;
                    }
                    break;

                case StatType.Defense:
                    if (manager.HasBonus(BonusType.Defense))
                        __result = manager.ApplyBonus(__result, BonusType.Defense);
                    break;

                case StatType.AttackSpeed:
                case StatType.SpellAttackSpeed:
                    if (manager.HasBonus(BonusType.AttackSpeed))
                        __result = manager.ApplyBonus(__result, BonusType.AttackSpeed);
                    break;

                case StatType.Movespeed:
                    // When Faster Races speed bonus is active, skip HB movement speed bonus to avoid double speed.
                    if (!IsFasterRacesSpeedBonusActive() && manager.HasBonus(BonusType.MovementSpeed))
                        __result = manager.ApplyBonus(__result, BonusType.MovementSpeed);
                    break;

                case StatType.Dodge:
                    if (manager.HasBonus(BonusType.DodgeChance))
                    {
                        float dodgeBonus = manager.GetBonusValue(BonusType.DodgeChance);
                        __result += dodgeBonus / 100f;
                    }
                    break;

                // === SKILL STATS ===
                case StatType.FarmingSkill:
                    if (manager.HasBonus(BonusType.FarmingSpeed))
                        __result = manager.ApplyBonus(__result, BonusType.FarmingSpeed);
                    break;

                case StatType.ExtraCropChance:
                    if (manager.HasBonus(BonusType.CropQuality))
                    {
                        float cropBonus = manager.GetBonusValue(BonusType.CropQuality);
                        __result += cropBonus / 100f;
                    }
                    break;

                case StatType.MiningSkill:
                    if (manager.HasBonus(BonusType.MiningSpeed))
                        __result = manager.ApplyBonus(__result, BonusType.MiningSpeed);
                    break;

                case StatType.MiningCrit:
                    if (manager.HasBonus(BonusType.MiningYield))
                    {
                        float miningBonus = manager.GetBonusValue(BonusType.MiningYield);
                        __result += miningBonus / 100f;
                    }
                    break;

                case StatType.WoodcuttingCrit:
                    if (manager.HasBonus(BonusType.WoodcuttingSpeed))
                        __result = manager.ApplyBonus(__result, BonusType.WoodcuttingSpeed);
                    if (manager.HasBonus(BonusType.WoodcuttingYield))
                    {
                        float woodBonus = manager.GetBonusValue(BonusType.WoodcuttingYield);
                        __result += woodBonus / 100f;
                    }
                    break;

                case StatType.FishingSkill:
                    if (manager.HasBonus(BonusType.FishingSpeed))
                        __result = manager.ApplyBonus(__result, BonusType.FishingSpeed);
                    if (manager.HasBonus(BonusType.FishingLuck))
                        __result = manager.ApplyBonus(__result, BonusType.FishingLuck);
                    break;

                case StatType.ExplorationSkill:
                    if (manager.HasBonus(BonusType.ForagingChance))
                        __result = manager.ApplyBonus(__result, BonusType.ForagingChance);
                    break;

                case StatType.ExtraForageableChance:
                    if (manager.HasBonus(BonusType.ForagingChance))
                    {
                        float forageBonus = manager.GetBonusValue(BonusType.ForagingChance);
                        __result += forageBonus / 100f;
                    }
                    break;

                case StatType.SmithingSkill:
                    if (manager.HasBonus(BonusType.CraftingSpeed))
                        __result = manager.ApplyBonus(__result, BonusType.CraftingSpeed);
                    break;

                // === HEALTH/MANA STATS ===
                case StatType.Health:
                    if (manager.HasBonus(BonusType.MaxHealth))
                        __result = manager.ApplyBonus(__result, BonusType.MaxHealth);
                    break;

                case StatType.Mana:
                    if (manager.HasBonus(BonusType.MaxMana))
                        __result = manager.ApplyBonus(__result, BonusType.MaxMana);
                    break;

                // === REGEN STATS ===
                case StatType.HealthRegen:
                    if (manager.HasBonus(BonusType.HealthRegen))
                        __result = manager.ApplyBonus(__result, BonusType.HealthRegen);
                    break;

                case StatType.ManaRegen:
                    if (manager.HasBonus(BonusType.ManaRegen))
                        __result = manager.ApplyBonus(__result, BonusType.ManaRegen);
                    break;

                // === ECONOMY STATS ===
                case StatType.GoldGain:
                    if (manager.HasBonus(BonusType.GoldFind))
                    {
                        float bonus = manager.GetBonusValue(BonusType.GoldFind);
                        __result += bonus / 100f;
                    }
                    break;

                case StatType.BonusExperience:
                case StatType.BonusFarmingEXP:
                case StatType.BonusWoodcuttingEXP:
                    if (manager.HasBonus(BonusType.ExperienceGain))
                    {
                        float bonus = manager.GetBonusValue(BonusType.ExperienceGain);
                        __result += bonus / 100f;
                    }
                    break;
            }
        }

        /// <summary>
        /// Apply active ability stat modifications
        /// </summary>
        private static void ApplyActiveAbilities(StatType stat, ref float __result, RacialBonusManager manager, Race race)
        {
            switch (race)
            {
                // Demon - Blood Rage: bonus damage/speed when low HP
                case Race.Demon:
                    if (AbilityConfig.EnableBloodRage.Value)
                    {
                        float hpRatio = StatFrameCache.CachedHPRatio;
                        float threshold = AbilityConfig.BloodRageHPThreshold.Value / 100f;

                        if (hpRatio <= threshold)
                        {
                            if (stat == StatType.AttackDamage)
                                __result *= (1f + AbilityConfig.BloodRageMeleeBonus.Value / 100f);
                            else if (stat == StatType.AttackSpeed)
                                __result *= (1f + AbilityConfig.BloodRageAttackSpeedBonus.Value / 100f);
                        }
                    }
                    break;

                // Human - Quick Learner: XP bonus based on leveled skills
                case Race.Human:
                    if (AbilityConfig.EnableQuickLearner.Value)
                    {
                        if (stat == StatType.BonusExperience || stat == StatType.BonusFarmingEXP ||
                            stat == StatType.BonusWoodcuttingEXP)
                        {
                            float quickLearnerBonus = StatFrameCache.CachedQuickLearnerBonus;
                            if (quickLearnerBonus > 0)
                                __result += quickLearnerBonus / 100f;
                        }
                    }
                    break;

                // Amari Dog - Loyalty Aura: stats based on max-friendship NPCs
                case Race.AmariDog:
                    if (AbilityConfig.EnableLoyaltyAura.Value)
                    {
                        if (stat == StatType.Defense || stat == StatType.AttackDamage ||
                            stat == StatType.SpellDamage || stat == StatType.Health)
                        {
                            float loyaltyBonus = ActiveAbilityManager.GetBonusValue(ActiveAbilityManager.LoyaltyAura);
                            if (loyaltyBonus > 0)
                                __result *= (1f + loyaltyBonus / 100f);
                        }
                    }
                    break;

                // Amari Bird - Tailwind: movement speed from outdoor time (skipped while Faster Races speed bonus is active)
                case Race.AmariBird:
                    if (!IsFasterRacesSpeedBonusActive() && AbilityConfig.EnableTailwind.Value && stat == StatType.Movespeed)
                    {
                        float tailwindBonus = ActiveAbilityManager.GetBonusValue(ActiveAbilityManager.TailwindOutdoor);
                        if (tailwindBonus > 0)
                            __result *= (1f + tailwindBonus / 100f);
                    }
                    break;

                // Amari Reptile - Hardened Scales: stacking defense from taking hits
                case Race.AmariReptile:
                    if (AbilityConfig.EnableHardenedScales.Value && stat == StatType.Defense)
                    {
                        int stacks = ActiveAbilityManager.GetStacks(
                            ActiveAbilityManager.HardenedScales,
                            AbilityConfig.HardenedScalesDecayTime.Value
                        );
                        if (stacks > 0)
                        {
                            float defBonus = stacks * AbilityConfig.HardenedScalesDefensePerStack.Value;
                            __result *= (1f + defBonus / 100f);
                        }
                    }
                    break;

                // Naga - Serpent's Grace: double fishing in rain/winter
                case Race.Naga:
                    if (AbilityConfig.EnableSerpentGrace.Value)
                    {
                        if (stat == StatType.FishingSkill)
                        {
                            bool isWinter = StatFrameCache.CachedIsWinter;
                            if (isWinter)
                            {
                                float multiplier = AbilityConfig.SerpentGraceFishingMultiplier.Value;
                                float baseFishingBonus = manager.GetBonusValue(BonusType.FishingSpeed) +
                                                         manager.GetBonusValue(BonusType.FishingLuck);
                                if (baseFishingBonus > 0)
                                {
                                    float extraBonus = baseFishingBonus * (multiplier - 1f) / 100f;
                                    __result *= (1f + extraBonus);
                                }
                            }
                        }
                    }
                    break;

                // Fire Elemental - Infernal Forge: block mana regen while active
                case Race.FireElemental:
                    if (stat == StatType.ManaRegen &&
                        AbilityConfig.EnableInfernalForge.Value &&
                        ActiveAbilityManager.IsRuntimeEnabled(ActiveAbilityManager.InfernalForge))
                    {
                        __result = 0f;
                    }
                    break;

                // Generic Elemental - Elemental Resonance: mining bonuses in mines
                // Infernal Forge mana lock only when body resolves to Fire (or still ambiguous generic), not Water
                case Race.Elemental:
                    if (AbilityConfig.EnableElementalResonance.Value)
                    {
                        if (StatFrameCache.CachedIsInMine)
                        {
                            if (stat == StatType.MiningSkill)
                                __result *= (1f + AbilityConfig.ElementalResonanceMiningSpeedBonus.Value / 100f);
                            else if (stat == StatType.MiningCrit)
                                __result *= (1f + AbilityConfig.ElementalResonanceMiningDamageBonus.Value / 100f);
                        }
                    }
                    if (AbilityConfig.EnableInfernalForge != null && AbilityConfig.EnableInfernalForge.Value)
                    {
                        Race eff = ElementalVariantResolver.ResolveElementalAbilityRace(race);
                        if (eff != Race.WaterElemental &&
                            stat == StatType.ManaRegen &&
                            AbilityConfig.EnableInfernalForge.Value &&
                            ActiveAbilityManager.IsRuntimeEnabled(ActiveAbilityManager.InfernalForge))
                        {
                            __result = 0f;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Apply racial drawback penalties
        /// </summary>
        private static void ApplyDrawbacks(StatType stat, ref float __result, Race race)
        {
            switch (race)
            {
                case Race.FireElemental:
                    if (stat == StatType.FishingSkill)
                    {
                        float penalty = AbilityConfig.FireHydrophobicFishingPenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    break;

                case Race.WaterElemental:
                    if (stat == StatType.Health)
                    {
                        float penalty = AbilityConfig.WaterFragileFormHPPenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    break;

                case Race.Angel:
                    if (stat == StatType.AttackDamage)
                    {
                        float penalty = AbilityConfig.AngelPacifistMeleePenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    break;

                case Race.Elf:
                    if (stat == StatType.Health)
                    {
                        float penalty = AbilityConfig.ElfFragileHPPenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    else if (stat == StatType.Defense)
                    {
                        float penalty = AbilityConfig.ElfFragileDefensePenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    break;

                case Race.AmariCat:
                    if (stat == StatType.Health)
                    {
                        float penalty = AbilityConfig.CatGlassCannonHPPenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    break;

                case Race.AmariDog:
                    if (stat == StatType.Movespeed)
                    {
                        float penalty = AbilityConfig.DogSlowStarterSpeedPenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    break;

                case Race.AmariBird:
                    if (stat == StatType.Health)
                    {
                        float penalty = AbilityConfig.BirdHollowBonesHPPenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    else if (stat == StatType.Defense)
                    {
                        float penalty = AbilityConfig.BirdHollowBonesDefensePenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    break;

                case Race.AmariAquatic:
                    if (stat == StatType.Movespeed)
                    {
                        float penalty = AbilityConfig.AquaticLandSlugSpeedPenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    break;

                case Race.AmariReptile:
                    if (stat == StatType.AttackSpeed)
                    {
                        float penalty = AbilityConfig.ReptileColdBloodedAttackSpeedPenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    break;

                case Race.Naga:
                    if (stat == StatType.Movespeed)
                    {
                        float penalty = AbilityConfig.NagaLandlockedSpeedPenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    else if (stat == StatType.FarmingSkill)
                    {
                        float penalty = AbilityConfig.NagaLandlockedFarmingPenalty.Value;
                        __result *= (1f - penalty / 100f);
                    }
                    break;
            }
        }

        /// <summary>
        /// Apply conditional synergies (time-of-day, season, HP threshold)
        /// </summary>
        private static void ApplyConditionalSynergies(StatType stat, ref float __result, Race race)
        {
            // === TIME-OF-DAY SYNERGIES ===

            // Angel - Solar Power: magic bonus during daytime
            if (race == Race.Angel && stat == StatType.SpellDamage)
            {
                if (StatFrameCache.CachedIsDaytime)
                {
                    __result *= (1f + AbilityConfig.AngelSolarPowerBonus.Value / 100f);
                }
            }

            // Demon - Night Stalker: melee bonus during nighttime
            if (race == Race.Demon && stat == StatType.AttackDamage)
            {
                if (!StatFrameCache.CachedIsDaytime)
                {
                    __result *= (1f + AbilityConfig.DemonNightStalkerBonus.Value / 100f);
                }
            }

            // === SEASON SYNERGIES ===
            if (StatFrameCache.CachedSeason != null)
            {
                // Elf - Spring Awakening: farming bonus in Spring
                if (race == Race.Elf &&
                    StatFrameCache.CachedIsSpring)
                {
                    if (stat == StatType.FarmingSkill || stat == StatType.ExtraCropChance ||
                        stat == StatType.ExtraForageableChance)
                    {
                        __result *= (1f + AbilityConfig.ElfSpringAwakeningBonus.Value / 100f);
                    }
                }

                // Fire Elemental - Summer's Fury: combat bonus in Summer
                if (race == Race.FireElemental &&
                    StatFrameCache.CachedIsSummer)
                {
                    if (stat == StatType.AttackDamage || stat == StatType.SpellDamage ||
                        stat == StatType.AttackSpeed || stat == StatType.Crit)
                    {
                        __result *= (1f + AbilityConfig.FireSummerFuryBonus.Value / 100f);
                    }
                }

                // Water Elemental - Winter's Embrace: defense/regen bonus in Winter
                if (race == Race.WaterElemental &&
                    StatFrameCache.CachedIsWinter)
                {
                    if (stat == StatType.Defense || stat == StatType.HealthRegen ||
                        stat == StatType.ManaRegen)
                    {
                        __result *= (1f + AbilityConfig.WaterWinterEmbraceBonus.Value / 100f);
                    }
                }
            }

            // === HEALTH THRESHOLD SYNERGIES ===
            float hpRatio = StatFrameCache.CachedHPRatio;

            // Amari Reptile - Last Stand: defense when low HP
            if (race == Race.AmariReptile && stat == StatType.Defense)
            {
                if (hpRatio <= AbilityConfig.ReptileLastStandHPThreshold.Value / 100f)
                {
                    __result *= (1f + AbilityConfig.ReptileLastStandDefenseBonus.Value / 100f);
                }
            }

            // Angel - Martyr's Light: health regen when very low HP
            if (race == Race.Angel && stat == StatType.HealthRegen)
            {
                if (hpRatio <= AbilityConfig.AngelMartyrHPThreshold.Value / 100f)
                {
                    __result *= AbilityConfig.AngelMartyrRegenMultiplier.Value;
                }
            }
        }

    }
}
