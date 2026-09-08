# Origami (canvas de costo) y tooltips/post-its

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
