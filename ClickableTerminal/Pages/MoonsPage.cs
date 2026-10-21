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
        private static bool pickCompany;

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
            var levels = new Dictionary<MenuEntry, SelectableLevel>();
            var sellers = new List<SelectableLevel>();
            var moons = Moons(terminal, catalog, data);

            foreach (var level in moons)
            {
                var route = catalog.Routes[level];

                if (Sells(level, data))
                {
                    sellers.Add(level);
                    continue;
                }

                if (Mysteries.Contains(level))
                {
                    if (!State.Has(Affordable) && !State.Has(ClearSky) && wanted.Count == 0)
                    {
                        var unknown = MysteryRow();
                        levels[unknown] = level;
                        rows.Add(new KeyValuePair<SelectableLevel, MenuEntry>(level, unknown));
                    }

                    continue;
                }

                if ((State.Has(Affordable) && route.Price > terminal.groupCredits)
                    || (State.Has(ClearSky) && level.currentWeather != LevelWeatherType.None))
                {
                    continue;
                }

                if (wanted.Count > 0 && (!data.Tags.TryGetValue(level, out var tags) || !wanted.All(tags.Contains)))
                {
                    continue;
                }

                var row = Row(terminal, catalog, data, level, route, NameOf(level), simulate);
                levels[row] = level;
                rows.Add(new KeyValuePair<SelectableLevel, MenuEntry>(level, row));
            }

            if (moons.Count == 0)
            {
                return null;
            }

            Action redraw = () => Navigator.Rebuild(terminal);

            var page = new MenuPage
            {
                Title = "MOONS",
                Subtitle = round.currentLevel != null ? "Orbiting " + NameOf(round.currentLevel) : null,

                SelectRows = true,
                Scale = 0.7f,
                ScaleMin = 0.6f,
                ScaleMax = 0.8f,
                DetailFor = entry => !levels.TryGetValue(entry, out var level) ? null
                    : Mysteries.Contains(level) ? MysteryDetail() : Detail(terminal, catalog, data, level, simulate),
                SortBy = column =>
                {
                    State.SortBy(column);
                    redraw();
                }
            };

            page.Columns.Add(new Column { Header = State.HeaderFor("NAME", 0) });
            // Some moons write a whole sentence where the risk grade goes.
            page.Columns.Add(new Column { Header = State.HeaderFor("RISK", 1), Chars = 6, MaxChars = 9 });
            page.Columns.Add(new Column { Header = State.HeaderFor("WEATHER", 2), Chars = 9 });
            page.Columns.Add(new Column { Header = State.HeaderFor("PRICE", 3), Chars = 7, AlignRight = true });

            var on = wanted.Count + (State.Has(Affordable) ? 1 : 0) + (State.Has(ClearSky) ? 1 : 0);
            page.Corner = new ToolButton
            {
                Label = on > 0 ? $"SETTINGS ({on})" : "SETTINGS",
                Active = on > 0,
                Click = () => SettingsWindow(terminal)
            };

            page.Summary = $"The Company is buying at {Mathf.RoundToInt(round.companyBuyingRate * 100f)}%";

            var company = catalog.Routes.FirstOrDefault(pair => IsCompany(pair.Key, pair.Value)).Key;
            if (company != null)
            {
                sellers.Insert(0, company);
            }

            AddRows(page, rows, data);

            if (sellers.Count > 0)
            {
                var top = new List<MenuEntry> { new MenuEntry { Label = "COMPANY MOONS", IsHeader = true } };
                foreach (var level in sellers)
                {
                    var row = Row(terminal, catalog, data, level, catalog.Routes[level], NameOf(level), simulate);
                    levels[row] = level;
                    top.Add(row);
                }

                // The line about the rate is the Company's button, as in the game's own list: it picks the Company's row.
                if (company != null)
                {
                    var companyRow = top[1];
                    page.Pinned.Add(new MenuEntry
                    {
                        Label = page.Summary,
                        Flat = true,
                        Click = () =>
                        {
                            pickCompany = true;
                            Navigator.Rebuild(terminal);
                        }
                    });
                    page.Summary = null;
                    if (pickCompany)
                    {
                        page.Pick = companyRow;
                        pickCompany = false;
                    }
                }

                top.Add(new MenuEntry { Label = "", IsHeader = true });
                if (!page.Entries.Any(entry => entry.IsHeader && entry.Label.Length > 0))
                {
                    top.Add(new MenuEntry { Label = "MOONS", IsHeader = true });
                }

                page.Entries.InsertRange(0, top);
            }

            return page;
        }

        // With TooManyMoons the day's draw is a section of its own, and the moons that mod keeps open beside it follow
        // under their authors, whatever the setting says.
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

        private static Detail Detail(Terminal terminal, Catalog catalog, MoonData data, SelectableLevel level, bool simulate)
        {
            var route = catalog.Routes[level];
            var here = StartOfRound.Instance.currentLevel == level;
            var detail = new Detail
            {
                Texture = MoonPicture.For(level),
                Missing = level.videoReel == null ? "NO VIDEO" : null,
                Title = NameOf(level),
            };

            detail.Facts.Add(new Fact { Label = "Risk", Value = string.IsNullOrEmpty(level.riskLevel) ? "-" : level.riskLevel });
            detail.Facts.Add(new Fact { Label = "Weather", Value = level.currentWeather == LevelWeatherType.None ? "Clear" : level.currentWeather.ToString() });
            detail.Facts.Add(new Fact { Label = "Route cost", Value = here ? "You are here" : route.Price > 0 ? "$" + route.Price : "FREE" });

            if (data.Locked.Contains(level))
            {
                detail.Facts.Add(new Fact { Label = "Status", Value = "Locked" });
            }
            else if (!here)
            {
                detail.Buttons.Add(new ToolButton
                {
                    Label = route.Price > 0 ? $"BUY ${route.Price}" : "ROUTE",
                    Primary = true,
                    Off = route.Price > terminal.groupCredits,
                    Click = () => Route(terminal, level, route)
                });
            }

            var info = catalog.InfoNode(route);
            if (info != null)
            {
                detail.Buttons.Add(new ToolButton { Label = "INFO", Click = () => Window.ShowPage(terminal, NameOf(level).ToUpperInvariant(), info) });
            }

            if (HasForecast(level))
            {
                detail.Buttons.Add(new ToolButton { Label = "FORECAST", Click = () => WeatherForecast.ShowWindow(level) });
            }

            if (simulate)
            {
                var name = NameOf(level);
                detail.Buttons.Add(new ToolButton
                {
                    Label = "SIMULATE",
                    Click = () => Window.Show(new Window { Title = "SIMULATE: " + name.ToUpperInvariant(), Text = Simulation(Commands.Ask(terminal, "simulate " + name), name) })
                });
            }

            return detail;
        }

        private static void Route(Terminal terminal, SelectableLevel level, Target route)
        {
            if (route.Price <= 0)
            {
                Fly(terminal, level, route);
                return;
            }

            var name = NameOf(level);
            var window = new Window
            {
                Title = "BUY " + name.ToUpperInvariant() + "?",
                Text = $"Route to {name}<pos=65%>${route.Price}\nCredits after<pos=65%>${terminal.groupCredits - route.Price}"
            };
            window.Buttons.Add(new ToolButton { Label = "BUY", Click = () => Fly(terminal, level, route) });
            window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = Window.Close });
            Window.Show(window);
        }

        internal static void Fly(Terminal terminal, SelectableLevel level, Target route)
        {
            if (route.Confirm == null)
            {
                Commands.Submit(terminal, route.Command);
                return;
            }

            var answer = Commands.Act(terminal, route.Confirm, 1);
            Navigator.Rebuild(terminal);
            Window.Show(new Window { Title = "ROUTE: " + NameOf(level).ToUpperInvariant(), Text = answer });
        }

        private static readonly Regex Chance = new Regex(@"^\*?\s*(.+?)\s*//\s*Chance:\s*(\S+)", RegexOptions.Compiled);

        // "* Name // Chance: 12.3%"
        private static string Simulation(string printed, string name)
        {
            var lines = Window.Tidy(printed, name).Split('\n').Select(line =>
            {
                var match = Chance.Match(System.Text.RegularExpressions.Regex.Replace(line, "<[^>]*>", "").Trim());
                return match.Success ? "  " + match.Groups[1].Value + "<pos=72%>" + match.Groups[2].Value : line;
            });
            return string.Join("\n", lines);
        }

        private static bool HasForecast(SelectableLevel level)
        {
            return WeatherForecast.Available && level.randomWeathers != null && level.randomWeathers.Length > 0;
        }

        private static bool IsCompany(SelectableLevel level, Target route)
        {
            return route.Noun == "company" || level.sceneName == "CompanyBuilding";
        }

        // The game has no flag for it, and "no enemies and no scrap" is also set on moons like Oldred and End.
        private static bool Sells(SelectableLevel level, MoonData data)
        {
            var name = Commands.Normalize(NameOf(level));
            return Settings.SellingMoons.Value.Split(',').Any(entry => Commands.Normalize(entry) == name);
        }

        private static List<SelectableLevel> Moons(Terminal terminal, Catalog catalog, MoonData data)
        {
            var listed = terminal.moonsCatalogueList;
            var catalogued = new HashSet<SelectableLevel>(listed.Where(level => level != null));
            var seen = new HashSet<SelectableLevel>();
            Mysteries.Clear();
            var moons = new List<SelectableLevel>();

            foreach (var level in listed.Concat(StartOfRound.Instance.levels ?? new SelectableLevel[0]))
            {
                if (level == null || !catalog.Routes.TryGetValue(level, out var route) || IsCompany(level, route) || !seen.Add(level))
                {
                    continue;
                }

                var hidden = data.Hidden.Contains(level) || (!data.LevelLoader && !catalogued.Contains(level));
                if (data.Locked.Contains(level) && !Settings.ShowLockedMoons.Value)
                {
                    continue;
                }

                if (hidden && !Reveal(level))
                {
                    if (Settings.ShowHiddenMoons.Value != ShowHidden.Mystery)
                    {
                        continue;
                    }

                    Mysteries.Add(level);
                }

                moons.Add(level);
            }

            return moons;
        }

        internal static void RandomWindow(Terminal terminal)
        {
            var round = StartOfRound.Instance;
            if (terminal.moonsCatalogueList == null || round == null)
            {
                return;
            }

            var catalog = Catalog.Read(terminal);
            var data = MoonData.Read();
            var open = Moons(terminal, catalog, data)
                .Where(level => level != round.currentLevel && !data.Locked.Contains(level) && !Sells(level, data) && !Mysteries.Contains(level)
                                && catalog.Routes[level].Price <= terminal.groupCredits)
                .ToList();

            if (open.Count == 0)
            {
                Window.Show(new Window { Title = "RANDOM MOON", Text = "There is no other moon the crew can fly to right now." });
                return;
            }

            var level = open[UnityEngine.Random.Range(0, open.Count)];
            var route = catalog.Routes[level];
            var name = NameOf(level);
            var window = new Window
            {
                Title = "RANDOM MOON: " + name.ToUpperInvariant(),
                Texture = MoonPicture.For(level),
                NoPicture = level.videoReel == null,
                Side = "<alpha=#99>RISK<alpha=#FF><pos=50%>" + (string.IsNullOrEmpty(level.riskLevel) ? "-" : level.riskLevel)
                       + "\n<alpha=#99>WEATHER<alpha=#FF><pos=50%>" + (level.currentWeather == LevelWeatherType.None ? "Clear" : level.currentWeather.ToString())
                       + "\n<alpha=#99>ROUTE COST<alpha=#FF><pos=50%>" + (route.Price > 0 ? "$" + route.Price : "FREE")
                       + (route.Price > 0 ? "\n<alpha=#99>CREDITS AFTER<alpha=#FF><pos=50%>$" + (terminal.groupCredits - route.Price) : "")
            };
            window.Buttons.Add(new ToolButton { Label = route.Price > 0 ? $"GO (${route.Price})" : "GO", Click = () => Fly(terminal, level, route) });
            window.Buttons.Add(new ToolButton { Label = "ANOTHER", Off = open.Count < 2, Click = () => RandomWindow(terminal) });
            window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = Window.Close });
            Window.Show(window);
        }

        // A moon has to carry every tag that is on.
        private static void SettingsWindow(Terminal terminal)
        {
            var counts = new SortedDictionary<string, int>();
            var data = MoonData.Read();

            foreach (var level in Moons(terminal, Catalog.Read(terminal), data))
            {
                if (!Sells(level, data) && !Mysteries.Contains(level) && data.Tags.TryGetValue(level, out var tags))
                {
                    foreach (var tag in tags)
                    {
                        counts.TryGetValue(tag, out var count);
                        counts[tag] = count + 1;
                    }
                }
            }

            Action again = () =>
            {
                Navigator.Rebuild(terminal);
                SettingsWindow(terminal);
            };

            var window = new Window { Title = "MOON SETTINGS" };
            var filters = window.Group("FILTERS");
            filters.Add(State.ToolFor("CAN AFFORD", Affordable, again));
            filters.Add(State.ToolFor("CLEAR SKY", ClearSky, again));

            var tagged = counts.Count > 0 ? window.Group("TAGS: only moons with every tag that is on") : new List<ToolButton>();
            foreach (var pair in counts)
            {
                var flag = Tag + pair.Key;
                tagged.Add(new ToolButton
                {
                    Label = $"{pair.Key} ({pair.Value})",
                    Active = State.Has(flag),
                    Click = () =>
                    {
                        State.Toggle(flag);
                        again();
                    }
                });
            }

            if (State.FlagsWith(Tag).Any())
            {
                tagged.Add(new ToolButton
                {
                    Label = "CLEAR TAGS",
                    Click = () =>
                    {
                        State.ClearFlags(Tag);
                        again();
                    }
                });
            }

            Window.Show(window);
        }

        private static MenuEntry Row(Terminal terminal, Catalog catalog, MoonData data, SelectableLevel level, Target route, string name, bool simulate)
        {
            var here = StartOfRound.Instance.currentLevel == level;
            var locked = data.Locked.Contains(level);
            var weather = level.currentWeather == LevelWeatherType.None ? "" : level.currentWeather.ToString();
            var risk = string.IsNullOrEmpty(level.riskLevel) ? "-" : level.riskLevel;

            return new MenuEntry
            {
                Label = name,
                Marker = here ? Marker.Here : locked ? Marker.Locked : Marker.None,
                Command = route.Command,
                AltClick = catalog.InfoNode(route) != null ? () => Window.ShowPage(terminal, name.ToUpperInvariant(), catalog.InfoNode(route)) : (System.Action)null,
                Dim = locked || (!here && route.Price > terminal.groupCredits),
                Cells = new[] { risk, weather, "$" + route.Price },
                Keys = new IComparable[] { name, RiskRank(level.riskLevel), weather, route.Price }
            };
        }

        private static bool Reveal(SelectableLevel level)
        {
            switch (Settings.ShowHiddenMoons.Value)
            {
                case ShowHidden.Always:
                    return true;
                case ShowHidden.AfterOrbit:
                case ShowHidden.Mystery:
                    return MoonMemory.HasFlownTo(level);
                default:
                    return false;
            }
        }

        // Mystery: a hidden moon nobody has flown to shows as ??? and cannot be clicked. Its name has to be typed, as
        // without this mod.
        private static readonly HashSet<SelectableLevel> Mysteries = new HashSet<SelectableLevel>();

        private static MenuEntry MysteryRow()
        {
            return new MenuEntry
            {
                Label = "???",
                Dim = true,
                Click = () => { },
                Cells = new[] { "?", "?", "?" },
                Keys = new IComparable[] { "\uffff", int.MaxValue, "\uffff", int.MaxValue }
            };
        }

        private static Detail MysteryDetail()
        {
            return new Detail
            {
                Title = "???",
                Missing = "NO SIGNAL",
                Text = "A moon the autopilot does not list. Find out its name and fly there once, and it shows here like the others."
            };
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

        // "Safe" first, then F up to S. Every + or - is a step, so S < S+ < S++, and modded grades like SS come after.
        // Anything unreadable sorts last.
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
