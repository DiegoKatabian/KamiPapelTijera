using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RocosoWalkState : IState
{
    FiniteStateMachine _fsm;
    Rocoso _rocoso;

    //nodo estrategico elegido para esta "sesion" de inalcanzable (se fija una sola vez al detectar
    //que Kami subio, para no recalcular ni titubear si ella se mueve un poco por la plataforma)
    Transform _nodoEstrategicoActual;
    bool _nodoEstrategicoEvaluado;

    public RocosoWalkState(FiniteStateMachine fsm, Rocoso r)
    {
        _fsm = fsm;
        _rocoso = r;
    }

    public void OnEnter()
    {
        _rocoso.anim.SetTrigger("isWalks");
        _rocoso.navAgent.isStopped = false; //retomar movimiento por si veniamos de Attack/Sleep/Start con el agent detenido
        ResetearNodoEstrategico();
    }

    public void OnUpdate()
    {
        bool inalcanzable = _rocoso.PlayerEsInalcanzablePorAltura();

        if (inalcanzable)
        {
            IrAlNodoEstrategico();
        }
        else
        {
            ResetearNodoEstrategico(); //Kami volvio a ser alcanzable; si vuelve a subir se reevalua el nodo mas cercano
            WalkTowardsPlayer();
        }

        //el giro ahora pasa por SetFacing: ademas de rotar, corrige la profundidad de las
        //particulas de rayos, que si no quedan tapadas por el sprite al mirar para el otro lado
        _rocoso.SetFacing(_rocoso.target.x > _rocoso.transform.position.x);

        //el gate de inalcanzable es clave: DistanceToPlayer() es distancia 3D (incluye Y), asi que si
        //Kami esta arriba de una plataforma pero horizontalmente cerca podria dar menor a
        //enterAttackRange y Rocoso "atacaria" a traves de la plataforma sin poder llegar
        if (!inalcanzable && _rocoso.DistanceToPlayer() < _rocoso.enterAttackRange)
        {
            _fsm.ChangeState(State.RocosoAttack);
        }

        if (!_rocoso.PlayerIsInViewRange())
        {
            //Debug.Log("walk: me paso a sleep");
            _fsm.ChangeState(State.RocosoSleep);
        }

        if (_rocoso.isDead)
        {
            _fsm.ChangeState(State.RocosoDeath);
        }
    }


    public void OnExit()
    {
        //Debug.Log("salgo de walk");
        _rocoso.navAgent.isStopped = true; //frenar al agent (ej. al entrar a Attack, para que no siga caminando/deslizando durante el cabezazo)
        ResetearNodoEstrategico();
    }

    public void WalkTowardsPlayer()
    {
        _rocoso.navAgent.SetDestination(_rocoso.target);
    }

    void ResetearNodoEstrategico()
    {
        _nodoEstrategicoActual = null;
        _nodoEstrategicoEvaluado = false;
    }

    //Kami se subio a una plataforma que Rocoso no puede alcanzar (por diseno, ver areaMask del
    //NavMeshAgent en el inspector): en vez de insistir caminando contra la base, se va UNA VEZ al nodo
    //estrategico mas cercano a donde esta ella (ubicado a mano en la escena, ej. cerca de la represa) y
    //se queda ahi -- el NavMeshAgent llega y frena solo, no hace falta re-settear el destino cada frame.
    //Si no hay nodos configurados, cae al comportamiento viejo de quedarse quieto en el lugar.
    void IrAlNodoEstrategico()
    {
        if (_nodoEstrategicoEvaluado)
        {
            return; //ya elegimos nodo (o la ausencia de nodos) para esta sesion de inalcanzable
        }

        _nodoEstrategicoActual = _rocoso.NodoEstrategicoMasCercanoA(_rocoso.target);
        _nodoEstrategicoEvaluado = true;

        if (_nodoEstrategicoActual == null)
        {
            _rocoso.navAgent.ResetPath();
        }
        else
        {
            _rocoso.navAgent.SetDestination(_nodoEstrategicoActual.position);
        }
    }
}
