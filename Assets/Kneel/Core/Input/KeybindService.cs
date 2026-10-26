using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kneel.Core
{
    // The player's rebinds: binding overrides on the PlayerControls actions, saved to PlayerPrefs as JSON. Every
    // script makes its own `new PlayerControls()`, so instead of each one loading them, this watches for any
    // PlayerControls action map being enabled and applies the current overrides to that instance first.
    public static class KeybindService
    {
        private const string PrefKey = "kneel.input.bindingOverrides";
        public const string AssetName = "PlayerControls";

        // Which override version each live PlayerControls instance has (weak: instances die with their scripts).
        private static ConditionalWeakTable<InputActionAsset, StrongBox<int>> applied = new ConditionalWeakTable<InputActionAsset, StrongBox<int>>();
        private static int version;

        public static string OverridesJson { get; private set; } = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            OverridesJson = PlayerPrefs.GetString(PrefKey, string.Empty);
            applied = new ConditionalWeakTable<InputActionAsset, StrongBox<int>>();
            version++;

            // The project enters Play mode without a domain reload: never subscribe twice.
            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onActionChange += OnActionChange;

            if (InputSystem.actions != null)
            {
                ApplyTo(InputSystem.actions);
            }
        }

        // Brings one PlayerControls instance up to date with the saved overrides.
        public static void ApplyTo(InputActionAsset asset)
        {
            if (asset == null || asset.name != AssetName)
            {
                return;
            }

            StrongBox<int> box = applied.GetValue(asset, _ => new StrongBox<int>(-1));
            if (box.Value == version)
            {
                return;
            }

            // Mark first: changing bindings re-resolves, which can raise more change events for this asset.
            box.Value = version;
            asset.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(OverridesJson))
            {
                asset.LoadBindingOverridesFromJson(OverridesJson);
            }
        }

        // Saves the overrides currently on `source` (the Controls screen's copy) as the player's binds.
        public static void Save(InputActionAsset source)
        {
            OverridesJson = source.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(PrefKey, OverridesJson);
            PlayerPrefs.Save();
            version++;
            applied.GetValue(source, _ => new StrongBox<int>()).Value = version;
        }

        private static void OnActionChange(object target, InputActionChange change)
        {
            if (change != InputActionChange.ActionMapEnabled && change != InputActionChange.ActionEnabled)
            {
                return;
            }

            InputActionAsset asset = target is InputActionMap map ? map.asset : (target as InputAction)?.actionMap?.asset;
            ApplyTo(asset);
        }
    }
}
