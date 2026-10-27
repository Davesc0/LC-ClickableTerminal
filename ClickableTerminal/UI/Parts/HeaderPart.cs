using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // "> TITLE" with the subtitle under it, our own credits box beside the top buttons, the time on the moon under them, and the rule across. The game's own credits are put away while one of our pages is up.
    internal class HeaderPart
    {
        private readonly Kit kit;
        private readonly Terminal terminal;
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI subtitle;
        private readonly TextMeshProUGUI clock;
        private readonly Image creditsBox;
        private readonly TextMeshProUGUI credits;
        private readonly Image rule;
        private int shownCredits = int.MinValue;

        private Graphic gameCreditsBox;
        private bool gameCreditsHidden;

        // Where the header ends and the page begins.
        internal float Bottom => kit.Head * 3f;

        internal HeaderPart(Kit kit, Terminal terminal, RectTransform root)
        {
            this.kit = kit;
            this.terminal = terminal;
            var head = kit.Head;

            title = kit.Text("Title", root, head * 1.5f);
            title.enableAutoSizing = true;
            title.fontSizeMax = head * 1.5f;
            title.fontSizeMin = head * 0.6f;
            title.overflowMode = TextOverflowModes.Ellipsis;

            subtitle = kit.Text("Subtitle", root, head * 0.7f);
            subtitle.color = kit.Faded(0.75f);

            clock = kit.Text("Clock", root, head * 0.7f);
            clock.alignment = TextAlignmentOptions.Right;

            creditsBox = kit.Box("Credits", root, new Color(0f, 0f, 0f, 0.45f));
            kit.Outline(creditsBox.rectTransform, kit.Thin, kit.Like.color);
            credits = kit.Text("Amount", creditsBox.rectTransform, head * 0.95f);
            credits.alignment = TextAlignmentOptions.Center;
            credits.enableAutoSizing = true;
            credits.fontSizeMax = head * 0.95f;
            credits.fontSizeMin = head * 0.5f;
            Ui.Stretch(credits.rectTransform);
            credits.margin = new Vector4(head * 0.2f, 0f, head * 0.2f, 0f);

            rule = kit.Box("Rule", root, kit.Faded(0.8f));
        }

        internal void Layout(MenuPage page, float width)
        {
            var head = kit.Head;
            var margin = kit.Margin;
            var hasSubtitle = !string.IsNullOrEmpty(page.Subtitle);

            title.text = "> " + page.Title;
            subtitle.text = page.Subtitle ?? "";
            Ui.SetActive(subtitle, hasSubtitle);

            // The title runs from the left edge to the credits' box.
            Kit.Place(title.rectTransform, margin, hasSubtitle ? head * 0.1f : head * 0.35f, width - margin - head * 18.6f, head * 1.7f);
            Kit.Place(subtitle.rectTransform, margin + head * 1.1f, head * 1.75f, width - margin * 2f - head * 6.1f, head * 0.8f);
            Kit.Place(clock.rectTransform, head * 20f, head * 1.75f, width - head * 20f - margin, head * 0.8f);

            // In line with the top buttons, which are a little smaller than the terminal's letters.
            var high = head * 1.5f * TopButtons.Scale;
            Kit.PlaceRight(creditsBox.rectTransform, head * 13.6f, head * 0.3f + (head - head * TopButtons.Scale) * 0.75f, head * 4.6f, high);
            Kit.Place(rule.rectTransform, margin, head * 2.6f, width - margin * 2f, kit.Thin);
            shownCredits = int.MinValue;
        }

        internal void Tick()
        {
            var time = StatusPanel.Clock() ?? "";
            if (clock.text != time)
            {
                clock.text = time;
            }

            if (terminal.groupCredits != shownCredits)
            {
                shownCredits = terminal.groupCredits;
                credits.text = "$" + shownCredits;
            }
        }

        internal void ShowGameCredits(bool show)
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
    }
}
