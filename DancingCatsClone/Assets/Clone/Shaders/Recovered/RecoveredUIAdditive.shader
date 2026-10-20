Shader "DancingCatsClone/Recovered/CustomShader/UI/UIAdditive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture",2D)="white"{}
        _StencilComp ("Stencil comparison",Float)=8
        _Stencil ("Stencil ID",Float)=0
        _StencilOp ("Stencil operation",Float)=0
        _StencilWriteMask ("Stencil write",Float)=255
        _StencilReadMask ("Stencil read",Float)=255
        _ColorMask ("Color mask",Float)=15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha One ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;float4 _MainTex_ST;
            Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o; }
            fixed4 Frag(Varyings i):SV_Target { return tex2D(_MainTex,i.uv)*i.color; }
            ENDCG
        }
    }
}
