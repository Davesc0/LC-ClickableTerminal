using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class ScanPage
    {
        private static readonly PageState State = new PageState { SortColumn = 2, Descending = true };

        internal static MenuPage Build(Terminal terminal)
        {
            var round = StartOfRound.Instance;
            if (round == null)
            {
                return null;
            }

            var inOrbit = round.inShipPhase;
            var rows = new List<MenuEntry>();
            var total = 0;

            foreach (var item in UnityEngine.Object.FindObjectsOfType<GrabbableObject>())
            {
                if (item == null || item.itemProperties == null || !item.itemProperties.isScrap)
                {
                    continue;
                }

                // The game's own split: in orbit everything counts, on a moon only what is still outside.
                if (!inOrbit && (item.isInShipRoom || item.isInElevator))
                {
                    continue;
                }

                var name = item.itemProperties.itemName ?? "";
                var hands = item.itemProperties.twoHanded ? 2 : 1;
                total += item.scrapValue;

                rows.Add(new MenuEntry
                {
                    Label = name,
                    Picture = item.itemProperties.itemIcon,
                    Icons = hands,
                    Cells = new[] { "", "$" + item.scrapValue },
                    Keys = new IComparable[] { name, hands, item.scrapValue }
                });
            }

            var page = new MenuPage
            {
                Title = "SCAN",
                Scale = 0.85f,
                ScaleMin = 0.7f,
                ScaleMax = 1f,
                IconColumn = 1,
                SortBy = column =>
                {
                    State.SortBy(column);
                    Navigator.Rebuild(terminal);
                }
            };

            if (rows.Count == 0)
            {
                page.Summary = inOrbit ? "There is nothing in the ship." : "There is nothing left outside the ship.";
                return page;
            }

            page.Columns.Add(new Column { Header = State.HeaderFor("NAME", 0) });
            page.Columns.Add(new Column { Header = State.HeaderFor("HANDS", 1), Chars = 7 });
            page.Columns.Add(new Column { Header = State.HeaderFor("VALUE", 2), Chars = 8, AlignRight = true });

            page.Subtitle = inOrbit ? "Everything in the ship" : "Everything still outside the ship";
            var best = rows.OrderByDescending(row => (int)row.Keys[2]).First();
            page.Extra = () =>
            {
                var panel = new Detail { Title = "TOTAL" };
                panel.Facts.Add(new Fact { Label = "Value", Value = "$" + total });
                panel.Facts.Add(new Fact { Label = "Objects", Value = rows.Count.ToString() });
                panel.Facts.Add(new Fact { Label = "Average", Value = "$" + Mathf.RoundToInt(total / (float)rows.Count) });
                panel.Facts.Add(new Fact { Label = "Most valuable", Value = $"{best.Label} {best.Cells[1]}" });
                return panel;
            };
            page.Entries.AddRange(State.Sorted(rows));
            return page;
        }
    }
}
