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
        }
    }
}
