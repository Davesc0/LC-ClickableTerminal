using System.Linq;

namespace ClickableTerminal
{
    internal static class Answers
    {
        // "asks" is for a mod whose question does not read like one (Extra Days to Deadline asks through its own API).
        internal static void Run(Terminal terminal, string title, string command, bool asks = false)
        {
            Show(terminal, title, Commands.Ask(terminal, command), asks);
        }

        internal static void AskFirst(Terminal terminal, string title, string question, string yes, string command)
        {
            var window = new Window { Title = title, Text = question };
            window.Buttons.Add(new ToolButton { Label = yes, Click = () => Run(terminal, title, command) });
            window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = Window.Close });
            Window.Show(window);
        }

        private static void Show(Terminal terminal, string title, string answer, bool forced = false)
        {
            var window = new Window { Title = title, Text = Window.Tidy(answer, "") };
            var node = terminal.currentNode;
            // A command another mod broke asks nothing.
            var failed = answer.Contains("[Another mod failed");
            var asks = !failed && forced || ResultPages.AsksToConfirm(answer)
                       || (node != null && node.overrideOptions && node.terminalOptions != null
                           && node.terminalOptions.Any(option => option?.noun != null && option.noun.word == "confirm"));

            if (asks)
            {
                window.Buttons.Add(new ToolButton
                {
                    Label = "CONFIRM",
                    Click = () =>
                    {
                        Show(terminal, title, Commands.Ask(terminal, "confirm"));
                        Navigator.Rebuild(terminal);
                    }
                });
                window.Buttons.Add(new ToolButton
                {
                    Label = "DENY",
                    Click = () =>
                    {
                        Commands.Ask(terminal, "deny");
                        Window.Drop();
                    }
                });
            }
            else
            {
                window.Buttons.Add(new ToolButton { Label = "OK", Click = Window.Close });
            }

            Window.Show(window);
        }

        internal static void Typed(Terminal terminal, string title, string prompt, string prefix, string send, int longest, params ToolButton[] extras)
        {
            Commands.Type(terminal, "");
            var window = new Window { Title = title, Text = prompt, Longest = longest };
            window.Send = text =>
            {
                text = (text ?? "").Trim();
                if (text.Length == 0)
                {
                    return;
                }

                Commands.Type(terminal, "");
                Run(terminal, title, prefix + text);
            };

            window.Buttons.AddRange(extras);
            window.Buttons.Add(new ToolButton { Label = send, Click = () => window.Send(Commands.Typed(terminal)) });
            window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = Window.Close });
            Window.Show(window);
        }

        internal static ToolButton Fill(Terminal terminal, string label, string value)
        {
            return new ToolButton { Label = label, Click = () => Commands.Type(terminal, value) };
        }

        internal static MenuPage InWindows(Terminal terminal, MenuPage page)
        {
            foreach (var entry in page.Entries)
            {
                var command = entry.Command;
                if (command != null && entry.Open == null && entry.Click == null && InteractiveTerminal.Registered(command) == null)
                {
                    var title = entry.Label;
                    entry.Click = () => Run(terminal, title, command);
                }
            }

            return page;
        }
    }
}
