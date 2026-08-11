# Kami: Papel y Tijera

Juego de Unity (URP + Steam): personajes 2D animados con **Spine** (runtime spine-unity **3.8** vendoreado en `Assets/Spine`) sobre mundo 3D. Código y comentarios en español.

## Dónde está todo

- `Assets/Scripts/Player/` — Kami, en MVC casero (ver abajo)
- `Assets/Scripts/PUBMechanics/` — solapas del libro pop-up (Solapa, TriggerSolapa)
- `Assets/Scripts/Cortables/` — todo lo que la tijera corta (`ICortable`) + `TijeraHitbox`
- `Assets/Scripts/Managers/` — AudioManager, LevelManager, EventManager, etc. (singletons)
- `Assets/Scripts/TriggerS/` — triggers de zona (base `TriggerScript`, con tooltip por color)
- `Assets/Scripts/Origami/` — minijuego de origami + `PedestalCanvasDisplay` (costo en pedestal)
- `Assets/Scripts/UI/` — `TooltipManager` + `PostIt` (post-its de tutorial)
- `Assets/Scripts/Particles/ParticleShooter.cs` — partículas del player por índice
- `Assets/Prefabs/Kami/Kami.prefab` — el player completo
- `Assets/Prefabs/OrigamiRoutes/` — sellos, `PedestalParent.prefab` y rutas de origami
- `Assets/Prefabs/UI/PostIt.prefab` — los post-its de escena son instancias de este
- `Assets/Prefabs/Particulas/` — prefabs de partículas
- `Assets/2D/Kami Spine/Atlas 5/skeleton.json` — skeleton ACTIVO (Atlas 1-4 son viejos)
- Escena de trabajo: `Nivel1_LaRural SpineTest.unity` (las otras escenas viejas pueden tirar warnings de refs stale)

Branch de laburo de animaciones: `feature/spine-animations`.

## Arquitectura del Player (MVC casero)

`Player.cs` es la fachada: stats, componentes, y `CurrentState` (única fuente de verdad).
Construye y coordina a los otros tres — ellos hablan con Player, nunca entre sí:

- **PlayerModel** — piensa (lógica de estados, física)
- **PlayerView** — reacciona: animaciones de Spine, sonidos y partículas. Escucha `OnStateChanged` y los eventos de las anims de Spine (`HandleAttack`, `HandleFootstep`)
- **PlayerController** — recibe input

## Sistema de Equipamiento (Tijeras) — Agosto 2026

**Status**: ✅ Implementado (solo skins de tijeras por ahora)

Kami tiene dos tipos de tijera con skins diferentes en Spine (Atlas 8):
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

## Ojo al editar

- El editor de Unity suele estar ABIERTO mientras trabajamos: al crear un asset, preferir el `.meta` que Unity autogenera. Pero si Unity no tiene foco puede tardar MUCHO en generarlo: es válido crear el `.meta` a mano con un GUID random, verificado sin colisiones por grep (Unity lo adopta al refrescar).
- Cirugía YAML de prefabs: leer el archivo entero antes y copiar patrones existentes. Referencias a componentes de prefabs anidados = bloques MonoBehaviour *stripped* (patrón copiable en `OrigamiRoute 1-Easy.prefab`). El fileID que una escena usa para un target dentro de una instancia anidada se computa `(source XOR prefabInstance) & 0x7FFFFFFFFFFFFFFF`.
- Line endings mixtos: algunos prefabs son CRLF y otros LF — preservar el del archivo al editar.
- Verificación post-cirugía: contar bloques `--- !u!` antes/después + grep de unicidad de fileIDs.
- No hay Unity CLI para compilar desde acá: la verificación final de compilación la hace el editor de Diego.
