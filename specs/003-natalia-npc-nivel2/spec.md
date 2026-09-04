# Feature Specification: Natalia — NPC de Nivel 2

**Feature Branch**: `003-natalia-npc-nivel2`

**Created**: 2026-09-04

**Status**: Draft

**Input**: Idea del equipo (Diego): "spine-animar a Natalia, NPC de nivel 2".
Investigado contra el código actual antes de escribir esta spec — ver
`docs/claude/quests-y-dialogos.md` y `docs/claude/nivel2-y-ui.md`.

## Contexto (de la investigación de código)

Natalia no existe en absoluto todavía (grep exhaustivo sobre scripts, assets de
quest/diálogo, escenas y tablas de localización: cero resultados) — es una feature
100% nueva, no un fix. El proyecto tiene un patrón maduro y repetido 4 veces (Abuela,
Dalia, Tiburcio, Norberto) para agregar un NPC con diálogo + quest: un
`*DialogueTrigger.cs` (heredando `QuestDialogueTrigger` si es quest Resource estándar,
o custom si es Event-based como Tiburcio), opcionalmente una `QuestSO`, entradas en
las 3 tablas de localización (`DialogueTable_es/_en/_pt.asset` bajo
`Assets/Localization Settings/Tables/`), y si tiene movimiento propio, estados
`NPC_IdleState`/`NPC_FollowPlayerState` sobre la base `NPC.cs`. **Dos NPCs ya
existentes son stubs vacíos** (`ChinoDialogueTrigger`, `NPC_Florista`) — vale la pena
que Natalia no termine en la misma pila de trabajo a medias (ver issue de tech-debt
asociada).

Esta feature tiene una dependencia externa real: la animación Spine de Natalia la
hace Valentino (ver reparto de roles en el `CLAUDE.md` global del estudio) — el
trabajo de código puede arrancar con un placeholder visual, pero no se puede llamar
"terminada" sin el skeleton final.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - El jugador puede hablar con Natalia (Priority: P1)

Natalia está parada en algún lugar de Nivel 2 (Newspaper) y el jugador puede
interactuar con ella con E, viendo diálogo con su retrato y nombre.

**Por qué esta prioridad**: es el mínimo indispensable — sin esto no hay NPC, solo un
modelo parado en la escena.

**Independent Test**: pararse en el trigger de Natalia, presionar E, confirmar que
aparece el globo de diálogo con su nombre/retrato/texto.

**Acceptance Scenarios**:

1. **Given** el jugador dentro del trigger de Natalia, **When** presiona E, **Then**
   se muestra el diálogo configurado con su nombre y sprite.
2. **Given** el diálogo de Natalia terminó, **When** el jugador vuelve a interactuar,
   **Then** avanza al siguiente diálogo de su secuencia (mismo patrón que
   `TriggerDialogue.PasarAlSiguienteDialogo()`).

---

### User Story 2 - Natalia da una quest (Priority: P2)

Siguiendo el patrón `QuestDialogueTrigger` (si es Resource) o un trigger custom (si es
Event, como Tiburcio): Natalia pide algo, el jugador lo cumple, vuelve y recibe
recompensa.

**Por qué esta prioridad**: da propósito a la interacción más allá de charla — pero
depende de decidir QUÉ pide (ver Assumptions/NEEDS CLARIFICATION), así que va después
de la Historia 1.

**Independent Test**: completar la condición de la quest de Natalia (recurso o
evento) y verificar que su diálogo 2 (agradecimiento + recompensa) se dispara
correctamente y el `QuestSlot` refleja el progreso.

**Acceptance Scenarios**:

1. **Given** el jugador aceptó la quest de Natalia, **When** cumple la condición
   configurada, **Then** `QuestManager` marca la quest completa y el próximo diálogo
   con Natalia entrega la recompensa.

---

### User Story 3 - Natalia tiene animación Spine propia (Priority: P2)

Reemplazar el placeholder visual por el skeleton Spine final que entregue Valentino,
integrado con al menos una animación Idle (y opcionalmente talking/reaction).

**Por qué esta prioridad**: bloqueada por arte externo — el código debe estar listo
para enchufarlo apenas llegue, no bloquear el resto de la feature esperándolo.

**Independent Test**: con el skeleton importado, confirmar que el `SkeletonAnimation`
de Natalia reproduce Idle en loop sin errores de consola.

**Acceptance Scenarios**:

1. **Given** el skeleton Spine de Natalia importado, **When** la escena carga,
   **Then** Natalia se anima en Idle sin warnings de referencias faltantes.

---

### Edge Cases

- ¿Qué pasa si el jugador le habla a Natalia antes de que su quest esté disponible
  (ej. depende de progreso previo, como Norberto depende de `OnAbuelaUnfold`)? →
  `[NEEDS CLARIFICATION: Natalia tiene prerequisitos de progreso, o está disponible
  desde que arranca Nivel 2?]`
- Si el skeleton Spine todavía no llegó, ¿qué representa a Natalia en la escena
  mientras tanto (sprite estático, cápsula gris, modelo temporal)? Definir esto en el
  plan para no bloquear User Story 1/2 por User Story 3.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST exponer un trigger de diálogo para Natalia siguiendo el
  patrón `TriggerDialogue`/`QuestDialogueTrigger` ya usado por los otros 4 NPCs.
- **FR-002**: El sistema MUST registrar sus líneas de diálogo en las 3 tablas de
  localización (es/en/pt) — no hardcodear texto en español solamente (rompería la
  paridad con `English-version`/`localization`, ver roadmap).
- **FR-003**: System MUST [NEEDS CLARIFICATION: condición de la quest de Natalia —
  qué recurso o evento la completa, y qué recompensa da. No especificado en el pedido
  original].
- **FR-004**: El GameObject de Natalia MUST tener un `SkeletonAnimation` listo para
  recibir el skeleton final de Spine sin refactor adicional (mismo patrón que Kami/
  otros NPCs Spine).
- **FR-005**: Natalia MUST ubicarse en `Level2_Newspaper.unity` (no en Nivel 1).

### Key Entities

- **NataliaDialogueTrigger**: nuevo script, hereda de `QuestDialogueTrigger` o
  `TriggerDialogue` según se resuelva el NEEDS CLARIFICATION de la quest.
- **QuestNN_Natalia** (si aplica): nueva `QuestSO`, mismo molde que las 4 existentes.
- **DialogueSO × N**: una entrada por línea de diálogo de Natalia.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El jugador completa una conversación completa con Natalia (todas las
  líneas) sin errores de consola ni texto sin traducir.
- **SC-002**: La quest de Natalia (si tiene una) se completa y entrega recompensa
  correctamente en una corrida de prueba manual.
- **SC-003**: Cero placeholders de arte visibles en el build final — el skeleton Spine
  de Valentino reemplaza cualquier stand-in antes del release de Nivel 2.

## Assumptions

- Se asume que Natalia tiene una quest simple, siguiendo el molde de las 4 existentes
  (Resource o Event), no un sistema nuevo — si el diseño real es más ambicioso, esta
  spec necesita revisarse antes de `/speckit-plan`.
- El trabajo de código (triggers, quest, localización) puede avanzar en paralelo a la
  animación Spine — no son secuenciales, son historias independientes (P1/P2 primero,
  P2 de arte cuando Valentino entregue el skeleton).
- Natalia no reemplaza ni modifica ningún NPC existente — es puramente aditiva.
