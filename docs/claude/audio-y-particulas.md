# Audio y partículas

## AudioManager (pooled + AudioBank, September 2026)

Singleton (`AudioManager.instance`) on `Assets/Prefabs/AudioManager.prefab`. It no longer keeps one
always-alive `AudioSource` per sound: configuration is data in `Resources/AudioBank.asset` and
playback goes through `AudioPool` (`Assets/Scripts/Managers/Audio/`), a fixed pool of generic
sources (`_poolSize`, default 24) that a `SoundEntry` configures at play time.

**Adding a sound** is now a bank edit, not a prefab edit: add a row to `AudioBank.asset` (clips,
volume, pitch, loop, bus, `maxSimultaneous`, `spatialBlend`) and run `Kami/Audio/Regenerate AudioId`
so the `AudioId` constant exists. `AudioBank` self-loads from Resources, so nothing needs wiring.

**API** — always with an `AudioId` constant, never a raw string:
- `Play(AudioId.X)` / `Play(AudioId.X, pitch)` / `Play(AudioId.X, centralPitch, variation)`
- `PlayAt(AudioId.X, position)` — positional one-shot
- `StopById(AudioId.X)`, `Stop(SoundHandle)`, `StopAll()`, `IsPlaying(AudioId.X)`
- `SetBusVolume(AudioBus, v)`, `SetGlobalVolume(v)`, `SetBGMVolumes(v)` / `ResetBGMVolumes()`
  (both are now just the Music bus), `PlayOnEnd(AudioId.A, AudioId.B)`, `SoundOn`

**Several clips in one entry = random pick per play.** That is what replaced `PlayRandom`. The
footsteps deliberately kept their original pairing as `Pasos_Kami_A` (clips 1+3) and `Pasos_Kami_B`
(2+4) instead of one four-clip entry, so each clip keeps exactly the odds it had before the
refactor. `MagicChannelingLoop01/02` is the one group that was NOT collapsed: `MultipleRectCheck`
stops each loop by id, so they must stay individually addressable and the random pick lives at the
call site.

**Buses**: `Music`, `SFX`, `UI`, `Ambience`, mapped in the bank to the groups of
`Resources/Audio Mixer Groups/AudioMixer 1.mixer` (exposed parameters `MusicVolume`, `SFXVolume`,
`UIVolume`, `AmbienceVolume`). Bus volume is still applied per source in code; moving it onto the
mixer parameters is Task 23 of the refactor plan.

**Music is Estampes Pagodes (public domain) since 2026-10-14 (spec 015, A.7).** The `MemoFlora*` ids (main loop, battle loop, post-battle, `GameOverOrchestral`, `MagicSuccess`, `PickupSpecial`, `QuestCompleted02`, `Receive_Reward`) kept their names but play the Estampes clips; volumes were matched by integrated loudness (the old clips were 10-14 dB quieter). The boss theme is `EstampesPagodes_Battle_Intro` then `MemoFloraBattleLoop01` (`EncounterManager`). `Assets/Sounds/MemoFlora/` is gone. The Level 1 demo build also empties the Level 2 music entries and the Sims-named clip while it builds (`SteamDemoBuild`), because the bank is in Resources and ships every clip it references.

**Level 2 SFX pass (task 6.E, 2026-09-28, compile-checked only)**: 27 placeholder rows, every one
reusing an existing clip (clip list = the only thing to swap in 6.G, nothing in code changes). SFX
bus unless noted; all 2D. Ids and where they fire:
- Police (page 3 arrest, `ArrestCutscene`): `CarBrake` + `PoliceSirenLoop` + the engine idle start with the cutscene (as the street and car
  appear, #256; they used to start at `CUE_CopsBehindGirls`, and the arrival horn beep there is gone), the siren stops at `CUE_TurnPage`/WrapUp/OnDestroy.
  (The cops' running steps were dropped 2026-10-21: NPC footsteps are #247.)
  `PoliceHorn` at `CUE_DriveOff` on pages 2, 3 and 5 (Diego, 2026-10-21; page 2's exit also loops
  `PoliceSirenLoop` for the whole cutscene, fading out as the car leaves, see below). The door and engine sounds of all three patrol car sites (page 2, 3 and 5) go through
  `Level2/PatrolCarAudio`, see "Patrol car sequence" below.
- `PlaneRumbleLoop` (#232, 2026-10-17): the loop under page 4's countdown before the Abuela falls (`AbuelaEntrance._rumbleSound`),
  stopped by her crash. Real `RumbleLoop.ogg` since 2026-10-21 (it was `CarEngine`'s clip at pitch 0.22).
- Page 4: `CellBarsSlam` (`PoliceStationPage.StartSequence`, also after a capture), `PadlockClank`
  (`CandadoCortable.ApplyCut`, so Level 1's chest padlocks clank too), `FenceRattle` (Natalia's door
  in `PoliceStationPage`, Kami's fence in `AbuelaEntrance.Impact`), `AbuelaFall` + `AbuelaCrash`
  (`AbuelaEntrance` `_fallSound`/`_crashSound`/`_fenceSound`, replacing the old `_impactSound`),
  `CopWhistle` (`PoliceOfficer._alertSound`, was `MagicFail`; also changed in `PoliceOfficer.prefab`),
  the capture (`Player.Die` on `DeathCause.Caught`) is `CopWhistle` on top of `GameOverOrchestral` (Diego, 2026-10-21; the
  `CaughtSting` row is gone). The typewriters and the drapes have no extra sound for now (`TypewriterClack` and `ClothRip` rows
  deleted; `_extraCutSound` is empty on `CuttableTypewriter.prefab`).
- `CopWhistle` at the arrests (#257, 2026-10-21, built, not heard): `DialogueEvent.soundOnLine` on the exact line where the cop's `Arrest` pose cue
  fires, so the sound and the pose start together (same moment in `DialogueManager.WriteText`): page 3 `Arrest_Shout` line 0, page 5
  `Grace_MeetingAccusation` line 1 (the cop's "Nadie se mueva"). One whistle per arrest, not one per cop (the cue is addressed to every `Police` actor but
  the sound is on the line); neither line had another per-line sound. `AnimationCue` has no sound field (not needed): if the whistle must land later than the
  pose's start, add a delay there instead of moving the line.
- Pages 1-2: a clue found plays `MagicSuccess` at pitch 1.4, the same as opening a chest in Level 1 (`FindCluesTracker.PlayClueSounds`;
  Diego 2026-10-21, the `ClueFound` and `ManholeClank` rows are gone), `RadioStatic` (dialogue line, below), `CarPassBy`/`CarHorn` on ambient
  traffic (`TrafficSpawner.PlayTrafficSounds`: pass-by at most once per 6s; horn at most once per 40s at
  5%, lowered from 20s / 15% when the real horns came in; both counted across ALL spawners; tunable per spawner,
  `hornChance` / `hornMinInterval` in its Sound header). The horn plays positional from the middle of the street.
- Page 3: the gift opening plays the same chest sound (`GiftDialogueTrigger.Open`, `MagicSuccess` at 1.4), `Handcuffs` (dialogue line).
- Page 5: `RopeCreak` once, when the arm is pulled down while Grace shows the catapult (`Catapult.ArmRoutine`,
  Diego 2026-10-19; it used to creak every 3-6s while Kami sat in the bucket). Cutting the rope plays the
  cuttable's own `TijeraHit` + `PaperCut` and, on the same frame, `CatapultLaunch` (the "Swish and Thud" clip, `Catapult.OnRopeCut`
  since 2026-10-21: on the `Launch` signal 0.6s later it read as a second sound; it replaced `Jump_Paperplane`; the old `CatapultThud` row and
  its duplicate clip are gone).
- Ambience bus, loops: `CafeAmbience` (page 1) and `BarAmbience` (page 3) on top of `CityAmbience`, a 2D bed under every page
  (`PageAmbience._bedAmbience`, started once at level start and never switched, Diego 2026-10-19), driven by `PageAmbience`
  (`Managers/`, sibling of `PageMusicManager`: one id per page in the Inspector, same
  play-if-not-already-playing rule). Drop `Prefabs/Level2/PageAmbience.prefab` into the scene; the
  prefab is pre-filled for Level 2. The café and the bar are positional (2026-10-19): `_ambienceSourcePerPage`
  points page 1 at `KamiCafe` and page 3 at the jazz band's object, and their rows are linear 40-140.
- `CopKnockedOut` plays when a patrol cop is cut and knocked out (`PoliceOfficer._knockedOutSound`, since 2026-09-29).
- Page 3's traffic jam (#177, 2026-10-13): `CarEngineIdleLoop` (loop, `CarEngine`'s clip) and `CarHornIdling`
  (the five real horn clips, like `CarHorn`), both positional, played by `Traffic/IdlingCar` on the `Paper Car Idling` prefab,
  each car honking every 20-45s (`_hornMinSeconds` / `_hornMaxSeconds` on the prefab; was 6-16s). The jazz band
  (`Live Music - Alone Together`, a scene AudioSource, not the bank) is stopped by the arrest (#176, `cutscenes.md`).

**Positional sounds and distance (#177).** The AudioListener is on the camera, 13-45 units from Kami. With Unity's
default rolloff (logarithmic, full volume only within 1 unit) a `PlayAt` sound is nearly silent at that distance,
which is why `SoundEntry` has `minDistance` / `maxDistance` / `linearRolloff` (defaults = Unity's, so the chickens'
rows are unchanged). The idling cars use linear 25-75 (engine) and 30-110 (horn). `AudioManager` applies them on every
play, so a pooled source never keeps the last sound's rolloff.

**Dialogue portraits (#245, 2026-10-17)**: `DialogueManager.WriteText` sets each line's portrait before the two
localization lookups (each takes a frame or more), hides the old text and blanks the speaker name only when it changes,
so the globe never shows the previous line's or dialogue's portrait. Not played yet.

**Per-line dialogue sounds**: `DialogueEvent.soundOnLine` (an AudioId, empty = none) is played by
`DialogueManager.WriteText` when that line starts being written. Set today: `Arrest_Cuffs` line 1
-> `Handcuffs`; `Natalia_01` line 7 -> `RadioStatic` (`NewsJingle` on line 8 was removed on 2026-10-19).
New sounds tied to a sentence are one YAML/Inspector field, not a Timeline signal.

**Real clips (2026-10-19, `Assets/Sounds/Level 2 SFXs/` and `Pencil Write/`)**: Diego's recordings, all rows at volume
0.5 and pitch 1 (his clips are already levelled). In so far: `CafeAmbience`, `CityAmbience`, `CarHorn` / `CarHornIdling`
(five clips each, pitch spread 0.08), `CopWhistle`, `Handcuffs`, `RadioStatic`, `Dialogue_PencilWrite`. Ambience beds
over 30s import as Streaming. `Dialogue_Sigh` and `NewsJingle` were removed (Diego). Positional rows (spatial blend 1)
must be played with `PlayAt`: a plain `Play` sounds from the world origin.

**Real clips pt 2 (2026-10-20 import, wired 2026-10-21, bank + code only, no scene touched, compile-checked, not heard in play)**:
all volume 0.5 / pitch 1 except `PlaneRumbleLoop` (0.35, as before) and `CarEngineRun` (pitch 1.35). `PoliceSirenLoop` <- Police car Siren Loop;
`PoliceHorn` <- the three Police Horn Siren Short clips (random pick, pitch spread 0.05); `CarBrake` <- Car Break Tires Squeak; `CellBarsSlam`;
`PadlockClank`; `FenceRattle`; `AbuelaCrash`; `AbuelaFall` <- Abuela Falling (2.97 s); `RopeCreak`; `CatapultLaunch`; `CarEngineIdleLoop` (still
positional 25-75); `PlaneRumbleLoop` <- RumbleLoop. Ambient traffic plays **`CarPassBy`** (an old placeholder engine clip at 0.12 / pitch 0.5)
instead of `CarEngine`, which is now the real ignition and only the police cars use it.

**Patrol car sequence (`Level2/PatrolCarAudio`, #256, redone 2026-10-21, built, not heard in play).** The first version chained open -> close ->
start by itself and the timing did not land, so `PatrolCarAudio` is now a set of single steps and each site calls them when it wants
(`Brake`, `StartIdle(position)`, `OpenDoor`, `CloseDoor`, `EngineStart`, `CarMoves(runDelay)`, `StopLoops`, `FadeOutLoopsAfter(delay, seconds)`).
The siren and `PoliceHorn` stay with each site. The three sequences:
- **Page 2 (`CrimeSceneGate`)**: the first cop to reach the door plays `CarDoorOpen`; the cops stand there `_waitAtCarSeconds` (1.5) after the last
  one arrives, then get in: `CarDoor` + `CarEngine` (the start, about 2 s) together; `_boardingSeconds` (prefab, 1.2; was 0.5) later the car moves
  (`PoliceHorn`); `CarEngineRun` starts `_engineRunDelaySeconds` (1) after it moves. One open/close pair for both cops (it was one pair each).
- **Page 3 (`ArrestCutscene`)**: at the cutscene start `CarBrake`, the siren and `CarEngineIdleLoop` (positional, `PlayAt` at the car: the same
  row the four jammed cars use, so the patrol idle is kept as a handle and stopped by handle, never `StopById`; the row's `maxSimultaneous` went 4 -> 6
  for it) run through the whole dialogue. At the door: `CarDoorOpen`, the girls wait `_waitAtCarSeconds` (the timeline holds), `CarDoor`; no engine start.
  When the car moves (`CUE_DriveOff`, `PoliceHorn`) the idle gives way to `CarEngineRun` after `_engineRunDelaySeconds` (0 = straight over).
- **Page 5 (`MuseumArrest`)**: as page 2 (open when Ariel arrives, hold `_waitAtCarSeconds`, close + `CarEngine` on `BoardCar`), then the `DriveOff`
  signal (1.2 s after `BoardCar` on the timeline since 2026-10-21, it was 0.6: move that marker to change the start-to-moving gap) plays siren + `PoliceHorn` and `CarMoves`.
- Loops stop on every exit: each site's end (`StopLoops`), `OnDisable` (pages 2 and 5), `OnDestroy` on scene unload (pages 2 and 3). A run loop or fade still
  waiting is cancelled by a generation counter.
- **`AudioManager.FadeOutById(id, seconds)` / `FadeOut(handle, seconds)`** (new, generic): fades the live instances over real time, then stops them.
  Page 2 uses it so the siren and the run die away as the car leaves (`_sirenFadeSeconds` 2, ending when the car reaches the mark).

**The Abuela's entrance sounds (page 4, `AbuelaEntrance`)**: `PlaneRumbleLoop` starts with the shake, `AbuelaFall` the instant she appears,
`AbuelaCrash` + `FenceRattle` on impact. The fall lasts `_fallSeconds` 2.7 s from `_dropHeight` 60 (the original field names again since 2026-10-21: the
temporary `_startHeight` / `_fallDuration` were folded back and the scene's values set to 60 / 2.7) so the fall and its 2.97 s clip end together; the fall shot looks at a point below her
start while it rumbles (`_offscreenFactor`) so she enters from above the frame, then follows her. Not seen in the Editor.

**Dialogue pencil (2026-10-19)**: `DialogueManager` chains `Dialogue_PencilWrite`'s ten clips back to back while a line is
written (`PencilStream`): it fills the typewriter's time for that line (characters / `_charsPerSecond`), picking each
clip at random among those that still fit what is left (never the same twice in a row; none fits = the closest), so long
lines get long strokes and every line ends on short ones. The row's 0.05 pitch spread varies each stroke. Skipping the
typewriter stops the pencil; a line written to the end lets the last stroke finish. `AudioManager.PlayClip` /
`ClipsOf` exist for this: callers that pick the clip themselves.

**Gotchas worth knowing:**
- A `SoundHandle` carries a generation counter. A stale handle can never stop whatever is playing on
  that pooled source now — always stop through the handle or the id, never by grabbing the source.
- An unknown id no longer throws `KeyNotFoundException` (the old `soundDict[name]` did): it logs a
  warning **once per id** and stays silent. Two ids in the code have no clip anywhere and rely on
  this: `ShipSailingLoop` (`BarquitoMovingState`) and `4S_MarimbaLoop*` (`TriggerSound`). Both
  components are dead — not present in any scene or prefab.
- The pool reclaims finished one-shots from `Update()`, not from a coroutine per sound, precisely so
  an outside `AudioManager.instance.StopAllCoroutines()` (`EncounterManager` does this) cannot
  strand sources it already handed out.
- **The refactor is closed (#55, 2026-10-05)**: the shims and the 61 old `AudioSource` children of
  `AudioManager.prefab` are gone. The one plan task not done is Task 23 (bus volume on the mixer's
  exposed parameters instead of per source in code), optional by design.

## TijeraHitbox y TijeraMiss

`TijeraHitbox` (trigger que se activa/desactiva por ataque): el flag `missed` arranca en `true` en `OnEnable` ("asumir miss hasta que se demuestre lo contrario") y solo pasa a `false` al cortar algo `ICortable`. En `OnDisable`, si sigue `true`, suena `TijeraMiss`. Tocar paredes/suelo NO afecta el flag — no volver a la semántica vieja (seteaba el flag en OnTriggerEnter y fallaba al errarle al aire).

## ParticleShooter (en Kami.prefab)

Array `particleSystemGameObject` por índice (constantes `PARTICLE_*` en PlayerView — usar esas, no números):

| # | Constante | Prefab / uso |
|---|-----------|--------------|
| 0 | PARTICLE_SPRINT | KamiSprintParticles — `Enable` on/off con el sprint, flip explícito con `SetFlip` (ver glosario) |
| 1 | PARTICLE_JUMP | KamiJumpParticles — `Shoot` en salto y aterrizaje |
| 2 | PARTICLE_REWARD | reward — `Enable` durante ReceivingReward |
| 3 | PARTICLE_SPLASH | SplashPasosKamiMojados — `Shoot` por paso mojado |
| 4 | PARTICLE_WIND | Particles_GetAffectedByWind — `Enable` con viento |
| 5 | PARTICLE_FOOTSTEP | KamiFootstepParticles — `Shoot` por paso seco |
| 6 | PARTICLE_RUNSTOP | KamiRunstopParticles — `Shoot` en frenada en seco |

**Arquitectura actual de las partículas de pies (jump/footstep/runstop):** viven como HIJOS del GO
"Footstep Anchor" dentro de Kami.prefab (instancias anidadas de sus prefabs) y se disparan con
`Shoot()` vía `PlayerView.ShootFootAnchorParticles(index)`, que además gira el anchor 180° según el
facing. NO se instancian más (el `Create` detached quedó en el código pero sin callers). Requisitos
para que esto funcione, en los PREFABS de `Assets/Prefabs/Particulas/`:
- `looping: 0`, `playOnAwake: 0` (one-shot que se dispara con Play).
- **`moveWithTransform: 0`** = Simulation Space **World**: las partículas emitidas quedan en el
  mundo y NO siguen a Kami. Este campo YAML es un bool legacy: `1` = "las partículas se mueven con
  el transform" (**Local**), `0` = **World**. OJO: el nombre confunde y ya causó un bug — la sesión
  del 30/7 los dejó en 1 creyendo que era World, y las nubecitas de cada paso/salto viajaban
  pegadas a los pies apilándose unas sobre otras (partículas "arrastradas" por el emisor). No es
  una fuga de instancias: son las mismas partículas simuladas en el espacio equivocado.
- El aterrizaje dispara su partícula al entrar a `Landing` desde `Falling` (en PlayerView).
  `BrieflySlowDown` ya NO dispara partículas (salía duplicada en hard falls).
- `KamiFootstepParticles` = copia de KamiJumpParticles al 55% de tamaño, colores 60% hacia blanco,
  menos emisión. `KamiRunstopParticles` empuja hacia adelante (los otros dos hacia atrás).
- El GUID de KamiRunstopParticles (`a1b2c3d4e5f6...`) parece trucho pero es real: una sesión lo
  inventó y escribió el .meta consistente. Funciona; no "corregirlo".

**Glosario anti-bug de partículas** (los tres modos de fallar que ya nos pasaron):
1. *Simulation space Local sin querer* → partículas que siguen al emisor y se apilan (el bug de
   arriba). Fix: `moveWithTransform: 0`.
2. *Fuga de instancias* (object leak / unbounded spawning) → `Instantiate` por evento sin `Destroy`
   ni pooling: clones que se acumulan en la jerarquía a lo largo del juego. El patrón `Create` de
   ParticleShooter lo evita destruyendo a los `timeToDestroy` (2s).
3. *Emisión que nunca para* → sistema `looping: 1` al que le dan `Play()` y nadie `Stop()`. Fix
   tipo TijeraHitbox: `Stop()` en OnDisable ("sin esto se acumulaban ataque tras ataque").
4. *Flip no sincronizado* → partículas que solo usan `Enable` (sprint) nunca pasan por `Shoot()`,
   así que nunca tenían flip explícito y quedaban espejadas al forzar facing (`ForceFacing`, ej.
   ChangePage). Fix: `ParticleShooter.SetFlip(index, flipped)` — asignación directa, a diferencia
   de `Shoot()` que hace TOGGLE del flip (no sirve para este caso). Se llama desde
   `PlayerView.SyncSprintParticlesFlipToFacing()`, en ChangePage y al entrar a `Running`.

## Brillitos de solapa

`BrillitosSolapa.prefab` (en `Assets/Prefabs/Particulas/`) = copia one-shot de "Brillitos Mágicos" (sin loop, burst de 30 en t=0, duración 0.7s). Instanciado como hijo en `Solapa.prefab` (escala local 0.125 porque el root escala ×80 con scalingMode Hierarchy). `Solapa.CambiarEstado()` le da `Play()`; si el campo serializado se pierde, `Awake` lo busca con `GetComponentInChildren`. **No tocar el "Brillitos Mágicos.prefab" original**: lo usan otras escenas.
