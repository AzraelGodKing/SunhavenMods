using System;
using System.Collections.Generic;
using HavensAlmanac.Data;
using SunhavenMods.Shared;
using UnityEngine;

namespace HavensAlmanac.Integration
{
    /// <summary>
    /// Soft integration with Gifting Assistant.
    /// GiftingAssistant types may only appear inside method bodies (never in fields,
    /// signatures, or lambdas) so this assembly still loads and reflects cleanly
    /// when Gifting Assistant is not installed (AZR-739).
    /// </summary>
    public class GiftingAssistantDataProvider : IModDataProvider
    {
        private sealed class PendingEntry
        {
            public string Priority;
            public string NpcName;
        }

        public string ModName => "Gifting";
        public string ModIcon => "\u2665";

        private string _hudSummary = "Loading...";
        private bool _isReady;
        private bool _integrationEnabled;
        private int _rosterCount;
        private int _pendingCount;
        private int _highPriorityPending;
        private int _urgentPending;
        private readonly List<PendingEntry> _pendingEntries = new List<PendingEntry>();

        public string HudSummary => _hudSummary;
        public bool IsReady => _isReady;
        public bool HasBriefingContent => _isReady && _integrationEnabled && _pendingCount > 0;

        public void Refresh()
        {
            _integrationEnabled = false;
            _rosterCount = 0;
            _pendingCount = 0;
            _highPriorityPending = 0;
            _urgentPending = 0;
            _pendingEntries.Clear();

            try
            {
                _integrationEnabled = GiftingAssistant.Data.GiftingAssistantAlmanacData.IsIntegrationEnabled;
                if (!_integrationEnabled)
                {
                    _hudSummary = ModLocalization.T("almanac.provider.gifting.disabled");
                    _isReady = true;
                    return;
                }

                if (!GiftingAssistant.Data.GiftingAssistantAlmanacData.TryGetSummary(out _hudSummary, out _rosterCount, out _pendingCount))
                {
                    _hudSummary = "Not ready";
                    _isReady = false;
                    return;
                }

                foreach (var entry in GiftingAssistant.Data.GiftingAssistantAlmanacData.GetSortedRosterEntries())
                {
                    if (entry.IsGiftedToday)
                        continue;

                    _pendingEntries.Add(new PendingEntry
                    {
                        Priority = entry.Priority.ToString(),
                        NpcName = entry.NpcName
                    });
                    if (entry.Priority >= GiftingAssistant.Data.GiftPriority.High)
                        _highPriorityPending++;
                    if (entry.Priority >= GiftingAssistant.Data.GiftPriority.Urgent)
                        _urgentPending++;
                }

                _isReady = true;
            }
            catch (Exception ex)
            {
                _hudSummary = "Error";
                _isReady = false;
                HavensAlmanac.Plugin.Log?.LogWarning($"[GiftingProvider] Refresh error: {ex.Message}");
            }
        }

        public void DrawDashboardSection()
        {
            if (!_integrationEnabled)
            {
                GUILayout.Label(ModLocalization.T("almanac.provider.gifting.disabled"));
                return;
            }

            if (_rosterCount == 0)
            {
                GUILayout.Label(ModLocalization.T("almanac.provider.gifting.noRoster"));
                return;
            }

            GUILayout.Label(ModLocalization.T("almanac.provider.gifting.stats", _rosterCount, _pendingCount));

            if (_pendingEntries.Count == 0)
                return;

            GUILayout.Space(4);
            GUILayout.Label(ModLocalization.T("almanac.provider.gifting.pendingHeader"), GUI.skin.label);
            foreach (var entry in _pendingEntries)
            {
                GUILayout.Label(ModLocalization.T("almanac.provider.gifting.priorityRow",
                    entry.Priority, entry.NpcName, ModLocalization.T("almanac.provider.gifting.notGifted")));
            }
        }

        public bool DrawBriefingSection()
        {
            if (!_integrationEnabled || _pendingCount == 0)
                return false;

            string npcWord = _pendingCount == 1
                ? ModLocalization.T("almanac.provider.gifting.npc")
                : ModLocalization.T("almanac.provider.gifting.npcs");
            GUILayout.Label(ModLocalization.T("almanac.provider.gifting.briefing.remaining", _pendingCount, npcWord));

            if (_highPriorityPending > 0)
                GUILayout.Label(ModLocalization.T("almanac.provider.gifting.briefing.high", _highPriorityPending));
            if (_urgentPending > 0)
                GUILayout.Label(ModLocalization.T("almanac.provider.gifting.briefing.urgent", _urgentPending));

            return true;
        }
    }
}
