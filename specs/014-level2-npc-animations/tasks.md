# Tasks: spec 014, Level 2 NPC animations

**Status 2026-10-09**: Phases 0-2 built in one session, by one agent (Diego: one agent at a time, no rush). 2.B also
swapped page 2's crime-scene cops (decision 8) and added the second page 4 guard. 3.A (Diego's playthrough) and the
`/graphify --update` are pending.

`[P]` = safe to run in parallel with its siblings in the same phase. **Only one task at a time writes
`Level2_Newspaper.unity`** (the scene tasks are marked `[SCENE]` and never run in parallel with each other).
Verification available from here: `python tools/compile-check.py` (compilation only) + YAML checks (block counts, fileID
uniqueness, XOR ids for stripped blocks, every GUID resolves). Feel, timing and facing are Diego's playtest.

## Phase 0: the shared pieces (code only, no scene)

- [x] **0.A** `SpineCharacter`: locomotion from measured speed (agent or transform), walk/run thresholds, `CapGait`,
  track-1 overlays for idle/walk, facing via `Skeleton.ScaleX` (+ `_artFacesRight`, `_faceKamiWhenIdle`, external
  control), poses (intro -> main -> next), rest pose, idle break, actor id registry, `ResetPhysics()`.
  Guard clauses + `[SpineCharacter]` logs on every pose change. (D1)
- [x] **0.B** [P] `AnimationCue` + `DialogueEvent.animationCues` + `DialogueSO.cuesOnEnd`, run from
  `DialogueManager.WriteText` / dialogue end. Unknown actor or pose = one warning, never an exception. (D2)
- [x] **0.C** [P] `SpineBoneAim` on `UpdateLocal`: aim `brazo_front2` at a target, weight/blend/max-angle, only during a
  chosen animation, flip-aware. (D4)
- [x] **0.D** (after 0.A) `NPC`: facing goes through `SpineCharacter` when present (keeps `_sr` for the Abuela), `_sr`
  null-guard in `EnrojecerSprite`, side-offset follow target with sticky side + pace matching (Inspector fields,
  0 = old behaviour), `ResetPhysics` in `WarpTo`. `NPC_FollowPlayerState` uses the new target. (D3)

## Phase 1: characters (prefabs + page code) [P across 1.A / 1.B / 1.C / 1.D, none touches the scene]

- [x] **1.A** [P] Natalia: configure `SpineCharacter` on `Natalia.prefab` (poses `Angry`, `Happy`, `Thinking`, `Shock`; run
  threshold; idle break; facing), fix the `Spine` child's `loop: 0`. Code beats: `ArrestCutscene` (escort walk cap,
  end shock), `PoliceStationPage` (angry in the cell, cleared when freed), `PageRideFollowers` / `PaperPlaneRide`
  (`ResetPhysics`). Happy pose clears on page turn.
- [x] **1.B** [P] Ariel: `SpineCharacter` + `SpineBoneAim` on `Ariel.prefab` (poses `Point` -> `Smug`, `Angry`; default anim
  back to `Idle`).
- [x] **1.C** [P] Escort/street cops: `SpineCharacter` on `CopEscort.prefab` (variants inherit): poses `Arrest` -> `Rest`
  (Idle + Lantern), walk without lantern, actor `Police`. `ArrestCutscene.prefab`: street cops -> Old Male + Femme
  variants, escort cops -> the same two variants (per Q1). `ArrestCutscene.CUE_StartEscort` caps the cops' gait.
- [x] **1.D** [P] Page 4 guards: `PoliceOfficer.prefab` gets a `Policia` Spine child replacing `CopVisual_PLACEHOLDER`,
  `SpineCharacter` (Walk/Idle + Lantern, facing driven by the officer), knock-out pose/tint on the Spine child;
  variants `PoliceOfficer Male Variant` (`Policia 2`) and `PoliceOfficer Female Variant` (`Policia 3`).
- [x] **1.E** [P] `MuseumPage`: the walk-up works with NPC cops (agent walk to the spot at `distance / _walkUpSeconds`,
  fallback to the slide with the agent off), two cops instead of one, everyone faces Kami on arrival.

## Phase 2: data and scene (sequential)

- [x] **2.A** [P with 2.B] Dialogue cues in the 7 DialogueSOs: `Natalia_01`, `Natalia_AfterTicket` (+ `cuesOnEnd`),
  `Natalia_AllClues`, `Arrest_Shout`, `Arrest_Accusation`, `Grace_Meeting`, `Museum_Evidence`.
- [x] **2.B** [SCENE] `Level2_Newspaper.unity`: Natalia instance starts `Angry`; page 4 officer -> Male variant + new Female
  variant instance on its route (Q2), both in `PoliceStationPage._officers`; page 5 `Ariel_PLACEHOLDER_POSITION` ->
  `Ariel.prefab`, `CopWitness` -> Old Male + new Femme instance, `MuseumPage` refs updated; the intro timeline's
  `CopFollow` vcam still follows a cop. (+ page 2 cops if Q8 = yes, in `CrimeSceneGate.prefab`.)
- [x] **2.C** Verification: compile-check, YAML integrity on every touched file, a "beat table" walk-through listing each
  row of the spec and the file/line that drives it.

## Phase 3: close-out

- [ ] **3.A** Diego plays Level 2 end to end against the beat tables in `spec.md`.
- [x] **3.B** Docs (`quests-y-dialogos.md`, `enemigos-e-ia.md`, `cutscenes.md`, `CLAUDE.md`), spec status, `/graphify
  Assets/Scripts --update` (new classes).

## Parallelism summary

Biggest blocks: 0.B / 0.C next to 0.A; then all of Phase 1 (1.A-1.E) at once, since none of them writes the scene.
The scene (2.B) is a single writer at the end. Up to 3 agents is plenty; one session can also do it all in order.
