using System;
using System.Collections.Generic;
using RhythmGame.Services;
using UnityEngine;

namespace DancingCats.Clone
{
    /// <summary>Rebinds original ThemeController and ThemeChangeMaterial pointers removed on import.</summary>
    public sealed class OriginalThemeVisuals : MonoBehaviour
    {
        private sealed class TileBinding
        {
            internal OriginalTile tile, eligibilityTile;
            internal Renderer[] renderers;
            internal ThemeCatalog.RendererBinding[] bindings;
        }

        private readonly List<TileBinding> _tiles = new List<TileBinding>();
        private Material[] _materials;
        private ThemeCatalog.EnvironmentBinding _binding;
        private GameObject[] _themes, _targets, _perfect, _endThemes;
        private Animator[] _quads;
        public int Index { get; private set; }
        public Color CurrentColor => _binding.themeColor[Index];

        public void Bind(GameObject environment, IEnumerable<OriginalTile> allTiles,
            Material[] materialLibrary, int environmentIndex, ThemeCatalog catalog)
        {
            if (environment == null) throw new ArgumentNullException(nameof(environment));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            _binding = catalog.GetEnvironment(environmentIndex);
            if (materialLibrary == null || materialLibrary.Length != catalog.MaterialCount)
                throw new ArgumentException("Original palette material library does not match the pointer manifest.", nameof(materialLibrary));
            _materials = materialLibrary;
            _themes = ResolveObjects(environment.transform, _binding.allThemes);
            _targets = ResolveObjects(environment.transform, _binding.allThemesTarget);
            _perfect = ResolveObjects(environment.transform, _binding.allPerfect);
            _endThemes = ResolveObjects(environment.transform, _binding.goEndTheme);
            var quadObjects = ResolveObjects(environment.transform, _binding.allQuad);
            _quads = new Animator[quadObjects.Length];
            for (int i = 0; i < quadObjects.Length; i++)
            {
                _quads[i] = quadObjects[i].GetComponent<Animator>();
                if (_quads[i] == null) throw new InvalidOperationException("Original theme Quad Animator is missing.");
            }
            string suffix = environmentIndex == 1 ? "" : "_" + (environmentIndex - 2);
            _tiles.Clear();
            foreach (var tile in allTiles)
            {
                if (tile == null || tile.element == null || tile.element.id == 8) continue;
                string stem = PrefabStem(tile.element.id);
                if (stem == null) continue;
                // MidShot decorations use scene-specific source lists and runtime names retain
                // the chosen original prefab name, including variants without palette bindings.
                string sourceName = tile.element.id == 7 ? tile.gameObject.name : stem + suffix;
                var source = catalog.FindPrefab(sourceName);
                if (source == null) continue; // FollowMid/FollowEnd have no original material-change component.
                var item = new TileBinding { tile = tile, eligibilityTile = tile, bindings = source.bindings,
                    renderers = new Renderer[source.bindings.Length] };
                if (tile.element.id == 9)
                {
                    var parent = tile.transform.parent;
                    while (parent != null)
                    {
                        var hold = parent.GetComponent<OriginalTile>();
                        if (hold != null && hold.element != null && (hold.element.id == 2 || hold.element.id == 3))
                        { item.eligibilityTile = hold; break; }
                        parent = parent.parent;
                    }
                }
                for (int i = 0; i < source.bindings.Length; i++)
                {
                    var target = Resolve(tile.transform, source.bindings[i].rendererPath);
                    item.renderers[i] = target.GetComponent<Renderer>();
                    if (item.renderers[i] == null) throw new InvalidOperationException("Original palette Renderer is missing: " + source.prefab + "/" + source.bindings[i].rendererPath);
                }
                _tiles.Add(item);
            }
            Index = 0;
            SelectScenery();
            SetAll(_endThemes, false);
            foreach (var item in _tiles) ApplyMaterials(item);
            SetQuad("Normal", false);
        }

        public void Advance(float playerZ) => SetIndex(Index + 1, playerZ);

        public void SetIndex(int index, float playerZ)
        {
            RequireBound();
            Index = (index % _themes.Length + _themes.Length) % _themes.Length;
            SelectScenery();
            foreach (var item in _tiles)
            {
                if (item.tile == null || item.eligibilityTile == null) continue;
                float margin = item.tile.element.id == 7 ? 50f : 5f;
                if (item.eligibilityTile.transform.position.z > playerZ - margin) ApplyMaterials(item);
            }
        }

        public void ShowHit(bool perfect, int streak)
        {
            RequireBound();
            if (!perfect)
            {
                _perfect[Index].SetActive(false);
                SetQuad("Normal", false);
            }
            else if (streak > 4)
            {
                _perfect[Index].SetActive(true);
                SetQuad("Perfect", false);
            }
            else _perfect[Index].SetActive(false);
        }

        public void Finish()
        {
            RequireBound();
            SetAll(_endThemes, true);
            SetAll(_themes, false);
            SetAll(_targets, false);
        }

        private void SelectScenery()
        {
            for (int i = 0; i < _themes.Length; i++) _themes[i].SetActive(i == Index);
            for (int i = 0; i < _targets.Length; i++) _targets[i].SetActive(i == Index);
            SetAll(_perfect, false);
        }

        private void ApplyMaterials(TileBinding tile)
        {
            for (int i = 0; i < tile.renderers.Length; i++)
            {
                int[] palette = tile.bindings[i].materials;
                if (palette == null || Index >= palette.Length) throw new InvalidOperationException("Original palette index is missing.");
                int materialIndex = palette[Index];
                if (materialIndex < 0 || materialIndex >= _materials.Length || _materials[materialIndex] == null)
                    throw new InvalidOperationException("Original palette material pointer is unresolved.");
                tile.renderers[i].sharedMaterial = _materials[materialIndex];
            }
        }
        private void SetQuad(string state, bool restart)
        {
            var animator = _quads[Index];
            if (!restart && animator.GetCurrentAnimatorStateInfo(0).IsName(state)) return;
            if (animator.HasState(0, Animator.StringToHash(state))) animator.Play(state, 0, 0f);
        }
        private static string PrefabStem(int id)
        {
            switch (id)
            {
                case 0: return "Jump1";
                case 1: return "Jump2";
                case 2: case 3: return "Hold";
                case 4: return "Follow";
                case 7: return "MidShotItem";
                case 9: return "Hold_Light";
                default: return null;
            }
        }
        private static GameObject[] ResolveObjects(Transform root, string[] paths)
        {
            var values = new GameObject[paths.Length];
            for (int i = 0; i < paths.Length; i++) values[i] = Resolve(root, paths[i]).gameObject;
            return values;
        }
        private static Transform Resolve(Transform root, string path)
        {
            var target = string.IsNullOrEmpty(path) ? root : root.Find(path);
            if (target == null) throw new InvalidOperationException("Original theme hierarchy is missing: " + path);
            return target;
        }
        private static void SetAll(GameObject[] values, bool active)
        { foreach (var item in values) item.SetActive(active); }
        private void RequireBound()
        { if (_binding == null) throw new InvalidOperationException("Bind original theme pointers before use."); }
    }
}
