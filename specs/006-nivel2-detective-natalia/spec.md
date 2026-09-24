# Feature Specification: Level 2 — The Stolen Pelusa (Natalia)

**Feature Branch**: `006-nivel2-detective-natalia`

**Created**: 2026-09-22 (revised 2026-09-22 after Diego's review pass)

**Status**: Draft

**Input**: Full 5-page Level 2 narrative (Diego, 2026-09-22): Kami meets Natalia,
they investigate the theft of a painting ("the Pelusa"), get framed by Ariel, end up
arrested, escape with the Grandmother's help, and clear their name at the museum
before catapulting back to their book. This is **"the most up-to-date truth"** of the
design — it replaces any earlier description that contradicts it (see "Docs updated"
at the end). Revised after a first review pass where Diego corrected several
technical assumptions (Origami reuse, Abuela's entrance, the catapult ending, the
capture/defeat text, and a project-wide prefab/ScriptableObject-first constraint) —
see each corrected item marked below.

## Context (from the code investigation, 2026-09-22)

The 5 pages were investigated against the current code before writing this spec (3
parallel agents, file:line citations kept in `tasks.md` per phase). Summary of what
already exists vs. what's new, **as corrected by Diego's review**:

**Reusable as-is:**
- `ICortable` + `TijeraHitbox` (`Assets/Scripts/Cortables/`) — generic, ready for any
  new prefab (poster, tape, ribbon, rope) with no hitbox changes.
- `PatrollingAgent` (`Assets/Scripts/AI/`) — NavMesh patrol base, already proven by
  `GallinaAgent`.
- `QuestSO`/`QuestManager`/`QuestEffector` — "one quest = one condition, completing it
  fires `GameObjectActivator`" is still how staged reveals and scene gating work.
- `NPC.cs`/`NPC_IdleState`/`NPC_FollowPlayerState` — generic follow FSM, with
  `StopFollowingPlayer()` as a direct precedent for "stays behind at this point."
- `ResourceType`/`InventoryItem` — precedent for a 1-count resource that's actually a
  narrative character/object (`abuela` as a `ResourceType`, see `Quest01_Abuela.asset`).
- **Origami / sello (stamp) system** (`Origami.cs`, `MultipleRectCheck.cs`,
  `PedestalCanvasDisplay.cs`) — **corrected**: this IS the mechanism for every
  fold/unfold beat in the story (café wrapper, trap letter, museum ticket), not a
  separate system. New content is authored as a new `OrigamiRoute` asset (e.g.
  `OrigamiRoute_Cafe`) using the existing route/pedestal pattern, folded in page 1 and
  unfolded again in page 5. See FR-005 below for what (if anything) needs extending.
- **Defeat/death pipeline** (`Player.Die()` → `DeathSequence()` → `DefeatOverlay`) —
  **corrected**: being caught by a police officer in page 4 reuses this pipeline
  as-is, same as Drowning/Rocoso. Only a new `DeathCause` value + a new localized
  overlay text key are needed — no separate non-lethal mechanism.
- **Reveal pattern via `GameObjectActivator`/`QuestEffector`** — **corrected**:
  Abuela's ceiling entrance in page 4 reuses this same "toggle GameObjects on an
  event" convention (already the project's standard way of doing scene reveals), not
  a new destruction system.

**Exists but needs extension:**
- ~~Abuela does NOT use the generic FSM above~~ — **resolved 2026-09-22**: she does now.
  The duplicated `Abuela_IdleState`/`Abuela_FollowPlayerState` were deleted and both she
  and Natalia run the shared `NPC_IdleState`/`NPC_FollowPlayerState`; only her Dropoff
  state is still her own.
- `Solapa`/`TriggerSolapa` forces Kami's full-body `PullSolapas` animation (see
  `docs/claude/spine-kami.md`). Fine for the manhole-cover flap if that gesture reads
  correctly; otherwise a trigger variant that skips `Player.PlayPullSolapa` is needed.
- `hasTijera` (`Player.cs:22`) is already a real state (with a full
  `NoScissortsOverride` animation set) but **is only ever set to `true`**
  (`GetTijera()`, `Player.cs:662-669`) — nothing currently strips it. Needs a
  symmetrical `LoseTijera()`.
- The paper-plane hat (`OrigamiPaperPlaneHat`, `Player.GetPaperPlaneHat()`) is today a
  jump buff, not an escape/page-transition trigger.
- The city cars (`Paper Car Prefab aniamted.prefab`, with a walk-cycle animation)
  are already placed 4× in `Level2_Newspaper.unity` with **zero script** — ready art
  waiting for behavior.

**Genuinely new (no precedent in the project):**
- Natalia as an NPC (zero code, zero assets — just a building's name in the scene and
  the old spec `003-natalia-npc-nivel2`, see below).
- Vision-cone / enemy detection (`specs/002-enemigo-sigilo/spec.md` is spec-only, zero
  code — the sole trace is a commented, never-implemented method in `Pathfinding.cs:7`).
- Ambient traffic system (cars/pedestrians that appear and disappear as obstacles) —
  no spawner/despawner of this kind exists (`EnemySpawner` was deleted, issues
  #23/#26/#28).
- Catapult level-ending — no existing physical launch; the only "end of level"
  precedent is an overlay button that calls `LevelManager.GoToScene` to a separate
  cutscene scene (`OverlayManager.cs:148-158`). **Corrected**: this feature doesn't
  need a new launch mechanism either — see FR-011.

## Relation to earlier specs

- **`specs/003-natalia-npc-nivel2/spec.md`** is now **superseded**: it assumed a
  Natalia with a single simple quest (the Abuela/Dalia/Tiburcio/Norberto mold). The
  real Natalia is a companion who travels with Kami across 5 pages inside a 3-quest
  detective arc. A pointer to this spec was left in that file.
- **`specs/002-enemigo-sigilo/spec.md`** is **reused and made concrete**: its
  already-closed decisions (vision cone + raycast LoS, an Alert state, returning to
  Patrolling after a timeout, "detection = death with cause + checkpoint respawn")
  are exactly what the page-4 police officer needs. This spec treats it as
  already-approved technical design for the `PoliceOfficer : PatrollingAgent` of
  Phase 4.

## Design constraint: prefab- and ScriptableObject-first (applies to every system below)

**Added per Diego's review, 2026-09-22.** Everything new in this feature should be
built as reusable **prefabs** and **ScriptableObject configs** wherever the existing
project conventions allow it (see `TextHighlightSettings`, `AudioBank`, `QuestSO` in
`CLAUDE.md` → "Convenciones de código" for the established pattern: data lives in an
editable asset, nothing needs manual scene wiring). The goal: once each system is
built, placing a new instance of it in a scene should be "drag a prefab in, set a few
Inspector fields," following short instructions — not writing or touching a script.
This applies specifically to:

- Traffic obstacles: a spawner prefab + an obstacle-set `ScriptableObject` (which
  prefab variants, speed range, spawn interval) rather than per-street-segment code.
- Cuttable variants (poster, tape, ribbon, rope): each a self-contained prefab,
  already the existing `ICortable` convention.
- The police officer: a prefab with Inspector-tunable vision angle/range/patrol
  waypoints, following `PatrollingAgent`'s existing virtual-extension-point pattern.
- Origami routes: already SO/prefab-based by convention — new routes are new assets,
  not new code.
- Quest chain data: already SO-based (`QuestSO`) — no change needed, just new assets.
- Evidence items: `ResourceType` + `InventoryItem` assets, already this pattern.

This is a cross-cutting requirement (FR-012 below), not a separate task — every phase
in `tasks.md` should be read with this in mind.

## User Scenarios & Testing *(mandatory)*

The "User Stories" are numbered by page because the story is sequential (page N
narratively depends on N-1) — but **Phase 0 and much of the per-page work is
parallelizable**, see `tasks.md` for the actual breakdown of what can be built
simultaneously.

### Story 1 — Page 1: Kami meets Natalia (Priority: P1)

Kami finds Natalia grumbling to herself. Natalia explains her problem with Ariel and
the stolen painting. Kami folds the café wrapper (an Origami route, see FR-005).
Natalia attaches herself to Kami and follows her. The "Find clues" quest starts.
Streets have cars/pedestrians that appear and disappear as obstacles.

**Independent Test**: talk to Natalia through her whole opening dialogue, confirm she
ends up following Kami and that the "Find clues" quest appears in the Flap.

**Acceptance Scenarios**:

1. **Given** the player is inside Natalia's trigger, **When** her opening dialogue
   finishes, **Then** `Natalia.isFollowing = true` and
   `QuestManager.AddQuest(QuestXX_FindClues)`.
2. **Given** Natalia is following Kami, **When** the player cuts a poster stuck to a
   pole, **Then** it cuts the same way a bush does (same `ICortable` flow).
3. **Given** ambient street traffic, **When** a car/pedestrian finishes its route,
   **Then** it despawns and (optionally) a new obstacle appears later — it's
   decoration/obstacle, not a progress blocker beyond normal physical collision.

---

### Story 2 — Page 2: Finding clues (Priority: P2)

Kami and Natalia check a trash can (opens like a flap) and find a belonging caught on
a manhole cover. They cut a strip of police tape to enter the museum and find a
broken wristwatch showing the time of the robbery. "Find clues" completes; "Go back
to Natalia's house" starts.

**Independent Test**: collect both clues (belonging + watch), confirm the quest
completes and the next one starts.

**Acceptance Scenarios**:

1. **Given** the trash can, **When** the player interacts (flap), **Then** the
   caught belonging is revealed/given.
2. **Given** the police tape is cut, **When** the player enters the museum, **Then**
   they can pick up the broken watch (evidence with the time of the robbery).
3. **Given** both clues are in the inventory, **Then** `OnQuestCompleted` fires for
   "Find clues" and the next stage's event/quest starts.

---

### Story 3 — Page 3: The trap (Priority: P3)

They arrive at Natalia's house. A giant gift box is at the door; Kami cuts the ribbon
and opens it: it's the original Pelusa. Using the same Origami route mechanism, they
unfold the letter inside: "the heist was a success... this is your cut." A shout of
"Federal police!" — the camera pulls back, Ariel accuses the girls in front of the
police. The police handcuff them and take them away in a patrol car (camera follows
the car) into the next page. "Go back to the house" completes; "Escape the police
station and return the painting" starts.

**Independent Test**: run the whole sequence from cutting the ribbon to the page
change that follows the patrol car, with no manual intervention between steps.

**Acceptance Scenarios**:

1. **Given** the ribbon is cut, **When** the box opens, **Then** the Pelusa
   (evidence/key object) appears and the letter's fold/unfold+read dialogue fires.
2. **Given** the letter has been read, **When** the dialogue ends, **Then** the
   arrest cutscene plays (camera pulls back → Ariel/police dialogue → handcuffs →
   camera follows the patrol car → page change) with no player input during the
   sequence.
3. **Given** the cutscene has ended, **Then** control returns to the player only on
   page 4, already inside the cell.

---

### Story 4 — Page 4: Escaping the police station (Priority: P4)

Kami and Natalia are in a cell. **Corrected**: the Grandmother falls on Kami, and the
nearby walls get disabled (with feedback — particles/sound) to open a path; there's
no real wall/ceiling destruction. Police officers patrol with vision cones — if they
spot the girls, they're sent back to the cell (reusing the normal defeat/respawn
pipeline with a changed overlay text, see FR-008). Grandmother and Natalia follow
Kami while she evades the officers, recovers the painting, her scissors, and her
confiscated paper, and folds a paper airplane (Origami) to reach the 2nd floor and
escape through a window. Inside the station there are cuttable objects (stacks of
paper, "wanted" posters) to get paper from if she needs it.

**Independent Test**: from the cell, escape while evading at least one patrolling
officer, recover scissors + paper, and exit through the window without ever being
caught (and separately: confirm being caught DOES send the player back to the cell
without it being a real game-over).

**Acceptance Scenarios**:

1. **Given** the page's start, **When** the scene loads, **Then** Kami has been
   stripped of **both her scissors and her paper** (`hasTijera = false`,
   `ResourceType.papel` zeroed with the confiscated amount remembered) and is locked
   up, and Abuela's entrance triggers automatically or via a trigger (fall on Kami →
   disable nearby walls + play feedback).
2. **Given** a patrolling officer, **When** Kami enters their vision cone and LoS
   with no obstruction for the configured time, **Then** the officer "catches" her —
   the normal defeat overlay plays with its text changed to something like "You were
   caught" (new `DeathCause` value + new localized overlay key, same pattern as
   `DefeatDrowning`/`DefeatRocoso`/`DefeatGeneric`), she respawns at a fixed point
   inside the cell, **is stripped of her gear again, and the recovery pickup resets**
   so the escape is fully repeatable.
3. **Given** the confiscated-gear pickup in the police station, **When** Kami reaches
   it, **Then** both her scissors and the confiscated paper amount are restored, and
   the pickup is consumed until the next capture re-arms it.
4. **Given** enough paper and her scissors recovered, **When** Kami reaches the
   2nd-floor window and uses the paper-airplane origami, **Then** the escape sequence
   triggers the change to page 5.

---

### Story 5 — Page 5: Clearing their name (Priority: P5)

They reach the museum, talk to the owner, and the confusion clears up: they unfold
the café ticket (same Origami route as page 1, now unfolded) to prove their
innocence, and compare their belongings with Ariel's (matching color palette —
scarf/cap) plus the watch matching the time of the robbery to prove his guilt. The
police apologize and the quest completes. Natalia and Grandmother stay behind. Kami
talks to the owner again; she tells Kami to use the catapult. Pressing E confirms it,
Kami and Grandmother get on, Kami cuts the rope, and they launch off. **Corrected**:
this is a hard-animated sequence, not a physics launch — the camera holds on the
assembled catapult; when the girls launch they quickly leave frame; the camera
lingers on the empty, used catapult; fade to black; the closing cutscene starts.

**Independent Test**: from talking to the owner through the rope cut, confirm the
final quest completes, Natalia/Abuela stop following Kami, and the scene transitions
to the ending cutscene.

**Acceptance Scenarios**:

1. **Given** both pieces of evidence (Ariel's belonging + the watch) are in the
   inventory, **When** Kami talks to the owner, **Then** the resolution dialogue
   fires and `QuestManager` completes "Escape the station and return the painting."
2. **Given** the quest is complete, **Then** `Natalia.StopFollowingPlayer()` and
   `Abuela.StopFollowingPlayer()` are called — both end up idle in place.
3. **Given** Kami talks to the owner a second time, **When** she presses E to confirm
   the catapult, **Then** the hard-animated sequence plays (Kami and Abuela board,
   Kami cuts the rope via `ICortable`, the camera holds on the empty catapult, fade to
   black), and the level transitions to the closing cutscene
   (`LevelManager.GoToScene` to a new scene — **NEEDS CLARIFICATION**: does Level 2's
   closing cutscene scene already exist, or does it need to be created/coordinated
   with Valentino, same as `Level1EndCutscene`?).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Natalia MUST be implemented on top of the generic
  `NPC`/`NPC_IdleState`/`NPC_FollowPlayerState` FSM (not duplicating the
  `NPC_Abuela` pattern).
- **FR-002**: The system MUST support two NPCs following Kami simultaneously
  (Abuela + Natalia in page 4) without shared state colliding.
- **FR-003**: The detective story MUST be modeled as 3 chained `QuestSO` assets
  ("Find clues", "Go back to Natalia's house", "Escape the station and return the
  painting"), each with its own condition (Resource or Event) gating the next via
  `QuestEffector`/events — not a single `QuestSO` with sub-objectives (the current
  system doesn't support that).
- **FR-004**: Cuttable posters/tape/ribbon/rope MUST implement `ICortable` following
  the existing `ObjetoCortable`/`PuertaCortable` pattern — no changes to the scissors
  hitbox.
- **FR-005** *(corrected)*: The café-wrapper fold, the trap letter, and the museum
  ticket unfold MUST reuse the existing Origami/sello system as new `OrigamiRoute`
  assets (e.g. `OrigamiRoute_Cafe`), not a separate new component. If the current
  route/pedestal system has no way to reveal text on completion or to represent
  "unfold an already-folded route," that gap MUST be closed as a small, targeted
  extension of the existing system (e.g. a reveal-text field/callback on the route
  asset) rather than building a parallel system.
- **FR-005b** *(added 2026-09-22, Diego's design call)*: The revealed text is a plain
  TMP line that appears when the player completes the **last** fold. The origami MUST
  NOT close instantly on completion: it holds while the player reads (a couple of
  seconds), then shows a "press X to close" prompt and waits for Interact — the same
  read-then-continue rhythm as a dialogue line, so the fold button press that finished
  the route can never dismiss the text it just revealed.
- **FR-006**: The system MUST provide ambient traffic (cars/pedestrians) that appear,
  move, and despawn as obstacles across all pages — one reused system, not one per
  page, configured via a prefab + obstacle-set ScriptableObject (see Design
  constraint above).
- **FR-007**: The page-4 police officer MUST inherit from `PatrollingAgent` and
  implement a vision cone + LoS, following the decisions already closed in
  `specs/002-enemigo-sigilo/spec.md`.
- **FR-008** *(corrected)*: Being detected by a police officer MUST reuse the
  existing defeat/respawn pipeline (`Player.Die()` → `DeathSequence()` →
  `DefeatOverlay`) with a new `DeathCause` value and a new localized overlay text key
  (same pattern as `DefeatDrowning`/`DefeatRocoso`/`DefeatGeneric`, e.g.
  `DefeatCaught` → "You were caught"), respawning at a fixed point inside the cell.
  No separate non-lethal mechanism is needed.
- **FR-009** *(extended 2026-09-22 after Diego's playtest review)*: `Player` MUST
  support losing and regaining **both the scissors and the paper** in code
  (`LoseTijera()` symmetrical to `GetTijera()`, plus zeroing/restoring
  `ResourceType.papel` with the confiscated amount remembered) for the page-4
  confiscation. Being caught mid-escape MUST re-confiscate and **reset the recovery
  pickup**, so the whole escape can be retried indefinitely from the cell.
- **FR-010**: All new dialogue MUST be registered in the 3 localization tables
  (es/en/pt) — no hardcoded Spanish text, consistent with the project's language rule
  for new content.
- **FR-011** *(corrected)*: The level ending (catapult) MUST be a hard-animated
  sequence (Animator-driven), not a physics launch — triggered by dialogue (E to
  confirm) + an `ICortable` rope + a camera hold on the empty catapult + fade to
  black + scene transition, without blocking if the player cancels before confirming.
- **FR-012** *(new, cross-cutting)*: Every new system in this feature MUST be built
  prefab- and/or ScriptableObject-first wherever the existing project conventions
  support it (see "Design constraint" above), so that placing a new instance in a
  scene is drag-and-configure, not new scripting.

### Key Entities

- **NataliaDialogueTrigger** / **NataliaNPC** — on top of `NPC`/`NPC_IdleState`/
  `NPC_FollowPlayerState`, not the fixed 4-dialogue `QuestDialogueTrigger` mold (her
  arc is longer than request→reminder→thanks→chat).
- **Quest_FindClues**, **Quest_GoBackToNataliasHouse**,
  **Quest_EscapeAndReturnThePainting** — 3 new `QuestSO` assets, same mold as the
  existing 4.
- **Evidence items** (caught belonging, broken watch, Ariel's scarf/cap, the Pelusa) —
  new `ResourceType` entries + `InventoryItem` assets, 1-count, same pattern as
  `abuela`.
- **OrigamiRoute_Cafe**, **OrigamiRoute_Letter** *(corrected — replaces the earlier
  "UnfoldableNote" idea)* — new `OrigamiRoute` assets/prefabs on the existing
  Origami/sello system, foldable in page 1/3 and unfoldable again where the story
  needs it (page 5's ticket), possibly with a small reveal-text extension (see
  FR-005).
- **CuttablePoster/Ribbon/Tape/Rope** — new `MonoBehaviour : ICortable`, one per
  visual type, minimal logic (same as `PuertaCortable`).
- **PoliceOfficer : PatrollingAgent** — new, vision cone + Alert state + reuses the
  defeat/respawn pipeline with `DeathCause.Caught` (or equivalent) on detection.
- **TrafficObstacle** (working name) — new spawn/move/despawn system for
  cars/pedestrians, configured via an obstacle-set ScriptableObject.
- **Abuela's entrance** *(corrected — no longer a "destructible wall" entity)* — a
  new triggered behavior (fall-on-Kami) that disables nearby wall GameObjects via the
  existing `GameObjectActivator`/`QuestEffector` reveal pattern, plus particle/sound
  feedback.
- **Catapult** *(corrected — no longer a physics entity)* — a new hard-animated
  end-of-level trigger (E → board → cut rope → Animator-driven launch → camera hold →
  fade → `GoToScene`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 5 pages can be played end to end with no manual editor
  intervention (one full run from "talk to Natalia" to the catapult).
- **SC-002**: The story's 3 quests complete in order and are reflected in the Flap
  (`QuestSlot`) at every stage.
- **SC-003**: Zero hardcoded Spanish text for new dialogue — everything goes through
  the 3 localization tables.
- **SC-004**: Being caught by a police officer in page 4 never leaves the player in a
  broken state (permanently scissors-less, unable to retry, etc.) — it's repeatable
  indefinitely via the normal defeat/respawn pipeline.
- **SC-005**: Reuses ≥80% of the existing `ICortable`/`PatrollingAgent`/`NPC`/
  `QuestSO`/Origami patterns for anything that fits those molds — new systems are
  written only where the project genuinely has no precedent (traffic, vision cone).
- **SC-006** *(new)*: Once each new system (traffic, cuttable variants, police
  officer, origami routes) is built, adding a new instance of it to a scene requires
  only dragging in a prefab and editing Inspector fields — verified by having someone
  other than the implementing agent place one following short written instructions,
  with no new script needed.

## Assumptions

- The café wrapper in page 1 and the museum ticket in page 5 **(resolved)** are the
  same Origami-route interaction, folded then unfolded — not a separate
  narrative-only beat and not a wholly new system.
- Evidence items are 1-count `ResourceType` items, not a separate "key item" system —
  none exists in the project and one isn't justified just for this.
- Being caught by a police officer in page 4 **(resolved)** reuses the normal
  death/respawn pipeline with a re-skinned overlay text — it is not a separate
  lighter-weight mechanism.
- Abuela's page-4 entrance **(resolved)** does not involve any real wall/ceiling
  destruction — it's a triggered fall + disabling nearby wall GameObjects (reusing
  the existing reveal convention) + particle/sound feedback.
- The catapult ending **(resolved)** is hard-animated, not physics-based — a camera
  hold + fade to black replaces any actual launch physics.
- Level 2's closing cutscene scene (after the catapult) is design/art's
  responsibility to coordinate separately (same pattern as `Level1EndCutscene`) — the
  code only needs the hook point (`GoToScene`).
- ~~Refactoring `NPC_Abuela` to use the generic FSM is out of scope~~ — **reversed by
  Diego on 2026-09-22**: Abuela's duplicated follow behaviour was scrapped outright and
  both NPCs now share Natalia's. Idle/follow/animator/sprite-flip/page-reparenting live
  on the `NPC` base; only her Dropoff state remains hers. It also fixed two latent bugs
  (a `KeyNotFoundException` on stopping her follow, and endless velocity drift). See
  `docs/claude/quests-y-dialogos.md`.
- Every new system in this feature should default to prefab/ScriptableObject
  authoring over scene-embedded logic (see "Design constraint" above) unless a
  specific system genuinely can't be expressed that way — that exception should be
  called out explicitly when it comes up during implementation, not silently
  defaulted to.

## Docs updated

- `docs/claude/quests-y-dialogos.md` — Natalia section and NPC roster updated to
  point here.
- `docs/claude/nivel2-y-ui.md` — museum-page status updated with the full 5-page arc.
- `specs/003-natalia-npc-nivel2/spec.md` — marked `Superseded by 006`.
