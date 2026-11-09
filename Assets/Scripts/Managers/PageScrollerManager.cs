using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PageScrollerManager : Singleton<PageScrollerManager>
{
    //este script se encarga de cambiar las paginas y algunas cosas mas

    //cuando tocas E, instancia la hoja que debe girar, y dispara metodos
    //entre ellos, cerrar/abrir pubs, luego mover al player
    
    //tambien chequea cual zona de cambio de pagina deberia mostrarse
    [SerializeField] GameObject[] pagesToToggle; //las 5 carpetas (buildingspagina1, 2, 3, 4 y 5)
    [SerializeField] Animator[] pagesAnimators; //sus 5 animators

    public int startingPage = 1;
    [HideInInspector] public int activePageIndex = 0; //activeIndex = startingPage - 1;
    public TriggerScript esferaPrev; //la zona para volver a pag anterior
    public TriggerScript esferaNext; //la zona para ir a pag siguiente

    public GameObject hojaMaster; //el prefab de la hoja que va para adelante
    public GameObject hojaMasterRev; //idem para atras
    [SerializeField] float delayTime;
    [SerializeField] float popupDelayTime;
    public bool _isNext = true;

    bool _isTurning = false;

    //a turn forced from code (a cutscene) can skip Kami riding the page edge, e.g. while she is
    //hidden "inside the patrol car". Reset when the turn finishes.
    bool _skipRideThisTurn;

    public GameObject glitterParent;
    ParticleSystem[] glitterSystems;

    GameObject hojaAux; //solo la guardo para luego poder destruirla

    bool _bossfightWasCompleted;

    //the "next page" sphere stays off while something holds it shut (Level 2: the page's task isn't done
    //yet, see PageExitLock). Never set in Level 1, where it keeps behaving as before.
    bool _nextLocked;

    public float DelayBetweenCamCloseUpAndCamBook = 2;

    [Header("Paper page turns (spec 025)")]
    [Tooltip("On: pages turn with the procedural paper sheet and the page objects fold with it (PaperTurn). " +
             "Off: the old HojaMaster sheet and the pop-up shrink/enlarge.")]
    [SerializeField] bool _paperPages;
    [Tooltip("The PaperTurn that runs paper turns. Empty = the one on this object.")]
    [SerializeField] PaperTurn _paperTurn;

    [Header("Pull before a turn (spec 024 3.C)")]
    [Tooltip("A page turn Kami starts herself opens with her PullSolapas gesture; the turn's delays grow by the gesture's length. Forced turns (cutscenes) never pull.")]
    [SerializeField] bool _pullBeforeTurn = true;
    float _pullExtraDelay; //this turn's extra delay for the pull

    int _turnFrom; //page index a turn leaves
    bool _paperThisTurn; //this turn runs on PaperTurn


    protected override void Awake()
    {
        base.Awake();
        EventManager.Subscribe(Evento.OnPlayerPressedE, TriggerPageScroll);
        EventManager.Subscribe(Evento.OnPageFinishTurning, FinishTurning);
        EventManager.Subscribe(Evento.OnEncounterEnd, OnEncounterEnd);

        activePageIndex = startingPage - 1;
        CheckSpheres(activePageIndex);
        if (_paperPages && _paperTurn == null)
        {
            _paperTurn = GetComponent<PaperTurn>();
            if (_paperTurn == null)
            {
                Debug.LogWarning("[PageScrollerManager] _paperPages is on but there is no PaperTurn: pages turn the old way");
            }
        }
        GetAllParticleSystemsInChildren(glitterParent);
        //esferaPrev.gameObject.SetActive(false);

    }

    private void Start()
    {
        
    }

    /// <summary>A page's folder (0-based), or null.</summary>
    public GameObject PageFolder(int index)
    {
        return pagesToToggle != null && index >= 0 && index < pagesToToggle.Length ? pagesToToggle[index] : null;
    }

    public void GetAllParticleSystemsInChildren(GameObject glitterParent)
    {
        glitterSystems = glitterParent.GetComponentsInChildren<ParticleSystem>();
    }

    public void TriggerPageScroll(params object[] parameters) //cuando tocas E
    {
        //the E that advances a dialogue line (a cutscene's too) is the same press: it must never also
        //turn the page Kami happens to stand next to (#168). The dialogue is still open when the
        //press is handled (it closes later that frame, in its coroutine), so the flag covers its last line.
        if (LevelManager.Instance != null && (LevelManager.Instance.inDialogue || LevelManager.Instance.inCutscene))
        {
            return;
        }

        if (!OverlayManager.Instance.isLocked)
        {
            if (esferaNext.triggerBool
                && !_isTurning
                /*&& activePageIndex < pagesToToggle.Length - 1*/)
            {
                PlayPullBeforeTurn();
                ChangeToNextPage();
            }

            if (esferaPrev.triggerBool
                && !_isTurning
                /*&& activePageIndex > 0*/)
            {
                PlayPullBeforeTurn();
                ChangeToPrevPage();
            }
        }
    }

    //spec 024 3.C: a turn Kami starts herself opens with her pulling the page (PullSolapas, a placeholder for a page
    //gesture); the turn's delays grow by the gesture's own length, so they follow the clip if it is re-exported.
    //Forced turns (TurnToNextPage, the arrest) never come through here and skip it.
    void PlayPullBeforeTurn()
    {
        _pullExtraDelay = 0f;
        if (!_pullBeforeTurn)
        {
            return;
        }
        Player kami = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (kami == null)
        {
            Debug.LogWarning("[PageScrollerManager] no player: the page turns without the pull");
            return;
        }
        _pullExtraDelay = kami.PlayPullSolapa(false);
        Debug.Log($"[PageScrollerManager] Kami pulls the page first: the turn waits {_pullExtraDelay:F2}s more");
    }

    /// <summary>
    /// Turns to the next page without Kami standing on the edge sphere (a cutscene ending). Same
    /// flow as the player's own turn. ridePage = false skips Kami hanging from the page edge.
    /// Returns false if a turn is already running.
    /// </summary>
    public bool TurnToNextPage(bool ridePage)
    {
        if (_isTurning)
        {
            Debug.LogWarning("[PageScrollerManager] TurnToNextPage: a page turn is already running, ignoring");
            return false;
        }

        _skipRideThisTurn = !ridePage;
        _pullExtraDelay = 0f; //a forced turn has no pull
        Debug.Log($"[PageScrollerManager] TurnToNextPage forced from code (ridePage {ridePage})");
        ChangeToNextPage();
        return true;
    }

    private void ChangeToPrevPage()
    {
        //Debug.Log("shrink page index " + activePageIndex);

        _turnFrom = activePageIndex;
        _paperThisTurn = _paperPages && _paperTurn != null;
        if (!_paperThisTurn) //a paper turn folds the page's objects itself: no shrink
        {
            pagesAnimators[activePageIndex].SetBool("isEnlarge", false);
            pagesAnimators[activePageIndex].SetBool("isShrink", true);
        }

        activePageIndex--; //el paso de pagina posta, para atras
        //convert active page index teniendo en cuenta la cantidad total de paginas. tal que si estoy en la primera pagina y voy para atras  me mande a la ultima
        activePageIndex = (activePageIndex + pagesToToggle.Length) % pagesToToggle.Length;

        _isNext = false; //isNext es una variable piola que mucha gente necesita
        _isTurning = true;
        esferaPrev.triggerBool = false;
        EventManager.Trigger(Evento.OnPageTurnStart, activePageIndex, _isNext);
        StartChangePageFX();

        StartCoroutine(CerrarPaginaCoroutine(delayTime + _pullExtraDelay));
        StartCoroutine(AbrirPaginaCoroutine(popupDelayTime + _pullExtraDelay));
        _pullExtraDelay = 0f;
    }

    private void ChangeToNextPage()
    {
        //Debug.Log("shrink page index " + activePageIndex);
        _turnFrom = activePageIndex;
        _paperThisTurn = _paperPages && _paperTurn != null;
        if (!_paperThisTurn) //a paper turn folds the page's objects itself: no shrink
        {
            pagesAnimators[activePageIndex].SetBool("isEnlarge", false);
            pagesAnimators[activePageIndex].SetBool("isShrink", true);
        }

        activePageIndex++; //el paso de pagina posta
        //convert active page index teniendo en cuenta la cantidad total de paginas. tal que si me paso, cicle de vuelta a la primera. asi cuando estoy en la ultima pagina, me manda a la primera. 
        activePageIndex = activePageIndex % pagesToToggle.Length;


        _isNext = true; //isNext es una variable piola que mucha gente necesita
        _isTurning = true;
        esferaNext.triggerBool = false;
        EventManager.Trigger(Evento.OnPageTurnStart, activePageIndex, _isNext);
        StartChangePageFX();

        StartCoroutine(CerrarPaginaCoroutine(delayTime + _pullExtraDelay));
        StartCoroutine(AbrirPaginaCoroutine(popupDelayTime + _pullExtraDelay));
        _pullExtraDelay = 0f;
    }

    void StartChangePageFX()
    {
        StartCoroutine(ChangePageCameraSequence());
        AudioManager.instance.Play(AudioId.MagicSuccess, 0.5f, 0.01f);
        StartCoroutine(PostProcessManager.Instance.LerpBloomIntensity());
        PlayGlitter();
    }

    IEnumerator ChangePageCameraSequence()
    {
        CameraManager.Instance.SetCamera(CameraMode.CloseUp);
        yield return new WaitForSeconds(DelayBetweenCamCloseUpAndCamBook);
        CameraManager.Instance.SetCamera(CameraMode.BookCenter);
    }

    /// <summary>
    /// Keeps the next-page sphere off (or lets it back on) for the page being shown. Called by whoever
    /// owns a page's "you can't leave yet" rule. During a turn only the flag changes: CheckSpheres runs
    /// for the new page when the old one closes.
    /// </summary>
    public void SetNextLocked(bool locked)
    {
        if (_nextLocked == locked)
        {
            return;
        }

        _nextLocked = locked;
        Debug.Log($"[PageScrollerManager] next page sphere {(locked ? "locked" : "unlocked")}");

        if (!_isTurning)
        {
            CheckSpheres(activePageIndex);
        }
    }

    public void CheckSpheres(int activePageIndex)
    {
        //este metodo chequea, segun la currentPage, que esferas deberian estar activas
        if (activePageIndex <= 0) //si estoy en la primera pagina...
        {
            esferaPrev.OnExitBehaviour();
            esferaPrev.gameObject.SetActive(false);
            if (_bossfightWasCompleted)
            {
                esferaPrev.gameObject.SetActive(true);
            }
            
        }
        else
        {
            esferaPrev.gameObject.SetActive(true);

        }

        if (activePageIndex >= pagesToToggle.Length - 1) //si estoy en la ultima pagina...
        {
            esferaNext.OnExitBehaviour();
            esferaNext.gameObject.SetActive(false);
            if (_bossfightWasCompleted)
            {
                esferaNext.gameObject.SetActive(true);
            }
        }
        else if (_nextLocked)
        {
            //Kami may be standing in it: leave by hand, switching a trigger off sends no OnTriggerExit
            esferaNext.OnExitBehaviour();
            esferaNext.gameObject.SetActive(false);
        }
        else
        {
            esferaNext.gameObject.SetActive(true);
        }

    }
    public IEnumerator CerrarPaginaCoroutine(float cerrarDelayTime) //cierro la pag actual
    {
        yield return new WaitForSeconds(cerrarDelayTime);
        LevelManager.Instance.inDialogue = true;  //freezeo a kami
        hojaAux = null;
        if (_paperThisTurn)
        {
            //before ClosePUBs: the proxies copy the page as it is now
            hojaAux = _paperTurn.Begin(_turnFrom, activePageIndex, _isNext, pagesToToggle[_turnFrom], pagesToToggle[activePageIndex]);
            if (hojaAux == null)
            {
                _paperThisTurn = false;
            }
        }
        PUBManager.Instance.ClosePUBs();
        if (!_paperThisTurn)
        {
            CreateHoja(_isNext); //instancio la hoja que corresponda
        }
        if (!_skipRideThisTurn)
        {
            StartPlayerRide();
        }
        CheckSpheres(activePageIndex); //chequeo si hay que poner/sacar zona
        PlayPageSound();
    }
    public IEnumerator AbrirPaginaCoroutine(float abrirDelayTime)
    {
        yield return new WaitForSeconds(abrirDelayTime);
        EventManager.Trigger(Evento.OnNewPageOpen, activePageIndex + 1, _isNext);
        PUBManager.Instance.OpenPUBs();
        TogglePages(activePageIndex);

        pagesAnimators[activePageIndex].SetBool("isShrink", false);
        pagesAnimators[activePageIndex].SetBool("isEnlarge", true);
    } //abro la nueva pag

    public void TogglePages(int pageIndex)
    {
        for (int i = 0; i < pagesToToggle.Length; i++) //este es el core, el q prende y apaga la carpeta de cada pagina
        {
            if (i == pageIndex)
            {
                pagesToToggle[i].SetActive(true);
            }
            else
            {
                pagesToToggle[i].SetActive(false);
            }
        }
    }
    void CreateHoja(bool isNext)
    {
        if (isNext)
        {
            hojaAux = Instantiate(hojaMaster, transform);
        }
        else
        {
            hojaAux = Instantiate(hojaMasterRev, transform);
        }
    }
    void StartPlayerRide()
    {
        //engancha a kami al borde de la hoja recien instanciada para que viaje agarrada durante el giro.
        //ojo: el componente Hoja NO esta en la raiz de HojaMaster/HojaMaster_Rev, esta en el hijo HojaCaraAbajo
        //(instancia anidada de Hoja.prefab/Hoja_Rev.prefab) -> hace falta GetComponentInChildren, no GetComponent.
        if (_paperThisTurn)
        {
            if (_paperTurn.Edge != null)
            {
                LevelManager.Instance.player.StartRidingPage(_paperTurn.Edge, _isNext);
            }
            else
            {
                Debug.LogWarning("[PageScrollerManager] the paper sheet has no edge: kami no queda agarrada");
            }
            return;
        }
        Hoja hojaComponent = hojaAux.GetComponentInChildren<Hoja>(true);
        Debug.Log($"[PageScrollerManager] StartPlayerRide en '{hojaAux.name}': hojaComponent {(hojaComponent == null ? "NO encontrado" : $"encontrado en '{hojaComponent.name}'")}, edgeBone {(hojaComponent != null && hojaComponent.EdgeBone != null ? $"'{hojaComponent.EdgeBone.name}' @ {hojaComponent.EdgeBone.position}" : "NULO")}");

        if (hojaComponent != null && hojaComponent.EdgeBone != null)
        {
            LevelManager.Instance.player.StartRidingPage(hojaComponent.EdgeBone, _isNext);
        }
        else
        {
            Debug.LogWarning("[PageScrollerManager] no se encontro el hueso de borde en la hoja instanciada: kami no queda agarrada");
        }
    }
    void FinishTurning(params object[] parameter) //esto recien se triggerea cuando TERMINA de cambiar la pagina
    {
        LevelManager.Instance.inDialogue = false;
        _isTurning = false;
        _skipRideThisTurn = false;
        CameraManager.Instance.SetCamera(CameraMode.Normal);
        if (hojaAux != null)
        {
            Destroy(hojaAux);
        }
        _paperThisTurn = false;
    } //este se dispara cuando la hoja termina de girar y avisa "che ya termine de girar" a traves el evento onpagefinishturnng
    void PlayPageSound()
    {
        if (_isNext)
        {
            AudioManager.instance.Play(AudioId.PageTurn01, 1f, 0.03f);
        }
        else
        {
            AudioManager.instance.Play(AudioId.PageTurn02, 1f, 0.03f);
        }
    }
    public void PlayGlitter()
    {
        foreach (ParticleSystem ps in glitterSystems)
        {
            ps.Play();
        }
    }
    void OnEncounterEnd(params object[] parameters)
     {
        if (!_bossfightWasCompleted)
        {
            _bossfightWasCompleted = true;
            //CheckSpheres(activePageIndex);
        }
    }
    private void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, TriggerPageScroll);
            EventManager.Unsubscribe(Evento.OnPageFinishTurning, FinishTurning);
            EventManager.Unsubscribe(Evento.OnEncounterEnd, OnEncounterEnd);
        }
    }
}