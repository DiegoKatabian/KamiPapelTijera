using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerOrigami : TriggerScript
{
    //Interactuable: aca el boton de accion sirve para arrancar el minijuego de origami (lo escucha el MultipleRectCheck que instancia).
    public override bool EsInteractuable => true;

    //cuando entras a este trigger hace nacer al origamicheck
    //el origami check se va a encargar de chequear que apretes tab y hagas el origami

    //para cargar en el inspector
    public Origami origami;
    public MultipleRectCheck checkPrefab;
    [SerializeField] GameObject particleSystemGO;

    [Tooltip("Canvas del pedestal que muestra el costo de papel; si queda vacio se busca solo en los hijos del padre")]
    [SerializeField] PedestalCanvasDisplay _canvasDisplay;

    [Header("Auto prompt")]
    [Tooltip("Open the origami by itself as soon as this pedestal appears and nothing else is on screen (dialogue, cutscene, menu), without Kami stepping on it. Only once: if the player cancels, the pedestal keeps working the normal way (step on it + Interact). Off = the classic pedestal.")]
    [SerializeField] bool _promptAutomatically = false;

    [Tooltip("Seconds the screen has to stay free before the auto prompt opens. Keeps the button press that closed the previous dialogue from also cancelling the origami it opens.")]
    [SerializeField] float _autoPromptDelay = 0.5f;

    [Header("Particle Parameters When Step On")]
    public Color blueParticleActiveColor;
    public float activeSpeed = 1.5f;
    public float activeSize = 2f;
    public float orbitalZ = 2f;

    //auxiliares
    MultipleRectCheck currentCheck;
    bool _autoPromptDone;
    //the running check was opened by the auto prompt with Kami NOT on the pedestal: nobody's
    //OnExitBehaviour will clean it up, so we do it when the origami ends
    bool _autoCheckOwned;
    ParticleSystem ps;
    ParticleSystem.MainModule mainModule;
    ParticleSystem.VelocityOverLifetimeModule velocityModule;
    Color originalStartColor;
    float originalStartSpeed;
    float originalStartSize;
    float originalorbitalZ;

    protected override void Start()
    {
        base.Start();
        ps = particleSystemGO.GetComponent<ParticleSystem>();
        mainModule = ps.main;
        velocityModule = ps.velocityOverLifetime;

        originalStartColor = mainModule.startColor.color;
        originalStartSpeed = mainModule.startSpeed.constant;
        originalStartSize = mainModule.startSize.constant;
        originalorbitalZ = velocityModule.orbitalZ.constant;
        //originalEmissionRate = mainModule.emission.rateOverTime.constant;

        EventManager.Subscribe(Evento.OnPlayerDie, ForceTriggerExit);
        EventManager.Subscribe(Evento.OnOrigamiEnd, OnAnyOrigamiEnd);

        //fallback por si nadie asigno el canvas en el inspector: lo buscamos desde el padre (PedestalParent)
        if (_canvasDisplay == null)
        {
            if (transform.parent != null)
            {
                _canvasDisplay = transform.parent.GetComponentInChildren<PedestalCanvasDisplay>();
            }

            if (_canvasDisplay == null)
            {
                Debug.LogWarning($"[TriggerOrigami] {gameObject.name}: no encontre PedestalCanvasDisplay ni asignado ni en los hijos del padre, el costo no se va a mostrar");
            }
            else
            {
                Debug.LogWarning($"[TriggerOrigami] {gameObject.name}: _canvasDisplay no estaba asignado en el inspector, lo encontre por fallback en {_canvasDisplay.gameObject.name}");
            }
        }
    }

    protected override void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, Interact);
            EventManager.Unsubscribe(Evento.OnPlayerDie, ForceTriggerExit);
            EventManager.Unsubscribe(Evento.OnOrigamiEnd, OnAnyOrigamiEnd);
        }
    }

    private void ForceTriggerExit(object[] parameters)
    {
        OnExitBehaviour();
    }

    public override void OnEnterBehaviour(Collider other)
    {
        if (origami.wasUsed)
        {
            print("ya activaste este sello");
        }
        else
        {
            //mostramos el costo en AMBAS ramas (tenga o no papel suficiente): justamente cuando
            //no le alcanza es cuando mas le sirve al player ver cuanto papel necesita
            if (_canvasDisplay != null)
            {
                _canvasDisplay.ShowCost(origami.paperCost);
            }
            else
            {
                Debug.LogWarning($"[TriggerOrigami] {gameObject.name}: _canvasDisplay es null, no puedo mostrar el costo del origami");
            }

            if (LevelManager.Instance.recursosRecolectados[ResourceType.papel] >= origami.paperCost)
            {
                base.OnEnterBehaviour(other);
                //an auto-prompted check may already exist: Kami stepping on the pedestal adopts it
                //(from here on the normal exit cleans it up) instead of spawning a second one that
                //would answer the same Interact press
                if (currentCheck == null)
                {
                    currentCheck = Instantiate(checkPrefab).SetOrigami(origami);
                }
                _autoCheckOwned = false;
                //particulas y sonidito de entrar en la zona
                SetParticleParameters();
            }
            else
            {
                //print("no tenes suficiente papel para hacer este origami");
                TooltipManager.Instance.ShowTooltip("No tenes suficiente papel para hacer este origami", postItColor);
            }
        }
    }

    public override void OnExitBehaviour()
    {
        //print("on exit beh");

        //el canvas se esconde SIEMPRE al salir, incluso si currentCheck es null:
        //cuando el player entro sin papel suficiente no hay check pero el canvas de costo esta visible igual
        if (_canvasDisplay != null)
        {
            _canvasDisplay.Hide();
        }

        if (currentCheck != null)
        {
            base.OnExitBehaviour();
            currentCheck.EndOrigami(origami);
            Destroy(currentCheck.gameObject);
            //apagar particulas y sonidito de salir en la zona
            ChangeBackToOriginalParticleParameters();
        }
    }

    void OnEnable()
    {
        if (_promptAutomatically && !_autoPromptDone)
        {
            StartCoroutine(AutoPromptWhenFree());
        }
    }

    //runs every time the pedestal appears until it has prompted once; a page turn that hides the
    //pedestal mid-wait kills the coroutine, and the next OnEnable re-arms it
    IEnumerator AutoPromptWhenFree()
    {
        float freeFor = 0f;
        while (freeFor < _autoPromptDelay)
        {
            yield return null;
            freeFor = CanAutoPrompt() ? freeFor + Time.deltaTime : 0f;
        }

        _autoPromptDone = true;

        if (origami == null || checkPrefab == null)
        {
            Debug.LogWarning($"[TriggerOrigami] {gameObject.name}: auto prompt needs 'origami' and 'checkPrefab' assigned, skipping it");
            yield break;
        }

        if (origami.wasUsed)
        {
            yield break;
        }

        if (LevelManager.Instance.recursosRecolectados[ResourceType.papel] < origami.paperCost)
        {
            Debug.Log($"[TriggerOrigami] {gameObject.name}: not enough paper to auto prompt, the pedestal works the normal way");
            yield break;
        }

        Debug.Log($"[TriggerOrigami] {gameObject.name}: auto prompting the origami");

        if (currentCheck == null)
        {
            currentCheck = Instantiate(checkPrefab).SetOrigami(origami);
            _autoCheckOwned = !triggerBool;
        }

        currentCheck.StartOrigami(origami);
    }

    bool CanAutoPrompt()
    {
        if (LevelManager.Instance == null || LevelManager.Instance.inDialogue || LevelManager.Instance.inCutscene)
        {
            return false;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.isShowing)
        {
            return false;
        }

        if (OverlayManager.Instance != null && OverlayManager.Instance.isLocked)
        {
            return false;
        }

        if (FlapManager.Instance != null && FlapManager.Instance.IsMenuOpen)
        {
            return false;
        }

        Player player = LevelManager.Instance.player;
        if (player == null)
        {
            return false;
        }

        switch (player.CurrentState)
        {
            case PlayerState.Casting:
            case PlayerState.ReceivingReward:
            case PlayerState.Dead:
            case PlayerState.RidingPage:
                return false;
            default:
                return true;
        }
    }

    //an auto-prompted check with Kami off the pedestal would otherwise stay alive listening for
    //Interact, and reopen the origami from anywhere on the next press
    void OnAnyOrigamiEnd(params object[] parameters)
    {
        if (!_autoCheckOwned || currentCheck == null)
        {
            return;
        }

        _autoCheckOwned = false;
        Destroy(currentCheck.gameObject);
        currentCheck = null;
    }

    public void SetParticleParameters()
    {
        mainModule.startColor = blueParticleActiveColor;
        mainModule.startSpeed = originalStartSpeed * activeSpeed;
        mainModule.startSize = activeSize;
        velocityModule.orbitalZ = orbitalZ;
        //mainModule.emission.rateOverTime = originalEmissionRate * 2f;
    }

    public void ChangeBackToOriginalParticleParameters()
    {
        mainModule.startColor = originalStartColor;
        mainModule.startSpeed = originalStartSpeed;
        mainModule.startSize = originalStartSize;
        velocityModule.orbitalZ = originalorbitalZ;
        //mainModule.emission.rateOverTime = originalEmissionRate;
    }
}
