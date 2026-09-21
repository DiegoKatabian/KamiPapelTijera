using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HongueroTiburcioDialogueTrigger : TriggerDialogue
{
    //tiburcio es el de la quest del arbol cortado. cuando el arbol cae, la quest se completa y puede venir a entregar reward.

    [SerializeField][Tooltip("Quest del arbol (se dispara cuando OnTreeCutForChickens)")]
    QuestSO myQuest;
    [SerializeField] int paperReward = 20;

    //cache de "el arbol ya fue cortado". OJO: es una CACHE, no la fuente de verdad.
    //la fuente de verdad la tiene QuestManager (eventosSucedidos): ver ArbolYaCortado().
    bool treeWasCut = false;

    protected override void Start()
    {
        if (myQuest == null)
        {
            Debug.LogError("[HongueroTiburcioDialogueTrigger] myQuest no esta asignada en inspector! Quest system no funcionara.");
        }

        EventManager.Subscribe(Evento.OnPlayerPressedE, Interact);
        EventManager.Subscribe(Evento.OnDialogueEnd, PasarAlSiguienteDialogo);
        EventManager.Subscribe(Evento.OnQuestCompleted, HandleQuestCompleted); //escuchar cuando corta el arbol
    }

    public override void Interact(params object[] parameter)
    {
        if (triggerBool)
        {
            //si es la primera vez, agregar quest a QuestManager
            if (currentDialogue == 0 && myQuest != null)
            {
                QuestManager.Instance.AddQuest(myQuest);
            }

            //flujo: dialogo0→1 (esperando arbol) → dialogo2 (arbol cayó) → dialogo3 (después de entregar)
            if (!ArbolYaCortado())
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
                    AudioManager.instance.Play(AudioId.QuestCompleted02);
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

    private bool ArbolYaCortado()
    {
        //POR QUE existe esto: treeWasCut solo se prende si llega OnQuestCompleted EN VIVO.
        //si ese evento se pierde (el trigger se suscribio despues de que paso, la quest todavia
        //no estaba en la lista del QuestManager cuando cayo el arbol, etc) el flag quedaba en
        //false para siempre y Tiburcio nunca reconocia el arbol cortado: el jugador quedaba
        //trabado sin forma de recuperarse salvo reiniciar.
        //aca consultamos el estado autoritativo (QuestManager.eventosSucedidos) recien al momento
        //de interactuar, asi el flag pasa a ser una cache recuperable y no la unica verdad.

        if (treeWasCut)
        {
            return true; //ya lo sabiamos, ni consultamos
        }

        if (myQuest == null || myQuest.condition.conditionType != ConditionType.Event)
        {
            //sin quest (o si algun dia la reconfiguran como Resource) no hay evento que consultar
            return false;
        }

        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("[HongueroTiburcioDialogueTrigger] No hay QuestManager en escena: no puedo verificar si el arbol ya fue cortado, sigo con el flag cacheado.");
            return false;
        }

        if (!QuestManager.Instance.EventoYaSucedio(myQuest.condition.evento))
        {
            return false; //el arbol realmente no fue cortado todavia
        }

        //llegamos aca solo si el evento paso pero nunca nos enteramos: recuperamos la cache
        treeWasCut = true;
        Debug.LogWarning("[HongueroTiburcioDialogueTrigger] Nunca llego OnQuestCompleted, pero QuestManager confirma que el arbol ya fue cortado: recupero el estado.");
        return true;
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
