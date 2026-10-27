using System;
using System.IO;
using UnityEngine;

namespace Kneel.Core
{
    // A single save slot stored as JSON at Application.persistentDataPath/save.json.
    public class JsonFileSaveService : ISaveService
    {
        private const string FileName = "save.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public bool HasSave()
        {
            return File.Exists(FilePath);
        }

        public SaveData Load()
        {
            if (!HasSave())
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't read the save at {FilePath}: {e.Message}");
                return null;
            }
        }

        public void Save(SaveData data)
        {
            data.savedAtUtc = DateTime.UtcNow.ToString("o");
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }

        public void DeleteSave()
        {
            if (HasSave())
            {
                File.Delete(FilePath);
            }
        }
    }
}
