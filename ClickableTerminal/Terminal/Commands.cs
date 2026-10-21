using System.Text;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class Commands
    {
        internal static string Normalize(string text)
        {
            var result = new StringBuilder(text.Length);
            var gap = false;

            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (gap && result.Length > 0)
                    {
                        result.Append(' ');
                    }

                    gap = false;
                    result.Append(char.ToLowerInvariant(c));
                }
                else if (char.IsWhiteSpace(c))
                {
                    gap = true;
                }
            }

            return result.ToString();
        }

        // The same as typing it and pressing Enter, so every mod that reads the input still sees it.
        internal static void Submit(Terminal terminal, string command)
        {
            Type(terminal, command);
            Submitting = true;
            try
            {
                terminal.OnSubmit();
            }
            finally
            {
                Submitting = false;
            }
        }

        internal static bool Submitting { get; private set; }

        internal static void Type(Terminal terminal, string typed)
        {
            var field = terminal.screenText;
            var page = field.text.Substring(0, Mathf.Max(0, field.text.Length - terminal.textAdded));

            terminal.modifyingText = true;
            field.text = page + typed;
            terminal.modifyingText = false;
            terminal.currentText = field.text;
            terminal.textAdded = typed.Length;
        }

        internal static void ShowOnly(Terminal terminal, TerminalNode node)
        {
            var text = "\n\n\n" + node.displayText;
            try
            {
                text = terminal.TextPostProcess(text, node);
            }
            catch (System.Exception)
            {
            }

            terminal.modifyingText = true;
            terminal.screenText.text = text;
            terminal.modifyingText = false;
            terminal.currentText = terminal.screenText.text;
            terminal.textAdded = 0;
        }

        internal static string PageText(Terminal terminal, TerminalNode node)
        {
            var text = node.displayText ?? "";
            try
            {
                text = terminal.TextPostProcess(text, node);
            }
            catch (System.Exception)
            {
            }

            return text.Trim('\n', '\r', ' ');
        }

        internal static string Ask(Terminal terminal, params string[] commands)
        {
            Plugin.Debug("Runs in the background: " + string.Join(", ", commands));
            var before = terminal.currentNode;
            var ran = Run(terminal, () =>
            {
                foreach (var command in commands)
                {
                    Submit(terminal, command);
                }
            });

            // Another mod failed before anything was printed: the screen only holds what was there before.
            var answer = TakeScreen(terminal);
            return ran || terminal.currentNode != before
                ? answer
                : "[Another mod failed while this command ran, so it did nothing. The BepInEx log has its error.]";
        }

        // The game's own checks (credits, the dropship, one vehicle at a time) still run.
        internal static string Act(Terminal terminal, TerminalNode confirmed, int amount)
        {
            terminal.playerDefinedAmount = amount;
            Run(terminal, () => terminal.LoadNewNodeIfAffordable(confirmed));
            return TakeScreen(terminal);
        }

        // Another mod throwing in its own patch must not keep the answer from showing. Its error is only logged.
        private static bool Run(Terminal terminal, System.Action work)
        {
            var image = terminal.terminalImage;
            var hadImage = image != null && image.enabled;
            try
            {
                Navigator.Quietly(work);
                return true;
            }
            catch (System.Exception e)
            {
                Plugin.WarnOnce($"Another mod failed in its own patch while the terminal ran a command (not ours to fix): {e}");
                return false;
            }
            finally
            {
                // A page with a picture (Code Rebirth's upgrades) leaves it on the monitor under our page. The map stays.
                if (!hadImage && image != null && image.enabled && terminal.displayingPersistentImage == null)
                {
                    image.enabled = false;
                    if (terminal.terminalImageMask != null)
                    {
                        terminal.terminalImageMask.enabled = false;
                    }
                }
            }
        }

        private static string TakeScreen(Terminal terminal)
        {
            var answer = terminal.screenText.text.Trim('\n', '\r', ' ');
            terminal.modifyingText = true;
            terminal.screenText.text = "";
            terminal.modifyingText = false;
            terminal.currentText = "";
            terminal.textAdded = 0;
            return answer;
        }

        internal static string Typed(Terminal terminal)
        {
            var text = terminal.screenText.text;
            return text.Substring(Mathf.Clamp(text.Length - terminal.textAdded, 0, text.Length));
        }
    }
}
