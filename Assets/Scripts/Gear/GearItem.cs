using Spine.Unity;
using UnityEngine;

/// <summary>
/// One piece of Kami's gear: the gear slot it goes in and the Spine skin that draws it (spec 011).
/// Adding an item to the game = this asset + its Spine skin in Kami's export + a line in
/// GearCatalog. Getting its inventory resource equips it (Player.EquipGainedGear).
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

    [Tooltip("On: getting the resource below equips this item. Off for items no bag item grants yet " +
             "(the outfits, until spec 011 Phase 3).")]
    [SerializeField] bool _grantedByResource = true;

    [Tooltip("The inventory resource that grants it. Ignored while 'Granted By Resource' is off.")]
    [SerializeField] ResourceType _resource;

    [Tooltip("ItemTable key of its name, for the Wardrobe tab (spec 011 Phase 3).")]
    [SerializeField] string _displayNameKey;

    public GearSlot Slot => _slot;
    public string SpineSkinName => _spineSkin;
    public string DisplayNameKey => _displayNameKey;

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
