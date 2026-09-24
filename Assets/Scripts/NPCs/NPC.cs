using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPC : Entity
{
    //los npcs tienen 2 estados, idle o seguirte. por ahora.
    //tambien tienen vida, speed y atk damage (heredan de entities)
    //pueden recibir da�o pero no pueden morir

    //Movement is NavMesh-based (Diego, 2026-09-24), same as the chickens and Rocoso. It used to be
    //hand-rolled steering (AddForce/Arrive integrating a velocity into transform.position), which
    //walked through walls and ignored the level's navigation. The project's split: waypoint
    //patrollers inherit PatrollingAgent, things that chase a moving target drive their own
    //NavMeshAgent (Rocoso's precedent) -- an NPC following Kami is the second kind.

    protected FiniteStateMachine _fsm;
    public SpriteRenderer _sr;

    public bool isFollowing;

    public Player player; //seria genial que no dependan del player para laburar

    [Tooltip("How close this NPC gets before stopping. Replaces the old arriveRadius.")]
    [SerializeField] protected float followStoppingDistance = 3f;

    [Tooltip("Seconds between path recalculations while following. Repathing every frame is wasted CPU for a target that barely moves between frames.")]
    [SerializeField] protected float repathInterval = 0.2f;

    [Tooltip("Optional: walk/idle animator driven by the follow state. Leave empty for an NPC without one.")]
    public Animator anim;

    [SerializeField, Tooltip("Bool parameter toggled on the animator while this NPC is moving under its own steering.")]
    string _walkAnimatorBool = "IsWalking";

    [SerializeField, Tooltip("Flip the sprite to face the direction of travel while moving.")]
    bool _flipSpriteToMovement = true;

    [SerializeField, Tooltip("Turn on for a character whose art is drawn facing the opposite way to the rest. Only mirrors which direction counts as 'unflipped' -- it does not change when the flip happens.")]
    bool _invertFlip = false;

    [SerializeField, Tooltip("Reparent this NPC under the player's parent when it starts following, so it travels with the active page. Turn off if it sits in a hierarchy that must not change.")]
    bool _reparentToPlayerPageOnFollow = true;

    [Header("Debug")]
    [SerializeField, Tooltip("Log a periodic report of everything that decides whether this NPC can move: position, effective agent size, isOnNavMesh, path status, distance to the nearest NavMesh point. Turn on when an NPC refuses to move.")]
    bool _debugMovement = false;

    [SerializeField, Tooltip("Seconds between debug reports, and between repeats of the 'cannot move' warning.")]
    float _debugInterval = 1f;

    public NavMeshAgent navAgent { get; private set; }

    float _repathTimer;
    float _debugTimer;
    float _nextUnusableWarning;

    protected virtual void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();

        if (navAgent == null)
        {
            Debug.LogError($"[{GetType().Name}] {gameObject.name}: no NavMeshAgent component. This NPC cannot move at all — add one to its prefab.");
            return;
        }

        //the sprite's facing is handled by flipX, so let the agent steer without rotating the art
        navAgent.updateRotation = false;
        navAgent.speed = _maxSpeed;
        navAgent.stoppingDistance = followStoppingDistance;

        if (_debugMovement)
        {
            //no isOnNavMesh here on purpose: PageNavMeshManager only adds the page's NavMeshData in its
            //own Start, so at Awake every agent in the scene still reports "not on a NavMesh"
            Debug.Log($"[{GetType().Name}] {gameObject.name} agent configured: speed {navAgent.speed}, stoppingDistance {navAgent.stoppingDistance}, " +
                      $"radius {navAgent.radius}, height {navAgent.height}, baseOffset {navAgent.baseOffset}, lossyScale {transform.lossyScale}. " +
                      $"Effective world size: radius {navAgent.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z):F3}, height {navAgent.height * transform.lossyScale.y:F3}.");
        }
    }

    protected virtual void Start()
    {
        _fsm = new FiniteStateMachine();
        _fsm.AddState(State.NPC_Idle, new NPC_IdleState(_fsm, this));
        _fsm.AddState(State.NPC_FollowPlayer, new NPC_FollowPlayerState(_fsm, this));
        _fsm.ChangeState(State.NPC_Idle);
    }

    protected void Update()
    {
        _fsm.Update();
        DebugTick();
    }

    void DebugTick()
    {
        if (!_debugMovement || !isFollowing)
        {
            return;
        }

        _debugTimer -= Time.deltaTime;
        if (_debugTimer > 0f)
        {
            return;
        }

        _debugTimer = _debugInterval;
        ReportAgentState("following");
    }

    /// <summary>
    /// Starts following the player. params object[] so it can be wired straight to an EventManager event.
    /// </summary>
    public void StartFollowingPlayer(params object[] parameter)
    {
        if (isFollowing)
        {
            return;
        }

        if (player == null)
        {
            Debug.LogWarning($"[{GetType().Name}] {gameObject.name}: asked to follow Kami but there is no Player reference, ignoring.");
            return;
        }

        if (_reparentToPlayerPageOnFollow)
        {
            transform.parent = player.transform.parent;
        }

        isFollowing = true;
        Debug.Log($"[{GetType().Name}] {gameObject.name} starts following Kami");

        if (_debugMovement)
        {
            ReportAgentState("started following");
        }
    }

    public void StopFollowingPlayer(params object[] parameter)
    {
        if (!isFollowing)
        {
            return;
        }

        isFollowing = false;
        StopAgent();
        SetWalkAnimation(false);

        Debug.Log($"[{GetType().Name}] {gameObject.name} stops following Kami");
    }

    /// <summary>Re-paths toward a moving target on an interval instead of every frame.</summary>
    public void MoveTowards(Vector3 destination, bool force = false)
    {
        if (!IsAgentUsable())
        {
            return;
        }

        navAgent.isStopped = false;

        _repathTimer -= Time.deltaTime;
        if (!force && _repathTimer > 0f)
        {
            return;
        }
        _repathTimer = repathInterval;

        //same validation PatrollingAgent uses: a destination off the mesh silently fails otherwise
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            //SetDestination returns false when the agent refuses the order outright (detached from the
            //mesh, path request rejected). It used to be discarded, which hid exactly this failure.
            if (!navAgent.SetDestination(hit.position) && _debugMovement)
            {
                Debug.LogWarning($"[{GetType().Name}] {gameObject.name}: the agent REJECTED SetDestination({hit.position}).");
            }
        }
        else
        {
            Debug.LogWarning($"[{GetType().Name}] {gameObject.name}: destination {destination} is not on the NavMesh, staying put.");
        }
    }

    public void StopAgent()
    {
        if (!IsAgentUsable())
        {
            return;
        }

        navAgent.ResetPath();
        navAgent.isStopped = true;
    }

    /// <summary>True once the agent is close enough to its destination to count as arrived.</summary>
    public bool HasArrived()
    {
        if (!IsAgentUsable() || navAgent.pathPending)
        {
            return false;
        }

        return navAgent.remainingDistance <= navAgent.stoppingDistance;
    }

    bool IsAgentUsable()
    {
        //an agent whose GameObject sits off the baked mesh (or on a page whose NavMesh is not the
        //active one) reports isOnNavMesh == false, and every call on it logs an engine error.
        //This used to return false SILENTLY, which made "the NPC just stands there" undiagnosable:
        //the NPC entered its follow state, called MoveTowards every frame, and nothing was ever
        //logged. Every rejection now says which condition failed.
        if (navAgent == null)
        {
            WarnCannotMove("there is no NavMeshAgent component on this GameObject");
            return false;
        }

        if (!navAgent.enabled)
        {
            WarnCannotMove("its NavMeshAgent component is disabled");
            return false;
        }

        if (!navAgent.isOnNavMesh)
        {
            WarnCannotMove("it is not standing on the active NavMesh (isOnNavMesh == false), so the agent refuses every move order. " +
                           "Either this page's NavMesh is not the active one, or the NPC's pivot is too far from the baked surface for an agent this size to snap onto it");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Throttled: MoveTowards checks this every frame, and a per-frame warning would bury the rest of
    /// the console under 60 identical lines a second.
    /// </summary>
    void WarnCannotMove(string reason)
    {
        if (Time.time < _nextUnusableWarning)
        {
            return;
        }

        _nextUnusableWarning = Time.time + _debugInterval;
        Debug.LogWarning($"[{GetType().Name}] {gameObject.name} CANNOT MOVE: {reason}.");
        ReportAgentState("cannot move");
    }

    /// <summary>
    /// Snapshot of everything that decides whether this NPC can move. The point of logging the nearest
    /// NavMesh point is to separate the two failure modes that look identical in game: "she is off the
    /// mesh" (a placement/bake problem) from "she is on the mesh but has no reason to walk" (a
    /// stoppingDistance/path problem).
    /// </summary>
    public void ReportAgentState(string context)
    {
        string label = $"[{GetType().Name}] {gameObject.name} ({context})";

        if (navAgent == null)
        {
            Debug.LogWarning($"{label}: no NavMeshAgent at all.");
            return;
        }

        //deliberately huge search radius: the whole question is HOW FAR off the mesh she is, and a
        //radius that only covers "basically on it already" would answer nothing
        string meshProximity;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit nearest, 50f, NavMesh.AllAreas))
        {
            Vector3 delta = nearest.position - transform.position;
            meshProximity = $"nearest active NavMesh point is {nearest.position}, delta {delta}, distance {delta.magnitude:F3}";
        }
        else
        {
            meshProximity = "NO active NavMesh point within 50 units -- either no NavMesh is active for this page, or she is nowhere near the baked surface";
        }

        Debug.Log($"{label}\n" +
                  $"  position {transform.position}, lossyScale {transform.lossyScale}, parent '{(transform.parent == null ? "<scene root>" : transform.parent.name)}'\n" +
                  $"  agent: enabled {navAgent.enabled}, isOnNavMesh {navAgent.isOnNavMesh}, isStopped {navAgent.isStopped}, speed {navAgent.speed}, stoppingDistance {navAgent.stoppingDistance}\n" +
                  $"  agent size: radius {navAgent.radius} -> {navAgent.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z):F3} world, height {navAgent.height} -> {navAgent.height * transform.lossyScale.y:F3} world, baseOffset {navAgent.baseOffset}\n" +
                  $"  {meshProximity}");

        if (!navAgent.isOnNavMesh)
        {
            //reading path state off a detached agent is exactly what spams engine errors
            return;
        }

        string playerInfo = player == null
            ? "no Player reference"
            : $"Kami at {player.transform.position}, distance {Vector3.Distance(transform.position, player.transform.position):F3}";

        Debug.Log($"{label} path: pathPending {navAgent.pathPending}, hasPath {navAgent.hasPath}, status {navAgent.pathStatus}, " +
                  $"destination {navAgent.destination}, remainingDistance {navAgent.remainingDistance:F3}, " +
                  $"desiredVelocity {navAgent.desiredVelocity.magnitude:F3}, velocity {navAgent.velocity.magnitude:F3}, {playerInfo}");
    }

    public void SetWalkAnimation(bool walking)
    {
        if (anim == null || string.IsNullOrEmpty(_walkAnimatorBool))
        {
            return;
        }

        anim.SetBool(_walkAnimatorBool, walking);
    }

    public void UpdateSpriteFlip()
    {
        if (!_flipSpriteToMovement || _sr == null || navAgent == null)
        {
            return;
        }

        //ignore the jitter of an almost-stopped agent, otherwise the sprite flickers on arrival
        if (Mathf.Abs(navAgent.velocity.x) < 0.05f)
        {
            return;
        }

        //characters are not all drawn facing the same way, so which flipX means "walking right" is a
        //per-character fact about the art, not something the movement code can know
        bool movingRight = navAgent.velocity.x > 0;
        _sr.flipX = _invertFlip ? !movingRight : movingRight;
    }

    /// <summary>
    /// Hook for NPC-specific transitions out of the shared idle/follow states (Abuela's dropoff).
    /// Return true if the state was changed, so the shared state stops evaluating its own rules.
    /// </summary>
    protected internal virtual bool TryExtraTransitions()
    {
        return false;
    }

    public override void TakeDamage(float dmg)
    {
        base.TakeDamage(dmg);
        StartCoroutine(EnrojecerSprite());
    }
    public IEnumerator EnrojecerSprite()
    {
        //print("enrojeci el sprite");
        _sr.material.color = Color.red;
        yield return new WaitForSeconds(0.25f);
        _sr.material.color = Color.white;
    }
    public override void Die(DeathCause cause = DeathCause.Generic)
    {
        TooltipManager.Instance.ShowTooltip("Este NPC no puede morir", PostItColor.Verde);
    }
}
