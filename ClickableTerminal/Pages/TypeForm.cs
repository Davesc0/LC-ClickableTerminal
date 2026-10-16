using System;

namespace ClickableTerminal
{
    // A small form for a command that wants something typed after it: the text as it is typed, then SEND or CANCEL.
    internal static class TypeForm
    {
        internal static MenuPage Build(Terminal terminal, string title, string prefix, string prompt, string note)
        {
            var page = new MenuPage { Title = title };

            // The command is put on the input line, so whatever is typed next goes after it, and Enter sends it too.
            Commands.Type(terminal, prefix);

            Func<string> typed = () =>
            {
                var all = Commands.Typed(terminal);
                return all.StartsWith(prefix) ? all.Substring(prefix.Length) : all;
            };

            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            page.Entries.Add(new MenuEntry { Label = prompt });
            page.Entries.Add(new MenuEntry { Label = "", Big = true, LiveLabel = () => "> " + typed() + "_" });
            if (note != null)
            {
                page.Entries.Add(new MenuEntry { Label = note });
            }

            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            page.Entries.Add(new MenuEntry
            {
                Label = "[ SEND ]",
                Click = () =>
                {
                    if (typed().Trim().Length == 0)
                    {
                        return;
                    }

                    // The form steps aside first, so the answer leads back to the list it was opened from.
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
    }
}
