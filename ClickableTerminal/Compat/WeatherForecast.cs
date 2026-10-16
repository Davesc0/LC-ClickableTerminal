using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace ClickableTerminal
{
    // WeatherRegistry's forecast: what tomorrow's weather on a moon can be, and how likely each one is.
    // Worked out from its own weights the way its "weather forecast" command does, and drawn as a page.
    internal static class WeatherForecast
    {
        internal const string Guid = "mrov.WeatherRegistry";

        private static readonly Regex Printed = new Regex(@"^Forecasting weather for (.+)$", RegexOptions.Compiled);
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Alphanumeric = new Regex(@"^[0-9]+|[-_/\\ ]", RegexOptions.Compiled);

        internal static bool Available => Chainloader.PluginInfos.ContainsKey(Guid);

        private class Chance
        {
            internal string Name;
            internal float Percent;
            internal bool Possible;
        }

        // The answer to a typed "weather forecast", drawn as our page instead.
        internal static bool Claim(Terminal terminal, View view, string printed)
        {
            if (!Available)
            {
                return false;
            }

            var first = printed.Split('\n').Select(line => Tags.Replace(line, "").Trim()).FirstOrDefault(line => line.Length > 0) ?? "";
            var match = Printed.Match(first);
            if (!match.Success)
            {
                return false;
            }

            var name = match.Groups[1].Value.Trim();
            var level = StartOfRound.Instance != null
                ? StartOfRound.Instance.levels.FirstOrDefault(each => each != null && Alphanumeric.Replace(each.PlanetName, "") == name)
                : null;
            if (level == null)
            {
                return false;
            }

            view.Builder = () => Page(terminal, level);
            return true;
        }

        internal static MenuPage Page(Terminal terminal, SelectableLevel level)
        {
            var page = new MenuPage { Title = "FORECAST", HideHeads = true, Plain = true };
            page.Columns.Add(new Column());
            page.Columns.Add(new Column { Chars = 8, AlignRight = true });

            Space(page);
            page.Entries.Add(new MenuEntry { Label = MoonsPage.NameOf(level), Big = true });

            if (!Read(level, out var now, out var chances))
            {
                page.Entries.Add(new MenuEntry { Label = "WeatherRegistry did not give a forecast." });
                ResultPages.AddBack(page, terminal, null);
                return page;
            }

            page.Entries.Add(new MenuEntry { Label = "Weather now", Cells = new[] { now } });
            Space(page);
            page.Entries.Add(new MenuEntry { Label = "TOMORROW", IsHeader = true });

            foreach (var chance in chances.Where(each => each.Possible && each.Percent > 0f))
            {
                page.Entries.Add(new MenuEntry { Label = chance.Name, Cells = new[] { chance.Percent.ToString("0.0") + "%" } });
            }

            // Weathers with a weight that this moon never gets.
            var never = chances.Where(each => !each.Possible).Select(each => each.Name).ToList();
            if (never.Count > 0)
            {
                Space(page);
                page.Entries.Add(new MenuEntry { Label = "Never here: " + string.Join(", ", never), Dim = true });
            }

            ResultPages.AddBack(page, terminal, null);
            return page;
        }

        private static bool Read(SelectableLevel level, out string now, out List<Chance> chances)
        {
            now = null;
            chances = new List<Chance>();

            try
            {
                var any = AccessTools.all;
                var manager = Reflect.Find("WeatherRegistry.WeatherManager");
                var weights = Reflect.Find("WeatherRegistry.Managers.WeightsManager");
                var kind = Reflect.Find("WeatherRegistry.Enums.WeatherWeightType");
                if (manager == null || weights == null || kind == null)
                {
                    return false;
                }

                var current = manager.GetMethod("GetCurrentWeather", any, null, new[] { typeof(SelectableLevel) }, null)?.Invoke(null, new object[] { level });
                now = current?.GetType().GetProperty("Name", any)?.GetValue(current) as string ?? level.currentWeather.ToString();

                var possible = weights.GetMethod("GetPlanetPossibleWeathers", any)?.Invoke(null, new object[] { level }) as IList;
                var allTypes = weights.GetMethod("GetWeatherWeightsAllTypes", any);
                var weathers = manager.GetMethod("GetWeathers", any)?.Invoke(null, null) as IEnumerable;
                if (allTypes == null || weathers == null)
                {
                    return false;
                }

                var byDefault = Enum.ToObject(kind, 0);
                var byWeather = Enum.ToObject(kind, 1);
                var byLevel = Enum.ToObject(kind, 2);
                var picked = new List<(object Weather, int Weight)>();
                var total = 0;

                foreach (var weather in weathers)
                {
                    if (!(allTypes.Invoke(null, new[] { level, weather }) is IDictionary all))
                    {
                        continue;
                    }

                    var fromDefault = all.Contains(byDefault) ? (int)all[byDefault] : 0;
                    var fromWeather = all.Contains(byWeather) ? (int)all[byWeather] : 0;
                    var fromLevel = all.Contains(byLevel) ? (int)all[byLevel] : 0;
                    total += fromDefault + fromWeather + fromLevel;

                    // The same pick as WeatherRegistry's own forecast: the moon's own weight, then weather to weather, then the default.
                    picked.Add((weather, fromLevel != 0 ? fromLevel : fromWeather != 0 ? fromWeather : fromDefault));
                }

                if (total <= 0)
                {
                    return false;
                }

                foreach (var (weather, weight) in picked.OrderByDescending(each => each.Weight))
                {
                    var type = weather.GetType();
                    var vanilla = type.GetProperty("VanillaWeatherType", any)?.GetValue(weather);
                    chances.Add(new Chance
                    {
                        Name = type.GetProperty("Name", any)?.GetValue(weather) as string ?? "?",
                        Percent = weight * 100f / total,
                        Possible = possible == null || vanilla == null || possible.Contains(vanilla)
                    });
                }

                return true;
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"WeatherRegistry forecast could not be read: {(e.InnerException ?? e).Message}");
                return false;
            }
        }

        private static void Space(MenuPage page)
        {
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
        }
    }
}
