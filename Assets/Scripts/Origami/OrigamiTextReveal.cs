using UnityEngine;

/// <summary>
/// Origami whose Apply() reveals localized text (a letter, a ticket) instead of spawning a
/// GameObject (OrigamiObjectSpawner/OrigamiShip) or only firing an event (OrigamiEventTriggerer).
/// Extends OrigamiEventTriggerer rather than Origami directly, so a route built with this class
/// still notifies a page-specific listener via the inherited eventoParaTrigerear -- same as
/// "OrigamiRoute AbuelaFold"/"OrigamiRoute AbuelaUnfold" firing OnAbuelaFold/OnAbuelaUnfold -- on
/// top of showing the folded/unfolded object's text.
///
/// This is the additive extension specs/006-nivel2-detective-natalia/spec.md FR-005 asks for:
/// investigation confirmed the existing Origami/sello system (Origami.cs, MultipleRectCheck.cs,
/// PedestalCanvasDisplay.cs) has no way to show text on completion, and no "play a route
/// backwards" concept -- every existing route (including AbuelaFold/AbuelaUnfold) plays its
/// origamiRoutes forward from index 0 and only differs in what Apply() does at the end. Adding a
/// text-reveal capability therefore means a new Origami subclass, not a change to the shared base.
///
/// Kept generic on purpose -- no cafe/letter-specific code here -- so both the trap letter
/// (folded on arrival, unfolded to read) and the cafe ticket (folded in page 1, a separate
/// unfold-only route later reveals its text) can use it, each as its own OrigamiRoute prefab
/// pointing at its own or a shared OrigamiTextRevealDisplay.
/// </summary>
public class OrigamiTextReveal : OrigamiEventTriggerer
{
    [Tooltip("Localization key resolved against _textDisplay's table (e.g. Level2_Dialogues) when this fold/unfold completes.")]
    [SerializeField] string _revealedTextKey;

    [Tooltip("Panel that fades in the revealed text. Optional: if missing, the event still fires but nothing is shown (a warning is logged).")]
    [SerializeField] OrigamiTextRevealDisplay _textDisplay;

    public override void Apply()
    {
        base.Apply();
        Debug.Log($"[OrigamiTextReveal] {gameObject.name}: fired {eventoParaTrigerear}, revealing key '{_revealedTextKey}'");

        if (_textDisplay == null)
        {
            Debug.LogWarning($"[OrigamiTextReveal] {gameObject.name}: _textDisplay is not assigned, the fold completed but no text will be shown");
            return;
        }

        _textDisplay.ShowText(_revealedTextKey);
    }
}
