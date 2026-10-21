using System.Collections;
using UnityEngine;

/// <summary>
/// The patrol car's sound sequence, shared by the three places it is used (page 2's crime scene, page 3's
/// arrest, page 5's arrest of Ariel) so they can't drift apart. Redone for #256 (Diego, 2026-10-21): the
/// first version chained door open -> close -> engine start by itself and the timing didn't land, so
/// now each site calls one step at the moment it wants it:
///
///   arrive      Brake()                  tires squeak (page 3: the squad rolls in)
///   standing    StartIdle(position)      engine idling under the dialogue (page 3 only)
///   door        OpenDoor() ... CloseDoor()   the site decides how long the cops/girls wait in between
///   start       EngineStart()            with the door close (pages 2 and 5; page 3 has no start)
///   moving      CarMoves(delay)          the run loop, <delay> seconds after the car begins to move
///   gone        StopLoops() / FadeOutLoopsAfter()
///
/// The siren loop and the PoliceHorn stay with each site, because they begin at a different moment in
/// each. The loops this class starts (idle, run) and the siren are all stopped by StopLoops.
/// </summary>
public static class PatrolCarAudio
{
    //bumped by StopLoops/CarMoves so a routine still waiting (run delay, fade delay) does nothing afterwards
    static int _generation;

    //the idle loop at the car; kept as a handle because page 3's four jammed cars play the same row,
    //and StopById would silence them too
    static SoundHandle _idle;

    //a fade has begun: a run loop that has not started yet must not start under it
    static bool _fading;

    /// <summary>Tires squeak: the patrol car pulls up.</summary>
    public static void Brake()
    {
        Play(AudioId.CarBrake);
    }

    /// <summary>
    /// The engine idles with the car standing, heard from the car. Positional (like the idling cars it
    /// shares the row with) so it stays on the street instead of sounding from inside the player's head.
    /// </summary>
    public static void StartIdle(Vector3 carPosition)
    {
        AudioManager audio = AudioManager.instance;
        if (audio == null)
        {
            Debug.LogWarning("[PatrolCarAudio] no AudioManager, the idle engine is skipped");
            return;
        }

        audio.Stop(_idle); //a second start (a test, a restart) never stacks two idles
        _idle = audio.PlayAt(AudioId.CarEngineIdleLoop, carPosition);
        Debug.Log("[PatrolCarAudio] engine idle starts");
    }

    public static void OpenDoor()
    {
        Play(AudioId.CarDoorOpen);
    }

    public static void CloseDoor()
    {
        Play(AudioId.CarDoor);
    }

    /// <summary>The key turns: the engine start clip (about 2 s). Played together with the door close.</summary>
    public static void EngineStart()
    {
        Play(AudioId.CarEngine);
    }

    /// <summary>
    /// The car has begun to move: <paramref name="runDelaySeconds"/> later the idle (if any) gives way to
    /// the engine run loop. Zero = right now.
    /// </summary>
    public static void CarMoves(float runDelaySeconds)
    {
        AudioManager audio = AudioManager.instance;
        if (audio == null)
        {
            Debug.LogWarning("[PatrolCarAudio] no AudioManager, the engine run is skipped");
            return;
        }

        _generation++;
        _fading = false;
        audio.StartCoroutine(RunAfter(audio, Mathf.Max(0f, runDelaySeconds), _generation));
    }

    /// <summary>Stops everything that loops while the car is out: idle, engine run and the siren.</summary>
    public static void StopLoops()
    {
        _generation++;
        _fading = false;
        AudioManager audio = AudioManager.instance;
        if (audio == null)
        {
            return;
        }

        audio.Stop(_idle);
        _idle = SoundHandle.None;
        audio.StopById(AudioId.CarEngineRun);
        audio.StopById(AudioId.PoliceSirenLoop);
    }

    /// <summary>
    /// After <paramref name="delaySeconds"/>, the engine run and the siren die away over
    /// <paramref name="fadeSeconds"/> (the car driving off the page, V4). Cancelled by StopLoops/CarMoves.
    /// </summary>
    public static void FadeOutLoopsAfter(float delaySeconds, float fadeSeconds)
    {
        AudioManager audio = AudioManager.instance;
        if (audio == null)
        {
            return;
        }

        audio.StartCoroutine(FadeAfter(audio, Mathf.Max(0f, delaySeconds), fadeSeconds, _generation));
    }

    static void Play(string id)
    {
        AudioManager audio = AudioManager.instance;
        if (audio == null)
        {
            Debug.LogWarning($"[PatrolCarAudio] no AudioManager, '{id}' is skipped");
            return;
        }

        audio.Play(id);
    }

    static IEnumerator RunAfter(AudioManager audio, float delay, int generation)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (generation != _generation || _fading)
        {
            yield break;
        }

        //the run replaces the idle: it is the same clip pitched up, so they never overlap
        audio.Stop(_idle);
        _idle = SoundHandle.None;
        audio.Play(AudioId.CarEngineRun);
        Debug.Log("[PatrolCarAudio] engine run starts");
    }

    static IEnumerator FadeAfter(AudioManager audio, float delay, float fadeSeconds, int generation)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (generation != _generation)
        {
            yield break;
        }

        _fading = true;
        audio.FadeOutById(AudioId.CarEngineRun, fadeSeconds);
        audio.FadeOutById(AudioId.PoliceSirenLoop, fadeSeconds);
        audio.FadeOut(_idle, fadeSeconds);
        _idle = SoundHandle.None;
        Debug.Log($"[PatrolCarAudio] the car's loops fade out over {fadeSeconds}s");
    }
}
