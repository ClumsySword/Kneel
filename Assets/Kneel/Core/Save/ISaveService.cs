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

        // One-time story beats already seen, and checkpoints reached (see SaveFlags).
        public System.Collections.Generic.List<string> flags = new System.Collections.Generic.List<string>();
    }

    // Named once-per-save facts: "L5.Reveal" (the throne hall's reveal played), "L5.MasterMet" (the boss's long intro
    // played), "L5.ShrineC" (rested at the Last Shrine), "GameCompleted". Written straight to the save.
    public static class SaveFlags
    {
        // For dev tools (MasterDevTool): while set, flags are read from and written to this set only, and the save
        // file is left alone, so a replayed cutscene doesn't change the real save. It lasts until Play mode ends.
        public static System.Collections.Generic.HashSet<string> Sandbox { get; private set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Sandbox = null;

        // Starts the sandbox from the save's flags (once; later calls keep it).
        public static void BeginSandbox()
        {
            if (Sandbox != null)
                return;
            SaveData data = SaveServices.Current.Load();
            Sandbox = data != null && data.flags != null
                ? new System.Collections.Generic.HashSet<string>(data.flags)
                : new System.Collections.Generic.HashSet<string>();
        }

        // Only in the sandbox: the save itself never forgets a flag.
        public static void SandboxClear(string flag) => Sandbox?.Remove(flag);

        public static bool Has(string flag)
        {
            if (Sandbox != null)
                return Sandbox.Contains(flag);
            SaveData data = SaveServices.Current.Load();
            return data != null && data.flags != null && data.flags.Contains(flag);
        }

        public static void Set(string flag)
        {
            if (Sandbox != null)
            {
                Sandbox.Add(flag);
                return;
            }

            SaveData data = SaveServices.Current.Load() ?? new SaveData
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
            };

            data.flags ??= new System.Collections.Generic.List<string>();
            if (data.flags.Contains(flag) == false)
            {
                data.flags.Add(flag);
                SaveServices.Current.Save(data);
            }
        }
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
