using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ClickableTerminal
{
    // The way Terminal Restyle does it: vanilla item class first, then words in the names, then the script's field
    // names.
    internal static class ItemCategories
    {
        internal const string Other = "OTHER";

        internal const int Slots = 15;

        // The rules that look at an item's code are tied to the slot, so a renamed category keeps them and an emptied
        // one drops them.
        private static readonly string[] Builtin =
        {
            "LIGHTING", "MELEE", "FIREARMS", "TOOL GUNS", "AMMO", "TACTICAL", "HAZARD CONTROL",
            "MOBILITY", "MEDICAL", "STORAGE", "COMMS", "SENSORS", "UTILITY"
        };

        private static readonly (string Category, Type[] Types)[] TypeMap =
        {
            ("LIGHTING", new[] { typeof(FlashlightItem) }),
            ("MELEE", new[] { typeof(Shovel), typeof(KnifeItem) }),
            ("FIREARMS", new[] { typeof(ShotgunItem) }),
            ("TOOL GUNS", new[] { typeof(PatcherTool) }),
            ("AMMO", new[] { typeof(GunAmmo) }),
            ("TACTICAL", new[] { typeof(StunGrenadeItem) }),
            ("MOBILITY", new[] { typeof(JetpackItem), typeof(TetraChemicalItem) }),
            ("STORAGE", new[] { typeof(BeltBagItem) }),
            ("COMMS", new[] { typeof(WalkieTalkie), typeof(BoomboxItem) }),
            ("SENSORS", new[] { typeof(RadarBoosterItem), typeof(BinocularsItem) }),
            ("UTILITY", new[] { typeof(LockPicker), typeof(SprayPaintItem), typeof(ExtensionLadderItem), typeof(RemoteProp) }),
        };

        // Substring matches, so short words are left out: "bat" would claim Battery.
        private static readonly (string Category, string[] Words)[] DefaultKeywords =
        {
            ("LIGHTING", new[] { "flashlight", "handlamp", "lamp", "lantern", "glowstick", "candle", "headlight", "floodlight", "spotlight", "lightbulb", "luminous", "light" }),
            ("MELEE", new[] { "sledgehammer", "sledge", "hammer", "sword", "axe", "blade", "knife", "machete", "club", "crowbar", "rapier", "spear", "scythe", "katana", "shovel", "cleaver", "dagger", "mace", "wrench", "chainsaw", "sickle", "halberd", "baseballbat", "nightstick", "tomahawk", "hatchet", "kukri", "glaive", "saber", "knuckle", "melee" }),
            ("FIREARMS", new[] { "rocketlauncher", "assaultrifle", "boltaction", "rifle", "pistol", "revolver", "shotgun", "launcher", "carbine", "sniper", "musket", "blaster", "cannon", "bazooka", "crossbow", "flamethrower", "minigun", "magnum", "handgun", "firearm", "gun" }),
            ("TOOL GUNS", new[] { "zappinator", "flaregun", "zapgun", "tazergun", "tasergun", "nailgun", "gluegun", "watergun", "tranquilizer", "blowtorch", "welder", "taser", "tazer" }),
            ("AMMO", new[] { "shotgunshell", "caliberround", "fuelcanister", "fueltank", "cartridge", "buckshot", "magazine", "missile", "rocket", "caliber", "bullet", "flare", "shell", "slug", "fuel", "ammo" }),
            ("TACTICAL", new[] { "stungrenade", "flashbang", "smokebomb", "grenade", "explosive", "dynamite", "molotov", "decoy", "landmine", "bomb" }),
            ("HAZARD CONTROL", new[] { "weedkiller", "insecticide", "pesticide", "herbicide", "repellent", "deterrent", "exterminat", "snaptrap", "gluetrap", "rattrap", "catfood", "poison", "begone", "deroot", "reaper", "burner", "antidote", "bait", "trap" }),
            ("MOBILITY", new[] { "autoinjector", "adrenaline", "parachute", "hookshot", "jetwings", "stimulant", "jetpack", "grapple", "zipline", "inhalant", "injector", "glider", "boots", "tzp" }),
            ("MEDICAL", new[] { "defibrillator", "medicalkit", "painkiller", "antiseptic", "healthpack", "stimpack", "firstaid", "bandage", "medical", "medkit", "syringe", "gauze", "morphine", "epinephrine", "antibiotic" }),
            ("STORAGE", new[] { "utilitybelt", "storingunit", "storageunit", "scrapsack", "backpack", "beltbag", "container", "cupboard", "satchel", "storage", "locker", "pouch", "crate", "chest", "sack" }),
            ("COMMS", new[] { "tuningcylinder", "talkiewalker", "walkietalkie", "transmitter", "microphone", "megaphone", "intercom", "boombox", "receiver", "headset", "jukebox", "speaker", "walkie", "pinger", "radio" }),
            ("SENSORS", new[] { "strangedevice", "radarbooster", "remoteradar", "nightvision", "binocular", "detector", "recorder", "monocle", "scanpad", "scanner", "compass", "thermal", "geiger", "camera", "sonar", "radar", "meter", "scan" }),
            ("UTILITY", new[] { "instructionmanual", "extensionladder", "utilitypylon", "hackingtool", "screwdriver", "spraypaint", "ducttape", "lockpick", "keycard", "toolbox", "manual", "pylon", "ladder", "remote", "paint" }),
        };

        private static readonly (string Category, string[] Words)[] MemberSignatures =
        {
            ("FIREARMS", new[] { "magazine", "reload", "muzzle", "recoil", "barrel", "bullet", "firerate", "firetime", "fireclip", "firemode", "safety", "chamber" }),
            ("MELEE", new[] { "reelingup", "hitforce", "objectshitby", "swing", "slash", "stab", "melee" }),
            ("LIGHTING", new[] { "bulb", "lamp", "flashlight", "lumin", "glow" }),
            ("AMMO", new[] { "ammotype", "shellsloaded" }),
            ("MOBILITY", new[] { "teleport", "savedlocation", "warppoint" }),
        };

        private static readonly Dictionary<Item, string> Cache = new Dictionary<Item, string>();

        private static List<(string Category, string[] Words)> keywords;
        private static Dictionary<string, string> named;
        private static string[] names;

        internal static void ClearCache()
        {
            Cache.Clear();
            keywords = null;
            named = null;
            names = null;
        }

        internal static string DefaultName(int slot)
        {
            return slot < Builtin.Length ? Builtin[slot] : "";
        }

        internal static string DefaultWords(int slot)
        {
            var category = DefaultName(slot);
            return string.Join(", ", DefaultKeywords.FirstOrDefault(entry => entry.Category == category).Words ?? new string[0]);
        }

        internal static IEnumerable<string> Order()
        {
            ReadConfig();
            return names.Where(name => name.Length > 0).Distinct().Concat(new[] { Other });
        }

        private static string Named(string builtin)
        {
            if (builtin == null)
            {
                return null;
            }

            var slot = Array.IndexOf(Builtin, builtin);
            return slot >= 0 && names[slot].Length > 0 ? names[slot] : null;
        }

        // Neither the name nor the code says what a "Karma-1" is.
        internal static string DefaultItems(int slot)
        {
            switch (DefaultName(slot))
            {
                case "FIREARMS":
                    return "Karma-1";
                case "MOBILITY":
                    return "Hoverboard";
                case "STORAGE":
                    return "Storing Unit";
                case "UTILITY":
                    return "Ducky Tube";
                default:
                    return "";
            }
        }

        private static void ReadConfig()
        {
            if (keywords != null)
            {
                return;
            }

            keywords = new List<(string Category, string[] Words)>();
            named = new Dictionary<string, string>();
            names = new string[Slots];

            for (var slot = 0; slot < Slots; slot++)
            {
                var category = (Settings.CategoryName[slot]?.Value ?? "").Trim().ToUpperInvariant();
                names[slot] = category;
                if (category.Length == 0)
                {
                    continue;
                }

                keywords.Add((category, (Settings.CategoryKeywords[slot]?.Value ?? "").Split(',').Select(Squash).Where(word => word.Length > 0).ToArray()));

                foreach (var name in (Settings.CategoryItems[slot]?.Value ?? "").Split(',').Select(Squash).Where(name => name.Length > 0))
                {
                    named[name] = category;
                }
            }
        }

        internal static string Of(Item item)
        {
            if (item == null)
            {
                return Other;
            }

            if (!Cache.TryGetValue(item, out var category))
            {
                Cache[item] = category = Resolve(item);
            }

            return category;
        }

        private static string Resolve(Item item)
        {
            ReadConfig();

            if (named.TryGetValue(Squash(item.itemName), out var forced))
            {
                return forced;
            }

            GrabbableObject script = null;
            try
            {
                script = item.spawnPrefab != null ? item.spawnPrefab.GetComponent<GrabbableObject>() : null;
            }
            catch (Exception)
            {
            }

            return Named(ExactType(script))
                   ?? KeywordMatch(item.itemName)
                   ?? KeywordMatch(script != null ? script.GetType().Name : null)
                   ?? Named(Signature(script))
                   ?? Named(InheritedType(script))
                   ?? Other;
        }

        private static string ExactType(GrabbableObject script)
        {
            if (script == null)
            {
                return null;
            }

            // The weed killer is a spray paint can with a flag set.
            if (script is SprayPaintItem spray && spray.isWeedKillerSprayBottle)
            {
                return "HAZARD CONTROL";
            }

            var type = script.GetType();
            return TypeMap.FirstOrDefault(entry => entry.Types.Contains(type)).Category;
        }

        private static string InheritedType(GrabbableObject script)
        {
            return script == null
                ? null
                : TypeMap.FirstOrDefault(entry => entry.Types.Any(type => type.IsInstanceOfType(script))).Category;
        }

        // "flaregun" beats "flare" and "gun".
        private static string KeywordMatch(string text)
        {
            var haystack = Squash(text);
            if (haystack.Length == 0)
            {
                return null;
            }

            string best = null;
            var bestLength = 0;

            foreach (var entry in keywords)
            {
                foreach (var word in entry.Words)
                {
                    if (word.Length > bestLength && haystack.Contains(word))
                    {
                        bestLength = word.Length;
                        best = entry.Category;
                    }
                }
            }

            return best;
        }

        // Magazine, Reload and MuzzleFlash fields make a firearm, whatever the item is called.
        private static string Signature(GrabbableObject script)
        {
            if (script == null)
            {
                return null;
            }

            var members = new List<string>();
            try
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                           | BindingFlags.Static | BindingFlags.DeclaredOnly;

                for (var type = script.GetType(); type != null && type != typeof(GrabbableObject); type = type.BaseType)
                {
                    members.AddRange(type.GetFields(flags).Select(field => Squash(field.Name)));
                    members.AddRange(type.GetProperties(flags).Select(property => Squash(property.Name)));
                }
            }
            catch (Exception)
            {
                return null;
            }

            string best = null;
            var bestHits = 1;

            foreach (var entry in MemberSignatures)
            {
                var hits = entry.Words.Count(word => members.Any(name => name.Contains(word)));
                if (hits > bestHits)
                {
                    bestHits = hits;
                    best = entry.Category;
                }
            }

            return best;
        }

        private static string Squash(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }
    }
}
