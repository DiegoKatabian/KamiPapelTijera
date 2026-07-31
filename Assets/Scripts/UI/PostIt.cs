using System.Collections;
using UnityEngine;
using TMPro;

public class PostIt : MonoBehaviour
{
    //un post-it de tooltip puesto a dedo en la escena.
    //maneja SU PROPIO ciclo de vida: fade-in, timer de muerte y fade-out.
    //el TooltipManager solo orquesta (elige color, localiza el texto y llama Show/Hide).

    //IMPORTANTE: el GameObject queda SIEMPRE activo una vez que el manager lo prendio:
    //el "apagado" es alpha 0 en el CanvasGroup. Si lo desactivaramos con SetActive(false),
    //se matarian las corrutinas (timer de muerte y fades) y el post-it quedaria en un estado roto.

    [SerializeField]
    public TextMeshProUGUI tmPro; //el TooltipManager escribe el texto localizado aca

    [Header("Fades")]
    [SerializeField, Tooltip("Segundos que tarda el fade-in al aparecer")]
    float _fadeInDuration = 0.25f;

    [SerializeField, Tooltip("Segundos que tarda el fade-out al esconderse")]
    float _fadeOutDuration = 0.4f;

    [Header("Cierre por input del player")]
    [SerializeField, Tooltip("Si esta prendido, el post-it se cierra cuando el player ataca (clic izquierdo / Ctrl izquierdo)")]
    bool _dismissOnAttack;

    [SerializeField, Tooltip("Si esta prendido, el post-it se cierra cuando el player interactua (E)")]
    bool _dismissOnInteract;

    [SerializeField, Tooltip("Si esta prendido, el post-it se cierra cuando el player salta (Espacio)")]
    bool _dismissOnJump;

    [Header("Compat: manejado desde afuera")]
    [SerializeField, Tooltip("Si esta prendido, el post-it se muestra solo al activarse su GameObject y se resetea al desactivarlo, sin timer de muerte. Lo usa TOOLTIP PAPER SALTO, que Player.cs prende y apaga con SetActive directo sin pasar por el TooltipManager")]
    bool _showOnEnable;

    CanvasGroup _canvasGroup;

    //guardamos las referencias por separado: el fade y el timer de muerte COEXISTEN
    //(mientras el post-it hace fade-in, su timer ya esta corriendo). Por eso nunca
    //usamos StopAllCoroutines: frenaria al otro sin querer.
    Coroutine _fadeCoroutine;
    Coroutine _deathTimerCoroutine;

    bool _isVisible;

    public bool IsVisible
    {
        get { return _isVisible; }
    }

    void Awake()
    {
        //los post-its estan puestos a dedo en la escena y pueden no tener CanvasGroup:
        //lo agregamos en runtime para no exigir cirugia de escena
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            Debug.LogWarning($"[PostIt] {gameObject.name}: no tenia CanvasGroup, lo agrego en runtime (conviene agregarlo en la escena para poder tunearlo)");
        }

        //arranca invisible. blocksRaycasts/interactable quedan false SIEMPRE:
        //los post-its son decorativos y no deben comerse clics del juego
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    void Start()
    {
        //suscripcion en Start (y no OnEnable) porque el GO queda siempre activo:
        //no hay ciclos de enable/disable que emparejar
        EventManager.Subscribe(Evento.OnPlayerPressedE, OnPlayerInteract);
        EventManager.Subscribe(Evento.OnPlayerPressedSpace, OnPlayerJump);
    }

    void OnEnable()
    {
        //compat con post-its que otro script prende a mano con SetActive (ej. TOOLTIP PAPER
        //SALTO, que Player.cs activa al agarrar el sombrero avioncito): sin esto quedarian
        //en alpha 0 para siempre porque nadie les llama Show(). Se muestran sin timer de
        //muerte: los apaga el mismo SetActive(false) de quien los prendio
        if (!_showOnEnable)
        {
            return;
        }

        if (_canvasGroup == null)
        {
            //Awake corre antes que OnEnable en la primera activacion: esto no deberia pasar
            Debug.LogWarning($"[PostIt] {gameObject.name}: OnEnable sin CanvasGroup, no me puedo mostrar");
            return;
        }

        Debug.Log($"[PostIt] {gameObject.name}: activado desde afuera (showOnEnable), me muestro sin timer de muerte");
        _isVisible = true;
        RestartCoroutine(ref _fadeCoroutine, FadeCoroutine(1f, _fadeInDuration, false));
    }

    void OnDisable()
    {
        //si alguien desactiva el GameObject, Unity ya mato nuestras corrutinas: limpiamos
        //los handles para no frenar corrutinas muertas en el proximo Show/Hide, y reseteamos
        //el alpha para que la proxima activacion arranque invisible y haga su fade-in entero.
        //en el flujo normal (post-its del TooltipManager) esto no corre nunca: quedan activos
        _isVisible = false;
        _fadeCoroutine = null;
        _deathTimerCoroutine = null;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }
    }

    void OnDestroy()
    {
        //mismo guard que el resto del repo: solo desuscribimos si se esta descargando la escena
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPlayerPressedE, OnPlayerInteract);
            EventManager.Unsubscribe(Evento.OnPlayerPressedSpace, OnPlayerJump);
        }
    }

    void Update()
    {
        //el ataque NO tiene evento utilizable: Evento.OnPlayerPrimaryClick existe en el enum
        //pero nadie lo triggerea (PlayerController llama Player.OnPrimaryClick() directo).
        //Por eso leemos el boton "Fire1" nosotros: en el Input Manager cubre clic izquierdo
        //y Ctrl izquierdo, que es exactamente el input de ataque del juego.
        if (_dismissOnAttack && _isVisible && Input.GetButtonDown("Fire1"))
        {
            //respetamos el mismo gate de agency que usa PlayerController para sus eventos
            if (LevelManager.Instance != null && !LevelManager.Instance.agency)
            {
                return;
            }

            Debug.Log($"[PostIt] {gameObject.name}: cerrado por input de ataque (Fire1)");
            Hide();
        }
    }

    //muestra el post-it con fade-in y (re)arranca su timer de muerte.
    //si ya estaba visible no hay parpadeo: el fade interpola desde el alpha actual
    //(si ya esta en 1, el fade termina al instante) y el timer simplemente se reinicia.
    public void Show(float killTime)
    {
        if (_canvasGroup == null)
        {
            Debug.LogWarning($"[PostIt] {gameObject.name}: Show llamado antes de Awake, lo ignoro");
            return;
        }

        //StartCoroutine sobre un GO inactivo tira excepcion: mejor un warning que dice QUIEN es
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[PostIt] {gameObject.name}: Show con el GameObject (o un padre) inactivo, no puedo arrancar corrutinas. Revisa la jerarquia del canvas");
            return;
        }

        _isVisible = true;

        RestartCoroutine(ref _fadeCoroutine, FadeCoroutine(1f, _fadeInDuration, false));
        RestartCoroutine(ref _deathTimerCoroutine, DeathTimerCoroutine(killTime));
    }

    //esconde el post-it con fade-out; al terminar el fade limpia el texto.
    //llamarlo sobre un post-it ya escondido es un no-op (asi HideTooltip() del manager
    //puede barrer todos los colores sin spamear logs ni fades)
    public void Hide()
    {
        if (!_isVisible)
        {
            return;
        }

        _isVisible = false;

        //el timer de muerte ya no tiene sentido si nos estamos escondiendo
        if (_deathTimerCoroutine != null)
        {
            StopCoroutine(_deathTimerCoroutine);
            _deathTimerCoroutine = null;
        }

        RestartCoroutine(ref _fadeCoroutine, FadeCoroutine(0f, _fadeOutDuration, true));
    }

    void OnPlayerInteract(params object[] parameters)
    {
        if (_dismissOnInteract && _isVisible)
        {
            Debug.Log($"[PostIt] {gameObject.name}: cerrado por input E");
            Hide();
        }
    }

    void OnPlayerJump(params object[] parameters)
    {
        if (_dismissOnJump && _isVisible)
        {
            Debug.Log($"[PostIt] {gameObject.name}: cerrado por input de salto (Espacio)");
            Hide();
        }
    }

    //frena la corrutina anterior (si la hay) y arranca la nueva, guardando la referencia.
    //NUNCA StopAllCoroutines: el fade y el timer de muerte conviven y se pisarian entre si
    void RestartCoroutine(ref Coroutine handle, IEnumerator routine)
    {
        if (handle != null)
        {
            StopCoroutine(handle);
        }

        handle = StartCoroutine(routine);
    }

    IEnumerator DeathTimerCoroutine(float killTime)
    {
        yield return new WaitForSeconds(killTime);

        Debug.Log($"[PostIt] {gameObject.name}: se cumplio su killTime de {killTime}s, lo escondo");
        _deathTimerCoroutine = null;
        Hide();
    }

    IEnumerator FadeCoroutine(float targetAlpha, float fullDuration, bool clearTextAtEnd)
    {
        float startAlpha = _canvasGroup.alpha;

        //la duracion es proporcional a la distancia que falta recorrer: si un fade interrumpe
        //a otro por la mitad, no queremos que el tramo corto dure lo mismo que uno entero
        float duration = fullDuration * Mathf.Abs(targetAlpha - startAlpha);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;

        if (clearTextAtEnd)
        {
            //el texto se limpia recien al FINAL del fade-out para que no desaparezca de golpe
            if (tmPro != null)
            {
                tmPro.text = "";
            }
            else
            {
                Debug.LogWarning($"[PostIt] {gameObject.name}: tmPro no esta asignado, no puedo limpiar el texto");
            }
        }

        _fadeCoroutine = null;
    }
}
