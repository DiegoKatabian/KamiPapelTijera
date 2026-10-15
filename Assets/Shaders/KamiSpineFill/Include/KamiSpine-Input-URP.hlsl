#ifndef KAMI_URP_LIT_INPUT_INCLUDED
#define KAMI_URP_LIT_INPUT_INCLUDED

// Copy of the Spine URP package's Spine-Input-URP.hlsl (the "Unlit" pass) plus _FillColor/_FillPhase.
// Must declare the same UnityPerMaterial members as KamiSpine-Input-Sprite-URP.hlsl for the ones it
// shares; the extra members of the other header are not needed by this pass.

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#undef LIGHTMAP_ON

CBUFFER_START(UnityPerMaterial)

float4 _MainTex_ST;
half4 _Color;
half4 _Black;
half _Cutoff;
half _ShadowAlphaCutoff;

half _Metallic;
half _Glossiness;
half _GlossMapScale;

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

half4 _FillColor;
half _FillPhase;

CBUFFER_END

sampler2D _MainTex;

#endif // KAMI_URP_LIT_INPUT_INCLUDED
