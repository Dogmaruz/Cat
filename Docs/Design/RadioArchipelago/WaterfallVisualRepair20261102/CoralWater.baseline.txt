Shader "RadioArchipelago/Coral Water"
{
    Properties
    {
        _BaseColor ("Water tint", Color) = (1,1,1,1)
        _FlowSpeed ("Flow speed", Float) = .8
        _Fall ("Vertical cascade", Range(0,1)) = 0
        _FoamStrength ("Foam", Range(0,1)) = .35
        _FoamLayer ("Dissolve legacy foam ribbons", Range(0,1)) = 0
        _VertexFoam ("Dissolve bright vertex-colour ribbons", Range(0,1)) = 0
        _RippleNormal ("Shared linear ripple normal data", 2D) = "bump" {}
        _CurrentSpeed ("Current travel, metres per flow second", Range(0,6)) = 3.2
        _FlowDirection ("Surface current direction XZ", Vector) = (.8,0,.6,0)
        _UseFlowUV ("Stream UV: U across, V downstream metres", Range(0,1)) = 0
        _FallFadeRange ("Fall height fade: invisible, fully visible", Vector) = (-55,-33,0,0)
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
        [HideInInspector] _WaterPreviewTime ("Editor capture clock", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Cull Off
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Pass
        {
            Name "LivingArchipelagoWater"
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
            TEXTURE2D(_RippleNormal); SAMPLER(sampler_RippleNormal);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _FlowDirection;
                float4 _FallFadeRange;
                float _FlowSpeed, _Fall, _FoamStrength, _FoamLayer, _VertexFoam, _WaterPreviewTime, _SrcBlend, _DstBlend, _ZWrite;
                float _CurrentSpeed, _UseFlowUV;
            CBUFFER_END

            // Smooth advected fields, not a set of drawn sine-wave stripes.
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * .1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * f * (f * (f * 6 - 15) + 10);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + 1), f.x), f.y);
            }
            float WaterTime() { return (_WaterPreviewTime >= 0 ? _WaterPreviewTime : _Time.y) * _FlowSpeed; }
            // One downstream-metre carrier survives a horizontal stream,
            // curved bank and vertical curtain without changing phase/sign.
            float2 AuthoredFlowCoordinate(float2 uv, float time)
            {
                return float2(uv.x * 8.0, uv.y - time * _CurrentSpeed);
            }
            // A sparse patch keeps its identity as it travels. Normal-only
            // interference reads as stationary sparkle from the moving camera.
            float CurrentFleck(float2 coordinate)
            {
                float2 cell=floor(coordinate), local=frac(coordinate);
                float seed=Hash(cell+17.3);
                float2 center=float2(lerp(.22,.78,Hash(cell+4.7)),.5);
                float bend=sin(local.y*5.0+seed*6.283)*.06;
                float width=lerp(.10,.22,Hash(cell+31.1));
                float outline=1-smoothstep(.62,1.0,length(float2((local.x-center.x+bend)/width,(local.y-center.y)/.38)));
                float broken=smoothstep(.24,.65,Noise(coordinate*4.0+seed*13));
                return outline*broken*step(.57,seed);
            }
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 color:TEXCOORD2; float2 uv:TEXCOORD3; half fog:TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(v.normalOS);
                float time = WaterTime();
                // At most seven centimetres: preserves the authored banks and
                // the much larger clearance beneath the levitating supports.
                float wave = sin(dot(p.positionWS.xz, float2(.48,.31)) - time * 1.5) * .045
                    + sin(dot(p.positionWS.xz, float2(-.28,.71)) + time * 1.1) * .025;
                float horizontal = smoothstep(.45,.9,abs(normal.y)) * (1 - _Fall);
                p.positionWS.y += wave * horizontal * (1 - _FoamLayer);
                float falling = max(_Fall,1-smoothstep(.35,.85,abs(normal.y))) * (1-_FoamLayer);
                p.positionWS += normal * sin(p.positionWS.y*.8+time*2.1+v.uv.x*5) * .065 * falling;
                o.positionCS = TransformWorldToHClip(p.positionWS);
                o.world=p.positionWS; o.normal=normal; o.color=v.color; o.uv=v.uv;
                o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings v, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(v); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float time=WaterTime();
                float3 view=GetWorldSpaceNormalizeViewDir(v.world);
                half3 imported=max(v.color.rgb,.003)*_BaseColor.rgb;
                float luminance=dot(imported,float3(.2126,.7152,.0722));
                // Discard the legacy raised ribbons before normal/PBR work.
                if (_FoamLayer>.5) clip(-1);
                if (_VertexFoam>.5 && (imported.r > imported.b*.4 || luminance < .20)) clip(-1);
                // Face the imported mirrored water sheets toward the viewer.
                float3 normal=normalize(v.normal);
                normal*=dot(normal,view)>=0 ? 1 : -1;
                float fall=max(_Fall * (1 - _FoamLayer), 1 - smoothstep(.35,.85,abs(normal.y)));
                float2 currentDirection=_FlowDirection.xz;
                currentDirection=dot(currentDirection,currentDirection)>.001 ? normalize(currentDirection) : float2(.8,.6);
                float2 crossDirection=float2(-currentDirection.y,currentDirection.x);
                float2 currentPosition=float2(dot(v.world.xz,crossDirection),dot(v.world.xz,currentDirection));
                currentPosition=lerp(currentPosition,float2(v.uv.x*8.0,v.uv.y),_UseFlowUV);
                currentPosition.y-=time*_CurrentSpeed;
                if (_UseFlowUV>.5)
                    currentPosition=AuthoredFlowCoordinate(v.uv,time);
                // Both layers are carried by the SAME current. A small lateral
                // drift adds variation without cancelling the visible travel.
                float2 surfaceUV=currentPosition*.095;
                float2 cascadePosition=float2(_Fall>.5 ? v.uv.x*8.0 : dot(v.world,float3(.73,0,.68)),v.world.y+time*10.0);
                float2 flowUV=cascadePosition*float2(.10,.09);
                float2 uv0=lerp(surfaceUV,flowUV,fall);
                float2 uv1=lerp(mul(float2x2(.8,-.6,.6,.8),surfaceUV)*1.87+float2(.006,0)*time,
                    flowUV*float2(2.2,.72)+float2(.012,.025)*time,fall);
                if (_UseFlowUV>.5)
                {
                    // Blend appearance with slope, never incompatible spatial
                    // coordinates. Joined UV-authored materials share this clock.
                    cascadePosition=currentPosition;
                    uv0=surfaceUV;
                    uv1=mul(float2x2(.8,-.6,.6,.8),surfaceUV)*1.87;
                }
                half4 ripple0=SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,uv0);
                half4 ripple1=SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,uv1);
                half3 n0=ripple0.xyz*2-1, n1=ripple1.xyz*2-1;
                float2 slope=n0.xy/max(n0.z,.25)*.55+n1.xy/max(n1.z,.25)*.34;
                float3 streamAcross=float3(crossDirection.x,0,crossDirection.y);
                float3 streamAlong=float3(currentDirection.x,0,currentDirection.y);
                if (_UseFlowUV>.5)
                {
                    // Follow the real bank tangent all the way over its lip.
                    // The default basis only handles a degenerate UV triangle.
                    streamAcross=normalize(float3(normal.z+.001,0,-normal.x));
                    streamAlong=normalize(cross(normal,streamAcross));
                    float3 dx=ddx(v.world),dy=ddy(v.world);
                    float2 ux=ddx(v.uv),uy=ddy(v.uv);
                    float determinant=ux.x*uy.y-ux.y*uy.x;
                    if (abs(determinant)>.000001)
                    {
                        float3 across=(dx*uy.y-dy*ux.y)/determinant;
                        float3 along=(dy*ux.x-dx*uy.x)/determinant;
                        streamAcross=normalize(across-normal*dot(across,normal));
                        streamAlong=normalize(along-normal*dot(along,normal));
                    }
                }
                if (_UseFlowUV>.5)
                    normal=normalize(normal+streamAcross*slope.x*lerp(1,.32,fall)+streamAlong*slope.y*lerp(1,.20,fall));
                else
                {
                    float3 horizontalNormal=normalize(float3(0,1,0)+streamAcross*slope.x+streamAlong*slope.y);
                    float3 tangent=normalize(float3(normal.z+.001,0,-normal.x));
                    float3 bitangent=normalize(cross(normal,tangent));
                    float3 cascadeNormal=normalize(normal+tangent*slope.x*.32+bitangent*slope.y*.20);
                    normal=normalize(lerp(horizontalNormal,cascadeNormal,fall));
                }

                half3 base=imported;
                half crest=smoothstep(.76,.94,ripple0.a)*smoothstep(.66,.88,ripple1.a);
                float fineFlow=0, cascadeFoam=0;
                if (fall<.01 || _UseFlowUV>.5)
                {
                    float flecks=CurrentFleck(currentPosition*float2(.68,.48));
                    flecks+=CurrentFleck(currentPosition*float2(1.05,.73)+float2(11.7,6.2))*.45;
                    float bank=_UseFlowUV*(1-smoothstep(.04,.22,min(v.uv.x,1-v.uv.x)));
                    half froth=(crest*.24+flecks*(1.05+bank*.65))*_FoamStrength;
                    if (_UseFlowUV>.5) froth*=1-fall;
                    base=lerp(base,half3(.66,.88,.88),froth);
                }
                // Longitudinal filaments and short aerated packets visibly
                // descend together, instead of blinking in place.
                if (fall>=.01 || _UseFlowUV>.5)
                {
                    float broadFlow=Noise(cascadePosition*float2(1.6,.17));
                    float2 lateralDrift=_UseFlowUV>.5 ? float2(0,0) : float2(.04,0)*time;
                    fineFlow=Noise(cascadePosition*float2(4.2,.60)+lateralDrift);
                    float cascadeFlecks=CurrentFleck(cascadePosition*float2(1.35,.32)+float2(4.6,9.2));
                    cascadeFoam=saturate(smoothstep(.29,.77,broadFlow*.70+fineFlow*.30)*.78+cascadeFlecks*.42);
                }
                if (_Fall>.5)
                {
                    float edge=min(v.uv.x,1-v.uv.x);
                    clip(edge-.004-fineFlow*.009);
                }
                base=lerp(base,half3(.64,.87,.91),cascadeFoam*fall*.64);
                InputData data=(InputData)0;
                data.positionWS=v.world; data.normalWS=normal; data.viewDirectionWS=view;
                data.shadowCoord=TransformWorldToShadowCoord(v.world); data.bakedGI=SampleSH(normal); data.shadowMask=half4(1,1,1,1);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=base; surface.alpha=1; surface.smoothness=lerp(.86,.69,fall); surface.metallic=.04; surface.occlusion=1;
                surface.normalTS=half3(0,0,1);
                surface.emission=cascadeFoam*fall*half3(.045,.075,.085);
                half4 result=UniversalFragmentPBR(data,surface);
                // Broad sky sheen remains visible where a mobile scene has no
                // baked reflection probe. It follows the moving surface normal.
                half fresnel=pow(1-saturate(dot(normal,view)),4);
                float3 reflected=reflect(-view,normal);
                half sky=smoothstep(-.15,.65,reflected.y);
                half3 sheen=lerp(half3(.20,.48,.58),half3(.62,.85,.91),smoothstep(.08,.95,reflected.y));
                result.rgb=lerp(result.rgb,sheen,(.30+fresnel*.38)*sky*(1-fall*.85));
                Light sun=GetMainLight(data.shadowCoord);
                half broad=saturate(dot(normal,normalize(sun.direction+view)));
                half glints=smoothstep(.72,.94,ripple0.a)*pow(saturate(dot(normal,normalize(view+float3(0,1,0)))),12);
                result.rgb+=(pow(broad,64)*.8+glints*.18)*(1-fall*.7)*sun.color*sun.shadowAttenuation;
                half alpha=_Fall>.5 ? (.72+fineFlow*.28)*smoothstep(_FallFadeRange.x,_FallFadeRange.y,v.world.y) : 1;
                result.rgb=MixFog(result.rgb,v.fog); return half4(result.rgb,alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
