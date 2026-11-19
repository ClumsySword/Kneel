using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kneel.UI
{
    // Persistent black overlay plus async scene loads: fade to black while the next scene loads in the
    // background, activate it behind the black, then fade back in. Created on first use.
    //
    // It also holds the level transitions' preload (BeginPreload): the next level loads in the background during
    // play, waiting at 90% with allowSceneActivation off, so the exit never hitches. While such a load waits,
    // Unity queues every other scene load behind it, so a death reload or a quit to the menu during a preload
    // can't simply start a new load: LoadScene discards the preload first. The waiting scene is let through,
    // and the moment it has loaded (after its Awake/OnEnable, before any Start) all its root objects are switched
    // off, so none of it ever runs a frame, renders or makes a sound; the scene that was asked for then loads
    // over it in the usual way. All of that happens under the black.
    public class SceneLoader : MonoBehaviour
    {
        // The black sits on top of everything; the loading screen (LoadingScreen) sits just under it.
        public const int BlackSortingOrder = 32000;
        public const int LoadingScreenSortingOrder = 31999;

        private static SceneLoader instance;

        private CanvasGroup fade;
        private AsyncOperation pending;
        private string discarding;

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

        // The scene preloading for a level transition, or null.
        public static string PendingScene { get; private set; }

        // When the pending preload started (realtimeSinceStartup), and how long it took to reach 90% (negative until then).
        public static float PreloadStartedAt { get; private set; } = -1f;

        public static float PreloadSeconds { get; private set; } = -1f;

        public static bool PreloadReady => instance != null && instance.pending != null && instance.pending.progress >= 0.9f;

        public static float BlackAlpha => instance != null && instance.fade != null ? instance.fade.alpha : 0f;

        // True for a scene that is being let through only to be thrown away (see the class comment).
        public static bool IsDiscarding(Scene scene) => instance != null && instance.discarding != null && scene.name == instance.discarding;

        // The project enters Play mode without a domain reload, so statics survive between sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            IsLoading = false;
            PendingScene = null;
            PreloadStartedAt = -1f;
            PreloadSeconds = -1f;
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
            canvas.sortingOrder = BlackSortingOrder;
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

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public IEnumerator FadeTo(float alpha, float duration)
        {
            fade.blocksRaycasts = true;
            yield return UITween.Fade(fade, alpha, duration);
            fade.blocksRaycasts = alpha > 0f;
        }

        public void SetBlack(float alpha)
        {
            fade.alpha = alpha;
            fade.blocksRaycasts = alpha > 0f;
        }

        // --- Level-transition preload ---

        // Starts the background load of `sceneName` (once; a second call for the same scene does nothing). While it
        // waits, background loading is held to its gentlest setting so it never takes time from the frame.
        public bool BeginPreload(string sceneName)
        {
            if (PendingScene == sceneName)
            {
                return true;
            }

            if (CanLoad(sceneName) == false || IsLoading)
            {
                return false;
            }

            if (pending != null)
            {
                Debug.LogWarning($"SceneLoader: already preloading '{PendingScene}', not '{sceneName}'.");
                return false;
            }

            Application.backgroundLoadingPriority = ThreadPriority.Low;
            pending = Preload(sceneName);
            if (pending == null)
            {
                Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
                return false;
            }

            PendingScene = sceneName;
            PreloadStartedAt = Time.realtimeSinceStartup;
            PreloadSeconds = -1f;
            StartCoroutine(TimePreload(pending));
            return true;
        }

        private IEnumerator TimePreload(AsyncOperation op)
        {
            while (op == pending && op.progress < 0.9f)
            {
                yield return null;
            }

            if (op == pending)
            {
                PreloadSeconds = Time.realtimeSinceStartup - PreloadStartedAt;
            }
        }

        // Lets the preloaded scene in (it replaces the current one). Done behind the loading screen. Returns the
        // operation, so the caller can wait for isDone.
        public AsyncOperation ActivatePreload()
        {
            if (pending == null)
            {
                return null;
            }

            // Behind the loading screen nothing else is running: give the last of the load all the time it wants.
            Application.backgroundLoadingPriority = ThreadPriority.High;
            AsyncOperation op = pending;
            pending = null;
            PendingScene = null;
            op.allowSceneActivation = true;
            StartCoroutine(RestorePriorityWhenDone(op));
            return op;
        }

        private static IEnumerator RestorePriorityWhenDone(AsyncOperation op)
        {
            while (op.isDone == false)
            {
                yield return null;
            }

            Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
        }

        // Throws the pending preload away (see the class comment). Only call it behind the black.
        private IEnumerator DiscardPreload()
        {
            if (pending == null)
            {
                yield break;
            }

            discarding = PendingScene;
            Debug.Log($"SceneLoader: discarding the preload of '{discarding}'.");
            AsyncOperation op = pending;
            pending = null;
            PendingScene = null;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            op.allowSceneActivation = true;
            while (op.isDone == false)
            {
                yield return null;
            }

            discarding = null;
            Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (discarding == null || scene.name != discarding)
            {
                return;
            }

            // After Awake and OnEnable, before Start: nothing in it gets to run.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                root.SetActive(false);
            }
        }

        // --- Plain loads (menus, Continue, death restart) ---

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
            yield return FadeTo(1f, fadeOut);

            // A preload waiting for activation would block this load: throw it away first, under the black. (Even a
            // preload of this very scene: it is the next level's, set up for a transition that isn't happening.)
            if (pending != null)
            {
                yield return DiscardPreload();
            }

            AsyncOperation op = Preload(sceneName);
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
