// Reconstructed from original GLES program sharedassets6.assets:80; original Properties and render state preserved.
Shader "DancingCatsClone/Recovered/Shader Graphs/Cats_BG2"
{
Properties {
		_Color01 ("Color01", Vector) = (0,0,0,0)
		_Color02 ("Color02", Vector) = (0,0,0,0)
		[HDR] _MainColor ("MainColor", Vector) = (0.1084906,1,0.3724781,0)
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
		_MainTillingOffset ("MainTillingOffset", Vector) = (48,24,0,-1.6)
		[ToggleUI] Middle ("Middle", Float) = 0
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
TEXTURE2D(_MainTex); SAMPLER(sampler__MainTex);
CBUFFER_START(UnityPerMaterial)
float4 _Color01;
float4 _Color02;
float4 _MainColor;
float4 _MainTillingOffset;
float Middle;
float _QueueOffset;
float _QueueControl;
CBUFFER_END
Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv0=v.uv0;o.uv1=v.uv1;o.color=v.color;return o; }
half4 Frag(Varyings i):SV_Target { float2 uv=i.uv0.xy*_MainTillingOffset.xy+_TimeParameters.x*_MainTillingOffset.zw; float factor=Middle!=0?1-i.uv0.y:i.uv0.y; return half4(lerp(_Color02.rgb,_Color01.rgb,i.uv0.y)+_MainColor.rgb*SAMPLE_TEXTURE2D(_MainTex,sampler__MainTex,uv).b*factor,1); }
ENDHLSL
}
}
}
