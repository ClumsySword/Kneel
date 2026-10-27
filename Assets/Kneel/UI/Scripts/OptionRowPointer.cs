using UnityEngine;
using UnityEngine.EventSystems;

namespace Kneel.UI
{
    // Mouse input for an OptionRow's parts: on the slider track, press/drag sets the value; on an arrow, a click
    // steps the choice.
    public class OptionRowPointer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerClickHandler
    {
        [SerializeField] private OptionRow row;

        // 0 = slider track, -1 / +1 = choice arrows.
        [SerializeField] private int step;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (step == 0 && eventData.button == PointerEventData.InputButton.Left)
            {
                row.SetFromPointer(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (step == 0 && eventData.button == PointerEventData.InputButton.Left)
            {
                row.SetFromPointer(eventData);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (step != 0 && eventData.button == PointerEventData.InputButton.Left && row.IsInteractable())
            {
                row.Step(step);
            }
        }
    }
}
