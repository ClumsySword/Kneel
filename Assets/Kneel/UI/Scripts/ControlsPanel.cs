using System.Collections;
using System.Collections.Generic;
using Kneel.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Kneel.UI
{
    // The Controls screen: every rebindable PlayerControls key, and rebinding. Confirm a line, then press the new
    // key or mouse button (Esc cancels). A key already used by another line swaps with it, so two actions never
    // share a key. Changes are saved at once (KeybindService) and reach every PlayerControls in the game.
    // Controls without a key of their own (aim, zoom, the moves made by combining keys) are listed under OTHER,
    // for reading only. One list in a masked viewport: it scrolls to keep the selected line in view, and with the
    // mouse wheel.
    public class ControlsPanel : MenuPanel, IScrollHandler
    {
        private struct Line
        {
            public string label;
            public string action;

            // For composite parts (Movement's WASD): the part's name.
            public string part;

            // For display-only lines (action null): what is shown instead of a key.
            public string info;

            public Line(string label, string action, string part = null)
            {
                this.label = label;
                this.action = action;
                this.part = part;
                info = null;
            }

            public static Line Info(string label, string text)
            {
                return new Line(label, null) { info = text };
            }
        }

        private class Row
        {
            public KeybindRow view;
            public InputAction action;
            public int bindingIndex;
        }

        private static readonly (string header, Line[] lines)[] Layout =
        {
            ("MOVEMENT", new[]
            {
                new Line("MOVE FORWARD", "Movement", "up"),
                new Line("MOVE BACK", "Movement", "down"),
                new Line("MOVE LEFT", "Movement", "left"),
                new Line("MOVE RIGHT", "Movement", "right"),
                new Line("SPRINT", "Sprint"),
                new Line("JUMP", "Jump"),
            }),
            ("ACTIONS", new[]
            {
                new Line("STRIKE", "Strike"),
                new Line("HEAVY ATTACK", "HeavyAttack"),
                new Line("BLOCK / PARRY", "Block"),
                new Line("DODGE ROLL", "Dodge"),
                new Line("DRAW / SHEATHE", "Draw"),
                new Line("HEAL", "Heal"),
                new Line("INTERACT", "Interact"),
            }),
            ("OTHER", new[]
            {
                Line.Info("AIM", "MOUSE"),
                Line.Info("ZOOM", "SCROLL WHEEL"),
                Line.Info("SHIELD BASH", "BLOCK + STRIKE"),
                Line.Info("AIR ATTACK", "STRIKE IN THE AIR"),
            }),
        };

        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private KeybindRow rowTemplate;
        [SerializeField] private TMP_Text headerTemplate;
        [SerializeField] private MenuItemView resetButton;
        [SerializeField] private MenuItemView backButton;
        [SerializeField] private float rowHeight = 40f;
        [SerializeField] private float headerHeight = 50f;

        [Header("Scrolling")]
        // The masked window rowContainer scrolls inside.
        [SerializeField] private RectTransform viewport;

        // Space kept above the first line and below the last, so they rest clear of the viewport's faded edges.
        [SerializeField] private float edgePadding;

        // How much of the next line stays visible above or below the selected one, in rows.
        [SerializeField] private float scrollMargin = 1.2f;
        [SerializeField] private float scrollTime = 0.12f;

        // One mouse-wheel notch, in rows.
        [SerializeField] private float wheelStep = 2f;

        private readonly List<Row> rows = new List<Row>();
        private readonly List<MenuSelectable> navigable = new List<MenuSelectable>();
        private PlayerControls controls;
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private int rebindEndedFrame = -10;
        private float cursor;
        private float scroll;

        protected override MenuSelectable FirstSelected => navigable[0];

        // Esc belongs to a rebind in progress, and to the frame it cancelled one.
        protected override bool CanClose => rebind == null && Time.frameCount > rebindEndedFrame + 1;

        protected override void Awake()
        {
            base.Awake();
            controls = new PlayerControls();
            KeybindService.ApplyTo(controls.asset);
            rowTemplate.gameObject.SetActive(false);
            headerTemplate.gameObject.SetActive(false);
            resetButton.Confirmed += _ => ResetToDefaults();
            backButton.Confirmed += _ => Back();
            Build();
            Refresh();
        }

        private void OnDestroy()
        {
            rebind?.Dispose();
            controls?.Dispose();
        }

        private void Build()
        {
            cursor = -edgePadding;
            foreach ((string header, Line[] lines) in Layout)
            {
                if (cursor < -edgePadding)
                {
                    cursor -= headerHeight * 0.3f;
                }

                TMP_Text title = Instantiate(headerTemplate, rowContainer);
                title.name = "Header_" + header;
                title.text = header;
                title.gameObject.SetActive(true);
                Place(title.rectTransform, headerHeight);

                foreach (Line line in lines)
                {
                    KeybindRow view = Instantiate(rowTemplate, rowContainer);
                    view.name = "Bind_" + line.label;
                    view.Text = line.label;
                    view.gameObject.SetActive(true);
                    Place((RectTransform)view.transform, rowHeight);
                    if (line.action == null)
                    {
                        // Not a key of its own: shown, never selected.
                        view.Value = line.info;
                        view.interactable = false;
                        continue;
                    }

                    InputAction action = controls.asset.FindAction(line.action, true);
                    view.Confirmed += StartRebind;
                    rows.Add(new Row { view = view, action = action, bindingIndex = FindBinding(action, line.part) });
                    navigable.Add(view);
                }
            }

            cursor -= headerHeight * 0.4f;
            Place((RectTransform)resetButton.transform, rowHeight);
            Place((RectTransform)backButton.transform, rowHeight);
            navigable.Add(resetButton);
            navigable.Add(backButton);
            selection.SetItems(navigable);
            foreach (MenuSelectable item in navigable)
            {
                item.Selected += ScrollIntoView;
            }
        }

        // How far the list can scroll: its height (with the bottom padding) past the viewport's.
        private float MaxScroll => Mathf.Max(0f, -cursor + edgePadding - viewport.rect.height);

        private void Update()
        {
            Vector2 position = rowContainer.anchoredPosition;
            float step = 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime / Mathf.Max(scrollTime, 0.001f));
            position.y = Mathf.Abs(position.y - scroll) < 0.5f ? scroll : Mathf.Lerp(position.y, scroll, step);
            rowContainer.anchoredPosition = position;
        }

        // Keeps the selected line, and a little of its neighbours, inside the viewport. Jumps there when the page
        // isn't showing yet (opening), glides otherwise.
        private void ScrollIntoView(MenuSelectable item)
        {
            var rect = (RectTransform)item.transform;
            float margin = rowHeight * scrollMargin;
            float top = -rect.anchoredPosition.y - rect.rect.height * 0.5f - margin;
            float bottom = -rect.anchoredPosition.y + rect.rect.height * 0.5f + margin;
            float target = scroll;
            if (item == navigable[0])
            {
                // All the way up, so the first section's header shows too.
                target = 0f;
            }
            else if (item == navigable[navigable.Count - 1])
            {
                target = MaxScroll;
            }
            else if (top < target)
            {
                target = top;
            }
            else if (bottom > target + viewport.rect.height)
            {
                target = bottom - viewport.rect.height;
            }

            scroll = Mathf.Clamp(target, 0f, MaxScroll);
            if (group.alpha <= 0.01f)
            {
                rowContainer.anchoredPosition = new Vector2(rowContainer.anchoredPosition.x, scroll);
            }
        }

        // Mouse wheel. Wheel deltas differ by platform (one notch can read 1, 6 or 120), so every event moves the
        // same fixed step.
        public void OnScroll(PointerEventData eventData)
        {
            if (!IsOpen || Mathf.Approximately(eventData.scrollDelta.y, 0f))
            {
                return;
            }

            scroll = Mathf.Clamp(scroll - Mathf.Sign(eventData.scrollDelta.y) * wheelStep * rowHeight, 0f, MaxScroll);
        }

        private static int FindBinding(InputAction action, string part)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (part == null ? !binding.isComposite && !binding.isPartOfComposite : binding.isPartOfComposite && binding.name == part)
                {
                    return i;
                }
            }

            Debug.LogError($"Controls: no binding '{part ?? "(single)"}' on {action.name}.");
            return 0;
        }

        private void Refresh()
        {
            foreach (Row row in rows)
            {
                row.view.Value = KeybindService.KeyName(row.action.bindings[row.bindingIndex].effectivePath);
            }
        }

        private void StartRebind(KeybindRow view)
        {
            Row row = rows.Find(r => r.view == view);
            if (row == null || rebind != null || !IsOpen)
            {
                return;
            }

            MenuAudio.Play(MenuCue.Confirm);
            string previousPath = row.action.bindings[row.bindingIndex].effectivePath;
            row.view.Value = "PRESS A KEY";

            // Lock the menu while listening, so the key that's pressed doesn't also move or confirm anything.
            SetInteractable(false);
            if (EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = false;
            }

            rebind = row.action.PerformInteractiveRebinding(row.bindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => EndRebind(row, previousPath, true))
                .OnCancel(_ => EndRebind(row, previousPath, false));

            if (row.action.bindings[row.bindingIndex].isPartOfComposite)
            {
                rebind.WithExpectedControlType("Button");
            }

            rebind.Start();
        }

        private void EndRebind(Row row, string previousPath, bool completed)
        {
            rebind.Dispose();
            rebind = null;
            rebindEndedFrame = Time.frameCount;

            if (completed)
            {
                // The key was someone else's: give them this line's old key.
                string newPath = row.action.bindings[row.bindingIndex].effectivePath;
                foreach (Row other in rows)
                {
                    if (other != row && other.action.bindings[other.bindingIndex].effectivePath == newPath)
                    {
                        other.action.ApplyBindingOverride(other.bindingIndex, previousPath);
                    }
                }

                KeybindService.Save(controls.asset);
                MenuAudio.Play(MenuCue.Confirm);
            }
            else
            {
                MenuAudio.Play(MenuCue.Back);
            }

            Refresh();
            StartCoroutine(UnlockAfterInput(row.view));
        }

        // Two frames later, so the key that finished the rebind (Enter, an arrow) has been released by the UI.
        private IEnumerator UnlockAfterInput(MenuSelectable reselect)
        {
            yield return null;
            yield return null;
            if (EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = true;
            }

            SetInteractable(true);
            selection.Select(reselect);
        }

        private void ResetToDefaults()
        {
            if (rebind != null)
            {
                return;
            }

            controls.asset.RemoveAllBindingOverrides();
            KeybindService.Save(controls.asset);
            MenuAudio.Play(MenuCue.Confirm);
            Refresh();
        }

        // Stacks lines top-down from the container's top edge.
        private void Place(RectTransform rect, float height)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, cursor - height * 0.5f);
            cursor -= height;
        }
    }
}
