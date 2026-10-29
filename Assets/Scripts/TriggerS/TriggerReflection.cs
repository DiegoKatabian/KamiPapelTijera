using UnityEngine;

/// <summary>
/// The zone in front of a reflecting pool: Kami standing still inside it for kamiAnims.lookIntoReflectionDelay
/// seconds (Player inspector) loops Atlas 15's "Look into reflection" until she moves. Nothing to press.
/// </summary>
public class TriggerReflection : TriggerScript
{
    bool _counted; //exits come from Unity, from ExitAll after a teleport and from OnDisable: count each stay once

    public override void OnEnterBehaviour(Collider other)
    {
        base.OnEnterBehaviour(other);

        Player kami = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (kami == null)
        {
            Debug.LogWarning($"[TriggerReflection] {gameObject.name}: LevelManager has no player");
            return;
        }

        if (!_counted)
        {
            _counted = true;
            kami.EnterReflectionZone();
        }
    }

    public override void OnExitBehaviour()
    {
        base.OnExitBehaviour();

        if (!_counted)
        {
            return;
        }

        _counted = false;
        Player kami = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (kami != null)
        {
            kami.ExitReflectionZone();
        }
    }
}
