# Changelog

## 0.1.0-beta.2

- The right package icon.
- The two card tables have their own hammer-menu icons (a felt table with cards and chips) instead of a deck of cards.
- Everything from beta.1 still applies: this is an early preview, so back up your world and characters.

## 0.1.0-beta.1

**Early preview. Brand new and not thoroughly tested: back up your world and characters.**

The debut. **Board Vikings: Cards** by DrakesMods.

- **Throwing Card:** one item for all 53 faces; pick the card when you craft it. It flies like a spear throw, goes where you aim, and lands as an item you can pick up. 1 Wood, 1 Coal.
- **Deck of Cards:** a whole 52-card deck in one item. Right-click to pull out any card, put cards back, or shuffle. Choose one of **15 card backs** when you craft it (20 Wood, 5 Coal).
- **Playing Card:** a card that came out of a deck. Not a weapon; goes back into a deck.
- **Giant Card:** a big card that stands on its edge. Press E to choose its face and its back; new cards copy your last choice. Lean them on each other for a house of cards: hold Ctrl and scroll while placing, or use the **Gizmo** mod (recommended; the built-in tilt steps aside when Gizmo is installed).
- **Archery target:** the game's archery target takes cards. A hit scores like an arrow, the card stays stuck, and E hands every card back with its own face.
- **Card Table (Small and Long):** a felt table with 2 to 8 seats. Press E to sit; the builder is the dealer and sets how many seats the table uses. (Dealing and playing at the table, the dealer's hat, rules and chips are coming in the next releases.)
- Cards are built in code (rounded, lit, matte, never stretched), with square inventory icons, a small card in hand and real card proportions.
- **Dealer's Visor** now uses a modelled green visor (translucent brim with a bent-up fold, white band and piping).
- Standalone: no DrakeModsLibs or other mod needed. Card faces are plain PNGs in `Assets/faces` (no asset bundle); releases go to Hexium; the DrakesTestLab suite (35 tests) covers items, icons, the deck, giant cards, throwing, the archery target and the table.

## 0.0.1

- Initial DrakeMod build pipeline (Gale + r2 `drakeTest`, package, Hexium-ready CI).
- Card registration fixes (sprite paths, null guards, deploy folder naming).
