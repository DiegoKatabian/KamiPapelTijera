/// <summary>
/// Player-facing actions, shared by the prompt system (which button icon a text shows) and the
/// controls diagram (which key to highlight). Promoted out of InputPromptSystem so more than one
/// system can name an action; the members and their meaning are unchanged.
/// </summary>
public enum Accion
{
    Saltar,
    Interactuar, //hablar, agarrar, pasar de pagina, avanzar el dialogo
    Atacar,      //cortar con la tijera
    Correr,
    Camara,
    Menu,
    Mover,
    CambiarTab   //L1/R1: ciclar secciones del Flap (issue #41 ronda 2, punto 3). SOLO joystick: estos ejes no tienen binding de teclado (ver InputHub.TabSiguienteDown/TabAnteriorDown), asi que el cartelito que usa este placeholder tiene que estar oculto con teclado (ver SoloConJoystick)
}
