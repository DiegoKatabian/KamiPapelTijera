# 019 - Review list "21 de octubre" (Diego + Valen)

Diego's and Valen's revision list from 2026-10-21, run by one coordinating session with at most two
agents at a time. Status: **built 2026-10-21, compile-checked, not played** (see "As built"). Nothing is committed: Diego
plays, checks the checklist and commits himself. Everything new is in English.

**Models by priority (Diego):** must have = Opus, should have / nice to have = Sonnet, read-only = Haiku.
**Scene ownership:** one writer per scene at any time. Valen's `aa7dddfc` + `688b7446` are pulled; Diego
declared `Level2_Newspaper.unity`, `MainMenu.unity` and both end-cutscene scenes free to edit.

## Decisions taken with Diego (2026-10-21)

1. Abuela fall fields (#255): fold back into the original names `_dropHeight` / `_fallSeconds` (scene
   values 60 / 2.7); `_offscreenFactor` stays (a real new knob).
2. `PoliceHorn` at drive-off stays on pages 2, 3 and 5.
3. Level flow: Level 1 -> `Nivel1_EndCutscene` -> main menu. Picking Level 2 in the menu plays
   `Nivel1_EndCutscene` -> Level 2. Level 2 -> `Level2_EndCutscene` -> main menu. No saves.
4. Origami fail (Diego B + Valen): the origami stays open, the sheet flashes red, the failed fold
   restarts, and a red message in the outlined cutscene font floats up from the sheet's centre and fades
   in 1.5 s. Text: "¡Doblaste mal! ¡Intentá de nuevo!" / "Wrong fold! Try again!" / PT.

## Items

| Id | Item | Priority | Lane |
|---|---|---|---|
| A | Jump tooltip: "super jump" ALL CAPS, a more saturated blue than "jump" | must | coordinator (Opus) |
| D | Page 5 ticket unfold: Kami frozen between a cancel and the automatic re-prompt | must | coordinator (Opus) |
| E | Main menu level select (Level 1 / Level 2, screenshot + Kami in the level's outfit + localized name) | must | menu lane (Opus) |
| C | Page 4: camera back to Normal when Kami lands outside the police station | should | Level 2 lane (Sonnet) |
| F | Continue #254 (same item in several bag slots) | should | coordinator |
| G | Report pending issues | should | coordinator |
| H | Level 2 SFX: #255 Abuela fields, #256 patrol car sequence, #257 cop whistle | should | Level 2 lane (Sonnet) |
| V4 | Page 2 cops leave: shorter car drive (to the end of the newspaper), siren fades out | should | Level 2 lane (Sonnet) |
| V1 | Origami fail stays open, red flash, restart, floating red message (merged with B) | should | general lane (Sonnet) |
| I | "Fold 1/1" text lower under the origami | should | general lane (Sonnet) |
| H2 | Level 2 end cutscene: black background, "continuará" only after the dialogue | nice | menu lane |
| B | (merged into V1) | nice | - |
| V2 | Animated dialogue globe on NPCs (Globo Dialogo 0-3, pop on 0, ~3 s loop) | nice | general lane (Sonnet) |
| V3 | Page 1: "Natalia, detective privada" instead of "mi nombre es Natalia" (ES/EN/PT) | nice | Level 2 nice lane (Sonnet) |
| V5 | Page 2: after the "bring the clues home" line Natalia appears at the page's end, waves, camera on her, back to Kami | nice | Level 2 nice lane (Sonnet) |
| V6 | Page 3 arrest: fade to black until Kami is in her cell, then pan to Natalia waving | nice | Level 2 nice lane (Sonnet) |
| BLD | Local build menu item: both levels and their cutscenes | - | menu lane |

## As built

**A (coordinator)**: `TextHighlightSettings.Category.uppercase` (new, off by default) + a "Super jump" category in
`Resources/TextHighlightSettings.asset` (blue `#006BFF`, bold, uppercase; words: super jump, super salto, súper salto,
supersalto, super pulo, súper pulo). `ResaltadorDeConceptos.Envolver` upper-cases a match of an uppercase category; the
longest-first regex already lets "super jump" win over "jump". The tooltip is UITexts `paperplane_jump`: EN and PT say
super jump / super salto, **ES does not** ("Tu próximo salto te llevará muy lejos."), so in Spanish nothing changes.

**D (coordinator)**: `TriggerOrigami` holds `LevelManager.inCutscene` from a cancel of a `_repromptAfterCancel` fold until
it re-prompts (`HoldKami`/`ReleaseKami`, released right before the fold reopens, on disable, or if `PromptNow` refuses);
`CanAutoPrompt` ignores its own hold. Applies to page 5's ticket and page 3's letter. Side effect: the HUD strip starts its
0.25 s fade during the ~0.2 s wait (it follows `inCutscene`, #188).

**F (coordinator, #254)**: Diego's `Editor.log` shows `NullReferenceException at InventorySlot.OnEnable` (line 47) on opening
the bag: a slot filled while the Flap was closed (`setNamePromised`) and emptied before it opened looked up a null item.
Also found: the slot's name lookup is async and registers with `LocalizedText` only when the table answers, so a lookup in
flight when the slot was emptied or re-filled (every `ReorganizeSlots` after a capture or a handover) wrote, and kept
re-writing on every device/language change, the OLD item's name into that slot. Fix in `InventorySlot`: one name lookup at
a time (`StartNameLookup`/`StopNameLookup`), `ClearSlot` cancels it and the promise, `OnEnable` skips a null item. Whether
this was Diego's "same item in several slots" is not proven: if it comes back, note whether the duplicates are icons or only
names, and read the `[InventoryManager]` slot logs (no duplicate-slot warning appears in today's log).

**Level 2 lane (Sonnet): H, C, V4** (built, compile-checked, not heard or seen):
- **#255**: `AbuelaEntrance` is back on `_dropHeight` (60) / `_fallSeconds` (2.7), set in the scene, `_offscreenFactor` 1.15
  explicit. The rest of the entrance (fall vcam, impulse, sounds, rumble, landing zoom) was checked present in scene and prefab.
- **#256**: `PatrolCarAudio` is now single steps (`Brake`, `StartIdle`, `OpenDoor`, `CloseDoor`, `EngineStart`, `CarMoves`,
  `StopLoops`, `FadeOutLoopsAfter`, a generation counter cancels a pending run/fade). Page 2: door opens when the first cop
  arrives, `_waitAtCarSeconds` 1.5, close + `CarEngine`, move after `_boardingSeconds` (prefab 0.5 -> 1.2), run 1 s after
  moving. Page 3: brake + siren + positional idle (a handle, so the four jammed cars sharing the row keep theirs; row
  `maxSimultaneous` 4 -> 6) start with the cutscene; doors only at boarding; idle gives way to the run at drive-off; the
  arrival horn beep was removed. Page 5: open, wait, close + engine inside `BoardCar`; **the timeline's `BoardCar` ->
  `DriveOff` gap was 0.6 s: `DriveOff` moved 3.6 -> 4.2 s, `Finish` 6.0 -> 6.6 s and the CarFollow shot extended to 7.1 s, so the car moves 1.2 s after close + engine start, like page 2**. `PoliceHorn` at
  drive-off on pages 2, 3, 5. New `AudioManager.FadeOutById` / `FadeOut(handle)` + `AudioPool.CollectHandlesOf`.
- **#257**: `soundOnLine: CopWhistle` on `Arrest_Shout` line 0 and `Grace_MeetingAccusation` line 1 (the lines whose cues
  play the cops' `Arrest` pose).
- **C**: `PoliceStationPage.AfterEscapeLanding` switches straight to `_cameraAfterEscape` (Normal) when Kami lands outside,
  once, then `Natalia_Escaped`.
- **V4**: `DriveOffTarget` local x 80.47 -> 42.4 (car ends at the page edge, world x ~41); the shot blends back
  `_cameraReturnLeadSeconds` (1.5) before the car arrives; siren + run fade over `_sirenFadeSeconds` (2).

**Menu lane (Opus): E, H2, BLD** (built, compile-checked, also with `DISABLESTEAMWORKS`; not seen in the Editor):
- **Level select**: New Game in `MainMenu.unity` opens `Canvas/LevelSelect` (`LevelSelectMenu` + two `LevelSelectCard`s +
  Back). Logo, New Game and the language buttons moved under `Canvas/MainMenuGroup` (same positions/order) and fade out
  with the logo's smoke; cross-fade 0.35 s. Esc / Start / B / Back return; a gamepad lands on the Level 1 card; cards scale
  1.05 on hover/selection (never `Select()`). If Level 2 is not in the build (the Steam demo), New Game starts Level 1 as before.
  Move everything with `Cards`.
- **Card art**: the pictures are placeholder PNGs `Assets/2D/UI/Level Select/LevelSelect_Level1|2_Screenshot.png`
  (overwrite with real 16:9 captures, same names, no rewiring). Kami is Valen's `Default Kami` / `Detective Kami` (their
  metas were on spriteMode 0, fixed to Single). The Detective Kami faces right, away from her picture: scale X -1 flips her.
- **Scene flow**: `Managers/Scenes/SceneFlow` (one-shot static "scene after the cutscene", reset on Play). Level 1 card =
  old flow (intro storyboard, Level 1). Level 2 card = `Nivel1_EndCutscene` -> Level 2: on that path the cutscene keeps
  black behind the dialogue, skips its "Continuará" splash and loads Level 2 when the dialogue ends. Reached from Level 1 it
  is unchanged (splash, E, main menu). Both end cutscenes already went to MainMenu.
- **H2**: `Level2_EndCutscene`'s `EndingSplash` starts inactive (black behind the dialogue); logo + "Continuará" fade in
  (2 s, was ~20 s) when the dialogue ends, E goes to the menu.
- **UITexts**: `level1_name` (Kami Papel y Tijera / Kami Paper Scissors / Kami Papel e Tesoura), `level2_name` (Kami Tinta
  y Condena / Kami Ink and Punishment / **Kami Tinta e Castigo**, PT to confirm), `levelselect_back`, `origami_fail_retry`.
  Titles from `docs/GDD.md` (no commas, matching the protected phrases).
- **BLD**: **Kami > Build > Local (Both Levels)** (`Assets/Editor/LocalBuild.cs`) builds the five scenes into `Builds/Local/`
  with `DISABLESTEAMWORKS` as an extra define for that build only (SteamManager compiles empty, so the exe runs by
  double-click without Steam relaunching the demo) and without the demo's Level 2 audio stripping. Not run.

**Level 2 nice lane (Sonnet): V3, V5, V6** (built, compile-checked, not played; scene 2371 -> 2383 blocks):
- **V3**: `natalia_01_06` now opens "Natalia, detective privada." / "Natalia, private detective." / "Natalia, detetive
  particular." (rest of the bubble unchanged).
- **V5**: `Level2/NataliaPageEndBeat` (`Page 2/Natalia Page End Beat`, mark `Natalia Wave Spot`, shot `CM Natalia Wave`) is
  queued by `FindCluesTracker._allCluesBeat` right after `Natalia_AllClues`: Kami locked, shot raised, Natalia warped to the
  NavMesh-snapped spot mid-blend, `Wave` (Spine "Saludo") 3 s, shot dropped, Kami freed after the blend. Then Natalia waits
  there until Kami is within `_rejoinRadius` 16 or a page turn starts, and follows again. **The spot (world ~33, 3.1, 4) is a
  guess: drag it onto the street near the exit and check the framing.**
- **V6**: `ArrestCutscene.CUE_DriveOff` fades to black over `_leaveFadeSeconds` 1.2; `TurnPage` waits for black; the page
  turn, cell placement, confiscation and cell bars happen behind it; `WrapUp` keeps the lock; `ArriveInTheCells` holds 0.4 s,
  fades in 0.8 s on Kami's cell, holds 1.2 s, pans to Natalia (`_nataliaCellShot` = the existing `CM Intro NataliaCell`),
  `Wave` 3 s, back, then `Release()`; `PoliceStationPage`'s intro already waits for `inCutscene`, so it starts after.
  First arrest only. The black lasts the whole page turn (~5-6 s): shorten `PageScrollerManager` delays if long.

**General lane (Sonnet): V1, I, V2** (built, compile-checked, not played):
- **V1**: both fail branches of `MultipleRectCheck` call `FailFold()` (crumble sound, paper loop stopped, cursor reset)
  instead of `EndOrigami`; `Origami.RestartCurrentFold()` resets only the current route (arrow + flipbook slider), leaving
  `currentRouteIndex`, `wasCompleted` and the OSU active flags alone. `OrigamiFailFeedback` on `CurrentFold.prefab` (nested
  in all 13 routes): sheet tint `_flashColor` over `_flashSeconds` 0.2, red `origami_fail_retry` in the outlined `Cutscene`
  TMP font rising `_risePixels` 140 and fading over `_messageSeconds` 1.5; a second failure restarts it. Cancel unchanged.
  `LocalizedText.Opciones.sinResaltado` keeps the highlighter from colouring "fold" in the red message. `Pag7 Animal` has no
  flipbook animator: message, no flash.
- **I**: the "Fold N/N" text sits at y -480 in `CurrentFold.prefab` (was -331); the per-route overrides in `3-Medium`,
  `AbuelaFold`, `AbuelaUnfold` were removed so the prefab is the one place to tune.
- **V2**: `GlobitoDialogo.prefab` uses Valen's `Globo Dialogo 0-3` (metas fixed to Single, 250 PPU) with
  `Animations/GlobitoDialogo` (3 s loop, squash-and-stretch pop anchored at the tail, 1.5 s hold on 0). New `UI/DialogueGlobe`
  shows it while `_talkTrigger` is alive, enabled and its collider on, hidden during `inDialogue`/`inCutscene` (empty = always
  on, the old behaviour). Wired: Dalia (`NPC_Florista`), Tiburcio (Level 1 scene; Chino got the new instance too but
  unwired, always on, he is a stub), Grace,
  the two page 2 guards (`CrimeSceneGate.prefab`), Natalia (`Natalia.prefab`, applied by the coordinator: shows only while
  she is talkable). Abuela, Norberto and the Letrero inherit the new art. Grace's globe shows before Kami has the evidence,
  although E does nothing then.

**Review pass (2026-10-21, two read-only Opus reviewers over the whole diff, fixes by the coordinator)**:
- `Level2_EndCutscene`: the H2 change had set the logo's alpha to 0 and relied on a `SplashFadeIn` that is disabled in the
  scene, so the logo would never show. Restored (alpha 1, `fadeSpeed` 0.05): `PlacaNegra (1)`'s own fade reveals it.
- Page 2: the siren/run fade started on the same frame as the engine run, so the run could be cancelled. The fade now
  starts at `max(_engineRunDelaySeconds + 0.25, _driveOffSeconds - _sirenFadeSeconds)`.
- Page 5: `Timeline_MuseumArrest` `DriveOff` 3.6 -> 4.2 s, `Finish` 6.0 -> 6.6 s, CarFollow shot to 7.1 s, so the engine
  start (2 s) and the run no longer overlap and the car moves 1.2 s after boarding, like page 2.
- `OrigamiRoute AbuelaUnfold`: `CurrentFold` (the fail message) moved to the last sibling so it draws over the sheets.
- #254 leftovers: `InventorySlot.OnDisable` re-promises a name lookup Unity killed when the slot hid; `ShowcaseSlot`'s
  description lookup is now single-flight and cancelled by `ClearSlot`.
- Not changed (noted): `TriggerOrigami`'s hold would overwrite another script's `inCutscene` set during its ~0.2 s wait
  (no beat does that today); the fail flash sets the sheet colour rather than multiplying it (all sheets are white).

**Status: all lanes built 2026-10-21, compile-checked (`tools/compile-check.py`), scenes checked for unique ids and resolved
refs. Nothing played, nothing committed.**
