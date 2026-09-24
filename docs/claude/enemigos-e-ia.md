# Enemigos e IA

## PatrollingAgent + GallinaAgent (NavMesh, agosto 2026) — el sistema nuevo

`Assets/Scripts/AI/PatrollingAgent.cs` es una clase base abstracta que **requiere
`NavMeshAgent`** (`[RequireComponent(typeof(NavMeshAgent))]`) y reemplazó al viejo FSM
de gallinas (`Enemies/GallinaAI.cs` + `GallinaWalkState`/`GallinaEvadeState`, borrados
en el commit "new patrolling ai 4 gallinas"). `Assets/Scripts/AI/GallinaAgent.cs`
hereda de `PatrollingAgent`. El NavMesh de `Nivel1_KamiPapelTijera.unity` fue baked
recién en ese mismo commit (`NavMesh.asset`, nuevo).

**Estados** (enum en PatrollingAgent): `Idle`, `Patrolling`, `Crossing`, `Evading`.

**Mecánica de patrulla**: `waypoints` (`List<Transform>`) recorridos en secuencia;
llegada detectada vía `navAgent.remainingDistance <= navAgent.stoppingDistance`
(default 0.5). `SetWaypoints(Transform[])` resetea la secuencia y llama
`navAgent.SetDestination()`. Cada waypoint se valida contra el NavMesh con
`NavMesh.SamplePosition()` (radio de búsqueda 5m) para que no queden off-mesh.

**Puntos de extensión virtuales** (para subclases):
- `OnWaypointReached()` — devolver `true` si la subclase ya cambió los waypoints (así
  la base no auto-avanza).
- `ShouldEvade()` — devolver `true` para entrar en evade.
- `UpdateEvadeDestination()` — se llama todos los frames durante evade.
- `OnEvadeStart()` — se llama una vez al entrar en evade (ej. sonido).

**GallinaAgent-específico**: tres zonas de patrulla (`preTreeWaypoints`, cruce del
árbol vía `treeStart/Center/End`, `postTreeWaypoints`), suscripto a
`EventManager.OnTreeCutForChickens` (dispara al cortar el árbol de la quest de
Tiburcio — ver `quests-y-dialogos.md`) para pasar de "antes del árbol" a "cruzando" a
"después del árbol" (zona segura, ya no evade). Evade es por distancia
(`evadeDistance`, default 5) salvo durante el cruce o en la zona segura; el destino de
evade se samplea sobre NavMesh.

**Para un enemigo stealth futuro**: `PatrollingAgent` es la base lista para heredar.
Un `StealthEnemy : PatrollingAgent` reutilizaría patrulla + NavMesh y solo necesitaría
sobreescribir `ShouldEvade()`/`UpdateEvadeDestination()` con detección real (cono de
visión, distancia de oído) más un estado intermedio "Alerta" entre Patrolling y
Evading. Hoy **no existe ningún sistema de detección** (vision cone, line-of-sight,
hearing) en el proyecto — se buscó explícitamente y no hay precedente, salvo un
método `InLineOfSight()` comentado (nunca implementado) en `Barquito/Pathfinding.cs`.

## Rocoso — FSM + NavMeshAgent (migrado, issue #21)

`Assets/Scripts/Enemies/Rocoso.cs` + `FiniteStateMachine.cs` (5 estados: Sleep, Start,
Walk, Attack, Death — `IState` genérico, mismo patrón que usan Barquito y algunos NPC).

**Movimiento actual**: `NavMeshAgent` propio (`[RequireComponent(typeof(NavMeshAgent))]`
en `Rocoso.cs`, campo público `navAgent`, cableado directo — Rocoso NO hereda
`PatrollingAgent`, esa base es para patrulla por waypoints y Rocoso solo persigue).
`RocosoWalkState.WalkTowardsPlayer()` llama `navAgent.SetDestination(target)` cada
frame; `OnEnter`/`OnExit` de Walk son el único lugar que togglea `isStopped` (retoma en
`false` al entrar, frena en `true` al salir — cubre Attack/Sleep/Death/Start sin tocar
esos archivos). El giro sigue siendo `SetFacing()` (180° instantáneo, ver `spine-kami.md`
para el patrón), por eso `navAgent.updateRotation = false`. El `Rigidbody` (`myRigidbody`)
queda en el GO pero `isKinematic = true` desde `Start()` — si no, la física de Unity le
pelea la posición al NavMeshAgent (jitter); ya no se usa para moverlo, solo pudo quedar
para colisión física con el jugador u otro uso.

**Reposicionamiento si el jugador está inalcanzable por altura**: `alturaInalcanzable`
(tuneable en el inspector, default 2.5) — si Kami está más alto que eso respecto a
Rocoso (subida a una plataforma), `Rocoso.PlayerEsInalcanzablePorAltura()` da `true` y
`RocosoWalkState` deja de perseguirla directamente. En vez de quedarse quieto contra la
base de la plataforma, busca el nodo más cercano de `_nodosEstrategicos` (array de
`Transform` ubicados a mano en la escena, misma página que Rocoso — pensado para la
represa: así Rocoso queda bien parado debajo cuando Kami la tira) y camina hasta ahí
UNA sola vez por "sesión" de inalcanzable (`RocosoWalkState._nodoEstrategicoEvaluado`
evita recalcular o titubear si ella se mueve un poco arriba; recién se reevalúa cuando
vuelve a ser alcanzable y sube de nuevo). Si `_nodosEstrategicos` está vacío, cae al
comportamiento viejo de quedarse quieto en el lugar (`navAgent.ResetPath()`), con
warning. Mientras es inalcanzable tampoco se dispara la transición a Attack (gate
`!inalcanzable &&` antes del chequeo de `enterAttackRange`) — sin ese gate,
`DistanceToPlayer()` (distancia 3D, incluye Y) podría dar un valor chico por cercanía
horizontal y Rocoso "atacaría" a través de la plataforma sin poder llegar.

**Tuning** (`Rocoso.cs`): `enterAttackRange` (11), `exitAttackRange` (30),
`viewRange` (60, distancia a la que despierta del sleep), `alturaInalcanzable` (2.5),
`_nodosEstrategicos` (array de `Transform`, vacío por default — hay que ubicarlos a
mano en la escena, mismo patrón que los waypoints de `GallinaAgent`).

**Ataque**: `RocosoHeadbuttHitBox` — mismo patrón que `TijeraHitbox`
(`audio-y-particulas.md`): collider trigger deshabilitado por default, se habilita
~0.2s durante la animación de headbutt, chequea `IGolpeable` en `OnTriggerEnter` y
marca un flag (`didHit`) que el estado de ataque revisa al terminar la animación.

**Agua**: `Rocoso.GetWet(wetDamage)` dispara una corrutina de daño por ahogo (daño
cada 0.8s) — llamado directo desde el sistema de río, sin implementar `IMojable`
explícitamente (a diferencia de Kami, ver `spine-kami.md` sobre `Rio.cs`/`IMojable`).

**Veredicto NavMesh**: migrado. Rocoso persigue con su propio `NavMeshAgent`, gira con
`SetFacing` (no con `updateRotation`) y espera abajo si el jugador está a más de
`alturaInalcanzable` unidades de altura (tuneable en el inspector).

**Dependencia de NavMesh bakeado por página**: Rocoso necesita que la página en la que
vive tenga un `NavMeshData` bakeado y activo — lo resuelve `PageNavMeshManager`
(`Assets/Scripts/AI/`), desacoplado de Rocoso. Las plataformas que Kami puede escalar
pero Rocoso no se marcan con un NavMesh Area custom excluido del `areaMask` del
`NavMeshAgent` de Rocoso (paso de Editor, no de código).

## EnemySpawner — no usado todavía

`Assets/Scripts/Enemies/EnemySpawner.cs`: singleton con prefab de `Enemy` genérico,
cantidad configurable, randomización de stats (HP/AttackDamage/Speed) y array de
`SpawnPoint[]`. El propio código lo marca ("esto todavia no se usa para nada") — está
armado pero sin caller activo en el gameplay actual.

## Barquito — un tercer paradigma de movimiento (A* por nodos, no NavMesh)

`Assets/Scripts/Barquito/BarquitoBehaviour.cs`: FSM (Idle/Moving) + A* propio sobre un
grafo de `Node` colocados a mano en la escena (`Pathfinding.cs`), con steering
`Arrive()` (desaceleración suave al acercarse al destino). No usa NavMesh ni Rigidbody
— es su propio sistema, pensado para el bote NPC. El proyecto convive con **dos
paradigmas de movimiento distintos** (NavMeshAgent en Gallina y Rocoso, cada uno con
su propio wiring — Rocoso no hereda `PatrollingAgent`; A* por nodos en Barquito).

## NPCs (Natalia, Abuela) — también NavMesh desde 2026-09-24

`NPC` (`Assets/Scripts/NPCs/NPC.cs`) dejó de moverse con steering propio
(`AddForce`/`Arrive` integrando `velocity` sobre `transform.position`) y ahora maneja su
propio `NavMeshAgent`, igual que Rocoso. O sea, el proyecto quedó con **dos paradigmas**,
no tres: `PatrollingAgent` para patrulla por waypoints (Gallina, y el `PoliceOfficer` de
la página 4 cuando se implemente), y `NavMeshAgent` propio para lo que persigue un blanco
que se mueve (Rocoso persiguiendo a Kami, los NPC siguiéndola). Barquito sigue siendo la
excepción con su A* por nodos.

Detalle del cambio y el requisito de bakear NavMesh por página: ver
`docs/claude/quests-y-dialogos.md`.

## Patrón de daño (IGolpeable)

`Assets/Scripts/AttackHitBoxes/IGolpeable.cs` — interfaz mínima
(`void GetGolpeado(float dmg)`) que implementan Player y los enemigos golpeables.
Todas las hitboxes de ataque (Rocoso headbutt, tijera de Kami — ver
`audio-y-particulas.md` sobre `TijeraHitbox`) siguen el mismo patrón: collider
trigger que arranca deshabilitado, se prende durante la ventana de la animación, y
usa un flag booleano ("¿pegó?") en vez de resolver el daño directo en el evento del
trigger — así el estado que disparó el ataque decide qué hacer después de que termina
la animación, no el hitbox.
