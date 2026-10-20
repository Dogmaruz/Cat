// Reconstructed from original GLES program sharedassets0.assets:868; original Properties and render state preserved.
Shader "DancingCatsClone/Recovered/Shader Graphs/Cats_Role_Tex"
{
Properties {
		_LightDir ("LightDir", Vector) = (0,1,0.77,0)
		Color_aaeea0d74e8d4ccd8a9ca042a3c40b21 ("Color", Vector) = (0,0,0,0)
		[ToggleUI] _SpeclurLight ("SpeclurLight", Float) = 1
		_SpeclurPower ("SpeclurPower", Range(0.01, 10)) = 2
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
		_TillingOffset ("TillingOffset", Vector) = (1,1,0,0)
		[ToggleUI] Fresnel ("Fresnel", Float) = 0
		[HDR] FreColor ("FreColor", Vector) = (0.0235849,0.4325759,1,0)
		FrePower ("FrePower", Range(1, 5)) = 1
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
float4 _LightDir;
float4 Color_aaeea0d74e8d4ccd8a9ca042a3c40b21;
float _SpeclurLight;
float _SpeclurPower;
float4 _TillingOffset;
float Fresnel;
float4 FreColor;
float FrePower;
float _QueueOffset;
float _QueueControl;
CBUFFER_END
Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv0=v.uv0;o.uv1=v.uv1;o.color=v.color;return o; }
half4 Frag(Varyings i):SV_Target { float3 n=normalize(i.normalWS), v=GetWorldSpaceNormalizeViewDir(i.positionWS), l=normalize(_LightDir.xyz); float d=.5+.5*max(dot(n,l),0);float3 rim=Fresnel!=0 ? pow(1-saturate(dot(n,v)),FrePower)*FreColor.rgb : 0;float spec=_SpeclurLight!=0?pow(max(dot(reflect(-l,n),v),0),_SpeclurPower*10):0; float2 uv=i.uv0.xy*_TillingOffset.xy+_TimeParameters.x*_TillingOffset.zw; return half4(SAMPLE_TEXTURE2D(_MainTex,sampler__MainTex,uv).rgb*Color_aaeea0d74e8d4ccd8a9ca042a3c40b21.rgb*(d+spec)+rim,1); }
ENDHLSL
}
}
}
