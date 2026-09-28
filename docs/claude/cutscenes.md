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

## Gotchas

- The cutscene lives at the **scene root**, not under Page 3: the page folder is switched off mid
  page-turn, and the wrap-up (cells, quests) runs after that. Its vcams are always on at priority 0,
  so they only go live through the Cameras track and never compete with `CameraManager` (priority 10).
- Cutscene cameras are **not** `CameraMode`s: `CameraManager.ToggleNextCamera` cycles its whole array,
  so a cutscene camera there would show up in the camera wheel (same problem as #43).
- The timeline files were written by hand (YAML). If the Timeline window ever shows an empty or broken
  track, rebuild that track in the Editor — the scripts don't depend on how the asset was authored.
