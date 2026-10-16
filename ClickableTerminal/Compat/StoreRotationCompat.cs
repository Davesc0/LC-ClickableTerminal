using System;
using System.Collections;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace ClickableTerminal
{
    // StoreRotationConfig decides which decorations and suits are on sale and puts some of them on discount.
    // While it is installed the choice is left to it, and its discounts are shown on our store pages.
    internal static class StoreRotationCompat
    {
        internal const string Guid = "pacoito.StoreRotationConfig";

        private static PropertyInfo instance;
        private static PropertyInfo rotation;
        private static bool looked;

        internal static bool Installed => Chainloader.PluginInfos.ContainsKey(Guid);

        // The discount in percent on a decoration or suit of the current selection, 0 when there is none.
        // Its list runs in the same order as the game's selection, so the place in one is the place in the other.
        internal static int Discount(Terminal terminal, TerminalNode node)
        {
            if (node == null || terminal.ShipDecorSelection == null || !Installed)
            {
                return 0;
            }

            try
            {
                Find();
                var networker = instance?.GetValue(null);
                var entries = networker != null ? rotation?.GetValue(networker) as IEnumerable : null;
                if (entries == null)
                {
                    return 0;
                }

                var index = node.buyUnlockable
                    ? terminal.ShipDecorSelection.FindIndex(other => other != null && other.shipUnlockableID == node.shipUnlockableID)
                    : terminal.ShipDecorSelection.IndexOf(node);

                var at = 0;
                foreach (var entry in entries)
                {
                    if (at++ == index)
                    {
                        return (int)entry.GetType().GetProperty("UnlockableDiscount", AccessTools.all).GetValue(entry);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.mlg.LogWarning($"StoreRotationConfig: could not read its discounts: {e.Message}");
                looked = true;
                instance = null;
            }

            return 0;
        }

        // The price it charges: its own formula, rounded the way it rounds.
        internal static int PriceAfter(int price, int discount)
        {
            return price - (int)(price * (discount / 100f));
        }

        private static void Find()
        {
            if (looked)
            {
                return;
            }

            looked = true;
            var networker = Reflect.Find("StoreRotationConfig.Networking.StoreRotationNetworker");
            instance = networker?.GetProperty("Instance", AccessTools.all);
            rotation = networker?.GetProperty("StoreRotation", AccessTools.all);
        }
    }
}
