using System;
using System.Collections.Generic;
using HavensAlmanac.Data;
using SunhavenMods.Shared;
using UnityEngine;

namespace HavensAlmanac.Integration
{
    /// <summary>
    /// Soft integration with Birthday Reminder.
    /// BirthdayReminder types may only appear inside method bodies (never in fields,
    /// signatures, or lambdas) so this assembly still loads and reflects cleanly
    /// when Birthday Reminder is not installed (AZR-739).
    /// </summary>
    public class BirthdayDataProvider : IModDataProvider
    {
        private sealed class BirthdayEntry
        {
            public string NPCName;
            public string GiftHint;
            public bool HasBeenGifted;
        }

        public string ModName => "Birthday";
        public string ModIcon => "\u2605";

        private List<BirthdayEntry> _birthdays = new List<BirthdayEntry>();
        private int _ungiftedCount;
        private string _hudSummary = "Loading...";
        private bool _isReady;

        public string HudSummary => _hudSummary;
        public bool IsReady => _isReady;
        public bool HasBriefingContent => _isReady && _birthdays != null && _birthdays.Count > 0;

        public void Refresh()
        {
            try
            {
                var manager = BirthdayReminder.Plugin.GetManager();
                if (manager == null) { _isReady = false; return; }

                var birthdays = new List<BirthdayEntry>();
                int ungifted = 0;
                var today = manager.TodaysBirthdays;
                if (today != null)
                {
                    foreach (var b in today)
                    {
                        if (b == null) continue;
                        birthdays.Add(new BirthdayEntry
                        {
                            NPCName = b.NPCName,
                            GiftHint = b.GiftHint,
                            HasBeenGifted = b.HasBeenGifted
                        });
                        if (!b.HasBeenGifted) ungifted++;
                    }
                }
                _birthdays = birthdays;
                _ungiftedCount = ungifted;

                if (_birthdays.Count == 0)
                    _hudSummary = "No birthdays";
                else
                    _hudSummary = $"{_birthdays.Count} birthday{(_birthdays.Count != 1 ? "s" : "")} / {_ungiftedCount} ungifted";

                _isReady = true;
            }
            catch (Exception ex)
            {
                _hudSummary = "Error";
                _isReady = false;
                HavensAlmanac.Plugin.Log?.LogWarning($"[BirthdayProvider] Refresh error: {ex.Message}");
            }
        }

        public void DrawDashboardSection()
        {
            if (_birthdays.Count == 0)
            {
                GUILayout.Label(ModLocalization.T("almanac.provider.birthday.noBirthdays"));
                return;
            }

            GUILayout.Label(ModLocalization.T("almanac.provider.birthday.todayList", _birthdays.Count));
            GUILayout.Space(4);

            foreach (var birthday in _birthdays)
            {
                string status = birthday.HasBeenGifted
                    ? ModLocalization.T("almanac.provider.birthday.gifted")
                    : ModLocalization.T("almanac.provider.birthday.notGifted");
                GUILayout.Label($"  {birthday.NPCName}{status}");

                if (!string.IsNullOrEmpty(birthday.GiftHint))
                {
                    GUILayout.Label($"    {birthday.GiftHint}");
                }
            }
        }

        public bool DrawBriefingSection()
        {
            if (_birthdays.Count == 0) return false;

            foreach (var birthday in _birthdays)
            {
                string status = birthday.HasBeenGifted
                    ? ModLocalization.T("almanac.provider.birthday.giftedBriefing")
                    : "";
                GUILayout.Label(ModLocalization.T("almanac.provider.birthday.briefing", birthday.NPCName, status));
            }

            if (_ungiftedCount > 0)
                GUILayout.Label(ModLocalization.T("almanac.provider.birthday.reminder"));

            return true;
        }
    }
}
