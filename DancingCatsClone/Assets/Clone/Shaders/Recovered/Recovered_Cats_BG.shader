// Reconstructed from original GLES program sharedassets1.assets:97; original Properties and render state preserved.
Shader "DancingCatsClone/Recovered/Shader Graphs/Cats_BG"
{
Properties {
		[HDR] Color1 ("Color1", Vector) = (0.1084906,1,0.3724781,0)
		[HDR] Color2 ("Color2", Vector) = (0.1084906,1,0.3724781,0)
		[NoScaleOffset] TexAdd ("TexAdd", 2D) = "white" {}
		[NoScaleOffset] Mask ("Mask", 2D) = "white" {}
		_TillingOffset ("TillingOffset", Vector) = (1,1,0,0)
		Alpha ("Alpha", Range(0, 1)) = 0.1
		[ToggleUI] Color_Uv1 ("Color(Uv1)", Float) = 0
		[ToggleUI] Tex_Uv1 ("Tex(Uv1)", Float) = 1
		[ToggleUI] Mask_Uv1 ("Mask(Uv1)", Float) = 0
		[ToggleUI] Boolean_f327145916584868b02f26db7c793ac8 ("isColor", Float) = 0
		[HDR] Color_abf7fe455142484c99b8c1a689d6e5b8 ("Color", Vector) = (0,0,0,0)
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
}
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
Pass {
Tags { "LightMode"="SRPDefaultUnlit" }
Blend One Zero, One Zero
ZWrite On
Cull Back
ZTest LEqual
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Varyings { float4 positionCS:SV_POSITION;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;float3 positionWS:TEXCOORD2;float3 normalWS:TEXCOORD3;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
TEXTURE2D(TexAdd); SAMPLER(sampler_TexAdd);
TEXTURE2D(Mask); SAMPLER(sampler_Mask);
CBUFFER_START(UnityPerMaterial)
float4 Color1;
float4 Color2;
float4 _TillingOffset;
float Alpha;
float Color_Uv1;
float Tex_Uv1;
float Mask_Uv1;
float Boolean_f327145916584868b02f26db7c793ac8;
float4 Color_abf7fe455142484c99b8c1a689d6e5b8;
float _QueueOffset;
float _QueueControl;
CBUFFER_END
Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv0=v.uv0;o.uv1=v.uv1;o.color=v.color;return o; }
half4 Frag(Varyings i):SV_Target { float y=Color_Uv1!=0?i.uv0.y:i.uv1.y; float2 uv=Tex_Uv1!=0?i.uv0.xy:i.uv1.xy; float2 maskuv=Mask_Uv1!=0?i.uv0.xy:i.uv1.xy; uv=uv*_TillingOffset.xy+_TimeParameters.x*_TillingOffset.zw; half3 add=SAMPLE_TEXTURE2D(TexAdd,sampler_TexAdd,uv).rgb*Alpha*SAMPLE_TEXTURE2D(Mask,sampler_Mask,maskuv).rgb; if(Boolean_f327145916584868b02f26db7c793ac8!=0) add*=Color_abf7fe455142484c99b8c1a689d6e5b8.rgb; return half4(lerp(Color1.rgb,Color2.rgb,y)+add,1); }
ENDHLSL
}
}
}
