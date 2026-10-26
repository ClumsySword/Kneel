using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kneel.UI
{
    // One navigable list (the main menu, a dialog's buttons, the options rows): wires wrap-around navigation,
    // moves the glow, plays the move tick, and while it is the active list makes sure something in it is always
    // selected, so keyboard and gamepad never lose focus after the mouse clears it.
    public class SelectionGroup : MonoBehaviour
    {
        [SerializeField] private MenuGlow glow;
        [SerializeField] private bool horizontal;

        private readonly List<MenuSelectable> items = new List<MenuSelectable>();
        private MenuSelectable last;
        private bool silentSelect;

        // Only the active group restores focus. The owner switches it as panels open and close.
        public bool Active { get; set; }

        public MenuSelectable LastSelected => last;

        public void SetItems(IList<MenuSelectable> newItems)
        {
            foreach (MenuSelectable item in items)
            {
                item.Selected -= OnItemSelected;
            }

            items.Clear();
            items.AddRange(newItems);
            for (int i = 0; i < items.Count; i++)
            {
                MenuSelectable prev = items[(i + items.Count - 1) % items.Count];
                MenuSelectable next = items[(i + 1) % items.Count];
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                if (horizontal)
                {
                    nav.selectOnLeft = prev;
                    nav.selectOnRight = next;
                }
                else
                {
                    nav.selectOnUp = prev;
                    nav.selectOnDown = next;
                }

                items[i].navigation = nav;
                items[i].Selected += OnItemSelected;
            }

            if (last != null && !items.Contains(last))
            {
                last = null;
            }
        }

        // Selects an item without the move tick (opening a panel, restoring focus).
        public void Select(MenuSelectable item)
        {
            if (item == null || EventSystem.current == null)
            {
                return;
            }

            last = item;
            if (EventSystem.current.currentSelectedGameObject == item.gameObject)
            {
                glow?.MoveTo((RectTransform)item.transform, false);
                return;
            }

            silentSelect = true;
            EventSystem.current.SetSelectedGameObject(item.gameObject);
            silentSelect = false;
        }

        private void OnItemSelected(MenuSelectable item)
        {
            if (glow != null)
            {
                glow.MoveTo((RectTransform)item.transform, false);
            }

            if (!silentSelect && last != null && last != item)
            {
                MenuAudio.Play(MenuCue.Move);
            }

            last = item;
        }

        private void Update()
        {
            if (!Active || last == null)
            {
                return;
            }

            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return;
            }

            GameObject current = events.currentSelectedGameObject;
            if (current == null || !current.activeInHierarchy)
            {
                Select(last);
            }
        }
    }
}
