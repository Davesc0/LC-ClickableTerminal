using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // Draws one of our pages over the terminal's screen, for one letter size. The page is cut into parts from the outside in: the header on top, the footer at the bottom, the bar under the header, the tabs on the left, the panels on the right, and the list in what is left. A window, when one is open, goes over all of it.
    internal class MenuView
    {
        private readonly Terminal terminal;
        private readonly RectTransform root;
        private readonly Kit kit;
        private readonly HeaderPart header;
        private readonly BarPart bar;
        private readonly TabsPart tabs;
        private readonly ListPart list;
        private readonly PanelPart detail;
        private readonly PanelPart extra;
        private readonly FooterPart footer;
        private readonly WindowPart window;

        private Vector2 laidOutFor;
        private Rect panelsArea;
        private bool panelsShown;
        private float nextRender;
        private float nextPanels;
        private static bool brokenCamera;

        internal MenuPage Page { get; private set; }

        // The row the panel beside the list is about: picked with a click, or under the cursor.
        internal MenuEntry Focused;

        internal float Scale { get; }

        internal MenuView(Terminal terminal, RectTransform frame, TMP_Text like, float scale)
        {
            this.terminal = terminal;
            Scale = scale;

            var holder = new GameObject("ClickableTerminalPage", typeof(RectTransform), typeof(Image));
            holder.layer = frame.gameObject.layer;
            root = (RectTransform)holder.transform;
            root.SetParent(frame, false);
            Ui.Stretch(root);
            root.SetAsFirstSibling();

            // No backdrop: the monitor's black shows through, so the page has no edge.
            var screen = holder.GetComponent<Image>();
            screen.color = Color.clear;
            screen.raycastTarget = false;

            kit = new Kit(like, scale, root);
            header = new HeaderPart(kit, terminal, root);
            bar = new BarPart(this, kit, root);
            list = new ListPart(this, kit, root);
            tabs = new TabsPart(this, kit, root);
            detail = new PanelPart(kit, root, "Detail");
            extra = new PanelPart(kit, root, "Extra");
            footer = new FooterPart(kit, root);

            // The window goes on last, so it covers the page.
            window = new WindowPart(kit, terminal, root);
            Ui.SetActive(root, false);
        }

        internal bool WindowOpen => Page != null && window.Open;

        internal TMP_Text WindowText => WindowOpen ? window.Text : null;

        internal int VisibleRows => list.Visible;

        internal void Show(MenuPage shown)
        {
            if (shown == null)
            {
                Ui.SetActive(root, false);
                header.ShowGameCredits(true);
                Page = null;
                return;
            }

            Ui.SetActive(root, true);
            header.ShowGameCredits(false);

            if (shown == Page)
            {
                return;
            }

            // A sort or filter click hands over a new page with the same title and tab: the place in the list is kept.
            if (Page == null || Page.Title != shown.Title || Tab(Page) != Tab(shown))
            {
                list.Reset();
                Focused = null;
                Window.Drop();
            }

            Page = shown;
            Layout();
        }

        private static string Tab(MenuPage page)
        {
            return page.Sidebar?.FirstOrDefault(entry => entry.Active)?.Label;
        }

        private void Layout()
        {
            var width = root.rect.width;
            var height = root.rect.height;
            var size = kit.Size;
            laidOutFor = root.rect.size;

            header.Layout(Page, width);
            tabs.Measure(Page, width);
            panelsShown = Page.DetailFor != null || Page.Extra != null;

            var top = bar.Layout(Page, new Rect(0f, header.Bottom, width, 0f), tabs.Shown, panelsShown);
            var content = new Rect(kit.Margin, top, width - kit.Margin * 2f, height - top - footer.Height);

            if (tabs.Shown)
            {
                tabs.Layout(Page, Cut.Left(ref content, tabs.Width));
                Cut.Left(ref content, size * 0.8f);
            }
            else
            {
                tabs.Layout(Page, new Rect(content.x, content.y, 0f, content.height));
            }

            panelsArea = panelsShown ? Cut.Right(ref content, size * 10.4f) : default;
            if (panelsShown && bar.PanelShift > 0f)
            {
                panelsArea = new Rect(panelsArea.x, panelsArea.y + bar.PanelShift, panelsArea.width, panelsArea.height - bar.PanelShift);
            }
            list.Layout(Page, content);
            LayoutPanels();
            list.FillRows();
            footer.SetHint(Page.Hint);

            // Pieces made just now would otherwise end up over an open window.
            window.Draw(WindowArea());
        }

        private void LayoutPanels()
        {
            if (!panelsShown)
            {
                detail.Hide();
                extra.Hide();
                return;
            }

            var size = kit.Size;
            var area = new Rect(panelsArea.xMax - size * 10f, panelsArea.y, size * 10f, panelsArea.height - size * 0.3f);
            var both = Page.DetailFor != null && Page.Extra != null;
            var detailHeight = area.height;
            if (both)
            {
                // The second panel gets what its lines and buttons need, the first one the rest: its picture and text shrink to fit.
                Detail second = null;
                try
                {
                    second = Page.Extra();
                }
                catch (Exception)
                {
                }

                var needed = extra.Needed(second, area.width);
                detailHeight = area.height - size * 0.4f - Mathf.Clamp(needed, area.height * 0.2f, area.height * 0.55f);
            }

            if (Page.DetailFor != null)
            {
                detail.Place(new Rect(area.x, area.y, area.width, detailHeight));
            }
            else
            {
                detail.Hide();
            }

            if (Page.Extra != null)
            {
                var y = Page.DetailFor != null ? area.y + detailHeight + size * 0.4f : area.y;
                extra.Place(new Rect(area.x, y, area.width, area.height - (y - area.y)));
            }
            else
            {
                extra.Hide();
            }

            panelsArea = area;
            DrawPanels();
        }

        internal void DrawPanels()
        {
            if (Page == null || !panelsShown)
            {
                return;
            }

            if (Page.DetailFor != null)
            {
                var focused = Focused;
                detail.Draw(() => focused != null ? Page.DetailFor(focused) : null);
            }

            if (Page.Extra != null)
            {
                extra.Draw(Page.Extra);
            }
        }

        // From under the header to above the footer, where a window is centred.
        private Rect WindowArea()
        {
            return new Rect(0f, kit.Head * 3f, root.rect.width, root.rect.height - kit.Head * 3f - kit.Size * 2.6f);
        }

        internal void Tick()
        {
            if (Page == null)
            {
                return;
            }

            // The screen's size can settle after the page was first laid out.
            if (root.rect.size != laidOutFor)
            {
                Layout();
            }

            window.Tick(WindowArea());
            header.Tick();
            footer.Tick();
            list.Tick();

            var live = Page.Camera;
            if (live != null && !live.isActiveAndEnabled && Time.unscaledTime >= nextRender && !brokenCamera)
            {
                nextRender = Time.unscaledTime + 1f / 15f;
                try
                {
                    live.Render();
                }
                catch (Exception e)
                {
                    // Once is enough to know it does not work. The picture then shows what the game last drew.
                    Plugin.WarnOnce($"The monitor's camera could not be drawn for the terminal: {e.Message}");
                    brokenCamera = true;
                }
            }

            if (Page.LivePanels && Time.unscaledTime >= nextPanels)
            {
                nextPanels = Time.unscaledTime + 0.5f;
                DrawPanels();
            }
        }

        internal void SetTyped(string text)
        {
            footer.SetTyped(text);
        }

        internal void Where(string text)
        {
            footer.SetWhere(text);
        }

        internal bool Scroll(int by)
        {
            if (Page == null)
            {
                return false;
            }

            return window.Open ? window.Scroll(by) : list.Scroll(by);
        }

        internal bool ScrollFrom(Vector3 world, bool down)
        {
            if (Page == null)
            {
                return false;
            }

            return window.Open ? window.ScrollFrom(down) : list.ScrollFrom(world, down);
        }

        internal bool ScrollTabs(Vector3 world, int by)
        {
            return Page != null && !window.Open && tabs.Scroll(world, by);
        }

        internal bool Jump(Vector3 world, bool forward, RectTransform frame, out Vector2 stand)
        {
            stand = default;
            return Page != null && !window.Open && list.Jump(world, forward, frame, out stand);
        }

        internal bool SwitchTab(bool next)
        {
            return Page != null && !window.Open && tabs.Switch(Page, next);
        }

        // BACK closes the window before it leaves the page. An application's window closes only with the application.
        internal bool CloseWindow()
        {
            if (!WindowOpen || window.IsApp)
            {
                return false;
            }

            Window.Close();
            return true;
        }

        internal bool InWindowView(Vector3 world)
        {
            return WindowOpen && window.InView(world);
        }

        internal string AppScreen()
        {
            return window.Screen();
        }

        // The cursor is on this spot: the panel shows the row it belongs to, unless rows are picked with a click.
        internal void Focus(HotSpot spot)
        {
            if (Page == null || spot == null || Page.DetailFor == null || !panelsShown || Page.SelectRows || window.Open)
            {
                return;
            }

            var entry = list.EntryOf(spot);
            if (entry != null && entry != Focused)
            {
                Focused = entry;
                DrawPanels();
            }
        }

        internal void Select(MenuEntry entry)
        {
            Focused = entry;
            list.FillRows();
            DrawPanels();
        }

        internal HotSpot SpotAt(Vector3 world)
        {
            if (Page == null)
            {
                return null;
            }

            if (window.Open)
            {
                return window.SpotAt(world);
            }

            return bar.SpotAt(world) ?? tabs.SpotAt(world) ?? detail.SpotAt(world) ?? extra.SpotAt(world) ?? list.SpotAt(world);
        }

        internal void Targets(RectTransform frame, Steering steering)
        {
            if (Page == null)
            {
                return;
            }

            if (window.Open)
            {
                window.Targets(frame, steering);
                return;
            }

            bar.Targets(frame, root, steering);
            tabs.Targets(frame, steering);
            detail.Targets(frame, root, steering);
            extra.Targets(frame, root, steering);
            if (panelsShown)
            {
                steering.Area(frame, root, ToLocal(root, panelsArea), Zone.Panel);
            }

            list.Targets(frame, steering);
        }

        internal Action ClickFor(MenuEntry entry)
        {
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

        // A rectangle measured from the top left corner of the page, in the page's own coordinates.
        internal static Rect ToLocal(RectTransform root, Rect area)
        {
            var whole = root.rect;
            return Rect.MinMaxRect(whole.xMin + area.xMin, whole.yMax - area.yMax, whole.xMin + area.xMax, whole.yMax - area.yMin);
        }

        internal static bool Under(RectTransform rect, Vector3 world)
        {
            return rect.gameObject.activeInHierarchy && rect.rect.Contains(rect.InverseTransformPoint(world));
        }

        internal static HotSpot At(HotSpot spot, RectTransform rect)
        {
            spot.Space = rect;
            spot.Area = rect.rect;
            return spot;
        }

        internal static HotSpot SpotAt(Buttons buttons, Vector3 world)
        {
            for (var i = 0; i < buttons.Used; i++)
            {
                var button = buttons.All[i];
                if (button.Spot.Click != null && Under(button.Rect, world))
                {
                    return At(button.Spot, button.Rect);
                }
            }

            return null;
        }

        internal static void Targets(RectTransform frame, Buttons buttons, Zone zone, Steering steering)
        {
            for (var i = 0; i < buttons.Used; i++)
            {
                var button = buttons.All[i];
                if (button.Spot.Click != null && button.Shown)
                {
                    steering.Add(frame, button.Rect, zone);
                }
            }
        }
    }
}
