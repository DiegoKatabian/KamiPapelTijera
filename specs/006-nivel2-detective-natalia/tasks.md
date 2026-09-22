# Tasks: Level 2 — The Stolen Pelusa (Natalia)

Phased breakdown for parallel-agent execution. `[P]` = can run in parallel with the
other `[P]` tasks in its own phase (they touch different files/systems, no
dependency between them). No `[P]` = sequential within the phase, or depends on
something from another phase that hasn't landed yet.

**Design constraint (applies to every task below, see `spec.md`)**: default to
prefab + ScriptableObject authoring over scene-embedded logic. Once a system is
built, adding an instance to a scene should be "drag a prefab, edit a few Inspector
fields" — not new scripting.

Starting-state citations are against real code (investigation 2026-09-22, see
`spec.md` → Context for the summary and the 3 agent reports behind it), revised
against Diego's 2026-09-22 review pass (Origami reuse, Abuela's entrance, the
catapult, the capture/defeat text).

---

## Phase 0 — Shared infrastructure (blocks pages 1-5, highly parallelizable)

None of this is page-specific; build it once, reuse it several times. Each track is
a separate agent — no file overlap between them.

- **0.A** `[P]` **Café/letter/ticket Origami routes**. *Corrected scope*: the
  underlying system (Origami/sello, `Origami.cs`/`MultipleRectCheck.cs`/
  `PedestalCanvasDisplay.cs`) is DONE and reused as-is — no new interaction system.
  What's actually needed: (1) author new `OrigamiRoute` assets (e.g.
  `OrigamiRoute_Cafe` for the café wrapper/ticket, `OrigamiRoute_Letter` for the trap
  letter) using the existing route/pedestal authoring pattern; (2) confirm whether
  the current route system can represent "unfold an already-folded route to reveal
  text" — if not, add a small, targeted extension (e.g. a reveal-text field/callback
  on the route asset) rather than building a parallel system. No dependencies.

- **0.B** `[P]` **Ambient traffic** (cars/pedestrians appear, move, despawn). MISSING
  (the art already exists: `Paper Car Prefab aniamted.prefab`, placed 4× in
  `Level2_Newspaper.unity`, no script). New spawner prefab + obstacle-set
  ScriptableObject (prefab variants, speed range, spawn interval), reusing
  `Barquito/BarquitoBehaviour.cs` steering as a base if it fits. No dependencies.

- **0.C** `[P]` **New cuttable prefabs**: poster, police tape, ribbon, catapult rope.
  The base system (`ICortable`) is DONE; only the 4 new components are needed,
  following the `PuertaCortable`/`ObjetoCortable` pattern. No dependencies — can be
  one agent for all 4 (they're nearly identical) or split if preferred.

- **0.D** `[P]` **Evidence data**: new `ResourceType` entries (caught belonging,
  broken watch, Ariel's scarf/cap, the Pelusa) + `InventoryItem` assets, same mold as
  `abuela`. Data/ScriptableObject work, no new logic. No dependencies.

- **0.E** `[P]` **`Player.LoseTijera()`**: method symmetrical to `GetTijera()`
  (`Player.cs:662-669`) that sets `hasTijera = false` and refreshes the
  view/equipment. Trivial, no dependencies — but a prerequisite for Phase 4 (cell
  confiscation).

- **0.F** `[P]` **`DeathCause.Caught` + defeat overlay text**. *Corrected — resolved,
  not an open design question anymore*: add a new `DeathCause` value, a new
  localized overlay text key (same pattern as `DefeatDrowning`/`DefeatRocoso`/
  `DefeatGeneric`, e.g. `DefeatCaught` → "You were caught") in the 3 localization
  tables, and wire a fixed cell-entry respawn point for that cause (same shape as the
  Drowning override, but a fixed point instead of a last-safe-position snapshot).
  Small, self-contained — a prerequisite for 4.B, but no longer a design decision to
  make first.

---

## Phase 1 — Page 1 (depends on 0.A, 0.C, 0.D)

- **1.A** `[P]` **NataliaDialogueTrigger + NPC**: on top of the generic
  `NPC`/`NPC_IdleState`/`NPC_FollowPlayerState` (NOT the `NPC_Abuela` mold). Opening
  dialogue (Ariel + the robbery), sets `isFollowing = true` and
  `QuestManager.AddQuest(FindClues)`. 100% new — zero prior Natalia code in the
  project.

- **1.B** `[P]` **Cuttable posters wiring** in the page-1 scene (uses 0.C).

- **1.C** `[P]` **Ambient traffic wiring** in page 1 (uses 0.B) — spawn points +
  routes over the 4 `Paper Car` instances already placed in the scene.

- **1.D** **Café-wrapper fold beat**: depends on 0.A (`OrigamiRoute_Cafe`) — plays
  the fold via the existing Origami/sello flow.

- **1.E** **Quest_FindClues** (`QuestSO`, condition type decided during technical
  planning — likely Event, fired once 2.A+2.C both complete). Depends on 1.A (who
  grants it) and on the completion condition being defined together with Phase 2.

---

## Phase 2 — Page 2 (depends on 0.C tape, 0.D, Phase 1 in progress)

- **2.A** `[P]` **Trash can as a flap**: decide whether to reuse `TriggerSolapa`
  as-is (full `PullSolapas` gesture) or a variant without
  `Player.PlayPullSolapa` — see Context in `spec.md`. Delivers the caught belonging
  (uses 0.D).

- **2.B** `[P]` **Cuttable police tape** (uses 0.C) opening the path to the museum.

- **2.C** `[P]` **Broken watch pickup** in the museum (uses 0.D) — a simple pickup,
  same pattern as existing `PickupCortable`-style collectibles.

- **2.D** **Close Quest_FindClues + start Quest_GoBackToNataliasHouse**: depends on
  2.A and 2.C (both clues) plus 1.E (the quest already existing).

---

## Phase 3 — Page 3 (depends on 0.A letter, 0.C ribbon, Phase 2 complete)

- **3.A** `[P]` **Gift box + cuttable ribbon** (uses 0.C) → reveals the Pelusa
  (uses 0.D for the evidence object).

- **3.B** **Letter fold/unfold + read beat** (uses 0.A's `OrigamiRoute_Letter`) with
  the "the heist was a success..." text. Depends on 3.A (the opened box exposes it).

- **3.C** **Arrest cutscene**: shout + camera pulls back (reuse `CameraManager`, a
  new `CameraMode` or scripted sequence) + Ariel/police dialogue + handcuffs + camera
  following the patrol car into the page change. The most "cinematic" piece of the
  level — sequential, depends on 3.B (triggers once the letter is read) and gates the
  start of page 4 (positions Kami already inside the cell).

- **3.D** **Close Quest_GoBackToNataliasHouse + start
  Quest_EscapeAndReturnThePainting**: depends on 3.C.

---

## Phase 4 — Page 4 (the largest; depends on 0.E, 0.F, 0.C posters/stacks, Phase 3
complete). Internally very parallelizable — 4 nearly independent systems that only
share the scene, not code:

- **4.A** `[P]` **Abuela's entrance**. *Corrected scope*: not real
  destruction — a new triggered behavior (fall-on-Kami animation) that disables the
  nearby wall GameObjects via the existing `GameObjectActivator`/`QuestEffector`
  reveal pattern (already the project's standard way of doing scene reveals), plus
  particle/sound feedback. New: the drop trigger/behavior itself; reused: the reveal
  mechanism.

- **4.B** `[P]` **PoliceOfficer** (`: PatrollingAgent` + vision cone + LoS + Alert
  state + `DeathCause.Caught` from 0.F on detection). This is the actual
  implementation of `specs/002-enemigo-sigilo/spec.md` — the spec is already closed,
  only the code is missing. The largest chunk of the phase; if it needs further
  splitting, divide into: detection (cone + raycast) vs. patrol/waypoints (trivial,
  already given by `PatrollingAgent`) vs. the capture response (uses 0.F).

- **4.C** `[P]` **Confiscated scissors/paper zone**: a pickup that calls
  `Player.GetTijera()` (uses 0.E for the cell's initial "no scissors" state).

- **4.D** `[P]` **Cuttable paper stacks / "wanted" posters** (uses 0.C, same
  `PickupCortable`-style pattern for granting `ResourceType.papel`).

- **4.E** **Paper-airplane escape**: adapts the existing
  `OrigamiPaperPlaneHat`/`Player.GetPaperPlaneHat()` buff + a new "reach the window →
  change page" trigger. Depends on 4.B/4.C/4.D being playable (needs paper AND
  recovered scissors to test end to end), though the code itself can start earlier
  with test data.

---

## Phase 5 — Page 5 (depends on 0.A ticket, 0.D evidence, Phase 4 complete)

- **5.A** `[P]` **Resolution dialogue with the owner**: gated on having both evidence
  items in the inventory (reuse `QuestSO`'s `Condition.Resource` pattern — "has N of
  this ResourceType" — rather than inventing a comparison system). Depends on 0.D.

- **5.B** `[P]` **Café ticket unfold** (uses 0.A) as proof of innocence.

- **5.C** **Close Quest_EscapeAndReturnThePainting** +
  `Natalia.StopFollowingPlayer()` + `Abuela.StopFollowingPlayer()` (the mechanism
  already exists — just wiring). Depends on 5.A.

- **5.D** **Catapult ending**. *Corrected scope*: hard-animated
  (Animator-driven), not physics-based. Second dialogue with the owner → confirm
  with E → Kami and Abuela board (may need a short scripted reposition of Abuela to
  the catapult, since she already stopped following in 5.C — confirm approach during
  technical planning) → Kami cuts the rope (uses 0.C) → the launch plays as an
  animation, the girls quickly leave frame, the camera holds on the empty catapult →
  fade to black → `LevelManager.GoToScene` to the closing cutscene.
  **NEEDS CLARIFICATION** (see `spec.md`): does the destination cutscene scene
  already exist?

---

## Parallelism summary

If split across agents/sessions, the maximum-parallelism order is:

1. **Start immediately, all together**: 0.A, 0.B, 0.C, 0.D, 0.E, 0.F (6 independent
   tracks).
2. **As soon as Phase 0 closes**: 1.A, 1.B, 1.C in parallel (1.D and 1.E follow
   those).
3. Page 2 has internal parallelism (2.A/2.B/2.C) once Phase 1 is playable.
4. Page 3 is the most sequential (3.A→3.B→3.C→3.D as a chain) — little to
   parallelize there beyond separating 3.A from prepping camera assets for 3.C.
5. **Page 4 is the largest parallelizable block in the whole level**: 4.A/4.B/4.C/4.D
   are 4 agents working simultaneously without stepping on each other.
6. Page 5 has parallelism again (5.A/5.B) before closing in a chain (5.C→5.D).

## Verification

Same as the rest of the project: `python tools/compile-check.py [tag]` after every
code task before calling it done. Nothing on this list is runtime-verified without
Diego actually playing it in the Editor — explicitly note in every PR/handoff what
was only compile-checked vs. what was actually played.
