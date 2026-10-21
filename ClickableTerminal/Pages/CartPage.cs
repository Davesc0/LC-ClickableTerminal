using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClickableTerminal
{
    // Kept by place in the game's item list.
    internal static class Cart
    {
        private static readonly Dictionary<int, int> Amounts = new Dictionary<int, int>();

        internal static int Count => Amounts.Values.Sum();

        internal static IEnumerable<KeyValuePair<int, int>> Lines => Amounts.OrderBy(pair => pair.Key).ToList();

        internal static int Of(int item)
        {
            return Amounts.TryGetValue(item, out var amount) ? amount : 0;
        }

        // Rounded the way the game rounds when it takes the money.
        internal static int PriceOf(Terminal terminal, int item)
        {
            var items = terminal.buyableItemsList;
            if (items == null || item < 0 || item >= items.Length || items[item] == null)
            {
                return 0;
            }

            var sales = terminal.itemSalesPercentages;
            var sale = sales != null && item < sales.Length ? sales[item] : 100;
            return (int)(items[item].creditsWorth * (sale / 100f));
        }

        internal static int Total(Terminal terminal)
        {
            return Amounts.Sum(pair => PriceOf(terminal, pair.Key) * pair.Value);
        }

        internal static bool CanAdd(Terminal terminal, int item)
        {
            return Count + terminal.numberOfItemsInDropship < DropshipLimit.Value
                   && Total(terminal) + PriceOf(terminal, item) <= terminal.groupCredits;
        }

        internal static bool Add(Terminal terminal, int item)
        {
            if (!CanAdd(terminal, item))
            {
                return false;
            }

            Amounts[item] = Of(item) + 1;
            return true;
        }

        internal static void Remove(int item)
        {
            var left = Of(item) - 1;
            if (left > 0)
            {
                Amounts[item] = left;
            }
            else
            {
                Amounts.Remove(item);
            }
        }

        internal static void Clear()
        {
            Amounts.Clear();
        }

        // Things a mod has taken out of the store since they were picked.
        internal static void Tidy(Terminal terminal)
        {
            var items = terminal.buyableItemsList;
            foreach (var item in Amounts.Keys.ToList())
            {
                if (items == null || item >= items.Length || items[item] == null)
                {
                    Amounts.Remove(item);
                }
            }
        }
    }

    internal static class CartPage
    {
        private class Line
        {
            internal string Name;
            internal int Amount;
            internal int Bought;
            internal int Cost;
            internal string Answer;
        }

        // Every line is bought the way "confirm" buys it, without the question pages. The game's checks and every mod
        // that acts on a purchase still run.
        private static bool ordering;

        internal static void Order(Terminal terminal)
        {
            if (ordering || Cart.Count == 0)
            {
                return;
            }

            ordering = true;
            Window.Show(new Window { Title = "CART", Text = "Ordering..." });
            terminal.StartCoroutine(OrderLines(terminal, Cart.Lines.ToList()));
        }

        // One line a frame, so mods that act on a purchase (instant delivery, their own prices) finish one before the
        // next.
        private static System.Collections.IEnumerator OrderLines(Terminal terminal, List<KeyValuePair<int, int>> cart)
        {
            var catalog = Catalog.Read(terminal);
            var items = terminal.buyableItemsList;
            var lines = new List<Line>();

            foreach (var pair in cart)
            {
                var item = pair.Key;
                var line = new Line { Name = items[item].itemName, Amount = pair.Value };
                lines.Add(line);

                if (!catalog.Items.TryGetValue(item, out var target) || target.Confirm == null)
                {
                    line.Answer = "The store does not sell it any more.";
                    continue;
                }

                // The game takes at most twelve of one thing at once.
                var left = pair.Value;
                while (left > 0)
                {
                    var credits = terminal.groupCredits;
                    var onRoute = terminal.numberOfItemsInDropship;
                    var amount = Mathf.Min(left, 12);
                    var answer = Commands.Act(terminal, target.Confirm, amount);
                    left -= amount;

                    yield return null;

                    var took = terminal.groupCredits < credits || terminal.numberOfItemsInDropship > onRoute;
                    if (took)
                    {
                        line.Bought += amount;
                        line.Cost += Mathf.Max(0, credits - terminal.groupCredits);
                    }
                    else
                    {
                        line.Answer = FirstLine(answer);
                        break;
                    }
                }

                if (line.Bought > 0)
                {
                    for (var i = 0; i < line.Bought; i++)
                    {
                        Cart.Remove(item);
                    }
                }
            }

            ordering = false;
            Window.Show(Receipt(terminal, lines));
            Navigator.Rebuild(terminal);
        }

        private static string FirstLine(string answer)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(answer ?? "", "<[^>]*>", "");
            return text.Split('\n').Select(each => each.Trim()).FirstOrDefault(each => each.Length > 0) ?? "";
        }

        // Lines that did not go through stay in the cart.
        private static Window Receipt(Terminal terminal, List<Line> lines)
        {
            var bought = lines.Where(line => line.Bought > 0).ToList();
            var refused = lines.Where(line => line.Bought < line.Amount).ToList();
            var text = new System.Text.StringBuilder();

            foreach (var line in bought)
            {
                text.Append(line.Bought).Append("x ").Append(line.Name).Append("<pos=75%>$").Append(line.Cost).Append('\n');
            }

            if (bought.Count > 0)
            {
                text.Append("\nTotal<pos=75%>$").Append(bought.Sum(line => line.Cost));
                text.Append("\nNew balance<pos=75%>$").Append(terminal.groupCredits);
                text.Append("\n").Append(terminal.numberOfItemsInDropship).Append(" on the way. The dropship delivers on the hour.\n");
            }

            if (refused.Count > 0)
            {
                text.Append(bought.Count > 0 ? "\n" : "").Append("NOT ORDERED (still in the cart)\n");
                foreach (var line in refused)
                {
                    text.Append(line.Amount - line.Bought).Append("x ").Append(line.Name);
                    if (!string.IsNullOrEmpty(line.Answer))
                    {
                        text.Append("\n<alpha=#99>  ").Append(line.Answer).Append("<alpha=#FF>");
                    }

                    text.Append('\n');
                }
            }

            return new Window { Title = bought.Count > 0 ? "ORDER PLACED" : "NOTHING WAS ORDERED", Text = text.ToString().TrimEnd() };
        }
    }
}
