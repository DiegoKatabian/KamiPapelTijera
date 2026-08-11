//el estado del cuerpo de kami. una sola fuente de verdad sobre que esta haciendo.
//OJO: atacar y recibir daño NO son estados: son animaciones que se superponen
//en los tracks 1 y 2 de spine, asi que conviven con cualquiera de estos.
public enum PlayerState
{
    Idle,
    Walking,         //WASD suave (joystick apenas inclinado) -> anim walk
    Skipping,        //WASD a fondo -> anim Skip
    Running,         //moviendose con SHIFT (y botas) -> anim Run
    Jumping,
    Falling,
    Landing,         //ventanita al tocar el suelo, bloquea inputs
    Casting,         //origami: no puede hacer nada mas
    ReceivingReward,
    Dead,
    RidingPage       //enganchada al borde de la hoja mientras gira: bloquea input y fisica, la mueve Player.LateUpdate
}
