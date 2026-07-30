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

Los tiempos de mezcla viven en `Player.animMix` (inspector). El `defaultMix` del SkeletonDataAsset es 0.2. Los one-shots encolan `AddEmptyAnimation` con delay = duración de la anim (con delay 0, el mix se comía el final de la anim).

## Animaciones (nombres exactos, Atlas 5)

- Locomoción: `Idle`, `walk`, `Skip`, `Run`, `RunStop`, `jump`, `falling`, `landing`
- Acción: `Attack` (parado), `AttackMOVE` (moviéndose/aire, no keyea piernas), `PullSolapas` (abrir solapa), `PullSolapasReverse` (cerrar solapa — **pendiente de exportar**; mientras no exista, PlayerView cae a `PullSolapas` con warning)
- Otros: `Casting`, `IdleToCasting`, `Reward`, `RewardLoop`, `Hit` (recibir daño), `Death`, `Wind`, `TitleScreen`
- Overrides: `NoScissortsOverride` (typo del skeleton), `PaperPlaneOverride`
- Existen pero NO se usan: `IdleNoScissors`, `walk2`, `jumpComplete`

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
