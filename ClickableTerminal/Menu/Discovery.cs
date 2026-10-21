using System.Collections.Generic;

namespace ClickableTerminal
{
    internal static class Discovery
    {
        internal static List<MenuEntry> Unlisted(Terminal terminal, HashSet<string> known)
        {
            var found = new List<MenuEntry>();

            foreach (var entry in Api.TerminalMenu.Added)
            {
                Add(found, known, entry.Label, entry.Command, entry.Description);
            }

            foreach (var pair in Settings.ExtraCommands.Value.Split(';'))
            {
                var parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    Add(found, known, parts[0].Trim(), parts[1].Trim(), null);
                }
            }

            foreach (var command in InteractiveTerminal.Commands())
            {
                Add(found, known, command, command, null);
            }

            return found;
        }

        private static void Add(List<MenuEntry> found, HashSet<string> known, string label, string command, string description)
        {
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(command)
                || KnownCommands.IsHidden(command) || !known.Add(Commands.Normalize(command)))
            {
                return;
            }

            found.Add(new MenuEntry { Label = label.ToUpperInvariant(), Command = command, Description = description });
        }
    }
}
