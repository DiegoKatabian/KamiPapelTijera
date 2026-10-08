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

    [Header("Code-driven")]
    [Tooltip("The pedestal is never stepped on: no cost canvas, no tooltip, no 'step on it + Interact'. Something else opens it by calling PromptNow() (Natalia's conversation on page 1: the dialogue is the only thing that answers the Interact press, so the origami can't compete with it). Combine with 'Prompt Automatically' for the first prompt.")]
    [SerializeField] bool _openOnlyByCode = false;

    [Tooltip("For a code-driven pedestal nobody re-prompts (e.g. the page 3 letter): when the player cancels the fold it opens again by itself once the screen is free. Without this a cancelled code-driven origami can never be reopened. Needs 'Open Only By Code'.")]
    [SerializeField] bool _repromptAfterCancel = false;

    [Header("Particle Parameters When Step On")]
    public Color blueParticleActiveColor;
    public float activeSpeed = 1.5f;
    public float activeSize = 2f;
    public float orbitalZ = 2f;

    //auxiliares
    MultipleRectCheck currentCheck;
    bool _autoPromptDone;
    bool _promptPending; //a PromptNow is waiting for the screen to be free
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
        if (_openOnlyByCode)
        {
            return;
        }

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

        //a code-driven origami is not tied to Kami standing here: leaving the pedestal must not end it
        if (_openOnlyByCode)
        {
            return;
        }

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
        _promptPending = false; //a coroutine killed by SetActive(false) never got to clear it
        if (_promptAutomatically && !_autoPromptDone)
        {
            StartCoroutine(AutoPromptWhenFree());
        }
    }

    //runs every time the pedestal appears until it has prompted once; a page turn that hides the
    //pedestal mid-wait kills the coroutine, and the next OnEnable re-arms it
    IEnumerator AutoPromptWhenFree()
    {
        yield return WaitUntilScreenIsFree();

        _autoPromptDone = true;
        OpenCheckFromCode();
    }

    /// <summary>
    /// Opens the origami once the screen is free, whatever happened before (the auto prompt already
    /// ran, the player cancelled). For the code that owns this pedestal, e.g. Natalia re-prompting the
    /// caf� fold after her reminder dialogue. A prompt that is already waiting is not stacked.
    /// </summary>
    public void PromptNow()
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning($"[TriggerOrigami] {gameObject.name}: PromptNow while the pedestal is inactive, ignoring it");
            return;
        }

        if (_promptPending)
        {
            return;
        }

        StartCoroutine(PromptNowRoutine());
    }

    IEnumerator PromptNowRoutine()
    {
        _promptPending = true;
        yield return WaitUntilScreenIsFree();
        _promptPending = false;
        OpenCheckFromCode();
    }

    IEnumerator WaitUntilScreenIsFree()
    {
        float freeFor = 0f;
        while (freeFor < _autoPromptDelay)
        {
            yield return null;
            freeFor = CanAutoPrompt() ? freeFor + Time.deltaTime : 0f;
        }
    }

    void OpenCheckFromCode()
    {
        if (origami == null || checkPrefab == null)
        {
            Debug.LogWarning($"[TriggerOrigami] {gameObject.name}: prompting needs 'origami' and 'checkPrefab' assigned, skipping it");
            return;
        }

        if (origami.wasUsed)
        {
            return;
        }

        if (LevelManager.Instance.recursosRecolectados[ResourceType.papel] < origami.paperCost)
        {
            Debug.Log($"[TriggerOrigami] {gameObject.name}: not enough paper to prompt, the pedestal works the normal way");
            return;
        }

        Debug.Log($"[TriggerOrigami] {gameObject.name}: prompting the origami");

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

        //the fold ended without finishing it (cancelled or failed): ask again
        if (_repromptAfterCancel && _openOnlyByCode && origami != null && !origami.wasUsed)
        {
            PromptNow();
        }
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
