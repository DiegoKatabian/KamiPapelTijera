# Paso de página: HojaMaster, PositionMarker y el enganche de Kami (RidingPage)

## HojaMaster NO es Spine

`Assets/Prefabs/Hoja/HojaMaster.prefab` (y `HojaMaster_Rev.prefab` para el sentido inverso) es un rig **Mecanim** (Animator + malla skinneada importada de `Assets/3D/HOJAS/HojaOriginal.fbx`), no un esqueleto Spine. La cadena de huesos se llama `PN000pageJoint00`…`PN000pageJoint30` (Transforms normales de Unity, animados por el Animator). Por eso el objeto `GlitterHoja` que cuelga de `PN000pageJoint30` lo hace por **parenting directo de Transform**, sin `BoneFollower`: un hueso Mecanim ya es un Transform de la jerarquía; un hueso Spine no (vive dentro del `Skeleton` y necesita un componente puente, como sí se usa en `Kami.prefab` — ver `spine-kami.md`).

`Assets/Scripts/Hoja.cs` vive en el GO `HojaCaraAbajo` (instancia de `Hoja.prefab` dentro de `HojaMaster`). `HojaIdleStart()` la dispara un Animation Event del clip de Mecanim cuando el giro llega a idle: fira `Evento.OnPageFinishTurning` y se autodestruye.

## Flujo de eventos de un page-turn

1. `Evento.OnPlayerPressedE` → `PageScrollerManager.TriggerPageScroll` (gateado por `esferaNext.triggerBool`/`esferaPrev.triggerBool`, o sea, el player ya tiene que estar parado en el trigger de borde).
2. `ChangeToNextPage`/`ChangeToPrevPage`: incrementa `activePageIndex`, dispara `Evento.OnPageTurnStart(activePageIndex, isNext)` — lo consume `PlayerPageSpawnManager.SetPlayerTargetPosition` (sólo precalcula y guarda `targetPos`, no mueve todavía).
3. `CerrarPaginaCoroutine` (tras `delayTime`): `LevelManager.inDialogue = true` (freeze), `CreateHoja(isNext)` instancia `HojaMaster`/`HojaMaster_Rev`, y engancha a Kami al borde (`StartPlayerRide`, ver abajo).
4. `AbrirPaginaCoroutine` (tras `popupDelayTime`): dispara `Evento.OnNewPageOpen` (consumido por `PlayerPageSpawnManager.PlacePlayerInNewPage` y `PositionMarker.OnChangePage`), abre PUBs, activa la carpeta de la nueva página.
5. Cuando la hoja termina de girar, `Hoja.HojaIdleStart()` dispara `Evento.OnPageFinishTurning` → lo consumen `PageScrollerManager.FinishTurning` (apaga `inDialogue`, restaura cámara, destruye la hoja) y `PlayerPageSpawnManager.FinishRide` (suelta a Kami, ver abajo).

El reposicionamiento "de siempre" (`PlayerPageSpawnManager.PositionPlayerAtPoint`: deshabilita el `CharacterController`, setea `transform.position`, lo rehabilita) sigue siendo la única fuente de verdad sobre dónde termina Kami — el enganche a la hoja es puramente visual durante la transición.

## PositionMarker: cómo se mide "dónde está Kami sobre el borde"

`Assets/Scripts/PositionMarker.cs`, colgado del prefab `esferaPrev`/`esferaNext` (los triggers de borde), no de Kami. Mientras el player está parado en el trigger (`TriggerTurnPage.OnTriggerStay`), copia **solo la Z** de `player.transform.position` sobre su propio transform (X/Y quedan fijos en el borde del libro), y usa la distancia resultante para un alpha `_powerFactor / distancia²` (indicador visual de proximidad). Esta es la convención de "posición sobre el borde" que reutiliza el enganche de Kami: Z es el eje que corre a lo largo del filo del libro, X/Y son la posición "de salto" a la página siguiente (confirmado en `PlayerPageSpawnManager.GetProjectedPositionInNewPage`: sólo `desiredX`/`spawnY` cambian entre páginas, `playerCurrentPosition.z` se preserva tal cual).

## RidingPage: Kami enganchada al borde durante el giro

Mecanismo (agosto 2026): mientras la hoja gira, Kami queda visualmente agarrada al hueso `PN000pageJoint30` en vez de congelada en el aire. Sin IK — se mueve el `transform` raíz de Kami entero (la animación "ala delta" ya viene autoposicionada con las manos en el borde).

- **`Hoja.cs`**: expone `EdgeBone` (busca `PN000pageJoint30` recursivamente en su jerarquía, con warning si no lo encuentra — robusto a la estructura de prefabs anidados sin depender de wiring manual).
- **`PageScrollerManager.CerrarPaginaCoroutine`**: justo después de `CreateHoja`, llama `Player.StartRidingPage(edgeBone, isNext)`.
- **`Player.cs`**: `StartRidingPage` calcula y guarda el offset Z entre la posición de Kami y el hueso en ese instante (misma lógica que `PositionMarker`), deshabilita el `CharacterController`, entra a `PlayerState.RidingPage`. Un `LateUpdate` (mismo patrón que `BoneFollower`/`SpineBoneTipFollower`: correr después de que el Animator de la hoja actualizó el hueso ese cuadro) reposiciona a Kami en `bonePos + offsetZ + rideRootOffset` (offset tuneable en el inspector, por si la animación no calza exacto con el pivot del hueso) todos los cuadros mientras dure el enganche.
- **`PlayerModel.Tick`**: retorna inmediato si `Player.IsRidingPage` — evita que la gravedad se siga integrando con el `CharacterController` deshabilitado (si sólo se zereaba el input, como hace `IsPullingSolapa`, la velocidad vertical acumulada explotaría en una caída brusca al soltarse).
- **`PlayerView.cs`**: al entrar a `RidingPage` encadena `jump` (mix instantáneo, `animMix.jumpMixDuration`) → `RidingPage` (constante `ANIMATION_RIDING_PAGE`, mix configurable `animMix.changePageJumpToRideMix`, default 0.5s) con `SetAnimation` + `AddAnimation(delay=duración del jump)` — así Kami "salta" a la hoja antes de quedar rideándola. Fallback-con-warning a `Idle` si el skeleton no trae `RidingPage` (mismo patrón que `PullSolapasReverse`). `ForceFacing(bool)` fija el flip explícito (a diferencia de `SetFacing`, que sólo reacciona a input crudo) y ahora también sincroniza el flip de las sprint particles (`SyncSprintParticlesFlipToFacing`, ver `audio-y-particulas.md`).
- **`PlayerPageSpawnManager.cs`**: `PlacePlayerInNewPage` (en `OnNewPageOpen`) se vuelve no-op mientras `IsRidingPage` es true — la colocación autoritativa final (`FinalizePlacement`, la misma de siempre) se aplica recién en `FinishRide` (suscripto a `OnPageFinishTurning`), que también suelta a Kami (`StopRidingPage`).

**Confirmado (agosto 2026)**: flujo completo probado en el editor (jump→ride, flip, sprint particles) — funciona en ambos sentidos. Si `HojaMaster_Rev` no tuviera la cadena `PN000pageJoint30`, `Hoja.cs` avisa con warning y Kami no se engancha en ese sentido, sin romper el resto.
