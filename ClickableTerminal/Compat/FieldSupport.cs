using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClickableTerminal
{
    // Contracted's Field Support, for the Defuse contract: the crew at the bomb tells the one at the terminal what they see (the marking on the case, what its display says, the wires from left to right), and the terminal answers with the cut order and the briefcase code. The mod asks it one question at a time, as typed commands. Here it runs in a window, every answer a button.
    internal static class FieldSupport
    {
        // "1. KILO". The answer is sent by its name: the wire count only takes "3", "4" or "5", not its number.
        internal static readonly Regex Choice = new Regex(@"^\s*(\d+)\.\s+(.+?)\s*$", RegexOptions.Compiled);
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

        private static readonly string[][] Home =
        {
            new[] { "START", "defuse start" },
            new[] { "STATUS", "defuse status" },
            new[] { "INSTRUCTIONS", "defuse instructions" },
            new[] { "REPORT", "defuse report" }
        };

        internal static void Show(Terminal terminal)
        {
            Ask(terminal, "defuse");
        }

        private static void Ask(Terminal terminal, string command)
        {
            var answer = Commands.Ask(terminal, command);
            var lines = answer.Replace("\r", "").Split('\n').ToList();
            var choices = new List<KeyValuePair<string, string>>();
            var text = new List<string>();
            var submit = false;

            // Only a question of the form lists its answers as "1. KILO"; the instructions number their rules the same way, and those stay text.
            var form = lines.Any(line => Tags.Replace(line, "").StartsWith("BACK to correct"));

            foreach (var raw in lines)
            {
                var line = Tags.Replace(raw, "");
                var choice = Choice.Match(line);
                if (form && choice.Success)
                {
                    choices.Add(new KeyValuePair<string, string>(choice.Groups[2].Value, choice.Groups[2].Value));
                    continue;
                }

                // What to type is what the buttons are for.
                if (line.StartsWith("BACK to correct") || line.StartsWith("Type 3, 4") || line.StartsWith("Type DEFUSE") || line.StartsWith("Type SUBMIT"))
                {
                    submit |= line.StartsWith("Type SUBMIT");
                    continue;
                }

                text.Add(raw);
            }

            var window = new Window { Title = "FIELD SUPPORT", Text = Window.Tidy(string.Join("\n", text), "Field Support") };

            if (choices.Count > 0)
            {
                var answers = window.Group("ANSWER");
                foreach (var choice in choices)
                {
                    var typed = choice.Value;
                    answers.Add(new ToolButton { Label = choice.Key, Click = () => Ask(terminal, typed) });
                }
            }

            if (submit)
            {
                window.Buttons.Add(new ToolButton { Label = "SUBMIT", Click = () => Ask(terminal, "defuse submit") });
            }

            if (form || submit)
            {
                window.Buttons.Add(new ToolButton { Label = "BACK", Click = () => Ask(terminal, "defuse back") });
                window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = () => Ask(terminal, "defuse cancel") });
            }
            else
            {
                foreach (var pair in Home)
                {
                    var typed = pair[1];
                    window.Buttons.Add(new ToolButton { Label = pair[0], Click = () => Ask(terminal, typed) });
                }
            }

            Window.Show(window);
        }
    }
}
