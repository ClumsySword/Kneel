using System.Collections.Generic;

namespace Kneel.Core
{
    // The game's five levels in order, with their display names. The menu's Levels screen lists them and the
    // pause menu names the current location from them.
    public static class LevelCatalog
    {
        public readonly struct Level
        {
            public Level(string numeral, string name, string scene)
            {
                Numeral = numeral;
                Name = name;
                Scene = scene;
            }

            public string Numeral { get; }

            public string Name { get; }

            public string Scene { get; }
        }

        public static readonly IReadOnlyList<Level> Levels = new[]
        {
            new Level("I", "The Broken Line", "L1_BrokenLine_Playable_L"),
            new Level("II", "The Hollow Village", "L2_HollowVillage"),
            new Level("III", "The Fallow Fields", "L3_FallowFields"),
            new Level("IV", "The Burning Gate", "L4_BurningGate"),
            new Level("V", "The Throne Room", "L5_ThroneRoom"),
        };

        // The level's name, or null for a scene that isn't one of the five (test arenas).
        public static string NameOf(string scene)
        {
            foreach (Level level in Levels)
            {
                if (level.Scene == scene)
                {
                    return level.Name;
                }
            }

            return null;
        }
    }
}
