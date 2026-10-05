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

- **Cameras** track: three shots (`CM Arrest PullBack` / `Escort` / `CarFollow`, children of the
  prefab). Overlap two clips to blend. Framing = each vcam's Transposer `Follow Offset` + its X tilt.
  PullBack/Escort follow Kami (assigned at runtime), CarFollow follows the patrol car.
- **Street (appears)** / **StreetCops (appears)**: when Ariel + the car, and the two street cops, are
  visible. Both switch off when the timeline ends.
- **Markers** row: 3 dialogue markers (`Arrest_Shout`, `Arrest_Accusation`, `Arrest_Cuffs`) and 5
  signals, in order: `CopsBehindGirls` → `StartEscort` → `BoardCar` → `DriveOff` → `TurnPage`.
  **Keep `TurnPage` before the end of the last camera clip** — the timeline's length is its clips, and
  a marker past the end never fires.

The only duration that is not on the timeline is the car's drive (`_driveOffSeconds` on
`ArrestCutscene`, seconds-to-arrive like all traffic). The escort walk has no duration of its own: Kami
walks until `BoardCar` fires; if she hasn't arrived by then, they board anyway (nothing can stall).

## Tuning the catapult ending (Level 2 page 5, 2026-09-28)

`Assets/Timelines/Level2/Timeline_Catapult.playable`, on `Page 5/Museum/Catapult_PLACEHOLDER_POSITION`
(`Catapult` + `CutsceneDirector`). It starts when Kami cuts the rope from the bucket (the boarding
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
`PoliceStationPage.StartSequence`. The Abuela is scheduled once the timeline ends.

## Tuning the page 5 arrival (Phase 6.D)

`Timeline_MuseumArrival.playable`, on the `Museum` GameObject. Markers: `Grace_Reaction` (0.4s),
signals `Arrival_Sirens` (1.2s), `Arrival_CarArrives` (2.2s), `Arrival_WalkUp` (5.0s), then
`Grace_Meeting` (9.6s). The signals call `MuseumPage.CUE_*`. The car (`PoliceCar_ARRIVAL_PLACEHOLDER`)
is moved by `MuseumPage` itself (not `TrafficObstacle.Launch`, which destroys the car on arrival) from
`ArrivalMarks/CarStart` to `CarStop` over `_carArriveSeconds`. Ariel and the cop start hidden and walk from
the car to where they are placed in the scene. Vcams: GroupNear and PullBack follow Kami (set in code by
`MuseumPage`), StreetWide is static.

## Gotchas

- The cutscene lives at the **scene root**, not under Page 3: the page folder is switched off mid
  page-turn, and the wrap-up (cells, quests) runs after that. Its vcams are always on at priority 0,
  so they only go live through the Cameras track and never compete with `CameraManager` (priority 10).
- Cutscene cameras are **not** `CameraMode`s: `CameraManager` owns one vcam per mode and the player
  cycles through the pickable ones (#43 made cycling skip the game-driven `OrigamiCasting`/`ReceiveReward`),
  and `SetCamera` switches off every vcam in its array, which would fight the Timeline's Cameras track.
- The timeline files were written by hand (YAML). If the Timeline window ever shows an empty or broken
  track, rebuild that track in the Editor — the scripts don't depend on how the asset was authored.
