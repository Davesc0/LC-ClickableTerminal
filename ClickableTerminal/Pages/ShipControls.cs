using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClickableTerminal
{
    // The ship's own buttons, pressed from the STATUS panel: the teleporters side by side, the doors and the lights side by side, and the lever as the big button at the bottom. A lit button is on (doors open, lights on).
    internal static class ShipControls
    {
        internal static void AddTo(Detail panel)
        {
            var round = StartOfRound.Instance;
            if (round == null)
            {
                return;
            }

            foreach (var teleporter in Teleporters())
            {
                var device = teleporter;
                var ready = device.buttonTrigger == null || device.buttonTrigger.interactable;
                panel.Buttons.Add(new ToolButton
                {
                    Label = ready ? (device.isInverseTeleporter ? "INVERSE" : "TELEPORT") : "CHARGING",
                    Off = !ready,
                    Row = 1,
                    Click = () =>
                    {
                        if (device != null && device.buttonTrigger != null && device.buttonTrigger.interactable)
                        {
                            device.PressTeleportButtonOnLocalClient();
                        }
                    }
                });
            }

            panel.Buttons.Add(new ToolButton
            {
                Label = "DOORS",
                Active = round.shipHasLanded && !round.hangarDoorsClosed,
                Off = !round.shipHasLanded,
                Row = 2,
                Click = ToggleDoors
            });

            if (round.shipRoomLights != null)
            {
                panel.Buttons.Add(new ToolButton
                {
                    Label = "LIGHTS",
                    Active = round.shipRoomLights.areLightsOn,
                    Row = 2,
                    Click = () => round.shipRoomLights.ToggleShipLights()
                });
            }

            panel.Buttons.Add(new ToolButton
            {
                Label = round.shipHasLanded ? "TAKE OFF" : round.inShipPhase ? "LAND" : "LEVER",
                Off = !round.shipHasLanded && !round.inShipPhase,
                Primary = true,
                Click = LeverWindow
            });
        }

        // The game tells when a teleporter comes into the world (Patches.ShipWatch); the ones gone with a scene are let go.
        private static IEnumerable<ShipTeleporter> Teleporters()
        {
            var known = Patches.ShipWatch.Teleporters;
            known.RemoveAll(found => found == null);
            return known.OrderBy(found => found.isInverseTeleporter);
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

        // Pulling the lever cannot be taken back, so a window asks first.
        private static void LeverWindow()
        {
            var round = StartOfRound.Instance;
            var lever = Object.FindObjectOfType<StartMatchLever>();
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
