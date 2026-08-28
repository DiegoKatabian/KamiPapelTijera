using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gallina behavior for the Tiburcio quest: patrols river side, evades Kami,
/// crosses fallen tree bridge when quest completes, then patrols ranch side forever.
///
/// Setup (inspector): drag Transform waypoints into the 3 arrays below.
/// Zero scripting required by level designer.
/// </summary>
public class GallinaAgent : PatrollingAgent
{
    [Header("Pre-Tree Patrol (river side)")]
    [Tooltip("Nodos de patrullaje ANTES de cortar el arbol. Gallina evade a Kami aca.")]
    [SerializeField] private Transform[] preTreeWaypoints;

    [Header("Tree Crossing")]
    [Tooltip("Base del arbol caido (donde empieza a cruzar)")]
    [SerializeField] private Transform treeStart;
    [Tooltip("Centro del arbol caido (punto medio del cruce)")]
    [SerializeField] private Transform treeCenter;
    [Tooltip("Punto final del cruce, ya del lado del rancho")]
    [SerializeField] private Transform treeEnd;

    [Header("Post-Tree Patrol (rancho side)")]
    [Tooltip("Nodos de patrullaje DESPUES de cruzar. Gallina NUNCA evade aca (zona segura).")]
    [SerializeField] private Transform[] postTreeWaypoints;

    [Header("Evade Behavior")]
    [SerializeField] private Player player;
    [SerializeField] private float evadeDistance = 5f;
    [SerializeField] private float evadeSeekDistance = 10f;

    [Header("Refs")]
    [SerializeField] private GallinaSounds gallinaSounds;

    private bool isCrossingTree = false;
    private bool hasCompletedCrossing = false;
    private bool hasQuestCompleteAnimParam = false;

    private const string QUEST_COMPLETE_ANIM_PARAM = "questComplete";

    protected override void Start()
    {
        base.Start();

        //el animator de Gallina hoy solo tiene el parametro _isCortado (ver GallinaWalk.controller).
        //si Diego agrega un trigger "questComplete" en el Animator Controller, se usa automaticamente.
        if (animator != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == QUEST_COMPLETE_ANIM_PARAM && param.type == AnimatorControllerParameterType.Trigger)
                {
                    hasQuestCompleteAnimParam = true;
                    break;
                }
            }

            if (!hasQuestCompleteAnimParam)
            {
                Debug.LogWarning($"[GallinaAgent {gameObject.name}] Animator no tiene trigger '{QUEST_COMPLETE_ANIM_PARAM}'. " +
                    "Agregalo en el Animator Controller para que la gallina muestre un cambio visual al completar la quest.");
            }
        }

        if (preTreeWaypoints == null || preTreeWaypoints.Length == 0)
        {
            Debug.LogError($"[GallinaAgent {gameObject.name}] preTreeWaypoints vacio! La gallina no va a patrullar.");
        }
        else
        {
            SetWaypoints(preTreeWaypoints);
        }

        if (treeStart == null || treeCenter == null || treeEnd == null)
        {
            Debug.LogWarning($"[GallinaAgent {gameObject.name}] Faltan nodos de cruce de arbol (start/center/end).");
        }

        if (postTreeWaypoints == null || postTreeWaypoints.Length == 0)
        {
            Debug.LogWarning($"[GallinaAgent {gameObject.name}] postTreeWaypoints vacio! La gallina no patrullara el rancho.");
        }

        if (player == null)
        {
            player = FindObjectOfType<Player>();
            if (player == null)
            {
                Debug.LogWarning($"[GallinaAgent {gameObject.name}] No se encontro Player en la escena, evade no va a funcionar.");
            }
        }
    }

    private void OnEnable()
    {
        EventManager.Subscribe(Evento.OnTreeCutForChickens, OnTreeFell);
    }

    private void OnTreeFell(params object[] parameters)
    {
        if (isCrossingTree || hasCompletedCrossing) return; //ya procesamos esto

        if (treeStart == null || treeCenter == null || treeEnd == null)
        {
            Debug.LogError($"[GallinaAgent {gameObject.name}] No puedo cruzar: faltan nodos de arbol.");
            return;
        }

        isCrossingTree = true;

        if (debugMode)
            Debug.Log($"[GallinaAgent {gameObject.name}] Arbol cayo! Iniciando cruce.");

        PlayQuestCompletionSignal();
        SetWaypoints(new[] { treeStart, treeCenter, treeEnd });
    }

    protected override bool OnWaypointReached()
    {
        //currentWaypointIdx == waypoints.Count - 1 significa que acabamos de llegar al ULTIMO
        //nodo de la secuencia actual (treeEnd, si estamos cruzando)
        if (isCrossingTree && currentWaypointIdx == waypoints.Count - 1)
        {
            isCrossingTree = false;
            hasCompletedCrossing = true;

            if (debugMode)
                Debug.Log($"[GallinaAgent {gameObject.name}] Cruce completo! Cambiando a patrol de rancho.");

            if (postTreeWaypoints != null && postTreeWaypoints.Length > 0)
            {
                SetWaypoints(postTreeWaypoints);
            }

            return true; //ya cambiamos de secuencia nosotros, que la base no auto-avance
        }

        return false; //patrol normal (pre-tree loop, o medio del cruce): que la base auto-avance
    }

    protected override bool ShouldEvade()
    {
        if (isCrossingTree || hasCompletedCrossing) return false; //no evade durante cruce ni en zona segura del rancho
        if (player == null) return false;

        float distance = Vector3.Distance(transform.position, player.transform.position);
        return distance < evadeDistance;
    }

    protected override void UpdateEvadeDestination()
    {
        if (player == null) return;

        Vector3 away = (transform.position - player.transform.position).normalized;
        Vector3 evadeTarget = transform.position + away * evadeSeekDistance;

        if (NavMesh.SamplePosition(evadeTarget, out NavMeshHit hit, evadeSeekDistance, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
        }
    }

    protected override void OnEvadeStart()
    {
        if (gallinaSounds != null)
        {
            gallinaSounds.PlayEvadeSound();
        }
    }

    /// <summary>Señal visual al completar la quest (arbol caido). No-op si el Animator Controller no tiene el trigger.</summary>
    private void PlayQuestCompletionSignal()
    {
        if (hasQuestCompleteAnimParam)
        {
            animator.SetTrigger(QUEST_COMPLETE_ANIM_PARAM);
        }
    }

    private void OnDisable()
    {
        EventManager.Unsubscribe(Evento.OnTreeCutForChickens, OnTreeFell);
    }
}
