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
        TryShowTooltip();
    }

    public virtual void OnExitBehaviour()
    {
        //print("se salio el player de " + gameObject.name);
        triggerBool = false;

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

        //escondemos SOLO nuestro color: antes se escondian todos y el exit de un trigger
        //pisaba post-its ajenos que seguian vivos
        TooltipManager.Instance.HideTooltip(postItColor);
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
        TooltipManager.Instance.ShowTooltip(tooltipTextToShow, postItColor);
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

    protected virtual void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, Interact);
        }
    }
}
