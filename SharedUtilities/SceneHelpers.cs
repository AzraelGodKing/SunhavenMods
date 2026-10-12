using UnityEngine.SceneManagement;

namespace SunhavenMods.Shared
{
    /// <summary>
    /// Utility methods for detecting Sun Haven game scenes.
    /// Consolidates scene detection logic used across all mods.
    /// </summary>
    public static class SceneHelpers
    {
        /// <summary>
        /// Known menu scene identifiers used in Sun Haven.
        /// </summary>
        private static readonly string[] MenuScenePatterns = { "menu", "title", "bootstrap" };

        /// <summary>
        /// Known specific menu scene names.
        /// </summary>
        private static readonly string[] ExactMenuScenes = { "MainMenu", "Bootstrap" };

        /// <summary>
        /// Checks if the given scene name represents a menu/non-game scene.
        /// </summary>
        /// <param name="sceneName">The scene name to check</param>
        /// <returns>True if this is a menu scene, false if it's a game scene</returns>
        public static bool IsMenuScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return true; // Assume menu if we can't determine

            // Check exact matches first
            foreach (var exact in ExactMenuScenes)
            {
                if (sceneName == exact)
                    return true;
            }

            // Check pattern matches (case-insensitive)
            string lower = sceneName.ToLowerInvariant();
            foreach (var pattern in MenuScenePatterns)
            {
                if (lower.Contains(pattern))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the current active scene is a menu scene.
        /// </summary>
        public static bool IsCurrentSceneMenu()
        {
            return IsMenuScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Checks if we're currently in an active game scene (not menu).
        /// </summary>
        public static bool IsInGame()
        {
            return !IsCurrentSceneMenu();
        }

        /// <summary>
        /// Gets the current scene name.
        /// </summary>
        public static string GetCurrentSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }

    }
}
