using System.Collections.Generic;
using SunhavenMods.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CropOptimizer.UI
{
    /// <summary>
    /// Sun Haven-styled hover tooltip card. Fixed-width (300px), auto-height based on how many
    /// stat rows the crop resolved. Pre-allocates every row at build time and shows/hides them
    /// per-frame — avoids layout-group churn and flicker.
    /// </summary>
    internal sealed class CropTooltipView
    {
        private const float PanelWidth = 320f;
        private const float BorderWidth = 3f;
        private const float RowHeight = 24f;
        private const float HeaderHeight = 32f;
        private const float StripeHeight = 3f;
        private const float PaddingTop = 0f;
        private const float PaddingSide = 14f;
        private const float PaddingBottom = 10f;
        private const float HeaderToStripeGap = 0f;
        private const float StripeToBodyGap = 8f;

        private readonly Transform _canvasRoot;
        private GameObject _panel;
        private RectTransform _panelRt;
        private Image _headerIcon;
        private TMP_Text _headerTitle;
        private TMP_Text _headerSubtitle;

        private Image _qualityStripe;

        private readonly List<Row> _rows = new List<Row>();
        private TMP_Text _extras;

        public CropTooltipView(Transform canvasRoot)
        {
            _canvasRoot = canvasRoot;
            Build();
        }

        public void SetVisible(bool visible)
        {
            if (_panel == null) Build();
            if (_panel != null) _panel.SetActive(visible);
        }

        /// <summary>Reposition the tooltip near a screen-space point.</summary>
        /// <remarks>
        /// The tooltip's anchor is top-left of the canvas with pivot (0,1), so `anchoredPosition`
        /// is measured as (+right, -down) from the canvas's top-left corner. We convert the
        /// cursor's canvas-local point (origin = canvas pivot, typically center) into that
        /// top-left space before clamping.
        /// </remarks>
        public void SetScreenPosition(Vector2 screenPoint)
        {
            if (_panelRt == null) return;
            var parent = (RectTransform)_panelRt.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, null, out Vector2 local))
                return;

            Rect canvasRect = parent.rect;
            float w = _panelRt.rect.width;
            float h = _panelRt.rect.height;

            // Cursor position in "from canvas top-left" coords.
            float fromLeft = local.x - canvasRect.xMin;
            float fromTop = local.y - canvasRect.yMax; // <= 0

            // Default: card below-right of the cursor. Flip left/up if it would overflow.
            const float offset = 18f;
            float ax = fromLeft + offset;
            if (ax + w > canvasRect.width) ax = fromLeft - offset - w;
            float ay = fromTop - offset;
            if (ay - h < -canvasRect.height) ay = fromTop + offset + h;

            ax = Mathf.Clamp(ax, 0f, Mathf.Max(0f, canvasRect.width - w));
            ay = Mathf.Clamp(ay, Mathf.Min(0f, h - canvasRect.height), 0f);

            _panelRt.anchoredPosition = new Vector2(ax, ay);
        }

        public void SetScale(float scale)
        {
            if (_panelRt == null) Build();
            if (_panelRt != null) _panelRt.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Fills in the tooltip content and relayouts the panel height.</summary>
        public void ApplyContent(TooltipContent c)
        {
            if (_panel == null) Build();

            _headerTitle.text = string.IsNullOrEmpty(c.Title) ? ModLocalization.T("crop.tooltip.crop") : c.Title;
            if (!string.IsNullOrEmpty(c.HeaderTag))
            {
                _headerSubtitle.gameObject.SetActive(true);
                _headerSubtitle.text = c.HeaderTag;
            }
            else
            {
                _headerSubtitle.gameObject.SetActive(false);
            }

            _qualityStripe.color = c.QualityColor;

            // Hide all rows first
            foreach (var r in _rows) r.Root.SetActive(false);

            // Row positions are measured from the top of the fill rect (anchor top-left, pivot top-left).
            // Rows live under fill, so y = -(header + stripe + stripeGap + rowIndex * rowHeight).
            int visible = 0;
            foreach (var spec in c.Rows)
            {
                if (visible >= _rows.Count) break;
                if (spec == null) continue;
                var row = _rows[visible++];
                row.Root.SetActive(true);
                row.Icon.sprite = UiStyle.GetIcon(spec.Icon);
                row.Icon.color = spec.IconColor;
                row.Text.text = spec.Text;
                row.Text.color = spec.TextColor.a == 0 ? UiStyle.TextDark : spec.TextColor;

                float y = -(HeaderHeight + StripeHeight + StripeToBodyGap + (visible - 1) * RowHeight);
                var rt = (RectTransform)row.Root.transform;
                rt.anchoredPosition = new Vector2(PaddingSide, y);
            }

            float contentWidth = PanelWidth - BorderWidth * 2 - PaddingSide * 2;
            float extrasHeight = 0f;
            if (!string.IsNullOrWhiteSpace(c.Extras))
            {
                _extras.gameObject.SetActive(true);
                _extras.text = c.Extras;
                _extras.ForceMeshUpdate();
                var extrasRt = _extras.rectTransform;
                extrasRt.sizeDelta = new Vector2(contentWidth, 0f);
                _extras.ForceMeshUpdate();
                extrasHeight = Mathf.Max(14f, _extras.preferredHeight) + 4f;
                float y = -(HeaderHeight + StripeHeight + StripeToBodyGap + visible * RowHeight + 4f);
                extrasRt.anchoredPosition = new Vector2(PaddingSide, y);
                extrasRt.sizeDelta = new Vector2(contentWidth, extrasHeight);
            }
            else
            {
                _extras.gameObject.SetActive(false);
            }

            // Total height = fill content + top/bottom borders.
            float fillHeight = HeaderHeight + StripeHeight + StripeToBodyGap
                             + visible * RowHeight + extrasHeight + PaddingBottom;
            float total = fillHeight + BorderWidth * 2;
            _panelRt.sizeDelta = new Vector2(PanelWidth, total);
        }

        // -------- Build --------

        private void Build()
        {
            if (_canvasRoot == null) return;

            // Outer = wood-colored border; Fill (inset) = near-black body.
            _panel = new GameObject("CropOptimizer_Tooltip", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvasRoot, false);
            _panelRt = (RectTransform)_panel.transform;
            _panelRt.anchorMin = new Vector2(0f, 1f);
            _panelRt.anchorMax = new Vector2(0f, 1f);
            _panelRt.pivot = new Vector2(0f, 1f);
            _panelRt.sizeDelta = new Vector2(PanelWidth, 120f);

            var border = _panel.GetComponent<Image>();
            border.sprite = UiStyle.SolidSprite;
            border.type = Image.Type.Simple;
            border.color = UiStyle.WoodBorder;
            border.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(_panel.transform, false);
            var fillRt = (RectTransform)fill.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(BorderWidth, BorderWidth);
            fillRt.offsetMax = new Vector2(-BorderWidth, -BorderWidth);
            var fillImg = fill.GetComponent<Image>();
            fillImg.sprite = UiStyle.SolidSprite;
            fillImg.color = UiStyle.PanelFill;
            fillImg.raycastTarget = false;

            // Header band
            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(fill.transform, false);
            var hrt = (RectTransform)header.transform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = Vector2.zero;
            hrt.sizeDelta = new Vector2(0f, HeaderHeight);

            var himg = header.GetComponent<Image>();
            himg.sprite = UiStyle.SolidSprite;
            himg.color = UiStyle.HeaderFill;
            himg.raycastTarget = false;

            _headerIcon = CreateIcon(header.transform, UiStyle.IconKind.Sprout, UiStyle.Sprout, 22f);
            var hirt = _headerIcon.rectTransform;
            hirt.anchorMin = new Vector2(0f, 0.5f);
            hirt.anchorMax = new Vector2(0f, 0.5f);
            hirt.pivot = new Vector2(0f, 0.5f);
            hirt.anchoredPosition = new Vector2(10f, 0f);

            _headerTitle = CreateText(header.transform, ModLocalization.T("crop.tooltip.crop"), 17f, FontStyles.Bold, UiStyle.HeaderGold, TextAlignmentOptions.MidlineLeft);
            var ttrt = _headerTitle.rectTransform;
            ttrt.anchorMin = new Vector2(0f, 0f); ttrt.anchorMax = new Vector2(1f, 1f);
            ttrt.offsetMin = new Vector2(38f, 0f); ttrt.offsetMax = new Vector2(-90f, 0f);

            _headerSubtitle = CreateText(header.transform, "", 11f, FontStyles.Italic, UiStyle.ParchmentLight, TextAlignmentOptions.MidlineRight);
            var strt = _headerSubtitle.rectTransform;
            strt.anchorMin = new Vector2(1f, 0f); strt.anchorMax = new Vector2(1f, 1f);
            strt.pivot = new Vector2(1f, 0.5f);
            strt.offsetMin = new Vector2(-90f, 0f); strt.offsetMax = new Vector2(-10f, 0f);
            strt.sizeDelta = new Vector2(80f, 0f);

            // Quality stripe sits flush under the header, full width of the fill.
            var stripe = new GameObject("QualityStripe", typeof(RectTransform), typeof(Image));
            stripe.transform.SetParent(fill.transform, false);
            var qrt = (RectTransform)stripe.transform;
            qrt.anchorMin = new Vector2(0f, 1f);
            qrt.anchorMax = new Vector2(1f, 1f);
            qrt.pivot = new Vector2(0.5f, 1f);
            qrt.anchoredPosition = new Vector2(0f, -HeaderHeight);
            qrt.sizeDelta = new Vector2(0f, StripeHeight);
            _qualityStripe = stripe.GetComponent<Image>();
            _qualityStripe.sprite = UiStyle.SolidSprite;
            _qualityStripe.color = UiStyle.QualityNormal;
            _qualityStripe.raycastTarget = false;

            // Pre-allocate 8 rows — tooltip rarely needs more.
            for (int i = 0; i < 8; i++)
                _rows.Add(BuildRow(fill.transform, i));

            // Extras text (italic, muted cream, word-wrapped).
            _extras = CreateText(fill.transform, "", 11f, FontStyles.Italic, UiStyle.TextSub, TextAlignmentOptions.TopLeft);
            _extras.enableWordWrapping = true;
            _extras.overflowMode = TextOverflowModes.Overflow;
            var ert = _extras.rectTransform;
            ert.anchorMin = new Vector2(0f, 1f);
            ert.anchorMax = new Vector2(0f, 1f);
            ert.pivot = new Vector2(0f, 1f);
            ert.sizeDelta = new Vector2(PanelWidth - BorderWidth * 2 - PaddingSide * 2, 0f);
            _extras.gameObject.SetActive(false);
        }

        private Row BuildRow(Transform parent, int _)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(PanelWidth - BorderWidth * 2 - PaddingSide * 2, RowHeight);

            var ic = CreateIcon(go.transform, UiStyle.IconKind.Sprout, UiStyle.Sprout, 18f);
            var irt = ic.rectTransform;
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);

            var tx = CreateText(go.transform, "", 15f, FontStyles.Normal, UiStyle.TextDark, TextAlignmentOptions.MidlineLeft);
            var txRt = tx.rectTransform;
            txRt.anchorMin = new Vector2(0f, 0f);
            txRt.anchorMax = new Vector2(1f, 1f);
            txRt.offsetMin = new Vector2(26f, 0f);
            txRt.offsetMax = new Vector2(-2f, 0f);

            go.SetActive(false);
            return new Row { Root = go, Icon = ic, Text = tx };
        }

        private static Image CreateIcon(Transform parent, UiStyle.IconKind kind, Color32 color, float size)
        {
            var go = new GameObject($"Icon_{kind}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = UiStyle.GetIcon(kind);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text CreateText(Transform parent, string text, float size, FontStyles style, Color32 color, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.richText = true;
            if (UiStyle.Font != null) t.font = UiStyle.Font;
            return t;
        }

        private sealed class Row
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text Text;
        }
    }

    /// <summary>Descriptor passed to <see cref="CropTooltipView.ApplyContent"/>; decouples the view from the data source.</summary>
    internal sealed class TooltipContent
    {
        public string Title;
        public string HeaderTag;
        public Color32 QualityColor = UiStyle.QualityNormal;
        public List<RowSpec> Rows = new List<RowSpec>();
        public string Extras;
    }

    internal sealed class RowSpec
    {
        public UiStyle.IconKind Icon;
        public Color32 IconColor;
        public string Text;
        public Color32 TextColor;

        public static RowSpec Make(UiStyle.IconKind icon, Color32 iconColor, string text, Color32 textColor = default)
            => new RowSpec { Icon = icon, IconColor = iconColor, Text = text, TextColor = textColor };
    }
}
