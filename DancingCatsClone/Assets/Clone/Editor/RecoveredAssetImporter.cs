using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The complete unmodified export and every removed component are kept outside the project.
// AssetRipper's graph placeholders cannot compile. Reconstructed shaders port the recovered
// GLES formulas and retain authored property names; the original ShaderGraph sources are unavailable.
public static class RecoveredAssetImporter
{
    const string Root = "Assets/Recovered";
    const string Metadata = "Assets/Clone/Editor/RecoveryMetadata";
    [Serializable] class ScriptInfo { public string guid, qualified_name; }
    [Serializable] class ScriptList { public ScriptInfo[] items; }
    [Serializable] class MaterialInfo { public string path, shader; public int custom_queue; }
    [Serializable] class MaterialList { public MaterialInfo[] materials; }
    [Serializable] class Entry { public string path, original, replacement, note; }
    [Serializable] class Report { public int materials, prefabs, icons, missingComponents; public List<Entry> entries = new List<Entry>(); }
    sealed class Properties
    {
        public readonly Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        public readonly Dictionary<string, Vector2> scales = new Dictionary<string, Vector2>(), offsets = new Dictionary<string, Vector2>();
        public readonly Dictionary<string, Vector4> vectors = new Dictionary<string, Vector4>();
        public readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        public string[] names = Array.Empty<string>();
        public Texture Texture(params string[] keys) { foreach (var k in keys) if (textures.TryGetValue(k, out var x) && x) return x; return null; }
        public Vector4 Vector(Vector4 fallback, params string[] keys) { foreach (var k in keys) if (vectors.TryGetValue(k, out var x)) return x; return fallback; }
        public float Float(float fallback, params string[] keys) { foreach (var k in keys) if (floats.TryGetValue(k, out var x)) return x; return fallback; }
    }

    [MenuItem("Clone/Repair recovered original assets")]
    public static void Repair()
    {
        var report = new Report();
        try
        {
            if (!AssetDatabase.IsValidFolder(Root)) throw new DirectoryNotFoundException(Root);
            RepairScriptReferences(report);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var surface = Shader.Find("DancingCatsClone/Recovered Surface");
            if (!surface || ShaderUtil.ShaderHasError(surface)) throw new Exception("Recovered Surface shader is missing or has compilation errors.");
            var originals = JsonUtility.FromJson<MaterialList>(File.ReadAllText(Path.Combine(Metadata, "shader-recovery-map.json"))).materials;
            var materialMap = originals.ToDictionary(x => Root + "/" + x.path.Replace('\\', '/'));
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material || !material.shader) continue;
                if (!materialMap.TryGetValue(path, out var originalInfo)) continue;
                string original = originalInfo.shader;
                var p = ReadSavedProperties(material);
                var exact = Shader.Find("DancingCatsClone/Recovered/" + original);
                if (exact)
                {
                    if (ShaderUtil.ShaderHasError(exact)) throw new Exception("Reconstructed shader has compilation errors: " + exact.name);
                    material.shader = exact; material.shaderKeywords = Array.Empty<string>();
                    ApplyProperties(material, p); material.renderQueue = originalInfo.custom_queue;
                    material.SetOverrideTag("RenderType", "");
                }
                else if (material.shader.name.StartsWith("RecoveredOriginal/", StringComparison.Ordinal)) RepairMaterial(material, original, ReadProperties(material), surface);
                else continue;
                EditorUtility.SetDirty(material);
                report.materials++;
                report.entries.Add(new Entry { path = path, original = original, replacement = material.shader.name,
                    note = exact ? "GLES fragment math, source property names, and serialized blend/depth/cull state reconstructed." : "Authored texture/color retained; fallback shader approximation." });
            }
            // Staging removes absent app/SDK behavior documents before Unity import. Inspect the
            // resulting real hierarchy instead of silently deleting any unexpected residue here.
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;
                report.prefabs++;
                foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                    report.missingComponents += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            }
            foreach (string path in Directory.GetFiles("Assets/Resources/OriginalIcons", "*.png"))
            {
                if (!AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'))) throw new Exception("Catalog icon is not imported as a Sprite: " + path);
                report.icons++;
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Metadata, "import-report.json"), JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
            if (report.missingComponents != 0) throw new Exception("Recovered prefabs still contain " + report.missingComponents + " missing scripts; see import-report.json.");
            Debug.Log("RECOVERED_IMPORT PASS: repaired " + report.materials + " materials; " + report.prefabs + " prefabs; " + report.icons + " catalog sprites; no missing scripts.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }
    }

    // Batch wrapper is separate so other editor validation passes can compose Repair().
    public static void Run()
    {
        try { Repair(); if (Application.isBatchMode) EditorApplication.Exit(0); }
        catch { if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
    }

    static Properties ReadSavedProperties(Material material)
    {
        var p = new Properties(); var serialized = new SerializedObject(material);
        var saved = serialized.FindProperty("m_SavedProperties");
        var textures = saved.FindPropertyRelative("m_TexEnvs");
        for (int i = 0; i < textures.arraySize; i++)
        {
            var element = textures.GetArrayElementAtIndex(i); string key = element.FindPropertyRelative("first").stringValue;
            var value = element.FindPropertyRelative("second");
            p.textures[key] = value.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
            p.scales[key] = value.FindPropertyRelative("m_Scale").vector2Value;
            p.offsets[key] = value.FindPropertyRelative("m_Offset").vector2Value;
        }
        var colors = saved.FindPropertyRelative("m_Colors");
        for (int i = 0; i < colors.arraySize; i++)
        {
            var e = colors.GetArrayElementAtIndex(i); p.vectors[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").colorValue;
        }
        var floats = saved.FindPropertyRelative("m_Floats");
        for (int i = 0; i < floats.arraySize; i++)
        {
            var e = floats.GetArrayElementAtIndex(i); p.floats[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").floatValue;
        }
        return p;
    }

    static void ApplyProperties(Material m, Properties p)
    {
        foreach (var kv in p.textures) if (m.HasProperty(kv.Key)) { m.SetTexture(kv.Key, kv.Value); m.SetTextureScale(kv.Key, p.scales[kv.Key]); m.SetTextureOffset(kv.Key, p.offsets[kv.Key]); }
        foreach (var kv in p.vectors) if (m.HasProperty(kv.Key)) m.SetVector(kv.Key, kv.Value);
        foreach (var kv in p.floats) if (m.HasProperty(kv.Key)) m.SetFloat(kv.Key, kv.Value);
    }

    static void RepairScriptReferences(Report report)
    {
        string source = Path.Combine(Metadata, "exported-script-guids.json");
        if (!File.Exists(source)) throw new FileNotFoundException(source);
        var originals = JsonUtility.FromJson<ScriptList>("{\"items\":" + File.ReadAllText(source) + "}").items;
        var installed = new Dictionary<string, string>();
        foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
        {
            var type = script.GetClass();
            if (type == null || type.FullName == null) continue;
            if (!(type.FullName.StartsWith("UnityEngine.") || type.FullName.StartsWith("TMPro."))) continue;
            installed[type.FullName] = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
        }
        var mapping = new Dictionary<string, string>();
        foreach (var s in originals)
            if (!string.IsNullOrEmpty(s.guid) && installed.TryGetValue(s.qualified_name, out string replacement)) mapping[s.guid] = replacement;
        foreach (string path in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(path);
            if (ext != ".prefab" && ext != ".unity" && ext != ".asset") continue;
            string old = File.ReadAllText(path);
            if (!old.StartsWith("%YAML", StringComparison.Ordinal)) continue;
            string derived = Regex.Replace(old, @"m_Script: \{fileID: 11500000, guid: ([a-fA-F0-9]{32}), type: 3\}", m =>
                mapping.TryGetValue(m.Groups[1].Value, out string replacement) ? m.Value.Replace(m.Groups[1].Value, replacement) : m.Value);
            if (derived == old) continue;
            File.WriteAllText(path, derived);
            report.entries.Add(new Entry { path = path, note = "Installed package MonoScript GUID remapped by fully qualified type name." });
        }
    }

    static Properties ReadProperties(Material material)
    {
        var p = new Properties();
        var names = new List<string>();
        for (int i = 0; i < material.shader.GetPropertyCount(); i++)
        {
            string key = material.shader.GetPropertyName(i); names.Add(key);
            switch (material.shader.GetPropertyType(i))
            {
                case ShaderPropertyType.Texture:
                    p.textures[key] = material.GetTexture(key); p.scales[key] = material.GetTextureScale(key); p.offsets[key] = material.GetTextureOffset(key); break;
                case ShaderPropertyType.Color: p.vectors[key] = material.GetColor(key); break;
                case ShaderPropertyType.Vector: p.vectors[key] = material.GetVector(key); break;
                default: p.floats[key] = material.GetFloat(key); break;
            }
        }
        p.names = names.ToArray(); return p;
    }

    static void RepairMaterial(Material m, string family, Properties p, Shader surface)
    {
        // Use only declared properties of the active original shader. Saved materials contain
        // stale values from older shaders which otherwise turn white cat atlases black/pink.
        if (family.StartsWith("UI/") || family.Contains("/UI/"))
        {
            m.shader = Shader.Find("UI/Default");
            var uiTexture = p.Texture("_MainTex", "_BaseMap"); if (uiTexture) m.SetTexture("_MainTex", uiTexture);
            m.SetColor("_Color", p.Vector(Vector4.one, "_Color")); return;
        }
        if (family == "Universal Render Pipeline/Lit" || family == "Universal Render Pipeline/Unlit")
        {
            m.shader = Shader.Find(family);
            foreach (var kv in p.textures) if (m.HasProperty(kv.Key)) { m.SetTexture(kv.Key, kv.Value); m.SetTextureScale(kv.Key, p.scales[kv.Key]); m.SetTextureOffset(kv.Key, p.offsets[kv.Key]); }
            foreach (var kv in p.vectors) if (m.HasProperty(kv.Key)) m.SetVector(kv.Key, kv.Value);
            foreach (var kv in p.floats) if (m.HasProperty(kv.Key)) m.SetFloat(kv.Key, kv.Value);
            return;
        }
        bool role = family.Contains("Role"), tile = family.Contains("JumpTile");
        bool additive = family.Contains("Additive") || family.Contains("TexAdd") || family.Contains("JiGuang") || family.Contains("TrailLizi");
        bool transparent = additive || family.Contains("Particle") || family.Contains("Trail") || family.Contains("Transparent") || family.Contains("Alpha") || family.Contains("Ecolor_A");
        bool cutout = family.Contains("AlphaClip");
        string[] textureKeys = { "_MainTex", "MainTex", "_BaseMap", "Texture2D_56cdf3387e9d4491920e2b081f7b5281", "_Texture", "Texture2D_9b89912d9a594b22a074866cf8c5445b" };
        string textureName = textureKeys.FirstOrDefault(k => p.textures.TryGetValue(k, out var x) && x && !k.StartsWith("unity_"));
        if (textureName == null) textureName = p.textures.Keys.FirstOrDefault(k => !k.StartsWith("unity_") && !k.ToLowerInvariant().Contains("emission") && p.textures[k]);
        Texture texture = textureName == null ? null : p.textures[textureName];
        Vector4 color = p.Vector(Vector4.one,
            "Color_aaeea0d74e8d4ccd8a9ca042a3c40b21", "MainColor", "Color_43d50ce605284bdfb64dcbea2b745524", "_BaseColor", "_Color", "Color", "TintColor");
        // Color properties emitted by ShaderGraph are Vector4, and opaque graph alpha is often zero.
        if (!transparent || cutout) color.w = 1;
        else color.w = p.Float(color.w == 0 ? 1 : color.w, "Alpha", "_Alpha");
        m.shader = surface; m.shaderKeywords = Array.Empty<string>();
        m.SetTexture("_MainTex", texture ? texture : Texture2D.whiteTexture); m.SetColor("_Color", color);
        if (textureName != null) { m.SetTextureScale("_MainTex", p.scales[textureName]); m.SetTextureOffset("_MainTex", p.offsets[textureName]); }
        if (p.vectors.TryGetValue("_TillingOffset", out var tiling)) { m.SetTextureScale("_MainTex", new Vector2(tiling.x, tiling.y)); m.SetTextureOffset("_MainTex", new Vector2(tiling.z, tiling.w)); }
        m.SetTexture("_EmissionTex", p.Texture("EmissionTex", "_EmissionMap") ?? Texture2D.blackTexture);
        m.SetColor("_EmissionColor", p.Vector(Vector4.zero, "EmissionColor", "_EmissionColor"));
        m.SetFloat("_EmissionStrength", p.Texture("EmissionTex", "_EmissionMap") ? p.Float(1, "E", "_EmissionStrength") : 0);
        m.SetFloat("_Shading", role || tile ? 1 : 0); m.SetVector("_LightDir", p.Vector(new Vector4(0,1,.77f,0), "_LightDir"));
        m.SetColor("_FresnelColor", p.Vector(Vector4.zero, "FreColor")); m.SetFloat("_FresnelPower", p.Float(1, "FrePower")); m.SetFloat("_FresnelStrength", p.Float(0, "Fresnel"));
        m.SetFloat("_SrcBlend", (float)(transparent && !cutout ? BlendMode.SrcAlpha : BlendMode.One));
        m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : transparent && !cutout ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
        m.SetFloat("_ZWrite", transparent && !cutout ? 0 : 1); m.SetFloat("_Cull", transparent || family.Contains("2Side") ? 0 : 2); m.SetFloat("_Cutoff", cutout ? .5f : 0);
        m.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : transparent ? "Transparent" : "Opaque");
        m.renderQueue = cutout ? (int)RenderQueue.AlphaTest : transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
    }
}
