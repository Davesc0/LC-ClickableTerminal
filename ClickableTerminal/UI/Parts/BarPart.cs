using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // Everything between the header and the list: the page's tool buttons (and its tabs where the screen is too narrow for a side bar), the summary line in its frame or the one flat button that takes its place, the corner button on the summary's line, and the pinned buttons under it.
    internal class BarPart
    {
        private readonly MenuView view;
        private readonly Kit kit;
        private readonly Buttons tools;
        private readonly Buttons pinned;
        private readonly Image summaryBox;
        private readonly TextMeshProUGUI summary;
        private readonly Buttons corners;
        private Rect area;

        // How far the panels start below the bar: a corner button with nothing else on its line sits over the panels only, so the tabs and the list keep that line.
        internal float PanelShift { get; private set; }

        internal BarPart(MenuView view, Kit kit, RectTransform root)
        {
            this.view = view;
            this.kit = kit;
            tools = new Buttons(kit, root, "Tool", kit.Size * 0.85f);
            pinned = new Buttons(kit, root, "Pinned", kit.Size);

            summaryBox = kit.FramedBox("Summary", root, 0.05f, 0.8f);
            summary = kit.Text("Text", summaryBox.rectTransform, kit.Size);
            Ui.Stretch(summary.rectTransform);
            summary.margin = new Vector4(kit.Size * 0.5f, 0f, kit.Size * 0.5f, 0f);

            corners = new Buttons(kit, root, "Corner", kit.Size * 0.9f);
        }

        // Lays the bar out from the top of the given area and returns how far down it reaches.
        internal float Layout(MenuPage page, Rect space, bool sideShown, bool panelsShown)
        {
            var size = kit.Size;
            var margin = kit.Margin;
            var width = space.width;
            var top = space.y;

            tools.Start();
            var shown = new List<ToolButton>();
            if (page.Sidebar != null && !sideShown)
            {
                foreach (var entry in page.Sidebar)
                {
                    shown.Add(new ToolButton { Label = entry.Label, Active = entry.Active, Click = view.ClickFor(entry) });
                }
            }

            shown.AddRange(page.Tools);

            var x = margin;
            foreach (var tool in shown)
            {
                var button = tools.Next();
                var wide = button.Text.GetPreferredValues(tool.Label).x + size;
                if (x > margin && x + wide > width - margin)
                {
                    x = margin;
                    top += size * 1.7f;
                }

                button.Text.text = tool.Label;
                button.Text.color = tool.Off ? kit.Faded(0.45f) : kit.Like.color;
                button.Box.color = kit.Faded(tool.Active ? 0.3f : 0.04f);
                Ui.TintOutline(button.Frame, kit.Faded(tool.Off ? 0.35f : 0.85f));
                button.Spot.Click = tool.Click;
                Ui.SetActive(button.Box, true);
                Kit.Place(button.Rect, x, top, wide, size * 1.45f);
                x += wide + size * 0.5f;
            }

            tools.Done();
            if (shown.Count > 0)
            {
                top += size * 1.9f;
            }

            // A lone flat button takes the summary's place: the whole line is the button.
            var line = page.Pinned.Count == 1 && page.Pinned[0].Flat ? page.Pinned[0] : null;
            var text = line != null ? line.Label : page.Summary;
            var hasSummary = !string.IsNullOrEmpty(text);
            var hasCorner = page.Corners.Count > 0;
            var right = panelsShown ? margin + size * 10.4f : margin;

            Ui.SetActive(summaryBox, hasSummary);
            corners.Start();
            if (hasCorner)
            {
                // Over the panels they share the panels' width; without panels each gets a width of its own.
                var count = page.Corners.Count;
                var whole = panelsShown ? size * 10f : size * 7f * count + size * 0.4f * (count - 1);
                var each = (whole - size * 0.4f * (count - 1)) / count;
                var cornerX = width - margin - whole;
                foreach (var tool in page.Corners)
                {
                    var button = corners.Next();
                    kit.Style(button, tool);
                    Kit.Place(button.Rect, cornerX, top, each, size * 1.5f);
                    cornerX += each + size * 0.4f;
                }

                if (!panelsShown)
                {
                    right = margin + whole + size * 0.4f;
                }
            }

            corners.Done();

            var summaryArea = new Rect(margin, top, width - margin - right, size * 1.5f);
            if (hasSummary)
            {
                summary.text = text;
                Kit.Place(summaryBox.rectTransform, summaryArea);
            }

            PanelShift = 0f;
            if (hasCorner && !hasSummary && panelsShown && page.Pinned.Count == 0)
            {
                PanelShift = size * 1.9f;
            }
            else if (hasSummary || hasCorner)
            {
                top += size * 1.9f;
            }

            pinned.Start();
            if (line != null)
            {
                var button = pinned.Next();
                button.Text.text = "";
                button.Box.color = Color.clear;
                Ui.TintOutline(button.Frame, Color.clear);
                button.Spot.Click = view.ClickFor(line);
                button.Spot.AltClick = line.AltClick;
                Ui.SetActive(button.Box, true);
                Ui.SetActive(button.Marker, false);
                Kit.Place(button.Rect, summaryArea);
            }
            else
            {
                top = LayoutPinned(page, top, width);
            }

            pinned.Done();
            area = PanelShift > 0f ? new Rect(width - margin - size * 10.4f, space.y, size * 10.4f, size * 1.5f) : new Rect(0f, space.y, width, top - space.y);
            return top;
        }

        private float LayoutPinned(MenuPage page, float top, float width)
        {
            var size = kit.Size;
            var margin = kit.Margin;
            var x = margin;
            var high = size * 1.5f;
            var icon = size * 0.95f;

            foreach (var entry in page.Pinned)
            {
                var button = pinned.Next();
                var marked = entry.Marker != Marker.None;
                var wide = button.Text.GetPreferredValues(entry.Label).x + size * 1.2f + (marked ? icon + size * 0.3f : 0f);
                if (x > margin && x + wide > width - margin)
                {
                    x = margin;
                    top += size * 1.8f;
                }

                button.Text.text = entry.Label;
                button.Text.alignment = TextAlignmentOptions.Left;
                button.Text.margin = new Vector4(size * 0.6f, 0f, 0f, 0f);
                button.Text.color = entry.Dim ? kit.Faded(0.5f) : kit.Like.color;
                button.Box.color = kit.Faded(0.06f);
                Ui.TintOutline(button.Frame, kit.Faded(0.85f));
                button.Spot.Click = view.ClickFor(entry);
                button.Spot.AltClick = entry.AltClick;
                Ui.SetActive(button.Box, true);
                Kit.Place(button.Rect, x, top, wide, high);

                Ui.SetActive(button.Marker, marked);
                if (marked)
                {
                    kit.Marker(button.Marker, entry.Marker, button.Text, entry.Label, size * 0.6f, high);
                }

                x += wide + size * 0.5f;
            }

            return page.Pinned.Count > 0 ? top + size * 1.9f : top;
        }

        internal HotSpot SpotAt(Vector3 world)
        {
            return MenuView.SpotAt(tools, world) ?? MenuView.SpotAt(pinned, world) ?? MenuView.SpotAt(corners, world);
        }

        internal void Targets(RectTransform frame, RectTransform root, Steering steering)
        {
            MenuView.Targets(frame, tools, Zone.Bar, steering);
            MenuView.Targets(frame, pinned, Zone.Bar, steering);
            MenuView.Targets(frame, corners, Zone.Bar, steering);

            if (area.height > 0f)
            {
                steering.Area(frame, root, MenuView.ToLocal(root, area), Zone.Bar);
            }
        }
    }
}
