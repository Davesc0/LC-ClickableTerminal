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
    // WhiteSpike's Interactive Terminal API, read by reflection so it stays optional.
    // Lategame Upgrades and others draw their keyboard driven screens with it: a list of elements with a cursor on one
    // of them, W and S to move, Enter to pick, A and D for pages. Everything here goes through the API's own methods,
    // so a click does exactly what the key would have done.
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

        // True for a few frames around a key press of ours, so it is not mistaken for the player typing.
        internal static bool PressingKey => Time.frameCount - pressedAt < 6;

        // What the exit key would do right now. The applications hang their own handlers on the key and swap them
        // from screen to screen (step back from a question, close a sub menu, leave), so those handlers are run
        // as they stand, exactly as the key would run them.
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

        // The applications decide for themselves what their exit key does on each screen: step back from a
        // question, close a sub menu, or leave. There is no telling from outside which it is, so the key itself
        // is pressed for them, whichever key the player has bound to it.
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

        // Lets go of the key again a moment later.
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

        // The command an application is registered under, spelled the way the API wants it typed.
        // It compares letter for letter, so "extend deadline" does not open what was registered as "Extend Deadline",
        // and the game's own parser then takes the words for something else entirely.
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

        // The application on screen, or null.
        internal static object Application()
        {
            var manager = Manager();
            return manager != null && application != null ? application.GetValue(manager) : null;
        }

        // The list the cursor is in right now. It changes as the application moves between its screens.
        internal static object Menu(object app)
        {
            return app != null ? Traverse.Create(app).Field("currentCursorMenu").GetValue() : null;
        }

        internal static Array Elements(object menu)
        {
            return menu != null ? Traverse.Create(menu).Field("elements").GetValue() as Array : null;
        }

        // What the entry prints for itself. Applications often make their own kind of entry that has no name at all
        // and writes a whole row (the upgrade, its levels, its price), so the printed text is asked for first.
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

        // Greyed out entries can still be stood on when the application says so.
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

        // What Enter does.
        internal static void Submit(object app)
        {
            Call(app, "Submit");
        }

        // True on applications that have pages and are showing one of them, not a screen that came up over it.
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

        // What A and D do.
        internal static bool Page(object app, bool forward)
        {
            return HasPages(app) && Call(app, forward ? "ChangeScreenForward" : "ChangeScreenBackward");
        }

        // The amount on entries that have one (how many to buy, how much to convert).
        internal static bool HasCounter(object element)
        {
            return element != null && element.GetType().GetMethod("IncrementCounter") != null
                   && (Traverse.Create(element).Property("ShowCounter").GetValue() as bool? ?? false);
        }

        internal static void Count(object element, bool up)
        {
            Call(element, up ? "IncrementCounter" : "DecrementCounter");
        }

        // Closes the application outright, whatever screen it is on.
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
            var manager = AccessTools.TypeByName("InteractiveTerminalAPI.UI.InteractiveTerminalManager");
            if (manager != null)
            {
                instance = AccessTools.Field(manager, "Instance");
                applications = AccessTools.Field(manager, "registeredApplications");
                application = AccessTools.Field(manager, "mainApplication");
            }

            var keys = AccessTools.TypeByName("InteractiveTerminalAPI.Input.Keybinds");
            if (keys != null)
            {
                exitAction = AccessTools.Field(keys, "cursorExitAction");
            }
        }
    }
}
