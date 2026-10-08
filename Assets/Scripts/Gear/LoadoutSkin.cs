using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// Dresses a Kami skeleton that no Player owns (the main menu's title Kami) from a GearLoadout, with
/// the same composer Level 1 uses (spec 012 FR-010). Atlas 13's Libro 1 skin has no shoes, so naming
/// that one skin leaves her barefoot: the right look is the composed skin (outfit + base shoes, no
/// scissors). Pointing it at Level1_StartLoadout means the menu can never drift from Level 1's look.
/// </summary>
[RequireComponent(typeof(SkeletonAnimation))]
public class LoadoutSkin : MonoBehaviour
{
    [SerializeField, Tooltip("What she wears. Level1_StartLoadout = the Libro 1 outfit and her base shoes, no scissors.")]
    GearLoadout _loadout;

    void Start()
    {
        //in Start, never Awake: SkeletonAnimation builds its skeleton in its own Awake
        SkeletonAnimation animation = GetComponent<SkeletonAnimation>();
        if (animation.Skeleton == null)
        {
            animation.Initialize(false);
        }

        if (_loadout == null)
        {
            Debug.LogWarning($"[LoadoutSkin] {gameObject.name}: no loadout assigned, keeping the skeleton's initial skin");
            return;
        }

        GearCatalog catalog = GearCatalog.Instance; //warns by itself when Resources/GearCatalog.asset is missing
        if (catalog == null)
        {
            return;
        }

        //composition order is the slot order (Outfit, Scissors, Feet, Hat), the same one PlayerGear gives Kami
        List<GearItem> items = new List<GearItem>();
        foreach (GearItem item in _loadout.Equipped)
        {
            if (item != null)
            {
                items.Add(item);
            }
        }
        items.Sort((a, b) => a.Slot.CompareTo(b.Slot));

        Spine.Skeleton skeleton = animation.Skeleton;
        skeleton.SetSkin(SpineSkinComposer.Compose(skeleton.Data, items, catalog));
        skeleton.SetSlotsToSetupPose();
        Debug.Log($"[LoadoutSkin] {gameObject.name} wears '{_loadout.name}' ({items.Count} items)");
    }
}
