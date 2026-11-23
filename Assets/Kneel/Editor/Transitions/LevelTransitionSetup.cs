using System.IO;
using Kneel.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kneel.EditorTools.Transitions
{
    // Kneel > Transitions: the data assets of the level transitions. Safe to rerun: it never overwrites a line,
    // an art slot or a setting the owner has changed; it only fills what is missing and refreshes the levels' looks
    // (grade profile, fog, ambient) from the scenes.
    public static class LevelTransitionSetup
    {
        public const string Folder = "Assets/Kneel/Core/Transitions/Resources";
        public const string SettingsPath = Folder + "/LevelTransitionSettings.asset";
        public const string CatalogPath = Folder + "/LevelTransitions.asset";
        public const string ArtFolder = "Assets/Kneel/UI/Art/Loading";
        public const string FontPath = "Assets/Kneel/UI/Fonts/CormorantGaramond-Medium SDF.asset";
        public const string EmberPath = "Assets/Kneel/UI/Art/RadialSoft.png";

        // Drafts for the owner (rules in the transitions brief, section 4). One per seam, keyed by the level left.
        public static readonly (string scene, string line)[] DraftLines =
        {
            ("L1_BrokenLine_Playable", "Thousands of bindings collapsed on that field. One running backwards was lost among them."),
            ("L2_HollowVillage", "Kael's unit burned these villages. The thralls he kills here are missed, and the loss is traced."),
            ("L3_FallowFields", "The carts went up the causeway and came back down. The search now follows this road."),
            ("L4_BurningGate", "The court knelt facing the doors. Beyond them, he knows exactly where Kael is."),
        };

        public static string ArtPathFor(int levelIndex)
        {
            LevelCatalog.Level level = LevelCatalog.Levels[levelIndex];
            string name = level.Name.Replace("The ", string.Empty).Replace(" ", string.Empty);
            return $"{ArtFolder}/LoadingArt_L{levelIndex + 1}_{name}.png";
        }

        [MenuItem("Kneel/Transitions/Create or Update Assets")]
        public static void CreateOrUpdate()
        {
            Directory.CreateDirectory(Folder);
            var settings = AssetDatabase.LoadAssetAtPath<LevelTransitionSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LevelTransitionSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<LevelTransitionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelTransitionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.font ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            catalog.ember ??= AssetDatabase.LoadAssetAtPath<Sprite>(EmberPath);

            // Seams: L1..L4, each keyed by the level left.
            var seams = new System.Collections.Generic.List<LevelTransitionCatalog.Seam>(catalog.seams ?? new LevelTransitionCatalog.Seam[0]);
            for (int i = 0; i < LevelCatalog.Levels.Count - 1; i++)
            {
                string scene = LevelCatalog.Levels[i].Scene;
                LevelTransitionCatalog.Seam seam = seams.Find(s => s.fromScene == scene);
                if (seam == null)
                {
                    seam = new LevelTransitionCatalog.Seam { fromScene = scene };
                    seams.Add(seam);
                }

                if (string.IsNullOrEmpty(seam.line))
                {
                    foreach (var draft in DraftLines)
                    {
                        if (draft.scene == scene)
                        {
                            seam.line = draft.line;
                        }
                    }
                }

                if (seam.art == null)
                {
                    seam.art = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPathFor(i));
                }
            }

            seams.Sort((a, b) => LevelCatalog.IndexOf(a.fromScene).CompareTo(LevelCatalog.IndexOf(b.fromScene)));
            catalog.seams = seams.ToArray();
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Level transition assets ready.");
        }

        // Reads each level's look from its scene (global volume profile, fog, ambient). Opens every level in turn;
        // nothing in the scenes is changed.
        [MenuItem("Kneel/Transitions/Capture Level Looks (opens each level)")]
        public static void CaptureLooks()
        {
            CreateOrUpdate();
            var catalog = AssetDatabase.LoadAssetAtPath<LevelTransitionCatalog>(CatalogPath);
            var looks = new System.Collections.Generic.List<LevelTransitionCatalog.LevelLook>();
            foreach (LevelCatalog.Level level in LevelCatalog.Levels)
            {
                string path = ScenePath(level.Scene);
                if (path == null)
                {
                    continue;
                }

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var look = new LevelTransitionCatalog.LevelLook
                {
                    scene = level.Scene,
                    fogColor = RenderSettings.fogColor,
                    ambientSky = RenderSettings.ambientSkyColor,
                    ambientEquator = RenderSettings.ambientEquatorColor,
                    ambientGround = RenderSettings.ambientGroundColor,
                };
                foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
                {
                    if (v.isGlobal && v.sharedProfile != null && (look.grade == null || v.priority < 1f))
                    {
                        look.grade = v.sharedProfile;
                    }
                }

                looks.Add(look);
            }

            catalog.looks = looks.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"Captured {looks.Count} level looks.");
        }

        // The same for one level, from its open scene (for runs that can't afford to open all five at once).
        public static void CaptureOpenLook()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelTransitionCatalog>(CatalogPath);
            string scene = EditorSceneManager.GetActiveScene().name;
            var looks = new System.Collections.Generic.List<LevelTransitionCatalog.LevelLook>(catalog.looks ?? new LevelTransitionCatalog.LevelLook[0]);
            looks.RemoveAll(l => l.scene == scene);
            var look = new LevelTransitionCatalog.LevelLook
            {
                scene = scene,
                fogColor = RenderSettings.fogColor,
                ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor,
            };
            foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
            {
                if (v.isGlobal && v.sharedProfile != null && (look.grade == null || v.priority < 1f))
                {
                    look.grade = v.sharedProfile;
                }
            }

            looks.Add(look);
            looks.Sort((a, b) => LevelCatalog.IndexOf(a.scene).CompareTo(LevelCatalog.IndexOf(b.scene)));
            catalog.looks = looks.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static string ScenePath(string sceneName)
        {
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (Path.GetFileNameWithoutExtension(s.path) == sceneName)
                {
                    return s.path;
                }
            }

            return null;
        }
    }
}
