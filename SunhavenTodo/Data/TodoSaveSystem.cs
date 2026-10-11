using System;
using System.IO;
using BepInEx;
using SunhavenMods.Shared;

namespace SunhavenTodo.Data
{
    public class TodoSaveSystem
    {
        private readonly TodoManager _manager;
        private readonly string _savePath;

        public TodoSaveSystem(TodoManager manager)
        {
            _manager = manager;
            _savePath = Path.Combine(Paths.ConfigPath, PluginInfo.PLUGIN_GUID);
            CharacterSaveStore.EnsureDirectory(_savePath);
        }

        private string GetSaveFilePath(string characterName) =>
            CharacterSaveStore.GetFilePath(_savePath, characterName, "_todos.json");

        public void Save()
        {
            var data = _manager.GetData();
            if (data == null || string.IsNullOrEmpty(data.CharacterName))
            {
                Plugin.Log?.LogWarning("Cannot save: No data or character name");
                return;
            }

            Plugin.Log?.LogInfo($"[Save] Saving {data.Items.Count} todo(s) for '{data.CharacterName}'");

            try
            {
                var json = SerializeToJson(data);
                var filePath = GetSaveFilePath(data.CharacterName);

                Plugin.Log?.LogDebug($"[Save] Writing to: {filePath} ({json.Length} chars)");

                if (!CharacterSaveStore.WriteAtomic(filePath, json))
                    throw new IOException("Atomic write failed");

                _manager.MarkClean();
                Plugin.Log?.LogInfo($"[Save] Saved successfully: {filePath}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[Save] Failed to save: {ex}");
            }
        }

        public TodoListData Load(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
            {
                Plugin.Log?.LogWarning("Cannot load: No character name");
                return null;
            }

            var filePath = GetSaveFilePath(characterName);
            var data = CharacterSaveStore.LoadWithBackup(
                filePath,
                TryDeserializePayload,
                out var source,
                onReadFailure: (p, ex) => Plugin.Log?.LogWarning($"Failed to read todo save '{p}': {ex.Message}"));

            if (data != null)
            {
                if (source == CharacterSaveSource.Backup)
                    Plugin.Log?.LogInfo($"Loaded from backup for {characterName}");

                Plugin.Log?.LogInfo($"Loaded todo list for {characterName}: {data.Items.Count} item(s)");
                return data;
            }

            if (CharacterSaveStore.GetAbsenceReason(filePath) == CharacterSaveAbsenceReason.FilesPresentButUnusable)
                Plugin.Log?.LogWarning($"Main and backup save files unusable for {characterName}");
            else
                Plugin.Log?.LogInfo($"No valid save file found for {characterName}, creating new todo list");

            return new TodoListData(characterName);
        }

        private TodoListData TryDeserializePayload(string json)
        {
            if (!CharacterSaveStore.LooksLikeJsonObject(json))
            {
                Plugin.Log?.LogWarning("Todo save file does not contain valid JSON");
                return null;
            }

            var data = DeserializeFromJson(json);
            if (data == null)
                Plugin.Log?.LogWarning("Failed to deserialize todo save file");

            return data;
        }

        #region JSON Serialization (manual — replaces unreliable JsonUtility)
        // Serialization logic lives in TodoJson.cs (no BepInEx dependency) so it
        // can be linked into the unit-test project without stubs.

        private static string SerializeToJson(TodoListData data) => TodoJson.Serialize(data);
        private static TodoListData DeserializeFromJson(string json) => TodoJson.Deserialize(json);

        #endregion
    }
}
