using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Kneel.Core
{
    // Switches every enabled PlayerControls action off, and later back on, so a menu (the pause menu) can freeze
    // the player without any gameplay script knowing. Disabling an action cancels it, so the scripts' cached
    // values (move, sprint, block held) reset rather than sticking while the menu is open.
    public static class GameplayInput
    {
        private static readonly List<InputAction> suspended = new List<InputAction>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            suspended.Clear();

            // A menu that leaves the scene (Quit to Main Menu, the death screen's choices) never resumes: forget
            // the old scene's actions, or the next Resume would switch them back on beside the new scene's.
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            suspended.Clear();
        }

        public static void Suspend()
        {
            foreach (InputAction action in InputSystem.ListEnabledActions())
            {
                InputActionAsset asset = action.actionMap != null ? action.actionMap.asset : null;
                if (asset != null && asset.name == KeybindService.AssetName)
                {
                    action.Disable();
                    suspended.Add(action);
                }
            }
        }

        // Re-enables what Suspend turned off, with any rebinds made in the meantime.
        public static void Resume()
        {
            foreach (InputAction action in suspended)
            {
                InputActionAsset asset = action.actionMap != null ? action.actionMap.asset : null;
                if (asset == null)
                {
                    continue;
                }

                KeybindService.ApplyTo(asset);
                action.Enable();
            }

            suspended.Clear();
        }
    }
}
