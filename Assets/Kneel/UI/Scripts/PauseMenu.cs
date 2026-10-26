using System;
using System.Collections;
using System.Collections.Generic;
using Kneel.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kneel.UI
{
    // The in-game pause menu: Esc (or Start on a gamepad). Freezes the game (timeScale 0, gameplay input off),
    // darkens it, muffles music and ambience, and offers Resume / Options / Controls / Quit to Main Menu / Quit to
    // Desktop in the main menu's style. Every scene except the splash and the main menu gets one from
    // Resources/PauseMenu (built by Tools/Kneel/Build Menu Scenes), so level scenes need nothing added to them.
    // It also keeps the play clock, since it is present whenever a level runs. All fades run on unscaled time.
    public class PauseMenu : MonoBehaviour
    {
        private enum Mode
        {
            Closed,
            Busy,
            List,
            Dialog,
            Panel,
            Leaving,
        }

        private const string PrefabName = "PauseMenu";
        private static readonly string[] MenuScenes = { "Splash", "MainMenu" };

        private static readonly KeyHintBar.Hint SelectHint = new KeyHintBar.Hint("ENTER", "A", "SELECT");
        private static readonly KeyHintBar.Hint ResumeHint = new KeyHintBar.Hint("ESC", "B", "RESUME");
        private static readonly KeyHintBar.Hint BackHint = new KeyHintBar.Hint("ESC", "B", "BACK");
        private static readonly KeyHintBar.Hint CancelHint = new KeyHintBar.Hint("ESC", "B", "CANCEL");

        [Header("Scenes")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private float leaveFadeTime = 1.2f;
        [SerializeField] private float arriveFadeTime = 0.8f;

        [Header("Look and feel (unscaled seconds)")]
        [SerializeField] private float openTime = 0.2f;
        [SerializeField] private float closeTime = 0.15f;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.7f;
        [SerializeField, Range(0f, 1f)] private float dimBehindPanel = 0.85f;
        // The list sits where the dialog opens, so it goes entirely.
        [SerializeField, Range(0f, 1f)] private float listAlphaBehindDialog = 0f;
        [SerializeField] private float itemSpacing = 54f;

        // Music and ambience low-pass cutoff while paused, in Hz.
        [SerializeField] private float pausedCutoff = 800f;

        [Header("References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private Image dim;
        [SerializeField] private CanvasGroup listGroup;
        [SerializeField] private RectTransform listContainer;
        [SerializeField] private MenuItemView itemTemplate;
        [SerializeField] private SelectionGroup listSelection;
        [SerializeField] private ConfirmDialog dialog;
        [SerializeField] private OptionsPanel options;
        [SerializeField] private ControlsPanel controls;
        [SerializeField] private TMP_Text locationLabel;
        [SerializeField] private TMP_Text playtimeLabel;
        [SerializeField] private KeyHintBar hints;

        // Used while paused when the scene has no EventSystem of its own (levels don't).
        [SerializeField] private EventSystem ownEventSystem;

        private readonly PauseAudio pauseAudio = new PauseAudio();
        private InputAction pauseAction;
        private InputAction cancelAction;
        private EventSystem events;
        private MenuItemView resumeView;
        private MenuPanel openPanel;
        private Mode mode = Mode.Closed;
        private int openedFrame = -1;
        private bool cursorWasVisible = true;

        public static PauseMenu Current { get; private set; }

        public static bool IsPaused => Current != null && Current.mode != Mode.Closed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // The project enters Play mode without a domain reload: never subscribe twice.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        // One menu per gameplay scene, living in that scene, so a reload or a scene change always starts fresh.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (loadMode != LoadSceneMode.Single || Array.IndexOf(MenuScenes, scene.name) >= 0 || (Current != null && Current.gameObject.scene == scene))
            {
                return;
            }

            var prefab = Resources.Load<PauseMenu>(PrefabName);
            if (prefab == null)
            {
                Debug.LogWarning("No Resources/" + PrefabName + " prefab: run Tools/Kneel/Build Menu Scenes.");
                return;
            }

            PauseMenu menu = Instantiate(prefab);
            menu.name = PrefabName;
            SceneManager.MoveGameObjectToScene(menu.gameObject, scene);
        }

        private void Awake()
        {
            Current = this;
            canvas.enabled = false;
            rootGroup.alpha = 0f;
            dim.color = new Color(0f, 0f, 0f, dimAlpha);
            ownEventSystem.gameObject.SetActive(false);
            SetListInteractable(false);
            BuildList();

            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");
        }

        private void OnEnable()
        {
            pauseAction.Enable();
        }

        private void OnDisable()
        {
            pauseAction.Disable();
        }

        private void OnDestroy()
        {
            pauseAction.Dispose();
            pauseAudio.Release();
            if (Current == this)
            {
                Current = null;
            }

            // Destroyed while paused (its scene unloading): never leave the next scene frozen.
            if (mode != Mode.Closed)
            {
                Time.timeScale = 1f;
            }
        }

        // Adding an entry here is all a new pause item needs.
        private List<MenuEntry> Entries()
        {
            return new List<MenuEntry>
            {
                new MenuEntry("Resume", Resume),
                new MenuEntry("Options", () => OpenPanel(options)),
                new MenuEntry("Controls", () => OpenPanel(controls)),
                new MenuEntry("Quit to Main Menu", QuitToMainMenu),
                new MenuEntry("Quit to Desktop", QuitToDesktop, () => AppExit.Supported),
            };
        }

        private void BuildList()
        {
            itemTemplate.gameObject.SetActive(false);
            var navigable = new List<MenuSelectable>();
            foreach (MenuEntry entry in Entries())
            {
                if (!entry.IsVisible())
                {
                    continue;
                }

                MenuItemView view = Instantiate(itemTemplate, listContainer);
                view.name = "Item_" + entry.Label.Replace(" ", string.Empty);
                view.Text = entry.Label.ToUpperInvariant();
                ((RectTransform)view.transform).anchoredPosition = new Vector2(0f, -navigable.Count * itemSpacing);
                view.gameObject.SetActive(true);
                MenuEntry captured = entry;
                view.Confirmed += _ => OnConfirmed(captured);
                navigable.Add(view);
                if (entry.OnConfirm == Resume)
                {
                    resumeView = view;
                }
            }

            // Centred on the screen.
            listContainer.anchoredPosition = new Vector2(0f, (navigable.Count - 1) * itemSpacing * 0.5f);
            listSelection.SetItems(navigable);
        }

        private void Update()
        {
            bool pausePressed = pauseAction.WasPressedThisFrame();
            if (mode == Mode.Closed)
            {
                Playtime.Add(Time.unscaledDeltaTime);
                if (pausePressed && !SceneLoader.IsLoading)
                {
                    Open();
                }

                return;
            }

            // The press that opened the menu mustn't also close it.
            if (Time.frameCount == openedFrame)
            {
                return;
            }

            bool back = pausePressed || (cancelAction != null && cancelAction.WasPressedThisFrame());
            if (!back)
            {
                return;
            }

            if (mode == Mode.List)
            {
                Resume();
            }
            else if (mode == Mode.Dialog)
            {
                dialog.Cancel();
            }
            else if (mode == Mode.Panel)
            {
                openPanel.Back();
            }
        }

        // Hit-stop ends on real time and sets timeScale back to 1, even mid-pause: hold it at 0. The OS pointer is
        // hidden in play (the aim ember stands in for it) and hidden again whenever the window regains focus, so
        // it is held visible every frame too.
        private void LateUpdate()
        {
            if (mode != Mode.Closed)
            {
                Time.timeScale = 0f;
                Cursor.visible = true;
            }
        }

        private void Open()
        {
            openedFrame = Time.frameCount;
            Time.timeScale = 0f;
            cursorWasVisible = Cursor.visible;
            Cursor.visible = true;
            GameplayInput.Suspend();
            pauseAudio.Capture(transform, pausedCutoff);
            UseEventSystem(true);
            SetMode(Mode.Busy);
            RefreshHeader();

            dim.color = new Color(0f, 0f, 0f, dimAlpha);
            listGroup.alpha = 1f;
            canvas.enabled = true;
            listSelection.Select(resumeView);
            hints.Show(SelectHint, ResumeHint);
            MenuAudio.Play(MenuCue.Confirm);
            StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            StartCoroutine(FadeAudio(0f, 1f, openTime));
            yield return UITween.Fade(rootGroup, 1f, openTime);
            EnterList(resumeView);
        }

        private void Resume()
        {
            if (mode != Mode.List)
            {
                return;
            }

            MenuAudio.Play(MenuCue.Back);
            StartCoroutine(CloseRoutine());
        }

        private IEnumerator CloseRoutine()
        {
            SetMode(Mode.Busy);
            SetListInteractable(false);
            StartCoroutine(FadeAudio(1f, 0f, closeTime));
            yield return UITween.Fade(rootGroup, 0f, closeTime);
            canvas.enabled = false;
            pauseAudio.Release();
            UseEventSystem(false);
            GameplayInput.Resume();
            mode = Mode.Closed;
            Time.timeScale = 1f;
            Cursor.visible = cursorWasVisible;
        }

        private void OnConfirmed(MenuEntry entry)
        {
            if (mode == Mode.List)
            {
                entry.OnConfirm();
            }
        }

        // Fades the list out, opens the page, and comes back to the entry that opened it.
        private void OpenPanel(MenuPanel panel)
        {
            MenuAudio.Play(MenuCue.Confirm);
            SetMode(Mode.Busy);
            SetListInteractable(false);
            StartCoroutine(PanelRoutine(panel, listSelection.LastSelected));
        }

        private IEnumerator PanelRoutine(MenuPanel panel, MenuSelectable returnTo)
        {
            StartCoroutine(FadeDim(dimBehindPanel, 0.2f));
            yield return UITween.Fade(listGroup, 0f, 0.2f);
            openPanel = panel;
            hints.Show(SelectHint, BackHint);
            panel.Open(() => StartCoroutine(ClosePanelRoutine(returnTo)));
            SetMode(Mode.Panel);
        }

        private IEnumerator ClosePanelRoutine(MenuSelectable returnTo)
        {
            SetMode(Mode.Busy);
            openPanel = null;
            listSelection.Select(returnTo);
            hints.Show(SelectHint, ResumeHint);
            StartCoroutine(FadeDim(dimAlpha, 0.25f));
            yield return UITween.Fade(listGroup, 1f, 0.25f);
            EnterList(returnTo);
        }

        private void QuitToMainMenu()
        {
            bool saved = AutoSave();
            OpenDialog("Return to the main menu?\n" + SavedNote(saved), LeaveToMainMenu);
        }

        private void QuitToDesktop()
        {
            bool saved = AutoSave();
            OpenDialog("Quit to desktop?\n" + SavedNote(saved), () =>
            {
                SetMode(Mode.Leaving);
                AppExit.Quit();
            });
        }

        private static string SavedNote(bool saved)
        {
            return saved ? "Your progress has been saved." : "Your progress could not be saved.";
        }

        private static bool AutoSave()
        {
            try
            {
                SaveServices.Current.Save(new SaveData
                {
                    sceneName = SceneManager.GetActiveScene().name,
                    playtimeSeconds = Playtime.Seconds,
                });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Auto-save failed: " + e.Message);
                return false;
            }
        }

        private void OpenDialog(string message, Action confirmed)
        {
            MenuAudio.Play(MenuCue.Confirm);
            SetMode(Mode.Dialog);
            SetListInteractable(false);
            MenuSelectable returnTo = listSelection.LastSelected;
            hints.Show(SelectHint, CancelHint);
            StartCoroutine(UITween.Fade(listGroup, listAlphaBehindDialog, 0.2f));
            dialog.Show(message, confirmed, () => StartCoroutine(ReturnFromDialog(returnTo)));
        }

        private IEnumerator ReturnFromDialog(MenuSelectable returnTo)
        {
            SetMode(Mode.Busy);
            listSelection.Select(returnTo);
            hints.Show(SelectHint, ResumeHint);
            yield return UITween.Fade(listGroup, 1f, 0.2f);
            EnterList(returnTo);
        }

        // The game stays frozen under the fade to black; the main menu's scene sets timeScale back to 1.
        private void LeaveToMainMenu()
        {
            if (!SceneLoader.CanLoad(mainMenuScene))
            {
                Debug.LogError($"Can't load '{mainMenuScene}': add it to the Build Profiles scene list.", this);
                EnterList(listSelection.LastSelected);
                return;
            }

            SetMode(Mode.Leaving);
            SceneLoader.Instance.LoadScene(mainMenuScene, leaveFadeTime, arriveFadeTime);
        }

        private void EnterList(MenuSelectable select)
        {
            SetListInteractable(true);
            listSelection.Select(select);
            SetMode(Mode.List);
        }

        private void RefreshHeader()
        {
            locationLabel.text = GameInfo.Location.CurrentLocation.ToUpperInvariant();
            playtimeLabel.text = $"<color=#B0764AD9>PLAYTIME</color>  {Playtime.Format()}";
        }

        private void SetListInteractable(bool on)
        {
            listGroup.interactable = on;
            listGroup.blocksRaycasts = on;
            listSelection.Active = on;
        }

        // Keyboard/gamepad navigation and submit are off while Busy (fades) and Leaving.
        private void SetMode(Mode next)
        {
            mode = next;
            if (events != null)
            {
                events.sendNavigationEvents = next == Mode.List || next == Mode.Dialog || next == Mode.Panel;
            }
        }

        private void UseEventSystem(bool on)
        {
            if (!on)
            {
                ownEventSystem.gameObject.SetActive(false);
                events = null;
                cancelAction = null;
                return;
            }

            events = EventSystem.current;
            if (events == null || events == ownEventSystem)
            {
                ownEventSystem.gameObject.SetActive(true);
                events = ownEventSystem;
            }

            var module = events.GetComponent<InputSystemUIInputModule>();
            cancelAction = module != null && module.cancel != null ? module.cancel.action : null;
        }

        private IEnumerator FadeAudio(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += UITween.DeltaTime)
            {
                pauseAudio.SetAmount(Mathf.Lerp(from, to, UITween.Smooth(t / duration)));
                yield return null;
            }

            pauseAudio.SetAmount(to);
        }

        private IEnumerator FadeDim(float to, float duration)
        {
            float from = dim.color.a;
            for (float t = 0f; t < duration; t += UITween.DeltaTime)
            {
                dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, UITween.Smooth(t / duration)));
                yield return null;
            }

            dim.color = new Color(0f, 0f, 0f, to);
        }
    }
}
