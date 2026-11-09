# Spec 024: EVA feedback batch (Oct 23-26 playtests)

**Status**: Phases 1 and 2 built, played by Diego and committed (2026-10-27; #276-#284 closed). Phases 3 and 4 built 2026-11-07/09,
played by Diego and committed in b7c90a94 (2026-11-09; 3.D was his manual fix); only 4.G #295 is open, waiting for `[POCHI]` #369. Next: specs 022, 023. Triaged with Diego item by item on 2026-10-26 (22 raw notes -> 20 tasks in 4 phases); on 2026-10-29
Phase 3 grew a second half, Kami's Atlas 15 beats (3.E-3.J = #329-#334).
Milestone M9. Epic **#275**, tasks #276-#295 (1.A = #276 ... 4.G = #295, in spec order) plus #329-#334. Branch: `after-eva-feedback`.
Siblings from the same triage: 022 (flying paper stickers) and 023 (settings menu), each its own epic.
**Not related**: specs 020 and 021 (magical girl change, magnifier and tool switching) are Diego's design work on another
branch; don't touch them. Note 021 adds a tool selector to the HUD strip: keep that in mind when touching the Flap.

## Source

Players' notes from the EVA (Nidstang, 23-24 Oct 2026) plus extras Diego added while triaging (rope entrance, `RopeCreak`,
the main-menu settings, audio channels, lupa sorting). Everything below was clarified with Diego; his answers are
quoted or paraphrased in each task. Where he said "this is a polish, not a bug" it stays a polish.

## Order of work and who does it

Run the phases in order. Phase 1 is cheap and independent; Phase 2 is small UI; Phase 3 touches player feel and shared
scripts; Phase 4 is Level 2 scene work (the riskiest to merge with Diego's test setup, so it goes last).
Each task is tagged **[O]** (Opus: shared scripts, scenes, prefabs, timelines) or **[S]** (Sonnet: asset tables, bank rows,
read-only inventories). `[P]` = parallel-safe with its siblings.

## Phase 1: numbers and audio

**1.A Bank volumes [S] `[P]`.** `Resources/AudioBank.asset`: `CarHornIdling` -6 dB (volume x0.501, "ta bien"),
`QuestCompleted02` -3 dB (x0.708; the `QuestCompleted02Pitched` row is a separate id and is NOT part of this), `RopeCreak`
+0.1 (the row's volume, 0.5 -> 0.6). Say the resulting numbers in the issue comment. Not heard from here.

**1.B Cut sounds on multi-cut [O].** Diego: when Kami cuts several objects in one swing, every cuttable plays its own
feedback sound at almost the same instant, so phase cancellation makes it sound doubled. A polish, not a bug. Wanted:
the 2nd/3rd instance pitched differently, and quieter so the sum is about as loud as one.
- Verified: the sounds are played **per cuttable**, not centrally: `ObjetoCortable` (`TijeraHit` + `PaperCut`, so a
  single cut already plays two clips), `HongoCortable` (same two), `CuttableDrapes`, `EntityCortable`, `PuertaCortable`,
  `PoliceOfficerCortable`, `RocosoCortable`, `Arbol3DCortable` (`TijeraHit`); `CuttableTwoPieces` inherits `ObjetoCortable`.
- Design (what I recommended, Diego approved "perfecto"): one static gate (`CutSoundGate`) that those call instead of
  `AudioManager.Play`. Cuts in the same swing form a burst: the 1st plays normally; from the 2nd on each sound is
  **delayed 15-40 ms** (decorrelates the phase, the main fix), **pitched** (+2 and +4 semitones, or a small random
  spread) and scaled by **1/sqrt(N)** (0.71 for two, 0.58 for three: incoherent sources add by power); **cap of 3 voices**
  per burst. How to detect "same swing": collect cuts for the rest of the frame (they usually share a physics step) plus a
  short window (~0.1 s) for stragglers; the build decides, but must not rely on callers knowing about each other.
- Keep `TijeraHitbox`'s `missed` semantics untouched (the miss sound plays on `OnDisable` only if nothing was cut).
- Both ids (`TijeraHit`, `PaperCut`) go through the gate. Level 1 and Level 2.

**1.C Paper payouts [O].** Posters (Level 2 only, the `CuttablePoster 1-5` family) must give **2 paper** each: "make sure
posters give 2"; Diego's note says posters currently give none. Level 2 trash cans give **4 paper** instead of 2 (`TrashCan.prefab`
and `TrashCan_ClueGlove`'s paper part; it was 2 since spec 006). No other rebalancing. Verify the poster prefabs have a
resource-giving component at all (the old note said they use stock `ObjetoCortable`, which gives nothing).

**1.D Spanish opening punctuation [S].** Diego: the opening marks (`¿` `¡`) are missing everywhere except Level 2. Audit every
Spanish table (`Assets/Localization Settings/Tables/*_es.asset`: DialogueTable, UITexts, TooltipTable, ItemTable, ...)
for sentences that end in `?` or `!` with no opening mark, and **show Diego the list before writing anything**. Only Spanish;
Portuguese has no opening marks and must not change. Also check the font actually has the glyphs (it may render blank).
Deliverable: the list, then (after his OK) the edits.

## Phase 2: UI and text

**2.A Dialogue text shifts up while typing [O].** Diego: the line moves up as the dynamic writing adds lines; he thinks
the text is vertically centred. **Correction to my first answer**: the typewriter *already* reveals with
`maxVisibleCharacters` on the fully laid-out text (`DialogueManager.EjecutarTypewriter`, issue #41.9), so "lay it out up
front" is not the missing piece. Diagnose first: the text element's vertical alignment (Middle/Center is the likely
cause: TMP re-centres as `maxVisibleCharacters` grows only if the visible-line bounds drive it, or a `ContentSizeFitter`
/ layout group resizes the box), the first-frame state before `ForceMeshUpdate`, the globe's rect. Fix with the smallest
change (top-anchored vertical alignment, or a fixed-height rect). Check Level 1's and Level 2's globe prefabs, and the
cutscene dialogue variants.

**2.B No Play during the kimmiarts logo [O].** Main menu's opening logo (`MainMenu.unity`): New Game must not be
usable while it shows. Lock until the logo ends **+1 s** (Diego). Gamepad and mouse. Don't touch `MainMenuManager`'s
joystick selection retry (#41.4): the lock must not make it select a button early.

**2.C Level Select title is clickable [O].** In the level select (`UI/LevelSelectMenu` + `LevelSelectCard`), clicking the
title text of a card does what clicking the card does. With the mouse and the gamepad (selecting a card enlarges its
portrait and title; both must stay in sync). Reuse the card's own click path, don't add a second one.

**2.D Flap above everything, and the origami pauses [O].** Diego: pressing Esc during an origami drops the Flap **behind**
the origami canvas; it must be in front of everything (the origami, the overlays), in both levels. And while the Flap is
open the origami **pauses**: no dragging behind the menu. Verified: no `sortingOrder` in `Assets/Prefabs/UI`, so the
ordering is probably canvas hierarchy order in the scenes; read both scenes' canvases. `MultipleRectCheck` reads the mouse
directly, so gate it on `FlapManager.IsMenuOpen`. Keep the Flap's own state rules (spec 011 Phase 4).

**2.E Trash-can tooltip flicker (page 4) [O], investigate only.** Players near page 4's jail trash cans either never saw
"press E to look inside" or saw it appear and vanish at once. Diego doesn't know when. Two hypotheses, test both before
changing anything: (1) the cans, and the page-turn sphere, share the Azul post-it and `HideTooltip(color)` hides by colour, so
leaving one trigger kills the tooltip another just showed (three cans plus the cell's sphere are close together); (2) a
global `HideTooltip()` (`DialogueManager.ShowDialogue` calls it; page 4 is full of dialogue and the post-capture
`ResetToFresh`). Output: a diagnosis comment on the issue, then the smallest fix if it is clear; otherwise ask.

## Phase 3: gameplay feel

**3.A Feedback when a stone can't be cut yet [O + maybe art].** Cuttable stones can't be cut until Kami has the sharpened
scissors; players didn't understand why. Wanted: (1) **sparks** when she hits one, as a **prefab variant of an existing
particle prefab** (Diego: reuse, e.g. the "Brillitos Mágicos" family), tuned to read as "bounced off"; (2) Kami's reaction:
try the existing `takehit` animation first; if it doesn't look right, a new Spine animation **`cut-pushback`** is needed
(then file a `[ART-VALEN]` issue, labelled `dependency-arte`). Find the stone object type first (probably an
`ObjetoCortable`-family prefab gated on the upgraded scissors; grep before assuming).

**3.B Don't let Kami refold the paper plane while it's ready [O].** Rule (Diego): if Kami **has the hat on with its jump
ready**, the plane origami is **disabled**; it enables again when she spends the jump. Both levels. Reason: refolding
spends paper and only gives the same hat with one jump.
- Verified: `Player.isPaperPlaneHat` and `augmentedJumpsLeft` (>0 = ready); `Evento.OnPaperPlaneHatLost` fires when it is
  spent or lost (`DestroyPaperPlaneHat`, also `ConfiscatedGear`). Lock = `isPaperPlaneHat && augmentedJumpsLeft > 0`; unlock
  on `OnPaperPlaneHatLost`. Page 4's `OrigamiRoute_PaperPlane` and Level 1's Avion route are the pedestals. A disabled
  pedestal shows no cost canvas and ignores the auto-prompt; a tooltip saying why is optional (ask Diego only if unsure).

**3.C Pull animation before a page turn [O].** When Kami triggers a page turn, she plays **`PullSolapas`** (Diego: use it
as the placeholder) and the normal page-turn sequence continues after it, with the delay lengthened enough to fit
(`PageScrollerManager.delayTime`, Diego agreed). Both levels. Player-initiated turns only; forced turns
(`TurnToNextPage(ridePage: false)`, the arrest) skip it.
- Gotcha already paid for: `PullSolapas` shares **track 3** with the attack and a swing in flight eats its pending
  `HandleAttack` event: call `Player.CancelAttack()` first (the same rule `PlayPullSolapa` follows;
  `docs/claude/spine-kami.md`, "quien se apropia del track 3").

**3.D Dam hitbox [Diego, manual].** Diego makes the cuttable dam's hitbox generous himself ("fix manual que puedo hacer yo"):
players stood a bit short or past it and the cut didn't register though it looked right. The issue exists to track it;
close it when he says it's done. (If he asks me instead: the trigger's box in Level 1's dam prefab.)

### Phase 3, second half: Kami's Atlas 15 beats (added 2026-10-29)

Pochi's Atlas 15 export (committed in 74edc838 "kami new animations") brought nine new Kami animations. The code for all
of them is built (see "Atlas 15" in `docs/claude/spine-kami.md`). Diego played and confirmed: the shader fix, `Transformacion`, and the
arrest's `Surprised`. Built but **not played yet**: idle break, scared face, the Abuela-fall surprise, the museum `Celebrate`, the
no-scissors overlay in the cell, and the bench and reflection logic (no instance is placed anywhere yet). Diego agreed to every extra
beat suggested on 2026-10-29. These tasks add those beats and place what is built. Story beats use the existing pieces:
`Player.PlayAnimationPose` / `EndAnimationPose` / `SetScaredFace`, or an `AnimationCue` with actor `Kami` on a `DialogueSO` line (PlayPose /
EndPose only today). A pose plays only while Kami is Idle and ends on any state change.

**3.E Benches in the levels [O].** Place `Prefabs/Interactables/BenchSeat.prefab` (trigger + `Seat`, tooltip `bench_sit`) on the
benches Kami can reach. `Kami_Bench.fbx` is used about 20 times in Level 2 (`Level2_Newspaper.unity`, and `Museum Page Parent.prefab`,
which pages 2 and 5 share); none are known in Level 1 (check). For each bench: the `Seat` X/Z on the seat, facing, a trigger box
that doesn't overlap a page sphere, a trash can or an NPC's talk trigger (all of them use E), and the bench's own collider not pushing
her off the seat. Put the trigger inside the bench's prefab when the bench is a prefab instance; otherwise use a scene instance.
Sit / stand with E, by moving, by jumping, with gamepad A.

**3.F Reflecting pools [O].** `Prefabs/Interactables/ReflectionZone.prefab` (`TriggerReflection`, no tooltip; standing still
`kamiAnims.lookIntoReflectionDelay` = 1 s inside it loops `Look into reflection`). **Neither level has a pool, puddle or fountain
object today** (searched by name 2026-10-29). Decide with Diego where they go (question below), then place and tune the zones.
On the river, the zone must be on dry ground outside `Rio`'s trigger, or she drowns while looking. If the animation looks at the
ground in front of her, the zone may need a facing like `TriggerBench._facing` (add it only if the play shows she looks away from the water).

**3.G Surprised at the gift (page 3) [O].** When the box opens and the Pelusa pops out (`GiftDialogueTrigger.Open`, during its
`_revealSeconds` hold, before `Natalia_GiftOpened`), Kami plays `Surprised`. It ends when Natalia's reaction ends, or when she moves.
Kami is not locked there, so check that the press that cut the ribbon doesn't skip it.

**3.H Surprised and scared at Ariel's accusation (page 5) [O].** On `Grace_MeetingAccusation`'s first line ("¡Ahí están las
fugitivas...!") Kami plays `Surprised` (a cue), and her scared face goes on until the evidence clears the girls. Default for the end:
off when `Museum_Evidence` starts, before its `Celebrate`. The cue system has no face action today. Recommended: Kami's
`TryApplyAnimationCue` reads `SetIdleOverlay` as "face overlay" (value = animation name, empty = off), so it stays data on the
DialogueSO. The alternative is code in `MuseumPage`.

**3.I Celebrate: Natalia freed, the escape, the third clue [O].** `Celebrate` (Reward -> RewardLoop, 3 s) on: `Natalia_Freed` (page 4
padlock, first time only, `PoliceStationPage._nataliaFreedLine`), `Natalia_Escaped` (page 4, back on the ground outside the window),
`Natalia_AllClues` (page 2, Natalia is happy there too). As `AnimationCue`s on each dialogue's first line. Check Kami is Idle at each
one (after the padlock cut she may still be in the attack's track 3, which is fine; after the window drop she may still be Landing).

**3.J Idle break on the title-screen Kami [O].** `MainMenu.unity`'s `kami_spine_titlescreen` is still on Atlas 13, dressed by
`Gear/LoadoutSkin`. Move it to Atlas 15 (its materials are already on the fill shader; check `LoadoutSkin` still composes the same look),
then play `Idle Break` every 5-10 s over her title animation. That needs a small component, since she is neither the Player nor a
`SpineCharacter`. Watch for a pop between `TitleScreen` and the break (mix time).

## Phase 4: Level 2 world (scene work, Opus only)

**4.A Sorting of background buildings and the newsstand [O].** Some background buildings and objects draw **over** Kami
(page 1 at least); the newsstand ("kiosquito de diarios") too. Rule: background buildings always behind everything; check
the newsstand. Find the renderer type first (3D meshes sort by depth and render queue, sprites by sorting layer/order) before
choosing the fix. Don't change the commit "EDIFICIOS DE FONDO"'s look, only what draws where.

**4.B `ClueLupa` sorting [O].** The magnifier markers (`Prefabs/Level2/ClueLupa.prefab`, a child of each clue) sometimes
draw behind or in front of fences and other 2D objects wrongly. Wanted: each lupa where it belongs in depth: the street
clues' lupas in **front of** the museum fences, the **watch**'s lupa **behind** them (it is inside the yard). Review every
placed lupa (hat, watch, glove can, the decoy can) and set their sorting. Note: not to be confused with spec 021's
magnifier tool.

**4.C Museum fences can be walked through [O].** Players walk **out** of the museum yard to the street through the
fences, both the front ones and the side ones (not by jumping). Diego clarified "clip" meant this, not a visual clip. The bars
are visual only: each `Reja_*` variant's `Panel` BoxCollider is the collision (#155); the gate gap is intentional on page 5
and blocked by `CrimeSceneGate` on page 2. Find the gap(s): the Panels' extents vs the posts, the `Reja Central Rota`
doors, the corners, and whether the page-turn walls interfere. Must not change the page 2 gate or Ariel's arrest route
on page 5.

**4.D Both police tapes uncuttable until the cops leave [O].** Page 2 has **two** `CuttablePoliceTape` objects stacked on
purpose ("they look nice", keep both). Today only one has the blocker, so the other can be cut early. Neither must be
cuttable until the cops have left (`CrimeSceneGate`). Don't delete either.

**4.E Hat clue: bigger pickup, away from the page edge [O].** `CluePickup_Hat` (page 2, on a parked car's hood): Kami
shouldn't have to climb onto the car, and many players pressed E near it and, being by the page edge, turned the page by
accident. Fix: **enlarge the pickup's trigger** so walking close collects it with no E, and **move the hat away** from the
next-page trigger (`esferaNext`). Keep its `ClueMarker` lupa following it.

**4.F Rope enters with a fade and a sparkle [O].** `CuttableRope` (page 5 catapult; switched on by `Catapult.ArmRoutine`)
should appear with a **fade-in and a magic sparkle** instead of popping in. Reuse the `Brillitos Mágicos` instance already
nested in the rope for a one-shot burst at the moment it appears (its continuous sparkles are in `Catapult._enableOnBoard`).
Don't change when it becomes cuttable (the trigger stays off until Kami boards).

**4.G Wheels turn [O, may need Pochi].** The wheels of every car (ambient traffic, the patrol cars while they drive) rotate
**only while the car moves**; parked and idling cars don't. `TrafficObstacle` already measures its collider's velocity
every `LateUpdate` (`GroundVelocity`): spin at `speed / radius`. First check whether the wheels are separate objects in
the car fbx variants (`PaperCar variacion 1-3`, `PaperCar Policia`, and the original mesh); if a car is one mesh, that car
needs a `[POCHI]` issue and is skipped.

## As built

**Phase 1 (2026-10-26, branch `after-eva-feedback`; played by Diego 2026-10-27, all good; committed with Phase 2).**
- 1.A: `CarHornIdling` 0.5 -> 0.251, `QuestCompleted02` 0.252 -> 0.178, `RopeCreak` 0.5 -> 0.6 (`AudioBank.asset`).
- 1.B: `Managers/Audio/CutSoundGate.cs`, a component on `AudioManager.prefab` (every scene instances that prefab), plus
  `AudioManager.ScaleVolume(handle, factor)`. The eight callers listed above call `CutSoundGate.Play`. Burst = same id within
  0.1 s of its first cut (unscaled time, so a burst never waits for the Flap); the first voice plays at once, the 2nd/3rd after
  15-40 ms at +2/+4 semitones, all voices rescaled to 1/sqrt(N), a 4th dropped. All of it is Inspector-tunable on the
  component. Detail: `audio-y-particulas.md`.
- 1.C: `CuttablePoster 1.prefab`'s script switched from `ObjetoCortable` to `PickupCortable` (paper x2, no respawn, Diego's
  OK), which posters 2-5 inherit as variants. `TrashCan.prefab` `_amount` 2 -> 4 (Level 1 has no trash cans), and the scene
  override that turns page 5's glove can into a paper can 2 -> 4. `TrashCan_ClueGlove` has no paper part (it gives only the
  glove), so it is unchanged.
- 1.D: the 56 fixes of `es-punctuation-audit.md` applied to the five `*_es.asset` tables exactly as listed (each string
  checked against the file before writing; Portuguese untouched). No font work needed.

**Phase 2 (2026-10-27, same branch; played by Diego 2026-10-27, all good; committed with Phase 1).**
- 2.A: cause confirmed in the TMP 3.0.6 source: Middle alignment centres on `maxVisibleDescender`, the lowest *revealed* line
  (`useMaxVisibleDescender`, on by default), so each new line moved the block up. `DialogueManager.EjecutarTypewriter` sets
  `useMaxVisibleDescender = false` on its text: centred on the whole line from the first character. Covers every
  `DialogueSystem2` (both levels, both end cutscenes, the main menu intro). No prefab change.
- 2.B: the logo is `MainMenu.unity`'s `kimmiarts` (fades in, holds 2 s, fades out, about 4 s). `ImageFadeIn.IsFadedOut` (new) +
  `MainMenuManager._studioLogo` / `_secondsAfterLogo` (1): `OnNewGameButtonDown` refuses the press until 1 s after the fade-out
  ends. The joystick selection retry (#41.4) is untouched, so New Game can be selected early but not pressed. Esc/Start do nothing.
- 2.C: each card's `Caption` (a child of the card's `Button`, hanging below it) had raycasts off; switched on for both cards, so a
  click or hover on the title bubbles to the card. No code.
- 2.D: `FlapManager.prefab`'s root got its own `Canvas` (override sorting, order 5) + `GraphicRaycaster`: above the scene canvas
  (origamis, overlays, post-its, `ScreenFade`, order 0) and Level 1's dialogue canvas (order 2) in both levels, with click priority
  to match. The loading canvas (order 2) only exists in `MainMenu`, which has no Flap, so it was left alone. `MultipleRectCheck`
  takes no input while `FlapManager.IsMenuOpen` and on the frame it closes (so the B or click that closes it never reaches the
  fold); a drag in progress is dropped without penalty (arrow back to the fold's start, no crumble or red flash), the gamepad
  cursor hides and reappears on the arrow. Side effect: the HUD strip now draws over the paper particles flying to it (spec 022).
- 2.E: diagnosis on #284 (once-per-session show spent by dialogues' hide-all; not reset by a capture; the left can shares the
  blue post-it with `esferaPrev`). Fix: `TriggerSolapa._tooltipOnlyUntilOpened` (on in `TrashCan.prefab`, `_maxTooltipShows` 1 -> 0):
  shown on every approach to an unopened can, hidden when opened, back after `ResetToFresh`. The can/sphere overlap was not moved.

**Phases 3 and 4 (2026-11-07, built on branch `025-paper-book` on top of spec 025's uncommitted work, at Diego's request: he
tests everything together; compile-check clean, YAML block/id/reference checks clean on every touched file, nothing played).**
- 3.A: the stone is the Rocoso (`RocosoCortable` is used only by `Rocoso.prefab`; the normal scissors bounce off under
  `_minDamageParaCortarlo`). On a bounce: `Prefabs/Particulas/RocosoBounceSparks.prefab` (a variant of `BrillitosSolapa`: faster,
  0.35 s, orange, 18 particles, gravity) on the rock's surface on Kami's side, and `Player.PlayCutBounceReaction` (the take-hit
  animation, no damage or stun). `_bounceSparks` / `_kamiReacts` on the component. No `cut-pushback` art requested yet: Diego judges the take-hit.
  2026-11-08 (Diego): every cuttable stone too (`PiedraCortable`, on `Piedra Prefab` and so all its variants): on top of its existing
  `Tijera_Miss_Roca` clink. Both go through `Cortables/CutBounce`.
- 3.B: `TriggerOrigami._lockedWhileHatReady` (on by default, only matters when the pedestal's origami is an `OrigamiPaperPlaneHat`,
  so no instance needed wiring): while `isPaperPlaneHat && augmentedJumpsLeft > 0` no cost canvas, no prompt, no check; a pedestal
  Kami stands on when the hat is given switches off one frame later; it comes back on `OnPaperPlaneHatLost` if she is still on it
  (checked by collider overlap, since a teleport sends a locked pedestal no exit). No tooltip (open question 2's default).
- 3.C: `PageScrollerManager._pullBeforeTurn`: a turn started at an edge sphere plays `PullSolapas` (0.8 s, through
  `Player.PlayPullSolapa`, which cancels the attack first) and both turn delays grow by the clip's real length at runtime, instead of
  editing `delayTime` in each scene. `TurnToNextPage` (forced turns) never pulls.
- 3.D: not touched (Diego's manual fix).
- 3.E: `BenchSeat` placed under 7 benches: page 1's three, page 3's two and the museum yard's two (`Museum Page Parent`, so pages
  2 and 5). Skipped: page 4's four (two sit past the book's edge walls, two are on the previous-page sphere, where E would both sit
  her and turn the page). `TriggerBench._fitToBench` fits the trigger box and the seat to the parent bench's meshes at Awake
  (benches are scaled 0.016 and rotated differently), padding 1.5 / 2.5 / 1.5 world units.
- 3.F (2026-11-08): the pools are for Level 3 (Diego), so for now they live in a test scene: `Prefabs/Interactables/ReflectingPool.prefab`
  (a blue `ReflectingPoolWater.mat` disc, no collider, plus a nested `ReflectionZone`) and `Assets/Scenes/Tests/ReflectionPoolTest.unity`, a copy
  of Level 1 (as on 2026-11-08) with the level intro off and one pool 9 units right of Kami's start, at a height worked out from her
  start (drag it onto the ground if it floats). Not in the build.
- 3.G: `GiftDialogueTrigger._kamiPose` (`Surprised`) on the box opening; an EndPose cue on `Natalia_GiftOpened`'s end.
- 3.H: `Grace_MeetingAccusation` line 0: Kami `Surprised` + `SetIdleOverlay` (her scared face on); `Museum_Evidence` line 0:
  `SetIdleOverlay` empty (off). `Player.TryApplyAnimationCue` now reads `SetIdleOverlay` as the scared face (any value = on).
- 3.I: Kami `Celebrate` cues on the first line of `Natalia_Freed`, `Natalia_Escaped` and `Natalia_AllClues`. Kami's PlayPose cues
  now wait up to `Player._poseCueMaxWait` (1 s) for her to stand still (she is still Landing after the window drop).
- 3.J: MainMenu's `kami_spine_titlescreen` on Atlas 15's SkeletonData, and **no idle break** (Diego, 2026-11-08: the menu Kami
  sits in `TitleScreen`, the idle break doesn't belong there; the `TitleIdleBreak` component built the day before was removed).
- 4.A: the 21 background building sprites (`Assets/2D/Props/Edificios`) sort 10 lower than before (Valen's -4..0 became -14..-10),
  so they keep their order among themselves and are behind every order-0 sprite and Kami. The newsstand (`PaperRevisteria`) is an
  opaque mesh that writes depth: it only covers Kami when she is behind it, so it was left alone.
- 4.B: the lupas' material (`Sprites Unlit`) is in queue 2982, below every other sprite (3000), and URP sorts by queue before distance:
  other sprites always drew over the lupas. `ClueLupa.prefab`'s sprite now has sorting order 1 (in front of order-0 sprites); the
  fences are opaque, so the watch's lupa inside the yard is still hidden behind the bars by depth.
- 4.C: the fence `Panel` boxes were 0.4 world units thick against Kami's 1-unit CharacterController radius; now 16 fbx units
  (about 1.2 world) in `Reja.prefab` and the six panel variants (not the two fallen gate doors). **Unverified**: whether the leak
  was the thin panels or a gap at the columns (their colliders are inside `KamiMuseo.fbx`): Diego, note where players get out.
- 4.D: `CrimeSceneGate` gates every `CuttableTwoPieces` under it (the scene's second `PoliceTape (1)` was never gated).
- 4.E: the hat was next to page 2's **previous**-page sphere (world x -39.8), not `esferaNext`. Its walk-into trigger is now 7 x 5 x 6
  (was 2.5 x 3 x 2.5), leaning 2 units toward the page's middle, so walking up collects it before the sphere. The hat itself was not
  moved (it sits on a parked car's hood that couldn't be checked from here).
- 4.F: `Catapult.RopeAppears`: the rope's sprites fade in over `_ropeFadeSeconds` (0.6) and a `BrillitosSolapa` burst (x4) plays on it
  when the catapult is armed. `RopeCreak`'s volume was already raised in 1.A.
- 4.G: blocked: every car model is one mesh (only the windows are separate). Filed #369 `[POCHI]`.


**Diego's play of Phases 3-4 (2026-11-08/09) and the fixes after it** (compile-check clean, YAML checks clean, not played again):
- Level 1, pages 2-5 of Level 2 and the main menu: all fine. Page 2's hat: Diego tuned its pickup trigger and the page sphere by hand.
- The Kami hidden behind something on Level 2 page 1 was the floor's material set to transparent; Diego made it opaque.
- 3.C: after the 0.8 s pull Kami stood still for `delayTime` (0.5 s) before the sheet lifted. Turns with a pull now use their own
  timing on `PageScrollerManager`: `_pullSpeed` (playback speed, 1) and `_gapAfterPull` (seconds from the pull's end to the sheet, 0.1,
  negative overlaps). The page opening keeps its authored distance from the sheet. `Player.PlayPullSolapa` / `PlayerView` take a speed.
- 3.E benches: seated, her CharacterController stays on whatever she stood on, so from the floor she sank into the bench and from its
  top she floated; and the fit overwrote the `Seat` at Awake, so moving it did nothing. Now the `Seat`'s Y is where her feet go for the
  sit pose, and `Player.UpdateSitHeight` raises or lowers her drawn body (the skeleton object, never the controller) to it over
  `_sitHeightBlendSeconds` (0.3), back down when she stands up. The fit puts the `Seat` at the bench's top centre plus
  `TriggerBench._seatOffset` (0, -1.8, 0 world units, from Kami_Bench's slab top 2.44 and her seated hips 2.4 above her feet): tune it
  once on `BenchSeat.prefab`.
- Spec 025's intro skip: the "Hold E to skip" line became `UI/HoldToSkipPrompt` on the intro prefabs' Skip Prompt: the Interact key
  icon (44 x 44, bottom right) inside a ring (`2D/UI/HoldToSkip/HoldToSkipRing.png`) that fills while it is held. Invisible until the
  first press, it fades in, drains when let go and fades out 1.2 s later. Reusable by any cutscene (`SetHold(progress, held)`, `Hide()`).
  The UITexts key `book_intro_skip` is no longer used. **Kami > Book Intro > Build** still makes the old text prompt on a fresh prefab.

- 3.F reflection (Diego picked "mirror Kami, other look", 2026-11-09; built, compile-checked, not seen): `ReflectingPool.prefab` now
  has `Pool Inside` (the old Water disc, `Kami/Pool Hole`: deep water colour, depth pushed to just short of the far plane, queue 2500),
  `Surface` (`Kami/Pool Surface`: tint, rings moving out, bright rim, queue 3100), a `Reflection Plane` mark at the disc's top, and
  `Mirror Kami`: a second Atlas 15 skeleton dressed by `LoadoutSkin` with `Level2_StartLoadout` (the detective look), drawn with
  `Materials/Reflection/KamiReflection_1-3` (Kami's materials with `_RECEIVE_SHADOWS_OFF`: under the floor it would sit in the floor's
  shadow). `TriggerS/ReflectionMirror` on the root places it upside down under the plane every frame, plays the real Kami's tracks at her
  times under its own skin (attachments, draw order) and then copies her bones' world pose (mixes and physics). It is seen only
  through the pool: the floor's depth hides it everywhere else, and a ray through the pool can't reach anything standing behind it.
  Knobs on `ReflectionMirror`: `_tint`, `_extraAnimation` (something only the reflection plays, on track 8, e.g. a face), `_maxDistance`.
  **Reworked the same day after Diego's play**: the upside-down copy under the floor went down through the book and showed outside
  the pool (the book didn't hide it by depth). Now the copy lies flat on the water at her feet, upside down with her head towards the
  camera and her face up (`ReflectionMirror` rotates her up axis +90 on x and flips y), and the pool is a real mask: `Kami/Pool Hole`
  writes stencil 1 (normal depth again, no far-plane trick) and `KamiReflection_1-3` draw only where the stencil is 1
  (`_StencilComp` 3 = Equal). Heights in the prefab: inside top 0.05, `Reflection Plane` 0.052, `Surface` top 0.055 (pool-local).
  The water look is on `Materials/Reflection/PoolInside|PoolSurface`. `ReflectingPoolWater.mat` (Diego's metallic tweak) is no longer
  used by the prefab and was kept. Risks: the cylinder caps' UVs drive the rings and the gradient; a depth-of-field effect reads the pool
  as far and may blur it.

## Verification

`python tools/compile-check.py` is compilation only. Nothing here was heard or seen from this machine. The kickoff prompt
lists the playtest checklist per phase; every issue's close comment says what was **not** verified.

## Open questions (defaults in bold)

1. 1.A: `QuestCompleted02Pitched` (-3 dB already, used by the dam and the Rocoso fight): **leave it alone.**
2. 3.B: when the plane is disabled, show a small tooltip? **No, just disabled** (Diego said "deshabilitado").
3. 2.B: while the logo shows, does Esc/Start do anything? **No.**
4. 3.E: which benches are sittable? **Every bench Kami can walk up to; skip decorative ones behind fences or off the walkable area.**
5. 3.F: where are the reflecting pools? **Ask Diego at the start of the session; the default is Level 1's river shore, on dry
   ground, at one or two calm spots.** Level 2 has no water.
6. 3.H: the scared face's end on page 5. **When `Museum_Evidence` starts.**
7. 3.J: title Kami's break interval. **Same 5-10 s as gameplay.**
