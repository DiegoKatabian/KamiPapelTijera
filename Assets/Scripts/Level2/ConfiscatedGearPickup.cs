using UnityEngine;

/// <summary>
/// The evidence pickup on page 4: walking into it gives back everything ConfiscatedGear holds.
/// ConfiscatedGear shows and hides this object, so a capture "re-arms" it just by showing it again.
/// Walk-into rather than A/E for the same reason as GrantResourcePickup: Natalia and the Abuela
/// follow close behind, and nothing should compete for the interact button here.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ConfiscatedGearPickup : MonoBehaviour
{
    [SerializeField, Tooltip("The ConfiscatedGear that holds what this pickup gives back.")]
    ConfiscatedGear _gear;

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != 3) //layer 3 is the player
        {
            return;
        }

        if (_gear == null)
        {
            Debug.LogWarning($"[ConfiscatedGearPickup] {gameObject.name}: no _gear assigned, nothing to give back");
            return;
        }

        //position the "you got this" stickers here, same order as every other pickup
        EventManager.Trigger(Evento.OnObjectWasCut, transform.position);
        _gear.ReturnAll();
    }
}
