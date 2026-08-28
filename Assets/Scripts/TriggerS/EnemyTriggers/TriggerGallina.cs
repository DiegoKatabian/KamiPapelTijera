/// <summary>
/// Trigger de proximidad de la gallina: solo maneja el tooltip ("La gallina intenta esquivarte!").
/// GallinaAgent calcula su propia distancia al player para evadir, no necesita que este
/// trigger le pase estado (a diferencia del viejo GallinaAI que dependia de playerIsInRange).
/// </summary>
public class TriggerGallina : TriggerScript
{
    // Sin overrides: OnEnterBehaviour/OnExitBehaviour de TriggerScript ya manejan el tooltip.
}
