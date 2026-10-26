# Board Vikings — Card Games

Playing cards for Valheim (DrakeMods). Craftable card items, buildable card pieces, and a card holder.

**Requires [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).**

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

Tag `v{Version}` (must match `mod.package.props`) to publish **Thunderstore** then **Hexium** via GitHub Actions.
