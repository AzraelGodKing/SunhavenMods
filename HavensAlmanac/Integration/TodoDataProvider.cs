using System;
using System.Collections.Generic;
using HavensAlmanac.Data;
using SunhavenMods.Shared;
using UnityEngine;

namespace HavensAlmanac.Integration
{
    /// <summary>
    /// Soft integration with Sun Haven Todo.
    /// SunhavenTodo types may only appear inside method bodies (never in fields,
    /// signatures, or lambdas) so this assembly still loads and reflects cleanly
    /// when Sun Haven Todo is not installed (AZR-739).
    /// </summary>
    public class TodoDataProvider : IModDataProvider
    {
        private sealed class HighPriorityTodo
        {
            public int Priority;
            public int Order;
            public string Title;
            public bool IsUrgent;
        }

        public string ModName => "Todo";
        public string ModIcon => "\u270E";

        private int _activeCount;
        private int _totalCount;
        private int _completedCount;
        private float _completionPercent;
        private List<HighPriorityTodo> _highPriorityItems = new List<HighPriorityTodo>();
        private string _hudSummary = "Loading...";
        private bool _isReady;

        public string HudSummary => _hudSummary;
        public bool IsReady => _isReady;
        public bool HasBriefingContent => _isReady && _activeCount > 0;

        public void Refresh()
        {
            try
            {
                var manager = SunhavenTodo.Plugin.GetTodoManager();
                if (manager == null) { _isReady = false; return; }

                var stats = manager.GetStats();
                _totalCount = stats.total;
                _completedCount = stats.completed;
                _activeCount = stats.active;
                _completionPercent = manager.GetCompletionPercent();

                var highPriority = new List<HighPriorityTodo>();
                foreach (var todo in manager.GetActiveTodos())
                {
                    if (todo == null || todo.Priority < SunhavenTodo.Data.TodoPriority.High)
                        continue;
                    highPriority.Add(new HighPriorityTodo
                    {
                        Priority = (int)todo.Priority,
                        Order = highPriority.Count,
                        Title = todo.Title,
                        IsUrgent = todo.Priority == SunhavenTodo.Data.TodoPriority.Urgent
                    });
                }
                // Highest priority first; keep original order within a priority (stable, like OrderByDescending).
                highPriority.Sort(CompareHighPriority);
                if (highPriority.Count > MaxHighPriorityItems)
                    highPriority.RemoveRange(MaxHighPriorityItems, highPriority.Count - MaxHighPriorityItems);
                _highPriorityItems = highPriority;

                _hudSummary = _totalCount == 0
                    ? "No tasks"
                    : $"{_activeCount} active / {_completionPercent:F0}%";
                _isReady = true;
            }
            catch (Exception ex)
            {
                _hudSummary = "Error";
                _isReady = false;
                HavensAlmanac.Plugin.Log?.LogWarning($"[TodoProvider] Refresh error: {ex.Message}");
            }
        }

        private const int MaxHighPriorityItems = 5;

        private static int CompareHighPriority(HighPriorityTodo a, HighPriorityTodo b)
        {
            int byPriority = b.Priority.CompareTo(a.Priority);
            return byPriority != 0 ? byPriority : a.Order.CompareTo(b.Order);
        }

        public void DrawDashboardSection()
        {
            GUILayout.Label(ModLocalization.T("almanac.provider.todo.stats", _totalCount, _completedCount, _activeCount));

            // Progress bar
            var barRect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
            GUI.Box(barRect, "");
            if (_totalCount > 0)
            {
                var fillRect = new Rect(barRect.x + 1, barRect.y + 1, (barRect.width - 2) * (_completionPercent / 100f), barRect.height - 2);
                GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            }
            GUILayout.Label(ModLocalization.T("almanac.provider.todo.percent", _completionPercent.ToString("F0")));

            if (_highPriorityItems.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label(ModLocalization.T("almanac.provider.todo.highPriority"), GUI.skin.label);
                foreach (var item in _highPriorityItems)
                {
                    string priority = item.IsUrgent ? "[!] " : "[*] ";
                    GUILayout.Label($"  {priority}{item.Title}");
                }
            }
        }

        public bool DrawBriefingSection()
        {
            if (_activeCount == 0) return false;

            int highCount = _highPriorityItems.Count;
            string taskWord = _activeCount == 1
                ? ModLocalization.T("almanac.provider.todo.task")
                : ModLocalization.T("almanac.provider.todo.tasks");
            GUILayout.Label(ModLocalization.T("almanac.provider.todo.briefing.active", _activeCount, taskWord));
            if (highCount > 0)
                GUILayout.Label(ModLocalization.T("almanac.provider.todo.briefing.high", highCount));
            return true;
        }
    }
}
