using System;
using System.Collections.Generic;

namespace ClickableTerminal
{
    // The game only knows where the ship is now, so the moons flown to are kept in the save itself, the way GeneralImprovements keeps its own: deleting the save forgets them, and a day that was not saved does not count. Only the host writes the save, so a client remembers them until it leaves the lobby.
    internal static class MoonMemory
    {
        private const string Key = "ClickableTerminal_FlownTo";
        private static readonly HashSet<string> Flown = new HashSet<string>();
        private static string loadedFor;

        private static bool Wanted => Settings.ShowHiddenMoons.Value == ShowHidden.AfterOrbit || Settings.ShowHiddenMoons.Value == ShowHidden.Mystery;

        internal static void RememberFlight(SelectableLevel level)
        {
            if (!Wanted || level == null)
            {
                return;
            }

            Load();
            Flown.Add(KeyOf(level));
        }

        internal static bool HasFlownTo(SelectableLevel level)
        {
            Load();
            return level != null && Flown.Contains(KeyOf(level));
        }

        internal static void Save()
        {
            var save = SaveName();
            if (!Wanted || !Hosting() || save == null)
            {
                return;
            }

            Load();
            try
            {
                ES3.Save(Key, string.Join(",", Flown), save);
            }
            catch (Exception error)
            {
                Plugin.mlg.LogWarning("Could not save the moons flown to: " + error.Message);
            }
        }

        // A new lobby starts from the save again.
        internal static void Forget()
        {
            Flown.Clear();
            loadedFor = null;
        }

        private static void Load()
        {
            var save = SaveName();
            var key = Hosting() ? save : "client";
            if (key == null || key == loadedFor)
            {
                return;
            }

            loadedFor = key;
            Flown.Clear();
            if (!Hosting())
            {
                return;
            }

            try
            {
                foreach (var name in ES3.Load(Key, save, "").Split(','))
                {
                    if (name.Trim().Length > 0)
                    {
                        Flown.Add(name.Trim());
                    }
                }
            }
            catch (Exception error)
            {
                Plugin.mlg.LogWarning("Could not read the moons flown to: " + error.Message);
            }
        }

        private static string KeyOf(SelectableLevel level)
        {
            return Commands.Normalize(MoonsPage.NameOf(level));
        }

        private static string SaveName()
        {
            var save = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.currentSaveFileName : null;
            return string.IsNullOrEmpty(save) ? null : save;
        }

        private static bool Hosting()
        {
            return GameNetworkManager.Instance != null && GameNetworkManager.Instance.isHostingGame;
        }
    }
}
