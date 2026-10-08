# Tasks: Followers ride the page turn (spec 012)

GitHub (M4): epic #156; 0.A #157, 1.A #158, 1.B #159, 1.C #160, 1.D #161, 2.A #162, 2.B #163, 2.C #164, 3.A #165, 3.B #166.

Phases run in order; `[P]` tasks inside a phase are parallel-safe with their siblings. Only ONE task
writes a scene (2.A). The Rigidbody fix that started this is already done (see `spec.md`).

## Phase 0 — Confirm the diagnosis (small, Diego plays)

- [ ] **0.A** Optional but cheap: Diego turns one page in Level 2 with Natalia following and
  `NPC._debugMovement` on, and the console shows `[NPC] Natalia CANNOT MOVE ... isOnNavMesh == false`
  right after `[PageNavMeshManager] NavMesh activado para pagina N`. Confirms causes 1 and 2 of the
  triage. If it doesn't match, stop and re-triage before building.

## Phase 1 — The four units (all `[P]`, disjoint files)

- [x] **1.A** `[P]` **`PageRideFollowers`** (FR-001..008, `Assets/Scripts/Level2/`): poll
  `Player.IsRidingPage`, board generic `NPC`s with `isFollowing`, ease + seat hold in `LateUpdate`
  (`[DefaultExecutionOrder]`), land with `SamplePosition` + `Warp` + `StartFollowingPlayer`, park-and-retry,
  `OnDisable` hands every agent back. All numbers in the Inspector with tooltips. `[PageRideFollowers]`
  logs at board, land and park. `.meta` with `python tools/make-meta.py`. Compile-check.
- [x] **1.B** `[P]` **Physics reset on teleports** (FR-009): `ResetLastPositionAndRotation()` on
  `Player.SkeletonAnimation` right after the ride's first snap onto the edge bone (in `Player.LateUpdate`,
  after `transform.position = newPos`, first ride frame only) and in
  `PlayerPageSpawnManager.PositionPlayerAtPoint` (after the CharacterController is re-enabled).
  Guard clauses with warnings for a missing `SkeletonAnimation`. Do NOT touch the `cc.Move` in
  `PlayerModel.ApplyPhysics` (issue #30). Compile-check.
- [x] **1.C** `[P]` **`LoadoutSkin`** (FR-010): the component on the main-menu Kami, composed from
  `Level1_StartLoadout`, sorted by `GearItem.Slot`. Compile-check. (The scene wiring is 2.B.)
- [x] **1.D** `[P]` **Arrest walk** (FR-011): `_escortWalkInput` 0.6 -> 0.45 in
  `Assets/Prefabs/Level2/ArrestCutscene.prefab` (and the scene instance if it overrides it), plus the guard in
  `ArrestCutscene.Update`: `Mathf.Min(_escortWalkInput, _player.walkThreshold - 0.05f)` with a one-time
  warning, and the field's tooltip says why. Compile-check. Diego checks the walk, the foot sliding and
  that Kami still reaches the door before `CUE_BoardCar`.

## Phase 2 — Wiring (depends on Phase 1; ONE agent writes the scenes)

- [x] **2.A** **Level 2 scene**: add `PageRideFollowers` to `Level2_Newspaper.unity` at the scene root
  (YAML surgery: read the area whole, unique fileIDs, preserve the file's line endings, count
  `--- !u!` blocks before and after, grep fileID uniqueness). Optionally drop the now-redundant
  `m_IsKinematic` override on the Kami instance.
- [x] **2.B** **MainMenu scene**: add `LoadoutSkin` to `kami_spine_titlescreen`, assign
  `Level1_StartLoadout`, set `initialSkinName` to `FullSkins/Kami Libro 1`; ask Diego about the inactive
  Atlas 12 "The Paper Model". Same YAML rules.
- [ ] **2.C** **Check every page's landing point**: log (temporarily) whether
  `NavMesh.SamplePosition(entry point, 5)` succeeds on each of Level 2's five pages at the page-entry
  and page-exit X, then remove the log. A page that fails needs a bake (Diego, in the Editor) before the
  playtest.

## Phase 3 — Docs and playtest handoff (depends on Phase 2)

- [ ] **3.A** Update the docs listed in `spec.md`, mark this spec's status, run
  `/graphify Assets/Scripts --update`.
- [ ] **3.B** Hand Diego the playtest checklist from `spec.md` and the named risks. **Diego plays it;
  tuning (seats, ease, bob, spacing) is his, in the Inspector, live during a turn.**

## Parallelism summary

Phase 1 is four independent units (`PageRideFollowers.cs` / `Player.cs` + `PlayerPageSpawnManager.cs` /
`LoadoutSkin.cs` / `ArrestCutscene.cs` + its prefab): up to 3 agents, no overlap. Phase 2 is sequential and small, one writer for the
scenes. No agent is needed for a spec this small unless Diego asks.

## Verification

`python tools/compile-check.py [tag]` after every code task. That is compilation only. Feel, timing,
the NavMesh landing and the Spine physics are Diego's playtest. Never report "verified": say what ran
and name what did not.
