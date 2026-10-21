using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    internal static class Ui
    {
        internal static Image NewBox(string name, RectTransform parent)
        {
            var box = new GameObject(name, typeof(RectTransform), typeof(Image));
            box.layer = parent.gameObject.layer;

            var rect = (RectTransform)box.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;

            var image = box.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        internal static TextMeshProUGUI NewText(string name, RectTransform parent, TMP_Text like, float size)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.layer = parent.gameObject.layer;
            holder.transform.SetParent(parent, false);

            var text = holder.AddComponent<TextMeshProUGUI>();
            text.font = like.font;
            text.fontSharedMaterial = like.fontSharedMaterial;
            text.fontSize = size;
            text.color = like.color;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
        }

        // Four lines, so it stays as thin as asked at any size.
        internal static RectTransform NewOutline(string name, RectTransform parent, float thickness)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.layer = parent.gameObject.layer;
            var rect = (RectTransform)holder.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            Edge(rect, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, thickness));
            Edge(rect, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, thickness));
            Edge(rect, Vector2.zero, new Vector2(0f, 1f), new Vector2(thickness, 0f));
            Edge(rect, new Vector2(1f, 0f), Vector2.one, new Vector2(thickness, 0f));
            return rect;
        }

        private static void Edge(RectTransform parent, Vector2 min, Vector2 max, Vector2 size)
        {
            var line = NewBox("Edge", parent);
            var rect = line.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2((min.x + max.x) / 2f, (min.y + max.y) / 2f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        internal static void TintOutline(RectTransform outline, Color color)
        {
            if (outline == null)
            {
                return;
            }

            foreach (var edge in outline.GetComponentsInChildren<Image>(true))
            {
                edge.color = color;
            }
        }

        // Scale makes it smaller than the terminal's letters, centred on the same line.
        internal static RectTransform NewButton(string name, string label, RectTransform parent, TMP_Text like, float right, float width = 5.5f, float scale = 1f)
        {
            var size = like.fontSize * scale;

            var box = NewBox(name, parent);
            box.color = new Color(0f, 0f, 0f, 0.45f);
            TintOutline(NewOutline("Frame", box.rectTransform, Mathf.Max(1f, size * 0.09f)), like.color);

            var rect = box.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(size * width, size * 1.5f);
            rect.anchoredPosition = new Vector2(-right, -like.fontSize * 0.3f - (like.fontSize - size) * 0.75f);

            var text = NewText("Label", rect, like, size);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            Stretch(text.rectTransform);
            return rect;
        }

        // The text's own area is wider than the box the game draws behind it.
        internal static RectTransform CreditsBox(TMP_Text credits)
        {
            if (credits == null)
            {
                return null;
            }

            var corners = new Vector3[4];
            credits.rectTransform.GetWorldCorners(corners);
            var start = (corners[0] + corners[1]) / 2f + (corners[3] - corners[0]).normalized * (credits.fontSize * 0.5f * credits.rectTransform.lossyScale.x);

            RectTransform best = null;
            var bestArea = float.MaxValue;

            for (var level = credits.transform.parent; level != null && level.parent != null; level = level.parent)
            {
                foreach (var graphic in level.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic == credits || graphic is TMP_Text)
                    {
                        continue;
                    }

                    var rect = graphic.rectTransform;
                    var local = rect.InverseTransformPoint(start);
                    var area = rect.rect.width * rect.rect.height;
                    if (rect.rect.Contains(local) && area > 0f && area < bestArea)
                    {
                        best = rect;
                        bestArea = area;
                    }
                }

                if (best != null)
                {
                    break;
                }
            }

            return best;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal static void PinToTop(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(-left - right, height);
        }

        internal static void Place(RectTransform rect, Rect area)
        {
            rect.localPosition = new Vector3(area.xMin, area.yMin, 0f);
            rect.sizeDelta = area.size;
        }

        internal static void SetActive(Component component, bool active)
        {
            if (component != null && component.gameObject.activeSelf != active)
            {
                component.gameObject.SetActive(active);
            }
        }
    }
}
