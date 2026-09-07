using UnityEngine;

//snapshot de los inputs de un frame. el controller lo llena, el model decide que hacer con el
public struct PlayerInputs
{
    public float hor;    //con smoothing de unity: para la fisica (aceleracion suave)
    public float ver;
    public float horRaw; //sin smoothing: para decidir estados (al soltar teclado cae a 0 al instante)
    public float verRaw;
    public bool jumpDown;
    public bool jumpUp;
}

public class PlayerController
{
    //solo leo inputs, no decido nada.
    //las decisiones de que se puede hacer las toma el model mirando el estado actual.
    //los botones que hacen cosas en otros objetos (no en player) disparan eventos globales, como siempre.
    //los nombres de las teclas/botones NO viven aca: viven en InputHub, que es el unico que los conoce.

    public PlayerInputs Inputs;

    Player _player;

    //el apreton de A de ESTE frame se resolvio como interactuar (y por lo tanto no salta)
    bool _gamepadInteractua;

    public PlayerController(Player player)
    {
        _player = player;
    }

    public void CheckControls() //a este lo disparo en el update
    {
        Inputs = default;

        if (InputHub.CorrerDown)
        {
            _player.IsSprinting = true;
        }

        if (InputHub.CorrerUp)
        {
            _player.IsSprinting = false;
        }

        if (InputHub.MuteDown)
        {
            EventManager.Trigger(Evento.OnPlayerPressedM);
        }

        if (!LevelManager.Instance.agency)
        {
            return;
        }

        //El boton A del joystick es CONTEXTUAL: si hay algo con que interactuar interactua, y si
        //no, salta. Asi un chico usa un solo boton para "hacer" y no tiene que aprender cual es
        //cual. El teclado NO cambia: E siempre interactua y el espacio siempre salta.
        //Time.timeScale > 0 descarta el menu abierto (pausa el juego): ahi A es el Submit de la UI.
        _gamepadInteractua = InputHub.AccionGamepadDown
                             && InteractionContext.HayInteraccionDisponible
                             && Time.timeScale > 0f;

        //InteractDown ya incluye al boton A (comparten el eje "Interact", que ademas es el Submit
        //del EventSystem), asi que con esto solo alcanza para los dos devices.
        if (InputHub.InteractDown)
        {
            EventManager.Trigger(Evento.OnPlayerPressedE);
        }

        //B ataca siempre, sin contexto. El gate de timeScale evita el tijeretazo por atras con el
        //menu abierto (donde igual OnPrimaryClick no llegaria a nada util).
        if (InputHub.AtaqueTecladoDown || (InputHub.AtaqueGamepadDown && Time.timeScale > 0f))
        {
            _player.OnPrimaryClick();
        }

        if (InputHub.OpcionesDown)
        {
            EventManager.Trigger(Evento.OnPlayerPressedEsc);
        }

        if (InputHub.InventarioDown)
        {
            EventManager.Trigger(Evento.OnPlayerPressedI);
        }

        if (InputHub.QuestsDown)
        {
            EventManager.Trigger(Evento.OnPlayerPressedU);
        }

        if (LevelManager.Instance.inDialogue) //en dialogo no se captura movimiento ni salto
        {
            return;
        }

        Vector2 movimiento = InputHub.Movimiento;
        Vector2 movimientoRaw = InputHub.MovimientoRaw;
        Inputs.hor = movimiento.x;
        Inputs.ver = movimiento.y;
        Inputs.horRaw = movimientoRaw.x;
        Inputs.verRaw = movimientoRaw.y;
        //Si el apreton de A se resolvio como "interactuar", no tiene que saltar ademas: es el
        //mismo boton fisico. El teclado no se ve afectado (el espacio no pasa por _gamepadInteractua),
        //salvo el caso irrelevante de apretar espacio en el mismo frame en que se interactua.
        Inputs.jumpDown = InputHub.SaltoDown && !_gamepadInteractua;
        Inputs.jumpUp = InputHub.SaltoUp;

        if (Inputs.jumpDown)
        {
            EventManager.Trigger(Evento.OnPlayerPressedSpace);
        }
    }
}
