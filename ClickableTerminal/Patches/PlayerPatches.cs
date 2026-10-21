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
        // The mouse moves the terminal cursor, so it must not turn the head too.
        [HarmonyPatch("PlayerLookInput")]
        [HarmonyPrefix]
        private static bool KeepHeadStill(PlayerControllerB __instance)
        {
            return !(TerminalMouse.InUse && __instance.inTerminalMenu);
        }

        // The game moves a third of the page per wheel turn, whatever its length. Here it is a set number of lines, and
        // on our pages the wheel is ours already.
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
