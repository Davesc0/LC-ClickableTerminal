using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ClickableTerminal
{
    // The game checks orders against a hard 12 and mods that raise it rewrite that number, so it is read from the check
    // itself.
    internal static class DropshipLimit
    {
        private const int Vanilla = 12;
        private static int? found;

        internal static int Value => found ?? (found = Read()).Value;

        private static int Read()
        {
            try
            {
                var check = AccessTools.Method(typeof(Terminal), nameof(Terminal.LoadNewNodeIfAffordable));
                var dropship = AccessTools.Field(typeof(Terminal), nameof(Terminal.numberOfItemsInDropship));
                var code = PatchProcessor.GetCurrentInstructions(check);

                // "... + numberOfItemsInDropship > 12"
                for (var i = 0; i < code.Count; i++)
                {
                    if (!Equals(code[i].operand, dropship))
                    {
                        continue;
                    }

                    for (var j = i + 1; j < code.Count && j <= i + 4; j++)
                    {
                        var limit = NumberIn(code[j]);
                        if (limit.HasValue && limit.Value > 0)
                        {
                            if (limit.Value != Vanilla)
                            {
                                Plugin.mlg.LogInfo($"The dropship takes {limit.Value} items here (a mod has changed it).");
                            }

                            return limit.Value;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"Could not read how many items the dropship takes, counting with {Vanilla}: {e.Message}");
            }

            return Vanilla;
        }

        // Written in, or read from a mod's setting (a static field or a getter).
        private static int? NumberIn(CodeInstruction instruction)
        {
            var op = instruction.opcode;
            var operand = instruction.operand;

            if (op == OpCodes.Ldc_R4 && operand is float single)
            {
                return (int)single;
            }

            if ((op == OpCodes.Ldc_I4 || op == OpCodes.Ldc_I4_S) && operand != null)
            {
                return Convert.ToInt32(operand);
            }

            if (op == OpCodes.Ldsfld && operand is FieldInfo field && field.IsStatic)
            {
                return AsNumber(field.GetValue(null));
            }

            if ((op == OpCodes.Call || op == OpCodes.Callvirt) && operand is MethodInfo method && method.IsStatic && method.GetParameters().Length == 0)
            {
                return AsNumber(method.Invoke(null, null));
            }

            return null;
        }

        private static int? AsNumber(object value)
        {
            if (value is int || value is float || value is double || value is long || value is short || value is byte)
            {
                return Convert.ToInt32(value);
            }

            var inner = value?.GetType().GetProperty("Value")?.GetValue(value);
            return inner != null && inner != value ? AsNumber(inner) : null;
        }
    }
}
