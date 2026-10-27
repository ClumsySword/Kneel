using System;
using System.Collections;
using UnityEngine;

namespace Kneel.UI
{
    // A full-screen page opened from the main list (Options, Controls, Levels): fades in, takes keyboard/gamepad
    // focus, and fades out again on Back (its BACK entry, Esc or the East button).
    public abstract class MenuPanel : MonoBehaviour
    {
        [SerializeField] protected CanvasGroup group;
        [SerializeField] protected SelectionGroup selection;
        [SerializeField] private float fadeTime = 0.25f;

        private Action closed;
        private bool busy;

        public bool IsOpen { get; private set; }

        // What is selected when the panel opens.
        protected abstract MenuSelectable FirstSelected { get; }

        // False while the panel is busy with something Esc belongs to (waiting for a key to rebind).
        protected virtual bool CanClose => true;

        protected virtual void Awake()
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        public void Open(Action onClosed)
        {
            if (IsOpen)
            {
                return;
            }

            closed = onClosed;
            IsOpen = true;
            StartCoroutine(OpenRoutine());
        }

        public void Back()
        {
            if (!IsOpen || busy || !CanClose)
            {
                return;
            }

            MenuAudio.Play(MenuCue.Back);
            OnClosing();
            StartCoroutine(CloseRoutine());
        }

        protected virtual void OnClosing()
        {
        }

        protected void SetInteractable(bool on)
        {
            group.interactable = on;
            group.blocksRaycasts = on;
            selection.Active = on;
        }

        private IEnumerator OpenRoutine()
        {
            busy = true;
            selection.Select(FirstSelected);
            yield return UITween.Fade(group, 1f, fadeTime);
            SetInteractable(true);
            busy = false;
        }

        private IEnumerator CloseRoutine()
        {
            busy = true;
            SetInteractable(false);
            yield return UITween.Fade(group, 0f, fadeTime);
            IsOpen = false;
            busy = false;
            closed?.Invoke();
        }
    }
}
