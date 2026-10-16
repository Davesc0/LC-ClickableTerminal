using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickableTerminal
{
    internal enum Marker
    {
        None,
        Here,
        Locked,
        Utility
    }

    internal class MenuEntry
    {
        // A small icon after the label.
        internal Marker Marker;

        internal string Label;
        internal string Description;

        // Command is sent as it is. Prefill is only typed in, for commands that need a name after them.
        internal string Command;
        internal string Prefill;

        // Opens another page of ours instead of sending anything.
        internal Func<MenuPage> Open;

        // A label that follows what is going on, like the message being typed. Read every frame.
        internal Func<string> LiveLabel;

        // Anything else a click should do, like ticking a filter.
        internal Action Click;

        // Sent on a right click, usually "info ...", and on a click of the mouse wheel.
        internal string AltCommand;
        internal string WheelCommand;

        internal bool IsHeader;
        internal bool Dim;

        // Drawn without a box: a line of text that can be clicked.
        internal bool Flat;

        // A size bigger than the rest, for the one thing a page is about.
        internal bool Big;

        // One text per column after the name, and one value per column (name included) to sort by.
        internal string[] Cells;
        internal IComparable[] Keys;

        // A cell with an action is a small button of its own, like the [ - ] and [ + ] of an amount.
        internal Action[] CellClicks;

        // Whatever the page wants to remember about the row, like which item it stands for.
        internal int Id = -1;

        // Drawn instead of text in the page's icon column: one hand or two.
        internal int Icons;
    }

    internal class Column
    {
        internal string Header;

        // The least room the column gets, in letters. It grows to fit its longest value, up to MaxChars when that is set.
        internal int Chars;
        internal int MaxChars;
        internal bool AlignRight;
        internal bool AlignCenter;
    }

    internal class ToolButton
    {
        internal string Label;
        internal bool Active;

        // Greyed out: a switch that is off.
        internal bool Off;
        internal Action Click;
    }

    internal class MenuPage
    {
        internal string Title;
        internal string Hint;

        // One line above the table, for totals and the like.
        internal string Summary;

        // Entries kept above the table, out of the sorting and the filters. They click like rows.
        internal List<MenuEntry> Pinned = new List<MenuEntry>();

        // For tables that are not really tables, like the confirmation page: no column headers, no shaded rows.
        internal bool HideHeads;
        internal bool Plain;

        // Rows closer together, for pages that are mostly sentences.
        internal bool Dense;

        // Which column shows icons instead of text, -1 for none.
        internal int IconColumn = -1;
        internal List<MenuEntry> Entries = new List<MenuEntry>();

        // Column 0 is the name. Empty means a plain list, with descriptions under the labels.
        internal List<Column> Columns = new List<Column>();
        internal Action<int> SortBy;
        internal List<ToolButton> Tools = new List<ToolButton>();

        internal bool HasDescriptions => Entries.Any(entry => !string.IsNullOrEmpty(entry.Description));
    }

    // What the player picked on a page: sorting and filters. Kept while the game runs.
    internal class PageState
    {
        internal int SortColumn = -1;
        internal bool Descending;
        private readonly HashSet<string> flags = new HashSet<string>();

        // First click sorts, the second turns it around, the third goes back to the game's own order.
        internal void SortBy(int column)
        {
            if (SortColumn != column)
            {
                SortColumn = column;
                Descending = false;
            }
            else if (!Descending)
            {
                Descending = true;
            }
            else
            {
                SortColumn = -1;
                Descending = false;
            }
        }

        internal bool Has(string flag)
        {
            return flags.Contains(flag);
        }

        internal void Toggle(string flag)
        {
            if (!flags.Remove(flag))
            {
                flags.Add(flag);
            }
        }

        internal IEnumerable<string> FlagsWith(string prefix)
        {
            return flags.Where(flag => flag.StartsWith(prefix)).Select(flag => flag.Substring(prefix.Length));
        }

        internal void ClearFlags(string prefix)
        {
            flags.RemoveWhere(flag => flag.StartsWith(prefix));
        }

        internal string HeaderFor(string header, int column)
        {
            return SortColumn != column ? header : header + (Descending ? " v" : " ^");
        }

        internal ToolButton ToolFor(string label, string flag, Action changed)
        {
            return new ToolButton
            {
                Label = (Has(flag) ? "[x] " : "[ ] ") + label,
                Active = Has(flag),
                Click = () =>
                {
                    Toggle(flag);
                    changed();
                }
            };
        }

        internal List<MenuEntry> Sorted(List<MenuEntry> rows)
        {
            if (SortColumn < 0)
            {
                return rows;
            }

            Func<MenuEntry, IComparable> key = row => row.Keys != null && SortColumn < row.Keys.Length ? row.Keys[SortColumn] : null;
            return (Descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ToList();
        }
    }
}
