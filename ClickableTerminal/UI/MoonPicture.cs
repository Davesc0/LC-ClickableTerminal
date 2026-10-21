using UnityEngine;
using UnityEngine.Video;

namespace ClickableTerminal
{
    // One video player, so one video at a time.
    internal static class MoonPicture
    {
        private static VideoPlayer player;
        private static RenderTexture picture;

        internal static Texture For(SelectableLevel level)
        {
            return For(level != null ? level.videoReel : null);
        }

        internal static Texture For(VideoClip clip)
        {
            if (clip == null)
            {
                return null;
            }

            if (player == null)
            {
                var holder = new GameObject("ClickableTerminalMoonPicture");
                Object.DontDestroyOnLoad(holder);
                player = holder.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.isLooping = true;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.renderMode = VideoRenderMode.RenderTexture;
            }

            if (picture == null)
            {
                picture = new RenderTexture(256, 256, 0) { name = "ClickableTerminalMoonPicture" };
                picture.Create();
                player.targetTexture = picture;
            }

            if (player.clip != clip)
            {
                player.clip = clip;
                player.Play();
            }
            else if (!player.isPlaying)
            {
                player.Play();
            }

            return picture;
        }
    }
}
