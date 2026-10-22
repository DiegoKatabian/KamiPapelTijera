using UnityEngine;

/// <summary>
/// On the root of a page-turn sheet prefab (HojaMaster Newspaper / HojaMaster_Rev Newspaper): which of its two
/// skinned meshes faces up when the turn starts and which when it ends, so BookPageSpread can print the right
/// page on each. A sheet without a BookPageSpread in the scene keeps its material as authored.
/// </summary>
public class PageTurnSheet : MonoBehaviour
{
    [Tooltip("True on the sheet that turns to the next page (lifts from the right half), false on the one that turns back.")]
    [SerializeField] bool _turnsForward = true;
    [Tooltip("The face seen when the turn starts, lying on the half the sheet lifts from.")]
    [SerializeField] Renderer _leavingFace;
    [Tooltip("The face seen when the turn ends, lying on the half the sheet lands on.")]
    [SerializeField] Renderer _arrivingFace;

    public bool TurnsForward => _turnsForward;
    public Renderer LeavingFace => _leavingFace;
    public Renderer ArrivingFace => _arrivingFace;

    // Awake, not Start: the sheet is created mid-frame by a coroutine and is drawn that same frame,
    // before a Start would run.
    void Awake()
    {
        if (_leavingFace == null || _arrivingFace == null)
        {
            Debug.LogWarning($"[PageTurnSheet] '{name}' is missing a face renderer: the sheet keeps its material");
            return;
        }
        if (BookPageSpread.Instance != null)
        {
            BookPageSpread.Instance.DressSheet(this);
        }
    }
}
