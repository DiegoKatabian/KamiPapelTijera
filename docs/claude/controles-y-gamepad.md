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

## Prompts de botones en los textos

Los textos del juego decían "Press E to talk", "Mantené SHIFT para correr", etc. Con un
joystick en la mano eso es información falsa. `InputPromptSystem.Procesar(texto)` corre
sobre los textos de tooltips y diálogos:

1. **Placeholders `{INPUT:*}`** (`{INPUT:saltar}`, `{INPUT:accion}`, `{INPUT:correr}`,
   `{INPUT:camara}`, `{INPUT:menu}`, `{INPUT:mover}`) — el camino limpio para textos nuevos.
   Funcionan siempre, con cualquier device.
2. **Tokens legacy** (SHIFT, ESPACIO, Click, "Press E"…) — se traducen **sólo si el jugador
   está usando joystick**, para no tener que editar las tablas de localización (que son
   contenido de Diego/Valentino). Con teclado el texto sale idéntico a hoy.

Hoy los prompts de joystick son **texto** (`(A)`, `(B)`, `(L1)`…), no íconos: falta el
atlas de sprites de botones (dependencia de arte). El código está escrito para que pasar a
`<sprite name=button_A>` de TextMeshPro sea cambiar una sola tabla.

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
- Quedan bindings de joystick en las entradas `Debug *` de Unity (botones 4, 5, 8, 9),
  pero están detrás de apretar L3+R3 juntos. Pre-existente, no lo tocamos.

## Problemas conocidos y pending issues

**IMPORTANTE**: ver `specs/004-joystick-controls/pending-issues.md` para la lista **completa** de bugs y features faltantes encontrados en testing (2026-09-08).

**P1 (críticos, bloquean gameplay)**:
- **#41.1 Navegación del Flap rota**: no se navega a botón de cerrar, secciones sin colores distintivos, no hay forma de salir de Tareas/Controles con joystick. Propuesta: R1/L1 para cambiar tabs, B para cerrar.
- **#41.2 B button "stuck" después de origami**: ataque deja de funcionar tras completar un origami en Level 2. Probablemente relacionado con cerrar origami con B.
- **#41.3 Input no se bloquea durante pausa**: se puede triggerear origamis, mover/flipear a Kami, etc. mientras el Flap está abierto (`Time.timeScale = 0`). El input debería estar completamente bloqueado.

**P2 (alta prioridad, UX/polish)**:
- **#41.4 Main menu no navigable con joystick**: botones de idioma y start no responden a stick.
- **#41.5 Tooltip de origami**: debería decir "Tocá A y arrastrá..." con joystick activo.
- **#41.6 Controles tab**: tabla de controles debe mostrar mapeo para joystick cuando está activo.
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
