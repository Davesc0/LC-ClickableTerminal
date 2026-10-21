using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class StorePage
    {
        private const string Items = "ITEMS";
        private const string Vehicles = "VEHICLES";
        private const string Upgrades = "SHIP UPGRADES";
        private const string Decorations = "DECORATIONS";
        private const string Suits = "SUITS";

        private class Section
        {
            internal string Group;
            internal string Title;
            internal List<MenuEntry> Rows;
        }

        private class Facts
        {
            internal Sprite Picture;
            internal string Name;
            internal string Text;
            internal string Kind;
            internal string Weight;
            internal string Price;
            internal int Owned;
            internal Target Target;
            internal bool IsItem;
            internal int Item;
            internal TerminalNode Info;
            internal bool Utility;
        }

        private static readonly PageState State = new PageState();
        private static readonly Dictionary<MenuEntry, Facts> About = new Dictionary<MenuEntry, Facts>();
        private static string open;

        internal static MenuPage Build(Terminal terminal)
        {
            var hidden = Hidden();
            var sections = Sections(terminal).Where(section => section.Rows.Count > 0 && !hidden.Contains(section.Title) && !hidden.Contains(section.Group)).ToList();
            if (sections.Count == 0)
            {
                return null;
            }

            var hasSaved = SavedCarts.All.Count > 0;
            if (sections.All(section => section.Title != open) && !(open == SavedTab && hasSaved))
            {
                open = sections.Count > 0 ? sections[0].Title : null;
            }

            var shown = sections.FirstOrDefault(section => section.Title == open);
            var items = shown != null && shown.Group == Items;
            var sales = OnSale(terminal);

            var page = new MenuPage
            {
                Title = "STORE",
                Subtitle = open == SavedTab ? "Carts you saved, to order again" : shown != null ? shown.Title + (sales > 0 ? $"  -  {sales} on sale" : "") : null,
                Hint = "Click: select  Right: info",
                SelectRows = true,
                Scale = 0.7f,
                ScaleMin = 0.6f,
                ScaleMax = 0.75f,
                Sidebar = new List<MenuEntry>(),
                DetailFor = entry => SavedRows.TryGetValue(entry, out var saved) ? SavedDetail(terminal, saved) : Detail(terminal, entry),
                Extra = items || Cart.Count > 0 ? (Func<Detail>)(() => CartPanel(terminal)) : null,
                SortBy = column =>
                {
                    State.SortBy(column);
                    Navigator.Rebuild(terminal);
                }
            };

            foreach (var section in sections)
            {
                var title = section.Title;
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

            if (hasSaved)
            {
                page.Sidebar.Add(new MenuEntry
                {
                    Label = SavedTab,
                    Active = open == SavedTab,
                    Click = () =>
                    {
                        open = SavedTab;
                        Navigator.Rebuild(terminal);
                    }
                });
            }

            if (open == SavedTab)
            {
                Cart.Tidy(terminal);
                page.Extra = () => CartPanel(terminal);
                AddSavedRows(terminal, page);
                return page;
            }

            page.Columns.Add(new Column { Header = State.HeaderFor("NAME", 0) });
            if (items)
            {
                page.Columns.Add(new Column { Header = "", Chars = 3 });
            }

            page.Columns.Add(new Column { Header = State.HeaderFor("PRICE", items ? 2 : 1), Chars = 5, AlignRight = true });

            Cart.Tidy(terminal);

            if (shown == null)
            {
                page.Entries.Add(new MenuEntry { Label = "Nothing on offer here.", Dim = true });
                return page;
            }

            foreach (var row in shown.Rows)
            {
                var picked = items ? Cart.Of(row.Id) : 0;
                if (items)
                {
                    var item = row.Id;
                    row.Cells = new[] { Cart.CanAdd(terminal, item) ? "[+]" : "", row.Cells[1] };
                    row.Keys = new[] { row.Keys[0], picked, row.Keys[2] };
                    row.CellClicks = new Action[]
                    {
                        () =>
                        {
                            Cart.Add(terminal, item);
                            Navigator.Rebuild(terminal);
                        },
                        null
                    };
                }
                else
                {
                    row.Cells = new[] { row.Cells[1] };
                    row.Keys = new[] { row.Keys[0], row.Keys[2] };
                }

                if (picked > 0)
                {
                    row.Sub = $"IN CART: {picked}" + (row.Sub != null ? "  " + row.Sub : "");
                }
            }

            page.Entries.AddRange(Family(State.Sorted(shown.Rows)));
            return page;
        }

        private static Detail Detail(Terminal terminal, MenuEntry entry)
        {
            if (!About.TryGetValue(entry, out var facts))
            {
                return null;
            }

            var detail = new Detail { Picture = facts.Picture, Title = facts.Name, Text = facts.Text };

            if (facts.IsItem)
            {
                var item = facts.Item;
                detail.Buttons.Add(new ToolButton
                {
                    Label = Cart.Of(item) > 0 ? $"ADD TO CART ({Cart.Of(item)})" : "ADD TO CART",
                    Off = !Cart.CanAdd(terminal, item),
                    Click = () =>
                    {
                        Cart.Add(terminal, item);
                        Navigator.Rebuild(terminal);
                    }
                });
                if (Cart.Of(item) > 0)
                {
                    detail.Buttons.Add(new ToolButton
                    {
                        Label = "REMOVE",
                        Click = () =>
                        {
                            Cart.Remove(item);
                            Navigator.Rebuild(terminal);
                        }
                    });
                }

                detail.Buttons.Add(new ToolButton { Label = "BUY NOW", Click = () => AskToBuy(terminal, facts) });
            }
            else
            {
                detail.Buttons.Add(new ToolButton { Label = "BUY", Click = () => AskToBuy(terminal, facts) });
            }

            if (facts.Info != null)
            {
                detail.Buttons.Add(new ToolButton { Label = "INFO", Click = () => Window.ShowPage(terminal, facts.Name.ToUpperInvariant(), facts.Info) });
            }

            detail.Facts.Add(new Fact { Label = "Type", Value = facts.Kind });
            if (facts.Weight != null)
            {
                detail.Facts.Add(new Fact { Label = "Weight", Value = facts.Weight });
            }

            detail.Facts.Add(new Fact { Label = "Price", Value = facts.Price });
            if (facts.IsItem)
            {
                detail.Facts.Add(new Fact { Label = "Utility slot", Value = facts.Utility ? "Yes" : "No" });
            }
            if (facts.Owned > 0)
            {
                detail.Facts.Add(new Fact { Label = "On the ship", Value = facts.Owned.ToString() });
            }

            return detail;
        }

        internal static void OfferUpgrade(Terminal terminal, string name, string text)
        {
            var window = new Window { Title = "NO " + name.ToUpperInvariant(), Text = text };

            var catalog = Catalog.Read(terminal);
            var unlockables = StartOfRound.Instance != null && StartOfRound.Instance.unlockablesList != null
                ? StartOfRound.Instance.unlockablesList.unlockables
                : null;
            var key = Commands.Normalize(name);
            var found = unlockables == null ? default : catalog.Unlockables.FirstOrDefault(pair =>
                pair.Key < unlockables.Count && unlockables[pair.Key] != null && Commands.Normalize(unlockables[pair.Key].unlockableName) == key);

            if (found.Value != null && found.Value.Confirm != null)
            {
                var target = found.Value;
                window.Buttons.Add(new ToolButton
                {
                    Label = $"BUY ${target.Price}",
                    Off = target.Price > terminal.groupCredits,
                    Click = () =>
                    {
                        Window.Show(new Window { Title = name.ToUpperInvariant(), Text = Commands.Act(terminal, target.Confirm, 1) });
                        Navigator.Rebuild(terminal);
                    }
                });
            }

            window.Buttons.Add(new ToolButton { Label = "OK", Click = Window.Close });
            Window.Show(window);
        }

        private static void AskToBuy(Terminal terminal, Facts facts)
        {
            var window = new Window
            {
                Title = "BUY " + facts.Name.ToUpperInvariant() + "?",
                Text = $"{facts.Name}<pos=65%>{facts.Price}\nCredits after<pos=65%>${terminal.groupCredits - PriceOf(facts)}"
            };
            window.Buttons.Add(new ToolButton { Label = "BUY", Click = () => Buy(terminal, facts) });
            window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = Window.Close });
            Window.Show(window);
        }

        private static int PriceOf(Facts facts)
        {
            var digits = facts.Price.Substring(facts.Price.LastIndexOf('$') + 1);
            return int.TryParse(digits, out var price) ? price : 0;
        }

        private static void Buy(Terminal terminal, Facts facts)
        {
            if (facts.Target.Confirm == null)
            {
                Commands.Submit(terminal, facts.Target.Command);
                return;
            }

            var answer = Commands.Act(terminal, facts.Target.Confirm, 1);
            Navigator.Rebuild(terminal);
            terminal.StartCoroutine(RebuildLater(terminal));
            Window.Show(new Window { Title = "ORDER: " + facts.Name.ToUpperInvariant(), Text = answer });
        }

        private static System.Collections.IEnumerator RebuildLater(Terminal terminal)
        {
            foreach (var wait in new[] { 0.3f, 1f })
            {
                yield return new WaitForSecondsRealtime(wait);
                if (Navigator.Current != null && Navigator.Current.Page != null && Navigator.Current.Page.Title == "STORE")
                {
                    Navigator.Rebuild(terminal);
                }
            }
        }

        private static void CartWindow(Terminal terminal)
        {
            var items = terminal.buyableItemsList;
            var total = Cart.Total(terminal);
            var text = new System.Text.StringBuilder();

            text.Append("Items<pos=75%>").Append(Cart.Count + terminal.numberOfItemsInDropship).Append(" / ").Append(DropshipLimit.Value);
            text.Append("\nTotal<pos=75%>$").Append(total);
            if (Cart.Count == 0)
            {
                text.Append("\n\nThe cart is empty.");
            }

            var window = new Window { Title = "CART", Text = text.ToString(), GroupsInLine = true };

            Action again = () =>
            {
                Navigator.Rebuild(terminal);
                CartWindow(terminal);
            };

            foreach (var line in Cart.Lines)
            {
                var item = line.Key;
                var row = window.Group($"{items[item].itemName}<pos=60%>${Cart.PriceOf(terminal, item) * line.Value}");
                row.Add(new ToolButton
                {
                    Label = "<",
                    Narrow = true,
                    Click = () =>
                    {
                        Cart.Remove(item);
                        again();
                    }
                });
                row.Add(new ToolButton { Label = line.Value.ToString(), Bare = true });
                row.Add(new ToolButton
                {
                    Label = ">",
                    Narrow = true,
                    Off = !Cart.CanAdd(terminal, item),
                    Click = () =>
                    {
                        Cart.Add(terminal, item);
                        again();
                    }
                });
            }

            if (Cart.Count > 0)
            {
                window.Buttons.Add(new ToolButton
                {
                    Label = "ORDER",
                    Off = total > terminal.groupCredits,
                    Click = () =>
                    {
                        Window.Close();
                        CartPage.Order(terminal);
                    }
                });
                window.Buttons.Add(new ToolButton
                {
                    Label = "EMPTY",
                    Click = () =>
                    {
                        Cart.Clear();
                        again();
                    }
                });
                window.RightButtons.Add(new ToolButton { Label = "SAVE CART", Click = () => SaveWindow(terminal) });
            }

            Window.Show(window);
        }

        private static void SaveWindow(Terminal terminal)
        {
            Commands.Type(terminal, "");
            var window = new Window { Title = "SAVE CART", Text = "Type a name for this cart, then Enter. It shows under SAVED CARTS in the store.", Longest = 24 };
            window.Send = name =>
            {
                name = (name ?? "").Trim();
                if (name.Length == 0)
                {
                    return;
                }

                Commands.Type(terminal, "");
                SavedCarts.Save(terminal, name);
                Navigator.Rebuild(terminal);
                Window.Show(new Window { Title = "SAVE CART", Text = $"Saved as \"{name}\"." });
            };
            window.Buttons.Add(new ToolButton { Label = "SAVE", Click = () => window.Send(Commands.Typed(terminal)) });
            window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = Window.Close });
            Window.Show(window);
        }

        private const string SavedTab = "SAVED CARTS";
        private static readonly Dictionary<MenuEntry, SavedCarts.Saved> SavedRows = new Dictionary<MenuEntry, SavedCarts.Saved>();

        private static void AddSavedRows(Terminal terminal, MenuPage page)
        {
            SavedRows.Clear();
            page.Columns.Add(new Column { Header = "NAME" });
            page.Columns.Add(new Column { Header = "ITEMS", Chars = 5, AlignRight = true });
            page.Columns.Add(new Column { Header = "PRICE", Chars = 6, AlignRight = true });

            foreach (var saved in SavedCarts.All)
            {
                var row = new MenuEntry
                {
                    Label = saved.Name,
                    Click = () => { },
                    Cells = new[] { saved.Count.ToString(), "$" + SavedCarts.Total(terminal, saved) }
                };
                SavedRows[row] = saved;
                page.Entries.Add(row);
            }
        }

        private static Detail SavedDetail(Terminal terminal, SavedCarts.Saved saved)
        {
            var detail = new Detail
            {
                Title = saved.Name,
                Text = string.Join("\n", saved.Lines.Select(line => $"{line.Value}x {line.Key}"))
            };
            detail.Facts.Add(new Fact { Label = "Items", Value = saved.Count.ToString() });
            detail.Facts.Add(new Fact { Label = "Price today", Value = "$" + SavedCarts.Total(terminal, saved) });

            detail.Buttons.Add(new ToolButton
            {
                Label = "LOAD",
                Click = () =>
                {
                    Cart.Clear();
                    Loaded(terminal, saved);
                }
            });
            detail.Buttons.Add(new ToolButton { Label = "ADD", Click = () => Loaded(terminal, saved) });
            detail.Buttons.Add(new ToolButton
            {
                Label = "DELETE",
                Click = () =>
                {
                    var window = new Window { Title = "DELETE CART", Text = $"Delete the saved cart \"{saved.Name}\"?" };
                    window.Buttons.Add(new ToolButton
                    {
                        Label = "DELETE",
                        Click = () =>
                        {
                            SavedCarts.Delete(saved);
                            Window.Close();
                            Navigator.Rebuild(terminal);
                        }
                    });
                    window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = Window.Close });
                    Window.Show(window);
                }
            });
            return detail;
        }

        private static void Loaded(Terminal terminal, SavedCarts.Saved saved)
        {
            var left = SavedCarts.Load(terminal, saved);
            Navigator.Rebuild(terminal);
            if (left > 0)
            {
                Window.Show(new Window
                {
                    Title = "CART",
                    Text = $"{left} of the items did not go in: the store does not sell them now, the dropship is full, or the credits do not reach."
                });
            }
            else
            {
                CartWindow(terminal);
            }
        }

        private static Detail CartPanel(Terminal terminal)
        {
            var items = terminal.buyableItemsList;
            var panel = new Detail { Title = "CART" };

            if (Cart.Count == 0)
            {
                panel.Text = "Empty. ADD TO CART puts the picked item here.";
                return panel;
            }

            var lines = Cart.Lines.ToList();
            panel.Text = string.Join("\n", lines.Take(3).Select(line => $"{line.Value}x {items[line.Key].itemName}"))
                         + (lines.Count > 3 ? $"\n+{lines.Count - 3} more" : "");

            var total = Cart.Total(terminal);
            panel.Facts.Add(new Fact { Label = "Items", Value = $"{Cart.Count + terminal.numberOfItemsInDropship} / {DropshipLimit.Value}" });
            panel.Facts.Add(new Fact { Label = "Total", Value = "$" + total });
            panel.Buttons.Add(new ToolButton { Label = "ORDER", Primary = true, Off = total > terminal.groupCredits, Click = () => CartPage.Order(terminal) });
            panel.Buttons.Add(new ToolButton { Label = "VIEW ALL", Click = () => CartWindow(terminal) });
            panel.Buttons.Add(new ToolButton
            {
                Label = "EMPTY",
                Click = () =>
                {
                    Cart.Clear();
                    Navigator.Rebuild(terminal);
                }
            });
            return panel;
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

        // Rows named "Family - Name" in the game's order become a family line with the members under it. Sorted by a
        // column, every row keeps its full name.
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
            var listed = new HashSet<MenuEntry>();

            foreach (var row in rows)
            {
                if (listed.Contains(row))
                {
                    continue;
                }

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

                result.Add(new MenuEntry { Label = head, IsHeader = true });
                foreach (var member in rows.Where(other => Head(other) == head).ToList())
                {
                    listed.Add(member);
                    member.Label = member.Label.Substring(head.Length + 3);
                    result.Add(member);
                }
            }

            return result;
        }

        private static HashSet<string> Hidden()
        {
            return new HashSet<string>(
                Settings.HiddenStoreCategories.Value.Split(',').Select(name => name.Trim().ToUpperInvariant()).Where(name => name.Length > 0));
        }

        private static List<Section> Sections(Terminal terminal)
        {
            var catalog = Catalog.Read(terminal);
            var sections = new List<Section>();
            About.Clear();

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
                var item = items[i];
                if (item == null || !catalog.Items.TryGetValue(i, out var target))
                {
                    continue;
                }

                onShip.TryGetValue(item, out var owned);
                var category = Settings.CategorizeItems.Value ? ItemCategories.Of(item) : Items;
                var row = Row(terminal, catalog, target, item.itemName, item.creditsWorth, Sale(terminal, i), owned, category, item.itemIcon);
                if (row == null)
                {
                    continue;
                }

                row.Id = i;
                About[row].Weight = Weight(item);
                About[row].IsItem = true;
                About[row].Item = i;

                // The same test the game makes when it picks a slot.
                if (!item.isScrap && !item.twoHanded && !item.disallowUtilitySlot)
                {
                    About[row].Utility = true;
                }

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

        private static string Weight(Item item)
        {
            var pounds = Mathf.RoundToInt(Mathf.Clamp(item.weight - 1f, 0f, 100f) * 105f);
            return pounds + " lb";
        }

        private static void AddVehicles(List<Section> sections, Terminal terminal, Catalog catalog)
        {
            var vehicles = terminal.buyableVehicles;
            if (vehicles == null)
            {
                return;
            }

            // The vehicles' sales come after the items' in the same array.
            var offset = terminal.buyableItemsList != null ? terminal.buyableItemsList.Length : 0;
            var rows = new List<MenuEntry>();
            var owned = UnityEngine.Object.FindObjectsOfType<VehicleController>().Count(vehicle => vehicle != null && !vehicle.carDestroyed);

            for (var i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i] != null && catalog.Vehicles.TryGetValue(i, out var target))
                {
                    var row = Row(terminal, catalog, target, vehicles[i].vehicleDisplayName, vehicles[i].creditsWorth, Sale(terminal, offset + i), owned, "Vehicle", null);
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

                var suit = unlockable.unlockableType == 0;
                var kind = suit ? "Suit" : unlockable.alwaysInStock ? "Ship upgrade" : "Decoration";

                // A broken modded decoration is left out, the rest of the store still shows.
                MenuEntry row;
                try
                {
                    row = Row(terminal, catalog, pair.Value, unlockable.unlockableName, price, 100, 0, kind, null);
                }
                catch (System.Exception e)
                {
                    Plugin.WarnOnce($"The store left out \"{unlockable.unlockableName}\", it could not be read: {e}");
                    continue;
                }

                if (row == null)
                {
                    continue;
                }

                if (discount > 0)
                {
                    row.Cells[1] = $"-{discount}% ${price}";
                    About[row].Price = row.Cells[1];
                    row.Id = discount;
                }

                (suit ? suits : unlockable.alwaysInStock ? upgrades : decorations).Add(row);
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

        private static MenuEntry Row(Terminal terminal, Catalog catalog, Target target, string name, int fullPrice, int sale, int owned, string kind, Sprite picture)
        {
            // Rounded the way the game rounds when it takes the money.
            var price = (int)(fullPrice * (sale / 100f));
            var shownPrice = sale < 100 ? $"-{100 - sale}% ${price}" : "$" + price;
            var row = new MenuEntry
            {
                Label = name,
                Sub = catalog.About(target, 1, 70),
                Picture = picture,
                Command = target.Command,
                AltClick = Info(terminal, name, catalog.InfoNode(target)),
                Dim = price > terminal.groupCredits,
                Cells = new[] { owned > 0 ? owned.ToString() : "", shownPrice },
                Keys = new IComparable[] { name, owned, price }
            };

            About[row] = new Facts
            {
                Picture = picture,
                Name = name,
                Text = catalog.About(target, 2, 160),
                Kind = kind,
                Price = shownPrice,
                Owned = owned,
                Target = target,
                Info = catalog.InfoNode(target)
            };
            return row;
        }

        private static Action Info(Terminal terminal, string name, TerminalNode node)
        {
            return node != null ? () => Window.ShowPage(terminal, name.ToUpperInvariant(), node) : (Action)null;
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
