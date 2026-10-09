# Spec 014: Level 2 NPC animations (Natalia, Ariel, the cops)

**Status**: built 2026-10-09 on branch `014-level2-npc-animations` (from `level2newspaper-pages-blocking-2`, which
carries the Spine import `8828e458`). Compile-checked and YAML-verified only: **not played yet**. Diego's answers to
the open questions are at the bottom ("Decisions"); where they changed the draft, the text below says so.
**Art**: Valen's exports, commit `05e41c87`: `Assets/2D/Spine/Natalia/NataliaUnityExport/Natalia.json`,
`Assets/2D/Spine/Ariel/Ariel.json`, `Assets/2D/Spine/Policia/Policia.json` (all Spine 4.2.43, same runtime as Kami).

## Goal

Natalia, Ariel and the cops stop being tinted placeholder sprites and act out the story with their Spine animations,
in gameplay and in the three Level 2 cutscenes, with every beat tunable from the Inspector or a DialogueSO (no code
constants for "which animation, when").

Done = Diego plays Level 2 from page 1 to the catapult and every moment in the tables below shows the listed
animation, nobody moonwalks or faces the wrong way, and Natalia no longer stands on top of Kami.

## What exists today (triangulated 2026-10-09)

| Piece | State |
|---|---|
| `Natalia.prefab` | Spine child `Spine` (scale 0.35, y -3.92), `SkeletonAnimation` playing `Walk` with **loop off**. `_sr` is now empty, so `NPC.FaceDirection` / `UpdateSpriteFlip` do nothing (no flip at all) and `NPC.EnrojecerSprite` would throw. |
| `Ariel.prefab` | Bare Spine object (no script), default anim `Point` looping. Used by page 3's arrest (`ArrestCutscene.prefab` > `Street`). Page 5 still uses the sprite `Ariel_PLACEHOLDER_POSITION`. |
| `CopEscort.prefab` + 3 variants | `NPC` + agent + Spine child. Skins: base = `Policia 2` (male), `Old Male Variant` = `Policia 1`, `Female Variant` = `Policia 3`, `Femme Variant` = `Policia 4`. Default anim `Arrest` looping. |
| Page 3 cops | 2 escort cops (`ArrestCutscene.prefab` > `EscortCops`, base `CopEscort`) + 2 street cops (`StreetCop1/2_PLACEHOLDER`, tinted sprites, visible 0.2-3.0s on the `StreetCops (appears)` track, next to Ariel). |
| Page 4 cops | ONE `PoliceOfficer.prefab` instance (`PatrollingAgent`, vision cone), visual = `CopVisual_PLACEHOLDER` sprite. Knock-out = sprite rotated 90 degrees + tint. |
| Page 5 cop | ONE sprite `CopWitness_PLACEHOLDER_POSITION`. `MuseumPage.WalkTo` slides Ariel and the cop by `transform.position` (no agent). |
| Page 2 cops | `CrimeSceneGate.prefab` > `Cop1/2_PLACEHOLDER` (sprites). **Not in Diego's list**, see Q8. |
| Dialogue | `DialogueEvent.soundOnLine` already fires a sound when a line starts (`DialogueManager.WriteText`): the same hook can fire animation cues. |
| Spine Timeline extension | **Not installed** (no `com.esotericsoftware.spine.timeline`). |

What the exports contain:

| Character | Animations | Notes |
|---|---|---|
| Natalia | `Idle`, `Idle Enojada`, `IdleHappy`, `IdleThinking` (0.8s, intro), `IdleThinkingLoop`, `IdleToShock` (0.67s, intro), `Shock`, `Walk`, `Run` | 2 physics constraints (hair). `Run` keys hair physics. |
| Ariel | `Idle`, `Point` (1.33s), `Smug face` (0-length, eyes/eyebrows slots only: an overlay), `Angry Idle`, `Walk` | 10 physics constraints. Pointing arm = `brazo_front2` > `antebrazo_front2` > `mano_back`, plain FK (no IK on it). |
| Cops | `Idle`, `Walk`, `Arrest` (1.83s), `Lantern` (0-length, keys the front hand + its IK target: an overlay), `animation` (empty, ignore) | Skins `Policia 1-4`, no physics. |

## Design

### D1. One reusable component: `SpineCharacter`

`Assets/Scripts/NPCs/Animation/SpineCharacter.cs`, on the character's root, finds its `SkeletonAnimation` child. Everything
is Inspector data with Spine dropdowns (`[SpineAnimation]`), so Valen's renames are a dropdown pick, not code.

- **Locomotion**: measures its own horizontal speed (the `NavMeshAgent`'s velocity when it has a working one, otherwise
  the transform's movement, which covers Ariel and anything a cutscene slides). Below `_movingThreshold` = idle anim,
  above = walk, above `_runThreshold` = run (empty run = never runs). `CapGait(Walk)` forbids running (the arrest escort).
- **Overlays on track 1**: one for idle, one for walk (the cops' `Lantern`; Ariel's `Smug face` comes from a pose).
- **Facing**: `Skeleton.ScaleX = +-1` (same as Kami), from horizontal velocity with a deadzone, `_artFacesRight` per
  character. Optional `_faceKamiWhenIdle`. Code can take over facing (`PoliceOfficer` turns with its cone).
- **Poses**: a named list in the Inspector. A pose = optional intro (played once) + main animation (loop, or once and
  then go to another pose) + optional overlay, and whether movement interrupts it. Examples:
  Natalia `Shock` = `IdleToShock` -> `Shock` loop; Ariel `Point` = `Point` once -> `Smug` (= `Idle` + `Smug face`);
  cops `Arrest` = `Arrest` once -> `Rest` (= `Idle` + `Lantern`).
- **Rest pose ("mood")**: what the character does while standing still: Natalia angry on page 1 and in her cell, happy
  after the clues and after the case closes. Locomotion still wins while moving; when she stops she goes back to the mood.
- **Idle break**: after a random `[min, max]` seconds of plain idle (no mood, no dialogue, no cutscene), play a pose
  (Natalia: `Thinking`) for N seconds, then back. Moving cancels it.
- **Actor id** (`Natalia`, `Ariel`, `Police`, `Guard`): lets data address characters without scene references (D2).
- **Teleports**: `ResetPhysics()` = `SkeletonAnimation.ResetLastPositionAndRotation()`, called from `NPC.WarpTo` and the
  page/plane ride code, the same trap Kami hit (spec 012 1.B): Natalia's hair and Ariel's 10 constraints would fly.

Why one component instead of three scripts: the three characters need the same five things (locomotion, flip, poses,
overlay, cues); the differences are data. Adding a fourth NPC (Grace, the Abuela later) = configure, no code.

### D2. Beats tied to dialogue lines: `AnimationCue`

Most beats happen on a specific line, and the cutscenes already hold on those lines (`CutsceneDialogueMarker`). So:

- `DialogueEvent` gets `animationCues` (list of `{ actor, action: PlayPose | EndPose | SetRestPose | ClearRestPose, pose }`),
  run by `DialogueManager.WriteText` when the line starts, next to `soundOnLine`.
- `DialogueSO` gets `cuesOnEnd`, run when the dialogue closes ("back to idle when that dialogue ends").
- Retiming = editing the DialogueSO in the Inspector. No timeline edits for these beats.

Beats that are not on a line (the escort starts, the cell door, the walk-up) are called from the code that already owns
that beat (`ArrestCutscene`, `PoliceStationPage`, `MuseumPage`). No new timeline tracks, no Spine Timeline package.

### D3. Natalia follows to a spot beside Kami, at Kami's pace

- Target = Kami + `_followSideOffset` (default 3.5) along the camera's right axis, **on the side Natalia is already on**
  (sticky, with hysteresis), so she never walks through Kami and parks beside her, not on top. Stopping distance drops to
  ~1 (it was 3 from Kami's centre).
- Speed = Kami's current horizontal speed (+ a catch-up factor when she is more than `_catchUpDistance` behind), clamped.
  Today her agent always moves at 15: with Kami walking she would always run, with Kami sprinting (28) she falls behind.
  Matching the pace is what makes "walk or run according to her speed" read right.
- Lives in `NPC` behind Inspector fields (offset 0 / pace matching off = old behaviour). **The Abuela gets it too**
  (Diego): `Abuela_Follower` uses offset 7 (she stands past Natalia's 3.5 when both are on the same side) and pace
  matching. Natalia's cap is 18 u/s: "a bit slower than Kami at full non-sprint speed" (20) is fine (Diego).

### D4. Ariel points AT Kami: `SpineBoneAim`

Yes, possible from Unity with no re-export. A small component hooks `SkeletonAnimation.UpdateLocal` (after the
animation pose is applied, before world transforms) and adds a rotation to `brazo_front2` so the shoulder-to-hand line
points at Kami. Inspector: bone (dropdown), target (empty = Kami), only while `Point` plays on track 0, weight, blend
in/out seconds, max deviation from the authored pose (default 40 degrees, so a Kami far above or behind him never twists the arm
into something broken). Works flipped. Forearm and hand keep Valen's pose.

## The beats

### Natalia

| Where / when | Animation | Driven by |
|---|---|---|
| Default, standing still | `Idle` | locomotion |
| Following, moving | `Walk` / `Run` by speed (run above ~12 u/s: Kami walks under 10, skips 10-20, sprints 28) | locomotion |
| Plain idle for 8-14s (random), no dialogue/cutscene | `IdleThinking` -> `IdleThinkingLoop` for 4s, then `Idle` | idle break |
| Page 1, until she talks to Kami | `Idle Enojada` (rest pose) | scene instance's initial rest pose; cleared by `Natalia_01` line 1 ("Oh! Sorry...") |
| Page 1, after the café fold (she thinks about what to do with Kami) | `IdleThinking` -> `IdleThinkingLoop` from `Natalia_AfterTicket` line 2 ("Someone dragged you here..."), `Idle` when that dialogue closes | line cue + `cuesOnEnd` |
| Page 2, all clues found | `IdleHappy` rest pose from `Natalia_AllClues` line 0, until the next page turn | line cue; the pose clears on page turn |
| Page 3, the arrest | `IdleToShock` -> `Shock` on `Arrest_Shout` line 0, held until the escort starts | line cue |
| Page 3, escort to the car | `Walk` (run forbidden) | `ArrestCutscene.CUE_StartEscort` |
| Page 4, in her cell | `Idle Enojada` rest pose until the padlock is cut | `ArrestCutscene.WrapUp` / `PoliceStationPage` lock-up and free |
| Page 5, case closed | `IdleHappy` rest pose from `Museum_Evidence` line 7 (the police apology), for good | line cue |
| Riding a page turn or the paper plane | `Walk` (Diego), the Abuela too (her Animator's walk bool) | `NPC.SetRiding` from `PageRideFollowers` / `PaperPlaneRide` |
| Any time she moves | flips to face where she walks; when stopped, faces Kami | facing |

### Ariel

| Where / when | Animation | Driven by |
|---|---|---|
| Default | `Idle` | locomotion |
| Page 3, `Arrest_Accusation` line 0 ("Officers! There they are...") | `Point` (arm aimed at Kami), then `Idle` + `Smug face` | line cue |
| Page 5, walk-up from the car | `Walk` | locomotion (the slide is measured) |
| Page 5, `Grace_Meeting` line 0 ("There they are, officers!") | `Point` -> `Idle` + `Smug face` | line cue |
| Page 5, guilt revealed: `Museum_Evidence` line 4 ("They're yours, Ariel.") | `Angry Idle` (smug face cleared), for good | line cue |

### Cops

| Where / when | Animation | Driven by |
|---|---|---|
Corrected by Diego (2026-10-09): the lantern is up whenever they hold the girls, and away when they escort them.

| Where / when | Animation | Driven by |
|---|---|---|
| Default standing (pages 2, 3, 5) | `Idle` + `Lantern` | locomotion + idle overlay (`CopEscort` default) |
| Page 3, `Arrest_Shout` line 0 | the street cops `Arrest`, then `Idle` + `Lantern` aimed at Kami; the escort cops pop up behind the girls in `Idle` + `Lantern` | line cue (actor `Police`) |
| Page 3, the escort to the car | `Walk`, no lantern | locomotion (walk overlay empty) |
| Page 5, walk-up from the car | `Walk` + `Lantern` | the two page 5 instances override the walk overlay |
| Page 5, `Grace_Meeting` line 1 | `Arrest`, then `Idle` + `Lantern` | line cue |
| Page 5, from the police apology (`Museum_Evidence` line 7) | plain `Idle` (and `Walk`), no lantern | line cue: `SetIdleOverlay` / `SetWalkOverlay` empty |
| Page 4 patrol | `Walk` + `Lantern`; `Idle` + `Lantern` when he pauses at a corner | locomotion + overlays (actor `Guard`) |
| Page 4, he catches Kami | `Arrest`, then `Idle` + `Lantern` until the page restarts | `PoliceOfficer.Catch` |
| Page 4 knocked out | Spine child lying down + grey tint (same placeholder trick as the sprite) | `PoliceOfficer.KnockOut` |

**Which cops where** (Diego): page 2 Male + Female (the crime scene, decision 8), page 3 Old Male + Femme (both the
street pair and the escort pair), page 4 Male + Female (**two** patrolling the same route, half a lap apart: the male
from corner D toward A, the female from corner B toward C, `_startWaypointIndex` 2), page 5 Old Male + Femme.
`PoliceOfficer.prefab` itself is the male (skin `Policia 2`, like `CopEscort`); `PoliceOfficer Female Variant` is new.

## Changes per file (estimate)

| File | Change | Size |
|---|---|---|
| `NPCs/Animation/SpineCharacter.cs` | new | ~300 lines |
| `NPCs/Animation/SpineBoneAim.cs` | new | ~90 |
| `NPCs/Animation/AnimationCue.cs` | new (cue struct + registry lookup) | ~60 |
| `Dialogos/DialogueSO.cs`, `UI/DialogueManager.cs` | `animationCues`, `cuesOnEnd`, run them | ~25 |
| `NPCs/NPC.cs` | facing through `SpineCharacter` when present, `_sr` null-guard, side-offset target + pace matching, `ResetPhysics` on warp | ~70 |
| `NPCs/NPC_FollowPlayerState.cs` | follow the side target | ~5 |
| `AI/PoliceOfficer.cs` | Spine child instead of the sprite (facing, knock-out pose, tint) | ~40 |
| `Level2/ArrestCutscene.cs` | escort: cap gait, end Natalia's shock; cell: rest pose | ~15 |
| `Level2/PoliceStationPage.cs` | Natalia angry in the cell / cleared when freed; second officer is just another array entry | ~10 |
| `Level2/MuseumPage.cs` | walk-up: cops are NPCs now (agent walk, or agent off during the slide), 2 cops | ~30 |
| `Level2/PageRideFollowers.cs`, `PaperPlaneRide.cs` | `ResetPhysics` after their teleports | ~4 |
| Prefabs | `Natalia`, `Ariel`, `CopEscort` (+ variants inherit) get `SpineCharacter` configured; Ariel gets `SpineBoneAim`; `PoliceOfficer` gets a Spine child + 2 variants (Male `Policia 2`, Female `Policia 3`) | YAML |
| `ArrestCutscene.prefab` | street cops -> Old Male + Femme `CopEscort` variants; escort cops -> the same two variants | YAML |
| `Level2_Newspaper.unity` | page 4: officer -> Male variant + a Female variant instance with its route; page 5: Ariel + 2 cops (Old Male, Femme) replacing the sprites; Natalia instance initial rest pose `Angry` | YAML |
| 7 `DialogueSO` assets | the cues in the tables above | YAML |
| Docs | `quests-y-dialogos.md` (Natalia), `enemigos-e-ia.md` (PoliceOfficer visual), `cutscenes.md` (where beats live now), `CLAUDE.md` index | |

Nothing changes for Valen: the exports are used as they are. A future re-export only needs the dropdowns re-picked if an
animation is renamed.

## As built (2026-10-09)

Where to tune (all Inspector, nothing in code):
- **Which animation, which pose**: the `SpineCharacter` on `Natalia.prefab`, `Ariel.prefab`, `CopEscort.prefab` (its three
  variants inherit it) and `PoliceOfficer.prefab`. Poses: Natalia `Angry` / `Happy` / `Thinking` / `Shock`, Ariel
  `Point` (-> `Smug`) / `Smug` / `Angry`, cops `Arrest`. Actor ids: `Natalia`, `Ariel`, `Police` (pages 2, 3, 5),
  `Guard` (page 4).
- **When**: the `Animation Cues` list on each dialogue line, and `Cues On End` on the dialogue (`Natalia_01`,
  `Natalia_AfterTicket`, `Natalia_AllClues`, `Arrest_Shout`, `Arrest_Accusation`, `Grace_Meeting`, `Museum_Evidence`).
  The rest is code-owned: escort start (`ArrestCutscene.CUE_StartEscort`), cell mood (`PoliceStationPage`,
  `_nataliaInCellRestPose`), the catch (`PoliceOfficer._catchPose`), rides (`NPC.SetRiding`).
- **Following**: `NPC` "Following Kami" header (side offset, pace factor, arrive / catch-up / max speed). Natalia 3.5 /
  0.9 / 8 / 1.3 / 18, Abuela offset 7. Natalia's `Run Speed` (walk -> run) is 12 on her `SpineCharacter`.
- **Ariel's arm**: `SpineBoneAim` on `Ariel.prefab` (bone `brazo_front2`, tip `mano_back`, max 40 degrees, blend 0.25 s,
  aims 2 units above Kami's pivot).
- **Second guard**: `PoliceOfficer Female` in Page 4, `_startWaypointIndex` 2, `_startFacingDegrees` 270, placed at corner
  B (7.95, 4.09, 19.13 in Page 4 space). Both are in `PoliceStationPage._officers`, so a capture resets both.

Things to know:
- **Art direction is assumed**: `_artFacesRight` is on for all three (Kami's art faces right at `ScaleX` 1). If a
  character moonwalks, untick it on that prefab's `SpineCharacter`; nothing else changes.
- Speed is measured, not told: an NPC slid by a cutscene, carried by a ride or walked by its agent animates the same.
  A one-frame jump above 150 u/s is treated as a teleport, not a sprint.
- The page 5 cops' `NavMeshAgent` is off in the scene (their walk-up is a slide, as before); `MuseumPage` also switches
  it off before moving them. `_copWitness` became `_copWitnesses` (two).
- Natalia starts angry (`_initialRestPose`, set in `Awake`); a test started on a later page clears it in `NPC_Natalia`.
- `Shock` holds even if she is still settling into her spot (the escort ends it); every other Natalia pose ends when
  she starts walking.
- The page 2 / 3 / 5 street placeholders were replaced by real prefab instances; the scene overrides that moved them
  (positions, the page 2 cops' 61-degree turn) were retargeted onto the new instances, not lost.
- Not verified at runtime: the facing direction of each export, the arm-aim angles, foot sliding (walk/run
  `Animation Speed` is 0 = off), and whether the page 3 street cops' agents snap onto the NavMesh where they stand.

## Out of scope

The Abuela (Animator-based, untouched), Grace (still a sprite: no Spine export yet), Kami, any timeline retiming, new
animations. Page 2's crime-scene cops unless Q8 says otherwise.

## Decisions (Diego, 2026-10-09)

All defaults below stand, except:
- **3**: Natalia a bit slower than Kami's full (non-sprint) speed is fine; **the Abuela gets the side spot and the pace
  matching too**.
- **4**: thinking is both the idle break and the page 1 beat after the café fold.
- **6**: the lantern rules in the cops' table above (lantern while holding the girls, away for the escort; page 5 walk-up
  with lantern; plain idle once the case is solved; page 4 guards `Walk` + `Lantern`, `Arrest` when they catch Kami).
- **8**: the crime-scene cops are the normal Male + Female.
- **10**: Natalia and the Abuela play `Walk` while riding a page turn or the paper plane.
- Page 4 has a **second patrolling cop** on the same route.
- Ariel's aim-at-Kami (D4) is in: `SpineBoneAim`.

The original questions, for the record:

1. **Page 3 has four cops today** (2 street cops next to Ariel for the shout, then 2 escort cops who pop up behind the
   girls at 3.0s). Default: **keep the four objects, both pairs become Old Male + Femme**, so on screen it reads as the
   same two cops moving behind the girls (zero choreography change). Alternative: merge into two cops who actually walk
   from the street to behind the girls (more choreography, a NavMesh walk inside a 0.8s gap).
2. **Page 4 second patrol cop's route**. Default: **the same "Poli Path" rectangle, starting at the opposite corner**
   (half a lap apart, so the room is never unwatched). Alternative: Diego blocks a second route and I wire it.
3. **Natalia's follow spot**: default **3.5 units to the side she is already on, stopping 1 unit from that spot, her pace
   matched to Kami's**. Should the Abuela get the same side spot (default: **no**, she keeps trailing 6 behind)?
4. **Natalia's idle break**: default **every 8-14s of plain idle, thinking for 4s**, never during a dialogue or cutscene,
   and not while she is in a mood (angry/happy).
5. **Page 2 happy**: default **happy as her resting pose from "These three clues..." until the page turns**.
6. **Lantern pop**: pages 3/5 cops walk without the lantern and stand with it, so the lantern appears/disappears when
   they start/stop walking. Default: **as you described**; say if you'd rather they keep it while walking after the arrest.
7. **Ariel's angry line**: default **`Museum_Evidence` line 4, "They're yours, Ariel."** (alternatives: line 3 "whose
   hat...", or line 5, Grace's patron reveal). Natalia happy: default **line 7, the police apology**.
8. **Page 2 crime-scene cops** (`CrimeSceneGate`, still sprites, not in your list): default **swap them too, Old Male +
   Femme, `Idle` + `Lantern`** (the same pair that later arrests the girls).
9. **Page 4's taunting cop** (`Jail_CopTaunt` in the intro): default **no special animation** (keeps patrolling, Idle/Walk +
   Lantern). Could play `Arrest` as a taunt gesture if you like.
10. **Riding a page turn / the paper plane**: default **Natalia plays `Idle`** while hanging next to Kami.
