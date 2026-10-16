using System;
using UnityEngine;

namespace ClickableTerminal
{
    // A place on the screen that does something when clicked: a row, a button, an entry of another mod's screen.
    internal class HotSpot
    {
        internal RectTransform Space;
        internal Rect Area;
        internal Action Click;
        internal Action AltClick;
        internal Action WheelClick;

        // For screens another mod draws: what resting the cursor on the spot does, and what the wheel does over it.
        internal Action Hover;
        internal Action<bool> Scroll;
    }
}
