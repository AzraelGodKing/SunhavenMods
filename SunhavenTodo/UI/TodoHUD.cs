using System;
using System.Collections.Generic;
using System.Linq;
using SunhavenMods.Shared;
using SunhavenTodo.Data;
using UnityEngine;

namespace SunhavenTodo.UI
{
    /// <summary>
    /// Movable HUD panel that displays the top 5 most urgent todo items.
    /// View-only interface - players can drag it anywhere on screen.
    /// </summary>
    public class TodoHUD : MonoBehaviour
    {
        // Window settings (base values, scaled by _scale)
        private const int WINDOW_ID = 98766;
        private const float BASE_WINDOW_WIDTH = 280f;
        private const float BASE_MIN_HEIGHT = 100f;
        private const float BASE_MAX_HEIGHT = 300f;
        private const float BASE_HEADER_HEIGHT = 28f;
        private const float BASE_ITEM_HEIGHT = 24f;
        private const float BASE_ICON_SIZE = 16f;
        private const int MAX_ITEMS = 5;

        private float _scale = 1f;
        private float WindowWidth => BASE_WINDOW_WIDTH * _scale;
        private float MinHeight => BASE_MIN_HEIGHT * _scale;
        private float MaxHeight => BASE_MAX_HEIGHT * _scale;
        private float HeaderHeight => BASE_HEADER_HEIGHT * _scale;
        private float ItemHeight => BASE_ITEM_HEIGHT * _scale;
        private float IconSize => BASE_ICON_SIZE * _scale;
        private int ScaledFont(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * _scale));
        private float Scaled(float value) => value * _scale;
        private int ScaledInt(float value) => Mathf.RoundToInt(value * _scale);

        // Manager reference
        private TodoManager _manager;

        // Display state
        private bool _isEnabled = true;
        private Rect _windowRect;

        // Position persistence callback
        public Action<float, float> OnPositionChanged;

        // Cached data
        private List<TodoItem> _cachedItems = new List<TodoItem>();
        private float _lastUpdateTime;
        private const float UPDATE_INTERVAL = 1f;

        // Sun Haven themed colors (matching TodoUI)
        private readonly Color _parchmentLight = new Color(0.96f, 0.93f, 0.86f, 0.95f);
        private readonly Color _parchment = new Color(0.92f, 0.87f, 0.78f, 0.95f);
        private readonly Color _parchmentDark = new Color(0.85f, 0.78f, 0.65f, 0.95f);
        private readonly Color _woodDark = new Color(0.35f, 0.25f, 0.15f);
        private readonly Color _woodMedium = new Color(0.50f, 0.38f, 0.25f);
        private readonly Color _goldRich = new Color(0.85f, 0.68f, 0.20f);
        private readonly Color _goldBright = new Color(0.95f, 0.80f, 0.30f);
        private readonly Color _textDark = new Color(0.25f, 0.20f, 0.15f);
        private readonly Color _borderDark = new Color(0.40f, 0.30f, 0.20f, 0.8f);

        // Priority colors
        private readonly Dictionary<TodoPriority, Color> _priorityColors = new Dictionary<TodoPriority, Color>
        {
            { TodoPriority.Low, new Color(0.50f, 0.60f, 0.70f) },
            { TodoPriority.Normal, new Color(0.45f, 0.55f, 0.45f) },
            { TodoPriority.High, new Color(0.85f, 0.65f, 0.25f) },
            { TodoPriority.Urgent, new Color(0.80f, 0.30f, 0.25f) }
        };

        // Category colors
        private readonly Dictionary<TodoCategory, Color> _categoryColors = new Dictionary<TodoCategory, Color>
        {
            { TodoCategory.General, new Color(0.55f, 0.50f, 0.45f) },
            { TodoCategory.Farming, new Color(0.45f, 0.65f, 0.35f) },
            { TodoCategory.Mining, new Color(0.50f, 0.45f, 0.55f) },
            { TodoCategory.Fishing, new Color(0.40f, 0.60f, 0.75f) },
            { TodoCategory.Combat, new Color(0.75f, 0.35f, 0.35f) },
            { TodoCategory.Crafting, new Color(0.65f, 0.55f, 0.40f) },
            { TodoCategory.Social, new Color(0.70f, 0.50f, 0.60f) },
            { TodoCategory.Quests, new Color(0.80f, 0.70f, 0.30f) },
            { TodoCategory.Collection, new Color(0.55f, 0.70f, 0.65f) }
        };

        // Styles
        private bool _stylesInitialized;
        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _itemStyle;
        private GUIStyle _priorityStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _emptyStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _wrappedTitleStyle;
        private GUIStyle _rowStyle;

        // Textures
        private Texture2D _windowBackground;
        private Texture2D _headerBackground;
        private Texture2D _itemEven;
        private Texture2D _itemOdd;
        private Texture2D _closeBtnNormal;
        private Texture2D _closeBtnHover;
        private Texture2D _closeBtnActive;

        public void Initialize(TodoManager manager)
        {
            _manager = manager;

            // Default position: top-right corner with some padding
            _windowRect = new Rect(
                Screen.width - WindowWidth - Scaled(20),
                Scaled(100),
                WindowWidth,
                MinHeight
            );

            if (_manager != null)
            {
                _manager.OnTodosChanged += RefreshCache;
                _manager.OnDataLoaded += RefreshCache;
            }
        }

        public void SetPosition(float x, float y)
        {
            _windowRect.x = Mathf.Clamp(x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(y, 0, Screen.height - _windowRect.height);
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        public void SetScale(float scale)
        {
            _scale = Mathf.Clamp(scale, 0.5f, 2.5f);
            _stylesInitialized = false;
        }

        public bool IsEnabled => _isEnabled;

        public void Toggle()
        {
            _isEnabled = !_isEnabled;
            Plugin.Log?.LogInfo($"TodoHUD toggled: {(_isEnabled ? "ON" : "OFF")}");
        }

        private void Update()
        {
            if (!_isEnabled || _manager == null) return;

            // Periodically refresh cache
            if (Time.unscaledTime - _lastUpdateTime > UPDATE_INTERVAL)
            {
                RefreshCache();
                _lastUpdateTime = Time.unscaledTime;
            }
        }

        private void RefreshCache()
        {
            if (_manager == null) return;

            _cachedItems = _manager.GetActiveTodos()
                .OrderByDescending(t => (int)t.Priority)
                .ThenByDescending(t => t.CreatedAt)
                .Take(MAX_ITEMS)
                .ToList();
        }

        private void OnGUI()
        {
            // Don't show if disabled or no manager
            if (!_isEnabled || _manager == null) return;

            // Don't show if main TodoUI is open
            var todoUI = Plugin.GetTodoUI();
            if (todoUI != null && todoUI.IsVisible) return;

            // Don't show if no data loaded
            if (string.IsNullOrEmpty(_manager.CurrentCharacter)) return;

            InitializeStyles();

            // Calculate dynamic height based on content
            float contentHeight = HeaderHeight + Scaled(8);
            if (_cachedItems.Count == 0)
            {
                contentHeight += Scaled(40);
            }
            else
            {
                for (int i = 0; i < _cachedItems.Count; i++)
                {
                    contentHeight += GetItemRowHeight(_cachedItems[i]);
                }
                contentHeight += Scaled(4);
            }
            _windowRect.width = WindowWidth;
            _windowRect.height = Mathf.Clamp(contentHeight, MinHeight, MaxHeight);

            // Store position before drawing
            var prevRect = _windowRect;

            // Draw the window
            GUI.depth = -500; // Below main UI but above game
            _windowRect = GUI.Window(WINDOW_ID, _windowRect, DrawWindow, "", _windowStyle);

            // Clamp to screen bounds
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);

            // Notify if position changed (for persistence)
            if (Math.Abs(_windowRect.x - prevRect.x) > 0.1f || Math.Abs(_windowRect.y - prevRect.y) > 0.1f)
            {
                OnPositionChanged?.Invoke(_windowRect.x, _windowRect.y);
            }
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            DrawHeader();
            DrawItems();

            GUILayout.EndVertical();

            // Header drag area — exclude the close button on the right so X isn't treated as drag
            float closeReserve = Scaled(8 + 26 + 8);
            GUI.DragWindow(new Rect(0, 0, Mathf.Max(Scaled(48f), _windowRect.width - closeReserve), HeaderHeight));
        }

        private void DrawHeader()
        {
            // Header background
            var headerRect = new Rect(0, 0, _windowRect.width, HeaderHeight);
            if (_headerBackground != null)
            {
                GUI.DrawTexture(headerRect, _headerBackground);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(Scaled(8));

            // Title with item count
            var activeCount = _manager?.GetActiveTodos().Count() ?? 0;
            GUILayout.Label(ModLocalization.T("todo.hud.title", activeCount), _headerStyle, GUILayout.Height(HeaderHeight));

            GUILayout.FlexibleSpace();

            // Drag hint
            var hintStyle = _hintStyle;
            var shortcut = Plugin.GetOpenListShortcutDisplay();
            GUILayout.Label(ModLocalization.T("todo.hud.openHint", shortcut), hintStyle, GUILayout.Height(HeaderHeight));

            GUILayout.FlexibleSpace();

            // Drag hint
            GUILayout.Label(ModLocalization.T("todo.hud.dragHint"), hintStyle, GUILayout.Height(HeaderHeight));

            GUILayout.Space(Scaled(4));
            if (GUILayout.Button("X", _closeButtonStyle, GUILayout.Width(Scaled(26)), GUILayout.Height(Scaled(22))))
            {
                _isEnabled = false;
                Plugin.Log?.LogInfo("TodoHUD hidden via close button (open Todo list → Sticky, or use HUD toggle hotkey)");
            }

            GUILayout.Space(Scaled(8));
            GUILayout.EndHorizontal();
        }

        private void DrawItems()
        {
            GUILayout.Space(Scaled(4));

            if (_cachedItems.Count == 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(ModLocalization.T("todo.hud.empty"), _emptyStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                for (int i = 0; i < _cachedItems.Count; i++)
                {
                    DrawItem(_cachedItems[i], i);
                }
            }

            GUILayout.Space(Scaled(4));
        }

        private void DrawItem(TodoItem item, int index)
        {
            var bgTex = index % 2 == 0 ? _itemEven : _itemOdd;
            var titleText = string.IsNullOrWhiteSpace(item.Title) ? ModLocalization.T("todo.item.noTitle") : item.Title;

            var wrappedTitleStyle = _wrappedTitleStyle;
            float titleAvailableWidth = GetTitleAvailableWidth(item);
            float titleHeight = Mathf.Ceil(wrappedTitleStyle.CalcHeight(new GUIContent(titleText), titleAvailableWidth));
            float rowHeight = Mathf.Max(ItemHeight, titleHeight + Scaled(6));

            // Use row style with background so content stays in layout flow (avoids blank title in some setups)
            var rowStyle = _rowStyle;
            rowStyle.normal.background = bgTex;

            GUILayout.BeginHorizontal(rowStyle, GUILayout.MinHeight(rowHeight));
            GUILayout.Space(Scaled(6));

            // Priority indicator
            var priorityColor = _priorityColors.TryGetValue(item.Priority, out var pc) ? pc : _textDark;
            var priorityStyle = _priorityStyle;
            priorityStyle.normal.textColor = priorityColor;
            GUILayout.Label(GetPriorityIcon(item.Priority), priorityStyle, GUILayout.Width(Scaled(16)));

            // Category indicator
            var categoryColor = _categoryColors.TryGetValue(item.Category, out var cc) ? cc : _textDark;
            var categoryStyle = _categoryStyle;
            categoryStyle.normal.textColor = categoryColor;
            GUILayout.Label($"[{GetCategoryShort(item.Category)}]", categoryStyle, GUILayout.Width(Scaled(36)));

            GUILayout.Space(Scaled(4));

            // Item icon for integration-generated tasks (e.g., museum items)
            if (item.IconItemId > 0)
            {
                var icon = IconCache.GetIcon(item.IconItemId);
                if (icon != null)
                {
                    var iconRect = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUILayout.Space(IconSize);
                }

                GUILayout.Space(Scaled(4));
            }

            // Wrapped title (no truncation)
            GUILayout.Label(titleText, wrappedTitleStyle, GUILayout.Width(titleAvailableWidth), GUILayout.MinHeight(titleHeight));
            GUILayout.FlexibleSpace();

            GUILayout.Space(Scaled(6));
            GUILayout.EndHorizontal();
        }

        private float GetItemRowHeight(TodoItem item)
        {
            if (item == null) return ItemHeight;

            var titleText = string.IsNullOrWhiteSpace(item.Title) ? ModLocalization.T("todo.item.noTitle") : item.Title;
            float titleWidth = GetTitleAvailableWidth(item);
            float titleHeight = Mathf.Ceil(_wrappedTitleStyle.CalcHeight(new GUIContent(titleText), titleWidth));
            return Mathf.Max(ItemHeight, titleHeight + Scaled(6));
        }

        private float GetTitleAvailableWidth(TodoItem item)
        {
            float reserved = Scaled(6 + 16 + 36 + 4 + 6); // left pad + priority + category + spacing + right pad
            if (item != null && item.IconItemId > 0)
                reserved += IconSize + Scaled(4);
            return Mathf.Max(Scaled(100), WindowWidth - reserved);
        }

        private string GetPriorityIcon(TodoPriority priority)
        {
            switch (priority)
            {
                case TodoPriority.Low: return "-";
                case TodoPriority.Normal: return "o";
                case TodoPriority.High: return "!";
                case TodoPriority.Urgent: return "!!";
                default: return "o";
            }
        }

        private string GetCategoryShort(TodoCategory category)
        {
            switch (category)
            {
                case TodoCategory.General: return "GEN";
                case TodoCategory.Farming: return "FRM";
                case TodoCategory.Mining: return "MIN";
                case TodoCategory.Fishing: return "FSH";
                case TodoCategory.Combat: return "CMB";
                case TodoCategory.Crafting: return "CRF";
                case TodoCategory.Social: return "SOC";
                case TodoCategory.Quests: return "QST";
                case TodoCategory.Collection: return "COL";
                default: return "GEN";
            }
        }

        #region Style Initialization

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            CreateTextures();
            CreateStyles();

            _stylesInitialized = true;
        }

        private void CreateTextures()
        {
            _windowBackground = MakeParchmentTexture(16, 64, _parchment, _parchmentLight, _borderDark, 2);
            _headerBackground = MakeGradientTex(8, Mathf.Max(1, ScaledInt(HeaderHeight)), _parchmentDark, _parchment);
            _itemEven = MakeTex(1, 1, new Color(_parchmentLight.r, _parchmentLight.g, _parchmentLight.b, 0.3f));
            _itemOdd = MakeTex(1, 1, new Color(_parchment.r, _parchment.g, _parchment.b, 0.3f));
            _closeBtnNormal = MakeTex(2, 2, _parchmentDark);
            _closeBtnHover = MakeTex(2, 2, new Color(_goldRich.r * 0.85f, _goldRich.g * 0.85f, _goldRich.b * 0.85f, 1f));
            _closeBtnActive = MakeTex(2, 2, _woodMedium);
        }

        private void CreateStyles()
        {
            _windowStyle = new GUIStyle
            {
                normal = { background = _windowBackground, textColor = _textDark },
                padding = new RectOffset(ScaledInt(4), ScaledInt(4), ScaledInt(4), ScaledInt(4)),
                border = new RectOffset(ScaledInt(4), ScaledInt(4), ScaledInt(4), ScaledInt(4))
            };

            _headerStyle = new GUIStyle
            {
                fontSize = ScaledFont(13),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _woodDark },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };

            _itemStyle = new GUIStyle
            {
                fontSize = ScaledFont(11),
                normal = { textColor = _textDark },
                padding = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };

            _priorityStyle = new GUIStyle
            {
                fontSize = ScaledFont(11),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textDark }
            };

            _categoryStyle = new GUIStyle
            {
                fontSize = ScaledFont(8),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _textDark }
            };

            _titleStyle = new GUIStyle
            {
                fontSize = ScaledFont(11),
                normal = { textColor = _textDark },
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,
                wordWrap = true
            };

            _emptyStyle = new GUIStyle
            {
                fontSize = ScaledFont(11),
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(_textDark.r, _textDark.g, _textDark.b, 0.6f) },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(10), ScaledInt(10), ScaledInt(10), ScaledInt(10))
            };

            _closeButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(11),
                fontStyle = FontStyle.Bold,
                normal = { background = _closeBtnNormal, textColor = _textDark },
                hover = { background = _closeBtnHover, textColor = _woodDark },
                active = { background = _closeBtnActive, textColor = _woodDark },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2)),
                border = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };

            _hintStyle = new GUIStyle(_headerStyle)
            {
                fontSize = ScaledFont(9),
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(_textDark.r, _textDark.g, _textDark.b, 0.5f) }
            };

            _wrappedTitleStyle = new GUIStyle(_titleStyle)
            {
                wordWrap = true,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperLeft
            };

            _rowStyle = new GUIStyle(_itemStyle)
            {
                padding = new RectOffset(ScaledInt(6), ScaledInt(6), ScaledInt(2), ScaledInt(2))
            };
        }

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

        private Texture2D MakeGradientTex(int width, int height, Color topColor, Color bottomColor)
        {
            var tex = new Texture2D(width, height);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                Color rowColor = Color.Lerp(topColor, bottomColor, t);
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = rowColor;
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
                    {
                        pixels[y * width + x] = borderColor;
                    }
                    else
                    {
                        float noise = (float)rand.NextDouble() * 0.02f - 0.01f;
                        pixels[y * width + x] = new Color(
                            Mathf.Clamp01(rowBase.r + noise),
                            Mathf.Clamp01(rowBase.g + noise),
                            Mathf.Clamp01(rowBase.b + noise),
                            rowBase.a
                        );
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
            if (_manager != null)
            {
                _manager.OnTodosChanged -= RefreshCache;
                _manager.OnDataLoaded -= RefreshCache;
            }
        }
    }
}
