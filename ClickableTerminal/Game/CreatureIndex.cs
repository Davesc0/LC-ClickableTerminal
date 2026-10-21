using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClickableTerminal
{
    // A bestiary file only has an id and a name. The scan node on the creature's prefab carries the same id.
    internal static class CreatureIndex
    {
        private static Dictionary<int, EnemyType> byScanId;

        private static Dictionary<string, EnemyType> byName;

        internal static EnemyType Find(int creatureFileId)
        {
            Build();
            return byScanId != null && byScanId.TryGetValue(creatureFileId, out var enemy) ? enemy : null;
        }

        // Some creatures are never scanned as themselves (the cadaver grows out of an infected player), so the name is
        // the fallback.
        internal static EnemyType Find(TerminalNode file)
        {
            var enemy = Find(file.creatureFileID);
            if (enemy != null || byName == null || string.IsNullOrWhiteSpace(file.creatureName))
            {
                return enemy;
            }

            var name = Commands.Normalize(file.creatureName);
            if (byName.TryGetValue(name, out enemy) || (name.EndsWith("s") && byName.TryGetValue(name.Substring(0, name.Length - 1), out enemy)))
            {
                return enemy;
            }

            // "Cadaver Blooms" in the file, "Cadaver Growths" or just "Cadaver" in the game.
            var first = name.Split(' ')[0];
            EnemyType only = null;
            foreach (var pair in byName)
            {
                if (first.Length > 4 && pair.Key.StartsWith(first))
                {
                    if (only != null && only != pair.Value)
                    {
                        return null;
                    }

                    only = pair.Value;
                }
            }

            return only;
        }

        internal static int? HealthOf(EnemyType enemy)
        {
            if (enemy == null || !enemy.canDie)
            {
                return null;
            }

            try
            {
                var ai = enemy.enemyPrefab != null ? enemy.enemyPrefab.GetComponent<EnemyAI>() : null;
                return ai != null ? ai.enemyHP : (int?)null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Build()
        {
            if (byScanId != null && byScanId.Count > 0)
            {
                return;
            }

            var levels = StartOfRound.Instance != null ? StartOfRound.Instance.levels : null;
            if (levels == null)
            {
                return;
            }

            byScanId = new Dictionary<int, EnemyType>();
            byName = new Dictionary<string, EnemyType>();

            // A modded creature may only be on its own moon.
            foreach (var level in levels)
            {
                if (level != null)
                {
                    Collect(level.Enemies);
                    Collect(level.OutsideEnemies);
                    Collect(level.DaytimeEnemies);
                }
            }

            // Creatures that spawn on no moon (the kidnapper fox, the lasso man) are still loaded.
            foreach (var enemy in Resources.FindObjectsOfTypeAll<EnemyType>())
            {
                Add(enemy);
            }
        }

        private static void Collect(List<SpawnableEnemyWithRarity> spawnable)
        {
            if (spawnable == null)
            {
                return;
            }

            foreach (var entry in spawnable)
            {
                Add(entry?.enemyType);
            }
        }

        private static void Add(EnemyType enemy)
        {
            if (enemy == null || enemy.enemyPrefab == null)
            {
                return;
            }

            var name = Commands.Normalize(enemy.enemyName ?? "");
            if (name.Length > 0 && !byName.ContainsKey(name))
            {
                byName[name] = enemy;
            }

            try
            {
                var scan = enemy.enemyPrefab.GetComponentInChildren<ScanNodeProperties>(true);
                if (scan != null && scan.creatureScanID >= 0 && !byScanId.ContainsKey(scan.creatureScanID))
                {
                    byScanId[scan.creatureScanID] = enemy;
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
