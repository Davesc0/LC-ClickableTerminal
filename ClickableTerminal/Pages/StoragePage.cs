using System.Collections.Generic;

namespace ClickableTerminal
{
    internal static class StoragePage
    {
        internal static MenuPage Build(Terminal terminal)
        {
            var unlockables = StartOfRound.Instance != null && StartOfRound.Instance.unlockablesList != null
                ? StartOfRound.Instance.unlockablesList.unlockables
                : null;
            if (unlockables == null)
            {
                return null;
            }

            var catalog = Catalog.Read(terminal);
            var upgrades = new List<MenuEntry>();
            var decorations = new List<MenuEntry>();
            var suits = new List<MenuEntry>();

            for (var id = 0; id < unlockables.Count; id++)
            {
                var unlockable = unlockables[id];
                if (unlockable == null || !unlockable.inStorage || !catalog.Unlockables.TryGetValue(id, out var target))
                {
                    continue;
                }

                // Typing the name of a stored thing brings it back, the same word that bought it.
                var row = new MenuEntry { Label = unlockable.unlockableName, Command = target.Command };
                var section = unlockable.unlockableType == 0 ? suits : unlockable.alwaysInStock ? upgrades : decorations;
                section.Add(row);
            }

            var page = new MenuPage { Title = "STORAGE", Hint = "Click to take it out" };
            AddSection(page, "SHIP UPGRADES", upgrades);
            AddSection(page, "DECORATIONS", decorations);
            AddSection(page, "SUITS", suits);

            if (page.Entries.Count == 0)
            {
                page.Hint = null;
                page.Entries.Add(new MenuEntry { Label = "Nothing is stored.", IsHeader = true });
            }

            return page;
        }

        private static void AddSection(MenuPage page, string title, List<MenuEntry> rows)
        {
            if (rows.Count > 0)
            {
                if (page.Entries.Count > 0)
                {
                    page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
                }

                page.Entries.Add(new MenuEntry { Label = title, IsHeader = true });
                page.Entries.AddRange(rows);
            }
        }
    }
}
