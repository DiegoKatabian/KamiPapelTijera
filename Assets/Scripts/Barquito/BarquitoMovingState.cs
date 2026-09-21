using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarquitoMovingState : IState
{
    //ojo que falla si el nodo0 no es el mas cercano cuando arranca la cosa
    BarquitoBehaviour _barco;
    FiniteStateMachine _fsm;

    bool goalReached;
    Node closestNodeToBarco;


    public BarquitoMovingState(FiniteStateMachine fsm, BarquitoBehaviour barco)
    {
        _barco = barco;
        _fsm = fsm;
    }

    public void OnEnter()
    {
        //Debug.Log("entro a moving state");
        SetGoal();
        AudioManager.instance.Play("ShipSailingLoop"); //no clip for this id exists yet

    }

    public void OnUpdate()
    {
        if (_barco._pathToFollow.Count != 0)
        {
            FollowPath();
        }

        if (goalReached)
        {
            _barco.playerIsInside = false;
            _fsm.ChangeState(State.BarquitoIdle);
        }

        _barco.transform.position += _barco.velocity * Time.deltaTime;
    }

    public void OnExit()
    {
        //Debug.Log("salgo del moving state");
        AudioManager.instance.StopById("ShipSailingLoop");

    }

    public void SetGoal()
    {
        //Debug.Log("arranca set goal");

        closestNodeToBarco = _barco.FindClosestNode(_barco.transform.position);
        //Debug.Log("closest node es " + closestNodeToBarco.name);


        if (closestNodeToBarco == _barco.allNodes[0]) //si estoy en el inicio
        {
            FindPathToGoal(_barco.allNodes[_barco.allNodes.Length - 1]); //voy al ultimo
            //Debug.Log("voy hacia " + _barco.allNodes[_barco.allNodes.Length - 1].name);

        }
        else if (closestNodeToBarco == _barco.allNodes[_barco.allNodes.Length - 1]) //si estoy en el ultimo
        {
            FindPathToGoal(_barco.allNodes[0]); //voy al inicio
            //Debug.Log("voy hacia " + _barco.allNodes[0].name);

        }

        goalReached = false;
    }

    public void FindPathToGoal(Node goalNode)
    {
        //Debug.Log("arranca find path");

        Node startingNode = closestNodeToBarco;
        _barco._pathToFollow = _barco._pf.AStar(startingNode, goalNode);
    }

    void FollowPath()
    {
        //Debug.Log("arranca follow path");

        Vector3 nextPos = _barco._pathToFollow[0].transform.position;
        Vector3 dir = nextPos - _barco.transform.position;
        _barco.transform.forward = dir;
        _barco.AddForce(_barco.Arrive(_barco._pathToFollow[0].transform.position));
        //_barco.transform.position += _barco.transform.forward * Time.deltaTime * _barco.speed;

        if (dir.magnitude < 0.1f)
        {
            _barco._pathToFollow.RemoveAt(0);
            //Debug.Log("llegue al next nodo");
        }

        if (_barco._pathToFollow.Count == 0)
        {
            goalReached = true;
        }
    }

}
