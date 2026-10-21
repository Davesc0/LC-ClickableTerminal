using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace ClickableTerminal
{
    internal class TerminalMouse : MonoBehaviour
    {
        private const float HideAfter = 5f;

        internal static bool InUse;

        internal static bool WheelTaken;

        private static TerminalMouse active;

        internal static float LineStep()
        {
            var self = active;
            var text = self != null && self.terminal != null && self.terminal.screenText != null ? self.terminal.screenText.textComponent : null;
            var scroll = text != null ? text.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
            if (scroll == null || scroll.content == null || scroll.viewport == null)
            {
                return 0f;
            }

            var hidden = scroll.content.rect.height - scroll.viewport.rect.height;
            var info = text.textInfo;
            var line = info != null && info.lineCount > 0 ? info.lineInfo[0].lineHeight : text.fontSize;
            return hidden > 1f ? line / hidden : 0f;
        }

        internal static string[] PadNames;

        private static readonly string[] Xbox = { "A", "X", "Y" };
        private static readonly string[] PlayStation = { "Cross", "Square", "Triangle" };
        private static readonly string[] Nintendo = { "B", "Y", "X" };

        internal static string Words(string hint)
        {
            var names = PadNames;
            if (names == null)
            {
                return hint;
            }

            string Key(int button, int name) => PadIcons.Markup(button) ?? names[name] + ":";

            return hint.Replace("Right click:", Key(PadIcons.West, 1)).Replace("Right:", Key(PadIcons.West, 1))
                .Replace("Wheel:", Key(PadIcons.North, 2)).Replace("Click:", Key(PadIcons.South, 0))
                .Replace("Click to", (PadIcons.Markup(PadIcons.South) ?? names[0]) + " to");
        }

        private static void Using(Gamepad pad)
        {
            var kind = pad.GetType().Name;
            PadNames = kind.Contains("DualShock") || kind.Contains("DualSense") ? PlayStation
                : kind.Contains("Switch") ? Nintendo
                : Xbox;
        }

        private void ShowPadIcon(HotSpot button, int pad, string word, Color color)
        {
            var label = button.Space.GetComponentInChildren<TextMeshProUGUI>(true);
            var icon = button.Space.Find("PadIcon")?.GetComponent<Image>();
            var sprite = PadNames != null ? PadIcons.Get(pad) : null;

            if (icon == null)
            {
                var holder = new GameObject("PadIcon", typeof(RectTransform), typeof(Image));
                holder.layer = button.Space.gameObject.layer;
                holder.transform.SetParent(button.Space, false);
                icon = holder.GetComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;

                var rect = icon.rectTransform;
                var height = button.Space.rect.height * 0.8f;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(height, height);
                rect.anchoredPosition = new Vector2(height * 0.35f, 0f);
            }

            icon.enabled = sprite != null;
            icon.sprite = sprite;
            icon.color = color;

            var text = sprite != null ? word : "[ " + word + " ]";
            if (label.text != text)
            {
                label.text = text;
                label.margin = sprite != null ? new Vector4(icon.rectTransform.sizeDelta.x * 1.2f, 0f, 0f, 0f) : Vector4.zero;
            }
        }

        internal const float HeaderScale = 0.75f;

        private Terminal terminal;
        private RectTransform frame;
        private RectTransform cursor;
        private RawImage cursorImage;
        private RectTransform hover;
        private Image hoverImage;
        private RectTransform hoverFrame;
        private HotSpot lastHit;
        private CanvasGroup gameText;
        private TextMeshProUGUI continueLabel;
        private MenuView menu;
        private readonly Dictionary<float, MenuView> views = new Dictionary<float, MenuView>();
        private readonly InteractiveMouse apps = new InteractiveMouse();

        private readonly HotSpot backButton = new HotSpot();
        private readonly HotSpot menuButton = new HotSpot();
        private readonly HotSpot continueButton = new HotSpot();
        private readonly HotSpot smallerButton = new HotSpot();
        private readonly HotSpot biggerButton = new HotSpot();
        private int refocusIn;
        private float lastMoved = -100f;
        private bool pageShown;
        private bool appWindow;
        private string lastShown;
        private float zoom;
        private float lastScrolled;
        private float heldScroll;
        private bool wheelWasOurs;
        private RenderTexture sharp;
        private static readonly Vector3[] Corners = new Vector3[4];
        private readonly List<Vector2> targets = new List<Vector2>();
        private readonly List<int> zones = new List<int>();
        private readonly Dictionary<int, Vector2> lastIn = new Dictionary<int, Vector2>();
        private Vector2 heldStep;
        private float stepAt;
        private readonly float[] padPressed = new float[9];

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

        private MenuPage failedPage;

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

                KeepScreenOn();
                return;
            }

            InteractiveTerminal.Tick();

            var field = terminal.screenText;
            var text = field.textComponent;
            var node = terminal.currentNode;

            BuildUi(field, text);

            if (!InUse)
            {
                InUse = true;
                cursor.localPosition = frame.rect.center;
                zoom = 0f;
            }

            // Any other mod that has taken the input away is left alone.
            var app = InteractiveTerminal.Running();
            var foreign = !app && !field.interactable;
            var view = Navigator.Current;

            appWindow = app && Settings.AppsInWindow.Value && Settings.OwnPages.Value && view != null && view.Page != null;
            var page = !foreign && (!app || appWindow) && view != null && Settings.OwnPages.Value ? view.Page : null;
            if (page != null && page == failedPage)
            {
                page = null;
            }

            var shownNow = $"{(node != null ? node.name : "-")} -> {(page != null ? page.Title : "game text")}{(foreign ? " (another mod has the input)" : "")}{(app ? " (application)" : "")}";
            if (shownNow != lastShown)
            {
                lastShown = shownNow;
                Plugin.Debug("Screen: " + shownNow);
            }

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

            if (pageShown)
            {
                menu.SetTyped(Commands.Typed(terminal));
                menu.Tick();

                // A free spinning wheel sends a step every frame, so the list moves at most twenty times a second.
                var wheel = mouse.scroll.ReadValue().y;
                if (!app && Mathf.Abs(wheel) > 0.01f && Time.unscaledTime - lastScrolled > 0.05f)
                {
                    lastScrolled = Time.unscaledTime;
                    if (!menu.ScrollSide(cursor.position, wheel > 0f ? -1 : 1))
                    {
                        menu.Scroll(wheel > 0f ? -2 : 2);
                    }
                }
            }

            MoveCursor(mouse);

            var keys = Keyboard.current;
            var steered = Steer(keys, app);

            var enter = !app && keys != null && terminal.textAdded == 0
                        && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame);

            if (keys != null && keys.anyKey.wasPressedThisFrame && !steered && !enter && !InteractiveTerminal.PressingKey)
            {
                lastMoved = -100f;
            }

            var pad = PadNames != null;
            var visible = !foreign && (pad || Time.unscaledTime - lastMoved < HideAfter);


            var label = !foreign && !app && view != null ? view.ButtonLabel : null;
            var welcome = label != null && view.ContinueTo != null;

            // The button sits over the bottom of the screen. On a long page it would cover the text.
            if (label != null && !welcome && text.preferredHeight > frame.rect.height - text.fontSize * 5f)
            {
                label = null;
            }
            Ui.SetActive(backButton.Space, app || (!foreign && Navigator.CanGoBack));
            Ui.SetActive(menuButton.Space, app || (!foreign && view != null && !view.IsHome && !welcome));
            Ui.SetActive(continueButton.Space, label != null);

            Ui.SetActive(smallerButton.Space, pageShown);
            Ui.SetActive(biggerButton.Space, pageShown);

            ShowPadIcon(backButton, PadIcons.East, "BACK", text.color);
            ShowPadIcon(menuButton, PadIcons.Select, "MENU", text.color);

            if (label != null && continueLabel.text != label)
            {
                continueLabel.text = label;
                continueButton.Space.sizeDelta = new Vector2(text.fontSize * (label.Length * 0.62f + 1f), text.fontSize * 1.7f);
            }

            var hit = !visible ? null
                : !app ? SpotUnderCursor()
                : Under(backButton, cursor.position) ? backButton
                : Under(menuButton, cursor.position) ? menuButton
                : AppSpot(cursor.position);

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

            ShowHover(app && !appWindow && hit != backButton && hit != menuButton ? null : hit, text.color);

            // Where the wheel neither changes an amount nor turns a page, the game scrolls the screen.
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
                        heldScroll = 1f;
                    }
                    else if (appWindow)
                    {
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

            cursorImage.color = text.color;
            Ui.SetActive(cursor, visible && !pad);

            var press = mouse.leftButton.wasPressedThisFrame || enter || PadPressed(0, pad => pad.buttonSouth);
            var other = mouse.rightButton.wasPressedThisFrame || PadPressed(1, pad => pad.buttonWest);

            if (press || other)
            {
                // A click on the locked cursor takes the focus away from the input field.
                refocusIn = 2;

                if (hit != null && press)
                {
                    Click(hit, false);
                }
                else if (hit != null && hit.AltClick != null)
                {
                    Click(hit, true);
                }
            }
            else if (!foreign && PadPressed(3, pad => pad.buttonEast) && backButton.Space.gameObject.activeSelf)
            {
                refocusIn = 2;
                Click(backButton, false);
            }
            else if (!foreign && PadPressed(4, pad => pad.selectButton) && menuButton.Space.gameObject.activeSelf)
            {
                refocusIn = 2;
                Click(menuButton, false);
            }

            if (refocusIn > 0 && --refocusIn == 0 && field.interactable && !field.isFocused)
            {
                field.ActivateInputField();
                field.Select();
            }
        }

        private float textTop = float.NaN;

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

        private static void Resize(float by)
        {
            PageScale.Change(Navigator.Current?.Page, by);
        }

        // The game switches the screen and its light off when the player steps away. Here they stay on, only for this
        // player.
        private void KeepScreenOn()
        {
            if (!Settings.ScreenAlwaysOn.Value || terminal == null || terminal.terminalInUse || terminal.terminalUIScreen == null)
            {
                return;
            }

            if (!terminal.terminalUIScreen.gameObject.activeSelf)
            {
                terminal.terminalUIScreen.gameObject.SetActive(true);
            }

            if (terminal.terminalLight != null && !terminal.terminalLight.enabled)
            {
                terminal.terminalLight.enabled = true;
            }
        }

        private void LateUpdate()
        {
            if (!InUse)
            {
                return;
            }

            // The game fades its scrollbar in whenever its hidden page is scrolled.
            if (pageShown && terminal.scrollBarCanvasGroup != null)
            {
                terminal.scrollBarCanvasGroup.alpha = 0f;
            }

            var player = GameNetworkManager.Instance.localPlayerController;
            var camera = player != null ? player.gameplayCamera : null;
            if (camera == null || frame == null)
            {
                return;
            }

            if (Settings.SharpTerminal.Value)
            {
                Sharpen(camera);
            }

            if (Settings.ZoomToScreen.Value)
            {
                // The game pulls the field of view back every frame, this runs after it.
                zoom = Mathf.Lerp(zoom > 0f ? zoom : camera.fieldOfView, FovThatFits(camera), 6f * Time.deltaTime);
                camera.fieldOfView = zoom;
            }
        }

        // While the terminal is in use the game points the camera at its 970x580 texture every frame. This runs after
        // that and hands it a bigger one. The game switches back by itself when the terminal closes.
        private void Sharpen(Camera camera)
        {
            var original = terminal.playerScreenTexHighRes;
            var hud = HUDManager.Instance;
            if (original == null || hud == null || hud.playerScreenTexture == null || Screen.height <= original.height)
            {
                return;
            }

            var height = Screen.height;
            var width = Mathf.RoundToInt(height * (float)original.width / original.height);

            if (sharp == null || sharp.width != width || sharp.height != height)
            {
                ReleaseSharp();

                var shape = original.descriptor;
                shape.width = width;
                shape.height = height;

                sharp = new RenderTexture(shape)
                {
                    name = "ClickableTerminalScreen",
                    filterMode = original.filterMode,
                    wrapMode = original.wrapMode
                };
                sharp.Create();
            }

            hud.playerScreenTexture.texture = sharp;
            camera.targetTexture = sharp;
        }

        private void ReleaseSharp()
        {
            if (sharp != null)
            {
                sharp.Release();
                Destroy(sharp);
                sharp = null;
            }
        }

        private void OnDestroy()
        {
            ReleaseSharp();
        }

        private float FovThatFits(Camera camera)
        {
            frame.GetWorldCorners(Corners);
            var tangent = 0f;

            foreach (var corner in Corners)
            {
                var local = camera.transform.InverseTransformPoint(corner);
                if (local.z <= 0.01f)
                {
                    return camera.fieldOfView;
                }

                tangent = Mathf.Max(tangent, Mathf.Abs(local.y) / local.z, Mathf.Abs(local.x) / local.z / camera.aspect);
            }

            return Mathf.Clamp(2f * Mathf.Atan(tangent / 0.88f) * Mathf.Rad2Deg, 15f, 70f);
        }

        private void MoveCursor(Mouse mouse)
        {
            var delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            lastMoved = Time.unscaledTime;
            PadNames = null;

            var bounds = frame.rect;
            var scale = bounds.width / (Screen.width * 0.5f) * Settings.CursorSpeed.Value;
            var position = (Vector2)cursor.localPosition + delta * scale;

            position.x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
            position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
            cursor.localPosition = position;
        }

        private bool Steer(Keyboard keys, bool app)
        {
            var now = Time.unscaledTime;

            var stick = Vector2.zero;
            if (Gamepad.all.Count > 0)
            {
                stick = PadStick(pad => pad.leftStick);

                if (pageShown && !app)
                {
                    var tab = PadPressed(5, pad => pad.leftShoulder) ? -1 : PadPressed(6, pad => pad.rightShoulder) ? 1 : 0;
                    if (tab != 0 && menu.SwitchTab(tab > 0))
                    {
                        PlayClick();
                    }

                    var leaf = PadPressed(7, pad => pad.leftTrigger) ? -1 : PadPressed(8, pad => pad.rightTrigger) ? 1 : 0;
                    if (leaf != 0)
                    {
                        menu.Scroll(leaf * Mathf.Max(1, menu.VisibleRows - 1));
                    }
                }

                var turn = PadStick(pad => pad.rightStick).y;
                if (Mathf.Abs(turn) > 0.3f)
                {
                    if (pageShown && (!app || appWindow) && now - lastScrolled > 0.07f)
                    {
                        lastScrolled = now;
                        menu.Scroll(turn > 0f ? -1 : 1);
                    }
                    else if ((!pageShown || app) && terminal.scrollBarVertical != null)
                    {
                        var bar = terminal.scrollBarVertical;
                        // Lines per second, so a long page and a short one scroll at the same pace.
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
            else if (PadHeld(pad => pad.dpad.up) || (!app && keys != null && keys.upArrowKey.isPressed))
            {
                want = Vector2.up;
            }
            else if (PadHeld(pad => pad.dpad.down) || (!app && keys != null && keys.downArrowKey.isPressed))
            {
                want = Vector2.down;
            }
            else if (PadHeld(pad => pad.dpad.left) || (!app && keys != null && keys.pageUpKey.isPressed))
            {
                want = Vector2.left;
            }
            else if (PadHeld(pad => pad.dpad.right) || (!app && keys != null && keys.pageDownKey.isPressed))
            {
                want = Vector2.right;
            }

            if (want == Vector2.zero)
            {
                heldStep = want;
                return false;
            }

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

        // One controller often shows up as two devices (its own and the one Steam makes), and the same press then
        // arrives twice, a frame apart.
        private bool PadPressed(int button, System.Func<Gamepad, ButtonControl> pick)
        {
            foreach (var pad in Gamepad.all)
            {
                if (pick(pad).wasPressedThisFrame)
                {
                    Using(pad);
                    var again = Time.unscaledTime - padPressed[button] < 0.2f;
                    padPressed[button] = Time.unscaledTime;
                    return !again;
                }
            }

            return false;
        }

        private static bool PadHeld(System.Func<Gamepad, ButtonControl> pick)
        {
            foreach (var pad in Gamepad.all)
            {
                if (pick(pad).isPressed)
                {
                    Using(pad);
                    return true;
                }
            }

            return false;
        }

        private static Vector2 PadStick(System.Func<Gamepad, StickControl> pick)
        {
            var most = Vector2.zero;
            foreach (var pad in Gamepad.all)
            {
                var value = pick(pad).ReadValue();
                if (value.sqrMagnitude > most.sqrMagnitude)
                {
                    most = value;
                    if (most.sqrMagnitude > 0.04f)
                    {
                        Using(pad);
                    }
                }
            }

            return most;
        }

        private void Step(Vector2 direction, bool app)
        {
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
            var zone = paged ? ZoneAt(from) : -1;

            if (paged && direction.y != 0f && zone == MenuView.ZoneList && menu.ScrollFrom(cursor.position, direction.y < 0f))
            {
                return;
            }

            // The page is in parts (the top row, the bar above the list, the tabs, the list, the panels). A step stays
            // in its part while it can, and only then goes over to the next part that way.
            // In the list a sideways step never slips up to the column heads: it goes over to the tabs or the panel.
            var at = Pick(from, direction, zone < 0 ? null : (System.Func<int, bool>)(z => z == zone), zone != MenuView.ZoneList);
            if (at < 0 && paged)
            {
                foreach (var next in NextZones(zone, direction))
                {
                    at = Enter(next, from, direction);
                    if (at >= 0)
                    {
                        break;
                    }
                }
            }

            if (at < 0 && paged && zone == MenuView.ZoneTabs && direction.y != 0f && menu.ScrollSide(cursor.position, direction.y < 0f ? 1 : -1))
            {
                return;
            }

            if (at >= 0)
            {
                var to = targets[at];

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
                        if (at >= targets.Count)
                        {
                            return;
                        }

                        to = targets[at];
                    }
                }

                cursor.localPosition = to;
                if (paged)
                {
                    lastIn[zones[at]] = to;
                }

                return;
            }

            if (pageShown && direction.y != 0f && (!paged || zone == MenuView.ZoneList || zone == MenuView.ZoneWindow))
            {
                menu.Scroll(direction.y < 0f ? 1 : -1);
            }
            else if (paged && zone == MenuView.ZoneList && menu.Jump(cursor.position, direction.x > 0f, frame, out var stand))
            {
                cursor.localPosition = stand;
            }
        }

        private void Collect(bool app)
        {
            targets.Clear();
            zones.Clear();
            AddTarget(backButton);
            AddTarget(menuButton);
            AddTarget(continueButton);
            AddTarget(smallerButton);
            AddTarget(biggerButton);

            if (app)
            {
                apps.Targets(frame, targets);
                while (zones.Count < targets.Count)
                {
                    zones.Add(MenuView.ZoneList);
                }
            }
            else if (pageShown)
            {
                menu.Targets(frame, targets, zones);
            }
        }

        private int ZoneAt(Vector2 point)
        {
            var zone = -1;
            var best = float.MaxValue;
            for (var i = 0; i < targets.Count; i++)
            {
                var distance = (targets[i] - point).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    zone = zones[i];
                }
            }

            return zone;
        }

        private static IEnumerable<int[]> NextZones(int zone, Vector2 direction)
        {
            var up = direction.y > 0f;
            var down = direction.y < 0f;
            var left = direction.x < 0f;
            var right = direction.x > 0f;

            switch (zone)
            {
                case MenuView.ZoneHeader:
                    if (down)
                    {
                        yield return new[] { MenuView.ZoneWindow };
                        yield return new[] { MenuView.ZoneBar };
                        yield return new[] { MenuView.ZoneTabs, MenuView.ZoneList, MenuView.ZonePanel };
                    }

                    break;
                case MenuView.ZoneBar:
                    if (up)
                    {
                        yield return new[] { MenuView.ZoneHeader };
                    }
                    else if (down)
                    {
                        yield return new[] { MenuView.ZoneTabs, MenuView.ZoneList, MenuView.ZonePanel };
                    }

                    break;
                case MenuView.ZoneTabs:
                case MenuView.ZoneList:
                case MenuView.ZonePanel:
                    if (up)
                    {
                        yield return new[] { MenuView.ZoneBar };
                        yield return new[] { MenuView.ZoneHeader };
                    }
                    else if (right && zone == MenuView.ZoneTabs)
                    {
                        yield return new[] { MenuView.ZoneList };
                        yield return new[] { MenuView.ZonePanel };
                    }
                    else if (right && zone == MenuView.ZoneList)
                    {
                        yield return new[] { MenuView.ZonePanel };
                    }
                    else if (left && zone == MenuView.ZoneList)
                    {
                        yield return new[] { MenuView.ZoneTabs };
                    }
                    else if (left && zone == MenuView.ZonePanel)
                    {
                        yield return new[] { MenuView.ZoneList };
                        yield return new[] { MenuView.ZoneTabs };
                    }

                    break;
                case MenuView.ZoneWindow:
                    if (up)
                    {
                        yield return new[] { MenuView.ZoneHeader };
                    }

                    break;
            }
        }

        // The nearest target that way, among those the filter lets through. -1 when there is none.
        private int Pick(Vector2 from, Vector2 direction, System.Func<int, bool> inZone, bool slanted)
        {
            var side = new Vector2(direction.y, direction.x);
            var sameLine = terminal.screenText.textComponent.fontSize * 0.6f;
            var vertical = direction.y != 0f;
            var nearest = float.MaxValue;

            if (vertical)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var along = Vector2.Dot(targets[i] - from, direction);
                    if ((inZone == null || inZone(zones[i])) && along > sameLine && along < nearest)
                    {
                        nearest = along;
                    }
                }
            }

            var best = float.MaxValue;
            var found = -1;
            for (var i = 0; i < targets.Count; i++)
            {
                if (inZone != null && !inZone(zones[i]))
                {
                    continue;
                }

                var offset = targets[i] - from;
                var along = Vector2.Dot(offset, direction);
                var across = Mathf.Abs(Vector2.Dot(offset, side));
                var fits = vertical ? along > sameLine && along <= nearest + sameLine : along > 1f && across <= sameLine;
                var score = vertical ? across : along;
                if (fits && score < best)
                {
                    best = score;
                    found = i;
                }
            }

            if (found < 0 && !vertical && slanted)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    if (inZone != null && !inZone(zones[i]))
                    {
                        continue;
                    }

                    var offset = targets[i] - from;
                    var along = Vector2.Dot(offset, direction);
                    var score = Mathf.Abs(Vector2.Dot(offset, side)) * 2f + along;
                    if (along > sameLine && score < best)
                    {
                        best = score;
                        found = i;
                    }
                }
            }

            return found;
        }

        // Into another part: where the cursor last was in it, else what is closest across from where it comes.
        private int Enter(int[] into, Vector2 from, Vector2 direction)
        {
            if (into.Length == 1 && lastIn.TryGetValue(into[0], out var was))
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    if (zones[i] == into[0] && (targets[i] - was).sqrMagnitude < 4f)
                    {
                        return i;
                    }
                }
            }

            var side = new Vector2(direction.y, direction.x);
            var best = float.MaxValue;
            var found = -1;
            for (var i = 0; i < targets.Count; i++)
            {
                if (System.Array.IndexOf(into, zones[i]) < 0)
                {
                    continue;
                }

                var offset = targets[i] - from;
                var along = Vector2.Dot(offset, direction);
                var score = Mathf.Abs(Vector2.Dot(offset, side)) + Mathf.Abs(along) * 0.3f + (along < 0f ? 100000f : 0f);
                if (score < best)
                {
                    best = score;
                    found = i;
                }
            }

            return found;
        }

        private bool SnapToNearest(bool app)
        {
            Collect(app);
            var from = (Vector2)cursor.localPosition;
            var best = -1;
            for (var i = 0; i < targets.Count; i++)
            {
                if (zones[i] != MenuView.ZoneHeader && (best < 0 || (targets[i] - from).sqrMagnitude < (targets[best] - from).sqrMagnitude))
                {
                    best = i;
                }
            }

            if (best < 0)
            {
                return false;
            }

            cursor.localPosition = targets[best];
            return true;
        }

        private HotSpot AppSpot(Vector3 world)
        {
            if (!appWindow)
            {
                return apps.SpotAt(world);
            }

            return menu.SpotAt(world) ?? (menu.InWindowView(world) ? apps.SpotAt(world) : null);
        }

        private void AddTarget(HotSpot button)
        {
            if (button.Space != null && button.Space.gameObject.activeSelf)
            {
                targets.Add(frame.InverseTransformPoint(button.Space.TransformPoint(button.Space.rect.center)));
                zones.Add(MenuView.ZoneHeader);
            }
        }

        private HotSpot SpotUnderCursor()
        {
            var world = cursor.position;

            if (Under(backButton, world))
            {
                return backButton;
            }

            if (Under(menuButton, world))
            {
                return menuButton;
            }

            if (Under(continueButton, world))
            {
                return continueButton;
            }

            if (Under(smallerButton, world))
            {
                return smallerButton;
            }

            if (Under(biggerButton, world))
            {
                return biggerButton;
            }

            return pageShown ? menu.SpotAt(world) : null;
        }

        private static bool Under(HotSpot button, Vector3 world)
        {
            if (button.Space == null || !button.Space.gameObject.activeSelf)
            {
                return false;
            }

            button.Area = button.Space.rect;
            return button.Area.Contains(button.Space.InverseTransformPoint(world));
        }

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

            // The highlight stays put in the hierarchy and is only moved. Handing it from parent to parent made the
            // whole screen rebuild.
            var min = frame.InverseTransformPoint(spot.Space.TransformPoint(spot.Area.min));
            var max = frame.InverseTransformPoint(spot.Space.TransformPoint(spot.Area.max));
            Ui.Place(hover, Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            hoverImage.color = new Color(color.r, color.g, color.b, 0.16f);
            Ui.TintOutline(hoverFrame, color);
            Ui.SetActive(hover, true);
        }

        private void BuildUi(TMP_InputField field, TMP_Text text)
        {
            if (cursor != null)
            {
                return;
            }

            // The input field is the scrolling content, so everything of ours lives on the part that stands still.
            var scroll = field.GetComponentInParent<ScrollRect>(true);
            if (scroll != null)
            {
                frame = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            }
            else
            {
                frame = (RectTransform)field.transform.parent;
            }


            gameText = field.GetComponent<CanvasGroup>();
            if (gameText == null)
            {
                gameText = field.gameObject.AddComponent<CanvasGroup>();
            }

            menu = new MenuView(terminal, frame, text, Mathf.Clamp(Settings.TextScale.Value, 0.6f, 1f));
            views[menu.Scale] = menu;

            // The credits box has a fixed size, so a large balance gets smaller letters.
            var credits = terminal.topRightText;
            if (credits != null)
            {
                credits.enableWordWrapping = false;
                credits.fontSizeMax = credits.fontSize;
                credits.fontSizeMin = credits.fontSize * 0.4f;
                credits.enableAutoSizing = true;

                var box = Ui.CreditsBox(credits);
                if (box != null)
                {
                    box.GetWorldCorners(Corners);
                    var boxRight = credits.rectTransform.InverseTransformPoint(Corners[2]).x;
                    var overrun = credits.rectTransform.rect.xMax - boxRight + credits.fontSize * 0.3f;
                    if (overrun > 0f)
                    {
                        var margin = credits.margin;
                        credits.margin = new Vector4(margin.x, margin.y, Mathf.Max(margin.z, overrun), margin.w);
                    }
                }
            }

            var size = text.fontSize;
            menuButton.Space = Ui.NewButton("ClickableTerminalMenu", "[ MENU ]", frame, text, size * 0.6f, 5.8f, HeaderScale);
            menuButton.Click = () =>
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
            backButton.Space = Ui.NewButton("ClickableTerminalBack", "[ BACK ]", frame, text, size * 5.35f, 5.8f, HeaderScale);
            backButton.Click = () =>
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

            smallerButton.Space = Ui.NewButton("ClickableTerminalSmaller", "-", frame, text, size * 11.85f, 1.9f, HeaderScale);
            smallerButton.Click = () => Resize(-0.05f);
            biggerButton.Space = Ui.NewButton("ClickableTerminalBigger", "+", frame, text, size * 10.1f, 1.9f, HeaderScale);
            biggerButton.Click = () => Resize(0.05f);

            continueButton.Space = Ui.NewButton("ClickableTerminalContinue", "[ CONTINUE ]", frame, text, 0f);
            continueButton.Space.anchorMin = continueButton.Space.anchorMax = continueButton.Space.pivot = new Vector2(0.5f, 0f);
            continueButton.Space.sizeDelta = new Vector2(size * 9f, size * 1.7f);
            continueButton.Space.anchoredPosition = new Vector2(0f, size * 3f);
            continueButton.Click = () => Navigator.Continue(terminal);
            continueLabel = continueButton.Space.GetComponentInChildren<TextMeshProUGUI>(true);

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
    }
}
