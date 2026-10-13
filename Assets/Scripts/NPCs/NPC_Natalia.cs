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
    [SerializeField, Tooltip("Test starts on pages 2-3: how far beside Kami she appears, in world units.")]
    float _testStartSideOffset = 3.5f;

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

    /// <summary>
    /// A test started on page 2-4 (#170): she is already Kami's companion, as if page 1 was played.
    /// Called by the started page's owner, because she lives under page 1's folder, which is off on
    /// such a test, so none of her own scripts run until she is moved out of it.
    /// followKami: she appears beside Kami and follows her (pages 2-3); false = only brought out of
    /// page 1 and left idle for the page to place her (page 4's cell).
    /// </summary>
    public void SetUpForTestStart(int page, bool followKami)
    {
        Player kami = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (kami == null)
        {
            Debug.LogWarning($"[NPC_Natalia] {gameObject.name}: no Player, cannot set her up for the page {page} test start");
            return;
        }

        Debug.Log($"[NPC_Natalia] test started on page {page}: she joins Kami as if page 1 was played");
        player = kami;

        NataliaDialogueTrigger talk = GetComponentInChildren<NataliaDialogueTrigger>(true);
        if (talk != null)
        {
            //Quest05 (find the clues) only belongs to page 2: by page 3 it was done
            talk.SkipToJoined(addFindCluesQuest: page == 2);
        }
        else
        {
            Debug.LogWarning($"[NPC_Natalia] {gameObject.name}: no NataliaDialogueTrigger among her children, her page 1 lines stay on");
        }

        if (!followKami)
        {
            //out of page 1's folder, next to where a follow would have put her (Kami's parent)
            transform.SetParent(kami.transform.parent, true);
            return;
        }

        //follow first: it reparents her out of page 1's folder, and an inactive agent can't warp
        StartFollowingPlayer();
        WarpTo(kami.transform.position - kami.transform.right * _testStartSideOffset);
    }
}
