# Changelog

## 0.2.0
### New
- Hide items from the store: SETTINGS on the store page, or the HiddenItems setting.
- Category order setting for the store's tabs.
- Alt or the right stick switches between the menus and the game's own screen. The key is set by SwitchKey.
- Contracted support: contracts on MOONS, CONTRACT LIST and FIELD SUPPORT on MODS.
- Lategame Upgrades contracts (Exorcism, Defusal, Data Retrieval) in windows.
- Scrollbars can be dragged with the mouse.
- FORECAST shows what the weather does and its scrap multipliers. Your own descriptions go in dev.davesco.ClickableTerminal.weathers.txt.
- ModColors: weathers and contracts in WeatherRegistry's and Contracted's colours. Off by default.

### Changed
- EXTRAS is gone: the ship controls are on the STATUS panel, MONSTER SCAN is under OTHER, RANDOM is on MOONS.
- New defaults: every decoration and suit on sale (DecorationsOnOffer -1), locked moons hidden (ShowLockedMoons off).
- Panel buttons sit at the bottom of the panel.
- The store's cart panel shows only the count and the total, and ADD and REMOVE sit side by side.
- STATUS counts the scrap on the ship in a new way, I hope it doesnt cause stutter.
- Visited hidden moons are kept in the save file. Visits remembered by 0.1.0 are forgotten. Im still not sure if this is the right way.
- The UI was rebuilt underneath. Nothing should look different, but it should be more stable and less stuttery.

### Fixed
- Stutter while moving the mouse with the STATUS panel open.
- The pin or padlock running into the RISK column at a big text size.

## 0.1.0
- First version: the terminal as clickable menus, with the mouse, the keyboard or a controller.
- Moons with a video and details panel, filters and tags, a COMPANY MOONS section, hidden moons as ??? if you want.
- One page store with a cart, saved carts and buy confirmations.
- Bestiary, storage, scan and logs as lists, files and logs open in a window.
- MONITOR page: the radar live, switch who you watch, ping and flash boosters, teleport, door codes.
- STATUS panel on the main menu, extras (doors, lights, teleporters, lever, random moon, monsters).
- Pages for many mods' commands, and every Interactive Terminal API app with the mouse and the controller,
  in a window over the page.
- Every page has its own text size (- and +).
