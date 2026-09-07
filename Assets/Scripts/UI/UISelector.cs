using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Helper para que la UI sea navegable con joystick.
///
/// Por que existe: en uGUI la navegacion por teclado/gamepad SOLO arranca cuando el
/// EventSystem tiene un objeto seleccionado. Con mouse no se nota (el hover/click
/// seleccionan solos), pero con joystick, si nadie selecciono nada, el stick no mueve
/// nada y el boton Submit (B) no le pega a ningun boton. Como el EventSystem de las
/// escenas tiene m_FirstSelected vacio, alguien tiene que seleccionar por codigo cada
/// vez que se prende un menu u overlay. Eso hace esta clase.
///
/// Todo el enganche es por codigo a proposito: no requiere cablear nada en el editor.
/// </summary>
public static class UISelector
{
    // Cada pedido de seleccion incrementa la generacion. Sirve para que un reintento
    // diferido (ver ReintentarSeleccion) se cancele solo si mientras tanto alguien pidio
    // otra seleccion o llamo a Limpiar(): si no, el reintento podria "resucitar" un boton
    // en un menu que ya se cerro, y el B del joystick quedaria apretando un fantasma.
    static int _generacion = 0;

    /// <summary>Selecciona el primer boton navegable que encuentre debajo de 'raiz'.</summary>
    public static void SeleccionarPrimero(GameObject raiz)
    {
        SeleccionarPrimero(raiz, true);
    }

    /// <summary>
    /// Igual que el anterior, pero con la opcion de no avisar si no hay nada seleccionable.
    /// Los overlays que se cierran con E/B y no tienen botones (defeat, mainquest) usan
    /// avisarSiNoHay=false: si no, cada muerte del jugador dejaria un warning en la consola.
    /// </summary>
    public static void SeleccionarPrimero(GameObject raiz, bool avisarSiNoHay)
    {
        if (raiz == null)
        {
            Debug.LogWarning("[UISelector] SeleccionarPrimero: me pasaron una raiz null, no selecciono nada");
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            Debug.LogWarning($"[UISelector] SeleccionarPrimero: no hay EventSystem.current (raiz '{raiz.name}'), no selecciono nada");
            return;
        }

        if (!raiz.activeInHierarchy)
        {
            Debug.LogWarning($"[UISelector] SeleccionarPrimero: la raiz '{raiz.name}' esta desactivada, no selecciono nada");
            return;
        }

        Selectable elegido = BuscarPrimerNavegable(raiz, true);
        if (elegido == null)
        {
            if (avisarSiNoHay)
            {
                Debug.LogWarning($"[UISelector] SeleccionarPrimero: no encontre ningun Selectable activo e interactuable debajo de '{raiz.name}', no selecciono nada");
            }
            return;
        }

        Aplicar(eventSystem, elegido.gameObject);

        // uGUI a veces se come un SetSelectedGameObject hecho en el MISMO frame en que el
        // objeto se acaba de activar (el modulo de input procesa su seleccion despues, y
        // puede pisarla). Por eso ademas de seleccionar ya, reintentamos al frame siguiente
        // si quedo sin seleccion. La corrutina la hospeda el propio EventSystem: es un
        // MonoBehaviour que siempre existe cuando hay UI, asi no hay que crear ningun
        // GameObject auxiliar. 'yield return null' corre igual con Time.timeScale = 0
        // (el flap pausa el juego al abrirse).
        _generacion++;
        if (eventSystem.isActiveAndEnabled)
        {
            eventSystem.StartCoroutine(ReintentarSeleccion(raiz, _generacion));
        }
    }

    /// <summary>
    /// Solo selecciona si el jugador esta usando joystick. Con mouse no queremos robarle
    /// el foco ni dejar botones resaltados que nadie pidio.
    /// </summary>
    public static void SeleccionarPrimeroSiJoystick(GameObject raiz)
    {
        SeleccionarPrimeroSiJoystick(raiz, true);
    }

    /// <summary>Variante con el flag de aviso (ver SeleccionarPrimero(raiz, avisarSiNoHay)).</summary>
    public static void SeleccionarPrimeroSiJoystick(GameObject raiz, bool avisarSiNoHay)
    {
        if (!HayQueSeleccionar())
        {
            return;
        }

        SeleccionarPrimero(raiz, avisarSiNoHay);
    }

    /// <summary>Deselecciona lo que haya, para que el joystick no siga "apretando" un boton fantasma.</summary>
    public static void Limpiar()
    {
        //cancela cualquier reintento diferido que estuviera en camino
        _generacion++;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return; //sin EventSystem no hay nada seleccionado: no es un error, es un no-op
        }

        if (eventSystem.currentSelectedGameObject == null)
        {
            return;
        }

        Debug.Log($"[UISelector] Limpiar: deselecciono '{eventSystem.currentSelectedGameObject.name}'");
        eventSystem.SetSelectedGameObject(null);
    }

    /// <summary>true si el jugador esta con joystick (o al menos tiene uno enchufado).</summary>
    static bool HayQueSeleccionar()
    {
        return InputHub.UltimoDeviceFueJoystick || InputHub.HayJoystickConectado;
    }

    static Selectable BuscarPrimerNavegable(GameObject raiz, bool avisarFallback)
    {
        //false = solo los hijos ACTIVOS: no queremos seleccionar un boton de un display apagado
        Selectable[] candidatos = raiz.GetComponentsInChildren<Selectable>(false);

        Selectable primerInteractuable = null;

        for (int i = 0; i < candidatos.Length; i++)
        {
            Selectable candidato = candidatos[i];
            if (candidato == null || !candidato.IsInteractable())
            {
                continue;
            }

            if (primerInteractuable == null)
            {
                primerInteractuable = candidato;
            }

            if (candidato.navigation.mode != Navigation.Mode.None)
            {
                return candidato; //el caso lindo: ademas de seleccionable, se puede navegar con el stick
            }
        }

        // Fallback: en este proyecto casi todos los botones vienen de Assets/Prefabs/UI/Button.prefab,
        // que tiene Navigation = None (entre otros, los DOS botones del victory overlay). Si filtraramos
        // esos, el victory overlay se quedaria sin seleccion y el jugador con joystick seguiria trabado,
        // que es justo el bug que venimos a arreglar. Seleccionarlo igual sirve: el Submit (boton B) le
        // pega al objeto seleccionado aunque su navigation sea None; lo unico que no anda es MOVERSE
        // entre botones con el stick, y eso se arregla poniendo Navigation = Automatic en el prefab.
        if (primerInteractuable != null && avisarFallback)
        {
            Debug.LogWarning($"[UISelector] debajo de '{raiz.name}' todos los botones tienen Navigation = None: " +
                             $"selecciono '{primerInteractuable.name}' igual (el boton B le pega), pero el stick no va a poder " +
                             "moverse entre botones hasta que se les ponga Navigation = Automatic en el prefab");
        }

        return primerInteractuable;
    }

    static void Aplicar(EventSystem eventSystem, GameObject objetivo)
    {
        //limpiar primero: si ya habia otro seleccionado, uGUI no siempre dispara el OnSelect del nuevo
        eventSystem.SetSelectedGameObject(null);
        eventSystem.SetSelectedGameObject(objetivo);
        Debug.Log($"[UISelector] seleccionado '{objetivo.name}'");
    }

    static IEnumerator ReintentarSeleccion(GameObject raiz, int generacion)
    {
        yield return null; //un frame: el EventSystem ya termino de procesar el cuadro en el que nos llamaron

        if (generacion != _generacion)
        {
            yield break; //alguien pidio otra seleccion o llamo a Limpiar(): este reintento quedo viejo
        }

        if (raiz == null || !raiz.activeInHierarchy)
        {
            yield break; //el menu se cerro mientras tanto
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            yield break;
        }

        if (eventSystem.currentSelectedGameObject != null && eventSystem.currentSelectedGameObject.activeInHierarchy)
        {
            yield break; //la seleccion original quedo bien (o el mouse selecciono otra cosa): no la pisamos
        }

        Selectable elegido = BuscarPrimerNavegable(raiz, false); //el warning del fallback ya salio en el intento original
        if (elegido == null)
        {
            yield break;
        }

        Debug.Log($"[UISelector] la seleccion se perdio en el frame de la activacion, reintento sobre '{raiz.name}'");
        Aplicar(eventSystem, elegido.gameObject);
    }
}
