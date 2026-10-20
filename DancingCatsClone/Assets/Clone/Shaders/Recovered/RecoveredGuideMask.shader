Shader "DancingCatsClone/Recovered/UI/GuideMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture",2D)="white"{}
        _Color ("Tint",Color)=(1,1,1,1)
        _Blur ("Edge fade",Range(1,1000))=100
        _Origin ("Original circle / rectangle",Vector)=(0,0,0,0)
        _MaskType ("Original mask type",Float)=0
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
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float2 local:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;float4 _Color,_TextureSampleAdd,_Origin;float _Blur,_MaskType;
            Varyings Vert(Attributes v) { Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;o.local=v.vertex.xy;return o; }
            fixed4 Frag(Varyings i):SV_Target
            {
                fixed4 c=(tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;
                if(_MaskType==0) { float distance=length(i.local-_Origin.xy);clip(distance-(_Origin.z-_Blur)); c.a*=saturate((distance-(_Origin.z-_Blur))/_Blur); }
                else if(_MaskType==1 || _MaskType==2) { bool inside=all(i.local>=_Origin.xy)&&all(i.local<=_Origin.zw);if(inside)c.a=0; }
                return c;
            }
            ENDCG
        }
    }
}
