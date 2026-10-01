# Tasks: Car collisions and roof ride (spec 009)

Phases run in order; `[P]` tasks inside a phase are parallel-safe with their siblings. No task here
touches a scene: the work is scripts and two prefabs (spec 009 FR list in `spec.md`).

## Phase 0 — Contracts (do first, small, unblocks the three parallel tasks)

- [x] **0.A** **Contracts**: `Assets/Scripts/Player/IImpactReceiver.cs` (`ImpactInfo` struct +
  `IImpactReceiver`) and `Assets/Scripts/Traffic/IMovingGround.cs`, exactly as written in the spec.
  `.meta` files with `python tools/make-meta.py`. Compile-check.

## Phase 1 — The three units (all `[P]`, depend only on 0.A)

- [x] **1.A** `[P]` **`TrafficCarHitbox`** (FR-001..005, FR-007): trigger-box damage hitbox with the
  frontal test, roof guard, `ImpactInfo` built from Inspector fields (`damage` 25, `frontFraction`
  0.35, `pushDistance` 8, `pushDuration` 0.3, `immunitySeconds` 1, `hitSound` = `AudioId.CarHorn`),
  `[RequireComponent(TrafficObstacle)]`, warn if the trigger collider or the kinematic Rigidbody is
  missing. Reads `TrafficObstacle.TravelDirection` (added by 1.B: agree the property name from the
  spec, both tasks use it).
- [x] **1.B** `[P]` **Roof ride on `TrafficObstacle`** (FR-006, FR-008): implements `IMovingGround`;
  `GroundVelocity`, `TravelDirection`, `releaseSecondsBeforeDespawn` (0.75). Cached fields only.
  Must not change the existing movement/despawn behaviour (spawner, travel duration, safety nets).
- [x] **1.C** `[P]` **Player side** (FR-002, FR-003 consumer, FR-005, FR-006, FR-009): `Player :
  IImpactReceiver` (`ReceiveImpact`: filters, `TakeDamage`, knockback state, immunity),
  `KnockbackVelocity` (linear decay, integral = `pushDistance`), `OnControllerColliderHit` ground
  tracking with the ~0.1 s latch and `groundNormalMinY` 0.6, `GroundVelocity`, `IsOnMovingGround`;
  `PlayerModel.ApplyPhysics` adds both velocities next to the wind line; `UpdateSafePosition`
  skips while on moving ground. **The only `cc.Move` stays the one in `ApplyPhysics`** (issue #30).

## Phase 2 — Wiring and close-out (depends on Phase 1)

- [x] **2.A** **Prefab wiring**: on `TrafficObstacle_Car.prefab` and `TrafficObstacle_Car_Horizontal.prefab`
  add a trigger `BoxCollider` (low, ends below the roof, first guess from the mesh bounds, a little
  ahead of the solid mesh), a kinematic Rigidbody (gravity off) and `TrafficCarHitbox`. YAML surgery:
  read each file whole, unique fileIDs, preserve its line endings, count `--- !u!` blocks before/after,
  grep fileID uniqueness. The Dark and Gray variants inherit; confirm they show the components.
- [x] **2.B** **Docs + playtest handoff**: update `docs/claude/nivel2-y-ui.md` (traffic section),
  `/graphify Assets/Scripts --update`, mark spec 009 status. Hand Diego the playtest checklist from
  the spec and the named risks list. **Diego plays it**; tuning (box sizes, push, damage) is his, in
  the Inspector.

Status 2026-09-30: all tasks done; built, compile-checked and played by Diego. Design changes vs. this list: see `spec.md`, "Implementation notes".

## Parallelism summary

Phase 0 is one short task. Phase 1 is three independent agents (1.A hitbox, 1.B car side, 1.C player
side), each in its own files (`TrafficCarHitbox.cs` / `TrafficObstacle.cs` / `Player.cs` +
`PlayerModel.cs`), no overlap. Phase 2 is sequential and small. Max 3 agents; there is no scene write.

## Verification

`python tools/compile-check.py [tag]` after every code task (baseline warnings:
`JumpFloodOutlineRenderer` CS0162 and `HongueroTiburcioDialogueTrigger` CS0414). That is compilation
only. Feel, physics, scale and the Falling/Landing stability are Diego's playtest (see `spec.md`,
"Risks"). Never report "verified": say what ran and name what did not.
