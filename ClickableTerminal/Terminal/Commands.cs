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

        // Same as typing the command and pressing Enter, so every mod that reads the input still sees it.
        internal static void Submit(Terminal terminal, string command)
        {
            Type(terminal, command);
            terminal.OnSubmit();
        }

        // Replaces whatever is typed so far, without sending it.
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

        // Prints the page on its own, as if it cleared the screen first.
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

        internal static string Typed(Terminal terminal)
        {
            var text = terminal.screenText.text;
            return text.Substring(Mathf.Clamp(text.Length - terminal.textAdded, 0, text.Length));
        }
    }
}
