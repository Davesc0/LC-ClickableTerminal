using UnityEngine;

namespace ClickableTerminal
{
    internal static class CursorArt
    {
        // X is the dark outline, o is the fill that takes the terminal's text colour.
        private static readonly string[] Rows =
        {
            "X           ",
            "XX          ",
            "XoX         ",
            "XooX        ",
            "XoooX       ",
            "XooooX      ",
            "XoooooX     ",
            "XooooooX    ",
            "XoooooooX   ",
            "XooooooooX  ",
            "XoooooooooX ",
            "XooooooXXXXX",
            "XoooXooX    ",
            "XooX XooX   ",
            "XoX  XooX   ",
            "XX    XooX  ",
            "X     XooX  ",
            "       XX   "
        };

        // An open hand, for "this takes one hand" and "this takes two".
        private static readonly string[] HandRows =
        {
            "   o o   ",
            " o o o   ",
            " o o o o ",
            " o o o o ",
            " ooooooo ",
            "oooooooo ",
            "ooooooooo",
            " oooooooo",
            " ooooooo ",
            "  ooooo  ",
            "  ooooo  "
        };

        // A map pin: where the ship is now.
        private static readonly string[] PinRows =
        {
            "  ooooo  ",
            " ooooooo ",
            "ooo   ooo",
            "ooo   ooo",
            "ooo   ooo",
            " ooooooo ",
            "  ooooo  ",
            "   ooo   ",
            "   ooo   ",
            "    o    ",
            "    o    "
        };

        private static readonly string[] LockRows =
        {
            "  ooooo  ",
            " oo   oo ",
            " oo   oo ",
            " oo   oo ",
            "ooooooooo",
            "ooooooooo",
            "oooo oooo",
            "oooo oooo",
            "ooooooooo",
            "ooooooooo",
            "ooooooooo"
        };

        // A pouch: the thing fits the utility slot.
        private static readonly string[] PouchRows =
        {
            "   ooo   ",
            "  o   o  ",
            "  o   o  ",
            "ooooooooo",
            "ooooooooo",
            "ooo   ooo",
            "ooo   ooo",
            "ooooooooo",
            "ooooooooo",
            "ooooooooo",
            " ooooooo "
        };

        private static Texture2D pouch;

        internal static Texture2D Pouch()
        {
            return pouch != null ? pouch : pouch = Draw(PouchRows);
        }

        private static Texture2D arrow;
        private static Texture2D hand;
        private static Texture2D pin;
        private static Texture2D padlock;

        internal static Texture2D Pin()
        {
            return pin != null ? pin : pin = Draw(PinRows);
        }

        internal static Texture2D Padlock()
        {
            return padlock != null ? padlock : padlock = Draw(LockRows);
        }

        internal static Texture2D Arrow()
        {
            return arrow != null ? arrow : arrow = Draw(Rows);
        }

        internal static Texture2D Hand()
        {
            return hand != null ? hand : hand = Draw(HandRows);
        }

        private static Texture2D Draw(string[] rows)
        {
            // Each drawn pixel becomes a block of 8x8 with an empty rim around the picture, and the texture is
            // sampled smoothly. Drawn one to one with hard sampling, the icons lost or grew a pixel
            // depending on where exactly they landed on the screen.
            const int scale = 8;
            var columns = rows[0].Length;
            var lines = rows.Length;
            var width = (columns + 2) * scale;
            var height = (lines + 2) * scale;

            // The empty part carries the colour of what it borders, so the edges do not get a dark or a light fringe.
            var outlined = System.Array.Exists(rows, row => row.IndexOf('X') >= 0);
            var empty = outlined ? new Color(0f, 0f, 0f, 0f) : new Color(1f, 1f, 1f, 0f);

            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = empty;
            }

            for (var line = 0; line < lines; line++)
            {
                // Texture rows start at the bottom.
                var row = rows[lines - 1 - line];
                for (var column = 0; column < columns; column++)
                {
                    if (row[column] != 'X' && row[column] != 'o')
                    {
                        continue;
                    }

                    var colour = row[column] == 'X' ? Color.black : Color.white;
                    for (var y = 0; y < scale; y++)
                    {
                        for (var x = 0; x < scale; x++)
                        {
                            pixels[((line + 1) * scale + y) * width + (column + 1) * scale + x] = colour;
                        }
                    }
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }
    }
}
