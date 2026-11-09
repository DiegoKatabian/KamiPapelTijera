using UnityEngine;

/// <summary>
/// Overrides how the paper page turn treats this object and everything under it (spec 025 D27, D29). Without one, the
/// turn classifies each piece by itself (PaperPieces.Classify) and logs the result once; add this component only where
/// the automatic choice looks wrong. **Kami > Pages > Tag Page Pieces** adds them for a whole page folder.
/// </summary>
public class PagePiece : MonoBehaviour
{
    public enum PieceClass
    {
        /// <summary>Let the turn decide from the renderers' kind and bounds.</summary>
        Auto = 0,
        /// <summary>Thin side facing the camera: falls backwards on its base line.</summary>
        Card = 1,
        /// <summary>A 3D prop: tips outward on its outer base edge, squashes at the end.</summary>
        Volume = 2,
        /// <summary>Rests on another piece (_host) and moves with it, never folds by itself.</summary>
        Rider = 3,
        /// <summary>Lies on the paper (roads, river): bends with the sheet, never folds.</summary>
        Flat = 4,
        /// <summary>Left out of the turn: stays visible and untouched (and may cut through the sheet).</summary>
        Excluded = 5,
        /// <summary>Hidden during the turn, not copied.</summary>
        Hidden = 6,
    }

    [Tooltip("How this object folds during a page turn. Auto = classified from its renderers.")]
    public PieceClass pieceClass = PieceClass.Auto;
    [Tooltip("Rider only: the piece it rests on. Empty = the piece under it is found automatically.")]
    public PagePiece host;
    [Tooltip("Turn off to leave it out of the turn while testing (bisecting a bad fold), without losing the class above.")]
    public bool includeInTurn = true;
}
