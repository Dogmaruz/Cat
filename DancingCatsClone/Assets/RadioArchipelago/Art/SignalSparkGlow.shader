Shader "RadioArchipelago/Signal Spark Glow"
{
    Properties
    {
        [HDR] _BaseColor ("Warm signal glow", Color) = (1,.58,.12,.58)
        _Intensity ("Pulse", Float) = .95
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float radius=length(i.uv*2-1);
                float falloff=pow(saturate(1-radius),2.4);
                // One arithmetic noise sample removes visible 8-bit rings in
                // the low-opacity halo without a texture or post-process pass.
                float noise=frac(52.9829189*frac(dot(i.positionCS.xy,float2(.06711056,.00583715))))-.5;
                float alpha=max(0,_BaseColor.a*falloff*_Intensity+noise*(1.0/255.0)*saturate(falloff*400));
                return half4(_BaseColor.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
