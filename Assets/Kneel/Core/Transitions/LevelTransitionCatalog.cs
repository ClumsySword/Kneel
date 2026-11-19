using UnityEngine;
using UnityEngine.Rendering;

namespace Kneel.Core
{
    // The authored data of the four seams that doesn't belong to a scene: the loading screen's art and line, and
    // each level's look (its grade, fog and ambient), which the neighbouring levels lean toward. What comes next is
    // LevelCatalog's order; where the exit is and how Kael walks out is the scene's LevelExit / LevelArrival.
    //
    // The owner rewrites a line here without touching code, and swaps final art by overwriting the PNG under the
    // same name (UI/Art/Loading/LoadingArt_L<n>.png, 2560 x 1440).
    [CreateAssetMenu(menuName = "Kneel/Level Transition Catalog", fileName = "LevelTransitions")]
    public class LevelTransitionCatalog : ScriptableObject
    {
        public const string ResourceName = "LevelTransitions";

        [System.Serializable]
        public class LevelLook
        {
            [Tooltip("LevelCatalog scene name.")]
            public string scene;

            [Tooltip("The level's own global grade (its GlobalVolume's profile). The next level's outro and this level's intro blend it in.")]
            public VolumeProfile grade;

            public Color fogColor = Color.gray;
            public Color ambientSky = Color.gray;
            public Color ambientEquator = Color.gray;
            public Color ambientGround = Color.gray;
        }

        [System.Serializable]
        public class Seam
        {
            [Tooltip("The level being left (LevelCatalog scene name). The next one is LevelCatalog's next.")]
            public string fromScene;

            [Tooltip("Concept art of the level just left, shown full-bleed at 16:9, letterboxed.")]
            public Texture2D art;

            [Tooltip("True while the art is the generated placeholder capture.")]
            public bool artIsPlaceholder = true;

            [Tooltip("The one line on the loading screen. 6-16 words, plain and physical.")]
            [TextArea(2, 4)]
            public string line;
        }

        public Seam[] seams = new Seam[0];
        public LevelLook[] looks = new LevelLook[0];

        [Header("Loading screen type")]
        public TMPro.TMP_FontAsset font;
        public Color lineColor = new Color(0.902f, 0.855f, 0.769f, 0.86f);
        public float lineSize = 30f;

        [Tooltip("The small 'still loading' ember (the menu's soft ember sprite).")]
        public Sprite ember;

        private static LevelTransitionCatalog loaded;

        public static LevelTransitionCatalog Load()
        {
            if (loaded == null)
            {
                loaded = Resources.Load<LevelTransitionCatalog>(ResourceName);
            }

            return loaded;
        }

        public Seam SeamFrom(string scene)
        {
            foreach (Seam s in seams)
            {
                if (s.fromScene == scene)
                {
                    return s;
                }
            }

            return null;
        }

        public LevelLook LookOf(string scene)
        {
            foreach (LevelLook l in looks)
            {
                if (l.scene == scene)
                {
                    return l;
                }
            }

            return null;
        }
    }
}
