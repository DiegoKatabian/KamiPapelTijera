using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CandadoCortable : PickupCortable
{
    //el candado dispara la animacion de abrir de un cofreCortable cuando lo corto.
    //[SerializeField] protected float timeUntilDestroy = 5;

    [SerializeField] protected CofreCortable cofreQueAbro;

    [Tooltip("Fired once when cut. For a padlock that opens something other than a chest.")]
    [SerializeField] UnityEvent onCut = new UnityEvent();

    bool _hasCodeListeners;

    //for listeners wired from code (PoliceStationPage opens a cell door): a scene object cannot point
    //a serialized UnityEvent at a component nested inside another prefab instance
    public void AddCutListener(UnityAction listener)
    {
        onCut.AddListener(listener);
        _hasCodeListeners = true;
    }

    protected override void ApplyCut()
    {
        base.ApplyCut();

        //metal-on-metal on top of the base cut sound: a padlock is not paper. Also plays on Level 1's
        //chest padlocks, which is the intent
        if (AudioManager.instance != null)
        {
            AudioManager.instance.Play(AudioId.PadlockClank);
        }

        //a padlock may lock a cell instead of a chest: only a padlock that opens nothing at all is a mistake
        if (cofreQueAbro != null)
        {
            cofreQueAbro.OpenChest();
        }
        else if (onCut.GetPersistentEventCount() == 0 && !_hasCodeListeners)
        {
            Debug.LogWarning($"[CandadoCortable] {gameObject.name}: cut, but it opens nothing (no chest and no onCut listener)");
        }

        onCut.Invoke();
    }
}
