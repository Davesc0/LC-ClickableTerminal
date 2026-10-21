using UnityEngine;

namespace ClickableTerminal
{
    internal static class TransmitPage
    {
        private const string Prefix = "transmit ";

        // The game cuts the message off after this many characters.
        private const int Longest = 10;

        internal static bool Handles(string verb)
        {
            return verb == "transmit";
        }

        internal static void ShowWindow(Terminal terminal)
        {
            if (Object.FindObjectOfType<SignalTranslator>() == null)
            {
                Missing(terminal);
                return;
            }

            Commands.Type(terminal, "");
            var window = new Window
            {
                Title = "TRANSMIT",
                Text = $"Type the message and press Enter or SEND. Up to {Longest} characters reach the crew.",
                Longest = Longest
            };
            window.Send = message => Send(terminal, message);
            window.Buttons.Add(new ToolButton { Label = "SEND", Click = () => Send(terminal, Commands.Typed(terminal)) });
            window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = Window.Close });
            Window.Show(window);
        }

        private static void Send(Terminal terminal, string message)
        {
            message = (message ?? "").Trim();
            if (message.Length == 0)
            {
                return;
            }

            if (message.Length > Longest)
            {
                message = message.Substring(0, Longest);
            }

            Commands.Type(terminal, "");
            var answer = Commands.Ask(terminal, Prefix + message);
            Window.Show(new Window { Title = "TRANSMIT", Text = Window.Tidy(answer, "") });
        }

        private static void Missing(Terminal terminal)
        {
            StorePage.OfferUpgrade(terminal, "Signal translator", "The ship has no signal translator. It is sold in the store, with the ship upgrades.");
        }
    }
}
