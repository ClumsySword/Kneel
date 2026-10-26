using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Kneel.EditorTools
{
    // Pressing Play with the MainMenu scene open starts from the Splash instead, the way the built game boots
    // (Play otherwise starts whatever scene is open). Any other open scene, a level included, still plays itself.
    [InitializeOnLoad]
    public static class MenuPlayModeStart
    {
        static MenuPlayModeStart()
        {
            EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Refresh();
            EditorSceneManager.sceneOpened += (_, __) => Refresh();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                {
                    Refresh();
                }
            };
            EditorApplication.delayCall += Refresh;
        }

        private static void Refresh()
        {
            // Scene switches inside Play mode (Splash -> MainMenu) must not change it.
            if (EditorApplication.isPlaying)
            {
                return;
            }

            bool menuOpen = SceneManager.GetActiveScene().path == MenuScenesBuilder.MainMenuPath;
            EditorSceneManager.playModeStartScene = menuOpen ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenesBuilder.SplashPath) : null;
        }
    }
}
