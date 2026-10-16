using System;
using System.Collections.Generic;

namespace ClickableTerminal
{
    internal class Target
    {
        internal string Command;
        internal string Noun;
        internal int Price;
    }

    // What to type for each thing the terminal sells, routes to or has a file on.
    // Read from the game's keyword lists, so it covers whatever mods have added to them.
    internal class Catalog
    {
        internal readonly Dictionary<int, Target> Items = new Dictionary<int, Target>();
        internal readonly Dictionary<int, Target> Vehicles = new Dictionary<int, Target>();
        internal readonly Dictionary<int, Target> Unlockables = new Dictionary<int, Target>();
        internal readonly Dictionary<int, Target> Creatures = new Dictionary<int, Target>();
        internal readonly Dictionary<int, Target> Logs = new Dictionary<int, Target>();
        internal readonly Dictionary<SelectableLevel, Target> Routes = new Dictionary<SelectableLevel, Target>();
        private readonly Dictionary<string, string> info = new Dictionary<string, string>();

        internal static Catalog Read(Terminal terminal)
        {
            var catalog = new Catalog();
            var keywords = terminal.terminalNodes != null ? terminal.terminalNodes.allKeywords : null;
            var levels = StartOfRound.Instance != null ? StartOfRound.Instance.levels : null;
            if (keywords == null)
            {
                return catalog;
            }

            foreach (var verb in keywords)
            {
                if (verb == null || !verb.isVerb || verb.compatibleNouns == null || string.IsNullOrWhiteSpace(verb.word))
                {
                    continue;
                }

                foreach (var pair in verb.compatibleNouns)
                {
                    var node = pair?.result;
                    if (node == null || pair.noun == null || string.IsNullOrWhiteSpace(pair.noun.word))
                    {
                        continue;
                    }

                    var isDefault = pair.noun.defaultVerb == verb;
                    var target = new Target
                    {
                        Command = isDefault ? pair.noun.word : verb.word + " " + pair.noun.word,
                        Noun = pair.noun.word,
                        Price = node.itemCost
                    };

                    if (verb.word == "info")
                    {
                        catalog.info[pair.noun.word] = target.Command;
                    }

                    Put(catalog.Items, node.buyItemIndex, target, isDefault);
                    Put(catalog.Vehicles, node.buyVehicleIndex, target, isDefault);
                    Put(catalog.Unlockables, node.shipUnlockableID, target, isDefault);
                    Put(catalog.Creatures, node.creatureFileID, target, isDefault);
                    Put(catalog.Logs, node.storyLogFileID, target, isDefault);

                    if (verb.word == "route" && levels != null && node.terminalOptions != null)
                    {
                        catalog.AddRoute(levels, node, target);
                    }
                }
            }

            return catalog;
        }

        internal string InfoFor(Target target)
        {
            return target != null && info.TryGetValue(target.Noun, out var command) ? command : null;
        }

        // The destination and the price sit on the CONFIRM option of the route page, not on the page itself.
        private void AddRoute(SelectableLevel[] levels, TerminalNode node, Target target)
        {
            foreach (var option in node.terminalOptions)
            {
                var confirm = option?.result;
                var index = confirm != null ? confirm.buyRerouteToMoon : -1;
                if (index < 0 || index >= levels.Length || levels[index] == null)
                {
                    continue;
                }

                target.Price = Math.Max(node.itemCost, confirm.itemCost);
                Routes[levels[index]] = target;
            }
        }

        private static void Put(Dictionary<int, Target> into, int id, Target target, bool isDefault)
        {
            if (id >= 0 && (isDefault || !into.ContainsKey(id)))
            {
                into[id] = target;
            }
        }
    }
}
