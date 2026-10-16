using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ClickableTerminal.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal static class PlayerPatches
    {
        // The mouse moves the terminal cursor, so it must not turn the head as well.
        [HarmonyPatch("PlayerLookInput")]
        [HarmonyPrefix]
        private static bool KeepHeadStill(PlayerControllerB __instance)
        {
            return !(TerminalMouse.InUse && __instance.inTerminalMenu);
        }

        // The game moves the terminal a third of the page per turn of the wheel, whatever the page's length.
        // Here it moves a set number of lines. On our own pages and over an application's counters and pages
        // the wheel is ours already, so the hidden game text is left where it is. Everything else the method
        // does (the hotbar) is untouched: this only steps in while the terminal is open.
        [HarmonyPatch("ScrollMouse_performed")]
        [HarmonyPrefix]
        private static bool ScrollByLines(PlayerControllerB __instance, InputAction.CallbackContext context)
        {
            if (!__instance.inTerminalMenu || !TerminalMouse.InUse || !Settings.LineScroll.Value
                || Chainloader.PluginInfos.ContainsKey("brabru.TerminalScrollPatch"))
            {
                return true;
            }

            if (TerminalMouse.WheelTaken)
            {
                return false;
            }

            var bar = __instance.terminalScrollVertical;
            var input = context;
            var turn = input.ReadValue<float>();
            if (bar != null && Mathf.Abs(turn) > 0.01f)
            {
                bar.value = Mathf.Clamp01(bar.value + Mathf.Sign(turn) * Settings.ScrollLines.Value * TerminalMouse.LineStep());
            }

            return false;
        }
    }
}
