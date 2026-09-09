using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

/// <summary>
/// EL UNICO lugar del juego que escribe texto localizado en un TextMeshPro.
///
/// Por que existe: la misma corrutina "SetLocalizedText" estaba copypasteada en SIETE
/// clases (TooltipManager, DialogueManager, InventorySlot, QuestSlot, DefeatOverlay,
/// TextUpdater, PliegueTextUpdater). Cuando llego el joystick hubo que enchufar
/// <see cref="InputPromptSystem"/> a mano en cada copia, y solo DOS quedaron enchufadas:
/// el resto de la UI seguia diciendo "Toca E" con un joystick en la mano. Con un solo
/// camino, enchufar algo nuevo se hace una vez y lo hereda toda la UI.
///
/// Ademas resuelve el segundo problema: el texto YA PROCESADO perdio el original, asi
/// que no alcanza con re-procesarlo cuando el jugador cambia de device. Por eso guardamos
/// el string CRUDO (localizado pero sin resolver los prompts) contra el TMP donde se
/// escribio, y ante <see cref="InputHub.OnDeviceCambio"/> reescribimos todo lo vivo.
/// Con eso no hay que cablear NADA por objeto: cualquier texto que pase por aca se
/// actualiza solo al agarrar el joystick y al volver al teclado.
/// </summary>
public static class LocalizedText
{
    // --------------------------------------------------------------- opciones

    /// <summary>
    /// Las diferencias reales entre las siete copias viejas. No son gustos: cada call site
    /// tenia su semantica de fallback y su nivel de ruido en consola, y cambiarlas ahora
    /// seria cambiar comportamiento que hoy funciona.
    /// </summary>
    public struct Opciones
    {
        /// <summary>Se concatena al texto localizado (ej: "Pliegue: " + "2/5").</summary>
        public string sufijo;

        /// <summary>El sufijo tambien se concatena cuando la clave NO existe en la tabla.</summary>
        public bool sufijoEnFallback;

        /// <summary>Si la tabla no carga: escribir igual la clave cruda en vez de dejar el texto viejo.</summary>
        public bool escribirSinTabla;

        /// <summary>Loguear warning cuando la tabla no carga.</summary>
        public bool avisarSinTabla;

        /// <summary>Loguear warning cuando la clave no esta en la tabla.</summary>
        public bool avisarSinClave;

        /// <summary>Prefijo de los warnings (ej: "TooltipManager"). Solo para diagnostico.</summary>
        public string origen;
    }

    // ------------------------------------------------------------------- API

    /// <summary>
    /// Resuelve la clave contra la tabla, la pasa por <see cref="InputPromptSystem"/> y la
    /// escribe en el TMP, dejandola registrada para reescribirla si cambia el device.
    /// Es corrutina porque cargar la tabla es asincronico (igual que las siete copias viejas).
    /// </summary>
    public static IEnumerator Escribir(TMP_Text destino, string clave, string tabla, Opciones opciones)
    {
        if (destino == null)
        {
            Debug.LogWarning($"[LocalizedText] me pidieron escribir '{clave}' pero el TMP destino es null (origen: {opciones.origen})");
            yield break;
        }

        //clave vacia = "borrame el texto". Las copias viejas escribian el fallback tal cual,
        //que con clave vacia es cadena vacia (o solo el sufijo, en los TextUpdater).
        if (string.IsNullOrEmpty(clave))
        {
            Aplicar(destino, TextoDeFallback(clave, opciones));
            yield break;
        }

        var tableOperation = LocalizationSettings.StringDatabase.GetTableAsync(tabla);
        yield return tableOperation;

        //el TMP se pudo destruir mientras cargaba la tabla (cambio de pagina, overlay cerrado)
        if (destino == null)
        {
            yield break;
        }

        StringTable stringTable = tableOperation.Result;
        if (stringTable == null)
        {
            if (opciones.avisarSinTabla)
            {
                Debug.LogWarning($"[{Origen(opciones)}] no pude cargar la tabla '{tabla}', uso el texto sin localizar");
            }

            if (opciones.escribirSinTabla)
            {
                Aplicar(destino, TextoDeFallback(clave, opciones));
            }
            yield break;
        }

        var entry = stringTable.GetEntry(clave);
        if (entry != null && !string.IsNullOrEmpty(entry.GetLocalizedString()))
        {
            Aplicar(destino, entry.GetLocalizedString() + opciones.sufijo);
            yield break;
        }

        if (opciones.avisarSinClave)
        {
            Debug.LogWarning($"[{Origen(opciones)}] la clave '{clave}' no esta en la tabla '{tabla}', muestro la clave cruda");
        }
        Aplicar(destino, TextoDeFallback(clave, opciones));
    }

    /// <summary>
    /// Escribe un texto YA resuelto (no viene de una tabla) pasandolo por los prompts, y lo
    /// deja registrado. Lo usan el puente con LocalizeStringEvent y cualquier texto armado a mano.
    /// </summary>
    public static void Aplicar(TMP_Text destino, string crudo)
    {
        if (destino == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(crudo))
        {
            //nada que reprocesar: sacarlo del registro evita que un refresh posterior le
            //resucite el texto que tenia antes de que lo limpiaran
            _crudos.Remove(destino);
            AnimadorDeIconos.Desregistrar(destino);
            destino.text = crudo;
            return;
        }

        //El resaltado de conceptos se hornea UNA SOLA VEZ, aca, y queda guardado DENTRO del
        //crudo. No puede ir junto a los prompts (que se recalculan enteros en cada cambio de
        //device) porque NO es idempotente: los tags <b><color> dejan la palabra intacta en el
        //medio, asi que volver a pasarle el resaltador a un texto ya resaltado la envolveria
        //de nuevo, y otra vez, anidando tags en cada cambio de device.
        crudo = ResaltadorDeConceptos.Resaltar(crudo);

        _crudos[destino] = crudo;
        EscribirProcesado(destino, crudo);
    }

    /// <summary>
    /// El UNICO lugar donde un crudo se convierte en el texto final que ve el jugador. Lo
    /// usan tanto <see cref="Aplicar"/> como el refresh por cambio de device: si los dos
    /// caminos no hicieran exactamente lo mismo, agarrar el joystick dejaria textos sin
    /// sprite asset o sin animar.
    /// </summary>
    static void EscribirProcesado(TMP_Text destino, string crudo)
    {
        AsegurarSpriteAsset(destino);
        destino.text = InputPromptSystem.Procesar(crudo);
        AnimadorDeIconos.Registrar(destino);
    }

    /// <summary>
    /// Sin sprite asset asignado, un tag &lt;sprite name="btn_a_0"&gt; se ve como texto crudo
    /// en pantalla. El atlas de iconos se construye por codigo (no es un asset del proyecto),
    /// asi que hay que enchufarselo a cada TMP antes de escribirle el texto.
    /// </summary>
    static void AsegurarSpriteAsset(TMP_Text destino)
    {
        TMP_SpriteAsset iconos = IconosDeBoton.Asset;
        if (iconos == null)
        {
            return;
        }

        if (destino.spriteAsset == null)
        {
            destino.spriteAsset = iconos;
            return;
        }

        if (destino.spriteAsset == iconos)
        {
            return;
        }

        //Hoy NINGUN TMP del proyecto trae sprite asset propio (verificado: los 31 estan en
        //fileID 0), asi que esto no deberia pasar nunca. Si alguna vez pasa, NO se lo pisamos
        //ni le tocamos su lista de fallbacks -- eso ensuciaria un asset compartido del
        //proyecto. Avisamos y ese texto se queda sin iconos, que es el mal menor.
        if (_avisadosSpriteAssetPropio.Add(destino.GetInstanceID()))
        {
            Debug.LogWarning($"[LocalizedText] '{destino.name}' ya tiene su propio sprite asset " +
                             $"('{destino.spriteAsset.name}'), asi que no le pongo el de iconos: " +
                             "sus prompts van a salir como texto. Si esto es a proposito, sumale " +
                             "IconosDeBoton.Asset como fallback a mano.");
        }
    }

    //Para no repetir el warning de arriba una vez por linea de dialogo.
    static readonly HashSet<int> _avisadosSpriteAssetPropio = new HashSet<int>();

    /// <summary>
    /// Saca un TMP del registro. Hay que llamarla cuando alguien le asigna '.text' A MANO
    /// (ClearSlot, HideDialogue): si no, el proximo cambio de device le reescribe el texto viejo.
    /// </summary>
    public static void Limpiar(TMP_Text destino)
    {
        if (destino == null)
        {
            return;
        }
        _crudos.Remove(destino);

        //si el texto se va a vaciar a mano, tampoco tiene sentido seguir animandole iconos
        AnimadorDeIconos.Desregistrar(destino);
    }

    static string TextoDeFallback(string clave, Opciones opciones)
    {
        return opciones.sufijoEnFallback ? clave + opciones.sufijo : clave;
    }

    static string Origen(Opciones opciones)
    {
        return string.IsNullOrEmpty(opciones.origen) ? "LocalizedText" : opciones.origen;
    }

    // ------------------------------------------------- registro y refresh por device

    //TMP -> string crudo (localizado, con sufijo, SIN los prompts resueltos). El crudo es lo
    //unico que permite reescribir el texto cuando el jugador cambia de device: del texto ya
    //procesado no se puede volver ("(A)" no sabe si salio de "E" o de "ESPACIO").
    static readonly Dictionary<TMP_Text, string> _crudos = new Dictionary<TMP_Text, string>();

    //buffer reusado: sacar entradas del diccionario mientras se recorre no se puede
    static readonly List<TMP_Text> _muertos = new List<TMP_Text>();

    //instance IDs de los LocalizeStringEvent ya enganchados. Guardamos el ID y no el
    //componente a proposito: dos objetos de Unity YA DESTRUIDOS se comparan iguales entre si
    //(el "fake null" hace Equals true), asi que un HashSet de componentes se vuelve traicionero
    //en cuanto uno muere. El ID es un int y nunca se reusa dentro de la misma corrida.
    static readonly HashSet<int> _enganchados = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        //con "Enter Play Mode" sin domain reload los estaticos sobreviven entre corridas:
        //sin esto el registro arrancaria lleno de TMPs de la sesion anterior
        _crudos.Clear();
        _muertos.Clear();
        _enganchados.Clear();
        _avisadosSpriteAssetPropio.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arrancar()
    {
        //-= antes de += : asi suscribirse dos veces es imposible aunque este metodo corra de nuevo
        InputHub.OnDeviceCambio -= AlCambiarDeDevice;
        InputHub.OnDeviceCambio += AlCambiarDeDevice;

        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;

        //El atlas de iconos se dibuja por codigo la primera vez que alguien lo pide. Pedirlo
        //ACA lo saca del medio del primer dialogo: si no, el hitch de generar la textura caeria
        //justo cuando el jugador abre el primer globo de texto. De paso, si fallo, se entera
        //al arrancar y no cinco minutos despues viendo tags crudos en pantalla.
        if (IconosDeBoton.Asset == null)
        {
            Debug.LogWarning("[LocalizedText] no se pudo construir el atlas de iconos de botones: " +
                             "los prompts van a salir como texto ('E', '(A)'...), que es el " +
                             "comportamiento viejo. Revisar los logs de [IconosDeBoton].");
        }

        EngancharLocalizeStringEvents();
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        EngancharLocalizeStringEvents();
    }

    static void AlCambiarDeDevice()
    {
        //barrer de nuevo cubre lo que se instancio despues de cargar la escena; es una
        //busqueda cara, pero cambiar de device pasa unas pocas veces por partida
        EngancharLocalizeStringEvents();
        Refrescar();
    }

    /// <summary>Reescribe todos los textos vivos con los prompts del device actual.</summary>
    public static void Refrescar()
    {
        _muertos.Clear();

        foreach (var par in _crudos)
        {
            TMP_Text destino = par.Key;

            //"fake null" de Unity: el componente fue destruido pero la referencia sigue viva.
            //Comparar con == null es justamente lo que detecta ese caso
            if (destino == null)
            {
                _muertos.Add(destino);
                continue;
            }

            EscribirProcesado(destino, par.Value);
        }

        for (int i = 0; i < _muertos.Count; i++)
        {
            _crudos.Remove(_muertos[i]);
        }

        if (_muertos.Count > 0)
        {
            Debug.Log($"[LocalizedText] refresco por cambio de device: {_crudos.Count} textos vivos, saque {_muertos.Count} destruidos");
        }
        _muertos.Clear();
    }

    // ------------------------------------------- puente con LocalizeStringEvent

    // Hay un CUARTO camino de texto localizado que no pasa por ninguna corrutina: el
    // componente LocalizeStringEvent de Unity Localization, cableado en el inspector, que
    // escribe directo en TMP_Text.set_text via UnityEvent. Asi se muestran el overlay de
    // main quest ("toca E para continuar"), la pantalla de controles del Flap y los overlays
    // de derrota/victoria: por eso ninguno se traducia al joystick.
    //
    // En vez de tocar esos prefabs uno por uno (y que Diego tenga que cablear algo), los
    // enganchamos por codigo: se les agrega un listener EXTRA al mismo UnityEvent. Unity
    // invoca primero las llamadas persistentes (el set_text con el texto crudo) y despues
    // las agregadas por codigo, asi que la nuestra siempre escribe ultima y gana.

    static void EngancharLocalizeStringEvents()
    {
        LocalizeStringEvent[] eventos = Object.FindObjectsOfType<LocalizeStringEvent>(true);

        for (int i = 0; i < eventos.Length; i++)
        {
            LocalizeStringEvent evento = eventos[i];
            if (evento == null || _enganchados.Contains(evento.GetInstanceID()))
            {
                continue;
            }

            TMP_Text destino = BuscarTMPDestino(evento);
            if (destino == null)
            {
                //el LocalizeStringEvent escribe en otra cosa (un Text viejo, un metodo propio):
                //no es un error, simplemente no tenemos donde registrar el crudo
                continue;
            }

            _enganchados.Add(evento.GetInstanceID());
            evento.OnUpdateString.AddListener(texto => Aplicar(destino, texto));

            //el evento ya pudo haber escrito el texto crudo antes de que lo enganchemos
            //(OnEnable corre antes que esto): lo forzamos a re-emitir
            evento.RefreshString();
        }
    }

    static TMP_Text BuscarTMPDestino(LocalizeStringEvent evento)
    {
        var unityEvent = evento.OnUpdateString;
        if (unityEvent == null)
        {
            return null;
        }

        int cantidad = unityEvent.GetPersistentEventCount();
        for (int i = 0; i < cantidad; i++)
        {
            TMP_Text tmp = unityEvent.GetPersistentTarget(i) as TMP_Text;
            if (tmp != null)
            {
                return tmp;
            }
        }

        return null;
    }
}
