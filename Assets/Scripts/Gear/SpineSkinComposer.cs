using System.Collections.Generic;
using Spine;
using UnityEngine;

/// <summary>
/// Builds Kami's skin from her gear (spec 011 FR-001/002/006): each item's Spine skin is added in
/// composition order (outfit, then Scissors, Feet, Hat), and a later entry with the same key
/// overwrites an earlier one, so gear beats the outfit. Whatever no item covers falls back to the
/// skeleton's `default` skin, which Spine always consults second.
/// </summary>
public static class SpineSkinComposer
{
    //every name is reported once per Play: the composer runs on every gear change, and a missing
    //skin (art not exported yet) is the expected state for a while
    static readonly HashSet<string> _reported = new HashSet<string>();
    static readonly List<Skin.SkinEntry> _entries = new List<Skin.SkinEntry>();

    /// <summary>
    /// Returns a NEW skin every call on purpose: Skeleton.SetSkin ignores a skin object it already
    /// has, so reusing one would silently never show the change.
    /// </summary>
    public static Skin Compose(SkeletonData data, IEnumerable<GearItem> itemsInOrder, GearCatalog catalog)
    {
        Skin composed = new Skin("kami-gear");

        foreach (GearItem item in itemsInOrder)
        {
            if (item == null)
            {
                continue;
            }

            Skin itemSkin = FindItemSkin(data, item);
            if (itemSkin == null)
            {
                //skip the whole item, clearing included: rubber boots whose art doesn't exist yet must
                //leave the outfit's shoes on, not a barefoot Kami
                continue;
            }

            ClearOwnedSlots(composed, data, catalog.OwnedSpineSlots(item.Slot));
            composed.AddSkin(itemSkin);
        }

        return composed;
    }

    static Skin FindItemSkin(SkeletonData data, GearItem item)
    {
        if (string.IsNullOrEmpty(item.SpineSkinName))
        {
            WarnOnce($"item:{item.name}", $"[SpineSkinComposer] '{item.name}' has no Spine skin set: skipped");
            return null;
        }

        Skin skin = data.FindSkin(item.SpineSkinName);
        if (skin == null)
        {
            WarnOnce($"skin:{item.SpineSkinName}", $"[SpineSkinComposer] Kami's export has no skin " +
                     $"'{item.SpineSkinName}' (item '{item.name}'): skipped until the art exists");
        }
        return skin;
    }

    //Removes what earlier layers (the outfit) put in the Spine slots this gear slot owns. Only the
    //composed skin is touched: an entry living in `default` can be replaced but never removed.
    static void ClearOwnedSlots(Skin composed, SkeletonData data, IReadOnlyList<string> ownedSlots)
    {
        foreach (string slotName in ownedSlots)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                continue;
            }

            SlotData slot = data.FindSlot(slotName);
            if (slot == null)
            {
                WarnOnce($"slot:{slotName}", $"[SpineSkinComposer] GearCatalog owns a Spine slot " +
                         $"'{slotName}' that Kami's skeleton doesn't have: ignored");
                continue;
            }

            //GetAttachments copies into the buffer, so removing afterwards doesn't touch the
            //dictionary while it is being iterated
            _entries.Clear();
            composed.GetAttachments(slot.Index, _entries);
            foreach (Skin.SkinEntry entry in _entries)
            {
                composed.RemoveAttachment(slot.Index, entry.Name);
            }
        }
        _entries.Clear();
    }

    static void WarnOnce(string key, string message)
    {
        if (_reported.Add(key))
        {
            Debug.LogWarning(message);
        }
    }
}
