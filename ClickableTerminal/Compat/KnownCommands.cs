using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;

namespace ClickableTerminal
{
    internal static class KnownCommands
    {
        internal class Group
        {
            internal string Plugin;
            internal string Title;
            internal string Description;

            internal string[] Claims;
            internal MenuEntry[] Entries;

            internal string[] OnlyIfRegistered;

            // For commands that cannot be listed ahead, like one per interior.
            internal Func<string, bool> Owns;

            internal Func<MenuPage> Page;
        }

        // A leftover cheat, and commands our own pages already cover.
        private static readonly HashSet<string> Hidden = new HashSet<string>
        {
            "reset credits", "melanie",
            "upgrades", "decor",
            "simulate", "filter", "preview", "sort"
        };

        private static readonly MenuEntry[] Unexplained =
        {
            new MenuEntry { Label = "LOGS", Command = "sigurd", Description = "Read the log entries the crew has found." },
            new MenuEntry { Label = "EJECT", Command = "eject", Description = "Fire the crew out of the airlock and start over." }
        };

        private static readonly MenuEntry[] Singles =
        {
            new MenuEntry { Label = "SHIP INVENTORY", Command = "ship", Description = "Put items away in the ship's inventory and take them out." },
            new MenuEntry { Label = "MERGE SCRAP", Command = "merge", Description = "Merge scrap of the same kind into one item." },
            new MenuEntry { Label = "REVIVE", Command = "revive", Description = "Bring dead crew members back to life, for credits." },
            new MenuEntry { Label = "WEATHER PROBE", Command = "probe", Description = "Change the weather on a moon, for credits." }
        };

        private static readonly KeyValuePair<string, MenuEntry>[] PluginSingles =
        {
            new KeyValuePair<string, MenuEntry>("ExtraDaysToDeadline", new MenuEntry
            {
                Label = "BUY EXTRA DAY",
                Command = "buyday",
                Description = "Ask the Company for one more day to reach the quota, for credits.",
                Click = () => Answers.Run(TheTerminal(), "BUY EXTRA DAY", "buyday", true)
            }),
            new KeyValuePair<string, MenuEntry>("abu.TooMuchScrap", new MenuEntry
            {
                Label = "TOO MUCH SCRAP",
                Command = "merge",
                Description = "Merge nearby piles of small scrap (bones, body parts) into one, to cut lag. Host only.",
                Click = () => Answers.AskFirst(TheTerminal(), "MERGE SCRAP",
                    "Merge the small scrap lying close together in the ship into fewer pieces?\n\n<alpha=#99>Only the kinds set in the mod's config merge "
                    + "(body parts and bones by default), only the host can, and by default only at the Company.", "MERGE", "merge")
            }),
            new KeyValuePair<string, MenuEntry>("BLUJAY.WeatherReroll", new MenuEntry
            {
                Label = "WEATHER REROLL",
                Command = "reroll",
                Description = "Roll the weather of every moon again, for credits. Each reroll that day costs more.",
                Click = () => Answers.Run(TheTerminal(), "WEATHER REROLL", "reroll")
            })
        };

        private static Terminal TheTerminal()
        {
            return UnityEngine.Object.FindObjectOfType<Terminal>();
        }

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
                // Lategame Upgrades lists the same apps in its own folder (same developer).
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
                    "interns", "intern", "atk", "initattack", "cd", "cooldown", "extend deadline", "probe",
                    "demon", "lookup", "bruteforce"
                },
                OnlyIfRegistered = new[] { "lgu", "contracts", "trade", "convert", "extend deadline", "probe" },
                Entries = new[]
                {
                    new MenuEntry { Label = "UPGRADE STORE", Command = "lgu", Description = "Buy upgrades for the ship and the crew." },
                    new MenuEntry { Label = "CONTRACTS", Command = "contracts", Description = "Take on a contract for a reward." },
                    new MenuEntry { Label = "EXTEND DEADLINE", Command = "extend deadline", Description = "Buy more days before the quota is due." },
                    new MenuEntry { Label = "WEATHER PROBE", Command = "probe", Description = "Change the weather on a moon." },
                    new MenuEntry { Label = "INTERNS", Command = "interns", Description = "Replace the dead player on the radar with a new employee." },
                    new MenuEntry { Label = "TRADE CREDITS", Command = "trade", Description = "Give player credits to someone else." },
                    new MenuEntry { Label = "CONVERT CREDITS", Command = "convert", Description = "Change company credits into player credits and back." },
                    new MenuEntry { Label = "STUN ATTACK", Command = "atk", Description = "Stun nearby enemies. Needs the Discombobulator." },
                    new MenuEntry { Label = "STUN COOLDOWN", Command = "cd", Description = "How long until the stun can be used again." },
                    new MenuEntry { Label = "EXORCISM", Click = () => LguContracts.Exorcism(TheTerminal()), Description = "Contract: what the spirit at the pentagram needs to be banished." },
                    new MenuEntry { Label = "DEFUSAL", Click = () => LguContracts.Defusal(TheTerminal()), Description = "Contract: the bomb's serial number in, the wire order out." },
                    new MenuEntry { Label = "DATA RETRIEVAL", Click = () => LguContracts.DataRetrieval(TheTerminal()), Description = "Contract: the laptop's IP address in, then log in on the laptop." },
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
                Plugin = LethalRpgPages.Guid,
                Title = "LETHAL RPG",
                Description = "Spend XP on upgrades for your body and your tools.",
                Claims = new[] { "rpg" },
                Page = LethalRpgPages.Menu
            },
            new Group
            {
                Plugin = "mattymatty.LobbyControl",
                Title = "LOBBY",
                Description = "Open or close the lobby to new players, and save or load the game. Host only.",
                Claims = new[] { "lobby" },
                Entries = new[]
                {
                    new MenuEntry { Label = "STATUS", Command = "lobby status", Description = "Whether the lobby is open, who can see it, and the save file." },
                    new MenuEntry { Label = "OPEN", Command = "lobby open", Description = "Let new players join. Only in orbit." },
                    new MenuEntry { Label = "CLOSE", Command = "lobby close", Description = "Keep new players out. Only in orbit." },
                    new MenuEntry { Label = "PUBLIC", Command = "lobby public", Description = "Anyone can find the lobby." },
                    new MenuEntry { Label = "FRIENDS ONLY", Command = "lobby friend", Description = "Only Steam friends can find the lobby." },
                    new MenuEntry { Label = "INVITE ONLY", Command = "lobby private", Description = "Only invited players can join." },
                    new MenuEntry
                    {
                        Label = "RENAME",
                        Description = "Give the lobby a new name.",
                        Click = () => Answers.Typed(TheTerminal(), "RENAME LOBBY", "Type the lobby's new name, then Enter.", "lobby rename ", "RENAME", 40)
                    },
                    new MenuEntry { Label = "AUTOSAVE", Command = "lobby autosave", Description = "Switch saving the game on its own on or off." },
                    new MenuEntry
                    {
                        Label = "SAVE",
                        Description = "Save the game under a name.",
                        Click = () => Answers.Typed(TheTerminal(), "SAVE GAME", "Type a name for the save, then Enter.", "lobby save ", "SAVE", 30)
                    },
                    new MenuEntry
                    {
                        Label = "LOAD",
                        Description = "Load a saved game into this lobby.",
                        Click = () => Answers.Typed(TheTerminal(), "LOAD GAME", "Type the name of the save to load, then Enter. What is not saved is lost.", "lobby load ", "LOAD", 30)
                    },
                    new MenuEntry
                    {
                        Label = "SWITCH SAVE",
                        Description = "Change which save file the game writes to, without loading it.",
                        Click = () => Answers.Typed(TheTerminal(), "SWITCH SAVE", "Type the name of the save file to use from now on, then Enter.", "lobby switch ", "SWITCH", 30)
                    },
                    new MenuEntry
                    {
                        Label = "DROP ALL",
                        Description = "Everyone drops everything they hold.",
                        Click = () => Answers.AskFirst(TheTerminal(), "DROP ALL", "Make every player drop everything they hold?", "DROP ALL", "lobby dropall")
                    },
                    new MenuEntry
                    {
                        Label = "CLEAR",
                        Description = "Reset this lobby to an empty game.",
                        Click = () => Answers.AskFirst(TheTerminal(), "CLEAR LOBBY", "Reset this lobby to an empty game? Everything not saved is lost.", "CLEAR", "lobby clear")
                    }
                }
            },
            new Group
            {
                Plugin = "com.seeya.operator",
                Title = "CASSETTES",
                Description = "Play, save and delete what the camcorder recorded.",
                Claims = new[] { "ohelp", "cassettes", "play", "stop", "save", "delete", "slow", "normal", "fast", "turbo", "pause", "resume", "volume", "transfer" },
                Entries = new[]
                {
                    new MenuEntry { Label = "SAVED CASSETTES", Command = "cassettes", Description = "Every saved cassette, with its ID, value, date and length." },
                    new MenuEntry
                    {
                        Label = "PLAY",
                        Description = "Play a cassette on the terminal, by its ID.",
                        Click = () => Answers.Typed(TheTerminal(), "PLAY", "Type the cassette's ID, then Enter.", "play ", "PLAY", 20)
                    },
                    new MenuEntry { Label = "STOP", Command = "stop", Description = "Stop playing." },
                    new MenuEntry { Label = "PAUSE", Command = "pause", Description = "Pause playing." },
                    new MenuEntry { Label = "RESUME", Command = "resume", Description = "Go on playing." },
                    new MenuEntry
                    {
                        Label = "SPEED",
                        Description = "Play slower or faster.",
                        Click = () =>
                        {
                            var terminal = TheTerminal();
                            var window = new Window { Title = "SPEED", Text = "How fast the cassette plays." };
                            foreach (var speed in new[] { "slow", "normal", "fast", "turbo" })
                            {
                                window.Buttons.Add(new ToolButton { Label = speed.ToUpperInvariant(), Click = () => Answers.Run(terminal, "SPEED", speed) });
                            }

                            Window.Show(window);
                        }
                    },
                    new MenuEntry
                    {
                        Label = "VOLUME",
                        Description = "How loud the cassette plays, 0 to 100.",
                        Click = () => Answers.Typed(TheTerminal(), "VOLUME", "Type the volume, 0 to 100, then Enter.", "volume ", "SET", 3)
                    },
                    new MenuEntry
                    {
                        Label = "SAVE",
                        Description = "Keep a cassette in the library, by its ID.",
                        Click = () => Answers.Typed(TheTerminal(), "SAVE CASSETTE", "Type the cassette's ID, then Enter.", "save ", "SAVE", 20)
                    },
                    new MenuEntry
                    {
                        Label = "DELETE",
                        Description = "Take a cassette out of the library, by its ID.",
                        Click = () => Answers.Typed(TheTerminal(), "DELETE CASSETTE", "Type the ID of the cassette to delete, then Enter.", "delete ", "DELETE", 20)
                    },
                    new MenuEntry { Label = "TRANSFER", Command = "transfer", Description = "Move what is playing onto the ship's monitor, where that is possible." }
                }
            },
            new Group
            {
                Plugin = "com.y4ngz.company",
                Title = "CONTRACTED",
                Description = "The contract on every moon, and Field Support for the Defuse contract.",
                Claims = new[] { "contractlist", "defuse", "fieldsupport", "field support" },
                Entries = new[]
                {
                    new MenuEntry
                    {
                        Label = "CONTRACT LIST",
                        Open = () => Contracts.Page(UnityEngine.Object.FindObjectOfType<Terminal>()),
                        Description = "Every moon with its contract. MOONS shows them too."
                    },
                    new MenuEntry
                    {
                        Label = "FIELD SUPPORT",
                        Click = () => FieldSupport.Show(UnityEngine.Object.FindObjectOfType<Terminal>()),
                        Description = "Contracted's own Defuse: what the crew sees on the briefcase in, the cut order and code out. Contracted uses LGU's Defusal instead while LGU is installed."
                    }
                }
            },
            new Group
            {
                Plugin = "gavin.shipcomputerassistant",
                Title = "CREW MONITOR",
                Description = "Health, place, weight and held item of every crew member.",
                Claims = new[] { "crew", "crew upgrade", "crew monitoring", "buy crew monitor", "status" },
                Entries = new[]
                {
                    new MenuEntry { Label = "CREW STATUS", Command = "crew", Description = "How every crew member is doing. Needs the software installed." },
                    new MenuEntry { Label = "BUY THE SOFTWARE", Command = "crew upgrade", Description = "Install Crew Monitoring Software on the ship, for credits." }
                }
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
                        Click = () => Answers.Typed(UnityEngine.Object.FindObjectOfType<Terminal>(), "SET SEED",
                            "Type the seed for the next moon, text or numbers, then Enter.", "set seed ", "SET", 40)
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

            var page = Navigator.MenuLook(UnityEngine.Object.FindObjectOfType<Terminal>(), new MenuPage { Title = group.Title });

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

            return Answers.InWindows(UnityEngine.Object.FindObjectOfType<Terminal>(), page);
        }

        private static readonly string[] None = new string[0];

        private static IEnumerable<Group> Present()
        {
            return Groups.Where(group => Chainloader.PluginInfos.ContainsKey(group.Plugin));
        }
    }
}
