using System;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Creates the shared static studio assets without capturing or changing a scene.</summary>
    public static class CourierLightingAssetBuilder
    {
        private const string Folder = "Assets/RadioArchipelago/Content/Lighting";
        private const string ProfilePath = Folder + "/CourierStudio.asset";
        private const string ReflectionPath = Folder + "/CourierStudio.cubemap";
        private const int CubeSize = 128;
        private const int FilterSamples = 64;
        private const int SpecularLastMip = 6;

        private static readonly Softbox[] Panels =
        {
            new Softbox(new Vector3(-.95f, .97f, 1.52f), .46f, .66f, new Color(1, .975f, .94f) * 1.8f),
            new Softbox(new Vector3(1.24f, .16f, 1.43f), .58f, .45f, new Color(.92f, .97f, 1) * 1.25f),
            new Softbox(new Vector3(.24f, .88f, -1.14f), .40f, .67f, new Color(1, .985f, .95f) * 1.5f)
        };

        public static CourierLightingProfile Ensure(CourierCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            EnsureFolder(Folder);
            bool changed = EnsureRenderingLayer();
            var reflections = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionPath);
            if (reflections == null)
            {
                RequireVacant(ReflectionPath);
                reflections = CreateReflections();
                AssetDatabase.CreateAsset(reflections, ReflectionPath);
                changed = true;
            }

            // An artist-selected profile and all of its existing tuning take precedence.
            var profile = catalog.lighting != null ? catalog.lighting
                : AssetDatabase.LoadAssetAtPath<CourierLightingProfile>(ProfilePath);
            if (profile == null)
            {
                RequireVacant(ProfilePath);
                profile = ScriptableObject.CreateInstance<CourierLightingProfile>();
                profile.name = "CourierStudio";
                profile.keyColor = new Color(1, .96f, .89f);
                profile.fillColor = new Color(.88f, .95f, 1);
                profile.rimColor = new Color(1, .98f, .93f);
                profile.keyPosition = new Vector3(-.95f, 1.52f, 1.52f);
                profile.fillPosition = new Vector3(1.24f, .71f, 1.43f);
                profile.rimPosition = new Vector3(.24f, 1.43f, -1.14f);
                profile.referenceHeight = 2.1f;
                profile.keyIntensity = 32;
                profile.fillIntensity = 18;
                profile.rimIntensity = 24;
                profile.reflectionIntensity = .7f;
                AssetDatabase.CreateAsset(profile, ProfilePath);
                changed = true;
            }
            if (profile.reflections == null)
            {
                profile.reflections = reflections;
                EditorUtility.SetDirty(profile);
                changed = true;
            }
            if (catalog.lighting != profile)
            {
                catalog.lighting = profile;
                EditorUtility.SetDirty(catalog);
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssets();
            return profile;
        }

        private static bool EnsureRenderingLayer()
        {
            uint before = RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue();
            if ((before & CourierLightingRig.RenderingLayer) != 0) return false;
            // URP removes undefined layer bits before uploading light constants to the GPU.
            for (int index = RenderingLayerMask.GetRenderingLayerCount(); index <= 7; index++)
                RenderPipelineEditorUtility.TryAddRenderingLayerName(string.Empty);
            RenderPipelineEditorUtility.TrySetRenderingLayerName(7, "Courier Studio");
            uint after = RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue();
            if ((after & CourierLightingRig.RenderingLayer) == 0)
                throw new InvalidOperationException("Could not register the courier studio rendering layer.");
            Debug.Log("RADIO_COURIER_LIGHTING_LAYER: defined mask 0x" + before.ToString("X") + " -> 0x" + after.ToString("X"));
            return true;
        }

        /// <summary>Explicitly rebakes generated reflections while preserving the asset GUID and references.</summary>
        public static void RebuildReflections()
        {
            EnsureFolder(Folder);
            var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionPath);
            if (existing == null) RequireVacant(ReflectionPath);
            var generated = CreateReflections();
            try
            {
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(generated, ReflectionPath);
                    generated = null;
                }
                else
                {
                    EditorUtility.CopySerialized(generated, existing);
                    existing.Apply(false, false);
                    EditorUtility.SetDirty(existing);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (generated != null) UnityEngine.Object.DestroyImmediate(generated);
            }
        }

        private static Cubemap CreateReflections()
        {
            var cube = new Cubemap(CubeSize, TextureFormat.RGBAHalf, true)
            {
                name = "CourierStudio",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            try
            {
                for (int mip = 0; mip < cube.mipmapCount; mip++)
                {
                    int size = Mathf.Max(1, CubeSize >> mip);
                    // Inverse of URP's lod = 6 * r * (1.7 - .7 * r).
                    // The final physical mip is padding; URP's rough endpoint is mip 6.
                    float level = Mathf.Clamp01(mip / (float)SpecularLastMip);
                    float roughness = Mathf.Clamp01((1.7f - Mathf.Sqrt(2.89f - 2.8f * level)) / 1.4f);
                    var samples = mip == 0 ? null : FilterDirections(roughness);
                    for (int face = 0; face < 6; face++)
                    {
                        var pixels = new Color[size * size];
                        for (int y = 0; y < size; y++)
                            for (int x = 0; x < size; x++)
                            {
                                float u = 2 * (x + .5f) / size - 1;
                                float v = 2 * (y + .5f) / size - 1;
                                var direction = FaceDirection((CubemapFace)face, u, v).normalized;
                                pixels[y * size + x] = samples == null
                                    ? Radiance(direction) : FilteredRadiance(direction, samples);
                            }
                        cube.SetPixels(pixels, (CubemapFace)face, mip);
                    }
                }
                // These mips are already GGX filtered; box filtering would replace them.
                cube.Apply(false, false);
                return cube;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(cube);
                throw;
            }
        }

        private static Color Radiance(Vector3 direction)
        {
            float sky = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.65f, .6f, direction.y));
            var color = Color.Lerp(new Color(.20f, .21f, .23f), new Color(.8f, .84f, .9f) * .35f, sky);
            foreach (var panel in Panels) color += panel.Sample(direction);
            color.a = 1;
            return color;
        }

        private static Color FilteredRadiance(Vector3 normal, Vector3[] samples)
        {
            Basis(normal, out var right, out var up);
            Color sum = Color.clear;
            float weight = 0;
            foreach (var sample in samples)
            {
                if (sample.z <= 0) continue;
                var direction = right * sample.x + up * sample.y + normal * sample.z;
                sum += Radiance(direction) * sample.z;
                weight += sample.z;
            }
            var result = sum / weight;
            result.a = 1;
            return result;
        }

        private static Vector3[] FilterDirections(float roughness)
        {
            var result = new Vector3[FilterSamples];
            float alpha = roughness * roughness;
            for (int i = 0; i < result.Length; i++)
            {
                float phi = 2 * Mathf.PI * (i + .5f) / result.Length;
                float xi = RadicalInverse((uint)i);
                float cosine = Mathf.Sqrt((1 - xi) / (1 + (alpha * alpha - 1) * xi));
                float sine = Mathf.Sqrt(Mathf.Max(0, 1 - cosine * cosine));
                // Reflect V=N about a GGX half-vector, in the local N frame.
                result[i] = new Vector3(2 * Mathf.Cos(phi) * sine * cosine,
                    2 * Mathf.Sin(phi) * sine * cosine, 2 * cosine * cosine - 1);
            }
            return result;
        }

        private static float RadicalInverse(uint bits)
        {
            bits = (bits << 16) | (bits >> 16);
            bits = ((bits & 0x55555555u) << 1) | ((bits & 0xaaaaaaaau) >> 1);
            bits = ((bits & 0x33333333u) << 2) | ((bits & 0xccccccccu) >> 2);
            bits = ((bits & 0x0f0f0f0fu) << 4) | ((bits & 0xf0f0f0f0u) >> 4);
            bits = ((bits & 0x00ff00ffu) << 8) | ((bits & 0xff00ff00u) >> 8);
            return bits * 2.3283064365386963e-10f;
        }

        private static void Basis(Vector3 normal, out Vector3 right, out Vector3 up)
        {
            var axis = Mathf.Abs(normal.y) > .999f ? Vector3.forward : Vector3.up;
            right = Vector3.Cross(axis, normal).normalized;
            up = Vector3.Cross(normal, right);
        }

        private static Vector3 FaceDirection(CubemapFace face, float u, float v)
        {
            switch (face)
            {
                case CubemapFace.PositiveX: return new Vector3(1, -v, -u);
                case CubemapFace.NegativeX: return new Vector3(-1, -v, u);
                case CubemapFace.PositiveY: return new Vector3(u, 1, v);
                case CubemapFace.NegativeY: return new Vector3(u, -1, -v);
                case CubemapFace.PositiveZ: return new Vector3(u, -v, 1);
                case CubemapFace.NegativeZ: return new Vector3(-u, -v, -1);
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void RequireVacant(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Courier lighting asset path has another asset type: " + path);
        }

        private readonly struct Softbox
        {
            private readonly Vector3 normal, right, up;
            private readonly float halfWidth, halfHeight;
            private readonly Color color;

            public Softbox(Vector3 direction, float halfWidth, float halfHeight, Color color)
            {
                normal = direction.normalized;
                Basis(normal, out right, out up);
                this.halfWidth = halfWidth;
                this.halfHeight = halfHeight;
                this.color = color;
            }

            public Color Sample(Vector3 direction)
            {
                float facing = Vector3.Dot(normal, direction);
                if (facing <= 0) return Color.clear;
                float x = Mathf.Abs(Vector3.Dot(right, direction) / facing);
                float y = Mathf.Abs(Vector3.Dot(up, direction) / facing);
                float horizontal = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(halfWidth * .65f, halfWidth, x));
                float vertical = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(halfHeight * .65f, halfHeight, y));
                return color * (horizontal * vertical);
            }
        }
    }
}
