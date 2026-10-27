using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class LethalRpgPages
    {
        internal const string Guid = "cucumis.lethalrpg";

        // "* Stamina      Lv 0/5   $50   -"
        internal static readonly Regex Upgrade = new Regex(@"^\*\s+(.+?)\s{2,}Lv\s*(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

        private static readonly string[] Footer = { "Pages:", "Buy:", "More:", "Skill menu:" };

        private static readonly (string Label, string Command)[] Lists =
        {
            ("BODY", "rpg"), ("TOOLS", "rpg tools"), ("UNIQUE", "rpg unique"), ("ULTIMATE", "rpg ult")
        };

        internal static MenuPage Menu()
        {
            var terminal = Object.FindObjectOfType<Terminal>();
            var page = Navigator.MenuLook(terminal, new MenuPage { Title = "LETHAL RPG" });

            page.Entries.Add(new MenuEntry { Label = "BODY UPGRADES", Description = "Stamina, strength, jump and the rest of the body.", Click = () => List(terminal, "rpg") });
            page.Entries.Add(new MenuEntry { Label = "TOOL UPGRADES", Description = "Upgrades for the tools you carry.", Click = () => List(terminal, "rpg tools") });
            page.Entries.Add(new MenuEntry { Label = "UNIQUE UPGRADES", Description = "One-of-a-kind upgrades.", Click = () => List(terminal, "rpg unique") });
            page.Entries.Add(new MenuEntry { Label = "ULTIMATES", Description = "The big ones, for the most XP.", Click = () => List(terminal, "rpg ult") });
            page.Entries.Add(new MenuEntry { Label = "MY STATS", Command = "rpg stats", Description = "Your level, XP and what you have bought." });
            page.Entries.Add(new MenuEntry { Label = "CREW", Command = "rpg crew", Description = "How the rest of the crew is built." });
            page.Entries.Add(new MenuEntry { Label = "BUILD", Command = "rpg build", Description = "Your build, to save and buy back later." });
            page.Entries.Add(new MenuEntry { Label = "INFO", Command = "rpg info", Description = "How the mod works." });
            page.Entries.Add(new MenuEntry { Label = "REFUND", Description = "Take back every upgrade and get the points to spend again.", Click = () => Refund(terminal) });
            return Answers.InWindows(terminal, page);
        }

        private static void List(Terminal terminal, string command, string note = null)
        {
            var answer = Commands.Ask(terminal, command);
            var lines = Tags.Replace(answer, "").Replace("\r", "").Split('\n');
            var text = string.Join("\n", lines.Where(line => !Footer.Any(start => line.TrimStart().StartsWith(start))));

            var window = new Window { Title = "LETHAL RPG", Text = (note != null ? "<alpha=#99>" + note + "<alpha=#FF>\n\n" : "") + Window.Tidy(text, "") };

            var pages = window.Group("LISTS");
            foreach (var list in Lists)
            {
                var target = list.Command;
                pages.Add(new ToolButton { Label = list.Label, Active = target == command, Click = () => List(terminal, target) });
            }

            var buy = window.Group("BUY A LEVEL");
            foreach (var line in lines)
            {
                var match = Upgrade.Match(line.Trim());
                if (!match.Success)
                {
                    continue;
                }

                var name = match.Groups[1].Value.Trim();
                var full = match.Groups[2].Value == match.Groups[3].Value;
                buy.Add(new ToolButton
                {
                    Label = name,
                    Off = full,
                    Click = () =>
                    {
                        var said = Window.Tidy(Commands.Ask(terminal, "rpg buy " + name.ToLowerInvariant()), "");
                        List(terminal, command, said.Length > 0 ? said : null);

                        // The host takes the purchase a moment later.
                        terminal.StartCoroutine(ReadAgain(terminal, command));
                    }
                });
            }

            if (buy.Count == 0)
            {
                window.Groups.RemoveAll(group => group.Value == buy);
            }

            Window.Show(window);
        }

        private static IEnumerator ReadAgain(Terminal terminal, string command)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            if (Window.Open != null && Window.Open.Title == "LETHAL RPG")
            {
                List(terminal, command);
            }
        }

        private static void Refund(Terminal terminal)
        {
            var window = new Window { Title = "REFUND", Text = Window.Tidy(Commands.Ask(terminal, "rpg refund"), "") };
            window.Buttons.Add(new ToolButton { Label = "REFUND", Click = () => Answers.Run(terminal, "REFUND", "rpg refund confirm") });
            window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = Window.Close });
            Window.Show(window);
        }
    }
}
