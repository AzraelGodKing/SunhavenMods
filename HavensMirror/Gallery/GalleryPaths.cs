using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HavensMirror.Config;
using SunhavenMods.Shared;

namespace HavensMirror.Gallery
{
    /// <summary>
    /// Resolves gallery root and per-character folders under the plugin directory.
    /// </summary>
    public static class GalleryPaths
    {
        public static string PluginDirectory
        {
            get
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                return string.IsNullOrEmpty(loc)
                    ? "."
                    : Path.GetDirectoryName(loc) ?? ".";
            }
        }

        public static string GalleryRoot
        {
            get
            {
                string folder = MirrorOptions.GalleryFolderName?.Value;
                if (string.IsNullOrWhiteSpace(folder))
                    folder = "gallery";
                return Path.Combine(PluginDirectory, folder.Trim());
            }
        }

        public static string SharedFolderName
        {
            get
            {
                string name = MirrorOptions.SharedFolderName?.Value;
                return string.IsNullOrWhiteSpace(name) ? "_shared" : name.Trim();
            }
        }

        public static string CharacterFolder(string characterName)
        {
            string safe = SanitizeFolderName(characterName);
            return Path.Combine(GalleryRoot, safe);
        }

        public static string SharedFolder => Path.Combine(GalleryRoot, SharedFolderName);

        public static string FileNameFor(LookSlot slot)
        {
            switch (slot)
            {
                case LookSlot.Summer: return "summer.png";
                case LookSlot.Autumn: return "autumn.png";
                case LookSlot.Winter: return "winter.png";
                case LookSlot.Vows: return "vows.png";
                case LookSlot.Shore: return "shore.png";
                case LookSlot.Costume: return "costume.png";
                default: return "spring.png";
            }
        }

        /// <summary>
        /// Suite-consistent filename sanitization (matches CharacterSaveStore / other mods).
        /// </summary>
        public static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return SharedFolderName;

            string cleaned = CharacterSaveStore.SanitizeFileName(name.Trim(), SharedFolderName);
            return string.IsNullOrEmpty(cleaned) ? SharedFolderName : cleaned;
        }

        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };

        /// <summary>
        /// Image files in <paramref name="folder"/> sorted by name. Accepts .png / .jpg / .jpeg in any case.
        /// </summary>
        public static List<string> ListImageFiles(string folder)
        {
            var files = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return files;

            try
            {
                foreach (string path in Directory.GetFiles(folder))
                {
                    if (HasImageExtension(path))
                        files.Add(path);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[Gallery] Could not list '{folder}': {ex.Message}");
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        /// <summary>
        /// Find the file for <paramref name="slot"/> among <paramref name="files"/>, tolerant of case
        /// (Spring.PNG), doubled extensions from Windows hiding "known extensions" (spring.png.png),
        /// and JPEGs (spring.jpg). Returns null when nothing matches.
        /// </summary>
        public static string FindSlotFile(List<string> files, LookSlot slot)
        {
            if (files == null)
                return null;

            string wanted = Path.GetFileNameWithoutExtension(FileNameFor(slot));
            foreach (string path in files)
            {
                if (string.Equals(StripImageExtensions(Path.GetFileName(path)), wanted, StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            return null;
        }

        /// <summary>Remove every trailing image extension: "spring.png.png" → "spring".</summary>
        public static string StripImageExtensions(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            string name = fileName.Trim();
            bool stripped = true;
            while (stripped)
            {
                stripped = false;
                foreach (string ext in ImageExtensions)
                {
                    if (name.Length > ext.Length && name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        name = name.Substring(0, name.Length - ext.Length).TrimEnd();
                        stripped = true;
                    }
                }
            }

            return name;
        }

        private static bool HasImageExtension(string path)
        {
            foreach (string ext in ImageExtensions)
            {
                if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True when the folder exists and contains at least one image file.
        /// Empty character folders (created from saves) are ignored for display.
        /// </summary>
        public static bool FolderHasPortraitFiles(string folder)
        {
            return ListImageFiles(folder).Count > 0;
        }

        public static void EnsureStarterLayout(Action<string> logInfo = null)
        {
            if (MirrorOptions.CreateStarterFolders == null || !MirrorOptions.CreateStarterFolders.Value)
                return;

            try
            {
                CharacterSaveStore.EnsureDirectory(GalleryRoot);
                CharacterSaveStore.EnsureDirectory(SharedFolder);
                string howto = Path.Combine(SharedFolder, "HOWTO.txt");
                File.WriteAllText(howto,
                    "Haven's Mirror\n" +
                    "==============\n" +
                    "\n" +
                    "What happens\n" +
                    "------------\n" +
                    "1. You install the mod.\n" +
                    "2. When the game runs, the mod reads your character saves and creates\n" +
                    "   an empty folder for each one under gallery/ (named like the character).\n" +
                    "3. Empty folders are fine — they are ignored until you add PNGs.\n" +
                    "4. Drop PNG bust images into a character folder (or into this _shared folder).\n" +
                    "5. Talk to someone — if that folder has images, your bust appears.\n" +
                    "\n" +
                    "Where images go\n" +
                    "---------------\n" +
                    "  gallery/<YourCharacterName>/   (created automatically from saves)\n" +
                    "  gallery/_shared/               (this folder — used if a character folder is empty)\n" +
                    "\n" +
                    "File names (PNG)\n" +
                    "----------------\n" +
                    "  spring.png    everyday / spring (default)\n" +
                    "  summer.png\n" +
                    "  autumn.png\n" +
                    "  winter.png\n" +
                    "  vows.png      wedding / ceremony\n" +
                    "  shore.png     swimsuit / beach\n" +
                    "  costume.png   halloween / costume\n" +
                    "\n" +
                    "You do not need every file. Missing names reuse the first image in that folder.\n" +
                    "If no file uses one of these names, the first image in the folder is used for every look.\n" +
                    "Any image size works (it is scaled to the NPC portrait height). .jpg also works.\n" +
                    "Tip: Windows may hide extensions, so 'spring.png' can really be 'spring.png.png'. That is fine too.\n" +
                    "\n" +
                    "Reload\n" +
                    "------\n" +
                    "After adding or changing images, press Ctrl+F8 in game (either Ctrl key; configurable).\n" +
                    "No restart needed. On some laptops F8 needs the Fn key.\n" +
                    "\n" +
                    "Still nothing? Open BepInEx/LogOutput.log and search for [Gallery]. It lists the exact\n" +
                    "folders the mod checked and the files it found there.\n");

                logInfo?.Invoke($"Gallery ready at '{GalleryRoot}'.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[Gallery] Could not create starter folders: {ex.Message}");
            }
        }
    }
}
