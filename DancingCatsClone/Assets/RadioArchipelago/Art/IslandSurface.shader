Shader "RadioArchipelago/Island Surface"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _DetailMap ("Stone detail", 2D) = "gray" {}
        _DetailStrength ("Stone texture strength", Range(0,1)) = 0
        _DetailScale ("Stone texture metres", Float) = .25
        _Smoothness ("Smoothness", Range(0,1)) = .25
        _Metallic ("Metallic", Range(0,1)) = 0
        _Road ("Metric paving", Float) = 0
        _ProtectGameplaySurface ("Keep walkable relief opaque", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _DetailStrength, _DetailScale, _Smoothness, _Metallic, _Road, _Cull, _ProtectGameplaySurface;
        CBUFFER_END
        TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
        // Presentation globals stay outside material data so the island keeps
        // its shared materials and SRP Batcher layout.
        float4 _RA_ClearViewCamera;
        float4 _RA_ClearViewActor;
        float4 _RA_ClearViewLanding;
        float _RA_ClearViewFloor;

        float ClearViewCoverage(float3 positionWS, float4 target)
        {
            if (target.w <= 0) return 0;
            float3 axis = target.xyz - _RA_ClearViewCamera.xyz;
            float3 offset = positionWS - _RA_ClearViewCamera.xyz;
            float along = dot(offset, axis) / max(dot(axis, axis), .0001);
            // Geometry at or behind the target must stay intact.
            if (along <= 0 || along >= 1) return 0;
            float3 radial = offset - axis * along;
            float radius = target.w * along;
            float radiusSquared = radius * radius;
            // The inner 80% clears completely; only the outer ring dithers.
            return 1 - smoothstep(radiusSquared * .64, radiusSquared, dot(radial, radial));
        }

        void ClipClearView(float3 positionWS, float2 positionSS)
        {
            [branch] if (_RA_ClearViewCamera.w < .5) return;
            if (_ProtectGameplaySurface > .5) return;
            // Other cameras (including Scene View) retain the complete island.
            float3 cameraOffset = GetCameraPositionWS() - _RA_ClearViewCamera.xyz;
            [branch] if (dot(cameraOffset, cameraOffset) > .0001) return;
            if (positionWS.y <= _RA_ClearViewFloor) return;
            float coverage = max(ClearViewCoverage(positionWS, _RA_ClearViewActor),
                ClearViewCoverage(positionWS, _RA_ClearViewLanding));
            if (coverage <= 0) return;
            // A fixed screen pattern agrees across color and depth prepasses.
            clip(InterleavedGradientNoise(positionSS, 0) - coverage);
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 color : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half fog : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS = p.positionCS; o.positionWS = p.positionWS;
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.color = v.color; o.uv = v.uv; o.fog = ComputeFogFactor(p.positionCS.z);
            return o;
        }
        half4 Frag(Varyings v, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
            ClipClearView(v.positionWS, v.positionCS.xy);
            half3 normal = normalize(v.normalWS);
            // Mirrored FBX wrappers already transform opaque surface normals.
            // Only two-sided foliage needs a face correction, including parity.
            if (_Cull < .5) normal *= IS_FRONT_VFACE(face, 1, -1) * GetOddNegativeScale();
            half3 albedo = max(v.color.rgb, .002) * _BaseColor.rgb;
            if (_DetailStrength > .001)
            {
                float3 blend = pow(abs(normal), 4); blend /= max(dot(blend, 1), .001);
                float3 p = v.positionWS * _DetailScale;
                half3 detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, p.yz).rgb * blend.x
                    + SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, p.xz).rgb * blend.y
                    + SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, p.xy).rgb * blend.z;
                half gray = dot(detail, half3(.299,.587,.114));
                albedo *= lerp(1, clamp(gray * 2.2, .55, 1.45), _DetailStrength);
            }
            if (_Road > .5)
            {
                float row = floor(v.uv.y / .56);
                float2 cell = float2(v.uv.x / .79 + frac(row * .5), v.uv.y / .56);
                float2 edge = min(frac(cell), 1 - frac(cell));
                float aa = max(fwidth(cell.x), fwidth(cell.y));
                float stone = smoothstep(.035, .035 + aa, min(edge.x, edge.y));
                float variation = frac(sin(dot(floor(cell), float2(12.9898,78.233))) * 43758.5453);
                albedo *= lerp(.57, .86 + variation * .23, stone);
            }
            InputData data = (InputData)0;
            data.positionWS = v.positionWS; data.normalWS = normal;
            data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(v.positionWS);
            data.shadowCoord = TransformWorldToShadowCoord(v.positionWS);
            data.bakedGI = SampleSH(normal);
            data.vertexLighting = VertexLighting(v.positionWS, normal);
            data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(v.positionCS);
            data.shadowMask = half4(1,1,1,1);
            SurfaceData surface = (SurfaceData)0;
            surface.albedo = albedo; surface.alpha = 1;
            surface.metallic = _Metallic; surface.smoothness = _Smoothness;
            surface.normalTS = half3(0,0,1); surface.occlusion = 1;
            half4 result = UniversalFragmentPBR(data, surface);
            result.rgb = MixFog(result.rgb, v.fog);
            return result;
        }

        half DepthFrag(Varyings v) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
            ClipClearView(v.positionWS, v.positionCS.xy);
            return v.positionCS.z;
        }

        void DepthNormalsFrag(Varyings v, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC,
            out half4 outNormalWS : SV_Target0
        #ifdef _WRITE_RENDERING_LAYERS
            , out uint outRenderingLayers : SV_Target1
        #endif
        )
        {
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
            ClipClearView(v.positionWS, v.positionCS.xy);
            float3 normal = normalize(v.normalWS);
            if (_Cull < .5) normal *= IS_FRONT_VFACE(face, 1, -1) * GetOddNegativeScale();
        #if defined(_GBUFFER_NORMALS_OCT)
            float2 octNormal = saturate(PackNormalOctQuadEncode(normal) * .5 + .5);
            outNormalWS = half4(PackFloat2To888(octNormal), 0);
        #else
            outNormalWS = half4(normal, 0);
        #endif
        #ifdef _WRITE_RENDERING_LAYERS
            outRenderingLayers = EncodeMeshRenderingLayer();
        #endif
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
