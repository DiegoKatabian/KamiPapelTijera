using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Drapes that split into a top and a bottom: the top stays hanging where it is and the bottom
/// drops away -- the inverse of a bush, where the stump stays and the top flies off. Covers
/// Level 2 page 4's escape window until Kami cuts it.
///
/// Field mapping onto the inherited Inspector slots, like every other cuttable: spriteEntero =
/// the uncut drapes, spriteBase = the TOP half (stays), spriteTop = the BOTTOM half (drops).
/// The inherited arc/launch settings are not used: the bottom half just falls.
///
/// _blocker is the solid collider that keeps Kami out of the window while the drapes are whole;
/// it is switched off on the cut and back on by RestoreUncut.
/// </summary>
public class CuttableDrapes : ObjetoCortable
{
    [Tooltip("Solid collider that blocks the window while the drapes are whole. Switched off when they are cut.")]
    [SerializeField] Collider _blocker;

    [Tooltip("How far the bottom half drops before it is gone, in world units.")]
    [SerializeField] float _dropDistance = 6f;

    [Tooltip("Seconds the bottom half takes to drop and shrink away.")]
    [SerializeField] float _dropSeconds = 0.8f;

    [Tooltip("Fired once when cut.")]
    [SerializeField] UnityEvent onCut = new UnityEvent();

    public UnityEvent OnCut => onCut;

    protected override void Start()
    {
        base.Start();

        if (_blocker == null)
        {
            Debug.LogWarning($"[CuttableDrapes] {gameObject.name}: no _blocker assigned, the window is open even before the drapes are cut");
        }
    }

    protected override void ApplyCut()
    {
        AudioManager.instance.Play(AudioId.TijeraHit);
        AudioManager.instance.Play(AudioId.PaperCut);
        AudioManager.instance.Play(AudioId.ClothRip);

        SepararSprites();
        StartCoroutine(DropBottomHalf());
        isCortable = false;

        if (_blocker != null)
        {
            _blocker.enabled = false;
        }

        Debug.Log($"[CuttableDrapes] Cut: {gameObject.name}");
        onCut.Invoke();
    }

    IEnumerator DropBottomHalf()
    {
        Transform bottom = spriteTop.transform;
        Vector3 start = posicionInicial;
        float elapsed = 0f;

        while (elapsed < _dropSeconds)
        {
            float t = elapsed / Mathf.Max(0.01f, _dropSeconds);
            //t*t: accelerates like a real fall; the drop is measured in world units, so undo the parent's scale
            float localDrop = _dropDistance * t * t / Mathf.Max(0.0001f, bottom.parent != null ? bottom.parent.lossyScale.y : 1f);
            bottom.localPosition = start + Vector3.down * localDrop;
            bottom.localScale = Vector3.Lerp(escalaInicialSpriteTop, Vector3.zero, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        bottom.gameObject.SetActive(false);
    }

    public override void RestoreUncut()
    {
        if (isCortable)
        {
            return;
        }

        base.RestoreUncut();

        //ReunirSprites writes the start position as a WORLD position (legacy quirk shared by every
        //cuttable); put the bottom half back where it really belongs
        spriteTop.transform.localPosition = posicionInicial;

        if (_blocker != null)
        {
            _blocker.enabled = true;
        }
    }
}
