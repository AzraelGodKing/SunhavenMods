using UnityEngine;

namespace SunhavenMods.Shared
{
    /// <summary>
    /// Utility methods for creating and caching Texture2Ds.
    /// Remember to cache the result — don't call these every frame.
    /// </summary>
    public static class GUIStyleHelper
    {
        /// <summary>
        /// Creates a solid color Texture2D (1x1 pixel).
        /// </summary>
        public static Texture2D MakeSolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Creates a Texture2D with a specified width and height, filled with a solid color.
        /// </summary>
        public static Texture2D MakeSolidTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
