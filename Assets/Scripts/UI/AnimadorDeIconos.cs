using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>
/// Anima los iconos de boton que aparecen inline en los textos (los tags
/// <c>&lt;sprite name="btn_a_0"&gt;</c>) para que "respiren" en loop, sin volver a maquetar
/// el texto.
///
/// Por que NO regeneramos el texto: cambiar el string (SetText / ForceMeshUpdate) obliga a
/// TMP a rehacer word-wrap y conteo de caracteres. Eso pelearia con el efecto maquina de
/// escribir de los dialogos (que revela moviendo maxVisibleCharacters) y costaria caro por
/// cuadro. En vez de eso reescribimos SOLO las UV del quad del icono sobre la malla que TMP
/// ya genero, que es el camino sancionado por TMP para efectos sobre glifos (es lo que hacen
/// los ejemplos oficiales tipo VertexColorCycler, pero sobre colores en vez de UVs).
///
/// Cada icono vive en el sprite asset como N sprites consecutivos <c>base_0</c>, <c>base_1</c>,
/// ... Animar = apuntar las UV del quad al cuadro que toca.
///
/// Se autoconstruye: la primera vez que alguien registra un texto se crea el GameObject host
/// que tiene el Update. Diego no tiene que agregar nada a ninguna escena (mismo patron que
/// <see cref="GamepadCursor"/>).
/// </summary>
public static class AnimadorDeIconos
{
    // --------------------------------------------------------------- constantes

    /// <summary>
    /// Cuadros por segundo de la respiracion. A 60fps reescribiriamos las UV de una animacion
    /// de 4 cuadros diez veces mas seguido de lo que se ve: puro desperdicio de CPU y de
    /// subidas de malla a la GPU.
    /// </summary>
    const float FPS_ANIMACION = 6f;

    /// <summary>
    /// Techo de cuadros que buscamos por icono. Es una red de seguridad para el barrido
    /// <c>base_0</c>, <c>base_1</c>... por si un sprite asset generado a mano tuviera cientos
    /// de nombres consecutivos: no queremos que armar el cache se vaya de mano.
    /// </summary>
    const int MAX_CUADROS = 32;

    // ------------------------------------------------------------------- estado

    /// <summary>Kill switch. Si Diego ve algo raro, se apaga y los iconos quedan quietos.</summary>
    public static bool Activo { get; set; } = true;

    //Los textos que estamos animando. Diccionario/HashSet de componentes es la convencion de
    //la casa (ver LocalizedText._crudos): el "fake null" de Unity se limpia barriendo.
    static readonly HashSet<TMP_Text> _registrados = new HashSet<TMP_Text>();

    //Buffer reusado: no se puede sacar del HashSet mientras se lo recorre.
    static readonly List<TMP_Text> _muertos = new List<TMP_Text>();

    //Cache: el TMP_SpriteCharacter que quedo escrito en el texto -> los cuadros de SU animacion.
    //La clave es estable porque nosotros NUNCA tocamos charInfo.textElement (solo UVs), asi que
    //aunque el icono se vea en el cuadro 2, el textElement sigue siendo el que puso el tag.
    //Un valor null significa "ya lo mire y no es animable" (cache negativo, evita re-parsear
    //el nombre y re-barrer el asset en cada tick).
    static readonly Dictionary<TMP_SpriteCharacter, InfoAnimacion> _animaciones =
        new Dictionary<TMP_SpriteCharacter, InfoAnimacion>();

    //Cuadro global de la animacion. Es GLOBAL a proposito: como no tocamos textElement, no hay
    //forma de leer "en que cuadro estaba" desde la malla, asi que la fase la lleva el animador.
    //Efecto secundario deseado: todos los iconos respiran sincronizados entre si.
    static int _contador;

    static float _proximoTick;

    static HostDeAnimacion _host;

    // ---------------------------------------------------------------------- API

    /// <summary>
    /// Empieza a animar los sprites de este texto. Idempotente: registrar dos veces no duplica.
    /// Si el texto no tiene ningun sprite, no lo registra (no gastamos ticks al pedo).
    /// </summary>
    public static void Registrar(TMP_Text destino)
    {
        if (destino == null)
        {
            Debug.LogWarning("[AnimadorDeIconos] me pidieron registrar un TMP_Text null");
            return;
        }

        //Filtro por el STRING y no por la malla porque el call site normalmente registra justo
        //despues de escribir el texto, y TMP todavia no genero nada (la generacion cae mas
        //tarde en el frame, via TMP_UpdateManager). Preguntarle al textInfo aca daria 0
        //caracteres siempre y no registrariamos nunca.
        if (!TieneTagDeSprite(destino.text))
        {
            return;
        }

        AsegurarHost();
        _registrados.Add(destino);
    }

    /// <summary>Lo saca del registro. Segura con null y con objetos ya destruidos.</summary>
    public static void Desregistrar(TMP_Text destino)
    {
        if (destino == null)
        {
            //Ojo: un objeto DESTRUIDO tambien entra aca (fake null de Unity), y su entrada
            //quedaria en el registro. No es una fuga: la barrida del tick limpia los muertos.
            return;
        }

        _registrados.Remove(destino);
    }

    // ------------------------------------------------------------------- el tick

    /// <summary>Lo llama el host una vez por frame; adentro se auto-limita a FPS_ANIMACION.</summary>
    static void Tick()
    {
        if (!Activo || _registrados.Count == 0)
        {
            return;
        }

        //unscaled: el menu Flap pausa el juego con Time.timeScale = 0 y ahi es JUSTO donde se
        //ven los iconos de la pantalla de controles. Con tiempo escalado quedarian congelados.
        if (Time.unscaledTime < _proximoTick)
        {
            return;
        }
        _proximoTick = Time.unscaledTime + (1f / FPS_ANIMACION);
        _contador++;

        _muertos.Clear();

        foreach (TMP_Text texto in _registrados)
        {
            //"fake null": el componente fue destruido pero la referencia sigue viva
            if (texto == null)
            {
                _muertos.Add(texto);
                continue;
            }

            //media UI del juego (Flap, overlays) esta apagada casi todo el tiempo: escribirle
            //UVs a una malla que nadie dibuja es tirar trabajo a la basura
            if (!texto.isActiveAndEnabled)
            {
                continue;
            }

            AnimarUnTexto(texto);
        }

        for (int i = 0; i < _muertos.Count; i++)
        {
            _registrados.Remove(_muertos[i]);
        }
        _muertos.Clear();
    }

    static void AnimarUnTexto(TMP_Text texto)
    {
        //releer textInfo en CADA tick: TMP lo regenera entero (el typewriter de los dialogos
        //mueve maxVisibleCharacters y eso rehace la malla), asi que cachear el array o los
        //indices entre ticks es garantia de escribir en memoria vieja
        TMP_TextInfo info = texto.textInfo;
        if (info == null || info.characterCount == 0)
        {
            return;
        }

        TMP_CharacterInfo[] caracteres = info.characterInfo;
        TMP_MeshInfo[] mallas = info.meshInfo;
        if (caracteres == null || mallas == null)
        {
            return;
        }

        //characterCount puede ser menor que el largo del array (TMP sobre-aloca y reusa)
        int cantidad = Mathf.Min(info.characterCount, caracteres.Length);
        bool escribimosAlgo = false;

        for (int i = 0; i < cantidad; i++)
        {
            TMP_CharacterInfo caracter = caracteres[i];

            if (caracter.elementType != TMP_TextElementType.Sprite || !caracter.isVisible)
            {
                continue;
            }

            InfoAnimacion animacion = ObtenerAnimacion(caracter, texto);
            if (animacion == null)
            {
                continue;
            }

            //el cuadro que el tag pidio es la FASE inicial; el contador global mueve la rueda
            int cuadro = (animacion.cuadroInicial + _contador) % animacion.cuadros.Length;
            if (EscribirUVs(mallas, caracter, animacion, cuadro))
            {
                escribimosAlgo = true;
            }
        }

        if (escribimosAlgo)
        {
            //una sola subida por texto (y no una por icono): adentro TMP recorre TODOS los
            //material index, asi que tambien re-sube la malla del sub-objeto donde viven los
            //sprites (ver TextMeshProUGUI.UpdateVertexData, que llama SetMesh en m_subTextObjects)
            texto.UpdateVertexData(TMP_VertexDataUpdateFlags.Uv0);
        }
    }

    /// <summary>
    /// Escribe las 4 UV del quad del icono. Devuelve false (sin explotar) si la malla esta en
    /// un estado intermedio: lo peor que puede pasar es que este tick no haga nada.
    /// </summary>
    static bool EscribirUVs(TMP_MeshInfo[] mallas, TMP_CharacterInfo caracter, InfoAnimacion animacion, int cuadro)
    {
        int indiceMalla = caracter.materialReferenceIndex;
        if (indiceMalla < 0 || indiceMalla >= mallas.Length)
        {
            return false;
        }

        Vector2[] uvs = mallas[indiceMalla].uvs0;
        int v = caracter.vertexIndex;
        if (uvs == null || v < 0 || v + 3 >= uvs.Length)
        {
            return false;
        }

        GlyphRect rect = animacion.cuadros[cuadro];

        //el glyphRect viene en PIXELES del atlas: normalizar dividiendo por el tamanio de la
        //textura. Mismo calculo que hace TMP al generar (TMP_Text.SaveSpriteVertexInfo).
        float ancho = animacion.anchoAtlas;
        float alto = animacion.altoAtlas;

        Vector2 abajoIzquierda = new Vector2(rect.x / ancho, rect.y / alto);
        Vector2 arribaIzquierda = new Vector2(abajoIzquierda.x, (rect.y + rect.height) / alto);
        Vector2 arribaDerecha = new Vector2((rect.x + rect.width) / ancho, arribaIzquierda.y);
        Vector2 abajoDerecha = new Vector2(arribaDerecha.x, abajoIzquierda.y);

        //ORDEN CRITICO: TMP escribe BL, TL, TR, BR en vertexIndex + 0..3
        //(TMP_Text.FillSpriteVertexBuffers). Invertirlo deja los iconos espejados o de cabeza.
        uvs[v + 0] = abajoIzquierda;
        uvs[v + 1] = arribaIzquierda;
        uvs[v + 2] = arribaDerecha;
        uvs[v + 3] = abajoDerecha;

        return true;
    }

    // ------------------------------------------------------- cache de animaciones

    /// <summary>Los cuadros de un icono, resueltos una sola vez por sprite del asset.</summary>
    class InfoAnimacion
    {
        public GlyphRect[] cuadros;
        public int cuadroInicial;
        public float anchoAtlas;
        public float altoAtlas;
    }

    /// <summary>
    /// Devuelve los cuadros de este icono, o null si no es animable (nombre sin sufijo
    /// numerico, un solo cuadro, atlas sin textura...). Cachea las dos respuestas.
    /// </summary>
    static InfoAnimacion ObtenerAnimacion(TMP_CharacterInfo caracter, TMP_Text texto)
    {
        TMP_SpriteCharacter sprite = caracter.textElement as TMP_SpriteCharacter;
        if (sprite == null)
        {
            return null;
        }

        InfoAnimacion cacheada;
        if (_animaciones.TryGetValue(sprite, out cacheada))
        {
            return cacheada;
        }

        InfoAnimacion armada = ArmarAnimacion(sprite, caracter, texto);
        _animaciones[sprite] = armada;
        return armada;
    }

    static InfoAnimacion ArmarAnimacion(TMP_SpriteCharacter sprite, TMP_CharacterInfo caracter, TMP_Text texto)
    {
        string nombre = sprite.name;
        if (string.IsNullOrEmpty(nombre))
        {
            return null;
        }

        int guion = nombre.LastIndexOf('_');
        if (guion <= 0 || guion == nombre.Length - 1)
        {
            //no tiene la forma "base_N": no es un icono animado, y no es un error
            return null;
        }

        int cuadroInicial;
        if (!int.TryParse(nombre.Substring(guion + 1), out cuadroInicial) || cuadroInicial < 0)
        {
            return null;
        }

        string baseDelNombre = nombre.Substring(0, guion);

        //charInfo.spriteAsset es el asset que TMP resolvio de verdad para ESTE caracter (ya
        //incluye fallbacks), asi que es mas confiable que texto.spriteAsset. Los otros dos son
        //red de seguridad por si quedara sin setear.
        TMP_SpriteAsset asset = caracter.spriteAsset;
        if (asset == null)
        {
            asset = sprite.textAsset as TMP_SpriteAsset;
        }
        if (asset == null)
        {
            asset = texto.spriteAsset;
        }
        if (asset == null)
        {
            Debug.LogWarning($"[AnimadorDeIconos] el sprite '{nombre}' no tiene sprite asset resoluble, no lo animo");
            return null;
        }

        Texture atlas = asset.spriteSheet;
        if (atlas == null && asset.material != null)
        {
            atlas = asset.material.mainTexture;
        }
        if (atlas == null || atlas.width == 0 || atlas.height == 0)
        {
            Debug.LogWarning($"[AnimadorDeIconos] el sprite asset '{asset.name}' no tiene textura de atlas, no puedo normalizar UVs");
            return null;
        }

        List<GlyphRect> cuadros = new List<GlyphRect>();
        for (int n = 0; n < MAX_CUADROS; n++)
        {
            int indice = asset.GetSpriteIndexFromName(baseDelNombre + "_" + n);
            if (indice < 0 || indice >= asset.spriteCharacterTable.Count)
            {
                break;
            }

            TMP_SpriteCharacter cuadro = asset.spriteCharacterTable[indice];
            TMP_SpriteGlyph glifo = cuadro != null ? cuadro.glyph as TMP_SpriteGlyph : null;
            if (glifo == null)
            {
                break;
            }

            cuadros.Add(glifo.glyphRect);
        }

        if (cuadros.Count < 2)
        {
            //un solo cuadro no es una animacion; cachear el null evita re-barrer el asset
            return null;
        }

        InfoAnimacion animacion = new InfoAnimacion();
        animacion.cuadros = cuadros.ToArray();
        //si el tag pidio un cuadro que no existe (btn_a_9 con 4 cuadros), arrancamos del 0 en
        //vez de tirar IndexOutOfRange en el modulo
        animacion.cuadroInicial = cuadroInicial < cuadros.Count ? cuadroInicial : 0;
        animacion.anchoAtlas = atlas.width;
        animacion.altoAtlas = atlas.height;

        Debug.Log($"[AnimadorDeIconos] icono '{baseDelNombre}' resuelto con {cuadros.Count} cuadros " +
                  $"(atlas '{asset.name}', {atlas.width}x{atlas.height})");

        return animacion;
    }

    // ------------------------------------------------------------------ el host

    static void AsegurarHost()
    {
        if (_host != null)
        {
            return;
        }

        GameObject go = new GameObject("AnimadorDeIconos");
        Object.DontDestroyOnLoad(go);
        //no ensucia la jerarquia ni se guarda en ninguna escena: es puro runtime
        go.hideFlags = HideFlags.HideAndDontSave;
        _host = go.AddComponent<HostDeAnimacion>();

        Debug.Log("[AnimadorDeIconos] host creado (autoconstruido, no hay que agregarlo a ninguna escena)");
    }

    /// <summary>
    /// La clase publica es estatica, pero para tener un Update hace falta un MonoBehaviour.
    /// Este es el unico motivo por el que existe.
    /// </summary>
    class HostDeAnimacion : MonoBehaviour
    {
        void Update()
        {
            Tick();
        }
    }

    // ------------------------------------------------------------------- utilidades

    static bool TieneTagDeSprite(string texto)
    {
        return !string.IsNullOrEmpty(texto) && texto.IndexOf("<sprite", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        //con "Enter Play Mode" sin domain reload los estaticos sobreviven entre corridas: sin
        //esto el registro arrancaria lleno de TMPs (y de sprites) de la sesion anterior
        _registrados.Clear();
        _muertos.Clear();
        _animaciones.Clear();
        _contador = 0;
        _proximoTick = 0f;
        _host = null;
        Activo = true;
    }
}
