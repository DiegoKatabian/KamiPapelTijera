using UnityEngine;

/// <summary>
/// A bench Kami can sit on (Atlas 15's Idle to Sit / Sit Tijera / Sit to Idle). Put this trigger box around
/// the bench: Interact inside it sits her down, Interact again (or moving, or jumping) stands her up.
/// Prefab: Prefabs/Interactables/BenchSeat.prefab.
/// </summary>
public class TriggerBench : TriggerScript
{
    public enum Facing
    {
        KeepCurrent,
        Right,
        Left
    }

    public override bool EsInteractuable => true;

    [SerializeField, Tooltip("Where she sits: only its X/Z are used, her feet stay on the floor and the animation lowers her onto the bench. Empty = she sits where she stands.")]
    Transform _seat;

    [SerializeField, Tooltip("Which way she faces once seated.")]
    Facing _facing = Facing.KeepCurrent;

    public override void Interact(params object[] parameter)
    {
        if (!triggerBool)
        {
            return;
        }

        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null || levelManager.inDialogue || levelManager.inCutscene)
        {
            return; //the press that advances a line must not also sit her down
        }

        Player kami = levelManager.player;
        if (kami == null)
        {
            Debug.LogWarning($"[TriggerBench] {gameObject.name}: LevelManager has no player, can't sit");
            return;
        }

        if (kami.IsSitting)
        {
            kami.StandUp();
            return;
        }

        bool? faceRight = _facing == Facing.KeepCurrent ? (bool?)null : _facing == Facing.Right;
        if (kami.SitDown(_seat, faceRight))
        {
            Debug.Log($"[TriggerBench] {gameObject.name}: Kami sits down");
        }
    }
}
