using System.Collections.Generic;

namespace HavensBirthright
{
    /// <summary>
    /// Manages racial bonuses for all races in Sun Haven.
    /// Caches the detected race and SubRace so detection only runs once per save load.
    /// </summary>
    public class RacialBonusManager
    {
        private Dictionary<Race, List<RacialBonus>> _racialBonuses;
        private Race? _currentPlayerRace;
        private int _cachedGameSubRace = -1; // Wish.SubRace as int, -1 = unknown

        public RacialBonusManager()
        {
            _racialBonuses = new Dictionary<Race, List<RacialBonus>>();
            InitializeDefaultBonuses();
        }

        /// <summary>
        /// Initialize default racial bonuses for each race
        /// </summary>
        private void InitializeDefaultBonuses()
        {
            // Human - Jack of all trades, master of none. Bonus to experience and social.
            _racialBonuses[Race.Human] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.ExperienceGain, RacialConfig.HumanExpBonus.Value, true,
                    "Adaptable: Gain experience faster"),
                new RacialBonus(BonusType.RelationshipGain, RacialConfig.HumanRelationshipBonus.Value, true,
                    "Charismatic: Build relationships faster"),
                new RacialBonus(BonusType.ShopDiscount, RacialConfig.HumanShopDiscount.Value, true,
                    "Silver Tongue: Small discount at shops"),
                new RacialBonus(BonusType.CommunityTokenGain, RacialConfig.HumanCommunityTokenBonus.Value, true,
                    "Community Leader: Earn more community tokens")
            };

            // Elf - Nature-attuned, excellent farmers and foragers
            _racialBonuses[Race.Elf] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.FarmingSpeed, RacialConfig.ElfFarmingBonus.Value, true,
                    "Nature's Touch: Farm crops faster"),
                new RacialBonus(BonusType.CropQuality, RacialConfig.ElfCropQualityBonus.Value, true,
                    "Green Thumb: Higher chance for quality crops"),
                new RacialBonus(BonusType.ForagingChance, RacialConfig.ElfForagingBonus.Value, true,
                    "Forest Walker: Find more foragables"),
                new RacialBonus(BonusType.ManaRegen, RacialConfig.ElfManaRegenBonus.Value, true,
                    "Arcane Heritage: Faster mana regeneration"),
                new RacialBonus(BonusType.WoodcuttingDamage, RacialConfig.ElfWoodcuttingDamageBonus.Value, true,
                    "Timber Master: Deal more damage to trees")
            };

            // Angel - Divine beings with healing and light magic affinity
            _racialBonuses[Race.Angel] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.MaxMana, RacialConfig.AngelMaxManaBonus.Value, true,
                    "Divine Reservoir: Increased maximum mana"),
                new RacialBonus(BonusType.MagicPower, RacialConfig.AngelMagicBonus.Value, true,
                    "Holy Light: Enhanced magic damage"),
                new RacialBonus(BonusType.HealthRegen, RacialConfig.AngelHealthRegenBonus.Value, true,
                    "Blessed Recovery: Faster health regeneration"),
                new RacialBonus(BonusType.LuckBonus, RacialConfig.AngelLuckBonus.Value, true,
                    "Fortune's Favor: Blessed with good luck"),
                new RacialBonus(BonusType.SpellAttackSpeed, RacialConfig.AngelSpellAttackSpeedBonus.Value, true,
                    "Divine Haste: Cast spells faster")
            };

            // Demon - Fierce combatants with dark powers
            _racialBonuses[Race.Demon] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.MeleeStrength, RacialConfig.DemonMeleeBonus.Value, true,
                    "Infernal Might: Increased melee damage"),
                new RacialBonus(BonusType.CriticalChance, RacialConfig.DemonCritBonus.Value, true,
                    "Ruthless: Higher critical hit chance"),
                new RacialBonus(BonusType.MaxHealth, RacialConfig.DemonHealthBonus.Value, true,
                    "Hellforged Vitality: Increased maximum health"),
                new RacialBonus(BonusType.GoldFind, RacialConfig.DemonGoldBonus.Value, true,
                    "Greed: Find more gold"),
                new RacialBonus(BonusType.TripleGoldChance, RacialConfig.DemonTripleGoldBonus.Value, true,
                    "Avarice: Chance for triple gold drops"),
                new RacialBonus(BonusType.MiningDamage, RacialConfig.DemonMiningDamageBonus.Value, true,
                    "Hellfire Pick: Deal more damage to rocks")
            };

            // Fire Elemental - Aggressive, damage-focused with fire affinity
            _racialBonuses[Race.FireElemental] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.MeleeStrength, RacialConfig.FireElementalMeleeBonus.Value, true,
                    "Burning Fury: Increased melee damage"),
                new RacialBonus(BonusType.MagicPower, RacialConfig.FireElementalMagicBonus.Value, true,
                    "Inferno: Enhanced magic damage"),
                new RacialBonus(BonusType.AttackSpeed, RacialConfig.FireElementalAttackSpeedBonus.Value, true,
                    "Wildfire: Faster attack speed"),
                new RacialBonus(BonusType.CriticalChance, RacialConfig.FireElementalCritBonus.Value, true,
                    "Scorching Strike: Higher critical hit chance")
            };

            // Water Elemental - Defensive, utility-focused with water affinity
            _racialBonuses[Race.WaterElemental] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.Defense, RacialConfig.WaterElementalDefenseBonus.Value, true,
                    "Tidal Shield: Increased defense"),
                new RacialBonus(BonusType.HealthRegen, RacialConfig.WaterElementalHealthRegenBonus.Value, true,
                    "Healing Waters: Faster health regeneration"),
                new RacialBonus(BonusType.ManaRegen, RacialConfig.WaterElementalManaRegenBonus.Value, true,
                    "Flowing Spirit: Faster mana regeneration"),
                new RacialBonus(BonusType.FishingLuck, RacialConfig.WaterElementalFishingBonus.Value, true,
                    "Aquatic Kinship: Better fishing luck"),
                new RacialBonus(BonusType.FishingMinigameSpeed, RacialConfig.WaterElementalFishingMinigameBonus.Value, true,
                    "Water Mastery: Faster fishing minigame"),
                new RacialBonus(BonusType.AirSkipChance, RacialConfig.WaterElementalAirSkipBonus.Value, true,
                    "Breathless: No need for air underwater")
            };

            // Generic Elemental fallback (if variant can't be determined)
            _racialBonuses[Race.Elemental] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.MiningSpeed, RacialConfig.ElementalMiningSpeedBonus.Value, true,
                    "Stone Affinity: Mine faster"),
                new RacialBonus(BonusType.MiningYield, RacialConfig.ElementalMiningYieldBonus.Value, true,
                    "Earth's Bounty: Chance for extra ore"),
                new RacialBonus(BonusType.MagicPower, RacialConfig.ElementalMagicBonus.Value, true,
                    "Elemental Mastery: Enhanced magic damage"),
                new RacialBonus(BonusType.Defense, RacialConfig.ElementalDefenseBonus.Value, true,
                    "Hardened Form: Increased defense")
            };

            // Generic Amari fallback (if variant can't be determined)
            _racialBonuses[Race.Amari] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.MovementSpeed, RacialConfig.AmariSpeedBonus.Value, true,
                    "Swift Paws: Move faster"),
                new RacialBonus(BonusType.AttackSpeed, RacialConfig.AmariAttackSpeedBonus.Value, true,
                    "Predator's Reflexes: Attack faster"),
                new RacialBonus(BonusType.CraftingSpeed, RacialConfig.AmariCraftingBonus.Value, true,
                    "Skilled Artisan: Craft items faster"),
                new RacialBonus(BonusType.WoodcuttingSpeed, RacialConfig.AmariWoodcuttingBonus.Value, true,
                    "Forest Hunter: Chop trees faster")
            };

            // Amari Cat - Feline Grace (movement); Predator's Strike (crit); Keen Senses (foraging); Nine Lives is cheat-death (CombatPatches)
            _racialBonuses[Race.AmariCat] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.MovementSpeed, RacialConfig.AmariCatSpeedBonus.Value, true,
                    "Feline Grace: Move faster"),
                new RacialBonus(BonusType.CriticalChance, RacialConfig.AmariCatCritBonus.Value, true,
                    "Predator's Strike: Higher critical hit chance"),
                new RacialBonus(BonusType.ForagingChance, RacialConfig.AmariCatForagingBonus.Value, true,
                    "Keen Senses: Find more foragables")
            };

            // Amari Dog - Loyal companions, tough and social
            _racialBonuses[Race.AmariDog] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.MaxHealth, RacialConfig.AmariDogHealthBonus.Value, true,
                    "Loyal Heart: Increased maximum health"),
                new RacialBonus(BonusType.Defense, RacialConfig.AmariDogDefenseBonus.Value, true,
                    "Guardian's Resolve: Increased defense"),
                new RacialBonus(BonusType.RelationshipGain, RacialConfig.AmariDogRelationshipBonus.Value, true,
                    "Best Friend: Build relationships faster"),
                new RacialBonus(BonusType.ExperienceGain, RacialConfig.AmariDogExpBonus.Value, true,
                    "Eager Learner: Gain experience faster")
            };

            // Amari Bird - Free spirits, quick and perceptive
            _racialBonuses[Race.AmariBird] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.MovementSpeed, RacialConfig.AmariBirdSpeedBonus.Value, true,
                    "Wind Rider: Move faster"),
                new RacialBonus(BonusType.ForagingChance, RacialConfig.AmariBirdForagingBonus.Value, true,
                    "Keen Eye: Find more foragables"),
                new RacialBonus(BonusType.ManaRegen, RacialConfig.AmariBirdManaRegenBonus.Value, true,
                    "Sky Spirit: Faster mana regeneration"),
                new RacialBonus(BonusType.DodgeChance, RacialConfig.AmariBirdDodgeBonus.Value, true,
                    "Evasive Flight: Higher chance to dodge attacks")
            };

            // Amari Aquatic - Water dwellers, fishing experts
            _racialBonuses[Race.AmariAquatic] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.FishingSpeed, RacialConfig.AmariAquaticFishingSpeedBonus.Value, true,
                    "Water Born: Fish faster"),
                new RacialBonus(BonusType.FishingLuck, RacialConfig.AmariAquaticFishingLuckBonus.Value, true,
                    "Tidal Blessing: Better fishing luck"),
                new RacialBonus(BonusType.ManaRegen, RacialConfig.AmariAquaticManaRegenBonus.Value, true,
                    "Flowing Spirit: Faster mana regeneration"),
                new RacialBonus(BonusType.HealthRegen, RacialConfig.AmariAquaticHealthRegenBonus.Value, true,
                    "Healing Waters: Faster health regeneration"),
                new RacialBonus(BonusType.FishingMinigameSpeed, RacialConfig.AmariAquaticFishingMinigameBonus.Value, true,
                    "Aquatic Instinct: Faster fishing minigame"),
                new RacialBonus(BonusType.AirSkipChance, RacialConfig.AmariAquaticAirSkipBonus.Value, true,
                    "Amphibious: Chance to skip air requirement")
            };

            // Amari Reptile - Tough survivors, resilient and strong
            _racialBonuses[Race.AmariReptile] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.Defense, RacialConfig.AmariReptileDefenseBonus.Value, true,
                    "Scaled Hide: Increased defense"),
                new RacialBonus(BonusType.MeleeStrength, RacialConfig.AmariReptileMeleeBonus.Value, true,
                    "Primal Strength: Increased melee damage"),
                new RacialBonus(BonusType.MaxHealth, RacialConfig.AmariReptileHealthBonus.Value, true,
                    "Cold Blood: Increased maximum health"),
                new RacialBonus(BonusType.MiningSpeed, RacialConfig.AmariReptileMiningBonus.Value, true,
                    "Burrow Instinct: Mine faster")
            };

            // Naga - Serpentine water dwellers, excellent fishers
            _racialBonuses[Race.Naga] = new List<RacialBonus>
            {
                new RacialBonus(BonusType.FishingSpeed, RacialConfig.NagaFishingSpeedBonus.Value, true,
                    "Aquatic Nature: Fish faster"),
                new RacialBonus(BonusType.FishingLuck, RacialConfig.NagaFishingLuckBonus.Value, true,
                    "Sea's Blessing: Better fishing luck"),
                new RacialBonus(BonusType.Defense, RacialConfig.NagaDefenseBonus.Value, true,
                    "Scaled Hide: Increased defense"),
                new RacialBonus(BonusType.ManaRegen, RacialConfig.NagaManaRegenBonus.Value, true,
                    "Tidal Magic: Faster mana regeneration"),
                new RacialBonus(BonusType.AirSkipChance, RacialConfig.NagaAirSkipBonus.Value, true,
                    "Serpent's Breath: Chance to skip air requirement")
            };

            Plugin.Log.LogInfo($"Initialized racial bonuses for {_racialBonuses.Count} races");
            BonusTransferRules.RebuildFromConfig(
                RacialConfig.CrossRaceBonusRules != null ? RacialConfig.CrossRaceBonusRules.Value : "");
        }

        /// <summary>
        /// Set the current player's race
        /// </summary>
        public void SetPlayerRace(Race race)
        {
            _currentPlayerRace = race;
            Plugin.Log.LogInfo($"Player race set to: {race}");
        }

        /// <summary>
        /// Set the player's race with elemental variant support
        /// If the race is Elemental, specify the variant to get Fire/Water specific bonuses
        /// </summary>
        public void SetPlayerRace(Race race, ElementalVariant variant)
        {
            if (race == Race.Elemental)
            {
                // Convert to specific elemental type based on variant
                _currentPlayerRace = variant switch
                {
                    ElementalVariant.Fire => Race.FireElemental,
                    ElementalVariant.Water => Race.WaterElemental,
                    _ => Race.Elemental // Fallback to generic
                };
            }
            else
            {
                _currentPlayerRace = race;
            }
            Plugin.Log.LogInfo($"Player race set to: {_currentPlayerRace} (variant: {variant})");
        }

        /// <summary>
        /// Set the player's race with SubRace caching.
        /// Called by PlayerPatches.DetectAndSetRace() after two-tier detection.
        /// The SubRace value is cached so it never needs to be re-detected.
        /// </summary>
        /// <param name="race">The resolved mod Race (e.g. AmariCat, FireElemental, Naga)</param>
        /// <param name="gameSubRace">The raw Wish.SubRace value as int, or -1 if unknown</param>
        public void SetPlayerRace(Race race, int gameSubRace)
        {
            _currentPlayerRace = race;
            _cachedGameSubRace = gameSubRace;
            Plugin.Log.LogInfo($"Player race set to: {race} (cached SubRace: {gameSubRace})");
        }

        /// <summary>
        /// Clears the cached player race and SubRace.
        /// Called on menu transition so race detection re-runs when a new save is loaded.
        /// </summary>
        public void ClearPlayerRace()
        {
            _currentPlayerRace = null;
            _cachedGameSubRace = -1;
            Plugin.Log.LogDebug("[RacialBonusManager] Player race cleared");
        }

        /// <summary>
        /// Get the current player's race
        /// </summary>
        public Race? GetPlayerRace()
        {
            return _currentPlayerRace;
        }

        /// <summary>
        /// Get all bonuses for a specific race
        /// </summary>
        public List<RacialBonus> GetBonusesForRace(Race race)
        {
            if (_racialBonuses.TryGetValue(race, out var bonuses))
            {
                return bonuses;
            }
            return new List<RacialBonus>();
        }

        /// <summary>
        /// Get all bonuses for the current player's race
        /// </summary>
        public List<RacialBonus> GetCurrentPlayerBonuses()
        {
            if (_currentPlayerRace.HasValue)
            {
                return GetBonusesForRace(_currentPlayerRace.Value);
            }
            return new List<RacialBonus>();
        }

        /// <summary>
        /// Get a specific bonus value for the current player
        /// </summary>
        public float GetBonusValue(BonusType type)
        {
            if (!_currentPlayerRace.HasValue)
                return 0f;

            float value = 0f;
            var bonuses = GetBonusesForRace(_currentPlayerRace.Value);
            foreach (var bonus in bonuses)
            {
                if (bonus.Type == type)
                {
                    value = bonus.Value;
                    break;
                }
            }

            value += BonusTransferRules.GetTransferredPercent(this, _currentPlayerRace.Value, type);
            return value;
        }

        /// <summary>
        /// Apply a bonus multiplier to a base value
        /// </summary>
        public float ApplyBonus(float baseValue, BonusType type)
        {
            float bonusPercent = GetBonusValue(type);
            if (bonusPercent == 0f)
                return baseValue;

            return baseValue * (1f + bonusPercent / 100f);
        }

        /// <summary>
        /// Check if player has a bonus of a specific type
        /// </summary>
        public bool HasBonus(BonusType type)
        {
            return GetBonusValue(type) != 0f;
        }
    }
}
