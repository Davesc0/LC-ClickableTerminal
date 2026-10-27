using UnityEngine;

namespace ClickableTerminal
{
    // What the mod does to the picture around the terminal: a sharper screen, the zoom, and the screen kept on.
    internal class ScreenTweaks
    {
        private readonly Terminal terminal;
        private readonly RectTransform frame;
        private RenderTexture sharp;
        private float zoom;
        private static readonly Vector3[] Corners = new Vector3[4];

        internal ScreenTweaks(Terminal terminal, RectTransform frame)
        {
            this.terminal = terminal;
            this.frame = frame;
        }

        internal void Opened()
        {
            zoom = 0f;
        }

        internal void LateUpdate(bool pageShown)
        {
            // The game fades its scrollbar in whenever its hidden page is scrolled.
            if (pageShown && terminal.scrollBarCanvasGroup != null)
            {
                terminal.scrollBarCanvasGroup.alpha = 0f;
            }

            var player = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
            var camera = player != null ? player.gameplayCamera : null;
            if (camera == null)
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

        // The game switches the screen and its light off when the player steps away. Here they stay on, only for this player.
        internal void KeepScreenOn()
        {
            if (!Settings.ScreenAlwaysOn.Value || terminal.terminalInUse || terminal.terminalUIScreen == null)
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

        // While the terminal is in use the game points the camera at its 970x580 texture every frame. This runs after that and hands it a bigger one. The game switches back by itself when the terminal closes.
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
                Release();

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

        internal void Release()
        {
            if (sharp != null)
            {
                sharp.Release();
                Object.Destroy(sharp);
                sharp = null;
            }
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
    }
}
