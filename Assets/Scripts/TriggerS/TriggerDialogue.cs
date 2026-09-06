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
            //print("trigger dialogue interact: muestro el dialogo " + _dialogues[currentDialogue].name);
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