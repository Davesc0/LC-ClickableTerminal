using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClickableTerminal
{
    internal class View
    {
        internal TerminalNode Node;
        internal bool IsHome;

        // Null for a text view: the game's own page is shown as it is.
        internal MenuPage Page;
        internal Func<MenuPage> Builder;

        // A page that only exists on our side, opened from another page of ours.
        internal bool Virtual;

        // Set on pages that only wait for the player (the welcome text): where CONTINUE leads.
        internal TerminalNode ContinueTo;

        // Set on the page after a confirmation ("Ordered 4 flashlights..."): the button that leads back.
        internal string ReturnLabel;

        // A page that asks for an answer. Once answered it is not a place to come back to.
        internal bool Question;

        // What to answer when the player walks away from the question instead. A mod that reads the answer
        // itself keeps waiting for it otherwise, and takes the next command for one.
        internal string Leave;

        internal string ButtonLabel => ContinueTo != null ? "[ CONTINUE ]" : ReturnLabel;
    }

    // Keeps track of where the player has been, so there is a way back,
    // and decides which of the game's pages are replaced by one of ours.
    internal static class Navigator
    {
        private const int HelpNode = 13;
        private const int WelcomeNode = 1;
        private const int CannotAffordNode = 2;
        private const int MaxHistory = 30;

        private static readonly List<View> History = new List<View>();
        private static bool goingBack;
        private static bool inApplication;

        // What was last sent to the terminal, typed or clicked.
        internal static string Submitted;
        internal static bool LeaveToMenu;

        internal static View Current => History.Count > 0 ? History[History.Count - 1] : null;
        internal static bool CanGoBack => History.Count > 1;

        internal static void Reset()
        {
            History.Clear();
            left = null;
        }

        // The pages the player had open when they stepped away from the terminal.
        private static List<View> left;

        internal static void Remember()
        {
            left = new List<View>(History);

            // A question, or a page that only makes sense while a mod waits for an answer, is not one to come back to.
            // Nor is a page whose loading does something (a switch of the monitor, an eject).
            while (left.Count > 1)
            {
                var last = left[left.Count - 1];
                if (!last.Question && last.Leave == null && (last.Node == null || last.Virtual || string.IsNullOrEmpty(last.Node.terminalEvent)))
                {
                    break;
                }

                left.RemoveAt(left.Count - 1);
            }
        }

        // Right after the game has opened the terminal on its own first page.
        internal static void Reopen(Terminal terminal)
        {
            if (!Settings.RememberLastPage.Value || left == null || left.Count < 2)
            {
                return;
            }

            History.Clear();
            History.AddRange(left);
            Restore(terminal);
        }

        // Pages the game goes through while something is done for the player in one go (the cart's order)
        // are none of the player's steps: nothing of them is kept.
        internal static void Quietly(Action work)
        {
            quiet = true;
            try
            {
                work();
            }
            finally
            {
                quiet = false;
            }
        }

        private static bool quiet;

        internal static void OnNodeLoaded(Terminal terminal, TerminalNode node)
        {
            if (quiet || node == null || terminal.terminalNodes == null)
            {
                return;
            }

            var help = Special(terminal, HelpNode);

            // The Interactive Terminal API answers the command that starts an application with the help page,
            // and loads that page once more when the application closes. Neither is the player going to the
            // main menu: the page the application was started from stays where it is, and comes back after.
            if (!goingBack && node == help && History.Count > 0)
            {
                // While it starts, its object is in the scene but not yet announced. While it closes, it still is.
                var running = InteractiveTerminal.Running();
                var starting = !running && (InteractiveTerminal.Registered(Submitted ?? "") != null || GameObject.Find("InteractiveTerminal") != null);

                if (starting)
                {
                    Submitted = null;
                    inApplication = true;
                    return;
                }

                if (inApplication || running)
                {
                    inApplication = false;

                    // Closed with MENU, the main menu is where it should end up after all.
                    if (!LeaveToMenu)
                    {
                        Restore(terminal);
                        return;
                    }
                }
            }

            inApplication = false;
            LeaveToMenu = false;

            if (!goingBack && Settings.SkipWelcome.Value && help != null && node == Special(terminal, WelcomeNode))
            {
                terminal.LoadNewNode(help);
                return;
            }

            var asked = Current;
            var answered = !goingBack && History.Count > 1 && asked.Node != null
                           && asked.Question && asked.Node != node;

            // DENY on a confirmation page: nothing happened, so straight back to where the player came from.
            if (answered && asked.Node.overrideOptions && IsDenial(asked.Node, node))
            {
                Back(terminal);
                return;
            }

            // Some pages are printed under what was on the screen before. Coming from a page of ours
            // that would be text the player never saw, so only the new page is kept.
            if (!goingBack && !node.clearPreviousText && (answered || (asked != null && asked.Page != null)))
            {
                Commands.ShowOnly(terminal, node);
            }

            var view = ViewFor(terminal, node, help, answered);

            if (goingBack)
            {
                // Coming back to a page the game knows nothing about: it is still ours to draw.
                if (History.Count > 0 && Current.Virtual)
                {
                    Rebuild(terminal);
                    return;
                }

                if (History.Count > 0)
                {
                    LabelReturn(view, History.Count > 1 ? History[History.Count - 2] : null, false);
                    History[History.Count - 1] = view;
                }
                else
                {
                    History.Add(view);
                }

                return;
            }

            // The main menu and the welcome text are where everything starts from.
            if (node == help || node == Special(terminal, WelcomeNode))
            {
                History.Clear();
                History.Add(view);
                return;
            }

            if (History.Count == 0)
            {
                History.Add(new View { IsHome = true, Node = help });
            }

            // A confirmation page is not a place to come back to.
            if (answered)
            {
                History.RemoveAt(History.Count - 1);
            }

            LabelReturn(view, Current, answered);

            if (Current.Node == node && History.Count > 1)
            {
                History[History.Count - 1] = view;
                return;
            }

            History.Add(view);
            if (History.Count > MaxHistory)
            {
                History.RemoveAt(1);
            }
        }

        internal static void Back(Terminal terminal)
        {
            if (!CanGoBack)
            {
                return;
            }

            Dismiss(terminal);
            History.RemoveAt(History.Count - 1);

            // Loading a page again also runs its event again (eject, the monitor switch...), so those are skipped.
            while (History.Count > 1 && Current.Node != null && !string.IsNullOrEmpty(Current.Node.terminalEvent))
            {
                History.RemoveAt(History.Count - 1);
            }

            var target = Current.IsHome ? Special(terminal, HelpNode) : Current.Node;
            if (target == null)
            {
                return;
            }

            goingBack = true;
            try
            {
                Load(terminal, target);
            }
            finally
            {
                goingBack = false;
            }
        }

        // Puts the page on top of the pile back on screen, the way BACK would land on it.
        private static void Restore(Terminal terminal)
        {
            if (Current == null)
            {
                return;
            }

            if (Current.Virtual)
            {
                Rebuild(terminal);
                return;
            }

            var target = Current.IsHome ? Special(terminal, HelpNode) : Current.Node;
            if (target == null)
            {
                return;
            }

            goingBack = true;
            try
            {
                Load(terminal, target);
            }
            finally
            {
                goingBack = false;
            }
        }

        // The answer goes in as if typed. What the mod says to it takes the question's place on the pile.
        private static void Dismiss(Terminal terminal)
        {
            var view = Current;
            if (view == null || view.Leave == null)
            {
                return;
            }

            var leave = view.Leave;
            view.Leave = null;
            Commands.Submit(terminal, leave);
        }

        internal static void Home(Terminal terminal)
        {
            var help = Special(terminal, HelpNode);
            if (help != null)
            {
                Dismiss(terminal);
                Load(terminal, help);
            }
        }

        // A page that exists only on our side, like the list of names under SWITCH. The game stays on the page it was on.
        internal static void Open(Func<MenuPage> builder)
        {
            var page = Try(builder);
            if (page != null && Current != null)
            {
                History.Add(new View { Node = Current.Node, Page = page, Builder = builder, Virtual = true });
            }
        }

        // Takes a page of ours off the pile without loading anything, for a form that hands over to the game.
        internal static void DropCurrent()
        {
            if (History.Count > 1 && Current.Virtual)
            {
                History.RemoveAt(History.Count - 1);
            }
        }

        internal static void Continue(Terminal terminal)
        {
            if (Current != null && Current.ContinueTo != null)
            {
                Load(terminal, Current.ContinueTo);
            }
            else if (Current != null && Current.ReturnLabel != null)
            {
                Back(terminal);
            }
        }

        // A page of the game's text opened from one of our lists (an info page, the answer to a purchase)
        // gets a button that says where it leads back to.
        private static void LabelReturn(View view, View before, bool answered)
        {
            if (view.Page != null || view.ContinueTo != null || view.Question)
            {
                return;
            }

            if (before != null && before.Page != null && !before.IsHome)
            {
                view.ReturnLabel = $"[ BACK TO {before.Page.Title} ]";
            }
            else if (answered)
            {
                view.ReturnLabel = "[ CONTINUE ]";
            }
        }

        // The title of the page a new page was opened from, for its "back to" button.
        private static string ReturnTitle()
        {
            for (var i = History.Count - (goingBack ? 2 : 1); i >= 0; i--)
            {
                var view = History[i];
                if (view.Question)
                {
                    continue;
                }

                return view.IsHome ? "MENU" : view.Page != null ? view.Page.Title : null;
            }

            return null;
        }

        private static bool IsDenial(TerminalNode question, TerminalNode answer)
        {
            if (question.terminalOptions == null)
            {
                return false;
            }

            foreach (var option in question.terminalOptions)
            {
                if (option?.noun != null && option.noun.word == "deny" && option.result == answer)
                {
                    return true;
                }
            }

            return false;
        }

        // After a sort or a filter click: the same page, drawn again from the game's data.
        internal static void Rebuild(Terminal terminal)
        {
            var view = Current;
            if (view == null || view.Builder == null)
            {
                return;
            }

            var page = Try(view.Builder);
            if (page != null)
            {
                view.Page = page;
            }
        }

        // Straight to the page, without the parser. That is the only way out of a confirmation page.
        private static void Load(Terminal terminal, TerminalNode node)
        {
            terminal.LoadNewNode(node);

            if (terminal.forceScrollbarCoroutine != null)
            {
                terminal.StopCoroutine(terminal.forceScrollbarCoroutine);
            }

            terminal.forceScrollbarCoroutine = terminal.StartCoroutine(terminal.forceScrollbarUp());
        }

        private static View ViewFor(Terminal terminal, TerminalNode node, TerminalNode help, bool answered)
        {
            var view = new View { Node = node };

            if (node == help)
            {
                // Read now, while the page is still what the game has just printed.
                var parsed = HelpParser.Parse(terminal.screenText.text);
                var entries = parsed.FindAll(KnownCommands.IsVanillaMain);

                // Whatever a mod has added goes on one page, last on the menu. What it wrote onto the menu itself comes along.
                var advertised = parsed.FindAll(entry => !KnownCommands.IsVanillaMain(entry) && !KnownCommands.IsHidden(entry.Command ?? entry.Prefill));
                Func<MenuPage> extras = () => ExtrasPage.Build(terminal);
                if (entries.Count > 0 && Settings.ShowExtras.Value)
                {
                    entries.Add(new MenuEntry { Label = "EXTRAS", Description = "To see some extra commands.", Open = extras });
                }

                Func<MenuPage> mods = () => ModsPage(terminal, advertised);
                if (entries.Count > 0 && Try(mods) != null)
                {
                    entries.Add(new MenuEntry { Label = "MODS", Description = "What the installed mods add to this terminal.", Open = mods });
                }

                if (entries.Count > 0)
                {
                    view.IsHome = true;
                    view.Page = new MenuPage { Title = "MAIN MENU", Entries = entries };
                }

                return view;
            }

            if (node == Special(terminal, WelcomeNode))
            {
                view.ContinueTo = help;
                view.Page = WelcomePage.Build();
                return view;
            }

            if (node.acceptAnything && node.terminalOptions != null && node.terminalOptions.Length > 0)
            {
                view.ContinueTo = node.terminalOptions[0].result;
                return view;
            }

            var answers = new List<string>();
            if (node.overrideOptions && node.terminalOptions != null)
            {
                foreach (var option in node.terminalOptions)
                {
                    if (option?.noun != null && !string.IsNullOrWhiteSpace(option.noun.word))
                    {
                        answers.Add(option.noun.word);
                    }
                }
            }

            // A page that takes no answer is not a question, whatever it is marked as (a mod's own receipt).
            // Nor is one that does not ask: a mod can put its refusal on the page of the question it replaced.
            var asked = terminal.screenText.text;
            if (answers.Count > 0 && answers.Exists(answer => asked.IndexOf(answer, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                // The orders and routes we know get their own form. Any other question keeps its wording and gets the buttons.
                var question = terminal.screenText.text;
                view.Question = true;
                view.Builder = () => ConfirmPage.Build(terminal, node) ?? ResultPages.Question(question, answers);
            }
            else
            {
                view.Builder = BuilderFor(terminal, node);
            }

            if (view.Builder == null && (SellMyScrapPages.Claim(terminal, view, terminal.screenText.text, ReturnTitle())
                                         || ScrapBankPages.Claim(terminal, view, terminal.screenText.text, Submitted, ReturnTitle())
                                         || WeatherForecast.Claim(terminal, view, terminal.screenText.text)))
            {
                view.Page = Try(view.Builder);
                return view;
            }

            // A mod asking the same way without the game's own mechanism behind it: it reads the answer itself.
            // Marked as a question is not enough: a refusal shown in a question's place carries the mark too.
            if (view.Builder == null && (ResultPages.AsksToConfirm(asked)
                                         || (node.isConfirmationNode && asked.IndexOf("confirm", StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                var question = terminal.screenText.text;
                view.Question = true;
                view.Leave = "deny";
                view.Builder = () => ResultPages.Question(question, new[] { "confirm", "deny" });
            }

            // Pages the game prints as sentences that read better as figures.
            if (view.Builder == null)
            {
                var back = ReturnTitle();
                var printed = terminal.screenText.text;

                if (node == Special(terminal, CannotAffordNode))
                {
                    view.Builder = () => ResultPages.CannotAfford(terminal, back);
                }
                else if (answered && ResultPages.IsReceipt(terminal, node))
                {
                    view.Builder = () => ResultPages.Receipt(terminal, node, back);
                }
                else if (TooManyEmotesStore.IsStore(printed))
                {
                    view.Builder = () => TooManyEmotesStore.Build(terminal, printed, back);
                }
                else if (ResultPages.ListsChances(printed))
                {
                    view.Builder = () => ResultPages.Chances(terminal, printed, back);
                }
                else if (node.displayTexture == null && terminal.displayingPersistentImage == null && ResultPages.IsProse(printed))
                {
                    // Messages and descriptions: plain sentences, drawn in the same style as the rest.
                    // Pages with a picture or a list of their own stay as the game prints them.
                    if (node.creatureFileID >= 0)
                    {
                        view.Builder = () => ResultPages.Creature(terminal, node, printed, back);
                    }
                    else if (node.storyLogFileID >= 0)
                    {
                        view.Builder = () => ResultPages.Log(terminal, node, printed, back);
                    }
                    else
                    {
                        view.Builder = () => ResultPages.Document(terminal, printed, back);
                    }
                }
            }

            if (view.Builder != null)
            {
                view.Page = Try(view.Builder);
            }

            return view;
        }

        private static Func<MenuPage> BuilderFor(Terminal terminal, TerminalNode node)
        {
            switch (KeywordOf(terminal, node))
            {
                case "moons":
                    return () => MoonsPage.Build(terminal);
                // UPGRADES and DECOR are parts of the store in the game, and sections of the same page here.
                case "store":
                case "upgrades":
                case "decor":
                    return () => StorePage.Build(terminal);
                case "bestiary":
                    return () => BestiaryPage.Build(terminal);
                case "storage":
                    return () => StoragePage.Build(terminal);
                case "scan":
                    return () => ScanPage.Build(terminal);
                case "sigurd":
                    return () => LogsPage.Build(terminal);
                case SelectableInteriorsPage.ListCommand:
                    return () => SelectableInteriorsPage.Build(terminal);
                case "other":
                    var printed = terminal.screenText.text;
                    return () => CommandsPage(terminal, printed);
                default:
                    return null;
            }
        }

        // A page of ours that fails must never take the terminal down with it: the game's text is still there.
        private static MenuPage Try(Func<MenuPage> builder)
        {
            try
            {
                return builder();
            }
            catch (Exception e)
            {
                Plugin.mlg.LogError($"Could not build a terminal page, showing the game's own instead: {e}");
                return null;
            }
        }

        private static MenuPage CommandsPage(Terminal terminal, string printed)
        {
            var page = new MenuPage { Title = "OTHER COMMANDS", Entries = HelpParser.Parse(printed).FindAll(KnownCommands.IsVanillaOther) };
            page.Entries.AddRange(KnownCommands.UnexplainedFor(terminal));

            foreach (var entry in page.Entries)
            {
                // SWITCH, PING and FLASH want a name. Instead of typing it, the names are listed on a page of their own.
                var verb = entry.Prefill != null ? entry.Prefill.Trim() : null;
                if (TargetsPage.Handles(verb))
                {
                    entry.Open = () => TargetsPage.Build(verb);
                }
                else if (TransmitPage.Handles(verb))
                {
                    entry.Open = () => TransmitPage.Build(terminal);
                }
                else if (entry.Command == "view monitor")
                {
                    // A switch, not a page: it turns the ship's map on or off on this screen and the list stays.
                    var command = entry.Command;
                    entry.Label = "VIEW MONITOR  " + (terminal.displayingPersistentImage != null ? "(on)" : "(off)");
                    entry.Click = () =>
                    {
                        var from = Current;
                        Commands.Submit(terminal, command);
                        if (Current != from)
                        {
                            Back(terminal);
                        }
                    };
                }
            }

            return page.Entries.Count > 0 ? page : null;
        }

        // Everything that is not the game's own: the mods we know as a page each, what mods wrote onto the
        // game's two menus, and under that the commands that work but that nothing on the terminal mentions.
        // Commands of mods that have a page of ours, though they are written onto the game's menus.
        private static readonly HashSet<string> Handled = new HashSet<string> { "emotes" };

        private static MenuPage ModsPage(Terminal terminal, List<MenuEntry> advertised)
        {
            // On top the mods this terminal knows and has pages for. Every other command of a mod goes under
            // UNLISTED, with its name only: what it does is the mod's own business.
            var page = new MenuPage { Title = "MODS" };
            page.Entries.AddRange(KnownCommands.GroupEntries());
            page.Entries.AddRange(KnownCommands.SinglesAmong(InteractiveTerminal.Commands()));

            foreach (var added in Api.TerminalMenu.Added)
            {
                page.Entries.Add(new MenuEntry { Label = added.Label.ToUpperInvariant(), Command = added.Command, Description = added.Description });
            }

            // Every Interactive Terminal API application works with our cursor, so none of them is unknown here.
            // The API has no line about what an application does: the ones we know have theirs above.
            var shown = new HashSet<string>(page.Entries.Select(entry => Commands.Normalize(entry.Command ?? entry.Prefill ?? "")));
            foreach (var command in InteractiveTerminal.Commands())
            {
                if (!KnownCommands.IsHidden(command) && shown.Add(Commands.Normalize(command)))
                {
                    page.Entries.Add(new MenuEntry { Label = command.ToUpperInvariant(), Command = command });
                }
            }

            var rest = new List<MenuEntry>(advertised);
            var other = NodeOf(terminal, "other");
            if (other != null && other.displayText != null)
            {
                rest.AddRange(HelpParser.Parse(other.displayText).FindAll(entry =>
                    !KnownCommands.IsVanillaOther(entry) && !KnownCommands.IsHidden(entry.Command ?? entry.Prefill)));
            }

            var known = new HashSet<string>(KnownCommands.VanillaCommands());
            var unlisted = new List<MenuEntry>();

            foreach (var entry in page.Entries)
            {
                known.Add(Commands.Normalize(entry.Command ?? entry.Prefill ?? ""));
            }

            foreach (var entry in rest)
            {
                var key = Commands.Normalize(entry.Command ?? entry.Prefill ?? "");
                if (!known.Add(key))
                {
                    continue;
                }

                if (Handled.Contains(key))
                {
                    page.Entries.Add(entry);
                }
                else
                {
                    unlisted.Add(new MenuEntry { Label = entry.Label, Command = entry.Command, Prefill = entry.Prefill, Open = entry.Open });
                }
            }

            foreach (var entry in Discovery.Unlisted(terminal, known))
            {
                unlisted.Add(new MenuEntry { Label = entry.Label, Command = entry.Command, Prefill = entry.Prefill });
            }

            if (unlisted.Count > 0)
            {
                // A folder of its own, last on the page.
                var sorted = unlisted.OrderBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase).ToList();
                page.Entries.Add(new MenuEntry
                {
                    Label = "UNLISTED",
                    Description = "Commands of mods this terminal has no page for.",
                    Open = () => new MenuPage { Title = "UNLISTED", Entries = sorted }
                });
            }

            return page.Entries.Count > 0 ? page : null;
        }

        private static TerminalNode NodeOf(Terminal terminal, string word)
        {
            var keywords = terminal.terminalNodes.allKeywords;
            if (keywords == null)
            {
                return null;
            }

            foreach (var keyword in keywords)
            {
                if (keyword != null && keyword.word == word)
                {
                    return keyword.specialKeywordResult;
                }
            }

            return null;
        }

        private static string KeywordOf(Terminal terminal, TerminalNode node)
        {
            var keywords = terminal.terminalNodes.allKeywords;
            if (keywords == null)
            {
                return null;
            }

            foreach (var keyword in keywords)
            {
                if (keyword != null && keyword.specialKeywordResult == node)
                {
                    return keyword.word;
                }
            }

            return null;
        }

        private static TerminalNode Special(Terminal terminal, int index)
        {
            var special = terminal.terminalNodes.specialNodes;
            return special != null && index < special.Count ? special[index] : null;
        }
    }
}
