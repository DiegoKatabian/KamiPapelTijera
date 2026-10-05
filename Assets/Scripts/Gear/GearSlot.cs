/// <summary>
/// Where a piece of gear goes on Kami (spec 011). The order is the composition order: each slot is
/// added on top of the ones before it, so gear always beats the outfit and changing outfit never
/// removes the boots or the scissors. Serialized as ints in the GearItem assets: append, never
/// reorder.
/// </summary>
public enum GearSlot
{
    Outfit,
    Scissors,
    Feet,
    Hat
}
