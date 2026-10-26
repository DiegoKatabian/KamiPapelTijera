# Spec 024: EVA feedback batch (Oct 23-26 playtests)

**Status**: Draft, 2026-10-26. Triaged with Diego item by item (22 raw notes -> 20 tasks in 4 phases). Nothing built.
Milestone M9. Epic **#275**, tasks #276-#295 (1.A = #276 ... 4.G = #295, in spec order). Branch: none yet (written on `level-2-sounds`, uncommitted).
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

## Verification

`python tools/compile-check.py` is compilation only. Nothing here was heard or seen from this machine. The kickoff prompt
lists the playtest checklist per phase; every issue's close comment says what was **not** verified.

## Open questions (defaults in bold)

1. 1.A: `QuestCompleted02Pitched` (-3 dB already, used by the dam and the Rocoso fight): **leave it alone.**
2. 3.B: when the plane is disabled, show a small tooltip? **No, just disabled** (Diego said "deshabilitado").
3. 2.B: while the logo shows, does Esc/Start do anything? **No.**
