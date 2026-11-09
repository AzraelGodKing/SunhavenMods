using System;
using System.Collections.Generic;
using SunhavenMods.Shared;
using TheVault.Patches;
using TheVault.Vault;
using UnityEngine;

namespace TheVault.UI
{
    /// <summary>HUD bar density (font/padding multiplier).</summary>
    public enum VaultHudDensity
    {
        Normal = 0,
        Compact = 1,
        Minimal = 2
    }

    public class VaultHUD : MonoBehaviour
    {
        private const int HudWindowId = 88420;

        private VaultManager _vaultManager;
        private bool _isEnabled = true;
        private HUDPosition _position = HUDPosition.TopLeft;
        /// <summary>When false, HUD xy follows <see cref="_position"/> each frame. When true, xy come from drag / saved pixels.</summary>
        private bool _useCustomHudPlacement;
        private Rect _windowRect;
        private float _layoutOuterW;
        private float _layoutOuterH;
        private float _hudDragBandHeight;
        private GUIStyle _hudWindowStyle;
        private Texture2D _clearWindowTex;

        /// <summary>Invoked when the player drags the HUD; Plugin persists X/Y to the config file.</summary>
        public Action<float, float> OnPositionChanged;

        private float _opacity = 0.95f;
        private float _scale = 1f;
        private VaultHudDensity _hudDensity = VaultHudDensity.Normal;

        private float _stylesForMul = -1f;
        private float _texturesForOpacity = -1f;
        private GUIStyle _bgStyle;
        private GUIStyle _borderStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _shortStyle;
        private Texture2D _bgTex;
        private Texture2D _borderTex;
        private Texture2D _shadowTex;
        private Texture2D _sepTex;

        private readonly Color _bgTop = new Color(0.11f, 0.13f, 0.18f, 1f);
        private readonly Color _bgBot = new Color(0.06f, 0.07f, 0.10f, 1f);
        private readonly Color _border = new Color(0.30f, 0.36f, 0.46f, 0.62f);
        private readonly Color _shadow = new Color(0f, 0f, 0f, 0.30f);
        private readonly Color _accent = new Color(0.45f, 0.72f, 0.98f, 1f);
        private readonly Color _value = new Color(0.96f, 0.86f, 0.48f, 1f);
        private readonly Color _short = new Color(0.88f, 0.9f, 0.96f, 1f);

        private const float ScreenInset = 12f;
        private const float MaxHeightFrac = 0.58f;
        private const float MaxWidthFrac = 0.90f;
        private const float MaxWidthPx = 1200f;

        private const float BasePad = 10f;
        private const float BaseGap = 12f;
        private const float BaseRowGap = 5f;
        private const float BaseIcon = 22f;
        private const float BaseGroupGap = 16f;

        private float _lastUpdateTime;
        private const float UpdateInterval = 0.5f;

        private bool _layoutDirty = true;
        private int _cacheFingerprint = 0;
        private int _lastScreenW;
        private int _lastScreenH;

        private static bool _loggedQuarantineHudOnce;

        private readonly Dictionary<string, int> _seasonal = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _keys = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _special = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _custom = new Dictionary<string, int>();

        private struct Cell
        {
            public bool Sep;
            public string Id;
            public int Amount;
        }

        private readonly List<Cell> _cells = new List<Cell>(64);
        private readonly List<List<Cell>> _rows = new List<List<Cell>>(10);
        private readonly Stack<List<Cell>> _rowListPool = new Stack<List<Cell>>();

        public enum HUDPosition
        {
            TopLeft,
            TopCenter,
            TopRight,
            BottomLeft,
            BottomCenter,
            BottomRight
        }

        public void Initialize(VaultManager vaultManager)
        {
            _vaultManager = vaultManager;
            Plugin.Log?.LogInfo("VaultHUD initialized");
        }

        public void SetEnabled(bool enabled) => _isEnabled = enabled;

        /// <summary>Updates anchor preset from config. Does not clear custom placement; use <see cref="ClearCustomHudPlacement"/> when the player changes [HUD] Position.</summary>
        public void SetPosition(HUDPosition position) => _position = position;

        public void ClearCustomHudPlacement() => _useCustomHudPlacement = false;

        /// <summary>Restore pixel position from config (both coordinates must be &gt;= 0).</summary>
        public void RestoreHudPixelPosition(float x, float y)
        {
            _windowRect.x = x;
            _windowRect.y = y;
            _useCustomHudPlacement = true;
        }
        public void SetScale(float scale) { _scale = Mathf.Clamp(scale, 0.5f, 3.0f); _stylesForMul = -1f; _layoutDirty = true; }
        public void SetHudDensity(VaultHudDensity density) { _hudDensity = density; _stylesForMul = -1f; _layoutDirty = true; }

        public bool IsEnabled => _isEnabled;
        public void Toggle() { _isEnabled = !_isEnabled; Plugin.Log?.LogInfo($"VaultHUD toggled: {(_isEnabled ? "ON" : "OFF")}"); }

        private void Update()
        {
            if (!_loggedQuarantineHudOnce &&
                (Plugin.LastVaultLoadQuarantinedCorruptFile || Plugin.VaultLoadFailedNoRecoverableData))
            {
                _loggedQuarantineHudOnce = true;
                Plugin.Log?.LogWarning(
                    Plugin.VaultLoadFailedNoRecoverableData
                        ? "[The Vault] Vault load recovered no data — saving is blocked until Start Fresh so a surviving .backup is not overwritten (see LogOutput / TheVault/Saves)."
                        : "[The Vault] A vault file was unreadable and was quarantined (see LogOutput / BepInEx). An empty vault is in use — check TheVault/Saves for .corrupt-*.bak");
            }

            if (!_isEnabled || _vaultManager == null) return;
            if (Time.time - _lastUpdateTime < UpdateInterval) return;
            RefreshCaches();
            _lastUpdateTime = Time.time;
        }

        private void RefreshCaches()
        {
            bool debugAll = Plugin.GetConfigDebugFullVaultInspector();
            int before = _cacheFingerprint;

            _seasonal.Clear();
            foreach (var id in VaultCurrencyIds.AllSeasonalFullIds)
            {
                int v = _vaultManager.GetCurrency(id);
                if (debugAll || v != 0) _seasonal[id] = v;
            }

            _keys.Clear();
            foreach (var id in VaultCurrencyIds.AllKeyFullIds)
            {
                int v = _vaultManager.GetCurrency(id);
                if (debugAll || v != 0) _keys[id] = v;
            }

            _special.Clear();
            foreach (var id in VaultCurrencyIds.AllSpecialFullIds)
            {
                int v = _vaultManager.GetCurrency(id);
                if (debugAll || v != 0) _special[id] = v;
            }

            _custom.Clear();
            if (debugAll)
                _vaultManager.FillDebugCustomHud(_custom);
            else
            {
                foreach (var def in _vaultManager.GetCurrenciesByCategory(CurrencyCategory.Custom))
                {
                    string full = VaultCurrencyIds.FullCustom(def.Id);
                    int v = _vaultManager.GetCurrency(full);
                    if (v != 0) _custom[full] = v;
                }
            }

            _cacheFingerprint = ComputeCacheFingerprint();
            if (before != _cacheFingerprint)
                _layoutDirty = true;
        }

        private int ComputeCacheFingerprint()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + HashCurrencyDict(_seasonal);
                hash = (hash * 31) + HashCurrencyDict(_keys);
                hash = (hash * 31) + HashCurrencyDict(_special);
                hash = (hash * 31) + HashCurrencyDict(_custom);
                return hash;
            }
        }

        private static int HashCurrencyDict(Dictionary<string, int> dict)
        {
            unchecked
            {
                int hash = dict.Count;
                foreach (var pair in dict)
                {
                    hash = (hash * 31) + pair.Key.GetHashCode();
                    hash = (hash * 31) + pair.Value;
                }
                return hash;
            }
        }

        private float DensityMul() => _hudDensity switch
        {
            VaultHudDensity.Minimal => 0.68f,
            VaultHudDensity.Compact => 0.82f,
            _ => 1f
        };

        private void EnsureStyles(float mul)
        {
            mul = Mathf.Max(0.01f, mul);

            bool needTextures = _bgTex == null || Mathf.Abs(_opacity - _texturesForOpacity) > 0.0001f;
            if (needTextures)
            {
                if (_bgTex != null) UnityEngine.Object.Destroy(_bgTex);
                if (_borderTex != null) UnityEngine.Object.Destroy(_borderTex);
                if (_shadowTex != null) UnityEngine.Object.Destroy(_shadowTex);
                if (_sepTex != null) UnityEngine.Object.Destroy(_sepTex);

                _bgTex = MakeVerticalGradientTex(8, 48, WithA(_bgTop, _opacity), WithA(_bgBot, _opacity));
                _borderTex = MakeTex(2, 2, WithA(_border, _opacity));
                _shadowTex = MakeTex(2, 2, WithA(_shadow, _opacity));
                _sepTex = MakeTex(1, 1, new Color(1f, 1f, 1f, 0.35f * _opacity));
                _texturesForOpacity = _opacity;
            }

            if (!needTextures && Mathf.Abs(mul - _stylesForMul) < 0.001f && _bgStyle != null)
                return;

            _stylesForMul = mul;

            int pad = Mathf.Max(2, Mathf.RoundToInt(BasePad * mul));
            int fontV = Mathf.Max(9, Mathf.RoundToInt(14 * mul));
            int fontS = Mathf.Max(8, Mathf.RoundToInt(13 * mul));

            if (_bgStyle == null) _bgStyle = new GUIStyle();
            _bgStyle.normal.background = _bgTex;
            _bgStyle.padding = new RectOffset(pad, pad, pad / 2, pad / 2);

            if (_borderStyle == null) _borderStyle = new GUIStyle();
            _borderStyle.normal.background = _borderTex;

            if (_valueStyle == null) _valueStyle = new GUIStyle(GUI.skin.label);
            _valueStyle.fontSize = fontV;
            _valueStyle.fontStyle = FontStyle.Bold;
            _valueStyle.alignment = TextAnchor.MiddleLeft;
            _valueStyle.normal.textColor = _value;

            if (_shortStyle == null) _shortStyle = new GUIStyle(GUI.skin.label);
            _shortStyle.fontSize = fontS;
            _shortStyle.fontStyle = FontStyle.Normal;
            _shortStyle.alignment = TextAnchor.MiddleLeft;
            _shortStyle.normal.textColor = _short;
        }

        private static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static Texture2D MakeTex(int w, int h, Color c)
        {
            var t = new Texture2D(w, h);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D MakeVerticalGradientTex(int w, int h, Color top, Color bot)
        {
            var t = new Texture2D(w, h);
            for (int y = 0; y < h; y++)
            {
                float k = h <= 1 ? 0f : y / (float)(h - 1);
                Color c = Color.Lerp(top, bot, k);
                for (int x = 0; x < w; x++) t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        private void BuildCells()
        {
            _cells.Clear();
            bool needSep = false;

            void AppendOrdered(string[] ordered, Dictionary<string, int> values)
            {
                bool any = false;
                foreach (var id in ordered)
                {
                    if (!values.TryGetValue(id, out int v)) continue;
                    if (needSep) { _cells.Add(new Cell { Sep = true }); needSep = false; }
                    _cells.Add(new Cell { Id = id, Amount = v });
                    any = true;
                }
                if (any) needSep = true;
            }

            AppendOrdered(VaultCurrencyIds.AllSeasonalFullIds, _seasonal);
            AppendOrdered(VaultCurrencyIds.AllKeyFullIds, _keys);
            AppendOrdered(VaultCurrencyIds.AllSpecialFullIds, _special);

            if (_custom.Count > 0)
            {
                var keys = new List<string>(_custom.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (var id in keys)
                {
                    if (needSep) { _cells.Add(new Cell { Sep = true }); needSep = false; }
                    _cells.Add(new Cell { Id = id, Amount = _custom[id] });
                }
            }

            if (_cells.Count > 0 && _cells[_cells.Count - 1].Sep)
                _cells.RemoveAt(_cells.Count - 1);
        }

        private float CellWidth(Cell c, float mul)
        {
            if (c.Sep) return Mathf.Max(6f, BaseGroupGap * mul);
            float icon = BaseIcon * mul;
            float valueW = _valueStyle.CalcSize(new GUIContent(FormatNumber(c.Amount))).x;
            float iconW = IconCache.IsIconLoaded(c.Id) ? icon : Mathf.Max(icon, _shortStyle.CalcSize(new GUIContent(GetShortName(c.Id))).x);
            return iconW + 3f * mul + valueW + 2f;
        }

        private List<Cell> RentRowList() => _rowListPool.Count > 0 ? _rowListPool.Pop() : new List<Cell>(16);

        private void ReleaseAllRowsToPool()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Clear();
                _rowListPool.Push(_rows[i]);
            }
            _rows.Clear();
        }

        private void WrapRows(float innerW, float mul)
        {
            ReleaseAllRowsToPool();
            if (_cells.Count == 0) return;

            float gap = BaseGap * mul;
            List<Cell> row = RentRowList();
            float w = 0f;

            void Flush()
            {
                if (row.Count == 0) return;
                _rows.Add(row);
                row = RentRowList();
                w = 0f;
            }

            for (int i = 0; i < _cells.Count; i++)
            {
                Cell c = _cells[i];
                float cw = CellWidth(c, mul);
                float add = (row.Count > 0 ? gap : 0f) + cw;
                if (row.Count > 0 && w + add > innerW + 0.5f)
                    Flush();
                if (row.Count > 0) w += gap;
                row.Add(c);
                w += cw;
            }
            Flush();
            if (row.Count == 0)
                _rowListPool.Push(row);
        }

        private float RowHeight(float mul)
        {
            float icon = BaseIcon * mul;
            float txt = _valueStyle.CalcSize(new GUIContent("0")).y;
            return Mathf.Max(icon + 2f, txt + 4f);
        }

        private Rect PlaceRect(float w, float h)
        {
            w = Mathf.Min(w, Screen.width - 2f * ScreenInset);
            float x = ScreenInset, y = ScreenInset;
            switch (_position)
            {
                case HUDPosition.TopLeft: x = ScreenInset; y = ScreenInset; break;
                case HUDPosition.TopCenter: x = (Screen.width - w) / 2f; y = ScreenInset; break;
                case HUDPosition.TopRight: x = Screen.width - w - ScreenInset; y = ScreenInset; break;
                case HUDPosition.BottomLeft: x = ScreenInset; y = Screen.height - h - ScreenInset; break;
                case HUDPosition.BottomCenter: x = (Screen.width - w) / 2f; y = Screen.height - h - ScreenInset; break;
                case HUDPosition.BottomRight: x = Screen.width - w - ScreenInset; y = Screen.height - h - ScreenInset; break;
            }
            return new Rect(x, y, w, h);
        }

        private void EnsureHudWindowStyle()
        {
            if (_hudWindowStyle != null) return;
            if (_clearWindowTex == null)
                _clearWindowTex = MakeTex(2, 2, new Color(0f, 0f, 0f, 0f));
            _hudWindowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = _clearWindowTex },
                onNormal = { background = _clearWindowTex },
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
        }

        private void OnGUI()
        {
            if (!_isEnabled || _vaultManager == null || !PlayerPatches.IsVaultLoaded) return;
            if (Plugin.IsMainVaultPanelVisible()) return;

            if (Time.time - _lastUpdateTime > UpdateInterval)
            {
                RefreshCaches();
                _lastUpdateTime = Time.time;
            }

            if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
            {
                _lastScreenW = Screen.width;
                _lastScreenH = Screen.height;
                _layoutDirty = true;
            }

            if (!_layoutDirty)
            {
                if (_cells.Count == 0)
                    return;

                DrawHudWindowFrame();
                return;
            }

            BuildCells();
            if (_cells.Count == 0) return;

            float density = DensityMul();
            float baseMul = _scale * density;

            float maxOuterW = Mathf.Min(Screen.width - 2f * ScreenInset, Screen.width * MaxWidthFrac, MaxWidthPx);
            float maxOuterH = Mathf.Max(80f, Screen.height * MaxHeightFrac - 2f * ScreenInset);

            float auto = 1f;
            float pad = BasePad * baseMul;
            float rowGap = BaseRowGap * baseMul;
            float outerW = 220f;
            float outerH = 40f;
            float innerW = 200f;
            float rh = 18f;

            for (int i = 0; i < 6; i++)
            {
                float mul = baseMul * auto;
                EnsureStyles(mul);
                pad = BasePad * mul;
                innerW = Mathf.Max(80f, maxOuterW - 2f * pad);
                WrapRows(innerW, mul);
                rh = RowHeight(mul);
                float contentH = _rows.Count * rh + Mathf.Max(0, _rows.Count - 1) * rowGap;
                outerH = Mathf.Max(28f * mul, contentH + 2f * pad + 4f);

                float widest = 0f;
                float gap = BaseGap * mul;
                foreach (var r in _rows)
                {
                    float wrow = 0f;
                    for (int ci = 0; ci < r.Count; ci++)
                    {
                        if (ci > 0) wrow += gap;
                        wrow += CellWidth(r[ci], mul);
                    }
                    widest = Mathf.Max(widest, wrow);
                }
                outerW = Mathf.Clamp(widest + 2f * pad, 100f, maxOuterW);

                float next = Mathf.Min(1f, (maxOuterH / Mathf.Max(1f, outerH)) * 0.99f, (maxOuterW / Mathf.Max(1f, outerW)) * 0.99f);
                if (Mathf.Abs(next - auto) < 0.01f) break;
                auto = next;
            }

            outerW = Mathf.Min(outerW, Screen.width - 2f * ScreenInset);
            _layoutOuterW = outerW;
            _layoutOuterH = outerH;

            float accentH = Mathf.Max(2f, 2.5f * _stylesForMul);
            _hudDragBandHeight = Mathf.Max(22f, pad + accentH + 8f);

            DrawHudWindowFrame();
            _layoutDirty = false;
        }

        private void DrawHudWindowFrame()
        {
            EnsureStyles(_scale * DensityMul());

            _windowRect.width = _layoutOuterW;
            _windowRect.height = _layoutOuterH;

            if (!_useCustomHudPlacement)
            {
                Rect placed = PlaceRect(_layoutOuterW, _layoutOuterH);
                _windowRect.x = placed.x;
                _windowRect.y = placed.y;
            }
            else
            {
                _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
                _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
            }

            EnsureHudWindowStyle();
            GUI.depth = -400;

            Rect prevRect = _windowRect;
            _windowRect = GUI.Window(HudWindowId, _windowRect, DrawHudWindow, "", _hudWindowStyle);
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));

            if (Math.Abs(_windowRect.x - prevRect.x) > 0.1f || Math.Abs(_windowRect.y - prevRect.y) > 0.1f)
            {
                _useCustomHudPlacement = true;
                OnPositionChanged?.Invoke(_windowRect.x, _windowRect.y);
            }
        }

        private void DrawHudWindow(int windowId)
        {
            float w = _layoutOuterW;
            float h = _layoutOuterH;
            float pad = BasePad * _stylesForMul;
            float accentH = Mathf.Max(2f, 2.5f * _stylesForMul);
            float rh = RowHeight(_stylesForMul);

            GUI.DrawTexture(new Rect(3f, 3f, w, h), _shadowTex, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(-1f, -1f, w + 2f, h + 2f), _borderTex, ScaleMode.StretchToFill);
            GUI.Box(new Rect(0f, 0f, w, h), GUIContent.none, _bgStyle);
            GUI.DrawTexture(new Rect(1f, 1f, w - 2f, accentH), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, WithA(_accent, _opacity * 0.85f), 0, 0);

            GUI.BeginGroup(new Rect(0f, 0f, w, h));
            float x0 = pad;
            float y = pad + accentH + 2f;
            float gapX = BaseGap * _stylesForMul;
            float icon = BaseIcon * _stylesForMul;

            for (int ri = 0; ri < _rows.Count; ri++)
            {
                if (ri > 0) y += BaseRowGap * _stylesForMul;
                float x = x0;
                var row = _rows[ri];
                for (int ci = 0; ci < row.Count; ci++)
                {
                    if (ci > 0) x += gapX;
                    Cell c = row[ci];
                    if (c.Sep)
                    {
                        float sw = Mathf.Max(6f, BaseGroupGap * _stylesForMul);
                        float cx = x + sw * 0.5f;
                        GUI.DrawTexture(new Rect(cx, y + 2f, 1f, icon - 4f), _sepTex);
                        x += sw;
                        continue;
                    }

                    Texture2D tex = IconCache.GetIconForCurrency(c.Id);
                    if (tex != null && IconCache.IsIconLoaded(c.Id))
                    {
                        GUI.DrawTexture(new Rect(x, y + 1f, icon, icon), tex, ScaleMode.ScaleToFit);
                        x += icon;
                    }
                    else
                    {
                        string sn = GetShortName(c.Id);
                        float sw = _shortStyle.CalcSize(new GUIContent(sn)).x;
                        GUI.Label(new Rect(x, y, Mathf.Max(icon, sw), rh), sn, _shortStyle);
                        x += Mathf.Max(icon, sw);
                    }

                    x += 3f * _stylesForMul;
                    string vt = FormatNumber(c.Amount);
                    float vw = _valueStyle.CalcSize(new GUIContent(vt)).x + 2f;
                    GUI.Label(new Rect(x, y, vw, rh), vt, _valueStyle);
                    x += vw;
                }
                y += rh;
            }

            GUI.EndGroup();

            GUI.DragWindow(new Rect(0f, 0f, w, _hudDragBandHeight));
        }

        private void OnDestroy()
        {
            if (_clearWindowTex != null)
            {
                Destroy(_clearWindowTex);
                _clearWindowTex = null;
            }
        }

        private static string FormatNumber(int value)
        {
            if (value >= 1_000_000)
            {
                float m = value / 1_000_000f;
                if (m >= 10f || m == (int)m) return $"{(int)m}M";
                return $"{m:0.#}M";
            }
            if (value >= 1_000)
            {
                float k = value / 1_000f;
                if (k >= 10f || k == (int)k) return $"{(int)k}K";
                return $"{k:0.#}K";
            }
            return value.ToString();
        }

        private static string GetShortName(string currencyId)
        {
            if (currencyId.StartsWith(VaultCurrencyIds.PrefixSeasonal))
            {
                string season = currencyId.Substring(VaultCurrencyIds.PrefixSeasonal.Length);
                return season switch { "Spring" => "Sp", "Summer" => "Su", "Fall" => "Fa", "Winter" => "Wi", _ => season.Substring(0, 2) };
            }
            if (currencyId.StartsWith(VaultCurrencyIds.PrefixKey))
            {
                string key = currencyId.Substring(VaultCurrencyIds.PrefixKey.Length);
                return key switch { "copper" => "Cu", "iron" => "Fe", "adamant" => "Ad", "mithril" => "Mi", "sunite" => "Su", "glorite" => "Gl", "kingslostmine" => "KL", _ => key.Substring(0, 2) };
            }
            if (currencyId.StartsWith(VaultCurrencyIds.PrefixSpecial))
            {
                string s = currencyId.Substring(VaultCurrencyIds.PrefixSpecial.Length);
                return s switch { "communitytoken" => "CT", "doubloon" => "Db", "blackbottlecap" => "BB", "redcarnivalticket" => "RC", "candycornpieces" => "CC", "manashard" => "MS", _ => s.Substring(0, 2).ToUpperInvariant() };
            }
            if (currencyId.StartsWith(VaultCurrencyIds.PrefixCustom))
            {
                string id = currencyId.Substring(VaultCurrencyIds.PrefixCustom.Length);
                if (id.Length <= 2) return id.ToUpperInvariant();
                return char.ToUpperInvariant(id[0]).ToString() + char.ToUpperInvariant(id[id.Length - 1]);
            }
            return "??";
        }
    }
}
