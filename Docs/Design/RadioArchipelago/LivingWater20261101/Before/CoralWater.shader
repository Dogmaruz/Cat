Shader "RadioArchipelago/Coral Water"
{
    Properties
    {
        _BaseColor ("Water tint", Color) = (1,1,1,1)
        _FlowSpeed ("Flow speed", Float) = .8
        _Fall ("Vertical cascade", Range(0,1)) = 0
        _FoamStrength ("Foam", Range(0,1)) = .35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Cull Off
        Pass
        {
            Name "FlowingOasisWater"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _FlowSpeed, _Fall, _FoamStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 color:TEXCOORD2; float2 uv:TEXCOORD3; half fog:TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.world=p.positionWS; o.normal=TransformObjectToWorldNormal(v.normalOS);
                o.color=v.color; o.uv=v.uv; o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 Frag(Varyings v, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(v); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float time=_Time.y*_FlowSpeed;
                float2 p=lerp(v.world.xz*.55,v.uv*float2(4,3),_Fall);
                float ripple=sin(p.x*2.7+time)*sin(p.y*3.5-time*.73);
                float streak=pow(saturate(sin(p.x*7+sin(p.y-time)*.34)),12);
                float flow=pow(saturate(sin(p.y*4-time*3+sin(p.x*9)*.3)),10);
                half foam=saturate((streak*.5+flow*.45)*_Fall+pow(saturate(ripple),8)*.35)*_FoamStrength;
                half3 base=max(v.color.rgb,.003)*_BaseColor.rgb;
                base*=.93+ripple*.07;
                base=lerp(base,half3(.70,.96,.93),foam);
                // Imported normals already include the mirrored FBX basis.
                // Face the thin water sheet toward the viewer without applying
                // transform parity twice and shading a sunlit pool from below.
                half3 normal=normalize(v.normal);
                normal*=dot(normal,GetWorldSpaceNormalizeViewDir(v.world))>=0 ? 1 : -1;
                normal=normalize(normal+float3(sin(p.y*2+time)*.035,0,cos(p.x*3-time)*.035)*(1-_Fall));
                InputData data=(InputData)0;
                data.positionWS=v.world; data.normalWS=normal; data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.world);
                data.shadowCoord=TransformWorldToShadowCoord(v.world); data.bakedGI=SampleSH(normal); data.shadowMask=half4(1,1,1,1);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=base; surface.alpha=1; surface.smoothness=.78; surface.metallic=.08; surface.occlusion=1;
                surface.normalTS=half3(0,0,1); surface.emission=foam*half3(.025,.05,.045);
                half4 result=UniversalFragmentPBR(data,surface); result.rgb=MixFog(result.rgb,v.fog); return result;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
