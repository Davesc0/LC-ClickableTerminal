using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ClickableTerminal
{
    // The controller: which kind is in use, and its buttons read so that one press counts once.
    internal static class Pad
    {
        internal const int South = 0;
        internal const int West = 1;
        internal const int East = 3;
        internal const int Select = 4;
        internal const int LeftShoulder = 5;
        internal const int RightShoulder = 6;
        internal const int LeftTrigger = 7;
        internal const int RightTrigger = 8;
        internal const int RightStick = 9;

        private static readonly string[] Xbox = { "A", "X", "Y" };
        private static readonly string[] PlayStation = { "Cross", "Square", "Triangle" };
        private static readonly string[] Nintendo = { "B", "Y", "X" };
        private static readonly float[] Pressed = new float[10];

        // What the click, the right click and the wheel click are called on the controller in use. Null with a mouse.
        internal static string[] Names { get; private set; }

        internal static bool InUse => Names != null;

        internal static void MouseMoved()
        {
            Names = null;
        }

        // The mouse's words turned into the controller's buttons: InputUtils' pictures when it is there, names otherwise.
        internal static string Words(string hint)
        {
            var names = Names;
            if (names == null)
            {
                return hint;
            }

            string Key(int button, int name) => PadIcons.Markup(button) ?? names[name] + ":";

            return hint.Replace("Right click:", Key(PadIcons.West, 1)).Replace("Right:", Key(PadIcons.West, 1))
                .Replace("Wheel:", Key(PadIcons.North, 2)).Replace("Click:", Key(PadIcons.South, 0))
                .Replace("Click to", (PadIcons.Markup(PadIcons.South) ?? names[0]) + " to");
        }

        private static void Using(Gamepad pad)
        {
            var kind = pad.GetType().Name;
            Names = kind.Contains("DualShock") || kind.Contains("DualSense") ? PlayStation
                : kind.Contains("Switch") ? Nintendo
                : Xbox;
        }

        // One controller often shows up as two devices (its own and the one Steam makes), and the same press then arrives twice, a frame apart.
        internal static bool Press(int button, Func<Gamepad, ButtonControl> pick)
        {
            foreach (var pad in Gamepad.all)
            {
                if (pick(pad).wasPressedThisFrame)
                {
                    Using(pad);
                    var again = Time.unscaledTime - Pressed[button] < 0.2f;
                    Pressed[button] = Time.unscaledTime;
                    return !again;
                }
            }

            return false;
        }

        internal static bool Held(Func<Gamepad, ButtonControl> pick)
        {
            foreach (var pad in Gamepad.all)
            {
                if (pick(pad).isPressed)
                {
                    Using(pad);
                    return true;
                }
            }

            return false;
        }

        internal static Vector2 Stick(Func<Gamepad, StickControl> pick)
        {
            var most = Vector2.zero;
            foreach (var pad in Gamepad.all)
            {
                var value = pick(pad).ReadValue();
                if (value.sqrMagnitude > most.sqrMagnitude)
                {
                    most = value;
                    if (most.sqrMagnitude > 0.04f)
                    {
                        Using(pad);
                    }
                }
            }

            return most;
        }
    }
}
