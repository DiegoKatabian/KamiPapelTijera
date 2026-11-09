// The inside of a reflecting pool (spec 024 3.F): the deep water colour, and stencil 1 wherever the pool is visible. The
// reflection (Kami's mirror materials, stencil Equal 1) is drawn only there: the pool is its mask (Diego, 2026-11-09).
Shader "Kami/Pool Hole"
{
    Properties
    {
        _DeepColor ("Deep water", Color) = (0.08, 0.16, 0.24, 1)
        _EdgeColor ("Near the rim", Color) = (0.16, 0.30, 0.40, 1)
    }
    SubShader
    {
        // the last opaque queue (2500): after every opaque and cut-out prop, so whatever stands in front of the pool has written its depth
        // and the mask is only set where the pool is really visible
        Tags { "Queue" = "AlphaTest+50" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        ZWrite On
        ZTest LEqual
        Cull Back
        // only where the pool is actually seen: something standing in front of it fails the depth test and keeps it out
        Stencil
        {
            Ref 1
            Comp Always
            Pass Replace
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _DeepColor;
            float4 _EdgeColor;

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
                float r = saturate(length(i.uv - 0.5) * 2);
                return lerp(_DeepColor, _EdgeColor, r * r);
            }
            ENDHLSL
        }
    }
}
