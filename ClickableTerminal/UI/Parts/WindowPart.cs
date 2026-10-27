using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // A window over the page, closed with its X or BACK, with a shade behind it over what it covers. Its title and X stay; the picture, the text, the line to type into and the buttons under them scroll together inside it.
    internal class WindowPart
    {
        private readonly Kit kit;
        private readonly Terminal terminal;
        private readonly RectTransform root;
        private readonly Image shade;
        private readonly Image box;
        private readonly TextMeshProUGUI title;
        private readonly Image rule;
        private readonly Button close;
        private readonly RectTransform view;
        private readonly RectTransform content;
        private readonly RawImage picture;
        private readonly TextMeshProUGUI missing;
        private readonly TextMeshProUGUI side;
        private readonly TextMeshProUGUI text;
        private readonly Image input;
        private readonly TextMeshProUGUI inputText;
        private readonly Buttons buttons;
        private readonly List<TextMeshProUGUI> headings = new List<TextMeshProUGUI>();
        private Window shown;
        private float scroll;
        private float most;
        private string titleShown;
        private readonly Image track;
        private readonly Image thumb;
        private readonly Grip grip;

        internal bool Open => shown != null;
        internal bool IsApp => shown != null && shown.App;
        internal TMP_Text Text => shown != null ? text : null;

        internal WindowPart(Kit kit, Terminal terminal, RectTransform root)
        {
            this.kit = kit;
            this.terminal = terminal;
            this.root = root;
            var size = kit.Size;

            shade = kit.Box("Shade", root, new Color(0f, 0f, 0f, 0.72f));
            Ui.Stretch(shade.rectTransform);
            Ui.SetActive(shade, false);

            box = kit.Box("Window", root, new Color(0.02f, 0.06f, 0.03f, 0.97f));
            kit.Outline(box.rectTransform, kit.Thin * 1.5f, kit.Faded(0.95f));
            title = kit.Text("Title", box.rectTransform, size * 1.15f);
            title.overflowMode = TextOverflowModes.Ellipsis;
            rule = kit.Box("Rule", box.rectTransform, kit.Faded(0.7f));

            close = kit.Button("Close", box.rectTransform, size);
            close.Text.text = "X";
            close.Box.color = kit.Faded(0.08f);
            Ui.TintOutline(close.Frame, kit.Faded(0.9f));
            close.Spot.Click = Window.Close;

            var holder = new GameObject("View", typeof(RectTransform), typeof(RectMask2D));
            holder.layer = root.gameObject.layer;
            view = (RectTransform)holder.transform;
            view.SetParent(box.rectTransform, false);
            content = kit.Holder("Content", view);

            picture = kit.Icon(content);
            missing = kit.Text("Missing", picture.rectTransform, size * 0.9f);
            missing.text = "[ NO IMAGE ]";
            missing.alignment = TextAlignmentOptions.Center;
            missing.color = kit.Faded(0.5f);
            Ui.Stretch(missing.rectTransform);

            side = kit.Text("Side", content, size * 0.9f);
            side.enableWordWrapping = true;
            side.alignment = TextAlignmentOptions.TopLeft;

            text = kit.Text("Text", content, size * 0.9f);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;

            input = kit.Box("Input", content, new Color(0f, 0f, 0f, 0.5f));
            kit.Outline(input.rectTransform, kit.Thin, kit.Faded(0.9f));
            inputText = kit.Text("Typed", input.rectTransform, size * 1.1f);
            Ui.Stretch(inputText.rectTransform);
            inputText.margin = new Vector4(size * 0.5f, 0f, size * 0.5f, 0f);
            Ui.SetActive(input, false);

            buttons = new Buttons(kit, content, "Button", size);

            // Beside the text when it is longer than the window.
            track = kit.Box("Track", box.rectTransform, kit.Faded(0.25f));
            thumb = kit.Box("Thumb", box.rectTransform, kit.Faded(0.9f));
            grip = new Grip(track.rectTransform, thumb.rectTransform, size * 0.6f, along => ScrollTo(along * most));
            Ui.SetActive(box, false);
        }

        // Follows Window.Open: a new window is drawn, an application's screen is followed as it changes.
        internal void Tick(Rect page)
        {
            if (!ReferenceEquals(Window.Open, shown))
            {
                shown = Window.Open;
                Draw(page);
            }
            else if (shown != null && shown.App)
            {
                var screen = terminal.screenText.text.Trim('\n', '\r');
                if (screen != shown.Text)
                {
                    shown.Text = screen;
                    Draw(page);
                }
            }
            else if (shown != null && shown.Send != null)
            {
                ShowTyped();
            }
        }

        internal void Forget()
        {
            shown = null;
            Ui.SetActive(shade, false);
            Ui.SetActive(box, false);
        }

        internal bool Scroll(int by)
        {
            return ScrollTo(scroll + Mathf.Sign(by) * kit.Size * 1.4f);
        }

        private bool ScrollTo(float to)
        {
            var was = scroll;
            scroll = Mathf.Clamp(to, 0f, most);
            content.anchoredPosition = new Vector2(0f, scroll);
            PlaceThumb();
            return scroll != was;
        }

        private void PlaceThumb()
        {
            var shows = most > 0f;
            Ui.SetActive(track, shows);
            Ui.SetActive(thumb, shows);
            if (!shows)
            {
                return;
            }

            var r = view.rect;
            var length = Mathf.Max(kit.Size, r.height * r.height / (r.height + most));
            var x = view.anchoredPosition.x + r.width + kit.Size * 0.35f;
            var top = -view.anchoredPosition.y;
            Kit.Place(track.rectTransform, x, top, kit.Thin * 2f, r.height);
            Kit.Place(thumb.rectTransform, x - kit.Thin, top + (r.height - length) * scroll / most, kit.Thin * 4f, length);
        }

        // A window with nothing to step between scrolls with the arrows.
        internal bool ScrollFrom(bool down)
        {
            return shown.Buttons.Count == 0 && shown.Groups.Count == 0 && Scroll(down ? 1 : -1);
        }

        internal bool InView(Vector3 world)
        {
            return shown != null && view.rect.Contains(view.InverseTransformPoint(world));
        }

        internal string Screen()
        {
            return terminal.screenText.text.Trim('\n', '\r');
        }

        internal HotSpot SpotAt(Vector3 world)
        {
            var held = grip.At(world);
            if (held != null)
            {
                return held;
            }

            if (close.Spot.Click != null && MenuView.Under(close.Rect, world))
            {
                return MenuView.At(close.Spot, close.Rect);
            }

            foreach (var button in buttons.All)
            {
                if (button.Shown && button.Spot.Click != null && InView(button) && MenuView.Under(button.Rect, world))
                {
                    return MenuView.At(button.Spot, button.Rect);
                }
            }

            return null;
        }

        // Only what is scrolled into view can be clicked or stepped onto.
        internal void Targets(RectTransform frame, Steering steering)
        {
            steering.Add(frame, close.Rect, Zone.Window);
            foreach (var button in buttons.All)
            {
                if (button.Shown && button.Spot.Click != null && InView(button))
                {
                    steering.Add(frame, button.Rect, Zone.Window);
                }
            }

            steering.Area(frame, box.rectTransform, box.rectTransform.rect, Zone.Window);
        }

        private bool InView(Button button)
        {
            var center = view.InverseTransformPoint(button.Rect.TransformPoint(button.Rect.rect.center));
            return view.rect.Contains(center);
        }

        private void ShowTyped()
        {
            var typed = Commands.Typed(terminal);
            if (typed.Length > shown.Longest)
            {
                typed = typed.Substring(0, shown.Longest);
            }

            var line = "> " + typed + "_";
            if (inputText.text != line)
            {
                inputText.text = line;
            }
        }

        private float Width(ToolButton tool)
        {
            return tool.Narrow ? kit.Size * 2.2f : kit.Measure(tool.Label) + (tool.Bare ? kit.Size * 0.8f : kit.Size * 1.6f);
        }

        private TextMeshProUGUI Heading(ref int used)
        {
            if (used == headings.Count)
            {
                headings.Add(kit.Text("Heading", content, kit.Size * 0.8f));
            }

            var heading = headings[used++];
            Ui.SetActive(heading, true);
            return heading;
        }

        // The page's area, from under the header to above the footer, is where the window is centred.
        internal void Draw(Rect page)
        {
            shade.rectTransform.SetAsLastSibling();
            box.rectTransform.SetAsLastSibling();
            Ui.SetActive(shade, shown != null);
            Ui.SetActive(box, shown != null);
            if (shown == null)
            {
                return;
            }

            var size = kit.Size;
            var width = Mathf.Min(page.width - kit.Margin * 2f, size * 32f);
            var inner = size * 0.8f;
            var textWidth = width - inner * 2f;
            var headHeight = size * 2.3f;
            var y = 0f;
            var titles = 0;
            buttons.Start();

            void Place(ToolButton tool, float x, float top, float wide)
            {
                var button = buttons.Next();
                kit.Style(button, tool);
                Kit.Place(button.Rect, x, top, wide, size * 1.5f);
            }

            var hasPicture = shown.Texture != null || shown.NoPicture;
            Ui.SetActive(picture, hasPicture);
            Ui.SetActive(missing, shown.Texture == null);
            side.text = shown.Side ?? "";
            Ui.SetActive(side, side.text.Length > 0);
            if (hasPicture || side.text.Length > 0)
            {
                // A picture on the left, the lines that go with it on its right.
                var aspect = shown.Texture != null ? shown.Texture.width / Mathf.Max(1f, shown.Texture.height) : 4f / 3f;
                var across = hasPicture ? Mathf.Min(size * 9f, textWidth * 0.45f) : 0f;
                var sideLeft = hasPicture ? across + size * 0.8f : 0f;
                var sideHeight = side.text.Length > 0 ? side.GetPreferredValues(side.text, textWidth - sideLeft, 0f).y : 0f;
                var down = 0f;
                if (hasPicture)
                {
                    picture.texture = shown.Texture != null ? shown.Texture : Texture2D.blackTexture;
                    down = shown.Texture != null ? across / aspect : across * 0.75f;
                    Kit.Place(picture.rectTransform, 0f, 0f, across, down);
                }

                Kit.Place(side.rectTransform, sideLeft, 0f, textWidth - sideLeft, sideHeight);
                y += Mathf.Max(down, sideHeight) + size * 0.8f;
            }

            // An application's screen is not wrapped: its letters get smaller until its widest line fits.
            text.text = shown.Text ?? "";
            text.enableWordWrapping = !shown.App;
            text.fontSize = size * 0.9f;
            if (shown.App && text.text.Length > 0)
            {
                var widest = text.GetPreferredValues(text.text).x;
                if (widest > textWidth)
                {
                    text.fontSize = size * 0.9f * textWidth / widest;
                }
            }

            var textHeight = text.text.Length > 0 ? text.GetPreferredValues(text.text, textWidth, 0f).y : 0f;
            Kit.Place(text.rectTransform, 0f, y, textWidth, textHeight);
            if (textHeight > 0f)
            {
                y += textHeight + size * 0.6f;
            }

            Ui.SetActive(input, shown.Send != null);
            if (shown.Send != null)
            {
                Kit.Place(input.rectTransform, 0f, y, textWidth, size * 1.9f);
                ShowTyped();
                y += size * 2.5f;
            }

            var sets = new List<KeyValuePair<string, List<ToolButton>>>(shown.Groups);
            if (shown.Buttons.Count > 0)
            {
                sets.Add(new KeyValuePair<string, List<ToolButton>>(null, shown.Buttons));
            }

            foreach (var set in sets)
            {
                // On one line: the title on the left, the buttons at the right end (the cart's rows).
                if (shown.GroupsInLine && !string.IsNullOrEmpty(set.Key))
                {
                    var row = Heading(ref titles);
                    row.text = set.Key;
                    row.color = kit.Like.color;
                    row.fontSize = size * 0.9f;

                    var end = textWidth;
                    for (var b = set.Value.Count - 1; b >= 0; b--)
                    {
                        var wide = Width(set.Value[b]);
                        end -= wide;
                        Place(set.Value[b], end, y, wide);
                        end -= size * 0.3f;
                    }

                    Kit.Place(row.rectTransform, 0f, y + size * 0.15f, end - size * 0.3f, size * 1.3f);
                    y += size * 1.75f;
                    continue;
                }

                if (!string.IsNullOrEmpty(set.Key))
                {
                    var heading = Heading(ref titles);
                    heading.text = set.Key;
                    heading.color = kit.Faded(0.7f);
                    heading.fontSize = size * 0.8f;
                    Kit.Place(heading.rectTransform, 0f, y, textWidth, size * 1.1f);
                    y += size * 1.3f;
                }

                var x = 0f;
                foreach (var tool in set.Value)
                {
                    var wide = Mathf.Min(Width(tool), textWidth);
                    if (x > 0f && x + wide > textWidth)
                    {
                        x = 0f;
                        y += size * 1.8f;
                    }

                    Place(tool, x, y, wide);
                    x += wide + size * 0.5f;
                }

                if (set.Value.Count > 0)
                {
                    y += size * 2.2f;
                }
            }

            // At the right end of the last line of buttons, or on a line of their own (the cart's SAVE CART).
            if (shown.RightButtons.Count > 0)
            {
                var line = shown.Buttons.Count > 0 ? y - size * 2.2f : y;
                var end = textWidth;
                for (var b = shown.RightButtons.Count - 1; b >= 0; b--)
                {
                    var wide = Width(shown.RightButtons[b]);
                    end -= wide;
                    Place(shown.RightButtons[b], end, line, wide);
                    end -= size * 0.5f;
                }

                if (shown.Buttons.Count == 0)
                {
                    y += size * 2.2f;
                }
            }

            buttons.Done();
            for (var i = titles; i < headings.Count; i++)
            {
                Ui.SetActive(headings[i], false);
            }

            var contentHeight = Mathf.Max(size, y - (sets.Count > 0 || shown.RightButtons.Count > 0 ? size * 0.4f : size * 0.6f));

            // A fixed most it can grow to. What does not fit scrolls.
            var viewHeight = Mathf.Min(contentHeight, page.height - headHeight - inner);
            var height = headHeight + viewHeight + inner;
            Kit.Place(box.rectTransform, page.x + (page.width - width) / 2f, page.y + (page.height - height) / 2f, width, height);

            title.text = shown.Title ?? "";
            Kit.Place(title.rectTransform, inner, size * 0.45f, width - inner * 2f - size * 2f, size * 1.4f);
            Kit.PlaceRight(close.Rect, inner * 0.6f, size * 0.4f, size * 1.6f, size * 1.5f);
            Kit.Place(rule.rectTransform, inner * 0.6f, size * 2.05f, width - inner * 1.2f, kit.Thin);
            Kit.Place(view, inner, headHeight, textWidth, viewHeight);

            // Drawn again after a click inside it, it stays where it was scrolled to.
            most = Mathf.Max(0f, contentHeight - viewHeight);
            scroll = shown.Title == titleShown ? Mathf.Min(scroll, most) : 0f;
            titleShown = shown.Title;
            Kit.Place(content, 0f, -scroll, textWidth, contentHeight);
            PlaceThumb();
        }
    }
}
