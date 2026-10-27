using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickableTerminal
{
    internal static class MonstersPage
    {
        private static readonly PageState State = new PageState { SortColumn = 1, Descending = true };

        internal static MenuPage Build()
        {
            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            var round = StartOfRound.Instance;
            var spawned = RoundManager.Instance != null ? RoundManager.Instance.SpawnedEnemies : null;
            var detail = Settings.Monsters.Value;

            var page = new MenuPage
            {
                Title = "MONSTER SCAN",
                Scale = 0.85f,
                ScaleMin = 0.7f,
                ScaleMax = 1f,
                SortBy = column =>
                {
                    State.SortBy(column);
                    Navigator.Rebuild(terminal);
                }
            };

            page.Corners.Add(new ToolButton { Label = "COUNT AGAIN", Click = () => Navigator.Rebuild(terminal) });

            if (round == null || round.inShipPhase || spawned == null)
            {
                page.Summary = "The ship is in orbit. There is nothing to count.";
                return page;
            }

            var alive = spawned.Where(enemy => enemy != null && !enemy.isEnemyDead && enemy.enemyType != null).ToList();
            var inside = alive.Count(enemy => !enemy.isOutside);
            page.Subtitle = "Alive on " + MoonsPage.NameOf(round.currentLevel);
            page.Summary = alive.Count == 0 ? "Nothing alive on this moon." : alive.Count == 1 ? "1 creature alive." : alive.Count + " creatures alive.";

            page.Extra = () =>
            {
                var panel = new Detail { Title = "TOTAL" };
                panel.Facts.Add(new Fact { Label = "Alive", Value = alive.Count.ToString() });
                if (detail == MonsterDetail.Places)
                {
                    panel.Facts.Add(new Fact { Label = "Inside", Value = inside.ToString() });
                    panel.Facts.Add(new Fact { Label = "Outside", Value = (alive.Count - inside).ToString() });
                }

                if (detail != MonsterDetail.Count && alive.Count > 0)
                {
                    panel.Facts.Add(new Fact { Label = "Kinds", Value = alive.Select(enemy => enemy.enemyType.enemyName).Distinct().Count().ToString() });
                }

                return panel;
            };

            if (detail == MonsterDetail.Count || alive.Count == 0)
            {
                return page;
            }

            page.Columns.Add(new Column { Header = State.HeaderFor("KIND", 0) });
            if (detail == MonsterDetail.Places)
            {
                page.Columns.Add(new Column { Header = State.HeaderFor("INSIDE", 1), Chars = 7, AlignRight = true });
                page.Columns.Add(new Column { Header = State.HeaderFor("OUTSIDE", 2), Chars = 8, AlignRight = true });
            }
            else
            {
                page.Columns.Add(new Column { Header = State.HeaderFor("COUNT", 1), Chars = 6, AlignRight = true });
            }

            var rows = new List<MenuEntry>();
            foreach (var kind in alive.GroupBy(enemy => enemy.enemyType.enemyName))
            {
                var count = kind.Count();
                var indoors = kind.Count(enemy => !enemy.isOutside);
                rows.Add(detail == MonsterDetail.Places
                    ? new MenuEntry { Label = kind.Key, Cells = new[] { indoors.ToString(), (count - indoors).ToString() }, Keys = new IComparable[] { kind.Key, indoors, count - indoors } }
                    : new MenuEntry { Label = kind.Key, Cells = new[] { count.ToString() }, Keys = new IComparable[] { kind.Key, count } });
            }

            page.Entries.AddRange(State.Sorted(rows));
            return page;
        }
    }
}
