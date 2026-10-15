// Kami's skeleton shader: "Universal Render Pipeline/Spine/Sprite" (lit, receives and casts shadows)
// + the solid flash of "Spine/Skeleton Fill" (_FillColor / _FillPhase), which Player.SetFlash drives
// through a MaterialPropertyBlock when Kami gets hit.
//
// How it is built: a fork of the Spine URP package's Spine-Sprite-URP.shader. Only two things differ:
//   1. The input headers are our copies, with _FillColor/_FillPhase inside UnityPerMaterial.
//   2. The two color passes (ForwardLit, Unlit) call the package's own fragment function and then
//      mix the result toward the fill color. Doing it LAST means the flash is a flat color that
//      lighting and shadows can't darken, exactly like the old Fill shader.
// The shadow / depth passes are untouched. Same properties as the package shader, so the existing
// materials keep every value when their shader is swapped.
//
// If the Spine URP package is updated, diff this file against its Spine-Sprite-URP.shader.
Shader "Kami/Spine/Sprite Fill URP"
{
	Properties
	{
		_MainTex("Main Texture", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)

		_BumpScale("Scale", Float) = 1.0
		_BumpMap("Normal Map", 2D) = "bump" {}

		[MaterialToggle] PixelSnap("Pixel snap", Float) = 0

		_EmissionColor("Color", Color) = (0,0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}
		_EmissionPower("Emission Power", Float) = 2.0

		_Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
		_GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
		[Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
		_MetallicGlossMap("Metallic", 2D) = "white" {}

		_DiffuseRamp("Diffuse Ramp Texture", 2D) = "gray" {}

		_FixedNormal("Fixed Normal", Vector) = (0,0,1,1)
		_ZWrite("Depth Write", Float) = 0.0
		_Cutoff("Depth alpha cutoff", Range(0,1)) = 0.0
		_ShadowAlphaCutoff("Shadow alpha cutoff", Range(0,1)) = 0.1
		_CustomRenderQueue("Custom Render Queue", Float) = 0.0

		_OverlayColor("Overlay Color", Color) = (0,0,0,0)
		_Hue("Hue", Range(-0.5,0.5)) = 0.0
		_Saturation("Saturation", Range(0,2)) = 1.0
		_Brightness("Brightness", Range(0,2)) = 1.0

		_RimPower("Rim Power", Float) = 2.0
		_RimColor("Rim Color", Color) = (1,1,1,1)

		_BlendTex("Blend Texture", 2D) = "white" {}
		_BlendAmount("Blend", Range(0,1)) = 0.0

		[MaterialToggle(_LIGHT_AFFECTS_ADDITIVE)] _LightAffectsAdditive("Light Affects Additive", Float) = 0
		[MaterialToggle(_TINT_BLACK_ON)]  _TintBlack("Tint Black", Float) = 0
		_Black("Dark Color", Color) = (0,0,0,0)

		// Hit flash. Normally 0 and written per renderer by Player.SetFlash, so these are not in the inspector.
		[HideInInspector] _FillColor("Fill Color", Color) = (1,0,0,1)
		[HideInInspector] _FillPhase("Fill Phase", Range(0, 1)) = 0

		[HideInInspector] _SrcBlend("__src", Float) = 1.0
		[HideInInspector] _DstBlend("__dst", Float) = 0.0
		[HideInInspector] _RenderQueue("__queue", Float) = 0.0
		[HideInInspector] _Cull("__cull", Float) = 0.0
		[HideInInspector] _StencilRef("Stencil Reference", Float) = 1.0
		[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Compare", Float) = 8 // Set to Always as default
	}

	SubShader
	{
		// Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
		// this Subshader will fail.
		Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Sprite" "AlphaDepth"="False" "CanUseSpriteAtlas"="True" "IgnoreProjector"="True" }
		LOD 150

		Stencil {
			Ref[_StencilRef]
			Comp[_StencilComp]
			Pass Keep
		}

		// ------------------------------------------------------------------
		//  Forward pass.
		Pass
		{
			// Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
			// no LightMode tag are also rendered by Universal Render Pipeline
			Name "ForwardLit"
			Tags{"LightMode" = "UniversalForward"}
			Blend[_SrcBlend][_DstBlend]
			ZWrite[_ZWrite]
			Cull[_Cull]

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard SRP library
			// All shaders must be compiled with HLSLcc and currently only gles is not using HLSLcc by default
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature _ _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON _ALPHAPREMULTIPLY_VERTEX_ONLY _ADDITIVEBLEND _ADDITIVEBLEND_SOFT _MULTIPLYBLEND _MULTIPLYBLEND_X2
			#pragma shader_feature _ _FIXED_NORMALS_VIEWSPACE _FIXED_NORMALS_VIEWSPACE_BACKFACE _FIXED_NORMALS_MODELSPACE _FIXED_NORMALS_MODELSPACE_BACKFACE _FIXED_NORMALS_WORLDSPACE
			#pragma shader_feature _ _SPECULAR _SPECULAR_GLOSSMAP
			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ALPHA_CLIP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _DIFFUSE_RAMP
			#pragma shader_feature _ _FULLRANGE_HARD_RAMP _FULLRANGE_SOFT_RAMP _OLD_HARD_RAMP _OLD_SOFT_RAMP
			#pragma shader_feature _COLOR_ADJUST
			#pragma shader_feature _RIM_LIGHTING
			#pragma shader_feature _TEXTURE_BLEND
			#pragma shader_feature _FOG
			#pragma shader_feature _RECEIVE_SHADOWS_OFF
			#pragma shader_feature _LIGHT_AFFECTS_ADDITIVE
			#pragma shader_feature _TINT_BLACK_ON

			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fog
			#pragma multi_compile _ PIXELSNAP_ON

			// -------------------------------------
			// Universal Pipeline keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile _ _SHADOWS_SOFT
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile_fragment _ _LIGHT_COOKIES

			// Farward+ renderer keywords
			#pragma multi_compile_fragment _ _LIGHT_LAYERS
			#pragma multi_compile _ _FORWARD_PLUS
			#pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS

			// -------------------------------------
			// Unity defined keywords
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer

			//--------------------------------------
			// Spine related keywords
			#pragma vertex ForwardPassVertexSprite
			#pragma fragment KamiForwardPassFragmentSprite

			#define USE_URP
			#define fixed4 half4
			#define fixed3 half3
			#define fixed half
			#include "Include/KamiSpine-Input-Sprite-URP.hlsl"
			#include "Packages/com.esotericsoftware.spine.urp-shaders/Shaders/Include/Spine-Sprite-ForwardPass-URP.hlsl"

			// The package's fragment, then the flash on top of its result. Its output is already in the
			// blend mode's space: premultiplied for every mode that blends with One/OneMinusSrcAlpha
			// (all of them unless Tint Black is on, where rgb stays straight), so the fill color is
			// premultiplied the same way, like Spine/Skeleton Fill does.
			half4 KamiForwardPassFragmentSprite(VertexOutputLWRP input
			#ifdef USE_WRITE_RENDERING_LAYERS
				, out float4 outRenderingLayers : SV_Target1
			#endif
			) : SV_Target0
			{
			#ifdef USE_WRITE_RENDERING_LAYERS
				half4 pixel = ForwardPassFragmentSprite(input, outRenderingLayers);
			#else
				half4 pixel = ForwardPassFragmentSprite(input);
			#endif

			#if defined(_TINT_BLACK_ON)
				pixel.rgb = lerp(pixel.rgb, _FillColor.rgb, _FillPhase);
			#else
				pixel.rgb = lerp(pixel.rgb, _FillColor.rgb * pixel.a, _FillPhase);
			#endif
				return pixel;
			}
			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"
			Tags{"LightMode" = "ShadowCaster"}

			ZWrite On
			ColorMask 0
			ZTest LEqual
			Cull Off

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard srp library
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 2.0

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature _ALPHATEST_ON

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			#pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			#pragma vertex ShadowPassVertexSprite
			#pragma fragment ShadowPassFragmentSprite

			#define USE_URP
			#define fixed4 half4
			#define fixed3 half3
			#define fixed half
			#include "Include/KamiSpine-Input-Sprite-URP.hlsl"
			#include "Packages/com.esotericsoftware.spine.urp-shaders/Shaders/Include/Spine-Sprite-ShadowCasterPass-URP.hlsl"
			ENDHLSL
		}

		Pass
		{
			Name "DepthOnly"
			Tags{"LightMode" = "DepthOnly"}

			ZWrite On
			ColorMask R
			Cull Off

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard srp library
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex DepthOnlyVertexSprite
			#pragma fragment DepthOnlyFragmentSprite

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature _ALPHATEST_ON
			#pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing

			#define USE_URP
			#define fixed4 half4
			#define fixed3 half3
			#define fixed half
			#include "Include/KamiSpine-Input-Sprite-URP.hlsl"
			#include "Packages/com.esotericsoftware.spine.urp-shaders/Shaders/Include/Spine-Sprite-DepthOnlyPass-URP.hlsl"
			ENDHLSL
		}

		// This pass is used when drawing to a _CameraNormalsTexture texture
		Pass
		{
			Name "DepthNormals"
			Tags{"LightMode" = "DepthNormals"}

			ZWrite On
			Cull[_Cull]

			HLSLPROGRAM
			#pragma vertex DepthNormalsVertexSprite
			#pragma fragment DepthNormalsFragmentSprite

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature _ _FIXED_NORMALS_VIEWSPACE _FIXED_NORMALS_VIEWSPACE_BACKFACE _FIXED_NORMALS_MODELSPACE _FIXED_NORMALS_MODELSPACE_BACKFACE _FIXED_NORMALS_WORLDSPACE
			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ALPHA_CLIP

			// -------------------------------------
			// Universal Pipeline keywords
			#pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing

			#define USE_URP
			#define fixed4 half4
			#define fixed3 half3
			#define fixed half
			#include "Include/KamiSpine-Input-Sprite-URP.hlsl"
			#include "Packages/com.esotericsoftware.spine.urp-shaders/Shaders/Include/Spine-Sprite-DepthNormalsPass-URP.hlsl"
			ENDHLSL
		}

		Pass
		{
			Name "Unlit"
			Tags { "LightMode" = "UniversalForward" "Queue" = "Transparent" "RenderType" = "Transparent"}

			ZWrite Off
			Cull Off
			Blend One OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma shader_feature _ _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON _ALPHAPREMULTIPLY_VERTEX_ONLY _ADDITIVEBLEND _ADDITIVEBLEND_SOFT _MULTIPLYBLEND _MULTIPLYBLEND_X2
			#pragma prefer_hlslcc gles
			#pragma vertex vert
			#pragma fragment KamiUnlitFragment

			#undef LIGHTMAP_ON

			#define USE_URP
			#define fixed4 half4
			#define fixed3 half3
			#define fixed half
			#include "Include/KamiSpine-Input-URP.hlsl"
			#include "Packages/com.esotericsoftware.spine.urp-shaders/Shaders/Include/Spine-Skeleton-ForwardPass-URP.hlsl"

			// This pass blends premultiplied (One OneMinusSrcAlpha), same fill rule as above.
			half4 KamiUnlitFragment(VertexOutput i) : SV_Target
			{
				half4 pixel = frag(i);
				pixel.rgb = lerp(pixel.rgb, _FillColor.rgb * pixel.a, _FillPhase);
				return pixel;
			}
			ENDHLSL
		}
	}

	FallBack "Universal Render Pipeline/Spine/Skeleton"
	CustomEditor "SpineSpriteShaderGUI"
}
