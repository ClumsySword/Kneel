using System;
using System.Collections.Generic;
using Kneel.Core;
using UnityEngine;

namespace Kneel.UI
{
    // The Levels screen: jump straight into any of the five levels. Choosing one raises LevelChosen with its
    // scene; the main menu does the fade and load.
    public class LevelsPanel : MenuPanel
    {
        [SerializeField] private RectTransform listContainer;
        [SerializeField] private MenuItemView itemTemplate;
        [SerializeField] private MenuItemView backButton;
        [SerializeField] private float itemSpacing = 54f;

        private readonly List<MenuSelectable> navigable = new List<MenuSelectable>();

        public event Action<string> LevelChosen;

        protected override MenuSelectable FirstSelected => navigable[0];

        protected override void Awake()
        {
            base.Awake();
            itemTemplate.gameObject.SetActive(false);
            foreach (LevelCatalog.Level level in LevelCatalog.Levels)
            {
                MenuItemView view = Instantiate(itemTemplate, listContainer);
                view.name = "Level_" + level.Numeral;
                view.Text = $"{level.Numeral}  ·  {level.Name.ToUpperInvariant()}";
                ((RectTransform)view.transform).anchoredPosition = new Vector2(0f, -navigable.Count * itemSpacing);
                view.gameObject.SetActive(true);
                string chosen = level.Scene;
                view.Confirmed += _ => LevelChosen?.Invoke(chosen);
                navigable.Add(view);
            }

            ((RectTransform)backButton.transform).anchoredPosition = new Vector2(0f, -(navigable.Count + 0.4f) * itemSpacing);
            backButton.Confirmed += _ => Back();
            navigable.Add(backButton);
            selection.SetItems(navigable);
        }
    }
}
