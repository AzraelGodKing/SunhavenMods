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
    }
}
