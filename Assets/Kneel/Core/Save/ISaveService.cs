using System;

namespace Kneel.Core
{
    // What the game remembers between sessions: the level to continue in, the playtime, the story flags, and the
    // state the player carried into that level. Fields added later are missing from older files; JsonUtility then
    // leaves them at their defaults, so every old save still loads (an old save has no carried state: entry.valid
    // is false, and the level starts with its own defaults, as it always did).
    [Serializable]
    public class SaveData
    {
        public string sceneName;
        public string savedAtUtc;
        public double playtimeSeconds;

        // Ash abilities used so far, which unlock the next ones (AshProgress), and the key hints already shown. The
        // meter itself isn't kept: it empties on death, and Continue starts it empty too.
        public int ashUses;
        public int ashHintsSeen;
        // One-time story beats already seen, and checkpoints reached (see SaveFlags).
        public System.Collections.Generic.List<string> flags = new System.Collections.Generic.List<string>();
        // The flag of the shrine rested at last ("L3.ShrineB"): a reload of its level puts the player there, not at
        // another shrine of that level rested at earlier. Empty in saves from before it existed.
        public string lastShrine;

        // What the player carried into entry.scene (the level transition writes it; a checkpoint that refills the
        // player, like L5's Last Shrine, updates it). Applied whenever that level is loaded from the save.
        public CarriedState entry = new CarriedState();

        // Level scenes renamed since a save could have been written: old name -> current name. (L1 lost its owner's
        // suffix when the levels became unowned, 2026-11-23.)
        private static readonly (string from, string to)[] RenamedScenes =
        {
            ("L1_BrokenLine_Playable_L", "L1_BrokenLine_Playable"),
        };

        // Points a save written under an old scene name at the scene's current name, so Continue still finds it.
        public SaveData Upgraded()
        {
            foreach ((string from, string to) in RenamedScenes)
            {
                if (sceneName == from)
                {
                    sceneName = to;
                }

                if (entry != null && entry.scene == from)
                {
                    entry.scene = to;
                }
            }

            return this;
        }
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

        // The project enters Play mode without a domain reload: a test that swapped the service never leaks into the
        // next session.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = new JsonFileSaveService();

        // Loads the save (or starts one), lets `change` edit it, and writes it back, so a partial save (the pause
        // menu's scene and playtime) never drops the fields it doesn't know about (flags, carried state).
        public static bool Update(Action<SaveData> change)
        {
            try
            {
                SaveData data = Current.Load() ?? new SaveData();
                data.flags ??= new System.Collections.Generic.List<string>();
                data.entry ??= new CarriedState();
                change(data);
                Current.Save(data);
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Save failed: " + e.Message);
                return false;
            }
        }
    }
}
