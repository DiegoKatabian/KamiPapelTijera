# Spec 021: The magnifier and tool switching (Level 2)

**Status**: Draft, 2026-10-26. Asked by Diego; nothing built. Approved 2026-10-26: Diego answered every question
("Answered by Diego" near the end) and accepted the rest of the defaults. To be built later, under milestone M10. Issue: none yet. Branch: none yet (written on `020-kami-magical-girl-change`, uncommitted).

## Request (Diego, 2026-10-26)

1. Level 2 gets a **new tool, the magnifier** (la lupa). Kami switches tools with `1`, `2`, `3`... or by tapping the UI.
2. Where should the switch button go and how should it look?
3. The magnifier works with the mouse: it **magnifies a section of the book**.
4. Later, not in this spec: Level 3 gets its own tool; the scissors might work by **tracing with the mouse**; all of
   it has to be adaptable to **iPad**. Recorded as design notes at the end so the context lives in a file; nothing
   here builds them, but nothing here may make them harder.

## Goal (what Diego sees)

> On page 1 Natalia gives Kami a magnifying glass. A small row of paper stickers appears in the HUD strip: the
> scissors (1) and the magnifier (2). I press `2` (or tap its sticker): Kami holds a big lens, moves slower, and a huge
> lens drawn on the screen follows my mouse, magnifying what is under its glass. I click a clue through the glass and
> it is collected from where I stand (walking into it or pressing E still works too). I press `1`: normal again, scissors
> in hand. Level 1 and the Steam demo look and play exactly as before, with no selector on screen.

## What exists today (checked in code, 2026-10-26)

- **One tool, implicit.** Left click / Ctrl / gamepad B go `PlayerController.CheckControls` -> `Player.OnPrimaryClick`
  -> `_readyToAttack && hasTijera && CanAttack()` -> `PlayerView.StartAttack`. There is no concept of "current tool".
- **Gear is looks plus effects, not tools.** `GearSlot` = Outfit, Scissors, Feet, Hat (append-only enum, spec 011).
  The Scissors slot swaps the scissors skin; `hasTijera` is owned-based, and `ConfiscatedGear` / `LoseTijera` act on it.
- **Input is centralised.** `InputHub` is the only file that knows axis names. Number keys `1`-`9` and the keypad are
  used by **nothing** in `Assets/Scripts` (grepped). Gamepad Y (button 3) is free: `Jump` was removed from it in the
  joystick work.
- **A virtual cursor exists**: `Input/GamepadCursor` (stick-driven, reports screen pixels like `Input.mousePosition`).
  Today only the origami uses it; reusing it for the lens is possible but not verified here.
- **A magnifier already exists as art**: `ClueLupa.prefab` (the floating pin over each clue, `Level2/ClueMarker`).
  That is the marker, not the tool.
- **Layers**: user layers 24-31 are unused in `TagManager.asset` (a `LensOnly` layer fits).
- **HUD**: `FlapManager`'s always-on strip (pull tab, health, paper, page number) fades during cutscenes through
  `_hudGroup` (#188). A selector in that strip inherits the fade for free.

## Decisions (proposed defaults, Diego can overrule any of them)

| # | Decision | Why |
|---|---|---|
| D1 | A **tool** is not **gear**. New `KamiTool` enum (`Scissors`, `Magnifier`, later `Camera`) and a `PlayerTools` class owned by `Player` (same pattern as `PlayerGear`): which tools Kami owns, which is active, `OnToolChanged`. | Gear is "what she wears" and is composed into a skin; confiscation, "newest pair wins" and `hasTijera` are scissors rules that must not leak onto a lens. Tools answer "what does my pointer do". |
| D2 | The active tool decides what the primary action does: `Scissors` = today's attack, exactly; `Magnifier` = no attack, the lens shows. `OnPrimaryClick` gets one guard at the top; nothing else in the attack path changes. | Smallest change to the working combat code. |
| D3 | Switching is allowed only when the game is free for the player: the same freeze rule as `Player.TryToggleGear` (FR-105): not in dialogue, cutscene, overlay, page turn, Casting, ReceivingReward, Dead, RidingPage, or while the Flap is open (the Flap has its own mouse). | Same class of bug as issues #41.2 / #41.14: one press must never act in two systems. |
| D4 | Keys: `1` Scissors, `2` Magnifier, `3` reserved. Pressing the active tool's key does nothing. Read in `InputHub` (`Tool1Down`...), not in `Player`. | `InputHub` is the single input door. |
| D5 | Gamepad: **Y** cycles to the next owned tool (new axis `ToolNext`, joystick button 3). Mouse wheel is not used (it is not used elsewhere, but it is easy to brush by accident on a trackpad). | Y is free and mnemonic; cycling needs one button for 2-3 tools. |
| D6 | **The selector lives in the Flap's HUD strip**, next to the paper counter: a row of round paper stickers, one per owned tool, with the key icon drawn in the corner (the project's own `IconosDeBoton` sprites, so it shows `Y` with a gamepad). Active = scale 1.15 and full colour; others 0.85 and faded (the Wardrobe's rule). Tapping a sticker = switching. **Hidden entirely while Kami owns a single tool** (so Level 1 and the demo are untouched). | The strip is already where she looks for "what do I have"; it follows the cutscene fade and Flap rules. |
| D7 | On every switch the tool's icon pops once above Kami's head (reuse the `ClueLupa` pop recipe) and a short paper sound plays (`ToolSwitch`, AudioBank row, placeholder clip). | The player is watching Kami, not the corner. |
| D8 | The lens is a **second camera**, not a UI trick: `LensCamera` copies the live main camera, is rotated to look along the ray through the cursor and has its field of view divided by the magnification, rendering into a `RenderTexture` shown in a round UI mask under the cursor. | Geometrically exact (equals magnifying that part of the screen), reuses the real scene, shows true 3D parallax, and `LensOnly` objects become trivial. |
| D9 | Hidden details are ordinary objects on a new **`LensOnly` layer**, which the main camera's culling mask excludes and the `LensCamera` includes. No shader work. | Authoring is "drop an object on a layer"; Valen can paint them. |
| D10 | **Clicking a clue through the lens opens it** (Diego, 2026-10-26). A clue is a `LensClickable`; a click inside the glass is mapped to the world through the lens camera (`lensCamera.ViewportPointToRay` from the click's position in the glass), so you click what you SEE magnified, not what is under the cursor's unmagnified point. The click runs the clue's existing collect/open action from any distance (the detective's reach). Kami can still trigger every clue as today (walk into it, press E): both paths end in the same latched action, so a clue is collected once. | The lens becomes the remote, deliberate way to take a clue; the old way is untouched, so nothing existing gets harder. |
| D11 | While the magnifier is active, left click / the gamepad confirm button **never attacks**; it only clicks the lens (D10). Over empty glass it does nothing. | One meaning per tool. Replaces the draft's "click does nothing". |
| D13 | **Natalia gives the magnifier after the café fold** (Diego, 2026-10-26). The sequence he wants: Natalia: "wait! you will need this:" -> fanfare, Kami receives the lens -> "press 2 to use it" -> "now let's go to the next page to the museum!". Built as two dialogues around the grant: `Natalia_AfterTicket` (to "you will need this:"), the gift (D15), then a new `Natalia_LensTip` ("press 2...", "next page..."); Quest05 and her follow start when the SECOND one ends (today they start when `Natalia_AfterTicket` ends). Diego edits the lines; the keys/texts are his. "Press 2" is written with a new `{INPUT:herramienta}` placeholder (keyboard `2`, gamepad Y, see `InputPromptSystem`) so it shows the right key. Before the gift Kami owns only the scissors and the selector is hidden (D6). Test starts on pages 2-5 get the lens through `Level2TestStartItems`. | A story gift teaches the new tool at the right moment. |
| D15 | **The lens is a bag item** (my proposal, to reuse what exists): a new `ResourceType.magnifier` appended to the enum (never reorder), an `InventoryItem` in BOTH InventoryManager lists plus its `ItemTable` keys (Level 2's scene copy is standalone, see `nivel2-y-ui.md`). Natalia's gift is `AddResource(magnifier, 1)`, so the reward sticker and fanfare (`Receive_Reward`, and the receiving pose if that path triggers it for resource gains: to be traced when building) come for free. `PlayerTools` owns the Magnifier while the count is above 0 (the way `hasTijera` follows the scissors count), so page 4's `ConfiscatedGear` takes it with a `-1` and gives it back, and the P-cheat / test starts just add the resource. Icon: Valen (same size family as the other bag icons). | One ownership path for every item; the confiscation and test-start code already speak "resource". |
| D16 | **Opening the lens can switch the camera to Book Center** (Diego's request, to try): `MagnifierLens` has `_changeCameraOnOpen` (bool, default **on**) and `_cameraOnOpen` (`CameraMode`, default `BookCenter`), both Inspector fields. It goes through `CameraManager.SetCameraForBeat`, so a page in `_pagesThatKeepTheirCamera` (page 4) keeps its camera. Closing the lens restores the mode that was live when it opened, if `_restoreCameraOnClose` (default on) and the player has not picked another camera in between. The lens camera copies whatever camera is live every frame, so changing cameras while it is open (the player's cycle, a cutscene) is safe. | He wants to try both framings; bool and enum make it a one-click test. |
| D17 | **Gamepad: the left stick moves the lens and Kami stays still** (Diego, 2026-10-26). `GamepadCursor` is reused as is, on the left stick. While the lens is up and the last device is a joystick, Kami's movement input is zeroed (the origami's Casting lock does the same; here a tool-specific hold in `PlayerModel`). To walk again she puts the lens away (Y or `1`). The 0.6x walk speed therefore only matters with keyboard and mouse. A on the gamepad only clicks the lens (no interact, no jump). | Diego's call; it keeps the stick unambiguous and needs no right-stick work. |
| D14 | **Art contract (Valen, 2026-10-26)**: (a) Kami holds a *big* lens in her hand, handled exactly like the scissors (a Spine skin layer plus the same "tool in hand" override animations); (b) a *huge* lens drawn as UI, on the scale of a narrator's tool, whose glass is the magnifying area. The glass is a transparent circle (a mask sprite shipped with the art) into which the `RenderTexture` is drawn, and the frame/handle is drawn over it. The lens diameter in pixels is the art's glass diameter, not a separate number. | The art is the effect's frame; the tuning field `_lensDiameterPx` must follow the sprite so glass and picture agree. |
| D12 | Level 1 / demo: no `PlayerTools` owned beyond the scissors, so none of this is visible. Level 2 starts with the scissors only; the magnifier arrives with Natalia's gift (D13). The loadout lists the starting tools (a field on `GearLoadout` or a sibling asset: whichever is smaller when built). | Consistent with how Level 2 already starts with its scissors. |

## Behaviour

### Tool switching
1. `PlayerTools` holds `owned` (set) and `active`. `TrySelect(tool)` refuses per D3, then sets `active`, raises
   `OnToolChanged(old, new)` and logs `[PlayerTools] ...`.
2. `Player.OnPrimaryClick` returns early when `active != Scissors` (log once at verbosity, not every click).
3. Losing the scissors (`LoseTijera` on page 4) does not remove the Scissors tool; it makes the Scissors tool
   unusable exactly as `hasTijera == false` already does. The selector shows the sticker greyed, not absent.
4. A tool becomes owned when its bag item arrives (D15: `ResourceType.magnifier`; the scissors keep following `hasTijera`),
   which shows the usual reward sticker; `PlayerTools` then pops the new sticker in the strip.
5. Start of level: the loadout's active tool is selected. After a respawn the active tool is kept.

### The lens
1. Active while `PlayerTools.active == Magnifier` and the game is free (D3 conditions); on open it switches the camera per D16. In a dialogue, cutscene,
   Flap, page turn or death it hides immediately and returns when free again.
2. Pointer = `Input.mousePosition` (keyboard and mouse), `GamepadCursor` position when the last device was a joystick.
   The lens art's glass is centred on the pointer; its diameter is the glass sprite's, scaled by `Screen.height`
   (D14; a ~300 px circle until Valen's art lands); magnification `_magnification` (3) is an Inspector field. The
   glass is clamped so it stays on screen.
3. `LensCamera` (disabled when not in use, so it costs nothing): each frame copy position, rotation, projection and
   culling from the active main camera, then rotate to look along `mainCamera.ScreenPointToRay(pointer)` and set
   `fieldOfView = mainFov / _magnification`. Renders to a `RenderTexture` the size of the lens (about 512 x 512), with
   the `LensOnly` layer added to its culling mask. Post-processing: off on the lens (cheaper; revisit if it looks flat).
4. **Kami moves slower while the lens is up** (Diego: "when you press 2, kami moves slower"): speed multiplier
   `_walkSpeedWhileLens` (0.6, Inspector), applied in `PlayerModel` the same way other speed modifiers are. 1.0 = no change. With a gamepad Kami does not
   walk at all while the lens is up (D17).
   The wearable big lens is in her hand the whole time (D14a), the huge UI one only while the game is free (item 1).
5. The lens is **screen-space**, so it works the same with every Cinemachine camera mode and with the page-turn
   cameras; it never needs a world position for itself.
6. Cursor: the system cursor is hidden while the lens is active (the lens is the cursor); `CursorManager` already
   owns cursor visibility, so the lens asks it, not `Cursor` directly.

### Clicking a clue (D10)
1. A `LensClickable` sits on a clue (its collider on the layer the lens camera sees). It has one entry point,
   `Activate()`, that calls the clue's own existing action and is a no-op once the clue is spent.
2. Click in the glass -> lens-camera ray -> first `LensClickable` hit (max distance `_maxRayDistance`) -> `Activate()`.
   Hovering one swaps the lens' cursor art to a "pointing" state and shows its `ClueLupa` pin pulsing (Valen's call).
3. What `Activate()` calls on the three page 2 clues: the hat and the watch (`CluePickup_*`, `GrantResourcePickup`):
   the collect they run when Kami walks in; the glove (in `TrashCan_ClueGlove`): the can's open, without Kami's
   `PullSolapas` gesture (she may be far away; see Q9). All keep the existing `ClueMarker` hide and the
   `FindCluesTracker` count, because those hang off the clue, not off Kami.
4. The sound is the clue's own (`MagicSuccess` at 1.4, played by `FindCluesTracker.PlayClueSounds`); the lens adds none.
5. Gamepad: the confirm button (A) clicks at the virtual cursor. Today A is contextual (interact, else jump, see
   `controles-y-gamepad.md`); while the lens is up A belongs to the lens, so `InteractionContext` must report "lens
   active" the same way it reports a dialogue, or A would also jump (D17).

## Level 2 use (phase 2, only after phase 1 is played)

Page 2's three clues are found today by walking into them (or pressing E at the can). That stays. With the lens, the
player can instead point at the clue (the `ClueLupa` pins are the cue) and click it from anywhere on the page.
The lens is **not required** for any clue (Q3). Optional extra, not part of this spec's first build: lens-only
details on the `LensOnly` layer (an ink footprint, a phone number in the newspaper's fine print) that give a bonus;
the newspaper pages are real textures (`Level2 Pages/`, 2048 x 3148), so an overlay plane 0.02 above a page is enough.

## Requirements

- **FR-1**: Level 1 and the Steam demo play identically (no selector, no layer changes visible, no new per-frame cost
  beyond one `PlayerTools` null check).
- **FR-2**: Switching never fires an attack, a jump or an interact in the same frame, in any `Update` order.
- **FR-3**: With the scissors active, combat is byte-for-byte today's path.
- **FR-4**: The selector shows the right owned tools, the right active one, and the right key icon for the active
  device (keyboard or gamepad), and updates on `InputHub.OnDeviceCambio`.
- **FR-5**: The lens never renders outside the free-play state, and its camera is disabled whenever the lens is hidden.
- **FR-6**: `LensOnly` objects are invisible to the main camera in every cutscene camera and in the page-turn views.
- **FR-7**: Everything tunable (magnification, walk speed, ray distance) is an Inspector field; no hardcoded design values.
- **FR-8**: A clue is collected exactly once however it is triggered (walk-in, E, lens click, in any order or
  simultaneously); `FindCluesTracker` counts it once.
- **FR-9**: Before Natalia's gift Kami has one tool, no selector shows and `1`/`2` do nothing.

## Files (planned)

New, in `Assets/Scripts/Tools/`: `KamiTool.cs`, `PlayerTools.cs`, `ToolSelectorHud.cs`, `Magnifier/MagnifierLens.cs`,
`Magnifier/LensClickable.cs`; prefabs `UI/ToolSelector.prefab` (under the Flap prefab's HUD
strip) and `Prefabs/Level2/MagnifierLens.prefab` (camera, RenderTexture, glass mask, Valen's frame); assets: a
`LensOnly` layer in `TagManager.asset` (only if the optional bonus details are built; clicking clues does not need it),
an AudioBank row `ToolSwitch`. Touched for D10/D13: `CluePickup_Hat|Watch` / `GrantResourcePickup` and
`TrashCan_ClueGlove` (get a `LensClickable`), `Level2TestStartItems`, `NataliaDialogueTrigger` / `Natalia_AfterTicket`.
Touched: `Player.cs` (owns `PlayerTools`, guard in `OnPrimaryClick`), `PlayerController.cs` (reads the tool keys via
`InputHub`), `InputHub.cs` (+ axes), `InputManager.asset` (`ToolNext`; CRLF file, trailing space after empty fields),
`GearLoadout`/`Level2_StartLoadout` (tools), `FlapManager.prefab` (the selector, by YAML surgery), `CursorManager`
(lens asks to hide the cursor), the main `Camera`'s culling mask (the one with the `CinemachineBrain`; not located yet, to be found in phase B).

## Phases

- **Phase A, switching without a lens (small).** `KamiTool`, `PlayerTools`, the `OnPrimaryClick` guard, `1`/`2` keys
  and Y, the selector HUD, the switch sound/pop. A placeholder second tool ("Magnifier" that just logs) proves it.
  Done when: in Level 2 `1`/`2`/Y and tapping change the active sticker, the scissors cut only while active, Level 1 is unchanged.
- **Phase B, the lens.** `LensOnly` layer, `MagnifierLens` prefab, pointer from mouse and `GamepadCursor`, gates,
  walk speed. Done when: the lens shows 3x magnification following the mouse on every camera mode and vanishes in dialogues/cutscenes.
- **Phase C, clicking clues and Natalia's gift.** `LensClickable` on the three page 2 clues, `ResourceType.magnifier` + its bag item (D15), Natalia's
  hand-over (D13), `Level2TestStartItems` grants. Done when: with the lens up, clicking the magnified hat, watch and
  glove collects each once, walking into them still works, and no clue can be collected twice by mixing the two ways.
- **Phase D, art and polish** (Valen, D14): Kami holding the big lens, the huge UI lens (glass mask + frame), the
  sticker icons and the pop. Until it lands, Phase B uses a plain circle and Kami holds nothing.
- **Optional later**: `LensOnly` bonus details (see "Level 2 use").

Verification path: `python tools/compile-check.py` for compilation; the rest (feel, magnification, GPU cost, gamepad
cursor, sticker layout) only Diego can judge in the Editor. A phase is not "verified" until he has played it.

## Risks and measurements

- **A second scene render.** The lens camera draws the scene again at about 512 x 512. Measure the frame time with the
  lens on, on the lowest target machine, before deciding on a camera-stacking or cheaper variant. On iPad this is the
  first thing to profile.
- **Texture sharpness.** At 3x a 2048 x 3148 page is shown at about 1.5x its resolution at 1080p: acceptable, but
  fine print should be authored at 2x the size that reads at 1x.
- **Number keys on non-US layouts.** `Alpha1..3` are the top-row digits; on AZERTY they need Shift. The gamepad Y and
  tapping are the fallbacks; revisit if a localized-keyboard player reports it.
- **Click collisions.** A click on a sticker must not also attack: stickers are `Selectable`s inside the HUD strip,
  and today only tooltips (not clicks) are suppressed over the Flap by `HoverDetector`; check what a UI click
  does to the world press (`OnPrimaryClick` reads the raw mouse button) and gate it for the sticker area.

## Answered by Diego (2026-10-26)

- **Q1 / Q11**: Natalia gives the magnifier, all after the ticket fold, in the sequence of D13.
- **Q2**: yes, page 4's `ConfiscatedGear` takes the lens too and gives it back (D15 makes it a `-1` / `+1`).
- **Q3**: the lens is an *additional* way to take a clue: click it through the glass; Kami can still walk into it or
  press E (D10). So the lens is not required (read this way; correct me if "yes" meant "required").
- **Q4**: Valen draws (a) Kami holding a big lens like the scissors, (b) a huge UI lens sized to the effect (D14).
- **Q5**: Kami moves slower while the lens is up with keyboard and mouse (0.6x default, a field).
- **Q6 / Q6b**: reuse `GamepadCursor` on the left stick and Kami stays still on gamepad (D17).
- **Q9**: yes, the glove's can opens from afar through the lens; opening the lens switches the camera to Book Center,
  with an Inspector bool and enum to change or disable it (D16).
- **Q10**: yes, A only clicks the lens while it is up (D17).

## Open questions

None. Diego accepted every remaining default on 2026-10-26: Q7 (3x), Q8 (key `3` and a `Camera` enum slot reserved, nothing
built), Q12 (the lens as a bag item, D15), Q13 (closing the lens restores the previous camera, D16) and Q14 (no walking with
a gamepad while the lens is up, D17). Anything surprising in the first playtest is a field, not a redesign.

## Design notes for later specs (not built here)

- **Level 3 tool: a camera** (flash freezes a moving thing for a few seconds; a photo can be placed as an object, as
  befits a photo album). Runner-up: tape/glue, which mends what the scissors cut. It plugs into `KamiTool` and the
  selector; that is why D1 keeps tools apart from gear.
- **Traced scissors**: keep click = today's attack and add drag = a traced cut: record the stroke in screen space and
  cut every `ICortable` within Kami's reach whose projected bounds the stroke crosses (a reach limit is essential, and
  Kami lunges toward the stroke). Gamepad keeps B. It would be a second mode of the Scissors tool, selected by gesture,
  not another tool. Prototype first on ropes, ribbons and police tape.
- **iPad**: `InputHub` should grow one **pointer** abstraction (mouse, touch, virtual cursor) before tracing or the lens
  are built on touch; Unity maps one touch to a mouse press, so the origami largely works already. Left third = virtual
  joystick, right third = jump/interact, the rest = gestures; multi-touch (one finger walks, one traces); the lens
  shows **above** the finger. Needs a Mac and an Apple developer account to build; profile the second render, Spine
  physics and the 4096 textures on a real iPad.
