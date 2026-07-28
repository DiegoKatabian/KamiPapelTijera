using System;
using UnityEngine;

//como se pinta a kami cuando recibe daño:
//SpineFill: flash de color solido via shader Spine/Skeleton Fill (_FillColor/_FillPhase). los materiales del skeleton tienen que usar ese shader.
//VertexTint: multiplica el color del skeleton (skeleton.R/G/B). no necesita shader especial, pero rojo = kami rojiza/oscura, no flash pleno.
public enum HitFlashMode
{
    SpineFill,
    VertexTint
}

//config del feedback de recibir daño, para toquetear desde el inspector y probar que queda mejor
[Serializable]
public class HitFeedbackConfig
{
    [Header("Animacion")]
    [Tooltip("true: la anim de Hit reemplaza a la del cuerpo (y despues vuelve). false: Hit se superpone encima como override")]
    public bool hitReplacesBodyAnimation = false;

    [Header("Bloqueos durante el hit stun")]
    public bool blockMovement = false;
    public bool blockAttack = true;
    public bool blockJump = false;
    [Tooltip("cuanto duran los bloqueos de arriba, en segundos")]
    public float stunDuration = 0.33f; //la anim de Hit dura 0.33s

    [Header("Flash de color")]
    public HitFlashMode flashMode = HitFlashMode.SpineFill;
    public Color flashColor = Color.red;
    [Tooltip("solo para SpineFill: que tan solido es el flash (0 = nada, 1 = color pleno tapa todo el dibujo)")]
    [Range(0f, 1f)] public float fillPhase = 0.85f;
    [Tooltip("cuanto se mantiene el color pleno antes de empezar a volver")]
    public float flashHoldDuration = 0.1f;
    [Tooltip("cuanto tarda en volver del flash al color original")]
    public float flashFadeDuration = 0.2f;
}
