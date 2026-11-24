Shader "RadioArchipelago/Amber Flow Water"
{
    Properties
    {
        _BaseColor ("Clear water", Color) = (.12,.48,.50,1)
        _FoamColor ("Aerated water", Color) = (.86,.97,.94,1)
        _UseLakeSurface ("Joined lake region in UV1", Range(0,1)) = 0
        _LakeColor ("Clear lake tint", Color) = (.10,.52,.57,1)
        _CurrentSpeed ("Downstream metres per second", Range(.1,10)) = 4.8
        _FoamStrength ("Travelling foam", Range(0,1)) = .85
        _RippleNormal ("Ripple normal data", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        // One connected sheet and one render mode from the stream to the foot.
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            Name "ContinuousAmberWater"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_RippleNormal); SAMPLER(sampler_RippleNormal);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _FoamColor, _LakeColor;
                float _CurrentSpeed, _FoamStrength, _UseLakeSurface;
            CBUFFER_END

            float Hash(float2 p)
            {
                float3 h=frac(p.xyx*.1031);
                h+=dot(h,h.yzx+33.33);
                return frac((h.x+h.y)*h.z);
            }
            float Noise(float2 p)
            {
                float2 c=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),
                    lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
            }
            struct Attributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float2 uv:TEXCOORD0;
                float2 region:TEXCOORD1;
                half4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 world:TEXCOORD0;
                half3 normal:TEXCOORD1;
                float2 uv:TEXCOORD2;
                half fade:TEXCOORD3;
                half fog:TEXCOORD4;
                half lake:TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(input.positionOS.xyz);
                o.normal=TransformObjectToWorldNormal(input.normalOS);
                // The welded mesh has genuine curved normals. UV.V is physical
                // downstream distance; the same clock displaces the whole ribbon.
                float phase=input.uv.y-_Time.y*_CurrentSpeed;
                float wave=sin(phase*2.1+input.uv.x*11)*.018
                    +sin(phase*3.8-input.uv.x*19)*.009;
                o.world+=o.normal*wave;
                o.positionCS=TransformWorldToHClip(o.world);
                o.uv=input.uv; o.fade=input.color.a;
                o.lake=_UseLakeSurface>.5 ? 1-saturate(input.region.x) : 0;
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 geometric=normalize(input.normal);
                float3 view=GetWorldSpaceNormalizeViewDir(input.world);
                geometric*=dot(geometric,view)>=0 ? 1 : -1;
                float fall=smoothstep(.08,.70,1-abs(geometric.y));
                // All detail has one advected coordinate. Foam identity survives
                // the horizontal reach, rounded lip, and vertical drop.
                float2 flow=float2(input.uv.x*8,input.uv.y-_Time.y*_CurrentSpeed);
                float broad=Noise(flow*float2(3.2,.35));
                float broken=Noise(flow*float2(6.5,.70)+17.3);
                float streaks=smoothstep(.55,.77,broad*.72+broken*.28);
                float packets=smoothstep(.52,.78,Noise(flow*float2(1.6,1.1)+11.7));
                float small=Noise(flow*float2(5.8,3.2)+4.2);
                // Narrow, interrupted threads with travelling aerated heads,
                // rather than round white islands painted onto a flat sheet.
                float foam=saturate(streaks*(.40+packets*.60)*(.78+small*.22)*_FoamStrength);
                float filaments=Noise(float2(flow.x*2.1,flow.y*.22));
                half3 base=lerp(_BaseColor.rgb*.85,_BaseColor.rgb*1.18,filaments);
                base=lerp(base,_FoamColor.rgb,saturate(foam+fall*.10));

                float2 rippleUV0=flow*.14, rippleUV1=flow*float2(.31,.23)+3.7;
                if (input.lake>.0001)
                {
                    rippleUV0=lerp(rippleUV0,flow*.095,input.lake);
                    rippleUV1=lerp(rippleUV1,mul(float2x2(.8,-.6,.6,.8),flow*.095)*1.87,input.lake);
                }
                float4 ripple0=SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,rippleUV0);
                float4 ripple1=SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,rippleUV1);
                half3 n0=ripple0.xyz*2-1;
                half3 n1=ripple1.xyz*2-1;
                float2 slope=n0.xy*.16+n1.xy*.09;
                // A broad lake and the narrow falling ribbon are ONE mesh and
                // height function. UV1 changes appearance smoothly; it never
                // starts a second animated surface on top of the lake.
                if (input.lake>.0001)
                {
                    slope*=lerp(1,3,input.lake);
                    float lakeFoam=smoothstep(.76,.94,ripple0.a)*smoothstep(.66,.88,ripple1.a)*.24;
                    float lakeVariation=Noise(flow*float2(.65,.70)+8.4);
                    half3 lakeBase=lerp(_LakeColor.rgb*.92,_LakeColor.rgb*1.08,lakeVariation);
                    lakeBase=lerp(lakeBase,_FoamColor.rgb,lakeFoam);
                    base=lerp(base,lakeBase,input.lake);
                }
                float3 dx=ddx(input.world),dy=ddy(input.world);
                float2 ux=ddx(input.uv),uy=ddy(input.uv);
                float det=ux.x*uy.y-ux.y*uy.x;
                float3 across=normalize(float3(geometric.z+.001,0,-geometric.x));
                float3 along=normalize(cross(geometric,across));
                if (abs(det)>.000001)
                {
                    across=normalize((dx*uy.y-dy*ux.y)/det);
                    along=normalize((dy*ux.x-dx*uy.x)/det);
                }
                float3 normal=normalize(geometric+across*slope.x+along*slope.y);
                Light sun=GetMainLight();
                // White water stays legible in the shadow of the lip; its
                // animated albedo carries motion rather than a static highlight.
                half light=.68+.32*saturate(dot(normal,sun.direction));
                half3 rgb=base*light;
                half spec=pow(saturate(dot(normal,normalize(sun.direction+view))),48);
                rgb+=sun.color*spec*.17*(1-foam*.8);
                half fresnel=pow(1-saturate(dot(normal,view)),4);
                rgb=lerp(rgb,half3(.46,.73,.76),fresnel*.16*(1-fall*.6));
                if (input.lake>.0001)
                {
                    float3 reflected=reflect(-view,normal);
                    half sky=smoothstep(-.15,.65,reflected.y);
                    half3 sheen=lerp(half3(.20,.48,.58),half3(.62,.85,.91),smoothstep(.08,.95,reflected.y));
                    rgb=lerp(rgb,sheen,(.22+fresnel*.25)*sky*input.lake);
                }
                float edge=min(input.uv.x,1-input.uv.x);
                float edgeFade=smoothstep(0,.014+small*.009,edge);
                if (input.lake>.0001) edgeFade=lerp(edgeFade,1,input.lake);
                half alpha=(.86+foam*.14)*edgeFade*input.fade;
                if (input.lake>.0001) alpha=lerp(alpha,input.fade,input.lake);
                clip(alpha-.008);
                return half4(MixFog(rgb,input.fog),alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
