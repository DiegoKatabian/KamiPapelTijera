using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultipleRectCheck : MonoBehaviour
{
    //este multiple rectangle check solo nace cuando estas dentro de un sello (por ahora origami)
    //asi corre el update pero solo cuando lo necesitamos
    //aca esta la mecanica de arrastrar el mouse y eso

    //el minijuego no habla de "mouse" sino de "puntero": con teclado el puntero es el mouse y con
    //joystick es el cursor virtual (GamepadCursor). Asi toda la logica de la ruta (los
    //RectangleContainsScreenPoint, los sonidos, CompleteRoute) queda EXACTAMENTE igual para los dos
    //y el joystick hereda gratis el comportamiento ya probado con mouse.

    [SerializeField, Tooltip("Margen extra en pixeles alrededor de la ruta cuando se juega con joystick. Con mouse la ruta es exacta como siempre.")]
    float _toleranciaJoystickPx = 80f;

    Origami desiredOrigami;

    bool invocando = false;
    bool arrastrando = false;

    //se decide UNA sola vez, al arrancar el minijuego (ver StartOrigami): si lo chequearamos cada
    //frame, rozar el mouse en medio de un pliegue cambiaria el puntero de golpe y la flecha saltaria
    bool _usandoJoystick = false;

    //buffer reusado por DistanciaAlRectEnPantalla: GetWorldCorners pide un array de 4 y no queremos
    //allocar uno por rectangulo por frame
    readonly Vector3[] _esquinas = new Vector3[4];

    public MultipleRectCheck SetOrigami(Origami ori)
    {
        desiredOrigami = ori;
        return this;
    }

    void OnDestroy()
    {
        //red de seguridad: el camino normal (TriggerOrigami.OnExitBehaviour) pasa por EndOrigami
        //antes de destruirnos, pero el GamepadCursor es DontDestroyOnLoad y si nos destruyeran por
        //otro lado (cambio de escena en medio de un pliegue) quedaria dibujado para siempre
        EsconderCursorJoystick();
    }

    // ------------------------------------------------------------------ puntero

    Vector3 PosicionPuntero
    {
        get
        {
            if (_usandoJoystick && GamepadCursor.Existe)
            {
                return GamepadCursor.Instancia.Posicion;
            }
            return Input.mousePosition;
        }
    }

    bool PunteroDown => _usandoJoystick ? InputHub.AccionGamepadDown : Input.GetMouseButtonDown(0);

    bool PunteroUp => _usandoJoystick ? InputHub.AccionGamepadUp : Input.GetMouseButtonUp(0);

    void Update()
    {
        //toggle: E/Enter/B arranca el minijuego, apretar de nuevo lo cancela. ya no hay que holdear
        if (!invocando)
        {
            if (InputHub.InteractDown)
            {
                StartOrigami(desiredOrigami);
                //cortamos el frame: con joystick el MISMO boton B que abre el minijuego es tambien
                //el de agarrar la flecha, asi que sin este return el apreton que abre agarraria de
                //una y un toque corto de B terminaria en "soltaste mal" al instante.
                //Con mouse esto no cambia nada: en el frame de la E no habia arrastre posible.
                return;
            }
        }
        else
        {
            if (CancelarDown())
            {
                //print("invocacion cancelada x apretar E de nuevo");
                EndOrigami(desiredOrigami);
                return;
            }
        }

        if (invocando && PunteroDown)
        {
            //chequeo si el puntero esta dentro de la imagen de inicio, y habilito el arranque
            if (RectTransformUtility.RectangleContainsScreenPoint(desiredOrigami.origamiRoutes[desiredOrigami.currentRouteIndex].inicioRectangle, PosicionPuntero))
            {
                arrastrando = true;
                AudioManager.instance.PlayRandom("PaperFold01", "PaperFold02");
                AudioManager.instance.PlayByName("PaperFoldLoop");
                CursorManager.Instance.SetCursor(CursorType.ClosedHand);
                SetAgarrandoCursorJoystick(true);
            }
            else
            {
                Debug.Log("invocaci?n cancelada x empezar en un lugar incorrecto");
                EndOrigami(desiredOrigami);
            }
        }

        //chequeo si el jugador solto el puntero mientras arrastraba
        if (invocando && arrastrando && PunteroUp)
        {
            CursorManager.Instance.SetCursor(CursorType.OpenHand);
            SetAgarrandoCursorJoystick(false);

            //chequeo si el jugador solto sobre la meta
            if (RectTransformUtility.RectangleContainsScreenPoint(desiredOrigami.origamiRoutes[desiredOrigami.currentRouteIndex].finalRectangle, PosicionPuntero))
            {
                //Debug.Log("invocaci?n exitosa");
                AudioManager.instance.PlayRandom("PaperFold01", "PaperFold02");
                AudioManager.instance.StopByName("PaperFoldLoop");
                //desiredOrigami.CompleteRoute();

                if (desiredOrigami.CompleteRoute())
                {
                    EndOrigami(desiredOrigami);
                }
                else
                {
                    //quedan pliegues: el cursor de joystick tiene que saltar al inicio de la ruta
                    //nueva, si no queda tirado donde termino la anterior
                    ColocarCursorJoystickEnInicio();
                }
            }
            else
            {
                //Debug.Log("invocaci?n cancelada x soltar mal");
                AudioManager.instance.PlayByName("Origami_Fail_Crumble", 1, 0.05f);
                EndOrigami(desiredOrigami);
            }

            arrastrando = false;
        }

        //bool encimaDeAlgunRectangulo = false; //solo lo usaba el bloque comentado de abajo

        foreach (RectTransform rectTransform in desiredOrigami.origamiRoutes[desiredOrigami.currentRouteIndex].routeRectangles) //chequeo si estoy encima de algun rectangulo
        {
            if (PunteroSobreRectangulo(rectTransform))
            {
                if (arrastrando)
                {
                    desiredOrigami.origamiRoutes[desiredOrigami.currentRouteIndex].SetImagePosition(PosicionPuntero);
                }
                //encimaDeAlgunRectangulo = true; //si s?, todo bien
                break;
            }
        }

        //if (arrastrando && !encimaDeAlgunRectangulo) //si no, end origami
        //{
        //    //me sal? de la ruta
        //    arrastrando = false;
        //    AudioManager.instance.PlayByName("Origami_Fail_Crumble", 1, 0.05f);
        //    Debug.Log("invocaci?n cancelada x salir de la ruta");
        //    EndOrigami(desiredOrigami);
        //}

    }

    /// <summary>
    /// Apretaron el boton de cancelar el minijuego en curso. Con joystick filtramos el boton B
    /// porque ahi B es tambien el de agarrar la flecha: si dejaramos que cancele, el mismo apreton
    /// con el que el jugador agarra el papel le cerraria el origami. E/Enter siguen cancelando
    /// siempre, y con mouse el comportamiento queda identico al de antes.
    /// </summary>
    bool CancelarDown()
    {
        if (_usandoJoystick)
        {
            return InputHub.InteractDown && !InputHub.AccionGamepadDown;
        }
        return InputHub.InteractDown;
    }

    /// <summary>
    /// El puntero esta sobre el rectangulo. Con mouse es el chequeo exacto de siempre; con joystick
    /// aceptamos ademas un margen de <see cref="_toleranciaJoystickPx"/> pixeles alrededor, porque
    /// mantener un stick adentro de un camino finito es durisimo (mas todavia para un chico).
    /// </summary>
    bool PunteroSobreRectangulo(RectTransform rect)
    {
        Vector3 puntero = PosicionPuntero;

        if (RectTransformUtility.RectangleContainsScreenPoint(rect, puntero))
        {
            return true;
        }

        if (!_usandoJoystick || _toleranciaJoystickPx <= 0f)
        {
            return false;
        }

        return DistanciaAlRectEnPantalla(rect, puntero) <= _toleranciaJoystickPx;
    }

    /// <summary>
    /// Distancia en pixeles de pantalla entre un punto y el rectangulo (0 si esta adentro).
    /// Usa el bounding box en pantalla de las 4 esquinas: alcanza de sobra para una tolerancia,
    /// y si el rect estuviera rotado el margen sale un poco mas generoso, nunca mas chico.
    /// </summary>
    float DistanciaAlRectEnPantalla(RectTransform rect, Vector2 punto)
    {
        Camera camara = CamaraDelRect(rect);
        rect.GetWorldCorners(_esquinas);

        Vector2 minimo = RectTransformUtility.WorldToScreenPoint(camara, _esquinas[0]);
        Vector2 maximo = minimo;

        for (int i = 1; i < _esquinas.Length; i++)
        {
            Vector2 enPantalla = RectTransformUtility.WorldToScreenPoint(camara, _esquinas[i]);
            minimo = Vector2.Min(minimo, enPantalla);
            maximo = Vector2.Max(maximo, enPantalla);
        }

        float dx = Mathf.Max(minimo.x - punto.x, 0f, punto.x - maximo.x);
        float dy = Mathf.Max(minimo.y - punto.y, 0f, punto.y - maximo.y);
        return Mathf.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// La camara con la que hay que convertir mundo -&gt; pantalla para ese RectTransform.
    /// En un canvas Screen Space - Overlay hay que pasar null: si le pasamos una camara, el punto
    /// sale corrido y no matchea con lo que devuelve RectangleContainsScreenPoint (que para overlay
    /// tambien usa null). En cualquier otro render mode va la camara del canvas.
    /// </summary>
    Camera CamaraDelRect(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning($"[MultipleRectCheck] {rect.name} no cuelga de ningun Canvas, asumo Screen Space - Overlay");
            return null;
        }

        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    Vector2 CentroEnPantalla(RectTransform rect)
    {
        return RectTransformUtility.WorldToScreenPoint(CamaraDelRect(rect), rect.position);
    }

    /// <summary>Planta el cursor virtual en el centro de la flecha verde de la ruta actual.</summary>
    void ColocarCursorJoystickEnInicio()
    {
        if (!_usandoJoystick)
        {
            return;
        }

        RectTransform inicio = desiredOrigami.origamiRoutes[desiredOrigami.currentRouteIndex].inicioRectangle;

        if (inicio == null)
        {
            Debug.LogWarning("[MultipleRectCheck] la ruta no tiene inicioRectangle, muestro el cursor en el centro de la pantalla");
            GamepadCursor.Instancia.Mostrar(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            return;
        }

        //arranca parado justo donde tiene que agarrar, asi no tiene que buscar la flecha con el stick
        GamepadCursor.Instancia.Mostrar(CentroEnPantalla(inicio));
    }

    void SetAgarrandoCursorJoystick(bool agarrando)
    {
        if (_usandoJoystick && GamepadCursor.Existe)
        {
            GamepadCursor.Instancia.SetAgarrando(agarrando);
        }
    }

    /// <summary>
    /// Esconde el cursor virtual si existe. Preguntamos por Existe y no por _usandoJoystick para
    /// que ningun camino de salida deje el cursor colgado en pantalla, y para no crear el
    /// GamepadCursor de la nada justo cuando el minijuego se esta cerrando.
    /// </summary>
    void EsconderCursorJoystick()
    {
        if (GamepadCursor.Existe)
        {
            GamepadCursor.Instancia.Esconder();
        }
    }

    public void StartOrigami(Origami origami)
    {
        if (!origami.wasUsed) //me parece que esto no deberia preguntarse aca
        {
            //print("arranca la invocacion");
            origami.gameObject.SetActive(true);
            invocando = true;

            //se decide aca y no cada frame: si el jugador roza el mouse en medio de un pliegue no
            //queremos que el puntero cambie de golpe y la flecha pegue un salto
            _usandoJoystick = InputHub.UltimoDeviceFueJoystick;
            Debug.Log($"[MultipleRectCheck] arranca el origami con {(_usandoJoystick ? "joystick" : "mouse")}");

            TooltipManager.Instance.ShowTooltip(origami.tooltipMessage, origami.postItColor);
            AudioManager.instance.PlayRandom("MagicChannelingLoop01", "MagicChannelingLoop02");
            CameraManager.Instance.SetCamera(CameraMode.CloseUp);
            EventManager.Trigger(Evento.OnOrigamiStart);
            //print("rect check: mando a actualizar");
            origami.TriggerPliegueTextUpdater();
            CursorManager.Instance.ShowCursor(true);
            ColocarCursorJoystickEnInicio();

        }

    }

    public void EndOrigami(Origami origami)
    {
        invocando = false;
        arrastrando = false;
        origami.FailOrigami();
        origami.gameObject.SetActive(false);
        //print("invocacion cancelada");
        TooltipManager.Instance.HideTooltip();
        AudioManager.instance.StopByName("PaperFoldLoop");
        AudioManager.instance.StopByName("MagicChannelingLoop01", "MagicChannelingLoop02");
        CursorManager.Instance.SetCursor(CursorType.OpenHand);
        EventManager.Trigger(Evento.OnOrigamiEnd);
        CursorManager.Instance.ShowCursor(false);
        CameraManager.Instance.SetCamera(CameraMode.Normal);
        //TriggerOrigami.OnExitBehaviour tambien pasa por aca justo antes de destruir este objeto:
        //por eso el cursor se esconde SIEMPRE en EndOrigami y no solo en los caminos "de juego"
        EsconderCursorJoystick();


    }
}
