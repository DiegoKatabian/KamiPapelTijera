# Kami: Papel y Tijera

Juego de Unity (URP + Steam): personajes 2D animados con **Spine** (runtime spine-unity **3.8** vendoreado en `Assets/Spine`) sobre mundo 3D. Código y comentarios en español.

## Dónde está todo

- `Assets/Scripts/Player/` — Kami, en MVC casero (ver abajo)
- `Assets/Scripts/PUBMechanics/` — solapas del libro pop-up (Solapa, TriggerSolapa)
- `Assets/Scripts/Cortables/` — todo lo que la tijera corta (`ICortable`) + `TijeraHitbox`
- `Assets/Scripts/Managers/` — AudioManager, LevelManager, EventManager, PageScrollerManager, PlayerPageSpawnManager, CameraManager, etc. (singletons)
- `Assets/Scripts/TriggerS/` — triggers de zona (base `TriggerScript`, con tooltip por color)
- `Assets/Scripts/Origami/` — minijuego de origami + `PedestalCanvasDisplay` (costo en pedestal)
- `Assets/Scripts/Input/` — `InputHub` (fachada única de input), `InteractionContext` (el botón B contextual), `GamepadCursor`, `InputPromptSystem`
- `Assets/Scripts/UI/` — `TooltipManager`/`PostIt` (tutorial), `FlapManager`/`CamWheelManager` (menú y selector de cámara), `InventorySlot`
- `Assets/Scripts/Inventory/` — `InventoryManager`/`InventoryItem` (recursos recolectables)
- `Assets/Scripts/Quests/` — `QuestManager`/`QuestEffector` + un ScriptableObject `QuestNN_Nombre.asset` por quest
- `Assets/Scripts/Dialogos/` — un `*DialogueTrigger.cs` por NPC + estados de NPC (`NPC_Abuela`, `NPC_Florista`, etc.)
- `Assets/Scripts/Enemies/` — Rocoso (FSM + física, ver `enemigos-e-ia.md`), `EnemySpawner` (armado, sin usar todavía)
- `Assets/Scripts/AI/` — `PatrollingAgent` (base NavMesh, agosto 2026) + `GallinaAgent`; `PageNavMeshManager` swapea el NavMesh activo por página (issue #21); ver `enemigos-e-ia.md`
- `Assets/Scripts/Barquito/` — NPC bote con A* propio por nodos (no NavMesh)
- `Assets/Scripts/AttackHitBoxes/` — `IGolpeable` + hitboxes de ataque de enemigos
- `Assets/Scripts/Particles/ParticleShooter.cs` — partículas del player por índice
- `Assets/Prefabs/Kami/Kami.prefab` — el player completo
- `Assets/Prefabs/OrigamiRoutes/` — sellos, `PedestalParent.prefab` y rutas de origami
- `Assets/Prefabs/UI/PostIt.prefab` — los post-its de escena son instancias de este
- `Assets/Prefabs/Particulas/` — prefabs de partículas
- `Assets/2D/Kami Spine/Atlas 11/skeleton.json` — skeleton ACTIVO (lo referencia `Kami.prefab`; los Atlas 1-10 son viejos)
- Escena de trabajo Nivel 1: `Nivel1_KamiPapelTijera.unity` (activa desde fines de agosto 2026 — `Nivel1_LaRural SpineTest.unity` quedó vieja/stale, no confundir; puede tener referencias rotas)
- Escena de trabajo Nivel 2: `Level2_Newspaper.unity` — ver `nivel2-y-ui.md` para estado actual (página del museo en progreso)

Active work branches:
- `feature/spine-animations` — Spine character animations (Spanish code/comments)
- `feature/joystick-controls` (issue #41) — gamepad support with animated input prompts (A=jump, B=attack/interact, L1=sprint, L2=camera), target M4 (Oct 15). See `specs/004-joystick-controls/spec.md` (English).

Roadmap and future feature specs: `specs/` (Spec Kit) and `ROADMAP.md`.

## Herramientas de análisis del proyecto

- **graphify** (`graphify-out/`): grafo de conocimiento del código en `Assets/Scripts` —
  `graphify-out/graph.html` (interactivo), `graphify-out/GRAPH_REPORT.md` (god nodes,
  comunidades, conexiones sorprendentes). Regenerar con `/graphify Assets/Scripts` tras
  cambios grandes de arquitectura; `--update` para incremental.
- **Spec Kit** (`.specify/`): flujo spec-driven para features medianas/grandes —
  `/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`.
  Constitución del proyecto en `.specify/memory/constitution.md`. Specs existentes en
  `specs/`.

### Modus operandi: documentación viva (NO es un issue, es una regla)

Mantener el grafo, las specs y los docs al día es **parte de terminar cualquier tarea**,
no un trabajo aparte que se agenda. Al cerrar un pedazo de trabajo:

1. **Docs primero**: si el cambio toca algo descrito en `CLAUDE.md` o `docs/claude/*.md`,
   se actualiza ese archivo **en el mismo commit**. Doc desactualizada = trabajo sin terminar.
2. **Specs**: si la feature tiene spec en `specs/`, marcar ahí lo implementado y las
   decisiones que cambiaron respecto al draft (con el porqué). La spec es la memoria del
   diseño, no un documento congelado.
3. **Grafo**: tras cambios de arquitectura (clases/managers nuevos, borrados, o
   dependencias nuevas entre sistemas), regenerar con `/graphify Assets/Scripts --update`.
   No hace falta por un bugfix de una línea.
4. **Código muerto**: si algo quedó sin callers, se borra en el momento — no se deja
   "por si acaso" (ver el caso `EnemySpawner`, issue #23).

## Arquitectura del Player (MVC casero)

`Player.cs` es la fachada: stats, componentes, y `CurrentState` (única fuente de verdad).
Construye y coordina a los otros tres — ellos hablan con Player, nunca entre sí:

- **PlayerModel** — piensa (lógica de estados, física)
- **PlayerView** — reacciona: animaciones de Spine, sonidos y partículas. Escucha `OnStateChanged` y los eventos de las anims de Spine (`HandleAttack`, `HandleFootstep`)
- **PlayerController** — recibe input

## Sistema de Equipamiento (Tijeras) — Agosto 2026

**Status**: ✅ Implementado (solo skins de tijeras por ahora)

Kami tiene dos tipos de tijera con skins diferentes en Spine (Atlas 11):
- `TijeraEquipment.Normal` → Spine skin `"Tijera_Normal"`
- `TijeraEquipment.Mejorada` → Spine skin `"Tijera_Upgrade_1"`

**Código**:
- `Player.cs:SetTijeraEquipment()` — cambia skin + lógica de daño en TijeraManager
- `Player.currentTijera` — enum que trackea equipo actual
- Al completar quest del chino, `LevelManager` llama `Player.GetTijeraMejorada()` que usa `SetTijeraEquipment()`

**Futuro (comentado en código para Spine 4.x):**
Cuando actualicemos a Spine 4.0, agregar multi-slot (botas, guantes, etc) usando `CurrentEquipment` struct y composición de skins. Hoy es simple porque solo maneja tijeras.

## Muerte con causa (río, rocoso) — Agosto 2026

La causa de muerte (`DeathCause`: Generic/Drowning/Rocoso) decide la anim, el texto del overlay y el respawn:

1. **Río**: `Rio.cs` espera `activationDelay` (tuneable) con el IMojable adentro — si sale antes, cancela y no pasa nada. Cumplido el delay llama `GetWet()`: para Kami eso es feedback (anim mojarse + sonido) y `Die(DeathCause.Drowning)` de una; otros IMojable siguen con damage normal. Rio NO conoce a Player, trata todo por IMojable.
2. **Rocoso**: `GetGolpeado()` → `TakeDamage(dmg, DeathCause.Rocoso)` (overload que enhebra la causa hasta `Die`).
3. **Anim**: `PlayerView.SetDeathAnimation(cause)` elige "Drowning" o "Death".
4. **Overlay**: `Player.DeathSequence` resuelve la posición de respawn (dueño de la política) y llama `ShowDefeatOverlay(cause, respawnOverride)`. `DefeatOverlay` (hereda `Overlay`) muestra la causa localizada — keys en tabla `UITexts`: `DefeatDrowning`, `DefeatRocoso`, `DefeatGeneric`.
5. **Respawn** al cerrar con E: drowning usa `Player.drowningRespawnMode` (`LastSafePosition` = snapshot generoso con doble buffer en PlayerModel, antigüedad 1-2× `safeSnapshotInterval`; o `LevelSpawnPoint` = entrada de página). Las demás muertes siempre respawn común (`lastUsedSpawn` del `PlayerPageSpawnManager`).

**Setup de escena pendiente**: el GO del defeat overlay necesita el componente `DefeatOverlay` (reemplaza a `Overlay`), con `causeText` (TMP) asignado, y reasignar la ref en `OverlayManager`. Las 3 keys hay que crearlas en los localization sheets.

## Convenciones de código

- Comentarios en español, explicando el *por qué*
- Llaves siempre, incluso en una línea; `switch` con `break` explícito
- `[SerializeField]` privado + `[Tooltip]` en español para todo valor tuneable
- `Debug.Log($"[NombreClase] ...")` en los puntos de decisión
- Guard clauses con `Debug.LogWarning` en referencias que pueden faltar
- **NO corregir los typos del skeleton** (`NoScissortsOverride`, `tiejraBack`): son del asset, no del código

## Detalle fino (leer según la tarea)

- Spine/skeleton/animaciones de Kami: @docs/claude/spine-kami.md
- Audio y partículas: @docs/claude/audio-y-particulas.md
- Canvas de costo de origami y tooltips/post-its: @docs/claude/origami-y-tooltips.md
- Paso de página (HojaMaster es Mecanim, no Spine), PositionMarker y el enganche de Kami al borde (RidingPage): @docs/claude/paginas-y-hoja.md
- Enemigos e IA (Rocoso, PatrollingAgent/GallinaAgent, Barquito, patrón de hitboxes): @docs/claude/enemigos-e-ia.md
- Nivel 2 (estado actual), LevelManager, Inventario, Flap/CamWheel UI, Cámara: @docs/claude/nivel2-y-ui.md
- Quests y diálogos de NPCs: @docs/claude/quests-y-dialogos.md
- Controles (teclado + joystick), botón B contextual, cursor virtual del origami: @docs/claude/controles-y-gamepad.md

## Ojo al editar

- El editor de Unity suele estar ABIERTO mientras trabajamos: al crear un asset, preferir el `.meta` que Unity autogenera. Pero si Unity no tiene foco puede tardar MUCHO en generarlo: es válido crear el `.meta` a mano con un GUID random, verificado sin colisiones por grep (Unity lo adopta al refrescar).
- Cirugía YAML de prefabs: leer el archivo entero antes y copiar patrones existentes. Referencias a componentes de prefabs anidados = bloques MonoBehaviour *stripped* (patrón copiable en `OrigamiRoute 1-Easy.prefab`). El fileID que una escena usa para un target dentro de una instancia anidada se computa `(source XOR prefabInstance) & 0x7FFFFFFFFFFFFFFF`.
- Line endings mixtos: algunos prefabs son CRLF y otros LF — preservar el del archivo al editar.
- Verificación post-cirugía: contar bloques `--- !u!` antes/después + grep de unicidad de fileIDs.
- **Sí se puede compilar desde acá**: `python tools/compile-check.py [tag]` compila
  `Assembly-CSharp` con el Roslyn que trae Unity, reusando el response file real que el
  editor dejó en `Library/Bee/artifacts/` (mismos defines, mismas referencias), en ~20s y
  sin abrir Unity. Warnings de baseline conocidos: `JumpFloodOutlineRenderer` CS0162 y
  `HongueroTiburcioDialogueTrigger` CS0414. **Ojo**: verifica compilación, NO runtime — el
  feel, la física y el input real los sigue validando Diego en el editor.
- `python tools/make-meta.py <ruta...>` genera los `.meta` de scripts/carpetas nuevas con
  GUID random verificado sin colisiones, sin depender de que Unity refresque.
