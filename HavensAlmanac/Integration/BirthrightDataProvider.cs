using System;
using System.Collections.Generic;
using HavensAlmanac.Data;
using SunhavenMods.Shared;
using UnityEngine;

namespace HavensAlmanac.Integration
{
    /// <summary>
    /// Soft integration with Haven's Birthright.
    /// HavensBirthright types may only appear inside method bodies (never in fields,
    /// signatures, or lambdas) so this assembly still loads and reflects cleanly
    /// when Haven's Birthright is not installed (AZR-739).
    /// </summary>
    public class BirthrightDataProvider : IModDataProvider
    {
        private sealed class BonusEntry
        {
            public string Description;
            public string FormattedValue;
        }

        public string ModName => "Birthright";
        public string ModIcon => "\u2694";

        private string _raceName = "None";
        private int _bonusCount;
        private List<BonusEntry> _bonuses = new List<BonusEntry>();
        private string _hudSummary = "Loading...";
        private bool _isReady;

        public string HudSummary => _hudSummary;
        public bool IsReady => _isReady;

        // Racial bonuses are static per-character; nothing daily to brief.
        public bool HasBriefingContent => false;

        public void Refresh()
        {
            try
            {
                var manager = HavensBirthright.Plugin.GetRacialBonusManager();
                if (manager == null) { _isReady = false; return; }

                var race = manager.GetPlayerRace();
                if (race.HasValue)
                {
                    _raceName = race.Value.ToString();
                    var bonuses = new List<BonusEntry>();
                    var current = manager.GetCurrentPlayerBonuses();
                    if (current != null)
                    {
                        foreach (var bonus in current)
                        {
                            if (bonus == null) continue;
                            bonuses.Add(new BonusEntry
                            {
                                Description = bonus.Description,
                                FormattedValue = bonus.GetFormattedValue()
                            });
                        }
                    }
                    _bonuses = bonuses;
                    _bonusCount = _bonuses.Count;
                    _hudSummary = $"{_raceName} ({_bonusCount} bonus{(_bonusCount != 1 ? "es" : "")})";
                }
                else
                {
                    _raceName = "None";
                    _bonusCount = 0;
                    _bonuses.Clear();
                    _hudSummary = "No race set";
                }

                _isReady = true;
            }
            catch (Exception ex)
            {
                _hudSummary = "Error";
                _isReady = false;
                HavensAlmanac.Plugin.Log?.LogWarning($"[BirthrightProvider] Refresh error: {ex.Message}");
            }
        }

        public void DrawDashboardSection()
        {
            GUILayout.Label(ModLocalization.T("almanac.provider.birthright.race", _raceName));

            if (_bonusCount == 0)
            {
                GUILayout.Label(ModLocalization.T("almanac.provider.birthright.noBonuses"));
                return;
            }

            GUILayout.Label(ModLocalization.T("almanac.provider.birthright.activeBonuses", _bonusCount));
            GUILayout.Space(4);

            foreach (var bonus in _bonuses)
            {
                GUILayout.Label($"  {bonus.Description}: {bonus.FormattedValue}");
            }
        }

        public bool DrawBriefingSection()
        {
            // Racial data is static, not daily-relevant
            return false;
        }
    }
}
