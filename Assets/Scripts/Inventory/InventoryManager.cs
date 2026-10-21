using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : Singleton<InventoryManager>
{
    [SerializeField] GameObject _inventorySlotsParent;
    [SerializeField] InventorySlot _showcaseSlot;
    InventorySlot[] _slots;

    [SerializeField] InventoryItem[] _allItems;

    public Dictionary<ResourceType, InventoryItem> itemsByResourceType = new Dictionary<ResourceType, InventoryItem>();

    public Dictionary<InventoryItem, int> itemsAmount = new Dictionary<InventoryItem, int>();

    public Sprite emptyItemSprite;

    private void Start()
    {
        _slots = _inventorySlotsParent.GetComponentsInChildren<InventorySlot>();

        foreach (InventoryItem item in _allItems)
        {
            //check if it wasnt added already
            if (itemsByResourceType.ContainsKey(item.resourceType))
            {
                Debug.Log("ya estaba");
            }

            itemsByResourceType.Add(item.resourceType, item);
        }

        //clear all slots
        _showcaseSlot.ClearSlot();
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].ClearSlot();
        }

        //fill the itemsamount dictionary
        foreach (InventoryItem item in _allItems)
        {
            itemsAmount.Add(item, 0);
        }

        EventManager.Subscribe(Evento.OnResourceUpdated, AddItem);
    }

    public void ShowcaseItem(InventoryItem item)
    {
        _showcaseSlot.SetItem(item);
    }

    //a comfy method that sets the value for a given key of the itemsmaount dictionary

    public void SetItemAmount(InventoryItem item, int amount)
    {
        itemsAmount[item] = amount;


    }

    public void AddItem(params object[] parameters)
    {
        ResourceType rt = (ResourceType)parameters[0];

        //every Kami now gets her outfit at level start (spec 011), so a scene whose list predates an
        //item would throw here on its first frame
        if (!itemsByResourceType.ContainsKey(rt))
        {
            Debug.LogWarning($"[InventoryManager] {rt} is not in this scene's item list (_allItems): it won't show in the bag. Add its InventoryItem asset to the list.");
            return;
        }

        InventoryItem item = itemsByResourceType[rt];
        int total = (int)parameters[1];
        SetItemAmount(item, total);

        //Look at EVERY slot before deciding: the old single pass claimed the first empty slot even when
        //the item sat in a later one, and "amount 0" claimed one for an item that was not in the bag at all
        int itemSlot = -1;
        int firstEmptySlot = -1;
        bool needsReorganize = false;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].currentItem == item)
            {
                if (itemSlot < 0)
                {
                    itemSlot = i;
                }
                else
                {
                    //one item in two slots is never right (Level 2 review 2026-10-20: four scissors in the bag): drop the copies
                    Debug.LogWarning($"[InventoryManager] {rt} was in slot {itemSlot} and again in slot {i}: clearing the copy");
                    _slots[i].ClearSlot();
                    needsReorganize = true;
                }
            }
            else if (_slots[i].currentItem == null && firstEmptySlot < 0)
            {
                firstEmptySlot = i;
            }
        }

        if (total <= 0)
        {
            //nothing left: free its slot if it has one, never claim one for it
            if (itemSlot >= 0)
            {
                _slots[itemSlot].ClearSlot();
                needsReorganize = true;
                Debug.Log($"[InventoryManager] {rt} gone from the bag (slot {itemSlot} freed)");
            }
        }
        else if (itemSlot >= 0)
        {
            _slots[itemSlot].SetItem(item); //already in the bag: refresh its amount
        }
        else if (firstEmptySlot >= 0)
        {
            _slots[firstEmptySlot].SetItem(item);
            Debug.Log($"[InventoryManager] {rt} (x{total}) takes slot {firstEmptySlot} of {_slots.Length}");
        }
        else
        {
            Debug.LogWarning($"[InventoryManager] the bag is full ({_slots.Length} slots): {rt} was not shown");
        }

        if (needsReorganize)
        {
            ReorganizeSlots();
        }
    }

    public void ReorganizeSlots()
    {
        List<InventoryItem> nonEmptyItems = new List<InventoryItem>();

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].currentItem != null)
            {
                nonEmptyItems.Add(_slots[i].currentItem); //me lleno mi lista aux, borro todos
                _slots[i].ClearSlot();
            }
        }

        for (int i = 0; i < nonEmptyItems.Count; i++)
        {
            _slots[i].SetItem(nonEmptyItems[i]); //lleno de nuevo
        }
    }

    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnResourceUpdated, AddItem);
        }
    }

}
