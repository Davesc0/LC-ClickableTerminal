using HarmonyLib;

namespace ClickableTerminal.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal static class StartOfRoundPatches
    {
        // Runs for every player when the ship reaches the orbit of a moon.
        [HarmonyPatch(nameof(StartOfRound.ArriveAtLevel))]
        [HarmonyPostfix]
        private static void RememberFlight(StartOfRound __instance)
        {
            MoonMemory.RememberFlight(__instance.currentLevel);
        }

        // Runs for every player once the ship has touched down.
        [HarmonyPatch(nameof(StartOfRound.OnShipLandedMiscEvents))]
        [HarmonyPostfix]
        private static void RememberLanding(StartOfRound __instance)
        {
            MoonMemory.RememberLanding(__instance.currentLevel);
        }
    }
}
