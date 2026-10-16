using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ClickableTerminal
{
    // Looking a type up by name walks every type of every mod, which takes a noticeable moment in a big modpack.
    // Each name is looked up once, found or not.
    internal static class Reflect
    {
        private static readonly Dictionary<string, Type> Known = new Dictionary<string, Type>();

        internal static Type Find(string name)
        {
            if (!Known.TryGetValue(name, out var type))
            {
                Known[name] = type = AccessTools.TypeByName(name);
            }

            return type;
        }
    }
}
