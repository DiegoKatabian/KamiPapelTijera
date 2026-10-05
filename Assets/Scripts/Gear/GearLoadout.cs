using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What Kami wears when a level starts (spec 011 FR-004). Each level's Kami points at one through
/// Player's starting loadout field.
/// </summary>
[CreateAssetMenu(fileName = "LevelN_StartLoadout", menuName = "Kami/Gear Loadout")]
public class GearLoadout : ScriptableObject
{
    [Tooltip("Equipped at level start: one item per gear slot, and always an outfit. If two items " +
             "share a slot, the last one wins (with a warning).")]
    [SerializeField] List<GearItem> _equipped = new List<GearItem>();

    [Tooltip("Owned at level start but not worn: they are in the bag and the Wardrobe from the first " +
             "frame. Everything in both lists is granted quietly (no sticker), except scissors: Kami " +
             "holds those through Player's 'Start With Tijera'.")]
    [SerializeField] List<GearItem> _alsoOwned = new List<GearItem>();

    public IReadOnlyList<GearItem> Equipped => _equipped;
    public IReadOnlyList<GearItem> AlsoOwned => _alsoOwned;

    /// <summary>Everything Kami owns at level start: the worn items, then the also-owned ones.</summary>
    public IEnumerable<GearItem> AllOwned()
    {
        foreach (GearItem item in _equipped)
        {
            yield return item;
        }
        foreach (GearItem item in _alsoOwned)
        {
            yield return item;
        }
    }
}
