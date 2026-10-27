using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // The middle of a page: the column heads and the rows under them, a track beside them when there are more rows than fit, or a picture instead of rows (the monitor's radar). Only the rows in view exist; scrolling hands them other entries.
    internal class ListPart
    {
        private const int MaxCells = 5;
        private const int MaxIcons = 2;

        private class Row
        {
            internal RectTransform Rect;
            internal Image Back;
            internal TextMeshProUGUI Label;
            internal TextMeshProUGUI Sub;
            internal TextMeshProUGUI Description;
            internal readonly TextMeshProUGUI[] Cells = new TextMeshProUGUI[MaxCells];
            internal readonly HotSpot[] CellSpots = new HotSpot[MaxCells];
            internal readonly RawImage[] Icons = new RawImage[MaxIcons];
            internal RawImage Marker;
            internal RectTransform PictureFrame;
            internal Image Picture;
            internal readonly HotSpot Spot = new HotSpot();
            internal bool Clickable;

            // What the row was last laid out for, so scrolling only moves what has to move.
            internal int Version = -1;
            internal float Indent;
            internal int CellCount;
            internal bool Marked;

            internal bool CanClick => Clickable || CellSpots.Any(spot => spot.Click != null);
        }

        private readonly MenuView view;
        private readonly Kit kit;
        private readonly RectTransform root;
        private readonly RectTransform area;
        private readonly RectTransform list;
        private readonly Image headRule;
        private readonly Image track;
        private readonly Image thumb;
        private readonly Image dialogBox;
        private readonly List<Button> heads = new List<Button>();
        private readonly List<Row> rows = new List<Row>();
        private RawImage picture;
        private readonly Grip grip;

        private MenuPage page;
        private Rect space;
        private float[] widths = new float[4];
        private float rowHeight;
        private float headsBottom;
        private float listHeight;
        private int first;
        private int visible;
        private int version;
        private bool sectioned;
        private bool table;
        private bool tall;
        private bool pictures;
        private bool wrapDescriptions;

        internal int Visible => visible;

        internal ListPart(MenuView view, Kit kit, RectTransform root)
        {
            this.view = view;
            this.kit = kit;
            this.root = root;

            dialogBox = kit.FramedBox("Dialog", root, 0.05f, 0.85f);
            Ui.SetActive(dialogBox, false);

            area = kit.Holder("Area", root);
            headRule = kit.Box("HeadRule", area, kit.Faded(0.6f));
            list = kit.Holder("List", area);
            track = kit.Box("Track", area, kit.Faded(0.25f));
            thumb = kit.Box("Thumb", area, kit.Faded(0.9f));
            grip = new Grip(track.rectTransform, thumb.rectTransform, kit.Size * 0.6f, along =>
            {
                var before = first;
                first = Mathf.RoundToInt(along * Mathf.Max(0, page.Entries.Count - visible));
                if (first != before)
                {
                    FillRows();
                }
            });
        }

        // A new page, or the same one drawn again: back to the top unless it is only a sort or a filter.
        internal void Reset()
        {
            first = 0;
        }

        internal void Layout(MenuPage shown, Rect content)
        {
            page = shown;
            var size = kit.Size;
            var entries = page.Entries;

            table = page.Columns.Count > 0;
            pictures = entries.Any(entry => entry.Picture != null);
            tall = table && (pictures || entries.Any(entry => !string.IsNullOrEmpty(entry.Sub)));
            var room = content.width - size * 2f;
            wrapDescriptions = !table && entries.Any(entry => !string.IsNullOrEmpty(entry.Description) && kit.Measure(entry.Description) * 0.9f > room);
            rowHeight = size * (tall ? 2.3f : page.Dense ? 1.3f : table ? 1.5f : wrapDescriptions ? 3.3f : page.HasDescriptions ? 2.5f : 1.6f);
            MeasureColumns();

            var headsHeight = !page.HideHeads && table ? size * 1.6f : 0f;
            var fits = Mathf.Max(1, Mathf.FloorToInt((content.height - headsHeight) / rowHeight));
            var scrolls = !page.Dialog && entries.Count > fits;

            // A list longer than the screen gets a track on its right, so the rows step aside for it.
            space = content;
            if (scrolls)
            {
                space.width -= size * 0.7f;
            }

            // A short page drawn as a framed box in the middle, like a question or a receipt.
            if (page.Dialog)
            {
                var side = (space.width - Mathf.Min(space.width - size * 1.6f, size * 24f)) / 2f;
                space = new Rect(space.x + side, space.y + size * 0.8f, space.width - side * 2f, space.height - size * 0.8f);
            }

            Kit.Place(area, space);

            for (var c = 1; c < page.Columns.Count; c++)
            {
                if (page.Columns[c].Share > 0f)
                {
                    widths[c] = space.width * page.Columns[c].Share;
                }
            }

            headsBottom = LayoutHeads();
            Kit.Place(list, 0f, headsBottom, space.width, space.height - headsBottom);

            sectioned = entries.Any(IsBar);
            listHeight = space.height - headsBottom;
            visible = page.Picture != null ? 0 : Mathf.Max(1, Mathf.FloorToInt(listHeight / rowHeight));
            version++;

            LayoutPicture();

            Ui.SetActive(track, scrolls);
            Ui.SetActive(thumb, scrolls);
            if (scrolls)
            {
                Kit.Place(track.rectTransform, space.width + size * 0.35f - kit.Thin * 2f, headsBottom, kit.Thin * 2f, listHeight);
            }

            Ui.SetActive(dialogBox, page.Dialog);
            if (page.Dialog)
            {
                var high = Mathf.Min(entries.Count, visible) * rowHeight + headsBottom;
                Kit.Place(dialogBox.rectTransform, space.x - size * 0.8f, space.y - size * 0.8f, space.width + size * 1.6f, high + size * 1.6f);
            }

            while (rows.Count < visible)
            {
                rows.Add(NewRow());
            }

            for (var i = 0; i < rows.Count; i++)
            {
                if (i < visible)
                {
                    Ui.PinToTop(rows[i].Rect, 0f, i * rowHeight, 0f, rowHeight);
                }
                else
                {
                    Ui.SetActive(rows[i].Rect, false);
                }
            }

            // The row the page asks for is picked, and scrolled to when it is out of view.
            if (page.Pick != null && entries.Contains(page.Pick))
            {
                view.Focused = page.Pick;
                var at = entries.IndexOf(page.Pick);
                if (at < first || at >= first + visible)
                {
                    first = Mathf.Max(0, at - 1);
                }
            }

            if (view.Focused == null || !entries.Contains(view.Focused))
            {
                var before = view.Focused;
                view.Focused = (before != null ? entries.FirstOrDefault(entry => !entry.IsHeader && entry.Label == before.Label) : null)
                               ?? entries.FirstOrDefault(entry => !entry.IsHeader && view.ClickFor(entry) != null);
            }
        }

        // The middle of the page is this picture instead of a list, as big as fits, in its shape.
        private void LayoutPicture()
        {
            if (page.Picture != null && picture == null)
            {
                picture = kit.Icon(list);
            }

            if (picture == null)
            {
                return;
            }

            Ui.SetActive(picture, page.Picture != null);
            if (page.Picture == null)
            {
                return;
            }

            picture.texture = page.Picture;
            picture.color = Color.white;
            var across = Mathf.Min(space.width, listHeight * page.PictureAspect);
            var down = across / page.PictureAspect;
            Kit.Place(picture.rectTransform, (space.width - across) / 2f, (listHeight - down) / 2f, across, down);
        }

        // The part that changes while scrolling: which entry each row shows. The rows themselves stay where they are.
        internal void FillRows()
        {
            var entries = page.Entries;
            var count = entries.Count;
            first = Mathf.Clamp(first, 0, Mathf.Max(0, count - visible));

            for (var i = 0; i < visible; i++)
            {
                var row = rows[i];
                var index = first + i;
                Ui.SetActive(row.Rect, index < count);
                if (index < count)
                {
                    Fill(row, entries[index], Shaded(index));
                }
            }

            view.Where(count > visible ? $"{first + 1}-{Mathf.Min(count, first + visible)} / {count}" : "");

            if (thumb.gameObject.activeSelf && count > visible)
            {
                var length = Mathf.Max(kit.Size, listHeight * visible / count);
                var at = (listHeight - length) * first / (count - visible);
                Kit.Place(thumb.rectTransform, space.width + kit.Size * 0.35f - kit.Thin * 3f, headsBottom + at, kit.Thin * 4f, length);
            }
        }

        // With section titles the rows under them step in. Without, every other row is shaded instead.
        private bool Shaded(int index)
        {
            return table && !sectioned && !page.Plain && index % 2 == 1;
        }

        internal bool Scroll(int by)
        {
            var before = first;
            first = Mathf.Clamp(first + by, 0, Mathf.Max(0, page.Entries.Count - visible));
            if (first != before)
            {
                FillRows();
            }

            return first != before;
        }

        // Stepping past the last row on screen with the arrows moves the list instead of the cursor.
        internal bool ScrollFrom(Vector3 world, bool down)
        {
            var onList = false;
            for (var i = 0; i < visible && i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.Rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var rect = row.Rect.rect;
                var height = row.Rect.InverseTransformPoint(world).y;
                if (height >= rect.yMin && height <= rect.yMax)
                {
                    onList = true;
                }
                else if (row.CanClick && (down ? height > rect.yMax : height < rect.yMin))
                {
                    return false;
                }
            }

            return onList && Scroll(down ? 1 : -1);
        }

        // Left and right on a list with no tabs or panel beside it: to the section before or after the one the cursor is in, or to the top and the bottom of a list that has no sections.
        internal bool Jump(Vector3 world, bool forward, RectTransform frame, out Vector2 stand)
        {
            stand = default;
            var at = -1;
            for (var i = 0; i < visible && i < rows.Count; i++)
            {
                if (MenuView.Under(rows[i].Rect, world))
                {
                    at = first + i;
                    break;
                }
            }

            var entries = page.Entries;
            if (at < 0 || entries.Count == 0)
            {
                return false;
            }

            var target = forward ? entries.Count - 1 : 0;
            if (forward)
            {
                for (var i = at + 1; i < entries.Count; i++)
                {
                    if (IsBar(entries[i]))
                    {
                        target = i;
                        break;
                    }
                }
            }
            else
            {
                // From the first row under a bar, back means the section before, not the bar just above.
                for (var i = at - 2; i >= 0; i--)
                {
                    if (IsBar(entries[i]))
                    {
                        target = i;
                        break;
                    }
                }
            }

            first = Mathf.Clamp(target, 0, Mathf.Max(0, entries.Count - visible));
            FillRows();

            for (var i = Mathf.Max(0, target - first); i < visible && i < rows.Count; i++)
            {
                if (rows[i].Rect.gameObject.activeInHierarchy && rows[i].CanClick)
                {
                    stand = RowPoint(frame, rows[i]);
                    return true;
                }
            }

            stand = frame.InverseTransformPoint(world);
            return true;
        }

        // Labels and cells that follow something live. Only the rows on screen are looked at.
        internal void Tick()
        {
            var entries = page.Entries;
            for (var i = 0; i < visible && i < rows.Count; i++)
            {
                var index = first + i;
                if (index >= entries.Count)
                {
                    continue;
                }

                var entry = entries[index];
                if (entry.LiveLabel != null)
                {
                    rows[i].Label.text = entry.LiveLabel();
                }

                if (entry.LiveCells == null)
                {
                    continue;
                }

                var cells = entry.LiveCells();
                if (entry.Cells != null && cells.SequenceEqual(entry.Cells))
                {
                    continue;
                }

                entry.Cells = cells;
                Fill(rows[i], entry, Shaded(index));
                if (entry == view.Focused)
                {
                    view.DrawPanels();
                }
            }
        }

        // The entry of the row a spot belongs to, for the panel that follows the cursor.
        internal MenuEntry EntryOf(HotSpot spot)
        {
            for (var i = 0; i < visible && i < rows.Count; i++)
            {
                if (rows[i].Spot == spot || rows[i].CellSpots.Contains(spot))
                {
                    return first + i < page.Entries.Count ? page.Entries[first + i] : null;
                }
            }

            return null;
        }

        internal HotSpot SpotAt(Vector3 world)
        {
            var held = grip.At(world);
            if (held != null)
            {
                return held;
            }

            foreach (var head in heads)
            {
                if (head.Spot.Click != null && MenuView.Under(head.Rect, world))
                {
                    return MenuView.At(head.Spot, head.Rect);
                }
            }

            foreach (var row in rows)
            {
                if (!MenuView.Under(row.Rect, world))
                {
                    continue;
                }

                for (var i = 0; i < MaxCells; i++)
                {
                    if (row.CellSpots[i].Click != null && MenuView.Under(row.Cells[i].rectTransform, world))
                    {
                        return MenuView.At(row.CellSpots[i], row.Cells[i].rectTransform);
                    }
                }

                if (!row.Clickable)
                {
                    return null;
                }

                // The whole row, also past a cell to click (the store's [+]): the cells were asked first.
                var clickable = row.Rect.rect;
                if (!clickable.Contains(row.Rect.InverseTransformPoint(world)))
                {
                    return null;
                }

                row.Spot.Space = row.Rect;
                row.Spot.Area = clickable;
                return row.Spot;
            }

            return null;
        }

        internal void Targets(RectTransform frame, Steering steering)
        {
            foreach (var head in heads)
            {
                if (head.Spot.Click != null && head.Shown)
                {
                    steering.Add(frame, head.Rect, Zone.List);
                }
            }

            foreach (var row in rows)
            {
                if (!row.Rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (row.Clickable)
                {
                    steering.Add(frame, row.Rect, RowLocal(row), Zone.List);
                }

                for (var i = 0; i < MaxCells; i++)
                {
                    if (row.CellSpots[i].Click != null)
                    {
                        steering.Add(frame, row.Cells[i].rectTransform, Zone.List);
                    }
                }
            }

            steering.Area(frame, root, MenuView.ToLocal(root, space), Zone.List);
        }

        // A row is stood on near its left end, past its picture.
        private Vector2 RowLocal(Row row)
        {
            var rect = row.Rect.rect;
            return new Vector2(rect.xMin + Mathf.Min(rect.width * 0.12f, kit.Size * 4f) + (pictures ? rowHeight : 0f), rect.center.y);
        }

        private Vector2 RowPoint(RectTransform frame, Row row)
        {
            return frame.InverseTransformPoint(row.Rect.TransformPoint(RowLocal(row)));
        }

        private static bool IsBar(MenuEntry entry)
        {
            return entry.IsHeader && !string.IsNullOrEmpty(entry.Label);
        }

        private float LayoutHeads()
        {
            var size = kit.Size;
            var columns = page.HideHeads ? 0 : page.Columns.Count;

            for (var i = 0; i < columns; i++)
            {
                if (i == heads.Count)
                {
                    var text = kit.Text("Head", area, size * 0.9f);
                    text.color = kit.Faded(0.85f);
                    heads.Add(new Button { Text = text });
                }

                var column = i;
                var head = heads[i];
                head.Text.text = page.Columns[i].Header;
                head.Text.alignment = page.Columns[i].AlignRight ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                head.Spot.Click = page.SortBy != null ? () => page.SortBy(column) : (Action)null;
                Ui.SetActive(head.Text, true);

                if (i == 0)
                {
                    Kit.Place(head.Rect, size * 0.4f, 0f, space.width - size * 0.4f - RightOf(0) - kit.CharWidth, size * 1.3f);
                }
                else
                {
                    Kit.Place(head.Rect, space.width - RightOf(i) - widths[i], 0f, widths[i], size * 1.3f);
                }
            }

            for (var i = columns; i < heads.Count; i++)
            {
                Ui.SetActive(heads[i].Text, false);
                heads[i].Spot.Click = null;
            }

            Ui.SetActive(headRule, columns > 0);
            Kit.Place(headRule.rectTransform, 0f, size * 1.35f, space.width, kit.Thin);
            return columns > 0 ? size * 1.6f : 0f;
        }

        // Every column is as wide as its longest value on the whole page, so the columns do not jump while scrolling.
        private void MeasureColumns()
        {
            var columns = page.Columns;
            if (widths.Length < columns.Count)
            {
                widths = new float[columns.Count];
            }

            var measured = new Dictionary<string, float>();
            float Measure(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0f;
                }

                if (!measured.TryGetValue(value, out var width))
                {
                    measured[value] = width = kit.Width(value);
                }

                return width;
            }

            for (var c = 1; c < columns.Count; c++)
            {
                var column = columns[c];
                var widest = Measure(column.Header);
                foreach (var entry in page.Entries)
                {
                    if (entry.Cells != null && c - 1 < entry.Cells.Length)
                    {
                        widest = Mathf.Max(widest, Measure(entry.Cells[c - 1]));
                    }
                }

                var width = Mathf.Max(column.Chars * kit.CharWidth, widest + kit.CharWidth);
                widths[c] = column.MaxChars > 0 ? Mathf.Min(width, column.MaxChars * kit.CharWidth) : width;
            }
        }

        // How far the right edge of a column is from the right edge of the list.
        private float RightOf(int column)
        {
            var right = kit.Size * 0.4f;
            for (var i = page.Columns.Count - 1; i > column; i--)
            {
                right += widths[i];
            }

            return right;
        }

        private void Fill(Row row, MenuEntry entry, bool shaded)
        {
            var size = kit.Size;
            var height = rowHeight;
            var colour = kit.Like.color;
            var columns = page.Columns;
            var cells = entry.IsHeader || entry.Cells == null ? 0 : Mathf.Min(MaxCells, columns.Count - 1);
            var hasPicture = pictures && !entry.IsHeader;
            var indent = size * (sectioned && !entry.IsHeader ? 1.6f : 0.4f) + (hasPicture ? height * 0.95f : 0f);
            var withSub = tall && !entry.IsHeader && !string.IsNullOrEmpty(entry.Sub);

            var picked = page.SelectRows && entry == view.Focused;
            row.Back.color = picked ? kit.Faded(0.3f) : IsBar(entry) ? kit.Faded(0.22f) : shaded && !entry.IsHeader ? kit.Faded(0.05f) : Color.clear;

            row.Label.text = entry.Label;
            row.Label.fontSize = entry.Big ? size * 1.3f : size;
            row.Label.color = entry.Dim ? kit.Faded(0.5f) : colour;
            row.Sub.text = withSub ? entry.Sub : "";
            row.Description.text = columns.Count == 0 ? entry.Description ?? "" : "";

            var click = view.ClickFor(entry);
            row.Spot.Click = page.SelectRows && !entry.IsHeader && click != null ? () => view.Select(entry) : click;
            row.Clickable = row.Spot.Click != null;
            row.Spot.AltClick = entry.AltClick;

            Ui.SetActive(row.PictureFrame, hasPicture);
            if (hasPicture)
            {
                var box = height * 0.8f;
                Kit.Place(row.PictureFrame, size * 0.3f, (height - box) / 2f, box, box);
                Ui.SetActive(row.Picture, entry.Picture != null);
                row.Picture.sprite = entry.Picture;
                row.Picture.color = entry.Dim ? kit.Faded(0.5f) : colour;
            }

            // Moving a text makes it rebuild itself, so the pieces are only placed again when the row's shape changes.
            var marked = entry.Marker != Marker.None;
            var reshape = row.Version != version || row.Indent != indent || row.CellCount != cells || row.Marked != marked;
            row.Version = version;
            row.Indent = indent;
            row.CellCount = cells;
            row.Marked = marked;

            if (reshape)
            {
                var right = cells > 0 ? RightOf(0) + kit.CharWidth : size * 0.4f;

                // A pin or a padlock after the name gets its room before the next column: the name is cut sooner.
                if (marked)
                {
                    right += size * 1.9f;
                }
                if (tall)
                {
                    Ui.PinToTop(row.Label.rectTransform, indent, height * 0.1f, right, size * 1.3f);
                    Ui.PinToTop(row.Sub.rectTransform, indent + size * 0.1f, height * 0.1f + size * 1.25f, right, size * 0.9f);
                }
                else
                {
                    // Never lower than a line of the bigger font, or a heading on a tight page gets cut off.
                    var labelHeight = columns.Count > 0 ? Mathf.Max(height, size * 1.4f) : size * 1.4f;
                    Ui.PinToTop(row.Label.rectTransform, indent, columns.Count > 0 ? 0f : size * 0.1f, right, labelHeight);
                }

                row.Description.enableWordWrapping = wrapDescriptions;
                Ui.PinToTop(row.Description.rectTransform, indent + size * 1.2f, size * 1.3f, size * 0.4f, size * (wrapDescriptions ? 1.9f : 1f));
            }

            Ui.SetActive(row.Marker, entry.Marker != Marker.None);
            if (entry.Marker != Marker.None)
            {
                kit.Marker(row.Marker, entry.Marker, row.Label, entry.Label, indent, tall ? size * 1.5f : height, row.Label.rectTransform.rect.width);
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
                cell.color = entry.Dim ? kit.Faded(0.5f) : colour;

                if (reshape)
                {
                    cell.alignment = column.AlignRight ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                    Kit.PlaceRight(cell.rectTransform, RightOf(i + 1), 0f, width, height);
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
            var high = kit.Size * 0.95f;
            var wide = high * row.Icons[0].texture.width / row.Icons[0].texture.height;
            for (var i = 0; i < count; i++)
            {
                var icon = row.Icons[i];
                icon.color = colour;
                Ui.SetActive(icon, true);
                Kit.PlaceRight(icon.rectTransform, columnLeft - (i + 1) * wide - i * kit.Size * 0.25f, (height - high) / 2f, wide, high);
            }
        }

        private Row NewRow()
        {
            var size = kit.Size;
            var row = new Row { Rect = kit.Holder("Row", list) };
            row.Back = kit.Box("Back", row.Rect, Color.clear);
            Ui.Stretch(row.Back.rectTransform);

            row.PictureFrame = kit.Holder("Holder", row.Rect);
            kit.Outline(row.PictureFrame, kit.Thin, kit.Faded(0.6f));
            row.Picture = kit.Picture(row.PictureFrame);
            Ui.Stretch(row.Picture.rectTransform);
            row.Picture.rectTransform.offsetMin = new Vector2(size * 0.2f, size * 0.2f);
            row.Picture.rectTransform.offsetMax = new Vector2(-size * 0.2f, -size * 0.2f);
            Ui.SetActive(row.PictureFrame, false);

            row.Label = kit.Text("Label", row.Rect, size);
            row.Sub = kit.Text("Sub", row.Rect, size * 0.75f);
            row.Sub.color = kit.Faded(0.7f);

            // A note under the label: smaller, dimmer and stepped in, so it does not compete with it.
            row.Description = kit.Text("Description", row.Rect, size * 0.8f);
            row.Description.color = kit.Faded(0.6f);

            for (var i = 0; i < MaxCells; i++)
            {
                row.Cells[i] = kit.Text("Cell", row.Rect, size);
                row.CellSpots[i] = new HotSpot();
            }

            for (var i = 0; i < MaxIcons; i++)
            {
                row.Icons[i] = kit.Icon(row.Rect);
                row.Icons[i].texture = CursorArt.Hand();
            }

            row.Marker = kit.Icon(row.Rect);
            return row;
        }
    }
}
