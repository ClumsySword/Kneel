using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kneel.UI
{
    // Persistent black overlay plus async scene loads: fade to black while the next scene loads in the
    // background, activate it behind the black, then fade back in. Created on first use.
    public class SceneLoader : MonoBehaviour
    {
        private static SceneLoader instance;

        private CanvasGroup fade;

        public static SceneLoader Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[SceneLoader]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<SceneLoader>();
                }

                return instance;
            }
        }

        public static bool IsLoading { get; private set; }

        // The project enters Play mode without a domain reload, so statics survive between sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            IsLoading = false;
        }

        // Starts loading a scene without activating it; set allowSceneActivation to switch.
        public static AsyncOperation Preload(string sceneName)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op != null)
            {
                op.allowSceneActivation = false;
            }

            return op;
        }

        public static bool CanLoad(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
        }

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            fade = gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
            fade.interactable = false;
            fade.blocksRaycasts = false;

            var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            black.transform.SetParent(transform, false);
            var rect = (RectTransform)black.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            black.GetComponent<Image>().color = Color.black;
        }

        public IEnumerator FadeTo(float alpha, float duration)
        {
            fade.blocksRaycasts = true;
            yield return UITween.Fade(fade, alpha, duration);
            fade.blocksRaycasts = alpha > 0f;
        }

        public void LoadScene(string sceneName, float fadeOut, float fadeIn)
        {
            if (IsLoading)
            {
                return;
            }

            StartCoroutine(LoadRoutine(sceneName, fadeOut, fadeIn));
        }

        private IEnumerator LoadRoutine(string sceneName, float fadeOut, float fadeIn)
        {
            IsLoading = true;
            AsyncOperation op = Preload(sceneName);
            yield return FadeTo(1f, fadeOut);

            while (op.progress < 0.9f)
            {
                yield return null;
            }

            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                yield return null;
            }

            Time.timeScale = 1f;
            IsLoading = false;
            yield return FadeTo(0f, fadeIn);
        }
    }
}
