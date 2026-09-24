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

**Status: DONE (2026-09-22), revised the same day after Diego's first playtest.** All 6
tracks implemented by parallel agents; combined compile-check clean. See issues
#86-#91 (sub-issues of epic #24); this section records outcomes, not just plans.

**What the playtest caught (and the lesson):** compile-checking and file-structure
review verified everything that is checkable without running the game, and both of the
bugs that mattered slipped straight through it:
1. **Traffic never despawned** — cars crossed an 80-unit street at a default speed of
   2-4 units/second, so they took 20-40s each while spawning every 5-10s, and piled up.
   The root cause was the *knob*, not the loop: "speed in units/second" is meaningless
   without knowing the world scale. Fixed by authoring **travel duration in seconds**
   instead (scale-independent), plus a max-alive cap and a max-lifetime safety despawn.
2. **Seven `.meta` files had the wrong importer** — `tools/make-meta.py` always writes
   `MonoImporter` (it is built for `.cs`), but prefabs need `PrefabImporter` and
   ScriptableObject `.asset` files need `NativeFormatImporter`. Any future hand-made
   `.meta` for a non-script asset must be corrected after running that tool.

None of this is page-specific; build it once, reuse it several times. Each track is
a separate agent — no file overlap between them.

- **0.A** `[P]` **Café/letter/ticket Origami routes** — ✅ Done. Built
  `OrigamiTextReveal`/`OrigamiTextRevealDisplay` (new, additive, subclass of
  `OrigamiEventTriggerer`) plus 3 new route prefabs: `OrigamiRoute_Cafe_Fold`,
  `OrigamiRoute_Cafe_Unfold`, `OrigamiRoute_Letter` (all in
  `Assets/Prefabs/OrigamiRoutes/`). **Correction to this doc's own earlier framing**:
  `OrigamiRoute` is a prefab pattern (an `Origami` MonoBehaviour + an array of
  `OrigamiRoute` drag-path components), not a ScriptableObject — see
  `docs/claude/origami-y-tooltips.md` for the corrected explanation. No "play a route
  backwards" capability exists or was added — fold and unfold are two separate
  prefabs (mirrors the existing Abuela fold/unfold precedent), toggled by
  `QuestEffector`. **Open before 1.D/3.B/5.B**: the 3 new prefabs still use Abuela's
  placeholder art (`OSU-Abuela.prefab`); needs real `OSU-Cafe`/`OSU-Letter` template
  prefabs from Valentino before it ships for real; localized text is placeholder
  copy, not final narrative text.

- **0.B** `[P]` **Ambient traffic** — ✅ Done, **fixed after playtest**.
  `TrafficObstacle`/`TrafficObstacleSet`/`TrafficSpawner` in `Assets/Scripts/Traffic/`,
  plus `TrafficObstacle_Car.prefab`, `TrafficSpawner.prefab`,
  `TrafficObstacleSet_StreetCars.asset` in `Assets/Prefabs/Traffic/`. Wraps the
  existing `Paper Car Prefab aniamted.prefab` art without modifying it; a pedestrian
  variant is just a new prefab + the same component, no new code.
  Timing is authored as **travel duration (seconds to cross the segment)**, never raw
  speed — the spawner derives per-instance speed from the real spawn→despawn distance,
  so the same asset reads correctly on a 5-unit alley and an 80-unit avenue. Also has
  `maxAlive` (skips spawns at the cap, so obstacles can't stack) and `maxLifetime` (a
  hard despawn that fires even if arrival never happens). Collision is the car's
  existing non-trigger `MeshCollider` — blocks the player's `CharacterController` but
  doesn't shove it (no `Rigidbody`); fine for ambient decoration, a deliberate
  non-goal.

- **0.C** `[P]` **New cuttable prefabs** — ✅ Done, **rebuilt after playtest**.
  The first pass was four `PuertaCortable`-style scripts (sound + `Destroy`) with no
  prefabs, which was both wrong behaviour and an unnecessary hand-off. Replaced with
  the real bush pattern (`ObjetoCortable`: whole sprite off → base + top on → top
  thrown in an arc and shrunk):
  - `CuttablePoster.prefab` uses **stock `ObjetoCortable`** — no new script needed.
  - `CuttableTwoPieces.cs` (one new class) extends `ObjetoCortable` for things that
    split sideways into two halves that BOTH fly out: `CuttablePoliceTape.prefab`,
    `CuttableRibbon.prefab`, `CuttableRope.prefab`. Inherited slots are reused as
    whole/left/right, and it adds a `UnityEvent onCut` for the ribbon (gift box) and
    rope (catapult launch), whose targets are later tasks.
  All four prefabs are built, with a trigger `BoxCollider` + kinematic `Rigidbody`
  (required: two triggers only report a hit if one side has a Rigidbody) and the bush
  sprites as stand-in art for Valentino to swap.

- **0.D** `[P]` **Evidence data** — ✅ Done. 4 new `ResourceType` values
  (`caughtBelonging`, `brokenWatch`, `arielScarfCap`, `pelusaPainting`) in
  `LevelManager.cs` + matching `InventoryItem` assets in `Assets/Scripts/Inventory/`
  (sprites left empty, no art yet), with real es/en/pt localization copy. **Open**:
  not yet added to any scene's `InventoryManager._allItems` array — whichever task
  places the actual pickups (2.A/2.C/3.A) needs to do that in the Editor.

- **0.E** `[P]` **`Player.LoseTijera()`** — ✅ Done. Mirrors `GetTijera()`: sets
  `hasTijera = false`, decrements `ResourceType.tijera` by 1, refreshes `PlayerView`.
  Guarded against double-calling when already unequipped. Nothing calls it yet (that's
  4.C).

- **0.F** `[P]` **`DeathCause.Caught` + defeat overlay text** — ✅ Done. Added
  `DeathCause.Caught` + `DefeatCaught` localized key (es/en/pt), wired into
  `DefeatOverlay.GetKeyForCause()`. Falls back to the generic `Death` animation (as
  intended — no dedicated anim) and the normal page-entry respawn (a fixed
  cell-respawn point doesn't exist yet — no jail scene to point it at). **Bonus
  finding**: investigated issue #20 (incomplete `DefeatOverlay` scene setup) —
  `Assets/Prefabs/UI/Overlays Parent.prefab` already has it correctly wired, and
  `Level2_Newspaper.unity` uses that prefab, so Level 2 looks already fixed;
  `Nivel1_KamiPapelTijera.unity` does NOT reference it, so the issue still stands for
  Level 1 specifically — worth confirming in-Editor before closing #20.

---

## Phase 1 — Page 1 (depends on 0.A, 0.C, 0.D)

**Status: built 2026-09-22, not yet played.** Scope grew during the session per Diego:
posters go in pages 1/2/3/5 (not just page 1), traffic gets a vertical + horizontal car
in every page, and a cuttable typewriter was added for page 4.

- **1.A** `[P]` **NataliaDialogueTrigger + NPC** — ✅ built. `NPC_Natalia : NPC` uses the
  generic FSM as-is; `NataliaDialogueTrigger : TriggerDialogue` (one level BELOW
  `QuestDialogueTrigger`, whose 4-dialogue mold subtracts a resource on dialogue 2 and
  can't express an Event quest). Opening dialogue → `AddQuest` + `StartFollowingPlayer`.
  `Natalia.prefab` placed in Page 1 with placeholder art. See
  `docs/claude/quests-y-dialogos.md` for the Event-quest handler gotcha this surfaced.

  **Follow-up (Diego, same day): Abuela's duplicated follow behaviour was scrapped and both
  NPCs now share Natalia's.** `Abuela_IdleState`/`Abuela_FollowPlayerState` deleted (and
  their `State` enum entries); the walk animator, sprite flip, page-reparenting and
  start/stop-following moved onto the `NPC` base; `Abuela_DropoffState` survives via a new
  `TryExtraTransitions()` hook.

  **Then (Diego, 2026-09-24) all NPC movement moved onto the NavMesh**, like the chickens:
  the hand-rolled `AddForce`/`Arrive` steering is deleted and `NPC` drives its own
  `NavMeshAgent` (Rocoso's precedent). `NavMeshAgent` added to `Natalia.prefab` and
  `Abuela.prefab`. **Blocker for testing: Level 2's pages need a baked NavMesh** or the NPCs
  simply will not move (`PageNavMeshManager`, issue #21). This also settles 4.B's movement:
  `PoliceOfficer : PatrollingAgent` already gets NavMesh patrol for free.

  This removed the tech debt the spec had explicitly parked
  ("refactoring `NPC_Abuela` is out of scope") and fixed two latent bugs — a
  `KeyNotFoundException` when stopping her follow, and endless velocity drift after
  stopping. Page 5's "Natalia and Abuela stay behind" (5.C) now works off one code path.

- **1.B** `[P]` **Cuttable posters** — ✅ built, scope expanded: 2 posters each in pages
  1, 2, 3 and 5, plus `CuttableTypewriter.prefab` (new, `PickupCortable`, grants 5 paper,
  respawns) placed in page 4. Positions are placeholders — Diego places them by hand.

- **1.C** `[P]` **Ambient traffic** — ✅ built, scope expanded: a vertical AND a horizontal
  spawner per page, obstacle sets pre-assigned. The horizontal side needed new assets
  because `Andando horizontal.anim` existed but no controller referenced it:
  `Paper Car Horizontal.controller`, `TrafficObstacle_Car_Horizontal.prefab`,
  `TrafficObstacleSet_StreetCarsHorizontal.asset`.

- **1.D** **Café-wrapper fold beat** — route asset ready (`OrigamiRoute_Cafe_Fold`,
  2-step `OSU-Avion` art per Diego); still needs its pedestal/scene wiring.

- **1.E** **Quest05_FindClues** — ✅ built. Event-based on the new
  `Evento.OnAllCluesFound` (index 50), reward `None`. **Phase 2 must fire that event only
  once BOTH clues are collected**, and note that an Event quest also needs its handler in
  `QuestManager` (added: `SetAllCluesFound`) or it can never complete.

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
  On capture, besides the defeat overlay + cell respawn, it must re-trigger the
  confiscation and reset of 4.C — Kami loses her gear again every time she's caught.

- **4.C** `[P]` **Confiscated gear + recovery pickup** *(scope confirmed by Diego
  2026-09-22)*: on imprisonment Kami loses **both scissors and paper** — `LoseTijera()`
  (0.E) plus zeroing `ResourceType.papel` while remembering how much was taken. A
  pickup in the police station restores both. Getting caught (4.B) re-confiscates and
  **re-arms the pickup**, so the escape is repeatable from the cell forever. Needs a
  small stateful component to hold the confiscated amounts across capture cycles.

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
