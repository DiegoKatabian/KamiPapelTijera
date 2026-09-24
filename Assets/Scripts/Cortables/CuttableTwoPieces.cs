using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A cuttable that splits sideways into two halves that BOTH fly out, instead of leaving a
/// stump behind like a bush does: police tape, a gift ribbon, the catapult rope. Reuses
/// ObjetoCortable's whole split/arc/shrink flow and only adds the second flying half.
///
/// Field mapping onto the inherited Inspector slots, so this looks like every other cuttable
/// in the project: spriteEntero = the uncut strip, spriteBase = the LEFT half, spriteTop = the
/// RIGHT half. The right half is thrown by the base class; the left half mirrors it here.
///
/// onCut exists because the things these are tied to (the gift box opening, the catapult
/// launch) are separate later tasks — wiring the listener in the Inspector keeps this
/// component from needing to know about them at all.
/// </summary>
public class CuttableTwoPieces : ObjetoCortable
{
    [Tooltip("Fired once when cut. Wire this to whatever should happen next (gift box opening, catapult launch). Leave empty for a strip that only needs to get out of the way.")]
    [SerializeField] private UnityEvent onCut;

    //for listeners wired from code (CrimeSceneGate) instead of the Inspector: a scene object
    //cannot point a serialized UnityEvent at a component nested inside another prefab instance
    public UnityEvent OnCut => onCut;

    private Vector3 _leftPieceStartPosition;
    private Vector3 _leftPieceStartScale;

    protected override void Start()
    {
        base.Start();

        if (spriteBase == null)
        {
            Debug.LogWarning($"[CuttableTwoPieces] {gameObject.name}: spriteBase (the LEFT half) is not assigned, only the right half will fly out");
            return;
        }

        _leftPieceStartPosition = spriteBase.transform.localPosition;
        _leftPieceStartScale = spriteBase.transform.localScale;
    }

    protected override void ApplyCut()
    {
        base.ApplyCut();
        Debug.Log($"[CuttableTwoPieces] Cut: {gameObject.name}");

        if (spriteBase != null)
        {
            StartCoroutine(MoveLeftPieceInArc());

            if (doesShrink)
            {
                StartCoroutine(ShrinkLeftPiece());
            }
        }

        onCut.Invoke();
    }

    //mirror of ObjetoCortable.MoverEnTiroOblicuo: same arc, thrown the other way
    private IEnumerator MoveLeftPieceInArc()
    {
        float elapsed = 0f;
        float flightTime = (2 * velocidadInicial * Mathf.Sin(anguloLanzamiento * Mathf.Deg2Rad)) / gravedad;

        while (elapsed < flightTime)
        {
            float horizontal = (velocidadInicial * Mathf.Cos(anguloLanzamiento * Mathf.Deg2Rad)) * elapsed;
            float x = _leftPieceStartPosition.x - horizontal;
            float y = alturaInicial + _leftPieceStartPosition.y + (velocidadInicial * Mathf.Sin(anguloLanzamiento * Mathf.Deg2Rad)) * elapsed - (0.5f * gravedad * elapsed * elapsed);
            float z = _leftPieceStartPosition.z - horizontal;

            spriteBase.transform.localPosition = new Vector3(x, y, -z);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ShrinkLeftPiece()
    {
        float elapsed = 0f;

        while (elapsed < selfDestructTime)
        {
            spriteBase.transform.localScale = Vector3.Lerp(_leftPieceStartScale, Vector3.zero, elapsed / selfDestructTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
