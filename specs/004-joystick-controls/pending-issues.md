# Joystick Controls — Pending Issues

**Branch**: `feature/joystick-controls`

**Date documented**: 2026-09-08

Found during user testing (Diego playing with gamepad controls). These are bugs and polish items blocking merge to main; marked for fix before **Oct 15 (M4)**.

---

## P1 (Critical) — Blocks Gameplay

### Issue #41.1: UI Navigation in Flap Menu is Broken — RESOLVED

**Category**: Feature-Breaking / UX

**Severity**: P1 — menu is unnavigable with joystick, critical friction for gamepad users.

**What happened**:
- No way to navigate to the close button of the Flap (ESC/Start to open, but no way to close with joystick once open).
- The 4 section tabs (Tareas/Morral/Settings/Controles) all show the same amber color — not visually distinct like they used to (they had individual colors that made navigation clear).
- No clear visual indicator of which inventory slot is currently selected.
- No clear visual indicator of which settings slider is currently selected.
- Once inside Tareas or Controles sections, **there is no way out** — stuck with no selectable elements and no button to exit.

**Fix (2026-09-08)**:
- `Assets/Scripts/UI/FlapManager.cs`: new `Update()`, only active while `_isOpen`, reads
  `InputHub.TabSiguienteDown`/`TabAnteriorDown` (R1/L1) to cycle `_flapDisplays` via a new
  `CambiarTab(direccion)` (hand-rolled modulo, wraps both directions), and
  `InputHub.AtaqueGamepadDown` (B) to close the Flap. B is contextual with the exit-confirm
  dialog: if `_seguroOverlay` is active, B answers it (`BTN_No()`) instead of closing the
  whole Flap out from under it. `ShowDesiredDisplay` now stamps `_currentDisplayIndex =
  flapDisplay.number` on every call site (BTN_\*, Open\*, and the new cycling), so R1/L1
  always start from whatever tab is actually showing.
- New input axes in `ProjectSettings/InputManager.asset`: `TabSiguiente` (joystick button 5
  / R1 — was unbound, camera toggle dropped its R1 binding a while ago) and `TabAnterior`
  (joystick button 4 / L1 — same physical button as `Run`, but a distinct axis name, no
  conflict since `PlayerController` is now gated while the Flap is open, see #41.3).
- The "nothing navigable → fall back to the display's own tab button" fallback
  (`SeleccionarDentroDe`) already existed from an earlier session and still covers
  Tareas/Controles — verified, not re-implemented.
- Found and fixed two real gaps the "highlight visible" note in this doc's gotchas claimed
  were already closed: `Assets/Prefabs/UI/SliderFlap.prefab` and
  `Assets/Prefabs/UI/InventorySlot.prefab` still had `m_SelectedColor: {0.96, 0.96, 0.96}`
  (the exact invisible-gray bug documented for `Button.prefab`) — the amber fix never
  propagated to them since each Selectable owns its own `m_Colors` block. Both now use the
  same amber as `Button.prefab` (`{1, 0.7607843, 0.2509804}`).
- The 4 tab buttons (`FlapDisplayButtonQuests/Settings/Controls/Inventory`, nested instances
  of the same source prefab) already had a distinct `m_Colors.m_HighlightedColor` override
  per instance (visible on mouse hover) but NONE had `m_Colors.m_SelectedColor` overridden —
  so all 4 fell back to the shared base prefab's color when navigated with joystick. Fix:
  copied each instance's own `m_HighlightedColor` RGB into a new `m_SelectedColor` override
  in the same `PrefabInstance` block (YAML surgery in `FlapManager.prefab`, no changes to
  the base tab-button prefab). Mouse hover and joystick selection now show the same color
  per section.

**Still needs**: manual confirmation in the Editor with a gamepad — R1/L1 cycling through
all 4 tabs in both directions, B closing the Flap, B answering the exit-confirm dialog when
it's open instead of closing the Flap behind it, and the 4 tabs showing distinct colors both
on hover (mouse) and on select (joystick).

**Files changed**:
- `Assets/Scripts/UI/FlapManager.cs`
- `ProjectSettings/InputManager.asset`
- `Assets/Scripts/Input/InputHub.cs` (`TabSiguienteDown`/`TabAnteriorDown`)
- `Assets/Prefabs/UI/FlapManager.prefab`, `Assets/Prefabs/UI/SliderFlap.prefab`,
  `Assets/Prefabs/UI/InventorySlot.prefab`

---

### Issue #41.2: Cannot Attack (B Button Unresponsive) After Origami in Level 1 Page 2 — RESOLVED

**Category**: Bug / Input

**Severity**: P1 — blocks combat, breaks gameplay loop.

**Root cause (confirmed 2026-09-08)**: race between two independent `Update()` loops reading the
same `InputHub.AtaqueGamepadDown` press. `MultipleRectCheck.CancelarDown()` (origami, B cancels)
and `PlayerController.CheckControls()` (B attacks) both read that button with zero coordination —
unlike A, which is arbitrated in one place via `PlayerController._gamepadInteractua`. Unity does
not guarantee `Update()` order between unrelated MonoBehaviours (no `ScriptExecutionOrder.asset` in
this project), so on frames where `MultipleRectCheck.Update()` happens to run **before**
`Player.Update()`: the B press cancels the origami first (`EndOrigami` → `Evento.OnOrigamiEnd` →
`Player.EndOrigamiCast` → `SetState(Idle)`, all synchronous), and by the time
`PlayerController.CheckControls()` reads the *same* B press a few lines later in the same frame,
`Player.CanAttack()` already sees `CurrentState == Idle` and lets a real attack fire
(`Player.OnPrimaryClick()` → `_view.StartAttack()`). This consumes `_readyToAttack` (only reset by
the Spine `HandleAttack` event on the attack animation) for a real, unintended attack the player
never asked for — reading as "B doesn't work" for as long as that phantom attack takes to resolve.

**Fix**: `Assets/Scripts/Player/Player.cs` — `SetState()` now records the frame number whenever
`CurrentState` transitions OUT of a state that blocks attack (`Casting`/`ReceivingReward`/`Dead`/
`RidingPage`) into one that doesn't. `CanAttack()` refuses to attack on that exact frame. This makes
the guard independent of `Update()` execution order: whichever system's `Update()` runs first, the
same physical B press can never both close a blocking state and start an attack in one frame.

**Expected behavior**:
- B should work for closing origami only during origami. It should work for attack normally everywhere else. Kami should not be able to attack during the origami.
- Closing origami with B should not block future B presses.

**How to reproduce (before the fix)**:
- Enter Level 1.
- Find an origami pedestal.
- Open origami (A).
- Press B to cancel/retry at least once.
- Close origami (press E or walk away).
- Try to attack: B is unresponsive.

**Still needs manual verification in the Editor** (not something `compile-check.py` can confirm):
open an origami, cancel with B a few times, close it, attack immediately — B should always work,
regardless of which frame ordering Unity picks that session.

---

### Issue #41.3: Cannot Move / Cannot Do Anything During Flap Pause — RESOLVED

**Category**: Bug / Input

**Severity**: P1 — gameplay is unfrozen during pause; input should be blocked.

**What happened**:
- While the Flap menu is open (paused state), the player can still:
  - Move Kami left/right with A or left stick (she flipflops but doesn't move — no forward motion, just facing flips).
  - Trigger origamis by pressing A near a pedestal.
  - Do other gameplay actions.
- `Time.timeScale = 0` is set, so no motion happens, but the **input is being accepted and processed**, which shouldn't happen.

**Root cause (confirmed 2026-09-08)**: `LevelManager.agency` is never assigned `false`
anywhere in code (searched explicitly) and `LevelManager.inDialogue` is never touched by
`FlapManager` — neither existing gate covered "the Flap is open." Concretely,
`InputHub.InteractDown` (shared by keyboard E and gamepad A) triggered
`Evento.OnPlayerPressedE` with zero pause gate, letting origamis/solapas fire through the
paused menu; movement/jump were only gated by `inDialogue`, not by the pause; and keyboard
attack (`Fire1`) had no pause gate at all (only the gamepad attack path checked
`Time.timeScale > 0f`).

**Fix**: `Assets/Scripts/UI/FlapManager.cs` gained a public `IsMenuOpen => _isOpen` property
(the same flag that gates `Time.timeScale`). `Assets/Scripts/Player/PlayerController.cs`
computes `bool menuAbierto = FlapManager.Instance != null && FlapManager.Instance.
IsMenuOpen` once per `CheckControls()` and uses it to skip: world-interact
(`InputHub.InteractDown` → `OnPlayerPressedE`), attack (both keyboard and gamepad — the
gamepad's existing `Time.timeScale > 0f` check stays as a belt-and-suspenders), and the
movement/jump block (added to the same early-return that already covered
`LevelManager.inDialogue`). Esc/O (`OpcionesDown`), I/U (`Inventario`/`QuestsDown`), Correr
and Mute are intentionally NOT gated — they're what opens/closes the Flap itself, or are
harmless no-ops while `Time.timeScale == 0`.

**Still needs**: manual confirmation in the Editor — stand on an origami pedestal or near a
solapa, open the Flap, try to move/attack/interact (nothing should happen), close the Flap,
confirm everything works again immediately.

**Files changed**:
- `Assets/Scripts/UI/FlapManager.cs` (`IsMenuOpen` property)
- `Assets/Scripts/Player/PlayerController.cs` (`menuAbierto` gate)

---

## P2 (High) — UX & Polish

### Issue #41.4: Main Menu Not Navigable with Joystick — RESOLVED (round 2, real root cause)

**Category**: UX

**Severity**: P2 — blocks start game flow for gamepad-only players.

**What happened**:
- Main menu (title screen) has buttons (Start Game, Language select) but they didn't respond to joystick navigation.
- No visual selection indicator when joystick was plugged in.
- Could only interact with mouse clicks.

**Expected behavior**:
- Left/right arrows or D-pad should navigate between language buttons.
- A should confirm selection.
- Start button should also confirm (submit).

**First fix attempt (commit `e848cc4`) was declared resolved but WASN'T** — a previous session read the code
(`MainMenuManager.SeleccionarBotonNuevoJuego()`, called once from `Start()`, selecting `NewGameButton` via
`UISelector.SeleccionarPrimeroSiJoystick`) and concluded it was fine without playtesting it. Diego played it and
the joystick still didn't navigate the menu.

**Real root cause (confirmed 2026-09-08, round 2)**: `Start()` only ever tries the selection ONCE. That single
attempt is gated by `UISelector.SeleccionarPrimeroSiJoystick` → `InputHub.HayJoystickConectado`, which calls
`Input.GetJoystickNames()`. On Windows, especially with XInput gamepads, that enumeration can still be empty for
the first several frames after the Player starts even though the controller is physically plugged in — and the
main menu is the FIRST scene the game loads, i.e. exactly the moment most likely to land inside that window. If
`Start()` runs before the OS finishes enumerating the device, `HayJoystickConectado` returns `false`, the
selection attempt is skipped, and — unlike the Flap, which re-selects every single time it opens
(`ShowDesiredDisplay`/`MoveFlap`) — nothing else ever retries: the selection is lost for the rest of that session
even though the gamepad is right there. Subscribing to `InputHub.OnDeviceCambio` would NOT have fixed this either:
that event only fires from inside code that actively reads `InputHub.UltimoDeviceFueJoystick` on a given frame,
and nothing in the Main Menu was reading it after the failed `Start()` — structurally the same bug already fixed
once this session in `CursorManager` for issue #41.7 (a passive subscriber isn't enough; something has to poll
every frame).

**Fix**: `MainMenuManager.Update()` now calls a new `ReintentarSeleccionSiHaceFalta()` every frame. It re-runs
`SeleccionarBotonNuevoJuego()` while (a) nothing is selected in the `EventSystem` yet, and (b) there's evidence of
a joystick (`InputHub.HayJoystickConectado || InputHub.UltimoDeviceFueJoystick`) — the second check avoids calling
`GameObject.Find` every frame for a keyboard/mouse-only session. It stops polling as soon as a selection exists,
or as soon as `_dialogueStarted` flips true (the auto-dialogue deliberately clears selection via
`UISelector.Limpiar()` at that point — re-selecting afterwards would leave a phantom button that the joystick's B
would keep "pressing" during the dialogue). New `[MainMenuManager]` logs on `Start()` and on each successful
selection include `Time.frameCount` so the exact frame the joystick got detected can be read from the Editor
console.

**Files changed**: `Assets/Scripts/Managers/MainMenuManager.cs` only (`Update()`, new
`ReintentarSeleccionSiHaceFalta()`, extra logging). `InputHub.cs` was deliberately left untouched — the fix lives
entirely on the polling side, not inside the joystick-detection facade.

**What the first attempt got right and is still valid, unchanged**: `Assets/Prefabs/UI/Button.prefab` has
`m_Navigation.m_Mode: 3` (Automatic), and `MainMenu.unity`'s `EventSystem` has `m_SubmitButton: Interact` /
`m_CancelButton: Options`, already mapped to gamepad A / Start — so once the initial selection lands, stick/arrow
navigation between the 3 language buttons and Start, and A to confirm, both work with no further changes needed.

**Still needs**: manual confirmation in the Editor, in BOTH of these cases — (1) gamepad already plugged in before
the game starts, and (2) gamepad plugged in only after the main menu has already loaded. Both should end up
navigable; check the `[MainMenuManager]` / `[UISelector]` console logs to see which frame picked up the joystick.

---

### Issue #41.5: Origami Tooltip Should Say "Tocá A y Arrastrá..." With Joystick — RESOLVED

**Category**: UX / Localization

**Severity**: P2 — gamepad user gets confusing tooltip text.

**Root cause (confirmed 2026-09-08)**: there are actually TWO origami tooltips, and only one was
broken. The "start" tooltip (`TriggerOrigami`'s `tooltipTextToShow`, "Mantené E para comenzar un
origami") already translated fine with joystick — its "Mantené E" already matched
`InputPromptSystem.RxTeclaConVerbo` (verb + key letter), turning into "Mantené (A) para comenzar".
The actual broken one is the SECOND tooltip, shown once the minigame starts:
`MultipleRectCheck.StartOrigami` calls `TooltipManager.ShowTooltip(origami.tooltipMessage, ...)`,
and `Origami.tooltipMessage` ("Arrastrá la flecha verde hasta el objetivo...") has no key letter or
recognized legacy word at all (mouse dragging doesn't mention any button), so none of the existing
legacy tokens matched — the text passed through `InputPromptSystem.Procesar` completely untouched
regardless of device. Not a placeholder issue: this text is legacy (no `{INPUT:*}`) in both cases.

**Fix**: `Assets/Scripts/Input/InputPromptSystem.cs` — added `RxArrastrarAlInicio`, a new anchored
legacy-token regex (same style as `RxTeclaAlInicio`) matching the drag verb at the start of the
string in all 3 languages (`Arrastrá`/`Arrastra` es, `Drag` en, `Arraste` pt — covers both the
`origami_guide` TooltipTable key used by `OrigamiRoute 1-Easy.prefab` and the hardcoded literal
Spanish text the other 9 `OrigamiRoute *.prefab` files use instead of that key, since both variants
start with the same verb). With joystick it prepends the grab prompt: "Tocá (A) y arrastrá..." /
"Press (A) and drag..." / "Toca (A) e arraste...". With keyboard/mouse the text is untouched (zero
regression — verified via `Procesar`'s early-return fast path, which only runs legacy translation
when a joystick is active).

**Verified**: `python tools/compile-check.py` compiles clean (only the 2 known baseline warnings).
Regex logic verified against the real .NET engine (PowerShell) for all 3 languages' exact table/
prefab strings.

**Separately flagged (not part of this fix)**: 9 of the 10 `OrigamiRoute *.prefab` files hardcode
the raw Spanish sentence in `tooltipMessage` instead of the `origami_guide` key (only `1-Easy` uses
the key correctly) — so EN/PT players see Spanish text for this tooltip regardless of language
setting. Unrelated to the joystick fix (the new regex covers the hardcoded text too), but worth a
follow-up prefab edit; flagged as a separate background task.

**Still needs**: manual confirmation in the Editor with a gamepad plugged in — open an origami
pedestal, check the "start" tooltip already says "(A)", then start the minigame and check the
"drag" tooltip now says "Tocá (A) y arrastrá...".

---

### Issue #41.6: Controles Section Text Should Update for Joystick — ALREADY WORKING (verified 2026-09-08)

**Category**: UX / Localization

**Severity**: P2 — player sees keyboard text even with joystick active.

**What happens**:
- The "Controles" tab in Flap shows control mappings (e.g., "WASD = Mover", "SHIFT = Correr").
- These are static text or a table, not dynamic prompts.
- With joystick active, it should say "L-Stick = Mover", "L1 = Correr", etc.

**Investigation (2026-09-08)**: the Controles tab's content is a single localized string
(table `UITexts`, key `controlsText`, one "TECLA - Acción" line per control — confirmed by
reading `Assets/Localization Settings/Tables/UITexts_es/en/pt.asset` directly), written by a
`LocalizeStringEvent` wired straight to `TMP_Text.set_text` in `Assets/Prefabs/UI/
FlapManager.prefab` (GameObject `ControlsDisplay` → `texto`) — no dedicated
`ControlsDisplay.cs`/`FlapDisplay_Controles` component exists or is needed. Two pieces that
already existed **before this session** (not part of any Tanda 1/2 diff) already cover this:

1. `Assets/Scripts/UI/LocalizedText.cs` (commit `94a1196`, "un solo camino de texto
   localizado, y que se reescriban al cambiar de device") hooks EVERY `LocalizeStringEvent`
   in the scene by code (`EngancharLocalizeStringEvents`), adds a listener that re-processes
   the text through `InputPromptSystem.Procesar` and registers it to be rewritten on
   `InputHub.OnDeviceCambio`. Its own comments literally name "la pantalla de controles del
   Flap" as one of the texts this fixes.
2. `InputPromptSystem.RxTeclaEnListaDeControles` (pre-existing regex, NOT part of Tanda 1's
   diff to that file — verified with `git diff`) matches exactly the "LETRA - Acción" format
   anchored to line start, and the other legacy-token regexes (`RxWasd`, `RxEspacio`,
   `RxEsc`, `RxClickOCtrl`, `RxCamaraMultiPalabra`) cover the rest of that same list's tokens
   in all 3 languages.

**Verdict**: no code change needed for this issue. Compiles clean (untouched).

**Known gap, NOT fixed (out of scope this session — `InputPromptSystem.cs` was owned by
another agent in this same batch)**: `RxTeclaEnListaDeControles`'s charset is `[EUI]` only.
The lines "O - Abrir Controles" and "M - Control de sonido" don't translate with joystick.
"M" has no gamepad equivalent by design (Mute is keyboard-only, no button to spare). "O"
should map to `(Start)` (same `Options` axis as Esc) but doesn't yet — flagged as a
follow-up background task rather than touched directly.

**Still needs**: manual confirmation in the Editor — open the Flap's Controles tab with a
gamepad active, confirm the list shows joystick prompts (except the known O/M gap above).

---

### Issue #41.7: Cursor Visibility / Auto-Hide Not Working

**Category**: UX / Polish

**Severity**: P2 — cursor feels orphaned or out of context.

**What happens**:
- Starting the game with joystick plugged in, the cursor does not appear initially (good).
- Moving the mouse makes the cursor appear (good).
- Switching back to joystick does not hide the cursor again; it stays visible indefinitely.
- No timeout to auto-hide if the cursor hasn't moved for a few seconds.

**Expected behavior**:
- Cursor should hide if:
  - Player starts with joystick (already works).
  - Player switches from mouse back to joystick (needs timer).
  - Cursor is inactive for N seconds (~5-10s).
- Cursor should **not** hide during origami (the mini-game might use mouse even with joystick).

**Files to check**:
- `Assets/Scripts/Input/GamepadCursor.cs` (manages cursor visibility).
- `Assets/Scripts/Input/InputHub.cs` (detects device switch via `UltimoDeviceFueJoystick`).
- `Cursor.visible` / `Cursor.lockState` control.

---

### Issue #41.8: Chino Dialogue Has Duplicate "[...]" Marker

**Category**: Bug / Localization

**Severity**: P2 — text redundancy, minor but noticeable.

**What happens**:
- NPC Chino's dialogue contains "[...]" twice in one place (e.g., "Come here [...] to trade [...]").
- Should appear only once.

**Expected behavior**:
- Dialogue string should have "[...]" appear once (likely at the end or as a stylistic pause).

**Files to check**:
- `Assets/Scripts/Dialogos/ChinoDialogueTrigger.cs` (or the DialogueSO asset for Chino).
- Localization tables: `DialogueTable_es.asset`, `DialogueTable_en.asset`, etc.

---

## P3 (Nice to Have) — Polish

### Issue #41.9: Text Should Render Sequentially (Typewriter Effect)

**Category**: UX / Polish

**Severity**: P3 — nice-to-have for feel.

**What happens**:
- Dialogue and tooltip text appears all at once (instant).

**Expected behavior**:
- Text should appear sequentially (typewriter effect) at a fast speed (~20-30 chars/sec).
- This is a polish feature that adds character and pacing.

**Implementation idea**:
- `DialogueManager.WriteText()` already exists — it handles line-by-line advancement.
- Could add a new method `WriteTextTypewriter()` that reveals chars one by one instead of all at once.
- Or use TextMeshPro's built-in animation scripting to achieve this effect.

**Files to check**:
- `Assets/Scripts/Dialogos/DialogueManager.cs` (see how `WriteText()` is implemented).
- Potentially use TextMeshPro's `textInfo` and character masking or `maxVisibleCharacters`.

---

## Summary Table

| Issue | Category | Severity | Blocker? | Estimated Effort |
|---|---|---|---|---|
| #41.1 Flap navigation | UX | P1 | No | RESOLVED (pending manual verification) |
| #41.2 B button stuck | Bug | P1 | No | RESOLVED (pending manual verification) |
| #41.3 Input during pause | Bug | P1 | No | RESOLVED (pending manual verification) |
| #41.4 Main menu nav | UX | P2 | No | RESOLVED for real, round 2 (see below — `e848cc4` alone wasn't enough) |
| #41.5 Origami tooltip | UX | P2 | No | RESOLVED (pending manual verification) |
| #41.6 Controles text | UX | P2 | No | RESOLVED — already worked, no code change (small O/M gap flagged separately) |
| #41.7 Cursor auto-hide | Polish | P2 | No | 2h |
| #41.8 Chino duplicate [...] | Bug | P2 | No | 30m |
| #41.9 Typewriter text | Polish | P3 | No | 3-4h |

**Remaining open work**: #41.7, #41.8 (P2), #41.9 (P3). All P1s resolved pending manual
gamepad verification in the Editor.

---

## Ronda 2 (2026-09-08) — bugs reales de gamepad tras el fix de #41.1

Diego jugó con gamepad real sobre los fixes de la ronda anterior (R1/L1/B ya andaban) y
reportó 3 cosas puntuales que seguían rotas dentro del mismo Flap. Las tres comparten
`FlapManager.cs`/prefabs relacionados, resueltas en una sola sesión.

### Issue #41.10: El botón de la tirita (abrir/cerrar Flap) no responde a A ni resalta — RESOLVED

**Categoría**: Bug / UX — navegable pero inerte.

**Reporte de Diego**: *"el boton de flap, si bien puedo navegar hasta él, con A no se
dispara (o sea, no se cierra el menu). además, no se resalta cuando lo tengo focuseado."*

**Causa raíz confirmada**: el GO "Tirita fondo" (`Assets/Prefabs/UI/FlapManager.prefab`,
`--- !u!114 &342370284742760076`) SÍ es un `UI.Button` real, navegable
(`m_Navigation.m_Mode: 3`), pero tenía dos huecos:
1. `m_OnClick.m_PersistentCalls.m_Calls: []` — vacío. Nunca se cableó ningún método.
2. `m_Colors.m_SelectedColor: {1, 1, 1, 1}` — igual al color normal (blanco), así que
   aunque el foco SÍ llegaba, no había ningún cambio visual que lo mostrara.

**Fix**:
- `Assets/Scripts/UI/FlapManager.cs`: nuevo método `BTN_ToggleFlap()` (sin argumentos),
  wrapper de `ToggleFlap(params object[] parameters)`. Necesario porque el Editor de
  Unity solo lista, para un `Button.OnClick` sin genéricos, métodos de 0 parámetros —
  `ToggleFlap(params object[])` tiene 1 parámetro en runtime (el array) y nunca iba a
  aparecer en el dropdown ni bindear por reflection con `m_Arguments` vacío.
- `Assets/Prefabs/UI/FlapManager.prefab`: `m_OnClick` del botón de la tirita ahora llama
  `FlapManager.BTN_ToggleFlap` (target = el `FlapManager` del mismo prefab), y
  `m_SelectedColor` pasa a ámbar (`{1, 0.7607843, 0.2509804, 1}`, el mismo que
  `Button.prefab` y el resto de esta ronda de fixes).

**Todavía falta**: confirmar a mano en el Editor con gamepad real — navegar hasta la
tirita, ver el resalte ámbar, apretar A y confirmar que abre/cierra el Flap igual que B.

**Archivos cambiados**: `Assets/Scripts/UI/FlapManager.cs`,
`Assets/Prefabs/UI/FlapManager.prefab`.

---

### Issue #41.11: Los handles de los sliders ("gallinas") no se animan al navegar con joystick — RESOLVED

**Categoría**: Bug / UX — animación rota en un solo device.

**Reporte de Diego**: *"con las gallinas me refiero a el handle de los sliders [...] los
sliders tienen de transition una Animation, y el animator tiene el controller
'ChickenHandle'. [...] quisiera que, asi como cuando la drageas con el mouse, cuando esta
focuseada de joystick, se anime."*

**Causa raíz confirmada**: `Assets/2D/UI/Button Animations/Chicken Handle.controller` es
un `AnimatorController` con 5 estados (Normal/Highlighted/Pressed/Selected/Disabled),
cableado a los 5 triggers estándar de `Selectable.m_AnimationTriggers` (uGUI moderno
dispara `Selected` cuando el `EventSystem` selecciona un objeto por navegación de
joystick, un trigger DISTINTO de `Highlighted`, que solo dispara con hover de mouse). Las
transiciones estaban bien armadas — el problema era el contenido: el `AnimatorState`
"Selected" (`fileID 1048294302174715532`) apuntaba a un `AnimationClip` "Selected"
(`fileID 2753280035520094638`) con TODAS las curvas vacías (`m_ScaleCurves: []`,
`m_PPtrCurves: []`) — un placeholder que nunca se llenó. El clip que sí tiene la
animación real (escala 1.1x + el spritesheet de la gallina caminando, 9 frames vía
`m_PPtrCurves`) es el del estado "Pressed" (`fileID -8430742404652959548`), que es
justamente lo que se ve al arrastrar el handle con mouse.

**Fix**: una sola línea YAML en `Chicken Handle.controller` — el `m_Motion` del
`AnimatorState` "Selected" pasa de apuntar al clip vacío a apuntar al mismo clip que usa
"Pressed" (`fileID: -8430742404652959548`). Reusar el mismo `AnimationClip` desde dos
`AnimatorState` es un patrón válido de Unity; no hizo falta tocar transiciones ni
triggers. Como los 3 sliders (`_sliderBrillo`/`_sliderContraste`/`_sliderVolumen`) son
instancias de `Assets/Prefabs/UI/SliderFlap.prefab`, que referencia este
`AnimatorController` por GUID como asset de proyecto (no copiado por instancia), el fix
alcanza a los 3 de una sola vez.

**Todavía falta**: confirmar a mano en el Editor con gamepad — navegar a cada uno de los
3 sliders y verificar que la gallina anima (camina) igual que al arrastrarla con mouse.

**Archivos cambiados**: `Assets/2D/UI/Button Animations/Chicken Handle.controller`.

---

### Issue #41.12: Cartel nuevo "L1/R1 para cambiar de sección" arriba de los íconos del Flap — IMPLEMENTED

**Categoría**: Feature nueva / UX (pedido con dibujo de referencia, no un bug).

**Pedido de Diego**: un cartelito arriba de la columna de íconos de sección
(Tareas/Morral/Settings/Controles) que indique que L1/R1 cambian de sección — visible
SOLO con joystick (con teclado/mouse esos ejes ni siquiera tienen binding).

**Implementación**:
- `Assets/Scripts/Input/InputPromptSystem.cs`: nueva `Accion.CambiarTab`. Placeholder
  `{INPUT:cambiarseccion}` (alias `cambiartab`/`changetab`/`switchtab`) resuelve a
  `"L1 / R1"` con joystick y a `""` con teclado (no hay tecla equivalente — el cartel que
  lo usa nunca se muestra con teclado, así que el string vacío nunca se ve).
- `Assets/Scripts/UI/SoloConJoystick.cs` (nuevo): `MonoBehaviour` chico que hace
  `gameObject.SetActive(InputHub.UltimoDeviceFueJoystick)` en `OnEnable` y se
  re-evalúa en cada `InputHub.OnDeviceCambio`, mismo patrón que
  `TutorialPromptVisual`. Reusable para cualquier otro cartel joystick-only a futuro.
- Localización: nueva key `FlapCambiarSeccion` en `UITexts` (Shared Data +
  `UITexts_es/en/pt.asset`), texto `"{INPUT:cambiarseccion} para cambiar de sección"`
  (es) y equivalentes en/pt.
- `Assets/Prefabs/UI/FlapManager.prefab`: nuevo GameObject `FlapTabHint`, hijo directo
  de la raíz del Flap (mismo padre que los 4 `FlapDisplayButton`, no de un tab
  individual, para que se vea sin importar qué sección está abierta), posicionado arriba
  del ícono más alto de la columna (`x: -682.1` igual que los 4 botones, `y: 826`, los
  botones van de `y: 115` a `y: 716`). Componentes: `RectTransform` + `CanvasRenderer` +
  `TextMeshProUGUI` (copiado del patrón de `Assets/Prefabs/UI/DisplayTitleText.prefab`,
  fuente/material iguales, tamaño de fuente reducido a 26 y alineación centrada) +
  `LocalizeStringEvent` (apunta a la tabla `UITexts`, key `FlapCambiarSeccion`, mismo
  wiring `OnUpdateString → TMP_Text.set_text` que usan los títulos de sección) +
  `SoloConJoystick`. El hook automático de `LocalizedText.cs`
  (`EngancharLocalizeStringEvents`, corre en cada carga de escena y en cada cambio de
  device) engancha este `LocalizeStringEvent` sin código adicional — mismo mecanismo que
  ya traduce el resto de la UI del Flap.

**Todavía falta**: confirmar a mano en el Editor — el cartel debe estar OCULTO con
teclado/mouse, aparecer al activar el joystick, decir "L1 / R1 para cambiar de sección"
(y su traducción en/pt), y no superponerse visualmente con el ícono de Tareas (el más
alto de la columna) en ninguna resolución. Ajustar la posición Y a mano si se ve pegado.

**Archivos cambiados**: `Assets/Scripts/Input/InputPromptSystem.cs`,
`Assets/Scripts/UI/SoloConJoystick.cs` (nuevo), `Assets/Prefabs/UI/FlapManager.prefab`,
`Assets/Localization Settings/Tables/UITexts Shared Data.asset`,
`Assets/Localization Settings/Tables/UITexts_es.asset`,
`Assets/Localization Settings/Tables/UITexts_en.asset`,
`Assets/Localization Settings/Tables/UITexts_pt.asset`.

---

## Ronda 3 (2026-09-08) — dos bugs reportados por Diego jugando, no relacionados entre sí más que por compartir el mismo patrón de root-causing (input compartido sin arbitraje, y un caller que corre más seguido de lo pensado)

### Issue #41.13: Tooltips se abren y se cierran instantáneamente — RESOLVED

**Categoría**: Bug / Sistema de tooltips.

**Reporte de Diego**: *"sigue el issue de los tooltips que se cierran. este issue me tiene
bastante mal. me parece que el culprit es el tooltip del salto. creo que cuando se cierra
ese, hace que despues todos los demas no puedan abrirse (bah, se abren y se cierran
insta)."*

**Causa raíz confirmada**: `PlayerModel.UpdateVerticalState()` llamaba a
`Player.DestroyPaperPlaneHat()` cada vez que el player estaba grounded, con velocidad
vertical ≤0, y `augmentedJumpsLeft == 0` — sin chequear si el sombrero de papel seguía
puesto (`isPaperPlaneHat`). Como nada vuelve a subir `augmentedJumpsLeft` salvo agarrar el
sombrero de nuevo (solo se resetea en `GetPaperPlaneHat()`), esa condición queda `true`
PARA SIEMPRE una vez agotados los saltos aumentados por primera vez en la partida. Eso
dispara `DestroyPaperPlaneHat()` en CADA frame grounded del resto de la sesión — y esa
función llama `TooltipManager.Instance.HideTooltip()`, un hide GLOBAL de los 5 post-its de
color, sin ningún guard de "ya lo escondí, no hace falta de nuevo". Resultado: cualquier
tooltip que se mostrara después de la primera vez que se usó el sombrero se cerraba casi
al instante, sin relación aparente con el sombrero — el reporte de Diego sobre "el tooltip
del salto" apuntaba al lugar correcto (el sombrero SÍ tiene su propio tooltip, "TOOLTIP
PAPER SALTO"), solo que el mecanismo real era indirecto vía este hide global.

**Fix**: `Assets/Scripts/Player/PlayerModel.cs` — se agregó el guard
`_player.isPaperPlaneHat &&` antes del chequeo de `augmentedJumpsLeft == 0`. Ahora
`DestroyPaperPlaneHat()` dispara una única vez, en el aterrizaje real que agota el
sombrero, y no vuelve a dispararse hasta la próxima vez que se lo agarre y se lo vuelva a
gastar.

**Cómo reproducir (antes del fix)**: agarrar el sombrero de papel (recompensa de origami),
gastar todos los `augmentedJumpsLeft` saltando, aterrizar. De ahí en más, CUALQUIER
tooltip (post-its de color, tutorial, etc.) que se muestre se cierra solo casi al
instante, por el resto de la sesión.

**Verificado**: `python tools/compile-check.py` compila limpio (solo los 2 warnings de
baseline conocidos).

**Todavía falta**: confirmar jugando — agarrar el sombrero, gastar los saltos, aterrizar,
y verificar que los tooltips normales (post-its de color) ya no parpadean después.

**Archivos cambiados**: `Assets/Scripts/Player/PlayerModel.cs`.

---

### Issue #41.14: A abre el diálogo de la abuela al cerrar el victory overlay — RESOLVED

**Categoría**: Bug / Input — mismo patrón arquitectónico que #41.2 (dos `Update()` leyendo
el mismo botón sin coordinarse).

**Reporte de Diego**: *"tocar A para quedarme a explorar (en el victory overlay) me abre
el dialogo de la abuela. sera que el mismo input de A queda un poco y triggerea las dos
cosas de una? creo que ya habiamos tenido este problemita."*

**Causa raíz confirmada**: el botón "quedarme a explorar" del victory overlay
(`OverlayManager.BTN_ContinueGame()`) corre vía el Submit del EventSystem — el mismo eje
físico que Interact/A — y llama a `Unlock()`, que apaga `isLocked`/`inDialogue`.
`DialogueManager.ShowDialogue()` sí chequea `OverlayManager.isLocked` y
`LevelManager.inDialogue` antes de arrancar un diálogo nuevo, pero Unity no garantiza el
orden de `Update()` entre MonoBehaviours de objetos distintos (no hay
`ScriptExecutionOrder.asset`, mismo problema de fondo que #41.2): si el `Update()` del
EventSystem (que procesa el Submit y dispara `Unlock()`) corre ANTES que
`PlayerController.CheckControls()` en ese mismo frame, `Unlock()` ya bajó esos flags
cuando `CheckControls()` lee el MISMO apretón de A y dispara `Evento.OnPlayerPressedE` sin
nada que lo vete (el gate existente, `menuAbierto`, solo cubre el Flap, no los overlays de
victoria/derrota). Si el player seguía parado en el trigger de un NPC — la abuela, justo
ahí tras terminar su boss fight — `TriggerDialogue.Interact()` recibe ese mismo evento y
abre su diálogo: el guard de `ShowDialogue()` ya no frena nada porque los flags que
chequea ya están en `false` para ese momento.

**Fix**: mismo patrón que `Player._frameSalidaDeEstadoQueBloqueaAtaque` (#41.2).
`Assets/Scripts/UI/OverlayManager.cs` ahora graba en qué frame corrió `Unlock()`
(`_frameDesbloqueado`, expuesto como la propiedad `SeDesbloqueoEsteFrame`).
`Assets/Scripts/Player/PlayerController.cs` — `CheckControls()` veta el disparo de
`Evento.OnPlayerPressedE` si el overlay se desbloqueó ESE MISMO frame. Así el mismo
apretón de A nunca puede cerrar el overlay Y ADEMÁS abrir un diálogo del mundo, sin
importar qué orden de `Update()` elija Unity ese frame en particular.

**Cómo reproducir (antes del fix)**: completar el boss fight de la abuela hasta que
aparezca el victory overlay, con el player parado de forma que el trigger de diálogo de
la abuela siga activo, apretar A sobre "quedarme a explorar" — dependiendo del orden de
`Update()` de ese frame en particular, a veces se abre el diálogo de la abuela
inmediatamente después de cerrar el overlay.

**Verificado**: `python tools/compile-check.py` compila limpio (solo los 2 warnings de
baseline conocidos).

**Todavía falta**: confirmar jugando — repetir el boss fight varias veces, apretar A
sobre "quedarme a explorar" cada vez, confirmar que nunca abre el diálogo de la abuela.

**Archivos cambiados**: `Assets/Scripts/UI/OverlayManager.cs`,
`Assets/Scripts/Player/PlayerController.cs`.

---

## Notes for Next Session

1. **All P1s are code-complete, none manually verified with a real gamepad yet.** Before
   anything else: plug in a controller and walk through #41.1 (R1/L1/B in the Flap), #41.2
   (origami cancel spam then attack), #41.3 (try to break pause), #41.13 (paper plane hat
   then any other tooltip), and #41.14 (Abuela boss fight victory overlay, mash A) in the
   Editor.
2. **Test with input system changes** — every fix to input or state gates should be tested with both keyboard and gamepad to avoid regressions.
3. **A button context is fragile** — it's doing a lot (jump, interact, menu submit, and now
   also overlay Submit). Be extra careful not to break any of those flows when modifying the
   gates. #41.2 and #41.14 are the same underlying class of bug (two independent `Update()`s
   reading one physical button with no arbitration, no `ScriptExecutionOrder.asset` in this
   project) — if a THIRD instance of this shows up, that's a signal to stop patching it
   case-by-case and consider a single arbitration point for all gamepad-Submit-adjacent input,
   not just A-vs-jump.
4. **Flap menu is a complex surface** — 4 tabs, multiple elements (slots, sliders, buttons). Consider doing a full audit of navigation setup once before coding, or build a small test scene to verify nav works end-to-end.
5. **Remaining work is #41.7 (cursor auto-hide), #41.8 (Chino "[...]" duplicate), #41.9 (typewriter, P3)** — none are blockers, all can be picked up independently.
6. **`TooltipManager.HideTooltip()` (no color) is a global hide** — any new caller of it
   outside the tooltip system itself (today only `Player.DestroyPaperPlaneHat()`) needs its
   own guard against firing more often than intended; #41.13 was invisible for a while
   precisely because the symptom (tooltips anywhere in the game closing instantly) looked
   nothing like its cause (a stale paper-plane-hat check). See the gotcha written up in
   `docs/claude/origami-y-tooltips.md`.
6. **Small flagged follow-up**: `InputPromptSystem.RxTeclaEnListaDeControles`'s charset
   (`[EUI]`) doesn't cover "O" (Abrir Controles) in the Controles tab list — should map to
   `(Start)`, same axis as Esc. One-line regex change plus a `PromptDeLetra` case.
