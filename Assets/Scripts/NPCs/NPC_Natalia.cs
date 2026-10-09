using UnityEngine;

/// <summary>
/// Natalia, Level 2's companion NPC (spec 006, FR-001).
///
/// Follow/idle behaviour lives entirely in the shared NPC base + NPC_IdleState /
/// NPC_FollowPlayerState, which Abuela now uses too (the duplicated Abuela_*State pair was
/// deleted 2026-09-22). This class only owns the one thing that is actually Natalia-specific:
/// resolving her Player reference at runtime so the prefab stays drag-and-drop.
/// </summary>
public class NPC_Natalia : NPC
{
    protected override void Start()
    {
        base.Start();

        //Resolving the Player here (not in the prefab) is what makes Natalia drag-and-drop:
        //a prefab cannot hold a scene reference, and LevelManager already owns the scene's Player.
        if (player == null && LevelManager.Instance != null)
        {
            player = LevelManager.Instance.player;
        }

        if (player == null)
        {
            Debug.LogWarning($"[NPC_Natalia] {gameObject.name}: no Player reference (neither in the Inspector nor on LevelManager). She will never be able to follow Kami.");
        }

        //she starts angry on page 1 (her SpineCharacter's initial rest pose) until her opening dialogue; a
        //test started on a later page skips that dialogue, so she would stay angry for the whole test
        if (Character != null && PageScrollerManager.Instance != null && PageScrollerManager.Instance.startingPage > 1)
        {
            Character.ClearRestPose();
        }

        //since spec 014 she is a Spine character: facing and animation go through SpineCharacter
        if (_sr == null && Character == null)
        {
            Debug.LogWarning($"[NPC_Natalia] {gameObject.name}: neither a SpriteRenderer (_sr) nor a SpineCharacter: she can't face where she walks.");
        }
    }
}
