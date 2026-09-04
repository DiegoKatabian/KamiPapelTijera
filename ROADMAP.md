# Roadmap — Kami: Papel y Tijera

Generado el 2026-09-04 tras una auditoría completa de código (agentes de exploración +
grafo de conocimiento con graphify) sobre `feature/spine-animations`. Vive junto a los
issues de GitHub — este documento es el resumen navegable, los issues son la unidad de
trabajo. Milestones en GitHub: https://github.com/DiegoKatabian/KamiPapelTijera/milestones

## Cómo se armó este roadmap

1. Grafo de conocimiento del código (`graphify-out/`, regenerar con `/graphify
   Assets/Scripts --update`) para mapear god nodes, comunidades y conexiones no obvias.
2. Tres agentes de exploración en paralelo leyeron a fondo: (a) IA/enemigos, (b) nivel 2
   + UI, (c) quests/diálogos — resultados volcados en `docs/claude/enemigos-e-ia.md`,
   `docs/claude/nivel2-y-ui.md`, `docs/claude/quests-y-dialogos.md`.
3. Revisión de historial de commits y de las 20+ branches remotas para entender qué
   está en progreso, qué quedó abandonado, y qué es fundacional (ej. Nivel 3/4 ya
   tienen escenas placeholder en branches sin mergear).
4. Spec Kit (`.specify/`, `specs/`) adoptado para las 3 features futuras que
   nombró Diego — cada una tiene spec completa antes de tocar código.

## Milestone 1 — Nivel 2: Newspaper (en curso)

El trabajo actual. Página del museo es la pieza claramente inacabada; el resto del
nivel (page-turn, inventario, quests, cámara, Flap UI) ya es estable.

- [#16](https://github.com/DiegoKatabian/KamiPapelTijera/issues/16) Terminar la página del Museo
- [#17](https://github.com/DiegoKatabian/KamiPapelTijera/issues/17) Botas de agua: falta skin de Spine
- [#18](https://github.com/DiegoKatabian/KamiPapelTijera/issues/18) ChinoDialogueTrigger es un stub vacío
- [#19](https://github.com/DiegoKatabian/KamiPapelTijera/issues/19) NPC_Florista es un stub vacío
- [#20](https://github.com/DiegoKatabian/KamiPapelTijera/issues/20) Setup pendiente: DefeatOverlay + keys de localización

## Milestone 2 — Enemigos v2: NavMesh + Sigilo

Las dos ideas de Diego para el sistema de enemigos, ambas con spec en `specs/`. El
punto de apoyo común es `PatrollingAgent` (`Assets/Scripts/AI/PatrollingAgent.cs`),
la base NavMesh recién creada para las gallinas — sólida, reusable, y ya probada en
producción.

- [#21](https://github.com/DiegoKatabian/KamiPapelTijera/issues/21) Migrar Rocoso a NavMeshAgent — spec [`specs/001-rocoso-navmesh`](specs/001-rocoso-navmesh/spec.md). **Refactor real, no un flag**: Rocoso mueve por `Rigidbody.AddForce()`, directamente incompatible con `NavMeshAgent` en el mismo objeto.
- [#22](https://github.com/DiegoKatabian/KamiPapelTijera/issues/22) Enemigo de sigilo — spec [`specs/002-enemigo-sigilo`](specs/002-enemigo-sigilo/spec.md). Sin precedente de detección en el proyecto — se construye desde cero sobre `PatrollingAgent`. Tiene 2 preguntas abiertas para Diego antes de planear.
- [#23](https://github.com/DiegoKatabian/KamiPapelTijera/issues/23) Decidir destino de `EnemySpawner` (armado, nunca usado — candidato a reusar para el enemigo de sigilo)

## Milestone 3 — Natalia: NPC de Nivel 2

- [#24](https://github.com/DiegoKatabian/KamiPapelTijera/issues/24) Implementar Natalia — spec [`specs/003-natalia-npc-nivel2`](specs/003-natalia-npc-nivel2/spec.md). No existe nada de Natalia hoy (grep exhaustivo: cero resultados) — sigue el patrón ya usado 4 veces (Abuela/Dalia/Tiburcio/Norberto). El trabajo de código no está bloqueado por el arte de Valentino — son historias independientes.

## Milestone 4 — Deuda técnica y pulido

Cabos sueltos encontrados durante la auditoría, ninguno urgente, todos anotados para
no perderlos.

- [#25](https://github.com/DiegoKatabian/KamiPapelTijera/issues/25) Tiburcio: `treeWasCut` sin fallback (bajo riesgo)
- [#26](https://github.com/DiegoKatabian/KamiPapelTijera/issues/26) Exportar animación `PullSolapasReverse`
- [#27](https://github.com/DiegoKatabian/KamiPapelTijera/issues/27) Decidir destino de las branches `localization` y `English-version`
- [#28](https://github.com/DiegoKatabian/KamiPapelTijera/issues/28) Mantener actualizado el grafo de conocimiento y las specs

## Milestone 5 — Niveles 3 y 4 (futuro, sin fecha)

- [#29](https://github.com/DiegoKatabian/KamiPapelTijera/issues/29) Planificar contenido — hoy son escenas placeholder duplicadas de `SampleScene` en branches `level3-scenes`/`level4-scenes` sin mergear.

## Hallazgos que no ameritaron issue propio (contexto para el equipo)

- **Tres paradigmas de movimiento conviven** en el codebase: NavMeshAgent (Gallina),
  física por fuerzas (Rocoso), A* por nodos manuales (Barquito). Vale tenerlo en mente
  al evaluar cualquier refactor de IA — "ya funciona en X" no siempre se traslada a Y.
  Ver `docs/claude/enemigos-e-ia.md`.
- El escenario de trabajo de Nivel 1 cambió de nombre/archivo: `Nivel1_KamiPapelTijera.unity`
  es la escena activa desde fines de agosto 2026, no `Nivel1_LaRural SpineTest.unity`
  (quedó vieja). Ya corregido en `CLAUDE.md`.
- Las tablas de localización viven en `Assets/Localization Settings/Tables/`, no en
  `Assets/Localization/` (nombre de carpeta con espacio, fácil de errar). Ya anotado en
  `docs/claude/quests-y-dialogos.md`.

## Herramientas que quedaron instaladas en el repo

- **graphify** (`graphify-out/`, gitignoreado — regenerable): grafo de conocimiento de
  `Assets/Scripts`. `graphify-out/graph.html` es navegable en cualquier browser sin
  server. Correr `/graphify Assets/Scripts --update` después de cambios grandes de
  arquitectura para no perder la foto actualizada.
- **Spec Kit** (`.specify/`, `specs/`): constitución del proyecto en
  `.specify/memory/constitution.md`; flujo `/speckit-specify` → `/speckit-plan` →
  `/speckit-tasks` → `/speckit-implement` para la próxima feature grande.
