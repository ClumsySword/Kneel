using System;

namespace Kneel.Core
{
    // What the game remembers between sessions. Only enough for the menu's Continue and the pause menu's
    // playtime for now; the real save system adds to it (checkpoint shrine, health, ...).
    [Serializable]
    public class SaveData
    {
        public string sceneName;
        public string savedAtUtc;
        public double playtimeSeconds;
    }

    // The menu talks to saves only through this, so the JSON file below can be swapped for a real save system.
    public interface ISaveService
    {
        bool HasSave();

        // Null when there is no save or it can't be read.
        SaveData Load();

        void Save(SaveData data);

        void DeleteSave();
    }

    // Where the game gets its save service. Replace Current at startup to use another implementation.
    public static class SaveServices
    {
        public static ISaveService Current { get; set; } = new JsonFileSaveService();
    }
}
