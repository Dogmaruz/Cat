// Reconstructed from original GLES program sharedassets0.assets:872; original Properties and render state preserved.
Shader "DancingCatsClone/Recovered/Shader Graphs/Cats_TrailShowAnim"
{
Properties {
		[HDR] Color_43d50ce605284bdfb64dcbea2b745524 ("Color", Vector) = (0.1084906,1,0.3724781,0)
		[NoScaleOffset] TexAdd ("TexAdd", 2D) = "white" {}
		_TillingOffset ("TillingOffset", Vector) = (1,1,0,0)
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
}
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
Pass {
Tags { "LightMode"="SRPDefaultUnlit" }
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZWrite Off
Cull Off
ZTest LEqual
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Varyings { float4 positionCS:SV_POSITION;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;float3 positionWS:TEXCOORD2;float3 normalWS:TEXCOORD3;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
TEXTURE2D(TexAdd); SAMPLER(sampler_TexAdd);
CBUFFER_START(UnityPerMaterial)
float4 Color_43d50ce605284bdfb64dcbea2b745524;
float4 _TillingOffset;
float _QueueOffset;
float _QueueControl;
CBUFFER_END
Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv0=v.uv0;o.uv1=v.uv1;o.color=v.color;return o; }
half4 Frag(Varyings i):SV_Target { float2 uv=i.uv0.xy*_TillingOffset.xy+_TimeParameters.x*_TillingOffset.zw; half4 t=SAMPLE_TEXTURE2D(TexAdd,sampler_TexAdd,uv)*Color_43d50ce605284bdfb64dcbea2b745524; t.a*=i.color.a; return t; }
ENDHLSL
}
}
}
