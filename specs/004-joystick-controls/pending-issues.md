# Joystick Controls — Pending Issues

**Branch**: `feature/joystick-controls`

**Date documented**: 2026-09-08

Found during user testing (Diego playing with gamepad controls). These are bugs and polish items blocking merge to main; marked for fix before **Oct 15 (M4)**.

---

## P1 (Critical) — Blocks Gameplay

### Issue #41.1: UI Navigation in Flap Menu is Broken

**Category**: Feature-Breaking / UX

**Severity**: P1 — menu is unnavigable with joystick, critical friction for gamepad users.

**What happens**:
- No way to navigate to the close button of the Flap (ESC/Start to open, but no way to close with joystick once open).
- The 4 section tabs (Tareas/Morral/Settings/Controles) all show the same amber color — not visually distinct like they used to (they had individual colors that made navigation clear).
- No clear visual indicator of which inventory slot is currently selected.
- No clear visual indicator of which settings slider is currently selected.
- Once inside Tareas or Controles sections, **there is no way out** — stuck with no selectable elements and no button to exit.

**Expected behavior**:
- Flap should open with Start (works).
- Joystick should navigate between section tabs (currently doesn't).
- Each section should have a distinct color or visual treatment (lost in transition).
- Selected slots/sliders should be visually prominent (especially color change, maybe animation like the mouse-dragged gallinas have).
- Each section should be escapable (maybe with B to close, matching the close button in pause overlay).
- Proposal: **R1 and L1 to cycle between sections** (like shoulder buttons for tab navigation), **B to close the Flap** (contextual, same as Start but for closing). **A should be Submit** (for sliders, buttons) and **Start should still toggle the menu**.

**Related**: Gallinas have a cute animation when dragged with mouse (bouncing/pulsing). The selected state in joystick nav should feel similar — add a light animation or scale/pulse to show "this is active."

**Files to check**:
- `Assets/Scripts/UI/FlapManager.cs`
- `Assets/Scripts/UI/FlapDisplayButton.cs` (tab selection)
- `Assets/Scripts/UI/InventorySlot.cs` (inventory selection feedback)
- `Assets/Scripts/UI/SliderFlap.prefab` (slider highlight)
- `Assets/Scripts/UI/UISelector.cs` (nav gate)

---

### Issue #41.2: Cannot Attack (B Button Unresponsive) After Origami in Level 1 Page 2

**Category**: Bug / Input

**Severity**: P1 — blocks combat, breaks gameplay loop.

**What happens**:
- B stops responding for attack/interact after completing an origami in Level 1, page 2.
- Suspected trigger: closing the origami with B (retry/cancel mechanic).
- Once stuck, B does nothing until... unknown (maybe scene reload?).
- **Note**: B should NOT work during the origami minigame itself — only cancels/retries the fold. If the bug is that it stops working AFTER closing, that's the issue to fix.

**Expected behavior**:
- B should work for attack/interact before, during, and after origami.
- Closing origami with B should not block future B presses.

**How to reproduce**:
- Enter Level 2.
- Find an origami pedestal.
- Open origami (A).
- Press B to cancel/retry at least once.
- Close origami (press E or walk away).
- Try to attack: B is unresponsive.

**Files to check**:
- `Assets/Scripts/Input/PlayerController.cs` (check if flag is cleared after origami closes)
- `Assets/Scripts/Origami/MultipleRectCheck.cs` (the return/cutoff logic for origami frame consumption)
- `Assets/Scripts/Player/Player.cs` (state machine — is Casting state exiting cleanly?)

---

### Issue #41.3: Cannot Move / Cannot Do Anything During Flap Pause

**Category**: Bug / Input

**Severity**: P1 — gameplay is unfrozen during pause; input should be blocked.

**What happens**:
- While the Flap menu is open (paused state), the player can still:
  - Move Kami left/right with A or left stick (she flipflops but doesn't move — no forward motion, just facing flips).
  - Trigger origamis by pressing A near a pedestal.
  - Do other gameplay actions.
- `Time.timeScale = 0` is set, so no motion happens, but the **input is being accepted and processed**, which shouldn't happen.

**Expected behavior**:
- While `FlapManager.isMenuOpen`, no gameplay input should be polled by `PlayerController`.
- Moving A/stick, attacking, interacting should all be blocked.
- Only menu navigation (arrow keys, Start, B for close, R1/L1 for tabs) should work.

**Files to check**:
- `Assets/Scripts/Input/PlayerController.cs` (add gate: `if (FlapManager.isMenuOpen) return;` at start of input polling)
- `Assets/Scripts/Input/InteractionContext.cs` (already gates by `LevelManager.inDialogue`, does Flap set that flag?)
- `Assets/Scripts/UI/FlapManager.cs` (check if it sets or clears any input gates)

---

## P2 (High) — UX & Polish

### Issue #41.4: Main Menu Not Navigable with Joystick

**Category**: UX

**Severity**: P2 — blocks start game flow for gamepad-only players.

**What happens**:
- Main menu (title screen) has buttons (Start Game, Language select) but they don't respond to joystick navigation.
- No visual selection indicator when joystick is plugged in.
- Can only interact with mouse clicks.

**Expected behavior**:
- Left/right arrows or D-pad should navigate between language buttons.
- A should confirm selection.
- Start button should also confirm (submit).

**Files to check**:
- `Assets/Scripts/UI/MainMenuUI.cs` or equivalent (check if it has EventSystem selection setup).
- `Assets/Scenes/MainMenu.unity` (check if any Selectable has `m_FirstSelected` set).

---

### Issue #41.5: Origami Tooltip Should Say "Tocá A y Arrastrá..." With Joystick

**Category**: UX / Localization

**Severity**: P2 — gamepad user gets confusing tooltip text.

**What happens**:
- Tooltip for origami (shown while standing in a pedestal) says "Press E to start" / "Click and drag" — keyboard-only prompts.
- With joystick active, it should say "Tocá A y arrastrá..." (or "Press A and drag..." in English).

**Expected behavior**:
- `InputPromptSystem` should catch the origami tooltip and replace the prompt text based on active device.
- This is likely a `{INPUT:*}` placeholder issue in the localization string.

**Files to check**:
- `Assets/Localization Settings/Tables/UITexts_es.asset` (search for origami tooltip string).
- `Assets/Scripts/Input/InputPromptSystem.cs` (check if it processes all UI strings or only specific ones).

---

### Issue #41.6: Controles Section Text Should Update for Joystick

**Category**: UX / Localization

**Severity**: P2 — player sees keyboard text even with joystick active.

**What happens**:
- The "Controles" tab in Flap shows control mappings (e.g., "WASD = Mover", "SHIFT = Correr").
- These are static text or a table, not dynamic prompts.
- With joystick active, it should say "L-Stick = Mover", "L1 = Correr", etc.

**Expected behavior**:
- `Controles` tab should detect active device and show appropriate mappings.
- Two tables: one for keyboard, one for gamepad (or single table with both columns, one highlighted).

**Files to check**:
- `Assets/Scripts/UI/FlapManager.cs` or the display that builds the Controles tab (likely a `FlapDisplay_Controles` component or similar).
- Create a `ControlsDisplay.cs` or similar that listens to `InputHub.OnDeviceCambio` and refreshes the table.

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
| #41.1 Flap navigation | UX | P1 | Yes | 4-6h |
| #41.2 B button stuck | Bug | P1 | Yes | 2-4h |
| #41.3 Input during pause | Bug | P1 | Yes | 1-2h |
| #41.4 Main menu nav | UX | P2 | No | 2-3h |
| #41.5 Origami tooltip | UX | P2 | No | 1h |
| #41.6 Controles text | UX | P2 | No | 2-3h |
| #41.7 Cursor auto-hide | Polish | P2 | No | 2h |
| #41.8 Chino duplicate [...] | Bug | P2 | No | 30m |
| #41.9 Typewriter text | Polish | P3 | No | 3-4h |

**Total P1 (blocking)**: ~7-12h  
**Total P2 (high priority)**: ~8-11h  
**Total P3 (nice to have)**: ~3-4h  
**Estimated post-testing total**: **~18-27h**

---

## Notes for Next Session

1. **Start with P1 issues** — they block gameplay. Prioritize #41.2 and #41.3 first (bugs), then #41.1 (UX).
2. **Test with input system changes** — every fix to input or state gates should be tested with both keyboard and gamepad to avoid regressions.
3. **A button context is fragile** — it's doing a lot (jump, interact, menu submit). Be extra careful not to break any of those flows when modifying the gates.
4. **Flap menu is a complex surface** — 4 tabs, multiple elements (slots, sliders, buttons). Consider doing a full audit of navigation setup once before coding, or build a small test scene to verify nav works end-to-end.
