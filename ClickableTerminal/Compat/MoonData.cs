using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;

namespace ClickableTerminal
{
    // Read by reflection, so neither LethalLevelLoader nor DawnLib has to be installed.
    internal class MoonData
    {
        internal readonly Dictionary<SelectableLevel, HashSet<string>> Tags = new Dictionary<SelectableLevel, HashSet<string>>();
        internal readonly HashSet<SelectableLevel> Hidden = new HashSet<SelectableLevel>();
        internal readonly HashSet<SelectableLevel> Locked = new HashSet<SelectableLevel>();

        internal const string Vanilla = "VANILLA";
        internal readonly Dictionary<SelectableLevel, string> Authors = new Dictionary<SelectableLevel, string>();

        internal bool LevelLoader;

        // TooManyMoons: Drawn is today's draw. A moon open without being drawn is one that mod keeps open next to it.
        internal const string Rotation = "ROTATION";
        internal bool Rotating;
        internal readonly HashSet<SelectableLevel> Drawn = new HashSet<SelectableLevel>();

        private static MoonData last;
        private static float lastAt = -100f;

        // Asked on every redraw, but this does not change between clicks, so it is read at most once every few seconds.
        internal static MoonData Read()
        {
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (last != null && now - lastAt < 3f)
            {
                return last;
            }

            var data = new MoonData();
            Try(data.ReadLevelLoader, "LethalLevelLoader");
            Try(data.ReadDawnLib, "DawnLib");
            Try(data.ReadRotation, "TooManyMoons");
            last = data;
            lastAt = now;
            return data;
        }

        private void ReadLevelLoader()
        {
            var content = Reflect.Find("LethalLevelLoader.PatchedContent");
            if (content == null || !(AccessTools.Property(content, "ExtendedLevels")?.GetValue(null) is IEnumerable levels))
            {
                return;
            }

            LevelLoader = true;
            var teams = Origins.ModTeams();

            foreach (var extended in levels)
            {
                if (extended == null || !(Traverse.Create(extended).Property("SelectableLevel").GetValue() is SelectableLevel level) || level == null)
                {
                    continue;
                }

                if (Traverse.Create(extended).Property("IsRouteHidden").GetValue() is bool hidden && hidden)
                {
                    Hidden.Add(level);
                }

                if (Traverse.Create(extended).Property("IsRouteLocked").GetValue() is bool locked && locked)
                {
                    Locked.Add(level);
                }

                var custom = Traverse.Create(extended).Property("ContentType").GetValue()?.ToString() != "Vanilla";
                Authors[level] = custom ? Origins.OfExtended(extended, teams) ?? Vanilla : Vanilla;

                if (Traverse.Create(extended).Property("ContentTags").GetValue() is IEnumerable tags)
                {
                    foreach (var tag in tags)
                    {
                        if (tag != null)
                        {
                            Add(level, Traverse.Create(tag).Field("contentTagName").GetValue() as string);
                        }
                    }
                }
            }
        }

        private void ReadDawnLib()
        {
            var content = Reflect.Find("Dawn.LethalContent");
            var registry = content != null ? AccessTools.Field(content, "Moons")?.GetValue(null) : null;
            if (registry == null || !(Traverse.Create(registry).Property("Values").GetValue() is IEnumerable moons))
            {
                return;
            }

            foreach (var moon in moons)
            {
                if (moon == null || !(Traverse.Create(moon).Property("Level").GetValue() is SelectableLevel level) || level == null)
                {
                    continue;
                }

                // Without LethalLevelLoader the namespace of the key is the closest thing to an author.
                if (!Authors.ContainsKey(level))
                {
                    var space = Traverse.Create(moon).Property("Key").Property("Namespace").GetValue() as string;
                    Authors[level] = space == "lethal_company" ? Vanilla : Origins.Clean(space) ?? Vanilla;
                }

                if (Traverse.Create(moon).Method("AllTags").GetValue() is IEnumerable tags)
                {
                    foreach (var tag in tags)
                    {
                        if (tag != null)
                        {
                            Add(level, Traverse.Create(tag).Property("Key").GetValue() as string);
                        }
                    }
                }
            }
        }

        private void ReadRotation()
        {
            var api = Reflect.Find("TooManyMoons.RotationApi");
            var levels = StartOfRound.Instance != null ? StartOfRound.Instance.levels : null;
            if (api == null || levels == null || !(AccessTools.Property(api, "Active")?.GetValue(null) is bool active) || !active)
            {
                return;
            }

            var isDrawn = AccessTools.Method(api, "IsDrawn");
            if (isDrawn == null)
            {
                return;
            }

            Rotating = true;
            foreach (var level in levels)
            {
                if (level != null && isDrawn.Invoke(null, new object[] { level }) is bool drawn && drawn)
                {
                    Drawn.Add(level);
                }
            }
        }

        // The two mods spell the same tag differently ("Free" and "free").
        private void Add(SelectableLevel level, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            if (!Tags.TryGetValue(level, out var tags))
            {
                Tags[level] = tags = new HashSet<string>();
            }

            tags.Add(tag.Trim().ToLowerInvariant().Replace('_', ' '));
        }

        private static void Try(Action read, string mod)
        {
            try
            {
                read();
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"Could not read the moon data of {mod}: {e.Message}");
            }
        }
    }
}
