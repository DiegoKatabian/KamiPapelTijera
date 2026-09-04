# Feature Specification: Rocoso sobre NavMesh

**Feature Branch**: `001-rocoso-navmesh`

**Created**: 2026-09-04

**Status**: Draft

**Input**: Idea del equipo (Diego): "aplicar navmesh al rocoso". Investigado contra el
código actual antes de escribir esta spec — ver `docs/claude/enemigos-e-ia.md`.

## Contexto (de la investigación de código)

`Rocoso.cs` mueve al enemigo con `Rigidbody.AddForce()` cada frame hacia la posición
del jugador (persecución directa, sin path planning) dentro de un FSM de 5 estados
(Sleep → Start → Walk → Attack → Death, ver `FiniteStateMachine.cs`/`IState`). Esto es
**incompatible en el mismo componente** con `NavMeshAgent`, que espera control
cinemático del transform, no fuerzas físicas. El proyecto YA tiene una base NavMesh
funcionando: `Assets/Scripts/AI/PatrollingAgent.cs` (recién creada para las gallinas),
con patrulla por waypoints, detección de llegada vía `remainingDistance`, y un NavMesh
baked en `Nivel1_KamiPapelTijera.unity`. Ese NavMesh fue pensado para las gallinas —
no hay garantía de que cubra bien el área de Rocoso ni que esté baked considerando su
radio de colisión; hay que confirmarlo en el editor, no asumirlo.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rocoso persigue esquivando obstáculos (Priority: P1)

Hoy Rocoso persigue en línea recta y puede quedar trabado contra geometría porque no
tiene noción de camino, solo de fuerza hacia el jugador. Con NavMesh, Rocoso debería
rodear obstáculos del escenario para llegar al jugador, igual que ya hacen las
gallinas.

**Por qué esta prioridad**: es el problema concreto que motiva la feature — un
enemigo de mundo 3D que no puede quedar pegado a una pared cuando el jugador lo evade
detrás de un obstáculo.

**Test independiente**: pararse detrás de un obstáculo grande dentro del `viewRange`
de Rocoso estando despierto; verificar que camina alrededor en vez de empujar contra
la geometría.

**Acceptance Scenarios**:

1. **Given** Rocoso despierto y persiguiendo, **When** el jugador se mete detrás de un
   obstáculo grande, **Then** Rocoso navega alrededor del obstáculo hacia el jugador
   en vez de quedar empujando contra él.
2. **Given** Rocoso en estado Walk, **When** el jugador sale de `viewRange`, **Then**
   Rocoso deja de perseguir y vuelve a Sleep (comportamiento actual preservado).

---

### User Story 2 - Las transiciones de estado existentes siguen andando (Priority: P1)

El refactor no debe romper Sleep/Start/Attack/Death — solo el movimiento del estado
Walk cambia de mecanismo (fuerza → NavMeshAgent). Las animaciones, el headbutt
(`RocosoHeadbuttHitBox`), el ahogo por agua (`GetWet()`) y la muerte deben funcionar
exactamente igual que hoy.

**Por qué esta prioridad**: es una migración de movimiento, no un rediseño de
comportamiento — el riesgo es romper algo que ya funciona (animaciones sincronizadas
con callbacks del Animator, el flag `didHit` del headbutt).

**Test independiente**: repetir el ciclo completo (despertar → perseguir → atacar →
recibir daño → morir) y comparar contra el comportamiento pre-refactor.

**Acceptance Scenarios**:

1. **Given** Rocoso en `enterAttackRange`, **When** entra en Attack, **Then** el
   headbutt se habilita/deshabilita en la misma ventana de animación que hoy.
2. **Given** Rocoso tocado por agua, **When** se dispara `GetWet()`, **Then** el daño
   por ahogo sigue aplicándose igual (esto no depende del sistema de movimiento).

---

### User Story 3 (opcional, evaluar con Diego) - Rocoso patrulla cuando no ve al jugador (Priority: P3)

Ya que se migra a `PatrollingAgent`, Rocoso podría patrullar una zona en vez de dormir
inmóvil hasta `viewRange`. **Fuera de alcance del refactor mínimo** — anotar como
posible follow-up, no bloquea las Historias 1 y 2.

---

### Edge Cases

- ¿Qué pasa si el jugador está fuera del NavMesh baked (ej. arriba de una plataforma
  sin bake)? `NavMeshAgent.SetDestination()` a un punto inválido no debe crashear ni
  dejar a Rocoso congelado sin log — usar el mismo patrón de `NavMesh.SamplePosition()`
  con radio de búsqueda que ya usa `PatrollingAgent`.
- ¿Qué pasa durante el Attack state, cuando Rocoso está parado pegando el headbutt?
  El `NavMeshAgent` no debe seguir intentando moverse mientras ataca — replicar el
  patrón `navAgent.isStopped = true` / `false` en las transiciones Walk↔Attack.
- ¿El NavMesh existente (baked para gallinas) cubre el área donde vive Rocoso? Si no,
  hay que re-bakear antes de que el refactor de código sirva de algo — esto es un
  paso de escena/editor, no de código, y debe verificarse primero.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Rocoso MUST navegar hacia el jugador usando `NavMeshAgent` en vez de
  `Rigidbody.AddForce()` durante el estado Walk.
- **FR-002**: El sistema MUST detener el `NavMeshAgent` (`isStopped = true`) al entrar
  en Attack, y reanudarlo al volver a Walk.
- **FR-003**: Las transiciones Sleep→Start→Walk→Attack→Death MUST preservar exactamente
  su lógica actual (rangos `enterAttackRange`/`exitAttackRange`/`viewRange`, callbacks
  de animación) — este refactor toca movimiento, no el árbol de estados.
- **FR-004**: El componente `Rigidbody` de Rocoso MUST eliminarse o quedar inerte para
  el movimiento (si otro sistema depende de física para colisión/daño, evaluar caso
  por caso en el plan — no asumir).
- **FR-005**: El NavMesh que cubre el área de Rocoso MUST verificarse/re-bakearse en
  el editor antes de dar la feature por terminada — no es un paso de código.

### Key Entities

- **Rocoso** (MonoBehaviour + FSM existente): pasa a requerir `NavMeshAgent` en vez de
  (o adicional a) `Rigidbody`.
- **RocosoWalkState** (`IState`): su lógica de movimiento se reescribe para llamar
  `navAgent.SetDestination(player.position)` en vez de `AddForce`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Rocoso rodea un obstáculo de prueba para llegar al jugador en vez de
  quedar trabado contra él (verificado a mano en el editor, no hay CLI de build).
- **SC-002**: El ciclo completo Sleep→Walk→Attack→Death se comporta igual que antes
  del refactor en una sesión de prueba manual.
- **SC-003**: Cero regresiones reportadas en el headbutt (`didHit` flag) o en el daño
  por agua tras el cambio.

## Assumptions

- El NavMesh actual de `Nivel1_KamiPapelTijera.unity` puede necesitar un re-bake que
  incluya el área de Rocoso; se asume que esto lo hace Diego en el editor como parte
  del trabajo, no algo automatizable desde acá.
- No hay múltiples Rocosos simultáneos en pantalla hoy — el costo de CPU de
  `NavMeshAgent` no es una preocupación de performance para esta migración.
- Se sigue el patrón de `PatrollingAgent` (mismo proyecto) en vez de inventar uno
  nuevo, para mantener consistencia — pero Rocoso NO necesita heredar de
  `PatrollingAgent` si su árbol de estados (FSM con `IState`) no encaja con el de
  `PatrollingAgent` (enum de estados propio); evaluar en el plan si conviene
  composición (un `NavMeshAgent` más FSM existente) en vez de herencia.
