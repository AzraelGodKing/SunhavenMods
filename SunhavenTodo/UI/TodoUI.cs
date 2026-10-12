using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SunhavenMods.Shared;
using SunhavenTodo.Data;
using UnityEngine;
using Wish;

namespace SunhavenTodo.UI
{
    public class TodoUI : MonoBehaviour
    {
        // Window settings (base values, scaled by _scale)
        private const int WINDOW_ID = 98765;
        private const float BASE_WINDOW_WIDTH = 520f;
        private const float BASE_WINDOW_HEIGHT = 600f;
        private const float BASE_HEADER_HEIGHT = 50f;
        private const float BASE_ITEM_HEIGHT = 36f;
        private const float BASE_ICON_SIZE = 24f;

        private float _scale = 1f;
        private float WindowWidth => BASE_WINDOW_WIDTH * _scale;
        private float WindowHeight => BASE_WINDOW_HEIGHT * _scale;
        private float HeaderHeight => BASE_HEADER_HEIGHT * _scale;
        private float ItemHeight => BASE_ITEM_HEIGHT * _scale;
        private float IconSize => BASE_ICON_SIZE * _scale;
        private int ScaledFont(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * _scale));
        private float Scaled(float value) => value * _scale;
        private int ScaledInt(float value) => Mathf.RoundToInt(value * _scale);

        // Input blocking identifier
        private const string PAUSE_ID = "SunhavenTodo_UI";

        // Display state
        private bool _isVisible;
        private Rect _windowRect;
        private Vector2 _scrollPosition;
        private float _openAnimation = 0f;

        // Input state
        private string _newTodoTitle = "";
        private string _newTodoDescription = "";
        private int _selectedPriority = 1; // Normal
        private int _selectedCategory = 0; // General
        private bool _showAddForm = false;
        private string _editingItemId = null;
        private bool _newTodoIsRecurring = false;
        private int _newTodoRecurInterval = 0; // Daily

        // Filter state
        private int _selectedCategoryFilter = -1; // -1 = All
        private bool _showCompletedItems = true;
        private string _searchQuery = "";

        // Cached filtered+sorted list for DrawTodoList (avoids per-frame LINQ allocations)
        private readonly List<TodoItem> _cachedDrawList = new List<TodoItem>();
        private bool _cachedDrawListDirty = true;
        private int _cachedFilterCategory = -2;
        private bool _cachedFilterShowCompleted = true;
        private string _cachedFilterSearch = null;

        // Manager reference
        private TodoManager _manager;

        // Sun Haven themed colors
        private readonly Color _parchmentLight = new Color(0.96f, 0.93f, 0.86f, 0.98f);
        private readonly Color _parchment = new Color(0.92f, 0.87f, 0.78f, 0.97f);
        private readonly Color _parchmentDark = new Color(0.85f, 0.78f, 0.65f, 0.95f);
        private readonly Color _woodDark = new Color(0.35f, 0.25f, 0.15f);
        private readonly Color _woodMedium = new Color(0.50f, 0.38f, 0.25f);
        private readonly Color _woodLight = new Color(0.65f, 0.52f, 0.38f);
        private readonly Color _goldRich = new Color(0.85f, 0.68f, 0.20f);
        private readonly Color _goldBright = new Color(0.95f, 0.80f, 0.30f);
        private readonly Color _goldPale = new Color(0.98f, 0.95f, 0.85f);
        private readonly Color _forestGreen = new Color(0.30f, 0.55f, 0.30f);
        private readonly Color _successGreen = new Color(0.35f, 0.65f, 0.35f);
        private readonly Color _skyBlue = new Color(0.45f, 0.65f, 0.85f);
        private readonly Color _urgentRed = new Color(0.80f, 0.30f, 0.25f);
        private readonly Color _textDark = new Color(0.25f, 0.20f, 0.15f);
        private readonly Color _textLight = new Color(0.95f, 0.92f, 0.88f);
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
        private GUIStyle _titleStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _labelBoldStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _textAreaStyle;
        private GUIStyle _itemStyle;
        private GUIStyle _itemCompletedStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _tabActiveStyle;
        private GUIStyle _statsStyle;
        private GUIStyle _categoryLabelStyle;
        private GUIStyle _priorityLabelStyle;

        // Cached inline styles (to avoid GC allocations every frame)
        private GUIStyle _footerStyle;
        private GUIStyle _completedTitleStyle;
        private GUIStyle _wrappedActiveTitleStyle;
        private GUIStyle _wrappedCompletedTitleStyle;
        private GUIStyle _rowStyle;
        private Dictionary<TodoPriority, GUIStyle> _priorityStyleCache;
        private Dictionary<TodoCategory, GUIStyle> _categoryStyleCache;

        // Textures
        private Texture2D _windowBackground;
        private Texture2D _headerBackground;
        private Texture2D _buttonNormal;
        private Texture2D _buttonHover;
        private Texture2D _buttonActive;
        private Texture2D _itemEven;
        private Texture2D _itemOdd;
        private Texture2D _itemCompleted;
        private Texture2D _tabNormal;
        private Texture2D _tabActive;
        private Texture2D _textFieldBg;
        private Texture2D _goldLine;
        private Texture2D _progressBg;
        private Texture2D _progressFill;

        public void Initialize(TodoManager manager)
        {
            _manager = manager;
            float w = WindowWidth;
            float h = WindowHeight;
            _windowRect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        }

        public void Show()
        {
            _isVisible = true;
            _openAnimation = 0f;
            PauseGame(true);
        }

        public void Hide()
        {
            _isVisible = false;
            _showAddForm = false;
            _editingItemId = null;
            PauseGame(false);
        }

        private void PauseGame(bool pause)
        {
            try
            {
                // Block/unblock player movement
                if (Player.Instance != null)
                {
                    if (pause)
                        Player.Instance.AddPauseObject(PAUSE_ID);
                    else
                        Player.Instance.RemovePauseObject(PAUSE_ID);
                }

                // Disable/enable player input
                var playerInputType = Type.GetType("PlayerInput, Assembly-CSharp");
                if (playerInputType != null)
                {
                    var method = playerInputType.GetMethod(pause ? "DisableInput" : "EnableInput",
                        BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                    method?.Invoke(null, new object[] { PAUSE_ID });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SunhavenTodo] Input blocking failed: {ex.Message}");
            }
        }

        public void Toggle()
        {
            if (_isVisible)
                Hide();
            else
                Show();
        }

        public bool IsVisible => _isVisible;

        public void SetScale(float scale)
        {
            _scale = Mathf.Clamp(scale, 0.5f, 2.5f);
            _stylesInitialized = false;
            _windowRect = new Rect((Screen.width - WindowWidth) / 2f, (Screen.height - WindowHeight) / 2f, WindowWidth, WindowHeight);
        }

        private void Update()
        {
            if (_isVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_showAddForm)
                {
                    _showAddForm = false;
                    _editingItemId = null;
                }
                else
                {
                    Hide();
                }
            }

            if (_isVisible)
            {
                _openAnimation = Mathf.MoveTowards(_openAnimation, 1f, Time.unscaledDeltaTime * 8f);
            }
        }

        private void OnGUI()
        {
            if (!_isVisible || _manager == null) return;

            InitializeStyles();

            _windowRect.width = WindowWidth;
            _windowRect.height = WindowHeight;

            GUI.color = new Color(1, 1, 1, _openAnimation);
            GUI.depth = -1000;

            _windowRect = GUI.Window(WINDOW_ID, _windowRect, DrawWindow, "", _windowStyle);
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);

            GUI.color = Color.white;
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            DrawHeader();
            DrawGoldDivider();
            DrawStats();
            DrawFilterBar();

            if (_showAddForm)
            {
                DrawAddForm();
            }
            else
            {
                DrawCategoryTabs();
                DrawTodoList();
            }

            DrawFooter();

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, WindowWidth, HeaderHeight));
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label(ModLocalization.T("todo.header.title"), _titleStyle);

            GUILayout.FlexibleSpace();

            var hud = Plugin.GetTodoHUD();
            if (Plugin.StaticHUDEnabled && hud != null && !hud.IsEnabled)
            {
                var stickyContent = new GUIContent(
                    ModLocalization.T("todo.button.sticky"),
                    ModLocalization.T("todo.button.sticky.tooltip"));
                if (GUILayout.Button(stickyContent, _buttonStyle, GUILayout.Width(64), GUILayout.Height(28)))
                    Plugin.ShowHUD();

                GUILayout.Space(8);
            }

            // Add button
            var addContent = new GUIContent(_showAddForm ? ModLocalization.T("todo.button.cancel") : ModLocalization.T("todo.button.add"));
            if (GUILayout.Button(addContent, _buttonStyle, GUILayout.Width(70), GUILayout.Height(28)))
            {
                _showAddForm = !_showAddForm;
                if (!_showAddForm)
                {
                    ResetAddForm();
                }
            }

            GUILayout.Space(8);

            // Close button
            if (GUILayout.Button("X", _buttonStyle, GUILayout.Width(28), GUILayout.Height(28)))
            {
                Hide();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawGoldDivider()
        {
            GUILayout.Space(4);
            var rect = GUILayoutUtility.GetRect(WindowWidth - Scaled(40), Scaled(3));
            if (Event.current.type == UnityEngine.EventType.Repaint && _goldLine != null)
            {
                GUI.DrawTexture(rect, _goldLine);
            }
            GUILayout.Space(4);
        }

        private void DrawStats()
        {
            var (total, completed, active) = _manager.GetStats();
            var percent = _manager.GetCompletionPercent();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.Label(ModLocalization.T("todo.stats.active", active), _statsStyle);
            GUILayout.Space(20);
            GUILayout.Label(ModLocalization.T("todo.stats.completed", completed), _statsStyle);
            GUILayout.Space(20);
            GUILayout.Label(ModLocalization.T("todo.stats.total", total), _statsStyle);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Progress bar
            GUILayout.Space(4);
            var progressRect = GUILayoutUtility.GetRect(WindowWidth - Scaled(60), Scaled(12));
            progressRect.x += 10;
            progressRect.width -= 20;

            if (Event.current.type == UnityEngine.EventType.Repaint)
            {
                if (_progressBg != null)
                    GUI.DrawTexture(progressRect, _progressBg);

                if (_progressFill != null && percent > 0)
                {
                    var fillRect = new Rect(progressRect.x + 2, progressRect.y + 2,
                        (progressRect.width - 4) * (percent / 100f), progressRect.height - 4);
                    GUI.DrawTexture(fillRect, _progressFill);
                }
            }

            GUILayout.Space(8);
        }

        private void DrawFilterBar()
        {
            GUILayout.BeginHorizontal();

            // Search field
            GUILayout.Label(ModLocalization.T("todo.filter.search"), _labelStyle, GUILayout.Width(50));
            _searchQuery = GUILayout.TextField(_searchQuery, _textFieldStyle, GUILayout.Width(150), GUILayout.Height(24));

            GUILayout.Space(10);

            // Show completed toggle
            var showCompletedContent = new GUIContent(_showCompletedItems
                ? ModLocalization.T("todo.filter.showDone.on")
                : ModLocalization.T("todo.filter.showDone.off"));
            if (GUILayout.Button(showCompletedContent, _buttonStyle, GUILayout.Width(100), GUILayout.Height(24)))
            {
                _showCompletedItems = !_showCompletedItems;
            }

            GUILayout.FlexibleSpace();

            // Clear completed button
            if (_manager.GetCompletedTodos().Any())
            {
                if (GUILayout.Button(ModLocalization.T("todo.filter.clearDone"), _buttonStyle, GUILayout.Width(80), GUILayout.Height(24)))
                {
                    _manager.ClearCompleted();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private void DrawCategoryTabs()
        {
            GUILayout.BeginHorizontal();

            // "All" tab
            var allStyle = _selectedCategoryFilter == -1 ? _tabActiveStyle : _tabStyle;
            if (GUILayout.Button(ModLocalization.T("todo.tab.all"), allStyle, GUILayout.Height(26)))
            {
                _selectedCategoryFilter = -1;
            }

            // Category tabs with counts
            var categoryCounts = _manager.GetCountsByCategory();
            foreach (TodoCategory cat in Enum.GetValues(typeof(TodoCategory)))
            {
                var count = categoryCounts.TryGetValue(cat, out var c) ? c : 0;
                var style = (int)cat == _selectedCategoryFilter ? _tabActiveStyle : _tabStyle;
                var catName = ModLocalization.T($"todo.category.{cat}");
                var label = count > 0 ? $"{catName} ({count})" : catName;

                if (GUILayout.Button(label, style, GUILayout.Height(26)))
                {
                    _selectedCategoryFilter = (int)cat;
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private void DrawTodoList()
        {
            // Invalidate cache when filter state changes
            if (_cachedFilterCategory != _selectedCategoryFilter || _cachedFilterShowCompleted != _showCompletedItems || _cachedFilterSearch != _searchQuery)
                _cachedDrawListDirty = true;

            if (_cachedDrawListDirty)
            {
                _cachedDrawListDirty = false;
                _cachedFilterCategory = _selectedCategoryFilter;
                _cachedFilterShowCompleted = _showCompletedItems;
                _cachedFilterSearch = _searchQuery;
                _cachedDrawList.Clear();
                foreach (var t in GetFilteredTodos())
                    _cachedDrawList.Add(t);
                _cachedDrawList.Sort((a, b) =>
                {
                    if (a.IsCompleted != b.IsCompleted) return a.IsCompleted.CompareTo(b.IsCompleted);
                    int pri = ((int)b.Priority).CompareTo((int)a.Priority);
                    if (pri != 0) return pri;
                    return b.CreatedAt.CompareTo(a.CreatedAt);
                });
            }

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            if (_cachedDrawList.Count == 0)
            {
                GUILayout.Space(20);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(ModLocalization.T("todo.list.empty"), _labelStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(20);
            }
            else
            {
                for (int i = 0; i < _cachedDrawList.Count; i++)
                {
                    DrawTodoItem(_cachedDrawList[i], i);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawTodoItem(TodoItem item, int index)
        {
            var bgTex = item.IsCompleted ? _itemCompleted : (index % 2 == 0 ? _itemEven : _itemOdd);
            var titleText = string.IsNullOrWhiteSpace(item.Title) ? ModLocalization.T("todo.item.noTitle") : item.Title;
            if (item.IsRecurring)
                titleText = $"[{GetRecurIcon(item.RecurInterval)}] {titleText}";

            // Wrap long titles and grow row height dynamically instead of clipping.
            var wrappedTitleStyle = item.IsCompleted ? _wrappedCompletedTitleStyle : _wrappedActiveTitleStyle;

            float controlsWidth = Scaled(20 + 4 + 20 + 45 + 4 + 22 + 2 + 22 + 4); // checkbox, spaces, priority, category, edit, delete button
            if (item.IconItemId > 0)
                controlsWidth += IconSize + Scaled(4);

            float contentPadding = Scaled(32); // window + row paddings
            float titleAvailableWidth = Mathf.Max(Scaled(120), WindowWidth - controlsWidth - contentPadding);
            float titleHeight = Mathf.Ceil(wrappedTitleStyle.CalcHeight(new GUIContent(titleText), titleAvailableWidth));
            float rowHeight = Mathf.Max(ItemHeight, titleHeight + Scaled(10));

            // Use a row style with background so the row stays in layout flow (avoids BeginArea + ScrollView coordinate bug that made titles appear blank)
            _rowStyle.normal.background = bgTex;

            GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(rowHeight));
            // Checkbox
            var isCompleted = GUILayout.Toggle(item.IsCompleted, "", GUILayout.Width(20));
            if (isCompleted != item.IsCompleted)
            {
                _manager.ToggleComplete(item.Id);
                _cachedDrawListDirty = true;
            }

            GUILayout.Space(4);

            // Priority indicator (using cached style)
            var priorityStyle = _priorityStyleCache.TryGetValue(item.Priority, out var ps) ? ps : _priorityLabelStyle;
            GUILayout.Label(GetPriorityIcon(item.Priority), priorityStyle, GUILayout.Width(20));

            // Category indicator (using cached style)
            var categoryStyle = _categoryStyleCache.TryGetValue(item.Category, out var cs) ? cs : _categoryLabelStyle;
            GUILayout.Label($"[{GetCategoryShort(item.Category)}]", categoryStyle, GUILayout.Width(45));

            GUILayout.Space(4);

            // Item icon (used by museum integration tasks)
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

                GUILayout.Space(4);
            }

            // Wrapped title with dynamic row height.
            GUILayout.Label(titleText, wrappedTitleStyle, GUILayout.Width(titleAvailableWidth), GUILayout.MinHeight(titleHeight));
            GUILayout.FlexibleSpace();

            // Edit button
            if (GUILayout.Button("✎", _buttonStyle, GUILayout.Width(22), GUILayout.Height(22)))
            {
                _editingItemId = item.Id;
                _newTodoTitle = item.Title ?? "";
                _newTodoDescription = item.Description ?? "";
                _selectedPriority = (int)item.Priority;
                _selectedCategory = (int)item.Category;
                _newTodoIsRecurring = item.IsRecurring;
                _newTodoRecurInterval = (int)item.RecurInterval;
                _showAddForm = true;
            }

            GUILayout.Space(2);

            // Delete button
            if (GUILayout.Button("x", _buttonStyle, GUILayout.Width(22), GUILayout.Height(22)))
            {
                _manager.RemoveTodo(item.Id);
                _cachedDrawListDirty = true;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawAddForm()
        {
            GUILayout.Space(10);

            // Form background
            GUILayout.BeginVertical(_windowStyle);

            GUILayout.Label(_editingItemId != null ? ModLocalization.T("todo.form.editTitle") : ModLocalization.T("todo.form.addTitle"), _headerStyle);
            GUILayout.Space(8);

            // Title
            GUILayout.Label(ModLocalization.T("todo.form.titleLabel"), _labelStyle);
            _newTodoTitle = GUILayout.TextField(_newTodoTitle, _textFieldStyle, GUILayout.Height(26));

            GUILayout.Space(6);

            // Description
            GUILayout.Label(ModLocalization.T("todo.form.descLabel"), _labelStyle);
            _newTodoDescription = GUILayout.TextArea(_newTodoDescription, _textAreaStyle, GUILayout.Height(50));

            GUILayout.Space(6);

            // Priority
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("todo.form.priorityLabel"), _labelStyle, GUILayout.Width(60));
            var priorities = Enum.GetNames(typeof(TodoPriority));
            for (int i = 0; i < priorities.Length; i++)
            {
                var style = i == _selectedPriority ? _tabActiveStyle : _tabStyle;
                if (GUILayout.Button(ModLocalization.T($"todo.priority.{priorities[i]}"), style, GUILayout.Height(24)))
                {
                    _selectedPriority = i;
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Category (one style per button so the selected category is clearly highlighted)
            GUILayout.Label(ModLocalization.T("todo.form.categoryLabel"), _labelStyle);
            var categories = Enum.GetNames(typeof(TodoCategory));
            const int categoryCols = 5;
            for (int rowStart = 0; rowStart < categories.Length; rowStart += categoryCols)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < categoryCols && rowStart + col < categories.Length; col++)
                {
                    int i = rowStart + col;
                    var style = i == _selectedCategory ? _tabActiveStyle : _tabStyle;
                    if (GUILayout.Button(ModLocalization.T($"todo.category.{categories[i]}"), style, GUILayout.Height(24)))
                        _selectedCategory = i;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);

            // Recurring toggle
            GUILayout.BeginHorizontal();
            _newTodoIsRecurring = GUILayout.Toggle(_newTodoIsRecurring, ModLocalization.T("todo.form.recurring"), _labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (_newTodoIsRecurring)
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label(ModLocalization.T("todo.form.resets"), _labelStyle, GUILayout.Width(50));
                var intervals = Enum.GetNames(typeof(Data.RecurInterval));
                for (int i = 0; i < intervals.Length; i++)
                {
                    var style = i == _newTodoRecurInterval ? _tabActiveStyle : _tabStyle;
                    if (GUILayout.Button(ModLocalization.T($"todo.recur.{intervals[i]}"), style, GUILayout.Height(24)))
                        _newTodoRecurInterval = i;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // Buttons
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(ModLocalization.T("todo.button.cancel"), _buttonStyle, GUILayout.Width(80), GUILayout.Height(28)))
            {
                _showAddForm = false;
                ResetAddForm();
            }

            GUILayout.Space(10);

            var canSave = !string.IsNullOrWhiteSpace(_newTodoTitle);
            GUI.enabled = canSave;
            if (GUILayout.Button(_editingItemId != null ? ModLocalization.T("todo.form.update") : ModLocalization.T("todo.form.addTask"), _buttonStyle, GUILayout.Width(100), GUILayout.Height(28)))
            {
                SaveNewTodo();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            GUILayout.Space(4);
            DrawGoldDivider();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Using cached footer style
            GUILayout.Label(ModLocalization.T("todo.footer.hint"), _footerStyle);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private IEnumerable<TodoItem> GetFilteredTodos()
        {
            var todos = _manager.GetAllTodos().ToList();

            // Filter by completion
            if (!_showCompletedItems)
            {
                todos = todos.Where(t => !t.IsCompleted).ToList();
            }

            // Filter by category
            if (_selectedCategoryFilter >= 0)
            {
                var category = (TodoCategory)_selectedCategoryFilter;
                todos = todos.Where(t => t.Category == category).ToList();
            }

            // Filter by search
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                string lowerQuery = _searchQuery.ToLowerInvariant();
                todos = todos.Where(t =>
                    t.Title.ToLowerInvariant().Contains(lowerQuery) ||
                    (!string.IsNullOrEmpty(t.Description) && t.Description.ToLowerInvariant().Contains(lowerQuery)))
                    .ToList();
                if (!_showCompletedItems)
                {
                    todos = todos.Where(t => !t.IsCompleted).ToList();
                }
                if (_selectedCategoryFilter >= 0)
                {
                    var category = (TodoCategory)_selectedCategoryFilter;
                    todos = todos.Where(t => t.Category == category).ToList();
                }
            }

            return todos;
        }

        private void SaveNewTodo()
        {
            if (string.IsNullOrWhiteSpace(_newTodoTitle)) return;

            var category = (TodoCategory)_selectedCategory;
            var item = new TodoItem(
                _newTodoTitle.Trim(),
                _newTodoDescription?.Trim() ?? "",
                (TodoPriority)_selectedPriority,
                category
            )
            {
                IsRecurring = _newTodoIsRecurring,
                RecurInterval = (Data.RecurInterval)_newTodoRecurInterval
            };

            if (_editingItemId != null)
            {
                item.Id = _editingItemId;
                var existing = _manager.GetTodoById(_editingItemId);
                if (existing != null)
                {
                    item.CreatedAt = existing.CreatedAt;
                    item.IsCompleted = existing.IsCompleted;
                    item.CompletedAt = existing.CompletedAt;
                    item.IconItemId = existing.IconItemId;
                    item.MuseumDestination = existing.MuseumDestination;
                }
                _manager.UpdateTodo(item);
            }
            else
            {
                _manager.AddTodo(item);
                // Switch filter to the category they just added so the new task is visible and the tab is highlighted
                _selectedCategoryFilter = (int)category;
            }
            _cachedDrawListDirty = true;
            _showAddForm = false;
            ResetAddForm();
        }

        private void ResetAddForm()
        {
            _newTodoTitle = "";
            _newTodoDescription = "";
            _selectedPriority = 1;
            _selectedCategory = 0;
            _editingItemId = null;
            _newTodoIsRecurring = false;
            _newTodoRecurInterval = 0;
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

        private string GetRecurIcon(Data.RecurInterval interval)
        {
            switch (interval)
            {
                case Data.RecurInterval.Daily: return "D";
                case Data.RecurInterval.Weekly: return "W";
                case Data.RecurInterval.Seasonal: return "S";
                default: return "R";
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
            DestroyTextures();
            _windowBackground = MakeParchmentTexture(32, 128, _parchment, _parchmentLight, _borderDark, 4);
            _headerBackground = MakeGradientTex(8, 32, _parchmentDark, _parchment);
            _buttonNormal = MakeRoundedRect(8, 8, _parchmentDark, _borderDark, 2);
            _buttonHover = MakeRoundedRect(8, 8, _woodLight, _borderDark, 2);
            _buttonActive = MakeRoundedRect(8, 8, _goldPale, _goldRich, 2);
            _itemEven = MakeTex(1, 1, new Color(_parchmentLight.r, _parchmentLight.g, _parchmentLight.b, 0.4f));
            _itemOdd = MakeTex(1, 1, new Color(_parchment.r, _parchment.g, _parchment.b, 0.4f));
            _itemCompleted = MakeTex(1, 1, new Color(_successGreen.r, _successGreen.g, _successGreen.b, 0.15f));
            _tabNormal = MakeRoundedRect(8, 8, _parchmentDark, _borderDark, 1);
            _tabActive = MakeRoundedRect(8, 8, _goldPale, _goldRich, 2);
            _textFieldBg = MakeTex(1, 1, new Color(1f, 1f, 1f, 0.9f));
            _goldLine = MakeGradientTex(64, 3, _goldBright, _goldRich);
            _progressBg = MakeTex(1, 1, new Color(_woodDark.r, _woodDark.g, _woodDark.b, 0.4f));
            _progressFill = MakeGradientTex(8, 8, _goldBright, _goldRich);
        }

        private void DestroyTextures()
        {
            if (_windowBackground != null) Destroy(_windowBackground);
            if (_headerBackground != null) Destroy(_headerBackground);
            if (_buttonNormal != null) Destroy(_buttonNormal);
            if (_buttonHover != null) Destroy(_buttonHover);
            if (_buttonActive != null) Destroy(_buttonActive);
            if (_itemEven != null) Destroy(_itemEven);
            if (_itemOdd != null) Destroy(_itemOdd);
            if (_itemCompleted != null) Destroy(_itemCompleted);
            if (_tabNormal != null) Destroy(_tabNormal);
            if (_tabActive != null) Destroy(_tabActive);
            if (_textFieldBg != null) Destroy(_textFieldBg);
            if (_goldLine != null) Destroy(_goldLine);
            if (_progressBg != null) Destroy(_progressBg);
            if (_progressFill != null) Destroy(_progressFill);
        }

        private void CreateStyles()
        {
            _windowStyle = new GUIStyle
            {
                normal = { background = _windowBackground, textColor = _textDark },
                padding = new RectOffset(ScaledInt(15), ScaledInt(15), ScaledInt(15), ScaledInt(15)),
                border = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(8), ScaledInt(8))
            };

            _titleStyle = new GUIStyle
            {
                fontSize = ScaledFont(22),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _woodDark },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(ScaledInt(5), ScaledInt(5), ScaledInt(5), ScaledInt(5))
            };

            _headerStyle = new GUIStyle
            {
                fontSize = ScaledFont(16),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _woodDark },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(5), ScaledInt(5), ScaledInt(5), ScaledInt(5))
            };

            _labelStyle = new GUIStyle
            {
                fontSize = ScaledFont(12),
                normal = { textColor = _textDark },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };

            _labelBoldStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold
            };

            _statsStyle = new GUIStyle(_labelStyle)
            {
                fontSize = ScaledFont(11),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _woodMedium }
            };

            _buttonStyle = new GUIStyle
            {
                fontSize = ScaledFont(11),
                fontStyle = FontStyle.Bold,
                normal = { background = _buttonNormal, textColor = _textDark },
                hover = { background = _buttonHover, textColor = _textDark },
                active = { background = _buttonActive, textColor = _woodDark },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(4), ScaledInt(4)),
                border = new RectOffset(ScaledInt(4), ScaledInt(4), ScaledInt(4), ScaledInt(4))
            };

            _textFieldStyle = new GUIStyle
            {
                fontSize = ScaledFont(12),
                normal = { background = _textFieldBg, textColor = _textDark },
                focused = { background = _textFieldBg, textColor = _textDark },
                padding = new RectOffset(ScaledInt(6), ScaledInt(6), ScaledInt(4), ScaledInt(4)),
                border = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };

            _textAreaStyle = new GUIStyle(_textFieldStyle)
            {
                wordWrap = true
            };

            _itemStyle = new GUIStyle
            {
                fontSize = ScaledFont(12),
                normal = { textColor = _textDark },
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(4), ScaledInt(4))
            };

            _itemCompletedStyle = new GUIStyle(_itemStyle)
            {
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
            };

            _tabStyle = new GUIStyle
            {
                fontSize = ScaledFont(10),
                normal = { background = _tabNormal, textColor = _textDark },
                hover = { background = _buttonHover, textColor = _textDark },
                active = { background = _tabActive, textColor = _woodDark },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(6), ScaledInt(6), ScaledInt(3), ScaledInt(3)),
                margin = new RectOffset(ScaledInt(2), ScaledInt(2), 0, 0),
                border = new RectOffset(ScaledInt(4), ScaledInt(4), ScaledInt(4), ScaledInt(4))
            };

            _tabActiveStyle = new GUIStyle(_tabStyle)
            {
                normal = { background = _tabActive, textColor = _woodDark },
                fontStyle = FontStyle.Bold
            };

            _categoryLabelStyle = new GUIStyle(_labelStyle)
            {
                fontSize = ScaledFont(9),
                fontStyle = FontStyle.Bold
            };

            _priorityLabelStyle = new GUIStyle(_labelStyle)
            {
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // Cached footer style
            _footerStyle = new GUIStyle(_labelStyle)
            {
                fontSize = ScaledFont(10),
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(_textDark.r, _textDark.g, _textDark.b, 0.6f) }
            };

            // Cached completed title style
            _completedTitleStyle = new GUIStyle(_labelBoldStyle)
            {
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                fontStyle = FontStyle.Italic
            };

            _wrappedActiveTitleStyle = new GUIStyle(_labelBoldStyle)
            {
                wordWrap = true,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperLeft
            };

            _wrappedCompletedTitleStyle = new GUIStyle(_completedTitleStyle)
            {
                wordWrap = true,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperLeft
            };

            _rowStyle = new GUIStyle(_itemStyle)
            {
                padding = new RectOffset(8, 8, 4, 4)
            };

            // Cache priority styles
            _priorityStyleCache = new Dictionary<TodoPriority, GUIStyle>();
            foreach (TodoPriority priority in Enum.GetValues(typeof(TodoPriority)))
            {
                var priorityColor = _priorityColors.TryGetValue(priority, out var pc) ? pc : _textDark;
                _priorityStyleCache[priority] = new GUIStyle(_priorityLabelStyle)
                {
                    normal = { textColor = priorityColor }
                };
            }

            // Cache category styles
            _categoryStyleCache = new Dictionary<TodoCategory, GUIStyle>();
            foreach (TodoCategory category in Enum.GetValues(typeof(TodoCategory)))
            {
                var categoryColor = _categoryColors.TryGetValue(category, out var cc) ? cc : _textDark;
                _categoryStyleCache[category] = new GUIStyle(_categoryLabelStyle)
                {
                    normal = { textColor = categoryColor }
                };
            }
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
                    {
                        pixels[y * width + x] = borderColor;
                    }
                    else
                    {
                        float noise = (float)rand.NextDouble() * 0.03f - 0.015f;
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
    }
}
