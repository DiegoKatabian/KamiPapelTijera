# Paso de página: HojaMaster, PositionMarker y el enganche de Kami (RidingPage)

## HojaMaster NO es Spine

`Assets/Prefabs/Hoja/HojaMaster.prefab` (y `HojaMaster_Rev.prefab` para el sentido inverso) es un rig **Mecanim** (Animator + malla skinneada importada de `Assets/3D/HOJAS/HojaOriginal.fbx`), no un esqueleto Spine. La cadena de huesos se llama `PN000pageJoint00`…`PN000pageJoint30` (Transforms normales de Unity, animados por el Animator). Por eso el objeto `GlitterHoja` que cuelga de `PN000pageJoint30` lo hace por **parenting directo de Transform**, sin `BoneFollower`: un hueso Mecanim ya es un Transform de la jerarquía; un hueso Spine no (vive dentro del `Skeleton` y necesita un componente puente, como sí se usa en `Kami.prefab` — ver `spine-kami.md`).

`Assets/Scripts/Hoja.cs` vive en el GO `HojaCaraAbajo` (instancia de `Hoja.prefab` dentro de `HojaMaster`). `HojaIdleStart()` la dispara un Animation Event del clip de Mecanim cuando el giro llega a idle: fira `Evento.OnPageFinishTurning` y se autodestruye.

## Flujo de eventos de un page-turn

1. `Evento.OnPlayerPressedE` → `PageScrollerManager.TriggerPageScroll` (gateado por `esferaNext.triggerBool`/`esferaPrev.triggerBool`, o sea, el player ya tiene que estar parado en el trigger de borde). Ignores the press during a dialogue or a cutscene (`inDialogue`/`inCutscene`, #168), and a teleport no longer leaves a sphere's flag on (`TriggerScript.ExitAll`, see gotcha 7 in `controles-y-gamepad.md`).
2. `ChangeToNextPage`/`ChangeToPrevPage`: incrementa `activePageIndex`, dispara `Evento.OnPageTurnStart(activePageIndex, isNext)` — lo consume `PlayerPageSpawnManager.SetPlayerTargetPosition` (sólo precalcula y guarda `targetPos`, no mueve todavía).
3. `CerrarPaginaCoroutine` (tras `delayTime`): `LevelManager.inDialogue = true` (freeze), `CreateHoja(isNext)` instancia `HojaMaster`/`HojaMaster_Rev`, y engancha a Kami al borde (`StartPlayerRide`, ver abajo).
4. `AbrirPaginaCoroutine` (tras `popupDelayTime`): dispara `Evento.OnNewPageOpen` (consumido por `PlayerPageSpawnManager.PlacePlayerInNewPage` y `PositionMarker.OnChangePage`), abre PUBs, activa la carpeta de la nueva página.
5. Cuando la hoja termina de girar, `Hoja.HojaIdleStart()` dispara `Evento.OnPageFinishTurning` → lo consumen `PageScrollerManager.FinishTurning` (apaga `inDialogue`, restaura cámara, destruye la hoja) y `PlayerPageSpawnManager.FinishRide` (suelta a Kami, ver abajo).

El reposicionamiento "de siempre" (`PlayerPageSpawnManager.PositionPlayerAtPoint`: deshabilita el `CharacterController`, setea `transform.position`, lo rehabilita) sigue siendo la única fuente de verdad sobre dónde termina Kami — el enganche a la hoja es puramente visual durante la transición.

## PositionMarker: cómo se mide "dónde está Kami sobre el borde"

`Assets/Scripts/PositionMarker.cs`, colgado del prefab `esferaPrev`/`esferaNext` (los triggers de borde), no de Kami. Mientras el player está parado en el trigger (`TriggerTurnPage.OnTriggerStay`), copia **solo la Z** de `player.transform.position` sobre su propio transform (X/Y quedan fijos en el borde del libro), y usa la distancia resultante para un alpha `_powerFactor / distancia²` (indicador visual de proximidad). Esta es la convención de "posición sobre el borde" que reutiliza el enganche de Kami: Z es el eje que corre a lo largo del filo del libro, X/Y son la posición "de salto" a la página siguiente (confirmado en `PlayerPageSpawnManager.GetProjectedPositionInNewPage`: sólo `desiredX`/`spawnY` cambian entre páginas, `playerCurrentPosition.z` se preserva tal cual).

## Level 2: a different newspaper on every page and sheet (2026-10-22)

Level 2's `HojaMaster Newspaper` / `HojaMaster_Rev Newspaper` carry a `PageTurnSheet` and the book a `BookPageSpread`, so each page shows its own two
newspaper halves and each turning sheet carries the page it lifts and the one it lands as. Level 1's `HojaMaster`/`HojaMaster_Rev` are untouched.
Detail, and how the sheet faces were fitted to the skinned mesh: "The book is a real newspaper" in `nivel2-y-ui.md`.
Fact worth knowing: each sheet is two skinned meshes with identical UVs and opposite winding (HojaOriginal = up when a turn ends,
PORFAVORANDA2 = up when it starts), animated by `Take 001` then held by `HojaIdle(2)`; the backward prefab is the same rig with its root unrotated.

## Kami pulls the page first (spec 024 3.C, 2026-11-07, not played)

A turn started at an edge sphere plays `PullSolapas` (`Player.PlayPullSolapa`, which cancels an attack in flight) and both
turn delays grow by its real length (`PageScrollerManager._pullBeforeTurn`, measured at runtime, so a re-export can't make it
drift). Forced turns (`TurnToNextPage`) never pull.

## Page turns forced from code (2026-09-25)

`PageScrollerManager.TurnToNextPage(bool ridePage)` runs the same next-page flow without Kami on
the edge sphere (the page 3 arrest ends with it). `ridePage = false` skips hanging Kami from the page
edge (she is hidden "inside the patrol car"). `PlayerPageSpawnManager.OverrideNextPlacement(pos)` is
a one-shot: the next turn drops Kami exactly there instead of the projected page entry, and since it
goes through the normal placement it also becomes her respawn point.

## RidingPage: Kami enganchada al borde durante el giro

Mecanismo (agosto 2026): mientras la hoja gira, Kami queda visualmente agarrada al hueso `PN000pageJoint30` en vez de congelada en el aire. Sin IK — se mueve el `transform` raíz de Kami entero (la animación "ala delta" ya viene autoposicionada con las manos en el borde).

- **`Hoja.cs`**: expone `EdgeBone` (busca `PN000pageJoint30` recursivamente en su jerarquía, con warning si no lo encuentra — robusto a la estructura de prefabs anidados sin depender de wiring manual).
- **`PageScrollerManager.CerrarPaginaCoroutine`**: justo después de `CreateHoja`, llama `Player.StartRidingPage(edgeBone, isNext)`.
- **`Player.cs`**: `StartRidingPage` calcula y guarda el offset Z entre la posición de Kami y el hueso en ese instante (misma lógica que `PositionMarker`), deshabilita el `CharacterController`, entra a `PlayerState.RidingPage`. Un `LateUpdate` (mismo patrón que `BoneFollower`/`SpineBoneTipFollower`: correr después de que el Animator de la hoja actualizó el hueso ese cuadro) reposiciona a Kami en `bonePos + offsetZ + rideRootOffset` (offset tuneable en el inspector, por si la animación no calza exacto con el pivot del hueso) todos los cuadros mientras dure el enganche.
- **`PlayerModel.Tick`**: retorna inmediato si `Player.IsRidingPage` — evita que la gravedad se siga integrando con el `CharacterController` deshabilitado (si sólo se zereaba el input, como hace `IsPullingSolapa`, la velocidad vertical acumulada explotaría en una caída brusca al soltarse).
- **`PlayerView.cs`**: al entrar a `RidingPage` encadena `jump` (mix instantáneo, `animMix.jumpMixDuration`) → `RidingPage` (constante `ANIMATION_RIDING_PAGE`, mix configurable `animMix.changePageJumpToRideMix`, default 0.5s) con `SetAnimation` + `AddAnimation(delay=duración del jump)` — así Kami "salta" a la hoja antes de quedar rideándola. Fallback-con-warning a `Idle` si el skeleton no trae la anim (en el skeleton activo, Atlas 11, se llama `RidePage`). `ForceFacing(bool)` fija el flip explícito (a diferencia de `SetFacing`, que sólo reacciona a input crudo) y ahora también sincroniza el flip de las sprint particles (`SyncSprintParticlesFlipToFacing`, ver `audio-y-particulas.md`).
- **`PlayerPageSpawnManager.cs`**: `PlacePlayerInNewPage` (en `OnNewPageOpen`) se vuelve no-op mientras `IsRidingPage` es true — la colocación autoritativa final (`FinalizePlacement`, la misma de siempre) se aplica recién en `FinishRide` (suscripto a `OnPageFinishTurning`), que también suelta a Kami (`StopRidingPage`).

**Confirmado (agosto 2026)**: flujo completo probado en el editor (jump→ride, flip, sprint particles) — funciona en ambos sentidos. Si `HojaMaster_Rev` no tuviera la cadena `PN000pageJoint30`, `Hoja.cs` avisa con warning y Kami no se engancha en ese sentido, sin romper el resto.


## Followers on page turns (spec 012)

`Level2/PageRideFollowers` (Level 2 scene root) polls `Player.IsRidingPage`: on the rising edge every `isFollowing` NPC
grabs a seat around Kami (the `PaperPlaneRide` technique), on the falling edge it `Warp`s next to her on the new page's
NavMesh and follows again (or parks and retries with a warning if there is no mesh near her). Polling is on purpose:
the order of `OnPageFinishTurning` subscribers is not guaranteed and `FinishRide` must place Kami first. Forced turns
with `ridePage: false` (the arrest) never set `IsRidingPage`, so nobody boards. Needs a baked NavMesh near each page's
entry point. Seats and timings are Inspector fields; detail in `specs/012-page-turn-followers/spec.md`.

**Landing spots (#171, 2026-10-13)**: only toward the middle of the page (`_landingSpacing` x slot, then twice that), never
outward: Kami lands at the book's edge, and the floor's NavMesh continues past the book's invisible walls, so an outer spot
sampled fine and stranded the follower outside. A spot is taken only if `NavMesh.CalculatePath` from it to Kami's own
NavMesh point is `PathComplete`; if neither is, the follower lands on Kami's point. After landing its follow side points at
the page centre, and the follow spot itself never falls past the page's edge (#246, see "Following changed" in `quests-y-dialogos.md`).

## Paper page turns (spec 025, built 2026-11-07, not played yet)

With `PageScrollerManager._paperPages` on (both level scenes), a turn no longer instantiates `HojaMaster`: `PaperTurn`
(scene root object `PaperTurn`, referenced by `_paperTurn`) runs it. Off = the old flow, untouched. Spec:
`specs/025-paper-book/spec.md` (Part A).

- **The sheet** is procedural (`Book/Paper/PaperSheet`, prefab `Prefabs/Book/PaperSheet.prefab`): an inextensible strip
  on `Book/Paper/Core/PaperCurl` that rests exactly on the measured book surface (gutter dip included) at the start and
  the end, so nothing jumps; it curls like the old clip (fitted by `tools/fit-paper-curl.py`). Its faces use the book's
  own half materials and mesh UVs; Level 2's newspaper is dressed by `BookPageSpread.DressPaperSheet`. Kami hangs from its
  `Edge` as before (`StartRidingPage`); it raises `OnPageFinishTurning` itself after `curl.duration` (3.33 s).
- **Page objects never move.** At the sheet's start (`delayTime` after the press) both pages' renderers are copied into
  draw-only proxies (`Graphics.DrawMesh`, `PaperPieces`), the real ones get `forceRenderingOff`, and at the end the proxies
  go and the real ones show again. Page activation, `OnNewPageOpen` and the NavMesh swap keep their old timing; only page
  N's `isShrink` is skipped. Spine characters on the page arriving and skinned meshes of an inactive page can't be copied:
  they appear when the turn ends (logged as "hidden only").
- **Pieces** (`PaperPieces.Group`): a `PagePiece` starts a piece; otherwise a child under the page folder is a piece unless
  it spans more than `maxPieceSize`, then it is split into its children. Classes (`PaperPieces.Classify`, or the
  `PagePiece`): Card (sprites and thin props, fall backwards away from the camera), Volume (tip outward on the outer base
  edge, squash at the end), Rider (sits on another piece, moves with it), Flat (captured from above into textures drawn on
  the sheet and on the halves, so roads bend with the paper), Hidden, Excluded. Kami and following NPCs are never touched.
- **Folding** (`Core/PaperTurnModel`, `Core/PaperFold`): each piece leans just enough that its bounds stay under the other
  page of its spread, which counts as `pageThicknessShare` of its tallest piece thick (so facing pop-ups fold in step).
  The test points are the box's outline sampled along its edges (`SampleOutline`), not only its 8 corners, and only the
  part on the piece's own half (from 0.5 outward): with corners alone the sheet passed through the middle of a wide
  building (page 3's, 2026-11-07).
- **Renderers that appear mid-turn** (spawned cars, objects switched on when the arriving page opens) are hidden until the
  turn ends (`PaperTurn.HideLateRenderers`, every frame): they have no proxy and would stand still on the folding paper.
- **Tuning** lives in `Assets/Data/PaperBook/PaperBook_Level1|2.asset` (`PaperBookData`): the curl (lead, shape, duration,
  `heightScale` < 1 flattens the curl like the old sheet), thresholds of each class, `pageThicknessShare`, `sheetLift`.
- **Debugging**: `PaperTurn`'s Debug header (pause, speed, scrub slider, class toggles, gizmos, `_logPieces`), F7 pause /
  F8 slow motion in the Editor. Every turn logs `[PaperTurn] page a -> b: N pieces (...)`, what could only be hidden
  (static batched, inactive Spine), what could not fit, and `cleaned up: N renderers shown again`.
- **Batching Static is off for page props** (`tools/unbatch-page-props.py`): a statically batched renderer reports the
  combined mesh and can't be copied. Keep new page props off Batching Static (the tagging tool warns).
- **Checks from outside Unity**: `python tools/run-paper-tests.py` (the C# core against the Python reference
  `tools/paper_core.py`), `python tools/preview-paper-turn.py` (side views). In Unity: Test Runner > EditMode >
  `PaperBookTests`; **Kami > Pages > Tag Page Pieces - Report** shows how a page folder will be grouped.
