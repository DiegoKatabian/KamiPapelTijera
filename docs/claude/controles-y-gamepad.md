# Controles: teclado, mouse y joystick

Issue #41, spec en `specs/004-joystick-controls/spec.md`. Branch `feature/joystick-controls`
(sale de `feature/spine-animations`).

**Regla base**: teclado/mouse y joystick funcionan **al mismo tiempo**, siempre. No hay
"modo joystick" que apague el teclado. El único lugar donde el device activo cambia algo
es la *presentación* (qué botón dice el tooltip, si se dibuja el cursor virtual del
origami) — nunca qué se puede hacer.

## Mapeo final

| Acción | Teclado / mouse | Joystick (layout Xbox) |
|---|---|---|
| Mover | WASD / flechas | stick izquierdo |
| Saltar | Espacio | **A** (botón 0), contextual |
| Interactuar (hablar, solapas, pasar página, origami) | E, Enter | **A** (botón 0), contextual |
| Atacar / cortar | Click izq, Ctrl | **B** (botón 1) |
| Correr | Shift | **L1** (botón 4) |
| Cambiar cámara | Click del medio | **L2** (9no eje) |
| Menú Flap | Esc, O | **Start** (botón 7) |
| Inventario / Quests directo | I / U | — (se llega por el menú con Start) |
| Mutear | M | — |
| Navegar UI | flechas / mouse | stick izquierdo; **A** = Submit, **Start** = Cancel |
| Cambiar de tab dentro del Flap (issue #41.1) | — (click en la solapa) | **R1** (botón 5) / **L1** (botón 4) |
| Cerrar el Flap (issue #41.1) | Esc, O (toggle) | **B** (botón 1), contextual — ver abajo |

### El botón A es contextual (lo más importante de este diseño)

Un solo botón para "hacer": si hay algo con qué interactuar, interactúa; si no, **salta**.
Así un chico no tiene que aprender cuál botón es cuál, y A queda donde todo el mundo espera
que esté el botón de confirmar.

Quién decide: `PlayerController.CheckControls()` pregunta a
`InteractionContext.HayInteraccionDisponible`. Si da `true`, el apretón de A se resuelve
como interactuar **y se le tapa el salto de ese frame** (`_gamepadInteractua`): es el mismo
botón físico, no puede hacer las dos cosas.

Consecuencia aceptada: parada adentro de un trigger interactuable (una solapa, el borde de
página), Kami **no salta con A**. Con el teclado sí, porque el espacio no pasa por esta
lógica.

B ataca siempre, sin contexto. Durante el origami Kami está en `PlayerState.Casting` y
`CanAttack()` da false, así que ahí B queda libre y lo usamos para **cancelar el
minijuego** (es la convención de "volver" de cualquier joystick).

**El teclado NO pasa por esta lógica**: E siempre interactúa, el espacio siempre salta y el
click siempre ataca, exactamente como antes. Es una regla de oro de este feature — cero
regresiones en lo que ya funcionaba.

### Pausa del Flap: NINGÚN input de gameplay se procesa (issue #41.3)

Mientras `FlapManager.IsMenuOpen` es `true` (mismo instante en que `Time.timeScale` pasa a
0 — ver `FlapManager.MoveFlap`), `PlayerController.CheckControls()` calcula
`menuAbierto = FlapManager.Instance.IsMenuOpen` y lo usa para saltear por completo: el
interactuar en el mundo (`InputHub.InteractDown` → `OnPlayerPressedE`, que antes disparaba
origamis/solapas con A/E aunque el menú tapara la pantalla), el ataque (teclado Y gamepad —
antes el teclado no pasaba por NINGÚN gate de pausa) y el bloque final de movimiento/salto
(mismo `return` que ya usaba `LevelManager.inDialogue`). Esc/O/I/U siguen andando (no pasan
por este gate) porque son justamente lo que abre y cierra el propio Flap.

Ojo: el gate se resuelve leyendo `FlapManager.Instance.IsMenuOpen`, NO
`LevelManager.agency` ni `LevelManager.inDialogue` — ninguno de los dos lo prendía al abrir
el Flap (se investigó explícitamente, `agency` nunca se setea en código y `inDialogue` es de
diálogo/overlay/page-turn). `IsMenuOpen` es la fuente de verdad porque es el mismo flag
(`_isOpen`) que decide cuándo `Time.timeScale` pasa a 0.

## Arquitectura

### `Assets/Scripts/Input/InputHub.cs` — la fachada

Clase **estática**. Es el ÚNICO archivo que conoce los nombres de los ejes del Input
Manager. Todo el resto pregunta por acciones (`InputHub.SaltoDown`,
`InputHub.InteractDown`, `InputHub.CambiarCamaraDown`…). Si mañana cambia un botón, se
cambia acá y en `ProjectSettings/InputManager.asset`, y nada más.

Cosas no obvias que resuelve:

- **`Input.GetButtonDown` TIRA EXCEPCIÓN si el eje no existe** en `InputManager.asset`.
  Como los ejes nuevos viven en un archivo de ProjectSettings (que se puede perder en un
  merge o quedar viejo), todas las lecturas van por envoltorios `*Seguro` que atrapan la
  `ArgumentException`, avisan **una sola vez por eje** y devuelven "no apretado". El juego
  sigue andando con teclado en vez de reventar 60 veces por segundo.
- **L2 no es un botón, es un eje analógico**: el "recién apretado" se detecta a mano con
  histéresis (`0.5` para apretar, `0.3` para soltar) y se cachea por frame, así dos
  consultas en el mismo cuadro no se comen el flanco entre ellas.
- **`UltimoDeviceFueJoystick`**: qué device usó último el jugador. Barre
  `KeyCode.JoystickButton0..19` + sticks; cualquier tecla o movimiento de mouse devuelve
  el foco al teclado. Se usa SOLO para presentación.
  **Ojo con una trampa que ya nos mordió**: para preguntar "¿se movió el stick?" hay que
  usar los ejes `JoystickDetectX`/`JoystickDetectY`, que leen SOLO el joystick. Los
  `Horizontal`/`Vertical` normales **mezclan WASD con el stick**, así que caminar con el
  teclado contaba como input de joystick y el juego se quedaba convencido de que estabas
  con joystick para siempre (los textos mostraban botones en vez de teclas).
- **`OnDeviceCambio`**: evento estático que salta cuando el jugador cambia de device. Lo
  escuchan los textos de la UI y los prompts visuales: sin esto, un tooltip escrito con
  teclado se quedaba diciendo "E" aunque después agarraras el joystick.

### `Assets/Scripts/Input/InteractionContext.cs` — "¿hay algo con qué interactuar?"

Registro estático que se llena solo. Cada `TriggerScript` que declara
`EsInteractuable => true` se anota al entrar el player (`OnEnterBehaviour`) y se borra al
salir (`OnExitBehaviour`), con red de seguridad en `OnDisable`/`OnDestroy` (hay triggers
que se destruyen o se apagan con el player adentro: pickups consumidos, cambio de página,
quests). Además cuenta como "hay interacción" `LevelManager.inDialogue`, que tapa diálogo,
cambio de página y overlay de derrota (`OverlayManager.Lock()` lo prende).

Se vacía en cada `sceneLoaded` — es estático y si no se quedaría con triggers de la escena
anterior que ya no se van a desregistrar.

**Triggers que hoy declaran `EsInteractuable => true`**: `TriggerDialogue` (y por herencia
todos los de NPC), `TriggerSolapa`, `TriggerBarquito`, `TriggerTijeraPickup`,
`TriggerOrigami`, `TriggerTurnPage`. El default de la clase base es `false` porque la
mayoría de los triggers son de zona o de tooltip y no responden al botón.

**Si agregás un trigger nuevo que reacciona al botón de acción, acordate de overridear
`EsInteractuable`.** Si no, el botón A va a saltar en vez de interactuar ahí.

### `Assets/Scripts/Input/GamepadCursor.cs` — cursor virtual del origami

El minijuego de origami nació para mouse (arrastrar la flecha verde hasta el círculo
rojo). Con joystick hace falta un puntero: `GamepadCursor` es un cursor movido por el
stick izquierdo que reporta su posición **en píxeles de pantalla**, la misma unidad que
`Input.mousePosition`. Así `MultipleRectCheck` no se entera de quién mueve el puntero.

Se **autoconstruye entero por código** (GameObject + Canvas Overlay + Image + sprites,
`DontDestroyOnLoad`): no hay nada que arrastrar en el inspector. Requisito duro de este
feature — todo tiene que andar al darle Play, sin tocar escenas ni prefabs. Reusa las
texturas de `CursorManager` (mano abierta/cerrada) y si no las encuentra genera un anillo
por código.

### `Assets/Scripts/Input/InputPromptSystem.cs` — qué botón dice el texto

Traduce los textos de la UI al device activo. Ver la sección "Prompts" más abajo.

## Origami con joystick

- **Kami ya queda quieta sola**: `Evento.OnOrigamiStart` → `Player.StartOrigamiCast` →
  `PlayerState.Casting` → `PlayerModel.IsInputLocked()` bloquea el movimiento. No hizo
  falta agregar ningún gate nuevo; el stick mueve el cursor y no a Kami.
- **A abre el minijuego y A agarra la flecha.** Son el mismo botón, así que
  `MultipleRectCheck.Update()` corta el frame (`return`) después de `StartOrigami`: sin
  eso, el apretón que abre agarraría en el mismo cuadro y un toque corto terminaría en
  "soltaste mal" al instante.
- **Con joystick cancela B**, no A (si cancelara A, el apretón de agarrar cerraría el
  origami). También cancelan E/Enter, soltar fuera de la ruta, o caminar fuera del
  pedestal. El jugador nunca queda trabado.
- **Tolerancia generosa**: `MultipleRectCheck._toleranciaJoystickPx` (default 80 px) agranda
  la zona válida alrededor de la ruta. **Sólo aplica con joystick**: con mouse la ruta sigue
  siendo exacta como siempre. Es un `[SerializeField]` nuevo sobre un prefab que ya existía,
  así que Unity le da el valor por defecto del inicializador de C# sin tocar el prefab.
- Números a tunear en el editor si se siente mal: velocidad del cursor
  (`GamepadCursor.VELOCIDAD_PX_POR_SEGUNDO`, 900 px/s), tamaño (48 px) y la tolerancia (80 px).

## Navegación de UI

El `EventSystem` de las escenas ya venía con `m_SubmitButton: Interact` y
`m_CancelButton: Options`, y esos ejes ahora tienen joystick: **A es Submit y Start es
Cancel sin tocar nada**.

Lo que faltaba: uGUI **no navega con stick si no hay nada seleccionado**, y
`m_FirstSelected` está en 0. Sin selección, el overlay de victoria (que a propósito NO se
cierra con E, sólo con sus botones) dejaba trabado a un jugador de joystick. Lo resuelve
`Assets/Scripts/UI/UISelector.cs`, enganchado en el Flap y en los overlays.

**Cuidado al tocar esto**: si queda un botón seleccionado durante el gameplay normal, el
botón A (que es Submit) lo apretaría mientras el jugador juega. Por eso la selección se
limpia al cerrar menús y overlays, y por eso sólo se selecciona cuando hay joystick.

### Fugas de foco que ya nos mordieron (no repetirlas)

- **`Selectable.Select()` es `EventSystem.SetSelectedGameObject`.** No es "resaltar", es
  *dar el foco*. `CamWheelManager.FakeSelectButton` lo usaba para pintar la cámara activa, y
  como está suscripto a `OnCameraChange`, después de cada cambio de cámara la rueda se
  quedaba con el foco y el siguiente Submit la volvía a apretar sola. Para resaltar sin dar
  foco: escala o swap de sprite (ver `CamWheelButton` y `FlapDisplayButton`).
- **Seleccionar con el menú cerrado.** `FlapManager` seleccionaba en `ShowDesiredDisplay`,
  que se llama *antes* de abrir el flap: quedaba un botón vivo con el menú cerrado y el
  jugador lo apretaba sin querer mientras jugaba. Ahora la selección la hace `MoveFlap` recién
  cuando el menú terminó de abrirse.
- **Un display sin nada navegable deja trabado.** Tareas no tiene botones (los `QuestSlot` son
  texto), así que no había nada que seleccionar y el stick no movía nada. El fallback es
  seleccionar la solapa del propio display, que siempre existe.
- **Navigation = None gana a nivel instancia.** Los tres sliders de settings tenían el override
  `m_Navigation.m_Mode: 0` en `FlapManager.prefab`, así que arreglar `SliderFlap.prefab` no
  alcanzaba. Al tocar navegación, revisar los overrides de las instancias, no sólo el prefab.
- **El color de "seleccionado" tiene que verse.** El de `Button.prefab` era gris 0.9607 sobre
  blanco: invisible. Ahora es ámbar. Y ojo con los overrides por instancia de
  `m_Colors.m_SelectedColor`: hacían que en "¿salir?" quedaran los dos botones pintados.
  **Esta correción NO se había propagado a todo lo navegable** (issue #41.1, septiembre 2026):
  `SliderFlap.prefab` e `InventorySlot.prefab` seguían con `m_SelectedColor` en el mismo gris
  invisible (0.9607 sobre fondo claro) — el ámbar de `Button.prefab` no alcanza porque cada
  Selectable tiene su PROPIO bloque `m_Colors`. Si agregás un Selectable nuevo (o heredás de
  uno viejo), revisá su `m_SelectedColor` a mano, no asumas que "ya está arreglado" porque
  otro prefab lo esté.

### Navegación del Flap: R1/L1 cambian de tab, B cierra (issue #41.1)

`FlapManager` tiene su propio `Update()`, activo SOLO mientras `_isOpen` (osea, solo cuando
hay algo que navegar) — no interfiere con `PlayerController` porque ese ya se auto-gatea
cuando el menú está abierto (ver pausa arriba), así que ningún input de gameplay compite por
el mismo botón:

- **R1/L1** (`InputHub.TabSiguienteDown`/`TabAnteriorDown`, ejes nuevos en
  `InputManager.asset` — R1 no tenía ningún uso, L1 comparte botón físico con Correr pero es
  un eje DISTINTO) ciclan `_flapDisplays` con módulo hecho a mano (`CambiarTab`).
  `ShowDesiredDisplay` guarda `_currentDisplayIndex = flapDisplay.number` en CADA call site
  (BTN_\*, Open\*, y el propio ciclado), así que R1/L1 siempre arrancan desde el tab que el
  jugador está viendo, sin importar cómo se llegó ahí.
- **B** (`InputHub.AtaqueGamepadDown`, mismo eje que ataca en gameplay) cierra el Flap. Es
  contextual con el seguro de "¿salir del juego?": si `_seguroOverlay` está activo, B le
  contesta a ESE dialogo (`BTN_No()`) en vez de cerrar el Flap entero por atrás dejando la
  pregunta sin responder — mismo patrón que "B cancela" en el origami.
- **Colores por tab**: cada una de las 4 solapas (`FlapDisplayButtonQuests/Settings/
  Controls/Inventory`, instancias anidadas del mismo prefab base) ya tenía un
  `m_Colors.m_HighlightedColor` distinto por instancia (override YAML, visible al pasar el
  mouse) — pero NINGUNA tenía `m_SelectedColor` overrideado, así que las 4 se veían del
  mismo ámbar del prefab base al navegar con joystick. Fix: se copiaron los mismos valores
  RGB de `m_HighlightedColor` a `m_Colors.m_SelectedColor` por instancia en
  `FlapManager.prefab` (cirugía YAML, mismos 4 bloques `PrefabInstance`, sin tocar el prefab
  base). Con mouse y con joystick ahora resaltan con el mismo color por sección.

**Ronda 2 (mismo issue #41, testing real con gamepad — ver
`specs/004-joystick-controls/pending-issues.md` #41.10-#41.12 para el detalle completo)**:

- **Botón de la tirita (abrir/cerrar Flap) sin wirear**: el GO "Tirita fondo"
  (`FlapManager.prefab`) es un `Button` real y navegable, pero su `OnClick` estaba vacío y
  `m_SelectedColor` era blanco (igual al normal, invisible al enfocar). Fix: nuevo
  `FlapManager.BTN_ToggleFlap()` (wrapper sin argumentos de `ToggleFlap(params object[])` —
  el Editor no lista para `OnClick` métodos con `params`, así que hacía falta un wrapper de
  0 parámetros) cableado al `OnClick`, más `m_SelectedColor` a ámbar como el resto.
- **Handles de los sliders ("gallinas") sin animar al navegar**: `Chicken Handle.controller`
  (`Assets/2D/UI/Button Animations/`) tiene un estado "Selected" (el que dispara el
  `EventSystem` al navegar con joystick, DISTINTO del "Highlighted" de hover de mouse) cuyo
  clip estaba completamente vacío — un placeholder nunca completado. Fix de una línea: el
  `AnimatorState` "Selected" ahora apunta al mismo clip que "Pressed" (el que sí tiene la
  animación real: escala + spritesheet de la gallina caminando, lo que se ve al arrastrar
  con mouse). Como los 3 sliders comparten este `AnimatorController` por asset, un solo fix
  alcanza a Brillo/Contraste/Volumen.
- **Cartel nuevo "L1/R1 para cambiar de sección"**: GO `FlapTabHint` en `FlapManager.prefab`,
  arriba de la columna de íconos, visible SOLO con joystick vía el componente nuevo
  `SoloConJoystick.cs` (`Assets/Scripts/UI/`, `OnEnable`/`OnDeviceCambio` togglean
  `SetActive`, mismo patrón que `TutorialPromptVisual`). Usa el placeholder nuevo
  `{INPUT:cambiarseccion}` (ver abajo) y la key de localización `FlapCambiarSeccion`
  (`UITexts`, en es/en/pt).

## Prompts de botones en los textos

Los textos del juego decían "Press E to talk", "Mantené SHIFT para correr", etc. Con un
joystick en la mano eso es información falsa. `InputPromptSystem.Procesar(texto)` corre
sobre los textos de tooltips y diálogos:

1. **Placeholders `{INPUT:*}`** (`{INPUT:saltar}`, `{INPUT:accion}`, `{INPUT:correr}`,
   `{INPUT:camara}`, `{INPUT:menu}`, `{INPUT:mover}`, `{INPUT:cambiarseccion}` — este último
   agregado en la ronda 2 del Flap, resuelve a "L1 / R1" con joystick y a `""` con teclado
   porque ese eje no tiene bind de teclado, ver más abajo) — el camino limpio para textos
   nuevos. Funcionan siempre, con cualquier device.
2. **Tokens legacy** (SHIFT, ESPACIO, Click, "Press E"…) — se traducen **sólo si el jugador
   está usando joystick**, para no tener que editar las tablas de localización (que son
   contenido de Diego/Valentino). Con teclado el texto sale idéntico a hoy.

Hoy los prompts de joystick son **texto** (`(A)`, `(B)`, `(L1)`…), no íconos: falta el
atlas de sprites de botones (dependencia de arte). El código está escrito para que pasar a
`<sprite name=button_A>` de TextMeshPro sea cambiar una sola tabla.

### El tab Controles del Flap YA se traduce solo (issue #41.6)

El contenido de esa solapa es UN solo bloque de texto localizado (tabla `UITexts`, clave
`controlsText`), formato "TECLA - Acción" por línea ("WASD - Mover", "E - Interactuar"...),
escrito por un `LocalizeStringEvent` de Unity Localization cableado directo a
`TMP_Text.set_text` — no por ningún componente propio del juego. Investigado en septiembre
2026 (issue #41.6) y confirmado que YA funciona sin tocar nada nuevo, por dos piezas que ya
existían:

1. `LocalizedText.cs` (`Assets/Scripts/UI/LocalizedText.cs`, commit `94a1196`, previo a esta
   sesión) engancha por código TODOS los `LocalizeStringEvent` de la escena (incluido el de
   Controles) con un listener extra que re-procesa el texto vía `InputPromptSystem.Procesar`
   y lo registra para reescribirlo en `InputHub.OnDeviceCambio`. Es la razón por la que el
   comentario de esa clase menciona textualmente "la pantalla de controles del Flap".
2. `InputPromptSystem.RxTeclaEnListaDeControles` (ya existía antes de esta sesión, no es
   parte de los cambios de Tanda 1 en ese archivo) matchea justo el formato "LETRA - Acción"
   anclado a inicio de línea, y `RxWasd`/`RxEspacio`/`RxEsc`/`RxClickOCtrl`/
   `RxCamaraMultiPalabra` cubren el resto de los tokens de esa misma lista en los 3 idiomas
   (verificado leyendo `UITexts_es/en/pt.asset` directo).

**Gap conocido, no arreglado (`InputPromptSystem.cs` está fuera de mi alcance en esta
sesión, ver `CLAUDE.md` de la tarea)**: `RxTeclaEnListaDeControles` solo reconoce `[EUI]`.
Las líneas "O - Abrir Controles" y "M - Control de sonido" de esa misma lista NO tienen
traducción a joystick — quedan mostrando la letra de teclado. "M" no tiene botón de gamepad
por diseño (no arreglable sin agregar un binding). "O" sí debería mapear a `(Start)` (mismo
eje "Options" que Esc) — flageado como tarea de background aparte.

## Cómo verificar sin abrir Unity

`python tools/compile-check.py [tag]` compila `Assembly-CSharp` con el Roslyn que trae
Unity, usando el response file real que dejó el editor en `Library/Bee/artifacts/`. Mismos
defines y mismas referencias que el editor, ~20 segundos, sin abrir Unity ni tocar la
Library. El `tag` opcional es una subcarpeta de salida, para correr varias en paralelo.

Warnings de baseline conocidos (no son nuestros): `JumpFloodOutlineRenderer` CS0162 y
`HongueroTiburcioDialogueTrigger` CS0414.

`python tools/make-meta.py <ruta...>` genera los `.meta` de scripts/carpetas nuevas con un
GUID random verificado sin colisiones, para no depender de que Unity refresque.

**Lo que esto NO verifica**: nada de runtime. El feel del cursor, si 80 px de tolerancia
alcanzan, si los botones del joystick de Diego mapean como el layout Xbox estándar, y el
gatillo L2 (que en Windows es el 9no eje pero cambia según el driver del joystick) sólo se
pueden validar jugando.

## Gotchas del Input Manager viejo

- **Varias entradas pueden compartir `m_Name`**: `Input.GetButtonDown("Interact")` da true
  si CUALQUIERA se disparó. Así se suma joystick sin tocar el binding de teclado. Es el
  mecanismo que usa todo este feature.
- Los campos vacíos del YAML terminan con **un espacio** después de los dos puntos.
  Preservarlo al editar a mano.
- `ProjectSettings/InputManager.asset` es CRLF en el working tree (`.gitattributes` tiene
  `* text=auto` y `core.autocrlf=input`, así que en HEAD se guarda LF). No "arreglar" eso.
- En `type: 2` (Joystick Axis) el campo `axis` es **0-indexado**: `axis: 8` es el "9no eje"
  de la UI de Unity.
- Botones Xbox en Windows: 0=A, 1=B, 2=X, 3=Y, 4=LB, 5=RB, 6=Back, 7=Start, 8=click stick
  izq, 9=click stick der.
- Se **sacaron** bindings viejos que ahora molestaban: `Jump` estaba en el botón 3 (Y),
  `Mute` en el botón 1 (que ahora es atacar), e `Inventory`/`Quests` en los clicks de stick
  (8 y 9), que se apretaban sin querer al correr. La cámara tuvo un tiempo R1 (botón 5)
  además de L2; se sacó a pedido de Diego, queda **sólo L2** (más el click del medio).
- **R1 (botón 5) quedó libre** desde que se le sacó el binding de cámara — issue #41.1 lo
  reusa para `TabSiguiente` (ciclar tabs del Flap), sin pisar nada. `TabAnterior` (L1, botón
  4) SÍ comparte botón físico con `Run`, pero son dos EJES distintos en `InputManager.asset`
  (mismo patrón que "varias entradas comparten `m_Name`", pero al revés: acá son nombres
  DISTINTOS sobre el mismo botón). `PlayerController` SÍ sigue leyendo `Run` con el Flap
  abierto (no se gateó a propósito: setear `IsSprinting` no mueve nada con `Time.timeScale`
  en 0), así que usar L1 para cambiar de tab también prende ese flag — inofensivo, y se
  autocorrige apenas se suelta el botón (`CorrerUp`), casi siempre todavía con el menú
  abierto.
- Quedan bindings de joystick en las entradas `Debug *` de Unity (botones 4, 5, 8, 9),
  pero están detrás de apretar L3+R3 juntos. Pre-existente, no lo tocamos.

## Problemas conocidos y pending issues

**IMPORTANTE**: ver `specs/004-joystick-controls/pending-issues.md` para la lista **completa** de bugs y features faltantes encontrados en testing (2026-09-08).

**P1 (críticos, bloquean gameplay)**:
- ~~#41.1 Navegación del Flap rota~~ — **resuelto** (septiembre 2026): `FlapManager` tiene
  `Update()` propio (activo solo con el menú abierto) que lee R1/L1 (`InputHub.
  TabSiguienteDown`/`TabAnteriorDown`, ejes nuevos) para ciclar `_flapDisplays`, y B
  (`AtaqueGamepadDown`) para cerrar — contextual con el seguro de salir (B le contesta "No"
  a ESE diálogo si está abierto, no cierra el Flap por atrás). El fallback "sin nada
  navegable cae en la solapa" (Tareas/Controles) ya existía de una sesión previa
  (`SeleccionarDentroDe`) y sigue andando. Se encontraron y arreglaron dos gaps reales
  del "highlight visible" que el doc daba por cerrado: `SliderFlap.prefab` e
  `InventorySlot.prefab` seguían con `m_SelectedColor` gris invisible (nunca se había
  propagado desde `Button.prefab`), y las 4 solapas no tenían `m_Colors.m_SelectedColor`
  overrideado por instancia (sólo `m_HighlightedColor`, visible con mouse pero no con
  joystick) — ahora las 4 resaltan con su color propio en los dos casos. Detalle en
  "Navegación del Flap" y en la sección de gotchas de foco, arriba. Pendiente: confirmar
  jugando con gamepad real.
- ~~#41.2 B button "stuck" después de origami~~ — **resuelto** (Nivel 1, página 2): causa raíz confirmada,
  no era un flag colgado sino una carrera entre dos `Update()` sin coordinar. `MultipleRectCheck.CancelarDown()`
  (B cancela el origami) y `PlayerController.CheckControls()` (B ataca) leen el MISMO
  `InputHub.AtaqueGamepadDown` sin arbitrarse entre sí — a diferencia de A, que sí tiene ese arbitraje central
  vía `_gamepadInteractua`. Unity no garantiza el orden de `Update()` entre MonoBehaviours de objetos distintos
  (no hay `ScriptExecutionOrder.asset` en el proyecto): si el `Update()` del origami corría antes que el de
  `Player` ese frame, el mismo apretón de B que cerraba el origami (`SetState(Idle)`, sincrónico) alcanzaba a
  además arrancar un ataque real (`Player.CanAttack()` ya veía Idle cuando `PlayerController` leía ese MISMO
  B unas líneas después), consumiendo `_readyToAttack` para un ataque fantasma que el jugador nunca pidió.
  Fix en `Player.cs`: `SetState()` anota en qué frame se sale de un estado que bloquea el ataque
  (Casting/ReceivingReward/Dead/RidingPage), y `CanAttack()` se niega ese mismo frame — funciona sin importar
  el orden de `Update()` de ese frame en particular. Pendiente: confirmar jugando (cancelar con B varias veces,
  cerrar, atacar de una).
- ~~#41.3 Input no se bloquea durante pausa~~ — **resuelto** (septiembre 2026):
  `PlayerController.CheckControls()` calcula `menuAbierto = FlapManager.Instance.IsMenuOpen`
  (propiedad nueva, espeja `_isOpen`) y lo usa para saltear interactuar-en-el-mundo, atacar
  (teclado Y gamepad — antes el teclado no tenía NINGÚN gate de pausa) y el bloque de
  movimiento/salto. Esc/O/I/U siguen sin gatear porque son lo que abre/cierra el propio
  Flap. Detalle en "Pausa del Flap", arriba. Pendiente: confirmar jugando (parada sobre un
  pedestal de origami, abrir el Flap, intentar mover/atacar/interactuar — nada debería
  pasar hasta cerrarlo).
- ~~#41.13 Tooltips se abren y se cierran instantáneamente~~ — **resuelto** (septiembre 2026,
  reportado por Diego jugando): no era un bug del sistema de tooltips en sí, sino
  `PlayerModel.UpdateVerticalState()` llamando a `Player.DestroyPaperPlaneHat()` (que hace
  `TooltipManager.Instance.HideTooltip()`, un hide GLOBAL de los 5 post-its) en CADA frame
  grounded, para siempre, una vez agotado el sombrero de papel/salto aumentado por primera
  vez en la partida (faltaba el guard `isPaperPlaneHat`). Detalle completo y el gotcha para
  no repetirlo en `origami-y-tooltips.md`. Pendiente: confirmar jugando (agarrar el
  sombrero, gastar los saltos, aterrizar, verificar que los tooltips normales ya no
  parpadean después).
- ~~#41.14 A abre el diálogo de la abuela al cerrar el victory overlay~~ — **resuelto**
  (septiembre 2026, mismo patrón que #41.2, ver punto 6 de los gotchas de A más abajo):
  el mismo apretón de A que cierra el victory overlay con su botón (Submit del EventSystem)
  podía además disparar `OnPlayerPressedE` hacia el mundo si `PlayerController.
  CheckControls()` corría después de `OverlayManager.Unlock()` en el mismo frame — abriendo
  el diálogo de la abuela si el player seguía parado en su trigger. Fix:
  `OverlayManager.SeDesbloqueoEsteFrame`. Pendiente: confirmar jugando (repetir el boss
  fight, apretar A sobre "quedarme a explorar" varias veces, confirmar que nunca abre el
  diálogo).

**P2 (alta prioridad, UX/polish)**:
- ~~#41.4 Main menu no navigable con joystick~~ — **resuelto de verdad esta vez** (septiembre 2026, ronda 2). La
  conclusión anterior ("ya resuelto en `e848cc4`") era **incorrecta**: se basaba en lectura estática y Diego
  confirmó jugando que seguía sin andar. **Causa raíz real**: `MainMenuManager.Start()` intenta seleccionar
  `NewGameButton` UNA sola vez, gateado por `UISelector.SeleccionarPrimeroSiJoystick` →
  `InputHub.HayJoystickConectado` (que llama `Input.GetJoystickNames()`). En Windows, sobre todo con mando XInput,
  ese enumerado puede tardar unos frames en completarse desde que arranca el Player — si el `Start()` del menú
  (que es la PRIMERISIMA escena que carga el juego, la ventana con más chance de pisar ese delay) corre antes de
  que termine, `HayJoystickConectado` da `false`, la selección se pierde, y a diferencia del Flap (que reintenta
  en CADA apertura vía `ShowDesiredDisplay`/`MoveFlap`) nadie más volvía a intentarlo: la sesión entera quedaba
  sin selección inicial aunque el joystick estuviera físicamente enchufado. Suscribirse a `InputHub.OnDeviceCambio`
  tampoco alcanza como fix: ese evento solo salta cuando ALGO lee `UltimoDeviceFueJoystick` ese frame, y nada en
  el Main Menu lo leía después del `Start()` fallido — mismo problema estructural que ya se había resuelto en
  `CursorManager` para el issue #41.7 (ahí también hacía falta un polling activo, no un suscriptor pasivo).
  **Fix**: `MainMenuManager.Update()` ahora llama `ReintentarSeleccionSiHaceFalta()` todos los frames — reintenta
  `SeleccionarBotonNuevoJuego()` mientras no haya nada seleccionado en el `EventSystem` Y haya evidencia de
  joystick (`HayJoystickConectado || UltimoDeviceFueJoystick`), y para de pollear apenas hay selección o apenas
  arranca `_dialogueStarted` (que a propósito limpia la selección con `UISelector.Limpiar()` — reseleccionar
  después de eso dejaría un botón fantasma que el B seguiría "apretando" durante el diálogo). Logs
  `[MainMenuManager]` nuevos en `Start()` y en cada selección exitosa, con el `Time.frameCount`, para que Diego
  pueda confirmar en la consola en qué frame se detectó el joystick. El resto de la investigación previa seguía
  siendo correcto y no se tocó: `Button.prefab` con `Navigation = Automatic` y el `EventSystem` de la escena
  mapeando Submit/Cancel a A/Start.
- ~~#41.5 Tooltip de origami~~ — **resuelto**: había DOS tooltips de origami, y solo uno estaba roto.
  El tooltip de "arranque" (`TriggerOrigami.tooltipTextToShow`, "Mantené E para comenzar") ya
  traducía bien porque matcheaba `RxTeclaConVerbo`. El roto era el de "arrastre"
  (`Origami.tooltipMessage`, mostrado por `MultipleRectCheck.StartOrigami` al arrancar el
  minijuego): "Arrastrá la flecha verde..." no menciona ninguna tecla, así que ningún token legacy
  lo tocaba con joystick. Fix en `InputPromptSystem.cs`: nuevo `RxArrastrarAlInicio` (mismo patrón
  que `RxTeclaAlInicio`, ancla el verbo al inicio del string en los 3 idiomas — Arrastrá/Arrastra
  es, Drag en, Arraste pt) que antepone el prompt de agarrar con joystick: "Tocá (A) y arrastrá...".
  Con teclado/mouse el texto queda igual que siempre (cero regresión, vía el camino rápido de
  `Procesar`). De paso se encontró que 9 de los 10 `OrigamiRoute *.prefab` hardcodean el texto en
  español en vez de usar la clave `origami_guide` de la tabla (solo `1-Easy` la usa bien) — no
  afecta este fix (el regex cubre el texto hardcodeado igual), pero sí significa que EN/PT ven
  español en ese tooltip; anotado como deuda técnica aparte, no arreglado acá.
- ~~#41.6 Controles tab~~ — **ya funcionaba, no hizo falta código nuevo**: investigado en
  septiembre 2026. El contenido es un `LocalizeStringEvent` (clave `controlsText` de la
  tabla `UITexts`, formato "TECLA - Acción" por línea), y YA estaba cubierto por
  `LocalizedText.cs` (engancha cualquier `LocalizeStringEvent` de la escena y lo reprocesa
  en cada cambio de device) + `InputPromptSystem.RxTeclaEnListaDeControles` (ya existía,
  matchea ese formato) — ambos de una sesión previa a esta. Gap conocido no arreglado (fuera
  de mi alcance, `InputPromptSystem.cs` tocado por otro agente en esta misma tanda): las
  líneas "O - Abrir Controles" y "M - Control de sonido" no traducen (el regex solo cubre
  `[EUI]`); flageado como tarea aparte. Detalle en "El tab Controles del Flap YA se traduce
  solo", arriba.
- **#41.7 Cursor auto-hide**: cursor aparece con mouse pero no desaparece al volver a joystick; falta timeout.
- **#41.8 Chino dialogue**: texto tiene "[...]" duplicado.

**P3 (nice to have)**:
- **#41.9 Typewriter effect**: texto debería aparecer secuencialmente (como escribiéndose).

## Gotchas específicos del contexto A-button que ya nos agarraron

1. **El A es el cuello de botella más frágil de todo el sistema.** Es jump + interact + menu submit. Tocar cualquier gate que afecte A potencialmente rompe 3 flujos distintos. **Test exhaustivamente en gameplay, origami y menús después de cada cambio.**

2. **`InteractionContext` vs `LevelManager.inDialogue`**: ambos gatean la lógica de A. `InteractionContext.HayInteraccionDisponible` checkea triggers cercanos + el flag `inDialogue`. Pero durante origami, el flag de `LevelManager.inDialogue` NO está prendido (solo `PlayerState.Casting`). Si modificas la lógica de A, verifica que funcione en TODOS estos contextos: gameplay normal, origami, diálogo abierto, durante pausa (Flap), overlay de derrota.

3. **TriggerScript y `EsInteractuable`**: cualquier trigger nuevo que responda a A debe setear `EsInteractuable => true`. Si no, A salta en vez de interactuar. Grep de "EsInteractuable" antes de agregar triggers.

4. **`SetActive(false)` en `AbuelaDialogueTrigger`**: fue un bug — si el trigger se desactiva mientras el player está dentro, `OnExitBehaviour` no corre (no entra a `OnTriggerExit`), así que `InteractionContext` queda pensando que hay trigger activo y A sigue interactuando invisible. La fix fue mover la deregistración a `OnDisable`/`OnDestroy` en la base `TriggerScript`. Si vuelves a tocar este código, revisa que todos los caminos donde un trigger puede "desaparecer" (SetActive false, Destroy, scene reload) se limpien en `InteractionContext`.

5. **`_gamepadInteractua` es un flag por frame**: `PlayerController.CheckControls()` lo setea si detects A+hay interacción. Pero si en el mismo frame otro código chequea `Input.GetButtonDown("Jump")` ANTES de que `CheckControls` corra, A contará como salto. La solución es **leer input en un solo lugar** (en `PlayerController`), resolver la acción ahí, y comunicar el resultado a otros sistemas vía eventos (es lo que hace hoy). Si necesitas que otro sistema reaccione a A, no leas `Input` directo — escucha `Evento.OnPlayerPressedE` / `OnPlayerPressedSpace`.

6. **Un overlay que se cierra con A puede filtrar ese mismo apretón hacia el mundo** (issue #41.14,
   septiembre 2026): `OverlayManager.BTN_ContinueGame()`/`Unlock()` corren vía el Submit del
   EventSystem (mismo eje físico que A/Interact). Si ese `Update()` corre ANTES que
   `PlayerController.CheckControls()` en el mismo frame, `Unlock()` ya bajó `isLocked`/`inDialogue`
   cuando `CheckControls()` lee el MISMO apretón y dispara `Evento.OnPlayerPressedE` sin nada que lo
   vete (el gate `menuAbierto` existente solo cubre el Flap, no los overlays de victoria/derrota).
   El guard de `DialogueManager.ShowDialogue()` (chequea `isLocked`/`inDialogue`) no alcanza a
   frenarlo porque para ese momento esos flags YA están en `false` — así el mismo A que cerraba el
   victory overlay del boss fight de la abuela le abría además su diálogo, si el player seguía
   parado en su trigger. Fix con el mismo patrón que #41.2:
   `OverlayManager.SeDesbloqueoEsteFrame` (graba `Time.frameCount` en `Unlock()`) +
   `PlayerController.CheckControls()` veta el `OnPlayerPressedE` de mundo en ese mismo frame. Mismo
   principio de fondo que el punto 5: un input que dispara dos sistemas sin coordinarse necesita un
   arbitraje explícito, no asumir que el orden de `Update()` va a ser el mismo siempre.

## Cómo arrancar en la próxima sesión

**Si tocas input, controles o joystick**:

1. Lee `specs/004-joystick-controls/pending-issues.md` completo (5 min). Es el estado real del feature.
2. Corre `python tools/compile-check.py` para verificar que no hay warnings nuevos sobre input/controllers.
3. **Test manual en el editor**:
   - Gameplay: salta con A, ataca con B, interactúa con A en trigger, corre con L1.
   - Origami: abre con A, mueve con stick, cancela con B o E, completa la ruta.
   - Menús: Flap abre con Start, cierra con Esc/Start, navega con arrows/stick, selecciona con A.
   - Device switching: empieza con teclado (texto dice "E"), enchufa joystick (texto cambia a "(A)"), desenchufa (vuelve a "E").
4. **Si tocas `InputHub.cs`, `PlayerController.cs`, o `InteractionContext.cs`**: antes de commitear, verifica:
   - Compilación limpia sin warnings nuevos.
   - A no "suena" en gameplay normal (no salta/interactúa involuntariamente cuando no debería).
   - Origami todavía funciona, B cancela, A/stick mueven el cursor.
   - Menú Flap navega sin quedarse atrapado.
   - Pausa realmente bloquea todo (no flipflops, no origamis).

5. **Si tocas UI (Flap, Main Menu, overlays)**: después de cambios, verifica que la selección con joystick es clara y ningún elemento queda "atrapado" sin nada navegable.
