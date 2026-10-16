using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;

namespace ClickableTerminal
{
    // What we know about commands beyond what the terminal prints: which ones to leave out,
    // how to describe the ones the game never explains, and which belong together under one mod.
    internal static class KnownCommands
    {
        internal class Group
        {
            internal string Plugin;
            internal string Title;
            internal string Description;

            // Every spelling the mod answers to, so none of them turns up loose on another page.
            internal string[] Claims;
            internal MenuEntry[] Entries;

            // Entries whose command only exists when the mod has registered it (its own config can turn features off).
            internal string[] OnlyIfRegistered;

            // For commands that cannot be listed ahead, like one per interior.
            internal Func<string, bool> Owns;

            // A page of its own instead of the list of entries.
            internal Func<MenuPage> Page;
        }

        // Left out everywhere: a leftover cheat, and commands our own pages already cover.
        private static readonly HashSet<string> Hidden = new HashSet<string>
        {
            "reset credits", "melanie",
            "upgrades", "decor",
            "simulate", "filter", "preview", "sort"
        };

        // Vanilla commands the terminal itself never lists.
        private static readonly MenuEntry[] Unexplained =
        {
            new MenuEntry { Label = "LOGS", Command = "sigurd", Description = "Read the log entries the crew has found." },
            new MenuEntry { Label = "EJECT", Command = "eject", Description = "Fire the crew out of the airlock and start over." }
        };

        // Mods that add a single command with a screen of its own: a name and a line about it, listed with the mods
        // instead of among the commands nobody explains. Only shown when the command is really there.
        private static readonly MenuEntry[] Singles =
        {
            new MenuEntry { Label = "SHIP INVENTORY", Command = "ship", Description = "Put items away in the ship's inventory and take them out." },
            new MenuEntry { Label = "MERGE SCRAP", Command = "merge", Description = "Merge scrap of the same kind into one item." },
            new MenuEntry { Label = "REVIVE", Command = "revive", Description = "Bring dead crew members back to life, for credits." },
            new MenuEntry { Label = "WEATHER PROBE", Command = "probe", Description = "Change the weather on a moon, for credits." }
        };

        // The same for mods that do not go through the Interactive Terminal API: shown when the mod is installed.
        private static readonly KeyValuePair<string, MenuEntry>[] PluginSingles =
        {
            new KeyValuePair<string, MenuEntry>("ExtraDaysToDeadline",
                new MenuEntry { Label = "BUY EXTRA DAY", Command = "buyday", Description = "Ask the Company for one more day to reach the quota." }),
            new KeyValuePair<string, MenuEntry>("abu.TooMuchScrap",
                new MenuEntry { Label = "TOO MUCH SCRAP", Command = "merge", Description = "Merge nearby piles of small scrap (bones, body parts) into one, to cut lag. Host only." })
        };

        // Other spellings the same mods answer to. One entry each is enough.
        private static readonly HashSet<string> SingleAliases = new HashSet<string> { "merge scrap", "mergescrap", "scrapmerge", "weather" };

        internal static IEnumerable<MenuEntry> SinglesAmong(IEnumerable<string> registered)
        {
            foreach (var pair in PluginSingles)
            {
                if (Chainloader.PluginInfos.ContainsKey(pair.Key))
                {
                    yield return pair.Value;
                }
            }

            foreach (var entry in Singles)
            {
                // Lategame Upgrades lists the same applications in its own folder (same developer): one place is enough.
                var key = Commands.Normalize(entry.Command);
                if (Present().Any(group => group.Claims.Contains(key)))
                {
                    continue;
                }

                var spelled = registered.FirstOrDefault(key => string.Equals(key, entry.Command, StringComparison.OrdinalIgnoreCase));
                if (spelled != null)
                {
                    yield return new MenuEntry { Label = entry.Label, Description = entry.Description, Command = spelled };
                }
            }
        }

        private static readonly Group[] Groups =
        {
            new Group
            {
                Plugin = "com.malco.lethalcompany.moreshipupgrades",
                Title = "LATEGAME UPGRADES",
                Description = "Upgrade store, contracts and the crew's other tools.",
                Claims = new[]
                {
                    "lategame", "lgu", "lategame store", "lgc", "contracts", "contract", "contract info",
                    "trade", "trade player credits", "lgu trade", "convert", "pc",
                    "interns", "intern", "atk", "initattack", "cd", "cooldown", "extend deadline", "probe"
                },
                OnlyIfRegistered = new[] { "lgu", "contracts", "trade", "convert", "extend deadline", "probe" },
                Entries = new[]
                {
                    new MenuEntry { Label = "UPGRADE STORE", Command = "lgu", Description = "Buy upgrades for the ship and the crew." },
                    new MenuEntry { Label = "CONTRACTS", Command = "contracts", Description = "Take on a contract for a reward." },
                    new MenuEntry { Label = "CONTRACT INFO", Command = "contract info", Description = "What each kind of contract asks for." },
                    new MenuEntry { Label = "EXTEND DEADLINE", Command = "extend deadline", Description = "Buy more days before the quota is due." },
                    new MenuEntry { Label = "WEATHER PROBE", Command = "probe", Description = "Change the weather on a moon." },
                    new MenuEntry { Label = "INTERNS", Command = "interns", Description = "Replace the dead player on the radar with a new employee." },
                    new MenuEntry { Label = "TRADE CREDITS", Command = "trade", Description = "Give player credits to someone else." },
                    new MenuEntry { Label = "CONVERT CREDITS", Command = "convert", Description = "Change company credits into player credits and back." },
                    new MenuEntry { Label = "STUN ATTACK", Command = "atk", Description = "Stun nearby enemies. Needs the Discombobulator." },
                    new MenuEntry { Label = "STUN COOLDOWN", Command = "cd", Description = "How long until the stun can be used again." },
                    new MenuEntry { Label = "ALL COMMANDS", Command = "lgc", Description = "Every command of the mod as it describes them." },
                    new MenuEntry { Label = "ABOUT", Command = "lategame", Description = "What the mod has to say about itself." }
                }
            },
            new Group
            {
                Plugin = "com.github.zehsteam.SellMyScrap",
                Title = "SELL MY SCRAP",
                Description = "Sell scrap from the ship without carrying it to the counter.",
                Claims = new[]
                {
                    "sell", "sell amount", "sellamount", "sell quota", "sellquota", "sell all", "sellall",
                    "sell item", "sellitem", "sell list", "selllist",
                    "view overtime", "viewovertime", "view scrap", "viewscrap", "view all scrap", "viewallscrap"
                },
                Page = SellMyScrapPages.Menu
            },
            new Group
            {
                Plugin = ScrapBankPages.Guid,
                Title = "SCRAP BANK",
                Description = "Put scrap in the bank as money, and sell from it.",
                Claims = new string[0],
                Owns = ScrapBankPages.Owns,
                Page = ScrapBankPages.Menu
            },
            new Group
            {
                Plugin = SelectableInteriorsPage.Guid,
                Title = "INTERIORS",
                Description = "Pick the interior the next moon will have.",
                Claims = new string[0],
                Owns = SelectableInteriorsPage.Owns,
                Page = () => SelectableInteriorsPage.Build(UnityEngine.Object.FindObjectOfType<Terminal>())
            },
            new Group
            {
                Plugin = "thej01.lc.TerminalSeedPicker",
                Title = "SEED PICKER",
                Description = "See or set the seed the next moon is built from.",
                Claims = new[] { "seed", "get seed", "set seed", "random seed" },
                Entries = new[]
                {
                    new MenuEntry { Label = "GET SEED", Command = "get seed", Description = "Show the seed of the moon you are on. Only after landing." },
                    new MenuEntry { Label = "RANDOM SEED", Command = "random seed", Description = "Set a completely random seed for the next moon. Only in orbit." },
                    new MenuEntry
                    {
                        Label = "SET SEED",
                        Description = "Type a seed for the next moon, text or numbers. Only in orbit.",
                        Open = () => TypeForm.Build(UnityEngine.Object.FindObjectOfType<Terminal>(), "SET SEED", "set seed ",
                            "Type the seed for the next moon:", "Text or numbers.")
                    }
                }
            }
        };

        private static readonly HashSet<string> VanillaMain = new HashSet<string> { "moons", "store", "bestiary", "storage", "other" };
        private static readonly HashSet<string> VanillaOther = new HashSet<string> { "view monitor", "switch", "ping", "flash", "transmit", "scan" };

        internal static bool IsVanillaMain(MenuEntry entry)
        {
            return VanillaMain.Contains(Commands.Normalize(entry.Command ?? entry.Prefill ?? ""));
        }

        internal static bool IsVanillaOther(MenuEntry entry)
        {
            return VanillaOther.Contains(Commands.Normalize(entry.Command ?? entry.Prefill ?? ""));
        }

        internal static IEnumerable<string> VanillaCommands()
        {
            return VanillaMain.Concat(VanillaOther).Concat(Unexplained.Select(entry => entry.Command)).Concat(new[] { "help" });
        }

        internal static bool IsHidden(string command)
        {
            var key = Commands.Normalize(command ?? "");
            return Hidden.Contains(key) || SingleAliases.Contains(key) || Present().Any(group => group.Claims.Contains(key) || (group.Owns != null && group.Owns(key)));
        }

        // The vanilla commands nobody lists, for the ones this terminal really has.
        internal static IEnumerable<MenuEntry> UnexplainedFor(Terminal terminal)
        {
            var keywords = terminal.terminalNodes != null ? terminal.terminalNodes.allKeywords : null;
            if (keywords == null)
            {
                yield break;
            }

            foreach (var entry in Unexplained)
            {
                if (keywords.Any(keyword => keyword != null && keyword.word == entry.Command))
                {
                    yield return entry;
                }
            }
        }

        // One main menu entry per mod we know, each opening a page with that mod's commands.
        internal static IEnumerable<MenuEntry> GroupEntries()
        {
            foreach (var group in Present())
            {
                var shown = group;
                yield return new MenuEntry
                {
                    Label = group.Title,
                    Description = group.Description,
                    Open = () => PageOf(shown)
                };
            }
        }

        private static MenuPage PageOf(Group group)
        {
            if (group.Page != null)
            {
                return group.Page();
            }

            var page = new MenuPage { Title = group.Title };

            foreach (var entry in group.Entries)
            {
                if (!(group.OnlyIfRegistered ?? None).Contains(entry.Command))
                {
                    page.Entries.Add(entry);
                    continue;
                }

                var spelled = InteractiveTerminal.Registered(entry.Command);
                if (spelled != null)
                {
                    page.Entries.Add(new MenuEntry { Label = entry.Label, Description = entry.Description, Command = spelled });
                }
            }

            return page;
        }

        private static readonly string[] None = new string[0];

        private static IEnumerable<Group> Present()
        {
            return Groups.Where(group => Chainloader.PluginInfos.ContainsKey(group.Plugin));
        }
    }
}
