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
            held.Clear();

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

        // The level transitions' hold on the NEXT level's input: from the moment that level is activated until the
        // intro hands control back, every PlayerControls action it enables is switched off again (HoldNew, each
        // frame) and remembered here. Unlike Suspend's list this one is never cleared by a scene unloading (the old
        // level unloads during the same activation), and it only ever holds actions of the arriving level, so
        // ReleaseHeld can't wake a destroyed scene's input.
        private static readonly List<InputAction> held = new List<InputAction>();

        public static int HeldCount => held.Count;

        public static void HoldNew()
        {
            foreach (InputAction action in InputSystem.ListEnabledActions())
            {
                InputActionAsset asset = action.actionMap != null ? action.actionMap.asset : null;
                if (asset != null && asset.name == KeybindService.AssetName)
                {
                    action.Disable();
                    if (held.Contains(action) == false)
                    {
                        held.Add(action);
                    }
                }
            }
        }

        public static void ReleaseHeld()
        {
            foreach (InputAction action in held)
            {
                InputActionAsset asset = action.actionMap != null ? action.actionMap.asset : null;
                if (asset == null)
                {
                    continue;
                }

                KeybindService.ApplyTo(asset);
                action.Enable();
            }

            held.Clear();
        }

        // Drops Suspend's list without re-enabling anything (the level it came from is being left for good).
        public static void Forget()
        {
            suspended.Clear();
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
