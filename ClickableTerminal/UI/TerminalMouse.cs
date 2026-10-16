using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace ClickableTerminal
{
    // Sits on the terminal. Moves a cursor over its screen and turns clicks into typed commands.
    // Shows either one of our own menu pages or the game's text with its words made clickable.
    internal class TerminalMouse : MonoBehaviour
    {
        private const float HideAfter = 5f;

        internal static bool InUse;

        // The wheel already does something of ours this frame: our page is on screen, or an application's counter or page.
        internal static bool WheelTaken;

        private static TerminalMouse active;

        // How far the scrollbar moves for one line of the text on screen, 0 when it all fits.
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

        // What the click, the right click and the wheel click are called on the controller in use. Null with a mouse.
        internal static string[] PadNames;

        private static readonly string[] Xbox = { "A", "X", "Y" };
        private static readonly string[] PlayStation = { "Cross", "Square", "Triangle" };
        private static readonly string[] Nintendo = { "B", "Y", "X" };

        // With a controller the mouse's words become the buttons: InputUtils' pictures when it is installed, names otherwise.
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

        // The button that does BACK or MENU, in front of the word, while a controller is in use.
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

        private Terminal terminal;
        private RectTransform frame;
        private RectTransform cursor;
        private RawImage cursorImage;
        private RectTransform hover;
        private Image hoverImage;
        private CanvasGroup gameText;
        private TextMeshProUGUI continueLabel;
        private MenuView menu;
        private readonly InteractiveMouse apps = new InteractiveMouse();

        private readonly HotSpot backButton = new HotSpot();
        private readonly HotSpot menuButton = new HotSpot();
        private readonly HotSpot continueButton = new HotSpot();
        private readonly HotSpot smallerButton = new HotSpot();
        private readonly HotSpot biggerButton = new HotSpot();
        private int refocusIn;
        private float lastMoved = -100f;
        private bool pageShown;
        private float zoom;
        private float lastScrolled;
        private float heldScroll;
        private bool wheelWasOurs;
        private RenderTexture sharp;
        private static readonly Vector3[] Corners = new Vector3[4];
        private readonly List<Vector2> targets = new List<Vector2>();
        private Vector2 heldStep;
        private float stepAt;
        private readonly float[] padPressed = new float[5];

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

        private void Update()
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

            // A screen drawn with the Interactive Terminal API gets the mouse on top of its own keys.
            // Any other mod that has taken the input away is left alone.
            var app = InteractiveTerminal.Running();
            var foreign = !app && !field.interactable;
            var view = Navigator.Current;
            var page = !foreign && !app && view != null ? view.Page : null;

            if (app)
            {
                apps.Tick(field, text);

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

            if (menu.Scale != Mathf.Clamp(Settings.TextScale.Value, 0.6f, 1f))
            {
                menu.Dispose();
                menu = new MenuView(terminal, frame, text);
            }

            pageShown = page != null;
            gameText.alpha = pageShown ? 0f : 1f;
            menu.Show(page);

            if (pageShown)
            {
                menu.SetTyped(Commands.Typed(terminal));
                menu.Tick();

                // A free spinning wheel sends a step every frame. The list moves at most twenty times a second.
                var wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && Time.unscaledTime - lastScrolled > 0.05f)
                {
                    lastScrolled = Time.unscaledTime;
                    menu.Scroll(wheel > 0f ? -2 : 2);
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

            var visible = !foreign && Time.unscaledTime - lastMoved < HideAfter;


            var label = !foreign && !app && view != null ? view.ButtonLabel : null;
            var welcome = label != null && view.ContinueTo != null;

            // The button sits over the bottom of the screen. On a long page it would cover the text, and BACK is there anyway.
            if (label != null && !welcome && text.preferredHeight > frame.rect.height - text.fontSize * 5f)
            {
                label = null;
            }
            // In an application BACK is its exit key and MENU closes it.
            Ui.SetActive(backButton.Space, app || (!foreign && Navigator.CanGoBack));
            Ui.SetActive(menuButton.Space, app || (!foreign && view != null && !view.IsHome && !welcome));
            Ui.SetActive(continueButton.Space, label != null);

            // The size of the letters on our pages, next to BACK and MENU. The game's own text pages keep theirs.
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
                : apps.SpotAt(cursor.position);

            // The application highlights the entry its cursor is on, so moving its cursor there is the highlight.
            if (app && hit != null && hit.Hover != null)
            {
                hit.Hover();
            }

            ShowHover(app && hit != backButton && hit != menuButton ? null : hit, text.color);

            // Over an amount the wheel changes it, on a screen with pages it turns the page.
            // Where it does neither it is left to the game, to scroll a screen that is longer than the monitor.
            var running = app ? InteractiveTerminal.Application() : null;
            var wheelIsOurs = app && ((hit != null && hit.Scroll != null) || InteractiveTerminal.HasPages(running));

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
            Ui.SetActive(cursor, visible);

            // On a controller: A clicks, X is the right click, Y the wheel click, B goes back, Select opens the menu.
            var press = mouse.leftButton.wasPressedThisFrame || enter || PadPressed(0, pad => pad.buttonSouth);
            var other = mouse.rightButton.wasPressedThisFrame || PadPressed(1, pad => pad.buttonWest);
            var third = mouse.middleButton.wasPressedThisFrame || PadPressed(2, pad => pad.buttonNorth);

            if (hit != null && hit.WheelClick != null && third)
            {
                refocusIn = 2;
                PlayClick();
                hit.WheelClick();
            }

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

        // The game switches the screen and its light off when the player steps away. Here they stay on, only for this player.
        // Written to the config, so it stays. The page is drawn again at the new size on the next frame.
        private static void Resize(float by)
        {
            var scale = Settings.TextScale;
            scale.Value = Mathf.Clamp(Mathf.Round((scale.Value + by) * 20f) / 20f, 0.6f, 1f);
        }

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

            // The game fades its scrollbar in whenever its own hidden page is scrolled.
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
                // The game pulls the field of view back to its own value every frame, this runs after it.
                zoom = Mathf.Lerp(zoom > 0f ? zoom : camera.fieldOfView, FovThatFits(camera), 6f * Time.deltaTime);
                camera.fieldOfView = zoom;
            }

        }

        // While the terminal is in use the game points the camera at its own "high res" texture every frame (970x580).
        // This runs after that and hands it a bigger one of the same kind. The game's texture is left as it is,
        // and the moment the terminal is closed the game goes back to its normal picture by itself.
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

        // The field of view at which the terminal screen takes up most of the picture.
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

            // Half a screen of mouse travel crosses the whole terminal.
            var bounds = frame.rect;
            var scale = bounds.width / (Screen.width * 0.5f) * Settings.CursorSpeed.Value;
            var position = (Vector2)cursor.localPosition + delta * scale;

            position.x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
            position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
            cursor.localPosition = position;
        }

        // The same cursor without a mouse. The left stick moves it freely, the right one scrolls,
        // the d-pad and the arrow keys put it on the next thing that can be clicked.
        // An application has its own use for the arrows, so there only the stick is ours.
        private bool Steer(Keyboard keys, bool app)
        {
            var now = Time.unscaledTime;

            if (Gamepad.all.Count > 0)
            {
                var stick = PadStick(pad => pad.leftStick);
                if (stick.sqrMagnitude > 0.04f)
                {
                    var bounds = frame.rect;
                    var position = (Vector2)cursor.localPosition + stick * (bounds.width * 0.9f * Settings.CursorSpeed.Value * Time.unscaledDeltaTime);
                    position.x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
                    position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
                    cursor.localPosition = position;
                    lastMoved = now;
                }

                var turn = PadStick(pad => pad.rightStick).y;
                if (Mathf.Abs(turn) > 0.3f)
                {
                    if (pageShown && !app && now - lastScrolled > 0.07f)
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

            // The arrow keys are the application's own while one is running, the d-pad it knows nothing about.
            var want = Vector2.zero;
            if (PadHeld(pad => pad.dpad.up) || (!app && keys != null && keys.upArrowKey.isPressed))
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

        // One controller often shows up as two devices (its own and the one Steam makes of it), and the same press
        // then arrives twice, a frame apart. So every device is looked at, and a button counts once in a while.
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
            if (app)
            {
                // Left and right turn the application's pages, up and down walk its entries.
                var running = InteractiveTerminal.Application();
                if (direction.x != 0f)
                {
                    // On an amount they change it, the way the wheel does with a mouse.
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
            }
            else if (pageShown && direction.y != 0f && menu.ScrollFrom(cursor.position, direction.y < 0f))
            {
                return;
            }

            targets.Clear();
            AddTarget(backButton);
            AddTarget(menuButton);
            AddTarget(continueButton);
            AddTarget(smallerButton);
            AddTarget(biggerButton);

            if (app)
            {
                apps.Targets(frame, targets);
            }
            else if (pageShown)
            {
                menu.Targets(frame, targets);
            }

            // The screen is taken a line at a time. Up and down go to the nearest line that has something to click
            // and pick what is closest on it, left and right stay on the line the cursor is on.
            var from = (Vector2)cursor.localPosition;
            var side = new Vector2(direction.y, direction.x);
            var sameLine = terminal.screenText.textComponent.fontSize * 0.6f;
            var vertical = direction.y != 0f;
            var nearest = float.MaxValue;

            if (vertical)
            {
                foreach (var point in targets)
                {
                    var along = Vector2.Dot(point - from, direction);
                    if (along > sameLine && along < nearest)
                    {
                        nearest = along;
                    }
                }
            }

            var best = float.MaxValue;
            var found = false;
            var to = from;

            foreach (var point in targets)
            {
                var offset = point - from;
                var along = Vector2.Dot(offset, direction);
                var across = Mathf.Abs(Vector2.Dot(offset, side));

                var fits = vertical
                    ? along > sameLine && along <= nearest + sameLine
                    : along > 1f && across <= sameLine;

                var score = vertical ? across : along;
                if (fits && score < best)
                {
                    best = score;
                    to = point;
                    found = true;
                }
            }

            if (found)
            {
                cursor.localPosition = to;
            }
            else if (pageShown && direction.y != 0f)
            {
                menu.Scroll(direction.y < 0f ? 1 : -1);
            }
            else if (pageShown && !app && menu.Jump(cursor.position, direction.x > 0f, frame, out var stand))
            {
                cursor.localPosition = stand;
            }
        }

        private void AddTarget(HotSpot button)
        {
            if (button.Space != null && button.Space.gameObject.activeSelf)
            {
                targets.Add(frame.InverseTransformPoint(button.Space.TransformPoint(button.Space.rect.center)));
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

            // The highlight stays where it is in the hierarchy and is only moved over whatever is under the cursor.
            // Handing it from parent to parent made the whole screen rebuild every time.
            var min = frame.InverseTransformPoint(spot.Space.TransformPoint(spot.Area.min));
            var max = frame.InverseTransformPoint(spot.Space.TransformPoint(spot.Area.max));
            Ui.Place(hover, Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            hoverImage.color = new Color(color.r, color.g, color.b, 0.3f);
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

            // Hides the game's text under our pages while the input field keeps taking keys.
            gameText = field.GetComponent<CanvasGroup>();
            if (gameText == null)
            {
                gameText = field.gameObject.AddComponent<CanvasGroup>();
            }

            menu = new MenuView(terminal, frame, text);

            // The credits sit in a box of a fixed size. A large balance gets smaller letters instead of running out of it.
            var credits = terminal.topRightText;
            if (credits != null)
            {
                credits.enableWordWrapping = false;
                credits.fontSizeMax = credits.fontSize;
                credits.fontSizeMin = credits.fontSize * 0.4f;
                credits.enableAutoSizing = true;

                // The text may run on past the box, so its right edge is pulled in to where the box ends.
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
            menuButton.Space = Ui.NewButton("ClickableTerminalMenu", "[ MENU ]", frame, text, size);
            menuButton.Click = () =>
            {
                // Closing an application puts the terminal back on the page it was opened from, the main menu.
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
            backButton.Space = Ui.NewButton("ClickableTerminalBack", "[ BACK ]", frame, text, size * 7f);
            backButton.Click = () =>
            {
                if (!InteractiveTerminal.Running())
                {
                    Navigator.Back(terminal);
                }
                else if (!InteractiveTerminal.RunExitHandlers() && !InteractiveTerminal.PressExitKey())
                {
                    InteractiveTerminal.Exit(InteractiveTerminal.Application());
                }
            };

            smallerButton.Space = Ui.NewButton("ClickableTerminalSmaller", "-", frame, text, size * 15.2f, 1.8f);
            smallerButton.Click = () => Resize(-0.05f);
            biggerButton.Space = Ui.NewButton("ClickableTerminalBigger", "+", frame, text, size * 13f, 1.8f);
            biggerButton.Click = () => Resize(0.05f);

            // Under pages that only wait for the player, like the welcome text.
            continueButton.Space = Ui.NewButton("ClickableTerminalContinue", "[ CONTINUE ]", frame, text, 0f);
            continueButton.Space.anchorMin = continueButton.Space.anchorMax = continueButton.Space.pivot = new Vector2(0.5f, 0f);
            continueButton.Space.sizeDelta = new Vector2(size * 9f, size * 1.7f);
            continueButton.Space.anchoredPosition = new Vector2(0f, size * 3f);
            continueButton.Click = () => Navigator.Continue(terminal);
            continueLabel = continueButton.Space.GetComponentInChildren<TextMeshProUGUI>(true);

            hoverImage = Ui.NewBox("ClickableTerminalHover", frame);
            hover = hoverImage.rectTransform;
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
