Shader "DancingCatsClone/Recovered Surface"
{
    Properties
    {
        _MainTex ("Original texture", 2D) = "white" {}
        _Color ("Original color", Color) = (1,1,1,1)
        _EmissionTex ("Original emission", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission color", Color) = (0,0,0,1)
        _EmissionStrength ("Emission strength", Float) = 0
        _Shading ("Directional shape lighting", Range(0,1)) = 0
        _LightDir ("Original light direction", Vector) = (0,1,0.77,0)
        _FresnelColor ("Original rim color", Color) = (0,0,0,1)
        _FresnelPower ("Original rim power", Float) = 1
        _FresnelStrength ("Original rim enabled", Float) = 0
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0
        [HideInInspector] _SrcBlend ("Source", Float) = 1
        [HideInInspector] _DstBlend ("Destination", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
        [HideInInspector] _Cull ("Culling", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "RecoveredForward"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionTex); SAMPLER(sampler_EmissionTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; half4 _Color, _EmissionColor, _FresnelColor;
            float4 _LightDir; float _EmissionStrength, _Shading, _FresnelPower, _FresnelStrength, _Cutoff;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.color = v.color; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color * i.color;
                clip(c.a - _Cutoff);
                float3 n = normalize(i.normalWS);
                half diffuse = 0.65h + 0.35h * saturate(dot(n, normalize(_LightDir.xyz + float3(0,0.0001,0))));
                c.rgb *= lerp(1.0h, diffuse, _Shading);
                c.rgb += SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, i.uv).rgb * _EmissionColor.rgb * _EmissionStrength;
                half rim = pow(saturate(1-dot(n, GetWorldSpaceNormalizeViewDir(i.positionWS))), max(0.01, _FresnelPower));
                c.rgb += rim * _FresnelColor.rgb * _FresnelStrength;
                return c;
            }
            ENDHLSL
        }
    }
}
