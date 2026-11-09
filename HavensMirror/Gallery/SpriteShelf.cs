using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HavensMirror.Gallery
{
    /// <summary>
    /// Loads and caches portrait sprites from a character (or shared) gallery folder.
    /// </summary>
    public sealed class SpriteShelf : IDisposable
    {
        private readonly Dictionary<LookSlot, Sprite> _sprites = new Dictionary<LookSlot, Sprite>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private string _loadedFrom;
        private string _loadedForCharacter;
        private bool _hasLoaded;

        public bool HasAny => _sprites.Count > 0;
        public string LoadedFrom => _loadedFrom;

        /// <summary>
        /// True when the shelf was last loaded for a different character than <paramref name="characterName"/>
        /// (e.g. it loaded on the title screen before a save was picked, or the player switched saves).
        /// </summary>
        public bool IsStaleFor(string characterName)
        {
            return !_hasLoaded
                   || !string.Equals(_loadedForCharacter ?? string.Empty, characterName ?? string.Empty, StringComparison.Ordinal);
        }

        public void Clear()
        {
            foreach (var pair in _sprites)
            {
                if (pair.Value != null)
                    UnityEngine.Object.Destroy(pair.Value);
            }

            _sprites.Clear();

            foreach (var tex in _textures)
            {
                if (tex != null)
                    UnityEngine.Object.Destroy(tex);
            }

            _textures.Clear();
            _loadedFrom = null;
            _hasLoaded = false;
            _loadedForCharacter = null;
        }

        public void Dispose() => Clear();

        /// <summary>
        /// Load portraits for the active character, falling back to the shared folder, then to images
        /// placed directly in the gallery folder. Character / shared folders are matched loosely
        /// (case, spaces, '_' and '-') and images in nested subfolders are found when the folder
        /// itself has none. Empty folders (created from saves with no PNGs yet) are skipped quietly.
        /// Returns a short status message for logs / notifications.
        /// </summary>
        public string LoadForCharacter(string characterName)
        {
            Clear();
            _hasLoaded = true;
            _loadedForCharacter = characterName;

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(characterName))
                candidates.Add(GalleryPaths.ResolveCharacterFolder(characterName));
            string shared = GalleryPaths.ResolveSharedFolder();
            if (!candidates.Contains(shared))
                candidates.Add(shared);
            // Last resort: images dropped straight into gallery/ (top level only, never other characters' folders).
            candidates.Add(GalleryPaths.GalleryRoot);

            var checkedSummary = new List<string>();
            foreach (string folder in candidates)
            {
                bool isRoot = string.Equals(folder, GalleryPaths.GalleryRoot, StringComparison.OrdinalIgnoreCase);
                List<string> files = isRoot
                    ? GalleryPaths.ListImageFiles(folder)
                    : GalleryPaths.ListImageFilesDeep(folder);
                checkedSummary.Add(DescribeFolder(folder, files));

                // Empty save-created folders are ignored — no error, no load attempt.
                if (files.Count == 0)
                    continue;

                Sprite first = null;
                foreach (LookSlot slot in Enum.GetValues(typeof(LookSlot)))
                {
                    string path = GalleryPaths.FindSlotFile(files, slot);
                    if (path == null)
                        continue;

                    Sprite sprite = TryLoadImage(path);
                    if (sprite == null)
                        continue;

                    _sprites[slot] = sprite;
                    if (first == null)
                        first = sprite;
                }

                // No file uses a known look name (e.g. "MyFarmer.png") — use the first image for every look.
                if (first == null)
                {
                    foreach (string path in files)
                    {
                        first = TryLoadImage(path);
                        if (first == null)
                            continue;

                        Plugin.Log?.LogInfo(
                            $"[Gallery] No file named spring/summer/autumn/winter/vows/shore/costume in '{folder}'; " +
                            $"using '{Path.GetFileName(path)}' for every look.");
                        break;
                    }
                }

                if (first == null)
                    continue;

                foreach (LookSlot slot in Enum.GetValues(typeof(LookSlot)))
                {
                    if (!_sprites.ContainsKey(slot))
                        _sprites[slot] = first;
                }

                _loadedFrom = folder;
                string source = files.Count > 0 ? Path.GetDirectoryName(files[0]) : folder;
                return string.Equals(source, folder, StringComparison.OrdinalIgnoreCase)
                    ? $"Loaded portraits from '{folder}'."
                    : $"Loaded portraits from '{source}' (subfolder of '{folder}').";
            }

            Plugin.Log?.LogInfo(
                $"[Gallery] No portrait images loaded for character '{characterName ?? "(none yet)"}'. Checked: " +
                string.Join("; ", checkedSummary));
            return "No portrait images yet (drop a PNG in gallery/<name>/ or gallery/_shared/, then press Ctrl+F8).";
        }

        private static string DescribeFolder(string folder, List<string> files)
        {
            if (!Directory.Exists(folder))
                return $"'{folder}' (missing)";
            if (files.Count == 0)
                return $"'{folder}' (no .png/.jpg files)";

            var names = new List<string>();
            foreach (string f in files)
                names.Add(Path.GetFileName(f));
            return $"'{folder}' ({string.Join(", ", names.ToArray())})";
        }

        public Sprite Get(LookSlot slot)
        {
            return _sprites.TryGetValue(slot, out var sprite) ? sprite : null;
        }

        private Sprite TryLoadImage(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                byte[] bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length == 0)
                    return null;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    Plugin.Log?.LogWarning(
                        $"[Gallery] Could not decode '{path}'. It must be a real PNG or JPG " +
                        "(a .webp/.avif renamed to .png will not load). Re-export or re-save it as PNG.");
                    return null;
                }

                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.name = Path.GetFileNameWithoutExtension(path);

                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);

                sprite.name = texture.name;
                _textures.Add(texture);
                Plugin.Log?.LogInfo($"[Gallery] Loaded {path} ({texture.width}x{texture.height}).");
                return sprite;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[Gallery] Error loading '{path}': {ex.Message}");
                return null;
            }
        }
    }
}
