using UnityEngine;

/// <summary>
/// The little animated speech bubble above an NPC that can be talked to (GlobitoDialogo.prefab).
///
/// It shows exactly while the NPC is talkable: its talk trigger exists, is enabled and its collider is on.
/// That follows every way the project switches talking off without a shared flag: Natalia's
/// SetTalkable(false) (collider off while she follows Kami), a guard that leaves with its trigger, a trigger
/// object switched off by its page, a `_burnAfterReading` trigger destroyed after use. It also hides while the
/// game is frozen for a dialogue or a cutscene, so it never pops beside the dialogue box.
///
/// The animated art is a child switched on and off here (its Animator is on that child, so it starts over with
/// a pop every time the globe appears). The root stays free for other scripts that still toggle it directly,
/// like AbuelaDialogueTrigger.Lock/Unlock.
/// </summary>
public class DialogueGlobe : MonoBehaviour
{
    [Tooltip("The animated bubble (child with the SpriteRenderer and the Animator). Switched on and off by this component.")]
    [SerializeField] GameObject _visual;

    [Tooltip("The NPC's talk trigger. The globe shows while it is alive, enabled and its collider is on. Empty = always shown, as the old static globe was.")]
    [SerializeField] TriggerScript _talkTrigger;

    [Tooltip("Hide the globe while a dialogue or a cutscene has the game frozen for the player.")]
    [SerializeField] bool _hideWhileFrozen = true;

    Collider _triggerCollider;
    bool _visible;
    bool _hasState;

    void Awake()
    {
        if (_visual == null)
        {
            Debug.LogWarning($"[DialogueGlobe] {name}: _visual is not wired, nothing to show");
        }
    }

    void OnEnable()
    {
        //re-evaluate from scratch: whoever switched the root off and on again expects the pop to replay
        _hasState = false;
        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        if (_visual == null)
        {
            return;
        }

        bool show = ShouldShow();
        if (_hasState && show == _visible)
        {
            return;
        }

        _hasState = true;
        _visible = show;
        _visual.SetActive(show);
    }

    bool ShouldShow()
    {
        if (!TalkTriggerAllows())
        {
            return false;
        }

        if (_hideWhileFrozen && LevelManager.Instance != null)
        {
            if (LevelManager.Instance.inDialogue || LevelManager.Instance.inCutscene)
            {
                return false;
            }
        }

        return true;
    }

    bool TalkTriggerAllows()
    {
        //not wired = the old behaviour (always on). ReferenceEquals because a DESTROYED trigger also compares
        //== null, and that case means "burned after reading": no more talking, so no globe
        if (ReferenceEquals(_talkTrigger, null))
        {
            return true;
        }

        if (_talkTrigger == null)
        {
            return false;
        }

        if (!_talkTrigger.isActiveAndEnabled)
        {
            return false;
        }

        if (_triggerCollider == null)
        {
            _triggerCollider = _talkTrigger.GetComponent<Collider>();
        }

        return _triggerCollider == null || _triggerCollider.enabled;
    }
}
