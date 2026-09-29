using System.Collections;
using UnityEngine;

/// <summary>
/// Level 2 page 4: the Abuela drops out of the sky next to Kami's cell, the cell fence is gone
/// (a reveal: its GameObjects are switched off, nothing is really destroyed), she says a line and
/// starts following Kami.
///
/// PoliceStationPage decides WHEN (Schedule) and puts everything back after a capture (ResetEntrance):
/// this only knows how to perform the entrance. Kami is locked only for the fall itself and the
/// line; the wait before it is free time in a closed cell.
/// </summary>
public class AbuelaEntrance : MonoBehaviour
{
    [SerializeField, Tooltip("The Abuela (the Abuela_Follower prefab instance). Starts inactive; she appears when she falls.")]
    NPC _abuela;

    [SerializeField, Tooltip("Where she lands: just outside the cell fence, ON the NavMesh, so she can walk after Kami right away.")]
    Transform _landingPoint;

    [SerializeField, Tooltip("How high above the landing point she starts falling, in world units.")]
    float _dropHeight = 45f;

    [SerializeField, Tooltip("Seconds the fall takes.")]
    float _fallSeconds = 0.9f;

    [SerializeField, Tooltip("Everything that disappears on impact: Kami's cell fence.")]
    GameObject[] _hideOnImpact;

    [SerializeField, Tooltip("Particles played at the landing point on impact (optional).")]
    ParticleSystem _impactParticles;

    //new field names on purpose: the scene still carries the old serialized _impactSound
    //(RockSmash), which would have overridden these defaults
    [SerializeField, Tooltip("Falling whistle, played as she starts to drop. Leave empty for none.")]
    string _fallSound = AudioId.AbuelaFall;

    [SerializeField, Tooltip("Sound played on impact. Leave empty for none.")]
    string _crashSound = AudioId.AbuelaCrash;

    [SerializeField, Tooltip("Rattle of the cell fence giving way, played on impact. Leave empty for none.")]
    string _fenceSound = AudioId.FenceRattle;

    [SerializeField, Tooltip("What she says after landing (optional).")]
    DialogueSO _landingLine;

    Coroutine _running;
    bool _lockedKami;

    public bool HasLanded { get; private set; }
    public NPC Abuela => _abuela;

    void Awake()
    {
        if (_abuela == null || _landingPoint == null)
        {
            Debug.LogWarning($"[AbuelaEntrance] {gameObject.name}: _abuela or _landingPoint is not assigned, the Abuela will never come");
        }
    }

    /// <summary>Starts the countdown to her fall, cancelling any countdown already running.</summary>
    public void Schedule(float delaySeconds)
    {
        if (_running != null)
        {
            StopCoroutine(_running);
        }
        _running = StartCoroutine(EntranceRoutine(delaySeconds));
        Debug.Log($"[AbuelaEntrance] the Abuela falls in {delaySeconds}s");
    }

    /// <summary>Back to before she came: she is gone, the fence is closed again.</summary>
    public void ResetEntrance()
    {
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }

        //a reset in the middle of the fall must not leave Kami locked
        SetKamiLocked(false);

        HasLanded = false;

        if (_abuela != null)
        {
            _abuela.StopFollowingPlayer();
            _abuela.gameObject.SetActive(false);
        }

        SetImpactObjectsActive(true);
    }

    IEnumerator EntranceRoutine(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (_abuela == null || _landingPoint == null)
        {
            _running = null;
            yield break;
        }

        //wait for a calm moment: never drop her in the middle of a dialogue, overlay or page turn
        while (LevelManager.Instance == null || LevelManager.Instance.inDialogue || LevelManager.Instance.inCutscene)
        {
            yield return null;
        }

        SetKamiLocked(true);
        Debug.Log("[AbuelaEntrance] the Abuela falls");

        _abuela.gameObject.SetActive(true);
        PlaySound(_fallSound);
        if (_abuela.navAgent != null)
        {
            _abuela.navAgent.enabled = false; //an enabled agent snaps her back to the NavMesh every frame
        }

        //the landing point is on the floor, but her transform sits at the sprite's pivot (the waist):
        //end the fall with her FEET on the floor, where the agent will put her anyway
        float pivotAboveFeet = _abuela.navAgent != null ? _abuela.navAgent.baseOffset * _abuela.transform.lossyScale.y : 0f;
        Vector3 landing = _landingPoint.position + Vector3.up * pivotAboveFeet;
        Vector3 start = landing + Vector3.up * _dropHeight;
        float elapsed = 0f;
        while (elapsed < _fallSeconds)
        {
            float t = elapsed / Mathf.Max(0.01f, _fallSeconds);
            _abuela.transform.position = Vector3.Lerp(start, landing, t * t); //t*t: accelerates like a real fall
            elapsed += Time.deltaTime;
            yield return null;
        }
        _abuela.transform.position = landing;

        Impact();

        if (_abuela.navAgent != null)
        {
            _abuela.navAgent.enabled = true;
        }
        _abuela.WarpTo(_landingPoint.position);

        if (_landingLine != null)
        {
            while (DialogueManager.Instance == null || !DialogueManager.Instance.CanShowDialogueNow)
            {
                yield return null;
            }
            DialogueManager.Instance.ShowDialogue(_landingLine);
            yield return null;

            //the line is over when the dialogue manager is free again
            while (!DialogueManager.Instance.CanShowDialogueNow)
            {
                yield return null;
            }
        }

        SetKamiLocked(false);

        //a prefab cannot hold a scene reference to Kami, and the Abuela's base prefab comes from Level 1
        if (_abuela.player == null)
        {
            _abuela.player = LevelManager.Instance.player;
        }
        _abuela.StartFollowingPlayer();
        HasLanded = true;
        _running = null;
    }

    void Impact()
    {
        Debug.Log("[AbuelaEntrance] impact: the cell fence is open");
        SetImpactObjectsActive(false);

        if (_impactParticles != null)
        {
            _impactParticles.transform.position = _landingPoint.position;
            _impactParticles.Play(true);
        }

        PlaySound(_crashSound);
        PlaySound(_fenceSound);
    }

    static void PlaySound(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }
        if (AudioManager.instance == null)
        {
            Debug.LogWarning($"[AbuelaEntrance] no AudioManager, '{id}' skipped");
            return;
        }
        AudioManager.instance.Play(id);
    }

    //only ever releases a lock this component took, so it can't cut another cutscene short
    void SetKamiLocked(bool locked)
    {
        if (LevelManager.Instance == null || locked == _lockedKami)
        {
            return;
        }

        _lockedKami = locked;
        LevelManager.Instance.inCutscene = locked;
    }

    void SetImpactObjectsActive(bool active)
    {
        if (_hideOnImpact == null)
        {
            return;
        }

        foreach (GameObject target in _hideOnImpact)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
