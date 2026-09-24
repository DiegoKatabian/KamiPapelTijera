# Origami (canvas de costo) y tooltips/post-its

## What an "Origami route" actually is (clarified 2026-09-22, spec 006 task 0.A)

Not a ScriptableObject — a **prefab**. `Origami` (abstract `MonoBehaviour`) sits on the
root of a UI prefab and holds an ordered array of `OrigamiRoute` components, each one a
single drag-path (start/route/end `RectTransform`s). `MultipleRectCheck` plays them
forward from index 0; when the last one completes, `Apply()` runs. Different
on-completion behaviors are different `Origami` subclasses (`OrigamiEventTriggerer`
fires an `Evento`, `OrigamiObjectSpawner`/`OrigamiShip` activate a GameObject) — never a
change to the shared base. There is **no "play a route backwards" concept** — folding
and unfolding the same object (e.g. Abuela) are two separate prefabs
(`OrigamiRoute AbuelaFold.prefab` / `OrigamiRoute AbuelaUnfold.prefab`), only one active
in the scene at a time, toggled via `QuestEffector`/`GameObjectActivator`.

**New**: `OrigamiTextReveal` (`Assets/Scripts/Origami/OrigamiTextReveal.cs`) — a subclass
of `OrigamiEventTriggerer` that also shows localized text on completion, for the Level 2
café wrapper (`OrigamiRoute_Cafe_Fold`/`OrigamiRoute_Cafe_Unfold`) and trap letter
(`OrigamiRoute_Letter`) — see `specs/006-nivel2-detective-natalia/spec.md` FR-005.
`OrigamiTextRevealDisplay` (sibling to `PedestalCanvasDisplay`, same CanvasGroup-fade
pattern) is the panel it drives, routed through `LocalizedText.Escribir` for
joystick/keyboard prompts and concept-highlighting. `Origami.cs`/`MultipleRectCheck.cs`/
`PedestalCanvasDisplay.cs` themselves were not touched — purely additive.

**Reading flow (Diego's design call, 2026-09-22)**: the text appears on the LAST fold, and
the panel does **not** close on its own. Input is ignored for `_readDelay` seconds (default
2), then a close prompt appears (`OrigamiReadClose` in `UITexts`, written as
`{INPUT:accion} to close` so the icon matches the active device) and Interact dismisses it
— the same read-then-continue rhythm as a dialogue line. The delay exists for a concrete
reason: the button that completes the fold is the same button that dismisses, so without it
the player's own last press would eat the text they just earned (the same class of
same-frame input collision as issues #41.2/#41.14).

**Placeholder art (Diego's call, 2026-09-22)**: the café wrapper is a **2-step fold using
`OSU-Avion`** (the paper plane, mirroring `OrigamiRoute Avion`) and the trap letter is a
**1-step fold using `OSU-Puente`** (the puentecito, mirroring **`OrigamiRoute 1-Easy`** —
Nivel 1 page 2). `OrigamiRoute_Cafe_Unfold` lists the same two folds as `_Cafe_Fold` in
reverse order, which is exactly how `AbuelaUnfold` expresses "unfold" against `AbuelaFold`.
Real `OSU-Cafe`/`OSU-Letter` art from Valentino replaces these later. `_textDisplay` on both
text-reveal prefabs is unassigned by design (scene-specific placement).

**Careful — two different "bridge" routes exist.** `OrigamiRoute 1-Easy` is the live
puentecito: 1 fold, `OSU-Puente` art, used in Nivel 1 page 2. `OrigamiRoute Puente.prefab`
is a separate 4-fold route that Diego believes was scrapped — do not use it as the reference
for "the bridge origami", and check with him before building anything new on it.

**Gotcha found while building these**: `MultipleRectCheck.StartOrigami()` never calls
`NextRoute()`, so the `m_IsActive` flags authored on the OSU instances decide which fold is
visible on a *fresh* start (`FailOrigami` only corrects it after a cancel). Whichever route
sits at `origamiRoutes[0]` must be the active one, or the first fold of a new route shows the
wrong art.

## Canvas de costo en pedestales de origami

`PedestalCanvasDisplay` (`Assets/Scripts/Origami/PedestalCanvasDisplay.cs`) va montado en el GO "Canvas" hijo de `Assets/Prefabs/OrigamiRoutes/PedestalParent.prefab`. Muestra costo de papel + ícono cuando el player pisa el trigger del sello.

**Regla de oro:** el canvas queda SIEMPRE activo; la visibilidad es solo alpha del CanvasGroup (arranca en 0). Nada de SetActive.

**Flujo:**
- Player entra al trigger → `TriggerOrigami.OnEnterBehaviour` llama `ShowCost(origami.paperCost)` en AMBAS ramas (tenga o no papel suficiente: cuando no le alcanza es cuando más le sirve verlo) → fade-in.
- `OnOrigamiStart` → fade-out mientras dura el minijuego.
- `OnOrigamiEnd` → re-muestra el costo solo si el player sigue en el pedestal y el origami no fue usado (`origami.wasUsed`).
- Player sale del trigger (o muere: `OnPlayerDie` fuerza el exit de TODOS los TriggerOrigami) → `Hide()` con fade-out. `Hide()` sobre un canvas ya invisible es no-op (evita ~12 logs/corrutinas por muerte).

**Cross-talk:** `OnOrigamiStart`/`OnOrigamiEnd` son eventos globales — los reciben los ~12 sellos del nivel. El flag `_playerOnPedestal` hace que solo reaccione el pedestal donde está parado el player. Ojo: `HandleOrigamiStart` NO apaga ese flag (el player sigue físicamente en el trigger).

**Wiring:** `TriggerOrigami` (`Assets/Scripts/TriggerS/TriggerOrigami.cs`) tiene el campo `_canvasDisplay`; los 5 `SelloOrigami*.prefab` wirean `_triggerOrigami` del canvas vía referencias stripped. Ambos scripts tienen fallback por búsqueda en jerarquía con warning si la ref falta.

**Tuneable en inspector:** `_fadeInDuration` / `_fadeOutDuration` (0.3s cada uno).

**Historia:** se ELIMINÓ el viejo `OrigamiPaperCostTextUpdater` del prefab — escribía "actual/costo" y pisaba el texto del canvas. El script sigue en `Assets/Scripts/UI/TextUpdater/` pero ya ningún prefab lo usa.

**Excepción por diseño (decisión de Diego, 31/7):** en `SelloOrigami AbuelaFold.prefab` los sellos de la abuela NO muestran ni la base 3D del pedestal ni el canvas de costo — solo partículas y post-it. Se logra con overrides `m_IsActive: 0` sobre el GO Canvas (target `601727638938904023`) y sobre el GO de la base (target compuesto `634737019527280759`, que es el Pedestal.prefab anidado visto desde PedestalParent). `PedestalCanvasDisplay.ShowCost/Hide` tienen guard para canvas inactivo: es una config soportada, loguea y no explota.

**Debug:** logs `[PedestalCanvasDisplay]` (incluyen el nombre del pedestal padre) y `[TriggerOrigami]`.

## Tooltips / post-its

Arquitectura: `TooltipManager` orquesta (localiza el texto vía `TooltipTable` y elige el `PostIt` por color); cada `PostIt` maneja SU PROPIO ciclo de vida (fade-in, timer de muerte, fade-out). El bug histórico era un único `killAllPostitsTimer` global compartido entre corrutinas que se pisaban entre colores — ya no hay timers en el manager.

### TooltipManager (`Assets/Scripts/UI/TooltipManager.cs`)

- `ShowTooltip(text, PostItColor)` — muestra el color pedido; si ya estaba visible reinicia su timer sin parpadeo.
- `HideTooltip()` — esconde todos; `HideTooltip(PostItColor)` — esconde SOLO ese color (lo usan los triggers al salir).
- Kill time POR COLOR en inspector: `naranjaKillTime: 5`, el resto 2. `killTime` (2) queda como fallback global si un color está en 0/negativo.

### PostIt (`Assets/Scripts/UI/PostIt.cs`)

Prefab: `Assets/Prefabs/UI/PostIt.prefab` (tiene CanvasGroup; fallback AddComponent con warning). Los 5 post-its de la escena son instancias de este prefab.

- API: `Show(killTime)` / `Hide()` / `IsVisible`. Fades por CanvasGroup; `Hide()` sobre uno escondido es no-op; el texto se limpia recién al final del fade-out.
- El GO queda SIEMPRE activo después del primer Show; el "apagado" es alpha 0 (`SetActive(false)` mataría las corrutinas de fade/timer).
- Flags de dismiss por input (inspector; overrides de escena en `Main Canvas.prefab`, no en la
  escena — son post-its ANIDADOS: la escena solo overridea lo que difiere del prefab de "Main
  Canvas", así que estos valores viven en las modificaciones de la instancia anidada de PostIt
  dentro de ESE prefab, no en `Nivel1_KamiPapelTijera.unity` directamente):
  - `_dismissOnAttack` — polling de `Input.GetButtonDown("Fire1")` en Update (cubre clic izq + LCtrl). Es polling porque `Evento.OnPlayerPrimaryClick` existe en el enum pero NADIE lo triggerea (PlayerController llama `Player.OnPrimaryClick()` directo). Respeta el gate `LevelManager.agency`.
  - `_dismissOnInteract` — `Evento.OnPlayerPressedE`.
  - `_dismissOnJump` — `Evento.OnPlayerPressedSpace`.
  - `_showOnEnable` — compat para el 6º post-it "TOOLTIP PAPER SALTO", que `Player.cs` prende/apaga con SetActive directo (`nuevoTooltipPapelSalto`), FUERA del array del manager: se muestra al activarse, sin timer de muerte.
  - Confirmado por lectura directa de `Main Canvas.prefab` (septiembre 2026): de los 5 colores,
    solo dos tienen algo prendido. **PostItAmarillo** tiene los 3 flags (`_dismissOnAttack`,
    `_dismissOnInteract`, `_dismissOnJump`) — se cierra con cualquier acción del player.
    **PostItBlanco (Ex-Azul)** tiene solo `_dismissOnInteract`. Naranja, Rosa y Verde no tienen
    ninguno prendido: solo se esconden por `HideTooltip()`/su propio kill time.

### Gotcha: `HideTooltip()` global llamado desde afuera del sistema de tooltips

`TooltipManager.HideTooltip()` (sin color) esconde los 5 post-its de una — lo usa código que
necesita "limpiar la pantalla" sin saber qué color está mostrando qué en ese momento. Hoy el único
caller externo es `Player.DestroyPaperPlaneHat()` (sombrero de papel / salto aumentado), pensado
para dispararse UNA vez al terminarse los saltos aumentados.

**Bug real (issue #41.13, septiembre 2026):** `PlayerModel.UpdateVerticalState()` llamaba a
`DestroyPaperPlaneHat()` sin chequear `Player.isPaperPlaneHat`, solo `augmentedJumpsLeft == 0` —
condición que queda `true` PARA SIEMPRE una vez agotado el sombrero por primera vez (nada la vuelve
a subir salvo agarrar el sombrero de nuevo). Resultado: `DestroyPaperPlaneHat()` (y por lo tanto
`HideTooltip()`) se disparaba en CADA frame grounded del resto de la sesión, y CUALQUIER tooltip
que se mostrara después se cerraba casi instantáneamente — el síntoma reportado ("los tooltips se
abren y se cierran insta") no tenía relación aparente con el sombrero de papel. Fix: guard
`_player.isPaperPlaneHat &&` antes del chequeo, en `PlayerModel.cs`.

**Si vuelve a aparecer este síntoma** ("ningún tooltip abre", sin importar el color/trigger),
sospechar primero de algo que esté llamando `HideTooltip()` sin color más seguido de lo pensado
(un `Update()`/`Tick()` sin guard de estado), no del sistema de tooltips en sí — el propio
`TooltipManager`/`PostIt` no tienen ningún timer ni loop que pueda causar esto por su cuenta.

### Triggers (`Assets/Scripts/TriggerS/TriggerScript.cs`)

- `TryShowTooltip(forceShow)` es el ÚNICO camino para mostrar: encapsula el gate `showTooltip`, el límite de muestras y el show. `forceShow` saltea el flag pero nunca el límite.
- `_maxTooltipShows` (0 = sin límite; el contador no persiste entre sesiones). `TriggerText.isOneTimeOnly` es legacy = `GetMaxTooltipShows() => 1` (gana el más restrictivo).
- `OnExitBehaviour` esconde SOLO su color y SOLO si esa entrada llegó a mostrar algo (flag `_shownThisEntry`) — antes el exit de cualquier trigger mataba post-its ajenos vivos.

**Debug:** logs `[TooltipManager]`, `[PostIt]` (con el nombre del GO) y `[TriggerScript]`.
