using HarmonyLib;

namespace ClickableTerminal.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal static class StartOfRoundPatches
    {
        [HarmonyPatch(nameof(StartOfRound.Awake))]
        [HarmonyPostfix]
        private static void NewLobby()
        {
            MoonMemory.Forget();
        }

        [HarmonyPatch(nameof(StartOfRound.ArriveAtLevel))]
        [HarmonyPostfix]
        private static void RememberFlight(StartOfRound __instance)
        {
            MoonMemory.RememberFlight(__instance.currentLevel);
        }
    }

    [HarmonyPatch(typeof(GameNetworkManager))]
    internal static class SavePatches
    {
        [HarmonyPatch(nameof(GameNetworkManager.SaveItemsInShip))]
        [HarmonyPostfix]
        private static void SaveMoons()
        {
            MoonMemory.Save();
        }
    }
}
