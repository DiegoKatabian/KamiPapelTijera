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

    public PlayerInputs Inputs;

    Player _player;

    public PlayerController(Player player)
    {
        _player = player;
    }

    public void CheckControls() //a este lo disparo en el update
    {
        Inputs = default;

        if (Input.GetButtonDown("Run"))
        {
            _player.IsSprinting = true;
        }

        if (Input.GetButtonUp("Run"))
        {
            _player.IsSprinting = false;
        }

        if (Input.GetButtonDown("Mute"))
        {
            EventManager.Trigger(Evento.OnPlayerPressedM);
        }

        if (!LevelManager.Instance.agency)
        {
            return;
        }

        if (Input.GetButtonDown("Interact"))
        {
            EventManager.Trigger(Evento.OnPlayerPressedE);
        }

        if (Input.GetButtonDown("Fire1"))
        {
            _player.OnPrimaryClick();
        }

        if (Input.GetButtonDown("Options"))
        {
            EventManager.Trigger(Evento.OnPlayerPressedEsc);
        }

        if (Input.GetButtonDown("Inventory"))
        {
            EventManager.Trigger(Evento.OnPlayerPressedI);
        }

        if (Input.GetButtonDown("Quests"))
        {
            EventManager.Trigger(Evento.OnPlayerPressedU);
        }

        if (LevelManager.Instance.inDialogue) //en dialogo no se captura movimiento ni salto
        {
            return;
        }

        Inputs.hor = Input.GetAxis("Horizontal");
        Inputs.ver = Input.GetAxis("Vertical");
        Inputs.horRaw = Input.GetAxisRaw("Horizontal");
        Inputs.verRaw = Input.GetAxisRaw("Vertical");
        Inputs.jumpDown = Input.GetButtonDown("Jump");
        Inputs.jumpUp = Input.GetButtonUp("Jump");

        if (Inputs.jumpDown)
        {
            EventManager.Trigger(Evento.OnPlayerPressedSpace);
        }
    }
}
