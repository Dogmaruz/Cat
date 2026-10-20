using System;
using System.Linq;
using UnityEngine;

namespace DancingCats.Clone
{
    [CreateAssetMenu(menuName = "Clone/Original asset library")]
    public sealed class OriginalAssets : ScriptableObject
    {
        [Serializable] public sealed class PrefabEntry { public string name; public GameObject asset; }
        public PrefabEntry[] prefabs = Array.Empty<PrefabEntry>();
        public Sprite[] sprites = Array.Empty<Sprite>();
        public Font[] fonts = Array.Empty<Font>();
        public Material[] themeMaterials = Array.Empty<Material>();

        public GameObject Prefab(string name)
        {
            var entry = prefabs.FirstOrDefault(p => p.name == name);
            if (entry == null || entry.asset == null) throw new InvalidOperationException("Original prefab is missing: " + name);
            return entry.asset;
        }

        public Sprite Sprite(string name) => sprites.FirstOrDefault(s => s != null && s.name == name);
        public string[] RoleNames => prefabs.Where(p => p.name.StartsWith("Role", StringComparison.Ordinal) && p.name.Contains("_main"))
            .Select(p => p.name).OrderBy(RoleNumber).ToArray();
        private static int RoleNumber(string name)
        {
            int end = name.IndexOf('_');
            return int.TryParse(name.Substring(4, end - 4), out int number) ? number : int.MaxValue;
        }
    }

    public enum ClonePhase { Menu, Ready, Playing, Paused, Dead, Results }
}
