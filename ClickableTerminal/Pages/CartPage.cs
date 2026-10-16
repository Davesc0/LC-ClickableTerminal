using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClickableTerminal
{
    // The items picked in the store, to be ordered together. Kept by their place in the game's list of items.
    internal static class Cart
    {
        private const int DropshipHolds = 12;
        private const int MostOfOne = 10;

        private static readonly Dictionary<int, int> Amounts = new Dictionary<int, int>();

        internal static int Count => Amounts.Values.Sum();

        internal static IEnumerable<KeyValuePair<int, int>> Lines => Amounts.OrderBy(pair => pair.Key).ToList();

        internal static int Of(int item)
        {
            return Amounts.TryGetValue(item, out var amount) ? amount : 0;
        }

        // Rounded the way the game does it when it takes the money.
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

        // The same limits the game has for an order: what the dropship holds, and what the crew can pay.
        internal static bool CanAdd(Terminal terminal, int item)
        {
            return Of(item) < MostOfOne
                   && Count + terminal.numberOfItemsInDropship < DropshipHolds
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
            internal int Cost;
            internal bool Ordered;
        }

        internal static MenuPage Build(Terminal terminal)
        {
            Cart.Tidy(terminal);

            var page = new MenuPage { Title = "CART", HideHeads = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 5 });
            page.Columns.Add(new Column { Chars = 12, AlignRight = true });
            page.Columns.Add(new Column { Chars = 5 });

            var items = terminal.buyableItemsList;
            var total = Cart.Total(terminal);

            if (Cart.Count == 0)
            {
                Space(page);
                page.Entries.Add(new MenuEntry { Label = "The cart is empty.", Big = true });
                page.Entries.Add(new MenuEntry { Label = "Click items in the store to put them in." });
                Space(page);
                page.Entries.Add(new MenuEntry { Label = "[ BACK ]", Click = () => Navigator.Back(terminal) });
                return page;
            }

            page.Entries.Add(new MenuEntry { Label = "YOUR ORDER", IsHeader = true });

            foreach (var line in Cart.Lines)
            {
                var item = line.Key;
                var amount = line.Value;

                page.Entries.Add(new MenuEntry
                {
                    Label = items[item].itemName,
                    Cells = new[] { "[ - ]", $"x{amount}  ${Cart.PriceOf(terminal, item) * amount}", Cart.CanAdd(terminal, item) ? "[ + ]" : "" },
                    CellClicks = new Action[]
                    {
                        () =>
                        {
                            Cart.Remove(item);
                            Navigator.Rebuild(terminal);
                        },
                        null,
                        () =>
                        {
                            Cart.Add(terminal, item);
                            Navigator.Rebuild(terminal);
                        }
                    }
                });
            }

            Space(page);
            Fact(page, "Total", "$" + total);
            Fact(page, "Credits left after", "$" + Mathf.Max(0, terminal.groupCredits - total));
            Fact(page, "In the dropship", $"{Cart.Count + terminal.numberOfItemsInDropship} of 12");
            Space(page);
            page.Entries.Add(new MenuEntry { Label = "[ ORDER ]", Click = () => Order(terminal) });
            page.Entries.Add(new MenuEntry
            {
                Label = "[ EMPTY THE CART ]",
                Click = () =>
                {
                    Cart.Clear();
                    Navigator.Rebuild(terminal);
                }
            });
            return page;
        }

        // Every line is bought the way a player would: the item's command, the amount, then "confirm".
        // So whatever a mod has to say about a purchase (its own price, a refusal) still counts.
        private static bool ordering;

        private static void Order(Terminal terminal)
        {
            if (ordering)
            {
                return;
            }

            ordering = true;
            Navigator.DropCurrent();
            Navigator.Open(() => Waiting(terminal));
            terminal.StartCoroutine(OrderLines(terminal, Cart.Lines.ToList()));
        }

        private static MenuPage Waiting(Terminal terminal)
        {
            var page = new MenuPage { Title = "CART", Plain = true };
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            page.Entries.Add(new MenuEntry { Label = "Ordering...", Big = true });
            return page;
        }

        // A line a frame: mods that act on a purchase (instant delivery, their own prices) get to finish one
        // before the next comes. A question left open on the way is answered "deny", so nothing is left waiting.
        private static System.Collections.IEnumerator OrderLines(Terminal terminal, List<KeyValuePair<int, int>> cart)
        {
            var catalog = Catalog.Read(terminal);
            var items = terminal.buyableItemsList;
            var lines = new List<Line>();

            foreach (var pair in cart)
            {
                var item = pair.Key;
                var amount = pair.Value;
                var line = new Line { Name = items[item].itemName, Amount = amount };
                lines.Add(line);

                if (!catalog.Items.TryGetValue(item, out var target) || !terminal.terminalInUse)
                {
                    continue;
                }

                var credits = terminal.groupCredits;
                var onRoute = terminal.numberOfItemsInDropship;

                try
                {
                Navigator.Quietly(() =>
                {
                    Commands.Submit(terminal, target.Command);

                    var asked = terminal.currentNode;
                    if (!AmountStepper.AppliesTo(terminal, asked) || asked.buyItemIndex != item)
                    {
                        return;
                    }

                    if (amount > 1)
                    {
                        terminal.playerDefinedAmount = amount;
                        terminal.LoadNewNodeIfAffordable(asked);
                        if (terminal.currentNode != asked)
                        {
                            return;
                        }
                    }

                    Commands.Submit(terminal, "confirm");
                });
                }
                catch (System.Exception e)
                {
                    Plugin.mlg.LogWarning($"Cart: ordering {line.Name} failed: {e}");
                }

                yield return null;

                LeaveQuestion(terminal);
                line.Ordered = terminal.groupCredits < credits || terminal.numberOfItemsInDropship > onRoute;
                line.Cost = Mathf.Max(0, credits - terminal.groupCredits);
            }

            Cart.Clear();
            ordering = false;
            Navigator.DropCurrent();
            Navigator.Open(() => Receipt(terminal, lines));
        }

        private static void LeaveQuestion(Terminal terminal)
        {
            var node = terminal.currentNode;
            if (node != null && node.overrideOptions && node.terminalOptions != null
                && node.terminalOptions.Any(option => option?.noun != null && option.noun.word == "deny"))
            {
                Navigator.Quietly(() => Commands.Submit(terminal, "deny"));
            }
        }

        private static MenuPage Receipt(Terminal terminal, List<Line> lines)
        {
            var page = new MenuPage { Title = "RECEIPT", HideHeads = true, Plain = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 5 });
            page.Columns.Add(new Column { Chars = 12, AlignRight = true });

            var ordered = lines.Where(line => line.Ordered).ToList();
            var refused = lines.Where(line => !line.Ordered).ToList();

            Space(page);
            page.Entries.Add(new MenuEntry { Label = ordered.Count > 0 ? "Order placed" : "Nothing was ordered", Big = true });
            Space(page);

            foreach (var line in ordered)
            {
                page.Entries.Add(new MenuEntry { Label = line.Name, Cells = new[] { "x" + line.Amount, "$" + line.Cost } });
            }

            if (ordered.Count > 0)
            {
                Space(page);
                page.Entries.Add(new MenuEntry { Label = "Total", Cells = new[] { "", "$" + ordered.Sum(line => line.Cost) } });
                page.Entries.Add(new MenuEntry { Label = "New balance", Cells = new[] { "", "$" + terminal.groupCredits } });
            }

            if (refused.Count > 0)
            {
                Space(page);
                page.Entries.Add(new MenuEntry { Label = "COULD NOT BE ORDERED", IsHeader = true });
                foreach (var line in refused)
                {
                    page.Entries.Add(new MenuEntry { Label = line.Name, Cells = new[] { "x" + line.Amount, "" }, Dim = true });
                }
            }

            if (ordered.Count > 0)
            {
                Space(page);
                page.Entries.Add(new MenuEntry { Label = $"{terminal.numberOfItemsInDropship} on the way. The dropship delivers on the hour.", Dim = true });
            }

            ResultPages.AddBack(page, terminal, null);
            return page;
        }

        private static void Fact(MenuPage page, string label, string value)
        {
            page.Entries.Add(new MenuEntry { Label = label, Cells = new[] { "", value, "" } });
        }

        private static void Space(MenuPage page)
        {
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
        }
    }
}
