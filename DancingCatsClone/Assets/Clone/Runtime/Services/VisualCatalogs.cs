using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RhythmGame.Services
{
    /// <summary>Owns parsed rig bindings for one application scope.</summary>
    public sealed class RoleMotionCatalog
    {
        [Serializable] private sealed class Manifest { public RoleBinding[] roles; }
        [Serializable] internal sealed class RoleBinding
        {
            public string prefab;
            public ManagerBinding[] managers;
            public BoneBinding[] bones;
            public ColliderBinding[] colliders;
        }
        [Serializable] internal sealed class ManagerBinding
        {
            public string path;
            public float dynamicRatio;
            public string[] bonePathsInUpdateOrder;
        }
        [Serializable] internal sealed class BoneBinding
        {
            public string path, childPath;
            public Vector3 boneAxis, springForce;
            public float radius, stiffnessForce, dragForce, threshold;
            public string[] colliderPaths;
        }
        [Serializable] internal sealed class ColliderBinding { public string path; public float radius; }

        private readonly IGameResources resources;
        private Dictionary<string, RoleBinding> bindings;

        public RoleMotionCatalog(IGameResources resources)
        {
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        internal RoleBinding GetBinding(string sourcePrefabName)
        {
            if (bindings == null)
            {
                var text = resources.Load<TextAsset>("OriginalData/role-spring-bindings");
                if (text == null) throw new FileNotFoundException("Original spring binding manifest is missing.");
                var manifest = JsonUtility.FromJson<Manifest>(text.text);
                if (manifest?.roles == null) throw new InvalidDataException("Original spring binding manifest has no roles.");
                var parsed = new Dictionary<string, RoleBinding>(manifest.roles.Length, StringComparer.Ordinal);
                foreach (var role in manifest.roles) parsed.Add(role.prefab, role);
                bindings = parsed;
            }

            string name = Path.GetFileName(sourcePrefabName ?? string.Empty).Replace("(Clone)", string.Empty).Trim();
            if (!name.EndsWith(".prefab", StringComparison.Ordinal)) name += ".prefab";
            if (bindings.TryGetValue(name, out var binding)) return binding;
            throw new InvalidDataException("Original role has no spring binding record: " + sourcePrefabName);
        }
    }

    /// <summary>Owns indexed palette manifests for one application scope.</summary>
    public sealed class ThemeCatalog
    {
        [Serializable] private sealed class Manifest
        {
            public string[] materials;
            public EnvironmentBinding[] environments;
            public PrefabBinding[] prefabs;
        }
        [Serializable] internal sealed class EnvironmentBinding
        {
            public int environmentIndex;
            public string[] allThemes, allThemesTarget, allSpeedLine, allPerfect, goEndTheme, allQuad;
            public Color[] themeColor;
        }
        [Serializable] internal sealed class PrefabBinding { public string prefab; public RendererBinding[] bindings; }
        [Serializable] internal sealed class RendererBinding { public string rendererPath; public int[] materials; }

        private readonly IGameResources resources;
        private Dictionary<int, EnvironmentBinding> environments;
        private Dictionary<string, PrefabBinding> prefabs;
        private int materialCount;

        public ThemeCatalog(IGameResources resources)
        {
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public int MaterialCount
        {
            get { EnsureLoaded(); return materialCount; }
        }

        internal EnvironmentBinding GetEnvironment(int environmentIndex)
        {
            EnsureLoaded();
            if (environments.TryGetValue(environmentIndex, out var binding)) return binding;
            throw new ArgumentOutOfRangeException(nameof(environmentIndex));
        }

        internal PrefabBinding FindPrefab(string prefabName)
        {
            EnsureLoaded();
            return prefabs.TryGetValue(prefabName, out var binding) ? binding : null;
        }

        private void EnsureLoaded()
        {
            if (environments != null) return;
            var asset = resources.Load<TextAsset>("OriginalData/theme-visual-bindings");
            if (asset == null) throw new InvalidOperationException("Original theme pointer manifest is missing.");
            var manifest = JsonUtility.FromJson<Manifest>(asset.text);
            if (manifest?.materials == null || manifest.environments == null || manifest.prefabs == null)
                throw new InvalidDataException("Original theme pointer manifest is incomplete.");
            var parsedEnvironments = new Dictionary<int, EnvironmentBinding>(manifest.environments.Length);
            foreach (var binding in manifest.environments) parsedEnvironments.Add(binding.environmentIndex, binding);
            var parsedPrefabs = new Dictionary<string, PrefabBinding>(manifest.prefabs.Length, StringComparer.Ordinal);
            foreach (var binding in manifest.prefabs) parsedPrefabs.Add(binding.prefab, binding);
            materialCount = manifest.materials.Length;
            prefabs = parsedPrefabs;
            environments = parsedEnvironments;
        }
    }
}
