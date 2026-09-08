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
        AplicarVisibilidadSegunDevice();
        InputHub.OnDeviceCambio += AplicarVisibilidadSegunDevice;
    }

    void OnDestroy()
    {
        //OnDeviceCambio es estatico: sin desuscribirse queda una referencia colgada a este
        //manager despues de cambiar de escena
        InputHub.OnDeviceCambio -= AplicarVisibilidadSegunDevice;
    }

    void AplicarVisibilidadSegunDevice()
    {
        Cursor.visible = !InputHub.UltimoDeviceFueJoystick && isCursorAlwaysVisible;
    }
}
