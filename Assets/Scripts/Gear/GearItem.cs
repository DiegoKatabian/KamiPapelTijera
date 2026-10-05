using Spine.Unity;
using UnityEngine;

/// <summary>
/// One piece of Kami's gear: the gear slot it goes in and the Spine skin that draws it (spec 011).
/// Adding an item to the game = this asset + its Spine skin in Kami's export + a line in
/// GearCatalog + its bag item (a ResourceType and an InventoryItem in both InventoryManager lists,
/// which is what the bag and the Wardrobe show). Getting its inventory resource equips it
/// (Player.EquipGainedGear); tapping it in the bag or the Wardrobe toggles it (Player.TryToggleGear).
/// </summary>
[CreateAssetMenu(fileName = "Gear_", menuName = "Kami/Gear Item")]
public class GearItem : ScriptableObject, IHasSkeletonDataAsset
{
    [Tooltip("Where it goes on Kami. One item per slot: equipping it replaces whatever was there.")]
    [SerializeField] GearSlot _slot;

    [Tooltip("The Spine skin that draws it. A name missing from Kami's export logs one warning and the " +
             "item is skipped, so an item can exist before its art does.")]
    [SpineSkin(fallbackToTextField: true)]
    [SerializeField] string _spineSkin;

    [Tooltip("On: the resource below is this item's bag item, and getting it equips the item. Off only " +
             "for an item no bag item grants yet (the enum's default 0 is 'hongos').")]
    [SerializeField] bool _grantedByResource = true;

    [Tooltip("The inventory resource that grants it. Owning it (count above 0) is what puts the item in " +
             "the Wardrobe. Ignored while 'Granted By Resource' is off.")]
    [SerializeField] ResourceType _resource;

    [Tooltip("ItemTable key of its name, for the Wardrobe tab (spec 011 Phase 3).")]
    [SerializeField] string _displayNameKey;

    [Header("Effects while worn")]
    [Tooltip("While Kami wears it, water doesn't drown her (the rain boots). Taken off, it protects " +
             "nothing, even if she still owns it (spec 011 FR-103).")]
    [SerializeField] bool _savesFromDrowning;

    public GearSlot Slot => _slot;
    public string SpineSkinName => _spineSkin;
    public string DisplayNameKey => _displayNameKey;
    public bool SavesFromDrowning => _savesFromDrowning;

    public bool TryGetResource(out ResourceType resource)
    {
        resource = _resource;
        return _grantedByResource;
    }

    //Only feeds the [SpineSkin] dropdown above: borrowing the catalog's skeleton means no item needs
    //its own reference to Kami's skeleton.
    SkeletonDataAsset IHasSkeletonDataAsset.SkeletonDataAsset
    {
        get
        {
            GearCatalog catalog = GearCatalog.Instance;
            return catalog != null ? catalog.SkeletonDataAsset : null;
        }
    }
}
