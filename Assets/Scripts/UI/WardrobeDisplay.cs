using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Wardrobe tab of the Flap (spec 011 FR-104): one row per gear slot with the gear items Kami
/// owns, the one she wears drawn bigger. The rows (label + container with a layout group) are
/// authored in FlapManager.prefab; the item buttons are copies of InventorySlot.prefab spawned here,
/// one per owned item, so a new item needs no prefab edit. Tapping one goes through
/// InventorySlot.BUTTON_OnPress, the same path as the bag (Player.TryToggleGear).
///
/// "Owned" is the bag's own count (LevelManager.recursosRecolectados), so the Wardrobe can never
/// disagree with the bag: confiscated scissors leave both at once.
/// </summary>
public class WardrobeDisplay : MonoBehaviour
{
    [Serializable]
    public class Row
    {
        [Tooltip("The gear slot this row shows. Its items appear in GearCatalog order.")]
        public GearSlot gearSlot;

        [Tooltip("Where this row's item buttons are spawned. Its layout group places them.")]
        public RectTransform slotsParent;
    }

    [Tooltip("The button spawned for each owned item: the bag's own slot, so it looks and reacts the same.")]
    [SerializeField] InventorySlot _slotPrefab;

    [Tooltip("One row per gear slot, top to bottom. A gear slot with no row (Hat, for now) just doesn't show.")]
    [SerializeField] List<Row> _rows = new List<Row>();

    [Tooltip("Scale of the item Kami is wearing: the 'equipped' highlight until the Wardrobe has its own art.")]
    [SerializeField] float _wornScale = 1.15f;

    [Tooltip("Scale of an item she owns but isn't wearing.")]
    [SerializeField] float _notWornScale = 0.85f;

    readonly Dictionary<Row, List<InventorySlot>> _spawned = new Dictionary<Row, List<InventorySlot>>();
    readonly Dictionary<InventorySlot, GearItem> _gearBySlot = new Dictionary<InventorySlot, GearItem>();

    Player _player;
    bool _dirty;

    void OnEnable()
    {
        _player = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (_player == null)
        {
            Debug.LogWarning("[WardrobeDisplay] no Player in LevelManager: the Wardrobe can't show what Kami wears");
        }
        else
        {
            _player.OnGearChanged += RefreshHighlights;
        }

        EventManager.Subscribe(Evento.OnResourceUpdated, MarkDirty);

        //rebuilt synchronously: FlapManager selects the first button right after showing this tab
        Rebuild();
    }

    void OnDisable()
    {
        if (_player != null)
        {
            _player.OnGearChanged -= RefreshHighlights;
        }
        EventManager.Unsubscribe(Evento.OnResourceUpdated, MarkDirty);
    }

    //Not rebuilt inside the event: InventoryManager updates the amounts InventorySlot.SetItem reads in
    //its own handler of the same event, and may run after this one.
    void MarkDirty(params object[] parameters)
    {
        _dirty = true;
    }

    void LateUpdate()
    {
        if (_dirty)
        {
            Rebuild();
        }
    }

    void Rebuild()
    {
        _dirty = false;

        GearCatalog catalog = GearCatalog.Instance;
        if (catalog == null || InventoryManager.Instance == null || LevelManager.Instance == null)
        {
            return; //the catalog warns once by itself; the managers exist in every level scene
        }

        if (_slotPrefab == null)
        {
            Debug.LogWarning("[WardrobeDisplay] no slot prefab assigned: the Wardrobe stays empty");
            return;
        }

        _gearBySlot.Clear();
        foreach (Row row in _rows)
        {
            if (row == null || row.slotsParent == null)
            {
                Debug.LogWarning("[WardrobeDisplay] a row has no slots parent: skipped");
                continue;
            }

            if (!_spawned.TryGetValue(row, out List<InventorySlot> slots))
            {
                slots = new List<InventorySlot>();
                _spawned.Add(row, slots);
            }

            int used = 0;
            foreach (GearItem item in catalog.ItemsIn(row.gearSlot))
            {
                InventoryItem bagItem = OwnedBagItem(item);
                if (bagItem == null)
                {
                    continue;
                }

                if (used == slots.Count)
                {
                    InventorySlot spawned = Instantiate(_slotPrefab, row.slotsParent);
                    spawned.name = $"WardrobeSlot_{row.gearSlot}_{used}";
                    slots.Add(spawned);
                }

                InventorySlot slot = slots[used];
                slot.gameObject.SetActive(true);
                slot.SetItem(bagItem);
                _gearBySlot[slot] = item;
                used++;
            }

            //reused, not destroyed: Destroy waits for the end of the frame, and a dead button could
            //still be the one FlapManager selects
            for (int i = used; i < slots.Count; i++)
            {
                slots[i].gameObject.SetActive(false);
            }
        }

        RefreshHighlights();
    }

    //The bag item that stands for this gear item, if Kami owns at least one; null otherwise
    InventoryItem OwnedBagItem(GearItem item)
    {
        if (!item.TryGetResource(out ResourceType resource))
        {
            return null;
        }

        if (!LevelManager.Instance.recursosRecolectados.TryGetValue(resource, out int amount) || amount < 1)
        {
            return null;
        }

        if (!InventoryManager.Instance.itemsByResourceType.TryGetValue(resource, out InventoryItem bagItem))
        {
            Debug.LogWarning($"[WardrobeDisplay] '{item.name}' is owned but {resource} isn't in this scene's InventoryManager list: not shown");
            return null;
        }

        return bagItem;
    }

    void RefreshHighlights()
    {
        foreach (KeyValuePair<InventorySlot, GearItem> pair in _gearBySlot)
        {
            bool worn = _player != null && _player.Gear.GetEquipped(pair.Value.Slot) == pair.Value;
            pair.Key.transform.localScale = Vector3.one * (worn ? _wornScale : _notWornScale);
        }
    }
}
