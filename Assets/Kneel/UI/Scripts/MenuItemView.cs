using System;
using UnityEngine.EventSystems;

namespace Kneel.UI
{
    // A menu entry that does something when confirmed: Enter/Space/South button, or a left click.
    public class MenuItemView : MenuSelectable, IPointerClickHandler, ISubmitHandler
    {
        public event Action<MenuItemView> Confirmed;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && IsInteractable())
            {
                Confirmed?.Invoke(this);
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (IsInteractable())
            {
                Confirmed?.Invoke(this);
            }
        }
    }
}
