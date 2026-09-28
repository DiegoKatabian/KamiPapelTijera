using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Plays a Timeline as an in-game cutscene. Generic: nothing here knows about any specific scene.
///
/// - Owns Kami while it plays (LevelManager.inCutscene): no movement, jump or attack.
/// - CutsceneDialogueMarkers on the timeline hold the timeline still until their dialogue is closed.
/// - Cinemachine tracks are bound to the scene's CinemachineBrain in code, so a cutscene prefab
///   works the moment it is dropped into a scene, with no scene wiring.
///
/// Everything else a cutscene does (cops appearing, a car driving off) is Unity Signals on the same
/// timeline, answered by a SignalReceiver on this GameObject that calls into a scene-specific
/// script (e.g. ArrestCutscene). Tuning = dragging clips and markers in the Timeline window.
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneDirector : MonoBehaviour, INotificationReceiver
{
    [SerializeField, Tooltip("Give Kami back to the player as soon as the timeline ends. Turn off when the scene-specific script still has work to do afterwards (e.g. a page turn) and will call Release() itself.")]
    bool _releaseControlWhenTimelineEnds = true;

    PlayableDirector _director;
    DialogueSO _waitingFor;
    bool _playing;

    public bool IsPlaying => _playing;

    void Awake()
    {
        _director = GetComponent<PlayableDirector>();
        _director.playOnAwake = false;
        _director.stopped += OnDirectorStopped;
    }

    void Start()
    {
        EventManager.Subscribe(Evento.OnDialogueEnd, OnDialogueEnded);
    }

    public void Play()
    {
        if (_playing)
        {
            Debug.LogWarning($"[CutsceneDirector] {gameObject.name}: already playing, ignoring Play()");
            return;
        }

        if (_director.playableAsset == null)
        {
            Debug.LogWarning($"[CutsceneDirector] {gameObject.name}: the PlayableDirector has no timeline assigned, nothing to play");
            return;
        }

        _playing = true;
        LevelManager.Instance.inCutscene = true;
        BindCinemachineTracks();

        _director.time = 0;
        _director.Play();
        Debug.Log($"[CutsceneDirector] {gameObject.name}: playing '{_director.playableAsset.name}'");
    }

    /// <summary>Gives Kami back to the player. Called automatically at the end unless _releaseControlWhenTimelineEnds is off.</summary>
    public void Release()
    {
        if (!_playing)
        {
            return;
        }

        _playing = false;
        LevelManager.Instance.inCutscene = false;
        Debug.Log($"[CutsceneDirector] {gameObject.name}: control goes back to the player");
    }

    void BindCinemachineTracks()
    {
        TimelineAsset timeline = _director.playableAsset as TimelineAsset;
        if (timeline == null)
        {
            return;
        }

        CinemachineBrain brain = FindObjectOfType<CinemachineBrain>();
        if (brain == null)
        {
            Debug.LogWarning($"[CutsceneDirector] {gameObject.name}: no CinemachineBrain in the scene, the cutscene's camera shots will not play");
            return;
        }

        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track is CinemachineTrack)
            {
                _director.SetGenericBinding(track, brain);
            }
        }
    }

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is CutsceneDialogueMarker marker)
        {
            if (marker.Dialogue == null)
            {
                Debug.LogWarning($"[CutsceneDirector] {gameObject.name}: a dialogue marker at {marker.time:F2}s has no dialogue assigned, skipping it");
                return;
            }

            StartCoroutine(HoldForDialogue(marker.Dialogue));
        }
    }

    //speed 0 instead of Pause(): the graph keeps evaluating, so the live camera shot and every
    //active clip stay exactly as they are while the player reads
    IEnumerator HoldForDialogue(DialogueSO dialogue)
    {
        SetTimelineSpeed(0);

        while (DialogueManager.Instance == null || !DialogueManager.Instance.CanShowDialogueNow)
        {
            yield return null;
        }

        _waitingFor = dialogue;
        DialogueManager.Instance.ShowDialogue(dialogue);

        while (_waitingFor != null)
        {
            yield return null;
        }

        SetTimelineSpeed(1);
    }

    void OnDialogueEnded(params object[] parameters)
    {
        if (_waitingFor == null || parameters == null || parameters.Length < 2)
        {
            return;
        }

        if ((DialogueSO)parameters[1] == _waitingFor)
        {
            _waitingFor = null;
        }
    }

    void SetTimelineSpeed(double speed)
    {
        if (_director.playableGraph.IsValid())
        {
            _director.playableGraph.GetRootPlayable(0).SetSpeed(speed);
        }
    }

    void OnDirectorStopped(PlayableDirector director)
    {
        Debug.Log($"[CutsceneDirector] {gameObject.name}: timeline finished");

        if (_releaseControlWhenTimelineEnds)
        {
            Release();
        }
    }

    void OnDestroy()
    {
        if (_director != null)
        {
            _director.stopped -= OnDirectorStopped;
        }

        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnDialogueEnd, OnDialogueEnded);
        }
    }
}
