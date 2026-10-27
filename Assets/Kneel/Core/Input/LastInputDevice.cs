using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Kneel.Core
{
    // Whether the player last used a gamepad or the keyboard and mouse, so on-screen hints can show the right
    // icons. A device counts as used on a button or key press, a stick or d-pad push, or (the mouse) a move.
    public static class LastInputDevice
    {
        public static bool IsGamepad { get; private set; }

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsGamepad = false;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // The project enters Play mode without a domain reload: never subscribe twice.
            InputSystem.onEvent -= OnEvent;
            InputSystem.onEvent += OnEvent;
        }

        private static void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            bool gamepad = device is Gamepad;
            if (gamepad == IsGamepad || (!gamepad && !(device is Keyboard) && !(device is Mouse)))
            {
                return;
            }

            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            {
                return;
            }

            // Stick directions are synthetic buttons, which the press check skips, so sticks are read directly.
            bool used = eventPtr.GetFirstButtonPressOrNull() != null
                || (device is Gamepad pad && (Pushed(pad.leftStick, eventPtr) || Pushed(pad.rightStick, eventPtr)))
                || (device is Mouse mouse && mouse.delta.ReadValueFromEvent(eventPtr, out Vector2 delta) && delta.sqrMagnitude > 9f);
            if (!used)
            {
                return;
            }

            IsGamepad = gamepad;
            Changed?.Invoke();
        }

        private static bool Pushed(StickControl stick, InputEventPtr eventPtr)
        {
            return stick.ReadValueFromEvent(eventPtr, out Vector2 value) && value.sqrMagnitude > 0.25f;
        }
    }
}
