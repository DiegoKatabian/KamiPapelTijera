using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestDialogueTrigger : TriggerDialogue
{
    //si tu npc tiene exactamente 4 dialogos y una quest de recursos:
    //este script es para vos
    
    [SerializeField] int _paperReward = 20;
    [SerializeField] protected QuestSO _quest; //la quest que da este pj
    protected bool _questCompleted;
    protected bool _questDelivered;
    protected bool firstTime = true;

    protected override void Start()
    {
        EventManager.Subscribe(Evento.OnPlayerPressedE, Interact); //los triggers siempre estan atentos a que el player aprete E
        EventManager.Subscribe(Evento.OnQuestCompleted, CompleteQuest);
        EventManager.Subscribe(Evento.OnDialogueEnd, OnDialogueEnded);
        EventManager.Subscribe(Evento.OnDialogueWriteText, OnDialogueTextWritten);
    }

    //dialogos: 
    //el 0 es pedir la quest (first time)
    //el 1 es el fijo mientras la quest esta prendida
    //el 2 es el de gracias por traerme las flores con reward (first time)
    //el 3 es el fijo despues de haber completado la quest

    //los dialogo2 tienen una particularidad:
    //mandan mensaje de OnQuestRewarded despues de su penultimo y ultimo texto.
    //esto se usa para disparar la fanfarrria de obtener reward

    public override void Interact(params object[] parameter)
    {
        if (triggerBool && !LevelManager.Instance.inDialogue) //este temita del interact se podria hacer mejor, ya lo demostre en otro lado
        {
            if (firstTime)
            {
                ShowFirstTimeDialogue();
                return;
            }

            if (_questCompleted && !_questDelivered)
            {
                DeliverQuest();
            }

            DialogueManager.Instance.ShowDialogue(_dialogues[currentDialogue]);

            if (currentDialogue == 2) //el 2 es el unico q pasa automatico al 3
            {
                currentDialogue++;
            }
        }

        if (_burnAfterReading)
        {
            Destroy(this);
        }
    }

    protected virtual void ShowFirstTimeDialogue()
    {
        //mostrar el 0 si o si, y pasar al 1
        DialogueManager.Instance.ShowDialogue(_dialogues[0]);
        QuestManager.Instance.AddQuest(_quest);
        currentDialogue++;
        firstTime = false;
    }

    protected virtual void CompleteQuest(params object[] parameter)
    {
        Debug.Log("quest completada");
        if ((QuestSO)parameter[0] == _quest) //si la quest que se completo es la mia
        {
            _questCompleted = true;
        }
    }

    protected virtual void DeliverQuest()
    {
        //Debug.Log("dalia: quest entregada!");
        QuestManager.Instance.RemoveQuest(_quest);
        AudioManager.instance.PlayByName("QuestCompleted02");
        currentDialogue = 2;
        _questDelivered = true;
    }

    protected virtual void OnDialogueTextWritten(params object[] parameters)
    {
        //Este metodo corre por CADA texto de CUALQUIER dialogo del juego (el evento es global),
        //asi que un NPC de quest a medio configurar (menos de 3 dialogos) reventaba el dialogo
        //de todos los demas, no solo el suyo
        if (_dialogues == null || _dialogues.Length < 3)
        {
            return;
        }

        //si es el dialogo 2
        if ((DialogueSO)parameters[0] == _dialogues[2])
        {
            DialogueSO dialogue = (DialogueSO)parameters[0];
            if (dialogue.currentText == dialogue.events.Length) //si es el penultimo textito
            {
                Debug.Log("disparo el penultimo texto del dialogo de reward");
                LevelManager.Instance.AddResource(_quest.condition.resourceType, -_quest.condition.requiredAmount); //esto deberia funcar para condition evento. tal vez con una interfaz IQuestCondition con metodo complete/deliver

                EventManager.Trigger(Evento.OnQuestRewardedStart, _quest);
                DialogueManager.Instance.lockedByAnimation = true; //el player lo va a poner en false cuando termine la animacion
                EventManager.Trigger(Evento.OnQuestDelivered, _quest);
                LevelManager.Instance.AddResource(ResourceType.papel, _paperReward);
            }
        }
    }

    protected virtual void OnDialogueEnded(params object[] parameters)
    {
        if (_dialogues == null || _dialogues.Length < 3)
        {
            return;
        }

        //si es el dialogo 2
        if ((DialogueSO)parameters[1] == _dialogues[2])
        {
            EventManager.Trigger(Evento.OnQuestRewardedEnd, _quest);
        }
    }

    protected override void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, Interact); //los triggers siempre estan atentos a que el player aprete E
            EventManager.Unsubscribe(Evento.OnQuestCompleted, CompleteQuest);
            EventManager.Unsubscribe(Evento.OnDialogueEnd, OnDialogueEnded);
            EventManager.Unsubscribe(Evento.OnDialogueWriteText, OnDialogueTextWritten);
        }
    }
}
