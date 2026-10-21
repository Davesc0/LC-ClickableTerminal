using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickableTerminal
{
    internal enum Marker
    {
        None,
        Here,
        Locked
    }

    internal class MenuEntry
    {
        internal Marker Marker;
        internal string Label;
        internal string Description;

        internal string Command;

        // Only typed in, not sent, for commands that need a name after them.
        internal string Prefill;
        internal Func<MenuPage> Open;
        internal Func<string> LiveLabel;
        internal Func<string[]> LiveCells;
        internal Action Click;
        internal Action AltClick;
        internal bool IsHeader;
        internal bool Dim;

        // No box: a line of text that can be clicked.
        internal bool Flat;
        internal bool Big;

        // One text per column after the name, and one sort key per column (name included).
        internal string[] Cells;
        internal IComparable[] Keys;
        internal Action[] CellClicks;
        internal int Id = -1;

        // Hand icons in the page's icon column: one or two.
        internal int Icons;
        internal UnityEngine.Sprite Picture;
        internal string Sub;
        internal bool Active;
    }

    internal class Detail
    {
        internal UnityEngine.Sprite Picture;
        internal UnityEngine.Texture Texture;
        internal string Missing;
        internal string Title;
        internal string Text;
        internal readonly List<Fact> Facts = new List<Fact>();
        internal readonly List<ToolButton> Buttons = new List<ToolButton>();
        internal readonly List<KeyValuePair<string, List<ToolButton>>> Groups = new List<KeyValuePair<string, List<ToolButton>>>();
        internal string Foot;

        internal List<ToolButton> Group(string title)
        {
            var buttons = new List<ToolButton>();
            Groups.Add(new KeyValuePair<string, List<ToolButton>>(title, buttons));
            return buttons;
        }
    }

    internal class Window
    {
        internal string Title;
        internal UnityEngine.Texture Texture;
        internal string Side;
        internal bool NoPicture;
        internal Action<string> Send;
        internal int Longest = 30;
        internal string Text;
        internal readonly List<KeyValuePair<string, List<ToolButton>>> Groups = new List<KeyValuePair<string, List<ToolButton>>>();
        internal readonly List<ToolButton> Buttons = new List<ToolButton>();

        // Each group on one line: the title on the left, the buttons at the right end.
        internal bool GroupsInLine;
        internal readonly List<ToolButton> RightButtons = new List<ToolButton>();

        internal List<ToolButton> Group(string title)
        {
            var buttons = new List<ToolButton>();
            Groups.Add(new KeyValuePair<string, List<ToolButton>>(title, buttons));
            return buttons;
        }

        // An Interactive Terminal API app's screen, followed live.
        internal bool App;
        internal Action Closing;

        internal static Window Open { get; private set; }

        internal static void Show(Window window)
        {
            Open = window;
        }

        internal static void Close()
        {
            var closed = Open;
            Open = null;
            closed?.Closing?.Invoke();
        }

        // Without Closing, because what it showed is gone already.
        internal static void Drop()
        {
            Open = null;
        }

        internal static void ShowPage(Terminal terminal, string title, TerminalNode node)
        {
            if (node != null)
            {
                Show(new Window { Title = title, Text = Tidy(Commands.PageText(terminal, node), title) });
            }
        }

        private static readonly System.Text.RegularExpressions.Regex Rule = new System.Text.RegularExpressions.Regex(@"^\s*[-=_]{3,}\s*$");
        private static readonly System.Text.RegularExpressions.Regex Label = new System.Text.RegularExpressions.Regex(@"^([A-Z][A-Z '\-]{2,24}):");
        private static readonly System.Text.RegularExpressions.Regex Spaces = new System.Text.RegularExpressions.Regex(@"\n{3,}");

        internal static string Tidy(string text, string title)
        {
            var lines = new List<string>();
            var key = Commands.Normalize(title ?? "");

            foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                if (Rule.IsMatch(raw))
                {
                    continue;
                }

                // "177-Burrow" under the title BURROW.
                if (lines.Count == 0 && key.Length > 0 && raw.Trim().Length > 0 && Commands.Normalize(raw).EndsWith(key))
                {
                    continue;
                }

                if (lines.Count == 0 && raw.Trim().Length == 0)
                {
                    continue;
                }

                lines.Add(Label.Replace(raw.Trim(), "<alpha=#FF>$1:<alpha=#BB>"));
            }

            return "<alpha=#BB>" + Spaces.Replace(string.Join("\n", lines), "\n\n").Trim();
        }
    }

    internal class Fact
    {
        internal string Label;
        internal string Value;
    }

    internal class Column
    {
        internal string Header;

        // Least room in letters. It grows to the longest value, up to MaxChars.
        internal int Chars;
        internal int MaxChars;
        internal bool AlignRight;

        // A share of the list's width instead of what the values need.
        internal float Share;
    }

    internal class ToolButton
    {
        internal string Label;
        internal bool Active;
        internal bool Off;
        internal bool Primary;

        // Keeps its small width in a panel's row, the others share the rest.
        internal bool Narrow;

        // Only the label, no frame, not clickable.
        internal bool Bare;
        internal Action Click;
    }

    internal class MenuPage
    {
        internal string Title;
        internal string Hint;
        internal string Subtitle;

        // Where the screen is narrow the tabs become buttons on top.
        internal List<MenuEntry> Sidebar;
        internal Func<MenuEntry, Detail> DetailFor;
        internal Func<Detail> Extra;
        internal string Summary;

        // Kept above the table, out of the sorting and the filters.
        internal List<MenuEntry> Pinned = new List<MenuEntry>();
        internal bool HideHeads;
        internal bool Plain;
        internal bool Dialog;
        internal UnityEngine.Texture Picture;
        internal float PictureAspect = 4f / 3f;

        // Drawn here a few times a second while the game is not drawing it.
        internal UnityEngine.Camera Camera;
        internal bool LivePanels;
        internal Action<string> Send;
        internal ToolButton Corner;
        internal bool SelectRows;
        internal MenuEntry Pick;

        // Against the terminal's own letter size. 0 means the TextScale setting decides.
        internal float Scale;
        internal float ScaleMin = 0.6f;
        internal float ScaleMax = 1f;

        internal MenuPage Sized(float scale, float min, float max)
        {
            Scale = scale;
            ScaleMin = min;
            ScaleMax = max;
            return this;
        }

        internal bool Dense;
        internal int IconColumn = -1;
        internal List<MenuEntry> Entries = new List<MenuEntry>();

        // Column 0 is the name. Empty means a plain list with descriptions under the labels.
        internal List<Column> Columns = new List<Column>();
        internal Action<int> SortBy;
        internal List<ToolButton> Tools = new List<ToolButton>();

        internal bool HasDescriptions => Entries.Any(entry => !string.IsNullOrEmpty(entry.Description));
    }

    internal class PageState
    {
        internal int SortColumn = -1;
        internal bool Descending;
        private readonly HashSet<string> flags = new HashSet<string>();

        // First click sorts, the second turns it around, the third goes back to the game's order.
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
