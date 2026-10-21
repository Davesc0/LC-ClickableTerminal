using UnityEngine;

namespace ClickableTerminal
{
    internal static class AmountStepper
    {
        internal const string Less = "[ - ]";
        internal const string More = "[ + ]";

        internal static bool AppliesTo(Terminal terminal, TerminalNode node)
        {
            if (node == null || !node.isConfirmationNode || !node.overrideOptions)
            {
                return false;
            }

            var items = terminal.buyableItemsList;
            return node.buyItemIndex == -7
                   || (items != null && node.buyItemIndex >= 0 && node.buyItemIndex < items.Length);
        }

        internal static void Change(Terminal terminal, int by)
        {
            var node = terminal.currentNode;
            var amount = terminal.playerDefinedAmount + by;

            if (!AppliesTo(terminal, node) || !CanSet(terminal, node, amount))
            {
                return;
            }

            terminal.playerDefinedAmount = amount;
            terminal.LoadNewNodeIfAffordable(node);
        }

        internal static bool CanSet(Terminal terminal, TerminalNode node, int amount)
        {
            if (amount < 1 || amount > 10)
            {
                return false;
            }

            var perOrder = node.buyItemIndex == -7 ? 9 : 1;
            return Cost(terminal, node, amount) <= terminal.groupCredits
                   && amount * perOrder + terminal.numberOfItemsInDropship <= 12;
        }

        private static int Cost(Terminal terminal, TerminalNode node, int amount)
        {
            if (node.buyItemIndex == -7)
            {
                return node.itemCost * amount;
            }

            var sales = terminal.itemSalesPercentages;
            var sale = sales != null && node.buyItemIndex < sales.Length ? sales[node.buyItemIndex] : 100;
            return (int)(terminal.buyableItemsList[node.buyItemIndex].creditsWorth * (sale / 100f) * amount);
        }
    }
}
