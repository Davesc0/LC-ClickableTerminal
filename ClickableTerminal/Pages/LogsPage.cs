using System.Collections.Generic;

namespace ClickableTerminal
{
    internal static class LogsPage
    {
        // Reading it counts, as on the game's own page.
        private static void Read(Terminal terminal, TerminalNode file)
        {
            Window.Show(new Window { Title = (file.creatureName ?? "").ToUpperInvariant(), Text = Window.Tidy(Commands.PageText(terminal, file), file.creatureName) });

            if (terminal.newlyUnlockedStoryLogs != null && terminal.newlyUnlockedStoryLogs.Remove(file.storyLogFileID))
            {
                Navigator.Rebuild(terminal);
            }
        }

        internal static MenuPage Build(Terminal terminal)
        {
            var files = terminal.logEntryFiles;
            if (files == null)
            {
                return null;
            }

            var catalog = Catalog.Read(terminal);
            var found = terminal.unlockedStoryLogs ?? new List<int>();
            var texts = new Dictionary<MenuEntry, TerminalNode>();
            var page = new MenuPage
            {
                Title = "LOGS",
                Subtitle = $"{found.Count} of {files.Count} entries found",
                DetailFor = entry => texts.TryGetValue(entry, out var file)
                    ? new Detail { Title = file.creatureName, Text = Catalog.Excerpt(file.displayText, 3, 240), Foot = "SELECT: READ" }
                    : null
            };

            foreach (var file in files)
            {
                if (file == null || !found.Contains(file.storyLogFileID) || !catalog.Logs.TryGetValue(file.storyLogFileID, out var target))
                {
                    continue;
                }

                var isNew = terminal.newlyUnlockedStoryLogs != null && terminal.newlyUnlockedStoryLogs.Contains(file.storyLogFileID);
                var log = file;
                var row = new MenuEntry
                {
                    Label = file.creatureName ?? "",
                    Description = isNew ? "New" : null,
                    Command = target.Command,
                    Click = () => Read(terminal, log)
                };
                texts[row] = file;
                page.Entries.Add(row);
            }

            if (page.Entries.Count == 0)
            {
                page.Entries.Add(new MenuEntry { Label = "No log entries have been found yet.", IsHeader = true });
            }

            return page;
        }
    }
}
