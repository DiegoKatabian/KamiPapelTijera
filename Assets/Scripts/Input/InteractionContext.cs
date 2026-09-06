using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sabe si en este instante hay algo con que interactuar cerca de Kami.
///
/// Por que existe: en el joystick el boton B es CONTEXTUAL (decision de diseno, spec 004):
/// si hay algo con que interactuar interactua, y si no, ataca. Para eso hace falta una
/// respuesta barata y confiable a "hay algo?" sin que PlayerController tenga que conocer
/// a todos los triggers del nivel.
///
/// Como se llena: cada <see cref="TriggerScript"/> que declara ser interactuable
/// (<see cref="TriggerScript.EsInteractuable"/>) se anota aca al entrar el player y se borra
/// al salir. Ademas cuentan como "hay interaccion" el dialogo abierto y el overlay de derrota,
/// que consumen el mismo evento OnPlayerPressedE sin ser triggers.
///
/// El teclado NO pasa por aca: E siempre interactua y el click siempre ataca, como siempre.
/// </summary>
public static class InteractionContext
{
    static readonly HashSet<Object> _activos = new HashSet<Object>();

    static bool _suscriptoASceneLoaded = false;

    /// <summary>Hay algo con que interactuar: un trigger encima, un dialogo abierto o un overlay esperando.</summary>
    public static bool HayInteraccionDisponible
    {
        get
        {
            if (HayDialogoAbierto())
            {
                return true;
            }

            //los triggers destruidos (cambio de pagina, pickups consumidos) quedan como
            //referencias "fake null" de Unity: los limpiamos en el momento de preguntar
            _activos.RemoveWhere(o => o == null);
            return _activos.Count > 0;
        }
    }

    static bool HayDialogoAbierto()
    {
        //LevelManager.inDialogue tapa dialogo, cambio de pagina y overlay de derrota:
        //en todos esos casos el boton tiene que avanzar/cerrar, nunca atacar
        return LevelManager.Instance != null && LevelManager.Instance.inDialogue;
    }

    public static void Registrar(Object interactuable)
    {
        if (interactuable == null)
        {
            return;
        }

        AsegurarLimpiezaEntreEscenas();
        _activos.Add(interactuable);
    }

    public static void Desregistrar(Object interactuable)
    {
        if (interactuable == null)
        {
            return;
        }

        _activos.Remove(interactuable);
    }

    /// <summary>
    /// El set es estatico, asi que sobrevive al cambio de escena y se quedaria con basura de la
    /// escena anterior (triggers destruidos que ya no van a desregistrarse solos). Nos suscribimos
    /// una unica vez para vaciarlo en cada carga.
    /// </summary>
    static void AsegurarLimpiezaEntreEscenas()
    {
        if (_suscriptoASceneLoaded)
        {
            return;
        }

        _suscriptoASceneLoaded = true;
        SceneManager.sceneLoaded += (scene, mode) => _activos.Clear();
    }

    /// <summary>Solo para debug/tests: cuantos interactuables tiene al player encima ahora mismo.</summary>
    public static int CantidadActivos
    {
        get
        {
            _activos.RemoveWhere(o => o == null);
            return _activos.Count;
        }
    }
}
