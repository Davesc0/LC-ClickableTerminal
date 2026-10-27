using System.Linq;
using System.Text.RegularExpressions;

namespace ClickableTerminal
{
    // What the moons page reads out of text: risk grades to sort by, and LethalLevelLoader's simulation.
    internal static class MoonText
    {
        private static readonly Regex Grade = new Regex(@"^(F|D|C|B|A|S+)([+\-]*)$", RegexOptions.Compiled);

        private static readonly Regex Chance = new Regex(@"^\*?\s*(.+?)\s*//\s*Chance:\s*(\S+)", RegexOptions.Compiled);

        // "* Name // Chance: 12.3%"
        internal static string Simulation(string printed, string name)
        {
            var lines = Window.Tidy(printed, name).Split('\n').Select(line =>
            {
                var match = Chance.Match(Regex.Replace(line, "<[^>]*>", "").Trim());
                return match.Success ? "  " + match.Groups[1].Value + "<pos=72%>" + match.Groups[2].Value : line;
            });
            return string.Join("\n", lines);
        }

        // "Safe" first, then F up to S. Every + or - is a step, so S < S+ < S++, and modded grades like SS come after. Anything unreadable sorts last.
        internal static int RiskRank(string risk)
        {
            var grade = (risk ?? "").Trim().ToUpperInvariant();
            if (grade == "SAFE")
            {
                return -1;
            }

            var match = Grade.Match(grade);
            if (!match.Success)
            {
                return int.MaxValue;
            }

            var letters = match.Groups[1].Value;
            var signs = match.Groups[2].Value;
            var steps = 0;

            foreach (var sign in signs)
            {
                steps += sign == '+' ? 1 : -1;
            }

            return "FDCBAS".IndexOf(letters[0]) * 10000 + (letters.Length - 1) * 1000 + 500 + steps;
        }
    }
}
