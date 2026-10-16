using UnityEngine;

namespace ClickableTerminal
{
    // A small form for the signal translator: type the message, then SEND or CANCEL.
    internal static class TransmitPage
    {
        private const string Prefix = "transmit ";

        // The game cuts the message off after this many characters.
        private const int Longest = 10;

        internal static bool Handles(string verb)
        {
            return verb == "transmit";
        }

        internal static MenuPage Build(Terminal terminal)
        {
            var page = new MenuPage { Title = "TRANSMIT" };

            // The translator is a ship upgrade. Without it standing in the ship the command does nothing.
            if (Object.FindObjectOfType<SignalTranslator>() == null)
            {
                page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
                page.Entries.Add(new MenuEntry { Label = "No signal translator on board.", Big = true });
                page.Entries.Add(new MenuEntry { Label = "It is sold in the store, with the ship upgrades." });
                page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
                page.Entries.Add(new MenuEntry { Label = "[ BACK ]", Click = () => Navigator.Back(terminal) });
                return page;
            }

            // The command is put on the input line, so whatever is typed next is the message, and Enter sends it too.
            Commands.Type(terminal, Prefix);

            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            page.Entries.Add(new MenuEntry { Label = "Type the message to send:" });
            page.Entries.Add(new MenuEntry { Label = "", Big = true, LiveLabel = () => "> " + Message(terminal) + "_" });
            page.Entries.Add(new MenuEntry { Label = $"Up to {Longest} characters reach the crew." });
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            page.Entries.Add(new MenuEntry
            {
                Label = "[ SEND ]",
                Click = () =>
                {
                    if (Message(terminal).Trim().Length == 0)
                    {
                        return;
                    }

                    // The form steps aside first, so the answer leads back to the command list.
                    Navigator.DropCurrent();
                    terminal.OnSubmit();
                }
            });
            page.Entries.Add(new MenuEntry
            {
                Label = "[ CANCEL ]",
                Click = () =>
                {
                    Commands.Type(terminal, "");
                    Navigator.Back(terminal);
                }
            });
            return page;
        }

        private static string Message(Terminal terminal)
        {
            var typed = Commands.Typed(terminal);
            return typed.StartsWith(Prefix) ? typed.Substring(Prefix.Length) : typed;
        }
    }
}
