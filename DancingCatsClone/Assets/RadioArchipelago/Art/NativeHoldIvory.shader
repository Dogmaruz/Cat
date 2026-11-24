Shader "RadioArchipelago/Native Hold Ivory"
{
    Properties
    {
        [NoScaleOffset] MainTex ("Original body atlas", 2D) = "white" {}
        MainColor ("Mint glaze tint", Color) = (.48,.80,.79,1)
        _LightDir ("Original light direction", Vector) = (0,1,.77,0)
        [ToggleUI] Fresnel ("Soft rim", Float) = 0
        FreColor ("Rim tint", Color) = (.08,.08,.075,1)
        FrePower ("Rim power", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 MainColor, FreColor;
                float4 _LightDir;
                float Fresnel, FrePower;
            CBUFFER_END
            TEXTURE2D(MainTex); SAMPLER(sampler_MainTex);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=v.uv;
                return o;
            }
            half4 Frag(Varyings v):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(v);
                half3 original = SAMPLE_TEXTURE2D(MainTex,sampler_MainTex,v.uv).rgb;
                // Preserve the native atlas's bevel/value pattern while removing
                // its baked blue hue. UVs, line, dots and hit effects stay native.
                half value = max(original.r,max(original.g,original.b));
                half3 n=normalize(v.normalWS);
                half diffuse=.5h+.5h*max(dot(n,normalize(_LightDir.xyz)),0);
                half rim = Fresnel!=0 ? pow(1-saturate(dot(n,GetWorldSpaceNormalizeViewDir(v.positionWS))),max(FrePower,.01)) : 0;
                return half4(value*MainColor.rgb*diffuse+rim*FreColor.rgb,1);
            }
            ENDHLSL
        }
    }
}
