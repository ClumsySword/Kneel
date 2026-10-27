using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Kneel.UI
{
    // A modal yes/no question in the menu's style. Cancel is selected by default, so a stray confirm press never
    // starts a destructive action.
    public class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text message;
        [SerializeField] private MenuItemView confirmButton;
        [SerializeField] private MenuItemView cancelButton;
        [SerializeField] private SelectionGroup selection;
        [SerializeField] private float fadeTime = 0.2f;

        private Action onConfirm;
        private Action onCancel;
        private bool busy;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            selection.SetItems(new MenuSelectable[] { confirmButton, cancelButton });
            confirmButton.Confirmed += _ => Close(true);
            cancelButton.Confirmed += _ => Close(false);
        }

        public void Show(string text, Action confirmed, Action cancelled)
        {
            if (IsOpen)
            {
                return;
            }

            message.text = text;
            onConfirm = confirmed;
            onCancel = cancelled;
            IsOpen = true;
            StartCoroutine(OpenRoutine());
        }

        // Esc / East button.
        public void Cancel()
        {
            Close(false);
        }

        private IEnumerator OpenRoutine()
        {
            busy = true;
            selection.Select(cancelButton);
            yield return UITween.Fade(group, 1f, fadeTime);
            group.interactable = true;
            group.blocksRaycasts = true;
            selection.Active = true;
            busy = false;
        }

        private void Close(bool confirmed)
        {
            if (!IsOpen || busy)
            {
                return;
            }

            MenuAudio.Play(confirmed ? MenuCue.Confirm : MenuCue.Back);
            StartCoroutine(CloseRoutine(confirmed));
        }

        private IEnumerator CloseRoutine(bool confirmed)
        {
            busy = true;
            selection.Active = false;
            group.interactable = false;
            group.blocksRaycasts = false;
            yield return UITween.Fade(group, 0f, fadeTime);
            IsOpen = false;
            busy = false;
            (confirmed ? onConfirm : onCancel)?.Invoke();
        }
    }
}
