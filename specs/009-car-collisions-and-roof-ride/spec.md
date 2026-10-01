# Spec 009: Car collisions (damage + knockback) and riding on a car's roof

Status: DONE 2026-09-30 (built, compile-checked, played and confirmed by Diego; see "Implementation notes"). Designed with Diego the same day. Level 2 (`Level2_Newspaper.unity`), ambient traffic.
Tasks: `tasks.md`. Kickoff prompt for the implementation session: `kickoff-prompt.md`.

## Request (Diego, 2026-09-30)

1. Being hit **head-on** by a car should hurt Kami (HP) and push her a little **to the side**, so a
   crash costs HP *and* takes her out of the car's path.
2. When Kami stands on a car's **roof**, the car should carry her along, like a moving platform.
3. Simple, solid (SOLID), reuse what exists, and everything tunable from the Inspector (damage,
   hitbox, push, ...).

## Current state (read from the code, 2026-09-30)

- Cars are `TrafficObstacle` (`Assets/Scripts/Traffic/`): they move by writing `transform.position`
  (`Vector3.MoveTowards`) toward a despawn point and destroy themselves on arrival. Tuning is
  travel **duration** (seconds to cross), never units/second (see `docs/claude/nivel2-y-ui.md`).
- Prefabs: `TrafficObstacle_Car.prefab` and `TrafficObstacle_Car_Horizontal.prefab` wrap the art
  prefab `Paper Car Prefab aniamted` (-> `Paper Car`); `_Car Dark Variant` and `_Car Gray Variant` are
  prefab variants of `_Car`. The car art has a **solid, convex `MeshCollider`** and **no Rigidbody**.
  `TrafficObstacle.faceMovementDirection` is `false` on these prefabs (the art is baked facing one way).
- Why Kami is "pushed by force" today: her `CharacterController` is depenetrated out of that solid
  collider as the car drives into her. There is no damage code anywhere on this path.
- `Player : Entity` already has `TakeDamage(dmg, DeathCause)` (HP, hit stun, red flash, Hit anim, via
  `hitFeedback`), `Die(cause)` and `DeathCause.Generic`. **Kami has 120 HP and walks at 20 u/s**
  (`Kami.prefab`: `_hp: 120`, `_maxSpeed: 20`, sprint x1.5). HP only comes back on respawn.
- `IGolpeable.GetGolpeado` is NOT reusable: it hardcodes `DeathCause.Rocoso` and Rocoso's animation.
- `PlayerModel.ApplyPhysics` has exactly one `cc.Move`, and already adds the wind's external velocity
  inside it. The wind comment (`Player.GetAffectedByWind`) and `PlayerModel.ForcedMove` explain why:
  **a horizontal `cc.Move` outside that call leaves `isGrounded` false and brings back the false
  Falling->Landing loop of issue #30. External pushes must be a velocity added in that one move.**
- `Player` and its `CharacterController` live on the same GameObject (`Kami.prefab`), so
  `Player.OnControllerColliderHit` works. Gravity is applied before every `Move`, so a grounded Kami
  produces a controller hit against the floor every frame.
- There is **no moving-platform system** in the repo.
- Precedent for a trigger + kinematic Rigidbody hitbox: `PoliceOfficerCortable` (two triggers only
  report a hit if one side has a Rigidbody; a character controller needs the same).

## Functional requirements

- **FR-001** A car hits Kami only when she meets its **front**. Contact with the sides or the tail keeps
  today's behaviour: the solid collider pushes her, no damage.
- **FR-002** A front hit applies `damage`, a lateral **knockback** and a short **immunity window**.
- **FR-003** Knockback direction: perpendicular to the car's travel direction (XZ plane), toward the
  side of the car's axis where Kami was. If she is within a small epsilon of the axis, push to the
  side opposite her last movement (`lastDirection`), else +lateral.
- **FR-004** Everything a designer tunes lives on the car prefab's Inspector: damage, hitbox
  (a trigger `BoxCollider`, edited with the collider gizmo), `frontFraction`, push distance, push
  duration, immunity, hit sound. Nothing hardcoded in C#.
- **FR-005** `Player.ReceiveImpact` ignores the hit when Kami is dead, in a cutscene
  (`LevelManager.inCutscene`), riding a page (`IsRidingPage`) or still immune.
- **FR-006** A car whose roof Kami stands on carries her horizontally at the car's own velocity.
  Kami keeps full control: she can walk on the roof, jump off, attack.
- **FR-007** Standing on (or landing on) a car's roof never triggers its damage hitbox.
- **FR-008** A car stops carrying Kami `releaseSecondsBeforeDespawn` seconds before it reaches its
  despawn point; the car then slides out from under her and she falls to the road. Scale-independent
  (seconds, not units), same lesson as the spawner's travel duration.
- **FR-009** No "last safe position" snapshot is taken while Kami is on moving ground (a moving car
  is not a place to respawn).
- **FR-010** No new input reads. Keyboard and gamepad are unaffected (`controles-y-gamepad.md` rule).

## Design

Four small units, each with one reason to change.

### 1. Contracts (tiny, defined first so the rest can run in parallel)

`Assets/Scripts/Player/IImpactReceiver.cs`:

```csharp
public struct ImpactInfo
{
    public float damage;
    public Vector3 pushDirection;   // unit vector, XZ plane
    public float pushDistance;      // total displacement of the knockback, in units
    public float pushDuration;      // seconds the knockback lasts
    public float immunitySeconds;   // no further impacts for this long
    public DeathCause cause;
}

public interface IImpactReceiver
{
    void ReceiveImpact(ImpactInfo impact);
}
```

`Assets/Scripts/Traffic/IMovingGround.cs`:

```csharp
public interface IMovingGround
{
    // World-space velocity of the surface, in units/second. Zero when it should not carry anyone.
    Vector3 GroundVelocity { get; }
}
```

Why interfaces and not `Player` directly: the car knows nothing about Kami and Kami knows nothing about
cars. A pedestrian, a future boat, or an NPC reuses either side without edits (Rocoso's `IGolpeable`
precedent). One implementer each today; that is accepted, it is the seam, not speculation.

### 2. `TrafficCarHitbox` (new, `Assets/Scripts/Traffic/`) — the damage side

On the car prefab root, next to `TrafficObstacle`. `[RequireComponent(typeof(TrafficObstacle))]`.
Needs a trigger `BoxCollider` and a **kinematic Rigidbody (no gravity)** on the same GameObject
(`Reset`/`Awake` guard: warn if missing, never add silently at runtime).

- `OnTriggerEnter(Collider other)`: `other.GetComponentInParent<IImpactReceiver>()`; null = ignore.
- Frontal test: project Kami's position on the travel direction (`TrafficObstacle.TravelDirection`,
  new, always the normalized direction regardless of release). With the box's half-length along that
  direction, Kami must lie in the front `frontFraction` of the box (default 0.35). Doing the test at
  runtime from the travel direction (instead of trusting where the box sits) keeps it correct when the
  same prefab is driven in either direction by different spawners.
- Roof guard (FR-007): skip when `other.bounds.min.y` (the bottom of Kami's controller capsule, i.e.
  her feet) is at or above the hitbox's `bounds.max.y`. It needs no new member on the receiver.
- Builds `ImpactInfo` from its own fields and calls `ReceiveImpact`. Plays `hitSound` (an `AudioId`
  string field, default `AudioId.CarHorn`, empty = none) through `AudioManager`.
- Inspector fields (all `[SerializeField]` + `[Tooltip]`): `damage` (25), `frontFraction` (0.35),
  `pushDistance` (8), `pushDuration` (0.3), `immunitySeconds` (1), `hitSound`.
- Box authoring: low, ending **below the roof**, covering the bumper and a little ahead of the solid
  mesh so the damage fires **before** the physical shove.
- Debug: `Debug.Log($"[TrafficCarHitbox] ...")` on every accepted/ignored hit with the reason.

### 3. Roof ride on `TrafficObstacle` — the carrying side

`TrafficObstacle` implements `IMovingGround`:
- `GroundVelocity` = `TravelDirection * _speed` while moving, XZ only (Y = 0), **zero** when the
  remaining distance / `_speed` is below `releaseSecondsBeforeDespawn` (new field, default 0.75) or
  when it is not moving.
- `TravelDirection` (new public property): the normalized XZ direction toward `_targetPosition`.
- Reads only cached fields, never `transform`, so a late read from a car that was just destroyed
  cannot throw a MissingReference.

### 4. `Player` side (`Player.cs`, `PlayerModel.cs`)

- `Player : IImpactReceiver`. `ReceiveImpact(ImpactInfo)`: filters (FR-005), then
  `TakeDamage(impact.damage, impact.cause)`, stores the knockback (direction, start time, distance,
  duration) and sets `_impactImmuneUntil`.
- **Knockback is an external velocity**, never a separate `cc.Move`. Over `pushDuration` the speed
  decays linearly from `2 * pushDistance / pushDuration` to 0 (so the integral is exactly
  `pushDistance`). Exposed as `Player.KnockbackVelocity` (zero when finished).
- **Ground tracking**: `OnControllerColliderHit(ControllerColliderHit hit)`: if
  `hit.normal.y >= groundNormalMinY` (serialized, default 0.6, so walls do not count) and
  `hit.collider.GetComponentInParent<IMovingGround>()` is not null, remember it with a latch of about
  0.1 s (the wind latch pattern: `_windLatchExpiry`). `Player.GroundVelocity` = that surface's
  velocity while the latch is alive, else zero. `Player.IsOnMovingGround` for the other guards.
- `PlayerModel.ApplyPhysics`: right next to the wind line, before `_move.y = _verticalVelocity`:
  `_move += _player.KnockbackVelocity + _player.GroundVelocity;`. Nothing else in the model changes.
- `PlayerModel.UpdateSafePosition`: do not snapshot while `_player.IsOnMovingGround` (FR-009).
- Jumping off does NOT inherit the car's momentum (the latch expires), the predictable arcade
  default. If it feels too stiff in play, keeping the last ground velocity for the duration of the
  jump is a one-line follow-up, not part of this spec.

### 5. Prefab wiring

On `TrafficObstacle_Car.prefab` and `TrafficObstacle_Car_Horizontal.prefab` roots (the Dark and Gray
variants inherit): add a trigger `BoxCollider`, a kinematic `Rigidbody` (gravity off), and
`TrafficCarHitbox`. Pure YAML surgery (no live Editor from here, see the repo CLAUDE.md): read the
whole prefab first, fresh unique fileIDs, verify block counts before/after and fileID uniqueness.
Box sizes are first guesses from the mesh bounds; Diego fine-tunes them with the gizmo.

Bonus: a kinematic Rigidbody on the root turns the car's solid child collider into part of a proper
kinematic compound body, which is the right way to move colliders (today it is a "static" collider
moved by hand every frame).

## Defaults (corrected for Kami's real numbers)

| Value | Default | Notes |
|---|---|---|
| `damage` | 25 | Kami has 120 HP: the 5th hit defeats her (4 hits leave 20). Diego approved 25. |
| `pushDistance` | 8 units | Kami walks 20 u/s, so ~0.4 s of walking. First guess: tune by feel. |
| `pushDuration` | 0.3 s | Peak push speed = 2 x 8 / 0.3 = ~53 u/s, decaying to 0. |
| `immunitySeconds` | 1 s | Stops double hits (same car, or two cars in a row). |
| `frontFraction` | 0.35 | Front 35 % of the box along the travel direction. |
| `releaseSecondsBeforeDespawn` | 0.75 s | Scale-independent. |
| `groundNormalMinY` | 0.6 | Ground tracking ignores wall-like contacts. |
| `hitSound` | `AudioId.CarHorn` | Already in the bank. |
| Death cause | `Generic` | Uses the existing `DefeatGeneric` text. A `RunOver` cause needs an enum value + `UITexts` in 3 languages: separate follow-up, not in this spec. |

## Risks and what only a playtest can answer (name these, do not paper over them)

1. **Carry stability**: does `isGrounded` stay true while carried (no Falling/Landing flicker, no
   footstep spam, issue #30)? The design mirrors the wind fix, but it is not proven until played.
2. **Roof shape**: the `Paper Car` mesh may not have a flat roof. A sloped or domed roof may not read
   as standable (slope limit 45, step offset 2 on Kami's controller).
3. **Carry speed**: a car crosses its segment in 3-4 s, so on a long street it moves at about Kami's
   own top speed (20+ u/s). She cannot out-walk it at default; she is a passenger. That is probably
   fine and fun, but it is a feel call.
4. **Despawn point vs. the floor**: if a despawn point sits where the page has no walkable ground,
   Kami would drop there after the release. Check each street's despawn point while playing.
5. **Hit before shove**: the damage must fire before the solid collider has already shoved her. If it
   does not, grow the box forward of the mesh a few units.
6. **Edge: landing on a car's front from a jump with feet below the box top** still counts as a hit.
   Acceptable; tune the box height if it bothers.
7. Controller callbacks while standing on a moving collider: the downward gravity component should
   keep `OnControllerColliderHit` firing every frame; verify it does.

## Out of scope

- Pedestrians, boats, or NPCs hurt by cars. Natalia and the Abuela (NavMesh followers) are NOT
  carried by cars and take no damage.
- A dedicated `RunOver` death cause/text, hit particles, a new hit sound.
- Momentum inheritance on jump, carrying anything other than Kami.
- Level 1 (it has no traffic).

## Verification

- `python tools/compile-check.py 009`: compilation only.
- **Not verified by the compile-check, must be played by Diego** (the list above): carry stability,
  roof walkability, push distance feel, hitbox size/position, front-vs-side classification, immunity,
  the despawn release, keyboard AND gamepad.
- Playtest checklist: (1) walk into a car's bumper, lose 25 HP and get pushed sideways out of the
  lane; (2) walk into its side, no damage, shoved as today; (3) get hit twice within 1 s, only one
  counts; (4) jump onto the roof from the sidewalk side, get carried; (5) walk and jump on the roof,
  control feels normal, no damage while on top; (6) ride until the release, slide off cleanly;
  (7) 5 front hits in a row, the 5th defeats Kami and respawns normally; (8) repeat with a gamepad.

## Implementation notes (design changes vs. this draft, and why)

Found while triangulating against the code, approved by Diego (2026-09-30):

1. **The car art carries its own looping Animator** (`Paper Car`'s "Andando", 4.77 s) that slides the
   solid collider relative to the `TrafficObstacle` root by ~17 u/s along local Z and scales it from 0
   to 1 at spawn. So `GroundVelocity = TravelDirection * _speed` and a hitbox on the root would both
   be wrong (collider ~35-40 u/s on a vertical street; the box up to ~39 units off the car).
   - `TrafficObstacle` **measures** the solid collider's velocity in `LateUpdate` (after the Animator)
     and caches it; `GroundVelocity` and `TravelDirection` come from that cache (still a velocity,
     still cached-fields-only). New optional `motionSource` field (default: first non-trigger collider).
   - `TrafficCarHitbox` lives on a `CarHitbox` child of the `Mesh` object (the solid collider's
     transform), so it follows the animated collider. No `[RequireComponent(TrafficObstacle)]`: it
     finds it with `GetComponentInParent` and warns if missing.
2. **Box fit**: the FBX could not be measured reliably from here (node translation, the importer's X
   mirroring, prefab overrides), so a hand-authored box could be tens of units off. The hitbox fits
   itself to the mesh bounds at spawn (`fitToSolidCollider`, `heightFraction` 0.6, `padding` 1); turn it
   off to author the box with the gizmo. The prefab's `BoxCollider` values are placeholders.
3. **Knockback side** at the car's axis uses the victim's `CharacterController.velocity` instead of
   `Player.lastDirection` (the hitbox only sees `IImpactReceiver`).
4. `PlayerModel`: `lastDirection` is now input-only (external velocities subtracted) so the paper
   plane glide does not inherit a car's speed.
5. `ReceiveImpact` skips the knockback when the hit killed her.
6. The art's two car sizes differ: the vertical car is the 0.015-scaled art (~15 long, 7.5 wide, 6 tall),
   the Horizontal one the raw 0.01 `Paper Car` (about 2/3 of that).

## Docs to update when done (living-docs rule)

`docs/claude/nivel2-y-ui.md` (traffic section: the hitbox, the roof ride, the new Player velocity
terms), and the graph (`/graphify Assets/Scripts --update`: new classes and a new dependency
Player <-> Traffic through interfaces).
