# Kickoff prompt — Spec 012: followers ride the page turn (Level 2)

Paste everything below the line into a fresh Claude Code session at the repo root.

---

We're implementing **spec 012** in KamiPapelTijera (Unity 2021.3, URP, Spine 4.2 runtime). Goal of this
session: **when Kami turns a page in Level 2, Natalia (and the Abuela when she follows) grab on to her,
fly over the page with her and land next to her on the new page, following again.** Today they stay
frozen at the old page's far edge. Three small riders travel with it: during the page 3 arrest Kami must
walk (not skip) to the patrol car, Kami's skeleton physics must ignore the
turn's teleports, and the main-menu Kami must wear Libro 1 + base shoes, no scissors. Everything is already
diagnosed and designed: build it, don't re-derive it.

## Branch and state

- Work on the **current branch** (`level2newspaper-pages-blocking-2` at the time of writing; check
  `git status` and `git branch --show-current`). Expect these UNCOMMITTED files from the triage session,
  all intentional: `Assets/Prefabs/Kami/Kami.prefab` (`m_IsKinematic: 1`, the fix for the shaking Kami),
  `Assets/Scenes/Level2_Newspaper.unity` (Diego's own `m_IsKinematic` scene override, now redundant),
  `docs/triage/`, `specs/012-page-turn-followers/`, plus doc edits. Don't revert or "clean" any of it.
- **Never commit, push or open a PR without Diego's explicit permission, every time.** Approval to build
  is not approval to commit: leave the work uncommitted for his review.
- GitHub (M4): epic **#156**; tasks 0.A **#157**, 1.A **#158**, 1.B **#159**, 1.C **#160**, 1.D **#161**, 2.A **#162**, 2.B **#163**, 2.C **#164**, 3.A **#165**, 3.B **#166**. Don't close or comment on any until Diego has played the work and asks for the close-out.

## Read first (in this order)

1. `specs/012-page-turn-followers/spec.md` (all of it: requirements, design, risks, verification) and
   `tasks.md`.
2. `docs/triage/2026-10-07-followers-page-turn-and-kami-physics.md`: the evidence behind the spec. Section 2
   is **resolved** (a non-kinematic Rigidbody fought the CharacterController; spine-unity fed the root's
   Y wobble 1:1 into the physics). Read it for the units lesson: skeleton units ARE local Unity units.
3. `docs/claude/paginas-y-hoja.md` (the page-turn event flow, `RidingPage`, `PositionMarker`),
   `docs/claude/enemigos-e-ia.md` and `docs/claude/quests-y-dialogos.md` ("Los NPC se mueven por NavMesh",
   "Un solo comportamiento de follow"), `docs/claude/nivel2-y-ui.md` (Level 2 pages and `PaperPlaneRide`),
   `docs/claude/spine-kami.md`.
4. Code, whole files: `Level2/PaperPlaneRide.cs` (**the mechanic to reuse; copy its Rider, Board, seat
   `LateUpdate` and `TryLand`, don't edit it**), `Managers/PageScrollerManager.cs`,
   `Managers/PlayerPageSpawnManager.cs`, `AI/PageNavMeshManager.cs`, `NPCs/NPC.cs`,
   `NPCs/NPC_FollowPlayerState.cs`, `Player/Player.cs` (`StartRidingPage`, `StopRidingPage`, `LateUpdate`,
   `IsRidingPage`, `FeetPosition`, `SkeletonAnimation`), `Gear/SpineSkinComposer.cs`, `Gear/PlayerGear.cs`
   (`EquippedInCompositionOrder`), `Gear/GearLoadout.cs`, `Gear/GearCatalog.cs`.
5. Scenes, to understand, not yet to edit: where `Natalia` and `AbuelaEntrance` sit in
   `Level2_Newspaper.unity`, and the `kami_spine_titlescreen` object in `MainMenu.unity`.

## Scope, in order (see `tasks.md`)

- **Phase 1, three disjoint units, `[P]`:**
  - **1.A `Level2/PageRideFollowers.cs`** (FR-001..008). The design is decided: **edge-detect
    `Player.IsRidingPage` in `Update`** (board on false->true, land on true->false; do NOT depend on the
    order of `OnPageFinishTurning` subscribers), boards `FindObjectsOfType<NPC>()` filtered
    `isFollowing && activeInHierarchy`, slots by distance to Kami, a short ease then an exact seat
    hold in `LateUpdate` with `[DefaultExecutionOrder]` so it runs AFTER `Player.LateUpdate`, landing =
    `SamplePosition` + `agent.Warp` + `StartFollowingPlayer()` with park-and-retry, `OnDisable` hands the
    agents back.
  - **1.B Physics reset on teleports** (FR-009): `Player.SkeletonAnimation.ResetLastPositionAndRotation()`
    right after `transform.position = newPos` on the first ride frame in `Player.LateUpdate`, and in
    `PlayerPageSpawnManager.PositionPlayerAtPoint` after the CharacterController is re-enabled.
  - **1.C `LoadoutSkin`** (FR-010) for the main-menu Kami, composed from `Level1_StartLoadout` sorted by
    `GearItem.Slot`.
  - **1.D Arrest walk** (FR-011): the escort's `_escortWalkInput` is 0.6 and `Player.walkThreshold` is
    0.5, so Kami skips. Set the prefab value to 0.45 and clamp it in `ArrestCutscene.Update` to just below
    the threshold, with a one-time warning. Do NOT redesign the escort (cops leading by NavMesh): that is a
    separate spec.
- **Phase 2, wiring, ONE writer:** 2.A scene root placement in `Level2_Newspaper.unity`; 2.B `LoadoutSkin`
  on `kami_spine_titlescreen` + `initialSkinName`; 2.C check each page's landing point with
  `NavMesh.SamplePosition` (temporary logs, remove after).
- **Phase 3:** docs, `/graphify Assets/Scripts --update`, and the playtest handoff.

## Ask Diego before building (each with a recommended default; one `AskUserQuestion` call)

1. **The inactive Atlas 12 "The Paper Model (SkeletonAnimation)" in `MainMenu.unity`** (skin
   `Tijera_Normal`): delete it, or repoint it at Atlas 13 + `LoadoutSkin`? Default: ask what it was for
   (a loading animation?), and if unused delete it. Never leave it able to show scissors.
2. **When do followers start moving to their seats?** Default: at `Player.StartRidingPage` (the same
   moment Kami snaps onto the edge). Alternative: at `OnPageTurnStart`, so a far follower has the whole
   `delayTime` to walk over before the leaf appears.
3. **Seat numbers.** Default = `PaperPlaneRide`'s (slot 1 (-2.5,-0.5,0.6), slot 2 (-5,-1.2,0.6), bob
   0.35 at 6 rad/s, ease 0.35 s, landing spacing 3). Diego tunes them live in the Inspector during a turn,
   so just make every one a `[SerializeField]` with a `[Tooltip]` and read it every frame.

## Decided rules: don't reopen these

- Every follower boards, even one far away (it eases to its seat). Level 2 only: the component lives in
  `Level2_Newspaper.unity`, no Level 1 script changes. The arrest's forced turn
  (`TurnToNextPage(ridePage: false)`) never sets `IsRidingPage`, so nobody boards, and the cutscene keeps
  its own warps.
- `PaperPlaneRide` stays untouched. A small helper used only by the new component is fine; unifying the
  two is a follow-up.
- Placeholder art: the same sprite + bob. The real "holding on" pose is Valentino's, later.

## Gotchas already paid for

- **Never `agent.updatePosition = false` and leave it:** every exit path (disable, scene unload, ride end)
  must give it back. `PaperPlaneRide.ForceDismountAll` is the model.
- **`StopFollowingPlayer()` BEFORE touching the agent** (it calls `StopAgent`, which needs a usable agent).
- **The NavMesh is swapped at `OnNewPageOpen`, seconds before the leaf finishes.** Landing happens after
  the ride ends, so the new page's mesh is already active. Followers boarded on the old mesh come back
  with `Warp`, never by writing `transform.position` on an agent (it gets overwritten).
- **Followers are not inside a page folder** (`StartFollowingPlayer` reparents them to Kami's parent, the
  scene root), so `TogglePages` doesn't disable them. Don't reparent them again.
- **`IsRidingPage` flips inside `FinishRide`, in the same event that places Kami.** Polling it one frame
  later is what makes the landing safe; an event subscription could run before the placement.
- **No `ScriptExecutionOrder` asset exists.** Seat hold needs `[DefaultExecutionOrder]` after
  `Player.LateUpdate`, or the riders wobble one frame behind Kami.
- **`NavMesh.SamplePosition` radius 5 can miss** on a page whose mesh is not baked near the entry: park
  and retry like `PaperPlaneRide.Update` does, and warn.
- **A deleted `Update()`/`LateUpdate()` compiles clean.** When you edit `Player.cs` or
  `PlayerPageSpawnManager.cs`, list the methods before and after. Don't touch the single `cc.Move` in
  `PlayerModel.ApplyPhysics` (issue #30).
- `EventManager` swallows handler exceptions into a log: log decisions as
  `Debug.Log($"[PageRideFollowers] ...")`.
- Spine units: skeleton units = local Unity units (the scale is applied at JSON load). The physics feed
  is 1:1; that's why the teleport reset matters.
- The Kami prefab's Rigidbody is now **kinematic**. Don't switch it back to fix a trigger problem: ask.

## Working rules

- English for everything new (code, comments, docs, names) and in replies to Diego, even if he writes
  Spanish. Leave existing Spanish alone, except a comment that becomes false.
- Comments explain the non-obvious why. Braces always, guard clauses that warn when a reference that should
  be wired is missing, `[SerializeField] private` + `[Tooltip]` for anything tunable, `[ClassName]` logs.
- Do the mechanical work yourself (YAML, `.meta` files with `python tools/make-meta.py`, docs). Ask Diego
  about decisions, not labour. YAML surgery: read the file whole, unique fileIDs, preserve each file's
  line endings (mixed in this repo), count `--- !u!` blocks before and after, grep fileID uniqueness.
  The Unity Editor is probably open: no live Editor control from here.
- Agents: none unless Diego asks (max 3, only one writes a scene).

## Verification

- `python tools/compile-check.py` after each task. Baseline warnings: `JumpFloodOutlineRenderer` CS0162,
  `HongueroTiburcioDialogueTrigger` CS0414. That is **compilation only**: say clearly what did not run
  (feel, timing, NavMesh landing, the Spine physics are all Diego's playtest).
- Hand Diego the playtest checklist in `spec.md` ("Verification") plus the named risks. Never report the
  feature as "verified"; report what was built, what compiled, and what only he can confirm.
- At the end, ask what to commit. If he approves, one descriptive commit, and per his usual close-out
  routine: status comments on issues only if he asks for issues.
