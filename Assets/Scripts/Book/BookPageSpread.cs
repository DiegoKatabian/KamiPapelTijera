using UnityEngine;

/// <summary>
/// Shows the current page's two halves on the open book, and dresses each page-turn sheet as it is created
/// (PageTurnSheet calls DressSheet from its Awake, inside PageScrollerManager.CreateHoja).
///
/// Order of a turn, so nothing pops: when the sheet appears it covers the half it lifts from, which switches
/// right then to the new page (the sheet uncovers it as it rises). The half it lands on switches on
/// OnPageFinishTurning, the frame PageScrollerManager destroys the sheet, so the landed face is replaced by
/// the same picture underneath.
///
/// Textures go in through MaterialPropertyBlocks: the book's materials are assets, and writing their
/// texture in Play mode would change the asset itself.
/// </summary>
public class BookPageSpread : MonoBehaviour
{
    public static BookPageSpread Instance { get; private set; }

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [Tooltip("The open book's renderer (Diario).")]
    [SerializeField] Renderer _book;
    [Tooltip("Material slot of the book's left half (Diario: 2, HojaB_PAR).")]
    [SerializeField] int _leftSlot = 2;
    [Tooltip("Material slot of the book's right half (Diario: 1, HojaA_IMPAR).")]
    [SerializeField] int _rightSlot = 1;
    [Tooltip("The pictures of every page and sheet face.")]
    [SerializeField] BookPageSet _pages;

    MaterialPropertyBlock _block;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[BookPageSpread] a second BookPageSpread on '{name}' is ignored");
            return;
        }
        Instance = this;
        _block = new MaterialPropertyBlock();
        EventManager.Subscribe(Evento.OnPageFinishTurning, OnPageFinishTurning);
    }

    void Start()
    {
        if (_book == null || _pages == null)
        {
            Debug.LogWarning($"[BookPageSpread] book renderer or page set not assigned on '{name}': the book keeps its materials' own textures");
            return;
        }
        ShowPage(CurrentPage());
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            EventManager.Unsubscribe(Evento.OnPageFinishTurning, OnPageFinishTurning);
        }
    }

    /// <summary>
    /// Puts the right picture on both faces of a sheet that was just created, and switches the half it is about to
    /// uncover. The page index has already moved to the destination when the sheet is created.
    /// </summary>
    public void DressSheet(PageTurnSheet sheet)
    {
        if (_pages == null || PageScrollerManager.Instance == null)
        {
            Debug.LogWarning("[BookPageSpread] DressSheet: no page set or no PageScrollerManager, the sheet keeps its material");
            return;
        }

        int to = CurrentPage();
        int from = sheet.TurnsForward ? to - 1 : to + 1;
        Debug.Log($"[BookPageSpread] dressing the {(sheet.TurnsForward ? "forward" : "backward")} sheet, page {from + 1} -> {to + 1}");

        SetTexture(sheet.LeavingFace, -1, _pages.Leaving(from, sheet.TurnsForward), "leaving face");
        SetTexture(sheet.ArrivingFace, -1, _pages.Arriving(from, sheet.TurnsForward), "arriving face");

        if (sheet.TurnsForward)
        {
            SetTexture(_book, _rightSlot, _pages.Right(to), $"right half of page {to + 1}");
        }
        else
        {
            SetTexture(_book, _leftSlot, _pages.Left(to), $"left half of page {to + 1}");
        }
    }

    void OnPageFinishTurning(params object[] parameters)
    {
        ShowPage(CurrentPage());
    }

    void ShowPage(int page)
    {
        if (_book == null || _pages == null)
        {
            return;
        }
        SetTexture(_book, _leftSlot, _pages.Left(page), $"left half of page {page + 1}");
        SetTexture(_book, _rightSlot, _pages.Right(page), $"right half of page {page + 1}");
    }

    int CurrentPage()
    {
        return PageScrollerManager.Instance != null ? PageScrollerManager.Instance.activePageIndex : 0;
    }

    /// <summary>slot -1 = the renderer's only material.</summary>
    void SetTexture(Renderer target, int slot, Texture2D texture, string what)
    {
        if (target == null)
        {
            Debug.LogWarning($"[BookPageSpread] no renderer for the {what}");
            return;
        }
        if (texture == null)
        {
            Debug.LogWarning($"[BookPageSpread] no texture for the {what} in '{(_pages != null ? _pages.name : "?")}': it keeps its material's own");
            return;
        }

        if (slot < 0)
        {
            target.GetPropertyBlock(_block);
            _block.SetTexture(BaseMapId, texture);
            target.SetPropertyBlock(_block);
        }
        else
        {
            target.GetPropertyBlock(_block, slot);
            _block.SetTexture(BaseMapId, texture);
            target.SetPropertyBlock(_block, slot);
        }
    }
}
