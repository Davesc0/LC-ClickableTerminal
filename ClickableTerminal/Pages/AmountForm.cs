using System;
using System.Collections.Generic;

namespace ClickableTerminal
{
    // A form for a command that wants an amount: set with buttons, so it works without a keyboard.
    internal static class AmountForm
    {
        private static readonly Dictionary<string, int> Amounts = new Dictionary<string, int>();

        internal static MenuPage Build(Terminal terminal, string title, string lead, string send, Func<int, string> command, int suggested, string suggestion)
        {
            var page = new MenuPage { Title = title, HideHeads = true, Plain = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 8 });
            page.Columns.Add(new Column { Chars = 8 });
            page.Columns.Add(new Column { Chars = 8 });

            if (!Amounts.TryGetValue(title, out var amount))
            {
                amount = 100;
            }

            Action<int> set = to =>
            {
                Amounts[title] = Math.Max(1, to);
                Navigator.Rebuild(terminal);
            };

            Space(page);
            page.Entries.Add(new MenuEntry { Label = lead + "  $" + amount, Big = true });
            Space(page);
            page.Entries.Add(new MenuEntry
            {
                Label = "More",
                Cells = new[] { "[ +1 ]", "[ +10 ]", "[ +100 ]" },
                CellClicks = new Action[] { () => set(amount + 1), () => set(amount + 10), () => set(amount + 100) }
            });
            page.Entries.Add(new MenuEntry
            {
                Label = "Less",
                Cells = new[] { "[ -1 ]", "[ -10 ]", "[ -100 ]" },
                CellClicks = new Action[] { () => set(amount - 1), () => set(amount - 10), () => set(amount - 100) }
            });

            if (suggested > 0 && suggestion != null)
            {
                Space(page);
                page.Entries.Add(new MenuEntry { Label = $"[ {suggestion}: ${suggested} ]", Click = () => set(suggested) });
            }

            Space(page);
            page.Entries.Add(new MenuEntry
            {
                Label = "[ " + send + " ]",
                Click = () =>
                {
                    // The form steps aside first, so the answer leads back to the list it was opened from.
                    Navigator.DropCurrent();
                    Commands.Submit(terminal, command(amount));
                }
            });
            page.Entries.Add(new MenuEntry { Label = "[ CANCEL ]", Click = () => Navigator.Back(terminal) });
            return page;
        }

        private static void Space(MenuPage page)
        {
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
        }
    }
}
