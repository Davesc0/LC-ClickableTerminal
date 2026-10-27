using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // A framed panel beside the list: a picture, a title, a few lines of text, "LABEL  value" lines, buttons side by side, rows of buttons under small titles, the main button pinned to the bottom, and a line at the foot. The text gets what room is left, and the picture is big only where there is room.
    internal class PanelPart
    {
        private const int MaxFacts = 8;

        private readonly Kit kit;
        private readonly Image box;
        private readonly Image picture;
        private readonly RawImage texture;
        private readonly TextMeshProUGUI missing;
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI text;
        private readonly Image rule;
        private readonly TextMeshProUGUI[] factLabels = new TextMeshProUGUI[MaxFacts];
        private readonly TextMeshProUGUI[] factValues = new TextMeshProUGUI[MaxFacts];
        private readonly Buttons buttons;
        private readonly Buttons groupButtons;
        private readonly List<TextMeshProUGUI> headings = new List<TextMeshProUGUI>();
        private readonly TextMeshProUGUI foot;

        internal bool Shown => box.gameObject.activeSelf;

        internal PanelPart(Kit kit, RectTransform root, string name)
        {
            this.kit = kit;
            var size = kit.Size;
            box = kit.FramedBox(name, root, 0.04f, 0.85f);
            var rect = box.rectTransform;

            picture = kit.Picture(rect);
            texture = kit.Icon(rect);
            missing = kit.Text("Missing", texture.rectTransform, size * 0.8f);
            missing.alignment = TextAlignmentOptions.Center;
            missing.color = kit.Faded(0.5f);
            Ui.Stretch(missing.rectTransform);

            title = kit.Text("Title", rect, size * 1.15f);
            title.enableAutoSizing = true;
            title.fontSizeMax = size * 1.15f;
            title.fontSizeMin = size * 0.6f;

            text = kit.Text("Text", rect, size * 0.8f);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = kit.Faded(0.85f);

            rule = kit.Box("Rule", rect, kit.Faded(0.6f));

            for (var i = 0; i < MaxFacts; i++)
            {
                factLabels[i] = kit.Text("Label", rect, size * 0.75f);
                factLabels[i].color = kit.Faded(0.7f);
                factValues[i] = kit.Text("Value", rect, size * 0.95f);
                factValues[i].alignment = TextAlignmentOptions.Right;
                factValues[i].overflowMode = TextOverflowModes.Ellipsis;
            }

            buttons = new Buttons(kit, rect, "Button", size * 0.9f);
            groupButtons = new Buttons(kit, rect, "Button", size * 0.9f);
            foot = kit.Text("Foot", rect, size * 0.7f);
            foot.color = kit.Faded(0.7f);
            Ui.SetActive(box, false);
        }

        internal void Place(Rect area)
        {
            Ui.SetActive(box, true);
            Kit.Place(box.rectTransform, area);
            drawn = null;
        }

        // What the panel showed last time. A live panel is asked again twice a second; when nothing has changed only its buttons get the new clicks, and the texts are not laid out again.
        private string drawn;

        private static string Fingerprint(Detail shown)
        {
            if (shown == null)
            {
                return "";
            }

            var text = new System.Text.StringBuilder();
            text.Append(shown.Title).Append('|').Append(shown.Text).Append('|').Append(shown.Missing).Append('|').Append(shown.Foot).Append('|');
            text.Append(shown.Picture != null ? shown.Picture.GetInstanceID() : 0).Append('|').Append(shown.Texture != null ? shown.Texture.GetInstanceID() : 0).Append('|');
            foreach (var fact in shown.Facts)
            {
                text.Append(fact.Label).Append('=').Append(fact.Value).Append(';');
            }

            foreach (var tool in shown.Buttons.Concat(shown.Groups.SelectMany(group => group.Value)))
            {
                text.Append(tool.Label).Append(tool.Active ? '+' : '-').Append(tool.Off ? 'x' : 'o').Append(tool.Primary ? 'p' : 'n').Append(tool.Row).Append(';');
            }

            foreach (var group in shown.Groups)
            {
                text.Append(group.Key).Append(';');
            }

            return text.ToString();
        }

        // The buttons in the order Draw hands them out.
        private static List<ToolButton> InOrder(Detail shown)
        {
            var normal = shown.Buttons.Where(button => !button.Primary).ToList();
            if (normal.Any(button => button.Row != 0))
            {
                normal = normal.GroupBy(button => button.Row).OrderBy(row => row.Key).SelectMany(row => row).ToList();
            }

            normal.AddRange(shown.Buttons.Where(button => button.Primary));
            return normal;
        }

        private void Rewire(Detail shown)
        {
            var tools = InOrder(shown);
            for (var i = 0; i < tools.Count && i < buttons.Used; i++)
            {
                buttons.All[i].Spot.Click = tools[i].Off ? null : tools[i].Click;
            }

            var grouped = shown.Groups.SelectMany(group => group.Value).ToList();
            for (var i = 0; i < grouped.Count && i < groupButtons.Used; i++)
            {
                groupButtons.All[i].Spot.Click = grouped[i].Off ? null : grouped[i].Click;
            }
        }

        private class Measured
        {
            internal List<Fact> Facts;
            internal List<ToolButton> Normal;
            internal List<ToolButton> Primary;
            internal string FootText;
            internal float PrimaryStep;
            internal float PrimaryHeight;
            internal bool HasPicture;
            internal int PerLine;
            internal List<List<ToolButton>> Rows;
            internal int ButtonLines;
            internal bool[] TwoLines;
            internal List<KeyValuePair<string, List<ToolButton>>> Groups;
            internal int[] GroupLines;
            internal float Below;
        }

        // How tall the panel has to be for its title, two lines of text, its facts and its buttons.
        internal float Needed(Detail shown, float width)
        {
            var size = kit.Size;
            var inner = size * 0.6f;
            var measured = Measure(shown, width - inner * 2f);
            var hasText = shown != null && !string.IsNullOrEmpty(shown.Text);
            return inner * 2f + size * 1.6f + (hasText ? size * 2f : 0f) + measured.Below;
        }

        private Measured Measure(Detail shown, float wide)
        {
            var size = kit.Size;
            var gap = size * 0.4f;
            var facts = shown != null ? shown.Facts : new List<Fact>();
            var all = shown != null ? shown.Buttons : new List<ToolButton>();
            var normal = all.Where(button => !button.Primary).ToList();
            var primary = all.Where(button => button.Primary).ToList();
            var footText = shown?.Foot ?? "";
            var primaryStep = primary.Count > 2 ? size * 1.75f : size * 1.95f;
            var primaryHeight = primary.Count * primaryStep;
            var hasPicture = shown != null && (shown.Picture != null || shown.Texture != null || shown.Missing != null);

            // As many buttons on a line as fit with their whole label, or, when they are given rows, a line per row.
            var perLine = Mathf.Max(1, normal.Count);
            while (perLine > 1 && !normal.All(button => kit.Measure(button.Label) + size * 0.8f <= (wide - (perLine - 1) * gap) / perLine))
            {
                perLine--;
            }

            var rows = normal.Any(button => button.Row != 0) ? normal.GroupBy(button => button.Row).OrderBy(row => row.Key).Select(row => row.ToList()).ToList() : null;
            var buttonLines = rows != null ? rows.Count : normal.Count == 0 ? 0 : (normal.Count + perLine - 1) / perLine;
            var twoLines = new bool[facts.Count];
            var factsHeight = 0f;
            for (var i = 0; i < facts.Count && i < MaxFacts; i++)
            {
                twoLines[i] = kit.Measure(facts[i].Label) * 0.8f + kit.Measure(facts[i].Value) + size * 1.2f > wide;
                factsHeight += twoLines[i] ? size * 2.3f : size * 1.25f;
            }

            var groups = shown != null ? shown.Groups : new List<KeyValuePair<string, List<ToolButton>>>();
            var groupLines = new int[groups.Count];
            var groupsHeight = 0f;
            for (var g = 0; g < groups.Count; g++)
            {
                var count = groups[g].Value.Count;
                var fit = Mathf.Max(1, count);

                // A row with small < > buttons in it stays one row, whatever the name between them.
                while (fit > 1 && !groups[g].Value.Any(button => button.Narrow)
                       && !groups[g].Value.All(button => kit.Measure(button.Label) + size * 0.8f <= (wide - (fit - 1) * gap) / fit))
                {
                    fit--;
                }

                groupLines[g] = fit;
                groupsHeight += (string.IsNullOrEmpty(groups[g].Key) ? 0f : size * 1.1f) + (count + fit - 1) / fit * size * 1.75f + size * 0.3f;
            }

            var below = factsHeight + (facts.Count > 0 ? size * 0.5f : 0f) + buttonLines * size * 1.75f + (footText.Length > 0 ? size * 1.1f : 0f)
                        + (primary.Count > 0 ? primaryHeight + size * 0.4f : 0f) + groupsHeight;

            return new Measured
            {
                Facts = facts, Normal = normal, Primary = primary, FootText = footText, PrimaryStep = primaryStep, PrimaryHeight = primaryHeight,
                HasPicture = hasPicture, PerLine = perLine, Rows = rows, ButtonLines = buttonLines, TwoLines = twoLines, Groups = groups,
                GroupLines = groupLines, Below = below
            };
        }

        internal void Hide()
        {
            Ui.SetActive(box, false);
            buttons.Start();
            buttons.Done();
            groupButtons.Start();
            groupButtons.Done();
        }

        // A panel that cannot be drawn stays empty, the rest of the page still works.
        internal void Draw(Func<Detail> make)
        {
            try
            {
                var shown = make();
                var fingerprint = Fingerprint(shown);
                if (fingerprint == drawn && shown != null)
                {
                    Rewire(shown);
                    return;
                }

                Draw(shown);
                drawn = fingerprint;
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"A side panel could not be drawn: {e}");
                drawn = null;
                try
                {
                    Draw((Detail)null);
                }
                catch (Exception)
                {
                }
            }
        }

        private void Draw(Detail shown)
        {
            var size = kit.Size;
            var width = box.rectTransform.rect.width;
            var height = box.rectTransform.rect.height;
            var inner = size * 0.6f;
            var wide = width - inner * 2f;
            var gap = size * 0.4f;

            var measured = Measure(shown, wide);
            var facts = measured.Facts;
            var normal = measured.Normal;
            var primary = measured.Primary;
            var footText = measured.FootText;
            var primaryStep = measured.PrimaryStep;
            var primaryHeight = measured.PrimaryHeight;
            var hasPicture = measured.HasPicture;
            var perLine = measured.PerLine;
            var rows = measured.Rows;
            var buttonLines = measured.ButtonLines;
            var twoLines = measured.TwoLines;
            var groups = measured.Groups;
            var groupLines = measured.GroupLines;
            var below = measured.Below;

            text.text = shown?.Text ?? "";
            var wanted = text.text.Length > 0 ? Mathf.Min(text.GetPreferredValues(text.text, wide, 0f).y, size * 4f) : 0f;
            var room = height - inner * 2f - below;
            var bigSide = shown != null && shown.Picture == null ? Mathf.Min(wide, size * 6.5f) : size * 3.6f;
            var big = hasPicture && room - size * 1.6f - Mathf.Min(wanted, size * 1.8f) >= bigSide + size * 0.3f;
            var small = hasPicture && !big;
            var titleHeight = small ? size * 2.4f : size * 1.6f;
            var textHeight = Mathf.Clamp(room - titleHeight - (big ? bigSide + size * 0.3f : 0f) - size * 0.3f, 0f, wanted);
            var y = inner;

            Ui.SetActive(picture, hasPicture && shown.Picture != null);
            Ui.SetActive(texture, hasPicture && shown.Picture == null);
            missing.text = shown?.Missing ?? "";
            Ui.SetActive(missing, hasPicture && shown.Texture == null && shown.Missing != null);
            if (hasPicture)
            {
                var side = big ? bigSide : size * 2.2f;
                RectTransform shape;
                if (shown.Picture != null)
                {
                    picture.sprite = shown.Picture;
                    picture.color = kit.Like.color;
                    shape = picture.rectTransform;
                }
                else
                {
                    // A picture of its own, like a moon video, keeps its colours. No video: a black box that says so.
                    texture.texture = shown.Texture != null ? shown.Texture : Texture2D.blackTexture;
                    texture.color = Color.white;
                    shape = texture.rectTransform;
                }

                Kit.Place(shape, big ? (width - side) / 2f : inner, y, side, side);
                if (big)
                {
                    y += side + size * 0.3f;
                }
            }

            title.text = shown?.Title ?? "";
            Ui.SetActive(title, title.text.Length > 0);
            var titleLeft = small ? inner + size * 2.6f : inner;
            Kit.Place(title.rectTransform, titleLeft, y + (small ? size * 0.5f : 0f), width - titleLeft - inner, size * 1.4f);
            y += titleHeight;

            Ui.SetActive(text, textHeight >= size * 0.9f);
            if (textHeight >= size * 0.9f)
            {
                Kit.Place(text.rectTransform, inner, y, wide, textHeight);
                y += textHeight + size * 0.3f;
            }

            Ui.SetActive(rule, facts.Count > 0);
            if (facts.Count > 0)
            {
                Kit.Place(rule.rectTransform, inner, y, wide, kit.Thin);
                y += size * 0.5f;
            }

            for (var i = 0; i < MaxFacts; i++)
            {
                var show = i < facts.Count;
                Ui.SetActive(factLabels[i], show);
                Ui.SetActive(factValues[i], show);
                if (!show)
                {
                    continue;
                }

                var split = twoLines[i];
                factLabels[i].text = facts[i].Label.ToUpperInvariant();
                Kit.Place(factLabels[i].rectTransform, inner, y, wide, size * 1.2f);
                factValues[i].text = facts[i].Value;
                Kit.Place(factValues[i].rectTransform, inner, split ? y + size * 1.05f : y, wide, size * 1.2f);
                y += split ? size * 2.3f : size * 1.25f;
            }

            // The buttons sit at the bottom, right above the main one, in the order they come: the facts stay on top and whatever can be done is in one place on every panel.
            var cellWidth = (wide - (perLine - 1) * gap) / perLine;
            var buttonsBottom = height - inner - (primary.Count > 0 ? primaryHeight + size * 0.3f : 0f);
            var buttonsTop = buttonsBottom - buttonLines * size * 1.75f + (buttonLines > 0 ? size * 0.3f : 0f);
            buttons.Start();
            if (rows != null)
            {
                for (var r = 0; r < rows.Count; r++)
                {
                    var across = (wide - (rows[r].Count - 1) * gap) / rows[r].Count;
                    for (var i = 0; i < rows[r].Count; i++)
                    {
                        var button = buttons.Next();
                        kit.Style(button, rows[r][i]);
                        Kit.Place(button.Rect, inner + i * (across + gap), buttonsTop + r * size * 1.75f, across, size * 1.45f);
                    }
                }
            }
            else
            {
                for (var i = 0; i < normal.Count; i++)
                {
                    var button = buttons.Next();
                    kit.Style(button, normal[i]);
                    Kit.Place(button.Rect, inner + (i % perLine) * (cellWidth + gap), buttonsTop + (i / perLine) * size * 1.75f, cellWidth, size * 1.45f);
                }
            }

            // The main thing to do: on a line of its own at the bottom, full width, a little taller.
            for (var i = 0; i < primary.Count; i++)
            {
                var button = buttons.Next();
                kit.Style(button, primary[i]);
                button.Box.color = kit.Faded(primary[i].Off ? 0.06f : primary.Count > 2 ? 0.06f : 0.22f);
                Kit.Place(button.Rect, inner, height - inner - primaryHeight + i * primaryStep, wide, primaryStep - size * 0.25f);
            }

            buttons.Done();

            groupButtons.Start();
            var titles = 0;
            for (var g = 0; g < groups.Count; g++)
            {
                if (!string.IsNullOrEmpty(groups[g].Key))
                {
                    if (titles == headings.Count)
                    {
                        var heading = kit.Text("Heading", box.rectTransform, size * 0.75f);
                        heading.color = kit.Faded(0.7f);
                        headings.Add(heading);
                    }

                    var shownHeading = headings[titles++];
                    shownHeading.text = groups[g].Key;
                    Ui.SetActive(shownHeading, true);
                    Kit.Place(shownHeading.rectTransform, inner, y, wide, size);
                    y += size * 1.1f;
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
                    var tool = list[i];
                    var button = groupButtons.Next();
                    kit.Style(button, tool);
                    button.Text.overflowMode = TextOverflowModes.Ellipsis;
                    if (narrowCount > 0)
                    {
                        var across = tool.Narrow ? narrow : shared;
                        Kit.Place(button.Rect, x, y, across, size * 1.45f);
                        x += across + gap;
                    }
                    else
                    {
                        Kit.Place(button.Rect, inner + (i % fit) * (cell + gap), y + (i / fit) * size * 1.75f, cell, size * 1.45f);
                    }
                }

                y += (list.Count + fit - 1) / fit * size * 1.75f + size * 0.3f;
            }

            groupButtons.Done();
            for (var i = titles; i < headings.Count; i++)
            {
                Ui.SetActive(headings[i], false);
            }

            foot.text = footText;
            Ui.SetActive(foot, footText.Length > 0);
            if (footText.Length > 0)
            {
                Kit.Place(foot.rectTransform, inner, Mathf.Max(y, buttonsTop - size * (buttonLines > 0 ? 1.4f : 1.2f)), wide, size);
            }
        }

        internal HotSpot SpotAt(Vector3 world)
        {
            return Shown ? MenuView.SpotAt(buttons, world) ?? MenuView.SpotAt(groupButtons, world) : null;
        }

        internal void Targets(RectTransform frame, RectTransform root, Steering steering)
        {
            if (!Shown)
            {
                return;
            }

            MenuView.Targets(frame, buttons, Zone.Panel, steering);
            MenuView.Targets(frame, groupButtons, Zone.Panel, steering);
        }

        internal Rect Area => box.rectTransform.rect;

        internal RectTransform Rect => box.rectTransform;
    }
}
