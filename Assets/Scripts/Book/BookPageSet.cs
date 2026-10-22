using UnityEngine;

/// <summary>
/// The pictures printed on a book whose every page is different (Level 2's newspaper), one entry per page of
/// the level. A turning sheet is a real sheet of the book: going forward from page i it carries page i's right
/// half on the face that leaves and page i+1's left half on the face that lands, and the other way round going
/// back. The sheet textures are separate from the book's because each face is warped to land exactly on its
/// half (tools/make-level2-newspaper-pages.py writes this asset).
/// </summary>
[CreateAssetMenu(menuName = "Kami/Book Page Set")]
public class BookPageSet : ScriptableObject
{
    [Tooltip("Per page of the level: what the book's left half shows.")]
    [SerializeField] Texture2D[] _left;
    [Tooltip("Per page of the level: what the book's right half shows.")]
    [SerializeField] Texture2D[] _right;
    [Tooltip("Turning forward FROM page i: the sheet face lying on the right half when the turn starts.")]
    [SerializeField] Texture2D[] _forwardLeaving;
    [Tooltip("Turning forward FROM page i: the sheet face that lands on the left half.")]
    [SerializeField] Texture2D[] _forwardArriving;
    [Tooltip("Turning back FROM page i: the sheet face lying on the left half when the turn starts.")]
    [SerializeField] Texture2D[] _backwardLeaving;
    [Tooltip("Turning back FROM page i: the sheet face that lands on the right half.")]
    [SerializeField] Texture2D[] _backwardArriving;

    public Texture2D Left(int page) => At(_left, page);
    public Texture2D Right(int page) => At(_right, page);
    public Texture2D Leaving(int fromPage, bool forward) => At(forward ? _forwardLeaving : _backwardLeaving, fromPage);
    public Texture2D Arriving(int fromPage, bool forward) => At(forward ? _forwardArriving : _backwardArriving, fromPage);

    static Texture2D At(Texture2D[] list, int index)
    {
        if (list == null || index < 0 || index >= list.Length)
        {
            return null;
        }
        return list[index];
    }
}
