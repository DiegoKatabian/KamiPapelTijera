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

    //movement a cutscene drives Kami with (Player.SetCutsceneWalk), in the same units as the stick
    Vector2 _cutsceneMove;

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

        //Issue #41.3: con el Flap abierto (pausa), NINGUN input de gameplay se procesa aca -
        //solo queda viva la navegacion de UI (que lee los mismos ejes por su cuenta via
        //EventSystem/UISelector, sin pasar por PlayerController) y R1/L1/B, que FlapManager
        //escucha directo de InputHub en su propio Update. Esc/I/U siguen funcionando (abren o
        //cierran el Flap) porque no pasan por este gate.
        //Spec 011 Q14: IsMenuOpen also covers the opening slide (Kami freezes as soon as the Flap
        //starts opening) and stops covering the closing one (she's free as soon as it starts closing).
        bool menuAbierto = FlapManager.Instance != null && FlapManager.Instance.IsMenuOpen;

        //A cutscene owns Kami: no movement, jump or attack. Interact only goes through while a
        //dialogue is on screen, so the player can advance the cutscene's lines but can't open a
        //trash can or a page sphere Kami walks past during a scripted walk.
        bool enCutscene = LevelManager.Instance.inCutscene;
        bool dialogoEnPantalla = DialogueManager.Instance != null && DialogueManager.Instance.isShowing;

        //El boton A del joystick es CONTEXTUAL: si hay algo con que interactuar interactua, y si
        //no, salta. Asi un chico usa un solo boton para "hacer" y no tiene que aprender cual es
        //cual. El teclado NO cambia: E siempre interactua y el espacio siempre salta.
        //Time.timeScale > 0 descarta el menu abierto (pausa el juego): ahi A es el Submit de la UI.
        _gamepadInteractua = InputHub.AccionGamepadDown
                             && InteractionContext.HayInteraccionDisponible
                             && Time.timeScale > 0f;

        //Si este mismo frame un overlay (victory/defeat/mainquest) se acaba de desbloquear por su
        //propio boton (Submit del EventSystem, mismo eje que Interact), el E de mundo queda vetado:
        //sin esto, el mismo apreton de A que cierra el overlay podia ADEMAS abrir el dialogo del NPC
        //en cuyo trigger quedo parado el player (ej. la abuela justo despues del victory del boss
        //fight). Ver OverlayManager.SeDesbloqueoEsteFrame.
        bool overlayRecienCerrado = OverlayManager.Instance != null && OverlayManager.Instance.SeDesbloqueoEsteFrame;

        //InteractDown ya incluye al boton A (comparten el eje "Interact", que ademas es el Submit
        //del EventSystem), asi que con esto solo alcanza para los dos devices.
        if (!menuAbierto && !overlayRecienCerrado && InputHub.InteractDown && (!enCutscene || dialogoEnPantalla))
        {
            EventManager.Trigger(Evento.OnPlayerPressedE);
        }

        //B ataca siempre, sin contexto. El gate de timeScale evita el tijeretazo por atras con el
        //menu abierto (donde igual OnPrimaryClick no llegaria a nada util); menuAbierto cubre
        //ademas el teclado, que antes no pasaba por ningun gate de pausa.
        if (!menuAbierto && !enCutscene && (InputHub.AtaqueTecladoDown || (InputHub.AtaqueGamepadDown && Time.timeScale > 0f)))
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

        if (enCutscene)
        {
            ApplyCutsceneMove();
            return;
        }

        //a cutscene that ended while Kami was mid-walk must not leave her walking on her own later
        _cutsceneMove = Vector2.zero;

        if (LevelManager.Instance.inDialogue || menuAbierto) //en dialogo o con el Flap abierto no se captura movimiento ni salto
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

    public void SetCutsceneMove(Vector2 move)
    {
        _cutsceneMove = move;
    }

    //the stick is ignored entirely: the cutscene's walk is the only movement, and never a jump
    void ApplyCutsceneMove()
    {
        _player.IsSprinting = false;
        Inputs.hor = _cutsceneMove.x;
        Inputs.ver = _cutsceneMove.y;
        Inputs.horRaw = Mathf.Abs(_cutsceneMove.x) > 0.01f ? Mathf.Sign(_cutsceneMove.x) : 0f;
        Inputs.verRaw = Mathf.Abs(_cutsceneMove.y) > 0.01f ? Mathf.Sign(_cutsceneMove.y) : 0f;
    }
}
