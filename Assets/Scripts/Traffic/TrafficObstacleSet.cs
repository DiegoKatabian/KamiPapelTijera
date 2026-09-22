using UnityEngine;

// Data asset for a street segment's ambient traffic: which prefab variants a
// TrafficSpawner using this set can spawn, and the timing ranges to pick from — so a
// level designer authors one asset per street rather than tuning raw numbers on every
// spawner instance (see CLAUDE.md "Convenciones de codigo").
//
// Timing is authored as TRAVEL DURATION (seconds to cross the segment), not speed in
// units/second: the spawn->despawn distance changes per street, and a speed that looks
// right on a short segment crawls on a long one. Duration reads the same on any segment
// and the spawner derives the per-instance speed from the actual distance.
[CreateAssetMenu(fileName = "TrafficObstacleSet", menuName = "Kami/Traffic/Obstacle Set")]
public class TrafficObstacleSet : ScriptableObject
{
    [Tooltip("Obstacle prefab variants a spawner using this set can spawn (e.g. different cars). One is picked at random per spawn.")]
    [SerializeField] private GameObject[] obstaclePrefabs;

    [Tooltip("Seconds an obstacle takes to cross the whole spawn->despawn segment. A random value in this range is picked per spawn. Lower = faster traffic, independent of how long the street is.")]
    [SerializeField] private Vector2 travelDurationRange = new Vector2(5f, 8f);

    [Tooltip("Time range in seconds between spawns. A random value in this range is picked after every spawn.")]
    [SerializeField] private Vector2 spawnIntervalRange = new Vector2(3f, 6f);

    [Tooltip("Maximum obstacles alive at once for one spawner. Spawns are skipped while at the cap, so a mistuned interval can never pile obstacles on top of each other.")]
    [SerializeField] private int maxAlive = 4;

    [Tooltip("Safety net: an obstacle despawns after this many seconds no matter what, even if it never reaches its despawn point. Keep it comfortably above the longest travel duration.")]
    [SerializeField] private float maxLifetime = 30f;

    public int MaxAlive => Mathf.Max(1, maxAlive);
    public float MaxLifetime => Mathf.Max(1f, maxLifetime);

    public GameObject GetRandomPrefab()
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
        {
            Debug.LogWarning($"[TrafficObstacleSet] {name} has no obstaclePrefabs assigned.");
            return null;
        }

        return obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
    }

    public float GetRandomTravelDuration()
    {
        float shortest = Mathf.Min(travelDurationRange.x, travelDurationRange.y);
        float longest = Mathf.Max(travelDurationRange.x, travelDurationRange.y);

        if (shortest <= 0f)
        {
            Debug.LogWarning($"[TrafficObstacleSet] {name} has a travelDurationRange of ({travelDurationRange.x}, {travelDurationRange.y}); a duration of 0 or less would teleport the obstacle. Falling back to 1 second.");
            return 1f;
        }

        return Random.Range(shortest, longest);
    }

    public float GetRandomSpawnInterval()
    {
        return Random.Range(
            Mathf.Min(spawnIntervalRange.x, spawnIntervalRange.y),
            Mathf.Max(spawnIntervalRange.x, spawnIntervalRange.y));
    }
}
