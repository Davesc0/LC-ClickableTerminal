using HarmonyLib;

namespace ClickableTerminal.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal static class StartOfRoundPatches
    {
        [HarmonyPatch(nameof(StartOfRound.ArriveAtLevel))]
        [HarmonyPostfix]
        private static void RememberFlight(StartOfRound __instance)
        {
            MoonMemory.RememberFlight(__instance.currentLevel);
        }

        [HarmonyPatch(nameof(StartOfRound.OnShipLandedMiscEvents))]
        [HarmonyPostfix]
        private static void RememberLanding(StartOfRound __instance)
        {
            MoonMemory.RememberLanding(__instance.currentLevel);
        }
    }
}
