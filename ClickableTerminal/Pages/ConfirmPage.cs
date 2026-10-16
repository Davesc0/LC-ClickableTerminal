using System;
using UnityEngine;

namespace ClickableTerminal
{
    // The "Please CONFIRM or DENY" question, laid out as figures with two buttons.
    internal static class ConfirmPage
    {
        internal static MenuPage Build(Terminal terminal, TerminalNode node)
        {
            string confirm = null;
            string deny = null;
            TerminalNode confirmed = null;

            if (node.terminalOptions != null)
            {
                foreach (var option in node.terminalOptions)
                {
                    if (option?.noun == null)
                    {
                        continue;
                    }

                    if (option.noun.word == "confirm")
                    {
                        confirm = option.noun.word;
                        confirmed = option.result;
                    }
                    else if (option.noun.word == "deny")
                    {
                        deny = option.noun.word;
                    }
                }
            }

            if (confirm == null || deny == null)
            {
                return null;
            }

            // Flying somewhere is one choice with no amount to it, so it does not use the order form.
            var destination = StartOfRound.Instance;
            var target = confirmed != null ? confirmed.buyRerouteToMoon : -1;
            if (destination != null && destination.levels != null && target >= 0 && target < destination.levels.Length
                && destination.levels[target] != null)
            {
                return Route(terminal, destination.levels[target], confirm, deny);
            }

            var page = new MenuPage { Title = "CONFIRM", HideHeads = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 6 });
            page.Columns.Add(new Column { Chars = 5 });
            page.Columns.Add(new Column { Chars = 8, AlignRight = true });

            // What the game is about to charge, worked out before it showed the question.
            var total = terminal.totalCostOfItems;
            var round = StartOfRound.Instance;
            var unlockables = round != null && round.unlockablesList != null ? round.unlockablesList.unlockables : null;
            var vehicles = terminal.buyableVehicles;

            if (AmountStepper.AppliesTo(terminal, node))
            {
                var amount = Mathf.Max(1, terminal.playerDefinedAmount);
                var name = node.buyItemIndex >= 0 ? terminal.buyableItemsList[node.buyItemIndex].itemName : "Survival kit";

                Bar(page, "You have requested to order:");
                Line(page, name, "$" + total / amount);
                page.Entries.Add(new MenuEntry
                {
                    Label = "Amount",
                    Cells = new[]
                    {
                        AmountStepper.CanSet(terminal, node, amount - 1) ? AmountStepper.Less : "",
                        amount.ToString(),
                        AmountStepper.CanSet(terminal, node, amount + 1) ? AmountStepper.More : ""
                    },
                    CellClicks = new Action[]
                    {
                        () => AmountStepper.Change(terminal, -1),
                        null,
                        () => AmountStepper.Change(terminal, 1)
                    }
                });
            }
            else if (vehicles != null && node.buyVehicleIndex >= 0 && node.buyVehicleIndex < vehicles.Length)
            {
                Bar(page, "You have requested to order:");
                Line(page, vehicles[node.buyVehicleIndex].vehicleDisplayName, "$" + total);

                if (terminal.hasWarrantyTicket)
                {
                    total = 0;
                    Line(page, "Covered by your warranty", "");
                }
            }
            else if (unlockables != null && node.shipUnlockableID >= 0 && node.shipUnlockableID < unlockables.Count)
            {
                Bar(page, "You have requested to order:");
                Line(page, unlockables[node.shipUnlockableID].unlockableName, "$" + total);
            }
            else
            {
                // Something we know nothing about (eject, a mod's own question): the game's text stays.
                return null;
            }

            Space(page);
            Line(page, "Total", "$" + total);
            Line(page, "Credits left after", "$" + Mathf.Max(0, terminal.groupCredits - total));
            Space(page);
            page.Entries.Add(new MenuEntry { Label = "[ CONFIRM ]", Command = confirm });
            page.Entries.Add(new MenuEntry { Label = "[ DENY ]", Command = deny });
            return page;
        }

        private static MenuPage Route(Terminal terminal, SelectableLevel level, string confirm, string deny)
        {
            var cost = terminal.totalCostOfItems;
            var page = new MenuPage { Title = "ROUTE", HideHeads = true, Plain = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 18 });

            Space(page);
            page.Entries.Add(new MenuEntry { Label = MoonsPage.NameOf(level), Big = true });
            Fact(page, "Weather", level.currentWeather == LevelWeatherType.None ? "Clear" : level.currentWeather.ToString());
            Fact(page, "Difficulty", string.IsNullOrEmpty(level.riskLevel) ? "-" : level.riskLevel);
            Space(page);
            Fact(page, "Cost", "$" + cost);
            if (cost > terminal.groupCredits)
            {
                Fact(page, "Missing", "$" + (cost - terminal.groupCredits));
            }
            else
            {
                Fact(page, "Remaining", "$" + (terminal.groupCredits - cost));
            }

            Space(page);
            page.Entries.Add(new MenuEntry { Label = "[ CONFIRM ]", Command = confirm });
            page.Entries.Add(new MenuEntry { Label = "[ DENY ]", Command = deny });
            return page;
        }

        private static void Fact(MenuPage page, string label, string value)
        {
            page.Entries.Add(new MenuEntry { Label = label, Cells = new[] { value } });
        }

        private static void Bar(MenuPage page, string text)
        {
            page.Entries.Add(new MenuEntry { Label = text, IsHeader = true });
        }

        private static void Space(MenuPage page)
        {
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
        }

        private static void Line(MenuPage page, string label, string value)
        {
            page.Entries.Add(new MenuEntry { Label = label, Cells = new[] { "", "", value } });
        }
    }
}
