# Spine: skeleton y animaciones de Kami

Runtime: **spine-unity 4.2** (package 2026-05-29, installed 2026-09-14 in `7f9706bd`; corrected here 2026-10-02, this doc used to say 3.8). Kami's active export is `Atlas 12 Spine4.2/skeleton.json` (Spine 4.2.43), referenced by `Kami.prefab` and MainMenu's skeletons since 2026-10-05 (spec 011 #140; before that they pointed at `Atlas 11`, a 3.8 export the 4.2 runtime refuses to load, and only the level scenes' overrides made Kami render). Verify any API against `Assets/Spine/Runtime/spine-csharp/` before using it.

## Tracks (PlayerView)

El cuerpo va abajo y el resto se superpone, en este orden:

| # | Track | Uso |
|---|-------|-----|
| 0 | body | locomoción, salto, casting, muerte |
| 1 | noscissors | override loop mientras no tiene tijera |
| 2 | wind | override loop con viento |
| 3 | attack | one-shots: Attack, AttackMOVE, PullSolapas |
| 4 | paperplane | override loop con el paper plane hat |
| 5 | hit | one-shot de daño (modo override) |

Los tiempos de mezcla viven en `Player.animMix` (inspector). El `defaultMix` del SkeletonDataAsset es 0.2, pero `jump`/`jumpNoScissors` siempre fuerzan `jumpMixDuration` (0 = instantáneo) por código — no tocar el defaultMix del asset para esto. Los one-shots encolan `AddEmptyAnimation` con delay = duración de la anim (con delay 0, el mix se comía el final de la anim).

## Animaciones (nombres exactos, Atlas 11)

- Locomoción: `Idle`, `walk`, `Skip`, `Run`, `RunStop`, `jump`, `falling`, `landing`
- Acción: `Attack` (parado), `AttackMOVE` (moviéndose/aire, no keyea piernas), `PullSolapas` (abrir solapa), `DownSolapas` (cerrar solapa — exportada, issue #26 cerrado; el draft la llamaba `PullSolapasReverse`, ese nombre NO existe en el skeleton)
- Otros: `Casting`, `IdleToCasting`, `Reward`, `RewardLoop`, `Hit` (recibir daño), `Death`, `Drowning` (muerte por río, sin variante NoScissors), `Wind`, `TitleScreen`
- Overrides: `NoScissortsOverride` (typo del skeleton), `PaperPlaneOverride`
- Existen pero NO se usan: `IdleNoScissors`, `walk2`, `jumpComplete`

## Skins: Kami Gear (spec 011, built and played 2026-10-05)

Atlas 12 skins: `default` (the whole body), `Tijera_Normal`, `Tijera_Upgrade_1` (only
`tijera_back2` + `tijera_front`). Nothing calls `SetSkin(name)` any more: Kami's skin is **composed at
runtime** from her gear and rebuilt on every gear change. How Spine mix-and-match works, and the
authoring rules for Valen: `specs/011-kami-gear/spec.md`.

```
default skin (Spine's fallback, never touched)
  + outfit, then Scissors, Feet, Hat (GearSlot order: the last one added wins a shared key)
  = Kami's skin
```

- **Code** (`Assets/Scripts/Gear/`): `PlayerGear` (owned by `Player`, like `PlayerModel`) holds one
  `GearItem` per `GearSlot` and raises `Player.OnGearChanged`. `PlayerView` marks the skin dirty and
  rebuilds it with `SpineSkinComposer.Compose` from `SkeletonAnimation.BeforeApply`: `SetSkin` (a new
  `Skin` every time, since `SetSkin` ignores the object it already has) + `SetSlotsToSetupPose`, then
  Spine's own Apply that frame re-hides what the NoScissors animations key empty. **Don't add an extra
  `AnimationState.Apply` after a skin change**: it re-fires the frame's Spine events (`HandleAttack`,
  `HandleFootstep`), because `animationLast` only advances in `AnimationState.Update`.
- **Start**: `Player.Start` applies `_startingLoadout` (Level 1 = `Level1_StartLoadout`: default outfit
  and no scissors, the prefab default; Level 2 = `Level2_StartLoadout`: detective outfit + normal
  scissors, scene override). Never in `Awake`: `SkeletonAnimation` builds its skeleton in its own
  `Awake`. The level scenes no longer set `initialSkinName`.
- **Getting an item equips it**: `Player.EquipGainedGear` listens to `Evento.OnResourceUpdated` and asks
  `GearCatalog.ForResource`. Gains only: confiscation (`LoseTijera` adds -1) is ignored, the scissors
  stay in the skin and the `*NoScissors` animations hide them. Equipping what is already on does nothing
  (Level 2 equips the normal scissors twice: loadout + `_startWithTijera`). Newest wins in its slot.
- **Visuals only**: `hasTijera`, `hasWaterBoots`, `hasSprintBoots` and `SetTijeraEquipment` (now only
  TijeraManager + hitbox) are still owned-based.
- **Missing art**: a `GearItem` whose Spine skin isn't in the export logs one warning per Play and is
  skipped **entirely**, its slot clearing included (missing boots art must leave the outfit's shoes on).
  Today that is `outfit/default` and `outfit/detective`. A missing `Resources/GearCatalog.asset` warns
  once and nothing composes (`default` skin only).
- **Sprint boots retired** (Diego, 2026-10-05): nothing gives them (the P cheat no longer does; Chino's
  quest still lists them, but Chino is a stub), and `Gear_SprintBoots` was deleted. The Lightfall winged
  boots replace them later as a new Feet item.

**Placeholder `feet/rain-boots` (2026-10-05, until Valen's #83)**: a skin added by hand to the Atlas 12
`skeleton.json`, built only from images already in the atlas: each shoe's white `_OL_` silhouette drawn
at the shoe's own size and tinted yellow (a flat rubber boot inside its paper border), the gaiters
(`polaina`) tinted yellow as the shaft, and the buckle + its outline overridden by transparent copies.
The transparent buckle is a placeholder-only trick: the shoes still live in `default`, where the
owned-slot clearing can't reach them. Valen's real skin leaves the buckle out. **A re-export of Kami
overwrites this skin**: if #83 isn't in that export yet, the rain boots just go back to one missing-skin
warning. Recipe: `tools/add-placeholder-rain-boots.py` (re-run it on a new export to bring the
placeholder back).

**Owned Spine slots** (`Resources/GearCatalog.asset`, Spine slot dropdowns): equipping an item first
clears whatever the outfit put in its gear slot's Spine slots, then adds the item. An empty gear slot
shows the outfit's own version.

| Gear slot | Owned Spine slots |
|---|---|
| Outfit | none (it is the base layer) |
| Scissors | `tijera_back2`, `tijera_front` |
| Feet | `25_leg_zapato_front`, `23_leg_zapato_ebilla_front`, `26_leg_polaina_front`, `30_leg_zapato_back`, `28_leg_zapato_ebilla_back`, `26_leg_polaina_back`, and the `_OL_` twin of each (12) |
| Hat | none yet |

Only entries of the composed skin can be cleared: whatever lives in `default` can be replaced but never
removed. That is why Valen moves the shoes out of `default` into `outfit/default` (#141).

**Adding an item** = one `GearItem` asset (`Create > Kami > Gear Item`, in `Assets/Scripts/Gear/`:
slot, Spine skin dropdown, the `ResourceType` that grants it) + one Spine skin in Kami's export + a line
in `GearCatalog`'s items. No code. When Valen's renamed export lands (`scissors/normal`,
`scissors/upgrade1`), only the two scissors items' skin fields change.

## Eventos dentro de las anims

- `HandleAttack` (0.467s) en `Attack` y `AttackMOVE` (desde Atlas 5): dispara la hitbox. `Player.attackMoveHitboxDelay = -1` (el fallback por timer queda para atlas viejos; con ambos activos la hitbox disparaba doble).
- `HandleFootstep` en las anims de locomoción: sonido + partícula de paso.

## Datos no obvios

- Punta de la tijera: hueso `tijera_front3` (length ~726); el trail se posiciona ahí vía `SpineBoneTipFollower`.
- Hueso con typo: `tiejraBack`.
- Anchors sobre el skeleton (el 3DModel viejo fue borrado del prefab en julio 2026): `particleAnchor` (BoneFollower → hueso `Smoke`; ya NO se usa para el polvo de salto, que ahora sale de `Player.FeetPosition`) y PaperPlaneAnchor (BoneFollower → `9_HEAD`, escala local 0.927).
- Flash de daño: los materiales del skeleton usan shader `Spine/Skeleton Fill` (`_FillColor`/`_FillPhase` vía MaterialPropertyBlock).
- Flip: `Skeleton.ScaleX = ±1` en `PlayerView.SetFacing`.
- Muerte en 3 tiempos: `Die()` dispara anim+música+OnPlayerDie; overlay tras `Player.defeatOverlayDelay`; respawn recién al cerrar el overlay con E. La anim `Death` queda congelada en el último frame.
- RunStop: estados de locomoción con `GetAxisRaw` (el smoothing de GetAxis metía Walking entre Skip e Idle); solo suena si venía `runstopMinFullSpeedTime` seguidos a velocidad máxima (`runstopReady`).
- `PlayerAnimationRelay.cs` sigue vivo porque lo usan escenas viejas — no borrar.

## Solapas (PUBMechanics)

`TriggerSolapa.Interact()` captura `solapaAfectada.IsOpen` ANTES de `CambiarEstado()` y llama `player.PlayPullSolapa(estabaAbierta)`: `false` = abrir (PullSolapas), `true` = cerrar (PullSolapasReverse). Mientras dura la anim, `Player.IsPullingSolapa` bloquea movimiento y salto. La solapa en sí es un Animator de Unity (bool `isOpen`) + partículas `BrillitosSolapa` one-shot + sonido PaperFold01.

### Gotcha: quien se apropia del track 3 tiene que resolver el ataque primero

`PullSolapas` y `Attack`/`AttackMOVE` comparten el **track 3**, y el ataque se "desengancha"
(`Player._readyToAttack` vuelve a `true`) SOLO desde el evento Spine `HandleAttack` que vive
adentro del clip (t=0.333s en `Attack`, 0.467s en `AttackMOVE`; el fallback por timer está
apagado, `attackMoveHitboxDelay = -1`).

En spine-csharp (3.8, and still in 4.2: checked 2026-10-02) una entry interrumpida **nunca dispara sus eventos pendientes**:
`AnimationState.cs` hace `var eventBuffer = mix < from.eventThreshold ? this.events : null;` y
`eventThreshold` arranca en 0, o sea la condición nunca da true. Entonces atacar y abrir una
solapa dentro de esos ~0.33s mataba el `HandleAttack` pendiente: `TijeraCoroutine` no corría
nunca, `_readyToAttack` quedaba en `false` **para el resto de la sesión** y la tijera no
servía más (bug reportado por Diego jugando, septiembre 2026). Nada gatea el interact
durante un ataque, así que la ventana es alcanzable con el teclado (Ctrl y E son dos teclas
distintas, se aprietan una atrás de la otra).

Fix: `Player.CancelAttack()` aborta el swing en vuelo (para la corrutina guardada en
`_tijeraRoutine`, apaga la hitbox, corta partículas y devuelve los flags), y
`Player.PlayPullSolapa()` lo llama **antes** de que el view se apropie del track. Guardar el
handle de la corrutina importa: sin eso, el swing cancelado seguía vivo y después apagaba la
hitbox del swing SIGUIENTE a mitad de camino.

**Si agregás cualquier animación nueva sobre el track 3** (otra solapa, una cutscene, un gesto),
llamá `CancelAttack()` primero. Es el mismo principio de fondo que los issues #41.2 y #41.14: dos
sistemas que tocan un recurso compartido necesitan un arbitraje explícito, no asumir que el
timing va a salir bien.
