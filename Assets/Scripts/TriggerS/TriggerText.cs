using UnityEngine;

public class TriggerText : TriggerScript
{
    //este es un tipo de triggerscript que solo muestra texto.

    [SerializeField, Tooltip("Legacy: mostrar una unica vez. Equivale a poner Max Tooltip Shows en 1 (se queda por compatibilidad con las escenas ya configuradas)")]
    bool isOneTimeOnly;

    public override void OnEnterBehaviour(Collider other)
    {
        triggerBool = true;

        //forceShow: un TriggerText existe justamente para mostrar texto, siempre ignoro
        //el flag showTooltip (comportamiento historico). El limite de muestras SI se respeta
        TryShowTooltip(true);
    }

    protected override int GetMaxTooltipShows()
    {
        int baseMax = base.GetMaxTooltipShows();

        if (!isOneTimeOnly)
        {
            return baseMax;
        }

        //isOneTimeOnly equivale a un maximo de 1: si ademas configuraron _maxTooltipShows,
        //gana el mas restrictivo (1, porque no hay maximo valido menor)
        return 1;
    }
}
