using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // Draws a MenuPage over the terminal screen: title, filter buttons, column headers,
    // a scrolling list of rows and what is being typed. Every text is one line that cuts off, nothing wraps.
    internal class MenuView
    {
        private const int MaxCells = 5;
        private const int MaxIcons = 2;

        private class Row
        {
            internal RectTransform Rect;
            internal Image Back;
            internal TextMeshProUGUI Label;
            internal TextMeshProUGUI Description;
            internal readonly TextMeshProUGUI[] Cells = new TextMeshProUGUI[MaxCells];
            internal readonly HotSpot[] CellSpots = new HotSpot[MaxCells];
            internal readonly RawImage[] Icons = new RawImage[MaxIcons];
            internal RawImage Marker;

            // What the row was last laid out for, so scrolling only moves what has to move.
            internal int Version = -1;
            internal float Indent;
            internal int CellCount;
            internal readonly HotSpot Spot = new HotSpot();
            internal bool Clickable;
        }

        private class Button
        {
            internal Image Box;
            internal TextMeshProUGUI Text;
            internal RawImage Marker;
            internal readonly HotSpot Spot = new HotSpot();

            internal RectTransform Rect => Box != null ? Box.rectTransform : Text.rectTransform;
        }

        private readonly Terminal terminal;
        private static readonly Vector3[] Corners = new Vector3[4];
        private readonly TMP_Text like;
        private readonly float size;
        private readonly float head;
        private readonly float margin;
        private readonly float charWidth;
        private readonly RectTransform root;
        private readonly RectTransform list;
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI summary;
        private readonly List<Button> pinned = new List<Button>();
        private readonly Image headRule;
        private readonly TextMeshProUGUI typed;
        private readonly TextMeshProUGUI position;
        private readonly List<Row> rows = new List<Row>();
        private readonly List<Button> tools = new List<Button>();
        private readonly List<Button> heads = new List<Button>();

        private MenuPage page;
        private string where = "";
        private string[] hintFor;
        private TMPro.TMP_SpriteAsset hintIcons;
        private int first;
        private int visibleRows;
        private float[] widths = new float[4];
        private float rowHeight;
        private bool sectioned;
        private bool table;
        private int version;

        internal MenuView(Terminal terminal, RectTransform frame, TMP_Text like)
        {
            this.terminal = terminal;
            this.like = like;
            // The top row is laid out in the terminal's own letter size, because the credits and the BACK and MENU
            // buttons in it do not shrink. Everything under it follows the text scale from the config.
            head = like.fontSize;
            Scale = Mathf.Clamp(Settings.TextScale.Value, 0.6f, 1f);
            size = head * Scale;
            margin = size * 0.6f;

            charWidth = like.GetPreferredValues("0000000000").x / 10f * Scale;

            var holder = new GameObject("ClickableTerminalPage", typeof(RectTransform));
            holder.layer = frame.gameObject.layer;
            root = (RectTransform)holder.transform;
            root.SetParent(frame, false);
            Ui.Stretch(root);
            root.SetAsFirstSibling();

            // The top row is shared: the game's credits on the left, BACK and MENU on the right.
            title = Ui.NewText("Title", root, like, head * 1.1f);

            // On a screen with little room between the credits and the buttons the title gets smaller, not cut off.
            title.enableAutoSizing = true;
            title.fontSizeMax = head * 1.1f;
            title.fontSizeMin = head * 0.5f;
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Overflow;
            Ui.PinToTop(title.rectTransform, head * 5.5f, head * 0.35f, head * 13.5f, head * 1.4f);

            var rule = Ui.NewBox("Rule", root);
            rule.color = Faded(0.5f);
            Ui.PinToTop(rule.rectTransform, margin, head * 2.5f, margin, 1f);

            summary = Ui.NewText("Summary", root, like, size);

            headRule = Ui.NewBox("HeadRule", root);
            headRule.color = Faded(0.35f);

            var listHolder = new GameObject("List", typeof(RectTransform));
            listHolder.layer = holder.layer;
            list = (RectTransform)listHolder.transform;
            list.SetParent(root, false);
            list.anchorMin = Vector2.zero;
            list.anchorMax = Vector2.one;

            typed = Ui.NewText("Typed", root, like, size);
            Bottom(typed.rectTransform);

            // Nothing on a page is smaller than the game's own terminal text. From a distance the screen
            // is only a few hundred pixels wide, and anything smaller falls apart.
            position = Ui.NewText("Position", root, like, size);
            position.alignment = TextAlignmentOptions.Right;
            position.color = Faded(0.6f);
            Bottom(position.rectTransform);
        }

        internal float Scale { get; }

        // For when the text scale is changed while the game runs: the view is thrown away and built again.
        internal void Dispose()
        {
            UnityEngine.Object.Destroy(root.gameObject);
        }

        internal void Show(MenuPage shown)
        {
            if (shown == null)
            {
                Ui.SetActive(root, false);
                page = null;
                return;
            }

            Ui.SetActive(root, true);

            if (shown != page)
            {
                // A sort or filter click hands over a new page with the same title: keep the place in the list.
                if (page == null || page.Title != shown.Title)
                {
                    first = 0;
                }

                page = shown;
                Layout();
            }
        }

        internal bool Scroll(int by)
        {
            if (page == null)
            {
                return false;
            }

            var before = first;
            first = Mathf.Clamp(first + by, 0, Mathf.Max(0, page.Entries.Count - visibleRows));
            if (first != before)
            {
                FillRows();
            }

            return first != before;
        }

        // Stepping past the last row on screen with the arrows moves the list instead of the cursor.
        internal bool ScrollFrom(Vector3 world, bool down)
        {
            if (page == null)
            {
                return false;
            }

            var onList = false;

            for (var i = 0; i < visibleRows && i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.Rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var area = row.Rect.rect;
                var height = row.Rect.InverseTransformPoint(world).y;

                if (height >= area.yMin && height <= area.yMax)
                {
                    onList = true;
                }
                else if (CanClick(row) && (down ? height > area.yMax : height < area.yMin))
                {
                    // There is still a row to step onto that way.
                    return false;
                }
            }

            return onList && Scroll(down ? 1 : -1);
        }

        // Left and right on the list: to the section before or after the one the cursor is in,
        // or to the top and the bottom of a list that has no sections.
        internal bool Jump(Vector3 world, bool forward, RectTransform frame, out Vector2 stand)
        {
            stand = default(Vector2);
            if (page == null)
            {
                return false;
            }

            var at = -1;
            for (var i = 0; i < visibleRows && i < rows.Count; i++)
            {
                if (Under(rows[i].Rect, world))
                {
                    at = first + i;
                    break;
                }
            }

            var count = page.Entries.Count;
            if (at < 0 || count == 0)
            {
                return false;
            }

            var target = forward ? count - 1 : 0;
            if (forward)
            {
                for (var i = at + 1; i < count; i++)
                {
                    if (IsBar(page.Entries[i]))
                    {
                        target = i;
                        break;
                    }
                }
            }
            else
            {
                // From the first row under a bar the way back is the section before, not the bar just above.
                for (var i = at - 2; i >= 0; i--)
                {
                    if (IsBar(page.Entries[i]))
                    {
                        target = i;
                        break;
                    }
                }
            }

            first = Mathf.Clamp(target, 0, Mathf.Max(0, count - visibleRows));
            FillRows();

            // The cursor goes on the first row there that can be clicked.
            for (var i = Mathf.Max(0, target - first); i < visibleRows && i < rows.Count; i++)
            {
                if (rows[i].Rect.gameObject.activeInHierarchy && CanClick(rows[i]))
                {
                    var area = rows[i].Rect.rect;
                    var point = new Vector2(Mathf.Lerp(area.xMin, area.xMax, 0.12f), area.center.y);
                    stand = frame.InverseTransformPoint(rows[i].Rect.TransformPoint(point));
                    return true;
                }
            }

            stand = frame.InverseTransformPoint(world);
            return true;
        }

        // Where the cursor can be put to stand on something that clicks, as points on the frame.
        internal void Targets(RectTransform frame, List<Vector2> into)
        {
            foreach (var button in tools)
            {
                Target(frame, button.Rect, 0.5f, into);
            }

            foreach (var button in pinned)
            {
                Target(frame, button.Rect, 0.5f, into);
            }

            foreach (var button in heads)
            {
                if (button.Spot.Click != null)
                {
                    Target(frame, button.Rect, 0.5f, into);
                }
            }

            foreach (var row in rows)
            {
                if (row.Clickable)
                {
                    Target(frame, row.Rect, 0.12f, into);
                }

                for (var i = 0; i < MaxCells; i++)
                {
                    if (row.CellSpots[i].Click != null)
                    {
                        Target(frame, row.Cells[i].rectTransform, 0.5f, into);
                    }
                }
            }
        }

        private static bool CanClick(Row row)
        {
            if (row.Clickable)
            {
                return true;
            }

            for (var i = 0; i < MaxCells; i++)
            {
                if (row.CellSpots[i].Click != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Target(RectTransform frame, RectTransform rect, float along, List<Vector2> into)
        {
            if (!rect.gameObject.activeInHierarchy)
            {
                return;
            }

            var area = rect.rect;
            var point = new Vector2(Mathf.Lerp(area.xMin, area.xMax, along), area.center.y);
            into.Add(frame.InverseTransformPoint(rect.TransformPoint(point)));
        }

        // Labels that follow something live. Only the rows on screen are looked at.
        internal void Tick()
        {
            if (page == null)
            {
                return;
            }

            if (!ReferenceEquals(hintFor, TerminalMouse.PadNames) || (hintFor != null && !ReferenceEquals(hintIcons, PadIcons.Sheet)))
            {
                DrawHint();
            }

            for (var i = 0; i < visibleRows && i < rows.Count; i++)
            {
                var index = first + i;
                if (index < page.Entries.Count && page.Entries[index].LiveLabel != null)
                {
                    rows[i].Label.text = page.Entries[index].LiveLabel();
                }
            }
        }

        internal void SetTyped(string text)
        {
            typed.text = "> " + text + "_";
        }

        internal HotSpot SpotAt(Vector3 world)
        {
            foreach (var button in tools)
            {
                if (Under(button.Rect, world))
                {
                    return At(button.Spot, button.Rect);
                }
            }

            foreach (var button in pinned)
            {
                if (Under(button.Rect, world))
                {
                    return At(button.Spot, button.Rect);
                }
            }

            foreach (var button in heads)
            {
                if (button.Spot.Click != null && Under(button.Rect, world))
                {
                    return At(button.Spot, button.Rect);
                }
            }

            foreach (var row in rows)
            {
                if (!Under(row.Rect, world))
                {
                    continue;
                }

                for (var i = 0; i < MaxCells; i++)
                {
                    if (row.CellSpots[i].Click != null && Under(row.Cells[i].rectTransform, world))
                    {
                        return At(row.CellSpots[i], row.Cells[i].rectTransform);
                    }
                }

                return row.Clickable ? At(row.Spot, row.Rect) : null;
            }

            return null;
        }

        private static bool Under(RectTransform rect, Vector3 world)
        {
            return rect.gameObject.activeInHierarchy && rect.rect.Contains(rect.InverseTransformPoint(world));
        }

        private static HotSpot At(HotSpot spot, RectTransform rect)
        {
            spot.Space = rect;
            spot.Area = rect.rect;
            return spot;
        }

        private void Layout()
        {
            title.text = page.Title;

            // The game's credits sit in the same top row and grow with the number, so the title starts after them.
            // The title starts right after the credits' box and stops before the buttons on the right (- + BACK MENU).
            var box = Ui.CreditsBox(terminal.topRightText);
            var left = head * 5.5f;
            if (box != null)
            {
                box.GetWorldCorners(Corners);
                left = Mathf.Clamp(root.InverseTransformPoint(Corners[2]).x - root.rect.xMin + head * 0.8f, head, root.rect.width * 0.4f);
            }

            title.alignment = TextAlignmentOptions.Left;
            Ui.PinToTop(title.rectTransform, left, head * 0.35f, head * 17.5f, head * 1.4f);

            MeasureColumns();

            var top = head * 2.9f;
            top = LayoutTools(top);
            top = LayoutSummary(top);
            top = LayoutHeads(top);

            list.offsetMin = new Vector2(margin, size * 2f);
            list.offsetMax = new Vector2(-margin, -top);

            table = page.Columns.Count > 0;
            rowHeight = size * (page.Dense ? 1.25f : table ? 1.4f : page.HasDescriptions ? 2.5f : 1.5f);

            // With section titles the rows under them step in. Without, every other row is shaded instead.
            sectioned = page.Entries.Any(IsBar);

            visibleRows = Mathf.Max(1, Mathf.FloorToInt(list.rect.height / rowHeight));
            version++;

            while (rows.Count < visibleRows)
            {
                rows.Add(NewRow());
            }

            for (var i = 0; i < rows.Count; i++)
            {
                if (i < visibleRows)
                {
                    Ui.PinToTop(rows[i].Rect, 0f, i * rowHeight, 0f, rowHeight);
                }
                else
                {
                    Ui.SetActive(rows[i].Rect, false);
                }
            }

            FillRows();
        }

        // The part that changes while scrolling: which entry each row shows. The rows themselves stay where they are.
        private void FillRows()
        {
            var count = page.Entries.Count;
            first = Mathf.Clamp(first, 0, Mathf.Max(0, count - visibleRows));

            for (var i = 0; i < visibleRows; i++)
            {
                var row = rows[i];
                var index = first + i;
                Ui.SetActive(row.Rect, index < count);

                if (index < count)
                {
                    Fill(row, page.Entries[index], rowHeight, sectioned, table && !sectioned && !page.Plain && index % 2 == 1);
                }
            }

            where = count > visibleRows ? $"{first + 1}-{Mathf.Min(count, first + visibleRows)} / {count}" : "";
            DrawHint();
        }

        // The hint names the buttons of whatever the player is steering with.
        private void DrawHint()
        {
            hintFor = TerminalMouse.PadNames;
            hintIcons = PadIcons.Sheet;
            if (hintIcons != null && position.spriteAsset != hintIcons)
            {
                position.spriteAsset = hintIcons;
            }

            position.text = string.IsNullOrEmpty(page.Hint) ? where : TerminalMouse.Words(page.Hint) + "   " + where;
        }

        private static bool IsBar(MenuEntry entry)
        {
            return entry.IsHeader && !string.IsNullOrEmpty(entry.Label);
        }

        private float LayoutTools(float top)
        {
            var x = margin;

            for (var i = 0; i < page.Tools.Count; i++)
            {
                if (i == tools.Count)
                {
                    var box = Ui.NewBox("Tool", root);
                    var text = Ui.NewText("Label", box.rectTransform, like, size * 0.9f);
                    text.alignment = TextAlignmentOptions.Center;
                    text.overflowMode = TextOverflowModes.Overflow;
                    Ui.Stretch(text.rectTransform);
                    tools.Add(new Button { Box = box, Text = text });
                }

                var tool = page.Tools[i];
                var button = tools[i];

                // Sized to what the label really takes, so it is never cut off.
                var width = button.Text.GetPreferredValues(tool.Label).x + size * 0.7f;

                // Too many to fit side by side go on a second line.
                if (x > margin && x + width > root.rect.width - margin)
                {
                    x = margin;
                    top += size * 1.7f;
                }

                button.Text.text = tool.Label;
                button.Text.color = tool.Off ? Faded(0.45f) : like.color;
                button.Box.color = Faded(tool.Active ? 0.35f : tool.Off ? 0.05f : 0.12f);
                button.Spot.Click = tool.Click;
                Ui.SetActive(button.Box, true);
                PinLeft(button.Rect, x, top, width, size * 1.4f);
                x += width + size * 0.4f;
            }

            for (var i = page.Tools.Count; i < tools.Count; i++)
            {
                Ui.SetActive(tools[i].Box, false);
            }

            return page.Tools.Count > 0 ? top + size * 1.8f : top;
        }

        private float LayoutSummary(float top)
        {
            var shown = !string.IsNullOrEmpty(page.Summary);
            Ui.SetActive(summary, shown);

            if (shown)
            {
                summary.text = page.Summary;
                Ui.PinToTop(summary.rectTransform, margin + size * 0.4f, top, margin, size * 1.3f);
                top += size * 1.6f;
            }

            return LayoutPinned(top);
        }

        // Small buttons side by side, on as many lines as they need.
        private float LayoutPinned(float top)
        {
            var x = margin;
            var tall = size * 1.5f;
            var icon = size * 0.95f;
            var limit = root.rect.width - margin;

            for (var i = 0; i < page.Pinned.Count; i++)
            {
                if (i == pinned.Count)
                {
                    var box = Ui.NewBox("Pinned", root);
                    var text = Ui.NewText("Label", box.rectTransform, like, size);
                    text.overflowMode = TextOverflowModes.Overflow;
                    Ui.Stretch(text.rectTransform);
                    text.margin = new Vector4(size * 0.5f, 0f, 0f, 0f);
                    pinned.Add(new Button { Box = box, Text = text, Marker = NewIcon(box.rectTransform) });
                }

                var entry = page.Pinned[i];
                var button = pinned[i];
                var marked = entry.Marker != Marker.None;
                var width = button.Text.GetPreferredValues(entry.Label).x + size + (marked ? icon + size * 0.3f : 0f);

                if (x > margin && x + width > limit)
                {
                    x = margin;
                    top += size * 1.8f;
                }

                button.Text.text = entry.Label;
                button.Text.color = entry.Dim ? Faded(0.5f) : like.color;
                button.Box.color = entry.Flat ? Faded(0f) : Faded(0.12f);
                button.Spot.Click = ClickFor(entry);
                button.Spot.AltClick = entry.AltCommand != null ? () => Commands.Submit(terminal, entry.AltCommand) : (Action)null;
                Ui.SetActive(button.Box, true);
                PinLeft(button.Rect, x, top, width, tall);

                Ui.SetActive(button.Marker, marked);
                if (marked)
                {
                    PlaceMarker(button.Marker, entry.Marker, button.Text, entry.Label, size * 0.2f, tall);
                }

                x += width + size * 0.5f;
            }

            for (var i = page.Pinned.Count; i < pinned.Count; i++)
            {
                Ui.SetActive(pinned[i].Box, false);
            }

            return page.Pinned.Count > 0 ? top + size * 1.9f : top;
        }

        private float LayoutHeads(float top)
        {
            var columns = page.HideHeads ? 0 : page.Columns.Count;

            for (var i = 0; i < columns; i++)
            {
                if (i == heads.Count)
                {
                    var text = Ui.NewText("Head", root, like, size);
                    text.color = Faded(0.7f);
                    heads.Add(new Button { Text = text });
                }

                var column = i;
                var button = heads[i];
                button.Text.text = page.Columns[i].Header;
                button.Text.alignment = page.Columns[i].AlignCenter ? TextAlignmentOptions.Center : page.Columns[i].AlignRight ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                button.Spot.Click = page.SortBy != null ? () => page.SortBy(column) : (Action)null;
                Ui.SetActive(button.Text, true);

                // The list is inset by the margin, and so are the headers above it.
                if (i == 0)
                {
                    Ui.PinToTop(button.Rect, margin + size * 0.4f, top, margin + RightOf(0) + charWidth, size * 1.3f);
                }
                else
                {
                    PinRight(button.Rect, margin + RightOf(i), top, widths[i], size * 1.3f);
                }
            }

            for (var i = columns; i < heads.Count; i++)
            {
                Ui.SetActive(heads[i].Text, false);
            }

            Ui.SetActive(headRule, columns > 0);
            Ui.PinToTop(headRule.rectTransform, margin, top + size * 1.35f, margin, 1f);

            return columns > 0 ? top + size * 1.55f : top;
        }

        // Every column is as wide as its longest value on the whole page, not only on the rows in view,
        // so nothing in it is cut off and the columns do not jump while scrolling. The name gets what is left.
        private void MeasureColumns()
        {
            var columns = page.Columns;
            if (widths.Length < columns.Count)
            {
                widths = new float[columns.Count];
            }

            var measured = new Dictionary<string, float>();

            for (var c = 1; c < columns.Count; c++)
            {
                var column = columns[c];
                var widest = Measure(column.Header, measured);

                foreach (var entry in page.Entries)
                {
                    if (entry.Cells != null && c - 1 < entry.Cells.Length)
                    {
                        widest = Mathf.Max(widest, Measure(entry.Cells[c - 1], measured));
                    }
                }

                // A letter of air between this column and the next.
                var width = Mathf.Max(column.Chars * charWidth, widest + charWidth);
                widths[c] = column.MaxChars > 0 ? Mathf.Min(width, column.MaxChars * charWidth) : width;
            }
        }

        private float Measure(string value, Dictionary<string, float> measured)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0f;
            }

            if (!measured.TryGetValue(value, out var width))
            {
                // Measured with one of our own texts, so it is the width the cell will really take.
                measured[value] = width = typed.GetPreferredValues(value).x;
            }

            return width;
        }

        // How far the right edge of a column is from the right edge of the list. For the name: where it has to end.
        private float RightOf(int column)
        {
            var right = size * 0.4f;
            for (var i = page.Columns.Count - 1; i > column; i--)
            {
                right += widths[i];
            }

            return right;
        }

        private void Fill(Row row, MenuEntry entry, float height, bool sectioned, bool shaded)
        {
            var colour = like.color;
            var columns = page.Columns;
            var cells = entry.IsHeader || entry.Cells == null ? 0 : Mathf.Min(MaxCells, columns.Count - 1);
            var indent = size * (sectioned && !entry.IsHeader ? 1.6f : 0.4f);

            row.Back.color = IsBar(entry) ? Faded(0.22f) : shaded && !entry.IsHeader ? Faded(0.05f) : Color.clear;

            row.Label.text = entry.Label;
            row.Label.fontSize = entry.Big ? size * 1.3f : size;
            row.Label.color = entry.Dim ? Faded(0.5f) : colour;
            row.Description.text = columns.Count == 0 ? entry.Description ?? "" : "";
            row.Spot.Click = ClickFor(entry);
            row.Clickable = row.Spot.Click != null;
            row.Spot.AltClick = entry.AltCommand != null ? () => Commands.Submit(terminal, entry.AltCommand) : (Action)null;
            row.Spot.WheelClick = entry.WheelCommand != null ? () => Commands.Submit(terminal, entry.WheelCommand) : (Action)null;

            // Moving a text makes it rebuild itself, so the pieces are only placed again when the row's shape changes.
            var reshape = row.Version != version || row.Indent != indent || row.CellCount != cells;
            row.Version = version;
            row.Indent = indent;
            row.CellCount = cells;

            if (reshape)
            {
                // Never lower than a line of the bigger font, or a heading on a tight page gets cut off.
                var labelHeight = columns.Count > 0 ? Mathf.Max(height, size * 1.4f) : size * 1.4f;
                Ui.PinToTop(row.Label.rectTransform, indent, columns.Count > 0 ? 0f : size * 0.1f,
                    cells > 0 ? RightOf(0) + charWidth : size * 0.4f, labelHeight);
                Ui.PinToTop(row.Description.rectTransform, indent + size * 1.2f, size * 1.3f, size * 0.4f, size * 1f);
            }

            Ui.SetActive(row.Marker, entry.Marker != Marker.None);
            if (entry.Marker != Marker.None)
            {
                PlaceMarker(row.Marker, entry.Marker, row.Label, entry.Label, indent, height);
            }

            var icons = 0;

            for (var i = 0; i < MaxCells; i++)
            {
                var cell = row.Cells[i];
                Ui.SetActive(cell, i < cells);
                row.CellSpots[i].Click = null;

                if (i >= cells)
                {
                    continue;
                }

                var column = columns[i + 1];
                var width = widths[i + 1];

                cell.text = entry.Cells != null && i < entry.Cells.Length ? entry.Cells[i] : "";
                cell.color = entry.Dim ? Faded(0.5f) : colour;

                if (reshape)
                {
                    cell.alignment = column.AlignCenter ? TextAlignmentOptions.Center : column.AlignRight ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                    PinRight(cell.rectTransform, RightOf(i + 1), 0f, width, height);
                }

                if (entry.CellClicks != null && i < entry.CellClicks.Length && cell.text.Length > 0)
                {
                    row.CellSpots[i].Click = entry.CellClicks[i];
                }

                if (page.IconColumn == i + 1)
                {
                    icons = Mathf.Min(MaxIcons, entry.Icons);
                    PlaceIcons(row, icons, RightOf(i + 1) + width, height, cell.color);
                }
            }

            for (var i = icons; i < MaxIcons; i++)
            {
                Ui.SetActive(row.Icons[i], false);
            }
        }

        // Side by side from the left edge of their column.
        private void PlaceIcons(Row row, int count, float columnLeft, float height, Color colour)
        {
            var tall = size * 0.95f;
            var wide = tall * row.Icons[0].texture.width / row.Icons[0].texture.height;

            for (var i = 0; i < count; i++)
            {
                var icon = row.Icons[i];
                icon.color = colour;
                Ui.SetActive(icon, true);
                PinRight(icon.rectTransform, columnLeft - (i + 1) * wide - i * size * 0.25f, (height - tall) / 2f, wide, tall);
            }
        }

        private Action ClickFor(MenuEntry entry)
        {
            // A section bar is only a button when it has something of its own to do, like folding the section.
            if ((entry.IsHeader && entry.Click == null) || (entry.Command == null && entry.Prefill == null && entry.Open == null && entry.Click == null))
            {
                return null;
            }

            if (entry.Click != null)
            {
                return entry.Click;
            }

            if (entry.Open != null)
            {
                return () => Navigator.Open(entry.Open);
            }

            if (entry.Prefill != null)
            {
                return () => Commands.Type(terminal, entry.Prefill);
            }

            return () => Commands.Submit(terminal, entry.Command);
        }

        private Row NewRow()
        {
            var holder = new GameObject("Row", typeof(RectTransform));
            holder.layer = list.gameObject.layer;

            var row = new Row { Rect = (RectTransform)holder.transform };
            row.Rect.SetParent(list, false);

            row.Back = Ui.NewBox("Back", row.Rect);
            Ui.Stretch(row.Back.rectTransform);

            row.Label = Ui.NewText("Label", row.Rect, like, size);

            // A note under the label: smaller, dimmer and stepped in, so it does not compete with it.
            row.Description = Ui.NewText("Description", row.Rect, like, size * 0.8f);
            row.Description.color = Faded(0.55f);

            for (var i = 0; i < MaxCells; i++)
            {
                row.Cells[i] = Ui.NewText("Cell", row.Rect, like, size);
                row.CellSpots[i] = new HotSpot();
            }

            for (var i = 0; i < MaxIcons; i++)
            {
                row.Icons[i] = NewIcon(row.Rect);
                row.Icons[i].texture = CursorArt.Hand();
            }

            row.Marker = NewIcon(row.Rect);

            return row;
        }

        // The marker sits right after the last letter of the text it belongs to.
        private void PlaceMarker(RawImage marker, Marker kind, TMP_Text text, string label, float left, float height)
        {
            var tall = size * 0.95f;
            marker.texture = kind == Marker.Here ? CursorArt.Pin() : kind == Marker.Locked ? CursorArt.Padlock() : CursorArt.Pouch();
            marker.color = text.color;
            PinLeft(marker.rectTransform, left + text.GetPreferredValues(label).x + size * 0.6f,
                (height - tall) / 2f, tall * marker.texture.width / marker.texture.height, tall);
        }

        private static RawImage NewIcon(RectTransform parent)
        {
            var holder = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
            holder.layer = parent.gameObject.layer;
            holder.transform.SetParent(parent, false);
            holder.SetActive(false);

            var icon = holder.GetComponent<RawImage>();
            icon.raycastTarget = false;
            return icon;
        }

        private Color Faded(float alpha)
        {
            return new Color(like.color.r, like.color.g, like.color.b, alpha);
        }

        private static void PinLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PinRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void Bottom(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(margin, size * 0.4f);
            rect.sizeDelta = new Vector2(-margin * 2f, size * 1.3f);
        }
    }
}
