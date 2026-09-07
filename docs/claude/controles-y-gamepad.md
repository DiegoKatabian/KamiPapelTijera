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
