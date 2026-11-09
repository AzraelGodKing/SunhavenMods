using System;
using System.Collections.Generic;
using HavensAlmanac.Config;
using HavensAlmanac.Data;
using SunhavenMods.Shared;
using UnityEngine;

namespace HavensAlmanac.UI
{
    /// <summary>
    /// Compact always-visible HUD showing one-line summaries from each installed mod.
    /// </summary>
    public class AlmanacHUD : MonoBehaviour
    {
        private const int WINDOW_ID = 98780;
        private const float BASE_WIDTH = 260f;
        private const float BASE_MIN_HEIGHT = 60f;
        private const float REFRESH_INTERVAL = 5f;

        private float _scale = 1f;
        private AlmanacDataAggregator _aggregator;
        private Rect _windowRect;
        private bool _isVisible = true;
        private float _refreshTimer;

        private float Width => BASE_WIDTH * _scale;
        private float MinHeight => BASE_MIN_HEIGHT * _scale;
        private float Scaled(float value) => value * _scale;
        private int ScaledFont(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * _scale));
        private int ScaledInt(float value) => Mathf.RoundToInt(value * _scale);

        // Styles
        private bool _stylesInitialized;
        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _iconStyle;
        private GUIStyle _summaryStyle;
        private GUIStyle _noModsStyle;
        private Texture2D _bgTexture;
        private Texture2D _headerTexture;

        // Position persistence
        public Action<float, float> OnPositionChanged;

        public bool IsVisible => _isVisible;

        public void Initialize(AlmanacDataAggregator aggregator)
        {
            _aggregator = aggregator;

            float x = AlmanacConfig.StaticHUDPositionX;
            float y = AlmanacConfig.StaticHUDPositionY;

            if (x < 0 || y < 0)
            {
                x = Screen.width - Width - Scaled(20);
                y = Scaled(80);
            }

            _windowRect = new Rect(x, y, Width, MinHeight);
        }

        public void SetScale(float scale)
        {
            _scale = Mathf.Clamp(scale, 0.5f, 2.5f);
            _stylesInitialized = false;
        }

        public void Show() => _isVisible = true;
        public void Hide() => _isVisible = false;
        public void Toggle() => _isVisible = !_isVisible;

        public void SetPosition(float x, float y)
        {
            _windowRect.x = x;
            _windowRect.y = y;
        }

        private void Update()
        {
            if (!_isVisible || _aggregator == null) return;

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= REFRESH_INTERVAL)
            {
                _refreshTimer = 0f;
                _aggregator.RefreshAll();
            }
        }

        private void OnGUI()
        {
            if (!_isVisible || !AlmanacConfig.StaticHUDEnabled) return;
            if (_aggregator == null) return;

            // Hide when dashboard or briefing is visible
            var dashboard = Plugin.GetAlmanacDashboard();
            var briefing = Plugin.GetDailyBriefing();
            if (dashboard != null && dashboard.IsVisible) return;
            if (briefing != null && briefing.IsVisible) return;

            if (!_stylesInitialized)
                InitializeStyles();

            _windowRect = GUI.Window(WINDOW_ID, _windowRect, DrawWindow, "", _windowStyle);
            ClampToScreen();
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("almanac.hud.title"), _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("x", GUILayout.Width(Scaled(18)), GUILayout.Height(Scaled(18))))
                Hide();
            GUILayout.EndHorizontal();

            GUILayout.Space(Scaled(2));

            // The always-registered Mod Health provider alone isn't enough to
            // make the HUD useful; nudge the user to install a companion mod
            // rather than showing a lonely telemetry line.
            if (_aggregator.IntegrationModCount == 0)
            {
                GUILayout.Label(ModLocalization.T("almanac.hud.noMods"), _noModsStyle);
                GUILayout.Label(ModLocalization.T("almanac.hud.installHint"), _noModsStyle);
            }
            else
            {
                foreach (var provider in _aggregator.Providers)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(provider.ModIcon, _iconStyle, GUILayout.Width(Scaled(22)));
                    GUILayout.Label(GetSummaryLine(provider), _summaryStyle);
                    GUILayout.EndHorizontal();
                }
            }

            // Drag the entire window
            GUI.DragWindow();
        }

        // AZR-734: "{name}: {summary}" was rebuilt for every provider on every OnGUI event.
        // Providers keep HudSummary cached between refreshes, so rebuild only when either string changes.
        private readonly Dictionary<IModDataProvider, CachedSummaryLine> _summaryLines =
            new Dictionary<IModDataProvider, CachedSummaryLine>();

        private sealed class CachedSummaryLine
        {
            public string Name;
            public string Summary;
            public string Line;
        }

        private string GetSummaryLine(IModDataProvider provider)
        {
            string name = provider.ModName;
            string summary = provider.HudSummary;
            if (!_summaryLines.TryGetValue(provider, out var cached))
            {
                cached = new CachedSummaryLine();
                _summaryLines[provider] = cached;
            }
            else if (ReferenceEquals(cached.Name, name) && ReferenceEquals(cached.Summary, summary))
            {
                return cached.Line;
            }

            cached.Name = name;
            cached.Summary = summary;
            cached.Line = $"{name}: {summary}";
            return cached.Line;
        }

        private void ClampToScreen()
        {
            float prevX = _windowRect.x;
            float prevY = _windowRect.y;

            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);

            if (Math.Abs(prevX - _windowRect.x) > 1f || Math.Abs(prevY - _windowRect.y) > 1f)
                OnPositionChanged?.Invoke(_windowRect.x, _windowRect.y);
        }

        private void InitializeStyles()
        {
            _stylesInitialized = true;

            // Warm parchment colors
            var bgColor = new Color(0.18f, 0.14f, 0.10f, 0.92f);
            var headerColor = new Color(0.22f, 0.17f, 0.12f, 0.95f);
            var goldText = new Color(0.95f, 0.85f, 0.55f);
            var lightText = new Color(0.91f, 0.87f, 0.82f);

            _bgTexture = GUIStyleHelper.MakeSolidTexture(bgColor);
            _headerTexture = GUIStyleHelper.MakeSolidTexture(headerColor);

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(6), ScaledInt(6)),
                border = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };
            _windowStyle.normal.background = _bgTexture;
            _windowStyle.onNormal.background = _bgTexture;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = ScaledFont(13),
                alignment = TextAnchor.MiddleLeft
            };
            _titleStyle.normal.textColor = goldText;

            _iconStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(14),
                alignment = TextAnchor.MiddleCenter
            };

            _summaryStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(12),
                alignment = TextAnchor.MiddleLeft
            };
            _summaryStyle.normal.textColor = lightText;

            _noModsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(11),
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            _noModsStyle.normal.textColor = new Color(0.7f, 0.6f, 0.5f);
        }

        private void OnDestroy()
        {
            if (_bgTexture != null) Destroy(_bgTexture);
            if (_headerTexture != null) Destroy(_headerTexture);
        }
    }
}
