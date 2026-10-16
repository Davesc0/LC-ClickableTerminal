using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace ClickableTerminal
{
    // Selectable Interiors prints its interiors as a list of names and numbers to type back in.
    // Here each one is a row to click.
    internal static class SelectableInteriorsPage
    {
        internal const string Guid = "NoteBoxz.SelectableInteriors";
        internal const string ListCommand = "listinside";

        private const string SetCommand = "setinside ";

        // "Forcing Interior Mineshaft (4) to be loaded!", what the mod answers to "setinside 4".
        private static readonly Regex Answer = new Regex(@"^Forcing Interior (.+) \((\d+)\) to be loaded", RegexOptions.Compiled);

        // What was clicked last, for when the mod's own state cannot be read.
        private static string clicked;

        internal static bool Owns(string key)
        {
            return key == ListCommand || key.StartsWith("setinside");
        }

        internal static MenuPage Build(Terminal terminal)
        {
            var keywords = terminal != null && terminal.terminalNodes != null ? terminal.terminalNodes.allKeywords : null;
            if (keywords == null)
            {
                return null;
            }

            var page = new MenuPage { Title = "INTERIORS" };
            var rows = new SortedDictionary<int, MenuEntry>();
            var extra = new List<MenuEntry>();
            var now = "none";

            foreach (var keyword in keywords)
            {
                if (keyword == null || keyword.word == null || !keyword.word.StartsWith(SetCommand) || keyword.specialKeywordResult == null)
                {
                    continue;
                }

                var answer = Answer.Match(keyword.specialKeywordResult.displayText ?? "");
                if (answer.Success && int.TryParse(answer.Groups[2].Value, out var id))
                {
                    var word = keyword.word;
                    Func<bool> picked = () => Readable() ? !FullRandom() && Forced() == id : clicked == word;
                    rows[id] = Row(terminal, answer.Groups[1].Value, word, picked);
                    now = picked() ? answer.Groups[1].Value : now;
                }
                else if (keyword.word == SetCommand + "none")
                {
                    var word = keyword.word;
                    extra.Insert(0, Row(terminal, "No forced interior", word, () => Readable() ? !FullRandom() && Forced() < 0 : clicked == word));
                }
                else if (keyword.word == SetCommand + "fullrandom")
                {
                    var word = keyword.word;
                    Func<bool> picked = () => Readable() ? FullRandom() : clicked == word;
                    extra.Add(Row(terminal, "Fully random", word, picked));
                    now = picked() ? "fully random" : now;
                }
            }

            page.Summary = "Interior of the next moon: " + now;

            if (rows.Count == 0 && extra.Count == 0)
            {
                page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
                page.Entries.Add(new MenuEntry { Label = "No interiors to pick from yet." });
                return page;
            }

            page.Entries.AddRange(extra);
            if (extra.Count > 0 && rows.Count > 0)
            {
                page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            }

            page.Entries.AddRange(rows.Values);
            return page;
        }

        // The list stays where it is: the mod's one line answer is skipped and the mark moves to the new pick.
        private static MenuEntry Row(Terminal terminal, string name, string command, Func<bool> picked)
        {
            return new MenuEntry
            {
                Label = name,
                LiveLabel = () => picked() ? "> " + name + "  (selected)" : name,
                Click = () =>
                {
                    var from = Navigator.Current;
                    clicked = command;
                    Commands.Submit(terminal, command);
                    if (Navigator.Current != from)
                    {
                        Navigator.Back(terminal);
                    }
                }
            };
        }

        private static bool Readable()
        {
            return Read("LVidForceing") is int;
        }

        private static int Forced()
        {
            return Read("LVidForceing") is int id ? id : -1;
        }

        private static bool FullRandom()
        {
            return Read("FullRandom") is bool random && random;
        }

        private static Type plugin;
        private static bool complained;

        private static object Read(string field)
        {
            try
            {
                if (plugin == null)
                {
                    plugin = Chainloader.PluginInfos.TryGetValue(Guid, out var info) && info.Instance != null
                        ? info.Instance.GetType()
                        : Reflect.Find("SelectableInteriors.SelectableInteriors");
                }

                return plugin?.GetField(field, AccessTools.all)?.GetValue(null);
            }
            catch (Exception e)
            {
                if (!complained)
                {
                    complained = true;
                    Plugin.mlg.LogWarning($"Interiors: cannot read the mod's '{field}': {e}");
                }

                return null;
            }
        }
    }
}
