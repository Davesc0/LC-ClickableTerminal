using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // The rule at the bottom, what is being typed under it, and on the right the page's hint and where the list is.
    internal class FooterPart
    {
        private readonly Kit kit;
        private readonly TextMeshProUGUI typed;
        private readonly TextMeshProUGUI position;
        private string hint;
        private string where = "";
        private string[] hintFor;
        private TMP_SpriteAsset hintIcons;

        // How much of the page the footer takes, from the bottom.
        internal float Height => kit.Size * 2.3f;

        internal FooterPart(Kit kit, RectTransform root)
        {
            this.kit = kit;
            var rule = kit.Box("FootRule", root, kit.Faded(0.8f));
            var rect = rule.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(kit.Margin, kit.Size * 1.9f);
            rect.sizeDelta = new Vector2(-kit.Margin * 2f, kit.Thin);

            typed = kit.Text("Typed", root, kit.Size);
            AlongBottom(typed.rectTransform);

            position = kit.Text("Position", root, kit.Size * 0.85f);
            position.alignment = TextAlignmentOptions.Right;
            position.color = kit.Faded(0.75f);
            AlongBottom(position.rectTransform);
        }

        internal void SetTyped(string text)
        {
            var shown = "> " + text + "_";
            if (typed.text != shown)
            {
                typed.text = shown;
            }
        }

        internal void SetHint(string pageHint)
        {
            hint = pageHint;
            Draw();
        }

        internal void SetWhere(string text)
        {
            where = text ?? "";
            Draw();
        }

        // The hint names the buttons of whatever the player is steering with, so it follows the controller.
        internal void Tick()
        {
            if (!ReferenceEquals(hintFor, Pad.Names) || (hintFor != null && !ReferenceEquals(hintIcons, PadIcons.Sheet)))
            {
                Draw();
            }
        }

        private void Draw()
        {
            hintFor = Pad.Names;
            hintIcons = PadIcons.Sheet;
            if (hintIcons != null && position.spriteAsset != hintIcons)
            {
                position.spriteAsset = hintIcons;
            }

            position.text = string.IsNullOrEmpty(hint) ? where : Pad.Words(hint) + "   " + where;
        }

        private void AlongBottom(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(kit.Margin, kit.Size * 0.4f);
            rect.sizeDelta = new Vector2(-kit.Margin * 2f, kit.Size * 1.3f);
        }
    }
}
