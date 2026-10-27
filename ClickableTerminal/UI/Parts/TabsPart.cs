using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // The tabs down the left side of a page: the open one filled, the others plain. They keep the same letter size on every page, and when there are more than fit they scroll, with the rule beside them as their track.
    internal class TabsPart
    {
        private readonly MenuView view;
        private readonly Kit kit;
        private readonly RectTransform root;
        private readonly Buttons tabs;
        private readonly Image rule;
        private readonly Image thumb;
        private List<MenuEntry> bar = new List<MenuEntry>();
        private Rect area;
        private int first;
        private int visible;
        private readonly Grip grip;

        internal float Font => kit.Head * 0.55f;
        internal bool Shown { get; private set; }
        internal float Width { get; private set; }

        internal TabsPart(MenuView view, Kit kit, RectTransform root)
        {
            this.view = view;
            this.kit = kit;
            this.root = root;
            tabs = new Buttons(kit, root, "Tab", Font);
            rule = kit.Box("SideRule", root, kit.Faded(0.5f));
            thumb = kit.Box("SideThumb", root, kit.Faded(0.9f));
            grip = new Grip(rule.rectTransform, thumb.rectTransform, kit.Size * 0.4f, along =>
            {
                var before = first;
                first = Mathf.RoundToInt(along * Mathf.Max(0, bar.Count - visible));
                if (first != before)
                {
                    Layout(view.Page, area, false);
                }
            });
        }

        // Whether the page gets a side bar at this width, and how wide: as its longest tab needs, within limits.
        internal void Measure(MenuPage page, float width)
        {
            Shown = page.Sidebar != null && page.Sidebar.Count > 0 && width >= kit.Size * 34f;
            Width = 0f;
            if (!Shown)
            {
                return;
            }

            var widest = 0f;
            foreach (var entry in page.Sidebar)
            {
                widest = Mathf.Max(widest, kit.Width(entry.Label) * Font / kit.Size);
            }

            Width = Mathf.Clamp(widest + Font * 1.6f, Font * 6f, Font * 11f);
        }

        internal void Layout(MenuPage page, Rect space, bool showOpen = true)
        {
            bar = Shown ? page.Sidebar : new List<MenuEntry>();
            area = space;
            var font = Font;
            var high = font * 2f;
            var step = high + font * 0.3f;

            visible = Mathf.Max(1, Mathf.FloorToInt((space.height + kit.Size * 0.2f) / step));
            var active = bar.FindIndex(entry => entry.Active);
            if (showOpen && active >= 0 && (active < first || active >= first + visible))
            {
                first = active < first ? active : active - visible + 1;
            }

            first = Mathf.Clamp(first, 0, Mathf.Max(0, bar.Count - visible));

            tabs.Start();
            for (var i = first; i < bar.Count && i < first + visible; i++)
            {
                var entry = bar[i];
                var button = tabs.Next();
                var hasPicture = entry.Picture != null;

                button.Text.text = entry.Label;
                button.Text.alignment = TMPro.TextAlignmentOptions.Left;
                button.Text.margin = new Vector4(hasPicture ? font * 2.4f : font * 0.7f, 0f, 0f, 0f);
                button.Text.color = entry.Dim ? kit.Faded(0.5f) : kit.Like.color;
                button.Box.color = entry.Active ? kit.Faded(0.28f) : Color.clear;
                Ui.TintOutline(button.Frame, entry.Active ? kit.Faded(0.9f) : Color.clear);
                button.Spot.Click = view.ClickFor(entry);
                Ui.SetActive(button.Box, true);
                Kit.Place(button.Rect, space.x, space.y + (i - first) * step, Width, high);

                Ui.SetActive(button.Picture, hasPicture);
                if (hasPicture)
                {
                    button.Picture.sprite = entry.Picture;
                    button.Picture.color = kit.Like.color;
                    Kit.Place(button.Picture.rectTransform, kit.Size * 0.4f, kit.Size * 0.25f, kit.Size * 1.2f, kit.Size * 1.2f);
                }
            }

            tabs.Done();

            Ui.SetActive(rule, Shown);
            var scrolls = Shown && bar.Count > visible;
            Ui.SetActive(thumb, scrolls);
            if (!Shown)
            {
                return;
            }

            var ruleX = space.x + Width + kit.Size * 0.35f;
            Kit.Place(rule.rectTransform, ruleX, space.y, kit.Thin, space.height);
            if (scrolls)
            {
                var length = Mathf.Max(kit.Size, space.height * visible / bar.Count);
                Kit.Place(thumb.rectTransform, ruleX - kit.Thin * 1.5f, space.y + (space.height - length) * first / (bar.Count - visible), kit.Thin * 4f, length);
            }
        }

        // The wheel over the tabs moves them instead of the list.
        internal bool Scroll(Vector3 world, int by)
        {
            if (!Shown || bar.Count <= visible)
            {
                return false;
            }

            var local = root.InverseTransformPoint(world);
            var x = local.x - root.rect.xMin;
            var y = root.rect.yMax - local.y;
            if (x > area.x + Width + kit.Size * 0.35f || y < area.y)
            {
                return false;
            }

            var before = first;
            first = Mathf.Clamp(first + System.Math.Sign(by), 0, bar.Count - visible);
            if (first != before)
            {
                Layout(view.Page, area, false);
            }

            return true;
        }

        // The tab after or before the open one, for the shoulder buttons.
        internal bool Switch(MenuPage page, bool next)
        {
            var list = page != null ? page.Sidebar : null;
            if (list == null || list.Count == 0)
            {
                return false;
            }

            var at = list.FindIndex(entry => entry.Active);
            for (var step = 1; step <= list.Count; step++)
            {
                var index = ((at < 0 ? 0 : at) + (next ? step : -step) + list.Count * 2) % list.Count;
                var click = view.ClickFor(list[index]);
                if (click != null)
                {
                    click();
                    return true;
                }
            }

            return false;
        }

        internal HotSpot SpotAt(Vector3 world)
        {
            return grip.At(world) ?? MenuView.SpotAt(tabs, world);
        }

        internal void Targets(RectTransform frame, Steering steering)
        {
            MenuView.Targets(frame, tabs, Zone.Tabs, steering);
            if (Shown)
            {
                steering.Area(frame, root, MenuView.ToLocal(root, new Rect(area.x, area.y, Width, area.height)), Zone.Tabs);
            }
        }
    }
}
