# Spec 012: Followers ride the page turn with Kami (Level 2)

Status: BUILT 2026-10-08, compile-checked only, NOT playtested. Phases 1 and 2 done (scenes wired); docs updated; the
graphify refresh and Diego's playtest/tuning are open. Decisions taken with Diego's "defaults to everything": followers
start moving when the ride starts (`IsRidingPage` rising edge, i.e. `StartRidingPage`), the seat numbers are
`PaperPlaneRide`'s, and the dead Atlas 12 "The Paper Model" in `MainMenu` (nothing referenced it) was deleted.
Deviations from the draft: `NPC.FaceDirection(bool)` was added (the flip logic of `UpdateSpriteFlip`, extracted, behavior
unchanged); landing tries ahead of Kami, then behind, then under her, and takes a `_landingSampleRadius` field;
`Player.ResetSkeletonPhysicsMemory()` wraps the Spine reset so both call sites share one guard.
After Diego's first playtest (2026-10-08) three things landed alongside, outside this spec's original scope: the café
fold is driven by Natalia's conversation (`TriggerOrigami._openOnlyByCode`), `PageExitLock` gates the next-page
spheres, and the arrest escort was lowered in speed and handed to the cops (spec 013).
Level 2 (`Level2_Newspaper.unity`). Tasks: `tasks.md`. Kickoff prompt for the implementation session: `kickoff-prompt.md`.
Triage and evidence: `docs/triage/2026-10-07-followers-page-turn-and-kami-physics.md`.

## Request (Diego, 2026-10-07/08)

1. When Kami turns a page, Natalia (and the Abuela, when she follows) must **not** stay frozen at the
   old page's far edge. They **grab on to Kami**, fly over the page with her (the same idea as the paper
   plane ride), and **start the new page together**.
2. Level 2 only. Level 1's Abuela flow stays untouched.
3. Every follower boards, even one far from Kami at the start of the turn (they ease to their seat so
   they always arrive together).
4. Three small things travel with this spec because they belong to the same area:
   - During the page 3 arrest, Kami must **walk** to the patrol car, not skip.
   - Kami's skeleton physics must not be kicked by the page turn's teleports (found while triaging the
     shaking).
   - The main-menu Kami must wear the Libro 1 outfit with her original gear and no scissors.

## Already done (2026-10-08, not part of the tasks)

- **The shaking Kami is fixed**: `Kami.prefab`'s `Rigidbody` was non-kinematic with gravity, fighting the
  `CharacterController`; the root wobbled and spine-unity fed that wobble 1:1 into every physics
  constraint. `m_IsKinematic: 1` on the prefab (Diego confirmed it in Level 2 as a scene override first,
  and tested that only the Y feed mattered). The Level 2 scene override is now redundant but harmless.

## Current state (read from the code, 2026-10-08)

- **Nothing carries followers across a page turn.** `PageScrollerManager` -> `PlayerPageSpawnManager`
  place only Kami. Every NPC `WarpTo` in the codebase is page-specific (`ArrestCutscene`,
  `PoliceStationPage`, `MuseumPage`, `AbuelaEntrance`, `PaperPlaneRide`).
- `PageNavMeshManager.OnNewPageOpen` removes the old page's `NavMeshData` and adds the new page's, so a
  follower's agent reports `isOnNavMesh == false` the moment the page opens, and `NPC.IsAgentUsable()`
  makes every `MoveTowards` a no-op with a throttled `[NPC] ... CANNOT MOVE` warning.
- The turn timeline: `ChangeToNextPage` (`OnPageTurnStart`) -> `delayTime` -> `CerrarPaginaCoroutine`
  (freezes Kami with `inDialogue`, instantiates `HojaMaster[_Rev]`, calls `Player.StartRidingPage`) ->
  `popupDelayTime` -> `AbrirPaginaCoroutine` (`OnNewPageOpen`: NavMesh swap, page folders toggled) ->
  the leaf finishes -> `Hoja.HojaIdleStart` -> `OnPageFinishTurning` -> `PlayerPageSpawnManager.FinishRide`
  (`StopRidingPage` + the final placement) and `PageScrollerManager.FinishTurning` (unfreeze).
- `Player.LateUpdate` holds Kami at `edgeBone.position + (0,0,_rideZOffset) + RideRootOffset` while
  `IsRidingPage`. `Player.IsRidingPage` is the one true "Kami is in the ride" signal. A forced turn with
  `TurnToNextPage(ridePage: false)` (the page 3 arrest) never sets it.
- `Level2/PaperPlaneRide.cs` already implements the mechanic to reuse: a `Rider` (NPC + seat + bob
  phase) is stopped (`StopFollowingPlayer`), its agent gets `updatePosition = false`, and a `LateUpdate`
  writes `player.transform.position + seat` (X mirrored with `Skeleton.ScaleX`). Landing is `TryLand`:
  `NavMesh.SamplePosition` near `player.FeetPosition + right * spacing * slot`, `agent.Warp`, then
  `StartFollowingPlayer()`; no floor -> park and retry every 0.5 s.
- Followers are plain `NPC`s (`isFollowing`, `player`, `navAgent`, `StartFollowingPlayer` /
  `StopFollowingPlayer` / `WarpTo`). `StartFollowingPlayer` reparents to Kami's parent (the scene root in
  Level 2), so followers are NOT inside a page folder and stay active across `TogglePages`.
- `Player.SkeletonAnimation` is public; `SkeletonAnimation.ResetLastPositionAndRotation()` is public in
  the vendored spine-unity.

## Functional requirements

- **FR-001** While Kami rides a page edge (`Player.IsRidingPage`), every NPC with `isFollowing == true` and
  an active GameObject boards: it stops following, its agent stops driving the transform, and it is
  held at a seat around Kami every frame.
- **FR-002** A follower far from Kami eases to its seat over a short, tunable time (default 0.35 s,
  smoothstep) instead of snapping. After that it tracks the seat exactly (no lag behind Kami).
- **FR-003** Seats are per slot (nearest follower to Kami = slot 1), X mirrored with Kami's facing,
  plus a small bob (placeholder for the real "holding on" pose). All numbers are Inspector fields.
- **FR-004** When Kami's ride ends (`IsRidingPage` goes false: `FinishRide` has already placed her on the
  new page), every rider lands next to her on the NEW page's NavMesh (`agent.updatePosition = true`,
  `Warp`), then follows again. A rider with no floor near Kami parks and retries (as `PaperPlaneRide`).
- **FR-005** A turn that never sets `IsRidingPage` (the arrest's `ridePage: false`) boards nobody; the
  cutscene's own warps still own those followers.
- **FR-006** Both directions work (forward `HojaMaster`, backward `HojaMaster_Rev`).
- **FR-007** Nothing about a follower may stay in the "boarded" state: if the component is disabled, the
  scene unloads or the ride ends in any way, agents get `updatePosition = true` back and followers resume.
- **FR-008** Level 1 is untouched: the component is placed in `Level2_Newspaper.unity` only and no Level 1
  script changes.
- **FR-009** Kami's skeleton physics ignore the page-turn teleports: after the ride snaps her onto the
  edge bone and after the final placement (`PositionPlayerAtPoint`), the physics' last-position memory
  is reset so the jump is not injected as a huge translation. Respawns (`RespawnPlayer`) go through the
  same method, so they get it too.
- **FR-011** The arrest's escort walk reads as a **walk**: Kami's stick amount during
  `ArrestCutscene`'s escort stays below `Player.walkThreshold` (0.5), so she is in the `Walking` state and
  speed scales down with the input (`_move *= CurrentSpeed` after the normalize, so 0.45 = 45% of her max
  speed). Today `_escortWalkInput` is 0.6 in `ArrestCutscene.prefab`, which is above the threshold, so she
  skips. The escort redesign (cops lead, girls are marched) is a SEPARATE spec, not this one.
- **FR-010** The main-menu title Kami (`kami_spine_titlescreen`, Atlas 13) wears a skin composed from
  `Level1_StartLoadout` (Libro 1 outfit + base shoes, no scissors) with the existing `SpineSkinComposer`
  and `GearCatalog`, in `Start`. The loadout is a field, so it can't drift from Level 1 by accident.

## Design

### 1. `PageRideFollowers` (new, `Assets/Scripts/Level2/`)

One component at the **scene root** of `Level2_Newspaper.unity` (not under a page folder: those are
switched off mid-turn; same reason as `PageMusicManager`).

- **Edge detection by polling, not by event.** `Update` compares `player.IsRidingPage` with last
  frame's value: `false -> true` = board, `true -> false` = land. This is on purpose: the event order of
  `OnPageFinishTurning` subscribers (`PlayerPageSpawnManager.FinishRide` must place Kami BEFORE the
  followers land next to her) is not guaranteed, and a poll one frame later does not depend on it. It
  also lands followers if the ride ends in some unexpected way (FR-007).
- **Who boards**: `FindObjectsOfType<NPC>()` once per ride start, filtered `isFollowing &&
  activeInHierarchy`. Generic on purpose (Natalia, the Abuela spawned at runtime, future followers) and
  no wiring. Ordered by distance to Kami for the slots.
- **Seat hold** in `LateUpdate` with `[DefaultExecutionOrder(...)]` high enough to run after
  `Player.LateUpdate` (the project has no `ScriptExecutionOrder` asset): `position = Kami.position +
  seat`, with the boarding ease blended in by a 0..1 factor. `Kami.transform.position` is the thing
  `Player.LateUpdate` just wrote, so the seat has no lag.
- **Boarding** copies `PaperPlaneRide.Board`: `StopFollowingPlayer()` BEFORE touching the agent, then
  `agent.updatePosition = false`. The agent keeps its internal position on the (about to be swapped)
  mesh; it is re-attached by the `Warp` at landing, which is why the landing must happen after
  `OnNewPageOpen` has activated the new page's NavMesh (it has: that event fires seconds before the
  leaf finishes).
- **Landing** copies `PaperPlaneRide.TryLand` (`SamplePosition` radius 5, `Warp`, `StartFollowingPlayer`,
  park-and-retry). Do NOT edit `PaperPlaneRide`. A tiny static helper used only by the new component is
  fine; unifying the two is a follow-up, not this spec.
- **Sprite flip while riding**: mirror Kami's facing (`NPC._sr.flipX` or the NPC's own flip helper,
  `_invertFlip` considered), so Natalia doesn't face backwards on the edge.
- **Defaults** (first guesses taken from `PaperPlaneRide`, Diego tunes them live in a turn):
  slot 1 seat (-2.5, -0.5, 0.6), slot 2 seat (-5, -1.2, 0.6), bob 0.35 u at 6 rad/s, board ease 0.35 s,
  landing spacing 3 u.

### 2. Physics reset on teleports (`Player.cs`, `PlayerPageSpawnManager.cs`)

`SkeletonRenderer.ApplyTransformMovementToPhysics` turns every frame's world-position delta into a
`Skeleton.PhysicsTranslate` (1:1, spine-unity applies `SkeletonDataAsset.scale` at JSON load). The ride
makes Kami jump from her spot onto the edge bone (one frame, many units) and the final placement jumps
her again. Calling `Player.SkeletonAnimation.ResetLastPositionAndRotation()` right after each of those
writes makes the next frame's delta start from the new position, so the jump is ignored. Two call
sites, each placed right AFTER the line that moves her: `Player.LateUpdate` on the first ride frame
(after `transform.position = newPos`; `_rideLoggedFirstFrame` already marks that frame; not in
`StartRidingPage`, which doesn't move her) and `PlayerPageSpawnManager.PositionPlayerAtPoint` (after
the CharacterController is re-enabled). Resetting after the write works whichever of Spine's and
Player's `LateUpdate` runs first that frame. It does NOT freeze the physics during the ride: the
smooth ride motion still makes the hair and cape trail, which is the look we want.

### 3. Main-menu Kami (`LoadoutSkin`, new, `Assets/Scripts/Gear/` or `UI/`)

Small `MonoBehaviour` on `kami_spine_titlescreen`: a `[SerializeField] GearLoadout` (default
`Level1_StartLoadout`), and in `Start`: take `Equipped`, sort by `GearItem.Slot` (the order
`PlayerGear.EquippedInCompositionOrder` gives: Outfit, Scissors, Feet, Hat), `Compose`, `SetSkin`,
`SetSlotsToSetupPose`. Guard clauses with `[LoadoutSkin]` warnings for a missing catalog or loadout.
The scene's `initialSkinName` stays as the pre-`Start` fallback; set it to `FullSkins/Kami Libro 1`
so the first frame is already right. The inactive `The Paper Model (SkeletonAnimation)` in the menu is on
Atlas 12 with `Tijera_Normal`: decide with Diego whether it is dead (delete) or used (point it at
Atlas 13 + the same component).

### 4. The arrest walk (`ArrestCutscene.cs`, `ArrestCutscene.prefab`)

Root cause: `_escortWalkInput` = 0.6 > `Player.walkThreshold` = 0.5, and `PlayerModel.UpdateLocomotionState`
picks `Skipping` for any stick magnitude at or above the threshold. Two changes: the prefab value to 0.45
(speed follows), and a guard in `ArrestCutscene.Update` that clamps the input to just below
`_player.walkThreshold`, with a one-time warning when the Inspector value was above it, so tuning the
number later can't bring the skip back. The Walking animation plays at a fixed rate, so Diego checks the
foot sliding at 45% speed; if it slides, tune `_escortWalkInput` up towards (but below) the threshold.

## Defaults and decisions

- Every follower boards (decided). Level 2 only (decided).
- Seat poses are placeholder sprites + bob until Valentino's "holding on" pose exists. Nothing in the code
  depends on that art.
- Seats are in WORLD units relative to Kami's root, like `PaperPlaneRide`.

## Risks and what only a playtest can answer (name these, do not paper over them)

- **Does it look like she is holding on?** Seat offsets, bob and the ease are taste. Diego tunes them on
  a live turn (an `OnValidate`-free design: read the fields every frame so Play-mode tweaks apply).
- **Is a long ease visible?** A follower far from Kami crosses the page in 0.35 s. If it looks like a
  teleport, lengthen the ease or start it at `OnPageTurnStart`.
- **The new page's NavMesh near Kami's landing.** If a page's mesh isn't baked near `pageEntryX`, the
  landing parks the follower; make sure that can't happen on pages 1-5 (task 2.B checks every page's
  entry point with `NavMesh.SamplePosition` logs).
- **Interaction with page-specific follower code**: `MuseumPage` (page 5 intro), `PoliceStationPage`
  (cells, `BringOutside`), `ArrestCutscene` and `PaperPlaneRide` all move followers. They run on
  other triggers than a ridden turn, but play every transition (1->2, 2->3, 3->4 arrest, 4->5, and
  backwards) to be sure nothing double-warps.
- **Frame ordering**: `PageRideFollowers.LateUpdate` must read Kami AFTER `Player.LateUpdate`.
  `[DefaultExecutionOrder]` handles it, but a wobble on the riders during the ride means it didn't.
- **Compile-check is not runtime.** Everything about feel and timing is Diego's playtest.

## Out of scope

- Real "holding on" art/animation (Valentino).
- Unifying `PaperPlaneRide` and `PageRideFollowers` landing code.
- Level 1 followers.
- Followers during forced/cutscene turns (they have their own scripted placement).
- The escort redesign (cops lead by NavMesh, the girls are marched in front of or behind them): its own
  spec.
- Camera changes during the ride.

## Verification

`python tools/compile-check.py [tag]` after each code task (baseline warnings: `JumpFloodOutlineRenderer`
CS0162, `HongueroTiburcioDialogueTrigger` CS0414). Compilation only. Diego's playtest checklist:

1. Pages 1 -> 2 and 2 -> 3 with Natalia following: she boards, flies with Kami, lands next to her,
   follows again; no `CANNOT MOVE` warning afterwards.
2. 3 -> 4 (the arrest): unchanged, nobody boards, Natalia still goes to her cell.
3. 4 -> 5 after the escape, with Natalia AND the Abuela: both board with their own seat, both land.
4. Turn back (prev page): same, mirrored.
5. Turn a page with a follower standing far away: she eases to the seat, nothing pops.
6. Open the Flap mid-turn and close it: followers stay on their seats.
7. Kami's hair/cape/skirt don't flail at the end of a turn or after a death respawn.
8. Level 1 page turns (Abuela quest) behave exactly as before.
9. Arrest (page 3): Kami walks to the car in the Walking state, no skipping, and still arrives before
   `CUE_BoardCar` fires.
10. Main menu: Kami in Libro 1 + base shoes, no scissors, physics alive, same look as a fresh Level 1.

## Docs to update when done (living-docs rule)

- `docs/claude/paginas-y-hoja.md`: followers on page turns (the polling design, the order trap).
- `docs/claude/nivel2-y-ui.md`: `PageRideFollowers` in the Level 2 section.
- `docs/claude/spine-kami.md`: the physics note (Rigidbody + inheritance, already written 2026-10-08) and the
  main-menu `LoadoutSkin`.
- `CLAUDE.md`: the main-menu Atlas note (the active title Kami is on Atlas 13) and this spec in the
  active work list.
- `/graphify Assets/Scripts --update` (a new component + a new dependency from it to `Player`, `NPC`).
