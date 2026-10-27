using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ClickableTerminal
{
    [BepInPlugin(Plugin.modGUID, Plugin.modName, Plugin.modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string modGUID = "dev.davesco.ClickableTerminal";
        public const string modName = "ClickableTerminal";
        public const string modVersion = "0.2.0";
        internal static readonly Harmony _harmony = new Harmony(modGUID);
        internal static ManualLogSource mlg = BepInEx.Logging.Logger.CreateLogSource(modGUID);

        void Awake()
        {
            Settings.Init(Config);

            // A category edited while the game runs takes effect on the next store visit.
            Config.SettingChanged += (sender, args) => ItemCategories.ClearCache();

            mlg.LogInfo($"Plugin {modName} is loaded!");
            _harmony.PatchAll();
        }

        private static readonly System.Collections.Generic.HashSet<string> Warned = new System.Collections.Generic.HashSet<string>();

        internal static void WarnOnce(string message)
        {
            if (Warned.Add(message))
            {
                mlg.LogWarning(message);
            }
        }

        internal static void Debug(string message)
        {
            if (Settings.DebugLog != null && Settings.DebugLog.Value)
            {
                mlg.LogInfo("[Debug] " + message);
            }
        }
    }
}
