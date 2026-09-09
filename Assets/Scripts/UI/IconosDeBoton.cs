using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>
/// Atlas de iconos de botones (joystick, teclas, mouse) para meter INLINE en cualquier
/// texto de TextMeshPro con <c>&lt;sprite name="btn_a_0"&gt;</c>.
///
/// Se AUTOCONSTRUYE entero por codigo: textura procedural + TMP_SpriteAsset + material,
/// mismo espiritu que <see cref="GamepadCursor"/>. Es a proposito: hoy no hay arte de
/// botones, y no queremos que nadie tenga que importar un atlas ni cablear un prefab para
/// que el juego se vea bien al darle Play. Cuando llegue el arte de verdad, esta clase se
/// reemplaza por un TMP_SpriteAsset importado y los call sites (que solo piden
/// <see cref="Tag"/>) no se enteran.
///
/// Cada icono tiene <see cref="FRAMES_POR_ICONO"/> frames de "respiracion" (late suave,
/// no parpadea). Los nombres siguen SIEMPRE el patron <c>&lt;base&gt;_&lt;frame&gt;</c>
/// porque el animador encuentra los frames buscando ese sufijo: no es cosmetico.
///
/// REGLA DURA DEL DIBUJO: los cuatro frames comparten celda y METRICAS de glifo. Lo unico
/// que cambia es el dibujo adentro. Si las metricas cambiaran por frame, TMP re-maquetaria
/// la linea en cada tick y el texto entero bailaria.
/// </summary>
public static class IconosDeBoton
{
    /// <summary>Cantidad de frames de animacion que tiene CADA icono. Todos tienen la misma.</summary>
    public const int FRAMES_POR_ICONO = 4;

    // ------------------------------------------------------------------ API

    /// <summary>
    /// El sprite asset, construido la primera vez que se lo pide y cacheado.
    /// Nunca devuelve null salvo catastrofe (en ese caso loguea y devuelve null).
    /// </summary>
    public static TMP_SpriteAsset Asset
    {
        get
        {
            if (!_construido)
            {
                Construir();
            }
            return _asset;
        }
    }

    /// <summary>
    /// Devuelve el tag listo para meter en un string de texto, apuntando al frame 0.
    /// Ej: <c>Tag("a")</c> =&gt; <c>&lt;sprite name="btn_a_0"&gt;</c>.
    /// Si el id no existe: loguea warning UNA vez y devuelve <see cref="string.Empty"/>.
    /// </summary>
    public static string Tag(string idIcono)
    {
        string prefijo = PrefijoDe(idIcono);
        if (prefijo == null)
        {
            //una sola vez por id: esto se llama desde el procesado de texto, que corre
            //muchisimas veces por partida. Un warning por frame taparia la consola
            if (_idsAvisados.Add(idIcono ?? "<null>"))
            {
                Debug.LogWarning($"[IconosDeBoton] no existe el icono '{idIcono}', devuelvo texto vacio");
            }
            return string.Empty;
        }

        return $"<sprite name=\"{prefijo}_0\">";
    }

    /// <summary>
    /// Nombre base de un icono (sin el sufijo de frame), o null si el id no existe.
    /// Lo usa el animador para armar los nombres de los otros frames.
    /// </summary>
    public static string PrefijoDe(string idIcono)
    {
        if (string.IsNullOrEmpty(idIcono))
        {
            return null;
        }

        AsegurarIndice();

        string prefijo;
        return _porId.TryGetValue(idIcono, out prefijo) ? prefijo : null;
    }

    // -------------------------------------------------------------- estado

    static TMP_SpriteAsset _asset;
    static bool _construido;
    static readonly Dictionary<string, string> _porId = new Dictionary<string, string>();
    static readonly HashSet<string> _idsAvisados = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        //con "Enter Play Mode" sin domain reload los estaticos sobreviven entre corridas, y
        //la textura/material de la corrida anterior ya fueron destruidos por Unity: sin este
        //reset el asset cacheado quedaria apuntando a objetos muertos (mismo patron que LocalizedText)
        _asset = null;
        _construido = false;
        _porId.Clear();
        _idsAvisados.Clear();
    }

    // ------------------------------------------------------- geometria del atlas

    const int ALTO_CELDA = 64;
    const int ANCHO_TEXTURA = 512;

    /// <summary>Supersampling del dibujo: se pinta a 2x y se promedia. Antialiasing barato.</summary>
    const int SS = 2;

    /// <summary>
    /// Metricas del glifo dentro de la celda. Con pointSize 68 y una celda de 64 px, el
    /// dibujo visible (~52 px) queda a la altura de una mayuscula; con bearingY 56 el icono
    /// queda centrado sobre la mitad de la caja de mayusculas en vez de colgado del baseline.
    /// </summary>
    const float POINT_SIZE = 68f;
    const float BEARING_Y = 56f;
    const float DESCENT = -8f;

    /// <summary>Aire extra despues del icono, para que no quede pegado a la letra siguiente.</summary>
    const float AIRE_AVANCE = 8f;

    //la respiracion: escala interna del dibujo y fuerza del halo, con la MISMA curva
    static readonly float[] ESCALAS = { 1.00f, 1.03f, 1.06f, 1.03f };
    static readonly float[] GLOWS = { 0.00f, 0.09f, 0.18f, 0.09f };

    // ------------------------------------------------------------- paleta

    static readonly Color NEGRO_BORDE = new Color(0.11f, 0.11f, 0.13f);
    static readonly Color VERDE_A = new Color(0.369f, 0.722f, 0.369f);   // #5EB85E
    static readonly Color ROJO_B = new Color(0.839f, 0.271f, 0.239f);    // #D6453D
    static readonly Color GRIS_BOTON = new Color(0.549f, 0.549f, 0.549f);// #8C8C8C
    static readonly Color BLANCO = Color.white;
    static readonly Color TECLA_CARA = new Color(0.914f, 0.902f, 0.878f);
    static readonly Color TECLA_BORDE = new Color(0.38f, 0.36f, 0.34f);
    static readonly Color TECLA_SOMBRA = new Color(0.26f, 0.25f, 0.23f);
    static readonly Color TECLA_LETRA = new Color(0.16f, 0.15f, 0.14f);
    static readonly Color MOUSE_CARA = new Color(0.86f, 0.85f, 0.83f);
    static readonly Color STICK_BASE = new Color(0.42f, 0.42f, 0.44f);
    static readonly Color STICK_BOLITA = new Color(0.76f, 0.76f, 0.78f);
    static readonly Color RUEDA_APAGADA = new Color(0.45f, 0.44f, 0.42f);
    static readonly Color ACENTO = new Color(0.91f, 0.64f, 0.24f);       // #E8A33D

    // ------------------------------------------------------- tabla de iconos

    enum Forma
    {
        BotonRedondo,
        Bumper,
        Start,
        Stick,
        Tecla,
        BarraEspacio,
        Shift,
        Wasd,
        Mouse,
    }

    struct Definicion
    {
        public string id;        // como lo pide el resto del juego: "a", "espacio", "mouse_izq"
        public string prefijo;   // nombre base del sprite en el atlas: "btn_a", "key_espacio"
        public int ancho;        // ancho de celda en px (el alto siempre es ALTO_CELDA)
        public Forma forma;
        public string etiqueta;  // texto dibujado adentro (puede ser null)
        public Color color;
        public float escalaLetra;
        public bool variante;    // mouse: false = boton izquierdo, true = ruedita

        public Definicion(string id, string prefijo, int ancho, Forma forma,
                          string etiqueta = null, float escalaLetra = 4f,
                          bool variante = false)
        {
            this.id = id;
            this.prefijo = prefijo;
            this.ancho = ancho;
            this.forma = forma;
            this.etiqueta = etiqueta;
            this.color = GRIS_BOTON;
            this.escalaLetra = escalaLetra;
            this.variante = variante;
        }

        public Definicion Con(Color c)
        {
            this.color = c;
            return this;
        }
    }

    static readonly Definicion[] DEFINICIONES =
    {
        //joystick (layout Xbox)
        new Definicion("a",     "btn_a",     64, Forma.BotonRedondo, "A").Con(VERDE_A),
        new Definicion("b",     "btn_b",     64, Forma.BotonRedondo, "B").Con(ROJO_B),
        new Definicion("l1",    "btn_l1",    64, Forma.Bumper, "L1", 3f),
        new Definicion("l2",    "btn_l2",    64, Forma.Bumper, "L2", 3f),
        new Definicion("r1",    "btn_r1",    64, Forma.Bumper, "R1", 3f),
        new Definicion("start", "btn_start", 64, Forma.Start),
        new Definicion("stick", "stick_izq", 64, Forma.Stick),

        //teclado
        new Definicion("e",       "key_e",       64,  Forma.Tecla, "E"),
        new Definicion("u",       "key_u",       64,  Forma.Tecla, "U"),
        new Definicion("i",       "key_i",       64,  Forma.Tecla, "I"),
        new Definicion("o",       "key_o",       64,  Forma.Tecla, "O"),
        new Definicion("m",       "key_m",       64,  Forma.Tecla, "M"),
        new Definicion("esc",     "key_esc",     80,  Forma.Tecla, "ESC", 2.6f),
        new Definicion("espacio", "key_espacio", 128, Forma.BarraEspacio),
        new Definicion("shift",   "key_shift",   96,  Forma.Shift),
        new Definicion("wasd",    "key_wasd",    112, Forma.Wasd),

        //mouse
        new Definicion("mouse_izq",   "mouse_izq",   64, Forma.Mouse),
        new Definicion("mouse_medio", "mouse_medio", 64, Forma.Mouse, null, 4f, true),
    };

    static void AsegurarIndice()
    {
        if (_porId.Count == DEFINICIONES.Length)
        {
            return;
        }

        _porId.Clear();
        for (int i = 0; i < DEFINICIONES.Length; i++)
        {
            _porId[DEFINICIONES[i].id] = DEFINICIONES[i].prefijo;
        }
    }

    // --------------------------------------------------------- construccion

    static void Construir()
    {
        //_construido se marca ANTES de trabajar: si algo revienta, no reintentamos el build
        //completo en cada frame (el getter se llama desde el procesado de texto)
        _construido = true;
        AsegurarIndice();

        //1) packing: una fila de celdas de 64 px de alto, cortando a lo ancho de la textura
        int cursorX = 0;
        int cursorY = 0;
        var celdas = new List<Celda>(DEFINICIONES.Length * FRAMES_POR_ICONO);

        for (int i = 0; i < DEFINICIONES.Length; i++)
        {
            Definicion def = DEFINICIONES[i];
            for (int frame = 0; frame < FRAMES_POR_ICONO; frame++)
            {
                if (cursorX + def.ancho > ANCHO_TEXTURA)
                {
                    cursorX = 0;
                    cursorY += ALTO_CELDA;
                }

                celdas.Add(new Celda
                {
                    nombre = def.prefijo + "_" + frame,
                    x = cursorX,
                    y = cursorY,
                    definicion = def,
                    frame = frame,
                });

                cursorX += def.ancho;
            }
        }

        int altoTextura = cursorY + ALTO_CELDA;

        //2) textura
        Texture2D textura = new Texture2D(ANCHO_TEXTURA, altoTextura, TextureFormat.RGBA32, false);
        textura.name = "IconosDeBoton_Atlas";
        textura.filterMode = FilterMode.Bilinear;
        textura.wrapMode = TextureWrapMode.Clamp;
        textura.hideFlags = HideFlags.DontSave;

        Color32[] pixeles = new Color32[ANCHO_TEXTURA * altoTextura];
        //Color32 arranca en (0,0,0,0) por default, o sea transparente: no hace falta limpiarlo

        var glifos = new List<TMP_SpriteGlyph>(celdas.Count);
        var caracteres = new List<TMP_SpriteCharacter>(celdas.Count);

        for (int i = 0; i < celdas.Count; i++)
        {
            Celda celda = celdas[i];
            Definicion def = celda.definicion;

            Lienzo lienzo = new Lienzo(def.ancho, ALTO_CELDA);
            DibujarIcono(lienzo, def, ESCALAS[celda.frame], GLOWS[celda.frame]);
            lienzo.VolcarEn(pixeles, ANCHO_TEXTURA, altoTextura, celda.x, celda.y);

            //OJO: el origen de la textura en Unity es ABAJO-izquierda y el de nuestro dibujo
            //es ARRIBA-izquierda, asi que la fila del GlyphRect es la de abajo de la celda
            var rect = new GlyphRect(celda.x, altoTextura - celda.y - ALTO_CELDA, def.ancho, ALTO_CELDA);
            var metricas = new GlyphMetrics(def.ancho, ALTO_CELDA, 0f, BEARING_Y, def.ancho + AIRE_AVANCE);

            var glifo = new TMP_SpriteGlyph((uint)i, metricas, rect, 1f, 0);
            glifos.Add(glifo);

            //unicode 0xFFFE = "no tiene unicode real, se busca por nombre". TMP saltea ese
            //valor al armar el lookup por unicode, asi que 72 sprites pueden compartirlo
            var caracter = new TMP_SpriteCharacter(0xFFFE, glifo);
            caracter.name = celda.nombre;
            caracter.scale = 1f;
            caracteres.Add(caracter);
        }

        textura.SetPixels32(pixeles);
        textura.Apply(false, false);

        //3) el sprite asset
        TMP_SpriteAsset asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        asset.name = "IconosDeBoton";
        asset.hideFlags = HideFlags.DontSave;
        asset.spriteSheet = textura;
        asset.spriteInfoList = new List<TMP_Sprite>();

        //las dos tablas tienen setter 'internal' (no se pueden asignar desde Assembly-CSharp),
        //pero el getter devuelve la lista viva: se llenan por AddRange
        asset.spriteGlyphTable.AddRange(glifos);
        asset.spriteCharacterTable.AddRange(caracteres);

        AplicarPorReflexion(asset);

        //TRAMPA de TMP 3.0.6: UpdateLookupTables() llama a UpgradeSpriteAsset() si el asset
        //tiene material y la version esta vacia. Ese "upgrade" BORRA las dos tablas y las
        //rearma desde spriteInfoList (vacia), o sea, nos dejaria el atlas en blanco. Se blinda
        //por dos lados: arriba le escribimos la version por reflexion, y ademas armamos los
        //lookups con el material todavia en null (asi el guard no se cumple ni aunque falle
        //la reflexion). El material se cuelga recien despues.
        asset.UpdateLookupTables();

        Material material = CrearMaterial(textura);
        if (material != null)
        {
            asset.material = material;
        }

        _asset = asset;

        Debug.Log($"[IconosDeBoton] atlas construido: {DEFINICIONES.Length} iconos x " +
                  $"{FRAMES_POR_ICONO} frames = {celdas.Count} sprites, textura " +
                  $"{ANCHO_TEXTURA}x{altoTextura}");
    }

    static Material CrearMaterial(Texture2D textura)
    {
        Shader shader = Shader.Find("TextMeshPro/Sprite");
        if (shader == null)
        {
            Debug.LogWarning("[IconosDeBoton] no encontre el shader 'TextMeshPro/Sprite' " +
                             "(seguro no esta incluido en el build). El atlas queda armado sin " +
                             "material propio: TMP va a usar el suyo y los iconos pueden verse raros.");
            return null;
        }

        Material material = new Material(shader);
        material.name = "IconosDeBoton_Material";
        material.hideFlags = HideFlags.DontSave;
        material.SetTexture(ShaderUtilities.ID_MainTex, textura);
        return material;
    }

    // Dos campos de TMP_SpriteAsset que necesitamos escribir y tienen setter 'internal', o sea
    // inaccesible desde Assembly-CSharp: 'm_FaceInfo' (las metricas que alinean el icono con el
    // texto) y 'm_Version' (que apaga la trampa del "upgrade", ver Construir). Si la reflexion
    // fallara, TMP tiene caminos B razonables, asi que esto NO rompe nada: avisamos y seguimos.
    static void AplicarPorReflexion(TMP_SpriteAsset asset)
    {
        FieldInfo campoVersion = typeof(TMP_SpriteAsset).GetField("m_Version",
                                                                  BindingFlags.Instance | BindingFlags.NonPublic);
        if (campoVersion != null)
        {
            campoVersion.SetValue(asset, "1.1.0");
        }

        FieldInfo campo = typeof(TMP_SpriteAsset).GetField("m_FaceInfo",
                                                           BindingFlags.Instance | BindingFlags.NonPublic);
        if (campo == null)
        {
            Debug.LogWarning("[IconosDeBoton] no encontre TMP_SpriteAsset.m_FaceInfo por reflexion; " +
                             "los iconos van a escalarse solos al ascent de la fuente (aceptable, " +
                             "pero puede quedar alineado distinto de lo pensado)");
            return;
        }

        FaceInfo cara = new FaceInfo();
        cara.pointSize = (int)POINT_SIZE;
        cara.scale = 1f;
        cara.lineHeight = POINT_SIZE;
        cara.ascentLine = BEARING_Y;
        cara.capLine = BEARING_Y;
        cara.meanLine = BEARING_Y * 0.5f;
        cara.baseline = 0f;
        cara.descentLine = DESCENT;

        campo.SetValue(asset, cara);
    }

    struct Celda
    {
        public string nombre;
        public int x;
        public int y;   // desde ARRIBA (nuestro dibujo); se invierte al armar el GlyphRect
        public Definicion definicion;
        public int frame;
    }

    // ============================================================ DIBUJO
    //
    // Todo se dibuja en "coordenadas de celda" (0..ancho, 0..64, con la Y creciendo hacia
    // ABAJO) sobre un lienzo supersampleado; al volcarlo se promedia. Cada primitiva tiene un
    // borde suave (parametro 'suave'), que ademas se reusa para los halos: un circulo con
    // suave grande ES un glow con caida, sin necesidad de otra funcion.

    class Lienzo
    {
        public readonly int ancho;   // en pixeles de celda
        public readonly int alto;
        public readonly int anchoSS;
        public readonly int altoSS;
        readonly float[] _p;         // RGBA PREmultiplicado: promediar asi no genera halos

        public Lienzo(int ancho, int alto)
        {
            this.ancho = ancho;
            this.alto = alto;
            anchoSS = ancho * SS;
            altoSS = alto * SS;
            _p = new float[anchoSS * altoSS * 4];
        }

        public void Mezclar(int x, int y, Color col, float a)
        {
            if (a <= 0f || x < 0 || y < 0 || x >= anchoSS || y >= altoSS)
            {
                return;
            }

            if (a > 1f)
            {
                a = 1f;
            }

            int i = (y * anchoSS + x) * 4;
            float inv = 1f - a;
            _p[i] = col.r * a + _p[i] * inv;
            _p[i + 1] = col.g * a + _p[i + 1] * inv;
            _p[i + 2] = col.b * a + _p[i + 2] * inv;
            _p[i + 3] = a + _p[i + 3] * inv;
        }

        /// <summary>Promedia el supersampling y escribe la celda en el atlas, invirtiendo la Y.</summary>
        public void VolcarEn(Color32[] destino, int anchoTextura, int altoTextura, int celdaX, int celdaY)
        {
            float muestras = SS * SS;

            for (int ly = 0; ly < alto; ly++)
            {
                //la fila 0 del dibujo es la de ARRIBA de la celda; en la textura la Y va al reves
                int filaTextura = altoTextura - 1 - (celdaY + ly);
                if (filaTextura < 0 || filaTextura >= altoTextura)
                {
                    continue;
                }

                for (int lx = 0; lx < ancho; lx++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;

                    for (int sy = 0; sy < SS; sy++)
                    {
                        int fila = (ly * SS + sy) * anchoSS;
                        for (int sx = 0; sx < SS; sx++)
                        {
                            int i = (fila + lx * SS + sx) * 4;
                            r += _p[i];
                            g += _p[i + 1];
                            b += _p[i + 2];
                            a += _p[i + 3];
                        }
                    }

                    r /= muestras;
                    g /= muestras;
                    b /= muestras;
                    a /= muestras;

                    //des-premultiplicar para volver a color "recto", que es lo que espera Color32
                    if (a > 0.0001f)
                    {
                        r /= a;
                        g /= a;
                        b /= a;
                    }

                    destino[filaTextura * anchoTextura + celdaX + lx] = new Color32(
                        (byte)(Mathf.Clamp01(r) * 255f),
                        (byte)(Mathf.Clamp01(g) * 255f),
                        (byte)(Mathf.Clamp01(b) * 255f),
                        (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
        }
    }

    /// <summary>Medio pixel de celda a cada lado del borde: el antialiasing "normal".</summary>
    const float SUAVE = 1f / SS;

    static float Cobertura(float distancia, float suave)
    {
        //distancia < 0 = adentro de la forma
        if (suave <= 0f)
        {
            return distancia <= 0f ? 1f : 0f;
        }
        return Mathf.Clamp01(0.5f - distancia / suave);
    }

    static void Circulo(Lienzo l, float cx, float cy, float r, Color col, float alfa = 1f, float suave = SUAVE)
    {
        float margen = r + suave + 1f;
        int x0 = Mathf.Max(0, (int)((cx - margen) * SS));
        int x1 = Mathf.Min(l.anchoSS - 1, (int)((cx + margen) * SS));
        int y0 = Mathf.Max(0, (int)((cy - margen) * SS));
        int y1 = Mathf.Min(l.altoSS - 1, (int)((cy + margen) * SS));

        for (int py = y0; py <= y1; py++)
        {
            float fy = (py + 0.5f) / SS;
            for (int px = x0; px <= x1; px++)
            {
                float fx = (px + 0.5f) / SS;
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy)) - r;
                float c = Cobertura(d, suave);
                if (c > 0f)
                {
                    l.Mezclar(px, py, col, c * alfa);
                }
            }
        }
    }

    static void RectRedondeado(Lienzo l, float cx, float cy, float w, float h, float r,
                               Color col, float alfa = 1f, float suave = SUAVE)
    {
        float hw = w * 0.5f;
        float hh = h * 0.5f;
        r = Mathf.Min(r, Mathf.Min(hw, hh));

        float mx = hw + suave + 1f;
        float my = hh + suave + 1f;
        int x0 = Mathf.Max(0, (int)((cx - mx) * SS));
        int x1 = Mathf.Min(l.anchoSS - 1, (int)((cx + mx) * SS));
        int y0 = Mathf.Max(0, (int)((cy - my) * SS));
        int y1 = Mathf.Min(l.altoSS - 1, (int)((cy + my) * SS));

        for (int py = y0; py <= y1; py++)
        {
            float fy = (py + 0.5f) / SS;
            for (int px = x0; px <= x1; px++)
            {
                float fx = (px + 0.5f) / SS;
                float qx = Mathf.Max(0f, Mathf.Abs(fx - cx) - (hw - r));
                float qy = Mathf.Max(0f, Mathf.Abs(fy - cy) - (hh - r));
                float d = Mathf.Sqrt(qx * qx + qy * qy) - r;
                float c = Cobertura(d, suave);
                if (c > 0f)
                {
                    l.Mezclar(px, py, col, c * alfa);
                }
            }
        }
    }

    static void Triangulo(Lienzo l, Vector2 p0, Vector2 p1, Vector2 p2, Color col, float alfa = 1f)
    {
        float minX = Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x)) - 1f;
        float maxX = Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x)) + 1f;
        float minY = Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y)) - 1f;
        float maxY = Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y)) + 1f;

        int x0 = Mathf.Max(0, (int)(minX * SS));
        int x1 = Mathf.Min(l.anchoSS - 1, (int)(maxX * SS));
        int y0 = Mathf.Max(0, (int)(minY * SS));
        int y1 = Mathf.Min(l.altoSS - 1, (int)(maxY * SS));

        for (int py = y0; py <= y1; py++)
        {
            float fy = (py + 0.5f) / SS;
            for (int px = x0; px <= x1; px++)
            {
                float fx = (px + 0.5f) / SS;
                float d1 = Lado(p0, p1, fx, fy);
                float d2 = Lado(p1, p2, fx, fy);
                float d3 = Lado(p2, p0, fx, fy);
                bool negativo = d1 < 0f || d2 < 0f || d3 < 0f;
                bool positivo = d1 > 0f || d2 > 0f || d3 > 0f;

                //adentro = todos los lados dan el mismo signo (asi no importa el orden de los vertices)
                if (!(negativo && positivo))
                {
                    l.Mezclar(px, py, col, alfa);
                }
            }
        }
    }

    static float Lado(Vector2 a, Vector2 b, float px, float py)
    {
        return (px - a.x) * (b.y - a.y) - (py - a.y) * (b.x - a.x);
    }

    // ----------------------------------------------------- fuente bitmap 5x7
    //
    // No tenemos ninguna fuente disponible desde codigo, asi que las letras son un bitmap
    // propio de 5x7 escalado. Escalado se ve prolijo y "de juego": perfecto para placeholder.
    // Los pixeles se pintan con borde DURO (suave 0) a proposito: con borde suave, dos
    // pixeles vecinos daban 0.5 + 0.5 de cobertura y quedaba una costura, y las letras se
    // veian punteadas.

    static readonly Dictionary<char, string> FUENTE = new Dictionary<char, string>
    {
        { 'A', ".###.|#...#|#...#|#####|#...#|#...#|#...#" },
        { 'B', "####.|#...#|#...#|####.|#...#|#...#|####." },
        { 'C', ".###.|#...#|#....|#....|#....|#...#|.###." },
        { 'D', "####.|#...#|#...#|#...#|#...#|#...#|####." },
        { 'E', "#####|#....|#....|####.|#....|#....|#####" },
        { 'I', "#####|..#..|..#..|..#..|..#..|..#..|#####" },
        { 'L', "#....|#....|#....|#....|#....|#....|#####" },
        { 'M', "#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#" },
        { 'O', ".###.|#...#|#...#|#...#|#...#|#...#|.###." },
        { 'R', "####.|#...#|#...#|####.|#.#..|#..#.|#...#" },
        { 'S', ".####|#....|#....|.###.|....#|....#|####." },
        { 'T', "#####|..#..|..#..|..#..|..#..|..#..|..#.." },
        { 'U', "#...#|#...#|#...#|#...#|#...#|#...#|.###." },
        { 'W', "#...#|#...#|#...#|#.#.#|#.#.#|##.##|#...#" },
        { '1', "..#..|.##..|..#..|..#..|..#..|..#..|.###." },
        { '2', ".###.|#...#|....#|...#.|..#..|.#...|#####" },
    };

    const float ESPACIO_ENTRE_LETRAS = 1f;   // en "pixeles" de la fuente 5x7

    static void Texto(Lienzo l, string s, float cx, float cy, float k, Color col, float alfa = 1f)
    {
        if (string.IsNullOrEmpty(s))
        {
            return;
        }

        float anchoTotal = s.Length * 5f * k + (s.Length - 1) * ESPACIO_ENTRE_LETRAS * k;
        float x = cx - anchoTotal * 0.5f;

        for (int i = 0; i < s.Length; i++)
        {
            string patron;
            if (FUENTE.TryGetValue(s[i], out patron))
            {
                string[] filas = patron.Split('|');
                for (int fy = 0; fy < filas.Length; fy++)
                {
                    string fila = filas[fy];
                    for (int fx = 0; fx < fila.Length; fx++)
                    {
                        if (fila[fx] != '#')
                        {
                            continue;
                        }
                        RectRedondeado(l,
                                       x + (fx + 0.5f) * k,
                                       cy - 3.5f * k + (fy + 0.5f) * k,
                                       k, k, 0f, col, alfa, 0f);
                    }
                }
            }

            x += (5f + ESPACIO_ENTRE_LETRAS) * k;
        }
    }

    // ------------------------------------------------------------- iconos

    static void DibujarIcono(Lienzo l, Definicion def, float esc, float glow)
    {
        switch (def.forma)
        {
            case Forma.BotonRedondo:
                DibujarBotonRedondo(l, def.color, def.etiqueta, esc, glow);
                break;
            case Forma.Bumper:
                DibujarBumper(l, def.etiqueta, def.escalaLetra, esc, glow);
                break;
            case Forma.Start:
                DibujarStart(l, esc, glow);
                break;
            case Forma.Stick:
                DibujarStick(l, esc, glow);
                break;
            case Forma.Tecla:
                DibujarTecla(l, def.etiqueta, def.escalaLetra, esc, glow);
                break;
            case Forma.BarraEspacio:
                DibujarBarraEspacio(l, esc, glow);
                break;
            case Forma.Shift:
                DibujarShift(l, esc, glow);
                break;
            case Forma.Wasd:
                DibujarWasd(l, esc, glow);
                break;
            case Forma.Mouse:
                DibujarMouse(l, def.variante, esc, glow);
                break;
            default:
                Debug.LogWarning($"[IconosDeBoton] no se dibujar la forma '{def.forma}' del icono '{def.id}'");
                break;
        }
    }

    static void DibujarBotonRedondo(Lienzo l, Color color, string letra, float esc, float glow)
    {
        float cx = l.ancho * 0.5f;
        float cy = l.alto * 0.5f;

        if (glow > 0f)
        {
            Circulo(l, cx, cy, 25f * esc, color, glow, 16f * esc);
        }

        Circulo(l, cx, cy, 26f * esc, NEGRO_BORDE);
        Circulo(l, cx, cy, 23f * esc, color);
        //el relieve: un blanco muy tenue corrido hacia arriba, como una luz cenital
        Circulo(l, cx, cy - 5f * esc, 17f * esc, BLANCO, 0.13f);
        Texto(l, letra, cx, cy + 1f * esc, 4f * esc, NEGRO_BORDE, 0.30f);
        Texto(l, letra, cx, cy, 4f * esc, BLANCO);
    }

    static void DibujarBumper(Lienzo l, string etiqueta, float k, float esc, float glow)
    {
        float cx = l.ancho * 0.5f;
        float cy = l.alto * 0.5f;

        if (glow > 0f)
        {
            RectRedondeado(l, cx, cy, 54f * esc, 36f * esc, 13f * esc, GRIS_BOTON, glow, 14f * esc);
        }

        RectRedondeado(l, cx, cy, 54f * esc, 36f * esc, 13f * esc, NEGRO_BORDE);
        RectRedondeado(l, cx, cy, 48f * esc, 30f * esc, 10f * esc, GRIS_BOTON);
        RectRedondeado(l, cx, cy - 7f * esc, 40f * esc, 12f * esc, 6f * esc, BLANCO, 0.14f);
        Texto(l, etiqueta, cx, cy + 1f * esc, k * esc, NEGRO_BORDE, 0.30f);
        Texto(l, etiqueta, cx, cy, k * esc, BLANCO);
    }

    static void DibujarStart(Lienzo l, float esc, float glow)
    {
        float cx = l.ancho * 0.5f;
        float cy = l.alto * 0.5f;

        if (glow > 0f)
        {
            Circulo(l, cx, cy, 25f * esc, GRIS_BOTON, glow, 16f * esc);
        }

        Circulo(l, cx, cy, 26f * esc, NEGRO_BORDE);
        Circulo(l, cx, cy, 23f * esc, GRIS_BOTON);
        Circulo(l, cx, cy - 5f * esc, 17f * esc, BLANCO, 0.13f);

        //las tres rayitas del boton Start
        for (int i = -1; i <= 1; i++)
        {
            RectRedondeado(l, cx, cy + i * 8f * esc, 24f * esc, 4f * esc, 2f * esc, BLANCO);
        }
    }

    static void DibujarStick(Lienzo l, float esc, float glow)
    {
        float cx = l.ancho * 0.5f;
        float cy = l.alto * 0.5f;

        if (glow > 0f)
        {
            Circulo(l, cx, cy, 26f * esc, GRIS_BOTON, glow, 16f * esc);
        }

        Circulo(l, cx, cy, 27f * esc, NEGRO_BORDE);
        Circulo(l, cx, cy, 24f * esc, STICK_BASE);

        //flechitas de direccion, para que se lea "esto se mueve" y no "esto es un boton"
        float d = 19f * esc;
        float s = 5f * esc;
        Triangulo(l, new Vector2(cx, cy - d - s), new Vector2(cx - s, cy - d + s * 0.6f), new Vector2(cx + s, cy - d + s * 0.6f), BLANCO, 0.85f);
        Triangulo(l, new Vector2(cx, cy + d + s), new Vector2(cx - s, cy + d - s * 0.6f), new Vector2(cx + s, cy + d - s * 0.6f), BLANCO, 0.85f);
        Triangulo(l, new Vector2(cx - d - s, cy), new Vector2(cx - d + s * 0.6f, cy - s), new Vector2(cx - d + s * 0.6f, cy + s), BLANCO, 0.85f);
        Triangulo(l, new Vector2(cx + d + s, cy), new Vector2(cx + d - s * 0.6f, cy - s), new Vector2(cx + d - s * 0.6f, cy + s), BLANCO, 0.85f);

        //la bolita del stick
        Circulo(l, cx, cy, 13f * esc, NEGRO_BORDE);
        Circulo(l, cx, cy, 11f * esc, STICK_BOLITA);
        Circulo(l, cx, cy - 3f * esc, 7f * esc, BLANCO, 0.5f);
    }

    /// <summary>Keycap fisica: sombra abajo, borde, cara y un brillo arriba.</summary>
    static void CuerpoTecla(Lienzo l, float cx, float cy, float w, float h, float esc, float glow, float radio)
    {
        if (glow > 0f)
        {
            RectRedondeado(l, cx, cy, w * esc, h * esc, radio * esc, TECLA_CARA, glow, 14f * esc);
        }

        RectRedondeado(l, cx, cy + 3f * esc, w * esc, h * esc, radio * esc, TECLA_SOMBRA);
        RectRedondeado(l, cx, cy, w * esc, h * esc, radio * esc, TECLA_BORDE);
        RectRedondeado(l, cx, cy, (w - 5f) * esc, (h - 5f) * esc, (radio - 2.5f) * esc, TECLA_CARA);
        RectRedondeado(l, cx, cy - h * 0.25f * esc, (w - 13f) * esc, h * 0.3f * esc,
                       (radio - 3f) * esc, BLANCO, 0.45f);
    }

    static void DibujarTecla(Lienzo l, string etiqueta, float k, float esc, float glow)
    {
        float cx = l.ancho * 0.5f;
        //-1.5 px: la sombra de abajo ocupa lugar, asi la tecla queda opticamente centrada
        float cy = l.alto * 0.5f - 1.5f;

        CuerpoTecla(l, cx, cy, l.ancho - 10f, 46f, esc, glow, 9f);
        Texto(l, etiqueta, cx, cy + 1f * esc, k * esc, TECLA_LETRA);
    }

    static void DibujarBarraEspacio(Lienzo l, float esc, float glow)
    {
        float cx = l.ancho * 0.5f;
        float cy = l.alto * 0.5f - 1.5f;

        CuerpoTecla(l, cx, cy, l.ancho - 10f, 46f, esc, glow, 9f);
        //la barra espaciadora no lleva letra: se reconoce por la barrita y por lo ancha que es
        RectRedondeado(l, cx, cy + 2f * esc, 64f * esc, 7f * esc, 3.5f * esc, TECLA_LETRA);
    }

    static void DibujarShift(Lienzo l, float esc, float glow)
    {
        float cx = l.ancho * 0.5f;
        float cy = l.alto * 0.5f - 1.5f;

        CuerpoTecla(l, cx, cy, l.ancho - 10f, 46f, esc, glow, 9f);
        //flecha para arriba: punta + tronco
        Triangulo(l, new Vector2(cx, cy - 14f * esc), new Vector2(cx - 13f * esc, cy - 1f * esc),
                  new Vector2(cx + 13f * esc, cy - 1f * esc), TECLA_LETRA);
        RectRedondeado(l, cx, cy + 6f * esc, 11f * esc, 14f * esc, 1.5f * esc, TECLA_LETRA);
    }

    static void DibujarWasd(Lienzo l, float esc, float glow)
    {
        float cx = l.ancho * 0.5f;
        float cy = l.alto * 0.5f - 1f;
        float paso = 33f * esc;

        CuerpoTecla(l, cx, cy - 14f * esc, 31f, 28f, esc, glow, 7f);
        Texto(l, "W", cx, cy - 14f * esc, 3f * esc, TECLA_LETRA);

        string abajo = "ASD";
        for (int i = 0; i < abajo.Length; i++)
        {
            float x = cx + (i - 1) * paso;
            CuerpoTecla(l, x, cy + 14f * esc, 31f, 28f, esc, glow, 7f);
            Texto(l, abajo[i].ToString(), x, cy + 14f * esc, 3f * esc, TECLA_LETRA);
        }
    }

    static void DibujarMouse(Lienzo l, bool esRueda, float esc, float glow)
    {
        float cx = l.ancho * 0.5f;
        float cy = l.alto * 0.5f;

        if (glow > 0f)
        {
            RectRedondeado(l, cx, cy, 38f * esc, 52f * esc, 18f * esc, MOUSE_CARA, glow, 14f * esc);
        }

        RectRedondeado(l, cx, cy, 38f * esc, 52f * esc, 18f * esc, NEGRO_BORDE);
        RectRedondeado(l, cx, cy, 33f * esc, 47f * esc, 15.5f * esc, MOUSE_CARA);

        if (!esRueda)
        {
            //boton izquierdo pintado con el color de acento
            RectRedondeado(l, cx - 8.6f * esc, cy - 12.5f * esc, 15.5f * esc, 21f * esc, 7f * esc, ACENTO);
        }

        //separadores: la linea que corta los botones del cuerpo y la que los separa entre si
        RectRedondeado(l, cx, cy - 2f * esc, 33f * esc, 2f * esc, 1f * esc, NEGRO_BORDE, 0.75f);
        RectRedondeado(l, cx, cy - 12.5f * esc, 2f * esc, 21f * esc, 1f * esc, NEGRO_BORDE, 0.75f);

        //ruedita
        Color colorRueda = esRueda ? ACENTO : RUEDA_APAGADA;
        RectRedondeado(l, cx, cy - 11f * esc, 7f * esc, 14f * esc, 3.5f * esc, NEGRO_BORDE);
        RectRedondeado(l, cx, cy - 11f * esc, 5f * esc, 12f * esc, 2.5f * esc, colorRueda);
    }
}
