# Cutscenes (Unity Timeline)

Built for Level 2 page 3's arrest (spec 006, Phase 3, 2026-09-25), but the pieces in
`Assets/Scripts/Cutscenes/` are generic — the next cutscene (e.g. Rocoso's, issue #35) reuses them.

## The pieces

| Piece | What it does |
|---|---|
| `CutsceneDirector` (on the PlayableDirector's GameObject) | `Play()` locks Kami (`LevelManager.inCutscene`), binds every Cinemachine track to the scene's `CinemachineBrain` **in code**, and plays. `Release()` gives Kami back; automatic at the end unless `_releaseControlWhenTimelineEnds` is off. |
| `CutsceneDialogueMarker` (Timeline marker, "Kami/Dialogue") | Holds the timeline still (root playable speed 0, **not** `Pause()`, so the live camera shot and every active clip stay put) until its `DialogueSO` is closed. Timing is only about the gaps between beats, never about reading speed. |
| Unity Signals + `SignalReceiver` | Every other beat. One `.signal` asset per beat; the receiver on the director's GameObject maps it to a method on the scene-specific script (`CUE_*`). |
| `HiddenRenderers` | "Get in the car": hides everything drawn under a GameObject and later restores exactly what it hid. |

`inCutscene` (see `controles-y-gamepad.md`): no movement, jump or attack; Interact only goes through
while a dialogue is on screen (so the player advances lines but can't open a trash can they walk past).
It is separate from `inDialogue` because `DialogueManager` clears that one at the end of **every**
dialogue. A scripted walk goes through `Player.SetCutsceneWalk(worldDirection, stickAmount)`: the same
input path as the stick, so Kami animates and collides normally.

## Tuning the arrest (what Diego touches)

Open `Assets/Timelines/Level2/Timeline_Arrest.playable` with the `ArrestCutscene_PLACEHOLDER_POSITION`
object selected (scene root, Level2_Newspaper). Everything is a drag:

- **Cameras** track: four shots (`CM Arrest PullBack` / `Ariel` / `Escort` / `CarFollow`, children of the
  prefab). Overlap two clips to blend. Framing = each vcam's Transposer `Follow Offset` + its X tilt.
  PullBack/Escort follow Kami (assigned at runtime), Ariel follows Ariel (a prefab reference, he is inside
  it), CarFollow follows the patrol car. PullBack is two clips (0-1.2s and 2.4-3.8s) around the Ariel clip
  (0.7-2.9s, #175): a clip can't sit inside another on the same track. Ariel is fully framed by 1.2s, so the
  whole `Arrest_Accusation` dialogue (Natalia's reply included) holds on him.
- **Street (appears)** / **StreetCops (appears)**: when Ariel + the car, and the two street cops, are
  visible. Both switch off when the timeline ends. StreetCops ends at 3.05s, just after the
  `CopsBehindGirls` signal, so the street cops vanish while the screen is black (below).
- **Markers** row: 3 dialogue markers (`Arrest_Shout`, `Arrest_Accusation`, `Arrest_Cuffs`) and 5
  signals, in order: `CopsBehindGirls` → `StartEscort` → `BoardCar` → `DriveOff` → `TurnPage`.
  **Keep `TurnPage` before the end of the last camera clip** — the timeline's length is its clips, and
  a marker past the end never fires.

**The escort cops appear behind a fade (#174, built 2026-10-13, not playtested).** `CopsBehindGirls` holds the
timeline, fades to black (`Canvas/ScreenFade`, `ScreenFader`, `_screenFader` on `ArrestCutscene`), puts cop 1 behind
Kami and cop 2 behind Natalia (`_copsBehindDistance` away from the car door, each from her own girl), resumes the
timeline while still black (so the StreetCops clip ends unseen), holds black and fades back in. Seconds are the
`Cops appear behind a fade` header on `ArrestCutscene`. The `Arrest_Cuffs` marker (3.4s) fires during the fade-in.

**The arrest cuts the jazz (#176).** When the cutscene starts (about 0.5s before the shout line), `ArrestCutscene`
stops page 3's band (`_bandMusic`, the `Live Music - Alone Together` AudioSource, disabled so it stays off if Kami
walks back to page 3) and raises `Evento.OnArrestStarted`, which starts `PageMusicManager`'s story music (see
`nivel2-y-ui.md`).

**Escort formation (spec 013, not playtested).** From `StartEscort` the cops walk the NavMesh to the car door and Kami
marches along their route, ahead of them, at `_escortWalkInput` (0.3, clamped under `Player.walkThreshold`). Tuning is
the `Escort formation` header on `ArrestCutscene`. No NavMesh route = the old behaviour (cops follow Kami), with a warning.

**The escort walk waits for Kami (2026-10-08).** `BoardCar` holds the timeline (`CutsceneDirector.HoldTimeline`) until
she is within `_arriveDistance` of the door (`_maxWaitForArrivalSeconds`, 40, then they board anyway), so the walk lasts as
long as the street is long and the marker's time no longer has to match it. Her pace is `_escortWalkInput` (0.45, the top
of the walk range: under `Player.walkThreshold` - 0.05). Scripted walks used to play the Skip animation at walking speed
because `PlayerController.ApplyCutsceneMove` sign()ed the raw input; it is analog now, so the stick amount decides
walk vs skip exactly as it does for the player.

**The patrol car sits still until the girls are in (2026-10-08).** The car is the ambient-traffic prefab, whose "Andando"
Animator slides the body down the street by itself, so `ArrestCutscene.ParkCar` turns its Animators off in `Start`: it rests
at its authored pose and `DriveOff` moves it with `TrafficObstacle.Launch` alone. `TurnPage` holds the timeline until the car
has reached `DriveOffTarget` (it destroys itself on arrival), so the CarFollow shot rides the whole drive.

The only duration that is not on the timeline is the car's drive (`_driveOffSeconds` on
`ArrestCutscene`, seconds-to-arrive like all traffic). The escort walk has no duration of its own: Kami
walks until `BoardCar` fires; if she hasn't arrived by then, they board anyway (nothing can stall).

## Tuning the catapult ending (Level 2 page 5, 2026-09-28)

`Assets/Timelines/Level2/Timeline_Catapult.playable`, on the `Gameplay` child of `Catapulta.prefab` (nested in
`Museum Page Parent`, page 5; `Catapult` + `CutsceneDirector`). The `Launch` signal also fires the `Shoot` trigger of the
prefab's Animator: the arm snaps up (`Catapulta_Shoot`, 0.46s, overshoots and settles; tune it in the Animation window). It starts when Kami cuts the rope from the bucket (the boarding
before it is gameplay, see `nivel2-y-ui.md`). One camera shot, `CM Catapult Hold` (child of the
catapult, CarFollow's recipe: Transposer in world space following the catapult, so it holds still;
framing = its `Follow Offset` + X tilt), and three signals: `Launch` (0.6s: the loaded art swaps for
the fired art, Kami and the Abuela vanish and `FlyingGirls_PLACEHOLDER` arcs to `FlightTarget` in
`_flightSeconds`) -> `FadeOut` (3.6s, over `_fadeSeconds`) -> `LoadEnding` (5.4s, loads
`Level2_EndCutscene`). The hold on the empty catapult is the gap between `Launch` and `FadeOut`. Keep
`LoadEnding` before the end of the shot clip (6s). The durations that are not on the timeline live on
`Catapult`: `_flightSeconds`, `_flightArcHeight`, `_fadeSeconds` and `_boardFadeSeconds`.

## Tuning the page 4 intro (Level 2, Phase 6.B, 2026-09-29)

`Assets/Timelines/Level2/Timeline_PoliceIntro.playable`, on the `PoliceStation` GameObject
(`PlayableDirector` + `CutsceneDirector`, started by `PoliceStationPage`, first time only, not
skippable). Cameras track: KamiCell, PatrolRoute, NataliaCell, Window (static vcams under
`PoliceStation/IntroCameras`: drag the vcam itself to reframe), then CopFollow (Transposer following the
patrolling cop) and NataliaCell again. Three dialogue markers: `Jail_CopTaunt` (9.2s), `Jail_NataliaReply`
(12.8s), `Narrator_JailIntro` (13.9s). No signals: the cell-bars sound comes from
`PoliceStationPage.StartSequence`. Once the timeline is over and the Abuela has landed and spoken (Kami is free again) `PoliceStationPage` switches the live camera to `BookCenter` (the whole station in view; `_cameraAfterIntro`, the player cycles or picks as usual afterwards; first time only, a capture restart leaves the camera alone, 2026-10-16, not played). The Abuela is scheduled when the timeline ends.

## Tuning the page 5 meeting (Phase 6.D, reworked by #182 on 2026-10-13)

`Timeline_MuseumArrival.playable`, on `Page 5/Museum Sequence`. No car arrives any more (#182): Ariel, the two cops and
their parked patrol car (`PoliceCar_Parked`) are on the page from the start. The timeline is camera only: GroupNear
(0-3s) -> PullBack (2.6-6s) -> GroupNear (5.6-9s), both following Kami (set in code by `MuseumPage`), with the
dialogue markers `Grace_Reaction` (0.4s) and `Grace_Meeting` (6.4s). It starts when Kami walks within
`MuseumPage._approachRadius` (12, gizmo on `Museum Sequence`) of Grace with the evidence, or talks to her.

## Tuning Ariel's arrest (page 5, #183, 2026-10-13, not playtested)

`Timeline_MuseumArrest.playable`, on `Page 5/Museum Sequence/ArielArrest` (`MuseumArrest` + `CutsceneDirector`), played
by `MuseumPage` when `Museum_Evidence` ends; once it is over `MuseumPage` waits for Kami to be free and plays Grace's
catapult offer (`Grace_CatapultOffer`, #223), and the case closes (`CloseTheCase`) when that dialogue ends. Cameras: `CM MuseumArrest
Ariel` (0-3.8s, follows Ariel) then `CM MuseumArrest CarFollow` (3.2-6.5s, follows the car), both CarFollow's recipe
(Transposer in world space: framing = `Follow Offset` + X tilt). Signals: `StartEscort` (0.3s) -> `BoardCar` (3.0s,
holds until Ariel is at `Marks/CarDoor`) -> `DriveOff` (3.6s, siren on, `TrafficObstacle.Launch` to
`Marks/DriveOffTarget`) -> `Finish` (6.0s, holds until the car has arrived, then hands back to `MuseumPage`).

- **Everyone walks on a NavMeshAgent** (Diego's playtest, 2026-10-13: sliding them along the route's corners floated
  them over the yard's slope). Ariel's prefab has no agent and page 3 shares it, so `MuseumArrest` adds one to him at
  `StartEscort` (`_arielAgentRadius` / `_arielAgentHeight` in world units, base offset = the gap he stands with,
  avoidance priority 30 so the cops give way). The cops use their own CopEscort agents and each one stops while the
  one in front of him is closer than `_copsKeepBack` (4.5). Pace: `_walkSpeed` (7, cops x1.25).
- **All three are warped onto the NavMesh first** (the cops stood still in the first try): an NPC placed in the scene
  wakes up before `PageNavMeshManager` adds its page's NavMesh (in its `Start`), its agent misses it and stays detached
  (`isOnNavMesh` false) until something warps it. Natalia and the Abuela always get a `WarpTo`; page-placed NPCs don't.
  Any cutscene that walks a page's NPCs must warp them first.
- `Museum_Evidence` leaves Ariel in his `Angry` pose, which does not end when he moves and covered the Walk:
  `StartEscort` ends it and makes it his rest pose (`_arielMood`), so he is angry again whenever he stops.
- No NavMesh near Ariel or the door = a warning, and they board where they stand.
- The car never turns (`faceMovementDirection` is off on the traffic prefab), so `DriveOffTarget` sits 80 units ahead
  of the parked car's nose. If the car drives backwards, drag the mark behind it instead.
- The car's "Andando" Animator is switched off in `MuseumArrest.Awake` (same as page 3's `ParkCar`). **Awake, not Start**
  (#220, 2026-10-15): page 5 is switched on by a page turn's coroutine, after Update, so the car's Animator got one pass
  before `Start`; Andando's first frame puts the car 4.2 under the road, 39 down the street and shrunk to ~0.2, and a
  disabled Animator leaves it there. Any script that parks an animated object on a page must do it in `Awake`.
- `ArielArrest` lives under page 5 (unlike page 3's arrest at the scene root): the page never turns away during it.

## Character animation beats (spec 014)

The characters' animations are NOT on the timelines (no Spine Timeline package): beats tied to a line live on that
line of the `DialogueSO` (`Animation Cues`: actor id + PlayPose / EndPose / SetRestPose / ClearRestPose / overlay), so the
cutscene's dialogue markers already time them. Arrest: `Arrest_Shout` (cops `Arrest`, Natalia `Shock`),
`Arrest_Accusation` (Ariel `Point`, his arm aimed at Kami). Meeting: `Grace_Meeting` (Ariel `Point`, cops `Arrest`),
then `Museum_Evidence` (Ariel `Angry`, Natalia `Happy`, cops lose the walking lantern; the page 5 witnesses never hold the idle
lantern since #225: their `_idleOverlay` is empty in the scene). Beats that are not lines are in the
cutscene scripts (`ArrestCutscene.CUE_StartEscort`: shock ends, everyone walks). Detail: `specs/014-level2-npc-animations/spec.md`.

## The HUD during cutscenes (#188)

Nothing on the timeline: the HUD strip fades out whenever `LevelManager.inCutscene` is on (so also during the
non-Timeline locks) and comes back when Kami is free. See "The HUD leaves during cutscenes" in `nivel2-y-ui.md`.

## Gotchas

- The cutscene lives at the **scene root**, not under Page 3: the page folder is switched off mid
  page-turn, and the wrap-up (cells, quests) runs after that. **This already broke once** (#169): an
  Editor commit (46ba596d) parented it under Page 3, the wrap-up's `StartCoroutine` failed on the inactive
  object ("Coroutine couldn't be started"), Quest07 was never added, and page 4's next sphere stayed locked.
  Moved back to the root on 2026-10-13 at the same world position. If you reorganise the hierarchy, keep it
  (and any other cutscene whose wrap-up outlives its page) at the root. Its vcams are always on at priority 0,
  so they only go live through the Cameras track and never compete with `CameraManager` (priority 10).
- Cutscene cameras are **not** `CameraMode`s: `CameraManager` owns one vcam per mode and the player
  cycles through the pickable ones (#43 made cycling skip the game-driven `OrigamiCasting`/`ReceiveReward`),
  and `SetCamera` switches off every vcam in its array, which would fight the Timeline's Cameras track.
- The timeline files were written by hand (YAML). If the Timeline window ever shows an empty or broken
  track, rebuild that track in the Editor — the scripts don't depend on how the asset was authored.
