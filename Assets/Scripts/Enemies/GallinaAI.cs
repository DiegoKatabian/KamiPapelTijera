using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GallinaAI : Enemy
{
    float maxForce = 5;
    protected FiniteStateMachine _fsm;
    public Node[] allNodes;
    [SerializeField][Tooltip("Nodos de patrullar en el rancho despues de que cae el arbol")]
    public Node[] ranchNodes;
    public float arriveRadius = 1;
    public Rigidbody rb;
    public float evadeSpeed = 20;
    public GallinaSounds gallinaSounds;
    [HideInInspector] public bool startAnimationHasFinished = false; //si el player ya se acerco y me desperto
    [HideInInspector] public Vector3 velocity;
    [HideInInspector] public List<Node> _pathToFollow = new List<Node>();
    [HideInInspector] public Pathfinding _pf = new Pathfinding();
    [HideInInspector] public Player _player;
    [HideInInspector] public bool playerIsInRange;
    [HideInInspector] public bool questCompleted = false;
    


    private void Start()
    {
        _fsm = new FiniteStateMachine();
        _fsm.AddState(State.GallinaWalk, new GallinaWalkState(_fsm, this));
        _fsm.AddState(State.GallinaEvade, new GallinaEvadeState(_fsm, this));
        _fsm.ChangeState(State.GallinaWalk);

        EventManager.Subscribe(Evento.OnTreeCutForChickens, HandleTreeCut);
    }

    private void Update()
    {
        _fsm.Update();
    }

    public Node FindClosestNode(Vector3 pos)
    {
        float currentDistance = Mathf.Infinity;
        Node closestNode = default;

        foreach (var node in allNodes)
        {
            float nodeDistance = Vector3.Distance(pos, node.transform.position);

            if (nodeDistance < currentDistance)
            {
                currentDistance = nodeDistance;
                closestNode = node;
            }
        }
        return closestNode;
    }

    public Vector3 Arrive(Vector3 pos)
    {
        Vector3 desired = pos - transform.position;
        if (desired.magnitude <= arriveRadius)
        {
            //estoy dentro del radio, me voy frenando
            float newSpeed = Speed * (desired.magnitude / arriveRadius);
            desired.Normalize();
            desired *= newSpeed;
        }
        else
        {
            //seek normal mientras estoy lejos del objeto
            desired.Normalize();
            desired *= Speed;
        }

        Vector3 steering = desired - velocity;
        steering = Vector3.ClampMagnitude(steering, maxForce);
        return steering;
    }

    public void RotateAccordingly()
    {
        //depende del velocity, ojota
        if (velocity.x > transform.position.x) //roto al enemy segun pa donde camine
        {
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    private void OnDisable()
    {
        EventManager.Unsubscribe(Evento.OnTreeCutForChickens, HandleTreeCut);
    }

    private void HandleTreeCut(params object[] parameters)
    {
        if (questCompleted) return; //ya lo procesamos una vez

        questCompleted = true;

        //cambiar a nodos de rancho
        if (ranchNodes != null && ranchNodes.Length > 0)
        {
            allNodes = ranchNodes;
            _fsm.ChangeState(State.GallinaWalk); //forzar walk state para empezar nueva ruta (OnEnter reinicia currentNodeIndex)

            Debug.Log($"[GallinaAI] {gameObject.name} cambio a ranchNodes");
        }
        else
        {
            Debug.LogWarning($"[GallinaAI] {gameObject.name} no tiene ranchNodes configurado");
        }
    }
}
