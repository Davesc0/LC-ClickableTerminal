using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickableTerminal
{
    internal static class BestiaryPage
    {
        private static readonly PageState State = new PageState();
        private static readonly Dictionary<MenuEntry, TerminalNode> Files = new Dictionary<MenuEntry, TerminalNode>();

        internal static MenuPage Build(Terminal terminal)
        {
            var files = terminal.enemyFiles;
            if (files == null)
            {
                return null;
            }

            var catalog = Catalog.Read(terminal);
            Files.Clear();
            var scanned = terminal.scannedEnemyIDs ?? new List<int>();
            var rows = new List<KeyValuePair<string, MenuEntry>>();
            var grouped = Settings.GroupCreaturesByAuthor.Value;
            var credits = grouped ? Origins.EnemyCredits() : null;

            foreach (var file in files)
            {
                if (file != null && (Settings.ShowAllCreatures.Value || scanned.Contains(file.creatureFileID))
                    && catalog.Creatures.TryGetValue(file.creatureFileID, out var target))
                {
                    var author = grouped ? AuthorOf(file, credits) : Origins.Vanilla;
                    rows.Add(new KeyValuePair<string, MenuEntry>(author, Row(terminal, file, target)));
                }
            }

            var known = files.Count(file => file != null && catalog.Creatures.ContainsKey(file.creatureFileID));
            var page = new MenuPage
            {
                Title = "BESTIARY",
                Scale = 0.7f,
                ScaleMin = 0.6f,
                ScaleMax = 0.8f,
                Subtitle = $"{scanned.Count(id => catalog.Creatures.ContainsKey(id))} of {known} creatures scanned",
                DetailFor = entry => Files.TryGetValue(entry, out var file) ? Detail(file) : null,
                SortBy = column =>
                {
                    State.SortBy(column);
                    Navigator.Rebuild(terminal);
                }
            };

            if (rows.Count == 0)
            {
                page.Entries.Add(new MenuEntry { Label = "No data collected on wildlife. Scans are required.", IsHeader = true });
                return page;
            }

            page.Columns.Add(new Column { Header = State.HeaderFor("NAME", 0) });
            page.Columns.Add(new Column { Header = State.HeaderFor("HP", 1), Chars = 5 });
            page.Columns.Add(new Column { Header = State.HeaderFor("POWER", 2), Chars = 7 });
            page.Columns.Add(new Column { Header = State.HeaderFor("HABITAT", 3), Chars = 10 });

            var groups = rows
                .GroupBy(row => row.Key)
                .OrderBy(group => group.Key == Origins.Vanilla ? 0 : group.Key == Origins.Unknown ? 2 : 1)
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groups.Count < 2)
            {
                page.Entries.AddRange(State.Sorted(rows.Select(row => row.Value).ToList()));
                return page;
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

            return page;
        }

        private static Detail Detail(TerminalNode file)
        {
            var detail = new Detail
            {
                Texture = file.displayTexture,
                Title = file.creatureName,
                Text = Catalog.Excerpt(file.displayText, 2, 170),
                Foot = "SELECT: OPEN FILE"
            };

            foreach (var figure in Figures(file))
            {
                detail.Facts.Add(new Fact { Label = figure.Key, Value = figure.Value });
            }

            return detail;
        }

        private static List<KeyValuePair<string, string>> Figures(TerminalNode file)
        {
            var figures = new List<KeyValuePair<string, string>>();
            var enemy = CreatureIndex.Find(file);

            var danger = Catalog.Field(file.displayText, "danger level");
            if (danger != null)
            {
                figures.Add(new KeyValuePair<string, string>("Danger", danger));
            }

            if (enemy != null)
            {
                var health = CreatureIndex.HealthOf(enemy);
                figures.Add(new KeyValuePair<string, string>("Health", health?.ToString() ?? "Can't be killed"));
                figures.Add(new KeyValuePair<string, string>("Power", enemy.PowerLevel.ToString("0.#")));
                figures.Add(new KeyValuePair<string, string>("Max at once", enemy.MaxCount.ToString()));
                figures.Add(new KeyValuePair<string, string>("Stunnable", enemy.canBeStunned ? "Yes" : "No"));
                figures.Add(new KeyValuePair<string, string>("Habitat", enemy.isDaytimeEnemy ? "Daytime" : enemy.isOutsideEnemy ? "Surface" : "Interior"));
            }

            return figures;
        }

        // Opening it counts as reading it, like the game's own page.
        private static void FileWindow(Terminal terminal, TerminalNode file)
        {
            var side = new System.Text.StringBuilder();

            // The danger is already in the file's text, often as a whole sentence.
            foreach (var figure in Figures(file).Where(figure => figure.Key != "Danger"))
            {
                side.Append("<alpha=#99>").Append(figure.Key.ToUpperInvariant()).Append("<alpha=#FF><pos=55%>").Append(figure.Value).Append('\n');
            }

            var picture = file.displayVideo != null ? MoonPicture.For(file.displayVideo) : file.displayTexture;
            Window.Show(new Window
            {
                Title = (file.creatureName ?? "").ToUpperInvariant(),
                Texture = picture,
                NoPicture = picture == null,
                Side = side.ToString().TrimEnd(),
                Text = Window.Tidy(Commands.PageText(terminal, file), file.creatureName)
            });

            if (terminal.newlyScannedEnemyIDs != null && terminal.newlyScannedEnemyIDs.Remove(file.creatureFileID))
            {
                Navigator.Rebuild(terminal);
            }
        }

        // Game files that are not about a creature, so nothing in the game can vouch for them.
        private static readonly HashSet<string> VanillaPlants = new HashSet<string> { "vain shrouds", "vain shroud" };

        private static string AuthorOf(TerminalNode file, Dictionary<EnemyType, string> credits)
        {
            var enemy = CreatureIndex.Find(file);
            if (enemy == null && VanillaPlants.Contains(Commands.Normalize(file.creatureName ?? "")))
            {
                return Origins.Vanilla;
            }

            return Origins.OfEnemy(enemy, credits);
        }

        private static MenuEntry Row(Terminal terminal, TerminalNode file, Target target)
        {
            var name = file.creatureName ?? "";
            var isNew = terminal.newlyScannedEnemyIDs != null && terminal.newlyScannedEnemyIDs.Contains(file.creatureFileID);
            var enemy = CreatureIndex.Find(file);

            var row = new MenuEntry
            {
                Label = name,
                Sub = isNew ? "NEW" : null,
                Command = target.Command,
                Click = () => FileWindow(terminal, file),
                Cells = new[] { "", "", "" },
                Keys = new IComparable[] { name, int.MaxValue, float.MaxValue, "" }
            };

            Files[row] = file;

            if (enemy != null)
            {
                var health = CreatureIndex.HealthOf(enemy);
                var habitat = enemy.isDaytimeEnemy ? "Daytime" : enemy.isOutsideEnemy ? "Surface" : "Interior";

                row.Cells = new[] { health?.ToString() ?? "-", enemy.PowerLevel.ToString("0.#"), habitat };
                row.Keys = new IComparable[] { name, health ?? int.MaxValue - 1, enemy.PowerLevel, habitat };
            }

            return row;
        }
    }
}
