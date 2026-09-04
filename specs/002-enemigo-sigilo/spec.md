# Feature Specification: Enemigo de sigilo

**Feature Branch**: `002-enemigo-sigilo`

**Created**: 2026-09-04

**Status**: Draft

**Input**: Idea del equipo (Diego): "enemigo basado en sigilo". Investigado contra el
código actual antes de escribir esta spec — ver `docs/claude/enemigos-e-ia.md`.

## Contexto (de la investigación de código)

No existe ningún precedente de detección en el proyecto (se buscó explícitamente:
sin vision cones, sin line-of-sight, sin hearing radius — el único rastro es un
método `InLineOfSight()` comentado y nunca implementado en `Barquito/Pathfinding.cs`).
Sí existe una base sólida para reusar: `Assets/Scripts/AI/PatrollingAgent.cs`, clase
base con `NavMeshAgent` + patrulla por waypoints + puntos de extensión virtuales
(`ShouldEvade()`, `UpdateEvadeDestination()`, `OnEvadeStart()`, `OnWaypointReached()`)
ya probados en producción por `GallinaAgent`. Este enemigo nuevo es candidato natural
a heredar de `PatrollingAgent` en vez de reinventar patrulla+NavMesh desde cero.

**Esta spec asume el diseño mínimo más simple que cumple "enemigo de sigilo"**: un
enemigo que patrulla una ruta, y si detecta al jugador (visión y/o distancia) dentro
de un cono/radio, entra en un estado de alerta que termina en persecución o en aviso.
El diseño de gameplay concreto (¿persigue? ¿avisa a otros enemigos? ¿el jugador puede
escabullirse agachándose o escondiéndose?) queda con `[NEEDS CLARIFICATION]` marcado
abajo — decidir con Diego antes de `/speckit-plan`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - El enemigo patrulla una ruta fija (Priority: P1)

Reusa el patrón de `GallinaAgent`: el enemigo camina entre waypoints en loop mientras
no detecta al jugador.

**Por qué esta prioridad**: es el estado base, y ya está resuelto por
`PatrollingAgent` — el costo de esta historia es bajo (heredar + configurar
waypoints), así que es la base sobre la que se apoya todo lo demás.

**Independent Test**: colocar el enemigo con 3+ waypoints en una escena de prueba y
confirmar que recorre el loop sin intervención.

**Acceptance Scenarios**:

1. **Given** el enemigo en modo Patrolling, **When** no hay jugador dentro de su rango
   de detección, **Then** sigue su ruta de waypoints en loop indefinidamente.

---

### User Story 2 - El enemigo detecta al jugador por visión (Priority: P1)

Un cono de visión (ángulo + distancia + line-of-sight vía raycast, ya que
`InLineOfSight()` existe comentado como precedente de intención) dispara la
transición de Patrolling a un estado de alerta.

**Por qué esta prioridad**: es el corazón mecánico de "sigilo" — sin detección
direccional, esto es indistinguible de un enemigo de persecución por distancia
(que ya existe: Rocoso, Gallina evadiendo).

**Independent Test**: cruzar el cono de visión del enemigo desde distintos ángulos;
confirmar que solo detecta dentro del cono + rango, y que un obstáculo entre el
enemigo y el jugador bloquea la detección (line-of-sight).

**Acceptance Scenarios**:

1. **Given** el jugador dentro del cono de visión y sin obstáculos, **When** pasa el
   tiempo de detección configurado, **Then** el enemigo entra en estado de Alerta.
2. **Given** el jugador fuera del cono (aunque esté cerca) o detrás de un obstáculo,
   **When** el enemigo evalúa detección, **Then** NO lo detecta.

---

### User Story 3 - El jugador puede perder al enemigo (Priority: P2)

Si el jugador sale del rango de detección y se rompe la línea de visión por un tiempo,
el enemigo abandona la alerta y vuelve a patrullar (posiblemente investigando la
última posición conocida antes de rendirse — patrón común de sigilo).

**Por qué esta prioridad**: sin esto, "sigilo" se degrada a "el enemigo te ve una vez
y te persigue para siempre", que ya es el comportamiento de evade de Gallina/Rocoso —
esta historia es la que justifica el género.

**Independent Test**: activar la alerta, romper línea de visión y alejarse; confirmar
que tras el timeout configurado el enemigo vuelve a Patrolling.

**Acceptance Scenarios**:

1. **Given** el enemigo en Alerta persiguiendo la última posición conocida,
   **When** no recupera contacto visual dentro del timeout, **Then** vuelve a
   Patrolling (sobre sus waypoints originales, reusando `SetWaypoints()` de
   `PatrollingAgent`).

---

### Edge Cases

- ¿Qué pasa si el jugador entra al cono de visión mientras el enemigo está cruzando
  entre waypoints (ya en movimiento)? No debería requerir estar "quieto" para
  detectar.
- ¿El enemigo debe reaccionar a sonido (ej. la tijera cortando algo) además de
  visión, o solo visión para la v1? → `[NEEDS CLARIFICATION: alcance de detección —
  solo visión, o visión + oído?]`
- ¿Qué le pasa al jugador si lo detectan — daño directo, alarma que llama a otros
  enemigos, game over, o solo lo empuja/persigue como Rocoso? →
  `[NEEDS CLARIFICATION: consecuencia de ser detectado, no está definida por el pedido
  original]`

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El enemigo MUST heredar de `PatrollingAgent` para reusar patrulla por
  waypoints y la integración con `NavMeshAgent`.
- **FR-002**: El sistema MUST detectar al jugador mediante un cono de visión
  configurable (ángulo, distancia) verificado con raycast de line-of-sight (patrón ya
  esbozado, sin implementar, en `Barquito/Pathfinding.cs`).
- **FR-003**: El sistema MUST tener un estado de "Alerta" distinto de "Evading" (el
  evade actual de Gallina es huida; sigilo necesita lo opuesto — el enemigo se
  acerca/investiga).
- **FR-004**: El enemigo MUST volver a Patrolling si pierde al jugador por un timeout
  configurable, reusando `SetWaypoints()` para retomar su ruta original.
- **FR-005**: System MUST [NEEDS CLARIFICATION: consecuencia de detección — daño,
  alarma a otros enemigos, o abrir un game-over/checkpoint como hace `Player.Die()`]

### Key Entities

- **EnemigoSigilo** (`: PatrollingAgent`): agrega campos de cono de visión
  (`visionAngle`, `visionRange`, `layerMask` para el raycast de LoS) y el estado
  Alerta.
- **Cono de visión**: no es una entidad de datos persistente, es lógica de
  `ShouldEvade()`/un nuevo método análogo evaluada por frame o por intervalo (definir
  frecuencia en el plan, por costo de raycasts).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El enemigo detecta al jugador solo dentro de su cono configurado —
  verificado a mano desde múltiples ángulos en el editor.
- **SC-002**: El enemigo abandona la persecución y retoma su ruta original tras el
  timeout de pérdida de contacto, sin quedar en un estado roto.
- **SC-003**: Reusa ≥80% de la lógica de `PatrollingAgent` sin duplicar código de
  patrulla/NavMesh (medido por revisión de código, no automatizado).

## Assumptions

- Se prioriza reusar `PatrollingAgent` en vez de escribir un sistema de IA paralelo,
  siguiendo la convención de "no reinventar" del proyecto.
- El diseño de gameplay fino (consecuencia de detección, sonido vs. solo visión)
  necesita una decisión de Diego antes de `/speckit-plan` — esta spec deja el
  esqueleto técnico listo pero no inventa reglas de diseño que no fueron pedidas.
- No hay arte/animación de Spine para este enemigo todavía — asumir que es un
  placeholder de gameplay (capsule/prefab genérico) hasta que se defina el visual,
  igual que el proyecto ya trata otros sistemas nuevos primero en gris.
