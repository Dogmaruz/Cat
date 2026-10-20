// Reconstructed from original GLES program sharedassets0.assets:864; original Properties and render state preserved.
Shader "DancingCatsClone/Recovered/Shader Graphs/Panio_Particle_AlphaBlend"
{
Properties {
		[HDR] Color_43d50ce605284bdfb64dcbea2b745524 ("Color", Vector) = (0.1084906,1,0.3724781,0)
		[NoScaleOffset] Texture2D_56cdf3387e9d4491920e2b081f7b5281 ("Texture2D", 2D) = "white" {}
		Alpha ("Alpha", Range(0, 1)) = 1
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
Cull Back
ZTest LEqual
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Varyings { float4 positionCS:SV_POSITION;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;float3 positionWS:TEXCOORD2;float3 normalWS:TEXCOORD3;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
TEXTURE2D(Texture2D_56cdf3387e9d4491920e2b081f7b5281); SAMPLER(sampler_Texture2D_56cdf3387e9d4491920e2b081f7b5281);
CBUFFER_START(UnityPerMaterial)
float4 Color_43d50ce605284bdfb64dcbea2b745524;
float Alpha;
float _QueueOffset;
float _QueueControl;
CBUFFER_END
Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv0=v.uv0;o.uv1=v.uv1;o.color=v.color;return o; }
half4 Frag(Varyings i):SV_Target { half4 t=SAMPLE_TEXTURE2D(Texture2D_56cdf3387e9d4491920e2b081f7b5281,sampler_Texture2D_56cdf3387e9d4491920e2b081f7b5281,i.uv0.xy); return half4(t.rgb*Color_43d50ce605284bdfb64dcbea2b745524.rgb*i.color.rgb,(t.a)*Color_43d50ce605284bdfb64dcbea2b745524.a*i.color.a*Alpha); }
ENDHLSL
}
}
}
