using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Authors original mesh assets. It never opens or modifies the user's scene.</summary>
    public static class CourierAssetBuilder
    {
        private const string Root = "Assets/RadioArchipelago/Content";
        private const string MeshFolder = Root + "/Meshes";
        private const string MaterialFolder = Root + "/Materials";
        private const string PrefabFolder = Root + "/Couriers";
        private const string IconFolder = Root + "/CourierIcons";
        private const int ThumbnailLayer = 31;

        [MenuItem("Radio Archipelago/Build courier assets")]
        public static void Run()
        {
            EnsureFolder(Root); EnsureFolder(MeshFolder); EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder); EnsureFolder(IconFolder);
            var palette = new Palette();
            var entries = new[]
            {
                BuildCourier("signal", "Тико", 0, palette),
                BuildCourier("sail", "Люмен", 1, palette),
                BuildCourier("orbit", "Нори", 2, palette)
            };
            string path = Root + "/CourierCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(path);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<CourierCatalog>(); AssetDatabase.CreateAsset(catalog, path); }
            catalog.entries = entries;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("RADIO_COURIERS_BUILT count=3 catalog=" + path);
        }

        [MenuItem("Radio Archipelago/Validate couriers")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(Root + "/CourierCatalog.asset");
            if (catalog == null || catalog.entries == null || catalog.entries.Length != 3)
                throw new InvalidDataException("Radio Archipelago needs all three courier definitions.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var silhouettes = new List<Vector3>();
            foreach (var entry in catalog.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id)
                    || entry.prefab == null || entry.icon == null || string.IsNullOrWhiteSpace(entry.displayName))
                    throw new InvalidDataException("Courier definition is incomplete or duplicated.");
                if (entry.prefab.GetComponent<CourierMotion>() == null)
                    throw new InvalidDataException("Courier has no local presentation rig: " + entry.id);
                if (entry.prefab.GetComponentsInChildren<Collider>(true).Length != 0
                    || entry.prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidDataException("Presentation prefabs must not own gameplay physics: " + entry.id);
                int triangles = 0;
                var renderers = entry.prefab.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0) throw new InvalidDataException("Courier has no actual 3D geometry: " + entry.id);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    foreach (var material in renderer.sharedMaterials)
                        if (material == null || !AssetDatabase.GetAssetPath(material).StartsWith(Root, StringComparison.Ordinal))
                            throw new InvalidDataException("Courier material is missing or outside its original catalog: " + entry.id);
                }
                foreach (var filter in entry.prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) throw new InvalidDataException("Courier mesh is missing: " + entry.id);
                    triangles += filter.sharedMesh.triangles.Length / 3;
                    foreach (var vertex in filter.sharedMesh.vertices)
                        if (!IsFinite(vertex.x) || !IsFinite(vertex.y) || !IsFinite(vertex.z))
                            throw new InvalidDataException("Non-finite courier vertex: " + entry.id);
                }
                if (triangles > 15000 || bounds.size.y < 1.2f || bounds.size.y > 3.5f)
                    throw new InvalidDataException("Courier triangle budget or scale is invalid: " + entry.id + " triangles=" + triangles);
                foreach (string dependency in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(entry.prefab), true))
                    if (dependency.StartsWith("Assets/Recovered/", StringComparison.Ordinal)
                        || dependency.StartsWith("Assets/Clone/", StringComparison.Ordinal)
                        || dependency.StartsWith("Assets/Resources/", StringComparison.Ordinal))
                        throw new InvalidDataException("Courier depends on legacy game content: " + dependency);
                silhouettes.Add(bounds.size);
                ValidateMotion(entry);
                Debug.Log("RADIO_COURIER_VALID id=" + entry.id + " triangles=" + triangles + " renderers=" + renderers.Length + " bounds=" + bounds.size);
            }
            for (int i = 0; i < silhouettes.Count; i++)
                for (int j = i + 1; j < silhouettes.Count; j++)
                    if ((silhouettes[i] - silhouettes[j]).sqrMagnitude < .015f)
                        throw new InvalidDataException("Couriers need visibly distinct proportions.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateMotion(CourierDefinition entry)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var actor = UnityEngine.Object.Instantiate(entry.prefab);
            SceneManager.MoveGameObjectToScene(actor, scene);
            try
            {
                var position = new Vector3(11, 3, -2); var rotation = Quaternion.Euler(0, 17, 0); var scale = Vector3.one * .7f;
                actor.transform.SetPositionAndRotation(position, rotation); actor.transform.localScale = scale;
                var motion = actor.GetComponent<CourierMotion>(); motion.ManualSimulation = true; motion.ResetMotion();
                motion.PlayIdle(); motion.SimulateSprings(.1f);
                motion.PlayJump(); for (int i = 0; i < 5; i++) motion.SimulateSprings(.1f);
                motion.PlayHold(true); for (int i = 0; i < 6; i++) motion.SimulateSprings(.1f);
                motion.PlayHold(false); motion.SetFacing(25); motion.SimulateSprings(.1f);
                motion.PlayEnd(); for (int i = 0; i < 7; i++) motion.SimulateSprings(.1f);
                motion.ResetMotion();
                if ((actor.transform.position - position).sqrMagnitude > 1e-8f || Quaternion.Angle(actor.transform.rotation, rotation) > .001f
                    || (actor.transform.localScale - scale).sqrMagnitude > 1e-8f)
                    throw new InvalidDataException("Courier animation changed gameplay root: " + entry.id);
                var visual = actor.transform.Find("VisualPivot");
                if (visual.localPosition.sqrMagnitude > 1e-8f || Quaternion.Angle(visual.localRotation, Quaternion.identity) > .001f
                    || (visual.localScale - Vector3.one).sqrMagnitude > 1e-8f)
                    throw new InvalidDataException("Courier animation did not reset for reuse: " + entry.id);
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static CourierDefinition BuildCourier(string id, string displayName, int kind, Palette p)
        {
            var authoringScene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Courier_" + id);
            SceneManager.MoveGameObjectToScene(root, authoringScene);
            try
            {
                Transform visual = Pivot(root.transform, "VisualPivot", Vector3.zero);
                Transform antenna, scarf, armLeft, armRight;
                if (kind == 0) MakeSignal(visual, p, out antenna, out scarf, out armLeft, out armRight);
                else if (kind == 1) MakeSail(visual, p, out antenna, out scarf, out armLeft, out armRight);
                else MakeOrbit(visual, p, out antenna, out scarf, out armLeft, out armRight);
                var animatedParts = new[] { antenna, scarf, armLeft, armRight };
                CombineGroup(visual, id + "_Body", animatedParts);
                for (int i = 0; i < animatedParts.Length; i++) CombineGroup(animatedParts[i], id + "_Part" + i, Array.Empty<Transform>());
                var motion = root.AddComponent<CourierMotion>();
                motion.Configure(visual, antenna, scarf, armLeft, armRight);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/Courier_" + id + ".prefab");
                var icon = RenderThumbnail(root, id);
                return new CourierDefinition { id = id, displayName = displayName, prefab = prefab, icon = icon };
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(authoringScene); }
        }

        private static void MakeSignal(Transform root, Palette p, out Transform antenna, out Transform scarf, out Transform left, out Transform right)
        {
            var profile = new[]
            {
                new Vector3(0, .21f, .01f), new Vector3(0, .30f, .30f), new Vector3(0, .55f, .49f),
                new Vector3(.01f, .90f, .55f), new Vector3(-.02f, 1.20f, .50f), new Vector3(-.10f, 1.43f, .35f),
                new Vector3(-.17f, 1.55f, .16f), new Vector3(-.21f, 1.58f, .002f)
            };
            Surface(root, "SignalBody", Lathe("SignalBody", profile, .78f, 32, 32), p.Cream);
            Surface(root, "SignalCrest", Lathe("SignalCrest", new[] { new Vector3(-.26f, 1.38f, .13f), new Vector3(-.43f, 1.46f, .12f), new Vector3(-.57f, 1.56f, .07f), new Vector3(-.60f, 1.66f, .002f) }, .7f, 20, 16), p.Cream);
            Eyes(root, "Signal", new Vector3(0, 1.04f, .405f), .19f, .082f, p.Navy);
            left = Arm(root, "SignalArmLeft", new Vector3(-.48f, .65f, .04f), -28, p.Cream);
            right = Arm(root, "SignalArmRight", new Vector3(.48f, .65f, .04f), 28, p.Cream);
            Egg(root, "SignalFootLeft", new Vector3(-.22f, .15f, .02f), new Vector3(.13f, .17f, .19f), p.Cream, new Vector3(0, 0, -12));
            Egg(root, "SignalFootRight", new Vector3(.22f, .15f, .02f), new Vector3(.13f, .17f, .19f), p.Cream, new Vector3(0, 0, 12));
            MakeBackpack(root, "Signal", new Vector3(.14f, .86f, -.39f), new Vector3(.76f, .76f, .40f), p.Mint, p);
            Tube(root, "SignalCollar", Ellipse(new Vector3(0, .76f, 0), .54f, .43f, 36, true), .093f, p.Coral, 12);
            Egg(root, "SignalScarfKnot", new Vector3(.49f, .77f, .18f), new Vector3(.13f, .13f, .11f), p.Coral);
            scarf = Pivot(root, "ScarfPivot", new Vector3(.42f, .79f, -.1f));
            Surface(scarf, "SignalScarfRibbon", Ribbon("SignalScarfRibbon", new[] { Vector3.zero, new Vector3(.22f, .02f, -.18f), new Vector3(.5f, .08f, -.30f), new Vector3(.72f, .03f, -.42f) }, .21f), p.Coral);
            antenna = Pivot(root, "AntennaPivot", new Vector3(-.24f, 1.21f, -.46f));
            Egg(antenna, "SignalAntennaSocket", Vector3.zero, new Vector3(.12f, .11f, .12f), p.Brass);
            var points = Bezier(new Vector3(0, .08f, 0), new Vector3(-.96f, .62f, -.03f), new Vector3(-.40f, 1.43f, .03f), new Vector3(.65f, .93f, .04f), 30);
            Tube(antenna, "SignalAntennaArch", points, .057f, p.MintDark, 10);
            Egg(antenna, "SignalAntennaTipShell", points[points.Length - 1], Vector3.one * .165f, p.Mint);
            Egg(antenna, "SignalAntennaTip", points[points.Length - 1] + Vector3.forward * .07f, Vector3.one * .135f, p.Glow);
            Egg(antenna, "SignalTipCollar", points[points.Length - 1] - new Vector3(.06f, 0, 0), Vector3.one * .075f, p.Brass);
        }

        private static void MakeSail(Transform root, Palette p, out Transform antenna, out Transform scarf, out Transform left, out Transform right)
        {
            Surface(root, "SailBody", Lathe("SailBody", new[]
            {
                new Vector3(0, .13f, .01f), new Vector3(0, .27f, .26f), new Vector3(0, .62f, .35f),
                new Vector3(0, 1.02f, .34f), new Vector3(-.02f, 1.42f, .27f), new Vector3(-.10f, 1.64f, .13f), new Vector3(-.13f, 1.68f, .005f)
            }, .83f, 32, 30), p.Amber);
            Eyes(root, "Sail", new Vector3(0, 1.15f, .258f), .115f, .068f, p.Navy);
            left = Arm(root, "SailArmLeft", new Vector3(-.30f, .71f, 0), -15, p.Navy);
            right = Arm(root, "SailArmRight", new Vector3(.30f, .71f, 0), 15, p.Navy);
            Egg(root, "SailFootLeft", new Vector3(-.14f, .09f, .02f), new Vector3(.075f, .12f, .13f), p.Navy);
            Egg(root, "SailFootRight", new Vector3(.14f, .09f, .02f), new Vector3(.075f, .12f, .13f), p.Navy);
            MakeBackpack(root, "Sail", new Vector3(.10f, .87f, -.29f), new Vector3(.54f, .62f, .32f), p.Brass, p);
            Tube(root, "SailCollar", Ellipse(new Vector3(0, .82f, 0), .35f, .30f, 28, true), .060f, p.Coral, 10);
            scarf = Pivot(root, "ScarfPivot", new Vector3(-.27f, .86f, -.09f));
            Surface(scarf, "SailScarfRibbon", Ribbon("SailScarfRibbon", new[] { Vector3.zero, new Vector3(-.17f, .02f, -.13f), new Vector3(-.38f, .08f, -.22f) }, .10f), p.Coral);
            antenna = Pivot(root, "AntennaPivot", new Vector3(-.11f, 1.26f, -.31f));
            var a = new Vector3(-.32f, .05f, 0); var b = new Vector3(.22f, 1.26f, 0); var c = new Vector3(.59f, .40f, .03f);
            Tube(antenna, "SailAntennaBow", Bezier(a, new Vector3(-.37f, .65f, 0), new Vector3(-.03f, 1.16f, 0), b, 22), .028f, p.Brass, 8);
            Tube(antenna, "SailAntennaMast", new[] { b, c, a }, .021f, p.Brass, 8);
            Surface(antenna, "SailCanvas", SailMesh("SailCanvas", a + new Vector3(.025f, .03f, -.01f), b + new Vector3(-.025f, -.04f, -.01f), c + new Vector3(-.035f, 0, -.01f)), p.Sail);
            Egg(antenna, "SailAntennaTop", b, Vector3.one * .06f, p.Brass);
            Egg(antenna, "SailAntennaLight", c, Vector3.one * .08f, p.GlowAmber);
            Egg(antenna, "SailAntennaBase", a, Vector3.one * .065f, p.Brass);
        }

        private static void MakeOrbit(Transform root, Palette p, out Transform antenna, out Transform scarf, out Transform left, out Transform right)
        {
            Egg(root, "OrbitBody", new Vector3(0, .67f, 0), new Vector3(.64f, .57f, .48f), p.Navy);
            Egg(root, "OrbitFacePatch", new Vector3(0, .83f, .49f), new Vector3(.39f, .33f, .075f), p.Cream);
            Eyes(root, "Orbit", new Vector3(0, .89f, .565f), .145f, .07f, p.Navy);
            left = Arm(root, "OrbitArmLeft", new Vector3(-.56f, .56f, .06f), -22, p.Navy);
            right = Arm(root, "OrbitArmRight", new Vector3(.56f, .56f, .06f), 22, p.Navy);
            Egg(root, "OrbitFootLeft", new Vector3(-.28f, .09f, .015f), new Vector3(.12f, .13f, .17f), p.Navy);
            Egg(root, "OrbitFootRight", new Vector3(.28f, .09f, .015f), new Vector3(.12f, .13f, .17f), p.Navy);
            MakeBackpack(root, "Orbit", new Vector3(.18f, .76f, -.43f), new Vector3(.78f, .62f, .36f), p.Mint, p);
            scarf = Pivot(root, "ScarfPivot", new Vector3(-.48f, .73f, -.20f));
            Surface(scarf, "OrbitScarfRibbon", Ribbon("OrbitScarfRibbon", new[] { Vector3.zero, new Vector3(-.18f, .06f, -.08f), new Vector3(-.45f, .04f, -.20f), new Vector3(-.65f, .12f, -.29f) }, .12f), p.Blue);
            antenna = Pivot(root, "AntennaPivot", new Vector3(-.21f, 1.0f, -.43f));
            var ring = new Vector3[35];
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = Mathf.Lerp(210, 20, i / (float)(ring.Length - 1)) * Mathf.Deg2Rad;
                ring[i] = new Vector3(Mathf.Cos(angle) * .56f + .06f, Mathf.Sin(angle) * .56f + .58f, 0);
            }
            Tube(antenna, "OrbitAntennaRing", ring, .054f, p.Blue, 10);
            Tube(antenna, "OrbitAntennaSupport", new[] { Vector3.zero, ring[0] }, .048f, p.Brass, 8);
            Egg(antenna, "OrbitAntennaBeaconShell", ring[ring.Length - 1], Vector3.one * .15f, p.Mint);
            Egg(antenna, "OrbitAntennaBeacon", ring[ring.Length - 1] + Vector3.forward * .065f, Vector3.one * .12f, p.Glow);
            Egg(antenna, "OrbitAntennaSocket", Vector3.zero, Vector3.one * .09f, p.Brass);
        }

        private static Transform Arm(Transform root, string name, Vector3 position, float degrees, Material material)
        {
            var pivot = Pivot(root, name + "Pivot", position);
            Egg(pivot, name, new Vector3(0, -.11f, .01f), new Vector3(.115f, .23f, .13f), material, new Vector3(0, 0, degrees));
            return pivot;
        }

        private static void Eyes(Transform root, string name, Vector3 center, float spread, float size, Material material)
        {
            Egg(root, name + "EyeLeft", center + new Vector3(-spread, 0, 0), new Vector3(size * .56f, size, .026f), material);
            Egg(root, name + "EyeRight", center + new Vector3(spread, 0, 0), new Vector3(size * .56f, size, .026f), material);
        }

        private static void MakeBackpack(Transform root, string name, Vector3 position, Vector3 size, Material color, Palette p)
        {
            var pack = Pivot(root, name + "RadioPack", position);
            Surface(pack, name + "RadioCase", RoundedBox(name + "RadioCase", size, .09f), color);
            Egg(pack, name + "SpeakerRim", new Vector3(0, .02f, -size.z * .54f), new Vector3(size.x * .35f, size.x * .35f, .045f), p.Brass);
            Egg(pack, name + "SpeakerFace", new Vector3(0, .02f, -size.z * .66f), new Vector3(size.x * .27f, size.x * .27f, .026f), p.Navy);
            for (int i = -2; i <= 2; i++)
            {
                float y = i * size.x * .075f;
                float width = Mathf.Sqrt(Mathf.Max(.001f, 1 - i * i * .12f)) * size.x * .40f;
                var bar = Surface(pack, name + "Grille" + (i + 2), RoundedBox(name + "Grille" + (i + 2), new Vector3(width, .019f, .022f), .007f), color);
                bar.localPosition = new Vector3(0, .02f + y, -size.z * .77f);
            }
            // The side speaker reads from the front three-quarter menu camera as well as from the game camera behind.
            Egg(pack, name + "SideSpeakerRim", new Vector3(size.x * .51f, .02f, 0), new Vector3(.045f, size.y * .27f, size.y * .27f), p.Brass);
            Egg(pack, name + "SideSpeakerFace", new Vector3(size.x * .57f, .02f, 0), new Vector3(.025f, size.y * .21f, size.y * .21f), p.Navy);
            for (int i = -2; i <= 2; i++)
            {
                float y = i * size.y * .065f;
                float length = Mathf.Sqrt(Mathf.Max(.001f, 1 - i * i * .12f)) * size.y * .32f;
                var bar = Surface(pack, name + "SideGrille" + (i + 2), RoundedBox(name + "SideGrille" + (i + 2), new Vector3(.022f, .019f, length), .007f), color);
                bar.localPosition = new Vector3(size.x * .615f, .02f + y, 0);
            }
            var handle = Bezier(new Vector3(-size.x * .22f, size.y * .48f, 0), new Vector3(-size.x * .22f, size.y * .70f, 0), new Vector3(size.x * .22f, size.y * .70f, 0), new Vector3(size.x * .22f, size.y * .48f, 0), 12);
            Tube(pack, name + "RadioHandle", handle, .025f, p.Brass, 8);
            Egg(pack, name + "RadioKnob", new Vector3(-size.x * .51f, size.y * .25f, 0), new Vector3(.055f, .055f, .055f), p.Brass);
            Tube(root, name + "StrapLeft", Bezier(position + new Vector3(-size.x * .45f, size.y * .35f, .03f), new Vector3(-size.x * .7f, 1.0f, .07f), new Vector3(-size.x * .85f, .49f, .16f), position + new Vector3(-size.x * .45f, -size.y * .34f, .02f), 16), .028f, p.MintDark, 8);
            Tube(root, name + "StrapRight", Bezier(position + new Vector3(size.x * .45f, size.y * .35f, .03f), new Vector3(size.x * .7f, 1.0f, .07f), new Vector3(size.x * .85f, .49f, .16f), position + new Vector3(size.x * .45f, -size.y * .34f, .02f), 16), .028f, p.MintDark, 8);
        }

        private static Transform Pivot(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform;
        }

        private static Transform Surface(Transform parent, string name, Mesh mesh, Material material, bool persist = false)
        {
            var node = Pivot(parent, name, Vector3.zero);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = persist ? StoreMesh(mesh, name) : mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            return node;
        }

        private static void CombineGroup(Transform pivot, string prefix, Transform[] excludedBranches)
        {
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var consumed = new List<MeshFilter>();
            foreach (var filter in pivot.GetComponentsInChildren<MeshFilter>(true))
            {
                bool excluded = false;
                foreach (var branch in excludedBranches)
                    if (filter.transform == branch || filter.transform.IsChildOf(branch)) { excluded = true; break; }
                if (excluded) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!byMaterial.TryGetValue(renderer.sharedMaterial, out var meshes))
                    byMaterial.Add(renderer.sharedMaterial, meshes = new List<CombineInstance>());
                meshes.Add(new CombineInstance { mesh = filter.sharedMesh, transform = pivot.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                consumed.Add(filter);
            }
            foreach (var pair in byMaterial)
            {
                var mesh = new Mesh { name = prefix + "_" + pair.Key.name };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true); mesh.RecalculateBounds();
                Surface(pivot, mesh.name, mesh, pair.Key, true);
            }
            foreach (var filter in consumed)
            {
                if (!AssetDatabase.Contains(filter.sharedMesh)) UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
                UnityEngine.Object.DestroyImmediate(filter.gameObject);
            }
        }

        private static void Egg(Transform parent, string name, Vector3 position, Vector3 size, Material material, Vector3 rotation = default)
        {
            var node = Surface(parent, name, Ellipsoid(name, size), material);
            node.localPosition = position; node.localRotation = Quaternion.Euler(rotation);
        }

        private static void Tube(Transform parent, string name, Vector3[] points, float radius, Material material, int sides)
            => Surface(parent, name, TubeMesh(name, points, radius, sides), material);

        private static Mesh StoreMesh(Mesh source, string name)
        {
            string path = MeshFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(source, path); return source; }
            // CopySerialized does not reliably invalidate a Mesh's native render buffers when
            // a rebuilt asset changes its vertex/index layout. Use the mesh API so Editor
            // previews and subsequent prefab instances see the same geometry as its CPU data.
            existing.Clear();
            existing.indexFormat = source.indexFormat;
            existing.vertices = source.vertices;
            existing.normals = source.normals;
            existing.uv = source.uv;
            existing.triangles = source.triangles;
            existing.RecalculateBounds();
            existing.UploadMeshData(false);
            UnityEngine.Object.DestroyImmediate(source);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Mesh Ellipsoid(string name, Vector3 radii)
        {
            const int segments = 20, rings = 12;
            var vertices = new Vector3[(segments + 1) * (rings + 1)]; var normals = new Vector3[vertices.Length]; var uv = new Vector2[vertices.Length];
            for (int y = 0; y <= rings; y++) for (int x = 0; x <= segments; x++)
            {
                float v = y / (float)rings, u = x / (float)segments, phi = v * Mathf.PI, theta = u * Mathf.PI * 2;
                var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                int i = y * (segments + 1) + x; vertices[i] = Vector3.Scale(unit, radii);
                normals[i] = new Vector3(unit.x / radii.x, unit.y / radii.y, unit.z / radii.z).normalized; uv[i] = new Vector2(u, v);
            }
            return MeshFrom(name, vertices, GridTriangles(segments, rings, true), uv, normals);
        }

        private static Mesh Lathe(string name, Vector3[] profile, float depthRatio, int segments, int rings)
        {
            var vertices = new Vector3[(segments + 1) * (rings + 1)]; var uv = new Vector2[vertices.Length];
            for (int y = 0; y <= rings; y++)
            {
                var p = SampleCurve(profile, y / (float)rings); float radius = Mathf.Max(.001f, p.z);
                for (int x = 0; x <= segments; x++)
                {
                    float theta = x / (float)segments * Mathf.PI * 2;
                    int i = y * (segments + 1) + x;
                    vertices[i] = new Vector3(p.x + Mathf.Cos(theta) * radius, p.y, Mathf.Sin(theta) * radius * depthRatio);
                    uv[i] = new Vector2(x / (float)segments, y / (float)rings);
                }
            }
            var mesh = MeshFrom(name, vertices, GridTriangles(segments, rings, false), uv);
            var n = mesh.normals;
            for (int y = 0; y <= rings; y++)
            {
                int a = y * (segments + 1), b = a + segments; var normal = (n[a] + n[b]).normalized; n[a] = n[b] = normal;
            }
            mesh.normals = n; return mesh;
        }

        private static Mesh TubeMesh(string name, Vector3[] points, float radius, int sides)
        {
            var vertices = new Vector3[points.Length * (sides + 1)]; var normals = new Vector3[vertices.Length]; var uv = new Vector2[vertices.Length];
            for (int p = 0; p < points.Length; p++)
            {
                var tangent = (points[Mathf.Min(points.Length - 1, p + 1)] - points[Mathf.Max(0, p - 1)]).normalized;
                var axis = Vector3.Cross(tangent, Vector3.forward).normalized;
                if (axis.sqrMagnitude < .1f) axis = Vector3.Cross(tangent, Vector3.up).normalized;
                var binormal = Vector3.Cross(tangent, axis).normalized;
                for (int s = 0; s <= sides; s++)
                {
                    float angle = s / (float)sides * Mathf.PI * 2; var normal = Mathf.Cos(angle) * axis + Mathf.Sin(angle) * binormal;
                    int i = p * (sides + 1) + s; vertices[i] = points[p] + normal * radius; normals[i] = normal; uv[i] = new Vector2(s / (float)sides, p / (float)(points.Length - 1));
                }
            }
            return MeshFrom(name, vertices, GridTriangles(sides, points.Length - 1, true), uv, normals);
        }

        private static int[] GridTriangles(int columns, int rows, bool reverse)
        {
            var result = new int[columns * rows * 6]; int cursor = 0;
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                int a = y * (columns + 1) + x, b = a + columns + 1, c = a + 1, d = b + 1;
                result[cursor++] = a; result[cursor++] = reverse ? c : b; result[cursor++] = reverse ? b : c;
                result[cursor++] = c; result[cursor++] = reverse ? d : b; result[cursor++] = reverse ? b : d;
            }
            return result;
        }

        private static Mesh RoundedBox(string name, Vector3 size, float radius)
        {
            const int resolution = 4;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            var half = size * .5f; radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .95f);
            var inner = half - Vector3.one * radius;
            var directions = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            foreach (var normal in directions)
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.Cross(Vector3.up, normal);
                Vector3 v = Vector3.Cross(normal, u);
                int offset = vertices.Count;
                for (int y = 0; y <= resolution; y++) for (int x = 0; x <= resolution; x++)
                {
                    Vector3 cube = normal + u * (2 * x / (float)resolution - 1) + v * (2 * y / (float)resolution - 1);
                    var point = Vector3.Scale(cube, half);
                    var nearest = new Vector3(Mathf.Clamp(point.x, -inner.x, inner.x), Mathf.Clamp(point.y, -inner.y, inner.y), Mathf.Clamp(point.z, -inner.z, inner.z));
                    var outward = (point - nearest).normalized;
                    vertices.Add(nearest + outward * radius); normals.Add(outward); uv.Add(new Vector2(x / (float)resolution, y / (float)resolution));
                }
                foreach (int index in GridTriangles(resolution, resolution, true)) triangles.Add(offset + index);
            }
            return MeshFrom(name, vertices.ToArray(), triangles.ToArray(), uv.ToArray(), normals.ToArray());
        }

        private static Mesh Ribbon(string name, Vector3[] points, float width)
        {
            const int steps = 16; var vertices = new Vector3[(steps + 1) * 4]; var uv = new Vector2[vertices.Length]; var triangles = new int[steps * 24];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps; var p = SampleCurve(points, t);
                float taper = 1 - t * .35f;
                int a = i * 4;
                vertices[a] = p + Vector3.up * width * .5f * taper + Vector3.forward * .006f;
                vertices[a + 1] = p - Vector3.up * width * .5f * taper + Vector3.forward * .006f;
                vertices[a + 2] = p + Vector3.up * width * .5f * taper - Vector3.forward * .006f;
                vertices[a + 3] = p - Vector3.up * width * .5f * taper - Vector3.forward * .006f;
                uv[a] = uv[a + 2] = new Vector2(t, 0); uv[a + 1] = uv[a + 3] = new Vector2(t, 1);
                if (i == steps) continue;
                int o = i * 24;
                int[] quad = { a, a + 4, a + 1, a + 1, a + 4, a + 5, a + 3, a + 6, a + 2, a + 7, a + 6, a + 3,
                    a + 2, a + 6, a, a, a + 6, a + 4, a + 1, a + 5, a + 3, a + 3, a + 5, a + 7 };
                Array.Copy(quad, 0, triangles, o, quad.Length);
            }
            return MeshFrom(name, vertices, triangles, uv);
        }

        private static Mesh SailMesh(string name, Vector3 a, Vector3 b, Vector3 c)
        {
            var center = (a + b + c) / 3 + Vector3.back * .055f;
            return MeshFrom(name, new[] { a, b, c, center, a, b, c, center }, new[] { 0, 1, 3, 1, 2, 3, 2, 0, 3, 7, 5, 4, 7, 6, 5, 7, 4, 6 },
                new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one * .33f, Vector2.zero, Vector2.up, Vector2.right, Vector2.one * .33f });
        }

        private static Mesh MeshFrom(string name, Vector3[] vertices, int[] triangles, Vector2[] uv, Vector3[] normals = null)
        {
            var nonDegenerate = new List<int>(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                if (Vector3.Cross(b - a, c - a).sqrMagnitude <= 1e-14f) continue;
                nonDegenerate.Add(triangles[i]); nonDegenerate.Add(triangles[i + 1]); nonDegenerate.Add(triangles[i + 2]);
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = nonDegenerate.ToArray(), uv = uv };
            if (normals == null) mesh.RecalculateNormals(); else mesh.normals = normals;
            mesh.RecalculateBounds(); return mesh;
        }

        private static Vector3 SampleCurve(Vector3[] points, float t)
        {
            float scaled = Mathf.Clamp01(t) * (points.Length - 1); int i = Mathf.Min(points.Length - 2, (int)scaled); float f = scaled - i;
            var a = points[Mathf.Max(0, i - 1)]; var b = points[i]; var c = points[i + 1]; var d = points[Mathf.Min(points.Length - 1, i + 2)];
            return .5f * (2 * b + (-a + c) * f + (2 * a - 5 * b + 4 * c - d) * f * f + (-a + 3 * b - 3 * c + d) * f * f * f);
        }

        private static Vector3[] Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int count)
        {
            var points = new Vector3[count];
            for (int i = 0; i < count; i++) { float t = i / (float)(count - 1), s = 1 - t; points[i] = s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d; }
            return points;
        }

        private static Vector3[] Ellipse(Vector3 center, float width, float depth, int count, bool closed)
        {
            var points = new Vector3[count + (closed ? 1 : 0)];
            for (int i = 0; i < points.Length; i++) { float a = i / (float)count * Mathf.PI * 2; points[i] = center + new Vector3(Mathf.Cos(a) * width, 0, Mathf.Sin(a) * depth); }
            return points;
        }

        private static Sprite RenderThumbnail(GameObject source, string id)
        {
            var preview = UnityEngine.Object.Instantiate(source);
            var cameraObject = new GameObject("Temporary courier thumbnail camera");
            var lightObject = new GameObject("Temporary courier thumbnail key");
            var fillObject = new GameObject("Temporary courier thumbnail fill");
            SceneManager.MoveGameObjectToScene(preview, source.scene);
            SceneManager.MoveGameObjectToScene(cameraObject, source.scene);
            SceneManager.MoveGameObjectToScene(lightObject, source.scene);
            SceneManager.MoveGameObjectToScene(fillObject, source.scene);
            RenderTexture target = null; Texture2D pixels = null; var previous = RenderTexture.active;
            try
            {
                preview.transform.position = new Vector3(0, -1000, 0);
                foreach (var node in preview.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = ThumbnailLayer;
                var motion = preview.GetComponent<CourierMotion>(); motion.ManualSimulation = true; motion.ResetMotion();
                preview.transform.rotation = Quaternion.Euler(0, -36, 0);
                var bounds = preview.GetComponentsInChildren<Renderer>()[0].bounds;
                foreach (var renderer in preview.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.scene = source.scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(1, 1, 1, 0);
                camera.cullingMask = 1 << ThumbnailLayer; camera.orthographic = true; camera.orthographicSize = bounds.size.y * .58f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 30; camera.allowHDR = false; camera.allowMSAA = true;
                camera.transform.position = bounds.center + new Vector3(0, .15f, 6); camera.transform.LookAt(bounds.center);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.color = new Color(1, .94f, .84f); light.cullingMask = 1 << ThumbnailLayer;
                light.transform.rotation = Quaternion.Euler(32, 145, 0);
                var fill = fillObject.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .55f; fill.color = new Color(.84f, .91f, 1); fill.cullingMask = 1 << ThumbnailLayer;
                fill.transform.rotation = Quaternion.Euler(20, -20, 0);
                target = new RenderTexture(512, 640, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                target.Create(); camera.targetTexture = target; camera.aspect = 512f / 640;
                camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(512, 640, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 512, 640), 0, 0); pixels.Apply();
                string path = IconFolder + "/Courier_" + id + ".png"; File.WriteAllBytes(path, pixels.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 1024; importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(preview); UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject); UnityEngine.Object.DestroyImmediate(fillObject);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        private sealed class Palette
        {
            public readonly Material Cream = Material("CourierCream", "FFF1D6", 0, .23f);
            public readonly Material Coral = Material("CourierCoral", "FF7959", .01f, .22f);
            public readonly Material Mint = Material("CourierMint", "81C8B7", .14f, .28f);
            public readonly Material MintDark = Material("CourierMintDark", "357E78", .22f, .28f);
            public readonly Material Navy = Material("CourierNavy", "243956", .06f, .27f);
            public readonly Material Blue = Material("CourierBlue", "537993", .18f, .31f);
            public readonly Material Amber = Material("CourierAmber", "F3C261", .06f, .24f);
            public readonly Material Brass = Material("CourierBrass", "BD9152", .62f, .33f);
            public readonly Material Sail = Material("CourierSail", "FFDEA0", .02f, .2f);
            public readonly Material Glow = Material("CourierMintBeacon", "65E7BC", .02f, .4f, true);
            public readonly Material GlowAmber = Material("CourierAmberBeacon", "FFDA70", .02f, .4f, true);

            private static Material Material(string name, string hex, float metallic, float smoothness, bool emissive = false)
            {
                string path = MaterialFolder + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("The courier catalog requires the project's URP Lit shader.");
                if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
                ColorUtility.TryParseHtmlString("#" + hex, out var color);
                material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * (emissive ? .38f : .035f));
                EditorUtility.SetDirty(material); return material;
            }
        }
    }
}
