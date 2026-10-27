using System;
using UnityEngine;

namespace ClickableTerminal
{
    internal class HotSpot
    {
        internal RectTransform Space;
        internal Rect Area;
        internal Action Click;
        internal Action AltClick;

        internal Action Hover;
        internal Action<bool> Scroll;

        // A scrollbar: held down, the point under the cursor is handed over every frame.
        internal Action<Vector3> Drag;

        // Called with true when the scrollbar is taken hold of and with false when it is let go.
        internal Action<bool> Holding;

    }
}
