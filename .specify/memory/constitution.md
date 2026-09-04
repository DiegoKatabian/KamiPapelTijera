# Kami: Papel y Tijera Constitution

## Core Principles

### I. Pensar antes de codear
Entender el problema y el código existente antes de escribir una línea. Ningún cambio
arranca por "probar a ver qué pasa" en un proyecto Unity donde la compilación final
la valida el editor de Diego, no un CI.

### II. Simplicidad ante todo
La solución más simple que cumple el objetivo; nada especulativo. No se diseña para
requisitos hipotéticos futuros. Tres líneas parecidas es mejor que una abstracción
prematura. YAGNI se aplica con dureza, sobre todo en un equipo de 2 personas.

### III. Cirugía, no refactors de paso
Se toca únicamente lo necesario para la tarea. Un fix de bug no arrastra un refactor
del archivo; un feature nuevo no "aprovecha" para reordenar código que ya andaba.
Los refactors grandes son su propia tarea, explícita, revisada aparte.

### IV. Ejecutar contra un objetivo verificable
Toda tarea tiene un objetivo definido y comprobable. Si no lo tiene, se define primero
(spec, issue, o al menos una frase clara de "qué se considera terminado").

### V. Trazabilidad y guard clauses (NON-NEGOTIABLE)
`Debug.Log($"[NombreClase] mensaje")` en los puntos de decisión — el origen debe ser
obvio de un vistazo. Guard clauses con `Debug.LogWarning` antes de mutar estado sobre
referencias que pueden faltar: nunca fallar en silencio (no-op silencioso) cuando algo
esperado no está wireado.

### VI. Activos de terceros son compartidos
Todo lo que viene del Asset Store o es vendoreado (Spine runtime, Cinemachine, etc.)
se asume compartido entre escenas/prefabs salvo que se confirme instancia local. Antes
de editar, verificar cuál es — editar el original rompe otras escenas en silencio.

## Restricciones técnicas

- **Motor**: Unity URP + Steam. Personajes 2D animados con **spine-unity 3.8**
  (vendoreado, sin autoupdate) sobre mundo 3D. Spine 3.8 NO tiene `TrackEntry.Reverse`
  ni otras APIs de Spine 4.x — verificar contra `Assets/Spine/Runtime/spine-csharp/`
  antes de asumir que algo existe.
- **Idioma**: código y comentarios en español. Comentarios explican el *por qué*, no
  el *qué* (el código ya lo dice si está bien nombrado).
- **Convenciones**: `PascalCase` para clases, `camelCase` para variables/campos.
  `[SerializeField]` privado + `[Tooltip]` en español para todo valor tuneable. Llaves
  siempre, incluso en una línea; `switch` con `break` explícito.
- **No hay CLI de compilación** desde este entorno — la verificación final de build la
  hace el editor de Diego. Ningún cambio se reporta como "funciona" sin decir
  explícitamente que no fue probado en el editor si ese es el caso.
- **No corregir typos del asset/skeleton** (`NoScissortsOverride`, `tiejraBack`, GUIDs
  "raros" pero reales) — son parte de un asset externo o de un GUID ya adoptado por
  Unity, no bugs de código.

## Flujo de trabajo (spec-driven)

Este proyecto usa Spec Kit para features de tamaño mediano/grande (una quest nueva, un
sistema de IA, un refactor de arquitectura). El flujo:
`/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`.
Fixes chicos y ajustes de una sola escena no necesitan spec formal — van directo,
documentados en el mensaje de commit.

Toda spec de gameplay debe declarar explícitamente:
1. Qué animaciones/skins de Spine nuevas requiere (si las hay) — Valentino las anima,
   no se asumen instantáneas.
2. Si toca un asset compartido (ver Principio VI).
3. Cómo se verifica sin CLI (checklist manual en el editor: qué probar, en qué escena).

## Gobernanza

Esta constitución documenta cómo ya trabaja el equipo (kimmiarts / Diego + Valentino),
no le impone un proceso nuevo. `CLAUDE.md` en la raíz del repo y `docs/claude/*.md`
son la fuente de verdad operativa del día a día; esta constitución es el resumen de
principios detrás de esas reglas, para que las specs de Spec Kit se validen contra
algo estable. Actualizar esta constitución cuando cambie una convención real del
equipo, no al revés.

**Version**: 1.0.0 | **Ratified**: 2026-09-04 | **Last Amended**: 2026-09-04
