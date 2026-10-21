using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class TargetsPage
    {
        internal static bool Handles(string verb)
        {
            return verb == "switch" || verb == "ping" || verb == "flash";
        }

        private class Seen
        {
            internal int Index;
            internal string Name;
            internal string Word;
            internal bool Dead;
            internal RadarBoosterItem Item;
        }

        internal static MenuPage MonitorPage(Terminal terminal)
        {
            var radar = StartOfRound.Instance != null ? StartOfRound.Instance.mapScreen : null;
            var camera = radar == null ? null : radar.mapCamera != null ? radar.mapCamera : radar.cam;
            if (camera == null || camera.targetTexture == null)
            {
                return null;
            }

            return new MenuPage
            {
                Title = "MONITOR",
                Subtitle = "Type a door's or trap's code from the map, then Enter",
                Picture = camera.targetTexture,
                Camera = camera,
                Extra = () => Controls(terminal, radar),
                LivePanels = true,
                Send = code =>
                {
                    code = (code ?? "").Trim();
                    if (code.Length > 0)
                    {
                        Commands.Ask(terminal, code);
                    }
                }
            };
        }

        private static Detail Controls(Terminal terminal, ManualCameraRenderer radar)
        {
            var everyone = Watchable(radar);
            var boosters = Boosters(radar);
            var watched = everyone.FirstOrDefault(each => each.Index == radar.targetTransformIndex);
            var panel = new Detail { Title = "RADAR" };

            void Run(string command)
            {
                Commands.Ask(terminal, command);
                Navigator.Rebuild(terminal);
            }

            var watching = panel.Group("WATCHING");
            if (everyone.Count > 0)
            {
                var at = Mathf.Max(0, everyone.IndexOf(watched));
                var before = everyone[(at - 1 + everyone.Count) % everyone.Count];
                watching.Add(new ToolButton { Label = "<", Narrow = true, Off = before.Word == null || everyone.Count < 2, Click = () => Run("switch " + before.Word) });
                watching.Add(new ToolButton { Label = watched != null ? watched.Name : "-", Active = true, Click = () => CrewWindow(terminal) });
                watching.Add(new ToolButton { Label = ">", Narrow = true, Off = everyone.Count < 2, Click = () => Run("switch") });
            }

            // The radar's own calls, the ones PING and FLASH make. They tell the other players themselves.
            var item = watched != null ? watched.Item : null;
            if (boosters.Count > 0)
            {
                var index = watched != null ? watched.Index : -1;
                var actions = panel.Group(null);
                actions.Add(new ToolButton
                {
                    Label = "PING",
                    Off = item == null,
                    Click = () =>
                    {
                        radar.PingRadarBooster(index);
                        Navigator.Rebuild(terminal);
                    }
                });
                actions.Add(new ToolButton
                {
                    Label = "FLASH",
                    Off = item == null || !item.radarEnabled || !FlashReady(item),
                    Click = () =>
                    {
                        radar.FlashRadarBooster(index);
                        Navigator.Rebuild(terminal);
                    }
                });
            }

            // Who can be beamed up (a player, or with some mods a radar booster) is the game's business.
            var teleporter = Object.FindObjectsOfType<ShipTeleporter>().FirstOrDefault(each => !each.isInverseTeleporter);
            var ready = teleporter != null && (teleporter.buttonTrigger == null || teleporter.buttonTrigger.interactable);
            panel.Group(null).Add(new ToolButton
            {
                Label = teleporter != null && !ready ? "CHARGING" : "TELEPORT",
                Off = teleporter != null && !ready,
                Click = () =>
                {
                    if (teleporter == null)
                    {
                        StorePage.OfferUpgrade(terminal, "Teleporter", "The ship has no teleporter. It is sold in the store, with the ship upgrades.");
                        return;
                    }

                    teleporter.PressTeleportButtonOnLocalClient();
                    Navigator.Rebuild(terminal);
                }
            });

            var shown = terminal.displayingPersistentImage != null;
            panel.Group("TERMINAL").Add(new ToolButton
            {
                Label = "MAP: " + (shown ? "ON" : "OFF"),
                Active = shown,
                Click = () => Run("view monitor")
            });

            return panel;
        }

        private static readonly System.Reflection.FieldInfo FlashCooldown = HarmonyLib.AccessTools.Field(typeof(RadarBoosterItem), "flashCooldown");

        private static bool FlashReady(RadarBoosterItem item)
        {
            return FlashCooldown == null || !((float)FlashCooldown.GetValue(item) >= 0f);
        }

        private static void CrewWindow(Terminal terminal)
        {
            var radar = StartOfRound.Instance != null ? StartOfRound.Instance.mapScreen : null;
            if (radar == null)
            {
                return;
            }

            var window = new Window { Title = "WATCH WHO?" };
            var crew = window.Group("CREW");
            var boosters = window.Group("RADAR BOOSTERS");
            foreach (var each in Watchable(radar))
            {
                var word = each.Word;
                (each.Item != null ? boosters : crew).Add(new ToolButton
                {
                    Label = each.Name + (each.Dead ? " (dead)" : ""),
                    Active = each.Index == radar.targetTransformIndex,
                    Off = word == null,
                    Click = () =>
                    {
                        Commands.Ask(terminal, "switch " + word);
                        Window.Close();
                        Navigator.Rebuild(terminal);
                    }
                });
            }

            window.Groups.RemoveAll(group => group.Value.Count == 0);
            window.Buttons.Add(new ToolButton { Label = "BACK", Click = Window.Close });
            Window.Show(window);
        }

        private static List<Seen> Watchable(ManualCameraRenderer radar)
        {
            var players = Players(radar);
            return players.Concat(Boosters(radar)).OrderBy(each => each.Index).ToList();
        }

        private static List<Seen> Players(ManualCameraRenderer radar)
        {
            var found = new List<Seen>();
            for (var i = 0; radar.radarTargets != null && i < radar.radarTargets.Count; i++)
            {
                var target = radar.radarTargets[i];
                if (target == null || target.transform == null || target.isNonPlayer || string.IsNullOrWhiteSpace(target.name))
                {
                    continue;
                }

                var player = target.transform.GetComponent<PlayerControllerB>();
                if (player != null && (player.isPlayerControlled || player.isPlayerDead))
                {
                    found.Add(new Seen { Index = i, Name = target.name, Word = Word(target.name), Dead = player.isPlayerDead });
                }
            }

            return found;
        }

        private static List<Seen> Boosters(ManualCameraRenderer radar)
        {
            var found = new List<Seen>();
            for (var i = 0; radar.radarTargets != null && i < radar.radarTargets.Count; i++)
            {
                var target = radar.radarTargets[i];
                if (target != null && target.transform != null && target.isNonPlayer && !string.IsNullOrWhiteSpace(target.name))
                {
                    found.Add(new Seen { Index = i, Name = target.name, Word = Word(target.name), Item = target.transform.GetComponent<RadarBoosterItem>() });
                }
            }

            return found;
        }

        private static string Word(string name)
        {
            var word = (name ?? "").Trim().Split(' ')[0];
            return word.Length > 2 ? word : null;
        }
    }
}
