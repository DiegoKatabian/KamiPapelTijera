using UnityEngine;

/// <summary>
/// Issue #41 ronda 2, punto 3: oculta el GameObject salvo que el jugador este usando
/// joystick. Lo usa el cartelito "L1 / R1 para cambiar de seccion" arriba de los iconos
/// del Flap (Assets/Prefabs/UI/FlapManager.prefab, GO "FlapTabHint"): con teclado/mouse
/// decirle "L1 / R1" al jugador es informacion inutil, y esos ejes ni siquiera tienen
/// binding de teclado (ver InputHub.TabSiguienteDown/TabAnteriorDown).
///
/// Mismo patron que TutorialPromptVisual: se suscribe a InputHub.OnDeviceCambio para
/// reaccionar en caliente si el jugador cambia de device a mitad de partida, y aplica el
/// estado actual en OnEnable (no solo en Awake) porque el objeto puede activarse ya con
/// el joystick puesto.
/// </summary>
public class SoloConJoystick : MonoBehaviour
{
    void OnEnable()
    {
        ActualizarVisibilidad();
        InputHub.OnDeviceCambio += ActualizarVisibilidad;
    }

    void OnDisable()
    {
        //OnDeviceCambio es un evento ESTATICO: sin desuscribirse quedan referencias
        //colgadas a objetos ya destruidos (y NullReferenceException en el proximo cambio)
        InputHub.OnDeviceCambio -= ActualizarVisibilidad;
    }

    void ActualizarVisibilidad()
    {
        //SetActive(false) disparado desde el propio OnEnable es seguro en Unity: termina
        //de correr este OnEnable y recien despues llama a OnDisable (que desuscribe arriba).
        gameObject.SetActive(InputHub.UltimoDeviceFueJoystick);
    }
}
