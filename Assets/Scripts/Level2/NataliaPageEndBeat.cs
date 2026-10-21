using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Level 2 page 2, after the last clue (V5, 2026-10-21): once Natalia's "take these clues to my house"
/// line is read, she is suddenly at the far end of the page, waving at Kami. The camera goes to her,
/// holds while she waves, and comes back; Kami is free after the blend back.
///
/// Why she then WAITS at the page's end instead of walking back to Kami: the exit is where Kami has to
/// go next, so Natalia stays put (she stops following) until Kami comes within _rejoinRadius, and then
/// follows again as usual. She also rejoins the moment a page turn starts, so PageRideFollowers (which
/// only boards followers) can never leave her behind if Kami turns the page from outside the radius.
///
/// The teleport is hidden by the camera, not by a fade: it happens _warpAtBlendFraction of the way
/// through the blend to her, when the camera is already mostly looking at the empty spot.
///
/// Started by FindCluesTracker as a queued step, so it never overlaps a dialogue. Framing is the vcam
/// (_shot) and its Follow target is _spot, which this script snaps onto the NavMesh before the shot goes
/// live: drag _spot to move both Natalia and the shot.
/// </summary>
public class NataliaPageEndBeat : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField, Tooltip("Natalia (the one from page 1: there is only one in the level).")]
    NPC _natalia;

    [SerializeField, Tooltip("Where she waves from, near the page's exit side. Snapped to the NavMesh when the beat plays (and the shot follows it), so put it on the street, not on a roof.")]
    Transform _spot;

    [SerializeField, Tooltip("The shot that shows her: a static vcam with a Transposer in world space following _spot (set in code). Framing = its Follow Offset + its X tilt. Empty = the camera stays on Kami.")]
    CinemachineVirtualCamera _shot;

    [Header("Beat")]
    [SerializeField, Tooltip("Pose id on Natalia's SpineCharacter: her wave ('Saludo').")]
    string _wavePose = "Wave";

    [SerializeField, Tooltip("Seconds she waves with the camera on her (after it has arrived).")]
    float _waveSeconds = 3f;

    [SerializeField, Tooltip("How far through the camera's blend she appears at the spot (0 = at once, 1 = when the camera has arrived). The camera is mostly on the empty spot by then, so nobody sees her pop in.")]
    [Range(0f, 1f)]
    float _warpAtBlendFraction = 0.55f;

    [SerializeField, Tooltip("How far from _spot the NavMesh is searched, in world units. The spot is a mark dropped by hand; the NavMesh might sit a bit off it.")]
    float _sampleRadius = 15f;

    [Header("Afterwards")]
    [SerializeField, Tooltip("Natalia waits at the spot until Kami is within this horizontal distance, then follows her again.")]
    float _rejoinRadius = 16f;

    [Header("Fallback spot")]
    [SerializeField, Tooltip("No NavMesh near _spot (page 2 not baked there?): she appears this far ahead of Kami toward the exit instead, on Kami's own line.")]
    float _fallbackAheadDistance = 25f;

    [SerializeField, Tooltip("...but never closer than this to the page's exit edge.")]
    float _fallbackEdgeMargin = 10f;

    bool _played;
    bool _running;
    bool _waiting;
    bool _lockedKami;

    void Start()
    {
        if (_natalia == null || _spot == null)
        {
            Debug.LogWarning($"[NataliaPageEndBeat] {gameObject.name}: _natalia or _spot is not assigned, the beat will be skipped.");
        }

        if (_shot == null)
        {
            Debug.LogWarning($"[NataliaPageEndBeat] {gameObject.name}: no _shot assigned, the camera will stay on Kami during the beat.");
        }

        EventManager.Subscribe(Evento.OnPageTurnStart, OnPageTurnStart);
    }

    /// <summary>Plays the beat once. Called by FindCluesTracker when Natalia's last-clue line has ended.</summary>
    public void Play()
    {
        if (_played)
        {
            return;
        }

        _played = true;

        if (_natalia == null || _spot == null || LevelManager.Instance == null || LevelManager.Instance.player == null)
        {
            Debug.LogWarning($"[NataliaPageEndBeat] {gameObject.name}: missing Natalia, spot or player: no beat.");
            return;
        }

        //she can only wave from the page's end if she came along: she joins when the café ticket is folded
        if (!_natalia.gameObject.activeInHierarchy || !_natalia.isFollowing)
        {
            Debug.LogWarning($"[NataliaPageEndBeat] {gameObject.name}: Natalia is not following Kami (or is switched off), no beat.");
            return;
        }

        //taken synchronously, so the next queued line in FindCluesTracker waits from this very frame
        SetKamiLocked(true);
        _running = true;
        StartCoroutine(Beat());
    }

    IEnumerator Beat()
    {
        Player kami = LevelManager.Instance.player;
        Vector3 ground = ResolveSpot(kami);
        _spot.position = ground;

        if (_shot != null)
        {
            _shot.Follow = _spot;
        }

        Debug.Log($"[NataliaPageEndBeat] the last clue is in: Natalia goes to the page's end at {ground} to wave");

        //she stays where she is until the camera is on its way: the agent is told to stop first
        _natalia.StopFollowingPlayer();
        CutsceneCamera.Show(_shot);
        float blend = _shot != null ? CutsceneCamera.BlendSeconds : 0f;

        yield return new WaitForSeconds(blend * _warpAtBlendFraction);

        _natalia.WarpTo(ground);
        if (_natalia.Character != null)
        {
            _natalia.Character.FaceTowards(kami.transform.position);
        }

        yield return new WaitForSeconds(blend * (1f - _warpAtBlendFraction));

        if (_natalia.Character != null)
        {
            _natalia.Character.PlayPose(_wavePose, _waveSeconds);
        }
        else
        {
            Debug.LogWarning("[NataliaPageEndBeat] Natalia has no SpineCharacter: she cannot wave.");
        }

        yield return new WaitForSeconds(_waveSeconds);

        CutsceneCamera.Hide(_shot);
        yield return new WaitForSeconds(blend);

        _running = false;
        _waiting = true;
        SetKamiLocked(false);
        Debug.Log("[NataliaPageEndBeat] the beat is over: Kami is free, Natalia waits at the page's end");
    }

    //the NavMesh point for the spot; when there is none nearby, a point ahead of Kami toward the exit
    Vector3 ResolveSpot(Player kami)
    {
        if (NavMesh.SamplePosition(_spot.position, out NavMeshHit hit, _sampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        Vector3 kamiPosition = kami.transform.position;
        float x = kamiPosition.x + _fallbackAheadDistance;
        PlayerPageSpawnManager pages = PlayerPageSpawnManager.Instance;
        if (pages != null)
        {
            x = Mathf.Min(x, pages.PageMaxX - _fallbackEdgeMargin);
        }

        Vector3 ahead = new Vector3(x, kamiPosition.y, kamiPosition.z);
        if (NavMesh.SamplePosition(ahead, out NavMeshHit aheadHit, _sampleRadius, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[NataliaPageEndBeat] no NavMesh within {_sampleRadius} units of {_spot.name} (is page 2 baked there?): using a point ahead of Kami instead");
            return aheadHit.position;
        }

        Debug.LogWarning($"[NataliaPageEndBeat] no NavMesh near {_spot.name} or ahead of Kami: Natalia is placed on the mark as it is");
        return _spot.position;
    }

    void Update()
    {
        if (!_waiting || _natalia == null)
        {
            return;
        }

        Player kami = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (kami == null)
        {
            return;
        }

        Vector3 toKami = kami.transform.position - _natalia.transform.position;
        toKami.y = 0f;
        if (toKami.magnitude <= _rejoinRadius)
        {
            Rejoin("Kami came close");
        }
    }

    //a page turn that starts while she is still waiting: she has to be a follower BEFORE the ride boards
    void OnPageTurnStart(params object[] parameters)
    {
        if (_waiting)
        {
            Rejoin("a page turn started");
        }
    }

    void Rejoin(string why)
    {
        _waiting = false;

        if (_natalia == null || !_natalia.gameObject.activeInHierarchy)
        {
            return;
        }

        if (_natalia.Character != null)
        {
            _natalia.Character.EndPose();
        }

        _natalia.StartFollowingPlayer();
        //the exit is behind the edge of the book: her spot beside Kami must be on the page's side (#246)
        _natalia.FollowSideTowardPageCentre();
        Debug.Log($"[NataliaPageEndBeat] Natalia follows Kami again ({why})");
    }

    //turning the page deactivates this object mid-beat in a test, never leave Kami frozen or the shot live
    void OnDisable()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        CutsceneCamera.Hide(_shot);
        SetKamiLocked(false);

        //she was already stopped and may be at the spot: she follows again rather than being stranded
        _waiting = true;
    }

    //only ever releases a lock this component took, so it can't cut another cutscene short
    void SetKamiLocked(bool locked)
    {
        if (LevelManager.Instance == null || (!locked && !_lockedKami))
        {
            return;
        }

        _lockedKami = locked;
        LevelManager.Instance.inCutscene = locked;
    }

    void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPageTurnStart, OnPageTurnStart);
        }
    }
}
