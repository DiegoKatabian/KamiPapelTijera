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
        //same rule as the name lookup (#254): only the latest one may live, or a late answer writes the
        //previous item's description; and none for a slot that SetItem just emptied (amount 0)
        StopDescriptionLookup();
        if (currentItem != null && gameObject.activeInHierarchy)
        {
            _descriptionRoutine = StartCoroutine(SetLocalizedText(item.itemDescription, descriptionTextComponent));
        }
    }

    Coroutine _descriptionRoutine;

    void StopDescriptionLookup()
    {
        if (_descriptionRoutine != null)
        {
            StopCoroutine(_descriptionRoutine);
            _descriptionRoutine = null;
        }
    }

    override public void ClearSlot()
    {
        StopDescriptionLookup();
        base.ClearSlot();
        //ver ClearSlot de InventorySlot: hay que sacarlo del registro de LocalizedText
        LocalizedText.Limpiar(descriptionTextComponent);
        descriptionTextComponent.text = "";
    }

    
}
