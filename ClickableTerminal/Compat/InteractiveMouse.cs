using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace ClickableTerminal
{
    internal class InteractiveMouse
    {
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

        private readonly List<HotSpot> spots = new List<HotSpot>();
        private readonly StringBuilder builder = new StringBuilder();
        private string scanned;
        private object scannedMenu;
        private int scanIn;

        private bool screenChanged;

        internal bool TakeScreenChange()
        {
            var changed = screenChanged;
            screenChanged = false;
            return changed;
        }

        internal void Reset()
        {
            spots.Clear();
            scanned = null;
            scannedMenu = null;
        }

        internal void Tick(string screen, TMP_Text text)
        {
            var app = InteractiveTerminal.Application();
            var menu = InteractiveTerminal.Menu(app);

            // The app rewrites its screen whenever its cursor moves. The old areas stay until the new letters are in, so
            // a click in between still lands.
            if (!ReferenceEquals(menu, scannedMenu))
            {
                screenChanged = true;
            }

            if (screen != scanned || !ReferenceEquals(menu, scannedMenu))
            {
                scanned = screen;
                scannedMenu = menu;
                scanIn = 2;
            }

            if (scanIn > 0 && --scanIn == 0)
            {
                Scan(text, app, menu);
            }
        }

        internal void Targets(RectTransform frame, List<Vector2> into)
        {
            foreach (var spot in spots)
            {
                var point = new Vector2(Mathf.Lerp(spot.Area.xMin, spot.Area.xMax, 0.12f), spot.Area.center.y);
                into.Add(frame.InverseTransformPoint(spot.Space.TransformPoint(point)));
            }
        }

        internal HotSpot SpotAt(Vector3 world)
        {
            foreach (var spot in spots)
            {
                if (spot.Area.Contains(spot.Space.InverseTransformPoint(world)))
                {
                    return spot;
                }
            }

            return null;
        }

        private void Scan(TMP_Text text, object app, object menu)
        {
            spots.Clear();

            var elements = InteractiveTerminal.Elements(menu);
            var info = text.textInfo;
            if (elements == null || info == null || info.lineInfo == null || info.characterInfo == null)
            {
                return;
            }

            var lines = Mathf.Min(info.lineCount, info.lineInfo.Length);
            var contents = new string[lines];
            for (var l = 0; l < lines; l++)
            {
                contents[l] = LineText(info, info.lineInfo[l]);
            }

            // Entries print in order, so each name is searched for from where the last one was found.
            var starts = new List<int>();
            var found = new List<int>();
            var from = 0;

            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements.GetValue(i);
                var name = element != null ? InteractiveTerminal.TextOf(element) : null;
                var key = name != null ? Key(name) : "";
                if (key.Length == 0)
                {
                    continue;
                }

                for (var l = from; l < lines; l++)
                {
                    if (contents[l].Contains(key))
                    {
                        starts.Add(l);
                        found.Add(i);
                        from = l + 1;
                        break;
                    }
                }
            }

            for (var s = 0; s < starts.Count; s++)
            {
                var index = found[s];
                var element = elements.GetValue(index);
                var first = starts[s];

                var last = s + 1 < starts.Count ? starts[s + 1] - 1 : first;
                if (s + 1 == starts.Count)
                {
                    while (last + 1 < lines && HasWords(contents[last + 1]) && !contents[last + 1].Contains("Page "))
                    {
                        last++;
                    }
                }

                while (last > first && !HasWords(contents[last]))
                {
                    last--;
                }

                var selectable = InteractiveTerminal.CanSelect(element);
                var spot = new HotSpot
                {
                    Space = text.rectTransform,
                    Area = AreaOf(text, info, first, last)
                };

                if (selectable)
                {
                    spot.Hover = () => InteractiveTerminal.Select(menu, index);
                    spot.Click = () =>
                    {
                        InteractiveTerminal.Select(menu, index);
                        InteractiveTerminal.Submit(app);
                    };

                    if (InteractiveTerminal.HasCounter(element))
                    {
                        spot.Scroll = up => InteractiveTerminal.Count(element, up);
                    }
                }

                spots.Add(spot);
            }
        }

        // No colour tags, and short enough to survive a line wrap.
        private static string Key(string name)
        {
            var plain = Tags.Replace(name, "").Split('\n')[0].Trim();
            return (plain.Length > 14 ? plain.Substring(0, 14) : plain).TrimEnd();
        }

        private string LineText(TMP_TextInfo info, TMP_LineInfo line)
        {
            builder.Clear();
            var last = Mathf.Min(line.lastCharacterIndex, info.characterInfo.Length - 1);
            for (var i = line.firstCharacterIndex; i <= last; i++)
            {
                builder.Append(info.characterInfo[i].character);
            }

            return builder.ToString();
        }

        private static bool HasWords(string line)
        {
            foreach (var c in line)
            {
                if (char.IsLetterOrDigit(c))
                {
                    return true;
                }
            }

            return false;
        }

        private static Rect AreaOf(TMP_Text text, TMP_TextInfo info, int first, int last)
        {
            var left = float.MaxValue;
            var right = float.MinValue;

            for (var l = first; l <= last; l++)
            {
                var line = info.lineInfo[l];
                var end = Mathf.Min(line.lastCharacterIndex, info.characterInfo.Length - 1);
                for (var i = line.firstCharacterIndex; i <= end; i++)
                {
                    if (char.IsWhiteSpace(info.characterInfo[i].character))
                    {
                        continue;
                    }

                    left = Mathf.Min(left, info.characterInfo[i].bottomLeft.x);
                    right = Mathf.Max(right, info.characterInfo[i].topRight.x);
                }
            }

            if (left > right)
            {
                left = 0f;
                right = 0f;
            }

            var top = info.lineInfo[first].ascender;
            var bottom = info.lineInfo[last].descender;
            return new Rect(left, bottom, right - left, top - bottom);
        }
    }
}
