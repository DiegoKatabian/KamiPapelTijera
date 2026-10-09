using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Level 2 page 4 (spec 006 Phase 6.C): when Kami folds the paper plane hat, whoever is following
/// her (Natalia, the Abuela) climbs aboard, so the augmented jump to the mezzanine takes all three of
/// them. They get off when the hat is used up.
///
/// Riding = the same idea as Kami hanging from the page edge (Player.RidingPage): they stop
/// following, the NavMeshAgent stops driving their transform (updatePosition off, the agent stays
/// on the mesh internally) and a LateUpdate holds them at a seat offset around Kami every frame.
/// Getting off = a warp next to her and following again. If Kami lands somewhere with no floor for the
/// agent (the mezzanine may not be baked), they stay parked where she landed and are picked up as
/// soon as she is back on a walkable spot, or when she crosses the window (RejoinAllForWindow).
///
/// Lives under Page 4's folder on purpose: it only listens while the page is the active one, and it
/// gets everyone off if the page is switched off (OnDisable) so nobody keeps riding into another page.
/// The real "sitting on the plane" poses are Valentino's (6.F); today it is the same sprites plus a bob.
/// </summary>
public class PaperPlaneRide : MonoBehaviour
{
    [SerializeField, Tooltip("Natalia (the one from page 1: there is only one in the level).")]
    NPC _natalia;

    [SerializeField, Tooltip("Page 4's Abuela entrance: the Abuela who follows Kami comes from there.")]
    AbuelaEntrance _abuelaEntrance;

    [Header("Seats (relative to Kami, X mirrored with the way she faces)")]
    [SerializeField, Tooltip("Natalia's seat, in world units from Kami's body center. Her transform sits at her waist.")]
    Vector3 _nataliaSeat = new Vector3(-2.5f, -0.5f, 0.6f);

    [SerializeField, Tooltip("The Abuela's seat.")]
    Vector3 _abuelaSeat = new Vector3(-5f, -1.2f, 0.6f);

    [SerializeField, Tooltip("How far each rider bobs up and down, in world units. Placeholder for the real pose.")]
    float _bobAmplitude = 0.35f;

    [SerializeField, Tooltip("Bob speed, in radians per second.")]
    float _bobSpeed = 6f;

    [Header("Getting off")]
    [SerializeField, Tooltip("Sideways spacing between the two riders when they land next to Kami, in world units.")]
    float _landingSpacing = 3f;

    [SerializeField, Tooltip("Seconds between checks for a rider parked without floor to see whether Kami is on walkable ground again.")]
    float _reclaimCheckInterval = 0.5f;

    class Rider
    {
        public NPC npc;
        public Vector3 seat;
        public int landingSlot; //1 = closest to Kami, 2 = one spacing further
        public float bobPhase;
        public bool parked;
        public Vector3 parkedPosition;
    }

    readonly List<Rider> _riders = new List<Rider>();
    float _nextReclaimCheck;

    NPC Abuela => _abuelaEntrance != null ? _abuelaEntrance.Abuela : null;

    public bool HasRiders => _riders.Count > 0;

    void OnEnable()
    {
        EventManager.Subscribe(Evento.OnOrigamiGivePaperPlaneHat, OnHatGiven);
        EventManager.Subscribe(Evento.OnPaperPlaneHatLost, OnHatLost);
    }

    void OnDisable()
    {
        EventManager.Unsubscribe(Evento.OnOrigamiGivePaperPlaneHat, OnHatGiven);
        EventManager.Unsubscribe(Evento.OnPaperPlaneHatLost, OnHatLost);

        //the page is going away: nobody may keep flying around in another page
        if (_riders.Count > 0)
        {
            ForceDismountAll();
        }
    }

    // ---------------------------------------------------------------- boarding

    void OnHatGiven(params object[] parameters)
    {
        Board(_natalia, _nataliaSeat, 1);
        Board(Abuela, _abuelaSeat, 2);
    }

    void Board(NPC follower, Vector3 seat, int landingSlot)
    {
        if (follower == null || !follower.gameObject.activeInHierarchy || !follower.isFollowing)
        {
            return;
        }

        foreach (Rider existing in _riders)
        {
            if (existing.npc == follower)
            {
                return;
            }
        }

        follower.StopFollowingPlayer(); //stops the agent while it is still usable
        NavMeshAgent agent = follower.navAgent;
        if (agent != null)
        {
            agent.updatePosition = false; //the agent stays on the mesh internally, the LateUpdate drives the transform
        }
        follower.SetRiding(true); //on the plane with Kami: walk cycle (spec 014)

        _riders.Add(new Rider { npc = follower, seat = seat, landingSlot = landingSlot, bobPhase = Random.value * Mathf.PI * 2f });
        Debug.Log($"[PaperPlaneRide] {follower.gameObject.name} climbs aboard the paper plane");
    }

    // ---------------------------------------------------------------- riding

    void LateUpdate()
    {
        if (_riders.Count == 0)
        {
            return;
        }

        Player player = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (player == null)
        {
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

            Vector3 seat = new Vector3(rider.seat.x * facing, rider.seat.y, rider.seat.z);
            seat.y += Mathf.Sin((Time.time * _bobSpeed) + rider.bobPhase) * _bobAmplitude;
            rider.npc.transform.position = player.transform.position + seat;
        }
    }

    void Update()
    {
        if (_riders.Count == 0 || Time.time < _nextReclaimCheck)
        {
            return;
        }

        _nextReclaimCheck = Time.time + _reclaimCheckInterval;

        //riders left parked (no floor where Kami landed) come back the moment she stands on walkable ground
        for (int i = _riders.Count - 1; i >= 0; i--)
        {
            if (_riders[i].parked && TryLand(_riders[i]))
            {
                _riders.RemoveAt(i);
            }
        }
    }

    // ---------------------------------------------------------------- getting off

    void OnHatLost(params object[] parameters)
    {
        DismountAll();
    }

    /// <summary>The hat is used up: everyone lands next to Kami and follows again, or stays parked where there is no floor.</summary>
    public void DismountAll()
    {
        for (int i = _riders.Count - 1; i >= 0; i--)
        {
            Rider rider = _riders[i];
            if (rider.npc == null || TryLand(rider))
            {
                _riders.RemoveAt(i);
                continue;
            }

            //no floor under Kami (the mezzanine): stay where she is standing, the Update keeps trying
            rider.parked = true;
            rider.parkedPosition = rider.npc.transform.position;
            rider.npc.SetRiding(false);
            Debug.Log($"[PaperPlaneRide] no floor for {rider.npc.gameObject.name} where Kami landed: parked until she is back on walkable ground");
        }
    }

    /// <summary>
    /// Everyone off, not following: for a capture restart, where the page warps Natalia to her cell and
    /// hides the Abuela right afterwards. Agents are handed back exactly as they were.
    /// </summary>
    public void ForceDismountAll()
    {
        foreach (Rider rider in _riders)
        {
            if (rider.npc != null && rider.npc.navAgent != null)
            {
                rider.npc.navAgent.updatePosition = true;
            }
            if (rider.npc != null)
            {
                rider.npc.SetRiding(false);
            }
        }

        _riders.Clear();
    }

    /// <summary>
    /// Kami crossed the window: everyone who was riding or parked becomes a follower again, so the page
    /// can warp them outside like any other follower. (They are still at their seat: the warp moves them.)
    /// </summary>
    public void RejoinAllForWindow()
    {
        Player player = LevelManager.Instance != null ? LevelManager.Instance.player : null;

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
            rider.npc.SetRiding(false);

            if (rider.npc.player == null)
            {
                rider.npc.player = player;
            }
            rider.npc.StartFollowingPlayer();
        }

        _riders.Clear();
    }

    bool TryLand(Rider rider)
    {
        Player player = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (player == null || rider.npc == null)
        {
            return false;
        }

        Vector3 target = player.FeetPosition + (Vector3.right * _landingSpacing * rider.landingSlot);

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 5f, NavMesh.AllAreas))
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
        rider.npc.SetRiding(false);
        rider.npc.ResetSkeletonPhysics();

        if (rider.npc.player == null)
        {
            rider.npc.player = player;
        }
        rider.npc.StartFollowingPlayer();
        Debug.Log($"[PaperPlaneRide] {rider.npc.gameObject.name} gets off the paper plane next to Kami");
        return true;
    }
}
