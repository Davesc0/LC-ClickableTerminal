using System;
using System.Collections.Generic;

namespace ClickableTerminal
{
    // HarmonyLib's TypeByName walks every type of every mod when a name is missing, which takes seconds in a big
    // modpack. Asking each assembly directly is quick.
    internal static class Reflect
    {
        private static readonly Dictionary<string, Type> Known = new Dictionary<string, Type>();

        internal static Type Find(string name)
        {
            if (Known.TryGetValue(name, out var type))
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.IsDynamic ? null : assembly.GetType(name, false);
                }
                catch (Exception)
                {
                    type = null;
                }

                if (type != null)
                {
                    break;
                }
            }

            Known[name] = type;
            return type;
        }
    }
}
