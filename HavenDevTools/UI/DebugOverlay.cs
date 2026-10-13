using System;
using HavenDevTools.Config;
using UnityEngine;
using Wish;
using SunhavenMods.Shared;
namespace HavenDevTools.UI
{
    /// <summary>
    /// Lightweight in-game debug overlay for quick information display
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        private bool _isVisible;
        private GUIStyle _overlayStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private bool _stylesInitialized;

        private float _updateTimer;
        private const float UPDATE_INTERVAL = 0.5f;

        // Cached data
        private string _playerName = "";
        private string _currentRace = "";
        private int _gold;
        private string _heldItemInfo = "";
        private string _position = "";

        private void Awake()
        {
            _isVisible = ModConfig.ShowOverlayOnStart?.Value ?? false;
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
            Plugin.Log?.LogInfo($"[DebugOverlay] {(_isVisible ? "Shown" : "Hidden")}");
        }

        private void Update()
        {
            if (!_isVisible) return;

            _updateTimer += Time.deltaTime;
            if (_updateTimer >= UPDATE_INTERVAL)
            {
                _updateTimer = 0f;
                UpdateCachedData();
            }
        }

        private void UpdateCachedData()
        {
            try
            {
                _playerName = Plugin.CurrentPlayerName ?? "Unknown";

                // Race
                var raceTracker = Plugin.GetRaceModifierTracker();
                _currentRace = raceTracker?.GetCurrentRace() ?? "Unknown";

                // Gold
                var currencyTracker = Plugin.GetCurrencyTracker();
                _gold = currencyTracker?.GetGold() ?? 0;

                // Held item
                var heldItem = Plugin.GetItemInspector()?.GetHeldItem();
                _heldItemInfo = heldItem.HasValue ? $"{heldItem.Value.name} ({heldItem.Value.id})" : "None";

                // Position
                if (Wish.Player.Instance != null)
                {
                    var pos = Wish.Player.Instance.transform.position;
                    _position = $"({pos.x:F1}, {pos.y:F1})";
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[DebugOverlay] Update error: {ex.Message}");
            }
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            InitializeStyles();

            var position = ModConfig.GetOverlayPosition();
            Rect overlayRect = GetOverlayRect(position);

            GUI.Box(overlayRect, "", _overlayStyle);

            GUILayout.BeginArea(new Rect(overlayRect.x + 8, overlayRect.y + 8, overlayRect.width - 16, overlayRect.height - 16));

            DrawOverlayContent();

            GUILayout.EndArea();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            var overlayBg = new Texture2D(1, 1);
            overlayBg.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.1f, 0.85f));
            overlayBg.Apply();

            _overlayStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = overlayBg }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.7f, 0.8f) }
            };

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.95f, 1f) }
            };

            _stylesInitialized = true;
        }

        private Rect GetOverlayRect(OverlayPositionType position)
        {
            float width = 220;
            float height = ModConfig.ShowPerformance?.Value ?? true ? 155 : 130;
            float margin = 10;

            return position switch
            {
                OverlayPositionType.TopLeft => new Rect(margin, margin, width, height),
                OverlayPositionType.TopRight => new Rect(Screen.width - width - margin, margin, width, height),
                OverlayPositionType.BottomLeft => new Rect(margin, Screen.height - height - margin, width, height),
                OverlayPositionType.BottomRight => new Rect(Screen.width - width - margin, Screen.height - height - margin, width, height),
                _ => new Rect(Screen.width - width - margin, margin, width, height)
            };
        }

        private void DrawOverlayContent()
        {
            GUILayout.Label(ModLocalization.T("devtools.title"), _valueStyle);
            GUILayout.Space(3);

            DrawRow(ModLocalization.T("devtools.overlay.player"), _playerName);
            DrawRow(ModLocalization.T("devtools.overlay.race"), _currentRace);
            DrawRow(ModLocalization.T("devtools.overlay.gold"), _gold.ToString("N0"));
            DrawRow(ModLocalization.T("devtools.overlay.position"), _position);
            DrawRow(ModLocalization.T("devtools.overlay.held"), _heldItemInfo);

            if (ModConfig.ShowPerformance?.Value ?? true)
            {
                GUILayout.Label(ModLocalization.T("devtools.perf.fps", PerformanceSampler.SmoothedFps), _valueStyle);
                GUILayout.Label(ModLocalization.T("devtools.perf.memory", PerformanceSampler.MemoryMb), _valueStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(ModLocalization.T("devtools.overlay.keys"), _labelStyle);
        }

        private void DrawRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(55));
            GUILayout.Label(value, _valueStyle);
            GUILayout.EndHorizontal();
        }
    }
}
