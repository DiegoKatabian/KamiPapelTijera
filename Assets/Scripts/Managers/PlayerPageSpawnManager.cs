using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerPageSpawnManager : Singleton<PlayerPageSpawnManager>
{
    //decime a que pagina pasaste, y yo te dire donde deberia spawnear el player.
    //tambien usan este script cuando el pj muere y debe respawnear

    //isNext es true cuando estoy pasando a la SIGUIENTE pagina.
    //isNext seria como lo opuesto a isPrev.

    [SerializeField] Player _player;
    [SerializeField] float pageEntryX;
    [SerializeField] float pageExitX;
    [SerializeField] float spawnY = 5;
    CharacterController _playerCC;
    Vector3 lastUsedSpawn; //para recordar el ultimo usado para cuando el player muera
    Vector3 targetPos = Vector3.zero; //para cuando el player cambia de pagina, saber a donde debe ir

    void Start()
    {
        //EventManager.Subscribe(Evento.OnEncounterStart, SaveCurrentPosition);
        EventManager.Subscribe(Evento.OnPageTurnStart, SetPlayerTargetPosition);
        EventManager.Subscribe(Evento.OnNewPageOpen, PlacePlayerInNewPage);
        EventManager.Subscribe(Evento.OnPageFinishTurning, FinishRide);
        _playerCC = _player.GetComponent<CharacterController>();
        lastUsedSpawn = _player.transform.position; //en principio, tu ultimo spawn es donde arranca el juego
    }

    //subscribing methods

    public void SetPlayerTargetPosition(params object[] parameters)
    {
        targetPos = GetProjectedPositionInNewPage(_player.transform.position, (bool)parameters[1]);
        Debug.Log($"[PlayerPageSpawnManager] SetPlayerTargetPosition: kami en {_player.transform.position}, targetPos calculado {targetPos}");
    }

    public void PlacePlayerInNewPage(params object[] parameter)
    {
        if (_player.IsRidingPage)
        {
            Debug.Log($"[PlayerPageSpawnManager] PlacePlayerInNewPage: kami enganchada a la hoja, salteo el teletransporte (targetPos guardado {targetPos}, lo aplica FinishRide)");
            return; //mientras esta enganchada a la hoja, la ubicacion final se aplica en FinishRide, cuando termina de girar
        }
        Debug.Log($"[PlayerPageSpawnManager] PlacePlayerInNewPage: teletransporto directo a {targetPos} (kami no estaba enganchada)");
        FinalizePlacement();
    }
    public void FinishRide(params object[] parameter)
    {
        //la hoja termino de girar: suelta a kami del hueso y la deja exactamente donde siempre hubiera caido
        if (!_player.IsRidingPage)
        {
            return;
        }
        Debug.Log($"[PlayerPageSpawnManager] FinishRide: suelto a kami y la coloco en {targetPos}");
        _player.StopRidingPage();
        FinalizePlacement();
    }
    void FinalizePlacement()
    {
        PositionPlayerAtPoint(targetPos);
        SavePosition(_player.transform.position);
    }
    public void SaveCurrentPosition(params object[] parameter)
    {
        SavePosition(_player.transform.position);
    }

    //methods called from other managers
    public void RespawnPlayer(params object[] parameter)
    {
        //Debug.Log("respawn player");
        PositionPlayerAtPoint(lastUsedSpawn);
    }

    //utilities
    public void PositionPlayerAtPoint(Vector3 point)
    {
        _playerCC.enabled = false;
        _player.transform.position = point;
        _playerCC.enabled = true;
        EventManager.Trigger(Evento.OnPlayerPlaced);
    }
    public Vector3 GetProjectedPositionInNewPage(Vector3 playerCurrentPosition, bool isNext)
    {
        float desiredX;
        Vector3 newPosition;

        if (isNext)
        {
            desiredX = pageEntryX;
        }
        else
        {
            desiredX = pageExitX;

        }

        newPosition = new Vector3(desiredX, spawnY, playerCurrentPosition.z);
        //Debug.Log(newPosition);
        return newPosition;
    }
    public void SavePosition(Vector3 pos)
    {
        lastUsedSpawn = pos;
    }

    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnNewPageOpen, PlacePlayerInNewPage);
            EventManager.Unsubscribe(Evento.OnEncounterStart, SaveCurrentPosition);
            EventManager.Unsubscribe(Evento.OnPageTurnStart, SetPlayerTargetPosition);
            EventManager.Unsubscribe(Evento.OnPageFinishTurning, FinishRide);
        }
    }
}
