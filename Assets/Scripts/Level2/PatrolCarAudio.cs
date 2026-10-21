using System.Collections;
using UnityEngine;

/// <summary>
/// The patrol car's sound sequence, shared by the three places it is used (page 2's crime scene, page 3's
/// arrest, page 5's arrest of Ariel) so they can't drift apart (Diego, 2026-10-21):
/// everyone who gets in plays door open, then door close; the drive-off plays the engine start together
/// with the last door close, and when the start is over the engine run loops under the siren.
/// The siren itself is still started by each site, because it begins at a different moment in each.
/// </summary>
public static class PatrolCarAudio
{
    //bumped by StopLoops so a drive-off routine still waiting on the engine start doesn't start the run loop afterwards
    static int _generation;

    /// <summary>Someone gets in: door open, then door close once the open has finished.</summary>
    public static void GetIn()
    {
        AudioManager audio = AudioManager.instance;
        if (audio == null)
        {
            Debug.LogWarning("[PatrolCarAudio] no AudioManager, the door sounds are skipped");
            return;
        }

        audio.Play(AudioId.CarDoorOpen);
        audio.PlayOnEnd(AudioId.CarDoorOpen, AudioId.CarDoor);
    }

    /// <summary>The car drives off: engine start (after any door sound still going), then the engine run loop.</summary>
    public static void DriveOff()
    {
        AudioManager audio = AudioManager.instance;
        if (audio == null)
        {
            Debug.LogWarning("[PatrolCarAudio] no AudioManager, the engine sounds are skipped");
            return;
        }

        audio.StartCoroutine(DriveOffRoutine(audio, _generation));
    }

    /// <summary>Stops everything that loops while the car is out: the engine run and the siren.</summary>
    public static void StopLoops()
    {
        _generation++;
        if (AudioManager.instance == null)
        {
            return;
        }

        AudioManager.instance.StopById(AudioId.CarEngineRun);
        AudioManager.instance.StopById(AudioId.PoliceSirenLoop);
    }

    //waits for the open door sound so the engine start lands with the close that follows it
    static IEnumerator DriveOffRoutine(AudioManager audio, int generation)
    {
        while (audio.IsPlaying(AudioId.CarDoorOpen))
        {
            yield return null;
        }

        audio.Play(AudioId.CarEngine);
        while (audio.IsPlaying(AudioId.CarEngine))
        {
            yield return null;
        }

        if (generation == _generation)
        {
            audio.Play(AudioId.CarEngineRun);
        }
    }
}
