using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;

namespace ClickableTerminal
{
    internal static class SellMyScrapPages
    {
        private const string Guid = "com.github.zehsteam.SellMyScrap";

        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Found = new Regex(@"^Found (\d+) items? with a total value of \$(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex Catalogue = new Regex(@"^Found (\d+) total items from all moons\. \(Page (\d+) / (\d+)\)", RegexOptions.Compiled);
        private static readonly Regex Quota = new Regex(@"^Quota fulfilled: (\$-?\d+ / \$-?\d+)(?: \(Need: (\$-?\d+)\))?", RegexOptions.Compiled);
        private static readonly Regex Overtime = new Regex(@"^Overtime bonus: (\$-?\d+)", RegexOptions.Compiled);
        private static readonly Regex Rate = new Regex(@"buying at %(\d+)", RegexOptions.Compiled);
        private static readonly Regex Asks = new Regex(@"\bCONFIRM or DENY\b", RegexOptions.Compiled);

        // "Bell (x2) : $127" and "Bell (x2) $127 (Vehicle)"
        private static readonly Regex Item = new Regex(@"^(.+?)\s*\(x(\d+)\)\s*:?\s*(\$-?\d+)\s*(\(\w+\))?$", RegexOptions.Compiled);
        private static readonly Regex Gap = new Regex(@"\s{2,}", RegexOptions.Compiled);

        private static readonly PageState State = new PageState();
        private static readonly PageState ItemState = new PageState();

        // ScrapBank grabs everything that starts with "sell". SellMyScrap also takes its commands with a dash, which
        // ScrapBank leaves alone.
        private static string Spelled(string command, string dashed)
        {
            return Chainloader.PluginInfos.ContainsKey("GJ.ScrapBank") ? dashed : command;
        }

        internal static MenuPage Menu()
        {
            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            var page = Navigator.MenuLook(terminal, new MenuPage { Title = "SELL MY SCRAP" });

            page.Entries.Add(new MenuEntry { Label = "SELL QUOTA", Command = Spelled("sell quota", "sell-quota"), Description = "Sell just enough to meet the quota." });
            page.Entries.Add(new MenuEntry { Label = "SELL ALL", Command = Spelled("sell all", "sell-all"), Description = "Sell all the scrap on the ship." });
            page.Entries.Add(new MenuEntry { Label = "SELL AMOUNT", Description = "Sell scrap worth an amount you set.", Click = () => AmountWindow(terminal) });
            page.Entries.Add(new MenuEntry { Label = "SELL ITEM", Description = "Pick a kind of scrap and sell every one of it.", Open = () => ItemPage(terminal) });
            page.Entries.Add(new MenuEntry { Label = "SELL LIST", Command = Spelled("sell list", "sell-list"), Description = "Sell what is on the mod's sell list." });
            page.Entries.Add(new MenuEntry { Label = "VIEW SCRAP", Command = "view scrap", Description = "The scrap on the ship and what it is worth." });
            page.Entries.Add(new MenuEntry { Label = "VIEW ALL SCRAP", Command = "view all scrap", Description = "Every kind of scrap there is in the game." });
            page.Entries.Add(new MenuEntry { Label = "VIEW OVERTIME", Command = "view overtime", Description = "The overtime bonus as it stands." });
            page.Entries.Add(new MenuEntry { Label = "ABOUT", Command = Spelled("sell", "sell-help"), Description = "The mod's own list of its commands." });
            Answers.InWindows(terminal, page);

            // The mod takes whatever comes next as the answer to its last question, so that wait is let go first.
            foreach (var entry in page.Entries)
            {
                var click = entry.Click;
                if (click != null)
                {
                    entry.Click = () =>
                    {
                        LetGo();
                        click();
                    };
                }
            }

            page.Entries.First(entry => entry.Label == "VIEW ALL SCRAP").Click = () =>
            {
                LetGo();
                AllScrap(terminal, "view all scrap");
            };
            return page;
        }

        private static void LetGo()
        {
            try
            {
                Reflect.Find("com.github.zehsteam.SellMyScrap.Commands.CommandManager")?
                    .GetField("AwaitingConfirmationCommand", HarmonyLib.AccessTools.all)?.SetValue(null, null);
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("SellMyScrap: could not let go of its waiting command: " + e.Message);
            }
        }

        private static void AllScrap(Terminal terminal, string command)
        {
            var answer = Commands.Ask(terminal, command);
            var cut = answer.IndexOf("The following commands are available", StringComparison.OrdinalIgnoreCase);
            var window = new Window
            {
                Title = "ALL SCRAP",
                Text = Window.Tidy(cut > 0 ? answer.Substring(0, cut) : answer, ""),
                Closing = LetGo
            };
            window.Buttons.Add(new ToolButton { Label = "PREV", Click = () => AllScrap(terminal, "prev") });
            window.Buttons.Add(new ToolButton { Label = "NEXT", Click = () => AllScrap(terminal, "next") });
            window.Buttons.Add(new ToolButton { Label = "CLOSE", Click = Window.Close });
            Window.Show(window);
        }

        private static void AmountWindow(Terminal terminal)
        {
            var time = TimeOfDay.Instance;
            var needed = time != null ? Math.Max(0, time.profitQuota - time.quotaFulfilled) : 0;
            Answers.Typed(terminal, "SELL AMOUNT", "Type the value of scrap to sell, then Enter.", Spelled("sell ", "sell-amount "), "SELL", 6,
                Answers.Fill(terminal, $"QUOTA NEEDS ${needed}", needed.ToString()));
        }

        private const string ItemTitle = "SELL ITEM";
        private static string report;

        private static MenuPage ItemPage(Terminal terminal)
        {
            var page = new MenuPage
            {
                Title = ItemTitle,
                SortBy = column =>
                {
                    ItemState.SortBy(column);
                    Navigator.Rebuild(terminal);
                }
            };

            page.Columns.Add(new Column { Header = ItemState.HeaderFor("NAME", 0) });
            page.Columns.Add(new Column { Header = ItemState.HeaderFor("COUNT", 1), Chars = 5, AlignRight = true });
            page.Columns.Add(new Column { Header = ItemState.HeaderFor("VALUE", 2), Chars = 9, AlignRight = true });

            var kinds = UnityEngine.Object.FindObjectsOfType<GrabbableObject>()
                .Where(item => item != null && item.itemProperties != null && item.itemProperties.isScrap
                               && item.isInShipRoom && !item.isHeld && !item.isPocketed)
                .GroupBy(item => item.itemProperties.itemName)
                .OrderBy(kind => kind.Key);

            var rows = new List<MenuEntry>();
            foreach (var kind in kinds)
            {
                var name = kind.Key;
                var count = kind.Count();
                var value = kind.Sum(item => item.scrapValue);

                rows.Add(new MenuEntry
                {
                    Label = name,
                    Cells = new[] { "x" + count, "$" + value },
                    Keys = new IComparable[] { name, count, value },
                    Click = () => SellNow(terminal, name, count, value)
                });
            }

            page.Summary = report ?? (rows.Count == 0 ? "No scrap in the ship." : "Click a kind to sell all of it.");
            page.Entries.AddRange(ItemState.Sorted(rows));
            return page;
        }

        private static void SellNow(Terminal terminal, string name, int count, int value)
        {
            if (Sell(terminal, name, out var refused))
            {
                report = refused ?? $"Selling {name} x{count}  (${value})";
                Navigator.Rebuild(terminal);
                terminal.StartCoroutine(Refresh(terminal));
                return;
            }

            report = null;
            Navigator.DropCurrent();
            Commands.Submit(terminal, Spelled("sell item ", "sell-item ") + name);
        }

        // The sold scrap leaves the ship a moment later.
        private static System.Collections.IEnumerator Refresh(Terminal terminal)
        {
            for (var i = 0; i < 16; i++)
            {
                yield return new UnityEngine.WaitForSecondsRealtime(0.5f);

                var view = Navigator.Current;
                if (view == null || view.Page == null || view.Page.Title != ItemTitle)
                {
                    yield break;
                }

                Navigator.Rebuild(terminal);
            }
        }

        // The same steps as the mod's "sell item" and then "confirm". False when the mod cannot be driven this way.
        private static bool Sell(Terminal terminal, string name, out string refused)
        {
            refused = null;

            try
            {
                var assembly = Chainloader.PluginInfos[Guid].Instance.GetType().Assembly;
                const string root = "com.github.zehsteam.SellMyScrap.";
                var any = HarmonyLib.AccessTools.all;

                var helper = assembly.GetType(root + "Helpers.ScrapHelper", true);
                var manager = assembly.GetType(root + "Managers.SellManager", true);
                var sellCommand = assembly.GetType(root + "Commands.SellCommand", true);
                var itemData = assembly.GetType(root + "Objects.ItemData", true);
                var sellType = assembly.GetType(root + "Data.SellType", true);
                var status = assembly.GetType(root + "Data.ConfirmationStatus", true);

                // "You must be landed on a moon that allows selling" and the like.
                var allowed = new object[] { null };
                if (!(bool)sellCommand.GetMethod("CanUseCommand", any).Invoke(null, allowed))
                {
                    var why = allowed[0] as TerminalNode;
                    refused = why != null ? (why.displayText ?? "").Trim() : "Cannot sell here.";
                    return true;
                }

                // The mod matches loosely ("Bell" also finds "Bell pepper").
                var loose = (System.Collections.IList)helper.GetMethod("GetAllScrapByItemNames", any)
                    .Invoke(null, new object[] { new[] { name }, false, false, false });
                var exact = (System.Collections.IList)Activator.CreateInstance(loose.GetType());
                var nameOf = itemData.GetProperty("ItemName", any);

                foreach (var item in loose)
                {
                    if ((string)nameOf.GetValue(item) == name)
                    {
                        exact.Add(item);
                    }
                }

                if (exact.Count == 0)
                {
                    refused = "Nothing of that kind left to sell.";
                    return true;
                }

                var scrap = manager.GetMethod("SetScrapToSell", any).Invoke(null, new object[] { exact });
                var value = (int)scrap.GetType().GetProperty("TotalScrapValue", any).GetValue(scrap);

                manager.GetMethod("CreateSellRequest", any).Invoke(null, new object[]
                {
                    Enum.Parse(sellType, "Item"), value, value, Enum.Parse(status, "AwaitingConfirmation"), -2, -1
                });
                manager.GetMethod("ConfirmSellRequest", any).Invoke(null, null);
                return true;
            }
            catch (Exception e)
            {
                Plugin.mlg.LogWarning($"SellMyScrap: could not sell from the list, using the mod's command instead: {(e.InnerException ?? e).Message}");
                return false;
            }
        }

        // While the mod waits for an answer it takes everything typed, even after its page is gone, so its questions get
        // answered.
        internal static bool Claim(Terminal terminal, View view, string printed, string back)
        {
            if (!Chainloader.PluginInfos.ContainsKey(Guid))
            {
                return false;
            }

            var lines = new List<string>();
            foreach (var raw in printed.Split('\n'))
            {
                lines.Add(Tags.Replace(raw, "").Trim());
            }

            var first = lines.Find(line => line.Length > 0) ?? "";

            if (Catalogue.IsMatch(first))
            {
                view.Question = true;
                view.Leave = "exit";
                view.Builder = () => AllScrap(terminal, lines);
                return true;
            }

            if (!Found.IsMatch(first))
            {
                return false;
            }

            if (Asks.IsMatch(printed))
            {
                view.Question = true;
                view.Leave = "deny";
                view.Builder = () => Sale(terminal, lines);
            }
            else
            {
                view.Builder = () => Scrap(terminal, lines, back);
            }

            return true;
        }

        private static MenuPage Sale(Terminal terminal, List<string> lines)
        {
            var page = new MenuPage { Title = "SELL", HideHeads = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 4, AlignRight = true });
            page.Columns.Add(new Column { Chars = 9, AlignRight = true });

            var items = new List<MenuEntry>();
            Space(page);

            foreach (var line in lines)
            {
                var found = Found.Match(line);
                var quota = Quota.Match(line);
                var overtime = Overtime.Match(line);
                var rate = Rate.Match(line);
                var item = Item.Match(line);

                if (found.Success)
                {
                    page.Entries.Add(new MenuEntry { Label = found.Groups[1].Value == "1" ? "Sell 1 item" : $"Sell {found.Groups[1].Value} items", Big = true });
                    Fact(page, "You receive", "$" + found.Groups[2].Value);
                }
                else if (quota.Success)
                {
                    Fact(page, "Quota", quota.Groups[1].Value);
                    if (quota.Groups[2].Success)
                    {
                        Fact(page, "Still needed", quota.Groups[2].Value);
                    }
                }
                else if (overtime.Success)
                {
                    Fact(page, "Overtime bonus", overtime.Groups[1].Value);
                }
                else if (rate.Success)
                {
                    Fact(page, "The Company is buying at", rate.Groups[1].Value + "%");
                }
                else if (item.Success)
                {
                    items.Add(Row(item));
                }
            }

            if (items.Count > 0)
            {
                Space(page);
                page.Entries.Add(new MenuEntry { Label = "ITEMS", IsHeader = true });
                page.Entries.AddRange(items);
            }

            Space(page);
            page.Entries.Add(new MenuEntry { Label = "[ CONFIRM ]", Command = "confirm" });
            page.Entries.Add(new MenuEntry { Label = "[ DENY ]", Click = () => Navigator.Back(terminal) });
            return page;
        }

        private static MenuPage Scrap(Terminal terminal, List<string> lines, string back)
        {
            var page = new MenuPage
            {
                Title = "SCRAP ON THE SHIP",
                SortBy = column =>
                {
                    State.SortBy(column);
                    Navigator.Rebuild(terminal);
                }
            };

            page.Columns.Add(new Column { Header = State.HeaderFor("NAME", 0) });
            page.Columns.Add(new Column { Header = State.HeaderFor("COUNT", 1), Chars = 5, AlignRight = true });
            page.Columns.Add(new Column { Header = State.HeaderFor("VALUE", 2), Chars = 9, AlignRight = true });

            var rows = new List<MenuEntry>();
            foreach (var line in lines)
            {
                var found = Found.Match(line);
                var item = Item.Match(line);

                if (found.Success)
                {
                    page.Summary = $"{found.Groups[1].Value} items worth ${found.Groups[2].Value}";
                }
                else if (item.Success)
                {
                    rows.Add(Row(item));
                }
            }

            page.Entries.AddRange(State.Sorted(rows));
            ResultPages.AddBack(page, terminal, back);
            return page;
        }

        // The mod pages this list and waits for "next", "prev", "page 3" or "exit".
        private static MenuPage AllScrap(Terminal terminal, List<string> lines)
        {
            var page = new MenuPage { Title = "ALL SCRAP", HideHeads = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 24 });
            var current = 1;
            var last = 1;
            var listing = false;

            foreach (var line in lines)
            {
                var head = Catalogue.Match(line);
                if (head.Success)
                {
                    int.TryParse(head.Groups[2].Value, out current);
                    int.TryParse(head.Groups[3].Value, out last);
                    page.Summary = $"{head.Groups[1].Value} kinds of scrap in the game  -  page {current} / {last}";
                    listing = true;
                }
                else if (line.StartsWith("The following commands"))
                {
                    listing = false;
                }
                else if (line.StartsWith("Error:"))
                {
                    page.Summary = line;
                }
                else if (listing && line.Length > 0)
                {
                    // Two names to a line.
                    var names = Gap.Split(line);
                    page.Entries.Add(new MenuEntry { Label = names[0], Cells = new[] { names.Length > 1 ? names[1] : "" } });
                }
            }

            page.Tools.Add(new ToolButton { Label = "< PREV", Off = current <= 1, Click = () => Commands.Submit(terminal, "prev") });
            page.Tools.Add(new ToolButton { Label = "NEXT >", Off = current >= last, Click = () => Commands.Submit(terminal, "next") });
            page.Tools.Add(new ToolButton { Label = "EXIT", Click = () => Navigator.Back(terminal) });

            Space(page);
            page.Entries.Add(new MenuEntry { Label = "[ GO TO PAGE... ]", Prefill = "page " });
            page.Entries.Add(new MenuEntry { Label = "[ EXIT ]", Click = () => Navigator.Back(terminal) });
            return page;
        }

        private static MenuEntry Row(Match item)
        {
            var name = item.Groups[1].Value.Trim();
            var count = int.Parse(item.Groups[2].Value);
            int.TryParse(item.Groups[3].Value.TrimStart('$'), out var value);

            // "(Vehicle)" or "(ShipInventory)" when it is not lying in the ship.
            if (item.Groups[4].Success)
            {
                name += " " + item.Groups[4].Value;
            }

            return new MenuEntry
            {
                Label = name,
                Cells = new[] { "x" + count, item.Groups[3].Value },
                Keys = new IComparable[] { name, count, value }
            };
        }

        private static void Fact(MenuPage page, string label, string value)
        {
            page.Entries.Add(new MenuEntry { Label = label, Cells = new[] { "", value } });
        }

        private static void Space(MenuPage page)
        {
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
        }
    }
}
