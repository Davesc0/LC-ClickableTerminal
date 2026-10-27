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
                    panel.Facts.Add(new Fact { Label = "Weather", Value = Tint.Weather(weather) });
                }
            }

            if (stats != null)
            {
                panel.Facts.Add(new Fact { Label = "Deaths", Value = stats.deaths.ToString() });
            }

            if (Settings.ShipControls.Value)
            {
                ShipControls.AddTo(panel);
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

        private static int scrapOnShip;
        private static float scrapCounted = -100f;

        // Counted again only when the game has moved scrap (Patches.ShipWatch), or now and then for what a mod might move on its own.
        private static int ScrapOnShip()
        {
            if (!Patches.ShipWatch.ScrapMoved && Time.unscaledTime - scrapCounted < 30f)
            {
                return scrapOnShip;
            }

            Patches.ShipWatch.ScrapMoved = false;
            scrapCounted = Time.unscaledTime;
            var total = 0;
            foreach (var item in Object.FindObjectsOfType<GrabbableObject>())
            {
                // Counted the way GeneralImprovements' ship monitor counts it, so the two show the same sum: no bodies, no scrap without a value, no stun grenade that has gone off.
                if (item != null && item.itemProperties != null && item.itemProperties.isScrap && item.itemProperties.minValue > 0
                    && item.isInShipRoom && item.isInElevator && !(item is RagdollGrabbableObject)
                    && !(item is StunGrenadeItem grenade && grenade.hasExploded && grenade.DestroyGrenade))
                {
                    total += item.scrapValue;
                }
            }

            scrapOnShip = total;
            return total;
        }
    }
}
