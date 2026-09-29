using System.Collections;
using UnityEngine;

/// <summary>
/// Level 2's ending (spec 006, 5.D): the museum's old catapult sends Kami and the Abuela back to
/// their book. Hard-animated, no physics.
///
/// 1. Locked until MuseumPage calls Unlock() (the evidence dialogue, which ends with Grace's offer, is over).
/// 2. Interact on the catapult = the confirm: a quick fade, Kami lands in the bucket (walled in by
///    _enableOnBoard so she can't wander off, but she can still attack) and the Abuela next to her.
///    Only now does the rope become cuttable, so an early swing can't waste it.
/// 3. Cutting the rope plays the timeline (CutsceneDirector). Its signals call the CUE_ methods:
///    the launch (the girls vanish, a placeholder of them arcs off-frame), the fade to black and
///    the scene change. The holds between them are the gaps between the signals: drag them in the
///    Timeline window. The flight is the only duration that lives here (_flightSeconds), the same
///    as the arrest's car drive.
/// </summary>
[RequireComponent(typeof(CutsceneDirector))]
public class Catapult : TriggerScript
{
    [Header("Boarding")]
    [SerializeField, Tooltip("Where Kami stands once she boards (her feet), inside the bucket.")]
    Transform _kamiSeat;

    [SerializeField, Tooltip("Where the Abuela sits once they board (her feet), next to Kami.")]
    Transform _abuelaSeat;

    [SerializeField, Tooltip("Turned on when Kami boards: the invisible walls around the bucket that keep her in it.")]
    GameObject[] _enableOnBoard;

    [SerializeField, Tooltip("Covers the teleport into the bucket. Optional: without it Kami just pops in.")]
    ScreenFader _fader;

    [SerializeField, Tooltip("Seconds of each half of the boarding fade (to black, then back).")]
    float _boardFadeSeconds = 0.3f;

    [SerializeField, Tooltip("Said once the girls are in the bucket (the Abuela). Optional.")]
    DialogueSO _boardedLine;

    [SerializeField, Tooltip("TooltipTable key shown once they are in the bucket.")]
    string _cutRopeTooltip = "catapult_cut_rope";

    [SerializeField, Tooltip("Post-it color for the cut-the-rope tooltip.")]
    PostItColor _cutRopeColor;

    [Header("Rope")]
    [SerializeField, Tooltip("The rope holding the arm down. Cutting it launches.")]
    CuttableTwoPieces _rope;

    [Header("Launch")]
    [SerializeField, Tooltip("Turned off at the launch: the loaded catapult art.")]
    GameObject[] _hideOnLaunch;

    [SerializeField, Tooltip("Turned on at the launch: the fired (empty) catapult art.")]
    GameObject[] _showOnLaunch;

    [SerializeField, Tooltip("A stand-in for the two girls in flight. Starts inactive; flies from the bucket to _flightTarget.")]
    Transform _flyingGirls;

    [SerializeField, Tooltip("Where the flying girls end up: somewhere off-frame of the catapult shot.")]
    Transform _flightTarget;

    [SerializeField, Tooltip("Seconds the flight takes. Short: they should leave the frame quickly.")]
    float _flightSeconds = 1.2f;

    [SerializeField, Tooltip("How high the flight arcs above the straight line, in world units.")]
    float _flightArcHeight = 20f;

    [SerializeField, Tooltip("Seconds between rope creaks while Kami waits in the bucket (random between the two values).")]
    Vector2 _ropeCreakInterval = new Vector2(3f, 6f);

    [Header("Ending")]
    [SerializeField, Tooltip("Seconds of the fade to black (starts at the FadeOut signal).")]
    float _fadeSeconds = 1.5f;

    [SerializeField, Tooltip("The scene loaded at the LoadEnding signal.")]
    GameScene _nextScene = GameScene.Level2EndCutscene;

    CutsceneDirector _cutscene;
    Collider _ropeTrigger;
    NPC _abuela;
    bool _unlocked;
    bool _boarded;
    bool _launched;

    public override bool EsInteractuable => _unlocked && !_boarded;

    void Awake()
    {
        _cutscene = GetComponent<CutsceneDirector>();
    }

    protected override void Start()
    {
        base.Start();

        if (_rope == null || _kamiSeat == null)
        {
            Debug.LogWarning($"[Catapult] {gameObject.name}: _rope or _kamiSeat is not assigned, the catapult can't launch");
        }
        else
        {
            //from code, like the gift ribbon: the rope is a separate prefab instance
            _rope.OnCut.AddListener(OnRopeCut);
            _ropeTrigger = _rope.GetComponent<Collider>();
            SetRopeCuttable(false);
        }

        SetAllActive(_enableOnBoard, false);

        if (_flyingGirls != null)
        {
            _flyingGirls.gameObject.SetActive(false);
        }
    }

    /// <summary>MuseumPage: the case is closed and Grace offered the catapult. The Abuela is the passenger.</summary>
    public void Unlock(NPC abuela)
    {
        if (_unlocked)
        {
            return;
        }

        _unlocked = true;
        _abuela = abuela;
        Debug.Log("[Catapult] unlocked, waiting for Kami to board");

        //Kami may already be standing in the trigger: register and prompt her now
        if (triggerBool)
        {
            triggerBool = true;
            TryShowTooltip();
        }
    }

    public override void OnEnterBehaviour(Collider other)
    {
        triggerBool = true;

        if (_unlocked && !_boarded)
        {
            TryShowTooltip();
        }
    }

    public override void Interact(params object[] parameter)
    {
        if (!triggerBool || !_unlocked || _boarded)
        {
            return;
        }

        //the press that closes a dialogue must not board her too
        if (LevelManager.Instance.inDialogue || LevelManager.Instance.inCutscene)
        {
            return;
        }

        StartCoroutine(Board());
    }

    IEnumerator Board()
    {
        _boarded = true;
        OnExitBehaviour(); //hides the board tooltip
        InteractionContext.Desregistrar(this); //EsInteractuable is false now, the setter no longer does it
        LevelManager.Instance.inCutscene = true;
        Debug.Log("[Catapult] Kami boards");

        if (_fader != null)
        {
            yield return _fader.FadeOut(_boardFadeSeconds);
        }

        SetAllActive(_enableOnBoard, true);

        if (PlayerPageSpawnManager.Instance != null && _kamiSeat != null)
        {
            PlayerPageSpawnManager.Instance.PositionPlayerAtPoint(_kamiSeat.position);
        }

        SeatAbuela();
        SetRopeCuttable(true);
        StartCoroutine(RopeCreakLoop());

        if (_fader != null)
        {
            yield return _fader.FadeIn(_boardFadeSeconds);
        }

        LevelManager.Instance.inCutscene = false;

        if (_boardedLine != null)
        {
            while (DialogueManager.Instance == null || !DialogueManager.Instance.CanShowDialogueNow)
            {
                yield return null;
            }
            DialogueManager.Instance.ShowDialogue(_boardedLine);
            yield return null;

            while (!DialogueManager.Instance.CanShowDialogueNow)
            {
                yield return null;
            }
        }

        if (!_launched && TooltipManager.Instance != null && !string.IsNullOrEmpty(_cutRopeTooltip))
        {
            TooltipManager.Instance.ShowTooltip(_cutRopeTooltip, _cutRopeColor, this);
        }
    }

    //the rope is under tension the whole time Kami sits in the bucket: an occasional creak, not a
    //loop, so the bank row stays a plain one-shot Diego can swap for a real recording
    IEnumerator RopeCreakLoop()
    {
        while (!_launched)
        {
            yield return new WaitForSeconds(Random.Range(_ropeCreakInterval.x, _ropeCreakInterval.y));

            if (_launched)
            {
                yield break;
            }

            if (AudioManager.instance != null)
            {
                AudioManager.instance.Play(AudioId.RopeCreak);
            }
        }
    }

    //the bucket is off the NavMesh: switch her agent off and place her by hand, feet on the seat
    void SeatAbuela()
    {
        if (_abuela == null || _abuelaSeat == null)
        {
            Debug.LogWarning("[Catapult] no Abuela or no _abuelaSeat, Kami flies alone");
            return;
        }

        _abuela.StopFollowingPlayer();
        float pivotAboveFeet = _abuela.navAgent != null ? _abuela.navAgent.baseOffset * _abuela.transform.lossyScale.y : 0f;
        if (_abuela.navAgent != null)
        {
            _abuela.navAgent.enabled = false;
        }
        _abuela.transform.position = _abuelaSeat.position + Vector3.up * pivotAboveFeet;
    }

    void OnRopeCut()
    {
        if (!_boarded || _launched)
        {
            return;
        }

        _launched = true;
        Debug.Log("[Catapult] the rope is cut, launching");

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip(_cutRopeColor, this);
        }

        _cutscene.Play();
    }

    void SetRopeCuttable(bool cuttable)
    {
        if (_ropeTrigger != null)
        {
            _ropeTrigger.enabled = cuttable;
        }
    }

    // ---------------------------------------------------------------- timeline signals

    /// <summary>Signal: the arm swings, the girls leave the bucket and fly off-frame.</summary>
    public void CUE_Launch()
    {
        Vector3 start = _kamiSeat != null ? _kamiSeat.position : transform.position;

        if (LevelManager.Instance.player != null)
        {
            HiddenRenderers.Hide(LevelManager.Instance.player.gameObject);
        }

        if (_abuela != null)
        {
            HiddenRenderers.Hide(_abuela.gameObject);
        }

        SetAllActive(_hideOnLaunch, false);
        SetAllActive(_showOnLaunch, true);
        //the arm hitting its stop, then the girls whooshing away (replaces Jump_Paperplane)
        AudioManager.instance.Play(AudioId.CatapultThud);
        AudioManager.instance.Play(AudioId.CatapultWhoosh);

        if (_flyingGirls != null && _flightTarget != null)
        {
            StartCoroutine(Fly(start));
        }
        else
        {
            Debug.LogWarning("[Catapult] no _flyingGirls or _flightTarget: the girls just vanish");
        }
    }

    IEnumerator Fly(Vector3 start)
    {
        _flyingGirls.position = start;
        _flyingGirls.gameObject.SetActive(true);
        Vector3 end = _flightTarget.position;
        float elapsed = 0f;

        while (elapsed < _flightSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _flightSeconds));
            Vector3 point = Vector3.Lerp(start, end, t);
            point.y += 4f * _flightArcHeight * t * (1f - t);
            _flyingGirls.position = point;
            yield return null;
        }

        _flyingGirls.gameObject.SetActive(false);
    }

    /// <summary>Signal: fade to black over _fadeSeconds.</summary>
    public void CUE_FadeOut()
    {
        if (_fader == null)
        {
            Debug.LogWarning("[Catapult] no _fader assigned, cutting straight to the next scene at LoadEnding");
            return;
        }

        _fader.FadeOut(_fadeSeconds);
    }

    /// <summary>Signal: load Level 2's closing cutscene.</summary>
    public void CUE_LoadEnding()
    {
        Debug.Log($"[Catapult] loading {_nextScene}");
        LevelManager.Instance.GoToScene(_nextScene);
    }

    static void SetAllActive(GameObject[] objects, bool active)
    {
        if (objects == null)
        {
            return;
        }

        foreach (GameObject target in objects)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_rope != null)
        {
            _rope.OnCut.RemoveListener(OnRopeCut);
        }
    }
}
