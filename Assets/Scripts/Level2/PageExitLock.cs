using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level 2: a page's "next page" sphere stays off until that page's task is done (Diego, 2026-10-08):
/// page 1 until the café ticket is folded, page 2 until the clues are found, page 3 until the arrest
/// has happened, page 4 until the police station is escaped. The unlock is latched: once the event has
/// fired, the page stays open even if Kami comes back to it later. Page 5 has no next page (the
/// catapult ends the level), so it needs no gate.
///
/// Lives at the scene root, like the other Level 2 page owners. It only decides WHEN the sphere is
/// locked; the sphere itself is PageScrollerManager's (SetNextLocked). The previous-page sphere is not
/// touched: going back is always allowed.
/// </summary>
public class PageExitLock : MonoBehaviour
{
    [Serializable]
    struct Gate
    {
        [Tooltip("Page number, 1-based like PageScrollerManager.startingPage.")]
        public int page;

        [Tooltip("The event that finishes this page's task and opens its next-page sphere for good.")]
        public Evento unlockEvent;

        public Gate(int page, Evento unlockEvent)
        {
            this.page = page;
            this.unlockEvent = unlockEvent;
        }
    }

    [SerializeField, Tooltip("One gate per page that must be earned. A page without a gate is open from the start.")]
    Gate[] _gates =
    {
        new Gate(1, Evento.OnCafeWrapperFolded),
        new Gate(2, Evento.OnAllCluesFound),
        new Gate(3, Evento.OnArrestSequenceEnded),
        new Gate(4, Evento.OnPoliceStationEscaped),
    };

    readonly HashSet<int> _unlockedPages = new HashSet<int>();
    readonly List<(Evento evento, EventManager.EventReceiver receiver)> _subscriptions = new List<(Evento, EventManager.EventReceiver)>();

    void Start()
    {
        if (PageScrollerManager.Instance == null)
        {
            Debug.LogWarning("[PageExitLock] no PageScrollerManager in the scene: no page will be locked");
            return;
        }

        foreach (Gate gate in _gates)
        {
            int page = gate.page; //the receiver must capture its own copy
            EventManager.EventReceiver receiver = parameters => Unlock(page);
            EventManager.Subscribe(gate.unlockEvent, receiver);
            _subscriptions.Add((gate.unlockEvent, receiver));
        }

        //the sphere of the page about to open has to be decided before PageScrollerManager shows it
        EventManager.Subscribe(Evento.OnPageTurnStart, OnPageTurnStart);

        Apply(PageScrollerManager.Instance.activePageIndex);
    }

    void OnDestroy()
    {
        foreach ((Evento evento, EventManager.EventReceiver receiver) in _subscriptions)
        {
            EventManager.Unsubscribe(evento, receiver);
        }
        _subscriptions.Clear();
        EventManager.Unsubscribe(Evento.OnPageTurnStart, OnPageTurnStart);
    }

    void OnPageTurnStart(params object[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            return;
        }

        Apply((int)parameters[0]); //param0 is the page index the turn is going TO
    }

    void Unlock(int page)
    {
        if (!_unlockedPages.Add(page))
        {
            return;
        }

        Debug.Log($"[PageExitLock] page {page}'s task is done: its next-page sphere is open for good");

        if (PageScrollerManager.Instance != null)
        {
            Apply(PageScrollerManager.Instance.activePageIndex);
        }
    }

    void Apply(int pageIndex)
    {
        int page = pageIndex + 1;
        bool gated = false;

        foreach (Gate gate in _gates)
        {
            if (gate.page == page)
            {
                gated = true;
                break;
            }
        }

        PageScrollerManager.Instance.SetNextLocked(gated && !_unlockedPages.Contains(page));
    }
}
