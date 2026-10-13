using System;
using System.Collections.Generic;
using BirthdayReminder.Data;
using UnityEngine;
using SunhavenMods.Shared;
namespace BirthdayReminder.UI
{
    /// <summary>
    /// Small movable HUD showing today's birthdays.
    /// Displays on wake up and can be dismissed.
    /// </summary>
    public class BirthdayHUD : MonoBehaviour
    {
        // Window settings (base values, scaled by _scale)
        private const int WINDOW_ID = 98770;
        private const int GIFT_WINDOW_ID = 98771;
        private const float BASE_WINDOW_WIDTH = 320f;
        private const float BASE_MIN_HEIGHT = 100f;
        private const float BASE_MAX_HEIGHT = 500f;
        private const float BASE_HEADER_HEIGHT = 28f;
        private const float BASE_ITEM_HEIGHT = 65f;
        private const float BASE_GIFT_WINDOW_WIDTH = 300f;
        private const float BASE_GIFT_WINDOW_HEIGHT = 400f;

        // UI scale (from config, 0.5-2.5)
        private float _scale = 1f;

        // Manager reference
        private BirthdayManager _manager;

        private float WindowWidth => BASE_WINDOW_WIDTH * _scale;
        private float MinHeight => BASE_MIN_HEIGHT * _scale;
        private float MaxHeight => BASE_MAX_HEIGHT * _scale;
        private float HeaderHeight => BASE_HEADER_HEIGHT * _scale;
        private float ItemHeight => BASE_ITEM_HEIGHT * _scale;
        private float GiftWindowWidth => BASE_GIFT_WINDOW_WIDTH * _scale;
        private float GiftWindowHeight => BASE_GIFT_WINDOW_HEIGHT * _scale;

        private int ScaledFont(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * _scale));
        private float Scaled(float value) => value * _scale;
        private int ScaledInt(float value) => Mathf.RoundToInt(value * _scale);

        // Display state
        private bool _isVisible;
        private Rect _windowRect;
        private float _showTimer;
        private const float AUTO_HIDE_DELAY = 15f;

        // Gift popup state
        private bool _showGiftPopup;
        private Rect _giftPopupRect;
        private BirthdayDisplayInfo _selectedNPC;
        private Vector2 _giftScrollPosition;

        // Position persistence callback
        public Action<float, float> OnPositionChanged;

        // Color scheme - warm festive theme
        private readonly Color _bgColor = new Color(0.18f, 0.14f, 0.12f, 0.95f);  // Dark brown
        private readonly Color _headerColor = new Color(0.75f, 0.35f, 0.45f, 1f);  // Rose/pink
        private readonly Color _borderColor = new Color(0.85f, 0.65f, 0.50f, 1f);  // Gold border
        private readonly Color _textLight = new Color(0.95f, 0.92f, 0.88f);  // Cream text
        private readonly Color _textDark = new Color(0.25f, 0.20f, 0.15f);
        private readonly Color _giftedColor = new Color(0.5f, 0.85f, 0.5f);  // Bright green
        private readonly Color _ungiftedColor = new Color(1f, 0.75f, 0.3f);  // Gold/amber
        private readonly Color _hintColor = new Color(0.75f, 0.70f, 0.65f);
        private readonly Color _lovedColor = new Color(0.95f, 0.45f, 0.55f);  // Pink for loved
        private readonly Color _likedColor = new Color(0.55f, 0.70f, 0.95f);  // Blue for liked
        private readonly Color _universalColor = new Color(0.85f, 0.75f, 0.45f);  // Gold for universal

        // Styles
        private bool _stylesInitialized;
        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _giftedStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _moreButtonStyle;
        private GUIStyle _giftItemStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _universalItemStyle;

        // Cached inline styles (to avoid GC allocations every frame)
        private GUIStyle _statusMessageStyle;
        private GUIStyle _itemBoxStyle;
        private GUIStyle _statusGiftedStyle;
        private GUIStyle _statusUngiftedStyle;
        private GUIStyle _nameGiftedStyle;
        private GUIStyle _nameUngiftedStyle;
        private GUIStyle _lovedSectionStyle;
        private GUIStyle _likedSectionStyle;
        private GUIStyle _universalSubHeaderStyle;
        private GUIStyle _lovedBulletStyle;
        private GUIStyle _likedBulletStyle;
        private GUIStyle _universalBulletStyle;

        // Textures
        private Texture2D _windowBackground;
        private Texture2D _headerBackground;
        private Texture2D _itemBackground;
        private Texture2D _separatorTex;

        private void Awake()
        {
            _windowRect = new Rect(100, 100, WindowWidth, MinHeight);
            _isVisible = false;  // Don't show by default - only show when player is in-game with birthdays
            Plugin.Log?.LogInfo("[BirthdayHUD] Awake called - isVisible set to false (waiting for player init)");
        }

        public void Initialize(BirthdayManager manager)
        {
            _manager = manager;
            Plugin.Log?.LogInfo($"[BirthdayHUD] Initialize called, manager: {manager != null}, screen: ({Screen.width}x{Screen.height})");

            // Set default position (top-right corner)
            if (Screen.width > 0 && Screen.height > 0)
            {
                _windowRect = new Rect(
                    Screen.width - WindowWidth - Scaled(20),
                    Scaled(80),
                    WindowWidth,
                    MinHeight
                );
                Plugin.Log?.LogInfo($"[BirthdayHUD] Default position set to ({_windowRect.x}, {_windowRect.y})");
            }
            else
            {
                // Fallback position if screen isn't ready
                _windowRect = new Rect(100, 100, WindowWidth, MinHeight);
                Plugin.Log?.LogInfo($"[BirthdayHUD] Using fallback position (100, 100) - screen not ready");
            }

            if (_manager != null)
            {
                _manager.OnBirthdaysUpdated += OnBirthdaysUpdated;
            }
        }

        public void SetPosition(float x, float y)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;

            if (x >= 0)
            {
                _windowRect.x = Mathf.Clamp(x, 0, Screen.width - _windowRect.width);
            }
            if (y >= 0)
            {
                _windowRect.y = Mathf.Clamp(y, 0, Screen.height - _windowRect.height);
            }
        }

        public void SetScale(float scale)
        {
            _scale = Mathf.Clamp(scale, 0.5f, 2.5f);
            _stylesInitialized = false;
            EnsureOnScreen();
        }

        public void Show()
        {
            _isVisible = true;
            _showTimer = 0f;

            // Ensure window is within screen bounds
            EnsureOnScreen();

            Plugin.Log?.LogInfo($"[BirthdayHUD] Show() called - isVisible: {_isVisible}, pos: ({_windowRect.x}, {_windowRect.y}), screen: ({Screen.width}x{Screen.height}), birthdays: {_manager?.TodaysBirthdays?.Count ?? 0}");
        }

        /// <summary>
        /// Ensure the HUD window is within the visible screen area
        /// </summary>
        private void EnsureOnScreen()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;

            // If position is off-screen or invalid, reset to default position
            bool needsReset = _windowRect.x < 0 ||
                              _windowRect.y < 0 ||
                              _windowRect.x > Screen.width - 50 ||
                              _windowRect.y > Screen.height - 50;

            if (needsReset)
            {
                Plugin.Log?.LogInfo($"[BirthdayHUD] Resetting position - was ({_windowRect.x}, {_windowRect.y})");
                _windowRect.x = Screen.width - WindowWidth - Scaled(20);
                _windowRect.y = Scaled(80);
            }

            // Clamp to screen bounds
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);
        }

        public void Hide()
        {
            _isVisible = false;
        }

        public void Toggle()
        {
            if (_isVisible)
                Hide();
            else
                Show();
        }

        private void OnBirthdaysUpdated()
        {
            if (_manager != null && _manager.HasBirthdays)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        private void Update()
        {
            // Always check for date changes, even when hidden
            _manager?.CheckForDateChange(Time.unscaledDeltaTime);

            if (!_isVisible) return;

            // Update status message timer
            _manager?.UpdateStatusMessage(Time.unscaledDeltaTime);

            if (_manager != null && !_manager.HasUngiftedBirthdays)
            {
                _showTimer += Time.unscaledDeltaTime;
                if (_showTimer >= AUTO_HIDE_DELAY)
                {
                    Hide();
                }
            }
            else
            {
                _showTimer = 0f;
            }
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            // IMGUI draws above many uGUI layers; defer while vanilla blocking UI is open (journal/calendar,
            // inventory, dialogue, etc.) so we do not corrupt calendar rendering or steal clicks.
            if (GameModalUiGuard.ShouldSuppressBirthdayImGui())
                return;

            if (!_stylesInitialized)
            {
                InitializeStyles();
            }

            int birthdayCount = (_manager != null && _manager.TodaysBirthdays != null) ? _manager.TodaysBirthdays.Count : 0;

            // Calculate height: header + padding + items + bottom padding
            float contentHeight = HeaderHeight + Scaled(12);
            if (birthdayCount == 0)
            {
                contentHeight += Scaled(40);
            }
            else
            {
                contentHeight += birthdayCount * ItemHeight;
            }
            contentHeight += Scaled(16);
            _windowRect.width = WindowWidth;
            _windowRect.height = Mathf.Clamp(contentHeight, MinHeight, MaxHeight);

            var prevRect = _windowRect;

            // Draw shadow
            DrawShadow(_windowRect, 4);

            GUI.depth = -800;
            _windowRect = GUI.Window(WINDOW_ID, _windowRect, DrawWindow, "", _windowStyle);

            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);

            if (_showGiftPopup && _selectedNPC != null)
            {
                DrawShadow(_giftPopupRect, 4);

                GUI.depth = -900;
                _giftPopupRect = GUI.Window(GIFT_WINDOW_ID, _giftPopupRect, DrawGiftPopup, "", _windowStyle);

                _giftPopupRect.x = Mathf.Clamp(_giftPopupRect.x, 0, Screen.width - _giftPopupRect.width);
                _giftPopupRect.y = Mathf.Clamp(_giftPopupRect.y, 0, Screen.height - _giftPopupRect.height);
            }

            if (Math.Abs(_windowRect.x - prevRect.x) > 0.1f || Math.Abs(_windowRect.y - prevRect.y) > 0.1f)
            {
                OnPositionChanged?.Invoke(_windowRect.x, _windowRect.y);
            }
        }

        private void DrawShadow(Rect rect, int offset)
        {
            var shadowColor = new Color(0, 0, 0, 0.3f);
            var shadowRect = new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height);
            GUI.color = shadowColor;
            GUI.DrawTexture(shadowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawWindow(int windowId)
        {
            // Draw border
            DrawBorder(_windowRect.width, _windowRect.height, Mathf.Max(1, ScaledInt(2)));

            GUILayout.BeginVertical();
            int birthdayCount = (_manager != null && _manager.TodaysBirthdays != null) ? _manager.TodaysBirthdays.Count : 0;
            string dateStr = _manager?.CurrentDateFormatted ?? "";
            string titleBase = birthdayCount <= 1
                ? ModLocalization.T("birthday.hud.title.one")
                : ModLocalization.T("birthday.hud.title.many");
            string headerTitle = string.IsNullOrEmpty(dateStr) ? titleBase : $"{titleBase} - {dateStr}";
            DrawHeader(headerTitle, _windowRect.width);
            DrawBirthdays();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, _windowRect.width - Scaled(24), HeaderHeight));
        }

        private void DrawBorder(float width, float height, int borderSize)
        {
            GUI.color = _borderColor;
            // Top
            GUI.DrawTexture(new Rect(0, 0, width, borderSize), Texture2D.whiteTexture);
            // Bottom
            GUI.DrawTexture(new Rect(0, height - borderSize, width, borderSize), Texture2D.whiteTexture);
            // Left
            GUI.DrawTexture(new Rect(0, 0, borderSize, height), Texture2D.whiteTexture);
            // Right
            GUI.DrawTexture(new Rect(width - borderSize, 0, borderSize, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawHeader(string title, float width)
        {
            var headerRect = new Rect(0, 0, width, HeaderHeight);
            if (_headerBackground != null)
            {
                GUI.DrawTexture(headerRect, _headerBackground);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(Scaled(10));

            // Cake icon using text
            GUILayout.Label("[*]", _headerStyle, GUILayout.Width(Scaled(24)), GUILayout.Height(HeaderHeight));
            GUILayout.Label(title, _headerStyle, GUILayout.Height(HeaderHeight));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("X", _closeButtonStyle, GUILayout.Width(Scaled(24)), GUILayout.Height(Scaled(24))))
            {
                Hide();
            }

            GUILayout.Space(Scaled(6));
            GUILayout.EndHorizontal();
        }

        private void DrawBirthdays()
        {
            GUILayout.Space(Scaled(6));

            // Show status message if any (e.g., "Refreshed!") - using cached style
            if (_manager != null && _manager.HasStatusMessage)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(_manager.StatusMessage, _statusMessageStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(Scaled(4));
            }

            if (_manager == null || _manager.TodaysBirthdays == null || _manager.TodaysBirthdays.Count == 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(ModLocalization.T("birthday.hud.noBirthdays"), _hintStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                foreach (var birthday in _manager.TodaysBirthdays)
                {
                    DrawBirthdayItem(birthday);
                }
            }

            GUILayout.Space(Scaled(6));
        }

        private void DrawBirthdayItem(BirthdayDisplayInfo birthday)
        {
            // Use cached box style for item background
            GUILayout.BeginVertical(_itemBoxStyle);

            GUILayout.BeginHorizontal();

            // Status indicator (using cached styles)
            var statusStyle = birthday.HasBeenGifted ? _statusGiftedStyle : _statusUngiftedStyle;
            GUILayout.Label(birthday.HasBeenGifted ? "[OK]" : "[!!]", statusStyle, GUILayout.Width(Scaled(40)));

            GUILayout.BeginVertical();

            // Name row (using cached styles)
            GUILayout.BeginHorizontal();
            var nameStyle = birthday.HasBeenGifted ? _nameGiftedStyle : _nameUngiftedStyle;
            GUILayout.Label(birthday.NPCName, nameStyle);

            GUILayout.FlexibleSpace();

            if (birthday.AllLovedGifts.Count > 0 || birthday.AllLikedGifts.Count > 0)
            {
                if (GUILayout.Button(ModLocalization.T("birthday.hud.gifts"), _moreButtonStyle, GUILayout.Width(Scaled(50)), GUILayout.Height(Scaled(20))))
                {
                    OpenGiftPopup(birthday);
                }
            }
            GUILayout.EndHorizontal();

            // Gift hint
            if (Plugin.ShowGiftHints && !birthday.HasBeenGifted && !string.IsNullOrEmpty(birthday.GiftHint))
            {
                GUILayout.Space(Scaled(2));
                GUILayout.Label(birthday.GiftHint, _hintStyle);
            }

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void OpenGiftPopup(BirthdayDisplayInfo birthday)
        {
            _selectedNPC = birthday;
            _showGiftPopup = true;
            _giftScrollPosition = Vector2.zero;

            _giftPopupRect = new Rect(
                _windowRect.x + _windowRect.width + Scaled(10),
                _windowRect.y,
                GiftWindowWidth,
                GiftWindowHeight
            );

            if (_giftPopupRect.x + GiftWindowWidth > Screen.width)
            {
                _giftPopupRect.x = _windowRect.x - GiftWindowWidth - Scaled(10);
            }
        }

        private void DrawGiftPopup(int windowId)
        {
            // Draw border
            DrawBorder(GiftWindowWidth, GiftWindowHeight, ScaledInt(2));

            GUILayout.BeginVertical();

            // Header
            var headerRect = new Rect(0, 0, GiftWindowWidth, HeaderHeight);
            if (_headerBackground != null)
            {
                GUI.DrawTexture(headerRect, _headerBackground);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(Scaled(10));
            GUILayout.Label(ModLocalization.T("birthday.gift.title", _selectedNPC.NPCName), _headerStyle, GUILayout.Height(HeaderHeight));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", _closeButtonStyle, GUILayout.Width(Scaled(24)), GUILayout.Height(Scaled(24))))
            {
                _showGiftPopup = false;
                _selectedNPC = null;
            }
            GUILayout.Space(Scaled(6));
            GUILayout.EndHorizontal();

            // Scrollable gift list
            _giftScrollPosition = GUILayout.BeginScrollView(_giftScrollPosition, GUILayout.Height(GiftWindowHeight - HeaderHeight - Scaled(10)));

            // === LOVED GIFTS (NPC-specific + Universal) ===
            GUILayout.Space(Scaled(6));
            DrawSectionHeader(ModLocalization.T("birthday.gift.loved").ToUpperInvariant(), _lovedColor);
            GUILayout.Space(Scaled(4));

            // NPC's loved gifts
            foreach (var gift in _selectedNPC.AllLovedGifts)
            {
                DrawGiftItem(gift, _lovedColor);
            }

            // Universal loved gifts
            if (BirthdayCache.UniversalLoved.Count > 0)
            {
                GUILayout.Space(Scaled(4));
                DrawSubHeader(ModLocalization.T("birthday.gift.universalLoved"), _universalColor);
                foreach (var gift in BirthdayCache.UniversalLoved)
                {
                    DrawGiftItem(gift, _universalColor);
                }
            }

            // Separator
            GUILayout.Space(Scaled(8));
            DrawSeparator();

            // === LIKED GIFTS (NPC-specific + Universal) ===
            GUILayout.Space(Scaled(8));
            DrawSectionHeader(ModLocalization.T("birthday.gift.liked").ToUpperInvariant(), _likedColor);
            GUILayout.Space(Scaled(4));

            // NPC's liked gifts
            foreach (var gift in _selectedNPC.AllLikedGifts)
            {
                DrawGiftItem(gift, _likedColor);
            }

            // Universal liked gifts
            if (BirthdayCache.UniversalLiked.Count > 0)
            {
                GUILayout.Space(Scaled(4));
                DrawSubHeader(ModLocalization.T("birthday.gift.universalLiked"), _universalColor);
                foreach (var gift in BirthdayCache.UniversalLiked)
                {
                    DrawGiftItem(gift, _universalColor);
                }
            }

            GUILayout.Space(Scaled(10));

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, GiftWindowWidth - Scaled(24), HeaderHeight));
        }

        private void DrawSectionHeader(string text, Color color)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(Scaled(8));
            // Use cached style based on color
            GUIStyle style;
            if (color == _lovedColor)
                style = _lovedSectionStyle;
            else if (color == _likedColor)
                style = _likedSectionStyle;
            else
                style = _sectionHeaderStyle;
            GUILayout.Label(text, style);
            GUILayout.EndHorizontal();
        }

        private void DrawSubHeader(string text, Color color)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(Scaled(12));
            // Use cached universal sub-header style
            GUILayout.Label(text, _universalSubHeaderStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawGiftItem(string gift, Color bulletColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(Scaled(16));

            // Colored bullet (using cached styles)
            GUIStyle bulletStyle;
            if (bulletColor == _lovedColor)
                bulletStyle = _lovedBulletStyle;
            else if (bulletColor == _likedColor)
                bulletStyle = _likedBulletStyle;
            else
                bulletStyle = _universalBulletStyle;
            GUILayout.Label("\u2022", bulletStyle, GUILayout.Width(Scaled(12)));

            // Gift name
            GUILayout.Label(gift, _giftItemStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawSeparator()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(Scaled(20));
            float sepW = GiftWindowWidth - Scaled(40);
            var rect = GUILayoutUtility.GetRect(sepW, 1);
            GUI.color = new Color(_borderColor.r, _borderColor.g, _borderColor.b, 0.4f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, sepW, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        #region Style Initialization

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            try
            {
                CreateTextures();
                CreateStyles();
                _stylesInitialized = true;
                Plugin.Log?.LogInfo("[BirthdayHUD] Styles initialized successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogError($"[BirthdayHUD] Failed to initialize styles: {ex.Message}");
                _windowStyle = GUI.skin.box;
                _stylesInitialized = true;
            }
        }

        private void CreateTextures()
        {
            _windowBackground = MakeTex(4, 4, _bgColor);
            _headerBackground = MakeGradientTex(4, 8, _headerColor, new Color(_headerColor.r * 0.7f, _headerColor.g * 0.7f, _headerColor.b * 0.7f, 1f));
            _itemBackground = MakeTex(4, 4, new Color(0.25f, 0.20f, 0.18f, 0.6f));
            _separatorTex = MakeTex(1, 1, _borderColor);
        }

        private void CreateStyles()
        {
            int pad = Mathf.Max(1, ScaledInt(4));
            int border = Mathf.Max(1, ScaledInt(2));
            _windowStyle = new GUIStyle
            {
                normal = { background = _windowBackground, textColor = _textLight },
                padding = new RectOffset(pad, pad, pad, pad),
                border = new RectOffset(border, border, border, border)
            };

            _headerStyle = new GUIStyle
            {
                fontSize = ScaledFont(13),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(ScaledInt(2), ScaledInt(2), ScaledInt(2), ScaledInt(2))
            };

            _nameStyle = new GUIStyle
            {
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _textLight },
                alignment = TextAnchor.MiddleLeft
            };

            _giftedStyle = new GUIStyle(_nameStyle)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(_textLight.r, _textLight.g, _textLight.b, 0.5f) }
            };

            _hintStyle = new GUIStyle
            {
                fontSize = ScaledFont(10),
                fontStyle = FontStyle.Italic,
                normal = { textColor = _hintColor },
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };

            _closeButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(14),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.9f, 0.9f) },
                hover = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };

            _moreButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ScaledFont(10),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _borderColor, background = MakeTex(1, 1, new Color(0.3f, 0.25f, 0.22f, 0.8f)) },
                hover = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.5f, 0.35f, 0.3f, 0.9f)) },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(4), ScaledInt(4), ScaledInt(2), ScaledInt(2)),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _giftItemStyle = new GUIStyle
            {
                fontSize = ScaledFont(11),
                normal = { textColor = _textLight },
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                padding = new RectOffset(0, 0, 1, 1)
            };

            _sectionHeaderStyle = new GUIStyle
            {
                fontSize = ScaledFont(11),
                fontStyle = FontStyle.Bold,
                normal = { textColor = _textLight },
                alignment = TextAnchor.MiddleLeft
            };

            _universalItemStyle = new GUIStyle(_giftItemStyle)
            {
                normal = { textColor = _universalColor }
            };

            // Cached inline styles (created once, not every frame)
            _statusMessageStyle = new GUIStyle(_hintStyle)
            {
                normal = { textColor = _giftedColor },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _itemBoxStyle = new GUIStyle
            {
                normal = { background = _itemBackground },
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(6), ScaledInt(6)),
                margin = new RectOffset(ScaledInt(6), ScaledInt(6), ScaledInt(2), ScaledInt(2))
            };

            _statusGiftedStyle = new GUIStyle(_nameStyle)
            {
                normal = { textColor = _giftedColor },
                fontStyle = FontStyle.Bold,
                fontSize = ScaledFont(13)
            };

            _statusUngiftedStyle = new GUIStyle(_nameStyle)
            {
                normal = { textColor = _ungiftedColor },
                fontStyle = FontStyle.Bold,
                fontSize = ScaledFont(13)
            };

            _nameGiftedStyle = new GUIStyle(_nameStyle)
            {
                normal = { textColor = new Color(_textLight.r, _textLight.g, _textLight.b, 0.5f) },
                fontStyle = FontStyle.Italic,
                fontSize = ScaledFont(13)
            };

            _nameUngiftedStyle = new GUIStyle(_nameStyle)
            {
                normal = { textColor = _textLight },
                fontStyle = FontStyle.Bold,
                fontSize = ScaledFont(13)
            };

            _lovedSectionStyle = new GUIStyle(_sectionHeaderStyle)
            {
                normal = { textColor = _lovedColor }
            };

            _likedSectionStyle = new GUIStyle(_sectionHeaderStyle)
            {
                normal = { textColor = _likedColor }
            };

            _universalSubHeaderStyle = new GUIStyle(_hintStyle)
            {
                normal = { textColor = _universalColor },
                fontStyle = FontStyle.Bold,
                fontSize = ScaledFont(9)
            };

            _lovedBulletStyle = new GUIStyle(_giftItemStyle)
            {
                normal = { textColor = _lovedColor }
            };

            _likedBulletStyle = new GUIStyle(_giftItemStyle)
            {
                normal = { textColor = _likedColor }
            };

            _universalBulletStyle = new GUIStyle(_giftItemStyle)
            {
                normal = { textColor = _universalColor }
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
                Color rowColor = Color.Lerp(bottomColor, topColor, t);
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = rowColor;
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
                _manager.OnBirthdaysUpdated -= OnBirthdaysUpdated;
            }
        }
    }
}
