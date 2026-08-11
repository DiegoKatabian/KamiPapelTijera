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

    public GameObject glitterParent;
    ParticleSystem[] glitterSystems;

    GameObject hojaAux; //solo la guardo para luego poder destruirla

    bool _bossfightWasCompleted;


    protected override void Awake()
    {
        base.Awake();
        EventManager.Subscribe(Evento.OnPlayerPressedE, TriggerPageScroll);
        EventManager.Subscribe(Evento.OnPageFinishTurning, FinishTurning);
        EventManager.Subscribe(Evento.OnEncounterEnd, OnEncounterEnd);

        activePageIndex = startingPage - 1;
        CheckSpheres(activePageIndex);
        GetAllParticleSystemsInChildren(glitterParent);
        //esferaPrev.gameObject.SetActive(false);

    }

    private void Start()
    {
        
    }

    public void GetAllParticleSystemsInChildren(GameObject glitterParent)
    {
        glitterSystems = glitterParent.GetComponentsInChildren<ParticleSystem>();
    }

    public void TriggerPageScroll(params object[] parameters) //cuando tocas E
    {
        if (!OverlayManager.Instance.isLocked)
        {
            if (esferaNext.triggerBool 
                && !_isTurning 
                /*&& activePageIndex < pagesToToggle.Length - 1*/)
            {
                ChangeToNextPage();
            }

            if (esferaPrev.triggerBool 
                && !_isTurning 
                /*&& activePageIndex > 0*/)
            {
                ChangeToPrevPage();
            }
        }
    }

    private void ChangeToPrevPage()
    {
        //Debug.Log("shrink page index " + activePageIndex);

        pagesAnimators[activePageIndex].SetBool("isEnlarge", false);
        pagesAnimators[activePageIndex].SetBool("isShrink", true);

        activePageIndex--; //el paso de pagina posta, para atras
        //convert active page index teniendo en cuenta la cantidad total de paginas. tal que si estoy en la primera pagina y voy para atras  me mande a la ultima
        activePageIndex = (activePageIndex + pagesToToggle.Length) % pagesToToggle.Length;

        _isNext = false; //isNext es una variable piola que mucha gente necesita
        _isTurning = true;
        esferaPrev.triggerBool = false;
        EventManager.Trigger(Evento.OnPageTurnStart, activePageIndex, _isNext);
        StartChangePageFX();

        StartCoroutine(CerrarPaginaCoroutine(delayTime));
        StartCoroutine(AbrirPaginaCoroutine(popupDelayTime));
    }

    private void ChangeToNextPage()
    {
        //Debug.Log("shrink page index " + activePageIndex);
        pagesAnimators[activePageIndex].SetBool("isEnlarge", false);
        pagesAnimators[activePageIndex].SetBool("isShrink", true);

        activePageIndex++; //el paso de pagina posta
        //convert active page index teniendo en cuenta la cantidad total de paginas. tal que si me paso, cicle de vuelta a la primera. asi cuando estoy en la ultima pagina, me manda a la primera. 
        activePageIndex = activePageIndex % pagesToToggle.Length;


        _isNext = true; //isNext es una variable piola que mucha gente necesita
        _isTurning = true;
        esferaNext.triggerBool = false;
        EventManager.Trigger(Evento.OnPageTurnStart, activePageIndex, _isNext);
        StartChangePageFX();

        StartCoroutine(CerrarPaginaCoroutine(delayTime));
        StartCoroutine(AbrirPaginaCoroutine(popupDelayTime));
    }

    void StartChangePageFX()
    {
        CameraManager.Instance.SetCamera(CameraMode.BookCenter);
        AudioManager.instance.PlayByName("MagicSuccess", 0.5f, 0.01f);
        StartCoroutine(PostProcessManager.Instance.LerpBloomIntensity());
        PlayGlitter();
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
        else
        {
            esferaNext.gameObject.SetActive(true);
        }

    }
    public IEnumerator CerrarPaginaCoroutine(float cerrarDelayTime) //cierro la pag actual
    {
        yield return new WaitForSeconds(cerrarDelayTime);
        LevelManager.Instance.inDialogue = true;  //freezeo a kami
        PUBManager.Instance.ClosePUBs();
        CreateHoja(_isNext); //instancio la hoja que corresponda
        StartPlayerRide();
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
        CameraManager.Instance.SetCamera(CameraMode.Normal);
        Destroy(hojaAux);
    } //este se dispara cuando la hoja termina de girar y avisa "che ya termine de girar" a traves el evento onpagefinishturnng
    void PlayPageSound()
    {
        if (_isNext)
        {
            AudioManager.instance.PlayByName("PageTurn01", 1f, 0.03f);
        }
        else
        {
            AudioManager.instance.PlayByName("PageTurn02", 1f, 0.03f);
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