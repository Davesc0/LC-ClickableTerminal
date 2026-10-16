# Store and cart

![Store items](https://raw.githubusercontent.com/Davesc0/LC-ClickableTerminal/main/docs/store-items.png)

STORE opens a short list of the store's parts, each with a page of its own:

- **ITEMS**: by category (LIGHTING, MELEE, FIREARMS...), with how many you own and the sale.
- **VEHICLES**, **SHIP UPGRADES**, **DECORATIONS**, **SUITS**: click to buy, you get a confirm page.
- **CART**: what you picked on the ITEMS page.

Turn StoreCategoriesFirst off for the old single page (no cart).

## Cart
- On ITEMS, click a row or **[+]** to put one in the cart, **[-]** to take one out.
- The CART page shows the total, the credits left after, and how full the dropship gets.
- **ORDER** buys everything, one line after the other the way the game buys, and ends on a receipt.
  Anything that couldn't be ordered is listed under it.
- The cart keeps to the game's limits: 12 items in the dropship, 10 of one kind, what you can pay.

## Categories
The categories are set in the config, section "4. Store categories": a name, item names that always
go there, and keywords. 15 places, the first 13 filled in, the last two free. An empty name turns a
place off. Anything that fits none of them goes to OTHER. The CATEGORIES button hides or shows them.

I tried to categorize a great varity of modded items, but I can't know them all. If you find an item that doesn't go where it should, you can always edit the categories.

A small bag after an item: it fits the utility slot (ShowUtilitySlotIcon).

## Decorations
- **ManageDecorations** on: DecorationsOnOffer sets how many decorations and suits are on sale.
  0 leaves it to the game, -1 puts every one on sale.
- With **StoreRotationConfig** installed the selection is left to it, and its discounts show on the
  DECORATIONS and SUITS pages.
