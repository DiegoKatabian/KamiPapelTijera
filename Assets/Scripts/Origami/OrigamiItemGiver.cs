using UnityEngine;

/// <summary>
/// An origami that also puts an item in the inventory when its last fold is done, on top of the
/// Evento that OrigamiEventTriggerer fires. Level 2 page 1: folding the café wrapper gives Kami the
/// café ticket she unfolds on page 5.
/// </summary>
public class OrigamiItemGiver : OrigamiEventTriggerer
{
    [SerializeField, Tooltip("What Kami gets when the fold is complete.")]
    ResourceType _grantedItem = ResourceType.cafeTicket;

    [SerializeField, Tooltip("How many she gets.")]
    int _grantedAmount = 1;

    public override void Apply()
    {
        base.Apply();

        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null)
        {
            Debug.LogWarning($"[OrigamiItemGiver] {gameObject.name}: there is no LevelManager in the scene, cannot grant {_grantedAmount}x {_grantedItem}.");
            return;
        }

        //the "you got this" sticker is placed at the last OnObjectWasCut position: put it on Kami,
        //the origami itself lives on the UI canvas
        if (levelManager.player != null)
        {
            EventManager.Trigger(Evento.OnObjectWasCut, levelManager.player.transform.position);
        }

        levelManager.AddResource(_grantedItem, _grantedAmount);
        Debug.Log($"[OrigamiItemGiver] {gameObject.name}: granted {_grantedAmount}x {_grantedItem}");
    }
}
