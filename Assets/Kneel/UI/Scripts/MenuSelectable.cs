using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kneel.UI
{
    // A text-only menu entry: no box, the label crossfades between a muted and a bright colour when the entry is
    // selected. Moving the mouse over it selects it, so mouse, keyboard and gamepad share one highlight.
    public class MenuSelectable : Selectable, IPointerMoveHandler
    {
        [SerializeField] private TMP_Text label;

        // Other parts of the entry that take the label's colour (an options row's value and arrows).
        [SerializeField] private Graphic[] tinted = new Graphic[0];
        [SerializeField] private Color normalColor = new Color(0x8C / 255f, 0x84 / 255f, 0x78 / 255f, 0.7f);
        [SerializeField] private Color selectedColor = new Color(0xF2 / 255f, 0xE6 / 255f, 0xD0 / 255f, 1f);
        [SerializeField] private float fadeTime = 0.15f;

        private Coroutine fade;
        private bool isSelected;

        public event Action<MenuSelectable> Selected;

        public TMP_Text Label => label;

        public bool IsSelected => isSelected;

        public string Text
        {
            get => label != null ? label.text : string.Empty;
            set
            {
                if (label != null)
                {
                    label.text = value;
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            if (Application.isPlaying)
            {
                SetColor(normalColor);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            fade = null;
            SetColor(isSelected ? selectedColor : normalColor);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            isSelected = true;
            FadeTo(selectedColor);
            Selected?.Invoke(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            isSelected = false;
            FadeTo(normalColor);
        }

        // Selects on pointer movement, not on enter: a scrolling list slides lines under a pointer that isn't
        // moving, and those mustn't take the keyboard's selection.
        public void OnPointerMove(PointerEventData eventData)
        {
            EventSystem events = EventSystem.current;
            if (IsInteractable() && events != null && events.currentSelectedGameObject != gameObject)
            {
                events.SetSelectedGameObject(gameObject);
            }
        }

        private void FadeTo(Color color)
        {
            if (label == null)
            {
                return;
            }

            if (fade != null)
            {
                StopCoroutine(fade);
                fade = null;
            }

            if (!isActiveAndEnabled)
            {
                SetColor(color);
                return;
            }

            fade = StartCoroutine(FadeRoutine(color));
        }

        private void SetColor(Color color)
        {
            if (label != null)
            {
                label.color = color;
            }

            foreach (Graphic graphic in tinted)
            {
                if (graphic != null)
                {
                    graphic.color = color;
                }
            }
        }

        private IEnumerator FadeRoutine(Color to)
        {
            Color from = label.color;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                SetColor(Color.Lerp(from, to, UITween.Smooth(t / fadeTime)));
                yield return null;
            }

            SetColor(to);
            fade = null;
        }
    }
}
