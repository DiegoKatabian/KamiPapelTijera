using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Periodically instantiates an obstacle prefab from a TrafficObstacleSet between
// spawnPoint and despawnPoint. One spawner = one street segment/lane; dragging this
// prefab into a scene and assigning an obstacle set + the two points is the whole
// setup (see CLAUDE.md "Design constraint: prefab- and ScriptableObject-first").
public class TrafficSpawner : MonoBehaviour
{
    [Tooltip("Where new obstacles appear.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Where obstacles move toward before despawning.")]
    [SerializeField] private Transform despawnPoint;

    [Tooltip("Which prefab variants, travel duration and spawn interval this street segment uses.")]
    [SerializeField] private TrafficObstacleSet obstacleSet;

    [Tooltip("Parent for spawned obstacles in the Hierarchy. Defaults to this spawner's own transform.")]
    [SerializeField] private Transform spawnedObstacleParent;

    [Tooltip("Spawn one obstacle immediately on enable instead of waiting out the first interval. Useful so a street isn't empty the moment the page opens.")]
    [SerializeField] private bool spawnOneImmediately = true;

    private Coroutine _spawnRoutine;
    private readonly List<GameObject> _alive = new List<GameObject>();

    private void OnEnable()
    {
        if (obstacleSet == null)
        {
            //Debug.LogWarning($"[TrafficSpawner] {name} has no obstacleSet assigned, will not spawn anything.");
            return;
        }

        if (spawnPoint == null || despawnPoint == null)
        {
            //Debug.LogWarning($"[TrafficSpawner] {name} is missing spawnPoint/despawnPoint, will not spawn anything.");
            return;
        }

        _spawnRoutine = StartCoroutine(SpawnLoop());

        float segmentLength = Vector3.Distance(spawnPoint.position, despawnPoint.position);
        //Debug.Log($"[TrafficSpawner] {name} started spawning from set '{obstacleSet.name}' over a {segmentLength:F1} unit segment (max {obstacleSet.MaxAlive} alive).");
    }

    private void OnDisable()
    {
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        if (spawnOneImmediately)
        {
            SpawnObstacle();
        }

        while (true)
        {
            yield return new WaitForSeconds(obstacleSet.GetRandomSpawnInterval());
            SpawnObstacle();
        }
    }

    private void SpawnObstacle()
    {
        // Destroyed obstacles leave null entries behind — clearing them here is what keeps
        // the cap meaningful instead of permanently blocking spawns after a few despawns.
        _alive.RemoveAll(obstacle => obstacle == null);

        if (_alive.Count >= obstacleSet.MaxAlive)
        {
            //Debug.Log($"[TrafficSpawner] {name} skipped a spawn: already at the cap of {obstacleSet.MaxAlive} alive obstacles.");
            return;
        }

        GameObject prefab = obstacleSet.GetRandomPrefab();
        if (prefab == null)
        {
            return; // TrafficObstacleSet already logged the warning
        }

        Transform parent = spawnedObstacleParent != null ? spawnedObstacleParent : transform;
        GameObject instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, parent);

        TrafficObstacle obstacle = instance.GetComponent<TrafficObstacle>();
        if (obstacle == null)
        {
            //Debug.LogWarning($"[TrafficSpawner] Prefab {prefab.name} has no TrafficObstacle component, destroying the spawned instance.");
            Destroy(instance);
            return;
        }

        _alive.Add(instance);
        obstacle.Launch(despawnPoint.position, obstacleSet.GetRandomTravelDuration(), obstacleSet.MaxLifetime);
        //Debug.Log($"[TrafficSpawner] {name} spawned {prefab.name} ({_alive.Count}/{obstacleSet.MaxAlive} alive).");
    }
}
