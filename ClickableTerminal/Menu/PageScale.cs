using System.Collections.Generic;
using UnityEngine;

namespace ClickableTerminal
{
    internal static class PageScale
    {
        private static readonly Dictionary<string, float> Chosen = new Dictionary<string, float>();

        internal static float For(MenuPage page)
        {
            if (!Chosen.TryGetValue(page.Title ?? "", out var scale))
            {
                scale = page.Scale > 0f ? page.Scale : Settings.TextScale.Value;
            }

            return Mathf.Clamp(scale, page.ScaleMin, page.ScaleMax);
        }

        internal static void Change(MenuPage page, float by)
        {
            if (page != null)
            {
                Chosen[page.Title ?? ""] = Mathf.Clamp(Mathf.Round((For(page) + by) * 20f) / 20f, page.ScaleMin, page.ScaleMax);
            }
        }
    }
}
