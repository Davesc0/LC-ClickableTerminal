using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class MoonsPage
    {
        private const string Affordable = "afford";
        private const string ClearSky = "clear";
        private const string Tag = "tag:";

        private static readonly Regex LeadingNumber = new Regex(@"^\s*\d+\s*", RegexOptions.Compiled);
        private static readonly Regex Grade = new Regex(@"^(F|D|C|B|A|S+)([+\-]*)$", RegexOptions.Compiled);
        private static readonly PageState State = new PageState();

        internal static MenuPage Build(Terminal terminal)
        {
            var listed = terminal.moonsCatalogueList;
            var round = StartOfRound.Instance;
            if (listed == null || round == null)
            {
                return null;
            }

            var catalog = Catalog.Read(terminal);
            var data = MoonData.Read();
            var wanted = State.FlagsWith(Tag).ToList();
            var simulate = HasKeyword(terminal, "simulate");
            var rows = new List<KeyValuePair<SelectableLevel, MenuEntry>>();
            var sellers = new List<SelectableLevel>();
            var moons = Moons(terminal, catalog, data);

            foreach (var level in moons)
            {
                var route = catalog.Routes[level];

                // A moon that buys scrap is not one to compare with the others: it goes up next to the Company.
                if (Sells(level, data))
                {
                    sellers.Add(level);
                    continue;
                }

                if ((State.Has(Affordable) && route.Price > terminal.groupCredits)
                    || (State.Has(ClearSky) && level.currentWeather != LevelWeatherType.None))
                {
                    continue;
                }

                // Every ticked tag has to be on the moon.
                if (wanted.Count > 0 && (!data.Tags.TryGetValue(level, out var tags) || !wanted.All(tags.Contains)))
                {
                    continue;
                }

                rows.Add(new KeyValuePair<SelectableLevel, MenuEntry>(level, Row(terminal, catalog, data, level, route, NameOf(level), simulate)));
            }

            if (moons.Count == 0)
            {
                return null;
            }

            Action redraw = () => Navigator.Rebuild(terminal);

            var page = new MenuPage
            {
                Title = "MOONS",
                Hint = simulate ? "Right: info  Wheel: simulate" : "Right click: info",
                SortBy = column =>
                {
                    State.SortBy(column);
                    redraw();
                }
            };

            page.Columns.Add(new Column { Header = State.HeaderFor("NAME", 0) });
            // Some moons write a whole sentence where the risk grade goes. That one column is kept in check,
            // the weather always gets the room its longest name needs.
            page.Columns.Add(new Column { Header = State.HeaderFor("RISK", 1), Chars = 6, MaxChars = 9 });
            page.Columns.Add(new Column { Header = State.HeaderFor("WEATHER", 2), Chars = 9 });
            page.Columns.Add(new Column { Header = State.HeaderFor("PRICE", 3), Chars = 7, AlignRight = true });

            page.Tools.Add(State.ToolFor("CAN AFFORD", Affordable, redraw));
            page.Tools.Add(State.ToolFor("CLEAR SKY", ClearSky, redraw));

            // Only when a mod has given the moons tags to filter by.
            if (data.Tags.Count > 0)
            {
                page.Tools.Add(new ToolButton
                {
                    Label = wanted.Count > 0 ? $"TAGS ({wanted.Count})" : "TAGS",
                    Active = wanted.Count > 0,
                    Click = () => Navigator.Open(() => TagsPage(terminal))
                });
            }

            // The places that buy scrap sit above the table as buttons, under the one rate the game has for all of them.
            page.Summary = $"The Company is buying at {Mathf.RoundToInt(round.companyBuyingRate * 100f)}%";

            foreach (var pair in catalog.Routes)
            {
                if (IsCompany(pair.Key, pair.Value))
                {
                    page.Pinned.Add(Buyer(terminal, catalog, data, pair.Key));
                    break;
                }
            }

            foreach (var level in sellers)
            {
                page.Pinned.Add(Buyer(terminal, catalog, data, level));
            }

            // With the Company as the only buyer the line about the rate is the button, as in the game's own list.
            if (sellers.Count == 0 && page.Pinned.Count == 1)
            {
                page.Pinned[0].Label = page.Summary;
                page.Pinned[0].Flat = true;
                page.Summary = null;
            }

            AddRows(page, rows, data);
            return page;
        }

        // One list, or a section per author when that is switched on and there is more than one author to tell apart.
        // With TooManyMoons the day's draw is one section of its own, and the moons that mod keeps open next to it
        // (a story pack's unlocked moons, say) follow under their authors, whatever the setting says.
        private static void AddRows(MenuPage page, List<KeyValuePair<SelectableLevel, MenuEntry>> rows, MoonData data)
        {
            var apart = data.Rotating && rows.Any(row => data.Drawn.Contains(row.Key)) && rows.Any(row => !data.Drawn.Contains(row.Key));

            var groups = rows
                .GroupBy(row => apart && data.Drawn.Contains(row.Key) ? MoonData.Rotation
                    : data.Authors.TryGetValue(row.Key, out var author) ? author : MoonData.Vanilla)
                .OrderBy(group => group.Key == MoonData.Rotation ? 0 : group.Key == MoonData.Vanilla ? 1 : 2)
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if ((!Settings.GroupMoonsByAuthor.Value && !apart) || groups.Count < 2)
            {
                page.Entries.AddRange(State.Sorted(rows.Select(row => row.Value).ToList()));
                return;
            }

            foreach (var group in groups)
            {
                if (page.Entries.Count > 0)
                {
                    page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
                }

                page.Entries.Add(new MenuEntry { Label = group.Key, IsHeader = true });
                page.Entries.AddRange(State.Sorted(group.Select(row => row.Value).ToList()));
            }
        }

        private static MenuEntry Buyer(Terminal terminal, Catalog catalog, MoonData data, SelectableLevel level)
        {
            var route = catalog.Routes[level];
            var locked = data.Locked.Contains(level);
            var here = StartOfRound.Instance.currentLevel == level;

            return new MenuEntry
            {
                Label = NameOf(level) + (route.Price > 0 ? $"  ${route.Price}" : ""),
                Marker = here ? Marker.Here : locked ? Marker.Locked : Marker.None,
                Command = route.Command,
                AltCommand = catalog.InfoFor(route),
                Dim = locked || (!here && route.Price > terminal.groupCredits)
            };
        }

        private static bool IsCompany(SelectableLevel level, Target route)
        {
            return route.Noun == "company" || level.sceneName == "CompanyBuilding";
        }

        // Named in the config. The game has no flag for it: "no enemies and no scrap" is set on moons that are
        // nothing like the Company (Oldred has both in play, End is a story moon).
        private static bool Sells(SelectableLevel level, MoonData data)
        {
            var name = Commands.Normalize(NameOf(level));
            return Settings.SellingMoons.Value.Split(',').Any(entry => Commands.Normalize(entry) == name);
        }

        // The moons this page may show at all, before the player's own filters: the catalogue in its own order,
        // then whatever else has a route, as far as the settings for hidden and locked moons let them through.
        private static List<SelectableLevel> Moons(Terminal terminal, Catalog catalog, MoonData data)
        {
            var listed = terminal.moonsCatalogueList;
            var catalogued = new HashSet<SelectableLevel>(listed.Where(level => level != null));
            var seen = new HashSet<SelectableLevel>();
            var moons = new List<SelectableLevel>();

            foreach (var level in listed.Concat(StartOfRound.Instance.levels ?? new SelectableLevel[0]))
            {
                // Only moons that can really be typed in. The Company is not one of them.
                if (level == null || !catalog.Routes.TryGetValue(level, out var route) || IsCompany(level, route) || !seen.Add(level))
                {
                    continue;
                }

                var hidden = data.Hidden.Contains(level) || (!data.LevelLoader && !catalogued.Contains(level));
                if ((hidden && !Reveal(level)) || (data.Locked.Contains(level) && !Settings.ShowLockedMoons.Value))
                {
                    continue;
                }

                moons.Add(level);
            }

            return moons;
        }

        // The command for one of the moons that can be flown to right now: not locked, not too expensive, not this one.
        internal static string RandomRoute(Terminal terminal)
        {
            var round = StartOfRound.Instance;
            if (terminal.moonsCatalogueList == null || round == null)
            {
                return null;
            }

            var catalog = Catalog.Read(terminal);
            var data = MoonData.Read();
            var open = Moons(terminal, catalog, data)
                .Where(level => level != round.currentLevel && !data.Locked.Contains(level) && catalog.Routes[level].Price <= terminal.groupCredits)
                .ToList();

            return open.Count > 0 ? catalog.Routes[open[UnityEngine.Random.Range(0, open.Count)]].Command : null;
        }

        // Every tag the moons on the page carry, each one a tick box with how many moons have it.
        private static MenuPage TagsPage(Terminal terminal)
        {
            var page = new MenuPage { Title = "MOON TAGS", Hint = "BACK shows the moons" };
            var counts = new SortedDictionary<string, int>();
            var data = MoonData.Read();

            foreach (var level in Moons(terminal, Catalog.Read(terminal), data))
            {
                // The buyers are not in the table, so they are not counted either.
                if (!Sells(level, data) && data.Tags.TryGetValue(level, out var tags))
                {
                    foreach (var tag in tags)
                    {
                        counts.TryGetValue(tag, out var count);
                        counts[tag] = count + 1;
                    }
                }
            }

            if (State.FlagsWith(Tag).Any())
            {
                page.Entries.Add(new MenuEntry
                {
                    Label = "Clear all",
                    Click = () =>
                    {
                        State.ClearFlags(Tag);
                        Navigator.Rebuild(terminal);
                    }
                });
                page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            }

            foreach (var pair in counts)
            {
                var flag = Tag + pair.Key;
                page.Entries.Add(new MenuEntry
                {
                    Label = $"{(State.Has(flag) ? "[x]" : "[ ]")} {pair.Key}  ({pair.Value})",
                    Click = () =>
                    {
                        State.Toggle(flag);
                        Navigator.Rebuild(terminal);
                    }
                });
            }

            return page;
        }

        private static MenuEntry Row(Terminal terminal, Catalog catalog, MoonData data, SelectableLevel level, Target route, string name, bool simulate)
        {
            var here = StartOfRound.Instance.currentLevel == level;
            var locked = data.Locked.Contains(level);
            var weather = level.currentWeather == LevelWeatherType.None ? "" : level.currentWeather.ToString();
            var risk = string.IsNullOrEmpty(level.riskLevel) ? "-" : level.riskLevel;

            // With WeatherRegistry the weather opens its forecast, also where the sky is clear and the cell is empty
            // (a space: an empty cell is not a button).
            var forecast = WeatherForecast.Available && level.randomWeathers != null && level.randomWeathers.Length > 0;
            var shown = forecast && weather.Length == 0 ? " " : weather;

            return new MenuEntry
            {
                CellClicks = forecast ? new System.Action[] { null, () => Navigator.Open(() => WeatherForecast.Page(terminal, level)), null } : null,
                Label = name,
                Marker = here ? Marker.Here : locked ? Marker.Locked : Marker.None,
                Command = route.Command,
                AltCommand = catalog.InfoFor(route),
                WheelCommand = simulate ? "simulate " + name : null,
                Dim = locked || (!here && route.Price > terminal.groupCredits),
                Cells = new[] { risk, shown, "$" + route.Price },
                Keys = new IComparable[] { name, RiskRank(level.riskLevel), weather, route.Price }
            };
        }

        private static bool Reveal(SelectableLevel level)
        {
            switch (Settings.ShowHiddenMoons.Value)
            {
                case ShowHidden.Always:
                    return true;
                case ShowHidden.AfterDiscovery:
                    return MoonMemory.HasFlownTo(level);
                case ShowHidden.AfterVisited:
                    return MoonMemory.HasLandedOn(level);
                default:
                    return false;
            }
        }

        private static bool HasKeyword(Terminal terminal, string word)
        {
            var keywords = terminal.terminalNodes != null ? terminal.terminalNodes.allKeywords : null;
            return keywords != null && keywords.Any(keyword => keyword != null && keyword.word == word);
        }

        // "41 Experimentation" -> "Experimentation"
        internal static string NameOf(SelectableLevel level)
        {
            var name = level.PlanetName ?? "";
            var stripped = LeadingNumber.Replace(name, "");
            return stripped.Length > 0 ? stripped : name;
        }

        // "Safe" first, then F up to S. Every + or - moves it a step, so S < S+ < S++ < S++++,
        // and modded grades like SS or SSS+ come after all of those. Anything unreadable sorts last.
        private static int RiskRank(string risk)
        {
            var grade = (risk ?? "").Trim().ToUpperInvariant();
            if (grade == "SAFE")
            {
                return -1;
            }

            var match = Grade.Match(grade);
            if (!match.Success)
            {
                return int.MaxValue;
            }

            var letters = match.Groups[1].Value;
            var signs = match.Groups[2].Value;
            var steps = 0;

            foreach (var sign in signs)
            {
                steps += sign == '+' ? 1 : -1;
            }

            return "FDCBAS".IndexOf(letters[0]) * 10000 + (letters.Length - 1) * 1000 + 500 + steps;
        }
    }
}
