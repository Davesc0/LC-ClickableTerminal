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

            var never = chances.Where(each => !each.Possible).Select(each => each.Name).ToList();
            if (never.Count > 0)
            {
                Space(page);
                page.Entries.Add(new MenuEntry { Label = "Never here: " + string.Join(", ", never), Dim = true });
            }

            ResultPages.AddBack(page, terminal, null);
            return page;
        }

        // The weather on a moon now and what it does. With WeatherRegistry also what it does to the scrap, and the chances for tomorrow.
        internal static void ShowWindow(SelectableLevel level)
        {
            string Named(string weather) => Tint.Weather(weather == "None" ? "Clear" : weather, weather);

            var now = CurrentName(level);
            var text = new System.Text.StringBuilder();
            text.Append("<alpha=#99>WEATHER NOW<alpha=#FF><pos=55%>").Append(Named(now)).Append('\n');
            var note = WeatherNotes.For(now);
            if (note != null)
            {
                text.Append("<alpha=#BB>").Append(note.Text);
                if (note.Mod != null && note.Mod != "the game")
                {
                    text.Append("\n<alpha=#77>From ").Append(note.Mod);
                }

                text.Append("<alpha=#FF>\n");
            }

            if (level.currentWeather != LevelWeatherType.None && Multipliers(now, out var amount, out var value))
            {
                text.Append("\n<alpha=#99>SCRAP AMOUNT<alpha=#FF><pos=55%>").Append(Times(amount));
                text.Append("\n<alpha=#99>SCRAP VALUE<alpha=#FF><pos=55%>").Append(Times(value)).Append('\n');
            }

            if (Available)
            {
                text.Append('\n');
                if (Read(level, out _, out var chances))
                {
                    text.Append("<alpha=#99>TOMORROW<alpha=#FF>\n");
                    foreach (var chance in chances.Where(each => each.Possible && each.Percent > 0f))
                    {
                        // Same width digits, so the numbers line up.
                        text.Append(Named(chance.Name)).Append("<pos=55%><mspace=0.6em>")
                            .Append(chance.Percent.ToString("0.0").PadLeft(5)).Append("</mspace>%\n");
                    }

                    var never = chances.Where(each => !each.Possible).Select(each => Named(each.Name)).ToList();
                    if (never.Count > 0)
                    {
                        text.Append("\n<alpha=#99>NEVER HERE<alpha=#BB>\n").Append(string.Join(", ", never));
                    }
                }
                else
                {
                    text.Append("WeatherRegistry did not give a forecast.");
                }
            }

            Window.Show(new Window
            {
                Title = (Available ? "FORECAST: " : "WEATHER: ") + MoonsPage.NameOf(level).ToUpperInvariant(),
                Text = text.ToString().TrimEnd()
            });
        }

        private static string Times(float by)
        {
            return "x" + by.ToString("0.##");
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

                    // The same pick as WeatherRegistry: the moon's weight, then weather to weather, then the default.
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

        private static Dictionary<string, Color> colours;
        private static float coloursTried = -100f;

        // The colour WeatherRegistry gives a weather, read once.
        internal static Color? ColorOf(string name)
        {
            if (!Available)
            {
                return null;
            }

            // Before the weathers are registered there is nothing to read; asked again a few seconds later.
            if (colours == null && Time.unscaledTime - coloursTried > 5f)
            {
                coloursTried = Time.unscaledTime;
                var found = new Dictionary<string, Color>();
                try
                {
                    foreach (var weather in Weathers())
                    {
                        var colour = weather.GetType().GetProperty("Color", AccessTools.all)?.GetValue(weather);
                        if (colour is Color known)
                        {
                            found[WeatherNotes.Key(NameOf(weather))] = known;
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.WarnOnce($"WeatherRegistry's weather colours could not be read: {(e.InnerException ?? e).Message}");
                }

                if (found.Count > 0)
                {
                    colours = found;
                }
            }

            return colours == null ? null : colours.TryGetValue(WeatherNotes.Key(name), out var result) ? result : (Color?)null;
        }

        // The name WeatherRegistry has for the weather on a moon now; the game's own without it.
        internal static string CurrentName(SelectableLevel level)
        {
            if (Available)
            {
                try
                {
                    var manager = Reflect.Find("WeatherRegistry.WeatherManager");
                    var current = manager?.GetMethod("GetCurrentWeather", AccessTools.all, null, new[] { typeof(SelectableLevel) }, null)?.Invoke(null, new object[] { level });
                    var name = current != null ? NameOf(current) : null;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
                catch (Exception e)
                {
                    Plugin.WarnOnce($"WeatherRegistry could not tell the weather on a moon: {(e.InnerException ?? e).Message}");
                }
            }

            return level.currentWeather.ToString();
        }

        // WeatherRegistry's scrap multipliers for a weather.
        internal static bool Multipliers(string name, out float amount, out float value)
        {
            amount = value = 1f;
            if (!Available)
            {
                return false;
            }

            try
            {
                var key = WeatherNotes.Key(name);
                var weather = Weathers().FirstOrDefault(each => WeatherNotes.Key(NameOf(each)) == key);
                if (weather == null)
                {
                    return false;
                }

                var any = AccessTools.all;
                amount = (float)(weather.GetType().GetProperty("ScrapAmountMultiplier", any)?.GetValue(weather) ?? 1f);
                value = (float)(weather.GetType().GetProperty("ScrapValueMultiplier", any)?.GetValue(weather) ?? 1f);
                return true;
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"WeatherRegistry's numbers for a weather could not be read: {(e.InnerException ?? e).Message}");
                return false;
            }
        }

        private static IEnumerable<object> Weathers()
        {
            var manager = Reflect.Find("WeatherRegistry.WeatherManager");
            var all = manager?.GetMethod("GetWeathers", AccessTools.all)?.Invoke(null, null) as IEnumerable;
            return all != null ? all.Cast<object>().ToList() : new List<object>();
        }

        private static string NameOf(object weather)
        {
            return weather.GetType().GetProperty("Name", AccessTools.all)?.GetValue(weather) as string;
        }

        private static void Space(MenuPage page)
        {
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
        }
    }
}
