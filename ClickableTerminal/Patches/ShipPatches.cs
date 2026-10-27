using System.Collections.Generic;
using HarmonyLib;

namespace ClickableTerminal.Patches
{
    // What the STATUS panel shows about the ship changes only when the game says so: scrap is set down or picked up, the ship's items are loaded, cleared at the end of a round or sold, a teleporter comes into the world. So the panel is told then, instead of searching the scene again and again.
    internal static class ShipWatch
    {
        internal static bool ScrapMoved = true;
        internal static readonly List<ShipTeleporter> Teleporters = new List<ShipTeleporter>();
    }

    [HarmonyPatch]
    internal static class ShipPatches
    {
        [HarmonyPatch(typeof(ShipTeleporter), nameof(ShipTeleporter.Awake))]
        [HarmonyPostfix]
        private static void TeleporterArrived(ShipTeleporter __instance)
        {
            if (!ShipWatch.Teleporters.Contains(__instance))
            {
                ShipWatch.Teleporters.Add(__instance);
            }
        }

        [HarmonyPatch(typeof(GameNetcodeStuff.PlayerControllerB), nameof(GameNetcodeStuff.PlayerControllerB.SetItemInElevator))]
        [HarmonyPostfix]
        private static void ScrapSetDown()
        {
            ShipWatch.ScrapMoved = true;
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadShipGrabbableItems))]
        [HarmonyPostfix]
        private static void ShipLoaded()
        {
            ShipWatch.ScrapMoved = true;
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
        [HarmonyPostfix]
        private static void RoundCleared()
        {
            ShipWatch.ScrapMoved = true;
        }

        [HarmonyPatch(typeof(DepositItemsDesk), nameof(DepositItemsDesk.SellAndDisplayItemProfits))]
        [HarmonyPostfix]
        private static void ScrapSold()
        {
            ShipWatch.ScrapMoved = true;
        }
    }
}
