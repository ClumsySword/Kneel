using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kneel.Core
{
    // Kael's Ash between scenes. The meter (Ash) lives only in memory: it carries from one level to the next, and
    // empties on death (the player's AshMeter) and on Continue. The use count, which unlocks the abilities, is kept
    // in the save and only New Game resets it. A level picked from the menu's LEVELS screen is a practice run: it
    // gets a seeded use count, and saves made during it keep the campaign's own.
    public static class AshProgress
    {
        // Uses a practice run of each level starts with (L1 to L5), so the abilities a player would have by then are
        // in hand.
        private static readonly int[] PracticeUses = { 0, 0, 3, 10, 20 };

        private static int campaignUses;
        private static int campaignHintsSeen;

        public static float Ash { get; set; }

        public static int Uses { get; set; }

        // Bit per key hint already shown (AshPresenter), so each shows once per save.
        public static int HintsSeen { get; set; }

        public static bool Practice { get; private set; }

        // The F1 overlay's switch: the meter works in L1 too.
        public static bool DebugGrant { get; set; }

        // What a save records: the campaign's values, even during a practice run.
        public static int SavedUses => Practice ? campaignUses : Uses;

        public static int SavedHintsSeen => Practice ? campaignHintsSeen : HintsSeen;

        // Granted in Level 2. Scenes that aren't one of the five levels (test arenas) have it too.
        public static bool MeterGranted
        {
            get
            {
                int level = LevelCatalog.IndexOf(SceneManager.GetActiveScene().name);
                return DebugGrant || level != 0;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            Practice = false;
            DebugGrant = false;
            LoadFrom(SaveServices.Current.Load());
        }

        // Continue: the save's uses and hints, an empty meter.
        public static void LoadFrom(SaveData save)
        {
            Practice = false;
            Ash = 0f;
            Uses = save != null ? save.ashUses : 0;
            HintsSeen = save != null ? save.ashHintsSeen : 0;
        }

        public static void NewGame()
        {
            Practice = false;
            Ash = 0f;
            Uses = 0;
            HintsSeen = 0;
        }

        // A LEVELS jump. The campaign's values are put aside for any save made during it.
        public static void BeginPractice(string scene)
        {
            if (Practice == false)
            {
                campaignUses = Uses;
                campaignHintsSeen = HintsSeen;
            }

            int level = LevelCatalog.IndexOf(scene);
            Practice = true;
            Ash = 0f;
            Uses = level >= 0 && level < PracticeUses.Length ? PracticeUses[level] : 0;
            HintsSeen = ~0;
        }

        public static void EmptyMeter()
        {
            Ash = 0f;
        }
    }
}
