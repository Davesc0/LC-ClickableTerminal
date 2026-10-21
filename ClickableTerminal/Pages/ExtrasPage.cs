using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class ExtrasPage
    {
        internal static MenuPage Build(Terminal terminal)
        {
            var entries = Entries(terminal);
            if (entries.Count == 0)
            {
                return null;
            }

            var page = Navigator.MenuLook(terminal, new MenuPage { Title = "EXTRAS", Subtitle = "Ship controls" });
            page.Entries.AddRange(entries);
            return page;
        }

        internal static List<MenuEntry> Entries(Terminal terminal)
        {
            var round = StartOfRound.Instance;
            var entries = new List<MenuEntry>();
            if (round == null)
            {
                return entries;
            }

            void Add(MenuEntry entry, string text)
            {
                var name = entry.Label;
                var state = entry.LiveCells;
                entry.LiveCells = null;
                if (state != null)
                {
                    entry.LiveLabel = () => name + ": " + state()[0];
                    entry.Label = entry.LiveLabel();
                }

                entry.Description = text;
                entries.Add(entry);
            }

            Add(new MenuEntry
            {
                Label = "SHIP DOORS",
                LiveCells = () => new[] { !round.shipHasLanded ? "LOCKED" : round.hangarDoorsClosed ? "CLOSED" : "OPEN" },
                Click = ToggleDoors
            }, "Open or close the ship's doors. Only while landed.");

            foreach (var teleporter in UnityEngine.Object.FindObjectsOfType<ShipTeleporter>().OrderBy(found => found.isInverseTeleporter))
            {
                var device = teleporter;
                Add(new MenuEntry
                {
                    Label = device.isInverseTeleporter ? "INVERSE TELEPORTER" : "TELEPORTER",
                    LiveCells = () => new[] { device == null || device.buttonTrigger == null || device.buttonTrigger.interactable ? "READY" : "CHARGING" },
                    Click = () =>
                    {
                        if (device != null && device.buttonTrigger != null && device.buttonTrigger.interactable)
                        {
                            device.PressTeleportButtonOnLocalClient();
                        }
                    }
                }, device.isInverseTeleporter
                    ? "Send whoever stands on it into the building, somewhere random."
                    : "Bring back the crew member shown on the monitor, without what they hold.");
            }

            if (round.shipRoomLights != null)
            {
                Add(new MenuEntry
                {
                    Label = "LIGHTS",
                    LiveCells = () => new[] { round.shipRoomLights.areLightsOn ? "ON" : "OFF" },
                    Click = () => round.shipRoomLights.ToggleShipLights()
                }, "Switch the lights in the ship.");
            }

            Add(new MenuEntry
            {
                Label = "LEVER",
                LiveCells = () => new[] { round.shipHasLanded ? "TAKE OFF" : round.inShipPhase ? "LAND" : "-" },
                Click = LeverWindow
            }, "Pull the ship's lever: land, or take off.");

            Add(new MenuEntry
            {
                Label = "RANDOM MOON",
                Click = () => MoonsPage.RandomWindow(terminal)
            }, "Pick one of the moons you can fly to and afford.");

            if (Settings.Monsters.Value != MonsterDetail.Off)
            {
                Add(new MenuEntry { Label = "MONSTERS", Open = MonstersPage.Build }, "What is alive on this moon right now.");
            }

            return entries;
        }

        // The buttons next to the door do more than move it (sound, the others' game), so they are pressed as they are.
        private static void ToggleDoors()
        {
            var round = StartOfRound.Instance;
            if (round == null || !round.shipHasLanded)
            {
                return;
            }

            var button = GameObject.Find(round.hangarDoorsClosed ? "StartButton" : "StopButton");
            var trigger = button != null ? button.GetComponentInChildren<InteractTrigger>() : null;
            if (trigger != null && trigger.interactable)
            {
                trigger.onInteract.Invoke(GameNetworkManager.Instance.localPlayerController);
            }
        }

        private static void LeverWindow()
        {
            var round = StartOfRound.Instance;
            var lever = UnityEngine.Object.FindObjectOfType<StartMatchLever>();
            var ready = round != null && lever != null && lever.triggerScript != null && lever.triggerScript.interactable
                        && !round.travellingToNewLevel && !round.shipIsLeaving;

            string question = null;
            if (ready && round.shipHasLanded)
            {
                question = "Take off and leave this moon?";
            }
            else if (ready && round.inShipPhase && round.currentLevel != null)
            {
                question = "Land on " + MoonsPage.NameOf(round.currentLevel) + "?";
            }

            var window = new Window { Title = "LEVER", Text = question ?? "The lever cannot be pulled right now." };
            if (question != null)
            {
                window.Buttons.Add(new ToolButton
                {
                    Label = round.shipHasLanded ? "TAKE OFF" : "LAND",
                    Click = () =>
                    {
                        Window.Close();
                        lever.LeverAnimation();
                        lever.PullLever();
                    }
                });
            }

            window.Buttons.Add(new ToolButton { Label = question != null ? "CANCEL" : "OK", Click = Window.Close });
            Window.Show(window);
        }
    }
}
