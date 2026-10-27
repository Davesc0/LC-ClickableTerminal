using HarmonyLib;

namespace ClickableTerminal.Patches
{
    [HarmonyPatch(typeof(Terminal))]
    internal static class TerminalPatches
    {
        // Buying is checked against the same list the game draws for the quota, so the list itself is resized. It is drawn from the day's seed, the same for every player.
        [HarmonyPatch(nameof(Terminal.RotateShipDecorSelection))]
        [HarmonyPostfix]
        private static void StockDecorations(Terminal __instance)
        {
            var round = StartOfRound.Instance;
            var unlockables = round != null && round.unlockablesList != null ? round.unlockablesList.unlockables : null;
            var selection = __instance.ShipDecorSelection;
            var wanted = Settings.DecorationsOnOffer.Value;

            if (!Settings.ManageDecorations.Value || StoreRotationCompat.Installed || wanted == 0 || unlockables == null || selection == null)
            {
                return;
            }

            // What is always in stock can be bought without being on the list.
            var rest = new System.Collections.Generic.List<TerminalNode>();
            foreach (var unlockable in unlockables)
            {
                var node = unlockable != null ? unlockable.shopSelectionNode : null;
                if (node != null && !unlockable.alwaysInStock && !selection.Contains(node) && !rest.Contains(node))
                {
                    rest.Add(node);
                }
            }

            if (wanted < 0)
            {
                selection.AddRange(rest);
                return;
            }

            while (selection.Count > wanted)
            {
                selection.RemoveAt(selection.Count - 1);
            }

            var random = new System.Random(round.randomMapSeed + 66);
            while (selection.Count < wanted && rest.Count > 0)
            {
                var pick = rest[random.Next(0, rest.Count)];
                selection.Add(pick);
                rest.Remove(pick);
            }
        }

        // The game waits a whole second before the input field takes keys, so the first letters typed are lost.
        [HarmonyPatch("selectTextFieldDelayed")]
        [HarmonyPrefix]
        private static bool QuickerTyping(Terminal __instance, ref System.Collections.IEnumerator __result)
        {
            __result = SelectSoon(__instance);
            return false;
        }

        private static System.Collections.IEnumerator SelectSoon(Terminal terminal)
        {
            terminal.screenText.ActivateInputField();
            yield return new UnityEngine.WaitForSeconds(0.2f);
            terminal.screenText.Select();
        }

        [HarmonyPatch(nameof(Terminal.QuitTerminal))]
        [HarmonyPrefix]
        private static void RememberPage()
        {
            Navigator.Remember();
        }

        [HarmonyPatch(nameof(Terminal.BeginUsingTerminal))]
        [HarmonyPostfix]
        private static void ReopenPage(Terminal __instance)
        {
            Navigator.Reopen(__instance);
        }

        [HarmonyPatch(nameof(Terminal.OnSubmit))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool TypedIntoWindow(Terminal __instance)
        {
            if (Commands.Submitting || !__instance.terminalInUse)
            {
                return true;
            }

            var send = Window.Open != null ? Window.Open.Send : TerminalMouse.PagesOn ? Navigator.Current?.Page?.Send : null;
            if (send == null)
            {
                return true;
            }

            var typed = Commands.Typed(__instance);
            Commands.Type(__instance, "");
            send(typed);
            return false;
        }

        [HarmonyPatch(nameof(Terminal.OnSubmit))]
        [HarmonyPrefix]
        private static void NoteSubmitted(Terminal __instance)
        {
            Navigator.Submitted = __instance.terminalInUse ? Commands.Typed(__instance).Trim() : null;
        }

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void AttachMouse(Terminal __instance)
        {
            ScrollPatchFix.Apply(Plugin._harmony, __instance);

            if (__instance.GetComponent<TerminalMouse>() == null)
            {
                __instance.gameObject.AddComponent<TerminalMouse>();
            }
        }

        // The scan record is saved with the game, so it is left alone and the refusal is skipped instead.
        [HarmonyPatch("AttemptLoadCreatureFileNode")]
        [HarmonyPrefix]
        private static bool OpenAnyCreatureFile(Terminal __instance, TerminalNode node)
        {
            if (!Settings.ShowAllCreatures.Value || node == null)
            {
                return true;
            }

            __instance.LoadNewNode(node);
            return false;
        }

        [HarmonyPatch(nameof(Terminal.LoadNewNode))]
        [HarmonyPostfix]
        private static void PageLoaded(Terminal __instance, TerminalNode node)
        {
            Navigator.OnNodeLoaded(__instance, node);
        }
    }
}
