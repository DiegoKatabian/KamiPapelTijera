using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbuelaDialogueTrigger : TriggerDialogue
{
    //este script maneja todo lo que es dialogos de la abuela
    //nada que ver con los movimientos. eso esta en el npc_abuela

    //la abuela tiene 2 dialogos. 
    //cuando te la encontras (que dispara la bossfight)
    //y el post-entrega (que dispara el final storyboard)

    //el segundo dialogo recien se desbloquea cuando entregas la quest de norberto


    //BUENO, NO REALMENTE
    //la abuela va a tener 3 dialogos
    //1. cuando la encontras -> habilita la bossfight
    //2. post-boss -> habilita el origami
    //3. post-entrega y post-norberto -> habilita el final storyboard


    bool firstTime = true;
    [SerializeField] NPC_Abuela abuela;
    bool isLocked = false;

    [SerializeField] GameObject globitoDialogo;
    [SerializeField] QuestSO requiredQuest; //la quest necesaria para desbloquear el segundo dialogo
    [SerializeField] GameObject foldOrigamiSeal, unfoldOrigamiSeal;
    bool _isFirstTimeEntering = true;


    //la abuela triggerea su primer dialogo de una, sin esperar a que apretes E

    protected override void Start()
    {
        EventManager.Subscribe(Evento.OnPlayerPressedE, Interact); //los triggers siempre estan atentos a que el player aprete E
        EventManager.Subscribe(Evento.OnDialogueEnd, OnDialogueEnded);
        EventManager.Subscribe(Evento.OnPlayerChooseContinueGame, Unlock);
        EventManager.Subscribe(Evento.OnQuestDelivered, CheckQuestCompletion);
        EventManager.Subscribe(Evento.OnEncounterEnd, Unlock);
        EventManager.Subscribe(Evento.OnAbuelaFold, OnAbuelaFolded);
        EventManager.Subscribe(Evento.OnAbuelaUnfold, OnAbuelaUnfolded);
    }

    public void OnDialogueEnded(params object[] parameters)
    {
        //Debug.Log("on dialogue ended");

        if ((DialogueSO)parameters[1] == _dialogues[1])
        {
            OnSecondDialogueEnded();
        }

        if ((DialogueSO)parameters[1] == _dialogues[2])
        {
            OnThirdDialogueEnded();
        }
    }
    private void OnAbuelaFolded(params object[] parameters)
    {
        //Debug.Log("on abuela folded");
        EnableOrigamiSeal(foldOrigamiSeal, false);
        LevelManager.Instance.AddResource(ResourceType.abuela, 1);
        abuela.GetFolded();
        AudioManager.instance.PlayByName("MagicSuccess", 1.6f);
        abuela.PlaceAbuelaAtUnfoldPoint();
        PageScrollerManager.Instance.esferaNext.gameObject.SetActive(true);
        PageScrollerManager.Instance.esferaPrev.gameObject.SetActive(true);

    }

    private void OnAbuelaUnfolded(params object[] parameters)
    {
        //Debug.Log("on abuela unfolded");
        EnableOrigamiSeal(unfoldOrigamiSeal, false);
        LevelManager.Instance.AddResource(ResourceType.abuela, -1);
        abuela.GetUnfolded();
        AudioManager.instance.PlayByName("MagicSuccess", 1.06f);
        Unlock();
    }

    void CheckQuestCompletion(params object[] parameters)
    {
        //si la quest que se completó es la required...
        if ((QuestSO)parameters[0] == requiredQuest)
        {
            //Debug.Log("required quest completed");

            EnableOrigamiSeal(unfoldOrigamiSeal, true);
        }
    }

    void Unlock(params object[] parameters)
    {
        //habilita poder hablarle a la abuela
        isLocked = false;
        globitoDialogo.SetActive(true);
    }
    public void Lock()
    {
        isLocked = true; //la lockeo para q no puedas hablarle hasta ser deslockeada
        globitoDialogo.SetActive(false);
    }

    public override void OnEnterBehaviour(Collider other)
    {
        triggerBool = true;

        if (_isFirstTimeEntering)
        {
            DialogueManager.Instance.ShowDialogue(_dialogues[currentDialogue]);
            _isFirstTimeEntering = false;
            return;
        }

        if (!isLocked)
        {
            TooltipManager.Instance.ShowTooltip(tooltipTextToShow, postItColor);
        }
    }
    public override void Interact(params object[] parameter)
    {
        if (isLocked)
        {
            return;
        }

        if (triggerBool)
        {
            DialogueManager.Instance.ShowDialogue(_dialogues[currentDialogue]);

            if (firstTime)
            {
                OnFirstDialogueEnded();
            }
        }
    }

    public void OnFirstDialogueEnded()
    {
        //Debug.Log("on first dialogue ended");

        firstTime = false;
        AudioManager.instance.PlayByName("MagicSuccess", 2f);
        Lock();
        PasarAlSiguienteDialogo(); //despues de hablarle x primera vez a la awela, pasa al dialogo 2
    }

    public void OnSecondDialogueEnded()
    {
        //Debug.Log("on second dialogue ended");
        EnableOrigamiSeal(foldOrigamiSeal, true);
        Lock();
        PasarAlSiguienteDialogo();
    }

    public void OnThirdDialogueEnded()
    {
        //Debug.Log("on third dialogue ended");
        isLocked = true;
    }

    public void EnableOrigamiSeal(GameObject origamiSeal, bool value)
    {
        //Debug.Log("enable origami seal" + origamiSeal + value);
        origamiSeal.SetActive(value);
    }

    protected override void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, Interact);
            EventManager.Unsubscribe(Evento.OnDialogueEnd, OnDialogueEnded);
            EventManager.Unsubscribe(Evento.OnPlayerChooseContinueGame, Unlock);
            EventManager.Unsubscribe(Evento.OnQuestDelivered, CheckQuestCompletion);
            EventManager.Unsubscribe(Evento.OnEncounterEnd, Unlock);
            EventManager.Unsubscribe(Evento.OnAbuelaFold, OnAbuelaFolded);
            EventManager.Unsubscribe(Evento.OnAbuelaUnfold, OnAbuelaUnfolded);
        }
    }
}
