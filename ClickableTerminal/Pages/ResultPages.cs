using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class ResultPages
    {
        // "* Facility   // Chance: 29.76%"
        private static readonly Regex Chance = new Regex(@"^\s*\*\s*(.+?)\s*//\s*Chance:\s*([0-9.,]+\s*%)", RegexOptions.Compiled);
        private static readonly Regex Arrival = new Regex(@"arrival to\s+\d*\s*(.+?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Rule = new Regex(@"^[-=_]{3,}$", RegexOptions.Compiled);
        private static readonly Regex Field = new Regex(@"^[A-Z][A-Za-z ]{1,18}:\s+\S.{0,36}$", RegexOptions.Compiled);
        private static readonly Regex ConfirmOrDeny = new Regex(@"\bconfirm\s+or\s+deny\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Labelled = new Regex(@"^([A-Z][A-Z ]{2,24}):\s*(.*)$", RegexOptions.Compiled);

        internal static MenuPage CannotAfford(Terminal terminal, string back)
        {
            var cost = terminal.totalCostOfItems;
            var credits = terminal.groupCredits;

            var page = new MenuPage { Title = "NOT ENOUGH CREDITS", HideHeads = true, Plain = true, Dialog = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 12, AlignRight = true });

            page.Entries.Add(new MenuEntry { Label = "You cannot afford this.", Big = true });
            Space(page);
            Fact(page, "Cost", "$" + cost);
            Fact(page, "Your credits", "$" + credits);
            Fact(page, "Missing", "$" + Mathf.Max(0, cost - credits));
            AddBack(page, terminal, back);
            return page;
        }

        internal static bool IsReceipt(Terminal terminal, TerminalNode node)
        {
            return !node.isConfirmationNode && !node.overrideOptions
                   && (node.buyItemIndex != -1 || node.buyVehicleIndex != -1 || node.shipUnlockableID != -1 || node.buyRerouteToMoon >= 0);
        }

        internal static MenuPage Receipt(Terminal terminal, TerminalNode node, string back)
        {
            var round = StartOfRound.Instance;
            var items = terminal.buyableItemsList;
            var vehicles = terminal.buyableVehicles;
            var unlockables = round != null && round.unlockablesList != null ? round.unlockablesList.unlockables : null;

            string heading;
            string what;
            string note = null;

            if (node.buyRerouteToMoon >= 0 && round != null && node.buyRerouteToMoon < round.levels.Length)
            {
                heading = "Autopilot routed";
                what = MoonsPage.NameOf(round.levels[node.buyRerouteToMoon]);
            }
            else if (node.buyItemIndex != -1)
            {
                var amount = Mathf.Max(1, terminal.playerDefinedAmount);
                var name = node.buyItemIndex >= 0 && items != null && node.buyItemIndex < items.Length ? items[node.buyItemIndex].itemName : "Survival kit";

                heading = "Order placed";
                what = amount > 1 ? $"{name}  x{amount}" : name;
                note = $"{terminal.numberOfItemsInDropship} on the way. The dropship delivers on the hour.";
            }
            else if (node.buyVehicleIndex >= 0 && vehicles != null && node.buyVehicleIndex < vehicles.Length)
            {
                heading = "Order placed";
                what = vehicles[node.buyVehicleIndex].vehicleDisplayName;
                note = "The dropship delivers on the hour.";
            }
            else if (node.shipUnlockableID >= 0 && unlockables != null && node.shipUnlockableID < unlockables.Count)
            {
                heading = "Order placed";
                what = unlockables[node.shipUnlockableID].unlockableName;
            }
            else
            {
                return null;
            }

            var page = new MenuPage { Title = "RECEIPT", HideHeads = true, Plain = true, Dialog = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 12, AlignRight = true });

            Space(page);
            page.Entries.Add(new MenuEntry { Label = heading, Big = true });
            Space(page);
            Fact(page, what, "$" + terminal.totalCostOfItems);
            Fact(page, "New balance", "$" + terminal.groupCredits);

            if (note != null)
            {
                Space(page);
                page.Entries.Add(new MenuEntry { Label = note, Dim = true });
            }

            AddBack(page, terminal, back);
            return page;
        }

        internal static bool ListsChances(string printed)
        {
            foreach (var line in printed.Split('\n'))
            {
                if (Chance.IsMatch(Tags.Replace(line, "")))
                {
                    return true;
                }
            }

            return false;
        }

        internal static MenuPage Chances(Terminal terminal, string printed, string back)
        {
            var page = new MenuPage { Title = "SIMULATE", HideHeads = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 9, AlignRight = true });

            foreach (var raw in printed.Split('\n'))
            {
                var line = Tags.Replace(raw, "").Trim();
                var entry = Chance.Match(line);
                var arrival = Arrival.Match(line);

                if (entry.Success)
                {
                    Fact(page, Tidy(entry.Groups[1].Value), entry.Groups[2].Value.Replace(" ", ""));
                }
                else if (arrival.Success && page.Summary == null)
                {
                    page.Summary = "Arrival at " + arrival.Groups[1].Value;
                }
                else if (line.Length > 1 && line.EndsWith(":") && !line.Contains("//"))
                {
                    // "POSSIBLE STRUCTURES:" and whatever other lists a mod prints under a heading.
                    if (page.Entries.Count > 0)
                    {
                        Space(page);
                    }

                    page.Entries.Add(new MenuEntry { Label = line.TrimEnd(':'), IsHeader = true });
                }
            }

            AddBack(page, terminal, back);
            return page;
        }

        internal static bool IsProse(string printed)
        {
            var any = false;

            foreach (var raw in printed.Split('\n'))
            {
                var line = Tags.Replace(raw, "").Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '*' || line[0] == '>' || line.Contains("//"))
                {
                    return false;
                }

                any = true;
            }

            return any;
        }

        internal static MenuPage Document(Terminal terminal, string printed, string back)
        {
            var lines = new List<string>();
            foreach (var raw in printed.Split('\n'))
            {
                lines.Add(Tags.Replace(raw, "").Trim());
            }

            var page = new MenuPage { Title = "NOTICE", Plain = true, Dense = true };
            var start = lines.FindIndex(line => line.Length > 0);

            // "41-Experimentation" over a row of dashes is the name of what the page is about.
            if (start >= 0 && start + 1 < lines.Count && Rule.IsMatch(lines[start + 1]))
            {
                Space(page);
                page.Entries.Add(new MenuEntry { Label = lines[start], Big = true });
                page.Title = "INFO";
                start += 2;
            }

            var paragraph = "";
            for (var i = Mathf.Max(0, start); i <= lines.Count; i++)
            {
                var line = i < lines.Count ? lines[i] : "";

                // "Date: August 22, 1968" is a line of its own, not the start of the next sentence.
                if (Field.IsMatch(line))
                {
                    if (paragraph.Length > 0)
                    {
                        AddParagraph(page, paragraph);
                    }

                    AddParagraph(page, line);
                    paragraph = "";
                    continue;
                }

                if (line.Length > 0)
                {
                    paragraph = paragraph.Length > 0 ? paragraph + " " + line : line;
                    continue;
                }

                if (paragraph.Length > 0)
                {
                    AddParagraph(page, paragraph);
                    paragraph = "";
                }
            }

            AddBack(page, terminal, back);
            return page;
        }

        internal static MenuPage Log(Terminal terminal, TerminalNode node, string printed, string back)
        {
            var page = Document(terminal, printed, back);
            page.Title = "LOG";

            if (!string.IsNullOrWhiteSpace(node.creatureName))
            {
                page.Entries.Insert(0, new MenuEntry { Label = "", IsHeader = true });
                page.Entries.Insert(0, new MenuEntry { Label = node.creatureName, Big = true });
                page.Entries.Insert(0, new MenuEntry { Label = "", IsHeader = true });
            }

            return page;
        }

        internal static bool AsksToConfirm(string printed)
        {
            return ConfirmOrDeny.IsMatch(Tags.Replace(printed, ""));
        }

        internal static MenuPage Question(string printed, IEnumerable<string> answers)
        {
            var page = new MenuPage { Title = "CONFIRM", Plain = true };
            var text = "";

            foreach (var raw in printed.Split('\n'))
            {
                // "Please CONFIRM or DENY." is what the buttons are for.
                var line = Tags.Replace(raw, "").Trim().TrimStart('>', '*', ' ');
                if (line.Length > 0 && !ConfirmOrDeny.IsMatch(line))
                {
                    text = text.Length > 0 ? text + " " + line : line;
                }
            }

            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            foreach (var line in Wrap(text, 44))
            {
                page.Entries.Add(new MenuEntry { Label = line });
            }

            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });

            foreach (var answer in answers)
            {
                page.Entries.Add(new MenuEntry { Label = "[ " + answer.ToUpperInvariant() + " ]", Command = answer });
            }

            return page;
        }

        internal static MenuPage Creature(Terminal terminal, TerminalNode node, string printed, string back)
        {
            var page = Document(terminal, printed, back);
            page.Title = "CREATURE FILE";
            page.HideHeads = true;
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 24 });

            var top = new List<MenuEntry>();
            top.Add(new MenuEntry { Label = "", IsHeader = true });
            top.Add(new MenuEntry { Label = node.creatureName ?? "", Big = true });

            var enemy = CreatureIndex.Find(node);
            if (enemy != null)
            {
                var health = CreatureIndex.HealthOf(enemy);

                Figure(top, "Health", health.HasValue ? health.Value.ToString() : "Cannot be killed");
                Figure(top, "Power", enemy.PowerLevel.ToString("0.#"));
                Figure(top, "Max at once", enemy.MaxCount.ToString());
                Figure(top, "Stunnable", enemy.canBeStunned ? "Yes" : "No");
                Figure(top, "Habitat", enemy.isDaytimeEnemy ? "Surface, daytime" : enemy.isOutsideEnemy ? "Surface" : "Interior");
            }

            top.Add(new MenuEntry { Label = "", IsHeader = true });
            page.Entries.InsertRange(0, top);
            return page;
        }

        private static void Figure(List<MenuEntry> into, string label, string value)
        {
            into.Add(new MenuEntry { Label = label, Cells = new[] { value } });
        }

        private static void AddParagraph(MenuPage page, string paragraph)
        {
            if (page.Entries.Count > 0 && !string.IsNullOrEmpty(page.Entries[page.Entries.Count - 1].Label))
            {
                Space(page);
            }

            var labelled = Labelled.Match(paragraph);
            if (labelled.Success)
            {
                page.Entries.Add(new MenuEntry { Label = labelled.Groups[1].Value.Trim(), IsHeader = true });
                page.Title = "INFO";
                paragraph = labelled.Groups[2].Value;
            }

            foreach (var line in Wrap(paragraph, labelled.Success ? 40 : 44))
            {
                page.Entries.Add(new MenuEntry { Label = line });
            }
        }

        // Rows are one line each and get cut off, so the sentences are broken up here.
        private static IEnumerable<string> Wrap(string text, int width)
        {
            width = Mathf.RoundToInt(width / Mathf.Clamp(Settings.TextScale.Value, 0.6f, 1f));
            var line = "";

            foreach (var word in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    yield return line;
                    line = word;
                }
                else
                {
                    line = line.Length > 0 ? line + " " + word : word;
                }
            }

            if (line.Length > 0)
            {
                yield return line;
            }
        }

        // "Toystoreflow", "Level_three"
        private static string Tidy(string name)
        {
            name = name.Replace('_', ' ').Trim();
            return name.Length > 6 && name.EndsWith("flow", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 4)
                : name;
        }

        internal static void AddBack(MenuPage page, Terminal terminal, string back)
        {
            Space(page);
            page.Entries.Add(new MenuEntry
            {
                Label = back != null ? $"[ BACK TO {back} ]" : "[ BACK ]",
                Click = () => Navigator.Back(terminal)
            });
        }

        private static void Fact(MenuPage page, string label, string value)
        {
            page.Entries.Add(new MenuEntry { Label = label, Cells = new[] { value } });
        }

        private static void Space(MenuPage page)
        {
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
        }
    }
}
