Shader "RadioArchipelago/Signal Spark Filament Aura"
{
    Properties
    {
        [HDR] _BaseColor ("Warm filament glow", Color) = (1,.48,.065,.40)
        _GlowWidth ("World glow radius", Range(0,.04)) = .012
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _GlowWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                // Imported FBX nodes retain their unit conversion. Expand in
                // metres so the soft edge is independent of that local scale.
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz)+normalize(o.normalWS)*_GlowWidth;
                o.positionCS=TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half facing=saturate(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)));
                half feather=smoothstep(0,.8h,facing);
                return half4(_BaseColor.rgb,_BaseColor.a*feather);
            }
            ENDHLSL
        }
    }
}
