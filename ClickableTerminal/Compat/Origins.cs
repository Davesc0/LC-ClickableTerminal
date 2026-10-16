using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;

namespace ClickableTerminal
{
    // Works out who made a piece of modded content, to sort lists into sections by creator.
    internal static class Origins
    {
        internal const string Vanilla = "VANILLA";
        internal const string Unknown = "OTHER";

        // Mod managers install every package into a folder called "Team-Package". The team in front of the dash
        // is the creator, and it is the same for all of their packages.
        internal static string TeamOfPath(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return null;
            }

            try
            {
                var plugins = Path.GetFullPath(Paths.PluginPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(file);
                if (!full.StartsWith(plugins, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var folder = full.Substring(plugins.Length).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                var dash = folder.IndexOf('-');
                return dash > 0 ? Spelled(folder.Substring(0, dash)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // One spelling per creator. A mod may call itself "MagicWesley" inside while its package says "Magic_Wesley":
        // the package folders are read first, and anything that matches one of them letter for letter is written its way.
        private static Dictionary<string, string> spellings;

        internal static string Spelled(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (spellings == null)
            {
                spellings = new Dictionary<string, string>();
                try
                {
                    foreach (var folder in Directory.GetDirectories(Paths.PluginPath))
                    {
                        var package = Path.GetFileName(folder);
                        var dash = package.IndexOf('-');
                        if (dash > 0)
                        {
                            var team = package.Substring(0, dash).Replace('_', ' ').Trim().ToUpperInvariant();
                            spellings[Squash(team)] = team;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            var tidy = name.Replace('_', ' ').Trim().ToUpperInvariant();
            var key = Squash(tidy);
            if (!spellings.TryGetValue(key, out var known))
            {
                spellings[key] = known = tidy;
            }

            return known;
        }

        private static string Squash(string name)
        {
            var letters = new System.Text.StringBuilder();
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    letters.Append(c);
                }
            }

            return letters.ToString();
        }

        // LethalLevelLoader's own record of which bundle files each mod was read from, turned into
        // "this mod belongs to that creator". A pack can carry content credited to other people,
        // the package it was installed with is what puts it all in one section.
        internal static Dictionary<object, string> ModTeams()
        {
            var teams = new Dictionary<object, string>();

            try
            {
                var manager = Reflect.Find("LethalLevelLoader.LethalBundleManager");
                if (manager == null || !(AccessTools.Field(manager, "obtainedExtendedModsDict")?.GetValue(null) is IDictionary groups))
                {
                    return teams;
                }

                foreach (DictionaryEntry entry in groups)
                {
                    if (!(Traverse.Create(entry.Key).Field("assetBundleInfos").GetValue() is IEnumerable infos) || !(entry.Value is IEnumerable mods))
                    {
                        continue;
                    }

                    string team = null;
                    foreach (var info in infos)
                    {
                        team = info != null ? TeamOfPath(Traverse.Create(info).Property("AssetBundleFilePath").GetValue() as string) : null;
                        if (team != null)
                        {
                            break;
                        }
                    }

                    if (team == null)
                    {
                        continue;
                    }

                    foreach (var mod in mods)
                    {
                        if (mod != null && !teams.ContainsKey(mod))
                        {
                            teams[mod] = team;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"Could not read which package each mod came from: {e.Message}");
            }

            return teams;
        }

        // The creator of something LethalLevelLoader has on record: the package first, the name the mod gives itself second.
        internal static string OfExtended(object extended, Dictionary<object, string> teams)
        {
            var mod = Traverse.Create(extended).Property("ExtendedMod").GetValue();
            if (mod != null && teams.TryGetValue(mod, out var team))
            {
                return team;
            }

            return Clean(Traverse.Create(extended).Property("AuthorName").GetValue() as string)
                   ?? Clean(Traverse.Create(extended).Property("ModName").GetValue() as string);
        }

        // What LethalLevelLoader and DawnLib say about each enemy, for the ones that bring no code of their own.
        internal static Dictionary<EnemyType, string> EnemyCredits()
        {
            var credits = new Dictionary<EnemyType, string>();

            try
            {
                var content = Reflect.Find("LethalLevelLoader.PatchedContent");
                if (content != null && AccessTools.Property(content, "ExtendedEnemyTypes")?.GetValue(null) is IEnumerable extended)
                {
                    var teams = ModTeams();

                    foreach (var entry in extended)
                    {
                        if (entry == null || !(Traverse.Create(entry).Property("EnemyType").GetValue() is EnemyType enemy) || enemy == null)
                        {
                            continue;
                        }

                        var custom = Traverse.Create(entry).Property("ContentType").GetValue()?.ToString() != "Vanilla";
                        credits[enemy] = custom ? OfExtended(entry, teams) : Vanilla;
                    }
                }

                var dawn = Reflect.Find("Dawn.LethalContent");
                var registry = dawn != null ? AccessTools.Field(dawn, "Enemies")?.GetValue(null) : null;
                if (registry != null && Traverse.Create(registry).Property("Values").GetValue() is IEnumerable infos)
                {
                    foreach (var info in infos)
                    {
                        if (info == null || !(Traverse.Create(info).Property("EnemyType").GetValue() is EnemyType enemy) || enemy == null
                            || (credits.TryGetValue(enemy, out var known) && known != null))
                        {
                            continue;
                        }

                        var space = Traverse.Create(info).Property("Key").Property("Namespace").GetValue() as string;
                        credits[enemy] = space == "lethal_company" ? Vanilla : Clean(space);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"Could not read who made the enemies: {e.Message}");
            }

            return credits;
        }

        internal static string OfEnemy(EnemyType enemy, Dictionary<EnemyType, string> credits)
        {
            if (enemy == null)
            {
                return Unknown;
            }

            // An enemy with its own script: the package that script was installed with.
            try
            {
                var script = enemy.enemyPrefab != null ? enemy.enemyPrefab.GetComponent<EnemyAI>() : null;
                var assembly = script != null ? script.GetType().Assembly : null;
                if (assembly != null && assembly != typeof(EnemyAI).Assembly)
                {
                    var team = TeamOfPath(assembly.Location);
                    if (team != null)
                    {
                        return team;
                    }
                }
            }
            catch (Exception)
            {
            }

            // A vanilla script on new assets (variants and the like): what the content loaders have on record.
            return credits.TryGetValue(enemy, out var credit) && credit != null ? credit : Vanilla;
        }

        // "Unknown" and "Unspecified" are what a loader fills in when the mod says nothing.
        internal static string Clean(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var tidy = name.Replace('_', ' ').Trim().ToUpperInvariant();
            return tidy.StartsWith("UNKNOWN") || tidy == "UNSPECIFIED" ? null : Spelled(tidy);
        }
    }
}
