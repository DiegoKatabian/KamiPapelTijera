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

## Rocoso — FSM + física, todavía NO listo para NavMesh

`Assets/Scripts/Enemies/Rocoso.cs` + `FiniteStateMachine.cs` (5 estados: Sleep, Start,
Walk, Attack, Death — `IState` genérico, mismo patrón que usan Barquito y algunos NPC).

**Movimiento actual**: `Rigidbody.AddForce()` cada frame hacia el jugador
(`RocosoWalkState`, `ForceMode.VelocityChange`) — sin path planning, persecución
directa. Esto es **directamente incompatible con `NavMeshAgent`** (que espera control
cinemático del transform, no física por fuerzas): darle NavMesh a Rocoso no es
"agregar un componente", es sacar el `Rigidbody` de la ecuación de movimiento y portar
la detección de llegada/estado a la API de `NavMeshAgent` (`remainingDistance` como
hace `PatrollingAgent`).

**Tuning** (`Rocoso.cs`): `enterAttackRange` (11), `exitAttackRange` (30),
`viewRange` (60, distancia a la que despierta del sleep).

**Ataque**: `RocosoHeadbuttHitBox` — mismo patrón que `TijeraHitbox`
(`audio-y-particulas.md`): collider trigger deshabilitado por default, se habilita
~0.2s durante la animación de headbutt, chequea `IGolpeable` en `OnTriggerEnter` y
marca un flag (`didHit`) que el estado de ataque revisa al terminar la animación.

**Agua**: `Rocoso.GetWet(wetDamage)` dispara una corrutina de daño por ahogo (daño
cada 0.8s) — llamado directo desde el sistema de río, sin implementar `IMojable`
explícitamente (a diferencia de Kami, ver `spine-kami.md` sobre `Rio.cs`/`IMojable`).

**Veredicto NavMesh**: el NavMesh recién baked en Nivel1 parece pensado para las
gallinas — no hay evidencia de que Rocoso ya lo use. Migrarlo es un refactor real, no
un flag para prender (ver plan de la feature "Rocoso NavMesh" en `specs/`).

## EnemySpawner — no usado todavía

`Assets/Scripts/Enemies/EnemySpawner.cs`: singleton con prefab de `Enemy` genérico,
cantidad configurable, randomización de stats (HP/AttackDamage/Speed) y array de
`SpawnPoint[]`. El propio código lo marca ("esto todavia no se usa para nada") — está
armado pero sin caller activo en el gameplay actual.

## Barquito — un tercer paradigma de movimiento (A* por nodos, no NavMesh)

`Assets/Scripts/Barquito/BarquitoBehaviour.cs`: FSM (Idle/Moving) + A* propio sobre un
grafo de `Node` colocados a mano en la escena (`Pathfinding.cs`), con steering
`Arrive()` (desaceleración suave al acercarse al destino). No usa NavMesh ni Rigidbody
— es su propio sistema, pensado para el bote NPC. Vale la pena saber que el proyecto
ya tiene **tres paradigmas de movimiento distintos** conviviendo (NavMeshAgent en
Gallina, física por fuerzas en Rocoso, A* por nodos en Barquito) — al planear el
refactor de Rocoso, no asumir que "como Gallina ya tiene NavMesh, es trivial".

## Patrón de daño (IGolpeable)

`Assets/Scripts/AttackHitBoxes/IGolpeable.cs` — interfaz mínima
(`void GetGolpeado(float dmg)`) que implementan Player y los enemigos golpeables.
Todas las hitboxes de ataque (Rocoso headbutt, tijera de Kami — ver
`audio-y-particulas.md` sobre `TijeraHitbox`) siguen el mismo patrón: collider
trigger que arranca deshabilitado, se prende durante la ventana de la animación, y
usa un flag booleano ("¿pegó?") en vez de resolver el daño directo en el evento del
trigger — así el estado que disparó el ataque decide qué hacer después de que termina
la animación, no el hitbox.
