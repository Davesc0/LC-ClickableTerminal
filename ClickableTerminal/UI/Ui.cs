using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    internal static class Ui
    {
        // A plain rectangle. Its position is set in the parent's own coordinates, see Place.
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

        // Text in the terminal's own font and colour.
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

        // A dark button with a label, pinned to the top right corner of its parent.
        internal static RectTransform NewButton(string name, string label, RectTransform parent, TMP_Text like, float right, float width = 5.5f)
        {
            var size = like.fontSize;

            var box = NewBox(name, parent);
            box.color = new Color(0f, 0f, 0f, 0.7f);

            var rect = box.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(size * width, size * 1.5f);
            rect.anchoredPosition = new Vector2(-right, -size * 0.3f);

            var text = NewText("Label", rect, like, size);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            Stretch(text.rectTransform);
            return rect;
        }

        // The shaded box the game draws behind its credits. The text's own area is wider than the box.
        // Looked for among the text's parents and their children: the smallest picture the text starts inside of.
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

        // A strip across the parent, measured down from its top edge.
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
