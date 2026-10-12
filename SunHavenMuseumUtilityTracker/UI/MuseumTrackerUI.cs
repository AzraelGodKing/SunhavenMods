using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SunhavenMods.Shared;
using SunHavenMuseumUtilityTracker.Data;
using SunHavenMuseumUtilityTracker.Patches;
using UnityEngine;
using Wish;

namespace SunHavenMuseumUtilityTracker.UI
{
    /// <summary>
    /// Main UI window for the Museum Utility Tracker - Sun Haven warm parchment theme.
    /// </summary>
    public class MuseumTrackerUI : MonoBehaviour
    {
        // Base window dimensions (scaled by _scale)
        private const float BASE_WINDOW_WIDTH = 640f;
        private const float BASE_WINDOW_HEIGHT = 700f;
        private const float BASE_ICON_SIZE = 34f;
        private const float BASE_HEADER_HEIGHT = 80f;

        // UI scale (from config, 0.5-2.5)
        private float _scale = 1f;

        // State
        private DonationManager _donationManager;
        private bool _isVisible;
        private Rect _windowRect;
        private Vector2 _scrollPosition;
        private int _windowId;

        // Hotkey
        private KeyCode _toggleKey = KeyCode.M;
        private bool _requireCtrl = true;

        // UI state
        private int _selectedSectionIndex = 0;
        private HashSet<string> _expandedBundles = new HashSet<string>();
        private bool _showOnlyNeeded = false;
        private string _searchQuery = "";

        // Animation
        private float _openAnimation = 0f;

        // Sync status
        private string _syncStatusMessage = "";
        private float _syncStatusTimer = 0f;

        // Game progress cache (to avoid expensive reflection calls every frame)
        private Dictionary<string, int> _cachedGameDonationCounts = new Dictionary<string, int>();
        private Dictionary<string, bool> _cachedGameCompleteStatus = new Dictionary<string, bool>();
        private Coroutine _cacheRefreshCoroutine;

        // Styles
        private bool _stylesInitialized;
        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _sectionTabStyle;
        private GUIStyle _sectionTabActiveStyle;
        private GUIStyle _bundleHeaderStyle;
        private GUIStyle _bundleHeaderCompleteStyle;
        private GUIStyle _itemRowStyle;
        private GUIStyle _itemNameStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _checkmarkStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _searchStyle;
        private GUIStyle _statsStyle;
        private GUIStyle _footerStyle;
        private GUIStyle _syncButtonStyle;
        private GUIStyle _syncStatusStyle;

        // Cached inline styles (to avoid GC allocations every frame)
        private GUIStyle _headerBoxStyle;
        private GUIStyle _headerCountStyle;
        private GUIStyle _searchLabelStyle;
        private GUIStyle _scrollStyle;
        private GUIStyle _emptyStyle;
        private GUIStyle _progressLabelStyle;
        private GUIStyle _itemNameDonatedStyle;
        private GUIStyle _rarityLabelStyleCached;
        private GUIStyle _checkStyleCached;
        private GUIStyle _neededStyleCached;

        // Textures
        private Texture2D _windowBackground;
        private Texture2D _headerBackground;
        private Texture2D _tabNormal;
        private Texture2D _tabHover;
        private Texture2D _tabActive;
        private Texture2D _bundleNormal;
        private Texture2D _bundleHover;
        private Texture2D _bundleComplete;
        private Texture2D _itemEven;
        private Texture2D _itemOdd;
        private Texture2D _itemDonated;
        private Texture2D _buttonNormal;
        private Texture2D _buttonHover;
        private Texture2D _progressBg;
        private Texture2D _searchBg;
        private Texture2D _dividerTex;
        private Texture2D _itemPlaceholderIcon;

        // Sun Haven warm parchment color palette
        private readonly Color _parchmentLight = new Color(0.96f, 0.93f, 0.86f, 0.98f);
        private readonly Color _parchment = new Color(0.92f, 0.87f, 0.78f, 0.97f);
        private readonly Color _parchmentDark = new Color(0.85f, 0.78f, 0.65f, 0.95f);
        private readonly Color _parchmentDarker = new Color(0.75f, 0.67f, 0.52f, 0.92f);

        // Warm wood/leather tones
        private readonly Color _woodDark = new Color(0.35f, 0.25f, 0.15f);
        private readonly Color _woodMedium = new Color(0.50f, 0.38f, 0.25f);
        private readonly Color _woodLight = new Color(0.65f, 0.52f, 0.38f);
        private readonly Color _leather = new Color(0.55f, 0.40f, 0.28f);

        // Accent colors - Sun Haven fantasy
        private readonly Color _goldRich = new Color(0.85f, 0.68f, 0.20f);
        private readonly Color _goldBright = new Color(0.95f, 0.80f, 0.30f);
        private readonly Color _goldPale = new Color(1.0f, 0.92f, 0.70f);
        private readonly Color _forestGreen = new Color(0.30f, 0.55f, 0.30f);
        private readonly Color _skyBlue = new Color(0.45f, 0.65f, 0.85f);
        private readonly Color _coralWarm = new Color(0.85f, 0.50f, 0.40f);

        // Text colors (stronger contrast for readability)
        private readonly Color _textDark = new Color(0.18f, 0.14f, 0.10f);
        private readonly Color _textMedium = new Color(0.32f, 0.28f, 0.22f);
        private readonly Color _textLight = new Color(0.48f, 0.42f, 0.34f);
        private readonly Color _textMuted = new Color(0.58f, 0.52f, 0.44f);

        // Status colors
        private readonly Color _successGreen = new Color(0.35f, 0.65f, 0.35f);
        private readonly Color _successGreenLight = new Color(0.50f, 0.75f, 0.45f);
        private readonly Color _neededOrange = new Color(0.85f, 0.55f, 0.25f);

        // Border/trim colors
        private readonly Color _borderDark = new Color(0.45f, 0.35f, 0.22f, 0.8f);
        private readonly Color _borderGold = new Color(0.75f, 0.60f, 0.25f, 0.7f);

        // Rarity colors - rich fantasy tones
        private readonly Dictionary<Data.ItemRarity, Color> _rarityColors = new Dictionary<Data.ItemRarity, Color>
        {
            { Data.ItemRarity.Common, new Color(0.50f, 0.45f, 0.38f) },
            { Data.ItemRarity.Uncommon, new Color(0.35f, 0.60f, 0.35f) },
            { Data.ItemRarity.Rare, new Color(0.35f, 0.55f, 0.80f) },
            { Data.ItemRarity.Epic, new Color(0.65f, 0.40f, 0.75f) },
            { Data.ItemRarity.Legendary, new Color(0.90f, 0.70f, 0.20f) }
        };

        // Section theme colors - warm variations
        private readonly Dictionary<string, (Color primary, Color accent)> _sectionThemes =
            new Dictionary<string, (Color, Color)>
        {
            { "hall_of_gems", (new Color(0.60f, 0.50f, 0.75f), new Color(0.75f, 0.65f, 0.90f)) },
            { "hall_of_culture", (new Color(0.70f, 0.55f, 0.35f), new Color(0.85f, 0.70f, 0.45f)) },
            { "aquarium", (new Color(0.40f, 0.60f, 0.75f), new Color(0.55f, 0.75f, 0.90f)) }
        };

        public bool IsVisible => _isVisible;

        private float WindowWidth => BASE_WINDOW_WIDTH * _scale;
        private float WindowHeight => BASE_WINDOW_HEIGHT * _scale;
        private float HeaderHeight => BASE_HEADER_HEIGHT * _scale;
        private float IconSize => BASE_ICON_SIZE * _scale;

        private int ScaledFont(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * _scale));
        private float Scaled(float value) => value * _scale;
        private int ScaledInt(float value) => Mathf.RoundToInt(value * _scale);

        public void SetScale(float scale)
        {
            _scale = Mathf.Clamp(scale, 0.5f, 2.5f);
            _stylesInitialized = false;
            float w = WindowWidth;
            float h = WindowHeight;
            _windowRect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        }

        public void Initialize(DonationManager donationManager)
        {
            _donationManager = donationManager;
            _isVisible = false;
            _windowId = GetHashCode();

            float w = WindowWidth;
            float h = WindowHeight;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;
            _windowRect = new Rect(x, y, w, h);

            SunhavenMods.Shared.IconCache.Initialize(Plugin.Log);
            Plugin.Log?.LogInfo("MuseumTrackerUI initialized");
        }

        public void SetToggleKey(KeyCode key, bool requireCtrl)
        {
            _toggleKey = key;
            _requireCtrl = requireCtrl;
        }

        public void Toggle()
        {
            if (!PlayerPatches.IsDataLoaded)
            {
                Plugin.Log?.LogWarning("Cannot toggle UI: data not loaded");
                return;
            }

            if (_isVisible) Hide();
            else Show();
        }

        public void Show()
        {
            _isVisible = true;
            _openAnimation = 0f;

            // Refresh game progress cache in background (spread over multiple frames)
            StartCacheRefresh();

            if (Player.Instance != null)
                Player.Instance.AddPauseObject("MuseumTracker_UI");

            Plugin.Log?.LogInfo("Museum Tracker UI opened");
        }

        /// <summary>
        /// Start refreshing the game progress cache in the background.
        /// </summary>
        private void StartCacheRefresh()
        {
            if (_cacheRefreshCoroutine != null)
            {
                StopCoroutine(_cacheRefreshCoroutine);
            }
            _cacheRefreshCoroutine = StartCoroutine(RefreshGameProgressCacheCoroutine());
        }

        /// <summary>
        /// Refresh the cached game progress data over multiple frames to avoid lag spikes.
        /// </summary>
        private IEnumerator RefreshGameProgressCacheCoroutine()
        {
            _cachedGameDonationCounts.Clear();
            _cachedGameCompleteStatus.Clear();

            var bundleIds = MuseumContent.GetAllBundleIds();
            int processedCount = 0;
            const int batchSize = 3; // Process 3 bundles per frame

            foreach (var bundleId in bundleIds)
            {
                _cachedGameDonationCounts[bundleId] = MuseumPatches.GetBundleDonationCount(bundleId);
                _cachedGameCompleteStatus[bundleId] = MuseumPatches.IsBundleCompleteInGame(bundleId);

                processedCount++;
                if (processedCount % batchSize == 0)
                {
                    yield return null; // Wait one frame before processing more
                }
            }

            _cacheRefreshCoroutine = null;
        }

        /// <summary>
        /// Refresh cache synchronously (used after sync button press).
        /// </summary>
        private void RefreshGameProgressCacheImmediate()
        {
            if (_cacheRefreshCoroutine != null)
            {
                StopCoroutine(_cacheRefreshCoroutine);
                _cacheRefreshCoroutine = null;
            }

            _cachedGameDonationCounts.Clear();
            _cachedGameCompleteStatus.Clear();

            var bundleIds = MuseumContent.GetAllBundleIds();
            foreach (var bundleId in bundleIds)
            {
                _cachedGameDonationCounts[bundleId] = MuseumPatches.GetBundleDonationCount(bundleId);
                _cachedGameCompleteStatus[bundleId] = MuseumPatches.IsBundleCompleteInGame(bundleId);
            }
        }

        public void Hide()
        {
            _isVisible = false;

            if (Player.Instance != null)
                Player.Instance.RemovePauseObject("MuseumTracker_UI");

            Plugin.Log?.LogInfo("Museum Tracker UI closed");
        }

        private void Update()
        {
            if (_isVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }

            if (_isVisible)
            {
                _openAnimation = Mathf.MoveTowards(_openAnimation, 1f, Time.unscaledDeltaTime * 6f);

                // Decay sync status message
                if (_syncStatusTimer > 0)
                {
                    _syncStatusTimer -= Time.unscaledDeltaTime;
                    if (_syncStatusTimer <= 0)
                    {
                        _syncStatusMessage = "";
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (!_isVisible || _donationManager == null || !PlayerPatches.IsDataLoaded)
                return;

            InitializeStyles();

            float alpha = _openAnimation;
            GUI.color = new Color(1, 1, 1, alpha);

            _windowRect = GUI.Window(_windowId, _windowRect, DrawWindow, "", _windowStyle);

            GUI.color = Color.white;
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            CreateTextures();
            CreateStyles();

            _stylesInitialized = true;
        }

        private void CreateTextures()
        {
            // Main window - warm parchment with wood border
            _windowBackground = MakeParchmentTexture(32, 128, _parchment, _parchmentLight, _borderDark, 4);

            // Header - darker parchment with gold trim feel
            _headerBackground = MakeGradientTex(8, 64, _parchmentDark, _parchment);

            // Tab textures
            _tabNormal = MakeRoundedRect(8, 8, _parchmentDark, _borderDark, 2);
            _tabHover = MakeRoundedRect(8, 8, _parchmentDarker, _woodMedium, 2);
            _tabActive = MakeRoundedRect(8, 8, _goldPale, _goldRich, 3);

            // Bundle textures
            _bundleNormal = MakeRoundedRect(6, 6, _parchmentDark, _borderDark, 2);
            _bundleHover = MakeRoundedRect(6, 6, _parchmentDarker, _woodMedium, 2);
            _bundleComplete = MakeRoundedRect(6, 6,
                new Color(_successGreenLight.r, _successGreenLight.g, _successGreenLight.b, 0.3f),
                _successGreen, 2);

            // Item row backgrounds (clearer alternating + donated state)
            _itemEven = MakeTex(1, 1, new Color(_parchmentLight.r, _parchmentLight.g, _parchmentLight.b, 0.72f));
            _itemOdd = MakeTex(1, 1, new Color(_parchment.r, _parchment.g, _parchment.b, 0.68f));
            _itemDonated = MakeTex(1, 1, new Color(_successGreenLight.r, _successGreenLight.g, _successGreenLight.b, 0.35f));

            // Buttons - warm wood style
            _buttonNormal = MakeRoundedRect(6, 6, _woodMedium, _woodDark, 2);
            _buttonHover = MakeRoundedRect(6, 6, _woodLight, _woodMedium, 2);

            // Progress bar
            _progressBg = MakeTex(1, 1, new Color(_woodDark.r, _woodDark.g, _woodDark.b, 0.4f));

            // Search box
            _searchBg = MakeRoundedRect(6, 6, _parchmentLight, _borderDark, 1);

            // Divider
            _dividerTex = MakeTex(1, 1, _borderDark);

            // Culture / undecorated aquarium slots have no game item ID — show a clear list tile
            _itemPlaceholderIcon = MakeRoundedRect(32, 32, new Color(0.88f, 0.82f, 0.72f, 1f), _borderDark, 2);
        }

        private void CreateStyles()
        {
            int border = ScaledInt(16);
            // Window style
            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = _windowBackground, textColor = _textDark },
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(border, border, border, border)
            };

            // Title style - bold and readable
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(26),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _woodDark },
                padding = new RectOffset(0, 0, 0, 0)
            };

            // Subtitle style
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(13),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _textMedium }
            };

            // Section tab styles (readable labels)
            _sectionTabStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _tabNormal, textColor = _textDark },
                hover = { background = _tabHover, textColor = _woodDark },
                active = { background = _tabHover, textColor = _woodDark },
                fontSize = ScaledFont(13),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(14), ScaledInt(10), ScaledInt(12), ScaledInt(10)),
                margin = new RectOffset(ScaledInt(4), ScaledInt(4), 0, 0)
            };

            _sectionTabActiveStyle = new GUIStyle(_sectionTabStyle)
            {
                normal = { background = _tabActive, textColor = _woodDark },
                hover = { background = _tabActive, textColor = _woodDark }
            };

            // Bundle header style (more padding, clearer text)
            _bundleHeaderStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _bundleNormal, textColor = _textDark },
                hover = { background = _bundleHover, textColor = _woodDark },
                active = { background = _bundleHover, textColor = _woodDark },
                fontSize = ScaledFont(14),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(ScaledInt(18), ScaledInt(14), ScaledInt(14), ScaledInt(14)),
                wordWrap = true
            };

            _bundleHeaderCompleteStyle = new GUIStyle(_bundleHeaderStyle)
            {
                normal = { background = _bundleComplete, textColor = _successGreen },
                hover = { background = _bundleComplete, textColor = _forestGreen }
            };

            // Item styles (more padding for easier scanning)
            _itemRowStyle = new GUIStyle
            {
                padding = new RectOffset(ScaledInt(16), ScaledInt(10), ScaledInt(8), ScaledInt(8)),
                margin = new RectOffset(0, 0, 2, 1)
            };

            _itemNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(14),
                normal = { textColor = _textDark },
                wordWrap = true
            };

            // Button style
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _buttonNormal, textColor = _parchmentLight },
                hover = { background = _buttonHover, textColor = Color.white },
                active = { background = _buttonHover, textColor = Color.white },
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(ScaledInt(14), ScaledInt(14), ScaledInt(8), ScaledInt(8))
            };

            // Close button style
            _closeButtonStyle = new GUIStyle(_buttonStyle)
            {
                fontSize = ScaledFont(18),
                fontStyle = FontStyle.Bold,
                normal = { background = MakeRoundedRect(6, 6, _coralWarm, new Color(0.7f, 0.35f, 0.25f), 2), textColor = Color.white },
                hover = { background = MakeRoundedRect(6, 6, new Color(0.95f, 0.55f, 0.45f), _coralWarm, 2), textColor = Color.white }
            };

            // Label style
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(12),
                normal = { textColor = _textDark }
            };

            // Checkmark style
            _checkmarkStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(16),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _successGreen },
                alignment = TextAnchor.MiddleCenter
            };

            // Toggle style
            _toggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = ScaledFont(12),
                normal = { textColor = _textMedium },
                hover = { textColor = _textDark }
            };

            // Search style (clearer box, comfortable padding)
            _searchStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = ScaledFont(14),
                normal = { background = _searchBg, textColor = _textDark },
                focused = { background = _searchBg, textColor = _textDark },
                padding = new RectOffset(ScaledInt(14), ScaledInt(10), ScaledInt(10), ScaledInt(10))
            };

            // Stats style (percentage in header)
            _statsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(22),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _woodDark }
            };

            // Footer style (readable hint text)
            _footerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textLight }
            };

            // Sync button style - teal/green to indicate sync action
            _syncButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = MakeRoundedRect(6, 6, new Color(0.30f, 0.55f, 0.50f), new Color(0.20f, 0.40f, 0.35f), 2), textColor = _parchmentLight },
                hover = { background = MakeRoundedRect(6, 6, new Color(0.40f, 0.65f, 0.60f), new Color(0.30f, 0.50f, 0.45f), 2), textColor = Color.white },
                active = { background = MakeRoundedRect(6, 6, new Color(0.35f, 0.60f, 0.55f), new Color(0.25f, 0.45f, 0.40f), 2), textColor = Color.white },
                fontSize = ScaledFont(11),
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(ScaledInt(10), ScaledInt(10), ScaledInt(6), ScaledInt(6))
            };

            // Sync status message style
            _syncStatusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(11),
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = _successGreen }
            };

            // Cached inline styles (created once, not every frame)
            _headerBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeRoundedRect(6, 6, new Color(_goldPale.r, _goldPale.g, _goldPale.b, 0.65f), _goldRich, 2) },
                padding = new RectOffset(ScaledInt(18), ScaledInt(12), ScaledInt(10), ScaledInt(10))
            };

            _headerCountStyle = new GUIStyle(_labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _textDark }
            };

            _searchLabelStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold
            };

            _scrollStyle = new GUIStyle(GUI.skin.scrollView);

            _emptyStyle = new GUIStyle(_labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = ScaledFont(15),
                fontStyle = FontStyle.Normal,
                normal = { textColor = _textMedium }
            };

            _progressLabelStyle = new GUIStyle(_labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = ScaledFont(13),
                fontStyle = FontStyle.Bold
            };

            _itemNameDonatedStyle = new GUIStyle(_itemNameStyle)
            {
                fontSize = ScaledFont(14),
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.22f, 0.52f, 0.28f) } // darker green for readability
            };

            _rarityLabelStyleCached = new GUIStyle(_labelStyle)
            {
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _checkStyleCached = new GUIStyle(_checkmarkStyle)
            {
                fontSize = ScaledFont(14)
            };

            _neededStyleCached = new GUIStyle(_checkmarkStyle)
            {
                normal = { textColor = _neededOrange },
                fontSize = ScaledFont(12)
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
                {
                    pixels[y * width + x] = rowColor;
                }
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

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                // Create subtle vertical gradient
                Color gradColor = Color.Lerp(lightColor, baseColor, t * 0.3f);

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
                        // Add subtle noise for parchment feel
                        float noise = ((x + y) % 3) * 0.01f;
                        pixels[y * width + x] = new Color(
                            gradColor.r + noise,
                            gradColor.g + noise * 0.8f,
                            gradColor.b + noise * 0.5f,
                            gradColor.a
                        );
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            // Header area
            DrawHeader();

            // Gold trim divider
            DrawGoldDivider();

            GUILayout.Space(Scaled(14));

            // Search and filter bar
            DrawSearchBar();

            GUILayout.Space(Scaled(14));

            // Section tabs
            DrawSectionTabs();

            GUILayout.Space(Scaled(16));

            // Content area
            DrawContent();

            GUILayout.Space(Scaled(10));

            // Footer
            DrawFooter();

            GUILayout.EndVertical();

            // Make window draggable from header
            GUI.DragWindow(new Rect(0, 0, WindowWidth, HeaderHeight));
        }

        private void DrawHeader()
        {
            var headerRect = GUILayoutUtility.GetRect(0, HeaderHeight, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(headerRect, _headerBackground, ScaleMode.StretchToFill);

            float pad = Scaled(25);
            GUILayout.BeginArea(new Rect(headerRect.x + pad, headerRect.y, headerRect.width - pad * 2f, headerRect.height));
            GUILayout.BeginHorizontal();

            // Left side - Title and subtitle
            GUILayout.BeginVertical();
            GUILayout.Space(Scaled(12));
            GUILayout.Label(ModLocalization.T("smut.title"), _titleStyle);
            GUILayout.Label(ModLocalization.T("smut.subtitle"), _subtitleStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Right side - Overall stats in a decorative box
            GUILayout.BeginVertical();
            GUILayout.Space(12);

            var (donated, total) = _donationManager.GetOverallStats();
            float percent = _donationManager.GetOverallCompletionPercent();

            // Stats box (using cached style)
            GUILayout.BeginVertical(_headerBoxStyle, GUILayout.Width(Scaled(100)));
            GUILayout.Label($"{percent:F0}%", _statsStyle);
            GUILayout.Label($"{donated}/{total}", _headerCountStyle);
            GUILayout.EndVertical();

            GUILayout.EndVertical();

            GUILayout.Space(Scaled(10));

            // Close button
            GUILayout.BeginVertical();
            GUILayout.Space(Scaled(20));
            if (GUILayout.Button("X", _closeButtonStyle, GUILayout.Width(Scaled(38)), GUILayout.Height(Scaled(38))))
            {
                Hide();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawGoldDivider()
        {
            var divRect = GUILayoutUtility.GetRect(0, Scaled(5), GUILayout.ExpandWidth(true));
            float margin = Scaled(24);
            divRect.x += margin;
            divRect.width -= margin * 2f;

            // Decorative gold divider (clearer separation)
            GUI.color = _borderDark;
            GUI.DrawTexture(new Rect(divRect.x, divRect.y, divRect.width, 1), Texture2D.whiteTexture);
            GUI.color = _goldRich;
            GUI.DrawTexture(new Rect(divRect.x, divRect.y + 1, divRect.width, 2), Texture2D.whiteTexture);
            GUI.color = _goldPale;
            GUI.DrawTexture(new Rect(divRect.x, divRect.y + 3, divRect.width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawSearchBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(Scaled(20));

            GUILayout.Label(ModLocalization.T("smut.search"), _searchLabelStyle, GUILayout.Width(Scaled(52)));

            // Search field
            _searchQuery = GUILayout.TextField(_searchQuery, _searchStyle, GUILayout.Width(Scaled(180)), GUILayout.Height(Scaled(28)));

            GUILayout.Space(Scaled(15));

            _showOnlyNeeded = GUILayout.Toggle(_showOnlyNeeded, " Needed only", _toggleStyle, GUILayout.Width(Scaled(115)));

            GUILayout.FlexibleSpace();

            // Show sync status message if active
            if (!string.IsNullOrEmpty(_syncStatusMessage))
            {
                GUILayout.Label(_syncStatusMessage, _syncStatusStyle, GUILayout.Width(Scaled(150)));
                GUILayout.Space(Scaled(8));
            }

            // Clear button
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                if (GUILayout.Button(ModLocalization.T("smut.clear"), _buttonStyle, GUILayout.Width(Scaled(55)), GUILayout.Height(Scaled(28))))
                {
                    _searchQuery = "";
                }
                GUILayout.Space(Scaled(8));
            }

            // Sync with Game button
            if (GUILayout.Button(ModLocalization.T("smut.sync"), _syncButtonStyle, GUILayout.Width(Scaled(105)), GUILayout.Height(Scaled(28))))
            {
                PerformGameSync();
            }

            GUILayout.Space(Scaled(20));
            GUILayout.EndHorizontal();
        }

        private void PerformGameSync()
        {
            try
            {
                var (donatedBefore, _) = _donationManager.GetOverallStats();

                MuseumPatches.SyncWithGameProgress();

                // Refresh cache immediately after syncing (user expects immediate feedback)
                RefreshGameProgressCacheImmediate();

                var (donatedAfter, total) = _donationManager.GetOverallStats();
                int newlyMarked = donatedAfter - donatedBefore;

                if (newlyMarked > 0)
                {
                    Plugin.SaveData();
                    _syncStatusMessage = $"Synced {newlyMarked} items!";
                    Plugin.Log?.LogInfo($"[UI] Synced {newlyMarked} items from game progress");
                }
                else
                {
                    _syncStatusMessage = "Already in sync!";
                }
                _syncStatusTimer = 4f; // Show message for 4 seconds
            }
            catch (System.Exception ex)
            {
                _syncStatusMessage = "Sync failed";
                _syncStatusTimer = 4f;
                Plugin.Log?.LogError($"[UI] Sync failed: {ex.Message}");
            }
        }

        private void DrawSectionTabs()
        {
            var sections = MuseumContent.GetAllSections();

            GUILayout.BeginHorizontal();
            GUILayout.Space(Scaled(20));

            for (int i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                var stats = _donationManager.GetSectionStats(section);
                bool isComplete = _donationManager.IsSectionComplete(section);
                bool isSelected = i == _selectedSectionIndex;

                var theme = _sectionThemes.TryGetValue(section.Id, out var t) ? t : (_woodMedium, _woodLight);

                var tabStyle = isSelected ? _sectionTabActiveStyle : _sectionTabStyle;

                // Build label
                string completeMark = isComplete ? " *" : "";
                string label = $"{section.Name}{completeMark}\n{stats.donated}/{stats.total}";

                if (GUILayout.Button(label, tabStyle, GUILayout.Width(Scaled(185)), GUILayout.Height(Scaled(52))))
                {
                    _selectedSectionIndex = i;
                    _scrollPosition = Vector2.zero;
                }

                if (i < sections.Count - 1)
                    GUILayout.Space(Scaled(8));
            }

            GUILayout.Space(Scaled(20));
            GUILayout.EndHorizontal();
        }

        private void DrawContent()
        {
            var sections = MuseumContent.GetAllSections();
            if (_selectedSectionIndex >= sections.Count)
                _selectedSectionIndex = 0;

            var section = sections[_selectedSectionIndex];
            var theme = _sectionThemes.TryGetValue(section.Id, out var t) ? t : (_woodMedium, _woodLight);

            GUILayout.BeginHorizontal();
            GUILayout.Space(Scaled(20));

            GUILayout.BeginVertical();

            // Section progress bar
            DrawSectionProgress(section, theme.Item1);

            GUILayout.Space(Scaled(10));

            // Scroll view (using cached style)
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, _scrollStyle,
                GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            bool hasVisibleContent = false;

            foreach (var bundle in section.Bundles)
            {
                bool bundleHasVisibleItems = DrawBundle(bundle, theme.Item1);
                if (bundleHasVisibleItems)
                {
                    hasVisibleContent = true;
                    GUILayout.Space(Scaled(10));
                }
            }

            // Empty state message
            if (!hasVisibleContent)
            {
                GUILayout.Space(Scaled(60));
                if (!string.IsNullOrEmpty(_searchQuery))
                    GUILayout.Label(ModLocalization.T("smut.empty.search", _searchQuery), _emptyStyle);
                else if (_showOnlyNeeded)
                    GUILayout.Label(ModLocalization.T("smut.empty.donated"), _emptyStyle);
            }

            GUILayout.EndScrollView();

            GUILayout.EndVertical();

            GUILayout.Space(Scaled(20));
            GUILayout.EndHorizontal();
        }

        private void DrawSectionProgress(MuseumSection section, Color sectionColor)
        {
            var stats = _donationManager.GetSectionStats(section);
            float percent = _donationManager.GetSectionCompletionPercent(section);

            var barRect = GUILayoutUtility.GetRect(0, Scaled(32), GUILayout.ExpandWidth(true));
            float inset = Scaled(3);

            // Background
            GUI.DrawTexture(barRect, _progressBg);

            // Fill (with slight rounding effect via inset)
            float fillW = Mathf.Max(0, (barRect.width - inset * 2) * (percent / 100f));
            var fillRect = new Rect(barRect.x + inset, barRect.y + inset, fillW, barRect.height - inset * 2);
            GUI.color = sectionColor;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Border
            GUI.color = _borderDark;
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(barRect.x, barRect.y + barRect.height - 1, barRect.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, 1, barRect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(barRect.x + barRect.width - 1, barRect.y, 1, barRect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Progress text (always dark for readability)
            var progressLabel = new GUIStyle(_labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = ScaledFont(14),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _textDark }
            };
            GUI.Label(barRect, $"{section.Name}: {stats.donated} / {stats.total}  ({percent:F0}%)", progressLabel);
        }

        private bool DrawBundle(MuseumBundle bundle, Color sectionColor)
        {
            bool isComplete = _donationManager.IsBundleComplete(bundle);
            bool isExpanded = _expandedBundles.Contains(bundle.Id);
            var stats = _donationManager.GetBundleStats(bundle);

            var visibleItems = GetVisibleItems(bundle);
            if (visibleItems.Count == 0 && (_showOnlyNeeded || !string.IsNullOrEmpty(_searchQuery)))
                return false;

            if (_showOnlyNeeded && isComplete && string.IsNullOrEmpty(_searchQuery))
                return false;

            // Get cached game progress for this bundle (avoid expensive reflection calls every frame)
            int gameDonationCount = _cachedGameDonationCounts.TryGetValue(bundle.Id, out var count) ? count : -1;
            bool gameComplete = _cachedGameCompleteStatus.TryGetValue(bundle.Id, out var complete) && complete;

            // Bundle header
            var headerStyle = (isComplete || gameComplete) ? _bundleHeaderCompleteStyle : _bundleHeaderStyle;
            string expandIcon = isExpanded ? "[-]" : "[+]";
            string completeIcon = (isComplete || gameComplete) ? " COMPLETE" : "";

            // Build label with game progress if available
            string label;
            if (gameDonationCount >= 0)
            {
                // Show both tracked and game progress
                label = $"{expandIcon}  {bundle.Name}{completeIcon}  ({stats.donated}/{stats.total})  [Game: {gameDonationCount}]";
            }
            else
            {
                label = $"{expandIcon}  {bundle.Name}{completeIcon}  ({stats.donated}/{stats.total})";
            }

            if (GUILayout.Button(label, headerStyle, GUILayout.ExpandWidth(true), GUILayout.Height(Scaled(44))))
            {
                if (isExpanded)
                    _expandedBundles.Remove(bundle.Id);
                else
                    _expandedBundles.Add(bundle.Id);
            }

            if (isExpanded)
            {
                DrawBundleItems(visibleItems);
            }

            return true;
        }

        private List<MuseumItem> GetVisibleItems(MuseumBundle bundle)
        {
            var items = new List<MuseumItem>();
            string searchLower = _searchQuery?.ToLower() ?? "";

            foreach (var item in bundle.Items)
            {
                bool isDonated = _donationManager.HasDonated(item.Id);

                if (_showOnlyNeeded && isDonated)
                    continue;

                if (!string.IsNullOrEmpty(searchLower))
                {
                    bool matchesSearch = item.Name.ToLower().Contains(searchLower) ||
                                        item.Rarity.ToString().ToLower().Contains(searchLower) ||
                                        bundle.Name.ToLower().Contains(searchLower);
                    if (!matchesSearch)
                        continue;
                }

                items.Add(item);
            }

            return items;
        }

        private void DrawBundleItems(List<MuseumItem> visibleItems)
        {
            int index = 0;
            foreach (var item in visibleItems)
            {
                DrawItemRow(item, index);
                index++;
            }
        }

        private void DrawItemRow(MuseumItem item, int index)
        {
            bool isDonated = _donationManager.HasDonated(item.Id);

            var bgTex = isDonated ? _itemDonated : (index % 2 == 0 ? _itemEven : _itemOdd);

            GUILayout.BeginHorizontal(_itemRowStyle, GUILayout.Height(Scaled(44)));

            // Draw background
            var lastRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == UnityEngine.EventType.Repaint && bgTex != null)
            {
                GUI.DrawTexture(lastRect, bgTex);
            }

            GUILayout.Space(Scaled(10));

            // Checkbox
            bool newDonated = GUILayout.Toggle(isDonated, "", GUILayout.Width(Scaled(24)));
            if (newDonated != isDonated)
            {
                _donationManager.ToggleDonated(item.Id);
            }

            GUILayout.Space(Scaled(8));

            // Item icon (placeholders use GameItemId <= 0 — no IconCache entry)
            var icon = item.GameItemId > 0 ? IconCache.GetIcon(item.GameItemId) : _itemPlaceholderIcon;
            if (icon != null)
            {
                float iconSz = IconSize;
                var iconRect = GUILayoutUtility.GetRect(iconSz, iconSz, GUILayout.Width(iconSz), GUILayout.Height(iconSz));
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                GUILayout.Space(Scaled(10));
            }
            else
            {
                GUILayout.Space(IconSize + Scaled(10));
            }

            // Item name with rarity color (use GUI.contentColor to avoid creating styles)
            var rarityColor = _rarityColors.TryGetValue(item.Rarity, out var c) ? c : _textDark;
            var savedColor = GUI.contentColor;

            GUI.contentColor = isDonated ? _successGreen : rarityColor;
            GUILayout.Label(item.Name, isDonated ? _itemNameDonatedStyle : _itemNameStyle, GUILayout.ExpandWidth(true));

            // Rarity label (using cached style with content color)
            GUI.contentColor = rarityColor;
            GUILayout.Label(item.Rarity.ToString(), _rarityLabelStyleCached, GUILayout.Width(Scaled(80)));

            // Status (using cached styles)
            if (isDonated)
            {
                GUI.contentColor = _successGreen;
                GUILayout.Label(ModLocalization.T("smut.item.donated"), _checkStyleCached, GUILayout.Width(Scaled(60)));
            }
            else
            {
                GUI.contentColor = _neededOrange;
                GUILayout.Label(ModLocalization.T("smut.item.needed"), _neededStyleCached, GUILayout.Width(Scaled(60)));
            }

            GUI.contentColor = savedColor;

            GUILayout.Space(Scaled(10));
            GUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var footerText = $"Press {(_requireCtrl ? "Ctrl+" : "")}{_toggleKey} to open/close  ·  ESC to close  ·  Sync copies completed bundles from game";
            GUILayout.Label(footerText, _footerStyle);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(Scaled(12));
        }
    }
}
