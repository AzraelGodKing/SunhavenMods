using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HavensRespec.UI
{
    /// <summary>
    /// Procedural sprites and shared colours for Haven's Respec UI pieces.
    /// Nothing here depends on game assets — every Texture2D is generated at runtime so the mod
    /// ships as a single DLL.
    /// </summary>
    internal static class RespecStyle
    {
        // "Tomb ink on parchment" palette — slightly darker than Crop Optimizer so the reset UI
        // feels a bit more serious / weighty (this action is destructive).
        public static readonly Color Parchment = new Color(0.96f, 0.90f, 0.75f, 1f);
        public static readonly Color Wood = new Color(0.38f, 0.25f, 0.16f, 1f);
        public static readonly Color WoodShadow = new Color(0.22f, 0.14f, 0.09f, 1f);
        public static readonly Color AccentGold = new Color(0.95f, 0.76f, 0.32f, 1f);
        public static readonly Color Danger = new Color(0.78f, 0.28f, 0.22f, 1f);
        public static readonly Color DangerHover = new Color(0.92f, 0.36f, 0.28f, 1f);
        public static readonly Color DangerPressed = new Color(0.62f, 0.20f, 0.16f, 1f);
        public static readonly Color Neutral = new Color(0.50f, 0.38f, 0.22f, 1f);
        public static readonly Color NeutralHover = new Color(0.64f, 0.49f, 0.28f, 1f);
        public static readonly Color NeutralPressed = new Color(0.36f, 0.27f, 0.16f, 1f);

        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite SolidRounded(Color fill, Color border, int size = 24, int borderThickness = 2, int cornerRadius = 6)
        {
            string key = $"rounded|{fill}|{border}|{size}|{borderThickness}|{cornerRadius}";
            if (_cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var transparent = new Color(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = IsInsideRoundedRect(x, y, size, size, cornerRadius);
                    bool onBorder = inside && (!IsInsideRoundedRect(x, y, size, size, cornerRadius, inset: borderThickness));
                    if (!inside)
                        tex.SetPixel(x, y, transparent);
                    else if (onBorder)
                        tex.SetPixel(x, y, border);
                    else
                        tex.SetPixel(x, y, fill);
                }
            }
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>Solid 4×4 white tile. Handy for panels tinted via <see cref="Image.color"/>.</summary>
        public static Sprite Solid()
        {
            if (_cache.TryGetValue("solid", out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _cache["solid"] = sprite;
            return sprite;
        }

        private static bool IsInsideRoundedRect(int x, int y, int w, int h, int r, int inset = 0)
        {
            r = Mathf.Max(0, r - inset);
            int left = inset;
            int right = w - 1 - inset;
            int bottom = inset;
            int top = h - 1 - inset;
            if (x < left || x > right || y < bottom || y > top)
                return false;

            int cx = Mathf.Clamp(x, left + r, right - r);
            int cy = Mathf.Clamp(y, bottom + r, top - r);
            int dx = x - cx;
            int dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }
    }
}
