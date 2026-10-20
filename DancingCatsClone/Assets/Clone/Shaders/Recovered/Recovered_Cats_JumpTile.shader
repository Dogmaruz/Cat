// Reconstructed from original GLES program sharedassets1.assets:94; original Properties and render state preserved.
Shader "DancingCatsClone/Recovered/Shader Graphs/Cats_JumpTile"
{
Properties {
		_LightDir ("LightDir", Vector) = (0,1,0.77,0)
		MainColor ("MainColor", Vector) = (0,0,0,1)
		[NoScaleOffset] MainTex ("MainTex", 2D) = "white" {}
		[HDR] EmissionColor ("EmissionColor", Vector) = (1,1,1,1)
		[NoScaleOffset] EmissionTex ("EmissionTex", 2D) = "black" {}
		E ("E", Range(1, 5)) = 1
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
TEXTURE2D(MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(EmissionTex); SAMPLER(sampler_EmissionTex);
CBUFFER_START(UnityPerMaterial)
float4 _LightDir;
float4 MainColor;
float4 EmissionColor;
float E;
float Fresnel;
float4 FreColor;
float FrePower;
float _QueueOffset;
float _QueueControl;
CBUFFER_END
Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv0=v.uv0;o.uv1=v.uv1;o.color=v.color;return o; }
half4 Frag(Varyings i):SV_Target { float3 n=normalize(i.normalWS), v=GetWorldSpaceNormalizeViewDir(i.positionWS), l=normalize(_LightDir.xyz); float d=.5+.5*max(dot(n,l),0);float3 rim=Fresnel!=0 ? pow(1-saturate(dot(n,v)),FrePower)*FreColor.rgb : 0;return half4(SAMPLE_TEXTURE2D(MainTex,sampler_MainTex,i.uv0.xy).rgb*MainColor.rgb*d+rim+SAMPLE_TEXTURE2D(EmissionTex,sampler_EmissionTex,i.uv0.xy).rgb*EmissionColor.rgb*E,1); }
ENDHLSL
}
}
}
