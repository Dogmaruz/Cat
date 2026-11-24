Shader "RadioArchipelago/Moon Resonance Sky"
{
    Properties
    {
        _ZenithColor ("Airy blue zenith", Color) = (.34,.64,.84,1)
        _HorizonColor ("Pale cyan horizon", Color) = (.76,.91,.93,1)
        _AbyssColor ("Misty lower sky", Color) = (.63,.83,.90,1)
        _CloudColor ("Distant cloud tint", Color) = (.94,.98,.98,1)
        _HazeStrength ("Horizon haze", Range(0,1)) = .35
        _HazeHeight ("Haze angular height", Range(.05,1)) = .38
        _CloudStrength ("Soft cloud wisps", Range(0,1)) = .18
        _CloudScale ("Cloud angular scale", Range(1,20)) = 7.5
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
        }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend Off

        Pass
        {
            Name "ArchipelagoSky"
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _AbyssColor, _CloudColor;
                float _HazeStrength, _HazeHeight, _CloudStrength, _CloudScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Skybox orientation follows its world rotation, never camera
                // translation. Direction-space clouds need no horizon UV seam.
                output.directionWS = TransformObjectToWorldDir(input.positionOS.xyz, false);
                return output;
            }

            float ArchipelagoHash(float2 sampleCoord)
            {
                // Arithmetic hash avoids textures and trigonometric noise.
                sampleCoord = frac(sampleCoord * float2(.1031, .1030));
                sampleCoord += dot(sampleCoord, sampleCoord.yx + 33.33);
                return frac((sampleCoord.x + sampleCoord.y) * sampleCoord.x);
            }

            float SoftNoise(float2 sampleCoord)
            {
                float2 cell = floor(sampleCoord);
                float2 blend = frac(sampleCoord);
                blend = blend * blend * (3 - 2 * blend);
                return lerp(lerp(ArchipelagoHash(cell), ArchipelagoHash(cell + float2(1,0)), blend.x),
                    lerp(ArchipelagoHash(cell + float2(0,1)), ArchipelagoHash(cell + float2(1,1)), blend.x), blend.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.directionWS);
                float up = saturate(direction.y);
                float down = saturate(-direction.y);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, smoothstep(0, 1, up));
                sky = lerp(sky, _AbyssColor.rgb, smoothstep(0, 1, down));
                float haze = 1 - smoothstep(0, max(_HazeHeight, .001), abs(direction.y));
                sky = lerp(sky, _HorizonColor.rgb, haze * _HazeStrength);

                // Two soft noise layers make restrained, horizontally stretched
                // wisps. They fade toward both poles, including the blue abyss.
                float2 cloudUV = float2(direction.x * .8 + direction.z * .6,
                    direction.y * 2.6 + direction.z * .28) * _CloudScale;
                float clouds = SoftNoise(cloudUV) * .72
                    + SoftNoise(cloudUV * 2.03 + float2(17.7, 3.1)) * .28;
                float cloudBand = 1 - smoothstep(.25, .92, abs(direction.y));
                float wisps = smoothstep(.52, .84, clouds) * cloudBand * _CloudStrength;
                                half3 color = lerp(sky, _CloudColor.rgb, wisps);
                float3 moonAxis = normalize(float3(-.25, .57, -.78));
                float alignment = dot(direction, moonAxis);
                float disc = smoothstep(.9922, .99255, alignment);
                float halo = smoothstep(.980, .9922, alignment) * (1 - disc);
                float crater = SoftNoise(float2(direction.x * 96, direction.y * 96));
                half3 pearl = lerp(half3(.84,.80,.92), half3(1,.95,.90), crater);
                color += halo * half3(.028,.022,.037);
                color = lerp(color, pearl, disc * .82);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
