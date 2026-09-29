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

**Placeholder sounds** (the Level 2 pass above added 27 more rows): `CarDoor` (the patrol car door in page 3's arrest, added 2026-09-25) reuses
`PaperFold02`'s clip until a real door slam exists; swap the clip in the bank row, nothing else. Its
`AudioId` constant was added by hand in the generator's format (running Regenerate AudioId keeps it).

**Level 2 SFX pass (task 6.E, 2026-09-28, compile-checked only)**: 27 placeholder rows, every one
reusing an existing clip (clip list = the only thing to swap in 6.G, nothing in code changes). SFX
bus unless noted; all 2D. Ids and where they fire:
- Police (page 3 arrest, `ArrestCutscene` CUE_ methods): `CarBrake`, `PoliceHorn`, `RunningFootsteps`,
  `PoliceSirenLoop` (loop, starts at `CUE_CopsBehindGirls`, stops at `CUE_TurnPage`/WrapUp/OnDestroy),
  `CarEngine` + `PoliceHorn` at `CUE_DriveOff` and when `CrimeSceneGate`'s car leaves (page 2).
- Page 4: `CellBarsSlam` (`PoliceStationPage.StartSequence`, also after a capture), `PadlockClank`
  (`CandadoCortable.ApplyCut`, so Level 1's chest padlocks clank too), `FenceRattle` (Natalia's door
  in `PoliceStationPage`, Kami's fence in `AbuelaEntrance.Impact`), `AbuelaFall` + `AbuelaCrash`
  (`AbuelaEntrance` `_fallSound`/`_crashSound`/`_fenceSound`, replacing the old `_impactSound`),
  `CopWhistle` (`PoliceOfficer._alertSound`, was `MagicFail`; also changed in `PoliceOfficer.prefab`),
  `CaughtSting` (`Player.Die` on `DeathCause.Caught`, on top of the shared defeat music),
  `TypewriterClack` (`PickupCortable._extraCutSound`, set on `CuttableTypewriter.prefab`), `ClothRip`
  (`CuttableDrapes.ApplyCut`).
- Pages 1-2: `ManholeClank` + `ClueFound` (`FindCluesTracker.PlayClueSounds`; the clank only for the
  hat), `RadioStatic` + `NewsJingle` (dialogue lines, below), `CarEngine`/`CarHorn` on ambient
  traffic (`TrafficSpawner.PlayTrafficSounds`: engine at most once per 6s and horn once per 20s at
  15%, both counted across ALL spawners; tunable per spawner).
- Page 3: `GiftBoxOpen` (`GiftDialogueTrigger.Open`), `Handcuffs` (dialogue line).
- Page 5: `RopeCreak` (`Catapult.RopeCreakLoop`, every 3-6s while Kami sits in the bucket),
  `CatapultThud` + `CatapultWhoosh` (`Catapult.CUE_Launch`, replacing `Jump_Paperplane`).
- Ambience bus, loops: `CafeAmbience` (page 1) and `BarAmbience` (page 3), driven by `PageAmbience`
  (`Managers/`, sibling of `PageMusicManager`: one id per page in the Inspector, same
  play-if-not-already-playing rule). Drop `Prefabs/Level2/PageAmbience.prefab` into the scene; the
  prefab is pre-filled for Level 2.
- `CopKnockedOut` has a row but no code path yet (no knock-out mechanic exists).

**Per-line dialogue sounds**: `DialogueEvent.soundOnLine` (an AudioId, empty = none) is played by
`DialogueManager.WriteText` when that line starts being written. Set today: `Arrest_Cuffs` line 1
-> `Handcuffs`; `Natalia_AfterTicket` line 4 -> `RadioStatic`, line 5 (the radio announcement) ->
`NewsJingle`. New sounds tied to a sentence are one YAML/Inspector field, not a Timeline signal.

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
- **The 61 `AudioSource` children of `AudioManager.prefab` are dead weight now** — nothing reads
  them. They are kept on purpose as the rollback path until Diego has played through and is happy;
  retiring them is the last step of the refactor.

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
