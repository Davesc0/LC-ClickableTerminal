using System;
using System.Linq;
using UnityEngine;

namespace ClickableTerminal
{
    // Things on the ship that have a button of their own somewhere, pressed from the terminal instead.
    internal static class ExtrasPage
    {
        internal static MenuPage Build(Terminal terminal)
        {
            var round = StartOfRound.Instance;
            if (round == null)
            {
                return null;
            }

            var page = new MenuPage { Title = "EXTRAS" };

            page.Entries.Add(new MenuEntry
            {
                Label = "SHIP DOORS",
                LiveLabel = () => "SHIP DOORS  " + (round.hangarDoorsClosed ? "(closed)" : "(open)"),
                Description = "Open or close the ship's doors. Only while landed.",
                Click = ToggleDoors
            });

            foreach (var teleporter in UnityEngine.Object.FindObjectsOfType<ShipTeleporter>().OrderBy(found => found.isInverseTeleporter))
            {
                var device = teleporter;
                var name = device.isInverseTeleporter ? "INVERSE TELEPORTER" : "TELEPORTER";

                page.Entries.Add(new MenuEntry
                {
                    Label = name,
                    LiveLabel = () => device == null || device.buttonTrigger == null || device.buttonTrigger.interactable ? name : name + "  (charging)",
                    Description = device.isInverseTeleporter
                        ? "Send whoever stands on it into the building."
                        : "Bring back the crew member shown on the monitor.",
                    Click = () =>
                    {
                        if (device != null && device.buttonTrigger != null && device.buttonTrigger.interactable)
                        {
                            device.PressTeleportButtonOnLocalClient();
                        }
                    }
                });
            }

            if (round.shipRoomLights != null)
            {
                page.Entries.Add(new MenuEntry
                {
                    Label = "LIGHTS",
                    LiveLabel = () => "LIGHTS  " + (round.shipRoomLights.areLightsOn ? "(on)" : "(off)"),
                    Description = "Switch the lights in the ship.",
                    Click = () => round.shipRoomLights.ToggleShipLights()
                });
            }

            page.Entries.Add(new MenuEntry
            {
                Label = "LEVER",
                Description = "Pull the ship's lever: land, or take off.",
                Open = () => LeverPage(terminal)
            });

            page.Entries.Add(new MenuEntry
            {
                Label = "RANDOM MOON",
                Description = "Pick one of the moons you can fly to. You still get to confirm.",
                Click = () =>
                {
                    var route = MoonsPage.RandomRoute(terminal);
                    if (route != null)
                    {
                        Commands.Submit(terminal, route);
                    }
                }
            });
            return page;
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

        // Pulling the lever cannot be taken back, so it asks first.
        private static MenuPage LeverPage(Terminal terminal)
        {
            var round = StartOfRound.Instance;
            var lever = UnityEngine.Object.FindObjectOfType<StartMatchLever>();
            var page = new MenuPage { Title = "LEVER", Plain = true };
            var ready = lever != null && lever.triggerScript != null && lever.triggerScript.interactable;

            string question;
            if (!ready || round.travellingToNewLevel || round.shipIsLeaving)
            {
                question = null;
            }
            else if (round.shipHasLanded)
            {
                question = "Take off and leave this moon?";
            }
            else if (round.inShipPhase && round.currentLevel != null)
            {
                question = "Land on " + MoonsPage.NameOf(round.currentLevel) + "?";
            }
            else
            {
                question = null;
            }

            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });

            if (question == null)
            {
                page.Entries.Add(new MenuEntry { Label = "The lever cannot be pulled right now.", Big = true });
                page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
                page.Entries.Add(new MenuEntry { Label = "[ BACK ]", Click = () => Navigator.Back(terminal) });
                return page;
            }

            page.Entries.Add(new MenuEntry { Label = question, Big = true });
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            page.Entries.Add(new MenuEntry
            {
                Label = "[ CONFIRM ]",
                Click = () =>
                {
                    Navigator.Back(terminal);
                    lever.LeverAnimation();
                    lever.PullLever();
                }
            });
            page.Entries.Add(new MenuEntry { Label = "[ DENY ]", Click = () => Navigator.Back(terminal) });
            return page;
        }
    }
}
