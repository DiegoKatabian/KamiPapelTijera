using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public class ShowcaseSlot : InventorySlot
{
    [SerializeField] TextMeshProUGUI descriptionTextComponent;
    
    override public void SetItem(InventoryItem item)
    {
        base.SetItem(item);
        //descriptionTextComponent.text = item.itemDescription;
        StartCoroutine(SetLocalizedText(item.itemDescription, descriptionTextComponent));
    }

    override public void ClearSlot()
    {
        base.ClearSlot();
        //ver ClearSlot de InventorySlot: hay que sacarlo del registro de LocalizedText
        LocalizedText.Limpiar(descriptionTextComponent);
        descriptionTextComponent.text = "";
    }

    
}
