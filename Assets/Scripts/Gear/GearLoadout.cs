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

    [Tooltip("Owned at level start but not worn, for the Wardrobe (spec 011 Phase 3). Nothing reads " +
             "this before that phase.")]
    [SerializeField] List<GearItem> _alsoOwned = new List<GearItem>();

    public IReadOnlyList<GearItem> Equipped => _equipped;
    public IReadOnlyList<GearItem> AlsoOwned => _alsoOwned;
}
