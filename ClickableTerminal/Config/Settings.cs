using System.Collections.Generic;
using BepInEx.Configuration;

namespace ClickableTerminal
{
    internal enum ShowHidden
    {
        Never,
        AfterDiscovery,
        AfterVisited,
        Always
    }

    internal static class Settings
    {
        internal static ConfigEntry<bool> SkipWelcome;
        internal static ConfigEntry<bool> SharpTerminal;
        internal static ConfigEntry<bool> ZoomToScreen;
        internal static ConfigEntry<float> CursorSpeed;
        internal static ConfigEntry<bool> LineScroll;
        internal static ConfigEntry<int> ScrollLines;
        internal static ConfigEntry<bool> ShowExtras;
        internal static ConfigEntry<bool> RememberLastPage;
        internal static ConfigEntry<bool> ScreenAlwaysOn;
        internal static ConfigEntry<float> TextScale;

        internal static ConfigEntry<ShowHidden> ShowHiddenMoons;
        internal static ConfigEntry<bool> ShowLockedMoons;
        internal static ConfigEntry<bool> GroupMoonsByAuthor;
        internal static ConfigEntry<string> SellingMoons;

        internal static ConfigEntry<bool> ManageDecorations;
        internal static ConfigEntry<int> DecorationsOnOffer;
        internal static ConfigEntry<bool> StoreCategoriesFirst;
        internal static ConfigEntry<bool> CategorizeItems;
        internal static ConfigEntry<bool> ShowUtilitySlotIcon;
        internal static readonly ConfigEntry<string>[] CategoryName = new ConfigEntry<string>[ItemCategories.Slots];
        internal static readonly ConfigEntry<string>[] CategoryItems = new ConfigEntry<string>[ItemCategories.Slots];
        internal static readonly ConfigEntry<string>[] CategoryKeywords = new ConfigEntry<string>[ItemCategories.Slots];
        internal static ConfigEntry<string> HiddenStoreCategories;

        internal static ConfigEntry<bool> ShowAllCreatures;
        internal static ConfigEntry<bool> GroupCreaturesByAuthor;
        internal static ConfigEntry<string> ExtraCommands;

        // The config file lists sections by name, so they are numbered to keep the general ones on top.
        private const string General = "1. General";
        private const string Moons = "2. Moons";
        private const string Store = "3. Store";
        private const string Categories = "4. Store categories";
        private const string Bestiary = "5. Bestiary";
        private const string CommandList = "6. Commands";

        internal static void Init(ConfigFile config)
        {
            SkipWelcome = config.Bind(General, "SkipWelcome", false,
                "Skip the welcome screen and open the main menu right away.");
            SharpTerminal = config.Bind(General, "SharpTerminal", true,
                "Draw the game at your window's resolution while you are using the terminal, so its text is sharp. " +
                "The game already switches to a slightly bigger picture at the terminal, this raises that one. " +
                "Everywhere else the picture stays as it was.");
            ZoomToScreen = config.Bind(General, "ZoomToScreen", false,
                "Zoom the camera in while using the terminal, so its screen fills the view. " +
                "Helps in modpacks where the player ends up standing further from the monitor.");
            CursorSpeed = config.Bind(General, "CursorSpeed", 1f,
                "How fast the terminal cursor follows the mouse.");
            RememberLastPage = config.Bind(General, "RememberLastPage", true,
                "Open the terminal on the page you left it on, instead of the main menu. Questions are not reopened.");
            ScreenAlwaysOn = config.Bind(General, "ScreenAlwaysOn", false,
                "Keep the terminal's screen and light on when nobody is using it, showing the last page. Only on your side.");
            ShowExtras = config.Bind(General, "ShowExtras", true,
                "Add EXTRAS to the main menu: the ship's doors, teleporters, lights and lever, a random moon.");
            LineScroll = config.Bind(General, "LineScroll", true,
                "On the pages the game prints as text, scroll a few lines at a time instead of a third of the page. " +
                "Stands aside when Terminal Scroll Patch is installed.");
            ScrollLines = config.Bind(General, "ScrollLines", 8, new ConfigDescription(
                "How many lines one turn of the mouse wheel scrolls with LineScroll.", new AcceptableValueRange<int>(1, 30)));
            TextScale = config.Bind(General, "TextScale", 1f, new ConfigDescription(
                "Size of the text on the list pages. 1 is the terminal's own letter size, smaller fits more rows on the screen.",
                new AcceptableValueRange<float>(0.6f, 1f)));

            ShowHiddenMoons = config.Bind(Moons, "ShowHiddenMoons", ShowHidden.Never,
                "When to list the moons that are kept off the catalogue, like Embrion and Artifice, or the ones a mod has hidden.\n" +
                "  Never           they stay secret, you have to know the name\n" +
                "  AfterDiscovery  once this save has flown there\n" +
                "  AfterVisited    once this save has landed there\n" +
                "  Always          listed from the start");
            ShowLockedMoons = config.Bind(Moons, "ShowLockedMoons", true,
                "List the moons a mod has locked: they are known, but nobody can fly there yet. They get a padlock after the name.");
            GroupMoonsByAuthor = config.Bind(Moons, "GroupMoonsByAuthor", false,
                "Split the moon list into sections by who made the moons: the game's own first, then one section per author. " +
                "Several moon mods by the same author share a section. Needs LethalLevelLoader or DawnLib to know who made what.");
            SellingMoons = config.Bind(Moons, "SellingMoons", "Galetry, Shutter, Oxyde",
                "Moons that buy scrap like the Company does, comma separated. They are listed next to the Company above the table. " +
                "A name that no installed moon has is simply skipped.");

            StoreCategoriesFirst = config.Bind(Store, "StoreCategoriesFirst", true,
                "Open the store on a short list of its parts (items, vehicles, upgrades, decorations, suits), each with a page of its own, " +
                "and a cart to order several items together. Off: everything on one long page, bought one at a time.");
            ManageDecorations = config.Bind(Store, "ManageDecorations", true,
                "Let this mod set how many decorations and suits are on sale (DecorationsOnOffer). " +
                "Off: the selection is left to the game or to another mod. Always off while StoreRotationConfig is installed.");
            DecorationsOnOffer = config.Bind(Store, "DecorationsOnOffer", 0, new ConfigDescription(
                "How many decorations and suits are on sale each quota. 0 leaves it to the game (four or five), " +
                "-1 puts every one of them on sale at once. This changes what can be bought, not only what is listed, " +
                "so everyone in the lobby should have the same number. It takes effect the next time the store's selection changes.",
                new AcceptableValueRange<int>(-1, 40)));
            HiddenStoreCategories = config.Bind(Store, "HiddenStoreCategories", "",
                "Parts of the store that are not listed, comma separated. The CATEGORIES button in the store ticks them on and off, " +
                "this is where it remembers them. Example: DECORATIONS, SUITS");

            CategorizeItems = config.Bind(Store, "CategorizeItems", true,
                "Sort the items into categories (LIGHTING, MELEE, FIREARMS...). Off: one ITEMS section with everything in it.");

            ShowUtilitySlotIcon = config.Bind(Store, "ShowUtilitySlotIcon", true,
                "Draw a small bag after the items that fit the utility slot.");

            // The categories are listed in the store in this order. What fits none of them goes to OTHER.
            for (var slot = 0; slot < ItemCategories.Slots; slot++)
            {
                var number = "Category " + (slot + 1);
                CategoryName[slot] = config.Bind(Categories, number + " name", ItemCategories.DefaultName(slot),
                    "The name of the category in the store. Empty: the category is not used, its items go to the others or to OTHER.");
                CategoryItems[slot] = config.Bind(Categories, number + " items", ItemCategories.DefaultItems(slot),
                    "Whole item names that always go to this category, comma separated. Beats everything else. " +
                    "Capitals, spaces and punctuation do not matter. Example: Flashlight, Pro-flashlight");
                CategoryKeywords[slot] = config.Bind(Categories, number + " keywords", ItemCategories.DefaultWords(slot),
                    "Words that put an item in this category when they are anywhere in its name or in the name of its script, " +
                    "comma separated. The longest matching word wins over shorter ones in other categories.");
            }

            ShowAllCreatures = config.Bind(Bestiary, "ShowAllCreatures", false,
                "List every creature file and let it be opened without scanning the creature first. " +
                "Nothing is written to the save, the files close again when this is turned off.");
            GroupCreaturesByAuthor = config.Bind(Bestiary, "GroupCreaturesByAuthor", false,
                "Split the creature list into sections by who made the creatures: the game's own first, then one section per author. " +
                "Several mods by the same author share a section.");

            ExtraCommands = config.Bind(CommandList, "ExtraCommands", "",
                "Your own entries for the command list, for commands the terminal does not show. " +
                "Label=what to type, separated by semicolons. Example: Emote shop=emotes; Upgrades=lgu");
        }
    }
}
