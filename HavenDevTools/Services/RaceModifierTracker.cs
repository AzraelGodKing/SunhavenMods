using System;
using System.Collections.Generic;
using System.Reflection;

namespace HavenDevTools.Services
{
    /// <summary>
    /// Service for tracking active race modifiers (integrates with Haven's Birthright)
    /// </summary>
    public class RaceModifierTracker
    {
        public RaceModifierTracker()
        {
            Plugin.Log?.LogDebug("[RaceModifierTracker] Initialized");
        }

        /// <summary>
        /// Get current player race (requires Haven's Birthright)
        /// </summary>
        public string GetCurrentRace()
        {
            if (!Plugin.HasHavensBirthright) return "Unknown (Haven's Birthright not loaded)";

            try
            {
                var birthrightAssembly = GetBirthrightAssembly();
                if (birthrightAssembly == null) return "Unknown";

                var pluginType = birthrightAssembly.GetType("HavensBirthright.Plugin");
                if (pluginType == null) return "Unknown";

                var getRacialBonusManagerMethod = pluginType.GetMethod("GetRacialBonusManager", BindingFlags.Public | BindingFlags.Static);
                if (getRacialBonusManagerMethod == null) return "Unknown";

                var racialBonusManager = getRacialBonusManagerMethod.Invoke(null, null);
                if (racialBonusManager == null) return "Unknown";

                var getPlayerRaceMethod = racialBonusManager.GetType().GetMethod("GetPlayerRace");
                if (getPlayerRaceMethod != null)
                {
                    var race = getPlayerRaceMethod.Invoke(racialBonusManager, null);
                    if (race != null)
                    {
                        return race.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[RaceModifierTracker] Error getting race: {ex.Message}");
            }

            return "Unknown";
        }

        /// <summary>
        /// Get all active racial bonuses (requires Haven's Birthright)
        /// </summary>
        public List<RaceBonusInfo> GetActiveRaceBonuses()
        {
            var result = new List<RaceBonusInfo>();

            if (!Plugin.HasHavensBirthright) return result;

            try
            {
                var birthrightAssembly = GetBirthrightAssembly();
                if (birthrightAssembly == null) return result;

                var pluginType = birthrightAssembly.GetType("HavensBirthright.Plugin");
                if (pluginType == null) return result;

                var getRacialBonusManagerMethod = pluginType.GetMethod("GetRacialBonusManager", BindingFlags.Public | BindingFlags.Static);
                if (getRacialBonusManagerMethod == null) return result;

                var racialBonusManager = getRacialBonusManagerMethod.Invoke(null, null);
                if (racialBonusManager == null) return result;

                var getCurrentBonusesMethod = racialBonusManager.GetType().GetMethod("GetCurrentPlayerBonuses");
                if (getCurrentBonusesMethod == null) return result;

                var bonuses = getCurrentBonusesMethod.Invoke(racialBonusManager, null) as System.Collections.IList;
                if (bonuses == null) return result;

                foreach (var bonus in bonuses)
                {
                    var bonusType = bonus.GetType();
                    var typeProp = bonusType.GetProperty("Type");
                    var valueProp = bonusType.GetProperty("Value");
                    var isPercentProp = bonusType.GetProperty("IsPercentage");
                    var descProp = bonusType.GetProperty("Description");

                    var info = new RaceBonusInfo
                    {
                        Type = typeProp?.GetValue(bonus)?.ToString() ?? "Unknown",
                        Value = valueProp != null ? Convert.ToSingle(valueProp.GetValue(bonus)) : 0f,
                        IsPercentage = isPercentProp != null && (bool)isPercentProp.GetValue(bonus),
                        Description = descProp?.GetValue(bonus)?.ToString() ?? ""
                    };

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[RaceModifierTracker] Error getting bonuses: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get bonuses for a specific race (requires Haven's Birthright)
        /// </summary>
        public List<RaceBonusInfo> GetBonusesForRace(string raceName)
        {
            var result = new List<RaceBonusInfo>();

            if (!Plugin.HasHavensBirthright) return result;

            try
            {
                var birthrightAssembly = GetBirthrightAssembly();
                if (birthrightAssembly == null) return result;

                var pluginType = birthrightAssembly.GetType("HavensBirthright.Plugin");
                if (pluginType == null) return result;

                var getRacialBonusManagerMethod = pluginType.GetMethod("GetRacialBonusManager", BindingFlags.Public | BindingFlags.Static);
                if (getRacialBonusManagerMethod == null) return result;

                var racialBonusManager = getRacialBonusManagerMethod.Invoke(null, null);
                if (racialBonusManager == null) return result;

                // Parse race enum
                var raceEnumType = birthrightAssembly.GetType("HavensBirthright.Race");
                if (raceEnumType == null) return result;

                object raceEnum;
                try
                {
                    raceEnum = Enum.Parse(raceEnumType, raceName, true);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogDebug($"[RaceModifierTracker] Enum.Parse for race '{raceName}': {ex.Message}");
                    return result;
                }

                var getBonusesForRaceMethod = racialBonusManager.GetType().GetMethod("GetBonusesForRace");
                if (getBonusesForRaceMethod == null) return result;

                var bonuses = getBonusesForRaceMethod.Invoke(racialBonusManager, new object[] { raceEnum }) as System.Collections.IList;
                if (bonuses == null) return result;

                foreach (var bonus in bonuses)
                {
                    var bonusType = bonus.GetType();
                    var typeProp = bonusType.GetProperty("Type");
                    var valueProp = bonusType.GetProperty("Value");
                    var isPercentProp = bonusType.GetProperty("IsPercentage");
                    var descProp = bonusType.GetProperty("Description");

                    var info = new RaceBonusInfo
                    {
                        Type = typeProp?.GetValue(bonus)?.ToString() ?? "Unknown",
                        Value = valueProp != null ? Convert.ToSingle(valueProp.GetValue(bonus)) : 0f,
                        IsPercentage = isPercentProp != null && (bool)isPercentProp.GetValue(bonus),
                        Description = descProp?.GetValue(bonus)?.ToString() ?? ""
                    };

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[RaceModifierTracker] Error getting bonuses for race: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get all available races (requires Haven's Birthright)
        /// </summary>
        public List<string> GetAllRaces()
        {
            var result = new List<string>();

            if (!Plugin.HasHavensBirthright)
            {
                return new List<string>
                {
                    "Human", "Elf", "Angel", "Demon",
                    "Elemental", "FireElemental", "WaterElemental",
                    "Amari", "AmariCat", "AmariDog", "AmariBird", "AmariAquatic", "AmariReptile",
                    "Naga"
                };
            }

            try
            {
                var birthrightAssembly = GetBirthrightAssembly();
                if (birthrightAssembly == null) return result;

                var raceEnumType = birthrightAssembly.GetType("HavensBirthright.Race");
                if (raceEnumType != null)
                {
                    foreach (var value in Enum.GetValues(raceEnumType))
                    {
                        result.Add(value.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[RaceModifierTracker] Error getting races: {ex.Message}");
            }

            return result;
        }

        private Assembly GetBirthrightAssembly()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "HavensBirthright")
                {
                    return assembly;
                }
            }
            return null;
        }
    }

    public class RaceBonusInfo
    {
        public string Type { get; set; }
        public float Value { get; set; }
        public bool IsPercentage { get; set; }
        public string Description { get; set; }

        public string GetFormattedValue()
        {
            if (IsPercentage)
            {
                return Value >= 0 ? $"+{Value}%" : $"{Value}%";
            }
            return Value >= 0 ? $"+{Value}" : $"{Value}";
        }
    }
}
