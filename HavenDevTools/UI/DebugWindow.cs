using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HavenDevTools.API;
using HavenDevTools.Config;
using HavenDevTools.Integrations;
using HavenDevTools.Services;
using SunhavenMods.Shared;
using UnityEngine;
using Wish;

namespace HavenDevTools.UI
{
    /// <summary>
    /// Main debug window UI (IMGUI-based)
    /// </summary>
    public class DebugWindow : MonoBehaviour
    {
        private bool _isVisible;
        private Rect _windowRect;
        private Vector2 _scrollPosition;

        // Styles
        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInitialized;

        // Window chrome
        private bool _isResizing;
        private Vector2 _resizeStartMouse;
        private Vector2 _resizeStartSize;
        private float _contentAreaHeight = 280f;
        private GUIStyle _headerBarStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _fpsHeaderStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _resizeGripStyle;
        private Texture2D _headerBarTexture;
        private Texture2D _resizeGripTexture;

        // Tab state
        private int _selectedTab;
        private int _toolsSubTab;
        private string _cachedTabLabelsLanguage;
        private string[] _mainTabLabels;
        private string[] _toolsSubTabLabels;
        private List<string> _raceNamesSource;
        private string[] _raceNamesForGrid;

        private static readonly HashSet<string> AzraelsExtensionGuids =
            new HashSet<string> { "com.azraelgodking.trinketfortune" };

        // Item search (Senpai's Chest style)
        private string _itemSearchText = "";
        private string _lastItemSearchQuery = null;
        private string _itemIdInput = "";
        private string _spawnAmount = "1";
        private int _selectedItemId;
        private string _selectedItemName = "";
        private List<KeyValuePair<int, string>> _searchResults = new List<KeyValuePair<int, string>>();
        private Vector2 _itemScrollPosition;

        // Currency state
        private Vector2 _currencyScrollPosition;

        // Bundle state
        private int _selectedSectionIndex;
        private int _selectedBundleIndex;
        private Vector2 _bundleScrollPosition;

        // Race state
        private int _selectedRaceIndex;
        private Vector2 _raceScrollPosition;

        // NPC relationships (CheatEnabler-style)
        private bool _relationshipRomanceOnly = true;
        private string _relationshipFilter = "";
        private int _selectedRelationshipIndex = -1;
        private string _relationshipHeartsInput = "40";
        private string _relationshipCycleInput = "8";
        private Vector2 _relationshipScrollPosition;
        private string _relationshipStatusMessage = "";
        private float _relationshipStatusUntil;
        private List<NpcRelationshipEditor.NpcRelationshipRow> _relationshipRows = new List<NpcRelationshipEditor.NpcRelationshipRow>();
        private bool _requestConsoleFocus;

        // Marriable (multi-marriage via Ultra Polygamy)
        private string _marriableFilter = "";
        private Vector2 _marriableScrollPosition;
        private readonly HashSet<string> _marriableSelectedKeys = new HashSet<string>(StringComparer.Ordinal);
        private List<NpcRelationshipEditor.NpcRelationshipRow> _marriableRows = new List<NpcRelationshipEditor.NpcRelationshipRow>();
        private string _marriableStatusMessage = "";
        private float _marriableStatusUntil;

        private const string PAUSE_ID = "HavenDevTools_Debug";

        private void Awake()
        {
            float w = ModConfig.DebugWindowWidth?.Value ?? DebugWindowLayout.DefaultWidth;
            float h = ModConfig.DebugWindowHeight?.Value ?? DebugWindowLayout.DefaultHeight;
            _windowRect = new Rect(50, 50, w, h);
            DebugWindowLayout.ClampToScreen(ref _windowRect);
        }

        public void Toggle()
        {
            if (_isVisible)
                Hide();
            else
                Show();
        }

        public void Show()
        {
            _isVisible = true;
            BlockInput();
            Plugin.Log?.LogDebug("[DebugWindow] Shown");
        }

        public void Hide()
        {
            _isVisible = false;
            UnblockInput();
            GUIUtility.keyboardControl = 0;
            Plugin.Log?.LogDebug("[DebugWindow] Hidden");
        }

        private void BlockInput()
        {
            if (ModConfig.PauseGameWhenDebugOpen?.Value != true)
                return;

            try
            {
                if (Wish.Player.Instance != null)
                    Wish.Player.Instance.AddPauseObject(PAUSE_ID);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[DebugWindow] Could not block input: {ex.Message}");
            }
        }

        private void UnblockInput()
        {
            try
            {
                if (Wish.Player.Instance != null)
                    Wish.Player.Instance.RemovePauseObject(PAUSE_ID);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[DebugWindow] Could not unblock input: {ex.Message}");
            }
        }

        private void Update()
        {
            if (_isVisible && Input.GetKeyDown(KeyCode.Escape))
                Hide();
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            InitializeStyles();
            DebugWindowLayout.ClampToScreen(ref _windowRect);

            _windowRect = GUI.Window(
                GetHashCode(),
                _windowRect,
                DrawWindow,
                "",
                _windowStyle
            );

            DebugWindowLayout.ClampToScreen(ref _windowRect);
        }

        private float ListHeight(float minHeight, float heightRatio = 0.42f)
        {
            return Mathf.Max(minHeight, _contentAreaHeight * heightRatio);
        }

        private void EnsureToolbarLabels()
        {
            string lang = ModLocalization.CurrentLanguage;
            if (_mainTabLabels != null && string.Equals(_cachedTabLabelsLanguage, lang, StringComparison.OrdinalIgnoreCase))
                return;

            _cachedTabLabelsLanguage = lang;
            _mainTabLabels = new[]
            {
                ModLocalization.T("devtools.tab.health"),
                ModLocalization.T("devtools.tab.tools"),
                ModLocalization.T("devtools.tab.suite"),
                ModLocalization.T("devtools.tab.extensions")
            };
            _toolsSubTabLabels = new[]
            {
                ModLocalization.T("devtools.tab.relationships"),
                ModLocalization.T("devtools.tab.marriable"),
                ModLocalization.T("devtools.tab.items"),
                ModLocalization.T("devtools.tab.currencies"),
                ModLocalization.T("devtools.tab.console"),
                ModLocalization.T("devtools.tab.log"),
                ModLocalization.T("devtools.tab.perf"),
                ModLocalization.T("devtools.tab.utility")
            };
        }

        private string[] GetRaceNamesForGrid(IReadOnlyList<string> races)
        {
            if (_raceNamesSource != null && _raceNamesForGrid != null && _raceNamesSource.Count == races.Count)
            {
                bool same = true;
                for (int i = 0; i < races.Count; i++)
                {
                    if (!string.Equals(_raceNamesSource[i], races[i], StringComparison.Ordinal))
                    {
                        same = false;
                        break;
                    }
                }
                if (same)
                    return _raceNamesForGrid;
            }

            _raceNamesSource = new List<string>(races);
            _raceNamesForGrid = _raceNamesSource.ToArray();
            return _raceNamesForGrid;
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            var darkBg = new Texture2D(1, 1);
            darkBg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.12f, 0.98f));
            darkBg.Apply();

            var buttonBg = new Texture2D(1, 1);
            buttonBg.SetPixel(0, 0, new Color(0.2f, 0.4f, 0.6f, 0.9f));
            buttonBg.Apply();

            var buttonHover = new Texture2D(1, 1);
            buttonHover.SetPixel(0, 0, new Color(0.3f, 0.5f, 0.7f, 0.95f));
            buttonHover.Apply();

            var sectionBg = new Texture2D(1, 1);
            sectionBg.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.18f, 0.9f));
            sectionBg.Apply();

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = darkBg, textColor = Color.white },
                padding = new RectOffset(10, 10, 25, 10)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.4f, 0.8f, 1f) }
            };

            _sectionHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.9f, 1f) }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                normal = { background = buttonBg, textColor = Color.white },
                hover = { background = buttonHover, textColor = Color.white },
                padding = new RectOffset(8, 8, 4, 4)
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.9f, 0.95f) }
            };

            _textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = sectionBg }
            };

            _headerBarTexture = new Texture2D(1, 1);
            _headerBarTexture.SetPixel(0, 0, new Color(0.14f, 0.18f, 0.28f, 0.98f));
            _headerBarTexture.Apply();
            _headerBarStyle = new GUIStyle
            {
                normal = { background = _headerBarTexture }
            };

            _subtitleStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.78f, 0.88f) }
            };

            var closeBg = new Texture2D(1, 1);
            closeBg.SetPixel(0, 0, new Color(0.45f, 0.18f, 0.18f, 0.9f));
            closeBg.Apply();
            var closeHover = new Texture2D(1, 1);
            closeHover.SetPixel(0, 0, new Color(0.65f, 0.22f, 0.22f, 0.95f));
            closeHover.Apply();
            _closeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { background = closeBg, textColor = Color.white },
                hover = { background = closeHover, textColor = Color.white }
            };

            _resizeGripTexture = DebugWindowLayout.CreateResizeGripTexture();
            _resizeGripStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _resizeGripTexture },
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };

            _windowStyle.padding = new RectOffset(8, 8, 8, 8);

            _stylesInitialized = true;
        }

        private void DrawHeaderBar(int windowId)
        {
            var headerRect = GUILayoutUtility.GetRect(0, DebugWindowLayout.HeaderHeight, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(headerRect, _headerBarTexture, ScaleMode.StretchToFill);

            const float closeWidth = 30f;
            var titleArea = new Rect(headerRect.x + 10f, headerRect.y + 4f, headerRect.width - closeWidth - 16f, headerRect.height - 6f);
            GUI.Label(new Rect(titleArea.x, titleArea.y, titleArea.width, 20f), ModLocalization.T("devtools.title"), _headerStyle);
            GUI.Label(
                new Rect(titleArea.x, titleArea.y + 20f, titleArea.width, 18f),
                ModLocalization.T("devtools.player", Plugin.CurrentPlayerName ?? string.Empty),
                _subtitleStyle);

            var closeRect = new Rect(headerRect.xMax - closeWidth - 6f, headerRect.y + 8f, closeWidth, 24f);
            if (GUI.Button(closeRect, "×", _closeButtonStyle))
                Hide();

            if (_fpsHeaderStyle == null)
            {
                _fpsHeaderStyle = new GUIStyle(_subtitleStyle)
                {
                    alignment = TextAnchor.MiddleRight
                };
            }
            _fpsHeaderStyle.normal.textColor = PerformanceSampler.FpsColor(PerformanceSampler.SmoothedFps);

            GUI.Label(
                new Rect(headerRect.xMax - closeWidth - 96f, headerRect.y + 10f, 84f, 20f),
                ModLocalization.T("devtools.perf.fps", PerformanceSampler.SmoothedFps),
                _fpsHeaderStyle);

            GUI.DragWindow(new Rect(0, 0, _windowRect.width - closeWidth - 8f, DebugWindowLayout.HeaderHeight));
        }

        private void DrawModStatusRow()
        {
            string yes = ModLocalization.T("devtools.yes");
            string no = ModLocalization.T("devtools.no");

            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("devtools.mod.thevault", Plugin.HasTheVault ? yes : no), _labelStyle);
            GUILayout.Label(ModLocalization.T("devtools.mod.smut", Plugin.HasSMUT ? yes : no), _labelStyle);
            GUILayout.Label(ModLocalization.T("devtools.mod.birthright", Plugin.HasHavensBirthright ? yes : no), _labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("devtools.mod.senpai", Plugin.HasSenpaisChest ? yes : no), _labelStyle);
            GUILayout.Label(ModLocalization.T("devtools.mod.todo", Plugin.HasSunhavenTodo ? yes : no), _labelStyle);
            GUILayout.Label(ModLocalization.T("devtools.mod.ultrapolygamy", Plugin.HasUltraPolygamy ? yes : no), _labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawWindowFooter()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("devtools.window.hint"), _subtitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (DebugWindowLayout.DrawResizeGrip(
                    _windowRect.width,
                    _windowRect.height,
                    _resizeGripStyle,
                    ref _isResizing,
                    ref _resizeStartMouse,
                    ref _resizeStartSize,
                    out float newWidth,
                    out float newHeight))
            {
                _windowRect.width = newWidth;
                _windowRect.height = newHeight;
                ModConfig.SaveDebugWindowSize(newWidth, newHeight);
            }
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));

            DrawHeaderBar(windowId);
            GUILayout.Space(6);
            DrawModStatusRow();
            GUILayout.Space(8);

            EnsureToolbarLabels();
            _selectedTab = GUILayout.Toolbar(_selectedTab, _mainTabLabels, _buttonStyle);
            GUILayout.Space(8);

            _contentAreaHeight = Mathf.Max(120f, _windowRect.height - 210f);
            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true));

            switch (_selectedTab)
            {
                case 0:
                    ModHealthDashboardPanel.RefreshRows(force: false);
                    ModHealthDashboardPanel.Draw(_boxStyle, _sectionHeaderStyle, _buttonStyle, _labelStyle);
                    break;
                case 1: DrawToolsTab(); break;
                case 2: AzraelsModsPanel.Draw(_boxStyle, _buttonStyle, _labelStyle, _sectionHeaderStyle); break;
                case 3: DrawExtensionsTab(); break;
            }

            GUILayout.EndScrollView();
            DrawWindowFooter();
            GUILayout.EndVertical();
        }

        private void DrawExtensionsTab()
        {
            var panels = DevToolsRegistry.Panels;
            if (panels.Count == 0)
            {
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Label(ModLocalization.T("devtools.extensions"), _sectionHeaderStyle);
                GUILayout.Space(5);
                GUILayout.Label(ModLocalization.T("devtools.extensions.none"), _labelStyle);
                GUILayout.EndVertical();
                return;
            }

            // Azrael's mods shown in Azrael's Mods tab - exclude from Extensions
            foreach (var panel in panels)
            {
                if (AzraelsExtensionGuids.Contains(panel.ModGuid)) continue;
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Label(panel.DisplayName, _sectionHeaderStyle);
                GUILayout.Space(5);
                try
                {
                    panel.Draw(_boxStyle, _buttonStyle, _labelStyle);
                }
                catch (Exception ex)
                {
                    GUILayout.Label($"Error: {ex.Message}", _labelStyle);
                }
                GUILayout.EndVertical();
                GUILayout.Space(8);
            }
        }

        #region Tools Tab

        private void DrawToolsTab()
        {
            EnsureToolbarLabels();
            _toolsSubTab = GUILayout.Toolbar(_toolsSubTab, _toolsSubTabLabels, _buttonStyle);
            GUILayout.Space(8);

            if (_toolsSubTab == 4 && _lastDrawnToolsSubTab != 4)
                _requestConsoleFocus = true;

            switch (_toolsSubTab)
            {
                case 0: DrawRelationshipsTab(); break;
                case 1: DrawMarriableTab(); break;
                case 2: DrawItemsTab(); break;
                case 3: DrawCurrenciesTab(); break;
                case 4: DrawConsoleTab(); break;
                case 5: DrawLogViewerTab(); break;
                case 6: DrawPerformanceTab(); break;
                case 7: DrawUtilityTab(); break;
            }

            _lastDrawnToolsSubTab = _toolsSubTab;
        }

        private void DrawRelationshipsTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(ModLocalization.T("devtools.relationships.title"), _sectionHeaderStyle);
            GUILayout.Space(4);

            if (!NpcRelationshipEditor.IsAvailable)
            {
                GUILayout.Label(ModLocalization.T("devtools.relationships.unavailable"), _labelStyle);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            _relationshipRomanceOnly = GUILayout.Toggle(_relationshipRomanceOnly, ModLocalization.T("devtools.relationships.romanceOnly"), _labelStyle);
            if (GUILayout.Button(ModLocalization.T("devtools.relationships.refresh"), _buttonStyle, GUILayout.Width(80)))
                RefreshRelationshipRows();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("devtools.relationships.filter"), _labelStyle, GUILayout.Width(40));
            _relationshipFilter = GUILayout.TextField(_relationshipFilter ?? string.Empty, _textFieldStyle);
            if (GUILayout.Button("X", _buttonStyle, GUILayout.Width(24)))
            {
                _relationshipFilter = string.Empty;
                RefreshRelationshipRows();
            }
            GUILayout.EndHorizontal();

            if (_relationshipRows.Count == 0)
                RefreshRelationshipRows();

            _relationshipScrollPosition = GUILayout.BeginScrollView(_relationshipScrollPosition, GUILayout.Height(ListHeight(100f, 0.45f)));
            for (int i = 0; i < _relationshipRows.Count; i++)
            {
                var row = _relationshipRows[i];
                string status = BuildRelationshipStatusLabel(row);
                bool selected = i == _selectedRelationshipIndex;
                if (GUILayout.Toggle(selected, $"{row.DisplayName} ({row.Key}) — {row.Hearts:0.#} {status}", _buttonStyle))
                {
                    _selectedRelationshipIndex = i;
                    _relationshipHeartsInput = ((int)Math.Round(row.Hearts)).ToString();
                }
            }
            GUILayout.EndScrollView();

            if (_selectedRelationshipIndex >= 0 && _selectedRelationshipIndex < _relationshipRows.Count)
            {
                var selected = _relationshipRows[_selectedRelationshipIndex];
                GUILayout.Label(ModLocalization.T("devtools.relationships.selected", selected.DisplayName, selected.Key), _labelStyle);

                GUILayout.BeginHorizontal();
                GUILayout.Label(ModLocalization.T("devtools.relationships.hearts"), _labelStyle, GUILayout.Width(50));
                _relationshipHeartsInput = GUILayout.TextField(_relationshipHeartsInput ?? "0", _textFieldStyle, GUILayout.Width(50));
                if (GUILayout.Button(ModLocalization.T("devtools.relationships.setHearts"), _buttonStyle))
                    RunRelationshipAction(() => NpcRelationshipEditor.SetHearts(selected.Key, ParseInt(_relationshipHeartsInput, 0)));
                foreach (int preset in new[] { 25, 40, 75, 100 })
                {
                    if (GUILayout.Button(preset.ToString(), _buttonStyle, GUILayout.Width(36)))
                        RunRelationshipAction(() => NpcRelationshipEditor.SetHearts(selected.Key, preset));
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("-5", _buttonStyle, GUILayout.Width(36)))
                    RunRelationshipAction(() => NpcRelationshipEditor.AdjustHearts(selected.Key, -5));
                if (GUILayout.Button("+5", _buttonStyle, GUILayout.Width(36)))
                    RunRelationshipAction(() => NpcRelationshipEditor.AdjustHearts(selected.Key, 5));
                if (selected.Romanceable && GUILayout.Button(ModLocalization.T("devtools.relationships.date"), _buttonStyle))
                    RunRelationshipAction(() => NpcRelationshipEditor.DateNpc(selected.Key));
                if (selected.Romanceable && GUILayout.Button(ModLocalization.T("devtools.relationships.platonic"), _buttonStyle))
                    RunRelationshipAction(() => NpcRelationshipEditor.SetPlatonic(selected.Key));
                GUILayout.EndHorizontal();

                if (selected.Romanceable)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(ModLocalization.T("devtools.relationships.marry"), _buttonStyle))
                        RunRelationshipAction(() => NpcRelationshipEditor.MarryNpc(selected.Key));
                    if (GUILayout.Button(ModLocalization.T("devtools.relationships.divorce"), _buttonStyle))
                        RunRelationshipAction(() => NpcRelationshipEditor.DivorceNpc(selected.Key));
                    if (GUILayout.Button(ModLocalization.T("devtools.relationships.setPrimary"), _buttonStyle))
                        RunRelationshipAction(() => NpcRelationshipEditor.SetPrimarySpouse(selected.Key));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(ModLocalization.T("devtools.relationships.cycle"), _labelStyle, GUILayout.Width(50));
                    _relationshipCycleInput = GUILayout.TextField(_relationshipCycleInput ?? "0", _textFieldStyle, GUILayout.Width(40));
                    if (GUILayout.Button(ModLocalization.T("devtools.relationships.skipCycle"), _buttonStyle))
                        RunRelationshipAction(() => NpcRelationshipEditor.SkipToCycle(selected.Key, ParseInt(_relationshipCycleInput, 0)));
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ModLocalization.T("devtools.relationships.divorcePrimary"), _buttonStyle))
                RunRelationshipAction(() => NpcRelationshipEditor.DivorcePrimarySpouse());
            if (GUILayout.Button(ModLocalization.T("devtools.relationships.resetAll"), _buttonStyle))
            {
                NpcRelationshipEditor.ResetAllHearts();
                SetRelationshipStatus(ModLocalization.T("devtools.relationships.actionOk"));
                RefreshRelationshipRows();
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_relationshipStatusMessage) && Time.realtimeSinceStartup < _relationshipStatusUntil)
                GUILayout.Label(_relationshipStatusMessage, _labelStyle);

            GUILayout.EndVertical();
        }

        private void DrawMarriableTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(ModLocalization.T("devtools.marriable.title"), _sectionHeaderStyle);
            GUILayout.Space(4);

            bool polyReady = UltraPolygamyHelper.IsAvailable;
            GUILayout.Label(ModLocalization.T(UltraPolygamyHelper.StatusLocalizationKey), _labelStyle);

            if (!NpcRelationshipEditor.IsAvailable)
            {
                GUILayout.Label(ModLocalization.T("devtools.marriable.unavailable"), _labelStyle);
                GUILayout.EndVertical();
                return;
            }

            if (!polyReady)
            {
                GUILayout.Label(ModLocalization.T("devtools.marriable.polyRequired"), _labelStyle);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ModLocalization.T("devtools.marriable.refresh"), _buttonStyle, GUILayout.Width(80)))
                RefreshMarriableRows();
            if (GUILayout.Button(ModLocalization.T("devtools.marriable.selectAll"), _buttonStyle))
                SelectAllMarriable(unmarriedOnly: true);
            if (GUILayout.Button(ModLocalization.T("devtools.marriable.clearSelection"), _buttonStyle))
                _marriableSelectedKeys.Clear();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("devtools.marriable.filter"), _labelStyle, GUILayout.Width(40));
            _marriableFilter = GUILayout.TextField(_marriableFilter ?? string.Empty, _textFieldStyle);
            if (GUILayout.Button("X", _buttonStyle, GUILayout.Width(24)))
            {
                _marriableFilter = string.Empty;
                RefreshMarriableRows();
            }
            GUILayout.EndHorizontal();

            if (_marriableRows.Count == 0)
                RefreshMarriableRows();

            _marriableScrollPosition = GUILayout.BeginScrollView(_marriableScrollPosition, GUILayout.Height(ListHeight(120f, 0.5f)));
            foreach (var row in _marriableRows)
            {
                bool selected = _marriableSelectedKeys.Contains(row.Key);
                string status = row.IsMarriedTo
                    ? ModLocalization.T("devtools.marriable.alreadyMarried")
                    : string.Empty;
                string label = string.IsNullOrEmpty(status)
                    ? $"{row.DisplayName} ({row.Key}) — {row.Hearts:0.#}"
                    : $"{row.DisplayName} ({row.Key}) — {row.Hearts:0.#} [{status}]";

                bool next = GUILayout.Toggle(selected, label, _buttonStyle);
                if (next != selected)
                {
                    if (next)
                        _marriableSelectedKeys.Add(row.Key);
                    else
                        _marriableSelectedKeys.Remove(row.Key);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Label(ModLocalization.T("devtools.marriable.selectedCount", _marriableSelectedKeys.Count), _labelStyle);

            GUI.enabled = _marriableSelectedKeys.Count > 0;
            if (GUILayout.Button(ModLocalization.T("devtools.marriable.marrySelected"), _buttonStyle))
                RunMarriableAction();
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_marriableStatusMessage) && Time.realtimeSinceStartup < _marriableStatusUntil)
                GUILayout.Label(_marriableStatusMessage, _labelStyle);

            GUILayout.EndVertical();
        }

        private void RefreshMarriableRows()
        {
            _marriableRows = new List<NpcRelationshipEditor.NpcRelationshipRow>(
                NpcRelationshipEditor.GetRows(romanceOnly: true, _marriableFilter));
            _marriableSelectedKeys.RemoveWhere(key =>
                _marriableRows.All(r => !string.Equals(r.Key, key, StringComparison.Ordinal)));
        }

        private void SelectAllMarriable(bool unmarriedOnly)
        {
            RefreshMarriableRows();
            foreach (var row in _marriableRows)
            {
                if (unmarriedOnly && row.IsMarriedTo)
                    continue;
                _marriableSelectedKeys.Add(row.Key);
            }
        }

        private void RunMarriableAction()
        {
            if (!UltraPolygamyHelper.IsAvailable)
            {
                SetMarriableStatus(ModLocalization.T("devtools.marriable.polyRequired"));
                return;
            }

            if (_marriableSelectedKeys.Count == 0)
            {
                SetMarriableStatus(ModLocalization.T("devtools.marriable.noneSelected"));
                return;
            }

            var keys = _marriableSelectedKeys.ToList();
            int married = 0;
            try
            {
                married = NpcRelationshipEditor.MarryMultiple(keys);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[Marriable] {ex.Message}");
            }

            if (married > 0)
            {
                SetMarriableStatus(ModLocalization.T("devtools.marriable.actionOk", married));
                _marriableSelectedKeys.Clear();
            }
            else
                SetMarriableStatus(ModLocalization.T("devtools.marriable.actionFail"));

            RefreshMarriableRows();
            RefreshRelationshipRows();
        }

        private void SetMarriableStatus(string message)
        {
            _marriableStatusMessage = message;
            _marriableStatusUntil = Time.realtimeSinceStartup + 3f;
        }

        private string BuildRelationshipStatusLabel(NpcRelationshipEditor.NpcRelationshipRow row)
        {
            var parts = new List<string>();
            if (row.IsDating)
                parts.Add(ModLocalization.T("devtools.relationships.status.dating"));
            if (row.IsMarriedTo)
                parts.Add(ModLocalization.T("devtools.relationships.status.married"));
            if (row.IsPrimarySpouse)
                parts.Add(ModLocalization.T("devtools.relationships.status.primary"));
            return parts.Count > 0 ? $"[{string.Join(", ", parts)}]" : string.Empty;
        }

        private void RefreshRelationshipRows()
        {
            _relationshipRows = new List<NpcRelationshipEditor.NpcRelationshipRow>(
                NpcRelationshipEditor.GetRows(_relationshipRomanceOnly, _relationshipFilter));
            if (_selectedRelationshipIndex >= _relationshipRows.Count)
                _selectedRelationshipIndex = _relationshipRows.Count - 1;
        }

        private void RunRelationshipAction(Func<bool> action)
        {
            bool ok = false;
            try
            {
                ok = action();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[Relationships] {ex.Message}");
            }

            SetRelationshipStatus(ok
                ? ModLocalization.T("devtools.relationships.actionOk")
                : ModLocalization.T("devtools.relationships.actionFail"));
            RefreshRelationshipRows();
        }

        private void SetRelationshipStatus(string message)
        {
            _relationshipStatusMessage = message;
            _relationshipStatusUntil = Time.realtimeSinceStartup + 3f;
        }

        private static int ParseInt(string text, int fallback)
        {
            return int.TryParse(text, out int value) ? value : fallback;
        }

        private int _lastDrawnToolsSubTab = -1;

        private void DrawConsoleTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(ModLocalization.T("devtools.console.title"), _sectionHeaderStyle);
            GUILayout.Space(5);
            GUILayout.Label(ModLocalization.T("devtools.console.hint"), _labelStyle);
            var console = Plugin.GetCommandConsole();
            if (console != null)
            {
                float consoleHeight = ListHeight(140f, 0.55f);
                if (_requestConsoleFocus)
                {
                    console.Draw(_boxStyle, _buttonStyle, _labelStyle, _textFieldStyle, requestFocus: true, outputScrollHeight: consoleHeight);
                    _requestConsoleFocus = false;
                }
                else
                    console.Draw(_boxStyle, _buttonStyle, _labelStyle, _textFieldStyle, outputScrollHeight: consoleHeight);
            }
            else
            {
                GUILayout.Label(ModLocalization.T("devtools.console.init"), _labelStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawLogViewerTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(ModLocalization.T("devtools.log.title"), _sectionHeaderStyle);
            GUILayout.Space(5);
            var logViewer = Plugin.GetLogViewer();
            if (logViewer != null)
            {
                logViewer.Draw(_boxStyle, _buttonStyle, _labelStyle, ListHeight(140f, 0.55f));
            }
            else
            {
                GUILayout.Label(ModLocalization.T("devtools.log.init"), _labelStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawPerformanceTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(ModLocalization.T("devtools.perf.title"), _sectionHeaderStyle);
            GUILayout.Space(5);

            GUILayout.Label(ModLocalization.T("devtools.perf.fps", PerformanceSampler.SmoothedFps), _labelStyle);
            GUILayout.Label(
                ModLocalization.T("devtools.perf.fpsRange", PerformanceSampler.MinFps, PerformanceSampler.MaxFps),
                _labelStyle);
            GUILayout.Label(ModLocalization.T("devtools.perf.memory", PerformanceSampler.MemoryMb), _labelStyle);
            GUILayout.Space(6);

            if (ModConfig.ShowFpsCounter != null)
            {
                bool showCounter = ModConfig.ShowFpsCounter.Value;
                bool newShowCounter = GUILayout.Toggle(showCounter, ModLocalization.T("devtools.perf.showFpsCounter"), _labelStyle);
                if (newShowCounter != showCounter)
                    ModConfig.ShowFpsCounter.Value = newShowCounter;
            }

            if (ModConfig.ShowPerformance != null)
            {
                bool showPerf = ModConfig.ShowPerformance.Value;
                bool newShowPerf = GUILayout.Toggle(showPerf, ModLocalization.T("devtools.perf.showOverlayPerf"), _labelStyle);
                if (newShowPerf != showPerf)
                    ModConfig.ShowPerformance.Value = newShowPerf;
            }

            GUILayout.Label(ModLocalization.T("devtools.perf.fpsCounterHint"), _labelStyle);
            GUILayout.Label(ModLocalization.T("devtools.perf.overlayHint"), _labelStyle);
            GUILayout.EndVertical();
        }

        #endregion

        #region Items Tab

        private void DrawItemsTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(ModLocalization.T("devtools.items.title"), _sectionHeaderStyle);
            GUILayout.Space(5);

            // Held item
            var heldItem = Plugin.GetItemInspector()?.GetHeldItem();
            if (heldItem.HasValue)
                GUILayout.Label(ModLocalization.T("devtools.items.held", heldItem.Value.name, heldItem.Value.id), _labelStyle);
            else
                GUILayout.Label(ModLocalization.T("devtools.items.heldNone"), _labelStyle);

            GUILayout.Space(10);

            // Search (Senpai's Chest style - live search, 2+ chars)
            GUILayout.Label(ModLocalization.T("devtools.search"), _labelStyle);
            GUILayout.BeginHorizontal();
            _itemSearchText = GUILayout.TextField(_itemSearchText, _textFieldStyle);
            if (GUILayout.Button(ModLocalization.T("devtools.clear"), _buttonStyle, GUILayout.Width(50)))
            {
                _itemSearchText = "";
                _lastItemSearchQuery = "";
                _searchResults.Clear();
            }
            GUILayout.EndHorizontal();

            if (_itemSearchText != _lastItemSearchQuery)
            {
                _lastItemSearchQuery = _itemSearchText;
                _searchResults = ItemSearch.SearchItems(_itemSearchText, 50);
            }

            if (_selectedItemId > 0)
            {
                GUILayout.Space(2);
                GUILayout.Label(ModLocalization.T("devtools.selected", ItemSearch.FormatDisplay(_selectedItemName, _selectedItemId)), _labelStyle);
            }

            if (_searchResults.Count > 0)
            {
                GUILayout.Space(4);
                float h = Mathf.Min(ListHeight(80f, 0.35f), 24 + _searchResults.Count * 22);
                _itemScrollPosition = GUILayout.BeginScrollView(_itemScrollPosition, GUILayout.Height(h));
                foreach (var result in _searchResults)
                {
                    string label = ItemSearch.FormatDisplay(result.Value, result.Key);
                    bool selected = result.Key == _selectedItemId;
                    if (GUILayout.Button(label, selected ? _buttonStyle : _labelStyle, GUILayout.Height(22)))
                    {
                        _selectedItemId = result.Key;
                        _selectedItemName = result.Value;
                        _itemIdInput = result.Key.ToString();
                    }
                }
                GUILayout.EndScrollView();
            }
            else if (_itemSearchText.Length >= 2)
            {
                GUILayout.Space(2);
                GUILayout.Label(ModLocalization.T("devtools.items.notFound"), _labelStyle);
            }

            GUILayout.Space(10);

            // Spawn
            GUILayout.Label(ModLocalization.T("devtools.spawn"), _labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("devtools.spawn.id"), _labelStyle, GUILayout.Width(22));
            _itemIdInput = GUILayout.TextField(_itemIdInput, _textFieldStyle, GUILayout.Width(70));
            GUILayout.Label(ModLocalization.T("devtools.spawn.qty"), _labelStyle, GUILayout.Width(28));
            _spawnAmount = GUILayout.TextField(_spawnAmount, _textFieldStyle, GUILayout.Width(40));
            if (GUILayout.Button("1", _buttonStyle, GUILayout.Width(22))) _spawnAmount = "1";
            if (GUILayout.Button("10", _buttonStyle, GUILayout.Width(28))) _spawnAmount = "10";
            if (GUILayout.Button("99", _buttonStyle, GUILayout.Width(28))) _spawnAmount = "99";
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ModLocalization.T("devtools.spawn.toInventory"), _buttonStyle))
            {
                if (int.TryParse(_itemIdInput, out int itemId) && int.TryParse(_spawnAmount, out int amount) && amount > 0)
                    Plugin.GetItemInspector()?.SpawnItem(itemId, amount);
            }
            if (_selectedItemId > 0 && GUILayout.Button(ModLocalization.T("devtools.spawn.one"), _buttonStyle, GUILayout.Width(70)))
            {
                Plugin.GetItemInspector()?.SpawnItem(_selectedItemId, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        #endregion

        #region Currencies Tab

        private void DrawCurrenciesTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("devtools.currency.title"), _sectionHeaderStyle);
            GUILayout.FlexibleSpace();
            var tracker = Plugin.GetCurrencyTracker();
            if (tracker != null
                && GUILayout.Button(ModLocalization.T("devtools.relationships.refresh"), _buttonStyle, GUILayout.Width(80)))
            {
                tracker.GetSummary(forceRefresh: true);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            if (tracker == null)
            {
                GUILayout.Label(ModLocalization.T("devtools.currency.unavailable"), _labelStyle);
                GUILayout.EndVertical();
                return;
            }

            var summary = tracker.GetSummary();

            // Basic currencies
            GUILayout.Label(ModLocalization.T("devtools.currency.gold", summary.Gold), _labelStyle);
            GUILayout.Label(ModLocalization.T("devtools.currency.orbs", summary.Orbs), _labelStyle);
            GUILayout.Label(ModLocalization.T("devtools.currency.tickets", summary.Tickets), _labelStyle);

            GUILayout.Space(10);

            // Inventory currencies
            GUILayout.Label(ModLocalization.T("devtools.currency.inventory"), _sectionHeaderStyle);
            _currencyScrollPosition = GUILayout.BeginScrollView(_currencyScrollPosition, GUILayout.Height(ListHeight(90f, 0.3f)));
            if (summary.InventoryCurrencies.Count == 0)
            {
                GUILayout.Label(ModLocalization.T("devtools.currency.none"), _labelStyle);
            }
            else
            {
                foreach (var kvp in summary.InventoryCurrencies)
                {
                    GUILayout.Label($"  {kvp.Key}: {kvp.Value}", _labelStyle);
                }
            }
            GUILayout.EndScrollView();

            // Vault currencies
            if (Plugin.HasTheVault)
            {
                GUILayout.Space(10);
                GUILayout.Label(ModLocalization.T("devtools.currency.vault"), _sectionHeaderStyle);
                if (summary.VaultCurrencies.Count == 0)
                {
                    GUILayout.Label(ModLocalization.T("devtools.currency.vaultEmpty"), _labelStyle);
                }
                else
                {
                    foreach (var kvp in summary.VaultCurrencies)
                    {
                        GUILayout.Label($"  {kvp.Key}: {kvp.Value}", _labelStyle);
                    }
                }
            }
            else
            {
                GUILayout.Space(5);
                GUILayout.Label(ModLocalization.T("devtools.currency.vaultMissing"), _labelStyle);
            }

            GUILayout.EndVertical();
        }

        #endregion

        #region Bundles Tab

        private void DrawBundlesTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(ModLocalization.T("devtools.museum.title"), _sectionHeaderStyle);
            GUILayout.Space(5);

            if (!Plugin.HasSMUT)
            {
                GUILayout.Label(ModLocalization.T("devtools.museum.notInstalled"), _labelStyle);
                GUILayout.EndVertical();
                return;
            }

            var inspector = Plugin.GetBundleInspector();
            if (inspector == null)
            {
                GUILayout.Label(ModLocalization.T("devtools.museum.unavailable"), _labelStyle);
                GUILayout.EndVertical();
                return;
            }

            // Donation stats
            var stats = inspector.GetDonationStats();
            if (stats.IsLoaded)
            {
                GUILayout.Label(ModLocalization.T("devtools.museum.character", stats.CharacterName), _labelStyle);
                GUILayout.Label(ModLocalization.T("devtools.museum.progress", stats.TotalDonated, stats.TotalItems, stats.CompletionPercent), _labelStyle);
            }
            else
            {
                GUILayout.Label(ModLocalization.T("devtools.museum.notLoaded"), _labelStyle);
            }

            GUILayout.Space(10);

            // Sections
            var sections = inspector.GetAllSections();
            if (sections.Count == 0)
            {
                GUILayout.Label(ModLocalization.T("devtools.museum.noData"), _labelStyle);
                GUILayout.EndVertical();
                return;
            }

            // Section selector
            string[] sectionNames = new string[sections.Count];
            for (int i = 0; i < sections.Count; i++)
            {
                sectionNames[i] = sections[i].Name;
            }

            GUILayout.Label(ModLocalization.T("devtools.museum.section"), _labelStyle);
            _selectedSectionIndex = GUILayout.SelectionGrid(_selectedSectionIndex, sectionNames, 3, _buttonStyle);

            if (_selectedSectionIndex >= sections.Count) _selectedSectionIndex = 0;
            var selectedSection = sections[_selectedSectionIndex];

            GUILayout.Space(5);

            // Bundle selector
            if (selectedSection.Bundles.Count > 0)
            {
                string[] bundleNames = new string[selectedSection.Bundles.Count];
                for (int i = 0; i < selectedSection.Bundles.Count; i++)
                {
                    bundleNames[i] = selectedSection.Bundles[i].Name;
                }

                if (_selectedBundleIndex >= selectedSection.Bundles.Count) _selectedBundleIndex = 0;

                GUILayout.Label(ModLocalization.T("devtools.museum.bundle"), _labelStyle);
                _selectedBundleIndex = GUILayout.SelectionGrid(_selectedBundleIndex, bundleNames, 2, _buttonStyle);

                var selectedBundle = selectedSection.Bundles[_selectedBundleIndex];

                GUILayout.Space(5);

                // Items in bundle
                GUILayout.Label(ModLocalization.T("devtools.museum.itemsIn", selectedBundle.Name), _sectionHeaderStyle);
                _bundleScrollPosition = GUILayout.BeginScrollView(_bundleScrollPosition, GUILayout.Height(ListHeight(100f, 0.35f)));
                foreach (var item in selectedBundle.Items)
                {
                    bool donated = inspector.HasDonated(item.Id);
                    string status = donated ? "[X]" : "[ ]";
                    string qtyLabel = item.Quantity > 1 ? $" x{item.Quantity}" : "";
                    bool canSpawn = item.GameItemId > 0;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{status} {item.Name} (ID: {item.GameItemId})", _labelStyle, GUILayout.Width(320));
                    var prevEnabled = GUI.enabled;
                    GUI.enabled = canSpawn;
                    if (GUILayout.Button(canSpawn ? ModLocalization.T("devtools.museum.spawn", qtyLabel) : "—", _buttonStyle, GUILayout.Width(90)))
                    {
                        if (canSpawn) Plugin.GetItemInspector()?.SpawnItem(item.GameItemId, item.Quantity);
                    }
                    GUI.enabled = prevEnabled;
                    if (!canSpawn) GUILayout.Label(ModLocalization.T("devtools.museum.idInAssets"), _labelStyle, GUILayout.Width(100));
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        #endregion

        #region Race Bonuses Tab

        private void DrawRaceBonusesTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(ModLocalization.T("devtools.race.title"), _sectionHeaderStyle);
            GUILayout.Space(5);

            var tracker = Plugin.GetRaceModifierTracker();
            if (tracker == null)
            {
                GUILayout.Label(ModLocalization.T("devtools.race.unavailable"), _labelStyle);
                GUILayout.EndVertical();
                return;
            }

            // Current race
            string currentRace = tracker.GetCurrentRace();
            GUILayout.Label(ModLocalization.T("devtools.race.current", currentRace), _labelStyle);

            GUILayout.Space(10);

            // Active bonuses
            if (Plugin.HasHavensBirthright)
            {
                var activeBonuses = tracker.GetActiveRaceBonuses();
                GUILayout.Label(ModLocalization.T("devtools.race.bonuses"), _sectionHeaderStyle);
                if (activeBonuses.Count == 0)
                {
                    GUILayout.Label(ModLocalization.T("devtools.race.noBonuses"), _labelStyle);
                }
                else
                {
                    foreach (var bonus in activeBonuses)
                    {
                        GUILayout.Label($"  {bonus.Type}: {bonus.GetFormattedValue()}", _labelStyle);
                        GUILayout.Label($"    {bonus.Description}", _labelStyle);
                    }
                }
            }
            else
            {
                GUILayout.Label(ModLocalization.T("devtools.race.birthrightMissing"), _labelStyle);
            }

            GUILayout.Space(10);

            // Race browser
            GUILayout.Label(ModLocalization.T("devtools.race.browse"), _sectionHeaderStyle);
            var races = tracker.GetAllRaces();
            if (races.Count > 0)
            {
                string[] raceNames = GetRaceNamesForGrid(races);
                if (_selectedRaceIndex >= races.Count) _selectedRaceIndex = 0;

                _selectedRaceIndex = GUILayout.SelectionGrid(_selectedRaceIndex, raceNames, 4, _buttonStyle);

                GUILayout.Space(5);

                var bonuses = tracker.GetBonusesForRace(races[_selectedRaceIndex]);
                _raceScrollPosition = GUILayout.BeginScrollView(_raceScrollPosition, GUILayout.Height(ListHeight(100f, 0.35f)));
                if (bonuses.Count == 0)
                {
                    GUILayout.Label(ModLocalization.T("devtools.race.noDefined"), _labelStyle);
                }
                else
                {
                    foreach (var bonus in bonuses)
                    {
                        GUILayout.Label($"  {bonus.Type}: {bonus.GetFormattedValue()}", _labelStyle);
                        GUILayout.Label($"    {bonus.Description}", _labelStyle);
                    }
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        #endregion

        #region Utility Tab

        private void DrawUtilityTab()
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(ModLocalization.T("devtools.utility"), _sectionHeaderStyle);
            GUILayout.Space(5);

            // Config
            GUILayout.Label(ModLocalization.T("devtools.config"), _sectionHeaderStyle);
            if (GUILayout.Button(ModLocalization.T("devtools.config.reload"), _buttonStyle))
            {
                ConfigReloader.ReloadHavenDevToolsConfig();
            }

            GUILayout.Space(10);

            // Logging
            GUILayout.Label(ModLocalization.T("devtools.logging"), _sectionHeaderStyle);

            if (GUILayout.Button(ModLocalization.T("devtools.logCurrencyIds"), _buttonStyle))
            {
                Plugin.Log?.LogDebug("=== Currency Item IDs ===");
                foreach (var kvp in CurrencyTracker.CurrencyItemIds)
                {
                    Plugin.Log?.LogDebug($"  {kvp.Key}: {kvp.Value}");
                }
            }

            if (GUILayout.Button(ModLocalization.T("devtools.logPlayerStats"), _buttonStyle))
            {
                LogPlayerStats();
            }

            GUILayout.EndVertical();
        }

        private void LogPlayerStats()
        {
            try
            {
                if (Wish.Player.Instance == null)
                {
                    Plugin.Log?.LogDebug("Player not available");
                    return;
                }

                Plugin.Log?.LogDebug("=== Player Stats ===");

                var playerType = Wish.Player.Instance.GetType();
                var props = playerType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                foreach (var prop in props)
                {
                    try
                    {
                        if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                        {
                            var value = prop.GetValue(Wish.Player.Instance);
                            if (value != null && (value is int || value is float || value is string || value is bool))
                            {
                                Plugin.Log?.LogDebug($"  {prop.Name}: {value}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogDebug($"[DebugWindow] Player stats property iteration: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error logging player stats: {ex.Message}");
            }
        }

        #endregion
    }
}
