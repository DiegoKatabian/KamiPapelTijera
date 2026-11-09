// The surface of a reflecting pool (spec 024 3.F, 2026-11-09): a thin tint over the reflection, slow rings moving out from
// the middle and a bright rim. Drawn after the mirror Kami (queue 3100). Unlit: it is a highlight layer, not a lit object.
Shader "Kami/Pool Surface"
{
    Properties
    {
        _Tint ("Water tint", Color) = (0.55, 0.75, 0.95, 0.18)
        _RingColor ("Ripple rings", Color) = (1, 1, 1, 0.12)
        _RimColor ("Rim", Color) = (0.9, 0.95, 1, 0.55)
        _RingCount ("Rings across the pool", Float) = 4
        _RingSpeed ("Ring speed", Float) = 0.15
        _RimWidth ("Rim width (0-1 of the radius)", Range(0, 0.3)) = 0.06
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Offset -1, -1

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Tint;
            float4 _RingColor;
            float4 _RimColor;
            float _RingCount;
            float _RingSpeed;
            float _RimWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2;
                clip(1 - r);
                // thin rings travelling outwards, fading towards the rim
                float phase = frac(r * _RingCount - _Time.y * _RingSpeed);
                float ring = smoothstep(0.0, 0.08, phase) * (1 - smoothstep(0.08, 0.2, phase)) * (1 - r);
                float rim = smoothstep(1 - _RimWidth, 1, r);
                float4 c = _Tint;
                c.rgb = lerp(c.rgb, _RingColor.rgb, ring * _RingColor.a);
                c.a = saturate(c.a + ring * _RingColor.a);
                c = lerp(c, _RimColor, rim * _RimColor.a);
                return c;
            }
            ENDHLSL
        }
    }
}
