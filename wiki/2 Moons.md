# Moons

![Moons](https://raw.githubusercontent.com/Davesc0/LC-ClickableTerminal/main/docs/moons.png)

A table of every moon you can route to: name, risk, weather, price. Click a column header to sort,
again to turn the order around, a third time for the game's own order.

- **Click** a moon to route there. You get a confirm page with the weather, risk, cost and what's left.
- **Right click**: the moon's info page.
- **Wheel click**: simulate (with LLL).
- **Weather**: with WeatherRegistry, click it for tomorrow's forecast on that moon.

## On top
- **CAN AFFORD** and **CLEAR SKY** filters.
- **TAGS**: when LethalLevelLoader or DawnLib give the moons tags, tick the ones a moon must have.
- **The Company is buying at N%**: click it to go to the Company. Moons that buy scrap like the
  Company (Galetry, Shutter, Oxyde by default, SellingMoons) sit next to it as buttons.

## Markers
- A pin after the name: you are on that moon.
- A padlock: a mod has locked the moon. It's listed, but nobody can fly there yet (ShowLockedMoons).

## Hidden moons
Moons the game or a mod keeps off the list (Embrion, Artifice...) can be shown never, once you have
flown there, once you have landed there, or always (ShowHiddenMoons).

## Sections
- **GroupMoonsByAuthor**: one section per moon maker, the game's own first, can be buggy, didn't stess tested it.
