using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestSlot : MonoBehaviour
{
    [HideInInspector] public QuestSO currentQuest;
    [SerializeField] TextMeshProUGUI nameTextComponent, descriptionTextComponent, amountTextComponent;
    [SerializeField] Image imageComponent;
    bool setNamePromised = false;

    private void Start()
    {
        EventManager.Subscribe(Evento.OnResourceUpdated, UpdateResource);
    }
    private void OnEnable()
    {
        if (setNamePromised)
        {
            StartCoroutine(SetLocalizedText(currentQuest.questName, nameTextComponent));
            StartCoroutine(SetLocalizedText(currentQuest.questDescription, descriptionTextComponent));
            setNamePromised = false;
        }
    }
    public virtual void SetQuest(QuestSO quest)
    {
        currentQuest = quest;

        imageComponent.sprite = currentQuest.questSprite; //seteo carita del npc

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(SetLocalizedText(currentQuest.questName, nameTextComponent));
            StartCoroutine(SetLocalizedText(currentQuest.questDescription, descriptionTextComponent));
        }
        else
        {
            setNamePromised = true;
        }
        
        if (currentQuest.condition.conditionType == ConditionType.Resource) //si es una quest de recolectar cosas
        {
            amountTextComponent.text = LevelManager.Instance.recursosRecolectados[currentQuest.condition.resourceType].ToString()
                + "/"
                + currentQuest.condition.requiredAmount; //escribo cuanto voy del total requerido
        }
        else
        {
            amountTextComponent.text = ""; //si no, vacio
        }
    }

    public void UpdateResource(params object[] parameters)
    {
        //si mi quest es de recolectar
        //y el recurso que se actualizo es el mismo que el de mi quest
        if (currentQuest != null &&
            currentQuest.condition.conditionType == ConditionType.Resource &&
            currentQuest.condition.resourceType == (ResourceType)parameters[0])
        {
            amountTextComponent.text = LevelManager.Instance.recursosRecolectados[currentQuest.condition.resourceType].ToString()
                + "/"
                + currentQuest.condition.requiredAmount;
        }
    }

    public void SelfDestruct()
    {
        Debug.Log("self destruct");
        //QuestManager.Instance.RemoveQuest(currentQuest);
        Destroy(gameObject);
    }

    //la corrutina se fue a LocalizedText (era una de siete copias identicas)
    private IEnumerator SetLocalizedText(string fallbackText, TMPro.TextMeshProUGUI textElement)
    {
        return LocalizedText.Escribir(textElement, fallbackText, "QuestTable", new LocalizedText.Opciones
        {
            origen = "QuestSlot"
        });
    }
}
