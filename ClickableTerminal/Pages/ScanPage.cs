using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClickableTerminal
{
    // The game answers a scan with one sentence. This lists the scrap behind that number.
    internal static class ScanPage
    {
        // Worth most first, until the player sorts it another way.
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

                // The same split the game makes: in orbit everything counts, on a moon only what is still outside.
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
                    Icons = hands,
                    Cells = new[] { "", "$" + item.scrapValue },
                    Keys = new IComparable[] { name, hands, item.scrapValue }
                });
            }

            var page = new MenuPage
            {
                Title = "SCAN",
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

            page.Summary = $"{(inOrbit ? "In the ship" : "Outside the ship")}: {rows.Count} objects   Total: ${total}";
            page.Entries.AddRange(State.Sorted(rows));
            return page;
        }
    }
}
