using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class ScrapBankPages
    {
        internal const string Guid = "GJ.ScrapBank";

        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        internal static readonly Regex Heading = new Regex(@"^=+\s*(.*?)\s*=+$", RegexOptions.Compiled);
        private static readonly Regex Paged = new Regex(@"(\d+)\s*/\s*(\d+)\s*$", RegexOptions.Compiled);

        // " 3. Cash register x2       $120"
        internal static readonly Regex Numbered = new Regex(@"^(\d+)\.\s+(.+?)\s+(\$[\d,.]+)$", RegexOptions.Compiled);

        // "2 / ALL = ..." once the mod's other language is stripped
        internal static readonly Regex Choice = new Regex(@"^(\d+)\s*/\s*([A-Z]+)", RegexOptions.Compiled);
        private static readonly Regex Leftover = new Regex(@"(\s*/\s*)+(?=\s*[:=]|\s*$)", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"\s{2,}", RegexOptions.Compiled);

        private static readonly HashSet<string> Words = new HashSet<string>
        {
            "bank", "sb", "scrapbank", "scrap", "ship", "sell", "scrapstore", "scrapsell", "scrapmoney", "banksell"
        };

        internal static bool Owns(string key)
        {
            var first = key.Split(' ')[0];
            return first != "ship" && first != "sell" && Words.Contains(first);
        }

        internal static MenuPage Menu()
        {
            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            var page = Navigator.MenuLook(terminal, new MenuPage { Title = "SCRAP BANK" });

            page.Entries.Add(new MenuEntry { Label = "BALANCE", Command = "bank status", Description = "The scrap money in the bank." });
            page.Entries.Add(new MenuEntry { Label = "STORE ALL", Command = "scrap all", Description = "Turn all the scrap in the ship into scrap money." });
            page.Entries.Add(new MenuEntry
            {
                Label = "STORE AMOUNT",
                Description = "Store scrap worth up to an amount you set.",
                Click = () => Answers.Typed(terminal, "STORE AMOUNT", "Type the value of scrap to store at most, then Enter.", "scrap ", "STORE", 6)
            });
            page.Entries.Add(new MenuEntry { Label = "STORE BY ITEM", Description = "Tick single items in the ship and store those.", Open = () => ItemPage(terminal) });
            page.Entries.Add(new MenuEntry { Label = "SELL QUOTA", Command = "sell quota", Description = "Sell scrap money to meet the quota. Only at the Company." });
            page.Entries.Add(new MenuEntry { Label = "SELL ALL", Command = "sell all", Description = "Sell all the scrap money. Only at the Company." });
            page.Entries.Add(new MenuEntry
            {
                Label = "SELL AMOUNT",
                Description = "Sell an amount of scrap money you set. Only at the Company.",
                Click = () =>
                {
                    var time = TimeOfDay.Instance;
                    var needed = time != null ? Math.Max(0, time.profitQuota - time.quotaFulfilled) : 0;
                    Answers.Typed(terminal, "SELL AMOUNT", "Type the scrap money to sell, then Enter.", "sell ", "SELL", 6,
                        Answers.Fill(terminal, $"QUOTA NEEDS ${needed}", needed.ToString()));
                }
            });
            return Answers.InWindows(terminal, page);
        }

        private const string ItemTitle = "STORE BY ITEM";
        private static readonly PageState ItemState = new PageState();
        private static string report;
        private static bool storing;
        private static Terminal pending;

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
            page.Columns.Add(new Column { Header = ItemState.HeaderFor("VALUE", 1), Chars = 9, AlignRight = true });

            var rows = new List<MenuEntry>();
            foreach (var item in Storable().OrderBy(item => item.itemProperties.itemName).ThenByDescending(item => item.scrapValue))
            {
                var picked = item;
                var name = item.itemProperties.itemName;
                var value = item.scrapValue;

                rows.Add(new MenuEntry
                {
                    Label = name,
                    Cells = new[] { "$" + value },
                    Keys = new IComparable[] { name, value },
                    Dim = storing,
                    Click = () => Store(terminal, picked)
                });
            }

            if (report != null && !storing)
            {
                Window.Show(new Window { Title = ItemTitle, Text = report });
                report = null;
            }

            page.Summary = storing ? "Storing..." : rows.Count == 0 ? "No scrap in the ship that can be stored." : "Click an item to store it.";
            page.Entries.AddRange(ItemState.Sorted(rows));
            return page;
        }

        private static Type Find(string name)
        {
            return Chainloader.PluginInfos[Guid].Instance.GetType().Assembly.GetTypes().FirstOrDefault(type => type.Name == name);
        }

        private static List<GrabbableObject> Storable()
        {
            MethodInfo eligible = null;
            try
            {
                eligible = Find("ScrapBankManager")?.GetMethod("IsEligibleShipScrap", AccessTools.all);
            }
            catch (Exception)
            {
            }

            return UnityEngine.Object.FindObjectsOfType<GrabbableObject>().Where(item =>
            {
                if (item == null || item.itemProperties == null || !item.itemProperties.isScrap || !item.isInShipRoom
                    || item.NetworkObject == null || !item.NetworkObject.IsSpawned)
                {
                    return false;
                }

                try
                {
                    return eligible != null ? (bool)eligible.Invoke(null, new object[] { item }) : !item.isHeld && !item.isPocketed && item.scrapValue > 0;
                }
                catch (Exception)
                {
                    return false;
                }
            }).ToList();
        }

        private static void Store(Terminal terminal, GrabbableObject item)
        {
            // The mod takes one at a time.
            if (storing || item == null || item.NetworkObject == null || !item.NetworkObject.IsSpawned)
            {
                return;
            }

            try
            {
                pending = terminal;

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    var manager = Find("ScrapBankManager");
                    var result = manager.GetNestedType("StoreResult", AccessTools.all);

                    // The mod reports back through a callback of its own type, so one is built for it.
                    var parameter = Expression.Parameter(result);
                    var done = Expression.Lambda(typeof(Action<>).MakeGenericType(result),
                        Expression.Call(typeof(ScrapBankPages).GetMethod(nameof(Stored), AccessTools.all), Expression.Convert(parameter, typeof(object))),
                        parameter).Compile();

                    var arguments = new object[] { item.NetworkObjectId, done, null };
                    storing = true;
                    if (!(bool)manager.GetMethod("BeginStoreItem", AccessTools.all).Invoke(null, arguments))
                    {
                        storing = false;
                        report = Clean(((string)arguments[2] ?? "Could not store it.").Replace('\n', ' '));
                    }
                }
                else
                {
                    Find("NetworkBridge").GetMethod("RequestStoreItem", AccessTools.all).Invoke(null, new object[] { item.NetworkObjectId });
                    storing = true;
                    terminal.StartCoroutine(WaitForHost(terminal, item));
                }
            }
            catch (Exception e)
            {
                storing = false;
                report = "Could not store: " + (e.InnerException ?? e).Message;
                Plugin.mlg.LogWarning("ScrapBank: " + report);
            }

            Redraw();
        }

        private static void Stored(object result)
        {
            storing = false;
            try
            {
                var message = result.GetType().GetProperty("Message", AccessTools.all)?.GetValue(result)
                              ?? result.GetType().GetField("Message", AccessTools.all)?.GetValue(result);
                report = Clean((message as string ?? "Stored.").Replace('\n', ' '));
            }
            catch (Exception)
            {
                report = "Stored.";
            }

            Redraw();
        }

        // A client only asks the host. The answer is the item leaving the ship.
        private static IEnumerator WaitForHost(Terminal terminal, GrabbableObject item)
        {
            var name = item.itemProperties.itemName;
            var until = Time.unscaledTime + 6f;

            while (item != null && item.NetworkObject != null && item.NetworkObject.IsSpawned && Time.unscaledTime < until)
            {
                yield return null;
            }

            storing = false;
            report = item == null || item.NetworkObject == null || !item.NetworkObject.IsSpawned
                ? "Stored " + name + "."
                : "The host has not stored " + name + ".";
            Redraw();
        }

        private static void Redraw()
        {
            var view = Navigator.Current;
            if (pending != null && view != null && view.Page != null && view.Page.Title == ItemTitle)
            {
                Navigator.Rebuild(pending);
            }
        }

        internal static bool Claim(Terminal terminal, View view, string printed, string submitted, string back)
        {
            if (!Chainloader.PluginInfos.ContainsKey(Guid))
            {
                return false;
            }

            var lines = new List<string>();
            foreach (var raw in printed.Split('\n'))
            {
                lines.Add(Clean(Tags.Replace(raw, "")));
            }

            var first = lines.Find(line => line.Length > 0) ?? "";
            var word = (submitted ?? "").Trim().ToLowerInvariant().Split(' ')[0].TrimStart('/');
            var ours = first.StartsWith("===") || Words.Contains(word) || (word.Length > 0 && int.TryParse(word, out _) && first.StartsWith("STORE"));

            if (!ours || first.StartsWith("Found "))
            {
                return false;
            }

            // This screen keeps waiting for a number even after the player walks away.
            if (lines.Exists(line => Choice.IsMatch(line) && line.Contains("CANCEL")))
            {
                view.Question = true;
                view.Leave = "cancel";
            }

            view.Builder = () => Page(terminal, lines, back);
            return true;
        }

        private static MenuPage Page(Terminal terminal, List<string> lines, string back)
        {
            var page = new MenuPage { Title = "SCRAP BANK", HideHeads = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 9, AlignRight = true });

            var asks = false;
            var blank = true;

            foreach (var line in lines)
            {
                var heading = Heading.Match(line);
                var numbered = Numbered.Match(line);
                var choice = Choice.Match(line);

                if (line.Length == 0)
                {
                    if (!blank)
                    {
                        page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
                    }

                    blank = true;
                    continue;
                }

                if (line.Contains("ENTER") || line.StartsWith("Page:"))
                {
                    continue;
                }

                blank = false;

                if (heading.Success)
                {
                    var name = heading.Groups[1].Value.Trim(' ', '/');
                    var paged = Paged.Match(name);
                    if (paged.Success && int.TryParse(paged.Groups[1].Value, out var current) && int.TryParse(paged.Groups[2].Value, out var last) && last > 1)
                    {
                        page.Tools.Add(new ToolButton { Label = "< PREV", Off = current <= 1, Click = () => Commands.Submit(terminal, "scrap list " + Math.Max(1, current - 1)) });
                        page.Tools.Add(new ToolButton { Label = "NEXT >", Off = current >= last, Click = () => Commands.Submit(terminal, "scrap list " + Math.Min(last, current + 1)) });
                    }

                    page.Entries.Add(new MenuEntry { Label = name, IsHeader = true });
                }
                else if (numbered.Success)
                {
                    var number = numbered.Groups[1].Value;
                    page.Entries.Add(new MenuEntry
                    {
                        Label = numbered.Groups[2].Value.Trim(),
                        Cells = new[] { numbered.Groups[3].Value },
                        Click = () => Commands.Submit(terminal, number)
                    });
                }
                else if (choice.Success)
                {
                    var number = choice.Groups[1].Value;
                    asks = true;
                    page.Entries.Add(new MenuEntry { Label = "[ " + choice.Groups[2].Value + " ]", Click = () => Commands.Submit(terminal, number) });
                }
                else
                {
                    page.Entries.Add(new MenuEntry { Label = line });
                }
            }

            if (!asks)
            {
                ResultPages.AddBack(page, terminal, back);
            }

            return page;
        }

        // The mod prints every line in two languages, and the terminal font only has the letters of one.
        internal static string Clean(string line)
        {
            var kept = new StringBuilder(line.Length);
            foreach (var c in line)
            {
                kept.Append(c < 128 ? c : ' ');
            }

            var text = Leftover.Replace(kept.ToString(), "");
            return Spaces.Replace(text, m => m.Value.Length > 3 ? m.Value : " ").Trim();
        }
    }
}
