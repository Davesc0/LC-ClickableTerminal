using System.Collections.Generic;

namespace ClickableTerminal.Api
{
    public static class TerminalMenu
    {
        internal static readonly List<MenuEntry> Added = new List<MenuEntry>();

        // For other mods: puts a command on the terminal's command list. "command" is what a player would type.
        public static void AddCommand(string label, string command, string description = null)
        {
            Added.Add(new MenuEntry { Label = label, Command = command, Description = description });
        }
    }
}
