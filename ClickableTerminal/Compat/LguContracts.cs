using System;
using System.Linq;

namespace ClickableTerminal
{
    // The terminal side of Lategame Upgrades' contracts (Contracted hands them out too when both are installed): "demon <name>" for Exorcism, "lookup <serial>" for Defusal, "bruteforce <ip>" for Data Retrieval. Each runs in a window here, the spirits as buttons, the serial and the address on a line to type.
    internal static class LguContracts
    {
        private const string Unknown = "There is no known data on";
        private const string Banish = "To banish this spirit";
        private const string Wires = "CUT THE WIRES IN THE FOLLOWING ORDER";

        internal static void Exorcism(Terminal terminal)
        {
            Show(terminal, Commands.Ask(terminal, "demon"));
        }

        internal static void Defusal(Terminal terminal)
        {
            Answers.Typed(terminal, "DEFUSAL", "Type the serial number written on the bomb, then Enter. The wrong one gives a wrong order.",
                "lookup ", "LOOK UP", 20);
        }

        internal static void DataRetrieval(Terminal terminal)
        {
            Answers.Typed(terminal, "DATA RETRIEVAL", "Type the IP address shown on the laptop, then Enter. Then log in on the laptop itself.",
                "bruteforce ", "CONNECT", 24);
        }

        // An answer of these commands typed on one of our pages: in the same window as from the menu.
        internal static bool Owns(string printed)
        {
            var first = (printed ?? "").Trim();
            return first.StartsWith(Unknown) || first.StartsWith(Banish) || first.StartsWith(Wires)
                   || first.StartsWith("YOU MUST BE IN A DEFUSAL CONTRACT") || first.StartsWith("YOU MUST ENTER A SERIAL NUMBER");
        }

        internal static void Show(Terminal terminal, string answer)
        {
            var text = (answer ?? "").Trim();

            // "There is no known data on . Known spirits are:" and a spirit a line: those become the buttons.
            if (text.StartsWith(Unknown))
            {
                var at = text.IndexOf(':');
                var spirits = text.Substring(at + 1).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
                var window = new Window
                {
                    Title = "EXORCISM",
                    Text = "Which spirit is it? Its name is at the pentagram. The rite needs three items placed on it; " +
                           "a wrong one calls something worse."
                };

                var buttons = window.Group("SPIRITS");
                foreach (var spirit in spirits)
                {
                    var name = spirit;
                    buttons.Add(new ToolButton { Label = name, Click = () => Show(terminal, Commands.Ask(terminal, "demon " + name.ToLowerInvariant())) });
                }

                window.Buttons.Add(new ToolButton { Label = "CLOSE", Click = Window.Close });
                Window.Show(window);
                return;
            }

            var shown = new Window
            {
                Title = text.StartsWith(Banish) ? "EXORCISM" : text.StartsWith(Wires) || text.StartsWith("YOU MUST") ? "DEFUSAL" : "LATEGAME UPGRADES",
                Text = Window.Tidy(Lines(text), "")
            };

            if (text.StartsWith(Banish))
            {
                shown.Buttons.Add(new ToolButton { Label = "OTHER SPIRITS", Click = () => Exorcism(terminal) });
            }

            shown.Buttons.Add(new ToolButton { Label = "CLOSE", Click = Window.Close });
            Window.Show(shown);
        }

        // The answer comes with a blank line between every colour or item; in a window a line each reads better, with one blank line under the heading.
        private static string Lines(string text)
        {
            var lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
            return lines.Count < 2 ? text : lines[0] + "\n\n" + string.Join("\n", lines.Skip(1));
        }
    }
}
