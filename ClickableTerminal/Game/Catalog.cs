using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickableTerminal
{
    internal class Target
    {
        internal string Command;
        internal string Noun;
        internal TerminalKeyword Keyword;
        internal int Price;

        internal TerminalNode Confirm;
    }

    internal class Catalog
    {
        internal readonly Dictionary<int, Target> Items = new Dictionary<int, Target>();
        internal readonly Dictionary<int, Target> Vehicles = new Dictionary<int, Target>();
        internal readonly Dictionary<int, Target> Unlockables = new Dictionary<int, Target>();
        internal readonly Dictionary<int, Target> Creatures = new Dictionary<int, Target>();
        internal readonly Dictionary<int, Target> Logs = new Dictionary<int, Target>();
        internal readonly Dictionary<SelectableLevel, Target> Routes = new Dictionary<SelectableLevel, Target>();
        private readonly Dictionary<string, string> infoText = new Dictionary<string, string>();
        private readonly Dictionary<TerminalKeyword, TerminalNode> infoNodes = new Dictionary<TerminalKeyword, TerminalNode>();
        private static readonly System.Text.RegularExpressions.Regex Tags = new System.Text.RegularExpressions.Regex("<[^>]*>");

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
                        Keyword = pair.noun,
                        Confirm = ConfirmOf(node),
                        Price = node.itemCost
                    };

                    if (verb.word == "info")
                    {
                        catalog.infoText[pair.noun.word] = node.displayText;
                        catalog.infoNodes[pair.noun] = node;
                    }

                    // Football's bestiary page carries vehicle index 0 (the Cruiser), so info, creature and log pages
                    // never count as purchases.
                    if (verb.word != "info" && node.creatureFileID < 0 && node.storyLogFileID < 0)
                    {
                        Put(catalog.Items, node.buyItemIndex, target, isDefault);
                        Put(catalog.Vehicles, node.buyVehicleIndex, target, isDefault);
                        Put(catalog.Unlockables, node.shipUnlockableID, target, isDefault);
                    }

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

        internal string About(Target target, int sentences, int most)
        {
            return target != null && infoText.TryGetValue(target.Noun, out var text) ? Excerpt(text, sentences, most) : null;
        }

        internal static string Excerpt(string raw, int sentences, int most)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var lines = Tags.Replace(raw, "").Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && line.IndexOf(':') < 0 && line.Split(' ').Length >= 4 && !IsHeading(line));
            var text = string.Join(" ", string.Join(" ", lines).Split(new[] { ' ', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            if (text.Length == 0)
            {
                return null;
            }

            var end = -1;
            for (var i = 0; i < sentences; i++)
            {
                var next = text.IndexOfAny(new[] { '.', '!', '?' }, end + 1);
                if (next < 0)
                {
                    break;
                }

                end = next;
            }

            if (end > 0)
            {
                text = text.Substring(0, end + 1);
            }

            if (text.Length > most)
            {
                var cut = text.LastIndexOf(' ', most - 1);
                text = text.Substring(0, cut > most / 2 ? cut : most - 1) + "...";
            }

            return text;
        }

        private static bool IsHeading(string line)
        {
            return line.Any(char.IsLetter) && line == line.ToUpperInvariant();
        }

        internal static string Field(string raw, string label)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            foreach (var line in Tags.Replace(raw, "").Split('\n'))
            {
                var at = line.IndexOf(label, StringComparison.OrdinalIgnoreCase);
                var colon = at >= 0 ? line.IndexOf(':', at) : -1;
                if (colon > 0)
                {
                    var value = line.Substring(colon + 1).Trim();
                    return value.Length > 0 ? value : null;
                }
            }

            return null;
        }

        // Typing "info <word>" can land on another keyword with the same word.
        internal TerminalNode InfoNode(Target target)
        {
            return target != null && target.Keyword != null && infoNodes.TryGetValue(target.Keyword, out var node) ? node : null;
        }

        // The destination and the price sit on the CONFIRM option of the route page.
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

        private static TerminalNode ConfirmOf(TerminalNode node)
        {
            if (node.terminalOptions == null)
            {
                return null;
            }

            foreach (var option in node.terminalOptions)
            {
                if (option?.noun != null && option.noun.word == "confirm")
                {
                    return option.result;
                }
            }

            return null;
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
