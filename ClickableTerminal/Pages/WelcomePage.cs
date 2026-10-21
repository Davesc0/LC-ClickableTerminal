using System;

namespace ClickableTerminal
{
    // Written out here, so whatever other mods append to the game's page does not come along.
    internal static class WelcomePage
    {
        internal static MenuPage Build()
        {
            var page = new MenuPage { Title = "FORTUNE-9 OS" };

            Line(page, "Welcome to the FORTUNE-9 OS");
            Line(page, "        Courtesy of the Company");
            Space(page);
            Line(page, $"Happy {DateTime.Now.DayOfWeek}.");
            Space(page);
            Line(page, "NOTICE: this terminal has been fitted");
            Line(page, "with a pointing device.");
            Space(page);
            Line(page, "Point at anything on screen and click it,");
            Line(page, "or type your commands as you always have.");
            Space(page);
            Line(page, "Type \"Help\" or press CONTINUE to begin.");
            return page;
        }

        private static void Line(MenuPage page, string text)
        {
            page.Entries.Add(new MenuEntry { Label = text });
        }

        private static void Space(MenuPage page)
        {
            page.Entries.Add(new MenuEntry { Label = "", IsHeader = true });
        }
    }
}
