using Kami.Paper;
using UnityEngine;

/// <summary>
/// One level's open book, measured for the paper page turns (spec 025): where the spine is, the shape of each half's top
/// surface, how each half's mesh UVs map onto world x/z, and how the sheet moves. Written by
/// tools/make-paper-book-assets.py from tools/measure-paper-book.py and tools/fit-paper-curl.py; the curl and the tuning
/// below can be changed here by hand (re-running the tool keeps nothing of those edits, so note them in the spec).
/// </summary>
[CreateAssetMenu(menuName = "Kami/Paper Book Data")]
public class PaperBookData : ScriptableObject
{
    [Header("Measured (tools/measure-paper-book.py)")]
    [Tooltip("World x where the two page halves meet. The spine runs along world z.")]
    public float spineX;
    [Tooltip("The pages' extent along the spine (world z).")]
    public Vector2 pageZ = new Vector2(-25, 25);
    [Tooltip("Right half's top surface: outward distance from the spine.")]
    public float[] rightOutward;
    [Tooltip("Right half's top surface: world height at each outward distance.")]
    public float[] rightHeight;
    [Tooltip("Left half's top surface: outward distance from the spine.")]
    public float[] leftOutward;
    [Tooltip("Left half's top surface: world height at each outward distance.")]
    public float[] leftHeight;
    [Tooltip("Right half: uv = (row0 . (x, z, 1), row1 . (x, z, 1)), the book mesh's own mapping.")]
    public Vector3 rightUvRow0, rightUvRow1;
    [Tooltip("Left half: uv mapping, as above.")]
    public Vector3 leftUvRow0, leftUvRow1;
    [Tooltip("Material slot of the right half on the book's renderer.")]
    public int rightSlot = 2;
    [Tooltip("Material slot of the left half on the book's renderer.")]
    public int leftSlot = 1;

    [Header("How the sheet moves (tools/fit-paper-curl.py)")]
    public CurlParams curl = new CurlParams();

    [Header("Tuning")]
    [Tooltip("+1: cards fall toward +z, away from the cameras (they all look along +z).")]
    public int cardFallSign = 1;
    [Tooltip("How far above its leaf each face of the sheet is drawn, so it never z-fights the book under it.")]
    public float sheetLift = 0.03f;
    [Tooltip("A group under a page folder wider than this (x or z) is split into its children's pieces.")]
    public float maxPieceSize = 30f;
    [Tooltip("A piece this thin vertically (relative to its footprint and in units) is Flat: it bends with the paper.")]
    public float flatThicknessRatio = 0.12f;
    public float flatMaxThickness = 1.5f;
    [Tooltip("A piece this thin along z (relative to its other sides) is a Card.")]
    public float cardThicknessRatio = 0.25f;
    [Tooltip("A piece whose bottom is this far above the page and sits on top of another piece rides it (Rider).")]
    public float riderMinLift = 1.0f;
    [Tooltip("Facing pages close on each other's pieces: each leaf counts as this share of its tallest piece thick. " +
             "0 = pieces only avoid the paper (they may pass through each other), 1 = never touch (fold very early).")]
    [Range(0f, 1f)] public float pageThicknessShare = 0.5f;

    public PaperProfile Profile(bool right)
    {
        float[] xo = right ? rightOutward : leftOutward;
        float[] y = right ? rightHeight : leftHeight;
        if (xo == null || y == null || xo.Length < 2 || xo.Length != y.Length)
        {
            Debug.LogWarning($"[PaperBookData] '{name}': the {(right ? "right" : "left")} profile is missing or broken, using a flat page");
            return new PaperProfile(new double[] { 0, 40 }, new double[] { 1, 1 });
        }
        double[] dx = new double[xo.Length], dy = new double[y.Length];
        for (int i = 0; i < xo.Length; i++)
        {
            dx[i] = xo[i];
            dy[i] = y[i];
        }
        return new PaperProfile(dx, dy);
    }

    /// <summary>The book mesh's uv at a world point of a half's top surface.</summary>
    public Vector2 UvAt(bool right, float worldX, float worldZ)
    {
        Vector3 r0 = right ? rightUvRow0 : leftUvRow0;
        Vector3 r1 = right ? rightUvRow1 : leftUvRow1;
        return new Vector2(r0.x * worldX + r0.y * worldZ + r0.z, r1.x * worldX + r1.y * worldZ + r1.z);
    }

    public int Slot(bool right) => right ? rightSlot : leftSlot;
}
