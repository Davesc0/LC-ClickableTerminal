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
        internal MenuPage Page;
        internal Func<MenuPage> Builder;
        internal bool Virtual;
        internal bool Generic;
        internal TerminalNode ContinueTo;
        internal string ReturnLabel;
        internal bool Question;

        // A mod that reads the answer itself keeps waiting otherwise, and takes the next command as the answer.
        internal string Leave;
        internal string ButtonLabel => ContinueTo != null ? "[ CONTINUE ]" : ReturnLabel;
    }

    internal static class Navigator
    {
        private const int HelpNode = 13;
        private const int WelcomeNode = 1;
        private const int CannotAffordNode = 2;
        private const int MaxHistory = 30;

        private static readonly List<View> History = new List<View>();
        private static bool goingBack;
        private static bool inApplication;

        internal static string Submitted;

        internal static string LastApplication;
        internal static bool LeaveToMenu;

        internal static View Current => History.Count > 0 ? History[History.Count - 1] : null;
        internal static bool CanGoBack => History.Count > 1;

        internal static void Reset()
        {
            History.Clear();
            left = null;
        }

        private static List<View> left;

        internal static void Remember()
        {
            left = new List<View>(History);

            // Questions, and pages whose loading does something (a monitor switch, an eject), are not places to come back to.
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

        // The pages the game goes through while we order the cart are not the player's steps.
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

            var fromOurs = !goingBack && Current != null && Current.Page != null && !Current.Question;

            var help = Special(terminal, HelpNode);

            // The Interactive Terminal API answers an app's start command with the help page and loads it again when the app closes. Neither is the player going to the main menu.
            if (!goingBack && node == help && History.Count > 0)
            {
                // While it starts its object is in the scene but not announced yet. While it closes, it still is.
                var running = InteractiveTerminal.Running();
                var starting = !running && (InteractiveTerminal.Registered(Submitted ?? "") != null || GameObject.Find("InteractiveTerminal") != null);

                if (starting)
                {
                    LastApplication = Submitted;
                    Submitted = null;
                    inApplication = true;
                    return;
                }

                if (inApplication || running)
                {
                    inApplication = false;

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

            if (answered && asked.Node.overrideOptions && IsDenial(asked.Node, node))
            {
                Back(terminal);
                return;
            }

            // Some pages print under what was on screen before. Coming from our page, that is text the player never saw.
            if (!goingBack && !node.clearPreviousText && (answered || (asked != null && asked.Page != null)))
            {
                Commands.ShowOnly(terminal, node);
            }

            var view = ViewFor(terminal, node, help, answered);
            Plugin.Debug($"Page loaded: {node.name} (keyword {KeywordOf(terminal, node) ?? "-"}, typed \"{Submitted}\"), {(view.Page != null || view.Builder != null ? "our page" : "game text")}");

            if (fromOurs && view.Question)
            {
                var question = Window.Tidy(terminal.screenText.text, "");
                // Closing without an answer denies, or the game would take the next command as the answer.
                var window = new Window { Title = "CONFIRM", Text = question, Closing = () => Commands.Ask(terminal, "deny") };
                window.Buttons.Add(new ToolButton
                {
                    Label = "CONFIRM",
                    Click = () =>
                    {
                        Window.Drop();
                        var answer = Commands.Ask(terminal, "confirm");
                        Window.Show(new Window { Title = "CONFIRM", Text = Window.Tidy(answer, "") });
                        Rebuild(terminal);
                    }
                });
                window.Buttons.Add(new ToolButton
                {
                    Label = "DENY",
                    Click = Window.Close
                });
                Window.Show(window);
                return;
            }

            if (fromOurs && view.Generic && !view.Question && LguContracts.Owns(terminal.screenText.text))
            {
                LguContracts.Show(terminal, terminal.screenText.text);
                Rebuild(terminal);
                return;
            }

            if (fromOurs && view.Generic && !view.Question && IsShortAnswer(terminal, node))
            {
                Window.Show(new Window { Title = "TERMINAL", Text = Window.Tidy(Commands.PageText(terminal, node), "") });
                Rebuild(terminal);
                return;
            }

            if (goingBack)
            {
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

            // Loading a page again runs its event again (eject, the monitor switch), so those are skipped.
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

        internal static void Open(Func<MenuPage> builder)
        {
            var page = Try(builder);
            if (page != null && Current != null)
            {
                History.Add(new View { Node = Current.Node, Page = page, Builder = builder, Virtual = true });
            }
        }

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

        // Skips the parser. That is the only way out of a confirmation page.
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
                // Read now, while the screen still holds what the game has just printed.
                var parsed = HelpParser.Parse(terminal.screenText.text);
                var entries = parsed.FindAll(KnownCommands.IsVanillaMain);

                var advertised = parsed.FindAll(entry => !KnownCommands.IsVanillaMain(entry) && !KnownCommands.IsHidden(entry.Command ?? entry.Prefill));
                Func<MenuPage> mods = () => ModsPage(terminal, advertised);
                if (entries.Count > 0 && Try(mods) != null)
                {
                    entries.Add(new MenuEntry { Label = "MODS", Description = "What the installed mods add to this terminal.", Open = mods });
                }

                if (entries.Count > 0)
                {
                    view.IsHome = true;
                    view.Page = MenuLook(terminal, new MenuPage { Title = "MAIN MENU", Entries = entries });
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

            // A page that takes no answer is not a question, whatever it is marked as. A mod can also put its refusal on the question's page.
            var asked = terminal.screenText.text;
            if (answers.Count > 0 && answers.Exists(answer => asked.IndexOf(answer, StringComparison.OrdinalIgnoreCase) >= 0))
            {
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
                                         || WeatherForecast.Claim(terminal, view, terminal.screenText.text)
                                         || Contracts.Claim(terminal, view, terminal.screenText.text)))
            {
                view.Page = Try(view.Builder);
                return view;
            }

            // A mod asking without the game's own mechanism reads the answer itself. The mark alone is not enough: a refusal in a question's place carries it too.
            if (view.Builder == null && (ResultPages.AsksToConfirm(asked)
                                         || (node.isConfirmationNode && asked.IndexOf("confirm", StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                var question = terminal.screenText.text;
                view.Question = true;
                view.Leave = "deny";
                view.Builder = () => ResultPages.Question(question, new[] { "confirm", "deny" });
            }

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
                        view.Generic = true;
                        view.Builder = () => ResultPages.Document(terminal, printed, back);
                    }
                }
            }

            if (view.Builder != null)
            {
                view.Page = Try(view.Builder);
            }
            else
            {
                view.Generic = true;
            }

            return view;
        }

        private static Func<MenuPage> BuilderFor(Terminal terminal, TerminalNode node)
        {
            switch (KeywordOf(terminal, node))
            {
                case "moons":
                    return () => MoonsPage.Build(terminal);
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

        // A page of ours that fails must never take the terminal down. The game's text is still there.
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

            var radar = page.Entries.FindIndex(entry => entry.Command == "view monitor" || TargetsPage.Handles(entry.Prefill?.Trim()));
            if (radar >= 0)
            {
                page.Entries.RemoveAll(entry => entry.Command == "view monitor" || TargetsPage.Handles(entry.Prefill?.Trim()));
                page.Entries.Insert(Mathf.Min(radar, page.Entries.Count), new MenuEntry
                {
                    Label = "MONITOR",
                    Description = "The ship's radar: watch the crew, ping boosters, open doors by code.",
                    Open = () => TargetsPage.MonitorPage(terminal)
                });
            }

            foreach (var entry in page.Entries)
            {
                var verb = entry.Prefill != null ? entry.Prefill.Trim() : null;
                if (TargetsPage.Handles(verb))
                {
                    entry.Open = () => TargetsPage.MonitorPage(terminal);
                }
                else if (TransmitPage.Handles(verb))
                {
                    entry.Click = () => TransmitPage.ShowWindow(terminal);
                }
                else if (entry.Command == "eject")
                {
                    entry.Click = () =>
                    {
                        var window = new Window
                        {
                            Title = "EJECT THE CREW?",
                            Text = "Everyone on board is fired out of the airlock and the run starts over. This cannot be undone."
                        };
                        window.Buttons.Add(new ToolButton
                        {
                            Label = "EJECT",
                            Click = () => Window.Show(new Window { Title = "EJECT", Text = Window.Tidy(Commands.Ask(terminal, "eject", "confirm"), "") })
                        });
                        window.Buttons.Add(new ToolButton { Label = "CANCEL", Click = Window.Close });
                        Window.Show(window);
                    };
                }
                else if (entry.Command == "view monitor")
                {
                    var command = entry.Command;
                    entry.Label = "VIEW MONITOR";
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

            if (page.Entries.Count == 0)
            {
                return null;
            }

            foreach (var entry in page.Entries)
            {
                entry.Label = System.Text.RegularExpressions.Regex.Replace(entry.Label, @"\s*\[[^\]]*\]", "").Trim();
            }

            if (Settings.Monsters.Value != MonsterDetail.Off)
            {
                page.Entries.Add(new MenuEntry { Label = "MONSTER SCAN", Description = "What is alive on this moon right now.", Open = MonstersPage.Build });
            }

            page.Subtitle = "Ship and crew commands";
            MenuLook(terminal, page);

            var monitor = page.Entries.FirstOrDefault(entry => entry.Label == "VIEW MONITOR");
            if (monitor != null)
            {
                monitor.LiveCells = () => new[] { terminal.displayingPersistentImage != null ? "ON" : "OFF" };
                monitor.Cells = monitor.LiveCells();
            }

            return page;
        }

        private static readonly HashSet<string> Handled = new HashSet<string> { "emotes" };

        private static MenuPage ModsPage(Terminal terminal, List<MenuEntry> advertised)
        {
            var page = new MenuPage { Title = "MODS" };
            var apps = new HashSet<string>(InteractiveTerminal.Commands().Select(Commands.Normalize));
            page.Entries.AddRange(KnownCommands.GroupEntries());
            page.Entries.AddRange(KnownCommands.SinglesAmong(InteractiveTerminal.Commands()));

            foreach (var added in Api.TerminalMenu.Added)
            {
                page.Entries.Add(new MenuEntry { Label = added.Label.ToUpperInvariant(), Command = added.Command, Description = added.Description });
            }

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
                var sorted = unlisted.OrderBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase).ToList();
                page.Entries.Add(new MenuEntry
                {
                    Label = "UNLISTED",
                    Description = "Commands of mods this terminal has no page for. Clicking one runs it the way typing it would.",
                    Open = () => Answers.InWindows(terminal, MenuLook(terminal, new MenuPage { Title = "UNLISTED", Subtitle = $"{sorted.Count} commands", Entries = sorted }))
                });
            }

            if (page.Entries.Count == 0)
            {
                return null;
            }

            page.Subtitle = "What the installed mods add to this terminal";
            return Answers.InWindows(terminal, MenuLook(terminal, page));
        }

        internal static MenuPage MenuLook(Terminal terminal, MenuPage page)
        {
            page.Extra = () => StatusPanel.Build(terminal);

            page.LivePanels = Settings.ShipControls.Value;
            return page.Sized(0.8f, 0.7f, 0.9f);
        }

        private static bool IsShortAnswer(Terminal terminal, TerminalNode node)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(node.displayText ?? "", "<[^>]*>", "").Trim();
            var lines = text.Split('\n').Count(line => line.Trim().Length > 0);
            return text.Length > 2 && text.Length < 200 && lines <= 3
                   && !node.acceptAnything && (node.terminalOptions == null || node.terminalOptions.Length == 0)
                   && node.buyItemIndex < 0 && node.buyVehicleIndex < 0 && node.shipUnlockableID < 0 && node.buyRerouteToMoon < 0
                   && node.creatureFileID < 0 && node.storyLogFileID < 0 && node.displayPlanetInfo < 0
                   && node != Special(terminal, HelpNode) && node != Special(terminal, WelcomeNode);
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
