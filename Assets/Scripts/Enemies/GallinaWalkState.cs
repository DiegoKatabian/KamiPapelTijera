using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GallinaWalkState : IState
{

    FiniteStateMachine _fsm;
    GallinaAI _gallina;
    private bool goalReached;

    [SerializeField] Node currentGoalNode;
    int currentNodeIndex = 0;

    public GallinaWalkState(FiniteStateMachine fsm, GallinaAI r)
    {
        _fsm = fsm;
        _gallina = r;
    }

    public void OnEnter()
    {
        //Debug.Log("gallina - entre a walk");
        //_gallina.anim.SetBool("isWalk", true);
        currentNodeIndex = 0; //reiniciar index al entrar al estado (por si los nodos cambiaron)
        SetGoal(_gallina);
    }

    public void OnUpdate()
    {
        if (goalReached) //si llego, nuevo camino
        {
            //Debug.Log("cambie de goal");
            SetGoal(_gallina);
        }

        _gallina.RotateAccordingly();
        WalkTowardsNode(currentGoalNode, _gallina);

        if (_gallina.playerIsInRange)
        {
            _fsm.ChangeState(State.GallinaEvade);
        }
    }

    public void OnExit()
    {
        //Debug.Log("salgo de walk");
        //Debug.Log("gallina - entre a walk");
    }

    public void SetGoal(GallinaAI yo)
    {
        currentGoalNode = yo.allNodes[currentNodeIndex];
        currentNodeIndex = (currentNodeIndex + 1) % yo.allNodes.Length;
        goalReached = false;
    }
    
    public void WalkTowardsNode(Node goalNode, GallinaAI yo)
    {
        Vector3 dir = goalNode.transform.position - yo.transform.position;
        dir.y = 0;
        yo.rb.AddForce(yo.Speed * Time.deltaTime * dir.normalized, ForceMode.VelocityChange);
        
        if (dir.magnitude < yo.arriveRadius*2)
        {
            //Debug.Log("llegue al node");
            goalReached = true;
        }
    }
}
