using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Read-only checks and isolated renders of the supplied, genuinely weighted character assets.</summary>
    public static class ImportedCourierValidation
    {
        private const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        private const int ReviewLayer = 30;
        private static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/RadioRigging/UnityReview"));
        private static readonly List<string> Notes = new List<string>();

        [MenuItem("Radio Archipelago/Imported couriers/Check rigs and poses")]
        public static void RunChecks() => RunChecks(Catalog().entries);

        public static void RunChecks(CourierDefinition[] entries)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run imported asset checks in Edit Mode.");
            Notes.Clear(); Directory.CreateDirectory(Output);
            try
            {
                foreach (var entry in entries) CheckAsset(entry);
                CourierMotionChecks.Run();
                File.WriteAllText(Path.Combine(Output, "rig-checks.txt"), "PASS\n" + string.Join("\n", Notes));
                Debug.Log("RADIO_IMPORTED_RIG PASS: " + entries.Length + " supplied skinned couriers, normalized weights, real UV/textures, explicit weighted controls and actual deformation/motion checks.");
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Output, "rig-checks.txt"), "FAIL\n" + string.Join("\n", Notes) + "\n" + error);
                throw;
            }
        }

        private static void CheckAsset(CourierDefinition entry)
        {
            Require(entry != null && entry.prefab != null, "A supplied courier definition is incomplete.");
            var prefab = entry.prefab;
            var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Require(skins.Length > 0, entry.id + ": expected actual skinned imported geometry, not the previous procedural stand-in.");
            Require(prefab.GetComponentsInChildren<Collider>(true).Length == 0 && prefab.GetComponentsInChildren<Rigidbody>(true).Length == 0,
                entry.id + ": visual character prefabs must not own gameplay physics.");
            var motion = prefab.GetComponentInChildren<CourierMotion>(true);
            Require(motion != null, entry.id + ": explicit courier motion is missing.");
            var rig = Binding(motion);
            Require(rig != null && rig.visualPivot != null && rig.visualPivot != motion.transform && rig.visualPivot.IsChildOf(motion.transform), entry.id + ": missing independent visual pivot.");
            var weighted = new HashSet<Transform>();
            int vertices = 0, triangles = 0, influenced = 0;
            var sources = new HashSet<string>();
            foreach (var skin in skins)
            {
                var mesh = skin.sharedMesh;
                Require(mesh != null && mesh.vertexCount > 0 && mesh.isReadable, entry.id + ": imported mesh must be present and readable for authoring validation: " + skin.name);
                string source = AssetDatabase.GetAssetPath(mesh); sources.Add(source);
                Require(source.StartsWith("Assets/RadioArchipelago/Imported/", StringComparison.Ordinal), entry.id + ": skin must reference the supplied imported model: " + source);
                Require(skin.bones != null && skin.bones.Length > 0 && skin.bones.All(bone => bone != null), entry.id + ": skin has invalid bone references: " + skin.name);
                Require(mesh.bindposes.Length == skin.bones.Length, entry.id + ": bind pose count does not match bones: " + skin.name);
                Require(skin.rootBone != null && skin.rootBone.IsChildOf(motion.transform), entry.id + ": skin root is outside the courier presentation hierarchy.");
                var positions = mesh.vertices; var uv = mesh.uv;
                Require(uv.Length == positions.Length && uv.Length > 0, entry.id + ": original texture coordinates are missing: " + skin.name);
                foreach (var value in positions) Require(Finite(value.x) && Finite(value.y) && Finite(value.z), entry.id + ": non-finite imported vertex.");
                foreach (var value in uv) Require(Finite(value.x) && Finite(value.y), entry.id + ": non-finite imported UV.");
                var counts = mesh.GetBonesPerVertex(); var weights = mesh.GetAllBoneWeights();
                // These are borrowed read-only views over mesh-owned memory (Allocator.None).
                {
                    Require(counts.Length == mesh.vertexCount, entry.id + ": every imported vertex needs a valid weight record.");
                    int cursor = 0;
                    for (int vertex = 0; vertex < counts.Length; vertex++)
                    {
                        Require(counts[vertex] > 0, entry.id + ": an imported vertex has no bone influence.");
                        float sum = 0;
                        for (int n = 0; n < counts[vertex]; n++)
                        {
                            Require(cursor < weights.Length, entry.id + ": truncated skin weight data.");
                            var weight = weights[cursor++];
                            Require(Finite(weight.weight) && weight.weight >= 0 && weight.boneIndex >= 0 && weight.boneIndex < skin.bones.Length, entry.id + ": invalid bone influence.");
                            sum += weight.weight;
                            if (weight.weight > .0001f) weighted.Add(skin.bones[weight.boneIndex]);
                        }
                        Require(Mathf.Abs(sum - 1) < .005f, entry.id + ": vertex weights are not normalized.");
                        influenced++;
                    }
                    Require(cursor == weights.Length, entry.id + ": skin weights have trailing unassigned influences.");
                }
                Require(skin.sharedMaterials.Length >= mesh.subMeshCount, entry.id + ": a textured mesh has missing material slots.");
                foreach (var material in skin.sharedMaterials)
                {
                    Require(material != null && material.shader != null && !material.shader.name.Contains("InternalError"), entry.id + ": missing or unsupported material.");
                    var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
                    Require(texture != null && texture.width >= 256 && texture.height >= 256, entry.id + ": supplied skin needs its real color texture: " + material.name);
                    Require(AssetDatabase.GetAssetPath(texture).StartsWith("Assets/RadioArchipelago/Imported/", StringComparison.Ordinal), entry.id + ": texture must come from the supplied character artwork.");
                }
                vertices += mesh.vertexCount;
                for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3;
            }
            Require(sources.Count > 0 && influenced == vertices, entry.id + ": supplied geometry must be used in its entirety.");
            CheckControl(entry.id, "head", rig.headPivot, motion.transform, weighted);
            CheckControl(entry.id, "left arm", rig.leftArm, motion.transform, weighted);
            CheckControl(entry.id, "right arm", rig.rightArm, motion.transform, weighted);
            CheckControl(entry.id, "left foot", rig.leftFoot, motion.transform, weighted);
            CheckControl(entry.id, "right foot", rig.rightFoot, motion.transform, weighted);
            CheckChain(entry.id, "antenna", rig.antennaChain, 2, motion.transform, weighted);
            Require(rig.antennaYawPivot != null && rig.antennaYawPivot.IsChildOf(motion.transform)
                && rig.antennaChain.All(node => node.IsChildOf(rig.antennaYawPivot)),
                entry.id + ": antenna needs a separate swivel above its true mounting point.");
            if (entry.id == "signal") CheckChain(entry.id, "crest", rig.crestChain, 2, motion.transform, weighted);
            if (entry.id == "signal" || entry.id == "orbit") CheckChain(entry.id, "scarf", rig.scarfChain, 4, motion.transform, weighted);
            if (entry.id == "sail") Require(rig.scarfChain == null || rig.scarfChain.Length == 0, "Lumen has no scarf in the supplied design.");
            bool shapeBlink = rig.blinkRenderer != null && rig.blinkRenderer.sharedMesh != null && rig.blinkRenderer.sharedMesh.GetBlendShapeIndex(rig.blinkShapeName) >= 0;
            Require(shapeBlink || rig.blinkEyes != null && rig.blinkEyes.Length >= 2 && rig.blinkEyes.All(eye => eye != null && eye.IsChildOf(motion.transform)), entry.id + ": blinking needs both real eye controls or an authored blend shape.");
            CheckMotion(entry);
            Notes.Add(entry.id + ": " + skins.Length + " skinned meshes; " + vertices + " weighted vertices; " + triangles + " triangles; " + weighted.Count + " deforming bones; sources " + string.Join(", ", sources));
        }

        private static void CheckControl(string id, string label, Transform control, Transform root, HashSet<Transform> weighted)
        {
            Require(control != null && control.IsChildOf(root), id + ": missing local " + label + " control.");
            Require(weighted.Any(bone => bone == control || bone.IsChildOf(control)), id + ": " + label + " control has no weighted geometry.");
        }

        private static void CheckChain(string id, string label, Transform[] chain, int minimum, Transform root, HashSet<Transform> weighted)
        {
            Require(chain != null && chain.Length >= minimum && chain.All(node => node != null && node.IsChildOf(root)), id + ": incomplete articulated " + label + " chain.");
            Require(chain.Distinct().Count() == chain.Length, id + ": repeated bone in " + label + " chain.");
            Require(chain.Any(node => weighted.Any(bone => bone == node || bone.IsChildOf(node))), id + ": " + label + " animation controls have no weighted geometry.");
        }

        private static void CheckMotion(CourierDefinition entry)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject actor = null;
            try
            {
                actor = UnityEngine.Object.Instantiate(entry.prefab); SceneManager.MoveGameObjectToScene(actor, scene);
                var motion = actor.GetComponentInChildren<CourierMotion>(); motion.ManualSimulation = true; motion.ResetMotion();
                var rig = Binding(motion); var nodes = actor.GetComponentsInChildren<Transform>(true);
                var rest = Snapshot(nodes); var root = Snapshot(new[] { actor.transform });
                var before = Bake(actor);
                var arms = new[] { ArmSamples(actor, rig.leftArm, entry.id), ArmSamples(actor, rig.rightArm, entry.id) };
                var armRest = arms.Select(samples => ArmCentroid(samples, rig.visualPivot)).ToArray();
                motion.PlayJump(2.4f); motion.SetFlightProgress(.55f); Step(motion, .9f);
                for (int i = 0; i < arms.Length; i++)
                {
                    var control = i == 0 ? rig.leftArm : rig.rightArm;
                    float angle = Quaternion.Angle(control.localRotation, rest[Array.IndexOf(nodes, control)].rotation);
                    Require(angle > 15 && angle <= 36, entry.id + ": long flight needs a restrained arm gesture of 15–35 degrees; actual " + control.name + " angle=" + angle.ToString("F3"));
                    var posed = ArmCentroid(arms[i], rig.visualPivot);
                    float rise = posed.y - armRest[i].y;
                    float outward = Mathf.Abs(posed.x) - Mathf.Abs(armRest[i].x);
                    Require(rise > .0025f || outward > .0025f, entry.id + ": actual arm-weighted vertices must rise or extend outward slightly during long flight; " +
                        control.name + " visual-local centroid deltaY=" + rise.ToString("F5") + ", outward=" + outward.ToString("F5"));
                    Notes.Add(entry.id + " " + control.name + " long-flight arm-weighted centroid: visual-local rise " + rise.ToString("F5") +
                        ", outward " + outward.ToString("F5") + "; gesture " + angle.ToString("F3") + " degrees.");
                }
                foreach (var chain in new[] { rig.antennaChain, rig.crestChain, rig.scarfChain })
                    if (chain != null && chain.Length > 0) Require(chain.Any(node => Quaternion.Angle(node.localRotation, rest[Array.IndexOf(nodes, node)].rotation) > .1f),
                        entry.id + ": an authored appendage chain remains rigid during flight: " + chain[0].name);
                Require(Deformed(before, Bake(actor)), entry.id + ": moving animation controls do not deform the supplied skin.");
                motion.SetPaused(true); var paused = Snapshot(nodes); var blends = BlinkWeight(rig); Step(motion, .6f); Same(paused, nodes, entry.id + " paused rig");
                Require(Mathf.Abs(BlinkWeight(rig) - blends) < .001f, entry.id + ": pause advances blinking.");
                motion.SetPaused(false); motion.PlayLanding(1); Step(motion, .04f);
                Require(rig.visualPivot.localScale.y < rest[Array.IndexOf(nodes, rig.visualPivot)].scale.y, entry.id + ": landing does not compress the actual supplied model.");
                motion.ResetMotion(); motion.PlayHold(false, 2.2f, true); Step(motion, .7f);
                Require(Quaternion.Angle(rig.visualPivot.localRotation, rest[Array.IndexOf(nodes, rig.visualPivot)].rotation) > 45, entry.id + ": ride pose does not rotate the courier visual.");
                motion.ResetMotion(); motion.PlayIdle(); Step(motion, .35f);
                Require((rig.leftFoot.localPosition - rest[Array.IndexOf(nodes, rig.leftFoot)].position).sqrMagnitude > .00001f ||
                    (rig.rightFoot.localPosition - rest[Array.IndexOf(nodes, rig.rightFoot)].position).sqrMagnitude > .00001f, entry.id + ": idle does not alternate actual feet.");
                motion.ResetMotion(); motion.PlayIdle(); Step(motion, 4.6f);
                float greeting = Quaternion.Angle(rig.rightArm.localRotation, rest[Array.IndexOf(nodes, rig.rightArm)].rotation);
                Require(greeting >= 8 && greeting <= 36, entry.id + ": idle greeting must stay a restrained 8–35 degree hand gesture; actual=" + greeting.ToString("F3"));
                motion.ResetMotion(); motion.PlayIdle(); Step(motion, 1.58f);
                bool eyesClose = rig.blinkEyes != null && rig.blinkEyes.Any(eye => eye.localScale.y < rest[Array.IndexOf(nodes, eye)].scale.y * .2f);
                Require(eyesClose || BlinkWeight(rig) > 90, entry.id + ": actual eyes do not close during the blink.");
                CheckBlinkGeometry(entry.id, rig);
                motion.ResetMotion(); Same(rest, nodes, entry.id + " rest restoration"); Same(root, new[] { actor.transform }, entry.id + " gameplay root");
            }
            finally { if (actor != null) UnityEngine.Object.DestroyImmediate(actor); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static List<Vector3[]> Bake(GameObject actor)
        {
            var result = new List<Vector3[]>();
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true); var vertices = mesh.vertices;
                    var culling = skin.bounds; culling.Expand(.002f);
                    foreach (var point in vertices)
                        Require(culling.Contains(skin.transform.TransformPoint(point)), skin.name + ": actual world geometry leaves its renderer culling bounds.");
                    result.Add(vertices);
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            return result;
        }
        private sealed class ArmVertexSample
        {
            public SkinnedMeshRenderer skin;
            public int[] vertices;
        }

        private static ArmVertexSample[] ArmSamples(GameObject actor, Transform arm, string id)
        {
            var result = new List<ArmVertexSample>();
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skin.sharedMesh;
                if (mesh == null) continue;
                var armBones = new HashSet<int>(Enumerable.Range(0, skin.bones.Length)
                    .Where(index => skin.bones[index] == arm || skin.bones[index] != null && skin.bones[index].IsChildOf(arm)));
                if (armBones.Count == 0) continue;
                var counts = mesh.GetBonesPerVertex(); var weights = mesh.GetAllBoneWeights();
                // Both arrays are borrowed read-only views over mesh-owned memory.
                int cursor = 0; var selected = new List<int>();
                for (int vertex = 0; vertex < counts.Length; vertex++)
                {
                    float influence = 0;
                    for (int n = 0; n < counts[vertex]; n++)
                    {
                        var weight = weights[cursor++];
                        if (armBones.Contains(weight.boneIndex)) influence += weight.weight;
                    }
                    if (influence >= .65f) selected.Add(vertex);
                }
                if (selected.Count > 0) result.Add(new ArmVertexSample { skin = skin, vertices = selected.ToArray() });
            }
            Require(result.Sum(sample => sample.vertices.Length) >= 3, id + ": bound arm needs actual vertices with at least .65 arm influence: " + arm.name);
            return result.ToArray();
        }

        private static Vector3 ArmCentroid(ArmVertexSample[] samples, Transform visualFrame)
        {
            var mesh = new Mesh(); var sum = Vector3.zero; int count = 0;
            try
            {
                foreach (var sample in samples)
                {
                    sample.skin.BakeMesh(mesh, true); var vertices = mesh.vertices;
                    foreach (int index in sample.vertices)
                    {
                        sum += visualFrame.InverseTransformPoint(sample.skin.transform.TransformPoint(vertices[index]));
                        count++;
                    }
                }
                Require(count > 0, "No actual arm-weighted vertices were baked.");
                return sum / count;
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        private static void CheckBlinkGeometry(string id, CourierRigBinding rig)
        {
            var skin = rig.blinkRenderer;
            if (skin == null || skin.sharedMesh == null) return;
            int index = skin.sharedMesh.GetBlendShapeIndex(rig.blinkShapeName);
            if (index < 0) return;
            var positions = new Vector3[skin.sharedMesh.vertexCount];
            var normals = new Vector3[positions.Length];
            for (int frame = 0; frame < skin.sharedMesh.GetBlendShapeFrameCount(index); frame++)
            {
                skin.sharedMesh.GetBlendShapeFrameVertices(index, frame, positions, normals, null);
                Require(normals.All(value => value.sqrMagnitude < 1e-10f), id + ": Blink must retain the authored smooth face normals instead of introducing eyelid creases.");
            }
            float prior = skin.GetBlendShapeWeight(index); var mesh = new Mesh();
            try
            {
                skin.SetBlendShapeWeight(index, 0); skin.BakeMesh(mesh, true); var open = mesh.vertices;
                skin.SetBlendShapeWeight(index, 100); skin.BakeMesh(mesh, true); var closed = mesh.vertices;
                Require(open.Length == closed.Length && open.Length > 0, id + ": Head blink must preserve supplied mesh topology.");
                int changed = 0; float maxDistance = 0;
                for (int i = 0; i < open.Length; i++)
                {
                    float distance = skin.transform.TransformVector(closed[i] - open[i]).magnitude;
                    if (distance > .0001f) changed++;
                    maxDistance = Mathf.Max(maxDistance, distance);
                }
                Require(changed > 0 && maxDistance > .0005f, id + ": closing Blink must actually deform the imported Head/eye vertices, not only change a weight value.");
                Notes.Add(id + " " + skin.name + " Blink affects " + changed + " actual vertices; max world deformation " + maxDistance.ToString("F5") + ".");
            }
            finally { skin.SetBlendShapeWeight(index, prior); UnityEngine.Object.DestroyImmediate(mesh); }
        }
        private static bool Deformed(List<Vector3[]> before, List<Vector3[]> after) => before.Count == after.Count &&
            before.Where((values, i) => values.Length == after[i].Length && values.Where((value, n) => (value - after[i][n]).sqrMagnitude > .000001f).Any()).Any();

        [MenuItem("Radio Archipelago/Imported couriers/Render animation review")]
        public static void RenderReview() => RenderReview(Catalog().entries);

        public static void RenderReview(CourierDefinition[] entries)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Render the imported character review in Edit Mode.");
            Directory.CreateDirectory(Output); var manifest = new List<string> { "Actual Unity meshes, imported textures, and CourierMotion poses. Preview scenes only; no saved scenes or assets changed." };
            foreach (var entry in entries)
            {
                var scene = EditorSceneManager.NewPreviewScene(); GameObject actor = null, cameraObject = null, keyObject = null, fillObject = null;
                var previous = RenderTexture.active; RenderTexture target = null; Texture2D pixels = null; CourierPreviewSkinBake baked = null;
                try
                {
                    actor = UnityEngine.Object.Instantiate(entry.prefab); SceneManager.MoveGameObjectToScene(actor, scene);
                    foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = ReviewLayer;
                    var motion = actor.GetComponentInChildren<CourierMotion>(); motion.ManualSimulation = true; motion.ResetMotion();
                    var bounds = BoundsOf(actor); float size = Mathf.Max(bounds.size.y, bounds.size.x * 1.15f) * .69f;
                    cameraObject = Node(scene, "Rig review camera"); var camera = cameraObject.AddComponent<Camera>();
                    camera.enabled = false; camera.scene = scene; camera.cullingMask = 1 << ReviewLayer;
                    camera.orthographic = true; camera.orthographicSize = size; camera.aspect = 1;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.965f, .949f, .906f, 1);
                    camera.nearClipPlane = .05f; camera.farClipPlane = 40; camera.allowHDR = false; camera.allowMSAA = true;
                    camera.transform.position = bounds.center + new Vector3(0, .18f, 8); camera.transform.LookAt(bounds.center);
                    keyObject = Light(scene, "Warm studio key", new Vector3(28, 145, 0), new Color(1, .95f, .87f), 1.2f);
                    fillObject = Light(scene, "Cool studio fill", new Vector3(18, -25, 0), new Color(.82f, .91f, 1), .6f);
                    target = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; target.Create(); camera.targetTexture = target;
                    pixels = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
                    baked = new CourierPreviewSkinBake(actor);
                    void Render(string pose, Action apply, float yaw = -24)
                    {
                        motion.ResetMotion(); actor.transform.localRotation = Quaternion.Euler(0, yaw, 0); apply();
                        baked.Refresh();
                        camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); pixels.Apply();
                        string filename = entry.id + "-" + pose + ".png"; File.WriteAllBytes(Path.Combine(Output, filename), pixels.EncodeToPNG());
                        manifest.Add(filename);
                    }
                    Render("01-front-rest", () => { }, 0);
                    Render("02-back-rest", () => { }, 180);
                    Render("03-idle-feet", () => { motion.PlayIdle(); Step(motion, .35f); });
                    Render("04-idle-wave", () => { motion.PlayIdle(); Step(motion, 4.6f); });
                    Render("05-blink", () => { motion.PlayIdle(); Step(motion, 1.58f); }, 0);
                    Render("06-jump", () => { motion.PlayJump(.45f); motion.SetFlightProgress(.3f); Step(motion, .14f); });
                    Render("07-long-flight", () => { motion.PlayJump(2.4f); motion.SetFlightProgress(.55f); Step(motion, .9f); });
                    Render("08-landing", () => { motion.PlayLanding(1); Step(motion, .04f); });
                    Render("09-riding-spin", () => { motion.PlayHold(false, 2.2f, true); Step(motion, .7f); });
                    Render("10-celebration", () => { motion.PlayEnd(); Step(motion, .6f); });
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                    if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                    baked?.Dispose();
                    foreach (var node in new[] { actor, cameraObject, keyObject, fillObject }) if (node != null) UnityEngine.Object.DestroyImmediate(node);
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
            File.WriteAllText(Path.Combine(Output, "render-manifest.txt"), string.Join("\n", manifest));
            Debug.Log("RADIO_IMPORTED_REVIEW PASS: " + manifest.Count + " actual Unity pose renders at " + Output);
        }

        private static GameObject Node(Scene scene, string name) { var node = new GameObject(name); SceneManager.MoveGameObjectToScene(node, scene); return node; }
        private static GameObject Light(Scene scene, string name, Vector3 rotation, Color color, float intensity)
        {
            var node = Node(scene, name); var light = node.AddComponent<UnityEngine.Light>(); light.type = LightType.Directional;
            light.color = color; light.intensity = intensity; light.cullingMask = 1 << ReviewLayer; light.shadows = LightShadows.None;
            node.transform.rotation = Quaternion.Euler(rotation); return node;
        }
        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true); Require(renderers.Length > 0, "No rendered geometry in supplied prefab.");
            return CourierVisualBounds.Measure(renderers);
        }
        private static CourierCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            Require(catalog != null && catalog.entries != null && catalog.entries.Length == 3, "Expected the three-courier catalog."); return catalog;
        }
        private static CourierRigBinding Binding(CourierMotion motion) => typeof(CourierMotion).GetField("rig", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(motion) as CourierRigBinding;
        private static float BlinkWeight(CourierRigBinding rig)
        {
            if (rig.blinkRenderer == null || rig.blinkRenderer.sharedMesh == null) return 0;
            int index = rig.blinkRenderer.sharedMesh.GetBlendShapeIndex(rig.blinkShapeName); return index >= 0 ? rig.blinkRenderer.GetBlendShapeWeight(index) : 0;
        }
        private struct Pose { public Vector3 position, scale; public Quaternion rotation; }
        private static Pose[] Snapshot(Transform[] nodes) => nodes.Select(node => new Pose { position = node.localPosition, rotation = node.localRotation, scale = node.localScale }).ToArray();
        private static void Same(Pose[] expected, Transform[] nodes, string label)
        {
            for (int i = 0; i < nodes.Length; i++) Require((expected[i].position - nodes[i].localPosition).sqrMagnitude < 1e-9f &&
                (expected[i].scale - nodes[i].localScale).sqrMagnitude < 1e-9f && Quaternion.Angle(expected[i].rotation, nodes[i].localRotation) < .02f, label + ": transform changed: " + nodes[i].name);
        }
        private static void Step(CourierMotion motion, float seconds) { while (seconds > .000001f) { float dt = Mathf.Min(1f / 60, seconds); motion.SimulateSprings(dt); seconds -= dt; } }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("RADIO_IMPORTED_RIG FAIL: " + message); }
    }
}
