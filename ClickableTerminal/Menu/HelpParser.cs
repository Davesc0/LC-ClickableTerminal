using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ClickableTerminal
{
    // Mods that add a block to the help page (TooManyEmotes does) get a menu entry for free.
    internal static class HelpParser
    {
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly char[] ArgumentStart = { '[', '{', '(' };

        internal static List<MenuEntry> Parse(string text)
        {
            var entries = new List<MenuEntry>();
            MenuEntry current = null;

            foreach (var raw in text.Split('\n'))
            {
                var line = Tags.Replace(raw, "").Trim();

                if (line.Length == 0)
                {
                    current = null;
                }
                else if (line[0] == '>')
                {
                    current = FromHeading(line.Substring(1).Trim().TrimEnd(':'));
                    if (current != null)
                    {
                        entries.Add(current);
                    }
                }
                else if (current != null)
                {
                    current.Description = current.Description == null ? line : current.Description + " " + line;
                }
            }

            return entries;
        }

        private static MenuEntry FromHeading(string heading)
        {
            // ">ATK / INITATTACK" lists two spellings of one command.
            var alias = heading.IndexOf(" / ", System.StringComparison.Ordinal);
            if (alias > 0)
            {
                heading = heading.Substring(0, alias).Trim();
            }

            var argument = heading.IndexOfAny(ArgumentStart);
            if (heading.Length == 0 || argument == 0)
            {
                return null;
            }

            var entry = new MenuEntry { Label = heading.ToUpperInvariant() };

            // ">PING [Radar booster name]" needs a name typed after it.
            if (argument > 0)
            {
                entry.Prefill = heading.Substring(0, argument).Trim().ToLowerInvariant() + " ";
            }
            else
            {
                entry.Command = heading.ToLowerInvariant();
            }

            return entry;
        }
    }
}
