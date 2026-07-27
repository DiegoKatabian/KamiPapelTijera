using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GranjeroNorbertoDialogueTrigger : QuestDialogueTrigger
{
    [SerializeField] GameObject tijeraPickup;
    bool isLocked = false;

    protected override void Start()
    {
        EventManager.Subscribe(Evento.OnPlayerPressedE, Interact); //los triggers siempre estan atentos a que el player aprete E
        EventManager.Subscribe(Evento.OnQuestCompleted, CompleteQuest);
        EventManager.Subscribe(Evento.OnDialogueEnd, OnDialogueEnded);
        EventManager.Subscribe(Evento.OnAbuelaUnfold, OnAbuelaUnfolded);

    }

    public override void OnEnterBehaviour(Collider other)
    {
        base.OnEnterBehaviour(other);

        if (firstTime && !LevelManager.Instance.inDialogue)
        {
            ShowFirstTimeDialogue();
        }
    }

    public override void Interact(params object[] parameter)
    {
        if (isLocked)
        {
            return;
        }

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

            //if (currentDialogue == 2) //el 2 es el unico q pasa automatico al 3
            //{
            //    currentDialogue++;
            //}
        }

        if (_burnAfterReading)
        {
            Destroy(this);
        }
    }

    private void OnAbuelaUnfolded(object[] parameters)
    {
        PasarAlSiguienteDialogo();
        isLocked = false;
    }

    protected override void OnDialogueEnded(object[] parameters)
    {
        if ((DialogueSO)parameters[1] == _dialogues[2])
        {
            EventManager.Trigger(Evento.OnQuestDelivered, _quest);
            isLocked = true;
        }
    }

    protected override void ShowFirstTimeDialogue()
    {
        tijeraPickup.SetActive(true);
        base.ShowFirstTimeDialogue();
    }

    protected override void DeliverQuest()
    {
        //Debug.Log("tio norberto: quest entregada!");
        QuestManager.Instance.RemoveQuest(_quest);
        AudioManager.instance.PlayByName("QuestCompleted02", 1.1f);
        currentDialogue = 2;
        _questDelivered = true;
    }

    protected override void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, Interact);
            EventManager.Unsubscribe(Evento.OnQuestCompleted, CompleteQuest);
            EventManager.Unsubscribe(Evento.OnDialogueEnd, OnDialogueEnded);
            EventManager.Unsubscribe(Evento.OnAbuelaUnfold, OnAbuelaUnfolded);

        }
    }
}
