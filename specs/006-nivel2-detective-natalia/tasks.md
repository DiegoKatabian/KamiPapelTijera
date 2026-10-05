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
  (`caughtBelonging`, `brokenWatch`, `arielScarfCap` (retired 2026-09-28, slot kept as
  `unusedArielScarfCap`), `pelusaPainting`) in
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

**Status: built 2026-09-22, played in Diego's full playthrough (2026-09-28).** Scope grew during the session per Diego:
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

- **1.D** **Café-wrapper fold beat** — ✅ wired 2026-09-25 (with Phase 3); since 2026-09-27 the fold
  also gives the `cafeTicket` inventory item page 5 unfolds (`OrigamiItemGiver`; before that the
  fold gave nothing, reported by Diego playing). The existing
  `SelloOrigami Cafe` scene instance moved under Page 1, auto-prompts, and appears when Natalia's
  opening dialogue ends (`NataliaDialogueTrigger._activateAfterOpening`).

- **1.E** **Quest05_FindClues** — ✅ built. Event-based on the new
  `Evento.OnAllCluesFound` (index 50), reward `None`. **Phase 2 must fire that event only
  once ALL clues are collected** (three since Diego's 2026-09-24 pass), and note that an Event quest also needs its handler in
  `QuestManager` (added: `SetAllCluesFound`) or it can never complete.

---

## Phase 2 — Page 2 (depends on 0.C tape, 0.D, Phase 1 in progress)

**Built 2026-09-24 (compiles; NOT yet played).** Diego's design pass changed the shape of
this page, so the tasks below record what was actually built, not the original draft:

- **Three clues, not two**: a **single glove** (`ResourceType.lostGlove`, new, appended
  last) in a trash can, the **hat** (`caughtBelonging`, now described as a hat) on the
  manhole, and the **broken wristwatch** (`brokenWatch`) on the museum's front-yard floor.
- **Cops gate the third clue**: two cops and a police car stand at the museum's broken
  fence. When Kami has **2** clues they drive off; only then can the police tape be cut.
- **The gift at Natalia's door closes the next quest** (was 3.D): talking to it completes
  and delivers `Quest06_GoBackToNataliasHouse`. Built here, placed on page 3.

- **2.A** `[P]` **Trash can as a flap** — ✅ built. Option A: stock `Solapa` +
  `TriggerSolapa` (full `PullSolapas` gesture), no variant script. **Every trash can in
  Level 2 (15, pages 1-5) is now `Prefabs/Interactables/TrashCan.prefab`**, which gives 2
  paper once; `TrashCan_ClueGlove.prefab` (a prefab variant) gives the glove instead and
  replaces page 2's `Kami_TrashCan`. Cans can be closed again; they are emptied once. The
  contents are a `GrantResourcePickup` in **Revealed** mode (opening = collecting), so the
  can and its contents never compete for the same button press. The open/close animation is
  a placeholder squash on a `Wobble` pivot (`Animations/TrashCan/`) until Valentino animates
  `Kami_TrashCan.fbx` (a single mesh today, no separate lid). Same prefab is meant for the
  sewer manholes later: only the art changes.

- **2.B** `[P]` **Police tape + cops** — ✅ built as `Prefabs/Level2/CrimeSceneGate.prefab`
  (`CrimeSceneGate.cs`): a nested `CuttablePoliceTape` whose trigger collider starts **off**
  (uncuttable), a solid `GapBlocker`, two placeholder cops (blue-tinted Natalia sprite) and a
  placeholder police car (`TrafficObstacle_Car`, driven off with `TrafficObstacle.Launch`, so
  its tuning is seconds-to-leave, not speed). On `Evento.OnCrimeSceneUnguarded` the cops vanish,
  the tape becomes cuttable and the car drives off; cutting the tape removes the blocker.
  **Placed at a placeholder position** — the museum fence is baked into `KamiMuseo.fbx` and has
  **no colliders at all**, so today Kami can walk around the gate; see open questions below.

- **2.C** `[P]` **Floor clues** — ✅ built: `CluePickup_Hat.prefab` and
  `CluePickup_Watch.prefab`, `GrantResourcePickup` in **PlayerTouches** mode (walk into it).
  Deliberately not an A/E interaction: Natalia follows Kami closely and her own dialogue
  trigger would compete for the same press. Placeholder sprites.

- **2.D** **Close Quest05 + start Quest06** — ✅ built as `FindCluesTracker.cs`
  (`Prefabs/Level2/FindCluesTracker.prefab`, placed on page 2). It counts clues in any order,
  queues Natalia's comment for each one, fires `OnCrimeSceneUnguarded` at 2 clues and
  `Evento.OnAllCluesFound` **exactly once** at 3 (latched). When Quest05 completes it queues
  her "these 3 clues should give us a great lead" line, removes Quest05 and adds
  `Quest06_GoBackToNataliasHouse` (Event-based on the new `OnGiftAtNataliasDoorReached`,
  with its `QuestManager` handler). `GiftDialogueTrigger.cs` + `GiftBox.prefab` (placeholder
  cube, page 3) fire that event when their first dialogue starts and deliver the quest when
  it ends.

**One Natalia, start to end (Diego, 2026-09-24)**: the page 1 instance is the only one. She
is not talkable while following (`NataliaDialogueTrigger.SetTalkable(false)`); the page 5
drop-off (5.C) must stop her follow and call `SetTalkable(true)`. The extra instances on pages
2 and 5 were removed.

**Page 5 continuity (Diego)**: page 5 is page 2 *after* all clues — tape already cut, no
cops, no car, empty clue can. Page 5 gets none of the page 2 gameplay objects; its three
trash cans are ordinary paper cans. Nothing carries state between the two pages.

**Open for Diego (Editor placement, all flagged `_PLACEHOLDER_POSITION` in the scene)**:
`CrimeSceneGate` onto the actual broken fence section, `GiftBox` onto Natalia's door, the
hat onto the manhole. The museum fence needs colliders (or the gate is decorative).

---

## Phase 3 — Page 3 (depends on 0.A letter, 0.C ribbon, Phase 2 complete)

**Built 2026-09-25 (compiles; NOT yet played).** Diego's design pass before building changed
three things, recorded here with the reason:
- **Quest06 closes after the arrest, not at the gift.** It completes and is delivered when the
  police sequence ends and Kami is in her cell on page 4, and Quest07 starts right then. The gift
  box no longer touches quests (its first line is now "what's this? let's cut it open!").
- **The ribbon can be cut at any time**, talked to or not.
- **Story origamis open by themselves** (`TriggerOrigami._promptAutomatically`), because it's more
  helpful for players; the classic step-on-it pedestal stays as the fallback. Same bool on the café.

- **3.A** **Gift box + cuttable ribbon** — ✅ built into `GiftBox.prefab`: nested `CuttableRibbon`
  (listener added from code), closed/open placeholder cubes, `PelusaPickup`
  (`GrantResourcePickup`, Revealed, `pelusaPainting`: Kami is literally caught holding it; 4.C
  confiscates it), Natalia's reaction (`Natalia_GiftOpened`).

- **3.B** **Letter fold + read beat** — ✅ a `SelloOrigami Letter` nested in the box appears when it
  opens and auto-prompts `OrigamiRoute_Letter`; its text shows on the new
  `OrigamiTextRevealPanel.prefab` (placed once under Level 2's Canvas). Final letter copy written.

- **3.C** **Arrest cutscene** — ✅ a real Unity Timeline, per Diego ("lean timeline workflow, I'll
  tune durations and camera takes later"): `ArrestCutscene.prefab` + `Timeline_Arrest.playable`,
  on generic pieces in `Assets/Scripts/Cutscenes/` (see `docs/claude/cutscenes.md`). Cops and Ariel
  appear on the street, the cops pop up behind the girls and escort them (Kami scripted-walks to the
  car door, Natalia and the cops follow her), sprites vanish + car door sound, the car drives off
  followed by the camera, and the page turns with Kami hidden. **Not a new `CameraMode`**: those
  would pollute the camera wheel (#43). No handcuff art exists; the beat is a dialogue line.
  Kami and Natalia land in **separate cells** (Diego: Kami gets her scissors back, then frees
  Natalia; Abuela breaking Kami's cell door is 4.A).

- **3.D** **Close Quest06 + start Quest07** — ✅ `Quest07_EscapeAndReturnThePainting` (Event,
  new `Evento.OnPaintingReturned` + its `QuestManager` handler, so 5.A only has to fire it).
  `OnGiftAtNataliasDoorReached` was renamed in place to `OnArrestSequenceEnded`.

**Hand-offs to Phase 4**: Natalia is idle and not talkable in her cell; freeing her must call
`StartFollowingPlayer()`. Kami's respawn point is already her cell (the forced page turn places her
through the normal placement path). The Pelusa is in the inventory for 4.C to confiscate.

**Open for Diego (Editor)**: drag the `_PLACEHOLDER_POSITION` objects into place (gift box,
`ArrestCutscene` street group/car/marks, both cells); retime the timeline; swap the `CarDoor`
placeholder clip; real art for Ariel, the cops, the open box and the Pelusa.

---

## Phase 4 — Page 4 (the largest; depends on 0.E, 0.F, 0.C posters/stacks, Phase 3
complete). Internally very parallelizable — 4 nearly independent systems that only
share the scene, not code:

**Cell flow (Diego, 2026-09-25/27)**: Kami and Natalia are in **separate cells**. The Abuela falls
from the sky and breaks **Kami's** cell door (4.A). Kami recovers her scissors (4.C), then cuts the
**padlock on Natalia's cell** (`CandadoCortable`, already on page 4) to free her, and Natalia starts
following again (`StartFollowingPlayer()`). Watch out: `CandadoCortable.ApplyCut()` calls
`cofreQueAbro.OpenChest()` without a null check and the page 4 instance has no chest, so it throws
today — the cell needs its own "what opens" hook.

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

**Status (2026-09-27): 4.A-4.E built, played in Diego's full playthrough (2026-09-28).** Details and file map in
`docs/claude/nivel2-y-ui.md` (Phase 4) and `docs/claude/enemigos-e-ia.md` (PoliceOfficer).
Decisions taken with Diego while building, and what changed from the draft above:

- **Layout**: Kami's cell is the west cell room (its east wall became a `CellFence`, no padlock); the
  evidence pickup sits right east of it, where Diego had the TijeraPickup. **Natalia's cell is new,
  in the front-left room, around the paper-plane pedestal**: freeing her is what opens the pedestal.
  The padlock moved there.
- **4.A** is a scripted fall (no Timeline, gameplay camera). First time: a narrator line ("tras ser
  engañadas...", Kami still), then 3 s, then the fall; after a capture, no narrator, the fall comes
  3 s after the respawn. Kami can move inside her closed cell while waiting and is only locked for
  the fall + the Abuela's two lines. The fence is switched off (reveal), with dust + `RockSmash`.
- **Getting caught restarts the WHOLE page** (not just a re-confiscation): both girls back in their
  cells, padlock and drapes whole, the Abuela gone and falling again after 3 s, cops back at their
  posts. The confiscation stash only grows, so a capture never loses anything; the plane hat is lost.
- **4.B**: only Kami can be spotted; alert = stop and stare / walk to the last seen spot / give up
  after 3 s, no chase; catching takes 1.5 s of plain view (meter drains when hidden). Spec 002's
  separate test scene was skipped (`startingPage = 4` sets the prison up by itself instead).
- **4.C** takes the scissors, all paper and the Pelusa; the pickup returns all of it.
- **4.D** needed nothing new: the two desk typewriters (2 paper, respawn 30s) cover it; the stray
  Phase 0 typewriter at the page origin was deleted. The padlock keeps its 1 paper.
- **4.E changed**: the window does NOT change the page. It is covered by new `CuttableDrapes` (top
  half stays, bottom half drops — an inverse bush); crossing it completes Quest07 ("escape") and
  starts the new **Quest08_ReturnThePelusa** ("take the original Pelusa back to the museum"). Quest07
  was split for this. The page is then left the normal way; the followers wait outside below the
  window. The escape origami is the Level 1 Avion route (2 folds, 2 paper, reusable).
- **Level 2 now starts with the normal scissors** (`Player._startWithTijera`); there is no upgraded
  pair in Level 2.
- No NavMesh rebake: every fence carves the NavMesh at runtime instead of being baked in.

**Still to verify by playing**: the whole loop above, the cone's readability and timings, the two
followers not blocking each other or Kami in the cell doorways, whether Kami's scissors reach the
padlock (moved to door-handle height) and the drapes, the Abuela's landing spot/scale/flip, and the
jump from the floor onto the mezzanine with the hat (22 units of jump vs ~18 of height: little margin).

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

**Status (2026-09-28): 5.A-5.D built, played in Diego's full playthrough (2026-09-28).** File map in
`docs/claude/nivel2-y-ui.md` (Phase 5). Decisions taken with Diego, and what changed from the draft:

- **The owner is Grace, the museum director**, a placeholder tinted sprite (`Grace.prefab`). What she
  says is picked by the page owner, `MuseumPage`.
- **Evidence = the hat + the glove (Ariel's) + the watch** (the time of the robbery). The scarf/cap
  item was retired (asset and list entries deleted, enum slot kept). The gate asks for the Pelusa,
  the ticket and the three clues; a normal playthrough always has them.
- **The café ticket is mandatory**: on page 1, Natalia only joins Kami once it is folded, and her
  new line there says she will help Kami get back to her book (Diego). Page 5's dialogue is about
  Grace thanking the girls and helping Kami and the Abuela home.
- **Order**: Grace's meeting -> the ticket pedestal pops up and auto-prompts the unfold -> the
  ticket's text -> the evidence dialogue (Natalia, the police apology, Grace's thanks) -> Quest08
  closes -> the five items leave the inventory, both followers stay where they stand, Natalia gets a
  farewell line, the catapult unlocks. **Revised after Diego's first play (same day)**: Grace's
  catapult offer is part of the evidence dialogue (one talk, not two); **Ariel is on the page** with
  one cop (they chased the escapees from page 4), accuses them, is arrested and apologizes to
  Natalia; Grace introduces herself and Natalia introduces the two of them. **Premise**: Natalia
  investigates a series of museum robberies and learns about the Pelusa from the café radio right
  after the fold (page 1), which sends them out for clues; Ariel was on her back all day at work and
  only just left her alone, so the girls' café receipt (21:47) is the alibi of both; Ariel had
  privileged access through his rich father.
- **5.D**: Interact on the catapult is the confirm. A short fade puts Kami in the bucket (invisible
  walls: she can't leave but can attack) and the Abuela next to her; the rope only becomes cuttable
  then. Cutting it plays `Timeline_Catapult` (camera hold, launch, fade, `Level2_EndCutscene`).
  The rope is the old loose root instance, moved (Diego). Placeholder cubes, no Animator: the
  loaded/fired art swap and the flight are code, timed by the timeline's signals.
- **Closing scene** `Level2_EndCutscene` is a placeholder copy of `Nivel1_EndCutscene` with a
  narrator dialogue (the flight, the Narrator's blue lamp, a third book), then MainMenu.
- Page 5 music stays Bohren. Root leftovers removed: the loose police tape and poster, the Level 1
  splash particles, the vertex-paint test plane, page 5's `NPC_Florista`.

**Still to verify by playing**: the whole page 1 -> 5 run with the new café requirement; Grace's,
the pedestal's and the catapult's placeholder positions; that Kami lands standing in the bucket and
reaches the rope (checked numerically only); the camera framing of the hold; the fade and the scene
change.

---

## Phase 6 — Presentation polish (designed 2026-09-28, after Diego played Phases 1-5)

Diego's notes after the first full playthrough of Level 2, designed the same day (all defaults
accepted). Everything here polishes flows that already work: no new systems except the followers'
plane ride (6.C). **Only one agent may write `Level2_Newspaper.unity` at a time**: 6.A-6.D all touch
it, so they run in sequence on one track; 6.E and 6.G are parallel-safe with them.

- **6.A** **Level 2 quest-start overlay**. Level 1 shows `MainQuestOverlay` when a specific
  dialogue ends (`OverlayManager.mainQuestTriggeringDialogue`); Level 2's `OverlayManager` still
  points at Level 1's dialogues, so it never fires. Trigger it when `Natalia_AfterTicket` ends (the
  moment she starts following), with a Level 2 text key in `UITexts` (the overlay's text is a
  `LocalizeStringEvent` on the prefab: override its entry on Level 2's instance, don't touch Level
  1's key). Copy (Diego picked option 1), es: *"A un nuevo libro llegaste / y los problemas
  empiezan a brotar. / Ayudá a tu nueva amiga, / que ella te ayudará a regresar."* + the "press E to
  continue" prompt as its own short line under the verse (`{INPUT:accion}`). en/pt adapted keeping a
  rhyme.

- **6.B** **Page 4 intro cutscene** (first time only, Timeline like the arrest, reusing
  `CutsceneDirector`/`CutsceneDialogueMarker`/signals). Order: camera tour of the page (cells ->
  the cop's patrol route -> Natalia's cell with the pedestal -> the mezzanine window; ~3 shots,
  ~8 s, every shot draggable) -> the patrolling cop says *"¡Acá van a quedarse encerradas para
  siempre! ¡Jajaja, ladronas embusteras!"* (`police_name`) -> Natalia: *"No somos embusteras ni
  ladronas."* -> the narrator's line, rewritten: *"Después de un rato encerradas, se escuchó un
  tremendo golpe..."* -> the Abuela falls `_abuelaDelaySeconds` later as today. Not skippable. After
  a capture: no cutscene (same rule as the narrator today). `PoliceStationPage.StartSequence`
  replaces its `IntroThenAbuela` coroutine with "play the intro cutscene, then schedule the Abuela".
  The cops must not detect Kami while it plays (they already go blind during `inCutscene`).

- **6.C** **Natalia thanks Kami + the followers ride the paper plane** (new, Diego 2026-09-28).
  1. When the padlock is cut (`PoliceStationPage.OnNataliaPadlockCut`), Natalia says (new dialogue
     `Natalia_Freed`): *"¡Gracias por salvarme! Tenemos que escapar por esa ventana abierta de
     allá arriba. ¿Podés doblar un avioncito de papel o algo así?"* — this is what points the
     player at the pedestal inside her cell.
  2. When the plane hat is folded (`Evento.OnOrigamiGivePaperPlaneHat`) while on page 4, both
     followers who are currently following **board the plane**: they stop following, their
     `NavMeshAgent` goes off, and they are held at seat offsets around Kami every frame
     (`LateUpdate`, the `RidingPage` pattern), so they fly up to the mezzanine with her.
  3. **They get off when the hat is used up** (Kami lands after the augmented jump:
     `Player.DestroyPaperPlaneHat`, which needs an event or a callback to hook): they are warped
     next to Kami, agents back on, following again. If the jump is wasted on the ground floor they
     simply walk again; refolding boards them again. Crossing the window keeps today's behaviour
     (both warped outside). A capture while riding must dismount them first (the page restart
     warps Natalia to her cell and hides the Abuela).
  4. Riding poses: placeholders now (same sprites, a small bob), real "sitting on the plane" art
     is **6.F**. Seat offsets and the bob are Inspector values.
  Check during implementation: whether the mezzanine is on page 4's NavMesh (if it isn't, the
  followers stand still up there until the window warps them out, which is acceptable).

- **6.D** **Page 5 arrival cutscene**. Ariel and the cop are **not** on the page at first.
  Talking to Grace plays a Timeline: Grace reacts (*"¿Puedo ayudarlas...? ¿¡Eso es el Pelusa!?"*)
  -> sirens -> the patrol car drives in (reuse the arrest's car/`TrafficObstacle.Launch` and the
  CopEscort/Ariel placeholders) -> Ariel and the cop walk up to the group while the camera pulls
  back to show them arriving, then returns to the group -> the already-approved `Grace_Meeting`
  lines (Ariel's accusation, the cop, Grace's and Natalia's introductions) -> the ticket pedestal
  appears as today. `MuseumPage` starts the Timeline instead of showing `_graceMeeting` directly.

- **6.E** `[P]` **DONE 2026-09-28 (played by Diego 2026-10-05)** **Sound effects pass (hooks + bank rows)**. Every hook in code with an
  `AudioBank` row and a placeholder clip where a similar sound exists; Diego swaps the real clips
  in the bank later (6.G). Missing today:
  - **Police (pages 3 and 5)**: siren loop, horn beeps, braking, running footsteps, handcuffs click
    on the `Arrest_Cuffs` line.
  - **Page 4**: cell bars slam when the girls are locked up (6.B), metal clank on the padlock cut,
    fence rattle when a fence opens, the Abuela's falling whistle + crash (today `RockSmash`), a
    whistle / "¡Eh!" when a cop first spots Kami, a sting when she is caught, typewriter clack at
    the paper sources, cloth rip on the drapes.
  - **Pages 1-2**: café ambience; radio static + news jingle before the announcement in
    `Natalia_AfterTicket`; manhole clank on the hat clue; a short "clue found" sting; car engines
    and horns on ambient traffic.
  - **Page 3**: the gift box opening; bar ambience.
  - **Page 5**: the rope creaking under tension; the catapult thud + whoosh (today
    `Jump_Paperplane`).
  - **Music**: page 4 still has no track (`PageMusicManager`, just an id to type in).
  Dialogue-synced sounds need a way to play a sound on a given line: decide during implementation
  between a per-line sound on `DialogueEvent` and a Timeline signal (cutscenes).
  **Done**: all of the above has a hook and a placeholder `AudioBank` row (27 ids, list and hook
  locations in `docs/claude/audio-y-particulas.md`); dialogue sounds use the new
  `DialogueEvent.soundOnLine` field (Handcuffs, RadioStatic, NewsJingle set); ambience via the new
  `PageAmbience` (prefab `Prefabs/Level2/PageAmbience.prefab`, still to be placed in the scene).
  Every clip is a placeholder: the real ones are **6.G**. `CopKnockedOut` has a row but no hook
  (no knock-out mechanic exists). Page 4 music: untouched (`PageMusicManager` handles an empty id
  without a warning).

- **6.F** `[P]` **[ART-VALEN] Riding poses**: Natalia and the Abuela sitting on / hanging from
  the paper plane (replaces 6.C's placeholders). Also the arrival/tour cutscenes' art needs (patrol
  car, Ariel, cops) are the same placeholders already tracked by the Phase 3 art issues.

- **6.G** `[P]` **Audio: real clips for 6.E** (Diego, audio lead): source/record every sound
  listed in 6.E and the page 4 music, then swap them into the `AudioBank` rows.

**Status 2026-09-29**: 6.A-6.E built; **played by Diego 2026-10-05, all good**. Added on Diego's request: patrolling cops are cuttable (knocked out `_knockedOutSeconds`=12, then resume).

**Parallelism**: one scene track (6.A -> 6.B -> 6.C -> 6.D, sequential because they all write the
scene), with 6.E (code + bank, no scene) alongside it; 6.F and 6.G are art/audio, outside code.

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
