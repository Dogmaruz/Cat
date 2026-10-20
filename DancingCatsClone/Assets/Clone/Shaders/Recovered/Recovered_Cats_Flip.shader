// Reconstructed from original GLES program sharedassets3.assets:53; original Properties and render state preserved.
Shader "DancingCatsClone/Recovered/Shader Graphs/Cats_Flip"
{
Properties {
		W_H ("W/H", Vector) = (4,4,0,0)
		[NoScaleOffset] Texture2D_15844ab26672480596edf1198ef1d845 ("Texture2D", 2D) = "white" {}
		[NoScaleOffset] Texture2D_43b8e64c0f804b40a568954e1d4a8d17 ("Mask", 2D) = "white" {}
		[HDR] Color_b66ccb83b9a0425baa3cc88db5f6da07 ("Color", Vector) = (1,1,1,1)
		Vector1_9abfae430c104222a8353b36c9e711ee ("Float", Range(1, 8)) = 1
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
TEXTURE2D(Texture2D_15844ab26672480596edf1198ef1d845); SAMPLER(sampler_Texture2D_15844ab26672480596edf1198ef1d845);
TEXTURE2D(Texture2D_43b8e64c0f804b40a568954e1d4a8d17); SAMPLER(sampler_Texture2D_43b8e64c0f804b40a568954e1d4a8d17);
CBUFFER_START(UnityPerMaterial)
float4 W_H;
float4 Color_b66ccb83b9a0425baa3cc88db5f6da07;
float Vector1_9abfae430c104222a8353b36c9e711ee;
float _QueueOffset;
float _QueueControl;
CBUFFER_END
Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv0=v.uv0;o.uv1=v.uv1;o.color=v.color;return o; }
half4 Frag(Varyings i):SV_Target { float count=W_H.x*W_H.y; float frame=floor(fmod(_TimeParameters.x+Vector1_9abfae430c104222a8353b36c9e711ee+.00001,count)); float row=floor((frame+.5)/W_H.x); float col=frame-W_H.x*row; float2 uv=(W_H.xy-float2(col,row)-1+i.uv0.xy)/W_H.xy; half3 t=SAMPLE_TEXTURE2D(Texture2D_15844ab26672480596edf1198ef1d845,sampler_Texture2D_15844ab26672480596edf1198ef1d845,uv).rgb; return half4(t*(1+(1-SAMPLE_TEXTURE2D(Texture2D_43b8e64c0f804b40a568954e1d4a8d17,sampler_Texture2D_43b8e64c0f804b40a568954e1d4a8d17,i.uv0.xy).rgb)*Color_b66ccb83b9a0425baa3cc88db5f6da07.rgb),1); }
ENDHLSL
}
}
}
