using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // The sizes, colours and pieces a page is built from, for one letter size. Every part of a page places its pieces with Place, in its parent's coordinates measured from the top left corner.
    internal class Kit
    {
        internal readonly TMP_Text Like;
        internal readonly float Head;
        internal readonly float Size;
        internal readonly float Margin;
        internal readonly float CharWidth;
        internal readonly float Scale;
        private readonly TextMeshProUGUI measurer;

        internal Kit(TMP_Text like, float scale, RectTransform root)
        {
            Like = like;
            Scale = scale;

            // The top row uses the terminal's own letter size, because its buttons do not shrink.
            Head = like.fontSize;
            Size = Head * scale;
            Margin = Size * 0.6f;
            CharWidth = like.GetPreferredValues("0000000000").x / 10f * scale;

            // Never shown: it only measures text at the page's letter size.
            measurer = Text("Measure", root, Size);
            measurer.color = Color.clear;
            Place(measurer.rectTransform, 0f, 0f, 0f, 0f);
        }

        internal float Thin => Mathf.Max(1f, Head * 0.08f);

        internal Color Faded(float alpha)
        {
            return new Color(Like.color.r, Like.color.g, Like.color.b, alpha);
        }

        // As wide as the text is at the page's letter size.
        internal float Width(string text)
        {
            return measurer.GetPreferredValues(text ?? "").x;
        }

        internal float Measure(string text)
        {
            return Width(text) * 0.9f;
        }

        internal TextMeshProUGUI Text(string name, RectTransform parent, float size)
        {
            return Ui.NewText(name, parent, Like, size);
        }

        internal Image Box(string name, RectTransform parent, Color color)
        {
            var box = Ui.NewBox(name, parent);
            box.color = color;
            return box;
        }

        internal RectTransform Outline(RectTransform parent, float thickness, Color color)
        {
            var outline = Ui.NewOutline("Frame", parent, thickness);
            Ui.TintOutline(outline, color);
            return outline;
        }

        internal Image FramedBox(string name, RectTransform parent, float fill, float frame)
        {
            var box = Box(name, parent, Faded(fill));
            Outline(box.rectTransform, Thin, Faded(frame));
            return box;
        }

        internal RectTransform Holder(string name, RectTransform parent)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.layer = parent.gameObject.layer;
            var rect = (RectTransform)holder.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        internal RawImage Icon(RectTransform parent)
        {
            var holder = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
            holder.layer = parent.gameObject.layer;
            holder.transform.SetParent(parent, false);
            holder.SetActive(false);

            var icon = holder.GetComponent<RawImage>();
            icon.raycastTarget = false;
            return icon;
        }

        internal Image Picture(RectTransform parent)
        {
            var holder = new GameObject("Picture", typeof(RectTransform), typeof(Image));
            holder.layer = parent.gameObject.layer;
            holder.transform.SetParent(parent, false);

            var image = holder.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        internal Button Button(string name, RectTransform parent, float fontSize)
        {
            var box = Ui.NewBox(name, parent);
            var frame = Ui.NewOutline("Frame", box.rectTransform, Thin);
            var text = Text("Label", box.rectTransform, fontSize);
            text.alignment = TextAlignmentOptions.Center;
            text.overflowMode = TextOverflowModes.Overflow;
            Ui.Stretch(text.rectTransform);
            var picture = Picture(box.rectTransform);
            Ui.SetActive(picture, false);
            return new Button { Box = box, Frame = frame, Text = text, Marker = Icon(box.rectTransform), Picture = picture };
        }

        // A button as a ToolButton asks for: greyed out, lit, or bare (only its label).
        internal void Style(Button button, ToolButton tool, float fill = 0.06f)
        {
            button.Text.text = tool.Label;
            button.Text.color = tool.Off ? Faded(0.45f) : Like.color;
            button.Box.color = tool.Bare ? Color.clear : Faded(tool.Active ? 0.3f : fill);
            Ui.TintOutline(button.Frame, tool.Bare ? Color.clear : Faded(tool.Off ? 0.4f : 0.85f));
            button.Spot.Click = tool.Off || tool.Bare ? null : tool.Click;
            Ui.SetActive(button.Box, true);
        }

        internal void Marker(RawImage marker, Marker kind, TMP_Text text, string label, float left, float height, float room = float.MaxValue)
        {
            var high = Size * 0.95f;
            marker.texture = kind == ClickableTerminal.Marker.Here ? CursorArt.Pin() : CursorArt.Padlock();
            marker.color = text.color;
            Place(marker.rectTransform, left + Mathf.Min(text.GetPreferredValues(label).x, room) + Size * 0.6f,
                (height - high) / 2f, high * marker.texture.width / marker.texture.height, high);
        }

        internal static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static void Place(RectTransform rect, Rect area)
        {
            Place(rect, area.x, area.y, area.width, area.height);
        }

        // Measured from the right edge of the parent, for pieces that keep to the right when the parent grows.
        internal static void PlaceRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }
    }

    // A scrollbar to take hold of: its track, the thumb on it, and how far beside the track it still counts.
    internal class Grip
    {
        internal readonly HotSpot Spot = new HotSpot();
        private readonly RectTransform track;
        private readonly RectTransform thumb;
        private readonly float reach;

        internal Grip(RectTransform track, RectTransform thumb, float reach, Action<float> moveTo)
        {
            this.track = track;
            this.thumb = thumb;
            this.reach = reach;
            Spot.Space = thumb;
            Spot.Drag = world => moveTo(Along(world));

            // Held, the thumb turns darker, so it shows that it follows the cursor.
            var look = thumb.GetComponent<UnityEngine.UI.Graphic>();
            var normal = Color.clear;
            Spot.Holding = held =>
            {
                if (look == null)
                {
                    return;
                }

                if (held)
                {
                    normal = look.color;
                    look.color = Color.Lerp(normal, Color.black, 0.45f);
                }
                else if (normal != Color.clear)
                {
                    look.color = normal;
                }
            };
        }

        internal HotSpot At(Vector3 world)
        {
            if (!thumb.gameObject.activeInHierarchy || !track.gameObject.activeInHierarchy)
            {
                return null;
            }

            var r = track.rect;
            var wide = Rect.MinMaxRect(r.xMin - reach, r.yMin, r.xMax + reach, r.yMax);
            if (!wide.Contains(track.InverseTransformPoint(world)))
            {
                return null;
            }

            Spot.Area = thumb.rect;
            return Spot;
        }

        // From 0 at the top of the track to 1 at the bottom, where the middle of the thumb would be under the point.
        internal float Along(Vector3 world)
        {
            var r = track.rect;
            var length = thumb.rect.height;
            var y = track.InverseTransformPoint(world).y;
            return Mathf.Clamp01((r.yMax - y - length / 2f) / Mathf.Max(1f, r.height - length));
        }
    }

    internal class Button
    {
        internal Image Box;
        internal RectTransform Frame;
        internal TextMeshProUGUI Text;
        internal RawImage Marker;
        internal Image Picture;
        internal readonly HotSpot Spot = new HotSpot();

        internal RectTransform Rect => Box != null ? Box.rectTransform : Text.rectTransform;

        internal bool Shown => Rect.gameObject.activeInHierarchy;
    }

    // A pool of buttons under one parent: Next hands out the next one, Done hides the ones not used this time.
    internal class Buttons
    {
        private readonly Kit kit;
        private readonly RectTransform parent;
        private readonly string name;
        private readonly float fontSize;
        internal readonly System.Collections.Generic.List<Button> All = new System.Collections.Generic.List<Button>();
        internal int Used { get; private set; }

        internal Buttons(Kit kit, RectTransform parent, string name, float fontSize)
        {
            this.kit = kit;
            this.parent = parent;
            this.name = name;
            this.fontSize = fontSize;
        }

        internal void Start()
        {
            Used = 0;
        }

        internal Button Next()
        {
            if (Used == All.Count)
            {
                All.Add(kit.Button(name, parent, fontSize));
            }

            return All[Used++];
        }

        internal void Done()
        {
            for (var i = Used; i < All.Count; i++)
            {
                Ui.SetActive(All[i].Box, false);
                All[i].Spot.Click = null;
            }
        }
    }

    // Cutting a rectangle into parts, measured from the top left corner like Kit.Place.
    internal static class Cut
    {
        internal static Rect Top(ref Rect area, float height)
        {
            var part = new Rect(area.x, area.y, area.width, height);
            area = new Rect(area.x, area.y + height, area.width, area.height - height);
            return part;
        }

        internal static Rect Bottom(ref Rect area, float height)
        {
            var part = new Rect(area.x, area.yMax - height, area.width, height);
            area = new Rect(area.x, area.y, area.width, area.height - height);
            return part;
        }

        internal static Rect Left(ref Rect area, float width)
        {
            var part = new Rect(area.x, area.y, width, area.height);
            area = new Rect(area.x + width, area.y, area.width - width, area.height);
            return part;
        }

        internal static Rect Right(ref Rect area, float width)
        {
            var part = new Rect(area.xMax - width, area.y, width, area.height);
            area = new Rect(area.x, area.y, area.width - width, area.height);
            return part;
        }

        internal static Rect Inset(Rect area, float left, float top, float right, float bottom)
        {
            return new Rect(area.x + left, area.y + top, area.width - left - right, area.height - top - bottom);
        }
    }
}
