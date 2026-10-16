using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class StorePage
    {
        private const string Affordable = "afford";
        private const string Folded = "fold:";

        private const string Items = "ITEMS";
        private const string Vehicles = "VEHICLES";
        private const string Upgrades = "SHIP UPGRADES";
        private const string Decorations = "DECORATIONS";
        private const string Suits = "SUITS";

        // The five kinds of thing the store sells, with the short name each has on its switch.
        private static readonly KeyValuePair<string, string>[] Groups =
        {
            new KeyValuePair<string, string>(Items, "ITEMS"),
            new KeyValuePair<string, string>(Vehicles, "VEHICLES"),
            new KeyValuePair<string, string>(Upgrades, "UPGRADES"),
            new KeyValuePair<string, string>(Decorations, "DECOR"),
            new KeyValuePair<string, string>(Suits, "SUITS")
        };

        private class Section
        {
            internal string Group;
            internal string Title;
            internal List<MenuEntry> Rows;
        }

        private static readonly PageState State = new PageState();

        internal static MenuPage Build(Terminal terminal)
        {
            return Settings.StoreCategoriesFirst.Value ? Landing(terminal) : Single(terminal);
        }

        // The store as a short list of its parts. Each opens a page of its own.
        private static MenuPage Landing(Terminal terminal)
        {
            var sections = Sections(terminal);
            var hidden = Hidden();
            var page = new MenuPage { Title = "STORE" };

            foreach (var group in Groups)
            {
                var name = group.Key;
                var count = sections.Where(section => section.Group == name && !hidden.Contains(section.Title)).Sum(section => section.Rows.Count);
                if (count == 0 || hidden.Contains(name))
                {
                    continue;
                }

                var about = count + " on offer";
                var sales = name == Items ? OnSale(terminal)
                    : sections.Where(section => section.Group == name).Sum(section => section.Rows.Count(row => row.Id > 0));
                about += sales > 0 ? $", {sales} on sale" : "";

                page.Entries.Add(new MenuEntry { Label = name, Description = about, Open = () => GroupPage(terminal, name) });
            }

            if (page.Entries.Count == 0)
            {
                return null;
            }

            if (terminal.buyableItemsList != null && terminal.buyableItemsList.Length > 0)
            {
                Cart.Tidy(terminal);
                page.Entries.Add(new MenuEntry
                {
                    Label = "CART",
                    Description = Cart.Count == 0 ? "Empty. Click items to put them in, then order them together."
                        : $"{Cart.Count} items for ${Cart.Total(terminal)}, waiting to be ordered.",
                    Open = () => CartPage.Build(terminal)
                });
            }

            return page;
        }

        private static int OnSale(Terminal terminal)
        {
            var items = terminal.buyableItemsList;
            var count = 0;

            for (var i = 0; items != null && i < items.Length; i++)
            {
                if (items[i] != null && Sale(terminal, i) < 100)
                {
                    count++;
                }
            }

            return count;
        }

        // One part of the store. Items go to the cart on a click, everything else is bought on the spot.
        private static MenuPage GroupPage(Terminal terminal, string group)
        {
            var hidden = Hidden();
            var items = group == Items;
            var sections = Sections(terminal).Where(section => section.Group == group).ToList();

            var page = new MenuPage
            {
                Title = group,
                Hint = items ? "Click: +1 to cart  Right: info" : "Right click: info",
                Dense = true,
                SortBy = column =>
                {
                    State.SortBy(column);
                    Navigator.Rebuild(terminal);
                }
            };

            page.Columns.Add(new Column { Header = State.HeaderFor("NAME", 0) });
            if (items)
            {
                // The amount in the cart sits between the two buttons that change it.
                page.Columns.Add(new Column { Header = State.HeaderFor("OWNED", 1), Chars = 5 });
                page.Columns.Add(new Column { Header = "", Chars = 3, AlignRight = true });
                page.Columns.Add(new Column { Header = State.HeaderFor("CART", 3), Chars = 4, AlignCenter = true });
                page.Columns.Add(new Column { Header = "", Chars = 3 });
                page.Columns.Add(new Column { Header = State.HeaderFor("PRICE", 5), Chars = 13, AlignRight = true });
            }
            else
            {
                page.Columns.Add(new Column { Header = State.HeaderFor("PRICE", 1), Chars = 13, AlignRight = true });
            }

            page.Tools.Add(State.ToolFor("CAN AFFORD", Affordable, () => Navigator.Rebuild(terminal)));

            if (items)
            {
                if (sections.Count > 1)
                {
                    page.Tools.Add(new ToolButton
                    {
                        Label = "CATEGORIES",
                        Active = sections.Any(section => hidden.Contains(section.Title)),
                        Click = () => Navigator.Open(() => CategoriesPage(terminal, Items))
                    });
                }

                Cart.Tidy(terminal);
                page.Tools.Add(new ToolButton
                {
                    Label = Cart.Count == 0 ? "CART" : $"CART: {Cart.Count}  ${Cart.Total(terminal)}",
                    Active = Cart.Count > 0,
                    Click = () => Navigator.Open(() => CartPage.Build(terminal))
                });
            }

            foreach (var section in sections)
            {
                if (hidden.Contains(section.Title) || section.Rows.Count == 0)
                {
                    continue;
                }

                foreach (var row in section.Rows)
                {
                    var owned = row.Cells[0];
                    var price = row.Cells[1];

                    if (!items)
                    {
                        row.Cells = new[] { price };
                        row.Keys = new[] { row.Keys[0], row.Keys[2] };
                        continue;
                    }

                    var item = row.Id;
                    var picked = Cart.Of(item);
                    Action more = () =>
                    {
                        Cart.Add(terminal, item);
                        Navigator.Rebuild(terminal);
                    };

                    row.Cells = new[]
                    {
                        owned,
                        picked > 0 ? "[-]" : "",
                        picked > 0 ? picked.ToString() : "",
                        Cart.CanAdd(terminal, item) ? "[+]" : "",
                        price
                    };
                    row.Keys = new[] { row.Keys[0], row.Keys[1], picked, picked, picked, row.Keys[2] };
                    row.Click = more;
                    row.CellClicks = new Action[]
                    {
                        null,
                        () =>
                        {
                            Cart.Remove(item);
                            Navigator.Rebuild(terminal);
                        },
                        null,
                        more,
                        null
                    };
                }

                // A part with one section needs no bar over it.
                if (sections.Count > 1)
                {
                    var title = section.Title;
                    var folded = State.Has(Folded + title);
                    page.Entries.Add(new MenuEntry
                    {
                        Label = $"{(folded ? "+" : "-")} {title}  ({section.Rows.Count})",
                        IsHeader = true,
                        Click = () =>
                        {
                            State.Toggle(Folded + title);
                            Navigator.Rebuild(terminal);
                        }
                    });

                    if (folded)
                    {
                        continue;
                    }
                }

                page.Entries.AddRange(Family(State.Sorted(section.Rows)));
            }

            return page;
        }

        // Rows named "Family - Name" that share the family, in the game's order, become one plain line with the family
        // and the rows under it with only their own names. Sorted by a column, every row keeps its full name.
        private static List<MenuEntry> Family(List<MenuEntry> rows)
        {
            if (State.SortColumn >= 0)
            {
                return rows;
            }

            string Head(MenuEntry row)
            {
                var cut = row.Label.IndexOf(" - ", StringComparison.Ordinal);
                return cut > 0 ? row.Label.Substring(0, cut) : null;
            }

            var families = rows.Select(Head).Where(head => head != null).GroupBy(head => head).Where(group => group.Count() > 1)
                .Select(group => group.Key).ToList();
            if (families.Count == 0)
            {
                return rows;
            }

            var result = new List<MenuEntry>();
            var placed = new HashSet<string>();

            foreach (var row in rows)
            {
                var head = Head(row);
                if (head == null || !families.Contains(head))
                {
                    result.Add(row);
                    continue;
                }

                if (!placed.Add(head))
                {
                    continue;
                }

                result.Add(new MenuEntry { Label = head, Dim = true });
                foreach (var member in rows.Where(other => Head(other) == head))
                {
                    member.Label = "  " + member.Label.Substring(head.Length + 3);
                    result.Add(member);
                }
            }

            return result;
        }

        // Everything on one page, the way it was before the store had parts.
        private static MenuPage Single(Terminal terminal)
        {
            var sections = Sections(terminal);
            var hidden = Hidden();

            var page = new MenuPage
            {
                Title = "STORE",
                Hint = Settings.ShowUtilitySlotIcon.Value ? "Right: info  Bag: utility slot" : "Right click: info",
                Dense = true,
                SortBy = column =>
                {
                    State.SortBy(column);
                    Navigator.Rebuild(terminal);
                }
            };

            page.Columns.Add(new Column { Header = State.HeaderFor("NAME", 0) });
            page.Columns.Add(new Column { Header = State.HeaderFor("OWNED", 1), Chars = 7 });
            page.Columns.Add(new Column { Header = State.HeaderFor("PRICE", 2), Chars = 13, AlignRight = true });

            // One switch per kind of thing, for the ones the store has at all.
            foreach (var group in Groups)
            {
                if (sections.All(section => section.Group != group.Key))
                {
                    continue;
                }

                var name = group.Key;
                page.Tools.Add(new ToolButton
                {
                    Label = group.Value,
                    Off = hidden.Contains(name),
                    Click = () =>
                    {
                        Flip(name);
                        Navigator.Rebuild(terminal);
                    }
                });
            }

            // The fine print: what the crew can afford, and every category on its own.
            page.Tools.Add(new ToolButton
            {
                Label = "...",
                Active = State.Has(Affordable) || sections.Any(section => section.Title != section.Group && hidden.Contains(section.Title)),
                Click = () => Navigator.Open(() => CategoriesPage(terminal, null))
            });

            string last = null;

            foreach (var section in sections)
            {
                if (hidden.Contains(section.Group) || hidden.Contains(section.Title) || section.Rows.Count == 0)
                {
                    continue;
                }

                // An empty line where one kind of thing ends and the next begins.
                if (last != null && last != section.Group)
                {
                    page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
                }

                last = section.Group;

                // A click on the bar folds the section away, so a long store can be read as a list of its parts.
                var title = section.Title;
                var folded = State.Has(Folded + title);
                page.Entries.Add(new MenuEntry
                {
                    Label = $"{(folded ? "+" : "-")} {title}  ({section.Rows.Count})",
                    IsHeader = true,
                    Click = () =>
                    {
                        State.Toggle(Folded + title);
                        Navigator.Rebuild(terminal);
                    }
                });

                if (!folded)
                {
                    page.Entries.AddRange(State.Sorted(section.Rows));
                }
            }

            return sections.Count > 0 || State.Has(Affordable) ? page : null;
        }

        // A tick box for every part of the store. What is switched off here stays off: it is written to the config.
        private static MenuPage CategoriesPage(Terminal terminal, string only)
        {
            var page = new MenuPage { Title = only == null ? "STORE FILTERS" : "CATEGORIES", Hint = "BACK shows the store" };
            var hidden = Hidden();

            page.Entries.Add(new MenuEntry
            {
                Label = (State.Has(Affordable) ? "[x]" : "[ ]") + " Only what the crew can afford",
                Click = () =>
                {
                    State.Toggle(Affordable);
                    Navigator.Rebuild(terminal);
                }
            });
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });

            if (hidden.Count > 0)
            {
                page.Entries.Add(new MenuEntry
                {
                    Label = "Show every category",
                    Click = () =>
                    {
                        Settings.HiddenStoreCategories.Value = "";
                        Navigator.Rebuild(terminal);
                    }
                });
            }

            // The filter above must not empty this list of the very categories it is there to switch.
            var affordable = State.Has(Affordable);
            if (affordable)
            {
                State.Toggle(Affordable);
            }

            var sections = Sections(terminal).Where(section => only == null || section.Group == only).ToList();

            if (affordable)
            {
                State.Toggle(Affordable);
            }

            foreach (var section in sections)
            {
                // Switched off as a whole kind, a single category cannot be on.
                var title = section.Title;
                var wholeKindOff = title != section.Group && hidden.Contains(section.Group);
                page.Entries.Add(new MenuEntry
                {
                    Label = $"{(hidden.Contains(title) || wholeKindOff ? "[ ]" : "[x]")} {title}  ({section.Rows.Count})",
                    Dim = wholeKindOff,
                    Click = () =>
                    {
                        Flip(wholeKindOff ? section.Group : title);
                        Navigator.Rebuild(terminal);
                    }
                });
            }

            return page;
        }

        private static HashSet<string> Hidden()
        {
            return new HashSet<string>(
                Settings.HiddenStoreCategories.Value.Split(',').Select(name => name.Trim().ToUpperInvariant()).Where(name => name.Length > 0));
        }

        private static void Flip(string name)
        {
            var hidden = Hidden();
            if (!hidden.Remove(name))
            {
                hidden.Add(name);
            }

            Settings.HiddenStoreCategories.Value = string.Join(", ", hidden.OrderBy(entry => entry));
        }

        // Everything on sale, part by part, in the order it is listed.
        private static List<Section> Sections(Terminal terminal)
        {
            var catalog = Catalog.Read(terminal);
            var sections = new List<Section>();

            AddItems(sections, terminal, catalog);
            AddVehicles(sections, terminal, catalog);
            AddUnlockables(sections, terminal, catalog);
            return sections;
        }

        private static void AddItems(List<Section> sections, Terminal terminal, Catalog catalog)
        {
            var items = terminal.buyableItemsList;
            if (items == null)
            {
                return;
            }

            var onShip = CountOnShip();
            var byCategory = new Dictionary<string, List<MenuEntry>>();

            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null || !catalog.Items.TryGetValue(i, out var target))
                {
                    continue;
                }

                onShip.TryGetValue(items[i], out var owned);
                var row = Row(terminal, catalog, target, items[i].itemName, items[i].creditsWorth, Sale(terminal, i), owned);
                if (row == null)
                {
                    continue;
                }

                row.Id = i;

                // The same test the game makes when it picks a slot for something picked up.
                if (Settings.ShowUtilitySlotIcon.Value && !items[i].isScrap && !items[i].twoHanded && !items[i].disallowUtilitySlot)
                {
                    row.Marker = Marker.Utility;
                }

                var category = Settings.CategorizeItems.Value ? ItemCategories.Of(items[i]) : Items;
                if (!byCategory.TryGetValue(category, out var rows))
                {
                    byCategory[category] = rows = new List<MenuEntry>();
                }

                rows.Add(row);
            }

            foreach (var category in ItemCategories.Order().Concat(new[] { Items }))
            {
                if (byCategory.TryGetValue(category, out var rows))
                {
                    sections.Add(new Section { Group = Items, Title = category, Rows = rows });
                }
            }
        }

        private static void AddVehicles(List<Section> sections, Terminal terminal, Catalog catalog)
        {
            var vehicles = terminal.buyableVehicles;
            if (vehicles == null)
            {
                return;
            }

            // The sales of the vehicles come after those of the items in the same array.
            var offset = terminal.buyableItemsList != null ? terminal.buyableItemsList.Length : 0;
            var rows = new List<MenuEntry>();

            for (var i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i] != null && catalog.Vehicles.TryGetValue(i, out var target))
                {
                    var row = Row(terminal, catalog, target, vehicles[i].vehicleDisplayName, vehicles[i].creditsWorth, Sale(terminal, offset + i), 0);
                    if (row != null)
                    {
                        rows.Add(row);
                    }
                }
            }

            if (rows.Count > 0)
            {
                sections.Add(new Section { Group = Vehicles, Title = Vehicles, Rows = rows });
            }
        }

        private static void AddUnlockables(List<Section> sections, Terminal terminal, Catalog catalog)
        {
            var unlockables = StartOfRound.Instance != null && StartOfRound.Instance.unlockablesList != null
                ? StartOfRound.Instance.unlockablesList.unlockables
                : null;
            if (unlockables == null)
            {
                return;
            }

            var upgrades = new List<MenuEntry>();
            var decorations = new List<MenuEntry>();
            var suits = new List<MenuEntry>();

            foreach (var pair in catalog.Unlockables)
            {
                var unlockable = pair.Key < unlockables.Count ? unlockables[pair.Key] : null;
                if (unlockable == null || unlockable.hasBeenUnlockedByPlayer || unlockable.alreadyUnlocked)
                {
                    continue;
                }

                // The decorations on offer change every quota, the upgrades are always there.
                var node = unlockable.shopSelectionNode;
                var rotating = node != null && terminal.ShipDecorSelection != null && terminal.ShipDecorSelection.Contains(node);
                if (!unlockable.alwaysInStock && !rotating)
                {
                    continue;
                }

                var price = node != null ? node.itemCost : pair.Value.Price;
                var discount = rotating ? StoreRotationCompat.Discount(terminal, node) : 0;
                if (discount > 0)
                {
                    price = StoreRotationCompat.PriceAfter(price, discount);
                }

                var row = Row(terminal, catalog, pair.Value, unlockable.unlockableName, price, 100, 0);
                if (row == null)
                {
                    continue;
                }

                if (discount > 0)
                {
                    row.Cells[1] = $"-{discount}% " + ("$" + price).PadLeft(6);
                    row.Id = discount;
                }

                var section = unlockable.unlockableType == 0 ? suits : unlockable.alwaysInStock ? upgrades : decorations;
                section.Add(row);
            }

            foreach (var section in new[]
                     {
                         new Section { Group = Upgrades, Title = Upgrades, Rows = upgrades },
                         new Section { Group = Decorations, Title = Decorations, Rows = decorations },
                         new Section { Group = Suits, Title = Suits, Rows = suits }
                     })
            {
                if (section.Rows.Count > 0)
                {
                    sections.Add(section);
                }
            }
        }

        private static MenuEntry Row(Terminal terminal, Catalog catalog, Target target, string name, int fullPrice, int sale, int owned)
        {
            // Rounded the way the game does it when it takes the money.
            var price = (int)(fullPrice * (sale / 100f));
            if (State.Has(Affordable) && price > terminal.groupCredits)
            {
                return null;
            }

            return new MenuEntry
            {
                Label = name,
                Command = target.Command,
                AltCommand = catalog.InfoFor(target),
                Dim = price > terminal.groupCredits,
                Cells = new[] { owned > 0 ? "x" + owned : "", sale < 100 ? $"-{100 - sale}% " + ("$" + price).PadLeft(6) : "$" + price },
                Keys = new IComparable[] { name, owned, price }
            };
        }

        private static int Sale(Terminal terminal, int index)
        {
            var sales = terminal.itemSalesPercentages;
            return sales != null && index >= 0 && index < sales.Length ? sales[index] : 100;
        }

        private static Dictionary<Item, int> CountOnShip()
        {
            var counts = new Dictionary<Item, int>();

            foreach (var item in UnityEngine.Object.FindObjectsOfType<GrabbableObject>())
            {
                if (item != null && item.itemProperties != null && item.isInShipRoom)
                {
                    counts.TryGetValue(item.itemProperties, out var current);
                    counts[item.itemProperties] = current + 1;
                }
            }

            return counts;
        }
    }
}
