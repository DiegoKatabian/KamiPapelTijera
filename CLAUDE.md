# Kami: Papel y Tijera

Juego de Unity (URP + Steam): personajes 2D animados con **Spine** (runtime spine-unity **3.8** vendoreado en `Assets/Spine`) sobre mundo 3D. Código y comentarios en español.

## Dónde está todo

- `Assets/Scripts/Player/` — Kami, en MVC casero (ver abajo)
- `Assets/Scripts/PUBMechanics/` — solapas del libro pop-up (Solapa, TriggerSolapa)
- `Assets/Scripts/Cortables/` — todo lo que la tijera corta (`ICortable`) + `TijeraHitbox`
- `Assets/Scripts/Managers/` — AudioManager, LevelManager, etc. (singletons)
- `Assets/Scripts/Particles/ParticleShooter.cs` — partículas del player por índice
- `Assets/Prefabs/Kami/Kami.prefab` — el player completo
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

## Ojo al editar

- El editor de Unity suele estar ABIERTO mientras trabajamos: al crear un asset, esperar/usar el `.meta` que Unity autogenera en vez de inventar un GUID.
- Cirugía YAML de prefabs: leer el archivo entero antes, copiar patrones existentes (ej. referencias *stripped* a prefabs anidados), y verificar GUIDs con grep.
- No hay Unity CLI para compilar desde acá: la verificación final de compilación la hace el editor de Diego.
