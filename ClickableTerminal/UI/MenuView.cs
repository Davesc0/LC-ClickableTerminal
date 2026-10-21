using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    internal class MenuView
    {
        private const int MaxCells = 5;
        private const int MaxIcons = 2;
        private const int MaxFacts = 8;

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

            internal int Version = -1;
            internal float Indent;
            internal int CellCount;
            internal readonly HotSpot Spot = new HotSpot();
            internal bool Clickable;
        }

        private class Button
        {
            internal Image Box;
            internal RectTransform Frame;
            internal TextMeshProUGUI Text;
            internal RawImage Marker;
            internal Image Picture;
            internal readonly HotSpot Spot = new HotSpot();

            internal RectTransform Rect => Box != null ? Box.rectTransform : Text.rectTransform;
        }

        private class Panel
        {
            internal Image Box;
            internal Image Picture;
            internal RawImage Texture;
            internal TextMeshProUGUI Missing;
            internal TextMeshProUGUI Title;
            internal TextMeshProUGUI Text;
            internal Image Rule;
            internal readonly TextMeshProUGUI[] FactLabels = new TextMeshProUGUI[MaxFacts];
            internal readonly TextMeshProUGUI[] FactValues = new TextMeshProUGUI[MaxFacts];
            internal readonly List<Button> Buttons = new List<Button>();
            internal readonly List<Button> GroupButtons = new List<Button>();
            internal readonly List<TextMeshProUGUI> Headings = new List<TextMeshProUGUI>();
            internal TextMeshProUGUI Foot;
        }

        private readonly Terminal terminal;
        private static readonly Vector3[] Corners = new Vector3[4];
        private readonly TMP_Text like;
        private readonly float size;
        private readonly float head;
        private readonly float margin;
        private readonly float charWidth;
        private readonly RectTransform root;
        private readonly RectTransform area;
        private readonly RectTransform list;
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI subtitle;
        private readonly TextMeshProUGUI clock;
        private readonly Image creditsBox;
        private readonly TextMeshProUGUI credits;
        private readonly Image summaryBox;
        private Button corner;
        private readonly TextMeshProUGUI summary;
        private readonly List<Button> pinned = new List<Button>();
        private readonly Image headRule;
        private readonly Image dialogBox;
        private readonly Image footRule;
        private readonly Image sideRule;
        private readonly TextMeshProUGUI typed;
        private readonly TextMeshProUGUI position;
        private readonly List<Row> rows = new List<Row>();
        private RawImage pagePicture;
        private readonly List<Button> tools = new List<Button>();
        private readonly List<Button> heads = new List<Button>();
        private readonly List<Button> tabs = new List<Button>();
        private readonly Panel detail;
        private readonly Panel extra;

        private readonly Image shade;
        private readonly Image winBox;
        private readonly TextMeshProUGUI winTitle;
        private readonly Image winRule;
        private readonly Button winClose;
        private readonly RectTransform winView;
        private readonly TextMeshProUGUI winText;
        private readonly List<Button> winButtons = new List<Button>();
        private Window shownWindow;
        private float winScroll;
        private float winMax;

        private MenuPage page;
        private string where = "";
        private string[] hintFor;
        private TMP_SpriteAsset hintIcons;
        private int first;
        private int visibleRows;
        private float[] widths = new float[4];
        private float rowHeight;
        private bool sectioned;
        private bool table;
        private bool tall;
        private bool pictures;
        private int version;
        private int shownCredits = int.MinValue;
        private MenuEntry focused;
        private bool sideShown;
        private bool panelsShown;

        private Graphic gameCreditsBox;
        private bool gameCreditsHidden;

        internal MenuView(Terminal terminal, RectTransform frame, TMP_Text like, float scale)
        {
            this.terminal = terminal;
            this.like = like;
            // The top row uses the terminal's own letter size, because its buttons do not shrink.
            head = like.fontSize;
            Scale = scale;
            size = head * Scale;
            margin = size * 0.6f;
            charWidth = like.GetPreferredValues("0000000000").x / 10f * Scale;

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

            title = Ui.NewText("Title", root, like, head * 1.5f);
            title.enableAutoSizing = true;
            title.fontSizeMax = head * 1.5f;
            title.fontSizeMin = head * 0.6f;
            title.overflowMode = TextOverflowModes.Ellipsis;

            subtitle = Ui.NewText("Subtitle", root, like, head * 0.7f);

            clock = Ui.NewText("Clock", root, like, head * 0.7f);
            clock.alignment = TextAlignmentOptions.Right;
            Ui.PinToTop(clock.rectTransform, head * 20f, head * 1.75f, margin, head * 0.8f);
            subtitle.color = Faded(0.75f);

            creditsBox = Ui.NewBox("Credits", root);
            creditsBox.color = new Color(0f, 0f, 0f, 0.45f);
            Ui.TintOutline(Ui.NewOutline("Frame", creditsBox.rectTransform, Thin()), like.color);
            credits = Ui.NewText("Amount", creditsBox.rectTransform, like, head * 0.95f);
            credits.alignment = TextAlignmentOptions.Center;
            credits.enableAutoSizing = true;
            credits.fontSizeMax = head * 0.95f;
            credits.fontSizeMin = head * 0.5f;
            Ui.Stretch(credits.rectTransform);
            credits.margin = new Vector4(head * 0.2f, 0f, head * 0.2f, 0f);
            var buttonHeight = head * 1.5f * TerminalMouse.HeaderScale;
            PinRight(creditsBox.rectTransform, head * 13.6f, head * 0.3f + (head - head * TerminalMouse.HeaderScale) * 0.75f, head * 4.6f, buttonHeight);

            var rule = Ui.NewBox("Rule", root);
            rule.color = Faded(0.8f);
            Ui.PinToTop(rule.rectTransform, margin, head * 2.6f, margin, Thin());

            summaryBox = Ui.NewBox("Summary", root);
            summaryBox.color = Faded(0.05f);
            Ui.TintOutline(Ui.NewOutline("Frame", summaryBox.rectTransform, Thin()), Faded(0.8f));
            summary = Ui.NewText("Text", summaryBox.rectTransform, like, size);
            Ui.Stretch(summary.rectTransform);
            summary.margin = new Vector4(size * 0.5f, 0f, size * 0.5f, 0f);

            dialogBox = Ui.NewBox("Dialog", root);
            dialogBox.color = Faded(0.05f);
            Ui.TintOutline(Ui.NewOutline("Frame", dialogBox.rectTransform, Thin()), Faded(0.85f));
            Ui.SetActive(dialogBox, false);

            var areaHolder = new GameObject("Area", typeof(RectTransform));
            areaHolder.layer = holder.layer;
            area = (RectTransform)areaHolder.transform;
            area.SetParent(root, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;

            headRule = Ui.NewBox("HeadRule", area);
            headRule.color = Faded(0.6f);

            var listHolder = new GameObject("List", typeof(RectTransform));
            listHolder.layer = holder.layer;
            list = (RectTransform)listHolder.transform;
            list.SetParent(area, false);
            list.anchorMin = Vector2.zero;
            list.anchorMax = Vector2.one;

            sideRule = Ui.NewBox("SideRule", root);
            sideRule.color = Faded(0.5f);

            listTrack = Ui.NewBox("Track", area);
            listTrack.color = Faded(0.25f);
            listThumb = Ui.NewBox("Thumb", area);
            listThumb.color = Faded(0.9f);
            sideThumb = Ui.NewBox("SideThumb", root);
            sideThumb.color = Faded(0.9f);

            detail = NewPanel("Detail");
            extra = NewPanel("Extra");

            footRule = Ui.NewBox("FootRule", root);
            footRule.color = Faded(0.8f);
            footRule.rectTransform.anchorMin = Vector2.zero;
            footRule.rectTransform.anchorMax = new Vector2(1f, 0f);
            footRule.rectTransform.pivot = Vector2.zero;
            footRule.rectTransform.anchoredPosition = new Vector2(margin, size * 1.9f);
            footRule.rectTransform.sizeDelta = new Vector2(-margin * 2f, Thin());

            typed = Ui.NewText("Typed", root, like, size);
            Bottom(typed.rectTransform);

            position = Ui.NewText("Position", root, like, size * 0.85f);
            position.alignment = TextAlignmentOptions.Right;
            position.color = Faded(0.75f);
            Bottom(position.rectTransform);

            // The window goes on last, so it covers the page.
            shade = Ui.NewBox("Shade", root);
            shade.color = new Color(0f, 0f, 0f, 0.72f);
            Ui.Stretch(shade.rectTransform);
            Ui.SetActive(shade, false);

            winBox = Ui.NewBox("Window", root);
            winBox.color = new Color(0.02f, 0.06f, 0.03f, 0.97f);
            Ui.TintOutline(Ui.NewOutline("Frame", winBox.rectTransform, Thin() * 1.5f), Faded(0.95f));
            winTitle = Ui.NewText("Title", winBox.rectTransform, like, size * 1.15f);
            winTitle.overflowMode = TextOverflowModes.Ellipsis;
            winRule = Ui.NewBox("Rule", winBox.rectTransform);
            winRule.color = Faded(0.7f);
            winClose = NewFramedButton("Close", winBox.rectTransform, size);
            winClose.Text.text = "X";
            winClose.Box.color = Faded(0.08f);
            Ui.TintOutline(winClose.Frame, Faded(0.9f));
            winClose.Spot.Click = Window.Close;

            var view = new GameObject("View", typeof(RectTransform), typeof(RectMask2D));
            view.layer = holder.layer;
            winView = (RectTransform)view.transform;
            winView.SetParent(winBox.rectTransform, false);

            var content = new GameObject("Content", typeof(RectTransform));
            content.layer = holder.layer;
            winContent = (RectTransform)content.transform;
            winContent.SetParent(winView, false);
            winPicture = NewIcon(winContent);
            winMissing = Ui.NewText("Missing", winPicture.rectTransform, like, size * 0.9f);
            winMissing.text = "[ NO IMAGE ]";
            winMissing.alignment = TextAlignmentOptions.Center;
            winMissing.color = Faded(0.5f);
            Ui.Stretch(winMissing.rectTransform);
            winSide = Ui.NewText("Side", winContent, like, size * 0.9f);
            winSide.enableWordWrapping = true;
            winSide.alignment = TextAlignmentOptions.TopLeft;
            winText = Ui.NewText("Text", winContent, like, size * 0.9f);
            winInput = Ui.NewBox("Input", winContent);
            winInput.color = new Color(0f, 0f, 0f, 0.5f);
            Ui.TintOutline(Ui.NewOutline("Frame", winInput.rectTransform, Thin()), Faded(0.9f));
            winInputText = Ui.NewText("Typed", winInput.rectTransform, like, size * 1.1f);
            Ui.Stretch(winInputText.rectTransform);
            winInputText.margin = new Vector4(size * 0.5f, 0f, size * 0.5f, 0f);
            Ui.SetActive(winInput, false);
            winText.enableWordWrapping = true;
            winText.overflowMode = TextOverflowModes.Overflow;
            winText.alignment = TextAlignmentOptions.TopLeft;
            Ui.SetActive(winBox, false);
        }

        private bool WindowOpen => page != null && shownWindow != null;

        internal TMP_Text WindowText => WindowOpen ? winText : null;

        internal bool InWindowView(Vector3 world)
        {
            return WindowOpen && winView.rect.Contains(winView.InverseTransformPoint(world));
        }

        internal string AppScreen()
        {
            return terminal.screenText.text.Trim('\n', '\r');
        }

        private RectTransform winContent;
        private float nextRender;
        private float nextPanels;
        private static bool brokenCamera;
        private RawImage winPicture;
        private Image winInput;
        private TextMeshProUGUI winInputText;
        private TextMeshProUGUI winMissing;
        private TextMeshProUGUI winSide;
        private readonly List<TextMeshProUGUI> winHeadings = new List<TextMeshProUGUI>();
        private string winTitleShown;

        private float sideWidth;
        private float summaryRight;

        private float TabFont => head * 0.55f;
        private bool wrapDescriptions;
        private Image listTrack;
        private Image listThumb;
        private Image sideThumb;
        private float barTop;
        private float barHeight;
        private float sideTop;
        private int sideFirst;
        private int sideVisible;

        internal bool ScrollSide(Vector3 world, int by)
        {
            if (page == null || WindowOpen || !sideShown || page.Sidebar.Count <= sideVisible)
            {
                return false;
            }

            var local = root.InverseTransformPoint(world);
            if (local.x - root.rect.xMin > margin + sideWidth + size * 0.35f || root.rect.yMax - local.y < sideTop)
            {
                return false;
            }

            var before = sideFirst;
            sideFirst = Mathf.Clamp(sideFirst + Math.Sign(by), 0, page.Sidebar.Count - sideVisible);
            if (sideFirst != before)
            {
                LayoutSidebar(sideTop, size * 2.3f, false);
            }

            return true;
        }

        internal bool CloseWindow()
        {
            if (!WindowOpen || shownWindow.App)
            {
                return false;
            }

            Window.Close();
            return true;
        }

        private void DrawWindow()
        {
            var shown = shownWindow;
            shade.rectTransform.SetAsLastSibling();
            winBox.rectTransform.SetAsLastSibling();
            Ui.SetActive(shade, shown != null);
            Ui.SetActive(winBox, shown != null);
            if (shown == null)
            {
                return;
            }

            var width = Mathf.Min(root.rect.width - margin * 2f, size * 32f);
            var inner = size * 0.8f;
            var textWidth = width - inner * 2f;
            var headHeight = size * 2.3f;
            var y = 0f;
            var used = 0;
            var titles = 0;

            void PlaceButton(ToolButton tool, float x, float top, float wide)
            {
                if (used == winButtons.Count)
                {
                    winButtons.Add(NewFramedButton("Button", winContent, size));
                }

                var button = winButtons[used++];
                button.Text.text = tool.Label;
                button.Text.color = tool.Off ? Faded(0.45f) : like.color;
                button.Box.color = tool.Bare ? Color.clear : Faded(tool.Active ? 0.3f : 0.06f);
                Ui.TintOutline(button.Frame, tool.Bare ? Color.clear : Faded(0.85f));
                button.Spot.Click = tool.Off || tool.Bare ? null : tool.Click;
                Ui.SetActive(button.Box, true);
                PinTop(button.Rect, x, top, wide, size * 1.5f);
            }

            float Width(ToolButton tool)
            {
                return tool.Narrow ? size * 2.2f : Measure(tool.Label) + (tool.Bare ? size * 0.8f : size * 1.6f);
            }

            void PlaceSets(List<KeyValuePair<string, List<ToolButton>>> sets, float left, float across, ref float top)
            {
                foreach (var set in sets)
                {
                    if (shown.GroupsInLine && !string.IsNullOrEmpty(set.Key))
                    {
                        if (titles == winHeadings.Count)
                        {
                            var heading = Ui.NewText("Heading", winContent, like, size * 0.8f);
                            winHeadings.Add(heading);
                        }

                        var row = winHeadings[titles++];
                        row.text = set.Key;
                        row.color = like.color;
                        row.fontSize = size * 0.9f;
                        Ui.SetActive(row, true);

                        var end = left + across;
                        for (var b = set.Value.Count - 1; b >= 0; b--)
                        {
                            var wide = Width(set.Value[b]);
                            end -= wide;
                            PlaceButton(set.Value[b], end, top, wide);
                            end -= size * 0.3f;
                        }

                        PinTop(row.rectTransform, left, top + size * 0.15f, end - left - size * 0.3f, size * 1.3f);
                        top += size * 1.75f;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(set.Key))
                    {
                        if (titles == winHeadings.Count)
                        {
                            var heading = Ui.NewText("Heading", winContent, like, size * 0.8f);
                            heading.color = Faded(0.7f);
                            winHeadings.Add(heading);
                        }

                        var title = winHeadings[titles++];
                        title.text = set.Key;
                        title.color = Faded(0.7f);
                        title.fontSize = size * 0.8f;
                        Ui.SetActive(title, true);
                        PinTop(title.rectTransform, left, top, across, size * 1.1f);
                        top += size * 1.3f;
                    }

                    var x = 0f;
                    foreach (var tool in set.Value)
                    {
                        var wide = Mathf.Min(Width(tool), across);
                        if (x > 0f && x + wide > across)
                        {
                            x = 0f;
                            top += size * 1.8f;
                        }

                        PlaceButton(tool, left + x, top, wide);
                        x += wide + size * 0.5f;
                    }

                    if (set.Value.Count > 0)
                    {
                        top += size * 2.2f;
                    }
                }
            }

            var hasPicture = shown.Texture != null || shown.NoPicture;
            Ui.SetActive(winPicture, hasPicture);
            Ui.SetActive(winMissing, shown.Texture == null);
            winSide.text = shown.Side ?? "";
            Ui.SetActive(winSide, winSide.text.Length > 0);
            if (hasPicture || winSide.text.Length > 0)
            {
                var aspect = shown.Texture != null ? shown.Texture.width / Mathf.Max(1f, shown.Texture.height) : 4f / 3f;
                var picture = hasPicture ? Mathf.Min(size * 9f, textWidth * 0.45f) : 0f;
                var sideLeft = hasPicture ? picture + size * 0.8f : 0f;
                var sideHeight = winSide.text.Length > 0 ? winSide.GetPreferredValues(winSide.text, textWidth - sideLeft, 0f).y : 0f;
                if (hasPicture)
                {
                    winPicture.texture = shown.Texture != null ? shown.Texture : Texture2D.blackTexture;
                    var tall = shown.Texture != null ? picture / aspect : picture * 0.75f;
                    PinTop(winPicture.rectTransform, 0f, 0f, picture, tall);
                    picture = tall;
                }

                PinTop(winSide.rectTransform, sideLeft, 0f, textWidth - sideLeft, sideHeight);
                y += Mathf.Max(picture, sideHeight) + size * 0.8f;
            }

            winText.text = shown.Text ?? "";
            winText.enableWordWrapping = !shown.App;
            winText.fontSize = size * 0.9f;
            if (shown.App && winText.text.Length > 0)
            {
                var widest = winText.GetPreferredValues(winText.text).x;
                if (widest > textWidth)
                {
                    winText.fontSize = size * 0.9f * textWidth / widest;
                }
            }

            var textHeight = winText.text.Length > 0 ? winText.GetPreferredValues(winText.text, textWidth, 0f).y : 0f;
            PinTop(winText.rectTransform, 0f, y, textWidth, textHeight);
            if (textHeight > 0f)
            {
                y += textHeight + size * 0.6f;
            }

            Ui.SetActive(winInput, shown.Send != null);
            if (shown.Send != null)
            {
                PinTop(winInput.rectTransform, 0f, y, textWidth, size * 1.9f);
                ShowTyped();
                y += size * 2.5f;
            }

            var rest = new List<KeyValuePair<string, List<ToolButton>>>(shown.Groups);
            if (shown.Buttons.Count > 0)
            {
                rest.Add(new KeyValuePair<string, List<ToolButton>>(null, shown.Buttons));
            }

            PlaceSets(rest, 0f, textWidth, ref y);
            var sets = rest;

            if (shown.RightButtons.Count > 0)
            {
                var line = shown.Buttons.Count > 0 ? y - size * 2.2f : y;
                var end = textWidth;
                for (var b = shown.RightButtons.Count - 1; b >= 0; b--)
                {
                    var wide = Width(shown.RightButtons[b]);
                    end -= wide;
                    PlaceButton(shown.RightButtons[b], end, line, wide);
                    end -= size * 0.5f;
                }

                if (shown.Buttons.Count == 0)
                {
                    y += size * 2.2f;
                }
            }

            for (var i = used; i < winButtons.Count; i++)
            {
                Ui.SetActive(winButtons[i].Box, false);
            }

            for (var i = titles; i < winHeadings.Count; i++)
            {
                Ui.SetActive(winHeadings[i], false);
            }

            var contentHeight = Mathf.Max(size, y - (sets.Count > 0 || shown.RightButtons.Count > 0 ? size * 0.4f : size * 0.6f));

            var room = root.rect.height - head * 3f - size * 2.6f;
            var viewHeight = Mathf.Min(contentHeight, room - headHeight - inner);
            var height = headHeight + viewHeight + inner;

            PinLeft(winBox.rectTransform, (root.rect.width - width) / 2f, head * 3f + (room - height) / 2f, width, height);

            winTitle.text = shown.Title ?? "";
            PinTop(winTitle.rectTransform, inner, size * 0.45f, width - inner * 2f - size * 2f, size * 1.4f);
            PinRight(winClose.Rect, inner * 0.6f, size * 0.4f, size * 1.6f, size * 1.5f);
            PinTop(winRule.rectTransform, inner * 0.6f, size * 2.05f, width - inner * 1.2f, Thin());
            PinTop(winView, inner, headHeight, textWidth, viewHeight);

            // Drawn again after a click inside it, it stays where it was scrolled to.
            winMax = Mathf.Max(0f, contentHeight - viewHeight);
            winScroll = shown.Title == winTitleShown ? Mathf.Min(winScroll, winMax) : 0f;
            winTitleShown = shown.Title;
            PinTop(winContent, 0f, -winScroll, textWidth, contentHeight);
        }

        private void ShowTyped()
        {
            var typed = Commands.Typed(terminal);
            if (typed.Length > shownWindow.Longest)
            {
                typed = typed.Substring(0, shownWindow.Longest);
            }

            var text = "> " + typed + "_";
            if (winInputText.text != text)
            {
                winInputText.text = text;
            }
        }

        private bool InView(RectTransform rect)
        {
            var center = winView.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            return winView.rect.Contains(center);
        }

        private IEnumerable<Button> CornerButtons()
        {
            if (corner != null && corner.Box.gameObject.activeSelf)
            {
                yield return corner;
            }
        }

        private IEnumerable<Button> WindowButtons()
        {
            yield return winClose;
            foreach (var button in winButtons)
            {
                if (button.Box.gameObject.activeSelf && InView(button.Rect))
                {
                    yield return button;
                }
            }
        }

        internal float Scale { get; }

        internal void Show(MenuPage shown)
        {
            if (shown == null)
            {
                Ui.SetActive(root, false);
                ShowGameCredits(true);
                page = null;
                return;
            }

            Ui.SetActive(root, true);
            ShowGameCredits(false);

            if (shown != page)
            {
                // A sort or filter click hands over a new page with the same title and tab: the place in the list is
                // kept.
                if (page == null || page.Title != shown.Title || Tab(page) != Tab(shown))
                {
                    first = 0;
                    focused = null;
                    Window.Drop();
                }

                page = shown;
                Layout();
            }
        }

        private static string Tab(MenuPage shown)
        {
            return shown.Sidebar?.FirstOrDefault(entry => entry.Active)?.Label;
        }

        private void ShowGameCredits(bool show)
        {
            var text = terminal.topRightText;
            if (text == null || gameCreditsHidden == !show)
            {
                return;
            }

            if (gameCreditsBox == null)
            {
                var box = Ui.CreditsBox(text);
                gameCreditsBox = box != null ? box.GetComponent<Graphic>() : null;
            }

            gameCreditsHidden = !show;
            text.enabled = show;
            if (gameCreditsBox != null)
            {
                gameCreditsBox.enabled = show;
            }
        }

        internal bool Scroll(int by)
        {
            if (page == null)
            {
                return false;
            }

            if (WindowOpen)
            {
                var was = winScroll;
                winScroll = Mathf.Clamp(winScroll + Mathf.Sign(by) * size * 1.4f, 0f, winMax);
                winContent.anchoredPosition = new Vector2(0f, winScroll);
                return winScroll != was;
            }

            var before = first;
            first = Mathf.Clamp(first + by, 0, Mathf.Max(0, page.Entries.Count - visibleRows));
            if (first != before)
            {
                FillRows();
            }

            return first != before;
        }

        internal int VisibleRows => visibleRows;

        internal bool ScrollFrom(Vector3 world, bool down)
        {
            if (page == null)
            {
                return false;
            }

            if (WindowOpen)
            {
                return shownWindow.Buttons.Count == 0 && shownWindow.Groups.Count == 0 && Scroll(down ? 1 : -1);
            }

            var onList = false;

            for (var i = 0; i < visibleRows && i < rows.Count; i++)
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
                else if (CanClick(row) && (down ? height > rect.yMax : height < rect.yMin))
                {
                    return false;
                }
            }

            return onList && Scroll(down ? 1 : -1);
        }

        internal bool Jump(Vector3 world, bool forward, RectTransform frame, out Vector2 stand)
        {
            stand = default(Vector2);
            if (page == null || WindowOpen)
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
                // From the first row under a bar, back means the section before, not the bar just above.
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

            for (var i = Mathf.Max(0, target - first); i < visibleRows && i < rows.Count; i++)
            {
                if (rows[i].Rect.gameObject.activeInHierarchy && CanClick(rows[i]))
                {
                    stand = RowPoint(frame, rows[i]);
                    return true;
                }
            }

            stand = frame.InverseTransformPoint(world);
            return true;
        }

        internal bool SwitchTab(bool next)
        {
            var bar = page != null && !WindowOpen ? page.Sidebar : null;
            if (bar == null || bar.Count == 0)
            {
                return false;
            }

            var at = bar.FindIndex(entry => entry.Active);
            for (var step = 1; step <= bar.Count; step++)
            {
                var index = ((at < 0 ? 0 : at) + (next ? step : -step) + bar.Count * 2) % bar.Count;
                var click = ClickFor(bar[index]);
                if (click != null)
                {
                    click();
                    return true;
                }
            }

            return false;
        }

        internal const int ZoneHeader = 0;
        internal const int ZoneBar = 1;
        internal const int ZoneTabs = 2;
        internal const int ZoneList = 3;
        internal const int ZonePanel = 4;
        internal const int ZoneWindow = 5;

        internal void Targets(RectTransform frame, List<Vector2> into, List<int> zones)
        {
            void Fill(int zone)
            {
                while (zones.Count < into.Count)
                {
                    zones.Add(zone);
                }
            }

            void Buttons(IEnumerable<Button> buttons, int zone)
            {
                foreach (var button in buttons)
                {
                    if (button.Spot.Click != null)
                    {
                        Target(frame, button.Rect, 0.5f, into);
                    }
                }

                Fill(zone);
            }

            if (WindowOpen)
            {
                foreach (var button in WindowButtons())
                {
                    Target(frame, button.Rect, 0.5f, into);
                }

                Fill(ZoneWindow);
                return;
            }

            Buttons(tools.Concat(pinned).Concat(CornerButtons()), ZoneBar);
            Buttons(tabs, ZoneTabs);
            Buttons(detail.Buttons.Concat(extra.Buttons).Concat(detail.GroupButtons).Concat(extra.GroupButtons), ZonePanel);

            foreach (var button in heads)
            {
                if (button.Spot.Click != null)
                {
                    Target(frame, button.Rect, 0.5f, into);
                }
            }

            foreach (var row in rows)
            {
                if (row.Clickable && row.Rect.gameObject.activeInHierarchy)
                {
                    into.Add(RowPoint(frame, row));
                }

                for (var i = 0; i < MaxCells; i++)
                {
                    if (row.CellSpots[i].Click != null)
                    {
                        Target(frame, row.Cells[i].rectTransform, 0.5f, into);
                    }
                }
            }

            Fill(ZoneList);
        }

        private Vector2 RowPoint(RectTransform frame, Row row)
        {
            var rect = row.Rect.rect;
            var point = new Vector2(rect.xMin + Mathf.Min(rect.width * 0.12f, size * 4f) + (pictures ? rowHeight : 0f), rect.center.y);
            return frame.InverseTransformPoint(row.Rect.TransformPoint(point));
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

            var r = rect.rect;
            var point = new Vector2(Mathf.Lerp(r.xMin, r.xMax, along), r.center.y);
            into.Add(frame.InverseTransformPoint(rect.TransformPoint(point)));
        }

        internal void Tick()
        {
            if (page == null)
            {
                return;
            }

            if (!ReferenceEquals(Window.Open, shownWindow))
            {
                shownWindow = Window.Open;
                DrawWindow();
            }
            else if (shownWindow != null && shownWindow.App)
            {
                var screen = AppScreen();
                if (screen != shownWindow.Text)
                {
                    shownWindow.Text = screen;
                    DrawWindow();
                }
            }
            else if (shownWindow != null && shownWindow.Send != null)
            {
                ShowTyped();
            }

            var live = page.Camera;
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

            var time = StatusPanel.Clock() ?? "";
            if (clock.text != time)
            {
                clock.text = time;
            }

            if (page.LivePanels && Time.unscaledTime >= nextPanels)
            {
                nextPanels = Time.unscaledTime + 0.5f;
                DrawPanels();
            }

            if (terminal.groupCredits != shownCredits)
            {
                shownCredits = terminal.groupCredits;
                credits.text = "$" + shownCredits;
            }

            if (!ReferenceEquals(hintFor, TerminalMouse.PadNames) || (hintFor != null && !ReferenceEquals(hintIcons, PadIcons.Sheet)))
            {
                DrawHint();
            }

            for (var i = 0; i < visibleRows && i < rows.Count; i++)
            {
                var index = first + i;
                if (index >= page.Entries.Count)
                {
                    continue;
                }

                var entry = page.Entries[index];
                if (entry.LiveLabel != null)
                {
                    rows[i].Label.text = entry.LiveLabel();
                }

                if (entry.LiveCells != null)
                {
                    var cells = entry.LiveCells();
                    if (entry.Cells == null || !cells.SequenceEqual(entry.Cells))
                    {
                        entry.Cells = cells;
                        Fill(rows[i], entry, rowHeight, sectioned, table && !sectioned && !page.Plain && index % 2 == 1);
                        if (entry == focused)
                        {
                            DrawPanels();
                        }
                    }
                }
            }
        }

        internal void SetTyped(string text)
        {
            typed.text = "> " + text + "_";
        }

        internal void Focus(HotSpot spot)
        {
            if (page == null || spot == null || page.DetailFor == null || !panelsShown || page.SelectRows || WindowOpen)
            {
                return;
            }

            for (var i = 0; i < visibleRows && i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Spot == spot || row.CellSpots.Contains(spot))
                {
                    var entry = first + i < page.Entries.Count ? page.Entries[first + i] : null;
                    if (entry != null && entry != focused)
                    {
                        focused = entry;
                        DrawPanels();
                    }

                    return;
                }
            }
        }

        internal HotSpot SpotAt(Vector3 world)
        {
            if (WindowOpen)
            {
                foreach (var button in WindowButtons())
                {
                    if (button.Spot.Click != null && Under(button.Rect, world))
                    {
                        return At(button.Spot, button.Rect);
                    }
                }

                return null;
            }

            foreach (var button in tools.Concat(pinned).Concat(tabs).Concat(detail.Buttons).Concat(extra.Buttons).Concat(detail.GroupButtons).Concat(extra.GroupButtons).Concat(CornerButtons()))
            {
                if (button.Spot.Click != null && Under(button.Rect, world))
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

                if (!row.Clickable)
                {
                    return null;
                }

                // A row with cells to click (the store's [+], STORAGE's second column) is clickable up to the first of
                // them, at full height.
                var rect = row.Rect.rect;
                var end = rect.xMax;
                for (var i = 0; i < MaxCells; i++)
                {
                    if (row.CellSpots[i].Click != null)
                    {
                        var cell = row.Cells[i].rectTransform;
                        cell.GetWorldCorners(Corners);
                        end = Mathf.Min(end, row.Rect.InverseTransformPoint(Corners[0]).x);
                    }
                }

                var area = Rect.MinMaxRect(rect.xMin, rect.yMin, end, rect.yMax);
                if (!area.Contains(row.Rect.InverseTransformPoint(world)))
                {
                    return null;
                }

                row.Spot.Space = row.Rect;
                row.Spot.Area = area;
                return row.Spot;
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
            title.text = "> " + page.Title;
            var hasSubtitle = !string.IsNullOrEmpty(page.Subtitle);
            subtitle.text = page.Subtitle ?? "";
            Ui.SetActive(subtitle, hasSubtitle);

            Ui.PinToTop(title.rectTransform, margin, hasSubtitle ? head * 0.1f : head * 0.35f, head * 18.6f, head * 1.7f);
            Ui.PinToTop(subtitle.rectTransform, margin + head * 1.1f, head * 1.75f, margin + head * 5f, head * 0.8f);

            shownCredits = int.MinValue;

            var width = root.rect.width;
            sideShown = page.Sidebar != null && page.Sidebar.Count > 0 && width >= size * 34f;
            sideWidth = sideShown ? Mathf.Clamp(page.Sidebar.Max(entry => typed.GetPreferredValues(entry.Label).x * TabFont / size) + TabFont * 1.6f, TabFont * 6f, TabFont * 11f) : 0f;
            panelsShown = page.DetailFor != null || page.Extra != null;

            MeasureColumns();

            var top = head * 3f;
            top = LayoutTools(top);
            top = LayoutSummary(top);

            var bottom = size * 2.3f;
            var left = sideShown ? sideWidth + size * 0.8f : 0f;
            var right = panelsShown ? size * 10.4f : 0f;
            LayoutSidebar(top, bottom);

            var spaceLeft = margin + left;
            var spaceRight = margin + right;

            table = page.Columns.Count > 0;
            pictures = page.Entries.Any(entry => entry.Picture != null);
            tall = table && (pictures || page.Entries.Any(entry => !string.IsNullOrEmpty(entry.Sub)));
            var room = width - spaceLeft - spaceRight - size * 2f;
            wrapDescriptions = !table && page.Entries.Any(entry => !string.IsNullOrEmpty(entry.Description) && Measure(entry.Description) * 0.9f > room);
            rowHeight = size * (tall ? 2.3f : page.Dense ? 1.3f : table ? 1.5f : wrapDescriptions ? 3.3f : page.HasDescriptions ? 2.5f : 1.6f);

            var headsHeight = !page.HideHeads && page.Columns.Count > 0 ? size * 1.6f : 0f;
            var fits = Mathf.Max(1, Mathf.FloorToInt((root.rect.height - top - bottom - headsHeight) / rowHeight));
            var scrolls = !page.Dialog && page.Entries.Count > fits;
            if (scrolls)
            {
                spaceRight += size * 0.7f;
            }

            if (page.Dialog)
            {
                var free = width - spaceLeft - spaceRight;
                var side = (free - Mathf.Min(free - size * 1.6f, size * 24f)) / 2f;
                spaceLeft += side;
                spaceRight += side;
                top += size * 0.8f;
            }

            area.offsetMin = new Vector2(spaceLeft, bottom);
            area.offsetMax = new Vector2(-spaceRight, -top);

            var listWidth = width - spaceLeft - spaceRight;
            for (var c = 1; c < page.Columns.Count; c++)
            {
                if (page.Columns[c].Share > 0f)
                {
                    widths[c] = listWidth * page.Columns[c].Share;
                }
            }

            var headsBottom = LayoutHeads(0f);
            list.offsetMin = Vector2.zero;
            list.offsetMax = new Vector2(0f, -headsBottom);

            sectioned = page.Entries.Any(IsBar);

            // The list area is only known after the layout has run, so it is worked out from the numbers above.
            var listHeight = root.rect.height - top - bottom - headsBottom;
            visibleRows = page.Picture != null ? 0 : Mathf.Max(1, Mathf.FloorToInt(listHeight / rowHeight));
            version++;

            if (page.Picture != null && pagePicture == null)
            {
                pagePicture = NewIcon(list);
            }

            if (pagePicture != null)
            {
                Ui.SetActive(pagePicture, page.Picture != null);
            }

            if (page.Picture != null)
            {
                pagePicture.texture = page.Picture;
                pagePicture.color = Color.white;
                var across = Mathf.Min(listWidth, listHeight * page.PictureAspect);
                var down = across / page.PictureAspect;
                PinLeft(pagePicture.rectTransform, (listWidth - across) / 2f, (listHeight - down) / 2f, across, down);
            }

            Ui.SetActive(listTrack, scrolls);
            Ui.SetActive(listThumb, scrolls);
            barTop = headsBottom;
            barHeight = listHeight;
            if (scrolls)
            {
                PinRight(listTrack.rectTransform, -size * 0.35f, barTop, Thin() * 2f, barHeight);
            }

            Ui.SetActive(dialogBox, page.Dialog);
            if (page.Dialog)
            {
                var high = Mathf.Min(page.Entries.Count, visibleRows) * rowHeight + headsBottom;
                PinLeft(dialogBox.rectTransform, spaceLeft - size * 0.8f, top - size * 0.8f, width - spaceLeft - spaceRight + size * 1.6f, high + size * 1.6f);
            }

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

            if (page.Pick != null && page.Entries.Contains(page.Pick))
            {
                focused = page.Pick;
                var at = page.Entries.IndexOf(page.Pick);
                if (at < first || at >= first + visibleRows)
                {
                    first = Mathf.Max(0, at - 1);
                }
            }

            if (focused == null || !page.Entries.Contains(focused))
            {
                var before = focused;
                focused = (before != null ? page.Entries.FirstOrDefault(entry => !entry.IsHeader && entry.Label == before.Label) : null)
                          ?? page.Entries.FirstOrDefault(entry => !entry.IsHeader && ClickFor(entry) != null);
            }

            LayoutPanels(top, bottom);
            FillRows();
        }

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

            if (listThumb.gameObject.activeSelf && count > visibleRows)
            {
                var thumb = Mathf.Max(size, barHeight * visibleRows / count);
                var at = (barHeight - thumb) * first / (count - visibleRows);
                PinRight(listThumb.rectTransform, -size * 0.35f - Thin(), barTop + at, Thin() * 4f, thumb);
            }
            DrawHint();
        }

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
            var shown = new List<ToolButton>(page.Tools);
            if (page.Sidebar != null && !sideShown)
            {
                shown.InsertRange(0, page.Sidebar.Select(entry => new ToolButton { Label = entry.Label, Active = entry.Active, Click = ClickFor(entry) }));
            }

            var x = margin;

            for (var i = 0; i < shown.Count; i++)
            {
                if (i == tools.Count)
                {
                    tools.Add(NewFramedButton("Tool", root, size * 0.85f));
                }

                var tool = shown[i];
                var button = tools[i];

                var width = button.Text.GetPreferredValues(tool.Label).x + size * 1f;

                if (x > margin && x + width > root.rect.width - margin)
                {
                    x = margin;
                    top += size * 1.7f;
                }

                button.Text.text = tool.Label;
                button.Text.color = tool.Off ? Faded(0.45f) : like.color;
                button.Box.color = Faded(tool.Active ? 0.3f : 0.04f);
                Ui.TintOutline(button.Frame, Faded(tool.Off ? 0.35f : 0.85f));
                button.Spot.Click = tool.Click;
                Ui.SetActive(button.Box, true);
                PinLeft(button.Rect, x, top, width, size * 1.45f);
                x += width + size * 0.5f;
            }

            for (var i = shown.Count; i < tools.Count; i++)
            {
                Ui.SetActive(tools[i].Box, false);
            }

            return shown.Count > 0 ? top + size * 1.9f : top;
        }

        private float LayoutSummary(float top)
        {
            var line = page.Pinned.Count == 1 && page.Pinned[0].Flat ? page.Pinned[0] : null;
            var text = line != null ? line.Label : page.Summary;
            var shown = !string.IsNullOrEmpty(text);
            Ui.SetActive(summaryBox, shown);

            var right = panelsShown ? margin + size * 10.4f : margin;
            var hasCorner = page.Corner != null;
            if (hasCorner && corner == null)
            {
                corner = NewFramedButton("Corner", root, size * 0.9f);
            }

            if (corner != null)
            {
                Ui.SetActive(corner.Box, hasCorner);
            }

            if (hasCorner)
            {
                corner.Text.text = page.Corner.Label;
                corner.Box.color = Faded(page.Corner.Active ? 0.3f : 0.06f);
                Ui.TintOutline(corner.Frame, Faded(0.85f));
                corner.Spot.Click = page.Corner.Click;
                PinRight(corner.Rect, margin, top, panelsShown ? size * 10f : size * 7f, size * 1.5f);
                if (!panelsShown)
                {
                    right = margin + size * 7.4f;
                }
            }

            if (shown)
            {
                summary.text = text;
                Ui.PinToTop(summaryBox.rectTransform, margin, top, right, size * 1.5f);
                summaryRight = right;
            }

            if (shown || hasCorner)
            {
                top += size * 1.9f;
            }

            return line != null ? LayoutPinned(top, line) : LayoutPinned(top, null);
        }

        private float LayoutPinned(float top, MenuEntry line)
        {
            var x = margin;
            var high = size * 1.5f;
            var icon = size * 0.95f;
            var limit = root.rect.width - margin;
            var shown = line != null ? new List<MenuEntry>() : page.Pinned;

            if (line != null)
            {
                if (pinned.Count == 0)
                {
                    pinned.Add(NewFramedButton("Pinned", root, size));
                }

                var button = pinned[0];
                button.Text.text = "";
                button.Box.color = Color.clear;
                Ui.TintOutline(button.Frame, Color.clear);
                button.Spot.Click = ClickFor(line);
                button.Spot.AltClick = AltFor(line);
                Ui.SetActive(button.Box, true);
                Ui.SetActive(button.Marker, false);
                Ui.PinToTop(button.Rect, margin, top - size * 1.9f, summaryRight, size * 1.5f);

                for (var i = 1; i < pinned.Count; i++)
                {
                    Ui.SetActive(pinned[i].Box, false);
                }

                return top;
            }

            for (var i = 0; i < shown.Count; i++)
            {
                if (i == pinned.Count)
                {
                    pinned.Add(NewFramedButton("Pinned", root, size));
                }

                var entry = shown[i];
                var button = pinned[i];
                var marked = entry.Marker != Marker.None;
                var width = button.Text.GetPreferredValues(entry.Label).x + size * 1.2f + (marked ? icon + size * 0.3f : 0f);

                if (x > margin && x + width > limit)
                {
                    x = margin;
                    top += size * 1.8f;
                }

                button.Text.text = entry.Label;
                button.Text.alignment = TextAlignmentOptions.Left;
                button.Text.margin = new Vector4(size * 0.6f, 0f, 0f, 0f);
                button.Text.color = entry.Dim ? Faded(0.5f) : like.color;
                button.Box.color = Faded(0.06f);
                Ui.TintOutline(button.Frame, Faded(0.85f));
                button.Spot.Click = ClickFor(entry);
                button.Spot.AltClick = AltFor(entry);
                Ui.SetActive(button.Box, true);
                PinLeft(button.Rect, x, top, width, high);

                Ui.SetActive(button.Marker, marked);
                if (marked)
                {
                    PlaceMarker(button.Marker, entry.Marker, button.Text, entry.Label, size * 0.6f, high);
                }

                x += width + size * 0.5f;
            }

            for (var i = shown.Count; i < pinned.Count; i++)
            {
                Ui.SetActive(pinned[i].Box, false);
            }

            return shown.Count > 0 ? top + size * 1.9f : top;
        }

        private void LayoutSidebar(float top, float bottom, bool showOpen = true)
        {
            var bar = sideShown ? page.Sidebar : new List<MenuEntry>();
            var high = TabFont * 2f;
            var step = high + TabFont * 0.3f;

            sideTop = top;
            sideVisible = Mathf.Max(1, Mathf.FloorToInt((root.rect.height - top - bottom + size * 0.2f) / step));
            var active = bar.FindIndex(entry => entry.Active);
            if (showOpen && active >= 0 && (active < sideFirst || active >= sideFirst + sideVisible))
            {
                sideFirst = active < sideFirst ? active : active - sideVisible + 1;
            }

            sideFirst = Mathf.Clamp(sideFirst, 0, Mathf.Max(0, bar.Count - sideVisible));

            for (var i = 0; i < bar.Count; i++)
            {
                if (i == tabs.Count)
                {
                    tabs.Add(NewFramedButton("Tab", root, TabFont));
                }

                var entry = bar[i];
                var button = tabs[i];
                if (i < sideFirst || i >= sideFirst + sideVisible)
                {
                    Ui.SetActive(button.Box, false);
                    continue;
                }

                var hasPicture = entry.Picture != null;

                button.Text.text = entry.Label;
                button.Text.alignment = TextAlignmentOptions.Left;
                button.Text.margin = new Vector4(hasPicture ? TabFont * 2.4f : TabFont * 0.7f, 0f, 0f, 0f);
                button.Text.color = entry.Dim ? Faded(0.5f) : like.color;
                button.Box.color = entry.Active ? Faded(0.28f) : Color.clear;
                Ui.TintOutline(button.Frame, entry.Active ? Faded(0.9f) : Color.clear);
                button.Spot.Click = ClickFor(entry);
                Ui.SetActive(button.Box, true);
                PinLeft(button.Rect, margin, top + (i - sideFirst) * step, sideWidth, high);

                Ui.SetActive(button.Picture, hasPicture);
                if (hasPicture)
                {
                    button.Picture.sprite = entry.Picture;
                    button.Picture.color = like.color;
                    PinLeft(button.Picture.rectTransform, size * 0.4f, size * 0.25f, size * 1.2f, size * 1.2f);
                }
            }

            for (var i = bar.Count; i < tabs.Count; i++)
            {
                Ui.SetActive(tabs[i].Box, false);
            }

            Ui.SetActive(sideRule, sideShown);
            var tabsScroll = sideShown && bar.Count > sideVisible;
            Ui.SetActive(sideThumb, tabsScroll);
            if (sideShown)
            {
                var height = root.rect.height - top - bottom;
                PinLeft(sideRule.rectTransform, margin + sideWidth + size * 0.35f, top, Thin(), height);

                if (tabsScroll)
                {
                    var thumb = Mathf.Max(size, height * sideVisible / bar.Count);
                    PinLeft(sideThumb.rectTransform, margin + sideWidth + size * 0.35f - Thin() * 1.5f,
                        top + (height - thumb) * sideFirst / (bar.Count - sideVisible), Thin() * 4f, thumb);
                }
            }
        }

        private void LayoutPanels(float top, float bottom)
        {
            var height = root.rect.height - top - bottom - size * 0.3f;
            var both = page.DetailFor != null && page.Extra != null;
            var detailHeight = both ? height * 0.62f : height;

            Ui.SetActive(detail.Box, panelsShown && page.DetailFor != null);
            Ui.SetActive(extra.Box, panelsShown && page.Extra != null);

            if (!panelsShown)
            {
                return;
            }


            if (page.DetailFor != null)
            {
                PinRight(detail.Box.rectTransform, margin, top, size * 10f, detailHeight);
            }

            if (page.Extra != null)
            {
                var y = page.DetailFor != null ? top + detailHeight + size * 0.4f : top;
                PinRight(extra.Box.rectTransform, margin, y, size * 10f, height - (y - top));
            }

            DrawPanels();
        }

        private void DrawPanels()
        {
            if (!panelsShown)
            {
                return;
            }

            if (page.DetailFor != null)
            {
                SafeDraw(detail, focused != null ? Try(() => page.DetailFor(focused)) : null);
            }

            if (page.Extra != null)
            {
                SafeDraw(extra, Try(page.Extra));
            }
        }

        // A panel that cannot be drawn stays empty, the rest of the page still works.
        private void SafeDraw(Panel panel, Detail shown)
        {
            try
            {
                Draw(panel, shown);
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"A side panel could not be drawn: {e}");
                if (shown != null)
                {
                    try
                    {
                        Draw(panel, null);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static Detail Try(Func<Detail> make)
        {
            try
            {
                return make();
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"A side panel could not be drawn: {e}");
                return null;
            }
        }

        private void Draw(Panel panel, Detail shown)
        {
            var width = panel.Box.rectTransform.rect.width;
            var height = panel.Box.rectTransform.rect.height;
            var inner = size * 0.6f;
            var wide = width - inner * 2f;
            var gap = size * 0.4f;

            var facts = shown != null ? shown.Facts : new List<Fact>();
            var all = shown != null ? shown.Buttons : new List<ToolButton>();
            var buttons = all.Where(button => !button.Primary).ToList();
            var primary = all.Where(button => button.Primary).ToList();
            var foot = shown?.Foot ?? "";
            var primaryStep = primary.Count > 2 ? size * 1.75f : size * 1.95f;
            var primaryHeight = primary.Count * primaryStep;
            var hasPicture = shown != null && (shown.Picture != null || shown.Texture != null || shown.Missing != null);

            var perLine = Mathf.Max(1, buttons.Count);
            while (perLine > 1)
            {
                var cell = (wide - (perLine - 1) * gap) / perLine;
                if (buttons.All(button => Measure(button.Label) + size * 0.8f <= cell))
                {
                    break;
                }

                perLine--;
            }

            var buttonLines = buttons.Count == 0 ? 0 : (buttons.Count + perLine - 1) / perLine;
            var twoLines = new bool[facts.Count];
            var factsHeight = 0f;
            for (var i = 0; i < facts.Count && i < MaxFacts; i++)
            {
                twoLines[i] = Measure(facts[i].Label) * 0.8f + Measure(facts[i].Value) + size * 1.2f > wide;
                factsHeight += twoLines[i] ? size * 2.3f : size * 1.25f;
            }

            var groups = shown != null ? shown.Groups : new List<KeyValuePair<string, List<ToolButton>>>();
            var groupLines = new int[groups.Count];
            var groupsHeight = 0f;
            for (var g = 0; g < groups.Count; g++)
            {
                var count = groups[g].Value.Count;
                var fit = Mathf.Max(1, count);
                while (fit > 1 && !groups[g].Value.Any(button => button.Narrow)
                       && !groups[g].Value.All(button => Measure(button.Label) + size * 0.8f <= (wide - (fit - 1) * gap) / fit))
                {
                    fit--;
                }

                groupLines[g] = fit;
                groupsHeight += (string.IsNullOrEmpty(groups[g].Key) ? 0f : size * 1.1f) + (count + fit - 1) / fit * size * 1.75f + size * 0.3f;
            }

            var below = factsHeight + (facts.Count > 0 ? size * 0.5f : 0f) + buttonLines * size * 1.75f + (foot.Length > 0 ? size * 1.1f : 0f)
                        + (primary.Count > 0 ? primaryHeight + size * 0.4f : 0f) + groupsHeight;

            panel.Text.text = shown?.Text ?? "";
            var wanted = panel.Text.text.Length > 0 ? Mathf.Min(panel.Text.GetPreferredValues(panel.Text.text, wide, 0f).y, size * 4f) : 0f;
            var room = height - inner * 2f - below;
            var bigSide = shown != null && shown.Picture == null ? Mathf.Min(wide, size * 6.5f) : size * 3.6f;
            var big = hasPicture && room - size * 1.6f - Mathf.Min(wanted, size * 1.8f) >= bigSide + size * 0.3f;
            var small = hasPicture && !big;
            var titleHeight = small ? size * 2.4f : size * 1.6f;
            var textHeight = Mathf.Clamp(room - titleHeight - (big ? bigSide + size * 0.3f : 0f) - size * 0.3f, 0f, wanted);
            var y = inner;

            Ui.SetActive(panel.Picture, hasPicture && shown.Picture != null);
            Ui.SetActive(panel.Texture, hasPicture && shown.Picture == null);
            panel.Missing.text = shown?.Missing ?? "";
            Ui.SetActive(panel.Missing, hasPicture && shown.Texture == null && shown.Missing != null);
            if (hasPicture)
            {
                var side = big ? bigSide : size * 2.2f;
                RectTransform picture;
                if (shown.Picture != null)
                {
                    panel.Picture.sprite = shown.Picture;
                    panel.Picture.color = like.color;
                    picture = panel.Picture.rectTransform;
                }
                else
                {
                    panel.Texture.texture = shown.Texture != null ? shown.Texture : Texture2D.blackTexture;
                    panel.Texture.color = Color.white;
                    picture = panel.Texture.rectTransform;
                }

                PinTop(picture, big ? (width - side) / 2f : inner, y, side, side);
                if (big)
                {
                    y += side + size * 0.3f;
                }
            }

            panel.Title.text = shown?.Title ?? "";
            Ui.SetActive(panel.Title, panel.Title.text.Length > 0);
            var titleLeft = small ? inner + size * 2.6f : inner;
            PinTop(panel.Title.rectTransform, titleLeft, y + (small ? size * 0.5f : 0f), width - titleLeft - inner, size * 1.4f);
            y += titleHeight;

            Ui.SetActive(panel.Text, textHeight >= size * 0.9f);
            if (textHeight >= size * 0.9f)
            {
                PinTop(panel.Text.rectTransform, inner, y, wide, textHeight);
                y += textHeight + size * 0.3f;
            }

            Ui.SetActive(panel.Rule, facts.Count > 0);
            if (facts.Count > 0)
            {
                PinTop(panel.Rule.rectTransform, inner, y, wide, Thin());
                y += size * 0.5f;
            }

            for (var i = 0; i < MaxFacts; i++)
            {
                var show = i < facts.Count;
                Ui.SetActive(panel.FactLabels[i], show);
                Ui.SetActive(panel.FactValues[i], show);
                if (!show)
                {
                    continue;
                }

                var split = twoLines[i];

                panel.FactLabels[i].text = facts[i].Label.ToUpperInvariant();
                PinTop(panel.FactLabels[i].rectTransform, inner, y, wide, size * 1.2f);

                var value = panel.FactValues[i];
                value.text = facts[i].Value;
                PinTop(value.rectTransform, inner, split ? y + size * 1.05f : y, wide, size * 1.2f);

                y += split ? size * 2.3f : size * 1.25f;
            }

            var cellWidth = (wide - (perLine - 1) * gap) / perLine;
            var normal = buttons.Count;
            buttons.AddRange(primary);
            for (var i = 0; i < Mathf.Max(buttons.Count, panel.Buttons.Count); i++)
            {
                if (i >= buttons.Count)
                {
                    Ui.SetActive(panel.Buttons[i].Box, false);
                    continue;
                }

                if (i == panel.Buttons.Count)
                {
                    panel.Buttons.Add(NewFramedButton("Button", panel.Box.rectTransform, size * 0.9f));
                }

                var button = panel.Buttons[i];
                button.Text.text = buttons[i].Label;
                button.Text.color = buttons[i].Off ? Faded(0.45f) : like.color;
                button.Box.color = Faded(buttons[i].Active ? 0.3f : 0.06f);
                Ui.TintOutline(button.Frame, Faded(buttons[i].Off ? 0.4f : 0.85f));
                button.Spot.Click = buttons[i].Off ? null : buttons[i].Click;
                Ui.SetActive(button.Box, true);
                if (i < normal)
                {
                    PinTop(button.Rect, inner + (i % perLine) * (cellWidth + gap), y + (i / perLine) * size * 1.75f, cellWidth, size * 1.45f);
                }
                else
                {
                    button.Box.color = Faded(buttons[i].Off ? 0.06f : primary.Count > 2 ? 0.06f : 0.22f);
                    PinTop(button.Rect, inner, height - inner - primaryHeight + (i - normal) * primaryStep, wide, primaryStep - size * 0.25f);
                }
            }

            y += buttonLines * size * 1.75f;

            var used = 0;
            for (var g = 0; g < groups.Count; g++)
            {
                if (!string.IsNullOrEmpty(groups[g].Key))
                {
                    while (g >= panel.Headings.Count)
                    {
                        var heading = Ui.NewText("Heading", panel.Box.rectTransform, like, size * 0.75f);
                        heading.color = Faded(0.7f);
                        panel.Headings.Add(heading);
                    }

                    panel.Headings[g].text = groups[g].Key;
                    Ui.SetActive(panel.Headings[g], true);
                    PinTop(panel.Headings[g].rectTransform, inner, y, wide, size * 1f);
                    y += size * 1.1f;
                }
                else if (g < panel.Headings.Count)
                {
                    Ui.SetActive(panel.Headings[g], false);
                }

                var fit = groupLines[g];
                var cell = (wide - (fit - 1) * gap) / fit;
                var list = groups[g].Value;
                var narrow = size * 2.2f;
                var narrowCount = list.Count(each => each.Narrow);
                var shared = list.Count > narrowCount ? (wide - (list.Count - 1) * gap - narrowCount * narrow) / (list.Count - narrowCount) : 0f;
                var x = inner;
                for (var i = 0; i < list.Count; i++)
                {
                    if (used == panel.GroupButtons.Count)
                    {
                        panel.GroupButtons.Add(NewFramedButton("Button", panel.Box.rectTransform, size * 0.9f));
                    }

                    var tool = list[i];
                    var button = panel.GroupButtons[used++];
                    button.Text.text = tool.Label;
                    button.Text.color = tool.Off ? Faded(0.45f) : like.color;
                    button.Box.color = Faded(tool.Active ? 0.3f : 0.06f);
                    Ui.TintOutline(button.Frame, Faded(tool.Off ? 0.4f : 0.85f));
                    button.Spot.Click = tool.Off ? null : tool.Click;
                    button.Text.overflowMode = TextOverflowModes.Ellipsis;
                    Ui.SetActive(button.Box, true);
                    if (narrowCount > 0)
                    {
                        var across = tool.Narrow ? narrow : shared;
                        PinTop(button.Rect, x, y, across, size * 1.45f);
                        x += across + gap;
                    }
                    else
                    {
                        PinTop(button.Rect, inner + (i % fit) * (cell + gap), y + (i / fit) * size * 1.75f, cell, size * 1.45f);
                    }
                }

                y += (list.Count + fit - 1) / fit * size * 1.75f + size * 0.3f;
            }

            for (var i = used; i < panel.GroupButtons.Count; i++)
            {
                Ui.SetActive(panel.GroupButtons[i].Box, false);
            }

            for (var g = groups.Count; g < panel.Headings.Count; g++)
            {
                Ui.SetActive(panel.Headings[g], false);
            }

            panel.Foot.text = foot;
            Ui.SetActive(panel.Foot, foot.Length > 0);
            if (foot.Length > 0)
            {
                PinTop(panel.Foot.rectTransform, inner, Mathf.Max(y, height - inner - size * 1f - (primary.Count > 0 ? primaryHeight + size * 0.2f : 0f)), wide, size * 1f);
            }
        }

        private float Measure(string text)
        {
            return typed.GetPreferredValues(text ?? "").x * 0.9f;
        }

        private Panel NewPanel(string name)
        {
            var panel = new Panel { Box = Ui.NewBox(name, root) };
            panel.Box.color = Faded(0.04f);
            Ui.TintOutline(Ui.NewOutline("Frame", panel.Box.rectTransform, Thin()), Faded(0.85f));

            var rect = panel.Box.rectTransform;
            panel.Picture = NewPicture(rect);
            panel.Texture = NewIcon(rect);
            panel.Missing = Ui.NewText("Missing", panel.Texture.rectTransform, like, size * 0.8f);
            panel.Missing.alignment = TextAlignmentOptions.Center;
            panel.Missing.color = Faded(0.5f);
            Ui.Stretch(panel.Missing.rectTransform);
            panel.Title = Ui.NewText("Title", rect, like, size * 1.15f);
            panel.Title.enableAutoSizing = true;
            panel.Title.fontSizeMax = size * 1.15f;
            panel.Title.fontSizeMin = size * 0.6f;
            panel.Text = Ui.NewText("Text", rect, like, size * 0.8f);
            panel.Text.enableWordWrapping = true;
            panel.Text.overflowMode = TextOverflowModes.Ellipsis;
            panel.Text.color = Faded(0.85f);
            panel.Rule = Ui.NewBox("Rule", rect);
            panel.Rule.color = Faded(0.6f);

            for (var i = 0; i < MaxFacts; i++)
            {
                panel.FactLabels[i] = Ui.NewText("Label", rect, like, size * 0.75f);
                panel.FactLabels[i].color = Faded(0.7f);
                panel.FactValues[i] = Ui.NewText("Value", rect, like, size * 0.95f);
                panel.FactValues[i].alignment = TextAlignmentOptions.Right;
                panel.FactValues[i].overflowMode = TextOverflowModes.Ellipsis;
            }

            panel.Foot = Ui.NewText("Foot", rect, like, size * 0.7f);
            panel.Foot.color = Faded(0.7f);
            Ui.SetActive(panel.Box, false);
            return panel;
        }

        private RectTransform NewHolder(RectTransform parent)
        {
            var holder = new GameObject("Holder", typeof(RectTransform));
            holder.layer = parent.gameObject.layer;
            var rect = (RectTransform)holder.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private float LayoutHeads(float top)
        {
            var columns = page.HideHeads ? 0 : page.Columns.Count;

            for (var i = 0; i < columns; i++)
            {
                if (i == heads.Count)
                {
                    var text = Ui.NewText("Head", area, like, size * 0.9f);
                    text.color = Faded(0.85f);
                    heads.Add(new Button { Text = text });
                }

                var column = i;
                var button = heads[i];
                button.Text.text = page.Columns[i].Header;
                button.Text.alignment = page.Columns[i].AlignRight ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                button.Spot.Click = page.SortBy != null ? () => page.SortBy(column) : (Action)null;
                Ui.SetActive(button.Text, true);

                if (i == 0)
                {
                    Ui.PinToTop(button.Rect, size * 0.4f, top, RightOf(0) + charWidth, size * 1.3f);
                }
                else
                {
                    PinRight(button.Rect, RightOf(i), top, widths[i], size * 1.3f);
                }
            }

            for (var i = columns; i < heads.Count; i++)
            {
                Ui.SetActive(heads[i].Text, false);
            }

            Ui.SetActive(headRule, columns > 0);
            Ui.PinToTop(headRule.rectTransform, 0f, top + size * 1.35f, 0f, Thin());

            return columns > 0 ? top + size * 1.6f : top;
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
                measured[value] = width = typed.GetPreferredValues(value).x;
            }

            return width;
        }

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
            var picture = pictures && !entry.IsHeader;
            var indent = size * (sectioned && !entry.IsHeader ? 1.6f : 0.4f) + (picture ? height * 0.95f : 0f);
            var withSub = tall && !entry.IsHeader && !string.IsNullOrEmpty(entry.Sub);

            var picked = page.SelectRows && entry == focused;
            row.Back.color = picked ? Faded(0.3f) : IsBar(entry) ? Faded(0.22f) : shaded && !entry.IsHeader ? Faded(0.05f) : Color.clear;

            row.Label.text = entry.Label;
            row.Label.fontSize = entry.Big ? size * 1.3f : size;
            row.Label.color = entry.Dim ? Faded(0.5f) : colour;
            row.Sub.text = withSub ? entry.Sub : "";
            row.Description.text = columns.Count == 0 ? entry.Description ?? "" : "";
            row.Spot.Click = page.SelectRows && !entry.IsHeader && ClickFor(entry) != null ? () => Select(entry) : ClickFor(entry);
            row.Clickable = row.Spot.Click != null;
            row.Spot.AltClick = AltFor(entry);

            Ui.SetActive(row.PictureFrame, picture);
            if (picture)
            {
                var box = height * 0.8f;
                PinLeft(row.PictureFrame, size * 0.3f, (height - box) / 2f, box, box);
                Ui.SetActive(row.Picture, entry.Picture != null);
                row.Picture.sprite = entry.Picture;
                row.Picture.color = entry.Dim ? Faded(0.5f) : colour;
            }

            // Moving a text makes it rebuild itself, so the pieces are only placed again when the row's shape changes.
            var reshape = row.Version != version || row.Indent != indent || row.CellCount != cells;
            row.Version = version;
            row.Indent = indent;
            row.CellCount = cells;

            if (reshape)
            {
                var right = cells > 0 ? RightOf(0) + charWidth : size * 0.4f;
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
                PlaceMarker(row.Marker, entry.Marker, row.Label, entry.Label, indent, tall ? size * 1.5f : height, row.Label.rectTransform.rect.width);
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
                    cell.alignment = column.AlignRight ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
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

        private void PlaceIcons(Row row, int count, float columnLeft, float height, Color colour)
        {
            var high = size * 0.95f;
            var wide = high * row.Icons[0].texture.width / row.Icons[0].texture.height;

            for (var i = 0; i < count; i++)
            {
                var icon = row.Icons[i];
                icon.color = colour;
                Ui.SetActive(icon, true);
                PinRight(icon.rectTransform, columnLeft - (i + 1) * wide - i * size * 0.25f, (height - high) / 2f, wide, high);
            }
        }

        private void Select(MenuEntry entry)
        {
            focused = entry;
            FillRows();
            DrawPanels();
        }

        private Action AltFor(MenuEntry entry)
        {
            return entry.AltClick;
        }

        private Action ClickFor(MenuEntry entry)
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

        private Row NewRow()
        {
            var holder = new GameObject("Row", typeof(RectTransform));
            holder.layer = list.gameObject.layer;

            var row = new Row { Rect = (RectTransform)holder.transform };
            row.Rect.SetParent(list, false);

            row.Back = Ui.NewBox("Back", row.Rect);
            Ui.Stretch(row.Back.rectTransform);

            row.PictureFrame = NewHolder(row.Rect);
            Ui.TintOutline(Ui.NewOutline("Frame", row.PictureFrame, Thin()), Faded(0.6f));
            row.Picture = NewPicture(row.PictureFrame);
            Ui.Stretch(row.Picture.rectTransform);
            row.Picture.rectTransform.offsetMin = new Vector2(size * 0.2f, size * 0.2f);
            row.Picture.rectTransform.offsetMax = new Vector2(-size * 0.2f, -size * 0.2f);
            Ui.SetActive(row.PictureFrame, false);

            row.Label = Ui.NewText("Label", row.Rect, like, size);

            row.Sub = Ui.NewText("Sub", row.Rect, like, size * 0.75f);
            row.Sub.color = Faded(0.7f);

            row.Description = Ui.NewText("Description", row.Rect, like, size * 0.8f);
            row.Description.color = Faded(0.6f);

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

        private Button NewFramedButton(string name, RectTransform parent, float fontSize)
        {
            var box = Ui.NewBox(name, parent);
            var frame = Ui.NewOutline("Frame", box.rectTransform, Thin());
            var text = Ui.NewText("Label", box.rectTransform, like, fontSize);
            text.alignment = TextAlignmentOptions.Center;
            text.overflowMode = TextOverflowModes.Overflow;
            Ui.Stretch(text.rectTransform);
            var picture = NewPicture(box.rectTransform);
            Ui.SetActive(picture, false);
            return new Button { Box = box, Frame = frame, Text = text, Marker = NewIcon(box.rectTransform), Picture = picture };
        }

        private void PlaceMarker(RawImage marker, Marker kind, TMP_Text text, string label, float left, float height, float room = float.MaxValue)
        {
            var high = size * 0.95f;
            marker.texture = kind == Marker.Here ? CursorArt.Pin() : CursorArt.Padlock();
            marker.color = text.color;
            PinLeft(marker.rectTransform, left + Mathf.Min(text.GetPreferredValues(label).x, room) + size * 0.6f,
                (height - high) / 2f, high * marker.texture.width / marker.texture.height, high);
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

        private static Image NewPicture(RectTransform parent)
        {
            var holder = new GameObject("Picture", typeof(RectTransform), typeof(Image));
            holder.layer = parent.gameObject.layer;
            holder.transform.SetParent(parent, false);

            var image = holder.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private float Thin()
        {
            return Mathf.Max(1f, head * 0.08f);
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

        private static void PinTop(RectTransform rect, float left, float top, float width, float height)
        {
            PinLeft(rect, left, top, width, height);
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
