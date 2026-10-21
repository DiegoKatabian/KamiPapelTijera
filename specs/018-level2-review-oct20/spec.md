# 018 - Level 2 review "martes 20" (2026-10-20)

Diego's revision list for Level 2, seven items. Status: **built 2026-10-20, compile-checked, uncommitted, not played.** Deviations from the draft below: item 3 needs no z-order change beyond swapping the two z values; item 7 only removes the 11.27 scale override (the base slider already fits its arrows) instead of copying Level 1's numbers; item 2 zooms the vcam's lens instead of moving it.
Everything added is in English (language rule). Each item lists what I found in the code, what
we will do, and what is still open (marked **OPEN**, asked to Diego before building).

## 1. Page marker in Level 2, desaturated

- **Found**: the page number ("Page N", bottom right) is `PaginaActualText` inside
  `FlapManager.prefab > PaginaActualHUD`, driven by `PaginaActualTextUpdater`. The prefab ships it
  **inactive** (`m_IsActive: 0`); Level 1's `Main Canvas.prefab` switches it on with an override,
  and Level 2's scene instance of `FlapManager` has no such override. That is the whole reason it
  is missing in Level 2.
- **Do**: switch it on in Level 2's scene instance and tint it nearly gray (Level 1's text colour
  is warm peach `fed8ab`; I read "much more saturated, practically gray" as **desaturated**). Colour
  is an Inspector value on the scene instance, so Diego can tune it. The brillitos sparkle on page
  change gets the same gray tint so it does not clash.
- Level 1 is not touched.

## 2. Page 4: close-up camera when the Abuela lands

- **Found**: `AbuelaEntrance` already raises `CM Abuela Fall` (a vcam whose Composer looks at her)
  during the rumble, follows the fall and holds on her through her two lines, then
  `CutsceneCamera.Hide` blends back to the player's camera (BookCenter on page 4, because of
  `_pagesThatKeepTheirCamera`).
- **Do**: from the moment she lands until her lines end, the shot is a **close-up on her**, then the
  camera goes back to BookCenter as it does today. **Decided (Diego)**: reuse the existing
  `CM Abuela Fall`, no new vcam: after the impact it frames her close (a tighter Follow Offset /
  Composer framing, blended in as she lands), draggable in the Editor. The player's `CloseUp` mode is
  not used (it follows Kami, not her).

## 3. Newspaper posters 2-5: the BOTTOM piece is cut off

- **Found**: `CuttablePoster 2-5` are variants of `CuttablePoster 1` using stock `ObjetoCortable`:
  the piece in the `spriteTop` slot flies and shrinks, `spriteBase` stays. In the art, "Recorte" (the
  torn top ~60%) is in the flying slot and "Base" (the torn bottom ~58%) stays: the opposite of what
  Diego wants. Both pieces are full-size 670x1000 canvases with the piece already in place, and
  both have a torn edge.
- **Do (my call, no new script)**: in the four variants, swap which art sits in which slot: `Recorte`
  (top) becomes the staying piece, `Base` (bottom) becomes the flying piece that shrinks, and swap
  their z order so the torn edge overlaps correctly while both show. `ObjetoCortable` and Poster 1
  are untouched. A new cuttable type is not needed because the behaviour is already "one piece
  stays, one flies": only which half is which changes.
- Need to check in the Editor that the bottom piece's arc (up and toward the camera) reads well.

## 4. Page 2: two dialogue triggers for the guards

- **Found**: `CrimeSceneGate.prefab` has one `GuardTalk` trigger (box 14x10x12 centred on the gate)
  playing `CrimeScene_Guard` (2 lines, portraits `Policia1_STOP` / `Policia1_TALK`), plus two guards:
  `Cop1` (`CopEscort`, skin `Policia 2`) and `Cop2` (`CopEscort Female Variant`, skin `Policia 3`).
  `CrimeSceneGate` switches off every non-NPC object in its `_cops` list when they leave, so any
  new trigger goes in that list too.
- **Do**: replace the single box by **two** triggers, one around each guard, same two lines
  (same localization keys, no new text), each with its own portrait set. **Decided (Diego)**: keep the
  skins, match the portraits to them: the cop wearing `Policia 2` uses `Policia2_STOP/Talk`, the cop
  wearing `Policia 3` uses `Policia3_STOP/Talk` (the second line of each uses the other mood of the
  same set, as the current asset does with `Policia1`). Two new `DialogueSO` assets that reuse the
  existing keys `crime_guard_01/02`; the old `CrimeScene_Guard` stays. I will confirm which skin each
  guard really wears on the scene instance before wiring (the prefab says Cop1 = Policia 2, Cop2 =
  Policia 3, the scene may override).

## 5. Too many scissors in the bag (page 5, 4 slots, same item)

- **Found, not yet explained**: `InventoryManager.AddItem` has a real hole: when an item's amount
  drops to 0 and it is not in any slot, the loop reaches the first empty slot and puts the item
  there with count 0 (a ghost slot); and it adds a new slot at the first empty one before it has
  checked the later slots for the same item. Neither alone says how one item reaches **four** slots,
  and the Wardrobe shows one button per owned item, not per unit. I cannot reproduce it from code.
- **Do**: (a) make `AddItem` idempotent: look for the item in every slot first, only then use the
  first empty one, and never create a slot for a total of 0; (b) log the slot count when
  scissors are added so the next occurrence is traceable. **Diego's repro**: a semi-full playthrough started on page 2-4, one capture and one scissors
  pickup (the confiscation return), four separate slots seen on page 5. He asked if the arrest or
  the evidence handover to Grace could be the cause. On paper neither adds scissors: the capture's
  `ConfiscatedGear` takes papel + Pelusa and `LoseTijera` removes one, then `ReturnAll` gives each back
  once (`GetTijera` adds 1); Grace's handover removes only the five evidence items, no scissors. So the
  cause is still open; the bug is most likely in the bag's slot bookkeeping after that
  remove-then-add. Diego could only say they were there after the handover. Not reproduced; see the log/duplicate warning in `AddItem`.

## 6. Remove the Wardrobe tab

- **Found**: `FlapManager` hides the Wardrobe tab (and R1/L1 skip it) when the scene's start loadout
  has `_hasWardrobe = 0`; Level 1's loadout already does. Side effect of that flag: `Player` also
  stops granting the outfits as bag items.
- **Decided (Diego)**: only the tab goes; the outfits stay in the bag and tapping them still swaps.
  So `_hasWardrobe` stays on in `Level2_StartLoadout` and a new small flag on `GearLoadout`
  (`_showWardrobeTab`, default on, off for Level 2) is what `FlapManager` reads for the tab and the
  R1/L1 skip. The Wardrobe code and display stay in the project, only hidden.

## 7. Origami sliders in Level 2: same neat look as Level 1

- **Found**: Level 2's three routes (`OrigamiRoute_Cafe_Fold`, `_Cafe_Unfold`, `_Letter`) each
  override the white slider (`route`, object `...913180`) with `m_LocalScale.y = 11.27`. Level 1's
  `OrigamiRoute 1-Easy` keeps it at scale 1 with a short size (120 x ~643) matched to the arrows.
  The 11x stretch is why it runs off the screen. The three routes also keep the shared `Ruta`'s
  default arrow positions (start y 211, end y -284) from the base prefab.
- **Do**: bring the three Level 2 routes to Level 1's recipe: slider back to scale 1 with a length
  equal to the distance between the start and end arrows, arrows where they were, rotation (down
  for the fold, up for the unfold and the letter) kept. Numbers worked out from the prefab YAML;
  the visual check is Diego's in the Editor. Level 1 prefabs and `OrigamiRoute Avion` (the paper
  plane, used by both levels) are untouched. Diego: no other Level 2 origami is broken.

## Out of scope

Refactors, Level 1 changes, new art, anything not in the seven items.

## Verification

`python tools/compile-check.py` for any code touched (items 5, maybe 2). Prefab/scene/loadout edits
are YAML surgery: block counts and fileID uniqueness checked before and after. Nothing here is
verified at runtime from this machine, so each item ends as "built, not played"; Diego plays them.
Uncommitted until Diego says so. Docs updated in the same pass: `nivel2-y-ui.md` (page marker,
Wardrobe, cops), `cutscenes.md` (Abuela shot), `origami-y-tooltips.md` (sliders).
