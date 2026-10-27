using UnityEngine;

namespace ClickableTerminal
{
    // The colours other mods give their things: WeatherRegistry's weathers and Contracted's contracts, as rich text around a word. With ModColors off, the text comes back as it was.
    internal static class Tint
    {
        private static bool On => Settings.ModColors.Value;

        // A colour a mod gives its own things (Contracted's contracts).
        internal static string Mod(string text, string colour)
        {
            return Wrap(text, colour);
        }

        // A weather's name as the terminal shows it ("Clear" for none), in WeatherRegistry's colour for it.
        internal static string Weather(LevelWeatherType weather)
        {
            return Weather(weather == LevelWeatherType.None ? "Clear" : weather.ToString(), weather.ToString());
        }

        internal static string Weather(string shown, string name)
        {
            if (!On || shown == "Clear")
            {
                return shown;
            }

            var colour = WeatherForecast.ColorOf(name);
            return colour.HasValue ? "<color=#" + ColorUtility.ToHtmlStringRGB(colour.Value) + ">" + shown + "</color>" : shown;
        }

        private static string Wrap(string text, string colour)
        {
            if (!On || string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(colour))
            {
                return text;
            }

            var hex = colour.Trim();
            if (!hex.StartsWith("#"))
            {
                hex = "#" + hex;
            }

            return ColorUtility.TryParseHtmlString(hex, out var parsed)
                ? "<color=#" + ColorUtility.ToHtmlStringRGB(parsed) + ">" + text + "</color>"
                : text;
        }
    }
}
