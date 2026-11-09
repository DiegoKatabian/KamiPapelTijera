# 025 - The paper book: paper page turns and the level intro

Asked by Diego on 2026-11-06. Designed and reviewed 2026-11-06; built 2026-11-07, not imported or played yet (section "As built"). Branch `025-paper-book`.
Tasks: `tasks.md`. GitHub: epic #366, milestone M11 (table at the end).

This spec merges two requests from the same day:

- **Part A, paper page turns**: the objects on a page stick to the paper of the book. When a page turns they ride
  the turning sheet, fold flat as the book closes on them, and unfold as the next page opens.
- **Part B, the level intro**: every level that starts on page 1 opens with the Narrator's 3D hand opening the
  closed book (title on the cover), page 1 unfolding, and the hand placing Kami on the page. Skippable.

They share one mechanism: opening the cover is a page turn with no outgoing page (D34).

**Reviewed on 2026-11-06** by two skeptical reviewers working against the code and the imported assets (section
"Review findings"). The first draft had wrong facts about the sheet rig and the books; this version replaces them.

## The goal (what Diego sees)

> **Page turns.** I walk to the edge and turn the page. The sheet lifts, and the houses, trees and road on its right
> half go with it, lying on the paper, the road bending with the curl. The things on the left half lean down as the
> sheet comes over them and end flat under it. Behind the lifting sheet the next page's things stand up, and the
> ones printed on the sheet's back stand up as it lands. Nothing shrinks or pops in.

> **Level intro.** I start a level. The camera shows the closed book on the Narrator's desk, front cover up, with
> the level's title on it. The Narrator's hand comes in, lifts the cover and opens the book. Page 1 unfolds as the
> cover swings. The hand comes back holding Kami, lowers her onto the page and lets go. She lands and I play.

- Page turns: both levels, forward and backward, and the forced turn of the page 3 arrest.
- Intro: every level start that begins on page 1: Level 1, Level 2, and later Level 3.
- **Claude builds all of it**: the 3D hand (model, UVs, texture, rig, animations), the intro books, the paper page
  system and the Unity side. Diego (2026-11-06): the hand is 3D, not Spine; Claude models it, UVs it and animates it
  for these intros and for Level 3 (Pochi is no longer on the hand). **No Blender** (D1).
- **The intro is skippable**: hold Interact for about 1 s.
- **Level 1 keeps the main menu's storyboard dialogue**: storyboard, then Level 1 loads and the book opens.
- **Diego wants to test it and have problems found and fixed fast**, so verification and debug tools are part of
  the work, not an extra (section "How we know it works").

## Review findings (2026-11-06, the facts the design now rests on)

**The turning sheet**
- `HojaMaster` holds two nested rigs (`Hoja` / `Hoja2`, each with its own Animator and its own 31-joint chain,
  `PN000pageJoint00` at the spine to `30` at the edge). Every vertex is weighted to one bone: the sheet is 30 rigid
  strips. Clip `Take 001`, 3.33 s, then `HojaIdle`.
- **It stretches in world space.** The rig root is scaled non-uniformly (`Hoja.prefab`, `m_LocalScale` (11263.6,
  5488.6, 11263.6): height squashed 2.05x), and Level 2's sheets add (0.356, 0.497, 0.621) on top. Level 2's
  sheet is 49.3 units long flat and 36.6 at mid-turn; Level 1's 137.5 and 81.5. Joint rotations are sheared up to
  about 19 degrees off the visible paper. (In rig space the chain is even and inextensible; the scale breaks it.)
- **At rest it does not lie on the book.** Level 2's book top is at y 1.04 (0.81 at the gutter); the sheet at turn
  start runs from y 0.03 at the spine to 2.98, and its spine edge does not match the book's spine (-0.43 vs -0.37
  at start, +0.22 at end). It overhangs the outer edge by 3.1.
- Timing in both levels: `delayTime` 0.5, `popupDelayTime` 2.5, so the sheet turns from 0.5 to 3.83 s; page N gets
  `isShrink` at 0.

**Page contents**
- Level 2 pages: 52 / 5 / 35 / 53 / 8 prefab instances; pages 3 and 4 have about 100 ProBuilder meshes with
  MeshColliders. Moving things: traffic (page 1), idling cars (page 3), cops and the Abuela (page 4), Ariel, Grace,
  escorts and a patrol car (page 5). Natalia reparents to Kami's parent while following.
- Level 1 folders: 70 / 56 / 63 / 142 instances. `Vallas.prefab` (127 uses) and the MB01 buildings are fully static
  in their prefabs (Batching Static included); 36 scene overrides set "Everything". Static batching is on; no
  lightmaps.
- **Most props are cards facing the camera** (Level 1's trees, bushes, stones: sprites in the XY plane; the spine
  runs along world Z). A card hinged on a line parallel to the spine can never lie flat. 3D volumes can't fold
  thinner than their thinnest side.
- Lights live in the street-lamp prefabs; particles in TrashCan, HumoChimenea, EspumaRioRocas, Fuego. The PUB houses
  (CasaTio, Herreria) are blendshape SkinnedMeshRenderers whose default state is `Closed`.
- `Objetos_Paginas` writes only the folder root's scale (path ""); its default state is `Enlarge`, so any folder
  that switches on is squashed for about 1 s.
- 118 of 120 fbx files are not Read/Write enabled.
- Switching page N+1 on earlier than today (the first draft's D25) breaks NavMesh ownership, traffic, quest
  `Start()`s and more. Real page objects must not be moved or activated early.

**The books**
- Level 2's `Diario` is scaled (3802.5, 4500, 4500) and turned 180 degrees in yaw; Level 1's `Libro` (5604, 4549,
  5372). A hinge rotating under that scale would shear the cover 18-23% at 90 degrees.
- `Diario` has **no outer cover face**: its cover submesh (110 triangles) is only the rim. 14 triangles cross the
  spine. `Libro` has a vertex column at x 0 and its cover face (12 triangles, UV v 0.44-0.84), so it splits cleanly.
- `Libro` is 140 x 55 units, `Diario` about 95 x 71. No lightmaps, no Batching Static on either; `Libro` casts no
  shadows; `Diario` carries a MeshCollider that is Level 2's floor.

**Lighting**
- Level 2's Film profile: Bloom threshold 0.67, Post Exposure +0.55, no tonemapping, linear colour. A 0.9-albedo
  glove would bloom. The desk lamp `LuzVelador` is a hard-shadow spot above the book.

**Kami and level start**
- `StartRidingPage` keeps only a Z offset, adds `RideRootOffset`, plays Jump -> RidePage and turns sprint particles
  on (`Player.cs:493`). `IsRidingPage` is read by `PageRideFollowers` (boards every follower), `PlayerModel.Tick`,
  `ReceiveImpact` and `PoliceOfficer`. `PlayerPageSpawnManager.FinishRide` (on `OnPageFinishTurning`) teleports her
  to `targetPos`, which is (0,0,0) when no turn set it.
- `PlayerPageSpawnManager.Start` saves her scene position as the respawn point. `MagicalGirlChange.Start` sets
  `inCutscene` and lifts her; `CutsceneDirector.Release` clears the same flag.
- `CameraManager.SetCamera` only switches vcams on and off; a Timeline's Cinemachine track overrides it, so the
  level-start cut needs no change.

**Tooling**
- Native YAML assets (`.anim`, `.playable`, Python-written `.asset`) leave nothing readable in Library/Artifacts;
  imported assets (fbx, PNG, anything with an importer) do. No timeline in the repo has an Animation track yet.
- No `dotnet` on PATH, but Unity's bundled runtime and `DotNetSdkRoslyn/csc.dll` compile and run a C# console test
  from here (checked 2026-11-06). `compile-check.py` skips `Editor/` folders; `Assembly-CSharp-Editor.rsp` already
  references NUnit and the Test Runner, so EditMode tests can live in a plain `Editor/` folder.
- matplotlib's 3D view sorts whole polygons, so overlapping meshes preview wrong; numba is installed for a z-buffer.

## Decisions (taken here, Diego can overrule any of them)

### Shared tooling and verification

| # | Decision | Why |
|---|---|---|
| D1 | **Python computes, a C# importer builds** (Diego, 2026-11-06). Python generators in `tools/` compute every number (vertices, UVs, joint hierarchy, keyframes) into JSON files under `Assets/Generated/` with their own extensions (`.handgen`, `.bookgen`). A C# `ScriptedImporter` turns each into Unity objects (Mesh with `RecalculateBounds`/tangents, AnimationClips via `SetEditorCurve` + `EnsureQuaternionContinuity`, the GameObject hierarchy) when Unity gets focus, with no click. Textures are PNGs with a normal importer; materials are copies of an existing URP Lit `.mat`; Signals are YAML (proven). Prefabs, Timelines and the Animator Controller are made by one menu item, **Kami > Book Intro > Build (missing only)**, which Diego clicks once per level; it never overwrites, so after that Diego owns the timing. | Hand-written `.anim` YAML imports broken without an error (binding CRCs, editor vs runtime curves). Imported assets leave artifacts Claude can read back and diff. |
| D8 | **Previews before Unity**: renders from a numpy + numba z-buffer rasteriser into `specs/025-paper-book/previews/`: the hand (turntable next to an 8-unit Kami, UV layout, checker), each clip's contact sheet (a frame every 0.25 s), and side views of real page pieces through a turn. | Claude cannot see the Editor; matplotlib 3D draws overlaps wrong. |
| D38 | **A pure-math core tested from here.** The curl, leaf frames, height table and fold solver live in `Assets/Scripts/Book/Paper/Core/` with no `UnityEngine` (own small vector types), so they compile into the game and also into a console test run from here by `tools/run-paper-tests.py` (Unity's bundled csc and runtime). A Python reference of the same math writes golden JSON; the C# tests must match it within 1e-4. Every session runs the tests before handing anything to Diego. | The math is where silent errors hide; this checks it without Unity. |
| D39 | **`compile-check.py` also builds `Assembly-CSharp-Editor`**, and EditMode tests in `Assets/Editor/Tests/` check what only Unity can (every clip path resolves on the hand prefab, the `Grip` stays on the cover edge when clips are sampled, every Timeline track is bound, every Signal has a receiver, the core's goldens through the Unity adapter). Each test logs `[GenCheck] PASS/FAIL ...`, which Claude reads from Editor.log. Diego runs them from the Test Runner once per phase. | Editor code that doesn't compile blocks Play; the tests turn Diego's one click into a full report. |
| D40 | **`tools/verify-gen.py`** runs after every generation, before Unity: closed meshes with outward winding, normals agree with faces, UV islands rasterised at 1024 with zero overlap pixels and even texel density, joint limits on every key, `Grip` to cover edge under 0.05 units on every frame, the intro book's open pose against the fbx (V3), predicted glove luminance under the bloom threshold. After Diego focuses Unity it also greps Editor.log for the generated paths and diffs the importer artifacts against the JSON (vertex counts and positions, clip lengths, curve paths). | Claude can be sure an asset is right before Diego looks at it. |
| D41 | **Debug tools ship with the features**: `PaperTurnDebug` (a turn-time slider, freeze, slow motion, a key that freezes the current frame, gizmos for the leaf curve, hinges and fold angles, global toggles per fold class, the per-piece `_excluded`), `BookIntro`'s overlay (step, time, cover angle, hold) with `_startAtSeconds` and Editor-only pause / x4 / restart keys. Logs are `[PaperTurn]` and `[BookIntro]` and name the object by its hierarchy path. | When something looks wrong Diego can freeze it, see which piece, and switch it off to bisect. |

### Part A: paper page turns

| # | Decision | Why |
|---|---|---|
| D23 | **A procedural sheet in paper mode** (`Book/PaperSheet`): one pure-math curl function, `Frame(u, t)` (position and orientation of the paper at distance `u` from the spine at time `t`), inextensible by construction, whose rest profile is the measured book surface (gutter dip included) on both halves. Its motion is fitted in Python to the old clip's look (0.C) and previewed. The mesh is a 64-segment strip deformed on the CPU, dressed through `PageTurnSheet` / `BookPageSpread` with the book halves' UV rectangles. It exposes an edge Transform for `StartRidingPage` and raises `OnPageFinishTurning` itself. It updates in `LateUpdate` with `[DefaultExecutionOrder(-50)]` (before Player at 0 and `PageRideFollowers` at 100). `HojaMaster` stays for `_paperPages` off. | The old rig stretches and doesn't lie on the book (review); a curve we own lies exactly on the page at rest and never stretches. |
| D24 | **Leaves**: anything a page lies on: the static left half, the static right half (the measured book surface), the sheet's front and back (`PaperSheet.Frame`) and, in Part B, the intro book's cover (a rigid hinge). Because the sheet's rest profile is the book surface, a piece is at its authored pose on the first and last frame of a turn by construction. | No jump, no offset. |
| D25 | **Visual proxies; real objects never move.** When a turn starts (during the 0.5 s `delayTime`, pooled) `PaperTurn` builds render-only copies of pages N and N+1: MeshRenderer (shares the mesh, no read needed), SpriteRenderer (copied), SkinnedMeshRenderer (baked, blendshape weights included; PUB houses forced to their open weights), Spine (a snapshot of its current mesh with its property block; an uninitialised Spine object is skipped and logged). Real renderers are hidden with `forceRenderingOff` (set on page N+1's inactive objects too), their Lights off, particles and trails hidden, not copied. `TogglePages`, `OnNewPageOpen` and the NavMesh swap keep today's timing; only page N's `isShrink` at 0, the folder's `Enlarge` squash and the PUB close/open are skipped in paper mode. At the end the proxies are destroyed and the real renderers shown. A forward turn has four groups (page N left: static, folds; page N right: rides the sheet front, folds; page N+1 left: rides the sheet back, unfolds; page N+1 right: static, unfolds); backward swaps the sides. A piece's half = which side of the spine the centre of its renderers' bounds is on. | Nothing in gameplay can break, and a turn leaves every real object exactly as it was. |
| D26 | **Opening angle** of each spread comes straight from `PaperSheet`'s math: spread N 180 -> 0, spread N+1 0 -> 180. | One number drives every fold. |
| D27 | **Fold classes** (`PagePiece._class`): **Card** (its thinnest side faces the camera: sprites and flat props) hinges on its base line and falls away from the camera to end face up; **Volume** (3D props) hinges on a line parallel to the spine at its outer base edge and falls outward, then in the last 25 degrees of closing squashes along the page's normal only, never sideways (Diego, 2026-11-06), while the sheet already covers it; **Rider** (a piece resting on another: a sign on a roof) stays rigid on its host; **Flat** (roads, river, ground decals) goes into the page capture (D32). | Cards can't flatten around a spine-parallel hinge and volumes can't fold thinner than their depth (review). |
| D28 | **Fold solver**: each frame a 128-bin height table of the other leaf of the spread; per piece the 8 corners of its oriented bounds (the union of its renderers' local bounds, never the pivot) in leaf space; a closed-form angle for cards, an 8-step bisection for volumes, so the highest corner stays under the other leaf. A fold never decreases while closing. A piece that can't fit logs once (`[PaperTurn] cannot fit <path>`) and folds fully. A per-piece curve can override it. | Cheap, deterministic, testable from here (D38). |
| D29 | **`PagePiece` is authored in the Editor** (class, hinge override, curve override, `_excluded`, host for Riders). **Kami > Pages > Tag Page Pieces** fills a page folder from the renderers' kind and bounds and writes a dry-run report first; Diego fixes the odd ones. A renderer without a `PagePiece` above it is treated as a Volume and logged once. | Author in the Editor; nothing is guessed silently. |
| D30 | **`PaperTurn`** (next to `PageScrollerManager`) owns proxies and folds, with a `Begin(leaves, pages)` / `End()` API. Page turns and the intro both use it. The intro never raises the `Evento` page events. | One system, two callers. |
| D32 | **Flat pieces become a texture.** At turn start an orthographic capture camera (layer `PaperCapture`, a new layer) renders each half top-down (page picture + its Flat pieces) into a RenderTexture, shown on the sheet's faces and on overlay quads over the static halves, with an unlit material so the page isn't lit twice. Gutter pieces (over the spine) crease for free because they are part of the capture. The first draft's runtime skinning (D32 old) is dropped: non-readable meshes, combined static batches, sprites and Spine can't go through it. | Bends exactly with the paper, no mesh reads, no subdivision. |
| D33 | **Per-level switch** `PageScrollerManager._paperPages`: off = today's flow and `HojaMaster`, on = this system. Both live until Diego has played both levels; then the old pop-up path is retired (5.C, with Diego's OK). | Compare, and nothing breaks while a level is half tagged. |
| D35 | **Every turn goes through it**: player turns both ways and the forced turn (`TurnToNextPage(ridePage: false)`). | One path. |
| D36 | **Performance**: proxies built in the 0.5 s before the sheet moves, pooled per page; Profiler on Diego's machine on Level 1 page 4 (142 instances). | Avoid a hitch at turn start. |
| D37 | **Batching Static off for page props** (Diego, 2026-11-06): `Vallas.prefab`, the MB01 buildings and the 36 scene overrides lose only the Batching Static flag (Navigation Static stays). | A statically batched renderer reports the combined mesh, so it can't be copied. The SRP Batcher keeps the cost small; checked with the Profiler. |

### Part B: the Narrator's hand

| # | Decision | Why |
|---|---|---|
| D2 | **The hand is a rigid-piece puppet**: one mesh per piece (forearm/sleeve, cuff, palm with the thumb base as one smooth mesh, 3 segments per finger, 2 more for the thumb), each a child of its joint Transform. Spherical joint ends centred on each pivot, tapered segments with flattened cross-sections, so bends don't open gaps. The importer can also build the same pieces as one skinned mesh with rigid weights (a flag from day one), same joints, same clips. | Animating = rotating Transforms; the skinned option is the fallback if it reads mechanical. |
| D3 | **The look**: an adult hand in a white cotton glove, five fingers, three stitched lines on the back, a short cuff, a dark navy wool sleeve that runs out of frame, about 5k triangles. URP Lit, albedo about 0.75 (a cream matched to the page texture's luminance), smoothness 0.15 or less, specular highlights off. | Matches the GDD's white glove without blooming (threshold 0.67, exposure +0.55). |
| D4 | **Scale**: one hand for every level (the same Narrator), about 45 units from wrist to middle fingertip, tuned on the first preview next to both books (Libro 140 wide, Diario 95). Kami (8 units) is about a finger. | The same person's hand; the books differ. |
| D5 | **UVs**: unwrapped by shape (cylindrical for fingers and sleeve, the seam on the side facing the next finger; planar islands for the palm), packed into one 1024 atlas, even texel density, no overlaps, 8 px padding. A layout PNG and a checker preview prove it (D40). Claude makes the base texture; Valentino can paint over the layout PNG. | Diego asked for clean UVs. |
| D6 | **Joints and sockets**: `Root`, `Forearm`, `Wrist`, `Palm`, 3 per finger, 3 for the thumb, joint limits checked on every key (D40). Sockets `Grip` (between thumb and index tips) and `Carry` (where Kami hangs). | Other code attaches to sockets, not finger joints. |
| D7 | **Editable later**: importer outputs are read-only, so Diego's edits can't be overwritten; to reshape a piece by hand he extracts it (or ProBuilderizes a copy) and it leaves the generator's control. | Generated first, hand-editable after. |

### Part B: the intro books and the intro

| # | Decision | Why |
|---|---|---|
| D9 | **Intro books**: for each level a copy of the level's book with its **world scale baked into the vertices** (normals recomputed) under a unit-scale transform, split at the spine into a fixed half (back cover + right page) and a moving half (front cover + left page) under a `Hinge`. Libro splits at its x 0 column; Diario's 14 spine-crossing triangles are clipped, and **its outer cover face and spine-side rim are generated** (it has none). The cover's outer face is its own submesh for the title. Closed = hinge at 180 degrees. Shadow settings copied from the level's book. | A hinge under the books' non-uniform scale would shear the cover. |
| D10 | **Hand and cover keyed from the same numbers**: the cover's angle curve first, then the hand's `Grip` placed on the cover edge's arc on every key. Checked by D40 (distance under 0.05) and the EditMode test (D39). | The glove holds the cover on every frame without runtime syncing. |
| D11 | **The intro book stands in for the level's book only during the intro.** It is renderer-only (no colliders). The level's book's renderers are hidden with `HiddenRenderers`, never the GameObject switched off (Diario's MeshCollider is Level 2's floor). Its open pose is the same world vertices, so the swap does not show. `BookPageSpread` gets a second renderer slot so the intro book shows page 1's newspaper in Level 2. | Every existing book system stays as it is. |
| D12 | **Front covers**: placeholders from `tools/make-book-covers.py` ("Kami Papel y Tijera", "Kami Tinta y Condena") fitted to the cover face; Valentino's covers replace the PNGs later (#365). | The closed book must show the title. |
| D13 | **Unity side**: `Prefabs/BookIntro/BookIntro_Level1|2.prefab` (intro book, hand, vcams, Timeline in `Assets/Timelines/BookIntro/`, played by `CutsceneDirector`) built by the D1 menu, plus `Cutscenes/BookIntro` for what a Timeline can't do. **The prefab's default pose is the open book with the hand off screen**; the closed pose comes from `director.Evaluate()` at t 0, never from code writing transforms (Timeline restores captured transforms when it stops). Wrap mode None. Animators sit on parents and clips animate child paths only; culling Always Animate. | Avoids Timeline's restore-on-stop, root motion and track offsets. |
| D34 | **Page 1 unfolds with the cover** through `PaperTurn.Begin` with the cover as the turning leaf and no outgoing page; Volumes inside the closed book use the same squash (D27). **Fallback, shipped first**: page 1 hidden while the book is closed and the folder's existing `Enlarge` played when the cover passes 90 degrees; D34 replaces it once Part A's slice works (Level 1 also needs its pages tagged, 5.B). The intro never raises page events (D30). | The intro does not wait on Part A, but ends on the same system. |
| D15 | **Kami is carried by a new `Player.StartCarry(Transform socket)` / `StopCarry(Vector3 releasePoint)`**, not the page ride: an `IsCarried` flag, a shared `IsHeld` read by `PlayerModel`, the ride `LateUpdate` and impacts; follows all three axes; no Jump animation, no sprint particles; plays `RidePage`; never sets `IsRidingPage` (followers would board, `FinishRide` would teleport her to (0,0,0)). From frame 0 of the intro she is attached and her renderers hidden (`KamiOutline` draws through walls), `TriggerScript.ExitAll()` and `ResetSkeletonPhysicsMemory()` on attach and release. The release point (her scene spawn) is saved as her respawn point explicitly. | The page ride has side effects everywhere. |
| D16 | **Sequence** (about 12 s): closed book, title readable (2 s) -> the hand takes the cover edge and swings it open while page 1 unfolds; the cover lands with a small bounce and a dust puff (3 s) -> the hand leaves (1 s) -> the hand comes back holding Kami and lowers her to her spawn (3 s) -> releases her, she lands, the hand rises out (1.5 s) -> blend to `Normal`, Kami free. | One action at a time. |
| D17 | **Cameras**: `Establishing`, `Opening`, `Placing` vcams in the prefab, then the brain blends back to `Normal`. `CameraManager` needs no change. | A Cinemachine track overrides its vcam switching. |
| D18 | **Skip**: after 1 s, "Hold {INPUT:accion} to skip" (UITexts `book_intro_skip`, ES/EN/PT), read from `InputHub.InteractHeld`; a hold that began before the intro (Level 1's storyboard press) is ignored. Holding 1 s calls `ApplyEndState()`, which calls every beat's handler; every handler is idempotent. | Skipping jumps time, so signals in between never fire. |
| D19 | **When it runs**: only on a page 1 start (`startingPage == 1`), once per scene load. | Same rule as `MagicalGirlChange`. |
| D20 | **Level 2 order**: intro, then `MagicalGirlChange`, which defers its lift, its `inCutscene` and its whole run until the intro ends (the hand's drop is the landing). The classic outfit stays equipped at `Start`. | `inCutscene` must have one owner at a time. |
| D21 | **Sound**: AudioBank rows `BookCoverOpen`, `BookCoverLand`, `NarratorHandMove` (placeholders). | Audio is Diego's. |
| D22 | **Locks**: `inCutscene` for the whole intro, owned by `CutsceneDirector`. | Same lock every cutscene uses. |

Retired: D14 (replaced by D34), D31 (proxies make it unnecessary), the old D32 (runtime skinning). Numbers are kept so
references still point at the same decisions.

## How we know it works

**Before Diego opens Unity (Claude, every session)**
1. `python tools/compile-check.py` for the game and the Editor assembly (D39).
2. `python tools/run-paper-tests.py`: the paper core against the Python goldens (D38).
3. `python tools/verify-gen.py`: meshes, UVs, joint limits, grip contact, book match, glove luminance (D40).
4. Previews in `specs/025-paper-book/previews/` for anything visual (D8).

**When Diego focuses Unity**
5. Claude reads Editor.log and the importer artifacts and diffs them against the JSON (D40).

**Diego, once per phase**
6. Test Runner > EditMode > Run All; Claude reads the `[GenCheck]` lines (D39).
7. Play the phase's checklist below. If something looks wrong: freeze it with `PaperTurnDebug` or `BookIntro`'s
   keys, switch fold classes or pieces off to find the culprit, and paste the `[PaperTurn]` / `[BookIntro]` lines.

**Runtime self-checks**: after every turn `PaperTurn` checks that no real transform changed and that every hidden
renderer is visible again, and logs any piece it skipped or could not fit, by hierarchy path.

**Diego-only steps** (everything else is Claude's): approve previews; focus Unity after a generation; click
**Kami > Book Intro > Build** once per level; run the Test Runner once per phase; play; Profiler checks (V18).

## Level 3 animation set (proposal, Diego confirms before Phase 7)

| Clip | Beat |
|---|---|
| `Idle_Hover` | The hand waiting above the desk, slight breathing motion (loop). |
| `Point_No` | Index raised, wagging: "No deberías ver estas fotos." |
| `Reach_Unfold_Fail` | Reaches for the paper dragon, pinches a corner, tugs, slips off. |
| `Plead` | Open palm, slow, asking Kami to stop. |
| `Offer_Palm` | Palm up and still, low enough for Kami to stand on it (loop). |
| `Grab` / `Carry` / `Place` | Shared with the intro, cut as separate clips. |
| `Wave` | The ending: the Narrator lets Kami go between books. |
| `Tap_Desk` | Fingers drumming on the desk (loop), impatience. |

Made by the same generator and importer, held by `NarratorHand.controller` for use outside Timelines.

## Phases (one per session; detail and parallel markers in `tasks.md`)

| Phase | Work | Needs |
|---|---|---|
| 0 | Tooling and measuring: the importer and JSON schema, the artifact reader and `verify-gen.py`; both books (scale, faces, spine); the curl fitted to the old clip and the book surfaces; the page audit; compile-check for Editor code and the paper test runner. | - |
| 1 | The Narrator's hand: pieces and hierarchy, UVs and texture, test poses. Previews to Diego. | 0.A |
| 2 | Intro books: split with baked scale, Diario's cover face, placeholder covers, the cover opening. | 0.A, 0.B |
| 3 | Paper core and the vertical slice: Python reference and previews, the pure C# core with tests, then Level 2 page 1 -> 2 forward with `PaperSheet`, proxies, cards only, Kami riding and the debug tools; then backward and forced turns. | 0.C, 0.D, 0.E |
| 4 | Flat capture, Volumes with squash and Riders, the tagging tool, Batching Static removal. | 3 |
| 5 | Roll-out: every Level 2 page, then Level 1; retire the old pop-up path with Diego's OK. | 4 |
| 6 | The intro: clips, Level 2 intro (fallback page 1 pop), the carry, skip, hand-offs, sounds, Level 1 intro, then page 1 unfolding on the cover (D34). | 1, 2; D34 needs 3 and 5.B |
| 7 | Level 3 clip set + Animator Controller (M5). | 1 |
| 8 | Close-out. | 5, 6 |

Phases 1, 2 and 3 can run at the same time in separate sessions: they touch different files.

## Checklist (exit criteria)

Paper page turns
- [ ] V12 Side-view previews of real pieces through a turn approved by Diego: nothing pokes through the sheet.
- [ ] V13 First and last frame of a turn: every proxy matches its real object (no jump), both directions.
- [ ] V14 After any number of turns back and forth, no real object changed (self-check silent) and gameplay on every page behaves as before.
- [ ] V15 Flat pieces bend with the curl and the swap between capture and real objects is invisible; a piece over the spine creases.
- [ ] V16 `_paperPages` off gives today's behaviour unchanged.
- [ ] V17 Forced turn (arrest) and backward turns work; Kami and the followers ride as before.
- [ ] V18 Profiler: no visible hitch at turn start or end on Level 1 page 4.
- [ ] V19 `run-paper-tests.py` green; EditMode tests green.

The hand and the intro
- [ ] V1 Turntable, UV layout and checker previews of the hand approved by Diego.
- [ ] V2 The importer outputs and the built prefab load with no missing references (Editor.log clean); artifacts match the JSON.
- [ ] V3 The intro book's open pose matches the level's book (same world vertices); the swap is invisible.
- [ ] V4 Nothing shows outside the book while it is closed; page 1 appears with the cover.
- [ ] V5 The glove holds the cover edge through the whole swing (verify-gen and the EditMode test).
- [ ] V6 Kami is carried, released over her spawn, lands; her respawn point is unchanged; no follower boards anything.
- [ ] V7 Skip works at any moment and leaves the same end state as the full intro.
- [ ] V8 Only on page 1 starts; test starts on pages 2-5 and respawns are unaffected.
- [ ] V9 Level 2: intro, then the transformation, with no second drop.
- [ ] V20 The glove does not bloom under Level 2's Film profile or the desk lamp.

Both
- [ ] V10 `compile-check.py` clean for game and Editor code (baseline warnings only); YAML consistent.
- [ ] V11 Docs updated (`paginas-y-hoja.md`, `cutscenes.md`, `spine-kami.md`, `nivel2-y-ui.md`, `audio-y-particulas.md`, `CLAUDE.md`).

What still needs Diego's eyes: how the folds and the curl read, framing, timing, lighting on the glove, how the
skip feels. Everything else is checked before he plays.

## Out of scope

- Valentino's cover art (#365) and any hand-painted glove texture.
- Real sounds (Diego).
- Level 3's scene and where its clips play.
- The Level 1 -> Level 2 transition beat from the script (GDD §2.7 point 4).
- Kami standing on the turning sheet instead of hanging from its edge.
- Real pop-up mechanisms authored per piece (layers that slide on their own).
- Copying particles and lights onto the proxies (they are hidden during a turn).

## Open questions (none block Phase 0)

- Does the hand need a second look per level? Default: one hand.
- Back covers: only if a camera ever shows the closed book from below. Default: none.
- Spine NPCs on page N+1 are inactive when the turn starts, so they have no mesh to copy: they appear when the folder
  switches on, as today. If that reads as a pop, a pose snapshot per NPC can be stored the first time each page is
  visited.
- UNVERIFIED by the review: whether agents re-attach by themselves after `AddNavMeshData` (irrelevant with proxies),
  whether `BakeMesh` works on inactive objects (checked in 3.C), the camera's exact facing per page (0.D).

## As built (2026-11-07, branch `025-paper-book`, uncommitted)

Every phase was built in one session at Diego's request ("go straight to the end"). **Unity was closed the whole
time**: nothing was imported, no scene was opened, nothing was played. What was checked from here is listed; everything
else waits for Diego's steps below.

### What exists

| Part | Built | Checked from here |
|---|---|---|
| 0.E tooling | `compile-check.py` also compiles Editor code; `run-paper-tests.py` | both run clean |
| 0.B / 0.C measuring | `measure-paper-book.py` (both books: spine, page z, surface profiles with the gutter dip, uv maps, fit error 0.005), `fit-paper-curl.py` (lead 0.25, ease 0, shape 1.8; 14.5 deg rms against the old clip's shape, which floated above the book and drooped below horizontal) | `previews/curl-fit.png` |
| 3.A / 3.B paper core | `Book/Paper/Core/` (C#, no UnityEngine) + `tools/paper_core.py` (Python twin) + goldens | 7/7 tests: C# = Python on 9 pieces x 10 times, the sheet never stretches and rests on both books, no static piece pokes through the sheet; `previews/paper-turn-level2.png` |
| 3.C-4.C runtime | `PaperTurn`, `PaperSheet` (+ prefab), `PaperPieces`, `PagePiece`, `PaperBookData` (+ 2 assets), overlay shader + material, `PageScrollerManager._paperPages`, `BookPageSpread.DressPaperSheet`, **Kami > Pages > Tag Page Pieces** | compiles; scenes wired with block counts and fileID uniqueness checked |
| 4.D | Batching Static removed from 29 flags in page prefabs (Vallas, MB01 x3, Reja, Paja, ...) and 10 in Level 1's scene | re-run reports 0 |
| 0.A importer | `KamiGenImporter` (`.kgen` JSON -> meshes, hierarchy, clips; rigid-skinned option), `unity_assets.py`, `zbuffer.py`, `verify-gen.py` | compiles; verify-gen 6 files, 0 failures |
| 1 the hand | `make-narrator-hand.py`: 18 pieces, 5280 triangles, one 1024 atlas (20 islands), two URP Lit materials (cream albedo for Level 2's bloom, no specular highlights), sockets Grip/Carry, poses open/pinch/fist/point | closed shells, outward winding, no UV overlap, joint limits; `previews/hand-*.png` |
| 2 intro books | `make-intro-books.py` (world scale baked, split at the spine, generated front cover + spine faces, Diario's missing cover included), `make-book-covers.py` (placeholder titles) | open pose = level book (worst vertex 0.0000, top surface identical); `previews/intro-books.png` |
| 6.A / 7.A clips | `make-book-intro-clips.py`: per level the hand and cover clips (grip on the cover edge within 0.03 units while holding, V5), Level 3's 10 clips | `previews/book-intro-Level1|2.png` |
| 6.B-6.G intro | `BookIntro`, `Player.StartCarry/StopCarry` (+ `IsHeld`), `MagicalGirlChange` waits, skip (UITexts `book_intro_skip` ES/EN/PT), 3 AudioBank rows, **Kami > Book Intro > Build** (prefabs, Timelines, scene placement, `NarratorHand.controller`) | compiles; EditMode tests `BookIntroTests` written |
| 6.H | page 1 unfolds on the cover (`PaperTurn.BeginIntro`, `_unfoldWithPaper`, fallback pop-up) | compiles |

### Changes from the design (and why)

- **Proxies are drawn with `Graphics.DrawMesh`, not GameObjects**: a copied transform can't hold the skew a source's
  parents may give it, and no objects means nothing to pool or clean up.
- **A turn starts at the sheet's start** (0.5 s after the press), not at the press: nothing needs drawing before the
  sheet exists, and the copies show the page as it is when it starts to move.
- **Flat pieces use four captures** (each page over each half): two ride the sheet, two lie still on the halves, so a
  flat piece across the spine is split by the two rectangles. Unity can only render active objects, so the captures
  render the proxies (which exist for the inactive arriving page too) on layer 31 through their own cameras.
- **Facing pages count each other's pieces** (`pageThicknessShare`, 0.5): the first preview had a page's pieces passing
  through the facing page's pieces.
- **The intro book's hand releases the cover at 120 degrees** and the cover falls open: following it all the way would
  turn the arm upside down. **The sleeve is 3x the hand long** so its end never shows.
- **Page lights are left alone** (they switch with their folder, as today); particles, trails and lines are hidden.
- **No separate `PaperTurnDebug` component**: the debug fields are a header on `PaperTurn` (one thing to wire).
- **BookIntro picks Kami up one frame after Start**: its Start runs before Player's (order -40), where her skeleton and
  her safe position are set up. Page 1 is hidden at Start and handed to the paper turn that next frame, when its Spine
  characters have a mesh.
- **Auto-classification at runtime**, `PagePiece` only as an override: tagging every prop by hand isn't needed to start.

### Diego's steps, in order

1. **Open Unity** on this branch and let it import (the `.kgen` files, textures, materials, prefab). Then I can run
   `python tools/verify-gen.py --artifacts` to diff what Unity imported against the generators.
2. **Kami > Book Intro > Build (missing only)** once: builds both intro prefabs and Timelines, places them in both
   level scenes (saving them), and builds `NarratorHand.controller`. Check the Console line `[BookIntroBuilder] ...`.
3. **Test Runner > EditMode > Run All**: `PaperBookTests` and `BookIntroTests` (each logs `[GenCheck]` lines).
4. **Play Level 2 from page 1**: the intro, then the transformation; turn pages 1 -> 2 -> 3 and back. Watch the Console
   for `[PaperTurn]` (how many pieces of each class, what could only be hidden, what could not fit) and `[BookIntro]`.
5. **Play Level 1 from the main menu**: storyboard, intro, page turns. The densest page is 4: Profiler on (V18).
6. Anything that looks wrong: pause the turn (F7) or the intro (F9), switch piece classes off on `PaperTurn` to find the
   piece, give it a `PagePiece` with another class or Excluded, and paste me the logs.

### First play (Diego, 2026-11-07) and the fixes after it

Diego imported, ran **Book Intro > Build** and the EditMode tests, played Level 2 pages 1-3 and Level 1 pages 1-4. Level 1:
all fine. Level 2: most pieces fold well; four problems, fixed from here (compile-check, `run-paper-tests.py` 7/7 and
`verify-gen.py --artifacts` 0 failures; **not played again**):
- **Test `ModelRunsOnBothBooks` failed on Level 1**: the test's house stood 0.06 above Level 1's page (1.44 high), inside the
  0.15 clearance, so it can never count as "fitted"; it still folds fully and squashes, which is the intended end state.
  The test now checks the fold only.
- **Page 3's building (and, most likely, the museum piece on page 2) stayed standing while the sheet passed through it**:
  the solver only tested the 8 corners of a piece's box, and Natalia's building (55 wide, 42 tall, across the spine) has its
  corners on both sides of the curl. Simulated: it stood unfolded from 30% of the turn on. Now the box's outline is tested
  along every edge (`SampleOutline`, every 1.5 units, at most 16 segments per edge), and only on the piece's own half
  (from 0.5 outward): across the spine the sheet lands on it at the end of every turn, which left it folded and would have
  made it jump when the real object came back. Simulated after the fix on both books and all four leaves: leaving pieces
  start standing, arriving pieces end standing. The part of a spine-crossing piece on the other half is not tested, so the
  sheet can still cut through that part near the spine.
- **Page 1's cars stood still on the paper**: the traffic spawner makes cars during the turn, and they had no proxy.
  `PaperTurn.HideLateRenderers` now hides, every frame of a turn, any renderer that appears under either page after the
  start (until the turn ends). A car spawned mid-turn pops in when the turn ends.
- **Kami looked like she was flying in the hand**: carried, she now plays `falling` (the same loop as her real fall), not
  `RidePage`.
- Also asked by Diego: page 2's end-of-clues beat (`NataliaPageEndBeat`) fades to black while Natalia moves to the page's
  end (0.4 s out, 0.2 s black, 0.5 s in, during the camera's blend to her; the scene's `ScreenFader` is found by itself).
- `verify-gen.py --artifacts`: the `.kgen` artifacts are stored without type trees, so their content can't be read from
  outside Unity; the tool says so and points at `BookIntroTests`, which check the same from inside.

Left for Diego (his call, 2026-11-07): the intro's camera framing and timing and the hand's exact position, tuned on the
Timelines by hand.

### Known risks (not verifiable from here)

- How the curl and the folds read in motion; the new sheet stands as tall as the page is wide (the old one was squashed
  to about half): if it leaves the CloseUp/BookCenter frame, `heightScale` on the book data flattens it.
- Classification thresholds on real pages (cards vs volumes vs flat), and pieces split or grouped oddly: the tagging
  tool's Report shows the grouping without playing.
- The capture overlays' look (unlit, captured from above) against the lit page around them.
- Sprite proxies with custom sprite shaders, and Spine NPCs on the page arriving (they pop in at the end).
- The intro's camera framing (three vcams built from the book's bounds) and the hand's scale in each level.
- `BakeMesh(mesh, false)` space for skinned props (PUB houses): if a house shows at the wrong size during a turn, that's it.

## GitHub

Epic #366, milestone **M11 — The paper book: paper page turns and the level intro (spec 025)**; 7.A is in
**M5 — Nivel 3**. Created 2026-11-06, reorganised after the review the same day.

| Phase | Issues |
|---|---|
| 0 | #335 0.A, #336 0.B, #337 0.C, #338 0.D, #349 0.E |
| 1 | #339 1.A, #340 1.B, #341 1.C |
| 2 | #342 2.A, #343 2.B, #344 2.C |
| 3 | #345 3.A, #346 3.B, #347 3.C, #348 3.D |
| 4 | #350 4.A, #351 4.B, #352 4.C, #353 4.D |
| 5 | #354 5.A, #355 5.B, #356 5.C |
| 6 | #357 6.A, #358 6.B, #359 6.C, #360 6.D, #361 6.E, #362 6.F, #367 6.G, #368 6.H |
| 7 | #363 7.A (M5) |
| 8 | #364 8.A |
| Art | #365 `[ART-VALEN]` front covers |
