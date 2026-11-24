Shader "RadioArchipelago/Signal Spark Glass"
{
    Properties
    {
        _BaseColor ("Glass tint", Color) = (.98,.88,.66,.022)
        [HDR] _RimColor ("Warm glass rim", Color) = (1,.8,.4,1)
        _RimOpacity ("Edge opacity", Range(0,1)) = .29
        _Smoothness ("Polish", Range(0,1)) = .92
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Name "Glass"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _RimColor;
                half _RimOpacity, _Smoothness;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n=normalize(i.normalWS);
                half3 v=GetWorldSpaceNormalizeViewDir(i.positionWS);
                half nv=saturate(dot(n,v));
                half fresnel=pow(1-nv,3.0h);
                Light light=GetMainLight();
                half3 h=normalize(v+light.direction);
                half spec=pow(saturate(dot(n,h)),lerp(48.0h,220.0h,_Smoothness));
                // Wide studio/sky reflections make glass readable without scene
                // refraction, additional lights, a screen copy or global bloom.
                half3 r=reflect(-v,n);
                half softbox=pow(saturate(dot(r,normalize(half3(-.62h,.72h,-.31h)))),28.0h);
                half sidebox=pow(saturate(dot(r,normalize(half3(.78h,.22h,.58h)))),45.0h);
                half sky=smoothstep(.05h,.85h,r.y);
                half3 color=_BaseColor.rgb*.36h+_RimColor.rgb*fresnel;
                color+=half3(1,.97h,.83h)*(softbox*.92h+spec*.9h)+half3(.72h,.95h,1)*sidebox*.68h;
                color+=half3(.64h,.86h,.91h)*sky*.10h;
                half alpha=saturate(_BaseColor.a+fresnel*_RimOpacity+softbox*.60h+sidebox*.45h+spec*.40h);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
