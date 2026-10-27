using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // The buttons that are there on every screen: - and + for the letter size, BACK and MENU on the top row, and CONTINUE under pages that only wait for the player.
    internal class TopButtons
    {
        internal const float Scale = 0.75f;

        internal readonly HotSpot Back = new HotSpot();
        internal readonly HotSpot Menu = new HotSpot();
        internal readonly HotSpot Continue = new HotSpot();
        internal readonly HotSpot Smaller = new HotSpot();
        internal readonly HotSpot Bigger = new HotSpot();
        private readonly HotSpot[] all;
        private readonly TextMeshProUGUI continueLabel;
        private readonly float size;

        internal TopButtons(RectTransform frame, TMP_Text text)
        {
            size = text.fontSize;
            Menu.Space = Ui.NewButton("ClickableTerminalMenu", "[ MENU ]", frame, text, size * 0.6f, 5.8f, Scale);
            Back.Space = Ui.NewButton("ClickableTerminalBack", "[ BACK ]", frame, text, size * 5.35f, 5.8f, Scale);
            Smaller.Space = Ui.NewButton("ClickableTerminalSmaller", "-", frame, text, size * 11.85f, 1.9f, Scale);
            Bigger.Space = Ui.NewButton("ClickableTerminalBigger", "+", frame, text, size * 10.1f, 1.9f, Scale);

            Continue.Space = Ui.NewButton("ClickableTerminalContinue", "[ CONTINUE ]", frame, text, 0f);
            Continue.Space.anchorMin = Continue.Space.anchorMax = Continue.Space.pivot = new Vector2(0.5f, 0f);
            Continue.Space.sizeDelta = new Vector2(size * 9f, size * 1.7f);
            Continue.Space.anchoredPosition = new Vector2(0f, size * 3f);
            continueLabel = Continue.Space.GetComponentInChildren<TextMeshProUGUI>(true);

            all = new[] { Back, Menu, Continue, Smaller, Bigger };
        }

        internal void Show(bool back, bool menu, string continueText, bool sizes, Color color)
        {
            Ui.SetActive(Back.Space, back);
            Ui.SetActive(Menu.Space, menu);
            Ui.SetActive(Continue.Space, continueText != null);
            Ui.SetActive(Smaller.Space, sizes);
            Ui.SetActive(Bigger.Space, sizes);

            PadIcon(Back, PadIcons.East, "BACK", color);
            PadIcon(Menu, PadIcons.Select, "MENU", color);

            if (continueText != null && continueLabel.text != continueText)
            {
                continueLabel.text = continueText;
                Continue.Space.sizeDelta = new Vector2(size * (continueText.Length * 0.62f + 1f), size * 1.7f);
            }
        }

        internal HotSpot At(Vector3 world)
        {
            foreach (var button in all)
            {
                if (Under(button, world))
                {
                    return button;
                }
            }

            return null;
        }

        internal static bool Under(HotSpot button, Vector3 world)
        {
            if (button.Space == null || !button.Space.gameObject.activeSelf)
            {
                return false;
            }

            button.Area = button.Space.rect;
            return button.Area.Contains(button.Space.InverseTransformPoint(world));
        }

        internal void Targets(RectTransform frame, Steering steering)
        {
            foreach (var button in all)
            {
                if (button.Space.gameObject.activeSelf)
                {
                    steering.Add(frame, button.Space, button == Continue ? Zone.List : Zone.Header);
                }
            }
        }

        // The controller's button in front of the word, while a controller is in use.
        private static void PadIcon(HotSpot button, int pad, string word, Color color)
        {
            var label = button.Space.GetComponentInChildren<TextMeshProUGUI>(true);
            var icon = button.Space.Find("PadIcon")?.GetComponent<Image>();
            var sprite = Pad.InUse ? PadIcons.Get(pad) : null;

            if (icon == null)
            {
                var holder = new GameObject("PadIcon", typeof(RectTransform), typeof(Image));
                holder.layer = button.Space.gameObject.layer;
                holder.transform.SetParent(button.Space, false);
                icon = holder.GetComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;

                var rect = icon.rectTransform;
                var height = button.Space.rect.height * 0.8f;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(height, height);
                rect.anchoredPosition = new Vector2(height * 0.35f, 0f);
            }

            icon.enabled = sprite != null;
            icon.sprite = sprite;
            icon.color = color;

            var text = sprite != null ? word : "[ " + word + " ]";
            if (label.text != text)
            {
                label.text = text;
                label.margin = sprite != null ? new Vector4(icon.rectTransform.sizeDelta.x * 1.2f, 0f, 0f, 0f) : Vector4.zero;
            }
        }
    }
}
