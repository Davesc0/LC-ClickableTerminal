using System;
using System.Linq;
using System.Reflection;

namespace ClickableTerminal
{
    // Y4NGZ's Contracted gives every moon a contract. It writes them into the game's moon list, which our MOONS page does not print, so they are asked for through the mod's own MoonContractApi instead, by reflection.
    internal static class Contracts
    {
        // The colour Contracted writes its contracts in.
        private const string Colour = "#00D7E6";

        private static Type api;
        private static bool looked;
        private static float refreshed = -100f;

        internal class Contract
        {
            internal string Name;
            internal string Short;
            internal string Title;
            internal string Briefing;
            internal string Description;
            internal string Art;
        }

        internal static bool Available
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    api = Reflect.Find("Y4NGZCompany.Contracts._Shared.MoonContractApi");
                }

                return api != null;
            }
        }

        // The mod draws the contracts again when the ship goes back to orbit. Asked at most once a second.
        private static void Refresh()
        {
            if (UnityEngine.Time.unscaledTime - refreshed < 1f)
            {
                return;
            }

            refreshed = UnityEngine.Time.unscaledTime;
            Call("RefreshAssignments", "ClickableTerminal moons page");
        }

        internal static Contract For(SelectableLevel level)
        {
            if (!Available || level == null || level.sceneName == "CompanyBuilding")
            {
                return null;
            }

            Refresh();
            var id = level.levelID;
            var name = Call("GetDisplayNameForLevelId", id);
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return new Contract
            {
                Name = name,
                Short = Call("GetShortNameForLevelId", id) ?? name,
                Title = Call("GetTitleForLevelId", id) ?? name.ToUpperInvariant(),
                Briefing = Call("GetBriefingForLevelId", id),
                Description = Call("GetGameplayDescriptionForLevelId", id),
                Art = Call("GetArtForLevelId", id)
            };
        }

        internal static string Tinted(string text)
        {
            return Tint.Mod(text, Colour);
        }

        // The contract in a window: what the Company says about it, what to do, and the mod's little drawing.
        internal static void ShowWindow(SelectableLevel level)
        {
            var contract = For(level);
            if (contract == null)
            {
                return;
            }

            var text = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(contract.Briefing))
            {
                text.Append(contract.Briefing).Append("\n\n");
            }

            if (!string.IsNullOrEmpty(contract.Description))
            {
                text.Append("<alpha=#99>WHAT TO DO<alpha=#FF>\n").Append(contract.Description).Append("\n\n");
            }

            if (!string.IsNullOrEmpty(contract.Art))
            {
                text.Append("<mspace=0.6em>").Append(contract.Art.Replace("<", "<noparse><</noparse>")).Append("</mspace>\n\n");
            }

            text.Append("<alpha=#99>Done, it lowers the quota. Failed, it raises it, by the moon's risk.<alpha=#FF>");
            Window.Show(new Window
            {
                Title = "CONTRACT: " + Tinted(contract.Title) + " - " + MoonsPage.NameOf(level).ToUpperInvariant(),
                Text = text.ToString()
            });
        }

        // Every listed moon with its contract, a click opens the contract. The mod's own "contractlist" prints the same as sentences.
        internal static MenuPage Page(Terminal terminal)
        {
            var page = new MenuPage { Title = "CONTRACTS", Subtitle = "Done lowers the quota, failed raises it" };
            page.Columns.Add(new Column { Header = "MOON" });
            page.Columns.Add(new Column { Header = "RISK", Chars = 6, MaxChars = 9 });
            page.Columns.Add(new Column { Header = "CONTRACT", Chars = 14 });

            foreach (var level in terminal.moonsCatalogueList.Where(each => each != null))
            {
                var contract = For(level);
                if (contract == null)
                {
                    continue;
                }

                var moon = level;
                page.Entries.Add(new MenuEntry
                {
                    Label = MoonsPage.NameOf(level),
                    Cells = new[] { string.IsNullOrEmpty(level.riskLevel) ? "-" : level.riskLevel, Tinted(contract.Name) },
                    Click = () => ShowWindow(moon)
                });
            }

            if (page.Entries.Count == 0)
            {
                page.Summary = "No contracts are given out right now.";
            }

            return page;
        }

        // The answer to a typed "contractlist", drawn as the page above.
        internal static bool Claim(Terminal terminal, View view, string printed)
        {
            if (!Available)
            {
                return false;
            }

            var lines = printed.Replace("\r", "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
            if (lines.Count == 0 || lines[0] != "CONTRACTS" || !lines.Skip(1).All(line => line.StartsWith("Contract:") || line.StartsWith("No moon contracts")))
            {
                return false;
            }

            view.Builder = () => Page(terminal);
            return true;
        }

        private static string Call(string method, object argument)
        {
            try
            {
                var found = api.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, new[] { argument.GetType() }, null);
                return found?.Invoke(null, new[] { argument }) as string;
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"Contracted's {method} could not be asked: {(e.InnerException ?? e).Message}");
                return null;
            }
        }
    }
}
