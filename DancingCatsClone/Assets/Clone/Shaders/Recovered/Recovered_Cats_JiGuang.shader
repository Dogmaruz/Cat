// Reconstructed from original GLES program sharedassets7.assets:62; original Properties and render state preserved.
Shader "DancingCatsClone/Recovered/Shader Graphs/Cats_JiGuang"
{
Properties {
		MainColor ("MainColor", Vector) = (1,1,1,1)
		[NoScaleOffset] MainTex ("MainTex", 2D) = "white" {}
		TillingOffset ("TillingOffset", Vector) = (2,1,-0.2,0)
		[NoScaleOffset] FlowTex ("FlowTex", 2D) = "white" {}
		TillingOffsetFlow ("TillingOffsetFlow", Vector) = (1,1,-0.2,0)
		[HDR] FresColor ("FresColor", Vector) = (1,1,1,1)
		Alpha ("Alpha", Range(0, 1)) = 0.5
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
Blend SrcAlpha One, One One
ZWrite Off
Cull Back
ZTest LEqual
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Varyings { float4 positionCS:SV_POSITION;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;float3 positionWS:TEXCOORD2;float3 normalWS:TEXCOORD3;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
TEXTURE2D(MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(FlowTex); SAMPLER(sampler_FlowTex);
CBUFFER_START(UnityPerMaterial)
float4 MainColor;
float4 TillingOffset;
float4 TillingOffsetFlow;
float4 FresColor;
float Alpha;
float _QueueOffset;
float _QueueControl;
CBUFFER_END
Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv0=v.uv0;o.uv1=v.uv1;o.color=v.color;return o; }
half4 Frag(Varyings i):SV_Target { float r=pow(1-saturate(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS))),2); float2 uv=i.uv0.xy*TillingOffset.xy+_TimeParameters.x*TillingOffset.zw; half4 t=SAMPLE_TEXTURE2D(MainTex,sampler_MainTex,uv); return half4(MainColor.rgb*t.rgb+r*FresColor.rgb,t.a*i.color.a*Alpha); }
ENDHLSL
}
}
}
