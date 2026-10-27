using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace ClickableTerminal
{
    // What a weather does, in a sentence or two. Neither the game nor WeatherRegistry keeps a description, so these are written here from the mods' own pages. Anyone can add or change one in a file of their own next to the config.
    internal static class WeatherNotes
    {
        internal class Note
        {
            internal string Weather;
            internal string Mod;
            internal string Package;
            internal string Text;
        }

        private static readonly Note[] Builtin =
        {
            //Vanilla
            Vanilla("None", "Clear skies. Nothing out of the ordinary."),
            Vanilla("Rainy", "Rain softens the ground: patches of quicksand form outside and pull down whoever walks into them."),
            Vanilla("Stormy", "Lightning strikes outside, drawn to metal. When scrap you carry starts to crackle, drop it."),
            Vanilla("Foggy", "Thick fog outside. You see much less far."),
            Vanilla("Flooded", "The water rises through the day and floods the low ground outside. You can drown in it."),
            Vanilla("Eclipsed", "Night all day. More creatures roam outside, and they are out from the moment you land."),
            Vanilla("DustClouds", "Clouds of dust outside. You see much less far."),

            //Wesleys Weathers
            Mod("Forsaken", "Wesley's Weathers", "Magic_Wesley-Wesleys_Weathers", "Set foot on this forsaken land and you will come back changed."),
            Mod("Hallowed", "Wesley's Weathers", "Magic_Wesley-Wesleys_Weathers", "The spirit of Halloween, everywhere, all the time."),
            Mod("Hurricane", "Wesley's Weathers", "Magic_Wesley-Wesleys_Weathers", "Strong winds, heavy rain, and trees."),
            Mod("Earthquakes", "Wesley's Weathers", "Magic_Wesley-Wesleys_Weathers", "A violent force of nature shakes the ground."),
            Mod("Minefield", "Wesley's Weathers", "Magic_Wesley-Wesleys_Weathers", "The ground beeps. Watch where you step."),

            //MrovWeathers
            Mod("Cloudy", "MrovWeathers", "mrov-MrovWeathers", "Clouds cover the sky and the outside is darker."),
            Mod("Blackout", "MrovWeathers", "mrov-MrovWeathers", "Every light in the facility is off."),

            //LegendWeathers
            Mod("Blood Moon", "Legend Weathers", "Zigzag-LegendWeathers", "Creatures spawn more often and come back to life seconds after they die. The fog clears. Kills can leave Blood Stones."),
            Mod("Majora Moon", "Legend Weathers", "Zigzag-LegendWeathers", "A moon is falling on the planet: more scrap than usual, but the ship leaves on its own before the crash."),

            //Zeranos Moon
            Mod("Winter", "Zeranos", "luakite-Zeranos_Moon", "Winter on Zeranos: snow and a cold wind."),
            Mod("Autumn", "Zeranos", "luakite-Zeranos_Moon", "Autumn on Zeranos: the trees turn."),
            Mod("Murrey Moon", "Zeranos", "luakite-Zeranos_Moon", "A red moon hangs in the sky."),
            Mod("Radioactive Zones", "Zeranos", "luakite-Zeranos_Moon", "Radioactive patches appear around the moon. Standing in one hurts; a ticking warns you when you are close."),

            //LethalElements
            Mod("Heatwave", "LethalElements", "LethalElements", "Scorching heat outside drains stamina and slows its recovery. The air shimmers."),
            Mod("Solar Flare", "LethalElements", "LethalElements", "Radiation from the sun scrambles walkie-talkies, the radar map, the teleporter and the signal translator, and drains batteries. Strong flares jam the facility's doors. Radar boosters help."),
            Mod("Snowfall", "LethalElements", "LethalElements", "Snow piles up through the day and slows everyone down. Stay too long in deep snow and you freeze. Water freezes over."),
            Mod("Blizzard", "LethalElements", "LethalElements", "Snowfall with freezing winds that push you around and give you frostbite, and a wave of cold now and then."),
            Mod("Toxic Smog", "LethalElements", "LethalElements", "Thick fog, and clouds of poison gas inside and out."),

            //Black Fog Weather
            Mod("Black Fog", "Black Fog Weather", "DemonMae-Black_Fog_Weather", "A black fog that follows you even inside, and something watches from it. It lifts around 3 PM."),

            //Blue
            Mod("Blue", "Blue", "Generic_GMD-Blue", "Everything turns blue."),

            //Kenkis Weathers
            Mod("Gusty", "Kenji's Weathers", "rectorado-Kenjis_Weathers", "Violent gusts of wind outside every now and then."),
            Mod("Infested", "Kenji's Weathers", "rectorado-Kenjis_Weathers", "Bugs of many kinds infest the moon, a new threat while you gather scrap."),
            Mod("Acid Rain", "Kenji's Weathers", "rectorado-Kenjis_Weathers", "Bursts of acid rain hurt anyone caught outside. Listen for it and take cover."),

            //Legmr Moon
            Mod("Snow", "Legmr_Moon", "Legmr-Legmr_Moon", "Freezing cold outside: you slow down, your view narrows, and at the worst you cannot sprint and start to freeze. Warm up inside, in the ship, or close to big creatures."),
            Mod("Glow Bugs", "Legmr_Moon", "Legmr-Legmr_Moon", "A darker sky and more creatures. A swarm of glow bugs follows one player at a time, and every creature goes after whoever has it."),
            Mod("FireSmog", "Legmr_Moon", "Legmr-Legmr_Moon", "Fires break out on the ground, grow and die down. Standing in one burns."),


            //TemporalStorm
            Mod("Temporal Storm", "TemporalStorm", "mr_hat-TemporalStorm", "Instability rises through the day: rifts open and let creatures through, rust hazards grow outside, metal scrap rusts (some turns temporal and worth more), and the Thunderlord roars."),
            
            //Tony Moons Remade
            Mod("Acid Flood", "Tony's Moons Remade", "Tony_Opic-Tonys_Moons_Remade", "The water rises through the day like Flooded, but it is acid: standing in it hurts. Acid rain falls too."),

            //Generic Weathers
            Mod("Anomaly", "Generic Weathers", "Generic_GMD-Generic_Weathers", "Now and then the ground lets go of you: bursts that pull you up into the sky."),
            Mod("Downpour", "Generic Weathers", "Generic_GMD-Generic_Weathers", "Bursts of heavy rain and loud wind every now and then."),
            Mod("Radiation", "Generic Weathers", "Generic_GMD-Generic_Weathers", "Some scrap inside is swapped for irradiated pieces: they are worth more, but their value decays over time."),
            Mod("Slimy", "Generic Weathers", "Generic_GMD-Generic_Weathers", "The whole moon is coated in slime."),

            //InsanityMod
            Mod("Paranoia", "InsanityMod", "Meeee-InsanityMod", "Insanity rises faster inside the facility, and creatures are more active."),

            //EchoCurse Weather
            Mod("Echo Curse", "EchoCurse Weather", "vtl0-EchoCurse_Weather", "Creatures hear half again as far, more of them come inside and out, and clouds darken the sky."),

            //Rapture Weather
            Mod("Rapture", "Rapture Weather", "Generic_GMD-Rapture_Weather", "From 9 AM, a rapture sweeps the moon every two hours."),
            
            //BeedsCurseClimates
            Mod("Torpid", "Beef's Curious Climates", "MrSaltedBeef-BeefsCuriousClimates", "A purple gas: breathing it drains stamina and makes you see and hear things."),
            Mod("Sunshower", "Beef's Curious Climates", "MrSaltedBeef-BeefsCuriousClimates", "Gentle rain under a rainbow. Running over a puddle makes you slip and drop what you carry."),

            //Wither Storm Weather
            Mod("Wither Storm", "Wither Storm Weather", "Tony_Opic-Wither_Storm_Weather", "The Wither Storm from Minecraft: Story Mode comes to the moon."),

            //Orage
            Mod("Orage", "Orage", "Generic_GMD-Orage", "Everything turns orange, with an orange haze over the ground."),
            Mod("Orange", "Orage", "Generic_GMD-Orage", "Everything turns orange, with an orange haze over the ground."),

            //MoonWakeUp
            Mod("Moonlight", "Moon WakeUp", "BakaMODs-MoonWakeUp", "Night all day, and a giant awakened moon sweeps the ground with its eyes. Stay out of its light."),

            //Meteor Showers as a Weather
            Mod("Meteor Shower", "Meteor Showers as a Weather", "BabyblueSheep-Meteor_Showers_as_a_Weather", "From 8:30 AM to 9:30 PM a meteor falls every few seconds."),

            //AcidRain
            Mod("Acid Rain", "AcidRain", "TheBoys8D-AcidRain", "Acid rain hurts players and creatures and corrodes scrap."),

            //Code Rebirth
            Mod("Tornado", "Code Rebirth", "XuXiaolan-CodeRebirth", "A tornado pulls in and throws whoever gets close outside."),
            Mod("Meteor Shower", "Code Rebirth", "XuXiaolan-CodeRebirth", "Meteorites rain down, so keep an eye on the sky. Rare crystals can be found where they land."),
            Mod("Night Shift", "Code Rebirth", "XuXiaolan-CodeRebirth", "Only on Oxyde. The weather itself adds no effects."),

            //HellWeather
            Mod("Hell", "Hell Weather", "stormytuna-HellWeather", "Every weather the moon can have, all at once: rain, storm, fog, flood and eclipse."),

            //WhimsicalWeather
            Mod("Whimsical", "Whimsical Weather", "Bobbie-WhimsicalWeather", "The world turns bright and rainbow-coloured, like Pyrovision: no blood, higher voices, and some items and creatures look different. Has flashing lights."),


        };

        private static List<Note> added;

        private static string FilePath => Path.Combine(Paths.ConfigPath, "dev.davesco.ClickableTerminal.weathers.txt");

        private static Note Vanilla(string weather, string text)
        {
            return new Note { Weather = weather, Mod = "the game", Text = text };
        }

        private static Note Mod(string weather, string mod, string package, string text)
        {
            return new Note { Weather = weather, Mod = mod, Package = package, Text = text };
        }

        // Spaces and capitals do not count: WeatherRegistry writes "DustClouds", others "Dust Clouds".
        internal static string Key(string name)
        {
            return new string((name ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        // The note for a weather. A line in the player's own file wins. When two mods have a weather of the same name, the one whose package is installed is taken.
        internal static Note For(string weather)
        {
            var key = Key(weather);
            var own = Added().LastOrDefault(note => Key(note.Weather) == key);
            if (own != null)
            {
                return own;
            }

            return Known(weather, Installed);
        }

        internal static Note Known(string weather, Func<string, bool> installed)
        {
            var key = Key(weather);
            var known = Builtin.Where(note => Key(note.Weather) == key).ToList();
            return known.Count <= 1 ? known.FirstOrDefault() : known.FirstOrDefault(note => installed(note.Package)) ?? known[0];
        }

        private static bool Installed(string package)
        {
            try
            {
                return package != null && Directory.Exists(Paths.PluginPath)
                       && Directory.GetDirectories(Paths.PluginPath).Any(folder => Path.GetFileName(folder).IndexOf(package, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // "Weather name = what it does", one a line. Lines starting with # are left out.
        private static List<Note> Added()
        {
            if (added != null)
            {
                return added;
            }

            added = new List<Note>();
            try
            {
                if (!File.Exists(FilePath))
                {
                    File.WriteAllLines(FilePath, new[]
                    {
                        "# What a weather does, as the terminal tells it. One weather a line: Weather name = what it does",
                        "# A weather written here replaces what the terminal knows about it. Example:",
                        "# Foggy = Thick fog outside. Bring a flashlight."
                    });
                    return added;
                }

                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var at = line.IndexOf('=');
                    if (line.TrimStart().StartsWith("#") || at <= 0)
                    {
                        continue;
                    }

                    added.Add(new Note { Weather = line.Substring(0, at).Trim(), Text = line.Substring(at + 1).Trim() });
                }
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("The weather notes could not be read: " + e.Message);
            }

            return added;
        }
    }
}
