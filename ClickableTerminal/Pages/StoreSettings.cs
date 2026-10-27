using System.Collections.Generic;
using System.Linq;

namespace ClickableTerminal
{
    // The store's SETTINGS page: the same tabs as the store, every item in them, and a button to hide or show each one. It writes HiddenItems, so the config says the same.
    internal static class StoreSettings
    {
        private static string open;
        private static readonly Dictionary<MenuEntry, MenuEntry> Shown = new Dictionary<MenuEntry, MenuEntry>();

        internal static MenuPage Build(Terminal terminal)
        {
            Shown.Clear();
            var sections = StorePage.AllSections(terminal);
            if (sections.Count == 0)
            {
                return null;
            }

            if (sections.All(section => section.Key != open))
            {
                open = sections[0].Key;
            }

            var page = new MenuPage
            {
                Title = "STORE SETTINGS",
                Subtitle = "Hidden things stay out of the store. They can still be bought by typing.",
                SelectRows = true,
                Scale = 0.7f,
                ScaleMin = 0.6f,
                ScaleMax = 0.75f,
                Sidebar = new List<MenuEntry>(),
                DetailFor = entry => Shown.TryGetValue(entry, out var row) ? Detail(terminal, row) : null
            };

            foreach (var section in sections)
            {
                var title = section.Key;
                page.Sidebar.Add(new MenuEntry
                {
                    Label = title,
                    Active = title == open,
                    Click = () =>
                    {
                        open = title;
                        Navigator.Rebuild(terminal);
                    }
                });
            }

            page.Columns.Add(new Column { Header = "NAME" });
            page.Columns.Add(new Column { Header = "", Chars = 7 });

            foreach (var row in sections.First(section => section.Key == open).Value)
            {
                var hidden = StorePage.IsHidden(row.Label);
                var entry = new MenuEntry
                {
                    Label = row.Label,
                    Picture = row.Picture,
                    Dim = hidden,
                    Click = () => { },
                    Cells = new[] { hidden ? "HIDDEN" : "" }
                };
                Shown[entry] = row;
                page.Entries.Add(entry);
            }

            return page;
        }

        private static Detail Detail(Terminal terminal, MenuEntry row)
        {
            var hidden = StorePage.IsHidden(row.Label);
            var detail = new Detail { Title = row.Label, Picture = row.Picture, Text = row.Sub };
            detail.Facts.Add(new Fact { Label = "In the store", Value = hidden ? "Hidden" : "Shown" });
            detail.Buttons.Add(new ToolButton
            {
                Label = hidden ? "SHOW" : "HIDE",
                Primary = true,
                Click = () =>
                {
                    Settings.HiddenItems.Value = Toggled(Settings.HiddenItems.Value, row.Label);
                    Navigator.Rebuild(terminal);
                }
            });
            return detail;
        }

        // The name taken out of the list when it is there, put in when it is not.
        private static string Toggled(string list, string name)
        {
            var key = WeatherNotes.Key(name);
            var parts = (list ?? "").Split(',').Select(each => each.Trim()).Where(each => each.Length > 0).ToList();
            if (parts.RemoveAll(each => WeatherNotes.Key(each) == key) == 0)
            {
                parts.Add(name);
            }

            return string.Join(", ", parts);
        }
    }
}
