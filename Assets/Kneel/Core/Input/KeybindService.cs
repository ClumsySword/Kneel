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
        private static PlayerControls reference;

        public static string OverridesJson { get; private set; } = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            OverridesJson = PlayerPrefs.GetString(PrefKey, string.Empty);
            applied = new ConditionalWeakTable<InputActionAsset, StrongBox<int>>();
            version++;
            reference?.Dispose();
            reference = null;

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

        // The key an action is on now, rebinds included ("E", "LEFT MOUSE"), for on-screen prompts. Reads a
        // private PlayerControls copy that is never enabled. Actions bound to a composite (Movement) aren't
        // supported: they have no single key.
        public static string KeyFor(string actionName)
        {
            if (reference == null)
            {
                reference = new PlayerControls();
            }

            ApplyTo(reference.asset);
            InputAction action = reference.asset.FindAction(actionName, true);
            foreach (InputBinding binding in action.bindings)
            {
                if (!binding.isComposite && !binding.isPartOfComposite)
                {
                    return KeyName(binding.effectivePath);
                }
            }

            return string.Empty;
        }

        // English key names from the control layouts. (GetBindingDisplayString follows the OS keyboard language:
        // Space read "MELLEMRUM" on a Danish layout, in an otherwise English game.)
        public static string KeyName(string path)
        {
            switch (path)
            {
                case "<Mouse>/leftButton":
                    return "LEFT MOUSE";
                case "<Mouse>/rightButton":
                    return "RIGHT MOUSE";
                case "<Mouse>/middleButton":
                    return "MIDDLE MOUSE";
                case "<Mouse>/backButton":
                    return "MOUSE 4";
                case "<Mouse>/forwardButton":
                    return "MOUSE 5";
                default:
                    return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice).ToUpperInvariant();
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
