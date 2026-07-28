using System;
using UnityEngine;

//tiempos de mezcla de los tracks superpuestos de spine, para tweakear desde el inspector.
//mixIn = cuanto tarda la anim en aparecer sobre lo que habia; mixOut = cuanto tarda en desvanecerse al terminar.
//OJO: el fade-out arranca DESPUES de que la anim termino (delay explicito), asi que la anim siempre se ve completa.
//las transiciones del cuerpo (walk<->idle etc) no salen de aca: usan el defaultMix del SkeletonDataAsset.
[Serializable]
public class AnimationMixConfig
{
    [Header("Ataque")]
    public float attackMixIn = 0.05f;
    public float attackMixOut = 0.05f;

    [Header("Hit")]
    public float hitMixIn = 0.05f;
    public float hitMixOut = 0.05f;

    [Header("Overrides (noscissors / paperplane)")]
    public float overrideMixIn = 0.2f;
    public float overrideMixOut = 0.2f;
}
