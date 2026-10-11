using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using SenpaisChest.Config;
using SenpaisChest.Data;
using SunhavenMods.Shared;
using SunHavenMuseumUtilityTracker;
using SunHavenMuseumUtilityTracker.Data;
using UnityEngine;
using Wish;

namespace SenpaisChest.UI
{
    [UnityEngine.DefaultExecutionOrder(-30000)]
    public class SmartChestUI : MonoBehaviour
    {
        private SmartChestManager _manager;
        private bool _isVisible;
        private Chest _currentChest;
        private SmartChestData _currentData;
        private string _chestId;
        private bool _confirmCopyRules;
        private float _confirmCopyRulesUntil;

        // UI scale (0.5–3.0; may include resolution factor)
        private float _scale = 1f;
        private const float BASE_WINDOW_WIDTH = 420f;
        private const float BASE_WINDOW_HEIGHT = 500f;
        private const float BASE_GROUPS_WIDTH = 400f;
        private const float BASE_GROUPS_HEIGHT = 450f;

        private float WindowWidth => BASE_WINDOW_WIDTH * _scale;
        private float WindowHeight => BASE_WINDOW_HEIGHT * _scale;
        private float GroupsWindowWidth => BASE_GROUPS_WIDTH * _scale;
        private float GroupsWindowHeight => BASE_GROUPS_HEIGHT * _scale;
        private float Scaled(float value) => value * _scale;
        private int ScaledFont(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * _scale));
        private int ScaledInt(float value) => Mathf.RoundToInt(value * _scale);

        // Window
        private Rect _windowRect = new Rect(100, 100, 420, 500);

        // Note panel shown when chest is open ("Press F9 to configure")
        private GameObject _notePanelRoot;
        private Rect _noteRect;
        private bool _noteRectDirty = true;
        private float _contentHeight = 500f;

        // Add rule form state
        private int _selectedRuleType;
        private string _itemIdInput = "";
        private int _selectedCategory;
        private int _selectedItemType;
        private int _selectedProperty;
        private int _selectedGroup;

        // Item search state
        private string _lastSearchQuery = "";
        private List<KeyValuePair<int, string>> _searchResults = new List<KeyValuePair<int, string>>();
        private Vector2 _searchScrollPos;
        private int _selectedItemId = -1;
        private string _selectedItemName = "";

        // Groups management
        private bool _groupsWindowVisible;
        private Rect _groupsWindowRect;
        private ItemGroup _editingGroup;
        private string _newGroupName = "";
        private string _groupItemSearch = "";
        private string _groupItemSearchLast = "";
        private string _groupPatternInput = "";
        private List<KeyValuePair<int, string>> _groupSearchResults = new List<KeyValuePair<int, string>>();
        private Vector2 _groupSearchScroll;
        private Vector2 _groupListScroll;
        private Vector2 _groupItemsScroll;

        // Dropdown options
        private static readonly string[] RuleTypeNames =
            { "By Item", "By Category", "By Item Type", "By Property", "By Group", "By wildcard name" };

        private string _wildcardPatternInput = "";
        private static readonly string[] BaseCategoryNames = { "Equip", "Use", "Craftable", "Monster", "Furniture", "Quest" };
        private static readonly string[] CategoryNamesWithMuseum = { "Equip", "Use", "Craftable", "Monster", "Furniture", "Quest", "Undonated Items" };
        private static readonly string[] ItemTypeNames = { "Normal", "Armor", "Food", "Fish", "Crop", "WateringCan", "Animal", "Pet", "Tool" };
        private static readonly string[] PropertyNames = { "isGem", "isForageable", "isAnimalProduct", "isMeal", "isFruit", "isArtisanryItem", "isPotion", "isNotDonated" };
        private static readonly string[] PropertyDisplayNames = { "Gems", "Forageables", "Animal Products", "Meals", "Fruits", "Artisanry Items", "Potions", "Museum (Not Donated)" };
        /// <summary>Control name for the item search field. Used so we only block game input (Backspace/UICancel) when the user is typing here, not when e.g. the cheat enabler chat is focused.</summary>
        internal const string SearchFieldControlName = "SmartChestItemSearch";
        internal const string GroupSearchFieldControlName = "SmartChestGroupItemSearch";
        internal const string WildcardPatternControlName = "SmartChestWildcardPattern";

        private readonly Color _bgDark = new Color(0.15f, 0.16f, 0.24f, 1f);
        private readonly Color _borderGold = new Color(0.75f, 0.65f, 0.30f, 1f);
        private readonly Color _goldText = new Color(0.95f, 0.85f, 0.35f, 1f);
        private readonly Color _whiteText = new Color(0.95f, 0.95f, 0.95f, 1f);
        private readonly Color _dimText = new Color(0.6f, 0.6f, 0.7f, 1f);
        private readonly Color _greenActive = new Color(0.20f, 0.55f, 0.45f, 1f);
        private readonly Color _greenHover = new Color(0.25f, 0.65f, 0.52f, 1f);
        private readonly Color _greenBright = new Color(0.30f, 0.70f, 0.55f, 1f);
        private readonly Color _redDanger = new Color(0.75f, 0.20f, 0.20f, 1f);
        private readonly Color _redHover = new Color(0.85f, 0.28f, 0.28f, 1f);
        private readonly Color _btnInactive = new Color(0.22f, 0.24f, 0.34f, 1f);
        private readonly Color _btnHover = new Color(0.30f, 0.32f, 0.44f, 1f);
        private readonly Color _ruleBoxColor = new Color(0.18f, 0.19f, 0.28f, 1f);
        private readonly Color _fieldBg = new Color(0.12f, 0.13f, 0.22f, 1f);
        private readonly Color _museumHighlight = new Color(0.35f, 0.65f, 0.85f, 1f);
        private readonly Color _parchmentLight = new Color(0.96f, 0.93f, 0.86f, 0.98f);
        private readonly Color _parchment = new Color(0.92f, 0.87f, 0.78f, 0.97f);
        private readonly Color _parchmentDark = new Color(0.85f, 0.78f, 0.65f, 0.95f);
        private readonly Color _woodDark = new Color(0.35f, 0.25f, 0.15f);
        private readonly Color _woodMedium = new Color(0.50f, 0.38f, 0.25f);
        private readonly Color _woodLight = new Color(0.65f, 0.52f, 0.38f);
        private readonly Color _goldPale = new Color(0.92f, 0.82f, 0.55f);
        private readonly Color _goldRich = new Color(0.72f, 0.55f, 0.20f);
        private readonly Color _borderWood = new Color(0.45f, 0.35f, 0.22f, 0.9f);
        private readonly Color _chestTextDark = new Color(0.08f, 0.06f, 0.05f, 1f);
        private readonly Color _chestTextDim = new Color(0.14f, 0.11f, 0.09f, 1f);
        private readonly Color _crimsonTitle = new Color(0.52f, 0.18f, 0.12f, 1f);
        private readonly Color _crimsonTitleLight = new Color(0.58f, 0.22f, 0.16f, 1f);
        private readonly Color _innerBorderCrimson = new Color(0.42f, 0.18f, 0.14f, 1f);

        // Textures
        private Texture2D _solidBg;
        private Texture2D _windowBg;
        private Texture2D _ruleBg;
        private Texture2D _btnInactiveTex;
        private Texture2D _btnHoverTex;
        private Texture2D _btnActiveTex;
        private Texture2D _btnActiveHoverTex;
        private Texture2D _redBtnTex;
        private Texture2D _redBtnHoverTex;
        private Texture2D _greenBtnTex;
        private Texture2D _greenBtnHoverTex;
        private Texture2D _closeBtnTex;
        private Texture2D _closeBtnHoverTex;
        private Texture2D _fieldBgTex;
        private Texture2D _chestParchmentBg;
        private Texture2D _chestWoodBorderTex;
        private Texture2D _chestRuleBoxTex;
        private Texture2D _chestSelectorTex;
        private Texture2D _chestSelectorHoverTex;
        private Texture2D _chestSelectorActiveTex;
        private Texture2D _chestAddBtnTex;
        private Texture2D _chestAddBtnHoverTex;
        private Texture2D _chestWoodBtnTex;
        private Texture2D _chestWoodBtnHoverTex;
        private Texture2D _chestDangerBtnTex;
        private Texture2D _chestDangerBtnHoverTex;
        private Texture2D _chestFieldBgTex;
        private Texture2D _chestGoldLineTex;
        private Texture2D _noteBannerBg;
        private Texture2D _noteBannerBorderTex;
        private Texture2D _configTitleBarTex;
        private Texture2D _configContentBg;
        private Texture2D _configGoldSeparatorTex;
        private Texture2D _configSectionHeaderTex;
        private Texture2D _configSearchFieldTex;
        private Texture2D _configRemoveBtnTex;
        private Texture2D _configRemoveBtnHoverTex;
        private Texture2D _configCloseBtnTex;
        private Texture2D _configCloseBtnHoverTex;
        private Texture2D _configWindowBg;
        private Texture2D _configInnerBorderTex;

        // Styles
        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _chestTitleStyle;
        private GUIStyle _chestLabelStyle;
        private GUIStyle _chestLabelBoldStyle;
        private GUIStyle _chestLabelDimStyle;
        private GUIStyle _chestSectionHeaderStyle;
        private GUIStyle _chestToggleStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _labelBoldStyle;
        private GUIStyle _labelDimStyle;
        private GUIStyle _ruleBoxStyle;
        private GUIStyle _ruleTextStyle;
        private GUIStyle _removeRuleBtnStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _selectorStyle;
        private GUIStyle _selectorActiveStyle;
        private GUIStyle _addButtonStyle;
        private GUIStyle _dangerButtonStyle;
        private GUIStyle _closeBottomButtonStyle;
        private GUIStyle _searchResultStyle;
        private GUIStyle _searchResultSelectedStyle;
        private GUIStyle _chestRuleBoxStyle;
        private GUIStyle _chestRuleTextStyle;
        private GUIStyle _chestSelectorStyle;
        private GUIStyle _chestSelectorActiveStyle;
        private GUIStyle _chestAddButtonStyle;
        private GUIStyle _chestDangerButtonStyle;
        private GUIStyle _chestCloseButtonStyle;
        private GUIStyle _chestSearchFieldStyle;
        private GUIStyle _chestRemoveRuleBtnStyle;
        private GUIStyle _chestSearchResultStyle;
        private GUIStyle _chestSearchResultSelectedStyle;
        private GUIStyle _noteBannerStyle;
        private GUIStyle _configTitleStyle;
        private GUIStyle _configSectionHeaderBoxStyle;
        private GUIStyle _configSearchFieldStyle;
        private GUIStyle _configRemoveButtonStyle;
        private GUIStyle _configCloseBottomStyle;
        private GUIStyle _configWindowStyle;
        private bool _stylesDirty = true;

        public bool IsVisible => _isVisible;

        /// <summary>True when we should block Backspace/UICancel from reaching the game (only when our search field has keyboard focus). When false, e.g. when the cheat enabler chat is focused, we do not block so that mod can receive input.</summary>
        internal static bool ShouldBlockGameInput(SmartChestUI ui)
        {
            if (ui == null || !ui.IsVisible)
                return false;

            // CheatEnabler / Quantum Console / uGUI chat — never swallow their keys
            if (TextInputFocusGuard.IsQuantumConsoleActive())
                return false;
            if (TextInputFocusGuard.IsUnityUiTextInputFocused())
                return false;

            var focused = GUI.GetNameOfFocusedControl();
            return focused == SearchFieldControlName || focused == GroupSearchFieldControlName ||
                   focused == WildcardPatternControlName;
        }

        public void Initialize(SmartChestManager manager)
        {
            _manager = manager;
            _windowRect = new Rect(100, 100, WindowWidth, WindowHeight);
            _groupsWindowRect = new Rect(150, 150, GroupsWindowWidth, GroupsWindowHeight);
        }

        public void SetScale(float scale)
        {
            _scale = Mathf.Clamp(scale, 0.5f, 3.0f);
            _stylesDirty = true;
            _configWindowStyle = null;
            _windowRect.width = WindowWidth;
            _windowRect.height = WindowHeight;
            _groupsWindowRect.width = GroupsWindowWidth;
            _groupsWindowRect.height = GroupsWindowHeight;
            // Reset content height so the next OnGUI pass remeasures at the new scale (AZR-358).
            _contentHeight = WindowHeight;
        }

        /// <summary>Full cleanup when chest closes. Clears note and config, closes chest.</summary>
        public void Hide()
        {
            _isVisible = false;
            _confirmCopyRules = false;
            ClearImGuiKeyboardFocus();
            var chestToClose = _currentChest;
            _currentChest = null;
            _currentData = null;
            _notePanelRoot = null;
            _noteRectDirty = true;

            Plugin.CurrentInteractingChest = null;
            SaveIfDirty();

            if (chestToClose != null)
            {
                try { chestToClose.EndInteract(0); }
                catch (Exception ex) { Plugin.Log?.LogDebug($"[SmartChestUI] EndInteract on close: {ex.Message}"); }
            }
        }

        /// <summary>Closes the config window only. Chest stays open and note remains visible.</summary>
        public void HideConfig()
        {
            SaveIfDirty(); // Ensure rules are saved before closing
            _isVisible = false;
            _confirmCopyRules = false;
            _groupsWindowVisible = false;
            ClearImGuiKeyboardFocus();
        }

        private static void ClearImGuiKeyboardFocus()
        {
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
        }

        public void ToggleForChest(Chest chest)
        {
            if (_isVisible && _currentChest == chest)
            {
                HideConfig();
                return;
            }

            _currentChest = chest;
            _chestId = SmartChestManager.GetChestId(chest);

            if (string.IsNullOrEmpty(_chestId))
            {
                Plugin.Log?.LogWarning("Cannot configure chest: no valid ID");
                return;
            }

            string chestName = GetChestName(chest);
            _currentData = _manager.GetOrCreateSmartChest(_chestId, chestName);

            // Reset add-rule form
            _selectedRuleType = 0;
            _itemIdInput = "";
            _lastSearchQuery = "";
            _searchResults.Clear();
            _selectedItemId = -1;
            _selectedItemName = "";
            _searchScrollPos = Vector2.zero;
            _selectedCategory = 0;
            _selectedItemType = 0;
            _selectedProperty = 0;
            _wildcardPatternInput = "";
            _confirmCopyRules = false;

            _isVisible = true;
            PositionWindowNextToChestPanel();
        }

        /// <summary>
        /// Shows the note ("Press F9 to configure") in the given panel when chest is open. Config opens only via F9.
        /// </summary>
        public void ShowNoteForChest(Chest chest, GameObject panelRoot)
        {
            _currentChest = chest;
            _chestId = SmartChestManager.GetChestId(chest);
            if (string.IsNullOrEmpty(_chestId))
            {
                Plugin.Log?.LogWarning("[SmartChestUI] Cannot show note: no valid chest ID");
                return;
            }
            // Do not call GetOrCreateSmartChest here; chest is only registered when user opens config (F9)
            _currentData = null;
            _notePanelRoot = panelRoot;
            _noteRectDirty = true;
            _isVisible = false; // Config not shown; only note
            Plugin.Log?.LogDebug($"[SmartChestUI] ShowNoteForChest: chest={chest != null}, panelRoot={panelRoot != null}");
        }

        /// <summary>
        /// Positions the config window at the bottom of the screen (same area as embedded panel)
        /// when the chest UI is open, so config is always in the same place.
        /// </summary>
        private void PositionWindowNextToChestPanel()
        {
            if (_currentChest == null) return;

            float y = Scaled(8f);
            float h = Mathf.Min(Scaled(400f), Screen.height - y);
            _windowRect.width = WindowWidth;
            _windowRect.height = h;
            _windowRect.x = Mathf.Clamp((Screen.width - _windowRect.width) * 0.5f, 0, Screen.width - _windowRect.width);
            _windowRect.y = y;
        }

        /// <summary>
        /// Delegates to SmartChestManager.GetChestName which caches the Chest.data field.
        /// </summary>
        private string GetChestName(Chest chest)
        {
            return SmartChestManager.GetChestName(chest);
        }

        private void OnGUI()
        {
            if (_stylesDirty || _configWindowStyle == null)
                InitializeStyles();
            if (_configWindowStyle == null)
                return;

            // Note panel: "Press F9 to configure rules" when chest is open and config not visible
            if (_notePanelRoot != null && !_isVisible)
            {
                if (!_notePanelRoot.activeInHierarchy)
                {
                    _notePanelRoot = null;
                    return;
                }
                if (_noteRectDirty || Event.current.type == UnityEngine.EventType.Layout)
                {
                    _noteRect = GetScreenRectFromTransform(_notePanelRoot.transform);
                    _noteRectDirty = false;
                }
                Rect drawRect = _noteRect;
                if (drawRect.width <= 0 || drawRect.height <= 0)
                {
                    drawRect = new Rect((Screen.width - Scaled(340f)) * 0.5f, Scaled(8f), Scaled(340f), Scaled(34f));
                }
                drawRect = ClampRectToScreen(drawRect);

                GUI.BeginGroup(drawRect);
                DrawNoteContent(drawRect.width, drawRect.height);
                GUI.EndGroup();
                return;
            }

            if (!_isVisible || _currentData == null)
                return;

            float maxHeight = Screen.height - Scaled(40f);
            _windowRect.width = WindowWidth;
            _windowRect.height = Mathf.Clamp(_contentHeight, Scaled(300f), maxHeight);
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);

            _windowRect = GUI.Window(
                PluginInfo.PLUGIN_GUID.GetHashCode(),
                _windowRect,
                DrawWindow,
                "",
                _configWindowStyle);

            if (_groupsWindowVisible)
            {
                _groupsWindowRect = GUI.Window(
                    PluginInfo.PLUGIN_GUID.GetHashCode() + 1,
                    _groupsWindowRect,
                    DrawGroupsWindow,
                    ModLocalization.T("chest.manageGroups"),
                    _configWindowStyle);
            }
        }

        /// <summary>
        /// Converts a RectTransform to screen-space Rect (Unity GUI: origin bottom-left).
        /// </summary>
        private static Rect GetScreenRectFromTransform(Transform transform)
        {
            var rt = transform as RectTransform;
            if (rt == null) return new Rect(0, 0, 400, 400);
            var canvas = rt.GetComponentInParent<Canvas>();
            
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            
            Vector2 min, max;
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // For ScreenSpaceOverlay, GetWorldCorners returns screen coordinates
                // corners[0] = bottom-left, corners[1] = top-left, corners[2] = top-right, corners[3] = bottom-right
                min = new Vector2(corners[0].x, corners[0].y); // bottom-left
                max = new Vector2(corners[2].x, corners[2].y); // top-right
            }
            else
            {
                // For Camera or WorldSpace, convert world to screen
                var cam = canvas != null ? canvas.worldCamera : Camera.main;
                min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            }
            
            // Convert to GUI coordinates (bottom-left origin)
            // Unity GUI uses bottom-left origin, so Y needs to be flipped
            float x = min.x;
            float y = Screen.height - max.y; // Flip Y: GUI origin is bottom-left
            float width = max.x - min.x;
            float height = max.y - min.y;
            
            Rect result = new Rect(x, y, width, height);
            return result;
        }

        /// <summary>
        /// Clamps a rect to stay fully within the visible screen (Unity GUI: bottom-left origin).
        /// </summary>
        private static Rect ClampRectToScreen(Rect rect)
        {
            float w = Mathf.Min(rect.width, Screen.width);
            float h = Mathf.Min(rect.height, Screen.height);
            float x = Mathf.Clamp(rect.x, 0, Screen.width - w);
            float y = Mathf.Clamp(rect.y, 0, Screen.height - h);
            return new Rect(x, y, w, h);
        }

        private void DrawWindow(int windowId)
        {
            DrawConfigContent(new Rect(0, 0, _windowRect.width, _windowRect.height));
        }

        private void DrawConfigContent(Rect rect)
        {
            if (Event.current.type == UnityEngine.EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Escape)
                {
                    Event.current.Use();
                    HideConfig();
                    return;
                }
            }

            float titleBarHeight = Scaled(36f);
            float outerBorder = Scaled(5f);
            float innerBorderW = Scaled(1f);

            if (_configInnerBorderTex != null)
            {
                float innerX = outerBorder;
                float innerY = outerBorder;
                float innerW = rect.width - outerBorder * 2;
                float innerH = rect.height - outerBorder * 2;
                GUI.DrawTexture(new Rect(innerX, innerY, innerW, innerBorderW), _configInnerBorderTex);
                GUI.DrawTexture(new Rect(innerX, innerY + innerH - innerBorderW, innerW, innerBorderW), _configInnerBorderTex);
                GUI.DrawTexture(new Rect(innerX, innerY, innerBorderW, innerH), _configInnerBorderTex);
                GUI.DrawTexture(new Rect(innerX + innerW - innerBorderW, innerY, innerBorderW, innerH), _configInnerBorderTex);
            }

            if (_configTitleBarTex != null)
                GUI.DrawTexture(new Rect(0, 0, rect.width, titleBarHeight), _configTitleBarTex);
            GUILayout.BeginArea(new Rect(0, 0, rect.width, titleBarHeight));
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(ModLocalization.T("chest.config.title"), _configTitleStyle, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", _closeButtonStyle, GUILayout.Width(Scaled(28)), GUILayout.Height(Scaled(24))))
                HideConfig();
            GUILayout.Space(Scaled(4));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUILayout.Space(titleBarHeight + Scaled(2));
            if (_configGoldSeparatorTex != null)
            {
                var sepRect = GUILayoutUtility.GetRect(rect.width - Scaled(24), Scaled(2));
                GUI.DrawTexture(sepRect, _configGoldSeparatorTex);
            }
            GUILayout.Space(Scaled(8));

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            bool newEnabled = GUILayout.Toggle(_currentData.IsEnabled, " Smart Chest Enabled", _chestToggleStyle);
            if (newEnabled != _currentData.IsEnabled)
            {
                _currentData.IsEnabled = newEnabled;
                _manager.MarkDirty();
                SaveIfDirty();
            }
            GUILayout.EndHorizontal();

            if (_configGoldSeparatorTex != null)
            {
                var sepR = GUILayoutUtility.GetRect(rect.width - 24, 2);
                GUI.DrawTexture(sepR, _configGoldSeparatorTex);
                GUILayout.Space(6);
            }

            GUILayout.Label(ModLocalization.T("chest.section.rules"), _configSectionHeaderBoxStyle);
            GUILayout.Space(4);

            if (_configGoldSeparatorTex != null)
            {
                var sepR2 = GUILayoutUtility.GetRect(rect.width - 24, 2);
                GUI.DrawTexture(sepR2, _configGoldSeparatorTex);
                GUILayout.Space(4);
            }

            if (_currentData.Rules.Count == 0)
            {
                GUILayout.Label(ModLocalization.T("chest.rules.none"), _chestLabelDimStyle);
            }
            else
            {
                float ruleItemHeight = 36f;
                float maxRulesHeight = 180f;
                float rulesHeight = Mathf.Min(_currentData.Rules.Count * ruleItemHeight, maxRulesHeight);
                GUILayout.BeginVertical(GUILayout.Height(rulesHeight));
                int removeIndex = -1;
                for (int i = 0; i < _currentData.Rules.Count; i++)
                {
                    GUILayout.BeginHorizontal(_chestRuleBoxStyle);
                    GUILayout.Label($"{i + 1}. {GetRuleDisplayText(_currentData.Rules[i])}", _chestRuleTextStyle, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("X", _chestRemoveRuleBtnStyle, GUILayout.Width(26), GUILayout.Height(22)))
                        removeIndex = i;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(2);
                }
                GUILayout.EndVertical();
                if (removeIndex >= 0)
                {
                    _currentData.Rules.RemoveAt(removeIndex);
                    _manager.MarkDirty();
                    SaveIfDirty();
                }
            }

            if (_configGoldSeparatorTex != null)
            {
                var sepR3 = GUILayoutUtility.GetRect(rect.width - 24, 2);
                GUI.DrawTexture(sepR3, _configGoldSeparatorTex);
                GUILayout.Space(8);
            }

            GUILayout.Label(ModLocalization.T("chest.section.addRule"), _configSectionHeaderBoxStyle);
            GUILayout.Space(6);

            if (_configGoldSeparatorTex != null)
            {
                var sepR4 = GUILayoutUtility.GetRect(rect.width - 24, 2);
                GUI.DrawTexture(sepR4, _configGoldSeparatorTex);
                GUILayout.Space(4);
            }

            GUILayout.Label(ModLocalization.T("chest.presets"), _chestLabelBoldStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ModLocalization.T("chest.preset.equipment"), _chestSelectorStyle, GUILayout.Height(24)))
                ApplyPreset("Equipment");
            if (GUILayout.Button(ModLocalization.T("chest.preset.consumables"), _chestSelectorStyle, GUILayout.Height(24)))
                ApplyPreset("Consumables");
            if (GUILayout.Button(ModLocalization.T("chest.preset.crafting"), _chestSelectorStyle, GUILayout.Height(24)))
                ApplyPreset("Crafting");
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            DrawSelectorButton(0, GUILayout.Height(24));
            DrawSelectorButton(1, GUILayout.Height(24));
            DrawSelectorButton(2, GUILayout.Height(24));
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            GUILayout.BeginHorizontal();
            DrawSelectorButton(3, GUILayout.Height(24));
            DrawSelectorButton(4, GUILayout.Height(24));
            if (IsSeparateWildcardRuleEnabled())
                DrawSelectorButton(5, GUILayout.Height(24));
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ModLocalization.T("chest.manageGroups"), _configCloseBottomStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22)))
                _groupsWindowVisible = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            if (_configGoldSeparatorTex != null)
            {
                var sepR5 = GUILayoutUtility.GetRect(rect.width - 24, 2);
                GUI.DrawTexture(sepR5, _configGoldSeparatorTex);
                GUILayout.Space(4);
            }

            DrawRuleInput();
            GUILayout.Space(8);

            if (_configGoldSeparatorTex != null)
            {
                var sepR6 = GUILayoutUtility.GetRect(rect.width - 24, 2);
                GUI.DrawTexture(sepR6, _configGoldSeparatorTex);
                GUILayout.Space(6);
            }

            if (GUILayout.Button(ModLocalization.T("chest.addRule"), _chestAddButtonStyle, GUILayout.Height(24)))
                AddRule();

            if (_currentData.Rules.Count > 0)
            {
                int sameNameCount = _manager.CountSameNameChests(_chestId);
                if (sameNameCount > 0)
                {
                    GUILayout.Space(4);
                    bool confirmActive = _confirmCopyRules && Time.unscaledTime <= _confirmCopyRulesUntil;
                    if (!confirmActive)
                        _confirmCopyRules = false;
                    string bulkLabel = confirmActive
                        ? $"Click Again to Confirm ({sameNameCount})"
                        : $"Copy Rules to All \"{_currentData.ChestName}\" ({sameNameCount})";
                    if (GUILayout.Button(bulkLabel, _chestSelectorStyle, GUILayout.Height(Scaled(24f))))
                    {
                        if (confirmActive)
                        {
                            int updated = _manager.CopyRulesToSameNameChests(_chestId);
                            _confirmCopyRules = false;
                            SaveIfDirty();
                            Plugin.Log?.LogInfo($"[SmartChestUI] Copied rules to {updated} same-name chest(s)");
                        }
                        else
                        {
                            _confirmCopyRules = true;
                            _confirmCopyRulesUntil = Time.unscaledTime + 3f;
                            Plugin.Log?.LogInfo("[SmartChestUI] Copy-rules confirmation armed for 3 seconds");
                        }
                    }
                }
            }

            if (_configGoldSeparatorTex != null)
            {
                var sepR7 = GUILayoutUtility.GetRect(rect.width - 24, 2);
                GUI.DrawTexture(sepR7, _configGoldSeparatorTex);
                GUILayout.Space(8);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ModLocalization.T("chest.removeSmartChest"), _configRemoveButtonStyle, GUILayout.Height(Scaled(28f))))
            {
                _manager.RemoveSmartChest(_chestId, userInitiated: true);
                SaveIfDirty();
                HideConfig();
            }
            GUILayout.Space(Scaled(8f));
            if (GUILayout.Button(ModLocalization.T("chest.close"), _configCloseBottomStyle, GUILayout.Height(Scaled(28f))))
                HideConfig();
            GUILayout.EndHorizontal();

            if (Event.current.type == UnityEngine.EventType.Repaint)
            {
                var lastRect = GUILayoutUtility.GetLastRect();
                _contentHeight = lastRect.yMax + Scaled(24f);
            }
            GUI.DragWindow(new Rect(0, 0, rect.width, Scaled(28)));

            if (Event.current.type == UnityEngine.EventType.KeyDown && Event.current.keyCode == KeyCode.Backspace)
                Event.current.Use();
        }

        private void DrawGroupsWindow(int windowId)
        {
            if (_configWindowStyle == null) InitializeStyles();
            var labelBold = _chestLabelBoldStyle;
            var label = _chestLabelStyle;
            var labelDim = _chestLabelDimStyle;

            GUILayout.Label(ModLocalization.T("chest.groups.intro"), labelDim, GUILayout.ExpandWidth(true));
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            _newGroupName = GUILayout.TextField(_newGroupName ?? "", _configSearchFieldStyle ?? _chestSearchFieldStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(ModLocalization.T("chest.groups.new"), _chestAddButtonStyle, GUILayout.Width(90)))
            {
                var name = (_newGroupName ?? "").Trim();
                if (name.Length > 0 && _manager.GetGroup(name) == null)
                {
                    _manager.GetOrCreateGroup(name);
                    _newGroupName = "";
                    _editingGroup = _manager.GetGroup(name);
                    _manager.MarkDirty();
                    SaveIfDirty();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            var groups = _manager.GetGroups();
            if (groups.Count > 0)
            {
                GUILayout.Label(ModLocalization.T("chest.groups.header"), labelBold);
                _groupListScroll = GUILayout.BeginScrollView(_groupListScroll, GUILayout.Height(100));
                for (int i = 0; i < groups.Count; i++)
                {
                    var g = groups[i];
                    GUILayout.BeginHorizontal();
                    bool isEditing = _editingGroup == g;
                    var btnStyle = isEditing ? _chestSelectorActiveStyle : _chestSelectorStyle;
                    if (GUILayout.Button($"{g.Name} ({g.ItemIds?.Count ?? 0})", btnStyle, GUILayout.ExpandWidth(true)))
                        _editingGroup = g;
                    if (GUILayout.Button("X", _chestRemoveRuleBtnStyle, GUILayout.Width(26), GUILayout.Height(22)))
                    {
                        _manager.RemoveGroup(g.Name);
                        if (_editingGroup == g) _editingGroup = null;
                        _manager.MarkDirty();
                        SaveIfDirty();
                        }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                GUILayout.Space(6);
            }

            if (_editingGroup != null && _manager.GetGroup(_editingGroup.Name) != null)
            {
                GUILayout.Label(ModLocalization.T("chest.groups.editing", _editingGroup.Name), labelBold);
                if (_editingGroup.ItemIds != null && _editingGroup.ItemIds.Count > 0)
                {
                    _groupItemsScroll = GUILayout.BeginScrollView(_groupItemsScroll, GUILayout.Height(80));
                    for (int i = _editingGroup.ItemIds.Count - 1; i >= 0; i--)
                    {
                        int id = _editingGroup.ItemIds[i];
                        var itemName = ItemSearch.GetItemName(id) ?? $"#{id}";
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(itemName, label, GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("X", _chestRemoveRuleBtnStyle, GUILayout.Width(24), GUILayout.Height(20)))
                        {
                            _editingGroup.ItemIds.RemoveAt(i);
                            _manager.MarkDirty();
                            SaveIfDirty();
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();
                }
                else
                {
                    GUILayout.Label(ModLocalization.T("chest.groups.noItems"), labelDim);
                }

                if (_editingGroup.NamePatterns != null && _editingGroup.NamePatterns.Count > 0)
                {
                    GUILayout.Space(4);
                    GUILayout.Label(ModLocalization.T("chest.groups.wildcardPatterns"), labelBold);
                    for (int i = _editingGroup.NamePatterns.Count - 1; i >= 0; i--)
                    {
                        string pattern = _editingGroup.NamePatterns[i];
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(pattern, label, GUILayout.ExpandWidth(true));
                        if (GUILayout.Button(ModLocalization.T("chest.groups.addIds"), _chestAddButtonStyle, GUILayout.Width(52), GUILayout.Height(20)))
                        {
                            int added = ExpandPatternToItemIds(_editingGroup, pattern);
                            Plugin.Log?.LogInfo($"[SmartChestUI] Group '{_editingGroup.Name}': pattern '{pattern}' added {added} item ID(s)");
                        }
                        if (GUILayout.Button("X", _chestRemoveRuleBtnStyle, GUILayout.Width(24), GUILayout.Height(20)))
                        {
                            _editingGroup.NamePatterns.RemoveAt(i);
                            _manager.MarkDirty();
                            SaveIfDirty();
                        }
                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.Space(4);
                GUILayout.Label(ModLocalization.T("chest.groups.addItemSearch"), labelBold);
                if (_groupItemSearch != _groupItemSearchLast)
                {
                    _groupItemSearchLast = _groupItemSearch ?? "";
                    _groupSearchResults = string.IsNullOrEmpty(_groupItemSearchLast) || _groupItemSearchLast.Length < 2
                        ? new List<KeyValuePair<int, string>>()
                        : ItemSearch.SearchItems(_groupItemSearchLast, 15);
                }
                GUI.SetNextControlName(GroupSearchFieldControlName);
                _groupItemSearch = GUILayout.TextField(_groupItemSearch ?? "", _configSearchFieldStyle ?? _chestSearchFieldStyle);
                if (_groupSearchResults.Count > 0)
                {
                    _groupSearchScroll = GUILayout.BeginScrollView(_groupSearchScroll, GUILayout.Height(100));
                    foreach (var kv in _groupSearchResults)
                    {
                        if (GUILayout.Button(ItemSearch.FormatDisplay(kv.Value, kv.Key), _chestSearchResultStyle, GUILayout.Height(22)))
                        {
                            if (_editingGroup.ItemIds == null) _editingGroup.ItemIds = new List<int>();
                            if (!_editingGroup.ItemIds.Contains(kv.Key))
                            {
                                _editingGroup.ItemIds.Add(kv.Key);
                                _manager.MarkDirty();
                                SaveIfDirty();
                            }
                            _groupItemSearch = "";
                            _groupItemSearchLast = "";
                            _groupSearchResults.Clear();
                            break;
                        }
                    }
                    GUILayout.EndScrollView();
                }

                GUILayout.Space(6);
                GUILayout.Label(ModLocalization.T("chest.groups.addWildcard"), labelBold);
                GUI.SetNextControlName(WildcardPatternControlName);
                _groupPatternInput = GUILayout.TextField(_groupPatternInput ?? "", _configSearchFieldStyle ?? _chestSearchFieldStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label(ModLocalization.T("chest.groups.wildcardExamples"), labelDim, GUILayout.ExpandWidth(true));
                if (GUILayout.Button(ModLocalization.T("chest.groups.addPattern"), _chestAddButtonStyle, GUILayout.Width(100), GUILayout.Height(22)))
                {
                    string pattern = (_groupPatternInput ?? "").Trim();
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        if (_editingGroup.NamePatterns == null)
                            _editingGroup.NamePatterns = new List<string>();
                        if (!_editingGroup.NamePatterns.Contains(pattern))
                        {
                            _editingGroup.NamePatterns.Add(pattern);
                            _manager.MarkDirty();
                            SaveIfDirty();
                        }
                        _groupPatternInput = "";
                    }
                }
                if (GUILayout.Button(ModLocalization.T("chest.groups.addMatchesToIds"), _chestAddButtonStyle, GUILayout.Width(132), GUILayout.Height(22)))
                {
                    string pattern = (_groupPatternInput ?? "").Trim();
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        int added = ExpandPatternToItemIds(_editingGroup, pattern);
                        Plugin.Log?.LogInfo($"[SmartChestUI] Group '{_editingGroup.Name}': input pattern '{pattern}' added {added} item ID(s)");
                    }
                    else
                    {
                        Plugin.Log?.LogWarning("Enter a wildcard pattern first, then click Add Matches to IDs");
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(ModLocalization.T("chest.close"), _chestCloseButtonStyle, GUILayout.Height(28)))
            {
                _groupsWindowVisible = false;
                _editingGroup = null;
                _newGroupName = "";
                _groupPatternInput = "";
            }
            GUI.DragWindow(new Rect(0, 0, GroupsWindowWidth, Scaled(24)));
        }

        private void DrawNoteContent(float width, float height)
        {
            if (_configWindowStyle == null) InitializeStyles();
            string keyStr = SmartChestConfig.StaticRequireCtrl ? $"Ctrl+{SmartChestConfig.StaticToggleKey}" : SmartChestConfig.StaticToggleKey.ToString();
            string text = $"Press [{keyStr}] to configure smart chest rules";
            if (_noteBannerBg != null)
                GUI.DrawTexture(new Rect(0, 0, width, height), _noteBannerBg);
            const float bw = 1f;
            if (_noteBannerBorderTex != null)
            {
                GUI.DrawTexture(new Rect(0, 0, width, bw), _noteBannerBorderTex);
                GUI.DrawTexture(new Rect(0, height - bw, width, bw), _noteBannerBorderTex);
                GUI.DrawTexture(new Rect(0, 0, bw, height), _noteBannerBorderTex);
                GUI.DrawTexture(new Rect(width - bw, 0, bw, height), _noteBannerBorderTex);
            }
            GUILayout.BeginArea(new Rect(0, 0, width, height));
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, _noteBannerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawSelectorButton(int index, params GUILayoutOption[] options)
        {
            var style = (index == _selectedRuleType) ? _chestSelectorActiveStyle : _chestSelectorStyle;
            if (GUILayout.Button(RuleTypeNames[index], style, options))
                _selectedRuleType = index;
        }

        private void DrawRuleInput()
        {
            var labelBold = _chestLabelBoldStyle;
            var label = _chestLabelStyle;
            var labelDim = _chestLabelDimStyle;
            var searchFieldStyle = _configSearchFieldStyle;

            switch (_selectedRuleType)
            {
                case 0: // ByItemId
                    if (_itemIdInput != _lastSearchQuery)
                    {
                        _lastSearchQuery = _itemIdInput;
                        _searchResults = ItemSearch.SearchItems(_itemIdInput, 20);
                        _searchScrollPos = Vector2.zero;
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(ModLocalization.T("chest.rule.search"), labelBold, GUILayout.Width(55));
                    GUI.SetNextControlName(SearchFieldControlName);
                    _itemIdInput = GUILayout.TextField(_itemIdInput, searchFieldStyle);
                    GUILayout.EndHorizontal();

                    if (_selectedItemId > 0)
                    {
                        GUILayout.Space(2);
                        GUILayout.Label(ModLocalization.T("chest.rule.selected", ItemSearch.FormatDisplay(_selectedItemName, _selectedItemId)), label);
                    }

                    if (_searchResults.Count > 0)
                    {
                        GUILayout.Space(4);
                        _searchScrollPos = GUILayout.BeginScrollView(_searchScrollPos, GUILayout.Height(Scaled(120f)));
                        foreach (var result in _searchResults)
                        {
                            bool isMuseum = IsUndonatedMuseumItem(result.Key);
                            string resultLabel = ItemSearch.FormatDisplay(result.Value, result.Key);
                            if (isMuseum)
                                resultLabel += " [Museum]";

                            GUILayout.BeginHorizontal();
                            if (isMuseum)
                            {
                                var barRect = GUILayoutUtility.GetRect(4, 22, GUILayout.Width(4), GUILayout.Height(22));
                                GUI.color = _museumHighlight;
                                GUI.DrawTexture(barRect, Texture2D.whiteTexture);
                                GUI.color = Color.white;
                            }
                            var resultStyle = (result.Key == _selectedItemId) ? _chestSearchResultSelectedStyle : _chestSearchResultStyle;
                            if (GUILayout.Button(resultLabel, resultStyle, GUILayout.Height(22)))
                            {
                                _selectedItemId = result.Key;
                                _selectedItemName = result.Value;
                            }
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndScrollView();
                    }
                    else if (_itemIdInput.Length >= 2)
                    {
                        GUILayout.Space(2);
                        GUILayout.Label(ModLocalization.T("chest.rule.noItemsFound"), labelDim);
                    }

                    break;

                case 1: // ByCategory
                    GUILayout.Label(ModLocalization.T("chest.rule.category"), labelBold);
                    var categoryNames = GetCategoryNames();
                    if (_selectedCategory < 0 || _selectedCategory >= categoryNames.Length)
                        _selectedCategory = 0;
                    DrawOptionGrid(categoryNames, ref _selectedCategory, 3);
                    break;

                case 2: // ByItemType
                    GUILayout.Label(ModLocalization.T("chest.rule.itemType"), labelBold);
                    DrawOptionGrid(ItemTypeNames, ref _selectedItemType, 3);
                    break;

                case 3: // ByProperty
                    GUILayout.Label(ModLocalization.T("chest.rule.property"), labelBold);
                    DrawOptionGrid(PropertyDisplayNames, ref _selectedProperty, 3);
                    break;

                case 4: // ByGroup
                    var groups = _manager.GetGroups();
                    GUILayout.Label(ModLocalization.T("chest.rule.group"), labelBold);
                    if (groups.Count == 0)
                    {
                        GUILayout.Label(ModLocalization.T("chest.rule.noGroups"), labelDim);
                    }
                    else
                    {
                        for (int i = 0; i < groups.Count; i++)
                        {
                            var g = groups[i];
                            var grpStyle = i == _selectedGroup ? _chestSearchResultSelectedStyle : _chestSearchResultStyle;
                            string grpLabel = $"{g.Name} ({g.ItemIds?.Count ?? 0} items)";
                            if (GUILayout.Button(grpLabel, grpStyle, GUILayout.Height(24)))
                                _selectedGroup = i;
                        }
                    }
                    break;

                case 5: // ByNamePattern (* and ? globs vs item display names)
                    if (!IsSeparateWildcardRuleEnabled())
                    {
                        GUILayout.Label(ModLocalization.T("chest.rule.wildcardManaged"), labelDim);
                        GUILayout.Label(ModLocalization.T("chest.rule.wildcardByGroup"), labelDim);
                        break;
                    }

                    GUILayout.Label(ModLocalization.T("chest.rule.namePattern"), labelBold);
                    GUI.SetNextControlName(WildcardPatternControlName);
                    _wildcardPatternInput = GUILayout.TextField(_wildcardPatternInput ?? "", searchFieldStyle)
                        ?? "";
                    GUILayout.Space(2);
                    GUILayout.Label(ModLocalization.T("chest.rule.namePatternExamples"), labelDim);
                    break;
            }
        }

        private void DrawOptionGrid(string[] options, ref int selected, int columns)
        {
            var selStyle = _chestSelectorStyle;
            var selActiveStyle = _chestSelectorActiveStyle;
            int rows = (options.Length + columns - 1) / columns;
            for (int row = 0; row < rows; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < columns; col++)
                {
                    int idx = row * columns + col;
                    if (idx < options.Length)
                    {
                        var style = (idx == selected) ? selActiveStyle : selStyle;
                        if (GUILayout.Button(options[idx], style, GUILayout.Height(24)))
                            selected = idx;
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
        }

        private string GetRuleDisplayText(SmartChestRule rule)
        {
            if (rule.Type == RuleType.ByItemId)
            {
                var name = ItemSearch.GetItemName(rule.ItemId);
                string baseText = !string.IsNullOrEmpty(name)
                    ? $"{name} ({rule.ItemId})"
                    : $"Item ID: {rule.ItemId}";
                if (IsUndonatedMuseumItem(rule.ItemId))
                    baseText += " [Museum]";
                return baseText;
            }
            return rule.GetDisplayText();
        }

        /// <summary>
        /// Returns true if the item is a museum item that hasn't been donated yet.
        /// Requires S.M.U.T. to be loaded. Returns false if S.M.U.T. is not present.
        /// </summary>
        private bool IsUndonatedMuseumItem(int gameItemId)
        {
            try
            {
                var donationManager = SunHavenMuseumUtilityTracker.Plugin.GetDonationManager();
                if (donationManager == null || !donationManager.IsLoaded)
                    return false;

                if (MuseumContent.FindByGameItemId(gameItemId) == null)
                    return false;

                return !donationManager.HasDonatedByGameId(gameItemId);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[SmartChestUI] IsUndonatedMuseumItem({gameItemId}): {ex.Message}");
                return false;
            }
        }

        private static string[] GetCategoryNames()
        {
            return IsSmutAvailable() ? CategoryNamesWithMuseum : BaseCategoryNames;
        }

        private static bool IsSmutAvailable()
        {
            try
            {
                var pluginInfos = Chainloader.PluginInfos;
                return pluginInfos != null && pluginInfos.ContainsKey("com.azraelgodking.sunhavenmuseumutilitytracker");
            }
            catch
            {
                return false;
            }
        }

        private int ExpandPatternToItemIds(ItemGroup group, string pattern)
        {
            if (group == null || string.IsNullOrWhiteSpace(pattern))
                return 0;

            if (group.ItemIds == null)
                group.ItemIds = new List<int>();

            int added = 0;
            var allItems = ItemSearch.GetAllItems();
            for (int i = 0; i < allItems.Count; i++)
            {
                var item = allItems[i];
                if (!ItemNamePatternMatch.Matches(item.Value, pattern))
                    continue;
                if (group.ItemIds.Contains(item.Key))
                    continue;

                group.ItemIds.Add(item.Key);
                added++;
            }

            if (added > 0)
            {
                _manager.MarkDirty();
                SaveIfDirty();
            }

            return added;
        }

        private static bool IsSeparateWildcardRuleEnabled()
        {
            var cfg = Plugin.GetConfig();
            if (cfg?.SeparateWildcardRuleInUI != null)
                return cfg.SeparateWildcardRuleInUI.Value;

            return SmartChestConfig.StaticSeparateWildcardRule;
        }

        private void AddRule()
        {
            SmartChestRule rule = null;

            switch (_selectedRuleType)
            {
                case 0: // ByItemId
                    if (_selectedItemId > 0)
                    {
                        rule = new SmartChestRule { Type = RuleType.ByItemId, ItemId = _selectedItemId };
                        _itemIdInput = "";
                        _lastSearchQuery = "";
                        _searchResults.Clear();
                        _selectedItemId = -1;
                        _selectedItemName = "";
                    }
                    else
                    {
                        Plugin.Log?.LogWarning("Select an item from the search results first");
                    }
                    break;

                case 1: // ByCategory
                    var categoryNames = GetCategoryNames();
                    if (_selectedCategory >= 0 && _selectedCategory < categoryNames.Length)
                    {
                        rule = new SmartChestRule { Type = RuleType.ByCategory, CategoryName = categoryNames[_selectedCategory] };
                    }
                    break;

                case 2: // ByItemType
                    if (_selectedItemType >= 0 && _selectedItemType < ItemTypeNames.Length)
                    {
                        rule = new SmartChestRule { Type = RuleType.ByItemType, ItemTypeName = ItemTypeNames[_selectedItemType] };
                    }
                    break;

                case 3: // ByProperty
                    if (_selectedProperty >= 0 && _selectedProperty < PropertyNames.Length)
                    {
                        rule = new SmartChestRule { Type = RuleType.ByProperty, PropertyName = PropertyNames[_selectedProperty] };
                    }
                    break;

                case 4: // ByGroup
                    var groups = _manager.GetGroups();
                    if (_selectedGroup >= 0 && _selectedGroup < groups.Count)
                    {
                        var g = groups[_selectedGroup];
                        if (g != null && !string.IsNullOrEmpty(g.Name))
                            rule = new SmartChestRule { Type = RuleType.ByGroup, GroupName = g.Name };
                    }
                    else
                    {
                        Plugin.Log?.LogWarning("Select a group, or create one in Manage Groups first");
                    }
                    break;

                case 5: // ByNamePattern
                    if (!IsSeparateWildcardRuleEnabled())
                    {
                        Plugin.Log?.LogWarning("Separate wildcard rule is disabled. Add wildcard patterns in Manage Groups.");
                        break;
                    }

                    {
                        string pat = (_wildcardPatternInput ?? "").Trim();
                        if (!string.IsNullOrEmpty(pat))
                        {
                            rule = new SmartChestRule { Type = RuleType.ByNamePattern, NamePattern = pat };
                            _wildcardPatternInput = "";
                        }
                        else
                        {
                            Plugin.Log?.LogWarning("Enter a name pattern (* and ? wildcards)");
                        }
                    }

                    break;
            }

            if (rule != null)
            {
                foreach (var existing in _currentData.Rules)
                {
                    if (existing.Type == rule.Type &&
                        existing.ItemId == rule.ItemId &&
                        existing.CategoryName == rule.CategoryName &&
                        existing.ItemTypeName == rule.ItemTypeName &&
                        existing.PropertyName == rule.PropertyName &&
                        existing.GroupName == rule.GroupName &&
                        existing.NamePattern == rule.NamePattern)
                    {
                        Plugin.Log?.LogInfo("Rule already exists, skipping duplicate");
                        return;
                    }
                }

                _currentData.Rules.Add(rule);

                var config = Plugin.GetConfig();
                if (!_currentData.IsEnabled && (config == null || config.AutoEnableSmartChestOnRuleAdd.Value))
                {
                    _currentData.IsEnabled = true;
                    Plugin.Log?.LogInfo("Auto-enabled Smart Chest after adding a rule");
                }

                _manager.MarkDirty();
                SaveIfDirty();
                Plugin.Log?.LogInfo($"Added rule: {rule.GetDisplayText()}");
            }
        }

        private void SaveIfDirty()
        {
            if (_manager == null || !_manager.IsDirty)
                return;

            var saveSystem = Plugin.GetSaveSystem();
            if (saveSystem == null)
            {
                Plugin.Log?.LogWarning("[UI] SaveIfDirty: SaveSystem is null!");
                return;
            }
            saveSystem.Save();
        }

        private void ApplyPreset(string presetName)
        {
            if (_currentData == null || string.IsNullOrWhiteSpace(presetName))
                return;

            var presetRules = new List<SmartChestRule>();
            switch (presetName)
            {
                case "Equipment":
                    presetRules.Add(new SmartChestRule { Type = RuleType.ByCategory, CategoryName = "Equip" });
                    break;
                case "Consumables":
                    presetRules.Add(new SmartChestRule { Type = RuleType.ByCategory, CategoryName = "Use" });
                    presetRules.Add(new SmartChestRule { Type = RuleType.ByItemType, ItemTypeName = "Food" });
                    presetRules.Add(new SmartChestRule { Type = RuleType.ByProperty, PropertyName = "isPotion" });
                    break;
                case "Crafting":
                    presetRules.Add(new SmartChestRule { Type = RuleType.ByCategory, CategoryName = "Craftable" });
                    presetRules.Add(new SmartChestRule { Type = RuleType.ByProperty, PropertyName = "isArtisanryItem" });
                    break;
                default:
                    return;
            }

            int added = 0;
            foreach (var rule in presetRules)
            {
                bool exists = _currentData.Rules.Exists(existing =>
                    existing.Type == rule.Type &&
                    existing.ItemId == rule.ItemId &&
                    existing.CategoryName == rule.CategoryName &&
                    existing.ItemTypeName == rule.ItemTypeName &&
                    existing.PropertyName == rule.PropertyName &&
                    existing.GroupName == rule.GroupName);
                if (!exists)
                {
                    _currentData.Rules.Add(rule);
                    added++;
                }
            }

            if (added > 0)
            {
                _currentData.IsEnabled = true;
                _manager.MarkDirty();
                SaveIfDirty();
                Plugin.Log?.LogInfo($"[SmartChestUI] Applied preset '{presetName}' (+{added} rule(s))");
            }
        }

        #region Style Initialization

        private void InitializeStyles()
        {
            if (!_stylesDirty && _configWindowStyle != null) return;

            CreateTextures();
            CreateStyles();
            _stylesDirty = false;
        }

        private void CreateTextures()
        {
            DestroyTextures();
            _solidBg = MakeTex(4, 4, _bgDark);
            _windowBg = MakeBorderedTex(16, 16, _bgDark, _borderGold, 2);
            _ruleBg = MakeTex(1, 1, _ruleBoxColor);
            _btnInactiveTex = MakeTex(1, 1, _btnInactive);
            _btnHoverTex = MakeTex(1, 1, _btnHover);
            _btnActiveTex = MakeTex(1, 1, _greenActive);
            _btnActiveHoverTex = MakeTex(1, 1, _greenHover);
            _redBtnTex = MakeTex(1, 1, _redDanger);
            _redBtnHoverTex = MakeTex(1, 1, _redHover);
            _greenBtnTex = MakeTex(1, 1, _greenActive);
            _greenBtnHoverTex = MakeTex(1, 1, _greenBright);
            _closeBtnTex = MakeTex(1, 1, _redDanger);
            _closeBtnHoverTex = MakeTex(1, 1, _redHover);
            _fieldBgTex = MakeTex(1, 1, _fieldBg);
            _chestParchmentBg = MakeParchmentTexture(32, 64, _parchment, _parchmentLight, _borderWood, 3);
            _chestWoodBorderTex = MakeTex(1, 1, _borderWood);
            _chestRuleBoxTex = MakeRoundedRect(16, 16, _parchmentDark, _borderWood, 2);
            _chestSelectorTex = MakeRoundedRect(8, 8, _parchmentDark, _borderWood, 2);
            _chestSelectorHoverTex = MakeRoundedRect(8, 8, _woodLight, _borderWood, 2);
            _chestSelectorActiveTex = MakeRoundedRect(8, 8, _goldPale, _goldRich, 2);
            _chestAddBtnTex = MakeRoundedRect(8, 8, new Color(0.30f, 0.55f, 0.45f), new Color(0.20f, 0.40f, 0.35f), 2);
            _chestAddBtnHoverTex = MakeRoundedRect(8, 8, new Color(0.38f, 0.65f, 0.52f), new Color(0.25f, 0.48f, 0.42f), 2);
            _chestWoodBtnTex = MakeRoundedRect(8, 8, _woodMedium, _woodDark, 2);
            _chestWoodBtnHoverTex = MakeRoundedRect(8, 8, _woodLight, _woodMedium, 2);
            _chestDangerBtnTex = MakeRoundedRect(8, 8, _redDanger, new Color(0.55f, 0.15f, 0.15f), 2);
            _chestDangerBtnHoverTex = MakeRoundedRect(8, 8, _redHover, new Color(0.65f, 0.18f, 0.18f), 2);
            _chestFieldBgTex = MakeRoundedRect(6, 6, _parchmentLight, _borderWood, 1);
            _chestGoldLineTex = MakeGradientTex(64, 3, _goldPale, _goldRich);
            _noteBannerBg = MakeTex(4, 4, new Color(0.45f, 0.10f, 0.08f, 1f));
            _noteBannerBorderTex = MakeTex(1, 1, _goldRich);

            _configTitleBarTex = MakeGradientTex(64, 32, _crimsonTitleLight, _crimsonTitle);
            _configContentBg = MakeParchmentTexture(64, 128, _parchment, _parchmentLight, _borderWood, 0);
            _configGoldSeparatorTex = MakeGradientTex(64, 2, _goldPale, _goldRich);
            _configSectionHeaderTex = MakeRoundedRect(16, 16, _parchmentDark, _goldRich, 2);
            _configSearchFieldTex = MakeRoundedRect(8, 8, Color.white, new Color(0.25f, 0.2f, 0.18f), 1);
            _configRemoveBtnTex = MakeRoundedRect(8, 8, _crimsonTitle, new Color(0.4f, 0.12f, 0.08f), 1);
            _configRemoveBtnHoverTex = MakeRoundedRect(8, 8, _crimsonTitleLight, new Color(0.45f, 0.15f, 0.10f), 1);
            _configCloseBtnTex = MakeRoundedRect(8, 8, _woodMedium, _woodDark, 1);
            _configCloseBtnHoverTex = MakeRoundedRect(8, 8, _woodLight, _woodMedium, 1);
            _configWindowBg = MakeBorderedTex(64, 64, _parchmentLight, _goldRich, 5);
            _configInnerBorderTex = MakeTex(1, 1, _innerBorderCrimson);
        }

        private void DestroyTextures()
        {
            if (_solidBg != null) Destroy(_solidBg);
            if (_windowBg != null) Destroy(_windowBg);
            if (_ruleBg != null) Destroy(_ruleBg);
            if (_btnInactiveTex != null) Destroy(_btnInactiveTex);
            if (_btnHoverTex != null) Destroy(_btnHoverTex);
            if (_btnActiveTex != null) Destroy(_btnActiveTex);
            if (_btnActiveHoverTex != null) Destroy(_btnActiveHoverTex);
            if (_redBtnTex != null) Destroy(_redBtnTex);
            if (_redBtnHoverTex != null) Destroy(_redBtnHoverTex);
            if (_greenBtnTex != null) Destroy(_greenBtnTex);
            if (_greenBtnHoverTex != null) Destroy(_greenBtnHoverTex);
            if (_closeBtnTex != null) Destroy(_closeBtnTex);
            if (_closeBtnHoverTex != null) Destroy(_closeBtnHoverTex);
            if (_fieldBgTex != null) Destroy(_fieldBgTex);
            if (_chestParchmentBg != null) Destroy(_chestParchmentBg);
            if (_chestWoodBorderTex != null) Destroy(_chestWoodBorderTex);
            if (_chestRuleBoxTex != null) Destroy(_chestRuleBoxTex);
            if (_chestSelectorTex != null) Destroy(_chestSelectorTex);
            if (_chestSelectorHoverTex != null) Destroy(_chestSelectorHoverTex);
            if (_chestSelectorActiveTex != null) Destroy(_chestSelectorActiveTex);
            if (_chestAddBtnTex != null) Destroy(_chestAddBtnTex);
            if (_chestAddBtnHoverTex != null) Destroy(_chestAddBtnHoverTex);
            if (_chestWoodBtnTex != null) Destroy(_chestWoodBtnTex);
            if (_chestWoodBtnHoverTex != null) Destroy(_chestWoodBtnHoverTex);
            if (_chestDangerBtnTex != null) Destroy(_chestDangerBtnTex);
            if (_chestDangerBtnHoverTex != null) Destroy(_chestDangerBtnHoverTex);
            if (_chestFieldBgTex != null) Destroy(_chestFieldBgTex);
            if (_chestGoldLineTex != null) Destroy(_chestGoldLineTex);
            if (_noteBannerBg != null) Destroy(_noteBannerBg);
            if (_noteBannerBorderTex != null) Destroy(_noteBannerBorderTex);
            if (_configTitleBarTex != null) Destroy(_configTitleBarTex);
            if (_configContentBg != null) Destroy(_configContentBg);
            if (_configGoldSeparatorTex != null) Destroy(_configGoldSeparatorTex);
            if (_configSectionHeaderTex != null) Destroy(_configSectionHeaderTex);
            if (_configSearchFieldTex != null) Destroy(_configSearchFieldTex);
            if (_configRemoveBtnTex != null) Destroy(_configRemoveBtnTex);
            if (_configRemoveBtnHoverTex != null) Destroy(_configRemoveBtnHoverTex);
            if (_configCloseBtnTex != null) Destroy(_configCloseBtnTex);
            if (_configCloseBtnHoverTex != null) Destroy(_configCloseBtnHoverTex);
            if (_configWindowBg != null) Destroy(_configWindowBg);
            if (_configInnerBorderTex != null) Destroy(_configInnerBorderTex);
        }

        private void CreateStyles()
        {
            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(ScaledInt(14), ScaledInt(14), ScaledInt(12), ScaledInt(12)),
                border = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };
            _windowStyle.normal.background = _windowBg;
            _windowStyle.normal.textColor = _whiteText;
            _windowStyle.onNormal.background = _windowBg;
            _windowStyle.onNormal.textColor = _whiteText;

            _titleStyle = new GUIStyle
            {
                fontSize = ScaledFont(15),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _whiteText },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };

            _sectionHeaderStyle = new GUIStyle
            {
                fontSize = ScaledFont(15),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _goldText },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(4), ScaledInt(4), ScaledInt(2), ScaledInt(2))
            };

            _labelStyle = new GUIStyle
            {
                fontSize = ScaledFont(13),
                normal = { textColor = _whiteText },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2)),
                wordWrap = true
            };

            _labelBoldStyle = new GUIStyle(_labelStyle) { fontStyle = FontStyle.Bold };

            _labelDimStyle = new GUIStyle(_labelStyle)
            {
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Italic,
                normal = { textColor = _dimText }
            };

            _ruleBoxStyle = new GUIStyle
            {
                normal = { background = _ruleBg },
                padding = new RectOffset(ScaledInt(10), ScaledInt(8), ScaledInt(6), ScaledInt(6)),
                margin = new RectOffset(ScaledInt(4), ScaledInt(4), ScaledInt(2), ScaledInt(2))
            };

            _ruleTextStyle = new GUIStyle(_labelStyle) { fontSize = ScaledFont(13) };

            _removeRuleBtnStyle = new GUIStyle
            {
                fontSize = ScaledFont(13),
                fontStyle = FontStyle.Bold,
                normal = { background = _redBtnTex, textColor = _whiteText },
                hover = { background = _redBtnHoverTex, textColor = _whiteText },
                active = { background = _redBtnHoverTex, textColor = _whiteText },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };

            _closeButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(14),
                fontStyle = FontStyle.Bold,
                normal = { background = _closeBtnTex, textColor = _whiteText },
                hover = { background = _closeBtnHoverTex, textColor = _whiteText },
                active = { background = _closeBtnHoverTex, textColor = _whiteText },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(1), ScaledInt(1))
            };

            _toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = ScaledFont(13) };
            _toggleStyle.normal.textColor = _whiteText;
            _toggleStyle.onNormal.textColor = _whiteText;
            _toggleStyle.hover.textColor = _whiteText;
            _toggleStyle.onHover.textColor = _whiteText;

            _textFieldStyle = new GUIStyle
            {
                fontSize = ScaledFont(13),
                normal = { background = _fieldBgTex, textColor = _whiteText },
                focused = { background = _fieldBgTex, textColor = _whiteText },
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(5), ScaledInt(5)),
                border = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };

            _selectorStyle = new GUIStyle
            {
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Bold,
                normal = { background = _btnInactiveTex, textColor = _whiteText },
                hover = { background = _btnHoverTex, textColor = _whiteText },
                active = { background = _btnActiveTex, textColor = _whiteText },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(5), ScaledInt(5)),
                margin = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(1), ScaledInt(1))
            };

            _selectorActiveStyle = new GUIStyle(_selectorStyle)
            {
                normal = { background = _btnActiveTex, textColor = _whiteText },
                hover = { background = _btnActiveHoverTex, textColor = _whiteText }
            };

            _addButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(14),
                fontStyle = FontStyle.Bold,
                normal = { background = _greenBtnTex, textColor = _whiteText },
                hover = { background = _greenBtnHoverTex, textColor = _whiteText },
                active = { background = _greenBtnHoverTex, textColor = _whiteText },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 5, 5)
            };

            _dangerButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Bold,
                normal = { background = _redBtnTex, textColor = _whiteText },
                hover = { background = _redBtnHoverTex, textColor = _whiteText },
                active = { background = _redBtnHoverTex, textColor = _whiteText },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 4, 4)
            };

            _closeBottomButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Bold,
                normal = { background = _btnInactiveTex, textColor = _whiteText },
                hover = { background = _btnHoverTex, textColor = _whiteText },
                active = { background = _btnHoverTex, textColor = _whiteText },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 4, 4)
            };

            _searchResultStyle = new GUIStyle
            {
                fontSize = ScaledFont(12),
                normal = { background = _ruleBg, textColor = _whiteText },
                hover = { background = _btnHoverTex, textColor = _whiteText },
                active = { background = _btnActiveTex, textColor = _whiteText },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(3), ScaledInt(3)),
                margin = new RectOffset(0, 0, ScaledInt(1), ScaledInt(1))
            };

            _searchResultSelectedStyle = new GUIStyle(_searchResultStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { background = _btnActiveTex, textColor = _whiteText },
                hover = { background = _btnActiveHoverTex, textColor = _whiteText }
            };

            _chestToggleStyle = new GUIStyle(_toggleStyle) { fontSize = ScaledFont(14) };
            _chestToggleStyle.normal.textColor = _chestTextDark;
            _chestToggleStyle.onNormal.textColor = _chestTextDark;
            _chestToggleStyle.hover.textColor = _chestTextDark;
            _chestToggleStyle.onHover.textColor = _chestTextDark;

            _chestTitleStyle = new GUIStyle(_titleStyle)
            {
                normal = { textColor = _woodDark }, hover = { textColor = _woodDark }, active = { textColor = _woodDark },
                focused = { textColor = _woodDark }, onNormal = { textColor = _woodDark }, onHover = { textColor = _woodDark },
                onActive = { textColor = _woodDark }, onFocused = { textColor = _woodDark }, fontSize = ScaledFont(17)
            };

            _chestSectionHeaderStyle = new GUIStyle(_sectionHeaderStyle)
            {
                normal = { textColor = _woodDark }, hover = { textColor = _woodDark }, active = { textColor = _woodDark },
                focused = { textColor = _woodDark }, onNormal = { textColor = _woodDark }, onHover = { textColor = _woodDark },
                onActive = { textColor = _woodDark }, onFocused = { textColor = _woodDark }, fontSize = ScaledFont(16)
            };

            _chestLabelStyle = new GUIStyle(_labelStyle) { normal = { textColor = _chestTextDark }, fontSize = ScaledFont(14) };
            _chestLabelBoldStyle = new GUIStyle(_chestLabelStyle) { fontStyle = FontStyle.Bold };
            _chestLabelDimStyle = new GUIStyle(_chestLabelStyle)
            {
                fontSize = ScaledFont(13), fontStyle = FontStyle.Italic, normal = { textColor = _chestTextDim }
            };

            _chestRuleBoxStyle = new GUIStyle { normal = { background = _chestRuleBoxTex }, padding = new RectOffset(ScaledInt(10), ScaledInt(8), ScaledInt(6), ScaledInt(6)), margin = new RectOffset(ScaledInt(4), ScaledInt(4), ScaledInt(2), ScaledInt(2)) };
            _chestRuleTextStyle = new GUIStyle(_labelStyle) { fontSize = ScaledFont(13), normal = { textColor = _woodDark }, hover = { textColor = _woodDark }, active = { textColor = _woodDark } };
            _chestSelectorStyle = new GUIStyle
            {
                fontSize = ScaledFont(12), fontStyle = FontStyle.Bold,
                normal = { background = _chestSelectorTex, textColor = _woodDark },
                hover = { background = _chestSelectorHoverTex, textColor = _woodDark },
                active = { background = _chestSelectorActiveTex, textColor = _woodDark },
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(5), ScaledInt(5)), margin = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(1), ScaledInt(1))
            };
            _chestSelectorActiveStyle = new GUIStyle(_chestSelectorStyle)
            {
                normal = { background = _chestSelectorActiveTex, textColor = _woodDark },
                hover = { background = _chestSelectorActiveTex, textColor = _woodDark }
            };
            _chestAddButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(14), fontStyle = FontStyle.Bold,
                normal = { background = _chestAddBtnTex, textColor = _parchmentLight },
                hover = { background = _chestAddBtnHoverTex, textColor = _parchmentLight },
                active = { background = _chestAddBtnHoverTex, textColor = _parchmentLight },
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 5, 5)
            };
            _chestDangerButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(12), fontStyle = FontStyle.Bold,
                normal = { background = _chestDangerBtnTex, textColor = _whiteText },
                hover = { background = _chestDangerBtnHoverTex, textColor = _whiteText },
                active = { background = _chestDangerBtnHoverTex, textColor = _whiteText },
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 4, 4)
            };
            _chestCloseButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(12), fontStyle = FontStyle.Bold,
                normal = { background = _chestWoodBtnTex, textColor = _parchmentLight },
                hover = { background = _chestWoodBtnHoverTex, textColor = _parchmentLight },
                active = { background = _chestWoodBtnHoverTex, textColor = _parchmentLight },
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 4, 4)
            };
            _chestSearchFieldStyle = new GUIStyle
            {
                fontSize = ScaledFont(13),
                normal = { background = _chestFieldBgTex, textColor = _woodDark },
                focused = { background = _chestFieldBgTex, textColor = _woodDark },
                hover = { background = _chestFieldBgTex, textColor = _woodDark },
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(5), ScaledInt(5)), border = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };
            _chestRemoveRuleBtnStyle = new GUIStyle
            {
                fontSize = ScaledFont(13), fontStyle = FontStyle.Bold,
                normal = { background = _chestDangerBtnTex, textColor = _whiteText },
                hover = { background = _chestDangerBtnHoverTex, textColor = _whiteText },
                active = { background = _chestDangerBtnHoverTex, textColor = _whiteText },
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset(2, 2, 2, 2)
            };
            _chestSearchResultStyle = new GUIStyle
            {
                fontSize = ScaledFont(12),
                normal = { background = _chestRuleBoxTex, textColor = _woodDark },
                hover = { background = _chestSelectorHoverTex, textColor = _woodDark },
                active = { background = _chestSelectorActiveTex, textColor = _woodDark },
                alignment = TextAnchor.MiddleLeft, padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(3), ScaledInt(3)), margin = new RectOffset(0, 0, ScaledInt(1), ScaledInt(1))
            };
            _chestSearchResultSelectedStyle = new GUIStyle(_chestSearchResultStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { background = _chestSelectorActiveTex, textColor = _woodDark },
                hover = { background = _chestSelectorActiveTex, textColor = _woodDark }
            };

            _noteBannerStyle = new GUIStyle
            {
                fontSize = ScaledFont(14), fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.92f, 0.85f) },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(8), ScaledInt(4), ScaledInt(8), ScaledInt(4))
            };

            _configTitleStyle = new GUIStyle
            {
                fontSize = ScaledFont(16), fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(4), ScaledInt(4), ScaledInt(4), ScaledInt(4))
            };

            _configSectionHeaderBoxStyle = new GUIStyle
            {
                fontSize = ScaledFont(14), fontStyle = FontStyle.Bold,
                normal = { background = _configSectionHeaderTex, textColor = _chestTextDark },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(12), ScaledInt(8), ScaledInt(6), ScaledInt(6)),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _configSearchFieldStyle = new GUIStyle
            {
                fontSize = ScaledFont(13),
                normal = { background = _configSearchFieldTex, textColor = _chestTextDark },
                focused = { background = _configSearchFieldTex, textColor = _chestTextDark },
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(5), ScaledInt(5)),
                border = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };

            _configRemoveButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(12), fontStyle = FontStyle.Bold,
                normal = { background = _configRemoveBtnTex, textColor = Color.white },
                hover = { background = _configRemoveBtnHoverTex, textColor = Color.white },
                active = { background = _configRemoveBtnHoverTex, textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 6, 6)
            };

            _configCloseBottomStyle = new GUIStyle
            {
                fontSize = ScaledFont(12), fontStyle = FontStyle.Bold,
                normal = { background = _configCloseBtnTex, textColor = Color.white },
                hover = { background = _configCloseBtnHoverTex, textColor = Color.white },
                active = { background = _configCloseBtnHoverTex, textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 6, 6)
            };

            _configWindowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(ScaledInt(5), ScaledInt(5), ScaledInt(5), ScaledInt(5)),
                border = new RectOffset(ScaledInt(5), ScaledInt(5), ScaledInt(5), ScaledInt(5))
            };
            _configWindowStyle.normal.background = _configWindowBg;
            _configWindowStyle.onNormal.background = _configWindowBg;
        }

        #endregion

        #region Texture Generation

        private Texture2D MakeTex(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeBorderedTex(int width, int height, Color fillColor, Color borderColor, int borderWidth)
        {
            var tex = new Texture2D(width, height);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = x < borderWidth || x >= width - borderWidth ||
                                   y < borderWidth || y >= height - borderWidth;
                    pixels[y * width + x] = isBorder ? borderColor : fillColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeGradientTex(int width, int height, Color topColor, Color bottomColor)
        {
            var tex = new Texture2D(width, height);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                Color rowColor = Color.Lerp(bottomColor, topColor, t);
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = rowColor;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeRoundedRect(int width, int height, Color fillColor, Color borderColor, int borderWidth)
        {
            var tex = new Texture2D(width, height);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = x < borderWidth || x >= width - borderWidth ||
                                   y < borderWidth || y >= height - borderWidth;
                    pixels[y * width + x] = isBorder ? borderColor : fillColor;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeParchmentTexture(int width, int height, Color baseColor, Color lightColor, Color borderColor, int borderWidth)
        {
            var tex = new Texture2D(width, height);
            var pixels = new Color[width * height];
            var rand = new System.Random(42);
            for (int y = 0; y < height; y++)
            {
                float gradientT = (float)y / (height - 1);
                Color rowBase = Color.Lerp(lightColor, baseColor, gradientT * 0.3f);
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = x < borderWidth || x >= width - borderWidth ||
                                   y < borderWidth || y >= height - borderWidth;
                    if (isBorder)
                        pixels[y * width + x] = borderColor;
                    else
                    {
                        float noise = (float)rand.NextDouble() * 0.03f - 0.015f;
                        pixels[y * width + x] = new Color(
                            Mathf.Clamp01(rowBase.r + noise),
                            Mathf.Clamp01(rowBase.g + noise),
                            Mathf.Clamp01(rowBase.b + noise),
                            rowBase.a);
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        #endregion

        private void OnDestroy()
        {
            DestroyTextures();
        }

        private void Awake()
        {
            _stylesDirty = true;
            _configWindowStyle = null;
        }

        private void OnEnable()
        {
            _stylesDirty = true;
            _configWindowStyle = null;
        }
    }
}
