# Changelog

## 0.1.1 — 2026-10-12
- Compact panel like the Money Sort original: a 300-px card at the top-left sized to its items (`CheatMenuOptions.PanelWidth` / `PanelMargin`), 42-px rows, 18–20 px text, number rows as `[ value ][ Label ]`; Logs tab stays full-screen.

## 0.1.0 — 2026-10-12
- First version, taken from the Money Sort prototype's cheat panel and made game-independent: `CheatRegistry`
  (buttons, number fields, toggles, read-outs, status line), code-built uGUI panel with a draggable button, Logs tab
  (`CheatLogBuffer`), `CheatMenu.IsAvailable` gate (Editor / development build / `CS_CHEATS`, off with `CS_CHEATS_DISABLED`).
