using System.Collections.Generic;

namespace ClickableTerminal
{
    // The log entries the crew has found, as a list to click instead of names to type after VIEW.
    internal static class LogsPage
    {
        internal static MenuPage Build(Terminal terminal)
        {
            var files = terminal.logEntryFiles;
            if (files == null)
            {
                return null;
            }

            var catalog = Catalog.Read(terminal);
            var found = terminal.unlockedStoryLogs ?? new List<int>();
            var page = new MenuPage { Title = "LOGS" };

            foreach (var file in files)
            {
                if (file == null || !found.Contains(file.storyLogFileID) || !catalog.Logs.TryGetValue(file.storyLogFileID, out var target))
                {
                    continue;
                }

                var isNew = terminal.newlyUnlockedStoryLogs != null && terminal.newlyUnlockedStoryLogs.Contains(file.storyLogFileID);
                page.Entries.Add(new MenuEntry
                {
                    Label = (file.creatureName ?? "") + (isNew ? "  (NEW)" : ""),
                    Command = target.Command
                });
            }

            if (page.Entries.Count == 0)
            {
                page.Entries.Add(new MenuEntry { Label = "No log entries have been found yet.", IsHeader = true });
            }

            return page;
        }
    }
}
