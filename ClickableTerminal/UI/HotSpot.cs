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
    }
}
