using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerDialogue : TriggerScript
{
    //Interactuable: aca el boton de accion sirve para hablar con el NPC / avanzar el dialogo.
    public override bool EsInteractuable => true;

    [SerializeField] protected bool _burnAfterReading;
    [SerializeField] protected DialogueSO[] _dialogues;

    protected int currentDialogue = 0;

    public override void Interact(params object[] parameter)
    {
        if (triggerBool)
        {
            //guard: un NPC a medio configurar (sin dialogos, o con el indice pasado del array)
            //tiraba excepcion y dejaba al jugador sin poder interactuar. Mejor avisar quien es
            if (_dialogues == null || _dialogues.Length == 0)
            {
                Debug.LogWarning($"[TriggerDialogue] {gameObject.name} no tiene dialogos asignados en el inspector");
                return;
            }

            if (currentDialogue < 0 || currentDialogue >= _dialogues.Length)
            {
                Debug.LogWarning($"[TriggerDialogue] {gameObject.name}: el indice de dialogo {currentDialogue} se paso del array ({_dialogues.Length}), uso el ultimo");
                currentDialogue = _dialogues.Length - 1;
            }

            if (_dialogues[currentDialogue] == null)
            {
                Debug.LogWarning($"[TriggerDialogue] {gameObject.name}: el dialogo {currentDialogue} esta vacio en el inspector");
                return;
            }

            DialogueManager.Instance.ShowDialogue(_dialogues[currentDialogue]);
        }

        if (_burnAfterReading)
        {
            Destroy(this);
        }
    }
    protected virtual void PasarAlSiguienteDialogo(params object[] parameter)
    {
        if (currentDialogue < _dialogues.Length)
        {
            currentDialogue++;
            //print(currentDialogue);
        }
    }
}