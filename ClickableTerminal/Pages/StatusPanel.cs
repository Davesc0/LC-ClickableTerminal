using UnityEngine;

namespace ClickableTerminal
{
    internal static class StatusPanel
    {
        internal static Detail Build(Terminal terminal)
        {
            var time = TimeOfDay.Instance;
            var round = StartOfRound.Instance;
            var stats = round != null ? round.gameStats : null;
            var panel = new Detail { Title = "STATUS" };

            if (stats != null)
            {
                panel.Facts.Add(new Fact { Label = "Day", Value = (stats.daysSpent + 1).ToString() });
            }

            if (time != null)
            {
                panel.Facts.Add(new Fact { Label = "Quota " + (time.timesFulfilledQuota + 1), Value = $"${time.quotaFulfilled} / ${time.profitQuota}" });
            }

            if (round != null)
            {
                panel.Facts.Add(new Fact { Label = "Scrap on ship", Value = "$" + ScrapOnShip() });
            }

            if (time != null)
            {
                var days = Mathf.Max(0, time.daysUntilDeadline);
                panel.Facts.Add(new Fact { Label = "Deadline", Value = days == 0 ? "Today" : days == 1 ? "1 day" : days + " days" });
            }

            if (round != null)
            {
                panel.Facts.Add(new Fact { Label = "Ship", Value = round.inShipPhase ? "In orbit" : round.shipHasLanded ? "Landed" : "Flying" });

                if (round.currentLevel != null)
                {
                    var weather = round.currentLevel.currentWeather;
                    panel.Facts.Add(new Fact { Label = "Moon", Value = MoonsPage.NameOf(round.currentLevel) });
                    panel.Facts.Add(new Fact { Label = "Weather", Value = weather == LevelWeatherType.None ? "Clear" : weather.ToString() });
                }
            }

            if (stats != null)
            {
                panel.Facts.Add(new Fact { Label = "Deaths", Value = stats.deaths.ToString() });
            }

            if (Settings.Extras.Value == ExtrasPlace.StatusPanel)
            {
                foreach (var entry in ExtrasPage.Entries(terminal))
                {
                    panel.Buttons.Add(new ToolButton
                    {
                        Label = entry.LiveLabel != null ? entry.LiveLabel() : entry.Label,
                        Click = entry.Click ?? (entry.Open != null ? () => Navigator.Open(entry.Open) : (System.Action)null),
                        Primary = true
                    });
                }
            }

            return panel;
        }

        // The same sum as the HUD clock. Null in orbit.
        internal static string Clock()
        {
            var time = TimeOfDay.Instance;
            var round = StartOfRound.Instance;
            if (time == null || round == null || round.inShipPhase)
            {
                return null;
            }

            var minutes = (int)(time.normalizedTimeOfDay * (60f * time.numberOfHours)) + 360;
            var hour = minutes / 60;
            if (hour >= 24)
            {
                return "12:00 AM";
            }

            var half = hour < 12 ? "AM" : "PM";
            hour = hour > 12 ? hour % 12 : hour;
            return $"{hour}:{minutes % 60:00} {half}";
        }

        private static int ScrapOnShip()
        {
            var total = 0;
            foreach (var item in Object.FindObjectsOfType<GrabbableObject>())
            {
                if (item != null && item.itemProperties != null && item.itemProperties.isScrap && item.isInShipRoom)
                {
                    total += item.scrapValue;
                }
            }

            return total;
        }
    }
}
