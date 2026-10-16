using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameNetcodeStuff;
using HarmonyLib;

namespace ClickableTerminal
{
    // How hard a creature hits. The game keeps no such number anywhere: every creature's script calls
    // DamagePlayer or KillPlayer with its own figures. So the script itself is read, once per creature,
    // for those calls and the damage written into them.
    internal static class EnemyDamage
    {
        private static readonly Dictionary<Type, string> Cache = new Dictionary<Type, string>();

        // "20", "20 / 40", "Kills outright", "40, can kill outright", or null when the script shows nothing.
        internal static string Of(EnemyType enemy)
        {
            var script = enemy != null && enemy.enemyPrefab != null ? enemy.enemyPrefab.GetComponent<EnemyAI>() : null;
            if (script == null)
            {
                return null;
            }

            var type = script.GetType();
            if (!Cache.TryGetValue(type, out var text))
            {
                try
                {
                    text = Read(type);
                }
                catch (Exception)
                {
                    text = null;
                }

                Cache[type] = text;
            }

            return text;
        }

        private static string Read(Type type)
        {
            var amounts = new SortedSet<int>();
            var varies = false;
            var kills = false;

            foreach (var method in MethodsOf(type))
            {
                List<CodeInstruction> code;
                try
                {
                    code = PatchProcessor.GetOriginalInstructions(method);
                }
                catch (Exception)
                {
                    continue;
                }

                for (var i = 0; i < code.Count; i++)
                {
                    if (!(code[i].operand is MethodInfo called) || called.DeclaringType != typeof(PlayerControllerB))
                    {
                        continue;
                    }

                    if (called.Name.StartsWith("KillPlayer"))
                    {
                        kills = true;
                    }
                    else if (called.Name == "DamagePlayer")
                    {
                        var amount = FirstArgument(code, i, called);
                        if (amount.HasValue && amount.Value > 0)
                        {
                            amounts.Add(amount.Value);
                        }
                        else
                        {
                            varies = true;
                        }
                    }
                }
            }

            var damage = amounts.Count > 0 ? string.Join(" / ", amounts) : varies ? "Varies" : null;
            if (kills)
            {
                return damage != null ? damage + ", can kill outright" : "Kills outright";
            }

            return damage;
        }

        // The creature's own code, down to but not including the class all creatures share,
        // with the hidden classes the compiler makes for its coroutines.
        private static IEnumerable<MethodBase> MethodsOf(Type type)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                     | BindingFlags.Static | BindingFlags.DeclaredOnly;

            for (var current = type; current != null && current != typeof(EnemyAI); current = current.BaseType)
            {
                foreach (var method in current.GetMethods(all))
                {
                    if (method.GetMethodBody() != null)
                    {
                        yield return method;
                    }
                }

                foreach (var nested in current.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
                {
                    foreach (var method in nested.GetMethods(all))
                    {
                        if (method.GetMethodBody() != null)
                        {
                            yield return method;
                        }
                    }
                }
            }
        }

        // Walks back from the call over everything that was pushed for it, to the first argument.
        // A plain number there is the damage. Anything else (a field, a calculation) is not read.
        private static int? FirstArgument(List<CodeInstruction> code, int call, MethodInfo called)
        {
            var needed = called.GetParameters().Length;

            for (var i = call - 1; i >= 0 && call - i < 40; i--)
            {
                var instruction = code[i];
                if (instruction.opcode.FlowControl == FlowControl.Branch || instruction.opcode.FlowControl == FlowControl.Cond_Branch)
                {
                    return null;
                }

                needed -= Pushes(instruction);
                if (needed <= 0)
                {
                    return Constant(instruction);
                }

                needed += Pops(instruction);
            }

            return null;
        }

        private static int Pushes(CodeInstruction instruction)
        {
            switch (instruction.opcode.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                    return 0;
                case StackBehaviour.Push1_push1:
                    return 2;
                case StackBehaviour.Varpush:
                    return instruction.operand is MethodInfo method && method.ReturnType == typeof(void) ? 0 : 1;
                default:
                    return 1;
            }
        }

        private static int Pops(CodeInstruction instruction)
        {
            switch (instruction.opcode.StackBehaviourPop)
            {
                case StackBehaviour.Pop0:
                    return 0;
                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                    return 1;
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    return 2;
                case StackBehaviour.Varpop:
                    if (instruction.operand is MethodBase method)
                    {
                        var instance = !method.IsStatic && instruction.opcode != OpCodes.Newobj ? 1 : 0;
                        return method.GetParameters().Length + instance;
                    }

                    return 0;
                default:
                    return 3;
            }
        }

        private static int? Constant(CodeInstruction instruction)
        {
            var code = instruction.opcode;

            if (code == OpCodes.Ldc_I4 || code == OpCodes.Ldc_I4_S)
            {
                return Convert.ToInt32(instruction.operand);
            }

            if (code == OpCodes.Ldc_I4_0) return 0;
            if (code == OpCodes.Ldc_I4_1) return 1;
            if (code == OpCodes.Ldc_I4_2) return 2;
            if (code == OpCodes.Ldc_I4_3) return 3;
            if (code == OpCodes.Ldc_I4_4) return 4;
            if (code == OpCodes.Ldc_I4_5) return 5;
            if (code == OpCodes.Ldc_I4_6) return 6;
            if (code == OpCodes.Ldc_I4_7) return 7;
            if (code == OpCodes.Ldc_I4_8) return 8;

            return null;
        }
    }
}
