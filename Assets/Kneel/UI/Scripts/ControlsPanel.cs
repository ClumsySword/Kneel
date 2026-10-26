using System.Collections;
using System.Collections.Generic;
using Kneel.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kneel.UI
{
    // The Controls screen, in two columns: every rebindable PlayerControls key, and rebinding. Confirm a line,
    // then press the new key or mouse button (Esc cancels). A key already used by another line swaps with it, so
    // two actions never share a key. Changes are saved at once (KeybindService) and reach every PlayerControls in
    // the game. Controls without a key of their own (aim, zoom, the moves made by combining keys) are listed
    // under OTHER, for reading only.
    public class ControlsPanel : MenuPanel
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

        // Column 0 is left, 1 right.
        private static readonly (string header, int column, Line[] lines)[] Layout =
        {
            ("MOVEMENT", 0, new[]
            {
                new Line("MOVE FORWARD", "Movement", "up"),
                new Line("MOVE BACK", "Movement", "down"),
                new Line("MOVE LEFT", "Movement", "left"),
                new Line("MOVE RIGHT", "Movement", "right"),
                new Line("SPRINT", "Sprint"),
                new Line("JUMP", "Jump"),
            }),
            ("OTHER", 0, new[]
            {
                Line.Info("AIM", "MOUSE"),
                Line.Info("ZOOM", "SCROLL WHEEL"),
                Line.Info("SHIELD BASH", "BLOCK + STRIKE"),
                Line.Info("AIR ATTACK", "STRIKE IN THE AIR"),
            }),
            ("ACTIONS", 1, new[]
            {
                new Line("STRIKE", "Strike"),
                new Line("HEAVY ATTACK", "HeavyAttack"),
                new Line("BLOCK / PARRY", "Block"),
                new Line("DODGE ROLL", "Dodge"),
                new Line("DRAW / SHEATHE", "Draw"),
                new Line("HEAL", "Heal"),
                new Line("INTERACT", "Interact"),
            }),
        };

        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private KeybindRow rowTemplate;
        [SerializeField] private TMP_Text headerTemplate;
        [SerializeField] private MenuItemView resetButton;
        [SerializeField] private MenuItemView backButton;
        [SerializeField] private float rowHeight = 40f;
        [SerializeField] private float headerHeight = 50f;

        // Column centres, this far left and right of the middle.
        [SerializeField] private float columnOffset = 400f;

        private readonly List<Row> rows = new List<Row>();
        private readonly List<MenuSelectable> navigable = new List<MenuSelectable>();
        private PlayerControls controls;
        private readonly float[] cursors = new float[2];
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private int rebindEndedFrame = -10;

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
            var columns = new[] { new List<MenuSelectable>(), new List<MenuSelectable>() };
            foreach ((string header, int column, Line[] lines) in Layout)
            {
                if (cursors[column] < 0f)
                {
                    cursors[column] -= headerHeight * 0.3f;
                }

                TMP_Text title = Instantiate(headerTemplate, rowContainer);
                title.name = "Header_" + header;
                title.text = header;
                title.gameObject.SetActive(true);
                Place(title.rectTransform, headerHeight, column);

                foreach (Line line in lines)
                {
                    KeybindRow view = Instantiate(rowTemplate, rowContainer);
                    view.name = "Bind_" + line.label;
                    view.Text = line.label;
                    view.gameObject.SetActive(true);
                    Place((RectTransform)view.transform, rowHeight, column);
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
                    columns[column].Add(view);
                }
            }

            // Reset and Back sit centred under the taller column.
            cursors[0] = Mathf.Min(cursors[0], cursors[1]) - headerHeight * 0.4f;
            Place((RectTransform)resetButton.transform, rowHeight, -1);
            Place((RectTransform)backButton.transform, rowHeight, -1);
            navigable.AddRange(columns[0]);
            navigable.AddRange(columns[1]);
            navigable.Add(resetButton);
            navigable.Add(backButton);
            selection.SetItems(navigable);
            LinkColumns(columns[0], columns[1]);
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

        // Stacks lines top-down from the container's top edge, in column 0 (left), 1 (right) or -1 (centred,
        // below both).
        private void Place(RectTransform rect, float height, int column)
        {
            int stack = Mathf.Max(column, 0);
            float x = column < 0 ? 0f : column == 0 ? -columnOffset : columnOffset;
            rect.anchoredPosition = new Vector2(x, cursors[stack] - height * 0.5f);
            cursors[stack] -= height;
        }

        // Left/right jumps between the columns, to the line in the same position.
        private static void LinkColumns(List<MenuSelectable> left, List<MenuSelectable> right)
        {
            for (int i = 0; i < left.Count; i++)
            {
                Navigation nav = left[i].navigation;
                nav.selectOnRight = right[Mathf.Min(i, right.Count - 1)];
                left[i].navigation = nav;
            }

            for (int i = 0; i < right.Count; i++)
            {
                Navigation nav = right[i].navigation;
                nav.selectOnLeft = left[Mathf.Min(i, left.Count - 1)];
                right[i].navigation = nav;
            }
        }
    }
}
