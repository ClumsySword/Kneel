using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Kneel.UI
{
    // A Controls line: what the action is on the left, its key on the right. Confirming it (Enter, click) asks
    // the Controls screen to listen for a new key.
    public class KeybindRow : MenuSelectable, ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text valueLabel;

        public event Action<KeybindRow> Confirmed;

        public string Value
        {
            set => valueLabel.text = value;
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (IsInteractable())
            {
                Confirmed?.Invoke(this);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && IsInteractable())
            {
                Confirmed?.Invoke(this);
            }
        }
    }
}
