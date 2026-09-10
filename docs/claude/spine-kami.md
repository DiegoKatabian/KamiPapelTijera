# Spine: skeleton y animaciones de Kami

Runtime: **spine-unity 3.8** (2021-11-10). Ojo: NO existe `TrackEntry.Reverse` (llegó en Spine 4.0) ni otras APIs modernas — verificar contra `Assets/Spine/Runtime/spine-csharp/` antes de usar algo.

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

## Skins (Atlas 11)

`default`, `Tijera_Normal`, `Tijera_Upgrade_1`. Se cambian con `Player.SetTijeraEquipment()`: `Skeleton.SetSkin(nombre)` + `SetSlotsToSetupPose()` + actualiza TijeraManager/hitbox. Botas/guantes a futuro (multi-slot recién con Spine 4.x — hoy 3.8).

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

En spine-csharp 3.8 una entry interrumpida **nunca dispara sus eventos pendientes**:
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
