#ifndef KAMI_URP_INPUT_SPRITE_INCLUDED
#define KAMI_URP_INPUT_SPRITE_INCLUDED

// Copy of the Spine URP package's Spine-Input-Sprite-URP.hlsl plus _FillColor/_FillPhase.
// The fill values have to live inside UnityPerMaterial, next to the rest, so the layout is the
// same in every pass and the property block Player.SetFlash writes lands in the right place.
// If the Spine URP package is updated, diff this against its Spine-Input-Sprite-URP.hlsl.

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

CBUFFER_START(UnityPerMaterial)

float4 _MainTex_ST;
half4 _Color;
half4 _Black;
half _Cutoff;
half _ShadowAlphaCutoff;

#ifndef SPRITE_SHADER_2D
half _Metallic;
half _Glossiness;
half _GlossMapScale;
#endif

half _BumpScale;

float _BlendAmount;

float _Hue;
float _Saturation;
float _Brightness;
half4 _OverlayColor;

half4 _EmissionColor;
float _EmissionPower;

float4 _FixedNormal;

float _RimPower;
half4 _RimColor;

// Kami: solid hit flash, same contract as Spine/Skeleton Fill.
half4 _FillColor;
half _FillPhase;

CBUFFER_END

#endif // KAMI_URP_INPUT_SPRITE_INCLUDED
