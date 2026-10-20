// Reconstructed from original GLES program resources.assets:52; original Properties and render state preserved.
Shader "DancingCatsClone/Recovered/Shader Graphs/Twins_Common_Tex"
{
Properties {
		[HDR] BaseColor ("BaseColor", Vector) = (0,0,0,0)
		[ToggleUI] _Light ("Light", Float) = 0
		_LightDir ("LightDir", Vector) = (0,1,0.77,0)
		[ToggleUI] Boolean_d66bb56d4b4f45d18fbf3478e3f2ca92 ("SpeclurLight", Float) = 0
		_SpeclurPower ("SpeclurPower", Range(1, 10)) = 2
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
		_TillingOffset ("TillingOffset", Vector) = (1,1,0,0)
		[ToggleUI] Fresnel ("Fresnel", Float) = 0
		[HDR] FreColor ("FreColor", Vector) = (0.0235849,0.4325759,1,0)
		FrePower ("FrePower", Range(1, 5)) = 1
		[ToggleUI] VertexOffset_1 ("VertexOffset", Float) = 1
		_Distance ("Distance", Float) = 120
		_StartPos ("StartPos", Vector) = (0,0,5,0)
		_OffsetValue ("OffsetValue", Vector) = (0,-120,0,0)
		_Lerp ("Lerp", Range(0, 1)) = 1
		_Position ("Position", Vector) = (0.5,0.8,0,0)
		[ToggleUI] _HaHa ("HaHa", Float) = 0
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
float4 BaseColor;
float _Light;
float4 _LightDir;
float Boolean_d66bb56d4b4f45d18fbf3478e3f2ca92;
float _SpeclurPower;
float4 _TillingOffset;
float Fresnel;
float4 FreColor;
float FrePower;
float VertexOffset_1;
float _Distance;
float4 _StartPos;
float4 _OffsetValue;
float _Lerp;
float4 _Position;
float _HaHa;
float _QueueOffset;
float _QueueControl;
CBUFFER_END
Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv0=v.uv0;o.uv1=v.uv1;o.color=v.color;return o; }
half4 Frag(Varyings i):SV_Target { float3 n=normalize(i.normalWS), v=GetWorldSpaceNormalizeViewDir(i.positionWS), l=normalize(_LightDir.xyz); float d=.5+.5*max(dot(n,l),0);float3 rim=Fresnel!=0 ? pow(1-saturate(dot(n,v)),FrePower)*FreColor.rgb : 0;float spec=Boolean_d66bb56d4b4f45d18fbf3478e3f2ca92!=0?pow(max(dot(reflect(-l,n),v),0),_SpeclurPower*10):0; half3 tint=_Light!=0?BaseColor.rgb*(d+spec):BaseColor.rgb; float2 uv=i.uv0.xy*_TillingOffset.xy+_TimeParameters.x*_TillingOffset.zw; half3 c=SAMPLE_TEXTURE2D(_MainTex,sampler__MainTex,uv).rgb*tint+rim; if(_HaHa!=0) { float y=i.positionCS.y/_ScaledScreenParams.y; if(_ProjectionParams.x>0)y=1-y; y=1-y; float mask=step(.5,(y-_Position.x)/(_Position.y-_Position.x)-_Lerp+1); c*=1-mask; } return half4(c,1); }
ENDHLSL
}
}
}
