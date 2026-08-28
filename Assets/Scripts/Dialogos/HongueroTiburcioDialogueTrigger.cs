using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HongueroTiburcioDialogueTrigger : TriggerDialogue
{
    //tiburcio es el de la quest del arbol cortado. cuando el arbol cae, la quest se completa y puede venir a entregar reward.

    [SerializeField] QuestSO myQuest;
    [SerializeField] int paperReward = 20;

    bool treeWasCut = false; //flag si el arbol ya fue cortado (quest completada)

    protected override void Start()
    {
        EventManager.Subscribe(Evento.OnPlayerPressedE, Interact);
        EventManager.Subscribe(Evento.OnDialogueEnd, PasarAlSiguienteDialogo);
        EventManager.Subscribe(Evento.OnQuestCompleted, HandleQuestCompleted); //escuchar cuando corta el arbol
    }

    public override void Interact(params object[] parameter)
    {
        if (triggerBool)
        {
            //flujo: dialogo0→1 (esperando arbol) → dialogo2 (arbol cayó) → dialogo3 (después de entregar)
            if (!treeWasCut)
            {
                //arbol aun no fue cortado
                if (currentDialogue == 0)
                {
                    currentDialogue = 1; //dialogo: "please cut the tree"
                }
            }
            else
            {
                //arbol ya fue cortado
                if (currentDialogue <= 1)
                {
                    currentDialogue = 2; //dialogo: "gracias por el arbol!"
                    AudioManager.instance.PlayByName("QuestCompleted02");
                }
                else if (currentDialogue == 2)
                {
                    currentDialogue = 3; //dialogo repetido post-delivery
                }
            }

            DialogueManager.Instance.ShowDialogue(_dialogues[currentDialogue]);
        }

        if (_burnAfterReading)
        {
            Destroy(this);
        }
    }

    private void HandleQuestCompleted(params object[] parameters)
    {
        //cuando se completa la quest (arbol cortado), marcar el flag
        QuestSO completedQuest = (QuestSO)parameters[0];
        if (completedQuest == myQuest)
        {
            treeWasCut = true;
            Debug.Log("[HongueroTiburcioDialogueTrigger] Arbol fue cortado, quest completada");
        }
    }

    protected override void PasarAlSiguienteDialogo(params object[] parameter)
    {
        if ((DialogueSO)parameter[1] == _dialogues[0])
        {
            //si el dialogo q termino fue mi dialogo0, paso al 1 automaticamente
            base.PasarAlSiguienteDialogo(parameter);
        }
    }

    protected override void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, Interact);
            EventManager.Unsubscribe(Evento.OnDialogueEnd, PasarAlSiguienteDialogo);
            EventManager.Unsubscribe(Evento.OnQuestCompleted, HandleQuestCompleted);
        }
    }
}
