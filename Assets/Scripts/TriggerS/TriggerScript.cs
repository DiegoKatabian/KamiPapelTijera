using UnityEngine;

[RequireComponent(typeof(Collider))]
public abstract class TriggerScript : MonoBehaviour
{
    //cuando me entran, pongo true
    //cuando se salen, pongo false
    //casi todo lo q sea trigger q responda al player va a heredar de esto

    //este script se lo pones a un area o zona, por ejemplo.
    //y despues alguien pregunta por ella y su triggerbool

    //el player es la layer 3

    [HideInInspector] public bool triggerBool = false;

    [SerializeField] protected bool showTooltip = true;
    [SerializeField] protected string tooltipTextToShow;
    [SerializeField] protected PostItColor postItColor;

    [SerializeField, Tooltip("0 = sin limite. Cuantas veces maximo se muestra este tooltip al player")]
    int _maxTooltipShows = 0;

    int _tooltipShowCount = 0; //cuantas veces ya se mostro (no persiste entre sesiones)

    //si ESTA entrada al trigger llego a mostrar tooltip. sin esto, un trigger que no mostro nada
    //(limite agotado, showTooltip apagado) igual escondia su color al salir, matando el post-it
    //vivo de OTRO trigger del mismo color
    bool _shownThisEntry = false;

    //Si apretar el boton de accion parado adentro de este trigger HACE algo. Lo usa el joystick:
    //el boton B es contextual (interactua si hay algo, si no ataca), y para decidirlo consulta
    //InteractionContext, que se llena con los triggers que declaran esto en true.
    //Default false porque la mayoria de los triggers son de zona/tooltip y no responden al boton.
    public virtual bool EsInteractuable => false;

    protected virtual void Start()
    {
        //print("me suscribo a onplayerpressed E - triggerscript " + gameObject.name);
        EventManager.Subscribe(Evento.OnPlayerPressedE, Interact); //los triggers siempre estan atentos a que el player aprete E
    }

    protected virtual void OnTriggerEnter(Collider other) //cuando el player entra, se dispara el behaviour de entrar
    {
        if (other.gameObject.layer == 3) //layer 3 es el player
        {
            OnEnterBehaviour(other);
        }
    }

    protected virtual void OnTriggerExit(Collider other)//cuando el player sale, se dispara el behaviour de salir
    {
        if (other.gameObject.layer == 3)
        {
            OnExitBehaviour();
        }
    }

    public virtual void OnEnterBehaviour(Collider other)
    {
        //print("entro el player");
        triggerBool = true;

        if (EsInteractuable)
        {
            InteractionContext.Registrar(this);
        }

        TryShowTooltip();
    }

    public virtual void OnExitBehaviour()
    {
        //print("se salio el player de " + gameObject.name);
        triggerBool = false;

        //ojo: esto va ANTES del early return de abajo. si se desregistrara despues, un trigger
        //que no mostro tooltip se quedaria registrado para siempre y el boton B nunca atacaria
        InteractionContext.Desregistrar(this);

        if (!_shownThisEntry)
        {
            //esta entrada no mostro nada: no hay que esconder nada (y menos pisar el color de otro)
            return;
        }

        _shownThisEntry = false;

        if (TooltipManager.Instance == null)
        {
            //sin warning: al descargar la escena el manager puede morir antes que los triggers
            return;
        }

        //escondemos SOLO nuestro color Y SOLO si el post-it sigue siendo nuestro: hay un unico
        //PostIt por color y varios triggers comparten el mismo (la abuela y las dos esferas de
        //cambio de pagina usan todas el Azul), asi que sin pasar 'this' nuestro exit mataba el
        //tooltip que otro trigger acababa de mostrar
        TooltipManager.Instance.HideTooltip(postItColor, this);
    }

    //unico camino para mostrar el tooltip del trigger: encapsula el gate de showTooltip,
    //el limite de muestras y el show en si, asi las subclases no duplican la logica.
    //forceShow saltea el flag showTooltip (lo usa TriggerText, que existe justamente para
    //mostrar texto y siempre ignoro ese flag) pero NUNCA saltea el limite de muestras.
    //devuelve true si el tooltip efectivamente se mostro
    protected bool TryShowTooltip(bool forceShow = false)
    {
        if (!showTooltip && !forceShow)
        {
            return false;
        }

        if (TooltipManager.Instance == null)
        {
            Debug.LogWarning($"[TriggerScript] {gameObject.name}: no hay TooltipManager en la escena, no puedo mostrar el tooltip");
            return false;
        }

        int maxShows = GetMaxTooltipShows();
        if (maxShows > 0 && _tooltipShowCount >= maxShows)
        {
            Debug.Log($"[TriggerScript] {gameObject.name}: tooltip ya mostrado {_tooltipShowCount} veces (maximo {maxShows}), no lo muestro mas");
            return false;
        }

        _tooltipShowCount++;
        _shownThisEntry = true;
        //nos registramos como duenos del color: asi el exit de otro trigger que comparta
        //este mismo PostIt no puede escondernos el tooltip antes de tiempo
        TooltipManager.Instance.ShowTooltip(tooltipTextToShow, postItColor, this);
        return true;
    }

    //virtual para que las subclases puedan sumar sus propias reglas de limite
    //(ej. TriggerText combina su isOneTimeOnly legacy con este sistema)
    protected virtual int GetMaxTooltipShows()
    {
        return _maxTooltipShows;
    }

    public virtual void Interact(params object[] parameter) //interact se dispara cuando tocas E
    {
        //print("trigger script interact");
    }

    //Red de seguridad: hay triggers que se destruyen o se apagan con el player adentro (pickups
    //consumidos, cambio de pagina, SetActive(false) por quest). Sin esto quedarian registrados
    //como interaccion disponible para siempre y el boton B del joystick dejaria de atacar ahi.
    protected virtual void OnDisable()
    {
        InteractionContext.Desregistrar(this);
    }

    protected virtual void OnDestroy()
    {
        InteractionContext.Desregistrar(this);

        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, Interact);
        }
    }
}
