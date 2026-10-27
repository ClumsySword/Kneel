using UnityEngine;

namespace Kneel.UI
{
    // Quitting the application. Not offered on WebGL (the browser tab owns the app); in the Editor it stops Play
    // mode instead.
    public static class AppExit
    {
        public static bool Supported
        {
            get
            {
#if UNITY_WEBGL
                return false;
#else
                return Application.platform != RuntimePlatform.WebGLPlayer;
#endif
            }
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
