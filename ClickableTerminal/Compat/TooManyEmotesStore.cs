using System;
using System.Text.RegularExpressions;

namespace ClickableTerminal
{
    // TooManyEmotes reads what is typed in its own code, so nothing in the game says what to send for a line.
    internal static class TooManyEmotesStore
    {
        private const string Header = "[TooManyEmotes]";

        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        internal static readonly Regex Entry = new Regex(@"^\s*\*\s*(.+?)\s*//\s*(.+?)\s*$", RegexOptions.Compiled);
        internal static readonly Regex Balance = new Regex(@"^Remaining (.+?) balance:\s*(.+?)\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static bool IsStore(string printed)
        {
            return printed.Contains(Header) && printed.Contains("//");
        }

        internal static MenuPage Build(Terminal terminal, string printed, string back)
        {
            var page = new MenuPage { Title = "EMOTES", Dense = true };
            page.Columns.Add(new Column { Header = "NAME" });
            page.Columns.Add(new Column { Header = "PRICE", Chars = 13, AlignRight = true });

            foreach (var raw in printed.Split('\n'))
            {
                var line = Tags.Replace(raw, "").Trim();
                var entry = Entry.Match(line);
                var balance = Balance.Match(line);

                if (entry.Success)
                {
                    var name = entry.Groups[1].Value;
                    var price = entry.Groups[2].Value;
                    var bought = price.IndexOf("purchased", StringComparison.OrdinalIgnoreCase) >= 0;

                    // Coloured by rarity. The colour stays on the label, the bare name is what gets sent.
                    var coloured = Entry.Match(raw.Trim());

                    page.Entries.Add(new MenuEntry
                    {
                        Label = coloured.Success && !bought ? coloured.Groups[1].Value : name,
                        Command = bought ? null : "emote " + name.ToLowerInvariant(),
                        Dim = bought,
                        Cells = new[] { bought ? "owned" : price }
                    });
                }
                else if (balance.Success)
                {
                    // "Emote credit: $100"
                    var text = Capital(balance.Groups[1].Value) + ": " + balance.Groups[2].Value;
                    page.Summary = page.Summary == null ? text : page.Summary + "   " + text;
                }
                else if (line.Length > 0 && !line.StartsWith("[") && !line.StartsWith("-") && line != "Store" && page.Entries.Count == 0 && page.Summary == null)
                {
                    // "Every emote is already unlocked!" and the like.
                    page.Summary = line;
                }
            }

            if (page.Entries.Count == 0 && page.Summary == null)
            {
                return null;
            }

            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            page.Entries.Add(new MenuEntry
            {
                Label = back != null ? $"[ BACK TO {back} ]" : "[ BACK ]",
                Click = () => Navigator.Back(terminal)
            });
            return page;
        }

        private static string Capital(string text)
        {
            return text.Length > 0 ? char.ToUpperInvariant(text[0]) + text.Substring(1) : text;
        }
    }
}
