using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public enum RewardType
{
    SprintBoots,
    WaterBoots,
    TijeraMejorada,
    None,
    Count
}

public class QuestManager : Singleton<QuestManager>
{
    //las quests son de tipo
    //Evento (se cumplen cuando se triggerea ese evento)
    //o Resource (se cumplen cuando obtenes N de ese resource)

    //quest completed se triggerea EN EL MOMENTO en el que pasa el evento o se obtiene el resource
    //quest delivered, cuando empezas el dialogo con el npc teniendo la quest completa
    //quest rewarded, cuando se TERMINA el dialogo de entregar la quest y empieza la secuencia de reward

    List<QuestSO> quests = new List<QuestSO>();
    Dictionary<Evento, bool> eventosSucedidos = new Dictionary<Evento, bool>();

    [SerializeField] GameObject questSlotPrefab;
    [SerializeField] GameObject questSlotsParent;
    List<QuestSlot> questSlots = new List<QuestSlot>();

    void Start()
    {
        EventManager.Subscribe(Evento.OnResourceUpdated, CheckQuests);
        EventManager.Subscribe(Evento.OnAbuelaDropoff, SetAbuelaDropoff);
        EventManager.Subscribe(Evento.OnQuestDelivered, GiveReward);
        EventManager.Subscribe(Evento.OnTreeCutForChickens, SetTreeCutForChickens);
        EventManager.Subscribe(Evento.OnAllCluesFound, SetAllCluesFound);
        EventManager.Subscribe(Evento.OnGiftAtNataliasDoorReached, SetGiftAtNataliasDoorReached);
        //clear all quests
        //quests.Clear();
    }

    private void GiveReward(params object[] parameters)
    {
        QuestSO deliveredQuest = (QuestSO)parameters[0];

        switch (deliveredQuest.rewardType)
        {
            case RewardType.SprintBoots:
                LevelManager.Instance.GiveSprintBoots();
                break;
            case RewardType.WaterBoots:
                LevelManager.Instance.GiveWaterBoots();
                break;
            case RewardType.TijeraMejorada:
                LevelManager.Instance.GiveTijeraMejorada();
                break;
            case RewardType.None:
                break;
            case RewardType.Count:
                break;
            default:
                break;
        }
    }
    public void SetAbuelaDropoff(params object[] parameter)
    {
        if (!eventosSucedidos.ContainsKey(Evento.OnAbuelaDropoff))
        {
            eventosSucedidos.Add(Evento.OnAbuelaDropoff, false);
        }

        eventosSucedidos[Evento.OnAbuelaDropoff] = true;
        CheckQuests();
    }

    public void SetTreeCutForChickens(params object[] parameter)
    {
        if (!eventosSucedidos.ContainsKey(Evento.OnTreeCutForChickens))
        {
            eventosSucedidos.Add(Evento.OnTreeCutForChickens, false);
        }

        eventosSucedidos[Evento.OnTreeCutForChickens] = true;
        CheckQuests();
    }
    //Level 2 (spec 006): completes Quest05_FindClues. Same mold as SetTreeCutForChickens --
    //an Event quest only completes if QuestManager itself marks the event as happened, so a
    //new Event condition always needs its handler registered here too.
    public void SetAllCluesFound(params object[] parameter)
    {
        if (!eventosSucedidos.ContainsKey(Evento.OnAllCluesFound))
        {
            eventosSucedidos.Add(Evento.OnAllCluesFound, false);
        }

        eventosSucedidos[Evento.OnAllCluesFound] = true;
        Debug.Log("[QuestManager] OnAllCluesFound registered, checking quests");
        CheckQuests();
    }

    //Level 2 (spec 006): completes Quest06_GoBackToNataliasHouse. Same mold as SetAllCluesFound.
    public void SetGiftAtNataliasDoorReached(params object[] parameter)
    {
        if (!eventosSucedidos.ContainsKey(Evento.OnGiftAtNataliasDoorReached))
        {
            eventosSucedidos.Add(Evento.OnGiftAtNataliasDoorReached, false);
        }

        eventosSucedidos[Evento.OnGiftAtNataliasDoorReached] = true;
        Debug.Log("[QuestManager] OnGiftAtNataliasDoorReached registered, checking quests");
        CheckQuests();
    }

    public bool EventoYaSucedio(Evento evento)
    {
        //consulta de SOLO LECTURA del estado autoritativo de eventos ya sucedidos.
        //existe para que un NPC pueda recuperarse si se perdio el OnQuestCompleted en vivo
        //(se suscribio tarde, el evento paso antes de que su quest estuviera en la lista, etc):
        //en vez de confiar unicamente en su flag cacheado, pregunta aca.
        //no muta nada ni dispara eventos, asi que es seguro llamarlo cuando sea.
        return eventosSucedidos.TryGetValue(evento, out bool sucedio) && sucedio;
    }

    public void CheckQuests(params object[] parameters)
    {
        //Debug.Log("me pongo a chequear todas las quests");
        foreach (QuestSO quest in quests)
        {
            if (quest.condition.conditionType == ConditionType.Resource &&
                LevelManager.Instance.recursosRecolectados[quest.condition.resourceType] >= quest.condition.requiredAmount)
            {
                //Debug.Log("quest manager: se completo la " + quest.name);
                CompleteQuest(quest);
            }

            if (quest.condition.conditionType == ConditionType.Event &&
                eventosSucedidos[quest.condition.evento])
            {
                Debug.Log("check quests: se completo la " + quest.name);
                CompleteQuest(quest);
            }
        }
    }
    public void AddQuest(QuestSO quest) //esto lo disparan los npcs cuando les hablo
    {
        //Debug.Log("agrego la quest");
        if (quest.condition.conditionType == ConditionType.Event &&
            !eventosSucedidos.ContainsKey(quest.condition.evento))
        {
            eventosSucedidos.Add(quest.condition.evento, false);
        }
        quests.Add(quest);

        questSlots.Add(Instantiate(questSlotPrefab, questSlotsParent.transform).GetComponent<QuestSlot>());
        questSlots.Last().SetQuest(quest);

        CheckQuests();
    }
    public void RemoveQuest(QuestSO quest)
    {
        Debug.Log("remuevo la quest");
        quests.Remove(quest);

        //find questslot in my list with this quest
        foreach (QuestSlot questSlot in questSlots)
        {
            if (questSlot.currentQuest == quest)
            {
                questSlots.Remove(questSlot);
                Destroy(questSlot.gameObject);
                break;
            }
        }
    }
    public void RemoveQuestSlot(QuestSlot questSlot)
    {

    }

    public void CompleteQuest(QuestSO quest)
    {
        EventManager.Trigger(Evento.OnQuestCompleted, quest);
        //Debug.Log("complete quest: " + quest);
    }

    void OnDestroy()
    {
        if(!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnResourceUpdated, CheckQuests);
            EventManager.Unsubscribe(Evento.OnAbuelaDropoff, SetAbuelaDropoff);
            EventManager.Unsubscribe(Evento.OnQuestDelivered, GiveReward);
            EventManager.Unsubscribe(Evento.OnTreeCutForChickens, SetTreeCutForChickens);
            EventManager.Unsubscribe(Evento.OnAllCluesFound, SetAllCluesFound);
            EventManager.Unsubscribe(Evento.OnGiftAtNataliasDoorReached, SetGiftAtNataliasDoorReached);
        }
    }
}
