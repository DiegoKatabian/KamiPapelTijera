// The flat page pieces (roads, river, decals) captured from above and drawn on the turning paper sheet (spec 025 D32).
// Unlit on purpose: the capture already holds the scene's lighting, lighting it again would darken it.
// Plain HLSL without a LightMode tag, so URP draws it in its SRPDefaultUnlit pass.
Shader "Kami/Paper Overlay"
{
    Properties
    {
        _MainTex ("Capture", 2D) = "black" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        // the overlay sits a hair above the sheet; this keeps it in front of the face it is drawn on
        Offset -1, -1

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv) * _Color;
                // the capture is cleared to transparent black: undo the darkening of soft edges
                c.rgb = c.a > 0.001 ? c.rgb / c.a : c.rgb;
                return c;
            }
            ENDHLSL
        }
    }
}
