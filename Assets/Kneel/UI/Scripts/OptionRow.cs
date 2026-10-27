using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Kneel.UI
{
    // An options line: a label on the left and either a slider or a ‹ choice › on the right. Left/Right (keys,
    // D-pad, stick) change the value; Up/Down move between rows as usual. The mouse drags the slider, clicks the
    // arrows, or clicks the row to cycle a choice.
    public class OptionRow : MenuSelectable, IPointerClickHandler
    {
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text sliderValueLabel;
        [SerializeField] private GameObject sliderRoot;
        [SerializeField] private RectTransform sliderTrack;
        [SerializeField] private RectTransform sliderFill;
        [SerializeField] private GameObject choiceRoot;
        [SerializeField] private float sliderStep = 0.05f;

        private bool isSlider;
        private float value;
        private Func<float, string> format;
        private Action<float> sliderChanged;
        private string[] choices;
        private int index;
        private Action<int> choiceChanged;

        public void SetupSlider(string text, float initial, Func<float, string> formatValue, Action<float> changed)
        {
            Text = text;
            isSlider = true;
            format = formatValue;
            sliderChanged = changed;
            sliderRoot.SetActive(true);
            choiceRoot.SetActive(false);
            SetSlider(initial, false);
        }

        public void SetupChoice(string text, string[] options, int initial, Action<int> changed)
        {
            Text = text;
            isSlider = false;
            choices = options;
            choiceChanged = changed;
            sliderRoot.SetActive(false);
            choiceRoot.SetActive(true);
            SetChoice(initial, false);
        }

        public void Step(int direction)
        {
            if (isSlider)
            {
                SetSlider(Mathf.Round((value + direction * sliderStep) / sliderStep) * sliderStep, true);
            }
            else if (choices != null && choices.Length > 0)
            {
                SetChoice((index + direction + choices.Length) % choices.Length, true);
            }
        }

        // From the slider track: screen position to 0..1 along the track.
        public void SetFromPointer(PointerEventData eventData)
        {
            if (!isSlider || !IsInteractable())
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(sliderTrack, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                Rect r = sliderTrack.rect;
                SetSlider(Mathf.InverseLerp(r.xMin, r.xMax, local.x), true);
            }
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (IsInteractable() && (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right))
            {
                Step(eventData.moveDir == MoveDirection.Right ? 1 : -1);
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isSlider && eventData.button == PointerEventData.InputButton.Left && IsInteractable())
            {
                Step(1);
            }
        }

        private void SetSlider(float v, bool notify)
        {
            v = Mathf.Clamp01(v);
            bool changed = !Mathf.Approximately(v, value);
            value = v;
            sliderFill.anchorMax = new Vector2(v, sliderFill.anchorMax.y);
            sliderValueLabel.text = format != null ? format(v) : Mathf.RoundToInt(v * 100f) + "%";
            if (notify && changed)
            {
                MenuAudio.Play(MenuCue.Move);
                sliderChanged?.Invoke(v);
            }
        }

        private void SetChoice(int i, bool notify)
        {
            index = Mathf.Clamp(i, 0, choices.Length - 1);
            valueLabel.text = choices[index];
            if (notify)
            {
                MenuAudio.Play(MenuCue.Move);
                choiceChanged?.Invoke(index);
            }
        }
    }
}
