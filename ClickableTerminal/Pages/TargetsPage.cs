using GameNetcodeStuff;

namespace ClickableTerminal
{
    // The names SWITCH, PING and FLASH can be followed by: the players and radar boosters on the ship's radar.
    internal static class TargetsPage
    {
        internal static bool Handles(string verb)
        {
            return verb == "switch" || verb == "ping" || verb == "flash";
        }

        internal static MenuPage Build(string verb)
        {
            var radar = StartOfRound.Instance != null ? StartOfRound.Instance.mapScreen : null;
            if (radar == null || radar.radarTargets == null)
            {
                return null;
            }

            var page = new MenuPage { Title = verb.ToUpperInvariant() };

            // SWITCH on its own moves to the next one in line.
            if (verb == "switch")
            {
                page.Entries.Add(new MenuEntry { Label = "Next in line", Command = verb });
                page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
            }

            var listed = 0;

            for (var i = 0; i < radar.radarTargets.Count; i++)
            {
                var target = radar.radarTargets[i];
                if (target == null || target.transform == null || string.IsNullOrWhiteSpace(target.name))
                {
                    continue;
                }

                // Only SWITCH works on players, and only on the ones who are really in the game.
                if (!target.isNonPlayer)
                {
                    var player = target.transform.GetComponent<PlayerControllerB>();
                    if (verb != "switch" || player == null || (!player.isPlayerControlled && !player.isPlayerDead))
                    {
                        continue;
                    }
                }

                // The game reads one word after the command and matches the start of the name with it.
                var word = target.name.Trim().Split(' ')[0];
                var label = target.name + (target.isNonPlayer ? "  (radar booster)" : "")
                                        + (i == radar.targetTransformIndex ? "  (on the monitor)" : "");

                page.Entries.Add(new MenuEntry
                {
                    Label = label,
                    Command = word.Length > 2 ? verb + " " + word : null,
                    Dim = word.Length <= 2
                });
                listed++;
            }

            if (listed == 0)
            {
                page.Entries.Add(new MenuEntry { Label = verb == "switch" ? "Nobody to switch to." : "No radar boosters are active.", IsHeader = true });
            }

            return page;
        }
    }
}
