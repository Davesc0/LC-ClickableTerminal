using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickableTerminal
{
    internal static class BestiaryPage
    {
        private static readonly PageState State = new PageState();

        internal static MenuPage Build(Terminal terminal)
        {
            var files = terminal.enemyFiles;
            if (files == null)
            {
                return null;
            }

            var catalog = Catalog.Read(terminal);
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

            var page = new MenuPage
            {
                Title = "BESTIARY",
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
            page.Columns.Add(new Column { Header = State.HeaderFor("POWER", 2), Chars = 8 });
            page.Columns.Add(new Column { Header = State.HeaderFor("HABITAT", 3), Chars = 10 });

            // The game's own first, the ones nobody could be found for last.
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

        // The game's own files that are not about a creature, so nothing in the game can vouch for them.
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
                Label = isNew ? name + "  (NEW)" : name,
                Command = target.Command,
                Cells = new[] { "", "", "" },
                Keys = new IComparable[] { name, int.MaxValue, float.MaxValue, "" }
            };

            if (enemy != null)
            {
                // Unkillable creatures sort after everything that has health.
                var health = CreatureIndex.HealthOf(enemy);
                var habitat = enemy.isDaytimeEnemy ? "Daytime" : enemy.isOutsideEnemy ? "Surface" : "Interior";

                row.Cells = new[] { health?.ToString() ?? "-", enemy.PowerLevel.ToString("0.#"), habitat };
                row.Keys = new IComparable[] { name, health ?? int.MaxValue - 1, enemy.PowerLevel, habitat };
            }

            return row;
        }
    }
}
