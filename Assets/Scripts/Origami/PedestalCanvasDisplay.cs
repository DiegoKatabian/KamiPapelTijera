using System.Collections;
using TMPro;
using UnityEngine;

public class PedestalCanvasDisplay : MonoBehaviour
{
    //va montado en el GameObject "Canvas" hijo de PedestalParent
    //muestra el costo de papel del origami cuando el player pisa el trigger del pedestal
    //el canvas queda SIEMPRE activo: la visibilidad se maneja solo por alpha del CanvasGroup (nada de SetActive)

    [SerializeField] CanvasGroup _canvasGroup; //fallback: GetComponent en Awake
    [SerializeField] TMPro.TextMeshProUGUI _costText; //el texto del costo
    [SerializeField] TriggerOrigami _triggerOrigami; //para chequear origami.wasUsed y el estado del trigger

    [Tooltip("Duracion en segundos del fade-in del canvas de costo")]
    [SerializeField] float _fadeInDuration = 0.3f;

    [Tooltip("Duracion en segundos del fade-out del canvas de costo")]
    [SerializeField] float _fadeOutDuration = 0.3f;

    //auxiliares
    //flag que dice si el player esta parado en ESTE pedestal: los eventos OnOrigamiStart/End son globales
    //(los reciben todos los pedestales del nivel), asi que este flag evita que el pedestal equivocado haga fade-in
    bool _playerOnPedestal;
    Coroutine _fadeCoroutine; //referencia al fade en curso, para frenarlo antes de arrancar uno nuevo (no usamos StopAllCoroutines)

    //nombre del pedestal padre, para debuguear cuando hay varios pedestales en el nivel
    string PedestalName => transform.parent != null ? transform.parent.name : gameObject.name;

    void Awake()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_canvasGroup == null)
        {
            Debug.LogWarning($"[PedestalCanvasDisplay] {PedestalName}: no hay CanvasGroup asignado ni en el GameObject, los fades no van a funcionar");
        }
        else
        {
            //arranca invisible: el costo se muestra recien cuando el player pisa el trigger
            _canvasGroup.alpha = 0f;
        }
    }

    void Start()
    {
        //fallback por si el prefab no wireo _triggerOrigami: el TriggerOrigami vive como hermano de
        //PedestalParent bajo la raiz del sello, asi que subimos dos niveles y buscamos desde ahi
        if (_triggerOrigami == null)
        {
            Transform selloRoot = transform.parent != null ? transform.parent.parent : null;
            if (selloRoot != null)
            {
                _triggerOrigami = selloRoot.GetComponentInChildren<TriggerOrigami>(true);
            }

            if (_triggerOrigami == null)
            {
                Debug.LogWarning($"[PedestalCanvasDisplay] {PedestalName}: _triggerOrigami no esta asignado y no lo encontre buscando desde la raiz del sello; el costo no se va a re-mostrar despues de fallar un origami");
            }
            else
            {
                Debug.LogWarning($"[PedestalCanvasDisplay] {PedestalName}: _triggerOrigami no estaba asignado en el inspector, lo encontre por fallback en {_triggerOrigami.gameObject.name}");
            }
        }

        //mismo patron que PedestalColorCheck: Subscribe en Start, Unsubscribe en OnDestroy con guard de escena
        EventManager.Subscribe(Evento.OnOrigamiStart, HandleOrigamiStart);
        EventManager.Subscribe(Evento.OnOrigamiEnd, HandleOrigamiEnd);
    }

    void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnOrigamiStart, HandleOrigamiStart);
            EventManager.Unsubscribe(Evento.OnOrigamiEnd, HandleOrigamiEnd);
        }
    }

    public void ShowCost(int paperCost)
    {
        //canvas apagado por diseño (ej: los sellos de la abuela no muestran costo): no es un error,
        //pero sin este guard StartCoroutine explotaria sobre un GameObject inactivo
        if (!gameObject.activeInHierarchy)
        {
            Debug.Log($"[PedestalCanvasDisplay] {PedestalName}: canvas desactivado por diseño, ignoro ShowCost({paperCost})");
            return;
        }

        //lo llama TriggerOrigami al entrar el player: setea el texto y hace fade-in
        _playerOnPedestal = true;

        if (_costText == null)
        {
            Debug.LogWarning($"[PedestalCanvasDisplay] {PedestalName}: falta la referencia a _costText, no puedo mostrar el numero de costo");
        }
        else
        {
            _costText.text = paperCost.ToString();
        }

        Debug.Log($"[PedestalCanvasDisplay] {PedestalName}: ShowCost({paperCost}) -> fade-in");
        StartFade(1f, _fadeInDuration);
    }

    public void Hide()
    {
        //mismo guard que ShowCost: canvas apagado por diseño = no hay nada que esconder
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        //OnPlayerDie fuerza el exit de TODOS los TriggerOrigami del nivel: si el player no estaba
        //en este pedestal y el canvas ya esta invisible, no hay nada que esconder (evita ~12 logs
        //y coroutines inutiles por cada muerte)
        if (!_playerOnPedestal && _fadeCoroutine == null && _canvasGroup != null && _canvasGroup.alpha <= 0f)
        {
            return;
        }

        //lo llama TriggerOrigami al salir el player: fade-out
        _playerOnPedestal = false;

        Debug.Log($"[PedestalCanvasDisplay] {PedestalName}: Hide -> fade-out");
        StartFade(0f, _fadeOutDuration);
    }

    void HandleOrigamiStart(params object[] parameters)
    {
        //evento global: solo reacciona el pedestal donde esta parado el player
        //ojo: NO tocamos _playerOnPedestal aca, porque el player sigue fisicamente en el trigger
        if (!_playerOnPedestal)
        {
            return;
        }

        Debug.Log($"[PedestalCanvasDisplay] {PedestalName}: OnOrigamiStart -> fade-out mientras dura el minijuego");
        StartFade(0f, _fadeOutDuration);
    }

    void HandleOrigamiEnd(params object[] parameters)
    {
        //evento global: re-mostramos el costo solo si el player sigue en ESTE pedestal
        if (!_playerOnPedestal)
        {
            return;
        }

        if (_triggerOrigami == null)
        {
            Debug.LogWarning($"[PedestalCanvasDisplay] {PedestalName}: falta la referencia a _triggerOrigami, no puedo chequear wasUsed en OnOrigamiEnd");
            return;
        }

        if (_triggerOrigami.origami == null)
        {
            Debug.LogWarning($"[PedestalCanvasDisplay] {PedestalName}: _triggerOrigami no tiene origami asignado, no puedo chequear wasUsed en OnOrigamiEnd");
            return;
        }

        //si el origami ya fue usado no tiene sentido volver a mostrar el costo
        if (_triggerOrigami.origami.wasUsed)
        {
            Debug.Log($"[PedestalCanvasDisplay] {PedestalName}: OnOrigamiEnd pero el origami ya fue usado -> no re-muestro el costo");
            return;
        }

        Debug.Log($"[PedestalCanvasDisplay] {PedestalName}: OnOrigamiEnd con player todavia en el pedestal -> re-muestro el costo");
        ShowCost(_triggerOrigami.origami.paperCost);
    }

    void StartFade(float targetAlpha, float duration)
    {
        if (_canvasGroup == null)
        {
            Debug.LogWarning($"[PedestalCanvasDisplay] {PedestalName}: no hay CanvasGroup, ignoro el fade hacia alpha {targetAlpha}");
            return;
        }

        //frenamos el fade anterior (si habia) antes de arrancar el nuevo, para que no peleen entre si
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }

        _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
    }

    IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        //partimos del alpha ACTUAL: si interrumpen un fade a mitad de camino, el nuevo arranca desde donde quedo (sin saltos)
        float startAlpha = _canvasGroup.alpha;

        if (duration <= 0f)
        {
            //con duracion cero (o negativa por error de tuneo) aplicamos directo, sin dividir por cero
            _canvasGroup.alpha = targetAlpha;
            _fadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        _fadeCoroutine = null;
    }
}
