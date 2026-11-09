using System;
using System.Collections.Generic;
using SunhavenMods.Shared;
using TheVault;
using TheVault.Patches;
using TheVault.Vault;
using UnityEngine;
using Wish;

namespace TheVault.UI
{
    /// <summary>
    /// Unity IMGUI-based vault interface.
    /// Displays all stored currencies and allows deposit/withdraw operations.
    /// </summary>
    public class VaultUI : MonoBehaviour
    {
        private VaultManager _vaultManager;
        private bool _isVisible;
        private Rect _windowRect;
        private Vector2 _scrollPosition;
        private Vector2 _debugVaultScroll;

        // UI State
        private CurrencyCategory _selectedCategory = CurrencyCategory.SeasonalToken;
        private string _depositAmount = "1";
        private string _selectedCurrencyId = "";
        private bool _showDebugVaultDump;

        // Styling
        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _withdrawButtonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _categoryButtonStyle;
        private GUIStyle _selectedCategoryStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _textFieldStyle;
        private Texture2D _windowBackground;
        private Texture2D _rowBackground;
        private Texture2D _rowAltBackground;
        private Texture2D _solidWhite;
        private Texture2D _buttonNormal;
        private Texture2D _buttonHover;
        private Texture2D _withdrawNormal;
        private Texture2D _withdrawHover;
        private Texture2D _tabNormal;
        private Texture2D _tabSelected;
        private Texture2D _headerBarBg;
        private GUIStyle _headerBarStyle;

        // Colors - Enhanced color scheme
        private readonly Color _windowBgColor = new Color(0.12f, 0.12f, 0.18f, 0.98f);
        private readonly Color _headerGradientStart = new Color(0.25f, 0.45f, 0.65f);
        private readonly Color _headerGradientEnd = new Color(0.15f, 0.30f, 0.50f);
        private readonly Color _accentColor = new Color(0.4f, 0.7f, 0.95f);
        private readonly Color _goldColor = new Color(0.95f, 0.8f, 0.3f);
        private readonly Color _withdrawColor = new Color(0.7f, 0.35f, 0.35f);
        private readonly Color _withdrawHoverColor = new Color(0.85f, 0.4f, 0.4f);
        private readonly Color _rowEvenColor = new Color(0.15f, 0.15f, 0.22f, 0.9f);
        private readonly Color _rowOddColor = new Color(0.18f, 0.18f, 0.26f, 0.9f);
        private readonly Color _textColor = new Color(0.9f, 0.9f, 0.95f);
        private readonly Color _textDimColor = new Color(0.6f, 0.6f, 0.7f);

        // Cached styles and textures for DrawCurrencyRow (to avoid GC allocations every frame)
        private GUIStyle _subtitleStyle;
        private GUIStyle _emptyStyle;
        private GUIStyle _toggleOnStyle;
        private GUIStyle _toggleOffStyle;
        private GUIStyle _iconFallbackStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _selectedNameStyle;
        private GUIStyle _amountLabelStyle;
        private GUIStyle _statusOkStyle;
        private GUIStyle _statusErrorStyle;
        private string _vaultStatusMessage;
        private bool _vaultStatusIsError;
        private float _vaultStatusUntil;
        private static bool _shownCorruptVaultSessionNotice;
        private Texture2D _toggleOnBg;
        private Texture2D _toggleOnHover;
        private Texture2D _toggleOffBg;
        private Texture2D _toggleOffHover;

        // Window dimensions (base values, scaled by _scale)
        // Width ~560 so -1/-5/-10 touch buttons + name still fit at scale 1 (Steam Deck V1).
        private const float BASE_WINDOW_WIDTH = 560f;
        // Row height ~52 so 44px min touch targets fit with padding.
        private const float BASE_ROW_HEIGHT = 52f;
        private const float BASE_HEADER_HEIGHT = 118f;
        // Extra footer room for amount chip row under qty / Withdraw / Deposit.
        private const float BASE_FOOTER_HEIGHT = 255f;
        private const float BASE_MIN_CONTENT_HEIGHT = 120f;
        private const float BASE_MAX_CONTENT_HEIGHT = 420f;
        private const float BASE_SETTINGS_CONTENT_HEIGHT = 390f;

        // UI scale (from config, 0.5-3.0)
        private float _scale = 1f;

        /// <summary>GUIStyle/GUI.skin may only be touched inside OnGUI; defer full style build until then.</summary>
        private bool _stylesDirty = true;

        private float WindowWidth => BASE_WINDOW_WIDTH * _scale;
        private float RowHeight => BASE_ROW_HEIGHT * _scale;
        private float HeaderHeight => BASE_HEADER_HEIGHT * _scale;
        private float FooterHeight => BASE_FOOTER_HEIGHT * _scale;
        private float MinContentHeight => BASE_MIN_CONTENT_HEIGHT * _scale;
        private float MaxContentHeight => BASE_MAX_CONTENT_HEIGHT * _scale;
        private float SettingsContentHeight => BASE_SETTINGS_CONTENT_HEIGHT * _scale;

        private int ScaledFont(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * _scale));
        private float Scaled(float value) => value * _scale;
        private int ScaledInt(float value) => Mathf.RoundToInt(value * _scale);

        /// <summary>Minimum ~44px touch target for Deck / trackpad; grows with WindowScale.</summary>
        private float TouchSize(float basePx) => Mathf.Max(44f, Scaled(basePx));

        // Toggle key
        private KeyCode _toggleKey = KeyCode.V;
        private bool _requiresModifier = true; // Requires Ctrl+V by default
        private KeyCode _altToggleKey = KeyCode.F8; // Alternative key for Steam Deck (no modifier)

        // Settings panel (in-game config editing)
        private bool _showSettings;
        private Vector2 _settingsScroll;
        private static readonly KeyCode[] SettingsKeyOptions = new[]
        {
            KeyCode.None, KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
            KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
            KeyCode.V, KeyCode.B, KeyCode.G, KeyCode.K, KeyCode.U, KeyCode.H
        };
        private static readonly string[] SettingsKeyNames = new[]
        {
            "None", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "V", "B", "G", "K", "U", "H"
        };

        public void Initialize(VaultManager vaultManager)
        {
            _vaultManager = vaultManager;
            _isVisible = false;

            // Center window on screen (initial size)
            float w = WindowWidth;
            float initialHeight = HeaderHeight + Scaled(260f) + FooterHeight;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - initialHeight) / 2f;
            _windowRect = new Rect(x, y, w, initialHeight);

            Plugin.Log?.LogDebug("VaultUI initialized");
        }

        public void SetToggleKey(KeyCode key, bool requireModifier = true)
        {
            _toggleKey = key;
            _requiresModifier = requireModifier;
        }

        public void SetAltToggleKey(KeyCode key)
        {
            _altToggleKey = key;
        }

        public void SetScale(float scale)
        {
            _scale = Mathf.Clamp(scale, 0.5f, 3.0f);
            _stylesDirty = true;
            _windowStyle = null;
            float w = WindowWidth;
            float h = HeaderHeight + 260f * _scale + FooterHeight;
            _windowRect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        }

        public bool IsVisible => _isVisible;

        // Unique identifier for pause object system
        private const string PAUSE_ID = "TheVault_UI";

        public void Show()
        {
            _isVisible = true;

            // Block game input while vault is open
            try
            {
                if (Player.Instance != null)
                {
                    Player.Instance.AddPauseObject(PAUSE_ID);
                }

                PlayerInput.DisableInput(PAUSE_ID);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Could not block game input: {ex.Message}");
            }

            VaultPointerGuard.Sync(_windowRect, true);
            VaultPointerGuard.EnsureBlocker();
            Plugin.Log?.LogDebug("Vault UI opened");

            // Log icon cache status when opening
            IconCache.LogStatus();

            // Check for secret gifts on first open
            SecretGifts.CheckAndGiveFirstOpenGift(PlayerPatches.LoadedCharacterName);

            if (!_shownCorruptVaultSessionNotice &&
                (Plugin.LastVaultLoadQuarantinedCorruptFile || Plugin.VaultLoadFailedNoRecoverableData))
            {
                _shownCorruptVaultSessionNotice = true;
                string msg = Plugin.VaultLoadFailedNoRecoverableData
                    ? "Your vault could not be read. Saving is blocked so your .backup is not overwritten. Restore from TheVault/Saves, or click Start Fresh below to abandon the backup."
                    : "Your vault file could not be read and was moved to a .corrupt-*.bak file in Saves. An empty vault is in memory — restore the backup from your config folder if needed.";
                SetVaultStatus(msg, true);
                Plugin.Log?.LogWarning("[VaultUI] " + msg);
            }
        }

        public void Hide()
        {
            // Always clear pause/input if we were open (e.g. switching UI mode).
            if (!_isVisible) return;
            _isVisible = false;

            try
            {
                if (Player.Instance != null)
                    Player.Instance.RemovePauseObject(PAUSE_ID);

                PlayerInput.EnableInput(PAUSE_ID);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Could not re-enable game input: {ex.Message}");
            }

            VaultPointerGuard.Sync(default, false);
            Plugin.Log?.LogDebug("Vault UI closed");
        }

        public void Toggle()
        {
            // Don't allow toggle until vault is loaded
            if (!PlayerPatches.IsVaultLoaded)
            {
                Plugin.Log?.LogDebug("Cannot toggle vault UI - save not yet loaded");
                return;
            }

            if (_isVisible)
                Hide();
            else
                Show();
        }

        private void OnDestroy()
        {
            if (_isVisible)
            {
                Hide();
            }
            VaultPointerGuard.DestroyBlocker();
            DisposeGeneratedTextures();
        }

        private void Awake()
        {
            _stylesDirty = true;
            _windowStyle = null;
        }

        private void OnEnable()
        {
            _stylesDirty = true;
            _windowStyle = null;
        }

        private void Update()
        {
            if (_isVisible)
                VaultPointerGuard.EnsureBlocker();

            if (!string.IsNullOrEmpty(_vaultStatusMessage) && Time.unscaledTime > _vaultStatusUntil)
                _vaultStatusMessage = null;

            // NOTE: Hotkey detection is now handled by PersistentRunner to avoid double-toggle.
            // PersistentRunner survives game cleanup and calls our Toggle() method directly.
            // Close on Escape or game cancel (Steam Input B / UICancel) when visible.
            // Do not Harmony-block UICancel globally — B/cancel should close the vault.

            if (_isVisible && (Input.GetKeyDown(KeyCode.Escape) || IsGameCancelPressed()))
            {
                Hide();
            }
        }

        /// <summary>
        /// Tries Wish.PlayerInput / Unity cancel actions. Wrapped so missing methods or
        /// Rewired name changes never crash the vault UI.
        /// </summary>
        private static bool IsGameCancelPressed()
        {
            try
            {
                if (PlayerInput.GetButtonDown("UICancel"))
                    return true;
            }
            catch
            {
                // Method or action may not exist on this game build.
            }

            try
            {
                if (PlayerInput.GetButtonDown("Cancel"))
                    return true;
            }
            catch
            {
                // Method or action may not exist on this game build.
            }

            try
            {
                if (PlayerInput.GetButtonDown("Close"))
                    return true;
            }
            catch
            {
                // Method or action may not exist on this game build.
            }

            try
            {
                if (Input.GetButtonDown("Cancel"))
                    return true;
            }
            catch
            {
                // "Cancel" may be missing from the Unity input manager.
            }

            return false;
        }

        private void SetVaultStatus(string message, bool isError)
        {
            _vaultStatusMessage = message;
            _vaultStatusIsError = isError;
            _vaultStatusUntil = Time.unscaledTime + 8f;
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

        private Texture2D MakeGradientTex(int width, int height, Color top, Color bottom)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                Color color = Color.Lerp(bottom, top, (float)y / height);
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, color);
            }
            tex.Apply();
            return tex;
        }

        private Texture2D MakeRoundedTex(int width, int height, Color color, int radius)
        {
            var tex = new Texture2D(width, height);
            var transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Check corners
                    bool inCorner = false;
                    int dx = 0, dy = 0;

                    if (x < radius && y < radius) { dx = radius - x; dy = radius - y; inCorner = true; }
                    else if (x >= width - radius && y < radius) { dx = x - (width - radius - 1); dy = radius - y; inCorner = true; }
                    else if (x < radius && y >= height - radius) { dx = radius - x; dy = y - (height - radius - 1); inCorner = true; }
                    else if (x >= width - radius && y >= height - radius) { dx = x - (width - radius - 1); dy = y - (height - radius - 1); inCorner = true; }

                    if (inCorner && dx * dx + dy * dy > radius * radius)
                        tex.SetPixel(x, y, transparent);
                    else
                        tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
            return tex;
        }

        private static void DestroyTex(ref Texture2D tex)
        {
            if (tex != null)
            {
                UnityEngine.Object.Destroy(tex);
                tex = null;
            }
        }

        /// <summary>
        /// Destroy IMGUI textures from the previous style build so scale rebuilds do not leak native memory.
        /// </summary>
        private void DisposeGeneratedTextures()
        {
            DestroyTex(ref _windowBackground);
            DestroyTex(ref _rowBackground);
            DestroyTex(ref _rowAltBackground);
            DestroyTex(ref _solidWhite);
            DestroyTex(ref _buttonNormal);
            DestroyTex(ref _buttonHover);
            DestroyTex(ref _withdrawNormal);
            DestroyTex(ref _withdrawHover);
            DestroyTex(ref _tabNormal);
            DestroyTex(ref _tabSelected);
            DestroyTex(ref _headerBarBg);
            DestroyTex(ref _toggleOnBg);
            DestroyTex(ref _toggleOnHover);
            DestroyTex(ref _toggleOffBg);
            DestroyTex(ref _toggleOffHover);

            _windowStyle = null;
            _headerBarStyle = null;
            _headerStyle = null;
            _titleStyle = null;
            _buttonStyle = null;
            _withdrawButtonStyle = null;
            _labelStyle = null;
            _valueStyle = null;
            _categoryButtonStyle = null;
            _selectedCategoryStyle = null;
            _rowStyle = null;
            _closeButtonStyle = null;
            _textFieldStyle = null;
            _subtitleStyle = null;
            _emptyStyle = null;
            _toggleOnStyle = null;
            _toggleOffStyle = null;
            _iconFallbackStyle = null;
            _hintStyle = null;
            _selectedNameStyle = null;
            _amountLabelStyle = null;
            _statusOkStyle = null;
            _statusErrorStyle = null;
        }

        private void InitializeStyles()
        {
            if (!_stylesDirty && _windowStyle != null && _headerBarStyle != null)
                return;

            DisposeGeneratedTextures();

            // Create textures
            _windowBackground = MakeRoundedTex(64, 64, _windowBgColor, 8);
            _rowBackground = MakeTex(2, 2, _rowEvenColor);
            _rowAltBackground = MakeTex(2, 2, _rowOddColor);
            _solidWhite = MakeTex(2, 2, Color.white);
            _buttonNormal = MakeRoundedTex(32, 32, new Color(0.25f, 0.45f, 0.65f, 0.9f), 4);
            _buttonHover = MakeRoundedTex(32, 32, new Color(0.35f, 0.55f, 0.75f, 0.95f), 4);
            _withdrawNormal = MakeRoundedTex(32, 32, _withdrawColor, 4);
            _withdrawHover = MakeRoundedTex(32, 32, _withdrawHoverColor, 4);
            _tabNormal = MakeRoundedTex(32, 32, new Color(0.2f, 0.2f, 0.28f, 0.9f), 6);
            _tabSelected = MakeGradientTex(32, 32, _accentColor, new Color(0.3f, 0.5f, 0.7f));
            _headerBarBg = MakeTex(2, 2, new Color(0.14f, 0.18f, 0.26f, 0.98f));
            _headerBarStyle = new GUIStyle { normal = { background = _headerBarBg }, padding = new RectOffset(ScaledInt(12), ScaledInt(12), ScaledInt(10), ScaledInt(10)) };

            // Build from a blank style — cloning GUI.skin.window keeps 9-slice atlas UVs that
            // can render as a "zoomed text" overlay after tab layout changes (AZR-234).
            // Focused/onNormal must use the same owned bg; first tab click focuses the window.
            int border = Math.Min(ScaledInt(12), 16);
            _windowStyle = new GUIStyle
            {
                padding = new RectOffset(ScaledInt(15), ScaledInt(15), ScaledInt(10), ScaledInt(15)),
                border = new RectOffset(border, border, border, border),
                normal = { background = _windowBackground, textColor = _textColor },
                onNormal = { background = _windowBackground, textColor = _textColor },
                hover = { background = _windowBackground, textColor = _textColor },
                onHover = { background = _windowBackground, textColor = _textColor },
                active = { background = _windowBackground, textColor = _textColor },
                onActive = { background = _windowBackground, textColor = _textColor },
                focused = { background = _windowBackground, textColor = _textColor },
                onFocused = { background = _windowBackground, textColor = _textColor }
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(24),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _goldColor }
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(14),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _accentColor }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(ScaledInt(12), ScaledInt(12), ScaledInt(6), ScaledInt(6)),
                normal = { background = _buttonNormal, textColor = _textColor },
                hover = { background = _buttonHover, textColor = Color.white },
                active = { background = _buttonHover, textColor = Color.white }
            };

            _withdrawButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ScaledFont(11),
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(ScaledInt(4), ScaledInt(4), ScaledInt(4), ScaledInt(4)),
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _withdrawNormal, textColor = _textColor },
                hover = { background = _withdrawHover, textColor = Color.white },
                active = { background = _withdrawHover, textColor = Color.white }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(13),
                normal = { textColor = _textColor }
            };

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(16),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _goldColor }
            };

            // Tab buttons: own every interaction state. Cloning GUI.skin.button leaves focused/
            // onNormal on the skin atlas — same AZR-234 "zoomed text" failure mode as the window chrome.
            _categoryButtonStyle = new GUIStyle
            {
                fontSize = ScaledFont(12),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(14), ScaledInt(14), ScaledInt(8), ScaledInt(8)),
                margin = new RectOffset(ScaledInt(3), ScaledInt(3), 0, 0),
                border = new RectOffset(4, 4, 4, 4),
                normal = { background = _tabNormal, textColor = _textDimColor },
                onNormal = { background = _tabNormal, textColor = _textDimColor },
                hover = { background = _buttonHover, textColor = _textColor },
                onHover = { background = _buttonHover, textColor = _textColor },
                active = { background = _buttonHover, textColor = _textColor },
                onActive = { background = _buttonHover, textColor = _textColor },
                focused = { background = _tabNormal, textColor = _textDimColor },
                onFocused = { background = _tabNormal, textColor = _textDimColor }
            };

            _selectedCategoryStyle = new GUIStyle(_categoryButtonStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { background = _tabSelected, textColor = Color.white },
                onNormal = { background = _tabSelected, textColor = Color.white },
                hover = { background = _tabSelected, textColor = Color.white },
                onHover = { background = _tabSelected, textColor = Color.white },
                active = { background = _tabSelected, textColor = Color.white },
                onActive = { background = _tabSelected, textColor = Color.white },
                focused = { background = _tabSelected, textColor = Color.white },
                onFocused = { background = _tabSelected, textColor = Color.white }
            };

            _rowStyle = new GUIStyle()
            {
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(6), ScaledInt(6)),
                margin = new RectOffset(0, 0, ScaledInt(2), ScaledInt(2))
            };

            _closeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ScaledFont(13),
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(ScaledInt(20), ScaledInt(20), ScaledInt(10), ScaledInt(10)),
                normal = { background = _buttonNormal, textColor = _textColor },
                hover = { background = _buttonHover, textColor = Color.white }
            };

            _textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = ScaledFont(13),
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(ScaledInt(6), ScaledInt(6), ScaledInt(4), ScaledInt(4)),
                normal = { textColor = _textColor }
            };

            // Cached styles for DrawWindow subtitle
            _subtitleStyle = new GUIStyle(_labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = ScaledFont(11)
            };
            _subtitleStyle.normal.textColor = _textDimColor;

            // Cached style for empty category message
            _emptyStyle = new GUIStyle(_labelStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };
            _emptyStyle.normal.textColor = _textDimColor;

            // Cached toggle button textures (created once, not every frame)
            _toggleOnBg = MakeTex(1, 1, new Color(0.2f, 0.5f, 0.2f, 0.9f));
            _toggleOnHover = MakeTex(1, 1, new Color(0.25f, 0.6f, 0.25f, 1f));
            _toggleOffBg = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.35f, 0.9f));
            _toggleOffHover = MakeTex(1, 1, new Color(0.4f, 0.4f, 0.45f, 1f));

            // Cached toggle button styles
            _toggleOnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ScaledFont(9),
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(1, 1, 1, 1),
                normal = { background = _toggleOnBg, textColor = new Color(0.5f, 1f, 0.5f) },
                hover = { background = _toggleOnHover, textColor = Color.white },
                active = { background = _toggleOnHover, textColor = Color.white }
            };

            _toggleOffStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ScaledFont(9),
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(1, 1, 1, 1),
                normal = { background = _toggleOffBg, textColor = new Color(0.6f, 0.6f, 0.6f) },
                hover = { background = _toggleOffHover, textColor = Color.white },
                active = { background = _toggleOffHover, textColor = Color.white }
            };

            // Cached icon fallback style
            _iconFallbackStyle = new GUIStyle(_labelStyle)
            {
                fontSize = ScaledFont(10),
                alignment = TextAnchor.MiddleCenter
            };
            _iconFallbackStyle.normal.textColor = _accentColor;

            // Cached hint style for controls
            _hintStyle = new GUIStyle(_labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };
            _hintStyle.normal.textColor = _textDimColor;

            // Cached selected name style
            _selectedNameStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold
            };
            _selectedNameStyle.normal.textColor = _accentColor;

            // Cached amount label style
            _amountLabelStyle = new GUIStyle(_labelStyle);
            _amountLabelStyle.normal.textColor = _textDimColor;

            _statusErrorStyle = new GUIStyle(_hintStyle)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _statusErrorStyle.normal.textColor = new Color(1f, 0.42f, 0.42f);

            _statusOkStyle = new GUIStyle(_hintStyle)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _statusOkStyle.normal.textColor = new Color(0.5f, 0.95f, 0.65f);

            _stylesDirty = false;
        }

        private void OnGUI()
        {
            // Don't show UI until vault is loaded for the current character
            if (!_isVisible || _vaultManager == null || !PlayerPatches.IsVaultLoaded) return;

            if (_stylesDirty || _windowStyle == null || _headerBarStyle == null)
            {
                InitializeStyles();
                if (_windowStyle == null || _headerBarStyle == null)
                    return;
            }

            // Compute content-reactive height
            float contentHeight = GetContentHeight();
            float minH = Scaled(320f);
            float totalHeight = Mathf.Clamp(HeaderHeight + contentHeight + FooterHeight, minH, Screen.height - Scaled(60f));
            _windowRect.width = WindowWidth;
            _windowRect.height = totalHeight;
            // Keep window on screen when resizing
            if (_windowRect.yMax > Screen.height - Scaled(20f)) _windowRect.y = Screen.height - totalHeight - Scaled(20f);
            if (_windowRect.y < Scaled(10f)) _windowRect.y = Scaled(10f);
            if (_windowRect.xMax > Screen.width - Scaled(10f)) _windowRect.x = Screen.width - WindowWidth - Scaled(10f);
            if (_windowRect.x < Scaled(10f)) _windowRect.x = Scaled(10f);

            // Draw shadow/backdrop with a private solid texture (never Texture2D.whiteTexture).
            Color prev = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.3f);
            if (_solidWhite != null)
                GUI.DrawTexture(new Rect(_windowRect.x + Scaled(4), _windowRect.y + Scaled(4), _windowRect.width, _windowRect.height), _solidWhite);
            GUI.color = prev;

            _windowRect = GUI.Window(
                GetHashCode(),
                _windowRect,
                DrawWindow,
                "",
                _windowStyle
            );

            VaultPointerGuard.Sync(_windowRect, true);
        }

        private void DrawWindow(int windowId)
        {
            if (!Plugin.GetConfigDebugFullVaultInspector())
                _showDebugVaultDump = false;

            // --- Main UI header: always visible (including when Settings is open) ---
            GUILayout.BeginVertical(_headerBarStyle, GUILayout.MinHeight(Scaled(100)));
            {
                GUILayout.Space(Scaled(4));
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(ModLocalization.T("vault.title"), _titleStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(Scaled(2));
                GUILayout.Label(ModLocalization.T("vault.subtitle"), _subtitleStyle);
                GUILayout.Space(Scaled(10));
                DrawCategoryTabs();
                GUILayout.Space(Scaled(4));
            }
            GUILayout.EndVertical();

            DrawHorizontalLine(_accentColor, 1);
            GUILayout.Space(Scaled(6));

            if (_showSettings)
            {
                DrawSettingsPanel();
            }
            else if (_showDebugVaultDump)
            {
                float scrollHeight = GetContentHeight();
                _debugVaultScroll = GUILayout.BeginScrollView(_debugVaultScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(scrollHeight));
                DrawDebugVaultDumpContent();
                GUILayout.EndScrollView();

                GUILayout.Space(Scaled(8));
                DrawHorizontalLine(_accentColor, 1);
                GUILayout.Space(Scaled(8));
                GUILayout.Label(ModLocalization.T("vault.debug.disabledHint"), _hintStyle);
            }
            else
            {
                float scrollHeight = GetContentHeight();
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(scrollHeight));
                DrawCurrencyList();
                GUILayout.EndScrollView();

                GUILayout.Space(Scaled(8));
                DrawHorizontalLine(_accentColor, 1);
                GUILayout.Space(Scaled(8));

                // Deposit/Withdraw controls
                DrawControls();
            }

            // Bottom: Close button only (Settings is in the tab bar)
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(ModLocalization.T("vault.close"), _closeButtonStyle, GUILayout.Width(Scaled(120)), GUILayout.Height(Scaled(35))))
            {
                Hide();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Make window draggable from header area (covers title + tabs)
            GUI.DragWindow(new Rect(0, 0, WindowWidth, Scaled(108)));
        }

        /// <summary>
        /// Returns content area height based on current mode (settings vs currency list) and item count.
        /// </summary>
        private float GetContentHeight()
        {
            if (_showSettings) return SettingsContentHeight;
            if (_showDebugVaultDump) return MaxContentHeight;
            var currencies = GetCurrenciesForCategory(_selectedCategory);
            int count = currencies.Count;
            if (count == 0) return MinContentHeight;
            float desired = count * RowHeight + Scaled(8f); // +padding
            return Mathf.Clamp(desired, MinContentHeight, MaxContentHeight);
        }

        private void DrawHorizontalLine(Color color, float height)
        {
            var rect = GUILayoutUtility.GetRect(1, height, GUILayout.ExpandWidth(true));
            if (_solidWhite == null) return;
            Color prev = GUI.color;
            GUI.color = color * 0.6f;
            GUI.DrawTexture(rect, _solidWhite);
            GUI.color = prev;
        }

        // Only show these categories in the UI
        private static readonly CurrencyCategory[] _enabledCategories = new[]
        {
            CurrencyCategory.SeasonalToken,
            CurrencyCategory.Key,
            CurrencyCategory.Special,
            CurrencyCategory.Custom
        };

        private void DrawCategoryTabs()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            foreach (CurrencyCategory category in _enabledCategories)
            {
                var style = !_showSettings && !_showDebugVaultDump && _selectedCategory == category ? _selectedCategoryStyle : _categoryButtonStyle;
                string icon = GetCategoryIcon(category);
                string label = $"{icon} {GetCategoryDisplayName(category)}";

                if (GUILayout.Button(label, style, GUILayout.MinWidth(88)))
                {
                    _showSettings = false;
                    _showDebugVaultDump = false;
                    _selectedCategory = category;
                    _selectedCurrencyId = "";
                }
            }

            if (Plugin.GetConfigDebugFullVaultInspector())
            {
                var dbgStyle = _showDebugVaultDump ? _selectedCategoryStyle : _categoryButtonStyle;
                if (GUILayout.Button(ModLocalization.T("vault.tab.debug"), dbgStyle, GUILayout.MinWidth(72)))
                {
                    _showSettings = false;
                    _showDebugVaultDump = true;
                    _selectedCurrencyId = "";
                }
            }

            // Settings tab - always visible as part of the Vault UI
            var settingsStyle = _showSettings && !_showDebugVaultDump ? _selectedCategoryStyle : _categoryButtonStyle;
            if (GUILayout.Button(ModLocalization.T("vault.tab.settings"), settingsStyle, GUILayout.MinWidth(88)))
            {
                _showSettings = true;
                _showDebugVaultDump = false;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private string GetCategoryIcon(CurrencyCategory category)
        {
            return category switch
            {
                CurrencyCategory.SeasonalToken => "\u2600", // Sun symbol
                CurrencyCategory.CommunityToken => "\u2665", // Heart
                CurrencyCategory.Key => "\u26bf", // Key-like symbol
                CurrencyCategory.Special => "\u2605", // Star symbol
                CurrencyCategory.Custom => "\u2606", // Open star (distinct from Special \u2605)
                _ => "\u2022"
            };
        }

        private string GetCategoryDisplayName(CurrencyCategory category)
        {
            return category switch
            {
                CurrencyCategory.SeasonalToken => "Seasonal",
                CurrencyCategory.CommunityToken => "Community",
                CurrencyCategory.Key => "Keys",
                CurrencyCategory.Special => "Special",
                CurrencyCategory.Custom => "Custom",
                _ => category.ToString()
            };
        }

        private int _rowIndex = 0;

        private void DrawDebugVaultDumpContent()
        {
            if (_vaultManager == null)
            {
                GUILayout.Label(ModLocalization.T("vault.notInitialized"), _emptyStyle);
                return;
            }

            GUILayout.Label(ModLocalization.T("vault.debug.intro"), _hintStyle);
            GUILayout.Space(Scaled(6));

            List<string> lines = _vaultManager.BuildDebugVaultReport();
            foreach (string line in lines)
                GUILayout.Label(line, _labelStyle);
        }

        private void DrawCurrencyList()
        {
            var currencies = GetCurrenciesForCategory(_selectedCategory);

            if (currencies.Count == 0)
            {
                GUILayout.Space(20);
                if (_selectedCategory == CurrencyCategory.Custom)
                {
                    GUILayout.Label(ModLocalization.T("vault.empty.custom"), _emptyStyle);
                    GUILayout.Space(6);
                    GUILayout.Label(ModLocalization.T("vault.hint.custom"), _hintStyle);
                    GUILayout.Space(4);
                    GUILayout.Label(ModLocalization.T("vault.hint.customId"), _hintStyle);
                }
                else if (_selectedCategory == CurrencyCategory.SeasonalToken)
                {
                    GUILayout.Label(ModLocalization.T("vault.empty.seasonal"), _emptyStyle);
                    GUILayout.Space(6);
                    GUILayout.Label(ModLocalization.T("vault.hint.seasonal"), _hintStyle);
                }
                else if (_selectedCategory == CurrencyCategory.Key)
                {
                    GUILayout.Label(ModLocalization.T("vault.empty.keys"), _emptyStyle);
                    GUILayout.Space(6);
                    GUILayout.Label(ModLocalization.T("vault.hint.keys"), _hintStyle);
                }
                else if (_selectedCategory == CurrencyCategory.Special)
                {
                    GUILayout.Label(ModLocalization.T("vault.empty.special"), _emptyStyle);
                    GUILayout.Space(6);
                    GUILayout.Label(ModLocalization.T("vault.hint.special"), _hintStyle);
                }
                else
                {
                    GUILayout.Label(ModLocalization.T("vault.empty.category"), _emptyStyle);
                }
                return;
            }

            _rowIndex = 0;
            foreach (var kvp in currencies)
            {
                DrawCurrencyRow(kvp.Key, kvp.Value, _rowIndex % 2 == 0);
                _rowIndex++;
            }
        }

        private Dictionary<string, int> GetCurrenciesForCategory(CurrencyCategory category) =>
            VaultUiShared.GetCurrenciesForCategory(_vaultManager, category);

        private void DrawCurrencyRow(string currencyId, int amount, bool isEvenRow)
        {
            bool isSelected = _selectedCurrencyId == currencyId;
            bool autoDepositEnabled = ItemPatches.IsAutoDepositEnabled(currencyId);

            // Row background — use owned textures, not Texture2D.whiteTexture (AZR-234).
            var rowRect = GUILayoutUtility.GetRect(0, RowHeight, GUILayout.ExpandWidth(true));
            Texture2D rowTex = isSelected
                ? _solidWhite
                : (isEvenRow ? _rowBackground : _rowAltBackground);
            if (rowTex != null)
            {
                Color prev = GUI.color;
                GUI.color = isSelected ? new Color(0.3f, 0.5f, 0.7f, 0.5f) : Color.white;
                GUI.DrawTexture(rowRect, rowTex);
                GUI.color = prev;
            }

            // Draw content using absolute positioning within the row
            float labelH = Scaled(26f);
            float labelY = rowRect.y + (rowRect.height - labelH) / 2;
            float xPos = rowRect.x + Scaled(8);

            // Auto-deposit toggle — touch-sized (≥44px)
            float toggleSize = TouchSize(20f);
            float toggleBtnY = rowRect.y + (rowRect.height - toggleSize) / 2;
            var toggleStyle = autoDepositEnabled ? _toggleOnStyle : _toggleOffStyle;

            var toggleRect = new Rect(xPos, toggleBtnY, toggleSize, toggleSize);
            string toggleText = autoDepositEnabled ? "ON" : "--";
            if (GUI.Button(toggleRect, toggleText, toggleStyle))
            {
                ItemPatches.ToggleAutoDeposit(currencyId);
                Plugin.Log?.LogDebug($"[UI] Toggle clicked for {currencyId}");
            }
            xPos += toggleSize + Scaled(4);

            // Currency icon - use game icon if available, fallback to text
            float iconSize = Scaled(28f);
            float iconY = rowRect.y + (rowRect.height - iconSize) / 2;
            Texture2D iconTexture = IconCache.GetIconForCurrency(currencyId);
            if (iconTexture != null && IconCache.IsIconLoaded(currencyId))
            {
                // Draw the actual game icon
                GUI.DrawTexture(new Rect(xPos, iconY, iconSize, iconSize), iconTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                // Fallback to text icon while loading or if icon unavailable (using cached style)
                string icon = VaultUiShared.GetCurrencyIcon(currencyId);
                GUI.Label(new Rect(xPos, labelY, iconSize, labelH), icon, _iconFallbackStyle);
            }
            xPos += iconSize + Scaled(4);

            // Currency name (scaled; wider for long names like "King's Lost Mine Key")
            float nameW = Scaled(160f);
            string displayName = VaultUiShared.GetDisplayName(_vaultManager, currencyId);
            GUI.Label(new Rect(xPos, labelY, nameW, labelH), displayName, _labelStyle);
            xPos += nameW + Scaled(4);

            // Amount with gold color - make it prominent with "x" prefix
            // Use K/M formatting for large numbers
            float amountW = Scaled(70f);
            string amountText = "x" + VaultUiShared.FormatNumber(amount);
            var amountStyle = new GUIStyle(_valueStyle) { fontSize = ScaledFont(16), alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(xPos, labelY, amountW, labelH), amountText, amountStyle);

            // Quick withdraw buttons — touch-sized, positioned from the right
            float btnH = TouchSize(28f);
            float btnW1 = TouchSize(34f);
            float btnW5 = TouchSize(34f);
            float btnW10 = TouchSize(40f);
            float btnGap = Scaled(4f);
            float btnY = rowRect.y + (rowRect.height - btnH) / 2;
            float rightEdge = rowRect.x + rowRect.width - Scaled(8);

            var withdraw10Rect = new Rect(rightEdge - btnW10, btnY, btnW10, btnH);
            var withdraw5Rect = new Rect(withdraw10Rect.x - btnGap - btnW5, btnY, btnW5, btnH);
            var withdraw1Rect = new Rect(withdraw5Rect.x - btnGap - btnW1, btnY, btnW1, btnH);

            // -10 button (rightmost)
            GUI.enabled = amount >= 10;
            if (GUI.Button(withdraw10Rect, "-10", _withdrawButtonStyle))
            {
                WithdrawToInventory(currencyId, 10);
            }

            // -5 button
            GUI.enabled = amount >= 5;
            if (GUI.Button(withdraw5Rect, "-5", _withdrawButtonStyle))
            {
                WithdrawToInventory(currencyId, 5);
            }

            // -1 button
            GUI.enabled = amount >= 1;
            if (GUI.Button(withdraw1Rect, "-1", _withdrawButtonStyle))
            {
                WithdrawToInventory(currencyId, 1);
            }

            GUI.enabled = true;

            // Row selection on click (only if not clicking a button)
            // Hit-tests must use the same scaled rects as the buttons above.
            if (Event.current.type == UnityEngine.EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                bool overToggle = toggleRect.Contains(Event.current.mousePosition);
                bool overWithdraw1 = withdraw1Rect.Contains(Event.current.mousePosition);
                bool overWithdraw5 = withdraw5Rect.Contains(Event.current.mousePosition);
                bool overWithdraw10 = withdraw10Rect.Contains(Event.current.mousePosition);

                if (!overToggle && !overWithdraw1 && !overWithdraw5 && !overWithdraw10)
                {
                    _selectedCurrencyId = currencyId;
                }
            }
        }

        private void DrawSettingsPanel()
        {
            GUILayout.Space(Scaled(4));
            GUILayout.Label(ModLocalization.T("vault.settings.title"), _subtitleStyle);
            GUILayout.Space(Scaled(6));
            float settingsScrollHeight = Mathf.Min(SettingsContentHeight, MaxContentHeight);
            _settingsScroll = GUILayout.BeginScrollView(_settingsScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(settingsScrollHeight));

            // Window scale (slider edits raw config; effective scale may include resolution factor)
            GUILayout.Label(ModLocalization.T("vault.settings.display"), _labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("vault.settings.windowScale"), _labelStyle, GUILayout.Width(Scaled(90)));
            float winScaleRaw = Plugin.GetConfigWindowScaleRaw();
            float newWinScale = GUILayout.HorizontalSlider(winScaleRaw, 0.5f, 3f, GUILayout.Width(Scaled(120)));
            if (Math.Abs(newWinScale - winScaleRaw) > 0.01f) Plugin.SetConfigWindowScale(newWinScale);
            float effective = Plugin.GetConfigWindowScale();
            GUILayout.Label(
                Math.Abs(effective - newWinScale) > 0.02f ? $"{newWinScale:F1}→{effective:F1}" : $"{newWinScale:F1}",
                _labelStyle,
                GUILayout.Width(Scaled(56)));
            GUILayout.EndHorizontal();
            bool scaleWithRes = Plugin.GetConfigScaleWithResolution();
            bool newScaleWithRes = GUILayout.Toggle(scaleWithRes, " Scale with resolution (height/1080)", _labelStyle);
            if (newScaleWithRes != scaleWithRes) Plugin.SetConfigScaleWithResolution(newScaleWithRes);
            GUILayout.Space(Scaled(6));

            // HUD
            GUILayout.Label(ModLocalization.T("vault.settings.hud"), _labelStyle);
            GUILayout.Label(ModLocalization.T("vault.settings.hudDragHint"), _hintStyle);
            bool hudOn = Plugin.GetConfigHUDEnabled();
            bool newHudOn = GUILayout.Toggle(hudOn, " Show vault currency bar", _labelStyle);
            if (newHudOn != hudOn) Plugin.SetConfigHUDEnabled(newHudOn);

            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("vault.settings.hudScale"), _labelStyle, GUILayout.Width(Scaled(80)));
            float scale = Plugin.GetConfigHUDScale();
            float newScale = GUILayout.HorizontalSlider(scale, 0.5f, 3f, GUILayout.Width(Scaled(120)));
            if (Math.Abs(newScale - scale) > 0.01f) Plugin.SetConfigHUDScale(newScale);
            GUILayout.Label($"{newScale:F1}", _labelStyle, GUILayout.Width(Scaled(28)));
            GUILayout.EndHorizontal();

            GUILayout.Label(ModLocalization.T("vault.settings.hudDensity"), _labelStyle);
            string density = Plugin.GetConfigHudDensity();
            string[] densityOpts = { "Normal", "Compact", "Minimal" };
            int dIdx = Array.IndexOf(densityOpts, density);
            if (dIdx < 0) dIdx = 0;
            int newDIdx = GUILayout.SelectionGrid(dIdx, densityOpts, 3, _buttonStyle);
            if (newDIdx != dIdx) Plugin.SetConfigHudDensity(densityOpts[newDIdx]);

            bool hudCompactLegacy = Plugin.GetConfigHUDCompactMode();
            bool newHudCompactLegacy = GUILayout.Toggle(hudCompactLegacy, " Legacy: Compact when density is Normal ([HUD] CompactMode)", _labelStyle);
            if (newHudCompactLegacy != hudCompactLegacy) Plugin.SetConfigHUDCompactMode(newHudCompactLegacy);

            GUILayout.Space(Scaled(6));

            GUILayout.Label(ModLocalization.T("vault.settings.debug"), _labelStyle);
            bool dbgOn = Plugin.GetConfigDebugFullVaultInspector();
            GUILayout.Label(
                dbgOn
                    ? "ON — zeros visible in tabs, Debug tab available, HUD shows all slots."
                    : "OFF — enable in Haven Dev Tools (F11) → Azrael's Mods → The Vault, or set [The Vault] FullVaultInspector in HavenDevTools config.",
                _hintStyle);
            GUILayout.Space(Scaled(6));

            // Open vault keys
            GUILayout.Label(ModLocalization.T("vault.settings.openVault"), _labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("vault.settings.key"), _labelStyle, GUILayout.Width(Scaled(50)));
            int toggleIdx = IndexOfKey(Plugin.GetConfigToggleKey());
            int newToggleIdx = GUILayout.SelectionGrid(toggleIdx, SettingsKeyNames, 6, _buttonStyle);
            if (newToggleIdx != toggleIdx && newToggleIdx >= 0) Plugin.SetConfigToggleKey(SettingsKeyOptions[newToggleIdx]);
            GUILayout.EndHorizontal();

            bool reqCtrl = Plugin.GetConfigRequireCtrl();
            bool newReqCtrl = GUILayout.Toggle(reqCtrl, " Require Ctrl (e.g. Ctrl+V)", _labelStyle);
            if (newReqCtrl != reqCtrl) Plugin.SetConfigRequireCtrl(newReqCtrl);

            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLocalization.T("vault.settings.altKey"), _labelStyle, GUILayout.Width(Scaled(50)));
            int altIdx = IndexOfKey(Plugin.GetConfigAltToggleKey());
            int newAltIdx = GUILayout.SelectionGrid(altIdx, SettingsKeyNames, 6, _buttonStyle);
            if (newAltIdx != altIdx && newAltIdx >= 0) Plugin.SetConfigAltToggleKey(SettingsKeyOptions[newAltIdx]);
            GUILayout.EndHorizontal();

            bool ctrlOpen = Plugin.GetConfigControllerOpenEnabled();
            bool newCtrlOpen = GUILayout.Toggle(ctrlOpen, " Controller open (LB+Y)", _labelStyle);
            if (newCtrlOpen != ctrlOpen) Plugin.SetConfigControllerOpenEnabled(newCtrlOpen);
            GUILayout.Label(
                "Hold LB + press Y (Xbox/Steam Deck). Remap via [UI] ControllerOpenModifier / ControllerOpenButton in TheVault.cfg. F8 AltToggle still works for Steam Input keyboard layers.",
                _hintStyle);

            GUILayout.Space(Scaled(8));
            GUILayout.Label(ModLocalization.T("vault.settings.autoSave"), _hintStyle);
            GUILayout.EndScrollView();
        }

        private static int IndexOfKey(KeyCode k)
        {
            for (int i = 0; i < SettingsKeyOptions.Length; i++)
                if (SettingsKeyOptions[i] == k) return i;
            return 0;
        }

        private void DrawControls()
        {
            if (Plugin.VaultLoadFailedNoRecoverableData)
            {
                GUILayout.Label(
                    "Vault load failed — saves blocked to protect your .backup. Restore files from TheVault/Saves, or Start Fresh to keep this empty vault.",
                    _statusErrorStyle);
                GUILayout.Space(Scaled(4));
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Start Fresh (abandon backup)", _buttonStyle, GUILayout.Width(Scaled(280)), GUILayout.Height(Scaled(32))))
                {
                    Plugin.ConfirmStartFreshVault();
                    SetVaultStatus("Start Fresh confirmed — empty vault may now be saved. Your previous .backup will rotate on the next save.", true);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(Scaled(8));
            }

            if (!string.IsNullOrEmpty(_vaultStatusMessage))
            {
                var st = _vaultStatusIsError ? _statusErrorStyle : _statusOkStyle;
                GUILayout.Label(_vaultStatusMessage, st);
                GUILayout.Space(Scaled(4));
            }

            if (string.IsNullOrEmpty(_selectedCurrencyId))
            {
                GUILayout.Label(ModLocalization.T("vault.row.hint"), _hintStyle);
            }
            else
            {
                float ctrlH = Mathf.Max(44f, Scaled(32f));

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUILayout.Label($"\u25b6 {VaultUiShared.GetDisplayName(_vaultManager, _selectedCurrencyId)}", _selectedNameStyle);

                GUILayout.Space(Scaled(15));

                GUILayout.Label(ModLocalization.T("vault.qty"), _amountLabelStyle, GUILayout.Width(Scaled(30)), GUILayout.Height(ctrlH));
                _depositAmount = GUILayout.TextField(_depositAmount, 6, _textFieldStyle, GUILayout.Width(Scaled(70)), GUILayout.Height(ctrlH));

                GUILayout.Space(Scaled(8));

                if (GUILayout.Button(ModLocalization.T("vault.withdraw"), _buttonStyle, GUILayout.Width(Scaled(100)), GUILayout.Height(ctrlH)))
                {
                    if (!int.TryParse(_depositAmount, out int wAmt) || wAmt <= 0)
                        SetVaultStatus("Enter a positive number for quantity.", true);
                    else
                        WithdrawToInventory(_selectedCurrencyId, wAmt);
                }

                if (GUILayout.Button(ModLocalization.T("vault.deposit"), _buttonStyle, GUILayout.Width(Scaled(100)), GUILayout.Height(ctrlH)))
                {
                    if (!int.TryParse(_depositAmount, out int dAmt) || dAmt <= 0)
                        SetVaultStatus("Enter a positive number for quantity.", true);
                    else
                        DepositFromInventory(_selectedCurrencyId, dAmt);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                // Amount chips — Deck / trackpad friendly; avoid typing in the TextField.
                GUILayout.Space(Scaled(4));
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                float chipW = TouchSize(40f);
                float chipMaxW = TouchSize(56f);
                if (GUILayout.Button("1", _buttonStyle, GUILayout.Width(chipW), GUILayout.Height(ctrlH)))
                    _depositAmount = "1";
                if (GUILayout.Button("5", _buttonStyle, GUILayout.Width(chipW), GUILayout.Height(ctrlH)))
                    _depositAmount = "5";
                if (GUILayout.Button("10", _buttonStyle, GUILayout.Width(chipW), GUILayout.Height(ctrlH)))
                    _depositAmount = "10";
                if (GUILayout.Button("Max", _buttonStyle, GUILayout.Width(chipMaxW), GUILayout.Height(ctrlH)))
                {
                    int vaultAmt = _vaultManager != null ? _vaultManager.GetCurrency(_selectedCurrencyId) : 0;
                    _depositAmount = Mathf.Max(0, vaultAmt).ToString();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                int mapId = ItemPatches.GetItemForCurrency(_selectedCurrencyId);
                if (mapId > 0)
                {
                    int inBag = ItemPatches.GetRawInventoryCount(mapId);
                    GUILayout.Label(ModLocalization.T("vault.inBag", inBag), _hintStyle);
                }
                else
                {
                    GUILayout.Label(ModLocalization.T("vault.noDeposit"), _hintStyle);
                }
            }

            // Global sweep button to force auto-deposit from inventory
            GUILayout.Space(Scaled(6));
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(ModLocalization.T("vault.sweep"), _buttonStyle, GUILayout.Width(Scaled(260)), GUILayout.Height(Mathf.Max(44f, Scaled(32)))))
            {
                Plugin.Log?.LogDebug("[Vault UI] Sweep button clicked");
                ItemPatches.ForceAutoDepositAll();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void WithdrawToInventory(string currencyId, int amount) =>
            VaultInventoryOperations.WithdrawToInventory(_vaultManager, currencyId, amount, SetVaultStatus);

        private void DepositFromInventory(string currencyId, int amount) =>
            VaultInventoryOperations.DepositFromInventory(_vaultManager, currencyId, amount, SetVaultStatus);
    }
}
