using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;

namespace ClickableTerminal
{
    // The game only knows where the ship is now, so visited moons are kept in a file of our own.
    internal static class MoonMemory
    {
        private const string Flown = "FlownTo";
        private const string Landed = "LandedOn";

        private static ConfigFile file;

        internal static void RememberFlight(SelectableLevel level)
        {
            Remember(Flown, level);
        }

        internal static void RememberLanding(SelectableLevel level)
        {
            Remember(Flown, level);
            Remember(Landed, level);
        }

        internal static bool HasFlownTo(SelectableLevel level)
        {
            return Knows(Flown, level);
        }

        private static void Remember(string list, SelectableLevel level)
        {
            var entry = Entry(list);
            if (entry == null || level == null || Knows(list, level))
            {
                return;
            }

            var names = Split(entry.Value);
            names.Add(KeyOf(level));
            entry.Value = string.Join(", ", names);
        }

        private static bool Knows(string list, SelectableLevel level)
        {
            var entry = Entry(list);
            return entry != null && level != null && Split(entry.Value).Contains(KeyOf(level));
        }

        private static string KeyOf(SelectableLevel level)
        {
            return Commands.Normalize(MoonsPage.NameOf(level));
        }

        private static ConfigEntry<string> Entry(string list)
        {
            var save = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.currentSaveFileName : null;
            if (string.IsNullOrEmpty(save))
            {
                return null;
            }

            if (file == null)
            {
                file = new ConfigFile(Path.Combine(Paths.ConfigPath, "ClickableTerminal.moons.cfg"), true);
            }

            return file.Bind(save, list, "", "Kept by ClickableTerminal for ShowHiddenMoons. Clear the line to forget.");
        }

        private static List<string> Split(string value)
        {
            return (value ?? "").Split(',').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();
        }
    }
}
