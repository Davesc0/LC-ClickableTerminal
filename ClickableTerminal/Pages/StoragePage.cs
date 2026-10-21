using System;
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
            var upgrades = new List<KeyValuePair<string, string>>();
            var decorations = new List<KeyValuePair<string, string>>();

            for (var id = 0; id < unlockables.Count; id++)
            {
                var unlockable = unlockables[id];
                if (unlockable == null || !unlockable.inStorage || !catalog.Unlockables.TryGetValue(id, out var target))
                {
                    continue;
                }

                // Typing a stored thing's name brings it back, the same word that bought it.
                var thing = new KeyValuePair<string, string>(unlockable.unlockableName, target.Command);
                (unlockable.alwaysInStock && unlockable.unlockableType != 0 ? upgrades : decorations).Add(thing);
            }

            var count = upgrades.Count + decorations.Count;
            var page = new MenuPage
            {
                Title = "STORAGE",
                Scale = 0.9f,
                ScaleMin = 0.8f,
                ScaleMax = 1f,
                Subtitle = count == 1 ? "1 thing put away" : count + " things put away",
                Hint = count > 0 ? "Click to take it out" : null
            };

            if (count == 0)
            {
                page.Summary = "Nothing is stored.";
                return page;
            }

            page.Columns.Add(new Column { Header = "SHIP UPGRADES" });
            page.Columns.Add(new Column { Header = "DECORATIONS", Share = 0.5f });

            for (var i = 0; i < Math.Max(upgrades.Count, decorations.Count); i++)
            {
                var left = i < upgrades.Count ? upgrades[i] : default(KeyValuePair<string, string>);
                var right = i < decorations.Count ? decorations[i] : default(KeyValuePair<string, string>);
                var rightCommand = right.Value;

                page.Entries.Add(new MenuEntry
                {
                    Label = left.Key ?? "",
                    Command = left.Value,
                    Cells = new[] { right.Key ?? "" },
                    CellClicks = new Action[] { rightCommand != null ? () => Commands.Submit(terminal, rightCommand) : (Action)null }
                });
            }

            return page;
        }
    }
}
