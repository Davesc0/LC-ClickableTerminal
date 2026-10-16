# Clickable Terminal

The terminal, but you can use it with the mouse or a controller.
Every page is redrawn as a clickable menu: moons, store, bestiary, storage, scan and the rest
Pages of other mods are pulled in too, under one MODS page.
You can still type every command as usual, and the terminal works with the mouse and controller even if you don't use the menus.

It changes a lot, so the details are in the [Wiki](https://thunderstore.io/c/lethal-company/p/Davesco/ClickableTerminal/wiki/), one page for every part of the terminal.


## Required

Nothing besides BepInEx.
Everything else is soft dependencies, you can use the terminal without them, but they make it better.

## Recommended

- InputUtils: with it the controller hints are button icons, without it button names.
- CorrectControllerIcon: optional. The icons follow the controller picked there.
- FontUpdate

## You no longer need
- Terminal Scroll Patch (if both present TSP is active)
- PersistentTerminal (mostly redundant I think)

Any mod that implements extra commands like pulling the lever, opening the door.
It also has a random moon route command. All under extra, you can turn it off.

## Compatible
- StoreRotationConfig (it picks the decorations, its discounts show on the store pages)
- Interactive Terminal API (see below)

## Incompatible
- TerminalApi (Should work, but may have conflicts)
- darmuhsTerminalStuff (its own store, moons and home pages, command history)
- Any mod changing the terminal pages, like TerminalUtils, TerminalFormatter, and other interactive moon/shop mods.

## Mods with their own pages or fixes

- Sell My Scrap
- ScrapBank
- MergeScrap
- Selectable Interiors
- Terminal Seed Picker
- ExtraDaysToDeadline
- TooManyEmotes (emote store)
- LethalLevelLoader and DawnLib (moon tags, authors, hidden and locked moons)
- Code Rebirth (refused purchases)
- WeatherRegistry (weather forecast)

Every app built on Interactive Terminal API should work with the mouse and the controller, including back and menu.
Tested with:
- Lategame Upgrades
- Extend Deadline
- Weather Probe 
- Revive
- MergeScrap
- Ship Inventory

