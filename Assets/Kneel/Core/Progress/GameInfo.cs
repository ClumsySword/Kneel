using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kneel.Core
{
    // Where the name of the player's current place comes from.
    public interface ILocationSource
    {
        string CurrentLocation { get; }
    }

    // What UI that describes the player's situation reads (the pause menu's header). Locations come from the
    // level's name until there are named areas inside levels; set Location to change that.
    public static class GameInfo
    {
        public static ILocationSource Location { get; set; } = new SceneLocation();

        // The project enters Play mode without a domain reload: start every session from the default.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Location = new SceneLocation();
        }
    }

    // The active scene's level name (LevelCatalog), or for a test scene its name without the owner suffix.
    public class SceneLocation : ILocationSource
    {
        public string CurrentLocation
        {
            get
            {
                string scene = SceneManager.GetActiveScene().name;
                string level = LevelCatalog.NameOf(scene);
                if (level != null)
                {
                    return level;
                }

                // "Arena_S" -> "Arena".
                int suffix = scene.LastIndexOf('_');
                return suffix > 0 && suffix >= scene.Length - 2 ? scene.Substring(0, suffix) : scene;
            }
        }
    }
}
