# ClickableTerminal - TODO

## Before the release
- CHANGELOG, version number in manifest.json and the plugin, then pack.ps1.

## Later features
- Door and trap codes on the MONITOR map. Idea: every code visible on the radar picture is a target to click
  or step onto with the d-pad (A sends it), the panel says what it is (DOOR, TURRET, MINE, SPIKE TRAP) and
  whether it is cooling down. Only what the radar shows, so no more than vanilla. A `< A > < 3 > SEND` line
  as a fallback. Maybe later a hack minigame behind a setting (off by default).
- ??? moons (ShowHiddenMoons = Mystery) clickable without cheating. Idea: FOLLOW SIGNAL, a blind route.
  NO SIGNAL in the panel, only the price (maybe hidden too, behind a setting), the name shows once the ship
  is there. Other idea: DECODE for credits or a day.
- Rethink EXTRAS. One idea: spread the extras and the mods' features to where they belong, in a
  lore-friendly way (ship controls with the ship, the crew's tools with the crew, money matters with the
  store), instead of one EXTRAS list and the STATUS panel.
- Moons that buy scrap (SellingMoons): now a COMPANY MOONS section on top with Gordion first, the rate line
  picks Gordion. See whether it needs more.
- Lag spikes and low fps in a big pack: check once with only the mods we support, to see whether it is us.

## Good to know when something breaks
- Many mods patch the same terminal methods: `LoadNewNodeIfAffordable` (five transpilers),
  `TextPostProcess` (nine mods), `ParsePlayerSentence` (transpilers and skipping prefixes from
  SellMyScrap, LobbyControl, MergeScrap, ScrapBank, WeatherRegistry). A click going to the wrong
  page or a wrong price usually starts there.
- Conflicts between other mods, not ours to fix:
  - TooMuchScrap and MergeScrap both answer "merge".
  - ScrapBank answers every "sell ..." and "bank", then LethalAPI.Terminal throws on the emptied
    input. LethalAPI.Terminal also fails to patch at all next to MonoDetour (its OnSubmit transpiler).
  - WeatherRegistry's "weather forecast <moon>" never finds the moon (lower-cased input against
    mixed-case names). Our forecast page reads its weights directly.
  - Permanent Moons 1.0.1 (BULLETBOT): its LoadNewNodeIfAffordable postfix assumes every credit change is a
    moon route and throws a NullReferenceException on any item or decoration purchase (currentMoon is only
    set for routes). Happens with typed commands too.
  - Football 1.1.14 (Kittenji): its bestiary page carries vehicle index 0, the Cruiser's. Our catalog ignores
    info, creature and log pages for purchases, but typing "football" may still make the game order a Cruiser.
  - ExtraDaysToDeadline needs RuntimeNetcodeRPCValidator and LethalAPI.Terminal; the latter breaks ScrapBank.
