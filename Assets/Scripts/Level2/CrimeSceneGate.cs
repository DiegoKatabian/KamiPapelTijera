using System.Collections;
using UnityEngine;

/// <summary>
/// Level 2 page 2 (spec 006, Story 2): the museum's broken fence, taped off and guarded.
///
/// While guarded, the police tape cannot be cut (its trigger collider is off, so the scissors
/// never register it) and a solid blocker fills the gap. On Evento.OnCrimeSceneUnguarded
/// (fired by FindCluesTracker) the cops board the car and it drives off; the tape becomes
/// cuttable. Cutting the tape removes the blocker.
///
/// The tape is found by GetComponentInChildren instead of a serialized reference, so this
/// prefab can nest a plain CuttablePoliceTape instance without stripped-reference plumbing.
/// </summary>
public class CrimeSceneGate : MonoBehaviour
{
    [SerializeField, Tooltip("Solid collider filling the fence gap. Turned off when the tape is cut.")]
    GameObject _blocker;

    [SerializeField, Tooltip("The police car. It drives to _driveOffTarget and is destroyed there.")]
    TrafficObstacle _policeCar;

    [SerializeField, Tooltip("Where the car drives to before disappearing.")]
    Transform _driveOffTarget;

    [SerializeField, Tooltip("Seconds the car takes to reach _driveOffTarget (a duration, not a speed, so it reads the same on any street).")]
    float _driveOffSeconds = 3f;

    [SerializeField, Tooltip("Seconds between the cops 'getting in' (disappearing) and the car pulling away.")]
    float _boardingSeconds = 0.5f;

    [SerializeField, Tooltip("The cops standing guard. They disappear when they board the car.")]
    GameObject[] _cops;

    CuttableTwoPieces _tape;
    Collider _tapeCollider;
    bool _unguarded;
    bool _carLaunched;

    void Awake()
    {
        _tape = GetComponentInChildren<CuttableTwoPieces>(true);

        if (_tape == null)
        {
            Debug.LogWarning($"[CrimeSceneGate] {gameObject.name}: no CuttableTwoPieces (police tape) found in children, the gate can never open.");
            return;
        }

        _tapeCollider = _tape.GetComponent<Collider>();
        if (_tapeCollider == null)
        {
            Debug.LogWarning($"[CrimeSceneGate] {gameObject.name}: the police tape has no Collider, it cannot be gated.");
        }
        else
        {
            _tapeCollider.enabled = false;
        }

        _tape.OnCut.AddListener(OpenGap);
    }

    void Start()
    {
        if (_blocker == null)
        {
            Debug.LogWarning($"[CrimeSceneGate] {gameObject.name}: no _blocker assigned, nothing physically closes the fence gap.");
        }

        EventManager.Subscribe(Evento.OnCrimeSceneUnguarded, Unguard);
    }

    void Unguard(params object[] parameters)
    {
        if (_unguarded)
        {
            return;
        }

        _unguarded = true;
        Debug.Log($"[CrimeSceneGate] {gameObject.name}: the cops leave, the tape can be cut now");
        StartCoroutine(DriveOff());
    }

    //turning the page deactivates it and kills a running coroutine: finish leaving on return
    void OnEnable()
    {
        if (_unguarded && !_carLaunched)
        {
            StartCoroutine(DriveOff());
        }
    }

    IEnumerator DriveOff()
    {
        //the tape opens once the guards are gone, not when the car is out of sight
        if (_tapeCollider != null)
        {
            _tapeCollider.enabled = true;
        }

        foreach (GameObject cop in _cops)
        {
            if (cop != null)
            {
                cop.SetActive(false);
            }
        }

        yield return new WaitForSeconds(_boardingSeconds);

        _carLaunched = true;

        if (_policeCar != null && _driveOffTarget != null)
        {
            //the lifetime cap is only a safety net for the despawn, give it plenty of room
            _policeCar.Launch(_driveOffTarget.position, _driveOffSeconds, _driveOffSeconds * 3f + 1f);
        }
        else
        {
            Debug.LogWarning($"[CrimeSceneGate] {gameObject.name}: _policeCar or _driveOffTarget missing, the car stays parked.");
        }
    }

    void OpenGap()
    {
        Debug.Log($"[CrimeSceneGate] {gameObject.name}: tape cut, the fence gap is open");

        if (_blocker != null)
        {
            _blocker.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (_tape != null)
        {
            _tape.OnCut.RemoveListener(OpenGap);
        }

        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnCrimeSceneUnguarded, Unguard);
        }
    }
}
