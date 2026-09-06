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

        //El boton B del joystick es CONTEXTUAL (spec 004): si hay algo con que interactuar
        //interactua, y si no ataca. Asi el chico usa un solo boton para "hacer" y no tiene que
        //aprender cual es cual. El teclado NO cambia: E siempre interactua, el click siempre ataca.
        //Time.timeScale > 0 descarta el caso del menu Flap abierto (pausa el juego): ahi B es
        //el Submit de la UI y no tiene que sacar un tijeretazo por atras.
        bool accionGamepad = InputHub.AccionGamepadDown;
        bool gamepadAtaca = accionGamepad
                            && !InteractionContext.HayInteraccionDisponible
                            && Time.timeScale > 0f;

        //InteractDown ya incluye al boton B (comparten el eje "Interact", que ademas es el Submit
        //del EventSystem). Cuando ese B se resolvio como ataque, no queremos ademas disparar el
        //evento de interactuar: por eso el !gamepadAtaca.
        if (InputHub.InteractDown && !gamepadAtaca)
        {
            EventManager.Trigger(Evento.OnPlayerPressedE);
        }

        if (InputHub.AtaqueTecladoDown || gamepadAtaca)
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
        Inputs.jumpDown = InputHub.SaltoDown;
        Inputs.jumpUp = InputHub.SaltoUp;

        if (Inputs.jumpDown)
        {
            EventManager.Trigger(Evento.OnPlayerPressedSpace);
        }
    }
}
