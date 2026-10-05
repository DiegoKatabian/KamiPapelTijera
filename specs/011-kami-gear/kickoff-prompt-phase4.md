# Kickoff prompt — Spec 011 (Kami Gear), Phase 4: a Flap that only listens while it's open

Paste everything below the line into a fresh Claude Code session at the repo root.

---

We're implementing **Phase 4 of spec 011** in KamiPapelTijera (Unity 2021.3, URP). It isn't about gear:
playing the Phase 3 Wardrobe, Diego found a Flap tab showing during gameplay, and he remembers changing the
Flap's settings "on any tab" while playing. Goal of this session: **a closed Flap can't be seen, clicked,
navigated or pressed, and the Flap's input belongs to one explicit state**. All of it lives in
`Assets/Scripts/UI/FlapManager.cs`. Fix the menu's behaviour, don't redesign the menu.

## Branch and state

- Work on branch **`011-kami-gear`**. Phases 2 and 3 (played by Diego 2026-10-05) and task 4.A are
  committed and pushed there. Check `git status` is clean before starting. Never commit, push or open a
  PR without Diego's explicit permission, every time.
- GitHub (M4, public build Oct 21): epic **#139**. This session = **#153 (4.B) → #154 (4.C)**, then the
  close-out. **#152 (4.A)** is built but not confirmed by Diego yet: it closes with this phase. Related:
  **#46** (#41.3, input during the Flap pause), cross-linked: the slide windows are the part this phase
  changes.

## Read first

1. `specs/011-kami-gear/spec.md`: findings **F5 and F6** (the whole diagnosis), "Functional requirements >
   Phase D" (FR-301..306), "Design > Phase D: the Flap's states", "Decisions" Q12-Q14, "Verification"
   (the Phase 4 bullet).
2. `specs/011-kami-gear/tasks.md`: "Phase 4".
3. `docs/claude/controles-y-gamepad.md`: "Pausa del Flap", "Navegación de UI", "Fugas de foco que ya nos
   mordieron" (the OPEN bullet at the top is this phase), "Navegación del Flap", and the A-button gotchas
   (points 5 and 6). `docs/claude/nivel2-y-ui.md`: "Flap UI" and "Wardrobe tab" (the prefab's two bands).
4. Code: `UI/FlapManager.cs` (all of it), `UI/UISelector.cs` (selection, the one-frame retry and its
   generation counter), `Player/PlayerController.cs` `CheckControls` (`menuAbierto` and the
   `Time.timeScale > 0` gates), the other `IsMenuOpen` readers (`Inventory/InventorySlot.ToggleGear`,
   `Player.GetWetOnceTheMenuCloses`, `TriggerS/TriggerOrigami.CanAutoPrompt`), `UI/HoverDetector.cs`
   (a HUD image whose click toggles the Flap), `UI/WardrobeDisplay.cs` (rebuilds in `OnEnable`),
   `UI/FlapDisplayButton.cs`.
5. `Assets/Prefabs/UI/FlapManager.prefab`, to understand it, not to edit it: the root's children are the
   HUD strip (pull tab `Tirita fondo`, `Vida`, `PapelAmmoHUD`, `PaginaActualHUD`, `HoverDetector`, at
   negative local y, always on screen) and the menu (`FondoGrande`, the five `*Display`s, the five
   `FlapDisplayButton*` tab instances, `SeguroseguroOverlay`, `FlapTabHint`), which sits off screen at the
   top while closed.

## The diagnosis (spec F6, from reading the code; reproduce it with Diego first if you can)

The EventSystem navigates with `Horizontal`/`Vertical` (arrows, WASD **and** the stick) and submits with
`Interact` (E/Enter/A). The active display stays live while the Flap is closed. So any Selectable left
selected after closing gets driven by walking: left/right moves a selected slider (brightness, contrast,
volume), and E/A presses a selected button, Exit included. Ways a selection survives closing:

1. **The close slide.** `_isOpen` only turns false when the 0.5 s slide ends, and `Update` keeps reading
   R1/L1/B until then. L1 is also sprint: "close the menu and start running" fires `CambiarTab` ->
   `ShowDesiredDisplay`, which selects inside the display AFTER `CloseFlap` cleared the selection.
   Joystick only (code selects only for joystick players).
2. **A click on a Flap Selectable on screen while closed.** 4.A closed the Wardrobe-tab case.

Two side effects of the same `_isOpen` timing:
- Gameplay input is free during the opening slide and blocked during the closing one.
- `ToggleFlap` decides on `_isOpen`, so pressing Esc during the opening slide restarts the opening
  instead of closing it: a slide can't be reversed.

## Scope, in order

- **#153 (4.B) Flap states** (FR-301, FR-302, FR-306):
  - `enum FlapState { Closed, Opening, Open, Closing }` replaces `_isOpen`. `MoveFlap` sets
    `Opening`/`Closing` when it starts and `Open`/`Closed` when it ends.
  - `ToggleFlap` reads the state (see question 2).
  - Menu input (`Update`: B, R1/L1, the exit confirm's B) and every selection made by code
    (`CambiarTab`, `ShowDesiredDisplay`'s `SeleccionarDentroDe`, `SeleccionarDisplayVisible`, `BTN_No`,
    `BTN_Salir`'s confirm selection) happen only in `Open`.
  - Leaving `Open` clears the selection (`UISelector.Limpiar`, which also cancels a pending retry).
  - `IsMenuOpen` keeps its name and its callers, and follows question 1. Add `IsFullyOpen` for "state ==
    `Open`" where a caller needs it.
- **#154 (4.C) A closed Flap can't be clicked or navigated** (FR-303, FR-305):
  - A `CanvasGroup` on each menu root (`_flapDisplays[i].display`, its `flapButton`, `_seguroOverlay`),
    added by code in `Awake` when missing. No prefab edits.
  - `blocksRaycasts` only in `Open`; `interactable` false only while `Closed` (see the gotcha below).
  - The safety net: while not `Open`, if `EventSystem.current.currentSelectedGameObject` sits under a menu
    root, call `UISelector.Limpiar()` and warn once per object, naming it.
  - The HUD strip is never touched: the pull tab and `HoverDetector` must open the Flap from any state.
- **Close-out** (below).

## Ask Diego before building (each has a recommended default)

1. **Q14, gameplay input during the slides.** Default: the menu owns input from the moment it starts
   opening until the moment it starts closing (`IsMenuOpen` = `Opening` or `Open`). So Esc freezes Kami
   at once and closing gives her back at once, the reverse of today.
2. **Reversing a slide.** Default: a toggle mid-slide reverses it. Esc while opening closes, Esc while
   closing reopens, from wherever the paper is. The slide already lerps from the current position, so
   it's just `ToggleFlap` reading the state.
3. **Should `InventorySlot.ToggleGear` require `IsFullyOpen`** instead of `IsMenuOpen`? Default: yes. Gear
   changes only from a fully open menu, which is also the only time its buttons take clicks after 4.C.

## Decided rules: don't reopen these

- 4.A stays: every tab button is inactive while the Flap is `Closed`, shown in `OpenFlap`, hidden when
  the closing slide ends (`SetTabsVisible`).
- One state enum is the only source of truth: nothing infers "open" from a position or from a slide.
- Code-only wiring (like `UISelector`, `GamepadCursor`): no prefab or scene edits in this phase.
- Esc/O/I/U are still not gated by `PlayerController` (they're what opens and closes the Flap).
  `OpenQuests`/`OpenInventory`/`OpenSettings` keep calling `ShowDesiredDisplay` BEFORE `ToggleFlap`.
- Code selects only for joystick players (`UISelector.SeleccionarPrimeroSiJoystick`). B closes, R1/L1
  cycle `_flapDisplays` by index (`FlapDisplay.number` is the index), and a display with nothing navigable
  falls back to its tab button.
- No gear persistence between levels (Diego, 2026-10-05).

## Gotchas already paid for (or found by reading the code)

- **`CanvasGroup.interactable = false` switches every Selectable under it to its Disabled tint** (0.78
  gray, half alpha). While `Closed` the paper is off screen, so it's never seen. During the slides it
  would flash, so the slides rely on "no raycasts + no selection" instead.
- **`MoveFlap` uses `Time.deltaTime`, and sets `Time.timeScale = 1` at the start of every slide** (0 once
  `Open`). Keep the slides at timeScale 1, or move them to `unscaledDeltaTime` deliberately. A slide run
  at timeScale 0 never moves.
- `ShowDesiredDisplay` must keep working while `Closed`: it decides which display shows when the Flap
  opens. Only its selection is gated.
- `SeleccionarDentroDe` falls back to the tab button, and tabs are inactive while `Closed`. Selecting
  only in `Open` keeps that consistent.
- `UISelector.SeleccionarPrimero` schedules a retry one frame later. `Limpiar()` bumps the generation and
  cancels it. Leaving `Open` must call `Limpiar()`, or the retry can re-select after you cleared.
- `WardrobeDisplay` rebuilds its buttons in `OnEnable`, and FlapManager selects the first one right after
  showing the tab. Keep that order.
- `Selectable.Select()` gives focus, it doesn't highlight. Never use it to show state.
- `PlayerController` also gates A/B on `Time.timeScale > 0`. That's fine; don't remove it.
- `Player.GetWetOnceTheMenuCloses` waits for `!IsMenuOpen`. With question 1's default it fires when the
  closing slide starts, which is what we want.
- `EventManager` swallows handler exceptions into a log, so log decisions: `Debug.Log($"[FlapManager] ...")`.
- **Replacing `_isOpen` is a deletion.** List every reader before cutting, and re-list the methods left
  afterwards. A missing `Update()` compiles clean.
- `FlapManager.cs` is LF and has trailing whitespace on some lines. The Edit tool trims trailing
  whitespace on the lines it rewrites, so check the diff stays surgical.

## Working rules

- English for everything new (code, comments, docs) and in replies to Diego. Leave existing Spanish
  alone, except a comment that becomes false: fix it.
- Comments explain the non-obvious why. Braces always, guard clauses that warn,
  `[SerializeField] private` + `[Tooltip]` for anything tunable.
- Do the mechanical work yourself. Ask Diego about decisions, not labour. Agents: none unless Diego asks.
- Triangulate the code first. If the spec doesn't match the code, stop and ask with a recommended default.

## Verification

- `python tools/compile-check.py` after each step. Known baseline warnings: `JumpFloodOutlineRenderer`
  CS0162, `HongueroTiburcioDialogueTrigger` CS0414. Compilation only: say clearly what wasn't run.
- Diego's playtest (spec "Verification", Phase 4, plus #152):
  - No tab ever shows during gameplay. Every tab, the Wardrobe included, works with the Flap open.
  - With a joystick: close the Flap and immediately hold L1, then walk and press A for a while. No slider
    moves, and nothing in the menu gets pressed.
  - With the mouse: open with the pull tab, click a slider, close with Esc, walk. Nothing changes.
  - Esc mid-slide reverses it (if question 2 says so). Fast open/close, the exit confirm with B, and
    R1/L1 all still work.
  - Kami's input follows question 1.
  - Console: no FR-305 warning in normal play. One would mean a leak path still exists: report it.

## Close-out

- Docs:
  - `docs/claude/controles-y-gamepad.md`: turn the OPEN focus-leak bullet into "resolved" with the real
    cause, and update "Pausa del Flap" (`IsMenuOpen` semantics, the states).
  - `docs/claude/nivel2-y-ui.md`: the Flap UI section.
  - `CLAUDE.md`: the Kami Gear status line.
- Spec and tasks: mark Phase 4 in `tasks.md` ("Phase 4 as built", with any changes vs. the spec and why),
  update the spec status, and record Diego's answers as Q14-Q16.
- `/graphify Assets/Scripts --update` from the repo root, then copy `graph.json`, `GRAPH_REPORT.md`,
  `graph.html` and `manifest.json` into `Assets/Scripts/graphify-out/`. Both folders are gitignored.
- Leave everything uncommitted for Diego's review. When he has played it and asks: commit and push, post
  a status comment on #152-#154 and close them, comment on #46 (close it only if Diego says #41.3 is fully
  covered), and comment on epic #139.
