using System;

namespace Kneel.UI
{
    // One line of a menu: its label, when it is shown, and what confirming it does. Menus are built from a list
    // of these, so adding LOAD GAME or CREDITS is one more entry.
    public sealed class MenuEntry
    {
        public MenuEntry(string label, Action onConfirm, Func<bool> isVisible = null)
        {
            Label = label;
            OnConfirm = onConfirm;
            IsVisible = isVisible ?? (() => true);
        }

        public string Label { get; }

        public Action OnConfirm { get; }

        public Func<bool> IsVisible { get; }
    }
}
