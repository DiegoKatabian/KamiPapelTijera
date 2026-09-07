# Feature Specification: Gamepad Controls with Dynamic Input Prompts

**Feature Branch**: `feature/joystick-controls`

**Created**: 2026-09-06

**Status**: Implemented (P0/P1) — see "Implementation Status" at the end

**Input**: Team idea (Diego): "Add gamepad controls, didactic and kid-friendly, with generous origami and animated button icons in UI".

## Context (Code Investigation)

Current input is 100% keyboard + mouse:
- **Movement**: WASD (`Input.GetAxis("Horizontal/Vertical")`) with smoothing for physics — **already works with gamepad** (left stick), tuneable sensitivity
- **Jump**: Spacebar (`Input.GetButtonDown("Jump")`)
- **Attack**: Left-click (`Fire1` → `Player.OnPrimaryClick()`)
- **Interact**: E (`Interact` → `EventManager.OnPlayerPressedE`)
- **Sprint**: Shift (`Run` → `Player.IsSprinting`)
- **Menu**: Esc (`Options` → `OnPlayerPressedEsc`)
- **Inventory**: I (`Inventory` → `OnPlayerPressedI`)
- **Quests**: U (`Quests` → `OnPlayerPressedU`)
- **Origami**: drag with mouse from green arrow (`startingPoint`) to red circle (`finalRectangle`). System interpolates linearly and validates movement respects a "route" (list of `RectTransform`). **Punitive for kids if they stray** — goal is generous + easy.

No gamepad attack/origami implementation yet — greenfield. UI prompts currently hardcode keyboard text ("Press E", "Press Space") with no visual icons — need dynamic system that shows keyboard OR gamepad icon based on detected input.

## Design Assumptions

1. **Standard gamepad**: Xbox/PS4 layout (2 analog sticks, 4 action buttons, bumpers, D-pad, start/select).
2. **Simultaneous keyboard + gamepad**: both inputs work at the same time. **No switching required.** UI detects active input(s) and shows relevant prompts.
3. **Coexistence prompts**: when both keyboard and gamepad can do an action, show both (e.g., "E / [A]" for interact).
4. **Input-aware UI everywhere**: all action prompts (tooltips, dialogue, tutorials, on-screen hints) dynamically show keyboard **or** gamepad icon. Animated icons are a "nice to have" (pulsing/bouncing).
5. **Origami generous**: larger hit radius (~60-100px tolerance), clear visual feedback (green path → yellow warning → red out-of-bounds), manual retry (button B), **no auto-reset**. Same stick as movement (left stick), not separate stick.
6. **Haptic feedback**: if gamepad supports it, subtle vibration on footsteps, medium on attack hit, strong on damage taken.
7. **Kid-friendly**: large button targets, visual clarity, no friction.

## Button Mapping (Final)

| Action | Keyboard | Gamepad |
|---|---|---|
| Move | WASD | L-Stick |
| Jump | Space | **A (green)** |
| Attack/Interact (context) | Left-Click / **E** | **B (red)** |
| Sprint | Shift | **L1** |
| Change Camera | Middle-Click | **L2** |
| Menu/Flap | Esc | Start |
| Navigate UI | Arrow keys | D-Pad / L-Stick |
| Origami drag | Mouse | L-Stick (same as move) |

**Note**: This mapping prioritizes **frequent actions on accessible buttons**:
- **A for jump** (green, most frequent — platformer priority)
- **B for attack/interact** (red, context-dependent: NPC/trigger = interact, enemy = attack, both = prioritize NPC)
- **L1 for sprint** (shoulder = sustained action)
- **L2 for camera cycle** (shoulder, lower priority, same as middle-click)
- Intuitive for kids, matches accessibility best practices

## User Stories & Testing *(mandatory)*

### User Story 1 - Gamepad Attack (Priority: P1)

Player can attack with gamepad button **B** (with nothing interactable nearby) and it behaves identically to left-click.

**Why P1**: core mechanic — without attack, combat is broken.

**Independent Test**: hold left stick neutral, press B repeatedly away from any NPC/trigger. Confirm Kami plays attack anim, scissors fire, hitbox activates. Combine with movement (hold left stick, press B) and confirm `AttackMOVE` anim plays.

**Acceptance Scenarios**:

1. **Given** Kami standing idle with nothing interactable nearby, **When** press B, **Then** `Attack` anim plays, scissors fire, hitbox is active for ~0.3s.
2. **Given** Kami moving (left stick held), **When** press B, **Then** `AttackMOVE` anim plays (no leg keyframing), scissors still fire.
3. **Given** Kami in air, **When** press B, **Then** `AttackMOVE` plays and hitbox fires (aerial attack works).
4. **Given** Kami attacked and landed a hit, **When** hitbox.missed is false, **Then** scissors sound plays + damage applies (same as mouse click).

---

### User Story 2 - Gamepad Jump and Sprint (Priority: P1)

Player can jump with A (green) and sprint with L1, both working identically to keyboard.

**Why P1**: core mobility — jump is the most frequent action in platformers.

**Independent Test**: press A in sequence from ground, confirm jump arc. Hold L1 while moving, confirm sprint particles + velocity increase + Run anim.

**Acceptance Scenarios**:

1. **Given** Kami on ground, **When** press A, **Then** Kami jumps (velocity applied, `jump` anim plays).
2. **Given** Kami in air after pressing A, **When** release A, **Then** Kami falls without jumping again.
3. **Given** Kami moving with left stick, **When** hold L1, **Then** `Player.IsSprinting = true`, Run anim plays, sprint particles visible.
4. **Given** Kami sprinting on wet ground (mojado flag), **When** no L1 pressed, **Then** sprint stops immediately, particles disable.

---

### User Story 3 - Context-Dependent B Button (Priority: P1)

Player presses B button which does different things based on context:
- **Near NPC/trigger**: interact (same as E key)
- **Near enemy**: attack (same as left-click)
- **Both near**: prioritize interact (NPC first, then fallback to attack)

**Why P1**: core UX — B is the secondary action button for attack/interact, freeing A for the most frequent action (jump).

**Independent Test**: 
- Stand in NPC trigger, press B. Confirm dialogue opens.
- Stand near an enemy, press B. Confirm attack fires.
- Stand between both, press B. Confirm dialogue takes priority.

**Acceptance Scenarios**:

1. **Given** within an interact trigger (NPC, solapa, quest trigger), **When** press B, **Then** `EventManager.OnPlayerPressedE` fires (exact same event as E key).
2. **Given** no interact trigger nearby but enemy in range, **When** press B, **Then** attack fires (scissors hitbox, `Player.OnPrimaryClick()` logic).
3. **Given** both NPC and enemy nearby, **When** press B, **Then** NPC interaction takes priority (dialogue opens, not attack).
4. **Given** dialogue open, **When** press B, **Then** advance to next dialogue line (same as E).

---

### User Story 4 - Camera Cycle with L2 (Priority: P1)

Player can cycle camera modes with L2 (same as middle-click in keyboard).

**Why P1**: quality of life — camera control is essential for visibility and player comfort.

**Independent Test**: press L2 repeatedly. Confirm camera cycles through available modes (CloseUp → BookCenter → Normal → General → ReceiveReward, looping).

**Acceptance Scenarios**:

1. **Given** in gameplay, **When** press L2, **Then** camera switches to next mode in cycle (same as `CameraManager.ToggleNextCamera()`).
2. **Given** at last camera mode, **When** press L2, **Then** camera wraps to first mode (circular cycle).
3. **Given** camera is mid-transition (page-turn), **When** press L2, **Then** queue the next camera change (no jarring interrupts).

---

### User Story 5 - Origami with Left Stick (Generous, Priority: P2)

Player can drag origami route point with left stick instead of mouse. Route has expanded hit radius and generous feedback (no harsh fail).

**Why P2**: core puzzle mechanic — must be accessible. Blocks "origami works" until done.

**Independent Test**: enter origami minigame, move left stick. Confirm route point (green arrow) follows stick position. Stay "near" route, confirm visual feedback (green). Stray far, confirm visual feedback (red) but **no auto-reset** — must press B to retry.

**Acceptance Scenarios**:

1. **Given** origami minigame active, **When** move left stick right, **Then** origami route start point (`inicioRectangle`) moves right proportionally (mapped from stick [-1,1] to screen space).
2. **Given** route point within `toleranceRadius` (~80px) of the intended path, **When** held there, **Then** UI changes to green, progress interpolates toward 1.0.
3. **Given** route point outside tolerance (50+ px from path), **When** held there, **Then** UI changes to red (warning), progress pauses, **but does NOT reset** — player must press B to manually retry.
4. **Given** completed all route waypoints, **When** interpolation reaches 1.0, **Then** route marks `wasCompleted = true`, `Origami.CompleteRoute()` fires, next route activates.

---

### User Story 6 - Dynamic Input Prompts (Priority: P2)

All UI text prompts (tooltips, dialogue, tutorials, on-screen hints) show animated gamepad button icons when gamepad is detected. Show both prompts when both inputs work (e.g., "E / [A]").

**Why P2**: usability + kid accessibility — without this, gamepad player sees "Press E" and doesn't know what that means.

**Independent Test**: 
- Connect gamepad, stand near NPC. Confirm tooltip shows "Y" or "[Y icon]" instead of "E". 
- In dialogue, confirm speaker name area has animated button prompts where applicable.
- Disconnect gamepad, confirm UI reverts to "E", "Space", "Click" text.

**Acceptance Scenarios**:

1. **Given** gamepad is connected and active, **When** a dialogue/tooltip with `{INPUT:action}` placeholder renders, **Then** it displays gamepad button B icon (animated pulse/bounce) instead of "E".
2. **Given** both keyboard and gamepad are active, **When** UI renders an action prompt, **Then** show both: "E / [Y icon]" or similar layout.
3. **Given** gamepad disconnects mid-game, **When** UI re-renders, **Then** prompts revert to keyboard (no crash, no orphaned icons).
4. **Given** tooltips with `{INPUT:jump}`, `{INPUT:attack}`, `{INPUT:sprint}`, **When** rendered, **Then** show correct gamepad icon (B, X, L1 respectively).

---

### Edge Cases

- **No gamepad connected initially, then connected**: UI should detect and refresh prompts. No manual restart required.
- **Gamepad disconnects mid-origami**: fallback to mouse drag (if available) or freeze origami until reconnect (TBD by team).
- **Multiple gamepads**: use the first one detected (or last active, TBD). No UI showing "Gamepad 1" / "Gamepad 2".
- **Origami with very high left-stick sensitivity**: on some gamepads, stick drift could cause involuntary movement. Implement small deadzone (~0.15) for origami dragging to prevent false starts.
- **Origami retry spam**: if player presses B repeatedly, should rapid-reset not cause animation stutters. Test with 10 resets/second.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `PlayerInputs` struct and `PlayerController.CheckControls()` MUST read gamepad button inputs (**A for jump, B for action/interact, L1 for sprint**) without breaking keyboard equivalents. Context detection: an interactable trigger (or an open dialogue/overlay) nearby = interact, otherwise = attack.
- **FR-002**: Gamepad inputs MUST fire identical events as keyboard (e.g., `OnPlayerPressedE` when Y pressed, not a separate `OnPlayerPressedY`). Preserves event-driven architecture.
- **FR-003**: The origami minigame MUST accept left-stick input as a pointer. Implemented as a virtual cursor (`GamepadCursor`) that reports a screen-pixel position, so `MultipleRectCheck` keeps calling `OrigamiRoute.SetImagePosition()` with the exact same units as `Input.mousePosition` and the route logic stays untouched. Uses the same stick as movement (Kami is already frozen during the minigame by `PlayerState.Casting`).
- **FR-004**: Origami routes MUST have tuneable `toleranceRadius` (inspector, default 80px) that expands the "safe zone" around `routeRectangles`. Points within radius count as valid even if not exactly on path.
- **FR-005**: Origami MUST display **visual feedback** (color change + UI indicator) when route point is in-tolerance (green), near-tolerance (yellow), or out-of-bounds (red).
- **FR-006**: Origami MUST allow manual retry via button B (`Input.GetButtonDown("Reload")`). Pressing B resets current route to start. NO auto-reset after time — respects player agency.
- **FR-007**: `DialogueManager` MUST support placeholder tags in localized strings: `{INPUT:action}` (context-dependent: attack or interact), `{INPUT:jump}`, `{INPUT:sprint}`. At runtime, replace with TMP rich text `<sprite name=button_A>` (or fallback text "A").
- **FR-008**: Icon injection system MUST detect active input device and choose icon set (keyboard vs. gamepad). If both active, show both prompts.
- **FR-009**: TMP sprite icons MUST be animated (pulse/bounce effect) — achievable via TextMesh Pro animation tags or simple scale/alpha tween on the sprite element.
- **FR-010**: Haptic feedback (if supported by gamepad): subtle rumble on footsteps (~0.2 intensity, 50ms), medium on attack hit (~0.5 intensity, 100ms), strong on damage received (~0.8 intensity, 150ms).

### Key Entities

- **PlayerController.cs**: read gamepad axes/buttons, populate `PlayerInputs` struct.
- **OrigamiRoute.cs**: new parameter `toleranceRadius`; new method `SetImagePositionFromStick(Vector2 stickInput)` that maps stick to screen space.
- **OrigamiUIFeedback.cs** (new): component that colors route point based on distance to path (green/yellow/red). Attach to origami canvas.
- **InputPromptSystem.cs** (new): singleton that detects active input device, provides `GetPromptText(inputAction)` → returns keyboard key or `<sprite name=button_A>` (context-aware) + fallback text.
- **DialogueTextProcessor.cs** (new): takes raw dialogue string with `{INPUT:*}` placeholders, processes via `InputPromptSystem`, returns TMP-friendly rich text.
- **GamepadVibration.cs** (new, optional): centralized haptic feedback — called from attack/damage/footstep events.
- **InputSettings.asset**: configure Input Manager with gamepad button bindings (X, B, L1, Y axes/buttons).

### Localization & Strings

All strings mentioning key presses MUST use `{INPUT:*}` placeholders in localization tables (`DialogueTable_es/_en/_pt`). Example:

```yaml
# BEFORE (old, keyboard-only)
InteractPrompt: "Press E to talk"

# AFTER (new, input-aware)
InteractPrompt: "Press {INPUT:action} to talk"
```

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user with **only a gamepad** (no keyboard/mouse) completes Nivel 1 start → finish, including ≥1 origami, without frustration or confusion.
- **SC-002**: Origami with gamepad achieves **≥90% success rate** on 10 manual attempts (varied speed/precision simulating a child's play).
- **SC-003**: All in-game prompts (tooltips, dialogue, tutorials) display **correct button icons** when gamepad is detected. No orphaned "Press E" text with gamepad plugged in.
- **SC-004**: **Zero crashes** on gamepad connect/disconnect during gameplay.
- **SC-005**: Haptic feedback (if supported) is **present and appropriately intense** — footsteps feel subtle, attacks feel punchy, damage feels impactful.
- **SC-006**: Coexistence test: with both keyboard and gamepad active, prompts show both (e.g., "E / [A]"). Removing gamepad doesn't break prompts.

## Implementation Notes

### Left Stick for Origami (Not Right Stick)

Origami uses the **same left stick** as movement, not a separate right stick. This means:
- **In origami minigame**, we override `PlayerModel.Tick()` to ignore left-stick movement input and instead map it to origami dragging.
- **Origami code** calls `OrigamiRoute.SetImagePositionFromStick(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"))`.
- **Deadzone**: implement ~0.15 deadzone in origami stick reading to avoid drift-induced false starts.

### Icon Spritesheet & Animation

- **Gamepad icons**: create or source a sprite atlas with Xbox buttons (A/B/X/Y, LB/RB, Start, D-pad, analog sticks).
- **TextMesh Pro integration**: import atlas into TMP Material, register sprites with names (`button_A`, `button_B`, `button_X`, `button_A`, `button_LB`, etc.).
- **Animation**: use TMP animation sequences or simple Unity `DOScale`/`DOAlpha` tweens on the sprite `<sprite>` element when it appears.

### Graceful Degradation

- If no gamepad icon font is available, `InputPromptSystem.GetPromptText()` returns fallback: "A" (text) instead of `<sprite name=button_A>`.
- If gamepad disconnects, all prompts revert to keyboard text — no error, no UI breakage.

## Timeline & Milestones

- **Week 1** (Sep 6–12): Implement attack/jump/sprint/interact with gamepad (FR-001/002), basic button mapping.
- **Week 2** (Sep 13–20): Origami with left stick + tolerance system (FR-003/004/005/006), manual retry.
- **Week 3** (Sep 21–27): Input prompt system (FR-007/008/009), icon injection, localization, testing with kids if possible.
- **Target merge**: **Sep 30** (M2 hito) or **Oct 8** (M3 if delays) — goal is stable before **Oct 15 (M4, public build / expo)**.

## Notes

- This feature is **inclusive**, not replacement — keyboard + mouse remain fully functional.
- Gamepad is the natural interface for arcade cabinets or console ports (per transmedia roadmap in GDD).
- The emphasis on **origami generosity** is critical: it's the game's signature mechanic and must shine with any input device.
- A harsh origami breaks the game for kids; a generous one makes it accessible and delightful.
