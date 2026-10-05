using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// Every gear item in the game, and which Spine slots each gear slot owns (spec 011).
///
/// HOW IT IS LOADED: nothing to wire anywhere. The asset lives at
/// Assets/Resources/GearCatalog.asset and is loaded by name. If it is missing, gear never composes
/// and Kami keeps the plain default skin, with one warning: the game still runs.
/// </summary>
[CreateAssetMenu(fileName = "GearCatalog", menuName = "Kami/Gear Catalog")]
public class GearCatalog : ScriptableObject, IHasSkeletonDataAsset
{
    public const string ResourcesPath = "GearCatalog";

    [Serializable]
    public class OwnedSlots
    {
        [Tooltip("The gear slot that owns the Spine slots below.")]
        public GearSlot gearSlot;

        [Tooltip("Spine slots this gear slot takes over. Equipping an item here first clears whatever " +
                 "the outfit put in these slots, then adds the item. That lets an item hide part of the " +
                 "outfit (rubber boots have no buckle) without transparent placeholders. Include the " +
                 "_OL_ outline twins.")]
        [SpineSlot] public List<string> spineSlots = new List<string>();
    }

    [Tooltip("Kami's skeleton. Only feeds the Spine dropdowns here and on every GearItem: at runtime " +
             "the skin is built from the skeleton Kami actually renders.")]
    [SerializeField] SkeletonDataAsset _skeletonDataAsset;

    [Tooltip("Every gear item in the game. Getting a resource equips an item only if it is listed here.")]
    [SerializeField] List<GearItem> _items = new List<GearItem>();

    [Tooltip("Which Spine slots each gear slot owns. The outfit owns none: it is the base layer.")]
    [SerializeField] List<OwnedSlots> _ownedSlots = new List<OwnedSlots>();

    [Tooltip("Gear slots the player changes by tapping an item in the bag or the Wardrobe (spec 011 " +
             "FR-102). The others change only by getting an item: the newest pair of scissors is always " +
             "the one she holds, so their damage always matches what she wears.")]
    [SerializeField] List<GearSlot> _changeableFromBag = new List<GearSlot> { GearSlot.Outfit, GearSlot.Feet };

    static GearCatalog _instance;
    static bool _missingReported;

    Dictionary<ResourceType, GearItem> _byResource;

    public static GearCatalog Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<GearCatalog>(ResourcesPath);
                if (_instance == null && !_missingReported)
                {
                    _missingReported = true;
                    Debug.LogWarning($"[GearCatalog] could not find Resources/{ResourcesPath}.asset: Kami's gear " +
                                     "will not show and she keeps the default skin. Create it with " +
                                     "Create > Kami > Gear Catalog and put it in Assets/Resources.");
                }
            }
            return _instance;
        }
    }

    public SkeletonDataAsset SkeletonDataAsset => _skeletonDataAsset;

    /// <summary>The item that getting this resource equips, or null if it is not gear.</summary>
    public GearItem ForResource(ResourceType resource)
    {
        if (_byResource == null)
        {
            BuildResourceLookup();
        }

        _byResource.TryGetValue(resource, out GearItem item);
        return item;
    }

    public bool CanChangeFromBag(GearSlot gearSlot) => _changeableFromBag.Contains(gearSlot);

    /// <summary>The catalog's items for one gear slot, in catalog order (the Wardrobe's row order).</summary>
    public IEnumerable<GearItem> ItemsIn(GearSlot gearSlot)
    {
        foreach (GearItem item in _items)
        {
            if (item != null && item.Slot == gearSlot)
            {
                yield return item;
            }
        }
    }

    public IReadOnlyList<string> OwnedSpineSlots(GearSlot gearSlot)
    {
        foreach (OwnedSlots owned in _ownedSlots)
        {
            if (owned != null && owned.gearSlot == gearSlot)
            {
                return owned.spineSlots;
            }
        }
        return Array.Empty<string>();
    }

    void BuildResourceLookup()
    {
        _byResource = new Dictionary<ResourceType, GearItem>();
        foreach (GearItem item in _items)
        {
            if (item == null)
            {
                Debug.LogWarning("[GearCatalog] an empty entry in the items list, skipped");
                continue;
            }

            if (!item.TryGetResource(out ResourceType resource))
            {
                continue;
            }

            if (_byResource.TryGetValue(resource, out GearItem first))
            {
                Debug.LogWarning($"[GearCatalog] '{item.name}' and '{first.name}' are both granted by {resource}: " +
                                 $"'{first.name}' wins");
                continue;
            }

            _byResource.Add(resource, item);
        }
    }

    //an edit during Play takes effect on the next item gained
    void OnValidate()
    {
        _byResource = null;
    }
}
