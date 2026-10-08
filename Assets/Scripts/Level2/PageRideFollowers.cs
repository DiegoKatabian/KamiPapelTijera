using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Level 2 (spec 012): when Kami turns a page the followers (Natalia, the Abuela) grab on to her, fly
/// over the page with her and land next to her on the new page, following again. Without this nothing
/// carries them: the page's NavMesh is swapped under their agents at OnNewPageOpen and Kami lands at
/// the new page's entry X while they stay at the old one.
///
/// Same technique as PaperPlaneRide: stop following, the NavMeshAgent stops driving the transform
/// (updatePosition off) and a LateUpdate holds each rider at a seat around Kami. Landing is a Warp onto
/// the new page's mesh, then following again.
///
/// Boarding and landing are detected by POLLING Player.IsRidingPage, not by subscribing to
/// OnPageFinishTurning: PlayerPageSpawnManager.FinishRide has to place Kami BEFORE the followers land
/// next to her, and the order of that event's subscribers is not guaranteed. A poll one frame later
/// does not depend on it, and it also lands everyone if the ride ends in some unexpected way.
///
/// A forced turn without a ride (the page 3 arrest, TurnToNextPage(ridePage: false)) never sets
/// IsRidingPage, so nobody boards and the cutscene's own warps stay in charge.
///
/// Lives at the scene root on purpose: page folders are switched off mid-turn. Level 2 only: it is
/// placed in Level2_Newspaper.unity and nowhere else.
/// </summary>
[DefaultExecutionOrder(100)] //after Player.LateUpdate, which snaps Kami onto the page edge: otherwise the riders lag one frame behind her
public class PageRideFollowers : MonoBehaviour
{
    [Header("Seats (relative to Kami's root, X mirrored with the way she faces)")]
    [SerializeField, Tooltip("Seat per slot: the follower nearest to Kami takes the first, the next nearest the second. World units from Kami's body center (a follower's transform sits at its waist). A third follower or more continue past the last two at the same step. Read every frame, so tuning during a turn applies live.")]
    Vector3[] _seats =
    {
        new Vector3(-2.5f, -0.5f, 0.6f),
        new Vector3(-5f, -1.2f, 0.6f),
    };

    [SerializeField, Tooltip("How far each rider bobs up and down, in world units. Placeholder for the real 'holding on' pose.")]
    float _bobAmplitude = 0.35f;

    [SerializeField, Tooltip("Bob speed, in radians per second.")]
    float _bobSpeed = 6f;

    [Header("Boarding")]
    [SerializeField, Tooltip("Seconds a follower takes to fly from where it stands to its seat, so one far from Kami arrives with her instead of popping. Smoothstep. After that it tracks the seat exactly.")]
    float _boardEaseSeconds = 0.35f;

    [Header("Getting off")]
    [SerializeField, Tooltip("Distance between landed followers, in world units, along the way Kami faces (into the page). Slot 1 is the nearest to her.")]
    float _landingSpacing = 3f;

    [SerializeField, Tooltip("How far from the landing spot the NavMesh is searched, in world units. Kami drops onto the page from a spawn height, so too small a radius parks the followers until she touches the floor.")]
    float _landingSampleRadius = 5f;

    [SerializeField, Tooltip("Seconds between retries for a follower parked because there was no floor near Kami.")]
    float _retryInterval = 0.5f;

    class Rider
    {
        public NPC npc;
        public int slot; //0 = the seat nearest to Kami
        public Vector3 startPosition;
        public float boardTime;
        public float bobPhase;
        public bool parked;
        public Vector3 parkedPosition;
    }

    readonly List<Rider> _riders = new List<Rider>();
    bool _wasRiding;
    float _nextRetry;
    bool _warnedNoSeats;

    Player Kami => LevelManager.Instance != null ? LevelManager.Instance.player : null;

    void OnDisable()
    {
        //never leave a follower in the boarded state (agent not driving its transform, not following)
        if (_riders.Count > 0)
        {
            Debug.Log("[PageRideFollowers] disabled while riders were aboard: handing them back");
            ReleaseAll();
        }
        _wasRiding = false;
    }

    // ---------------------------------------------------------------- edge detection

    void Update()
    {
        Player player = Kami;
        if (player == null)
        {
            return;
        }

        bool riding = player.IsRidingPage;

        if (riding && !_wasRiding)
        {
            BoardAll(player);
        }
        else if (!riding && _wasRiding)
        {
            Debug.Log("[PageRideFollowers] the ride is over: landing the riders");
            LandAll(player);
        }

        _wasRiding = riding;

        //riders parked without floor come down as soon as there is some near Kami
        if (!riding && _riders.Count > 0 && Time.time >= _nextRetry)
        {
            _nextRetry = Time.time + _retryInterval;
            LandAll(player);
        }
    }

    // ---------------------------------------------------------------- boarding

    void BoardAll(Player player)
    {
        //riders still parked from a turn a moment ago: hand them back so they board again like everyone else
        if (_riders.Count > 0)
        {
            ReleaseAll();
        }

        List<NPC> candidates = new List<NPC>();
        foreach (NPC npc in FindObjectsOfType<NPC>())
        {
            if (npc.isFollowing && npc.gameObject.activeInHierarchy)
            {
                candidates.Add(npc);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.Log("[PageRideFollowers] Kami grabs the page edge but nobody is following her");
            return;
        }

        Vector3 kamiPosition = player.transform.position;
        candidates.Sort((a, b) => HorizontalSqrDistance(a.transform.position, kamiPosition).CompareTo(HorizontalSqrDistance(b.transform.position, kamiPosition)));

        for (int i = 0; i < candidates.Count; i++)
        {
            Board(candidates[i], i);
        }
    }

    void Board(NPC follower, int slot)
    {
        follower.StopFollowingPlayer(); //stops the agent while it is still usable
        NavMeshAgent agent = follower.navAgent;
        if (agent != null)
        {
            agent.updatePosition = false; //the agent keeps its internal position, the LateUpdate drives the transform
        }

        _riders.Add(new Rider
        {
            npc = follower,
            slot = slot,
            startPosition = follower.transform.position,
            boardTime = Time.time,
            bobPhase = Random.value * Mathf.PI * 2f,
        });
        Debug.Log($"[PageRideFollowers] {follower.gameObject.name} grabs on to Kami (slot {slot + 1}, {Mathf.Sqrt(HorizontalSqrDistance(follower.transform.position, Kami.transform.position)):F1} units away)");
    }

    // ---------------------------------------------------------------- riding

    void LateUpdate()
    {
        if (_riders.Count == 0)
        {
            return;
        }

        Player player = Kami;
        if (player == null)
        {
            return;
        }

        if ((_seats == null || _seats.Length == 0))
        {
            if (!_warnedNoSeats)
            {
                _warnedNoSeats = true;
                Debug.LogWarning("[PageRideFollowers] no seats configured: riders stay where they are until they land");
            }
            return;
        }

        float facing = player.SkeletonAnimation != null && player.SkeletonAnimation.Skeleton.ScaleX < 0f ? -1f : 1f;

        foreach (Rider rider in _riders)
        {
            if (rider.npc == null)
            {
                continue;
            }

            if (rider.parked)
            {
                rider.npc.transform.position = rider.parkedPosition;
                continue;
            }

            Vector3 seat = SeatForSlot(rider.slot);
            seat.x *= facing;
            seat.y += Mathf.Sin((Time.time * _bobSpeed) + rider.bobPhase) * _bobAmplitude;
            Vector3 target = player.transform.position + seat;

            float t = _boardEaseSeconds <= 0f ? 1f : Mathf.Clamp01((Time.time - rider.boardTime) / _boardEaseSeconds);
            t = t * t * (3f - (2f * t)); //smoothstep
            rider.npc.transform.position = Vector3.Lerp(rider.startPosition, target, t);

            //the same facing as Kami, so nobody hangs from the edge looking backwards
            rider.npc.FaceDirection(facing > 0f);
        }
    }

    Vector3 SeatForSlot(int slot)
    {
        if (slot < _seats.Length)
        {
            return _seats[slot];
        }

        //more followers than seats: keep going at the step between the last two
        Vector3 last = _seats[_seats.Length - 1];
        Vector3 step = _seats.Length > 1 ? last - _seats[_seats.Length - 2] : new Vector3(-2.5f, 0f, 0f);
        return last + (step * (slot - _seats.Length + 1));
    }

    // ---------------------------------------------------------------- getting off

    void LandAll(Player player)
    {
        for (int i = _riders.Count - 1; i >= 0; i--)
        {
            Rider rider = _riders[i];
            if (rider.npc == null || TryLand(rider, player))
            {
                _riders.RemoveAt(i);
                continue;
            }

            if (!rider.parked)
            {
                //no floor near Kami (the page's mesh isn't baked there, or she is still dropping in): wait where she landed
                rider.parked = true;
                rider.parkedPosition = rider.npc.transform.position;
                Debug.LogWarning($"[PageRideFollowers] no NavMesh within {_landingSampleRadius} units of where Kami landed for {rider.npc.gameObject.name}: parked, retrying every {_retryInterval}s. If this never resolves, the page needs a NavMesh bake near the entry point");
            }
        }
    }

    bool TryLand(Rider rider, Player player)
    {
        float facing = player.SkeletonAnimation != null && player.SkeletonAnimation.Skeleton.ScaleX < 0f ? -1f : 1f;
        Vector3 feet = player.FeetPosition;
        float offset = _landingSpacing * (rider.slot + 1);

        //into the page first, then behind her, then right under her
        Vector3[] candidates =
        {
            feet + (Vector3.right * facing * offset),
            feet - (Vector3.right * facing * offset),
            feet,
        };

        NavMeshHit hit = default;
        bool found = false;
        foreach (Vector3 candidate in candidates)
        {
            if (NavMesh.SamplePosition(candidate, out hit, _landingSampleRadius, NavMesh.AllAreas))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            return false;
        }

        NavMeshAgent agent = rider.npc.navAgent;
        if (agent != null)
        {
            agent.updatePosition = true;
            agent.Warp(hit.position);
        }
        else
        {
            rider.npc.transform.position = hit.position;
        }

        if (rider.npc.player == null)
        {
            rider.npc.player = player;
        }
        rider.npc.StartFollowingPlayer();
        Debug.Log($"[PageRideFollowers] {rider.npc.gameObject.name} lands next to Kami at {hit.position}");
        return true;
    }

    /// <summary>Everyone off where they are and following again (the component is going away mid-ride).</summary>
    void ReleaseAll()
    {
        Player player = Kami;
        foreach (Rider rider in _riders)
        {
            if (rider.npc == null)
            {
                continue;
            }

            if (rider.npc.navAgent != null)
            {
                rider.npc.navAgent.updatePosition = true;
            }

            if (rider.npc.player == null)
            {
                rider.npc.player = player;
            }

            if (rider.npc.gameObject.activeInHierarchy)
            {
                rider.npc.StartFollowingPlayer();
            }
        }

        _riders.Clear();
    }

    static float HorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return (dx * dx) + (dz * dz);
    }
}
