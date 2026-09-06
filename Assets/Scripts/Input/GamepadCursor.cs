using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cursor virtual movido con el stick izquierdo, para que el minijuego de origami
/// (que nacio pensado para el mouse) se pueda jugar con joystick sin reescribirlo.
///
/// Se AUTOCONSTRUYE entero por codigo (GameObject + Canvas + Image + sprites): no hay
/// nada que arrastrar en el inspector de Unity. Es un requisito duro, no una comodidad:
/// tiene que andar solo al darle Play, sin tocar escenas ni prefabs.
///
/// La posicion que expone esta en PIXELES DE PANTALLA, la misma unidad que
/// Input.mousePosition, asi el codigo que consulta RectTransformUtility no se entera de
/// si el puntero lo mueve un mouse o un stick.
/// </summary>
public class GamepadCursor : MonoBehaviour
{
    /// <summary>
    /// Velocidad del cursor en pixeles por segundo. Criterio: a ~900 px/s cruza una
    /// pantalla de 1920 en poco mas de 2 segundos (y una de 1280 en ~1.4s). Mas rapido
    /// se vuelve imposible de frenar sobre la ruta del origami; mas lento aburre.
    /// </summary>
    const float VELOCIDAD_PX_POR_SEGUNDO = 900f;

    /// <summary>Lado del cursor en pixeles. Parecido al cursor de mouse del juego.</summary>
    const float TAMANIO_PX = 48f;

    /// <summary>Bien alto para dibujarse encima de todos los canvas del juego.</summary>
    const int ORDEN_DE_DIBUJO = 32000;

    /// <summary>Lado de la textura de fallback que generamos si no hay CursorManager.</summary>
    const int TAMANIO_TEXTURA_FALLBACK = 32;

    static GamepadCursor _instancia;

    /// <summary>
    /// Hay una instancia viva. Sirve para esconder el cursor sin FORZAR su creacion:
    /// pedir <see cref="Instancia"/> desde un camino de salida (destruir el trigger, salir
    /// del juego) crearia un GameObject al pedo, o peor, durante el shutdown.
    /// </summary>
    public static bool Existe => _instancia != null;

    /// <summary>
    /// La instancia unica. Si no existe se crea sola aca mismo, con su Canvas y todo.
    /// </summary>
    public static GamepadCursor Instancia
    {
        get
        {
            if (_instancia == null)
            {
                //Awake se ejecuta adentro de AddComponent y ya deja _instancia seteada
                GameObject go = new GameObject("GamepadCursor");
                DontDestroyOnLoad(go);
                go.AddComponent<GamepadCursor>();
            }
            return _instancia;
        }
    }

    Image _imagen;
    RectTransform _rectCursor;
    Sprite _spriteManoAbierta;
    Sprite _spriteManoCerrada;
    Vector2 _posicion;
    bool _visible;
    bool _spritesListos;

    /// <summary>Posicion actual del cursor, en pixeles de pantalla (igual que Input.mousePosition).</summary>
    public Vector2 Posicion => _posicion;

    public bool EstaVisible => _visible;

    void Awake()
    {
        if (_instancia != null && _instancia != this)
        {
            Debug.LogWarning("[GamepadCursor] ya habia una instancia viva, destruyo la duplicada");
            Destroy(gameObject);
            return;
        }

        _instancia = this;
        ConstruirCanvas();
    }

    void OnDestroy()
    {
        if (_instancia == this)
        {
            _instancia = null;
        }
    }

    void Update()
    {
        if (!_visible || _rectCursor == null)
        {
            return;
        }

        //unscaled: el minijuego de origami puede correr con el juego pausado o ralentizado,
        //y el cursor tiene que seguir respondiendo igual
        _posicion += InputHub.StickIzquierdo * (VELOCIDAD_PX_POR_SEGUNDO * Time.unscaledDeltaTime);
        ClampearAPantalla();
        _rectCursor.anchoredPosition = _posicion;
    }

    /// <summary>Prende el cursor y lo planta en esa posicion de pantalla.</summary>
    public void Mostrar(Vector2 posicionInicialEnPantalla)
    {
        AsegurarSprites();

        _posicion = posicionInicialEnPantalla;
        ClampearAPantalla();

        if (_rectCursor != null)
        {
            _rectCursor.anchoredPosition = _posicion;
        }

        SetAgarrando(false);

        if (_imagen != null)
        {
            _imagen.enabled = true;
        }

        _visible = true;
        Debug.Log($"[GamepadCursor] cursor visible en {_posicion}");
    }

    public void Esconder()
    {
        if (!_visible)
        {
            return;
        }

        _visible = false;

        if (_imagen != null)
        {
            _imagen.enabled = false;
        }

        Debug.Log("[GamepadCursor] cursor escondido");
    }

    /// <summary>Mano cerrada mientras arrastra, mano abierta cuando esta suelto.</summary>
    public void SetAgarrando(bool agarrando)
    {
        AsegurarSprites();

        if (_imagen == null)
        {
            return;
        }

        _imagen.sprite = agarrando ? _spriteManoCerrada : _spriteManoAbierta;
    }

    void ClampearAPantalla()
    {
        _posicion.x = Mathf.Clamp(_posicion.x, 0f, Screen.width);
        _posicion.y = Mathf.Clamp(_posicion.y, 0f, Screen.height);
    }

    // ------------------------------------------------------------- construccion

    void ConstruirCanvas()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ORDEN_DE_DIBUJO;

        //ConstantPixelSize a escala 1: asi anchoredPosition ES la posicion en pixeles de
        //pantalla y no hay que convertir nada entre lo que dibujamos y lo que reportamos
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        //a proposito NO agregamos GraphicRaycaster: el cursor no tiene que interceptar clicks

        GameObject goImagen = new GameObject("Cursor");
        goImagen.transform.SetParent(transform, false);

        _imagen = goImagen.AddComponent<Image>();
        _imagen.raycastTarget = false;
        _imagen.enabled = false;

        _rectCursor = _imagen.rectTransform;
        //anclado a la esquina inferior izquierda para que anchoredPosition sea directamente
        //la coordenada de pantalla; pivot arriba a la izquierda porque el cursor de mouse del
        //juego usa hotspot (0,0) = esquina superior izquierda de la textura, y asi la "punta"
        //de la mano cae en el mismo lugar en los dos modos
        _rectCursor.anchorMin = Vector2.zero;
        _rectCursor.anchorMax = Vector2.zero;
        _rectCursor.pivot = new Vector2(0f, 1f);
        _rectCursor.sizeDelta = new Vector2(TAMANIO_PX, TAMANIO_PX);
        _rectCursor.anchoredPosition = Vector2.zero;
    }

    void AsegurarSprites()
    {
        if (_spritesListos)
        {
            return;
        }

        //reusamos las texturas del cursor de mouse para que el cursor de joystick se vea igual
        Texture2D abierta = CursorManager.Instance != null ? CursorManager.Instance.openHand : null;
        Texture2D cerrada = CursorManager.Instance != null ? CursorManager.Instance.closedHand : null;

        if (abierta == null || cerrada == null)
        {
            Debug.LogWarning("[GamepadCursor] no encontre las texturas de CursorManager (openHand/closedHand), " +
                             "uso un cursor generado por codigo. El minijuego funciona igual.");
            Texture2D fallback = CrearTexturaFallback();
            abierta = abierta != null ? abierta : fallback;
            cerrada = cerrada != null ? cerrada : fallback;
        }

        _spriteManoAbierta = CrearSprite(abierta);
        _spriteManoCerrada = CrearSprite(cerrada);
        _spritesListos = true;

        if (_imagen != null && _imagen.sprite == null)
        {
            _imagen.sprite = _spriteManoAbierta;
        }
    }

    static Sprite CrearSprite(Texture2D textura)
    {
        return Sprite.Create(textura,
                             new Rect(0f, 0f, textura.width, textura.height),
                             new Vector2(0.5f, 0.5f));
    }

    // Anillo blanco con un puntito en el centro. Feo pero visible sobre cualquier fondo, y
    // sobre todo: no depende de ningun asset que haya que asignar a mano.
    static Texture2D CrearTexturaFallback()
    {
        int lado = TAMANIO_TEXTURA_FALLBACK;
        Texture2D textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        textura.filterMode = FilterMode.Bilinear;
        textura.wrapMode = TextureWrapMode.Clamp;

        float centro = (lado - 1) * 0.5f;
        float radioExterior = centro - 1f;
        float radioInterior = radioExterior - 4f;
        Color[] pixeles = new Color[lado * lado];

        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float distancia = Vector2.Distance(new Vector2(x, y), new Vector2(centro, centro));
                Color color = Color.clear;

                if (distancia <= 2f)
                {
                    color = Color.white;
                }
                else if (distancia >= radioInterior && distancia <= radioExterior)
                {
                    color = Color.white;
                }

                pixeles[(y * lado) + x] = color;
            }
        }

        textura.SetPixels(pixeles);
        textura.Apply();
        return textura;
    }
}
