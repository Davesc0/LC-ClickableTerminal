# ClickableTerminal - TODO

## Later features
- Hidden moons shown as "???" instead of left out: one more choice for ShowHiddenMoons. Clicking
  one must not route anywhere the player could not type.
- Saved carts: save the current cart under a name, load it back with one click.
- Clock in the header row while landed on a moon.
- List of the monsters alive on the current moon. Decide how much it gives away: names only,
  counts, inside/outside.

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
