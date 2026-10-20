Shader "DancingCatsClone/Recovered/CustomShader/Particles/Additive"
{
    Properties
    {
        _TintColor ("Tint Color",Color)=(.5,.5,.5,.5)
        _MainTex ("Particle Texture",2D)="white"{}
        _InvFade ("Soft Particles Factor",Range(.01,3))=1
        _Alpha ("Alpha",Range(0,1))=1
        _EnableFog ("Enable Fog",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float fog:TEXCOORD1; };
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;half4 _TintColor;float _Alpha,_EnableFog,_InvFade;
            CBUFFER_END
            Varyings Vert(Attributes v) { Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.color=v.color;o.fog=ComputeFogFactor(p.positionCS.z);return o; }
            half4 Frag(Varyings i):SV_Target { half4 c=2*i.color*_TintColor*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);c.a*=_Alpha;if(_EnableFog!=0)c.rgb=MixFogColor(c.rgb,half3(0,0,0),i.fog);return c; }
            ENDHLSL
        }
    }
}
