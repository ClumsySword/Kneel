using System.Collections;
using System.Collections.Generic;
using Kneel.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Kneel.UI
{
    // The title screen: fades in the logo then the menu, builds the menu from a list of entries, and routes
    // Continue / New Game / Levels / Options / Controls / Exit. Input is locked during every fade so nothing
    // double-triggers.
    public class MainMenuController : MonoBehaviour
    {
        private enum Mode
        {
            Locked,
            List,
            Dialog,
            Panel,
        }

        [Header("Scenes")]
        [SerializeField] private string newGameScene = "L1_BrokenLine_Playable_L";
        [SerializeField] private float leaveFadeTime = 1.2f;
        [SerializeField] private float arriveFadeTime = 0.8f;

        [Header("Intro (unscaled seconds)")]
        [SerializeField] private float introDelay = 0.3f;
        [SerializeField] private float logoFadeTime = 1.6f;
        [SerializeField] private float itemsDelay = 0.1f;
        [SerializeField] private float itemsFadeTime = 0.9f;

        [Header("Layout")]
        [SerializeField] private float itemSpacing = 50f;
        [SerializeField, Range(0f, 1f)] private float logoAlphaBehindPanel = 0f;
        [SerializeField, Range(0f, 1f)] private float logoAlphaBehindDialog = 0.12f;
        [SerializeField, Range(0f, 1f)] private float listAlphaBehindDialog = 0.35f;

        [Header("Footer")]
        [SerializeField] private string studioName = "ClumsySword";
        [SerializeField] private int copyrightYear = 2026;

        [Header("References")]
        [SerializeField] private CanvasGroup logoGroup;
        [SerializeField] private CanvasGroup listGroup;
        [SerializeField] private CanvasGroup footerGroup;
        [SerializeField] private RectTransform listContainer;
        [SerializeField] private MenuItemView itemTemplate;
        [SerializeField] private SelectionGroup listSelection;
        [SerializeField] private ConfirmDialog dialog;
        [SerializeField] private OptionsPanel options;
        [SerializeField] private ControlsPanel controls;
        [SerializeField] private LevelsPanel levels;
        [SerializeField] private MenuAudio menuAudio;
        [SerializeField] private EmberFade[] particleFades = new EmberFade[0];
        [SerializeField] private TMP_Text copyrightLabel;
        [SerializeField] private TMP_Text versionLabel;

        private readonly List<MenuItemView> views = new List<MenuItemView>();
        private ISaveService saves;
        private MenuItemView continueView;
        private MenuItemView newGameView;
        private MenuPanel openPanel;
        private InputAction cancelAction;
        private Mode mode = Mode.Locked;
        private float particleAlpha;

        private void Awake()
        {
            Time.timeScale = 1f;
            saves = SaveServices.Current;
            copyrightLabel.text = $"© {copyrightYear} {studioName}".ToUpperInvariant();
            versionLabel.text = "VER. " + Application.version;

            logoGroup.alpha = 0f;
            listGroup.alpha = 0f;
            footerGroup.alpha = 0f;
            SetParticlesAlpha(0f);
            SetListInteractable(false);
            BuildList();
            levels.LevelChosen += scene =>
            {
                if (mode == Mode.Panel)
                {
                    Leave(scene);
                }
            };
        }

        // Adding an entry here is all a new menu item needs.
        private List<MenuEntry> Entries()
        {
            return new List<MenuEntry>
            {
                new MenuEntry("Continue", Continue, () => saves.HasSave()),
                new MenuEntry("New Game", NewGame),
                new MenuEntry("Levels", () => OpenPanel(levels)),
                new MenuEntry("Options", () => OpenPanel(options)),
                new MenuEntry("Controls", () => OpenPanel(controls)),
                new MenuEntry("Exit", ConfirmExit, () => AppExit.Supported),
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
                var rect = (RectTransform)view.transform;
                rect.anchoredPosition = new Vector2(0f, -views.Count * itemSpacing);
                view.gameObject.SetActive(true);
                MenuEntry captured = entry;
                view.Confirmed += _ => OnConfirmed(captured);
                views.Add(view);
                navigable.Add(view);

                if (entry.OnConfirm == Continue)
                {
                    continueView = view;
                }
                else if (entry.OnConfirm == NewGame)
                {
                    newGameView = view;
                }
            }

            listSelection.SetItems(navigable);
        }

        private IEnumerator Start()
        {
            SetMode(Mode.Locked);

            // From the component, not EventSystem.currentInputModule: that is only set on the EventSystem's first Update.
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            if (module != null && module.cancel != null)
            {
                cancelAction = module.cancel.action;
            }

            // Default: CONTINUE when there is a save, otherwise NEW GAME. Selected now so it fades in lit.
            listSelection.Select(continueView != null ? continueView : newGameView);
            if (menuAudio != null)
            {
                menuAudio.StartMusic();
            }

            yield return UITween.Wait(introDelay);
            StartCoroutine(FadeParticles(1f, logoFadeTime * 1.5f));
            yield return UITween.Fade(logoGroup, 1f, logoFadeTime);
            yield return UITween.Wait(itemsDelay);
            StartCoroutine(UITween.Fade(footerGroup, 1f, itemsFadeTime));
            yield return UITween.Fade(listGroup, 1f, itemsFadeTime);
            EnterList(listSelection.LastSelected);
        }

        private void Update()
        {
            if (cancelAction == null || !cancelAction.WasPressedThisFrame())
            {
                return;
            }

            if (mode == Mode.Dialog)
            {
                dialog.Cancel();
            }
            else if (mode == Mode.Panel)
            {
                openPanel.Back();
            }
        }

        private void OnConfirmed(MenuEntry entry)
        {
            if (mode != Mode.List)
            {
                return;
            }

            entry.OnConfirm();
        }

        private void Continue()
        {
            SaveData data = saves.Load();
            string scene = data != null && SceneLoader.CanLoad(data.sceneName) ? data.sceneName : newGameScene;
            Leave(scene);
        }

        private void NewGame()
        {
            if (!saves.HasSave())
            {
                Playtime.Reset();
                Leave(newGameScene);
                return;
            }

            OpenDialog("Begin a new journey?\nExisting progress will be lost.", () =>
            {
                saves.DeleteSave();
                Playtime.Reset();
                Leave(newGameScene);
            });
        }

        // Fades the list (and logo) out, opens the panel, and returns to the entry that opened it on Back.
        private void OpenPanel(MenuPanel panel)
        {
            MenuAudio.Play(MenuCue.Confirm);
            SetMode(Mode.Locked);
            SetListInteractable(false);
            StartCoroutine(PanelRoutine(panel, listSelection.LastSelected));
        }

        private IEnumerator PanelRoutine(MenuPanel panel, MenuSelectable returnTo)
        {
            StartCoroutine(UITween.Fade(logoGroup, logoAlphaBehindPanel, 0.25f));
            StartCoroutine(FadeParticles(logoAlphaBehindPanel, 0.25f));
            yield return UITween.Fade(listGroup, 0f, 0.2f);
            openPanel = panel;
            panel.Open(() => StartCoroutine(ClosePanelRoutine(returnTo)));
            SetMode(Mode.Panel);
        }

        private IEnumerator ClosePanelRoutine(MenuSelectable returnTo)
        {
            SetMode(Mode.Locked);
            openPanel = null;
            listSelection.Select(returnTo);
            StartCoroutine(UITween.Fade(logoGroup, 1f, 0.3f));
            StartCoroutine(FadeParticles(1f, 0.3f));
            yield return UITween.Fade(listGroup, 1f, 0.25f);
            EnterList(returnTo);
        }

        private void ConfirmExit()
        {
            OpenDialog("Quit to desktop?", Quit);
        }

        private void OpenDialog(string message, System.Action confirmed)
        {
            MenuAudio.Play(MenuCue.Confirm);
            SetMode(Mode.Dialog);
            SetListInteractable(false);
            MenuSelectable returnTo = listSelection.LastSelected;
            StartCoroutine(UITween.Fade(listGroup, listAlphaBehindDialog, 0.2f));
            StartCoroutine(UITween.Fade(logoGroup, logoAlphaBehindDialog, 0.2f));
            StartCoroutine(FadeParticles(logoAlphaBehindDialog, 0.2f));
            dialog.Show(message, confirmed, () => StartCoroutine(ReturnFromDialog(returnTo)));
        }

        private IEnumerator ReturnFromDialog(MenuSelectable returnTo)
        {
            SetMode(Mode.Locked);
            listSelection.Select(returnTo);
            StartCoroutine(UITween.Fade(logoGroup, 1f, 0.2f));
            StartCoroutine(FadeParticles(1f, 0.2f));
            yield return UITween.Fade(listGroup, 1f, 0.2f);
            EnterList(returnTo);
        }

        private void EnterList(MenuSelectable select)
        {
            SetListInteractable(true);
            listSelection.Select(select);
            SetMode(Mode.List);
        }

        private void Leave(string sceneName)
        {
            if (!SceneLoader.CanLoad(sceneName))
            {
                Debug.LogError($"Can't load '{sceneName}': add it to the Build Profiles scene list.", this);
                if (openPanel == null)
                {
                    EnterList(listSelection.LastSelected);
                }

                return;
            }

            SetMode(Mode.Locked);
            SetListInteractable(false);
            MenuAudio.Play(MenuCue.Confirm);
            if (menuAudio != null)
            {
                menuAudio.FadeOutMusic(leaveFadeTime);
            }

            SceneLoader.Instance.LoadScene(sceneName, leaveFadeTime, arriveFadeTime);
        }

        private void Quit()
        {
            SetMode(Mode.Locked);
            SetListInteractable(false);
            AppExit.Quit();
        }

        private void SetListInteractable(bool on)
        {
            listGroup.interactable = on;
            listGroup.blocksRaycasts = on;
            listSelection.Active = on;
        }

        // Keyboard/gamepad navigation and submit are off while Locked (fades, scene loads).
        private void SetMode(Mode next)
        {
            mode = next;
            if (EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = next != Mode.Locked;
            }
        }

        private IEnumerator FadeParticles(float to, float duration)
        {
            float from = particleAlpha;
            for (float t = 0f; t < duration; t += UITween.DeltaTime)
            {
                SetParticlesAlpha(Mathf.Lerp(from, to, UITween.Smooth(t / duration)));
                yield return null;
            }

            SetParticlesAlpha(to);
        }

        private void SetParticlesAlpha(float alpha)
        {
            particleAlpha = alpha;
            foreach (EmberFade fade in particleFades)
            {
                if (fade != null)
                {
                    fade.Alpha = alpha;
                }
            }
        }
    }
}
