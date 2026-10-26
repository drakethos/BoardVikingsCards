# Board Vikings — Card Games

Playing cards for Valheim (DrakeMods): throwing cards, a deck you play from, and giant cards you can stand up and build with.

**Requires [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) and DrakeModsLibs.**
**Recommended: the Gizmo mod** for free rotation when placing giant cards (see below).

## What's in it

| Item | What it is | Cost |
| --- | --- | --- |
| **Throwing Card** | One item for all 53 faces. Pick the card when you craft it (the variant picker). Throw it like a spear, or keep it for decoration. | 1 Wood, 1 Coal |
| **Deck of Cards** | A full 52-card deck in a single item. Right-click it to pull out any card, put cards back, or shuffle. | 20 Wood, 5 Coal |
| **Playing Card** | A card that came out of a deck. It is not a weapon and goes back into a deck. It cannot be crafted on its own. | — |
| **Giant Card** | A big card piece (Cards tab of the hammer). Press **E** to choose its face. | 1 Wood, 1 Coal |

Throwing cards and playing cards are kept separate on purpose: cards you play with never turn into weapons, and cards you throw never end up in a deck.

## The deck

Right-click the deck. Pick a suit tab and click any card to take it out. Your own cards are listed at the bottom: click one to put it on top of the deck, or use **Put all my cards back**. **Shuffle** is one button — nobody shuffles 52 cards by hand.

## Giant cards and houses of cards

Giant cards stand on their bottom edge. While placing one you can lean it, so two cards can lean on each other.

- **With the Gizmo mod (recommended):** rotate the card with Gizmo's keys as you would any piece. It rotates around the bottom edge, so it stands and leans naturally. The built-in tilt stays out of the way.
- **Without Gizmo:** hold **Ctrl** and scroll to lean the card in small steps. The card keeps facing the way you aim it and leans around its two bottom corners, like a `<`. Scroll without Ctrl to turn it as usual. (Change the key or the step size in the config.)

## Config

`Giant card`: tilt key, tilt step, and an option to use the built-in tilt even when Gizmo is installed.

## Install

Import the Thunderstore zip via **Gale**, **r2modman**, or **Thunderstore Mod Manager**, or extract into:

```
BepInEx/plugins/DrakeMods-BoardVikings-CardGames/
```

## Build / deploy (local)

Uses the DrakeMod RenameIt-style workflow:

1. Copy `environment.props.example` → `environment.props` (already set for `drakeTest` + Gale).
2. Build `Cards.csproj` (Debug).
3. Output deploys to:
   - Gale: `%AppData%\com.kesomannen.gale\valheim\profiles\drakeTest\BepInEx\plugins\DrakeMods-BoardVikings-CardGames\`
   - r2modman: `%AppData%\r2modmanPlus-local\Valheim\profiles\drakeTest\BepInEx\plugins\DrakeMods-BoardVikings-CardGames\`
4. Thunderstore zip lands under `C:\Modding\Thunderstore\DrakeMods-BoardVikings-CardGames-{version}.zip`.

Tests: `Tests/gen_cards_suite.py` writes the DrakesTestLab suite; the build copies it into the test profiles.

Tag `v{Version}` (must match `mod.package.props`) to publish **Thunderstore** then **Hexium** via GitHub Actions.
