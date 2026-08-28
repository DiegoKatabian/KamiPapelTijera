using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Generic base class for agents that patrol waypoints using NavMesh.
/// Handles movement, waypoint following, state management, and evade behavior.
///
/// Derived classes override virtual methods to implement specific behavior:
/// - OnWaypointReached(): Custom logic when agent reaches a waypoint
/// - ShouldEvade(): Determines if agent should evade (e.g., player proximity)
/// - UpdateEvadeDestination(): Custom evade behavior
///
/// Example: public class GuardPatrol : PatrollingAgent { ... }
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public abstract class PatrollingAgent : MonoBehaviour
{
    // Movement components
    protected NavMeshAgent navAgent;
    protected Animator animator;

    // Waypoint tracking
    [SerializeField] protected List<Transform> waypoints = new List<Transform>();
    protected int currentWaypointIdx = 0;
    protected Transform currentWaypoint;

    // State management
    protected enum AgentState { Idle, Patrolling, Crossing, Evading }
    protected AgentState state = AgentState.Idle;

    // Configuration
    [SerializeField] protected float stoppingDistance = 0.5f;
    [SerializeField] protected bool debugMode = false;

    // Event tracking
    protected bool hasReachedWaypoint = false;

    protected virtual void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>(); //el Animator suele estar en un hijo (mesh/sprite), no en el root

        if (navAgent == null)
        {
            Debug.LogError($"[PatrollingAgent {gameObject.name}] NavMeshAgent component not found!");
        }
    }

    protected virtual void Start()
    {
        navAgent.stoppingDistance = stoppingDistance;
        state = AgentState.Patrolling;
    }

    protected virtual void Update()
    {
        // Check if agent reached waypoint (nunca mientras evade, para no pisar el indice de patrol)
        if (state != AgentState.Evading && navAgent.enabled && !navAgent.pathPending)
        {
            if (navAgent.remainingDistance <= navAgent.stoppingDistance)
            {
                if (!hasReachedWaypoint)
                {
                    hasReachedWaypoint = true;
                    if (debugMode)
                        Debug.Log($"[{gameObject.name}] Waypoint {currentWaypointIdx}/{waypoints.Count} reached: {currentWaypoint.position}");

                    //si OnWaypointReached devuelve true, significa que ya cambio de waypoints el mismo
                    //(ej: transicion de zona) y NO hay que avanzar automaticamente al siguiente indice,
                    //porque eso saltearia el primer nodo de la nueva secuencia
                    bool handledTransition = OnWaypointReached();
                    if (!handledTransition)
                    {
                        MoveToNextWaypoint();
                    }
                }
            }
            else
            {
                hasReachedWaypoint = false;
            }
        }

        // Check evade condition
        if (ShouldEvade())
        {
            if (state != AgentState.Evading)
            {
                state = AgentState.Evading;
                if (debugMode)
                    Debug.Log($"[{gameObject.name}] State: Evading");
                OnEvadeStart();
            }
            UpdateEvadeDestination();
        }
        else if (state == AgentState.Evading)
        {
            state = AgentState.Patrolling;
            if (debugMode)
                Debug.Log($"[{gameObject.name}] State: Patrolling (resumed)");
            ResumeCurrentWaypoint(); //retoma el waypoint que perseguia antes de evadir (no salta al siguiente)
        }
    }

    /// <summary>
    /// Set a new waypoint sequence for this agent to follow.
    /// Agent will walk to waypoints[0] immediately.
    /// </summary>
    public virtual void SetWaypoints(Transform[] newWaypoints)
    {
        if (newWaypoints == null || newWaypoints.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] SetWaypoints called with empty array!");
            return;
        }

        waypoints = new List<Transform>(newWaypoints);
        currentWaypointIdx = 0;
        currentWaypoint = waypoints[0];

        if (navAgent.enabled && NavMesh.SamplePosition(currentWaypoint.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
            hasReachedWaypoint = false;

            if (debugMode)
                Debug.Log($"[{gameObject.name}] NavMesh waypoints set. Total: {waypoints.Count}");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Waypoint {currentWaypointIdx} not on NavMesh: {currentWaypoint.position}");
        }
    }

    /// <summary>Re-set destination to the current waypoint without advancing the index (e.g., after evade ends).</summary>
    protected virtual void ResumeCurrentWaypoint()
    {
        if (currentWaypoint == null) return;

        if (navAgent.enabled && NavMesh.SamplePosition(currentWaypoint.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
            hasReachedWaypoint = false;
        }
    }

    /// <summary>Move agent to the next waypoint in sequence (loops back to 0).</summary>
    protected virtual void MoveToNextWaypoint()
    {
        if (waypoints.Count == 0)
        {
            Debug.LogError($"[{gameObject.name}] No waypoints set!");
            return;
        }

        currentWaypointIdx = (currentWaypointIdx + 1) % waypoints.Count;
        currentWaypoint = waypoints[currentWaypointIdx];

        if (navAgent.enabled && NavMesh.SamplePosition(currentWaypoint.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
            hasReachedWaypoint = false;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Waypoint {currentWaypointIdx} not on NavMesh: {currentWaypoint.position}");
        }
    }

    // === VIRTUAL METHODS FOR DERIVED CLASSES ===

    /// <summary>
    /// Called when agent reaches a waypoint. Override to add custom logic.
    /// Return true if this call already changed the waypoint sequence (via SetWaypoints) —
    /// this tells the base class NOT to auto-advance to the next index, which would otherwise
    /// skip the first waypoint of the new sequence. Return false for normal patrol looping.
    /// </summary>
    protected virtual bool OnWaypointReached()
    {
        return false; // Default: let base class auto-advance to next waypoint (loop).
    }

    /// <summary>Return true if agent should evade (e.g., player is too close).</summary>
    protected virtual bool ShouldEvade()
    {
        return false;
    }

    /// <summary>Update evade destination. Called every frame while evading.</summary>
    protected virtual void UpdateEvadeDestination()
    {
        // Default: do nothing. Override in derived classes.
    }

    /// <summary>Called once when agent transitions into evading (e.g., play a sound). Override in derived classes.</summary>
    protected virtual void OnEvadeStart()
    {
        // Default: do nothing. Override in derived classes.
    }
}
