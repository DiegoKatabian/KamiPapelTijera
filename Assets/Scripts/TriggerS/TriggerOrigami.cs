using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerOrigami : TriggerScript
{
    //cuando entras a este trigger hace nacer al origamicheck
    //el origami check se va a encargar de chequear que apretes tab y hagas el origami

    //para cargar en el inspector
    public Origami origami;
    public MultipleRectCheck checkPrefab;
    [SerializeField] GameObject particleSystemGO;

    [Tooltip("Canvas del pedestal que muestra el costo de papel; si queda vacio se busca solo en los hijos del padre")]
    [SerializeField] PedestalCanvasDisplay _canvasDisplay;

    [Header("Particle Parameters When Step On")]
    public Color blueParticleActiveColor;
    public float activeSpeed = 1.5f;
    public float activeSize = 2f;
    public float orbitalZ = 2f;

    //auxiliares
    MultipleRectCheck currentCheck;
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
                currentCheck = Instantiate(checkPrefab).SetOrigami(origami);
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
