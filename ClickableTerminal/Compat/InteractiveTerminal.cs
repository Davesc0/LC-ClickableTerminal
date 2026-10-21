using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ClickableTerminal
{
    // Read by reflection, so the Interactive Terminal API stays optional.
    internal static class InteractiveTerminal
    {
        private static bool looked;
        private static FieldInfo instance;
        private static FieldInfo applications;
        private static FieldInfo application;
        private static FieldInfo exitAction;

        private static InputControl<float> held;
        private static int releaseIn;
        private static int pressedAt = -100;

        // So our own key press is not mistaken for the player typing.
        internal static bool PressingKey => Time.frameCount - pressedAt < 6;

        // Apps swap their exit handlers from screen to screen, so they are run as they stand, like the key would.
        internal static bool RunExitHandlers()
        {
            Find();

            try
            {
                var action = exitAction != null ? exitAction.GetValue(null) as InputAction : null;
                var callbacks = action != null ? AccessTools.Field(typeof(InputAction), "m_OnPerformed")?.GetValue(action) : null;
                if (callbacks == null)
                {
                    return false;
                }

                var count = (int)callbacks.GetType().GetProperty("length").GetValue(callbacks);
                var item = callbacks.GetType().GetProperty("Item");
                var handlers = new List<Action<InputAction.CallbackContext>>();

                // Copied first: a handler usually takes itself off the key and puts another one on.
                for (var i = 0; i < count; i++)
                {
                    if (item.GetValue(callbacks, new object[] { i }) is Action<InputAction.CallbackContext> handler)
                    {
                        handlers.Add(handler);
                    }
                }

                foreach (var handler in handlers)
                {
                    handler(default(InputAction.CallbackContext));
                }

                return handlers.Count > 0;
            }
            catch (Exception e)
            {
                Plugin.mlg.LogWarning($"Interactive Terminal API: could not run its exit handlers: {(e.InnerException ?? e).Message}");
                return false;
            }
        }

        // There is no telling from outside what the exit key does on this screen, so the key itself is pressed.
        internal static bool PressExitKey()
        {
            Find();

            try
            {
                var action = exitAction != null ? exitAction.GetValue(null) as InputAction : null;
                var control = action != null && action.controls.Count > 0 ? action.controls[0] as InputControl<float> : null;
                if (control == null)
                {
                    return false;
                }

                Queue(control, 1f);
                held = control;
                releaseIn = 2;
                pressedAt = Time.frameCount;
                return true;
            }
            catch (Exception e)
            {
                Plugin.mlg.LogWarning($"Interactive Terminal API: could not press its exit key: {e.Message}");
                return false;
            }
        }

        internal static void Tick()
        {
            if (held != null && --releaseIn <= 0)
            {
                try
                {
                    Queue(held, 0f);
                }
                catch (Exception)
                {
                }

                held = null;
            }
        }

        private static void Queue(InputControl<float> control, float value)
        {
            using (StateEvent.From(control.device, out var change))
            {
                control.WriteValueIntoEvent(value, change);
                InputSystem.QueueEvent(change);
            }
        }

        internal static bool Running()
        {
            return Manager() != null;
        }

        // The API compares letter for letter, so "extend deadline" would not open "Extend Deadline".
        internal static string Registered(string command)
        {
            foreach (var key in Commands())
            {
                if (string.Equals(key, command, StringComparison.OrdinalIgnoreCase))
                {
                    return key;
                }
            }

            return null;
        }

        internal static IEnumerable<string> Commands()
        {
            Find();
            var commands = new List<string>();

            try
            {
                if (applications?.GetValue(null) is IDictionary registered)
                {
                    foreach (var key in registered.Keys)
                    {
                        commands.Add(key.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"Could not read the Interactive Terminal API applications: {e.Message}");
            }

            return commands;
        }

        internal static object Application()
        {
            var manager = Manager();
            return manager != null && application != null ? application.GetValue(manager) : null;
        }

        internal static object Menu(object app)
        {
            return app != null ? Traverse.Create(app).Field("currentCursorMenu").GetValue() : null;
        }

        internal static Array Elements(object menu)
        {
            return menu != null ? Traverse.Create(menu).Field("elements").GetValue() as Array : null;
        }

        // Many apps make entries with no name that print a whole row, so the printed text is asked for first.
        internal static string TextOf(object element)
        {
            try
            {
                var print = element.GetType().GetMethod("GetText", new[] { typeof(int) });
                if (print != null && print.Invoke(element, new object[] { 49 }) is string text && !string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            catch (Exception)
            {
            }

            return Traverse.Create(element).Property("Name").GetValue() as string;
        }

        internal static bool CanSelect(object element)
        {
            if (element == null)
            {
                return false;
            }

            var active = Traverse.Create(element).Property("Active").GetValue() as Delegate;
            var isActive = active == null || (bool)active.DynamicInvoke(element);
            return isActive || (Traverse.Create(element).Property("SelectInactive").GetValue() as bool? ?? true);
        }

        internal static void Select(object menu, int index)
        {
            Traverse.Create(menu).Field("cursorIndex").SetValue(index);
        }

        internal static void Submit(object app)
        {
            Call(app, "Submit");
        }

        internal static bool HasPages(object app)
        {
            if (app == null)
            {
                return false;
            }

            var pages = Traverse.Create(app).Field("currentPage").GetValue();
            if (pages == null || !(Traverse.Create(pages).Field("elements").GetValue() is Array screens) || screens.Length < 2)
            {
                return false;
            }

            var shown = Traverse.Create(app).Field("currentScreen").GetValue();
            var index = Traverse.Create(pages).Field("pageIndex").GetValue() as int? ?? 0;
            return index >= 0 && index < screens.Length && ReferenceEquals(shown, screens.GetValue(index));
        }

        internal static bool Page(object app, bool forward)
        {
            return HasPages(app) && Call(app, forward ? "ChangeScreenForward" : "ChangeScreenBackward");
        }

        internal static bool HasCounter(object element)
        {
            return element != null && element.GetType().GetMethod("IncrementCounter") != null
                   && (Traverse.Create(element).Property("ShowCounter").GetValue() as bool? ?? false);
        }

        internal static void Count(object element, bool up)
        {
            Call(element, up ? "IncrementCounter" : "DecrementCounter");
        }

        internal static void Exit(object app)
        {
            Call(app, "OnScreenExit", default(InputAction.CallbackContext));
        }

        private static bool Call(object target, string method, params object[] arguments)
        {
            try
            {
                var found = target != null ? target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) : null;
                if (found == null)
                {
                    return false;
                }

                found.Invoke(target, arguments);
                return true;
            }
            catch (Exception e)
            {
                Plugin.mlg.LogWarning($"Interactive Terminal API: {method} failed: {(e.InnerException ?? e).Message}");
                return false;
            }
        }

        private static object Manager()
        {
            Find();

            try
            {
                var manager = instance != null ? instance.GetValue(null) as UnityEngine.Object : null;
                return manager != null ? manager : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Find()
        {
            if (looked)
            {
                return;
            }

            looked = true;
            var manager = Reflect.Find("InteractiveTerminalAPI.UI.InteractiveTerminalManager");
            if (manager != null)
            {
                instance = AccessTools.Field(manager, "Instance");
                applications = AccessTools.Field(manager, "registeredApplications");
                application = AccessTools.Field(manager, "mainApplication");
            }

            var keys = Reflect.Find("InteractiveTerminalAPI.Input.Keybinds");
            if (keys != null)
            {
                exitAction = AccessTools.Field(keys, "cursorExitAction");
            }
        }
    }
}
