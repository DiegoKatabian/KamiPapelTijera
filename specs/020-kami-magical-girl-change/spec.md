# 020 - Kami's magical girl outfit change (Level 2 opening)

Issue #274 (milestone M4). Branch `020-kami-magical-girl-change`. Asked by Diego on 2026-10-26; the art team
is drawing the Spine animation right now, so the code must work before it lands and pick it up with no code change.

## The goal (what Diego sees)

> When I open Unity and play Level 2, Kami spawns a little higher than usual and drops in, wearing her classic
> outfit. As soon as she lands the transformation starts, with everything I asked for, and then I play as usual.

"Everything I asked for":

1. **Animation**: Kami's Spine animation "kami magical girl change outfit" on her body track.
2. **Sound** and **particles** with it.
3. A **white-silver fill** over an interval of time; in the **middle** of that interval her Spine skin changes from
   the classic outfit (`FullSkins/Kami Libro 1` + `Zapatos/Zapatos_Base`) to the detective one
   (`FullSkins/Kami Diario` + `Zapatos/Zapatos_Detective`).
4. A **series of camera cuts** that give her the spotlight, using the cameras the level already has.
5. Very magical girl: snappy cuts, a sparkle aura, a white flash at the peak, a sparkle burst at the change.

Then control comes back and Level 2 plays exactly as before (detective outfit, normal scissors, page 1 untouched).

## Decisions (taken here, Diego can overrule any of them in the Inspector)

| # | Decision | Why |
|---|---|---|
| D1 | One self-contained prefab, `Prefabs/Level2/MagicalGirlChange.prefab`, with the component `Level2/MagicalGirlChange` and its two sparkle children, placed once at Level 2's scene root. | Author in the prefab, not at runtime; the scene only gets one instance. |
| D2 | Kami is lifted by `_dropHeight` (6 units) at level start by the component, not by moving her in the scene. | Her scene position stays the respawn / last safe point; the lift is undone by gravity. |
| D3 | Runs only when the scene starts on page 1 (`PageScrollerManager.startingPage == _pageNumber`). Test starts on pages 2-5 skip it and keep the loadout's detective look. | Test starts already fake the earlier pages. |
| D4 | The animation name is an Inspector field, `_animationName` = **`MagicalGirlChangeOutfit`**. If Kami's skeleton has no animation by that name, `_placeholderAnimation` (`Casting`, looped) plays for `_placeholderSeconds` (4) with one warning. | Valen's animation is not exported yet. Name it `MagicalGirlChangeOutfit` in Spine, or type the real name in the field. |
| D5 | Every beat is placed in **normalized time** (0 = the animation starts, 1 = it ends): sound, camera cuts, fill interval, burst. | When the real animation lands with a different length everything rescales by itself. |
| D6 | Fill: colour `#E6ECF5` (white-silver), from `_fillStart` 0.3 to `_fillEnd` 0.7, shaped by `_fillCurve` (rise, hold at full white around the middle, fade). The skin swap is at the middle of that interval, under full white. Uses the same `_FillColor`/`_FillPhase` property block as the hit flash (`Player.SetSkinFill`). | Diego: "a fill over an interval, the skin change happens in the middle". The swap hides under the white. |
| D7 | Cameras: the existing `CameraManager` modes. Default cuts: CloseUp at 0, OrigamiCasting (left, close) at 0.25, ReceiveReward (right, close) at 0.5, CloseUp at 0.78, then Normal after the end. While it runs the brain's default blend is shortened to `_cutBlendSeconds` (0.35 s, ease in-out); the original blend is restored before the final cut back to Normal, so the return is the usual smooth 1.5 s. | "Use the cameras already available"; magical girl sequences cut fast. |
| D8 | Sound: new AudioBank row `KamiMagicalChange` (SFX bus, 2D), placeholder clip = the `MagicSuccess` clip at pitch 1.15, played at `_soundAt` 0. Swapping the clip is a bank edit. | A real transformation sound is pending (issue checklist). |
| D9 | Particles: a `Brillitos Mágicos` aura (looping, scale 3) for the whole beat and a `BrillitosSolapa` burst (one-shot, scale 5) at the swap. Both are moved to Kami's body centre when the beat starts (`_particleOffset` nudges them toward the camera). | Reuses the project's own magic sparkles. |
| D10 | Kami is locked with `LevelManager.inCutscene` from the lift until the end (so the HUD and camera wheel fade like in every cutscene, and no input moves her mid-air). | Same lock every cutscene uses. |

## Flow (`MagicalGirlChange`, `[DefaultExecutionOrder(100)]` so its `Start` runs after `Player.Start`)

1. `Start`: page check (D3). Lock (D10). Equip the classic look (`_classicLook`: `Gear_OutfitDefault`, `Gear_ShoesBase`).
   Lift Kami `_dropHeight` with her `CharacterController` off, then `ResetSkeletonPhysicsMemory()` and
   `TriggerScript.ExitAll()` (the teleport rules in `spine-kami.md` and `controles-y-gamepad.md`).
2. Wait for the landing: at least `_minAirSeconds`, then `cc.isGrounded` and state `Idle` (timeout `_maxWaitForLanding`, then go anyway).
   Also wait until `CameraManager`'s level-start cut to Normal has happened (`LevelStartDelayTime`), so it can't cut over the first shot.
3. Play: shorten the blend, place and start the aura, play the animation (D4), then each frame fire whatever normalized beat was crossed:
   sound, camera cuts, fill value, and at the swap point the detective look (`Player.StartingLoadout.Equipped`) + the burst.
4. End: restore the blend, stop the aura, fill 0, `Player.ResumeBodyAnimation()` (back to the state's own animation), `SetCamera(Normal)`, unlock.
5. If the object is disabled mid-way (scene unload, someone switching it off) it finishes at once: detective look, fill 0, unlock.

## Out of scope

- The Spine animation itself (Valen), the real sound (Diego).
- Spine events inside the animation driving the swap: if Valen adds one later, the normalized `_swapAt` can be replaced by it.

## Verification loop (how this was built)

One coordinator (Opus) implements; one verifier subagent (Sonnet, read-only) checks the build against the goal and
the checklist below, reports pass/fail per line with file:line evidence, and the coordinator fixes and re-runs it until
every line passes. At most two agents at a time, only the coordinator above Sonnet.

Checklist (the loop's exit criteria):

- [x] V1 `python tools/compile-check.py` is clean (only the two baseline warnings).
- [x] V2 The prefab instance is in `Level2_Newspaper.unity`, at the scene root, and its YAML is consistent (unique fileIDs, GUIDs resolve to real files, nested-instance ids follow the XOR rule).
- [x] V3 Kami is lifted at start, only on a page 1 start, and her respawn / last safe position stays the ground spot.
- [x] V4 She drops in wearing `FullSkins/Kami Libro 1` + `Zapatos/Zapatos_Base` (skins that exist in the active Atlas 14 export) and ends in the starting loadout's look.
- [x] V5 The sequence starts only after she has landed and after the level-start camera cut.
- [x] V6 Animation by name with the placeholder fallback; Kami returns to her normal body animation after.
- [x] V7 Sound row + `AudioId` constant exist and match; it plays.
- [x] V8 Aura during, burst at the swap, both at Kami.
- [x] V9 Fill: white-silver, over an interval, peak around the swap, back to 0 at the end; does not break the hit flash.
- [x] V10 Camera cuts use existing `CameraManager` modes; blend restored; ends on Normal; the player's camera cycling works after.
- [x] V11 Kami is locked during the whole beat and fully free after (`inCutscene` false); nothing can leave it stuck (timeouts, OnDisable).
- [x] V12 Level 1 and Level 2 test starts on pages 2-5 are unaffected.
- [x] V13 Docs updated (`CLAUDE.md`, `nivel2-y-ui.md` or `cutscenes.md`, `audio-y-particulas.md`).

What the loop cannot verify: anything at runtime (the feel, the framing of each camera, particle sizes, the fill's look on
the real material). Those are Diego's playtest, tuned in the prefab's Inspector.

## As built

Built 2026-10-26 on branch `020-kami-magical-girl-change` (928230a3). **Played by Diego on 2026-10-27: works as intended; #274 closed.** The placeholder `Casting` stays until Valen's `MagicalGirlChangeOutfit` is in the export.

- Code: `Assets/Scripts/Level2/MagicalGirlChange.cs` (new); `Player.SetSkinFill` and `Player.ResumeBodyAnimation` (new,
  additive); `CameraManager.LevelStartDelayTime` (new getter).
- Prefab: `Assets/Prefabs/Level2/MagicalGirlChange.prefab` (hand-written YAML: the component plus nested `Brillitos Mágicos`
  as `Sparkles Aura`, scale 3, emission 25/s, no play on awake, and `BrillitosSolapa` as `Sparkles Burst`, scale 5).
  One instance at the root of `Level2_Newspaper.unity`.
- Sound: AudioBank row `KamiMagicalChange` + its `AudioId` constant (added by hand in the generator's format, at the end).
- Loop: one iteration. The Sonnet verifier passed V1-V13 with 4 low findings. Two were fixed: `OnDisable` no longer finishes
  while the scene unloads, and `Run` checks `LevelManager`. Two were left as they are: the player can still cycle the camera
  during the beat (the next cut or the end puts it back), and the 0.05 s margin over the level-start camera cut (the landing
  wait of about 0.6 s makes it moot).
- Expected timing with the placeholder: about 0.6 s of falling and landing, then a 4 s beat starting at about 1.05 s
  (after the level-start camera cut).

**Diego's playtest checklist**: Level 2 started on page 1. Check that she drops in wearing Libro 1, the cuts frame her well
(tune `_shots`), the sparkles are the right size (the nested instances' scale), the white peak hides the swap, and she walks
off in Diario. Then a page 2 start: no transformation. When Valen's animation lands, name it `MagicalGirlChangeOutfit` (or
type its name in `_animationName`). The beat then lasts as long as the animation, with nothing to retime.
