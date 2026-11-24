Shader "RadioArchipelago/Island Tree Leaves"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = .18
        _VeinStrength ("Soft vein detail", Range(0,1)) = .30
        _MottleStrength ("Subtle leaf variation", Range(0,.3)) = .08
        _TransmissionStrength ("Leaf backlight", Range(0,.35)) = .14
        _TransmissionTint ("Backlight tint", Color) = (.85,1,.62,1)
        _WindStrength ("Tip sway metres", Range(0,.3)) = .055
        _WindSpeed ("Wind speed", Range(0,4)) = 1.15
        _WindScale ("Wind spatial scale", Range(.05,3)) = .35
        _WindDirection ("Wind direction XZ", Vector) = (.85,0,.53,0)
        _ProtectGameplaySurface ("Keep walkable relief opaque", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
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
            half4 _BaseColor, _TransmissionTint;
            float4 _WindDirection;
            float _Smoothness, _WindStrength, _WindSpeed, _WindScale, _Cull, _ProtectGameplaySurface;
            float _VeinStrength, _MottleStrength, _TransmissionStrength;
        CBUFFER_END
        // Same presentation globals as Island Surface; shared leaf materials
        // retain the actor/landing visibility corridor and SRP Batcher layout.
        float4 _RA_ClearViewCamera;
        float4 _RA_ClearViewActor;
        float4 _RA_ClearViewLanding;
        float _RA_ClearViewFloor;
        float3 _LightDirection;
        float3 _LightPosition;

        float LeafClearViewCoverage(float3 positionWS, float4 target)
        {
            if (target.w <= 0) return 0;
            float3 axis = target.xyz - _RA_ClearViewCamera.xyz;
            float3 offset = positionWS - _RA_ClearViewCamera.xyz;
            float along = dot(offset, axis) / max(dot(axis, axis), .0001);
            if (along <= 0 || along >= 1) return 0;
            float3 radial = offset - axis * along;
            float radius = target.w * along;
            float radiusSquared = radius * radius;
            return 1 - smoothstep(radiusSquared * .64, radiusSquared, dot(radial, radial));
        }

        void LeafClipClearView(float3 positionWS, float2 positionSS)
        {
            [branch] if (_RA_ClearViewCamera.w < .5) return;
            if (_ProtectGameplaySurface > .5) return;
            float3 cameraOffset = GetCameraPositionWS() - _RA_ClearViewCamera.xyz;
            [branch] if (dot(cameraOffset, cameraOffset) > .0001) return;
            if (positionWS.y <= _RA_ClearViewFloor) return;
            float coverage = max(LeafClearViewCoverage(positionWS, _RA_ClearViewActor),
                LeafClearViewCoverage(positionWS, _RA_ClearViewLanding));
            if (coverage <= 0) return;
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
            float3 normalWS : TEXCOORD1;
            half3 color : TEXCOORD2;
            half fog : TEXCOORD3;
            float2 uv : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        void LeafWindVertex(Attributes input, out float3 positionWS, out float3 normalWS)
        {
            positionWS = TransformObjectToWorld(input.positionOS.xyz);
            normalWS = TransformObjectToWorldNormal(input.normalOS);
            float2 windAxis = _WindDirection.xz;
            windAxis *= rsqrt(max(dot(windAxis, windAxis), .0001));
            float phase = _Time.y * _WindSpeed + dot(positionWS.xz, float2(.27,.19)) * _WindScale;
            float wave = sin(phase) * .72 + sin(phase * 1.73 + positionWS.z * .31) * .28;
            // Vertex alpha is wind weight, never transparency. Root alpha 0
            // pins each leaf; squared weight gives tips a gentle curved sway.
            float weight = saturate(input.color.a);
            float bend = _WindStrength * weight * weight * wave;
            positionWS.xz += windAxis * bend;
            positionWS.y -= abs(bend) * .12;
        }

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            LeafWindVertex(input, output.positionWS, output.normalWS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.color = input.color.rgb;
            output.uv = input.uv;
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        float LeafNoiseHash(float2 sampleCoord)
        {
            sampleCoord = frac(sampleCoord * float2(.1031,.1030));
            sampleCoord += dot(sampleCoord, sampleCoord.yx + 33.33);
            return frac((sampleCoord.x + sampleCoord.y) * sampleCoord.x);
        }

        float LeafMottle(float2 uv)
        {
            float2 sampleCoord = uv * float2(12,18);
            float2 cell = floor(sampleCoord), blend = frac(sampleCoord);
            blend = blend * blend * (3 - 2 * blend);
            float noise = lerp(lerp(LeafNoiseHash(cell), LeafNoiseHash(cell + float2(1,0)), blend.x),
                lerp(LeafNoiseHash(cell + float2(0,1)), LeafNoiseHash(cell + float2(1,1)), blend.x), blend.y);
            // Texture-free detail fades before it can shimmer on distant leaves.
            float fade = 1 - saturate(max(fwidth(sampleCoord.x), fwidth(sampleCoord.y)));
            return (noise - .5) * fade;
        }

        half LeafVeins(float2 uv)
        {
            // UV0: U is across a leaf, V runs from its stem to the pointed tip.
            float across = abs(uv.x - .5);
            float midribWidth = max(fwidth(uv.x), .001);
            float midrib = 1 - smoothstep(.012, .012 + midribWidth, across);
            float branchPhase = uv.y * 8 - across * 4.5;
            float branchDistance = abs(frac(branchPhase + .5) - .5);
            float branchWidth = max(fwidth(branchPhase), .025);
            float branches = (1 - smoothstep(.022, .022 + branchWidth, branchDistance))
                * smoothstep(.025, .12, across) * (1 - smoothstep(.85, 1, uv.y));
            float branchFade = 1 - saturate(fwidth(branchPhase) * 1.5);
            return midrib * .24 + branches * branchFade * .10;
        }

        half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            LeafClipClearView(input.positionWS, input.positionCS.xy);
            half3 normal = normalize(input.normalWS);
            if (_Cull < .5) normal *= IS_FRONT_VFACE(face, 1, -1) * GetOddNegativeScale();
            InputData data = (InputData)0;
            data.positionWS = input.positionWS;
            data.normalWS = normal;
            data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            data.bakedGI = SampleSH(normal);
            data.vertexLighting = VertexLighting(input.positionWS, normal);
            data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            data.shadowMask = half4(1,1,1,1);
            SurfaceData surface = (SurfaceData)0;
            // Keep the authored vertex palette. Veins and mottling only change
            // value subtly; the curved mesh supplies the real leaf silhouette.
            surface.albedo = max(input.color, .002) * _BaseColor.rgb;
            surface.albedo *= 1 + LeafVeins(input.uv) * _VeinStrength + LeafMottle(input.uv) * _MottleStrength;
            surface.alpha = 1;
            surface.metallic = 0;
            surface.smoothness = _Smoothness;
            surface.normalTS = half3(0,0,1);
            surface.occlusion = 1;
            half4 result = UniversalFragmentPBR(data, surface);
            Light sun = GetMainLight();
            half backlit = saturate(dot(-normal, sun.direction));
            // Pay for the transmission shadow lookup only on backlit surfaces.
            [branch] if (backlit > .01h && _TransmissionStrength > .001)
            {
                sun = GetMainLight(data.shadowCoord, input.positionWS, data.shadowMask);
            #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(sun.layerMask, GetMeshRenderingLayer()))
            #endif
                {
                    half viewThrough = saturate(dot(-sun.direction, data.viewDirectionWS));
                    half transmission = backlit * (.35h + .65h * viewThrough * viewThrough)
                        * _TransmissionStrength * sun.shadowAttenuation * sun.distanceAttenuation;
                    // Restrained thin-leaf transmission; never emission.
                    result.rgb += surface.albedo * _TransmissionTint.rgb * sun.color * transmission;
                }
            }
            result.rgb = MixFog(result.rgb, input.fog);
            return result;
        }

        half DepthFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            LeafClipClearView(input.positionWS, input.positionCS.xy);
            return input.positionCS.z;
        }

        void DepthNormalsFrag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC,
            out half4 outNormalWS : SV_Target0
        #ifdef _WRITE_RENDERING_LAYERS
            , out uint outRenderingLayers : SV_Target1
        #endif
        )
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            LeafClipClearView(input.positionWS, input.positionCS.xy);
            float3 normal = normalize(input.normalWS);
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

        Varyings LeafShadowVert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            LeafWindVertex(input, output.positionWS, output.normalWS);
        #if _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 lightDirectionWS = normalize(_LightPosition - output.positionWS);
        #else
            float3 lightDirectionWS = _LightDirection;
        #endif
            output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, lightDirectionWS));
            output.positionCS = ApplyShadowClamping(output.positionCS);
            return output;
        }

        half4 LeafShadowFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            return 0;
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
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LeafShadowVert
            #pragma fragment LeafShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
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
