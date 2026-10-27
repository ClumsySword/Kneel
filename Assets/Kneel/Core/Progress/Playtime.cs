using System;
using UnityEngine;

namespace Kneel.Core
{
    // Total time played, kept in the save. The pause menu (present in every level) adds the time a level runs
    // unpaused; New Game starts it from zero.
    public static class Playtime
    {
        public static double Seconds { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            SaveData save = SaveServices.Current.Load();
            Seconds = save != null ? save.playtimeSeconds : 0d;
        }

        public static void Add(float seconds)
        {
            Seconds += seconds;
        }

        public static void Reset()
        {
            Seconds = 0d;
        }

        // "01:02:03" (hours keep counting past 99).
        public static string Format()
        {
            TimeSpan t = TimeSpan.FromSeconds(Seconds);
            return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}
