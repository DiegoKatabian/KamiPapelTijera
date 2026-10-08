# Spec 013: Arrest escort, the cops march the girls to the patrol car (Level 2 page 3)

Status: BUILT 2026-10-08 in `ArrestCutscene` (first pass, compile-checked, NOT playtested). Diego asked for it after
playing spec 012 and accepted the open questions' defaults: the cops just walk close behind (no shove), the existing
cameras stay, and `BoardCar` is the safety net. Also asked: Kami's escort speed lower (`_escortWalkInput` 0.45 -> 0.3).
What was built: with `_copsLeadTheEscort` on, `CUE_StartEscort` asks for a NavMesh route from the lead cop to the car
door; the cops walk it at Kami's escort pace x `_copsSpeedFactor`, stopping `_copsKeepBack` behind her; Kami is aimed
`_girlsLookAhead` further along THEIR route (never turning back toward it) and waits when she gets more than
`_maxGirlsAhead` ahead of the lead cop. Natalia keeps the normal follow (she trails Kami, so she is ahead of the cops
too). No route (page 3 not baked near the door) = the old behaviour, with a warning. Not done: a visible shove, a
second cop flanking by design (both just walk to the door and hold back from Kami).
Separate from spec 012 on purpose (different file, different risk). The one-number part of the same
complaint, "Kami skips instead of walking", is task **1.D of spec 012** (#161). GitHub: #167.

## Request (Diego, 2026-10-08)

During the arrest (`ArrestCutscene`, page 3), the escort should read as the police taking the girls away:
1. The cops **walk toward the patrol car on the NavMesh**.
2. Kami and Natalia are **marched along by them**, forcefully, **in front of the cops** (girls in front,
   cops behind). Today it is the reverse: Kami walks a scripted line to the car door and Natalia and the
   cops follow Kami through the normal NPC follow state.

## Decisions

- Formation: **girls in front, cops behind** (Diego, 2026-10-08).
- Own spec/issue (Diego, recommended and accepted).

## Current state (read from the code, 2026-10-08)

- `ArrestCutscene.CUE_StartEscort` sets `_escorting`; `Update` calls `Player.SetCutsceneWalk(dirToDoor,
  _escortWalkInput)` every frame, a straight line to `_carDoorMark`, and starts `StartFollowingPlayer()` on
  Natalia and the escort cops (`NPC_FollowPlayerState` = path toward Kami with `followStoppingDistance`).
  `CUE_BoardCar` stops the walk and the follows and hides everyone.
- Kami has no NavMeshAgent: she is a `CharacterController` moved by `PlayerModel.ApplyPhysics`, fed in a
  cutscene through `Player.SetCutsceneWalk` (same input path as the stick; below `walkThreshold` 0.5 she
  walks, at or above she skips).
- Cops are `CopEscort.prefab` (`NPC`, tinted placeholder sprite, agent speed `_maxSpeed` 15,
  `followStoppingDistance` 5). Natalia is the one `Natalia.prefab` NPC.
- All five Level 2 pages have a baked NavMesh asset registered in `PageNavMeshManager`, page 3 included.
- The timeline (`Timeline_Arrest`) has the beats `CopsBehindGirls` -> `StartEscort` -> `BoardCar` ->
  `DriveOff` -> `TurnPage`. `CUE_BoardCar` fires at a fixed time: if the group hasn't arrived, everyone
  boards anyway (nothing can stall).

## Direction (to be designed)

- The **cops are the source of truth**: a lead cop gets a NavMesh path to the car door (`NavMeshAgent`,
  walking speed). The other cop trails or flanks.
- The girls' targets are points **ahead of the cops along that same path** (a fixed distance in front of the
  lead cop's progress, Natalia a bit further, so Kami and Natalia are side by side or in a short line).
  Kami is driven by `SetCutsceneWalk` toward her target point at a stick amount below `walkThreshold`;
  Natalia is an agent heading for hers.
- Speeds are tied together: the girls never get further than a tunable distance from the cops, and slow
  down when they would; if a girl is stuck, the cops wait.
- A fallback to the current straight-line walk if page 3's NavMesh gives no path to the door.
- Everything tunable on `ArrestCutscene` (distances, speed, spacing) or on the timeline, like the rest.

## Open questions (ask Diego before building)

1. Walking speed of the whole group: Kami's walk at `walkThreshold` (about 45% of her max) or a bit slower?
2. Should the cops visibly *push* (a placeholder shove) or just walk close behind? Default: just walk close.
3. What if Kami or Natalia is not where the escort expects (a trash can, the gift box on the way)? Default:
   the `CharacterController` and agent avoid; the timeline signal `BoardCar` is the safety net.
4. Camera: do the existing shots (`PullBack`, `Escort`, `CarFollow`) still frame a group that moves along a
   curve? Default: keep them and let Diego retime.

## Risks (name these)

- Driving Kami along a path by script is new: her `CharacterController` can get stuck on geometry the
  NavMesh allows. The `BoardCar` signal is the only safety net today.
- Feel only a playtest answers: spacing, speed and timing against `Timeline_Arrest`.
- Compile-check is not runtime.

## Out of scope

- The walk-vs-skip fix (spec 012, #161).
- New art or animations for being escorted.
