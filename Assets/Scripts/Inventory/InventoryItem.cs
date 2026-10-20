using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryItem", menuName = "ScriptableObjects/Item")]
public class InventoryItem : ScriptableObject
{
    public string itemName;
    public string itemDescription;
    public Sprite itemSprite;
    [Tooltip("Optional icon per language, for items with words drawn on them (the cafe ticket). Wins over itemSprite.")]
    public LocalizedSprites localizedSprite;
    public Color itemColor; //para q cambie el color del sticker jeje :P
    public ResourceType resourceType;
    /// <summary>The icon to show: the current language's when the item has one.</summary>
    public Sprite Icon
    {
        get
        {
            Sprite localized = localizedSprite != null ? localizedSprite.Get() : null;
            return localized != null ? localized : itemSprite;
        }
    }

    //public int amount;
    //public bool isRecortable;
    //public InventoryItem possibleCutChild;
}
