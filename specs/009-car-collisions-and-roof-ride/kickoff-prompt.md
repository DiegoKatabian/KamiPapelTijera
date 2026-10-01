# Kickoff prompt: implement spec 009 (paste this into a fresh session)

```
You are implementing spec 009 of Kami: Papel y Tijera (Unity 2021.3, URP, spine-unity 3.8):
car collisions (front-hit damage + sideways knockback) and riding on a car's roof (moving platform).
Repo: C:\Users\Diego\OneDrive\Documents\GitHub\KamiPapelTijera. Work on the current branch
(level2newspaper-pages-blocking-2 when this was written; check `git status` and tell me if it differs).
Epic #138 (sub-issues #132-#137, milestone M4). Everything here is DESIGNED but NOT started: the
spec was agreed with me on 2026-09-30 and its numbers are already approved.

READ FIRST (in this order, whole files):
1. specs/009-car-collisions-and-roof-ride/spec.md  (the design, FRs, defaults, risks)
2. specs/009-car-collisions-and-roof-ride/tasks.md (phases, which tasks are [P])
3. CLAUDE.md and docs/claude/nivel2-y-ui.md (traffic section), docs/claude/controles-y-gamepad.md
4. Code: Assets/Scripts/Traffic/TrafficObstacle.cs, TrafficSpawner.cs, TrafficObstacleSet.cs;
   Assets/Scripts/Player/Player.cs (TakeDamage, Die, hitFeedback, GetAffectedByWind + windVelocity +
   the wind latch, FeetPosition, IsRidingPage), PlayerModel.cs (ApplyPhysics, ForcedMove's warning,
   UpdateSafePosition), Entity.cs, DeathCause.cs, HitFeedbackConfig.cs;
   Assets/Scripts/Cortables/PoliceOfficerCortable.cs (trigger + kinematic Rigidbody precedent);
   Assets/Prefabs/Traffic/TrafficObstacle_Car.prefab and _Car_Horizontal.prefab;
   Assets/3D/Libros/Diario Kami/Prefabs/Car/Paper Car.prefab (the solid MeshCollider).

SCOPE: issues #132 (0.A contracts) -> #133, #134, #135 (1.A/1.B/1.C, parallel) -> #136 (2.A prefabs)
-> #137 (2.B docs + graph + handoff). One session, in that order.

REUSE, DON'T REBUILD:
- Damage goes through `Player.TakeDamage(dmg, DeathCause.Generic)` (hit stun, flash and Hit anim come
  with it). Do NOT reuse IGolpeable (it hardcodes DeathCause.Rocoso).
- External pushes are a VELOCITY added inside the single `cc.Move` of PlayerModel.ApplyPhysics, next
  to the wind line, exactly like the wind (windVelocity + latch). A second horizontal cc.Move brings
  back the Falling->Landing loop of issue #30 (see the ForcedMove comment). Knockback and moving
  ground both follow this rule.
- The hitbox pattern is PoliceOfficerCortable's: trigger collider + kinematic Rigidbody (two triggers
  only report if one side has a Rigidbody; the CharacterController needs the same).
- Audio: AudioId.CarHorn already exists in the bank. No new sound.

DESIGN RULES ALREADY DECIDED (do not re-litigate):
- Front hit only: damage + lateral knockback + 1 s immunity. Sides and tail = today's solid shove,
  no damage. The frontal test runs at runtime from TrafficObstacle.TravelDirection (the same prefab
  is driven in both directions by different spawners).
- Defaults: damage 25 (Kami has 120 HP, so the 5th hit defeats her), pushDistance 8, pushDuration 0.3,
  immunitySeconds 1, frontFraction 0.35, releaseSecondsBeforeDespawn 0.75, groundNormalMinY 0.6.
  All [SerializeField] + [Tooltip] on the car prefab; nothing hardcoded.
- Roof ride: carry via IMovingGround.GroundVelocity (velocity, never position deltas: Update order
  between car and Kami is not guaranteed). No parenting (page reparenting and RidingPage fight it).
  No momentum inheritance on jump. Release before the despawn point, in SECONDS (scale-independent).
- No snapshot of "last safe position" while on moving ground.
- Followers (Natalia, Abuela) are not carried and take no damage. No new DeathCause (follow-up).
- No new input reads; keyboard and gamepad stay untouched.

GOTCHAS ALREADY PAID FOR:
- A UnityEngine.Object behind an interface is not `== null` safe after Destroy: TrafficObstacle's
  GroundVelocity must read cached fields only, never `transform`.
- Deleting or moving code: list what falls in the range before AND the methods left after. A removed
  Update()/Awake() compiles clean and breaks silently (the NPC.cs lesson).
- Prefabs: YAML surgery, no live Editor. Read each prefab whole, fresh unique fileIDs, preserve its
  line endings (mixed CRLF/LF), count `--- !u!` blocks before/after, grep fileID uniqueness. The Dark
  and Gray variants must inherit the new components (confirm). Scripts' .meta via
  `python tools/make-meta.py` (it writes MonoImporter, correct for scripts only).
- Windows shell: write multi-line Python/JSON with the Write tool, not heredocs; Python text mode
  writes CRLF (open with newline=''); non-ASCII GitHub text goes through `gh api --input file.json`.
- OnControllerColliderHit only fires during a Move: gravity is applied before every Move so it fires
  each frame while grounded. Verify in the code that this still holds when standing on a car.

WORKING RULES:
- Reply to me in ENGLISH. Everything you add (code, comments, tooltips, docs, names) in English.
  Comments explain the non-obvious WHY. Logs as Debug.Log($"[ClassName] ...") at decision points;
  guard clauses with Debug.LogWarning when a reference that should be wired is missing.
- Do the mechanical work yourself (prefab YAML, .meta files, docs). Ask me only about decisions.
- NEVER git commit, push or open a PR unless I say so. Leave the work uncommitted for my review.
- Keep PRs/commits surgical: touch only what the spec lists. No refactors in passing.
- Do NOT close issues #132-#138 until I have played it and say it works. When I do: one status comment
  per phase issue (what was built + any design change vs. the spec and why), close them as completed,
  one comment on the epic.

VERIFICATION (be literal about it):
- `python tools/compile-check.py 009` after every code task: it proves COMPILATION ONLY. Baseline
  warnings: JumpFloodOutlineRenderer CS0162 and HongueroTiburcioDialogueTrigger CS0414.
- Never write "verified" or "works". Say what ran, and name by name what did NOT run: carry stability
  (isGrounded stays true, no Falling/Landing flicker), roof walkability (is the Paper Car roof flat?),
  push feel, hitbox size/position, front-vs-side classification, immunity, the despawn release,
  gamepad. Those are my playtest (the checklist is at the end of the spec).

HOW TO RUN IT:
1. Triangulate first: confirm the spec against the code as it is NOW (line numbers and names may have
   drifted) and report any mismatch before writing code.
2. Ask me any real questions as a short numbered list, EACH with your recommended default, then wait.
   (Probably few or none: the numbers are decided. A real one would be a prefab detail the YAML
   reveals, e.g. where the front of each car art actually is.)
3. Build 0.A yourself, then 1.A/1.B/1.C. You may use up to 3 agents for Phase 1 (one per issue, files
   do not overlap; none writes a scene, there is no scene work in this spec). You do 2.A and 2.B.
4. End with: what changed (files), what compiled, the named untested list, and the playtest checklist.
```
