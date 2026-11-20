# ClassicSpins Cheats (`com.classicspins.cheats`)

A tester cheat menu you can drop into any Unity game. The game says **which** cheats exist; the package draws the
panel. It started as the cheat panel of the Money Sort prototype (`GameplayCheatUI` + IngameDebugConsole). This
version is not tied to any one game, and it does not depend on PrototypeFramework.

- **Floating `CHEAT` button.** You can drag it, and the package remembers where you left it. It turns red with a count when
  errors have been logged.
- **Cheats tab.** Sections of buttons (laid out two to a row), whole-number fields with a submit button, ON/OFF toggles
  and live read-outs. Clearer note: a read-out is a line of text that shows the game's current state and updates while
  the panel is open.
- **Logs tab.** Shows the last 300 lines of the Unity log, in colour. It has *Clear*, *Copy all* (copies with stack
  traces) and *Errors only*.
- **Built entirely in code** with uGUI and the legacy built-in font. There is no prefab and no TMP font asset, so
  installing the package needs no setup.
- **Draws on its own screen-space overlay canvas** (sorting order 32000), inside `Screen.safeArea`. It needs an
  `EventSystem` in the scene, which every uGUI game already has.

## When it exists

`CheatMenu.IsAvailable` decides whether the menu is created:

| Build | Menu created? |
|---|---|
| Unity Editor | yes |
| Development build | yes |
| Release build with the `CS_CHEATS` scripting define (a tester build) | yes |
| Release build without `CS_CHEATS` | no |
| Any build with `CS_CHEATS_DISABLED` (for example, to take clean screenshots in the Editor) | no |

When `IsAvailable` is false, `CheatMenu.Create` returns `null` and nothing is created. Money Sort used the define
`GAME_IS_CHEAT`. In a game that keeps that define, add `CS_CHEATS` next to it.

## Install

**Embedded:** copy the folder `Packages/com.classicspins.cheats/` into the other game's `Packages/` folder.

**From git:** add this line to `Packages/manifest.json`:

```json
"com.classicspins.cheats": "https://github.com/FoxCubGames/cardslots_prototype.git?path=/Packages/com.classicspins.cheats"
```

There are two assemblies:

- **`ClassicSpins.Cheats`** has no engine references (`CheatRegistry`, `CheatLogBuffer`), so it can be unit-tested
  with plain `dotnet test`.
- **`ClassicSpins.Cheats.UI`** is the panel. Reference both from the assembly that sets the menu up.

## Use

```csharp
using ClassicSpins.Cheats;

var cheats = new CheatRegistry();
cheats.Group("Level")
    .Button("Win", () => board.CheatWin())
    .Button("Lose", () => board.CheatLose())
    .Number("Go to level", () => progress.CurrentLevel, level => game.StartLevel(level), "Play");
cheats.Group("Economy")
    .Number("Add coins", 1000, n => wallet.Add(n), "Add")
    .Toggle("Reveal hidden", () => board.RevealHidden, on => board.RevealHidden = on);
cheats.Group("State").Text(() => $"Coins {wallet.Coins}");

CheatMenu.Create(cheats);   // once, at start-up; returns null where cheats are off
```

- **Showing a result.** Call `cheats.Status("…")` to show a message under the header. If a cheat throws an
  exception, the panel shows it there instead of breaking.
- **Removing a group.** Disposing a `CheatGroup` removes its items. This suits cheats that belong to one screen or one
  scope.
- **Hiding the menu.** `CheatMenuView.SetVisible(false)` hides the button and the panel, for example while taking
  captures.
- **Main thread only.** Register and change cheats from the main thread. Log capture is thread-safe.

`CheatMenuOptions` sets the canvas sorting order, the reference resolution and match, the button caption and start
position, the UI layer, and whether logs are captured and how many lines are kept.

## Rules for the game side

These are the conventions the CardSlot wiring follows (see `docs/workflow/DECISIONS.md` D-048):

- **Go through the game's own flows.** A cheat calls the same flows a real action would. For example, "Win" runs the
  normal win flow. It does not change what a screen shows behind the screen's back.
- **Save as you would anywhere else.** A cheat that changes saved data saves it the same way the rest of the game does.
- **Keep cheats out of release builds.** Register cheat-only services only where `CheatMenu.IsAvailable` is true, so a
  release build never has them.
