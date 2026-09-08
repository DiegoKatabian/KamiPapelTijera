using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
//using static UnityEditor.Progress;

public enum CursorType
{
    ClosedHand,
    OpenHand
}
public class CursorManager : Singleton<CursorManager>
{
    public Texture2D openHand, closedHand;

    [SerializeField] bool isCursorAlwaysVisible = true;

    [SerializeField]
    [Tooltip("Segundos sin mover el mouse antes de esconder el cursor del sistema (jugando con " +
             "teclado/mouse). No aplica mientras el origami esta abierto y se juega con mouse.")]
    float segundosInactividadParaEsconderCursor = 7f;

    Vector3 _ultimaPosicionMouse;
    float _timerInactividad;

    public void SetCursor(CursorType type)
    {
        if (type == CursorType.ClosedHand)
        {
            Cursor.SetCursor(closedHand, Vector2.zero, CursorMode.Auto);
        }
        else
        {
            Cursor.SetCursor(openHand, Vector2.zero, CursorMode.Auto);
        }
    }

    public void ShowCursor(bool value)
    {
        //Jugando con joystick el cursor del sistema no pinta nada en pantalla (el origami dibuja
        //su propio cursor virtual), asi que el device manda sobre isCursorAlwaysVisible. Ese flag
        //sigue siendo la configuracion del proyecto para teclado/mouse: no lo pisamos, lo tapamos
        //solo mientras el jugador este con el joystick.
        if (InputHub.UltimoDeviceFueJoystick)
        {
            Cursor.visible = false;
            return;
        }

        if (isCursorAlwaysVisible)
        {
            Cursor.visible = true;
            return;
        }

        Cursor.visible = value;
    }

    void Start()
    {
        //el jugador puede arrancar la escena ya con el joystick en la mano
        _ultimaPosicionMouse = Input.mousePosition;
        AplicarVisibilidadSegunDevice();
        InputHub.OnDeviceCambio += AplicarVisibilidadSegunDevice;
    }

    void OnDestroy()
    {
        //OnDeviceCambio es estatico: sin desuscribirse queda una referencia colgada a este
        //manager despues de cambiar de escena
        InputHub.OnDeviceCambio -= AplicarVisibilidadSegunDevice;
    }

    void Update()
    {
        //Tocar el getter todos los frames es lo que hace que InputHub detecte a tiempo el cambio
        //de device en gameplay normal. OnDeviceCambio solo se dispara cuando algo LLAMA a
        //UltimoDeviceFueJoystick (issue #41.7): fuera de tooltips/UI/origami, nada lo consultaba
        //en el loop principal, asi que volver al joystick podia pasar frames enteros sin que el
        //evento saltara y el cursor del sistema se quedaba pegado en pantalla.
        bool esJoystick = InputHub.UltimoDeviceFueJoystick;

        if (esJoystick)
        {
            //el device ya manda ocultar el cursor (ver AplicarVisibilidadSegunDevice); no hace
            //falta timer de inactividad y conviene resetearlo para no arrastrar cuenta vieja
            _timerInactividad = 0f;
            _ultimaPosicionMouse = Input.mousePosition;
            return;
        }

        ActualizarTimeoutDeInactividad();
    }

    /// <summary>
    /// Con teclado/mouse activo, esconde el cursor del sistema si el mouse no se movio en
    /// <see cref="segundosInactividadParaEsconderCursor"/> segundos. No aplica durante el origami
    /// jugado con mouse (PlayerState.Casting): ahi el cursor del sistema ES el puntero del
    /// minijuego, esconderlo a mitad de un arrastre rompe la interaccion.
    /// </summary>
    void ActualizarTimeoutDeInactividad()
    {
        Vector3 posicionMouse = Input.mousePosition;
        bool seMovio = posicionMouse != _ultimaPosicionMouse;
        _ultimaPosicionMouse = posicionMouse;

        if (seMovio)
        {
            _timerInactividad = 0f;
            //por si el timeout lo habia escondido, restaurar la visibilidad que le toca al device
            AplicarVisibilidadSegunDevice();
            return;
        }

        if (EstaJugandoOrigamiConMouse())
        {
            _timerInactividad = 0f;
            return;
        }

        //unscaled: el Flap y el origami pueden pausar/ralentizar el juego y el timeout tiene que
        //seguir corriendo igual (mismo criterio que GamepadCursor.Update)
        _timerInactividad += Time.unscaledDeltaTime;

        if (_timerInactividad >= segundosInactividadParaEsconderCursor && Cursor.visible)
        {
            Cursor.visible = false;
            Debug.Log("[CursorManager] cursor escondido por inactividad");
        }
    }

    bool EstaJugandoOrigamiConMouse()
    {
        //en este punto ya sabemos que el device activo NO es joystick (Update corta antes); si
        //Kami esta casteando un origami, el mouse es el puntero del minijuego
        Player player = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        return player != null && player.CurrentState == PlayerState.Casting;
    }

    void AplicarVisibilidadSegunDevice()
    {
        Cursor.visible = !InputHub.UltimoDeviceFueJoystick && isCursorAlwaysVisible;
        //cualquier evento de cambio de device cuenta como actividad: no queremos que el timeout
        //dispare un frame despues de que el jugador acaba de tocar el mouse
        _timerInactividad = 0f;
    }
}
