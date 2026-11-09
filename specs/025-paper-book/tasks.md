# 025 - The paper book: tasks

Spec: `spec.md` (decisions D1-D41, "How we know it works"). `[P]` = can run in parallel with the other `[P]` tasks
of its phase (different files). Model is a suggestion. Every task ends with the checks in "How we know it works"
that apply to it; "Diego:" lists his steps. Issue numbers are in `spec.md` > GitHub.

## Phase 0: tooling and measuring (all parallel)

- **0.A** [P] Importer and JSON schema (D1): `Assets/Editor/Generated/` `ScriptedImporter`s for `.handgen` and
  `.bookgen` (Mesh, AnimationClips, hierarchy, the rigid-skinned flag), `tools/unity_artifacts.py` (the artifact
  reader factored out of `measure-book-pages.py`), `tools/verify-gen.py` skeleton (D40), the z-buffer previewer (D8).
  A tiny test file (a cube + a 1 s clip) proves the round trip: generate, Unity imports, the artifact diff passes.
  Diego: focus Unity once.
- **0.B** [P] (sonnet) Measure both books into `tools/data/book-intro.json`: world-scaled vertices, the spine column,
  the cover face (Libro) and the rim (Diario), the 14 spine-crossing triangles, materials, shadow settings.
- **0.C** [P] Fit the procedural curl (D23) to the old clip in rig space and to each level's measured book surface
  (gutter dip, spine x, outer edge), into `tools/data/paper-curl.json`, with a side-view contact sheet old vs new.
- **0.D** [P] Page audit into `specs/025-paper-book/page-audit.md`: per page folder, every renderer with its kind
  (mesh, sprite, skinned, Spine), bounds, half, proposed class (Card / Volume / Rider / Flat), pieces resting on
  pieces, Batching Static (scene and prefab), lights, particles, PUB houses, the camera's facing per page.
- **0.E** [P] (sonnet) `compile-check.py` also compiles `Assembly-CSharp-Editor.rsp` (D39); `tools/run-paper-tests.py`
  compiles `Assets/Scripts/Book/Paper/Core/` plus `tools/paper-tests/*.cs` with Unity's bundled csc and runs them on
  the bundled runtime (D38), with one placeholder test.

## Phase 1: the Narrator's hand (needs 0.A)

- **1.A** Pieces and hierarchy (`tools/make-narrator-hand.py` -> `NarratorHand.handgen`): D2, D3, D4, D6, joint
  limits, sockets, the rigid-skinned flag. verify-gen: closed meshes, winding, normals.
- **1.B** UVs, atlas, base texture, material (D5, D3): layout PNG, checker preview, turntable, luminance check.
  Diego approves (V1).
- **1.C** [P with 1.B] Test poses: open, fist, pinch (`Grip`), carry (`Carry`), point; contact sheet; limits checked.

## Phase 2: the intro books (needs 0.A, 0.B)

- **2.A** Split books (D9): bake world scale, unit-scale hinge, Libro at x 0, Diario's spine triangles clipped and
  its cover face and spine rim generated, cover face submesh, shadow settings. verify-gen: open pose vs the fbx (V3).
- **2.B** [P] (sonnet) Placeholder covers, `tools/make-book-covers.py` (D12).
- **2.C** The cover opening: hinge curve with a landing bounce, contact sheet.

## Phase 3: paper core and the vertical slice (needs 0.C, 0.D, 0.E)

- **3.A** Python reference of the core math (curl, leaf frames, height table, fold solver for Cards and Volumes),
  golden JSON, side-view previews of real Level 2 page 1 and 2 pieces. Diego approves (V12).
- **3.B** The pure C# core (`Book/Paper/Core/`) and its tests against the goldens (D38). Green before 3.C starts.
- **3.C** Vertical slice: `PaperSheet` (D23), `PaperTurn` with proxies (D25, D30), Cards only (Volumes shown rigid),
  Kami riding the new edge, `PagePiece` on pages 1-2 by hand, `_paperPages` on in Level 2, forward 1 -> 2 only, the
  runtime self-checks and `PaperTurnDebug` (D41). EditMode tests for the adapter. Diego: run tests, play 1 -> 2.
  This proves the riskiest part first.
- **3.D** Backward and forced turns (D35), `_paperPages` off path untouched (V16), followers (V17).

## Phase 4: the rest of Part A (needs 3)

- **4.A** Flat capture (D32): `PaperCapture` layer, capture camera, RenderTextures, overlay quads, unlit material,
  Gutter pieces (V15).
- **4.B** Volumes with the end squash and Riders (D27, D28), goldens extended.
- **4.C** [P] **Kami > Pages > Tag Page Pieces** (D29) with a dry-run report.
- **4.D** [P] (sonnet) Batching Static off for page props (D37): `Vallas`, MB01 buildings, the 36 scene overrides.

## Phase 5: roll-out (needs 4)

- **5.A** Level 2: tag every page, tune, Profiler (V14, V18). Diego plays all five pages both ways.
- **5.B** Level 1: the same (V14, V18).
- **5.C** Retire the old pop-up path (PUB, `Objetos_Paginas` shrink/enlarge, `HojaMaster` in paper levels) once
  Diego agrees. Nothing deleted without his OK.

## Phase 6: the level intro (needs 1, 2)

- **6.A** Intro clips (`tools/make-book-intro-clips.py`, D10, D16): hand and cover from the same numbers,
  `Grab` / `Carry` / `Place` separate; verify-gen grip check; contact sheet.
- **6.B** Level 2 intro: **Kami > Book Intro > Build** (D1, D13), `Cutscenes/BookIntro`, cameras (D17), the swap
  (D11, `BookPageSpread` second slot), the fallback page 1 pop (D34), the debug overlay (D41), EditMode tests for the
  Timeline. Diego: click Build, run tests, play.
- **6.C** [P] `Player.StartCarry` / `StopCarry` (D15) and the respawn save.
- **6.D** [P] Skip (D18): `InteractHeld`, prompt and UITexts, `ApplyEndState()`, idempotent handlers.
- **6.E** Hand-offs (D19, D20): page 1 only, `MagicalGirlChange` deferred, level-start code checked.
- **6.F** [P] (sonnet) Sound rows (D21).
- **6.G** Level 1 intro: the same as 6.B for Level 1 (storyboard first). Diego: click Build, play.
- **6.H** Page 1 unfolds on the cover (D34) through `PaperTurn.Begin`, replacing the fallback in Level 2, then
  Level 1 (needs 3.D, and 5.B for Level 1).

## Phase 7: Level 3 hand clips (needs 1; milestone M5)

- **7.A** The clips Diego picks + `NarratorHand.controller`, contact sheets.

## Phase 8: close-out (needs 5, 6)

- **8.A** Docs (V11), `/graphify Assets/Scripts --update`, spec "As built", issues closed with evidence.

## Art (Valentino, any time)

- Front covers for Level 1 and Level 2, replacing 2.B's placeholders (same size and file names).

## Parallelism summary

- Phase 0: all five tasks at once.
- After Phase 0: Phases 1, 2 and 3 in three sessions at once (hand generator, book generator, paper core).
- Phase 6's 6.A-6.G can start once 1 and 2 are done, alongside Phases 4-5; 6.H waits for Part A.
- Phase 7 can start as soon as Phase 1 is done.
- Only one session writes a given scene at a time.
