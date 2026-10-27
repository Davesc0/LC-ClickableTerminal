using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // Sits on the terminal while it is in use: decides what the screen shows (one of our pages, the game's text, or an application), moves the cursor with the mouse or the controller, and turns clicks into what the spot under the cursor does.
    internal class TerminalMouse : MonoBehaviour
    {
        private const float HideAfter = 5f;

        internal static bool InUse;

        // The wheel already does something of ours this frame: our page is on screen, or an application's counter or page.
        internal static bool WheelTaken;

        private static TerminalMouse active;

        // The switch key turns the menus off (or on, when the config has them off) until the game is closed.
        private static bool swapped;
        private bool switchHeld;
        private bool switchSpoiled;

        internal static bool PagesOn => Settings.OwnPages.Value != swapped;

        // How far the scrollbar moves for one line of the text on screen, 0 when it all fits.
        internal static float LineStep()
        {
            var self = active;
            var text = self != null && self.terminal != null && self.terminal.screenText != null ? self.terminal.screenText.textComponent : null;
            var scroll = text != null ? text.GetComponentInParent<ScrollRect>() : null;
            if (scroll == null || scroll.content == null || scroll.viewport == null)
            {
                return 0f;
            }

            var hidden = scroll.content.rect.height - scroll.viewport.rect.height;
            var info = text.textInfo;
            var line = info != null && info.lineCount > 0 ? info.lineInfo[0].lineHeight : text.fontSize;
            return hidden > 1f ? line / hidden : 0f;
        }

        private Terminal terminal;
        private RectTransform frame;
        private RectTransform cursor;
        private RawImage cursorImage;
        private RectTransform hover;
        private Image hoverImage;
        private RectTransform hoverFrame;
        private CanvasGroup gameText;
        private TopButtons buttons;
        private ScreenTweaks tweaks;
        private MenuView menu;
        private readonly Dictionary<float, MenuView> views = new Dictionary<float, MenuView>();
        private readonly InteractiveMouse apps = new InteractiveMouse();
        private readonly Steering steering = new Steering();

        private HotSpot lastHit;
        private HotSpot dragging;
        private MenuPage failedPage;
        private string lastShown;
        private int refocusIn;
        private float lastMoved = -100f;
        private bool pageShown;
        private bool appWindow;
        private float lastScrolled;
        private float heldScroll;
        private bool wheelWasOurs;
        private Vector2 heldStep;
        private float stepAt;
        private float textTop = float.NaN;

        private void Awake()
        {
            terminal = GetComponent<Terminal>();
            active = this;
            Navigator.Reset();
        }

        private void OnDisable()
        {
            InUse = false;
        }

        private void OnDestroy()
        {
            tweaks?.Release();
        }

        // A page that cannot be drawn falls back to the game's own text instead of breaking the terminal every frame.
        private void Update()
        {
            try
            {
                Frame();
            }
            catch (System.Exception e)
            {
                failedPage = Navigator.Current != null ? Navigator.Current.Page : null;
                Plugin.WarnOnce($"The terminal page could not be drawn, the game's own text is shown instead: {e}");
            }
        }

        private void LateUpdate()
        {
            if (InUse && tweaks != null)
            {
                tweaks.LateUpdate(pageShown);
            }
        }

        private void Frame()
        {
            var player = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
            var mouse = Mouse.current;

            if (terminal == null || !terminal.terminalInUse || player == null || !player.inTerminalMenu
                || mouse == null || terminal.screenText == null)
            {
                if (InUse)
                {
                    InUse = false;
                    pageShown = false;
                    Ui.SetActive(cursor, false);
                    Ui.SetActive(hover, false);
                }

                tweaks?.KeepScreenOn();
                return;
            }

            InteractiveTerminal.Tick();

            var field = terminal.screenText;
            var text = field.textComponent;
            Build(field, text);

            if (!InUse)
            {
                InUse = true;
                cursor.localPosition = frame.rect.center;
                tweaks.Opened();
            }

            Switch();

            // Any other mod that has taken the input away is left alone.
            var app = InteractiveTerminal.Running();
            var foreign = !app && !field.interactable;
            var view = Navigator.Current;
            var page = ShownPage(view, app, foreign);

            FollowApplication(app, field, text);
            ShowPage(page, text, foreign);

            if (pageShown)
            {
                menu.SetTyped(Commands.Typed(terminal));
                menu.Tick();

                // A free spinning wheel sends a step every frame, so the list moves at most twenty times a second.
                var wheel = mouse.scroll.ReadValue().y;
                if (!app && Mathf.Abs(wheel) > 0.01f && Time.unscaledTime - lastScrolled > 0.05f)
                {
                    lastScrolled = Time.unscaledTime;
                    if (!menu.ScrollTabs(cursor.position, wheel > 0f ? -1 : 1))
                    {
                        menu.Scroll(wheel > 0f ? -2 : 2);
                    }
                }
            }

            MoveCursor(mouse);

            var keys = Keyboard.current;
            var steered = Steer(keys, app);

            // Enter with nothing typed clicks what the cursor is on. With something typed it sends that, as always.
            var enter = !app && keys != null && terminal.textAdded == 0
                        && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame);

            if (keys != null && keys.anyKey.wasPressedThisFrame && !steered && !enter && !InteractiveTerminal.PressingKey)
            {
                lastMoved = -100f;
            }

            // On a controller there is no cursor, only the frame around what is chosen, and that does not fade.
            var pad = Pad.InUse;
            var visible = !foreign && (pad || Time.unscaledTime - lastMoved < HideAfter);
            ShowButtons(view, app, foreign, text);

            // A scrollbar taken hold of stays held until the button is let go, wherever the cursor wanders.
            if (dragging != null && !mouse.leftButton.isPressed)
            {
                dragging.Holding?.Invoke(false);
                dragging = null;
            }

            var hit = dragging != null ? dragging
                : !visible ? null
                : !app ? SpotUnderCursor()
                : TopButtons.Under(buttons.Back, cursor.position) ? buttons.Back
                : TopButtons.Under(buttons.Menu, cursor.position) ? buttons.Menu
                : AppSpot(cursor.position);

            // A new page on a controller: the choice goes on the nearest thing that can be clicked.
            if (pad && hit == null && !foreign && SnapToNearest(app))
            {
                hit = !app ? SpotUnderCursor() : AppSpot(cursor.position);
            }

            if (hit != lastHit)
            {
                lastHit = hit;
                if (pageShown)
                {
                    menu.Focus(hit);
                }
            }

            // The app highlights the entry its cursor is on, so moving its cursor there is the highlight.
            if (app && hit != null && hit.Hover != null)
            {
                hit.Hover();
            }

            ShowHover(app && !appWindow && hit != buttons.Back && hit != buttons.Menu ? null : hit, text.color);
            dragging?.Drag(cursor.position);
            ApplicationWheel(app, hit, mouse);

            cursorImage.color = text.color;
            Ui.SetActive(cursor, visible && !pad);
            Clicks(mouse, hit, enter, foreign, field);
        }

        // Alt counts when it is let go without another key in between, so Alt+Tab and the like do not switch.
        private void Switch()
        {
            var keys = Keyboard.current;
            var key = Settings.SwitchKey.Value;
            var pressed = false;

            if (keys != null && key != Key.None)
            {
                var alt = key == Key.LeftAlt || key == Key.RightAlt;
                var down = alt ? keys.leftAltKey.isPressed || keys.rightAltKey.isPressed : keys[key].isPressed;
                if (down && !switchHeld)
                {
                    switchHeld = true;
                    switchSpoiled = false;
                }
                else if (down && (keys.anyKey.wasPressedThisFrame && !(alt ? keys.leftAltKey.wasPressedThisFrame || keys.rightAltKey.wasPressedThisFrame : keys[key].wasPressedThisFrame)
                                  || !Application.isFocused))
                {
                    switchSpoiled = true;
                }
                else if (!down && switchHeld)
                {
                    switchHeld = false;
                    pressed = !switchSpoiled && Application.isFocused;
                }
            }

            if (!pressed && !Pad.Press(Pad.RightStick, pad => pad.rightStickButton))
            {
                return;
            }

            swapped = !swapped;
            if (Window.Open != null && !Window.Open.App)
            {
                Window.Drop();
            }

            // The game's screen gets the page we are on, printed again without running its event. Pages that only exist on our side (MODS, UNLISTED, MONITOR) show the game page they were opened from.
            var node = Navigator.Current?.Node;
            if (!PagesOn && node != null && !InteractiveTerminal.Running())
            {
                Commands.ShowOnly(terminal, node);
                if (terminal.scrollBarVertical != null)
                {
                    terminal.scrollBarVertical.value = 1f;
                }
            }

            PlayClick();
            Plugin.Debug("Switched to " + (PagesOn ? "the menus" : "the game's screen"));
        }

        private MenuPage ShownPage(View view, bool app, bool foreign)
        {
            appWindow = app && Settings.AppsInWindow.Value && PagesOn && view != null && view.Page != null;
            var page = !foreign && (!app || appWindow) && view != null && PagesOn ? view.Page : null;
            if (page != null && page == failedPage)
            {
                page = null;
            }

            var node = terminal.currentNode;
            var shownNow = $"{(node != null ? node.name : "-")} -> {(page != null ? page.Title : "game text")}{(foreign ? " (another mod has the input)" : "")}{(app ? " (application)" : "")}";
            if (shownNow != lastShown)
            {
                lastShown = shownNow;
                Plugin.Debug("Screen: " + shownNow);
            }

            return page;
        }

        // An application opened from one of our pages runs in a window over that page, with its screen in it.
        private void FollowApplication(bool app, TMP_InputField field, TMP_Text text)
        {
            if (appWindow && (Window.Open == null || !Window.Open.App))
            {
                Window.Show(new Window
                {
                    Title = (Navigator.LastApplication ?? "APPLICATION").ToUpperInvariant(),
                    App = true,
                    Closing = () => InteractiveTerminal.Exit(InteractiveTerminal.Application())
                });
            }
            else if (!app && Window.Open != null && Window.Open.App)
            {
                Window.Drop();
            }

            if (app && appWindow && menu.WindowText != null)
            {
                apps.Tick(menu.AppScreen(), menu.WindowText);
            }
            else if (app && !appWindow)
            {
                apps.Tick(field.text, text);

                // Another of its screens starts at its top, not where the last one was scrolled to.
                if (apps.TakeScreenChange() && terminal.scrollBarVertical != null)
                {
                    terminal.scrollBarVertical.value = 1f;
                    heldScroll = 1f;
                }
            }
            else
            {
                apps.Reset();
            }
        }

        // Each page has its own letter size. A view is made once per size and kept; the others are put away.
        private void ShowPage(MenuPage page, TMP_Text text, bool foreign)
        {
            var scale = page != null ? PageScale.For(page) : menu.Scale;
            if (!views.TryGetValue(scale, out var wanted))
            {
                views[scale] = wanted = new MenuView(terminal, frame, text, scale);
            }

            if (wanted != menu)
            {
                menu.Show(null);
                menu = wanted;
            }

            pageShown = page != null;
            gameText.alpha = pageShown ? 0f : 1f;
            KeepTextClear(text, !pageShown && !foreign);
            menu.Show(page);
        }

        private void ShowButtons(View view, bool app, bool foreign, TMP_Text text)
        {
            var label = !foreign && !app && view != null ? view.ButtonLabel : null;
            var welcome = label != null && view.ContinueTo != null;

            // The button sits over the bottom of the screen. On a long page it would cover the text.
            if (label != null && !welcome && text.preferredHeight > frame.rect.height - text.fontSize * 5f)
            {
                label = null;
            }

            // In an application BACK is its exit key and MENU closes it.
            buttons.Show(
                app || (!foreign && Navigator.CanGoBack),
                app || (!foreign && view != null && !view.IsHome && !welcome),
                label,
                pageShown,
                text.color);
        }

        // Over an amount the wheel changes it, on a screen with pages it turns the page. Where it does neither it is left to the game, to scroll a screen that is longer than the monitor.
        private void ApplicationWheel(bool app, HotSpot hit, Mouse mouse)
        {
            var running = app ? InteractiveTerminal.Application() : null;
            var wheelIsOurs = app && ((hit != null && hit.Scroll != null) || InteractiveTerminal.HasPages(running) || appWindow);

            if (wheelIsOurs)
            {
                var turned = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(turned) > 0.01f && Time.unscaledTime - lastScrolled > 0.15f)
                {
                    lastScrolled = Time.unscaledTime;
                    if (hit != null && hit.Scroll != null)
                    {
                        hit.Scroll(turned > 0f);
                    }
                    else if (InteractiveTerminal.Page(running, turned < 0f))
                    {
                        // A new page starts at its top.
                        heldScroll = 1f;
                    }
                    else if (appWindow)
                    {
                        // A screen taller than the window scrolls inside it.
                        menu.Scroll(turned > 0f ? -1 : 1);
                    }
                }

                // The game scrolls the screen on the same wheel. That is put back before anything is drawn.
                var bar = terminal.scrollBarVertical;
                if (bar != null)
                {
                    if (!wheelWasOurs)
                    {
                        heldScroll = bar.value;
                    }
                    else if (bar.value != heldScroll)
                    {
                        bar.value = heldScroll;
                    }
                }
            }

            wheelWasOurs = wheelIsOurs;
            WheelTaken = pageShown || wheelIsOurs;
        }

        // On a controller: A clicks, X is the right click, B goes back, Select opens the menu.
        private void Clicks(Mouse mouse, HotSpot hit, bool enter, bool foreign, TMP_InputField field)
        {
            var press = mouse.leftButton.wasPressedThisFrame || enter || Pad.Press(Pad.South, pad => pad.buttonSouth);
            var other = mouse.rightButton.wasPressedThisFrame || Pad.Press(Pad.West, pad => pad.buttonWest);

            if (press || other)
            {
                // A click on the locked cursor takes the focus away from the input field.
                refocusIn = 2;
                if (hit != null && press && hit.Drag != null && mouse.leftButton.wasPressedThisFrame)
                {
                    dragging = hit;
                    dragging.Holding?.Invoke(true);
                }
                else if (hit != null && press)
                {
                    Click(hit, false);
                }
                else if (hit != null && hit.AltClick != null)
                {
                    Click(hit, true);
                }
            }
            else if (!foreign && Pad.Press(Pad.East, pad => pad.buttonEast) && buttons.Back.Space.gameObject.activeSelf)
            {
                refocusIn = 2;
                Click(buttons.Back, false);
            }
            else if (!foreign && Pad.Press(Pad.Select, pad => pad.selectButton) && buttons.Menu.Space.gameObject.activeSelf)
            {
                refocusIn = 2;
                Click(buttons.Menu, false);
            }

            if (refocusIn > 0 && --refocusIn == 0 && field.interactable && !field.isFocused)
            {
                field.ActivateInputField();
                field.Select();
            }
        }

        // The game's own text pages start a line lower, so BACK and MENU do not sit on their first line.
        private void KeepTextClear(TMP_Text text, bool clear)
        {
            if (float.IsNaN(textTop))
            {
                textTop = text.margin.y;
            }

            var want = clear ? Mathf.Max(textTop, text.fontSize * 1.9f) : textTop;
            if (text.margin.y != want)
            {
                var margin = text.margin;
                text.margin = new Vector4(margin.x, want, margin.z, margin.w);
            }
        }

        private void MoveCursor(Mouse mouse)
        {
            var delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            lastMoved = Time.unscaledTime;
            Pad.MouseMoved();

            // Half a screen of mouse travel crosses the whole terminal.
            var bounds = frame.rect;
            var scale = bounds.width / (Screen.width * 0.5f) * Settings.CursorSpeed.Value;
            var position = (Vector2)cursor.localPosition + delta * scale;
            position.x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
            position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
            cursor.localPosition = position;
        }

        // The cursor without a mouse. The d-pad, the left stick and the arrow keys put it on the next thing that can be clicked, the right stick scrolls, the shoulders switch tabs and the triggers turn a page.
        private bool Steer(Keyboard keys, bool app)
        {
            var now = Time.unscaledTime;
            var stick = Vector2.zero;

            if (Gamepad.all.Count > 0)
            {
                stick = Pad.Stick(pad => pad.leftStick);

                if (pageShown && !app)
                {
                    var tab = Pad.Press(Pad.LeftShoulder, pad => pad.leftShoulder) ? -1 : Pad.Press(Pad.RightShoulder, pad => pad.rightShoulder) ? 1 : 0;
                    if (tab != 0 && menu.SwitchTab(tab > 0))
                    {
                        PlayClick();
                    }

                    var leaf = Pad.Press(Pad.LeftTrigger, pad => pad.leftTrigger) ? -1 : Pad.Press(Pad.RightTrigger, pad => pad.rightTrigger) ? 1 : 0;
                    if (leaf != 0)
                    {
                        menu.Scroll(leaf * Mathf.Max(1, menu.VisibleRows - 1));
                    }
                }

                var turn = Pad.Stick(pad => pad.rightStick).y;
                if (Mathf.Abs(turn) > 0.3f)
                {
                    if (pageShown && (!app || appWindow) && now - lastScrolled > 0.07f)
                    {
                        lastScrolled = now;
                        menu.Scroll(turn > 0f ? -1 : 1);
                    }
                    else if ((!pageShown || app) && terminal.scrollBarVertical != null)
                    {
                        // Lines per second, so a long page and a short one scroll at the same pace.
                        var bar = terminal.scrollBarVertical;
                        var step = LineStep();
                        bar.value = Mathf.Clamp01(bar.value + turn * (step > 0f ? step * 25f : 1.2f) * Time.unscaledDeltaTime);
                        heldScroll = bar.value;
                    }
                }
            }

            // While an app runs the arrow keys are its own. It knows nothing about the d-pad.
            var want = Vector2.zero;
            if (stick.sqrMagnitude > 0.3f)
            {
                want = Mathf.Abs(stick.y) >= Mathf.Abs(stick.x)
                    ? (stick.y > 0f ? Vector2.up : Vector2.down)
                    : (stick.x > 0f ? Vector2.right : Vector2.left);
            }
            else if (Pad.Held(pad => pad.dpad.up) || (!app && keys != null && keys.upArrowKey.isPressed))
            {
                want = Vector2.up;
            }
            else if (Pad.Held(pad => pad.dpad.down) || (!app && keys != null && keys.downArrowKey.isPressed))
            {
                want = Vector2.down;
            }
            else if (Pad.Held(pad => pad.dpad.left) || (!app && keys != null && keys.pageUpKey.isPressed))
            {
                want = Vector2.left;
            }
            else if (Pad.Held(pad => pad.dpad.right) || (!app && keys != null && keys.pageDownKey.isPressed))
            {
                want = Vector2.right;
            }

            if (want == Vector2.zero)
            {
                heldStep = want;
                return false;
            }

            // One step on the press, then a run of them while it is held.
            if (want != heldStep)
            {
                heldStep = want;
                stepAt = now + 0.4f;
                Step(want, app);
            }
            else if (now >= stepAt)
            {
                stepAt = now + (app || want.x != 0f ? 0.25f : 0.09f);
                Step(want, app);
            }

            lastMoved = now;
            return true;
        }

        private void Step(Vector2 direction, bool app)
        {
            // Left and right turn an application's pages, or change the amount the cursor is on.
            if (app && direction.x != 0f)
            {
                var running = InteractiveTerminal.Application();
                var under = apps.SpotAt(cursor.position);
                if (under != null && under.Scroll != null)
                {
                    under.Scroll(direction.x > 0f);
                }
                else if (InteractiveTerminal.HasPages(running))
                {
                    InteractiveTerminal.Page(running, direction.x > 0f);
                }

                return;
            }

            Collect(app);
            var from = (Vector2)cursor.localPosition;
            var paged = pageShown && !app;
            var zone = steering.ZoneAt(from);
            var down = direction.y < 0f;

            if (paged && direction.y != 0f && zone == Zone.List && menu.ScrollFrom(cursor.position, down))
            {
                return;
            }

            var at = steering.Step(from, direction, terminal.screenText.textComponent.fontSize * 0.6f);
            if (at < 0 && paged && zone == Zone.Tabs && direction.y != 0f && menu.ScrollTabs(cursor.position, down ? 1 : -1))
            {
                return;
            }

            if (at >= 0)
            {
                var to = steering.Targets[at].Point;

                // In an application's window the next entry may be below what is in view: the window follows it.
                if (app && appWindow)
                {
                    for (var tries = 0; tries < 60 && !menu.InWindowView(frame.TransformPoint(to)); tries++)
                    {
                        if (!menu.Scroll(to.y < from.y ? 1 : -1))
                        {
                            break;
                        }

                        Collect(app);
                        if (at >= steering.Targets.Count)
                        {
                            return;
                        }

                        to = steering.Targets[at].Point;
                    }
                }

                cursor.localPosition = to;
                steering.Remember(at);
                return;
            }

            if (pageShown && direction.y != 0f && (!paged || zone == Zone.List || zone == Zone.Window))
            {
                menu.Scroll(down ? 1 : -1);
            }
            else if (paged && zone == Zone.List && menu.Jump(cursor.position, direction.x > 0f, frame, out var stand))
            {
                cursor.localPosition = stand;
            }
        }

        private void Collect(bool app)
        {
            steering.Clear();
            buttons.Targets(frame, steering);
            if (app)
            {
                apps.Targets(frame, steering);
            }
            else if (pageShown)
            {
                menu.Targets(frame, steering);
            }
        }

        private bool SnapToNearest(bool app)
        {
            Collect(app);
            var best = steering.Nearest(cursor.localPosition, true);
            if (best < 0)
            {
                return false;
            }

            cursor.localPosition = steering.Targets[best].Point;
            return true;
        }

        // In the window: its X, else the application's entry under the point, if that part of it is in view.
        private HotSpot AppSpot(Vector3 world)
        {
            if (!appWindow)
            {
                return apps.SpotAt(world);
            }

            return menu.SpotAt(world) ?? (menu.InWindowView(world) ? apps.SpotAt(world) : null);
        }

        private HotSpot SpotUnderCursor()
        {
            return buttons.At(cursor.position) ?? (pageShown ? menu.SpotAt(cursor.position) : GameBarAt(cursor.position));
        }

        // The game's own scrollbar beside its text pages, taken hold of like ours.
        private HotSpot GameBarAt(Vector3 world)
        {
            var bar = terminal.scrollBarVertical;
            var track = bar != null ? (RectTransform)bar.transform : null;
            var handle = bar != null ? bar.handleRect : null;
            if (track == null || handle == null || !track.gameObject.activeInHierarchy || LineStep() <= 0f)
            {
                return null;
            }

            if (gameBarGrip == null)
            {
                gameBarGrip = new Grip(track, handle, terminal.screenText.textComponent.fontSize * 0.6f, along =>
                {
                    bar.value = 1f - along;
                    heldScroll = bar.value;
                });
            }

            return gameBarGrip.At(world);
        }

        private Grip gameBarGrip;

        private void PlayClick()
        {
            var clips = terminal.keyboardClips;
            if (terminal.terminalAudio != null && clips != null && clips.Length > 0)
            {
                terminal.terminalAudio.PlayOneShot(clips[Random.Range(0, clips.Length)]);
            }
        }

        private void Click(HotSpot spot, bool alternate)
        {
            PlayClick();
            if (alternate)
            {
                spot.AltClick();
            }
            else if (spot.Click != null)
            {
                spot.Click();
            }
        }

        private void ShowHover(HotSpot spot, Color color)
        {
            if (spot == null)
            {
                Ui.SetActive(hover, false);
                return;
            }

            // The highlight stays put in the hierarchy and is only moved. Handing it from parent to parent made the whole screen rebuild.
            var min = frame.InverseTransformPoint(spot.Space.TransformPoint(spot.Area.min));
            var max = frame.InverseTransformPoint(spot.Space.TransformPoint(spot.Area.max));
            Ui.Place(hover, Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            hoverImage.color = new Color(color.r, color.g, color.b, 0.16f);
            Ui.TintOutline(hoverFrame, color);
            Ui.SetActive(hover, true);
        }

        private void Build(TMP_InputField field, TMP_Text text)
        {
            if (cursor != null)
            {
                return;
            }

            // The input field is the scrolling content, so everything of ours lives on the part that stands still.
            var scroll = field.GetComponentInParent<ScrollRect>(true);
            frame = scroll != null ? (scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform) : (RectTransform)field.transform.parent;

            // Hides the game's text under our pages while the input field keeps taking keys.
            gameText = field.GetComponent<CanvasGroup>();
            if (gameText == null)
            {
                gameText = field.gameObject.AddComponent<CanvasGroup>();
            }

            menu = new MenuView(terminal, frame, text, Mathf.Clamp(Settings.TextScale.Value, 0.6f, 1f));
            views[menu.Scale] = menu;
            tweaks = new ScreenTweaks(terminal, frame);
            FitCredits();

            buttons = new TopButtons(frame, text);
            buttons.Menu.Click = () =>
            {
                if (InteractiveTerminal.Running())
                {
                    Navigator.LeaveToMenu = true;
                    InteractiveTerminal.Exit(InteractiveTerminal.Application());
                }
                else
                {
                    Navigator.Home(terminal);
                }
            };
            buttons.Back.Click = () =>
            {
                if (menu.CloseWindow())
                {
                    return;
                }

                if (!InteractiveTerminal.Running())
                {
                    Navigator.Back(terminal);
                }
                else if (!InteractiveTerminal.RunExitHandlers() && !InteractiveTerminal.PressExitKey())
                {
                    InteractiveTerminal.Exit(InteractiveTerminal.Application());
                }
            };
            buttons.Smaller.Click = () => PageScale.Change(Navigator.Current?.Page, -0.05f);
            buttons.Bigger.Click = () => PageScale.Change(Navigator.Current?.Page, 0.05f);
            buttons.Continue.Click = () => Navigator.Continue(terminal);

            var size = text.fontSize;
            hoverImage = Ui.NewBox("ClickableTerminalHover", frame);
            hover = hoverImage.rectTransform;
            hoverFrame = Ui.NewOutline("Frame", hover, Mathf.Max(1f, size * 0.12f));
            hover.gameObject.SetActive(false);

            var cursorObject = new GameObject("ClickableTerminalCursor", typeof(RectTransform), typeof(RawImage));
            cursorObject.layer = frame.gameObject.layer;
            cursor = (RectTransform)cursorObject.transform;
            cursor.SetParent(frame, false);
            cursor.anchorMin = cursor.anchorMax = new Vector2(0.5f, 0.5f);
            cursor.pivot = new Vector2(0f, 1f);

            var arrow = CursorArt.Arrow();
            var width = size * 0.8f;
            cursor.sizeDelta = new Vector2(width, width * arrow.height / arrow.width);
            cursorImage = cursorObject.GetComponent<RawImage>();
            cursorImage.texture = arrow;
            cursorImage.raycastTarget = false;
        }

        // The credits box has a fixed size, so a large balance gets smaller letters instead of running out of it.
        private void FitCredits()
        {
            var credits = terminal.topRightText;
            if (credits == null)
            {
                return;
            }

            credits.enableWordWrapping = false;
            credits.fontSizeMax = credits.fontSize;
            credits.fontSizeMin = credits.fontSize * 0.4f;
            credits.enableAutoSizing = true;

            var box = Ui.CreditsBox(credits);
            if (box == null)
            {
                return;
            }

            var corners = new Vector3[4];
            box.GetWorldCorners(corners);
            var boxRight = credits.rectTransform.InverseTransformPoint(corners[2]).x;
            var overrun = credits.rectTransform.rect.xMax - boxRight + credits.fontSize * 0.3f;
            if (overrun > 0f)
            {
                var margin = credits.margin;
                credits.margin = new Vector4(margin.x, margin.y, Mathf.Max(margin.z, overrun), margin.w);
            }
        }
    }
}
