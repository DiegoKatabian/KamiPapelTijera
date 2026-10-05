using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// What Kami is wearing: at most one GearItem per gear slot (spec 011). Plain C# owned by Player,
/// like PlayerModel: it keeps the state and says when it changed. Turning that into a Spine skin is
/// PlayerView's job.
/// </summary>
public class PlayerGear
{
    readonly GearItem[] _equipped = new GearItem[Enum.GetValues(typeof(GearSlot)).Length];
    readonly Action _onChanged;

    public PlayerGear(Action onChanged)
    {
        _onChanged = onChanged;
    }

    public GearItem GetEquipped(GearSlot slot) => _equipped[(int)slot];

    /// <summary>Effects follow what she wears, not what she owns (spec 011 FR-103).</summary>
    public bool SavesFromDrowning
    {
        get
        {
            foreach (GearItem item in _equipped)
            {
                if (item != null && item.SavesFromDrowning)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Outfit first, then Scissors, Feet, Hat. Empty slots are skipped.</summary>
    public IEnumerable<GearItem> EquippedInCompositionOrder()
    {
        foreach (GearItem item in _equipped)
        {
            if (item != null)
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Replaces everything with the level's starting loadout. Always reports a change, even for a
    /// missing or empty loadout, so Kami's first skin is composed no matter what.
    /// </summary>
    public void ApplyLoadout(GearLoadout loadout)
    {
        Array.Clear(_equipped, 0, _equipped.Length);

        if (loadout == null)
        {
            Debug.LogWarning("[PlayerGear] no starting loadout: Kami starts with no outfit and no gear (default skin only)");
        }
        else
        {
            foreach (GearItem item in loadout.Equipped)
            {
                if (item == null)
                {
                    Debug.LogWarning($"[PlayerGear] '{loadout.name}' has an empty entry, skipped");
                    continue;
                }

                GearItem previous = _equipped[(int)item.Slot];
                if (previous != null)
                {
                    Debug.LogWarning($"[PlayerGear] '{loadout.name}' puts both '{previous.name}' and '{item.name}' " +
                                     $"in {item.Slot}: '{item.name}' wins");
                }
                _equipped[(int)item.Slot] = item;
            }

            if (_equipped[(int)GearSlot.Outfit] == null)
            {
                Debug.LogWarning($"[PlayerGear] '{loadout.name}' has no outfit: Kami wears only the default skin underneath");
            }
        }

        Debug.Log($"[PlayerGear] starting loadout '{(loadout != null ? loadout.name : "none")}': {Describe()}");
        _onChanged?.Invoke();
    }

    /// <summary>
    /// Puts the item in its slot, replacing what was there (newest wins). Equipping what is already
    /// on does nothing and reports no change: Level 2 equips the normal scissors twice (loadout,
    /// then the starting scissors pickup).
    /// </summary>
    public void Equip(GearItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("[PlayerGear] Equip: null item, ignored");
            return;
        }

        int index = (int)item.Slot;
        GearItem previous = _equipped[index];
        if (previous == item)
        {
            Debug.Log($"[PlayerGear] '{item.name}' is already equipped, nothing to change");
            return;
        }

        _equipped[index] = item;
        Debug.Log($"[PlayerGear] equipped '{item.name}' in {item.Slot} (was {(previous != null ? previous.name : "empty")})");
        _onChanged?.Invoke();
    }

    /// <summary>Empties a slot so the outfit's own version shows. The outfit can only be swapped.</summary>
    public void Unequip(GearSlot slot)
    {
        if (slot == GearSlot.Outfit)
        {
            Debug.LogWarning("[PlayerGear] Unequip: Kami always wears an outfit, equip another one instead");
            return;
        }

        GearItem previous = _equipped[(int)slot];
        if (previous == null)
        {
            return;
        }

        _equipped[(int)slot] = null;
        Debug.Log($"[PlayerGear] unequipped '{previous.name}' from {slot}");
        _onChanged?.Invoke();
    }

    string Describe()
    {
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < _equipped.Length; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }
            text.Append((GearSlot)i).Append('=').Append(_equipped[i] != null ? _equipped[i].name : "empty");
        }
        return text.ToString();
    }
}
