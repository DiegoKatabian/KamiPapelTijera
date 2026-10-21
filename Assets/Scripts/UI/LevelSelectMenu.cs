using System.Collections;
using UnityEngine;

/// <summary>
/// The main menu's level select (spec 019): New Game fades the logo and the menu buttons out and the two
/// level cards in; Esc, Start or B (or the Back button) brings the menu back. The cards' buttons call
/// MainMenuManager.BTN_StartLevel1 / BTN_StartLevel2, which own what happens after a pick.
///
/// Everything stays active and only CanvasGroups change: switching a uGUI Selectable's parent off while its
/// group changes leaves its cached "interactable" state stale (see "Fugas de foco" in
/// docs/claude/controles-y-gamepad.md), and a hidden group with interactable off is also skipped by the
/// stick's navigation, so the hidden menu buttons can't be reached while the cards are up, nor the other way.
/// </summary>
public class LevelSelectMenu : MonoBehaviour
{
    [SerializeField, Tooltip("This panel's own group: starts invisible and not clickable (alpha 0, raycasts off).")]
    CanvasGroup _panel;

    [SerializeField, Tooltip("What fades out while the cards are up: the logo + menu buttons group, the logo's smoke.")]
    CanvasGroup[] _hideWhileOpen;

    [SerializeField, Tooltip("Selected when the panel opens with a joystick (the Level 1 card).")]
    GameObject _firstCard;

    [SerializeField, Tooltip("Sent back to when the panel closes, so the joystick lands on New Game again.")]
    MainMenuManager _menu;

    [SerializeField, Tooltip("The scene whose presence in the build decides whether there is anything to choose. " +
                             "The Steam demo ships without Level 2: New Game then starts Level 1 directly.")]
    GameScene _optionalLevel = GameScene.Level2;

    [SerializeField, Tooltip("Seconds of the cross-fade between the menu and the cards.")]
    float _fadeSeconds = 0.35f;

    bool _choiceLocked; //a level was picked: no going back, no second pick
    Coroutine _fade;

    public bool IsOpen { get; private set; }

    /// <summary>False in a build that does not contain the optional level (the Level 1 demo).</summary>
    public bool IsAvailable
    {
        get
        {
            string sceneName = SceneCatalog.NameOf(_optionalLevel);
            return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
        }
    }

    void Awake()
    {
        if (_panel == null)
        {
            Debug.LogWarning("[LevelSelectMenu] no _panel CanvasGroup wired: using the one on this object");
            _panel = GetComponent<CanvasGroup>();
        }
        SetGroupVisible(_panel, false, 0f);
    }

    void Update()
    {
        if (!IsOpen || _choiceLocked)
        {
            return;
        }

        //Esc and Start are the "Options" axis, B is the gamepad's attack: the usual "go back" of every menu here
        if (InputHub.OpcionesDown || InputHub.AtaqueGamepadDown)
        {
            Close();
        }
    }

    public void Open()
    {
        if (IsOpen || _choiceLocked)
        {
            return;
        }

        IsOpen = true;
        Debug.Log("[LevelSelectMenu] open");
        UISelector.Limpiar(); //New Game keeps the focus otherwise, and it is about to fade out
        StartFade(true);
        SelectFirstCard();
    }

    /// <summary>Back button / Esc / Start / B.</summary>
    public void BTN_Back()
    {
        Close();
    }

    public void Close()
    {
        if (!IsOpen || _choiceLocked)
        {
            return;
        }

        IsOpen = false;
        Debug.Log("[LevelSelectMenu] closed, back to the menu");
        UISelector.Limpiar();
        StartFade(false);
        if (_menu != null)
        {
            _menu.SelectNewGameButtonAgain();
        }
    }

    /// <summary>Called by MainMenuManager when a card was picked: the panel stays on screen under the black plate.</summary>
    public void LockChoice()
    {
        _choiceLocked = true;
        if (_panel != null)
        {
            _panel.interactable = false;
            _panel.blocksRaycasts = false;
        }
    }

    public void SelectFirstCard()
    {
        if (_firstCard == null)
        {
            Debug.LogWarning("[LevelSelectMenu] no _firstCard wired: the joystick has nothing selected in the level select");
            return;
        }
        UISelector.SeleccionarPrimeroSiJoystick(_firstCard);
    }

    void StartFade(bool open)
    {
        if (_fade != null)
        {
            StopCoroutine(_fade);
        }

        //clicks and navigation switch at once; only the alpha takes time
        SetInteraction(_panel, open);
        foreach (CanvasGroup group in _hideWhileOpen)
        {
            SetInteraction(group, !open);
        }
        _fade = StartCoroutine(Fade(open));
    }

    IEnumerator Fade(bool open)
    {
        float panelFrom = _panel != null ? _panel.alpha : 0f;
        float panelTo = open ? 1f : 0f;
        float t = 0f;
        while (t < 1f)
        {
            t = _fadeSeconds > 0f ? Mathf.Min(1f, t + Time.unscaledDeltaTime / _fadeSeconds) : 1f;
            if (_panel != null)
            {
                _panel.alpha = Mathf.Lerp(panelFrom, panelTo, t);
            }
            foreach (CanvasGroup group in _hideWhileOpen)
            {
                if (group != null)
                {
                    //the menu fades out faster than the cards fade in, so the two never sit on top of each other
                    group.alpha = open ? Mathf.Min(group.alpha, 1f - t) : Mathf.Max(group.alpha, t);
                }
            }
            yield return null;
        }
        _fade = null;
    }

    static void SetInteraction(CanvasGroup group, bool on)
    {
        if (group == null)
        {
            Debug.LogWarning("[LevelSelectMenu] a CanvasGroup in the list is missing");
            return;
        }
        group.interactable = on;
        group.blocksRaycasts = on;
    }

    static void SetGroupVisible(CanvasGroup group, bool on, float alpha)
    {
        if (group == null)
        {
            return;
        }
        SetInteraction(group, on);
        group.alpha = alpha;
    }
}
