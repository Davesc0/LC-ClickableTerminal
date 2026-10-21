using System;
using HarmonyLib;

namespace ClickableTerminal
{
    // BraBru's Terminal Scroll Patch calls FindObjectOfType on every wheel turn, because the field it expects is gone in
    // this game version. That is a visible hitch each time, so it gets the terminal handed to it.
    internal static class ScrollPatchFix
    {
        private static bool tried;
        private static Terminal terminal;

        internal static void Apply(Harmony harmony, Terminal current)
        {
            terminal = current;

            if (tried)
            {
                return;
            }

            tried = true;

            try
            {
                var patch = Reflect.Find("TerminalRelativeScroll.TerminalScrollPatch");
                var lookup = patch != null ? AccessTools.Method(patch, "GetTerminal") : null;
                if (lookup != null)
                {
                    harmony.Patch(lookup, prefix: new HarmonyMethod(typeof(ScrollPatchFix), nameof(HandOver)));
                    Plugin.mlg.LogInfo("Terminal Scroll Patch found: its terminal lookup now uses the one we already have.");
                }
            }
            catch (Exception e)
            {
                Plugin.mlg.LogWarning($"Could not speed up Terminal Scroll Patch: {e.Message}");
            }
        }

        private static bool HandOver(ref Terminal __result)
        {
            if (terminal == null)
            {
                return true;
            }

            __result = terminal;
            return false;
        }
    }
}
