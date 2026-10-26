using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using DancingCats.Clone;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Frozen-camera before/after evidence from actual imported skins and runtime motion.</summary>
    public static class CourierHopVisualReview
    {
        private const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        private const int Layer = 26, Columns = 4, LabelHeight = 22;
        private const float GameplayScale = .47f, Step = 1f / 240, ForwardSpeed = 6.5f;
        private static readonly string Root = Path.GetFullPath("Library/CourierHopReview");
        private static readonly FieldInfo RigField = typeof(CourierMotion).GetField("rig", BindingFlags.Instance | BindingFlags.NonPublic);

        [Serializable] private sealed class View
        {
            public string id;
            public int width, height;
            public Vector3 position, euler;
            public bool orthographic, followHeight;
            public float size, fieldOfView;
        }
        [Serializable] private sealed class Framing
        {
            public int version = 1;
            public float gameplayScale = GameplayScale, simulationStep = Step, forwardSpeed = ForwardSpeed;
            public View[] views;
        }
        [Serializable] private sealed class Input
        {
            public string courier, prefab, dependencyHash;
            public Vector3 instanceScale;
            public string visualPivot, leftFoot, rightFoot;
        }
        [Serializable] private sealed class ViewBounds
        {
            public string view;
            public Vector2 min, max;
            public bool fits;
        }
        [Serializable] private sealed class Metric
        {
            public string courier, sequence, label;
            public int cell, hop;
            public float time, progress, duration, bodyRollDegrees;
            public Vector3 visualScaleRatio, visualLocalPosition, rootPosition;
            public Vector3 boundsMin, boundsMax, bodyBoundsMin, bodyBoundsMax;
            public Vector3 leftFootCentroid, rightFootCentroid;
            public float leftMinusRightFootWorldY, leftMinusRightFootLocalY;
            public ViewBounds[] views;
        }
        [Serializable] private sealed class Report
        {
            public string stage, capturedUtc, unityVersion, motionSourceSha256, lightingDependencyHash, description;
            public Framing framing;
            public Input[] inputs;
            public Metric[] frames;
            public string[] sheets;
        }
        private sealed class Sample
        {
            public string label;
            public int hop;
            public float time, progress, duration;
        }
        private sealed class FootVertices
        {
            public SkinnedMeshRenderer skin;
            public int[] left, right;
        }
        private sealed class Actor : IDisposable
        {
            public GameObject stage, model;
            public CourierMotion motion;
            public CourierRigBinding rig;
            public Renderer[] renderers;
            public CourierPreviewSkinBake bake;
            public FootVertices[] feet;
            public Vector3 restScale;
            public Quaternion restRotation;
            public Vector3 leftRest, rightRest;
            public void Dispose()
            {
                bake?.Dispose();
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [MenuItem("Radio Archipelago/Imported couriers/Hop review/Capture baseline")]
        public static void Baseline() => Render("Baseline");

        [MenuItem("Radio Archipelago/Imported couriers/Hop review/Capture candidate")]
        public static void Candidate() => Render("Candidate");

        private static void Render(string stageName)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating,
                "Capture hop evidence in Edit Mode after compilation and imports finish.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            Require(catalog?.entries != null && catalog.entries.Length == 3 && catalog.entries.All(e => e?.prefab != null), "The three imported courier definitions are required.");
            Require(catalog.lighting != null, "The existing courier studio lighting profile is required.");
            string folder = Path.Combine(Root, stageName), framingPath = Path.Combine(Root, "framing.json");
            Require(stageName != "Baseline" || !File.Exists(Path.Combine(folder, "metrics.json")), "The baseline is already frozen. Preserve it for the matching candidate.");
            Require(stageName == "Baseline" || File.Exists(framingPath), "Capture the unchanged baseline before the candidate.");
            Directory.CreateDirectory(folder);
            var scene = EditorSceneManager.NewPreviewScene();
            var previousTarget = RenderTexture.active;
            var actors = new List<Actor>();
            var metrics = new List<Metric>(); var sheets = new List<string>();
            try
            {
                foreach (var entry in catalog.entries) actors.Add(CreateActor(scene, entry, catalog.lighting));
                Framing framing;
                if (stageName == "Baseline")
                {
                    framing = FitRestFraming(actors);
                    File.WriteAllText(framingPath, JsonUtility.ToJson(framing, true));
                }
                else framing = JsonUtility.FromJson<Framing>(File.ReadAllText(framingPath));
                Require(framing != null && framing.version == 1 && framing.views?.Length == 3 && Mathf.Abs(framing.gameplayScale - GameplayScale) < .00001f,
                    "The frozen framing file is incompatible with this review tool.");
                var inputs = new List<Input>();
                for (int i = 0; i < actors.Count; i++)
                {
                    foreach (var other in actors) other.stage.SetActive(other == actors[i]);
                    var actor = actors[i]; var entry = catalog.entries[i];
                    string prefabPath = AssetDatabase.GetAssetPath(entry.prefab);
                    inputs.Add(new Input { courier = entry.id, prefab = prefabPath,
                        dependencyHash = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString(), instanceScale = actor.model.transform.localScale,
                        visualPivot = actor.rig.visualPivot.name, leftFoot = actor.rig.leftFoot.name, rightFoot = actor.rig.rightFoot.name });
                    CaptureSequence(scene, actor, entry.id, "short-pair", new[] { .36f, .42f }, framing, folder, metrics, sheets);
                    CaptureSequence(scene, actor, entry.id, "long-flight", new[] { 1.2f }, framing, folder, metrics, sheets);
                }
                string sourcePath = "Assets/RadioArchipelago/Runtime/CourierMotion.cs";
                string sourceHash;
                using (var hash = SHA256.Create()) sourceHash = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(sourcePath))).Replace("-", "").ToLowerInvariant();
                File.WriteAllText(Path.Combine(folder, "metrics.json"), JsonUtility.ToJson(new Report {
                    stage = stageName, capturedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                    motionSourceSha256 = sourceHash, lightingDependencyHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(catalog.lighting)).ToString(),
                    description = "Actual catalog imported skins/materials, CourierMotion manual simulation at 240 Hz, existing CourierLightingRig/profile. " +
                        "Gameplay instance scale/offset and camera pose/FOV match OriginalPlayController; gameplay images use portrait 9:16. " +
                        "Disposable stage follows NativeRhythmRules.JumpY at fixed input speed 6.5; camera follows forward motion, diagnostic cameras also follow flight height. " +
                        "Short hops are consecutive: PlayLanding then PlayJump at the same contact, as in ordinary gameplay. The final contact plays Idle and Landing. " +
                        "No bone overrides, fake poses, saved scene, chart, model, music, collider or project-setting changes. Sheet cells are row-major; labels show cell number and seconds. " +
                        "World AABBs and weighted-foot centroids come from CPU-baked imported vertices. Roll and scale are relative to the bound visual pivot rest pose. " +
                        "Diagnostic cameras are fitted once from all three rest silhouettes with a 60% motion margin, then frozen for baseline/candidate.",
                    framing = framing, inputs = inputs.ToArray(), frames = metrics.ToArray(), sheets = sheets.ToArray()
                }, true));
                File.WriteAllText(Path.Combine(folder, "readme.txt"), "Courier hop review: " + stageName + "\n" +
                    "Each courier has short-pair and long-flight sheets in gameplay, rear-threequarter and front views.\n" +
                    "Read cells left-to-right then top-to-bottom; label = cell number and sequence time in seconds. Full matching data in metrics.json.\n" +
                    "Short-pair: .36 s hop followed immediately by .42 s hop, then .36 s landing/settle.\n" +
                    "Long-flight: 1.2 s flight, then .36 s landing/settle.\n" +
                    "Same frozen cameras, source prefab scales/materials, studio lighting profile and deterministic inputs across both captures.\n");
                Require(metrics.All(m => m.views.All(v => v.fits)), "One or more complete imported silhouettes leave the fixed camera margin; inspect metrics.json before using the comparison.");
                Debug.Log("COURIER_HOP_REVIEW PASS " + stageName + ": " + sheets.Count + " PNG sheets, " + metrics.Count + " actual-skin samples at " + folder);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                foreach (var actor in actors) actor.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Actor CreateActor(Scene scene, CourierDefinition entry, CourierLightingProfile lighting)
        {
            var actor = new Actor { stage = Node(scene, "Disposable hop stage " + entry.id) };
            try
            {
                actor.stage.transform.position = Vector3.up * NativeRhythmRules.GroundOffset;
                actor.model = UnityEngine.Object.Instantiate(entry.prefab);
                SceneManager.MoveGameObjectToScene(actor.model, scene);
                actor.model.transform.SetParent(actor.stage.transform, false);
                actor.model.transform.localPosition = Vector3.down * NativeRhythmRules.GroundOffset;
                actor.model.transform.localRotation = Quaternion.identity;
                actor.model.transform.localScale = Vector3.one * GameplayScale;
                foreach (var node in actor.model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = Layer;
                foreach (var source in actor.model.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
                foreach (var animator in actor.model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                actor.motion = actor.model.GetComponentInChildren<CourierMotion>(true);
                Require(actor.motion != null, entry.id + " has no actual CourierMotion.");
                actor.motion.ManualSimulation = true; actor.motion.ResetMotion();
                actor.rig = RigField?.GetValue(actor.motion) as CourierRigBinding;
                Require(actor.rig?.visualPivot != null && actor.rig.leftFoot != null && actor.rig.rightFoot != null,
                    entry.id + " requires the actual imported visual/left-foot/right-foot binding.");
                actor.restScale = actor.rig.visualPivot.localScale; actor.restRotation = actor.rig.visualPivot.localRotation;
                actor.leftRest = actor.rig.visualPivot.InverseTransformPoint(actor.rig.leftFoot.position);
                actor.rightRest = actor.rig.visualPivot.InverseTransformPoint(actor.rig.rightFoot.position);
                actor.renderers = actor.model.GetComponentsInChildren<Renderer>(true);
                actor.feet = FindFeet(actor.model, actor.rig);
                var lights = Node(scene, "Existing courier studio lighting");
                lights.transform.SetParent(actor.stage.transform, false);
                lights.AddComponent<CourierLightingRig>().Initialize(lighting, actor.model, Vector3.back, 1 << Layer);
                actor.bake = new CourierPreviewSkinBake(actor.model);
                return actor;
            }
            catch { actor.Dispose(); throw; }
        }

        private static Framing FitRestFraming(List<Actor> actors)
        {
            Bounds bounds = CourierVisualBounds.Measure(actors[0].renderers);
            foreach (var actor in actors.Skip(1)) bounds.Encapsulate(CourierVisualBounds.Measure(actor.renderers));
            var views = new[] {
                new View { id = "gameplay", width = 288, height = 512, position = new Vector3(0, 3.85f, -7.9f), euler = new Vector3(20, 0, 0), fieldOfView = 45 },
                new View { id = "rear-threequarter", width = 384, height = 384, orthographic = true, followHeight = true },
                new View { id = "front", width = 384, height = 384, orthographic = true, followHeight = true }
            };
            for (int i = 1; i < views.Length; i++)
            {
                var view = views[i]; var direction = i == 1 ? new Vector3(-.48f, .22f, -1).normalized : new Vector3(0, .12f, 1).normalized;
                view.position = bounds.center + direction * 8;
                var rotation = Quaternion.LookRotation(bounds.center - view.position); view.euler = rotation.eulerAngles;
                float half = 0;
                foreach (var corner in Corners(bounds))
                {
                    var local = Quaternion.Inverse(rotation) * (corner - bounds.center);
                    half = Mathf.Max(half, Mathf.Abs(local.x), Mathf.Abs(local.y));
                }
                view.size = Mathf.Max(.4f, half * 1.6f);
            }
            return new Framing { views = views };
        }

        private static void CaptureSequence(Scene scene, Actor actor, string id, string sequence, float[] durations, Framing framing,
            string folder, List<Metric> metrics, List<string> filenames)
        {
            var samples = Samples(durations); int rows = (samples.Count + Columns - 1) / Columns;
            var cameras = new Camera[framing.views.Length]; var targets = new RenderTexture[cameras.Length];
            var pixels = new Texture2D[cameras.Length]; var sheets = new Texture2D[cameras.Length];
            var previousTarget = RenderTexture.active;
            try
            {
                for (int i = 0; i < cameras.Length; i++)
                {
                    var view = framing.views[i]; var camera = Node(scene, "Fixed hop review " + view.id).AddComponent<Camera>(); cameras[i] = camera;
                    camera.enabled = false; camera.scene = scene; camera.cullingMask = 1 << Layer;
                    camera.orthographic = view.orthographic; camera.orthographicSize = Mathf.Max(.01f, view.size);
                    camera.fieldOfView = view.fieldOfView > 0 ? view.fieldOfView : 45;
                    camera.aspect = (float)view.width / view.height; camera.nearClipPlane = .03f; camera.farClipPlane = 80;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.93f, .94f, .96f, 1);
                    camera.allowHDR = false; camera.allowMSAA = true;
                    targets[i] = new RenderTexture(view.width, view.height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    targets[i].Create(); camera.targetTexture = targets[i];
                    pixels[i] = new Texture2D(view.width, view.height, TextureFormat.RGB24, false);
                    sheets[i] = new Texture2D(view.width * Columns, (view.height + LabelHeight) * rows, TextureFormat.RGB24, false);
                    var blank = Enumerable.Repeat(new Color32(24, 29, 38, 255), sheets[i].width * sheets[i].height).ToArray(); sheets[i].SetPixels32(blank);
                }
                ResetStage(actor); actor.motion.ResetMotion(); actor.motion.ManualSimulation = true; actor.motion.PlayJump(durations[0]); actor.motion.SetFlightProgress(0);
                float clock = 0; int currentHop = 0; float hopStart = 0; bool settled = false;
                for (int cell = 0; cell < samples.Count; cell++)
                {
                    var sample = samples[cell];
                    while (clock < sample.time - .000001f)
                    {
                        float boundary = hopStart + durations[currentHop];
                        float next = Mathf.Min(clock + Step, sample.time);
                        if (!settled) next = Mathf.Min(next, boundary);
                        float p = settled ? 1 : Mathf.Clamp01((next - hopStart) / durations[currentHop]);
                        float height = settled ? 0 : NativeRhythmRules.JumpY(0, NativeRhythmRules.HeightForDistance(durations[currentHop] * ForwardSpeed), p);
                        actor.stage.transform.position = new Vector3(0, NativeRhythmRules.GroundOffset + height, next * ForwardSpeed);
                        if (!settled) actor.motion.SetFlightProgress(p);
                        actor.motion.SimulateSprings(next - clock); clock = next;
                        if (!settled && clock >= boundary - .000001f)
                        {
                            float strength = Mathf.Lerp(.65f, 1.25f, Mathf.InverseLerp(.3f, 1.3f, durations[currentHop]));
                            actor.motion.PlayLanding(strength);
                            if (currentHop + 1 < durations.Length) { hopStart = boundary; currentHop++; actor.motion.PlayJump(durations[currentHop]); actor.motion.SetFlightProgress(0); }
                            else { settled = true; actor.motion.PlayIdle(); }
                        }
                    }
                    actor.bake.Refresh();
                    Bounds bounds = CourierVisualBounds.Measure(actor.renderers), body = CourierVisualBounds.MeasureBody(actor.renderers);
                    var centroid = FootCentroids(actor.feet);
                    Vector3 up = (Quaternion.Inverse(actor.restRotation) * actor.rig.visualPivot.localRotation) * Vector3.up;
                    var metric = new Metric { courier = id, sequence = sequence, cell = cell + 1, label = sample.label, hop = sample.hop,
                        time = sample.time, progress = sample.progress, duration = sample.duration,
                        bodyRollDegrees = Mathf.Atan2(-up.x, up.y) * Mathf.Rad2Deg,
                        visualScaleRatio = Divide(actor.rig.visualPivot.localScale, actor.restScale), visualLocalPosition = actor.rig.visualPivot.localPosition,
                        rootPosition = actor.model.transform.position, boundsMin = bounds.min, boundsMax = bounds.max, bodyBoundsMin = body.min, bodyBoundsMax = body.max,
                        leftFootCentroid = centroid[0], rightFootCentroid = centroid[1], leftMinusRightFootWorldY = centroid[0].y - centroid[1].y,
                        leftMinusRightFootLocalY = (actor.rig.visualPivot.InverseTransformPoint(actor.rig.leftFoot.position) - actor.leftRest).y
                            - (actor.rig.visualPivot.InverseTransformPoint(actor.rig.rightFoot.position) - actor.rightRest).y,
                        views = new ViewBounds[cameras.Length] };
                    for (int i = 0; i < cameras.Length; i++)
                    {
                        var camera = cameras[i]; var view = framing.views[i];
                        Vector3 follow = new Vector3(0, view.followHeight ? actor.stage.transform.position.y - NativeRhythmRules.GroundOffset : 0, actor.stage.transform.position.z);
                        camera.transform.SetPositionAndRotation(view.position + follow, Quaternion.Euler(view.euler));
                        metric.views[i] = ProjectBounds(camera, bounds, view.id);
                        camera.Render(); RenderTexture.active = targets[i];
                        pixels[i].ReadPixels(new Rect(0, 0, view.width, view.height), 0, 0); pixels[i].Apply();
                        int x = cell % Columns * view.width, y = (rows - 1 - cell / Columns) * (view.height + LabelHeight);
                        sheets[i].SetPixels32(x, y + LabelHeight, view.width, view.height, pixels[i].GetPixels32());
                        DrawLabel(sheets[i], x + 8, y + 5, (cell + 1).ToString("00") + "  " + sample.time.ToString("0.000", CultureInfo.InvariantCulture));
                    }
                    metrics.Add(metric);
                }
                for (int i = 0; i < sheets.Length; i++)
                {
                    sheets[i].Apply(); string file = id + "-" + sequence + "-" + framing.views[i].id + ".png";
                    File.WriteAllBytes(Path.Combine(folder, file), sheets[i].EncodeToPNG()); filenames.Add(file);
                }
            }
            finally
            {
                RenderTexture.active = previousTarget;
                foreach (var camera in cameras) if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
                foreach (var target in targets) if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                foreach (var texture in pixels.Concat(sheets)) if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                ResetStage(actor); actor.motion.ResetMotion();
            }
        }

        private static List<Sample> Samples(float[] durations)
        {
            var result = new List<Sample>(); float start = 0;
            for (int hop = 0; hop < durations.Length; hop++)
            {
                float duration = durations[hop];
                float[] times = duration < .6f ? new[] { 0, .035f, .065f, duration * .33f, duration * .5f, duration * .67f, duration * .85f, duration - .001f }
                    : new[] { 0, .035f, .08f, .15f, .3f, .45f, .6f, .75f, .9f, 1.05f, duration - .001f };
                foreach (float time in times) result.Add(new Sample { label = "hop-" + (hop + 1), hop = hop + 1, time = start + time, progress = time / duration, duration = duration });
                start += duration;
            }
            foreach (float time in new[] { 0, .035f, .075f, .14f, .24f, .36f })
                result.Add(new Sample { label = "landing+" + time.ToString("F3", CultureInfo.InvariantCulture), hop = durations.Length + 1, time = start + time, progress = 1, duration = durations.Last() });
            return result;
        }

        private static FootVertices[] FindFeet(GameObject model, CourierRigBinding rig)
        {
            var result = new List<FootVertices>();
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var counts = skin.sharedMesh.GetBonesPerVertex(); var weights = skin.sharedMesh.GetAllBoneWeights();
                var left = new List<int>(); var right = new List<int>(); int cursor = 0;
                for (int vertex = 0; vertex < counts.Length; vertex++)
                {
                    float l = 0, r = 0;
                    for (int i = 0; i < counts[vertex]; i++)
                    {
                        var weight = weights[cursor++]; var bone = skin.bones[weight.boneIndex];
                        if (bone == rig.leftFoot || bone != null && bone.IsChildOf(rig.leftFoot)) l += weight.weight;
                        if (bone == rig.rightFoot || bone != null && bone.IsChildOf(rig.rightFoot)) r += weight.weight;
                    }
                    if (l >= .5f) left.Add(vertex); if (r >= .5f) right.Add(vertex);
                }
                if (left.Count + right.Count > 0) result.Add(new FootVertices { skin = skin, left = left.ToArray(), right = right.ToArray() });
            }
            Require(result.Sum(s => s.left.Length) > 0 && result.Sum(s => s.right.Length) > 0, "Both bound imported feet must have actual weighted vertices.");
            return result.ToArray();
        }

        private static Vector3[] FootCentroids(FootVertices[] samples)
        {
            var result = new[] { Vector3.zero, Vector3.zero }; int left = 0, right = 0; var scratch = new Mesh();
            try
            {
                foreach (var sample in samples)
                {
                    sample.skin.BakeMesh(scratch, true); var vertices = scratch.vertices;
                    foreach (int index in sample.left) { result[0] += sample.skin.transform.TransformPoint(vertices[index]); left++; }
                    foreach (int index in sample.right) { result[1] += sample.skin.transform.TransformPoint(vertices[index]); right++; }
                }
                result[0] /= left; result[1] /= right; return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(scratch); }
        }

        private static ViewBounds ProjectBounds(Camera camera, Bounds bounds, string name)
        {
            var result = new ViewBounds { view = name, min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue, fits = true };
            foreach (var corner in Corners(bounds))
            {
                var p = camera.WorldToViewportPoint(corner); result.min = Vector2.Min(result.min, p); result.max = Vector2.Max(result.max, p);
                result.fits &= p.x >= .015f && p.x <= .985f && p.y >= .015f && p.y <= .985f && p.z > camera.nearClipPlane;
            }
            return result;
        }
        private static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
        }
        private static void DrawLabel(Texture2D sheet, int x, int y, string value)
        {
            // Tiny deterministic bitmap digits avoid Editor GUI/font state and platform-specific text rasterization.
            string[] digits = { "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
                "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111" };
            foreach (char character in value)
            {
                string glyph = character >= '0' && character <= '9' ? digits[character - '0'] : character == '.' ? "000000000000010" : "000000000000000";
                for (int row = 0; row < 5; row++) for (int col = 0; col < 3; col++)
                    if (glyph[row * 3 + col] == '1') for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                        sheet.SetPixel(x + col * 2 + dx, y + (4 - row) * 2 + dy, new Color(.92f, .94f, .97f));
                x += 8;
            }
        }
        private static Vector3 Divide(Vector3 value, Vector3 rest) => new Vector3(value.x / rest.x, value.y / rest.y, value.z / rest.z);
        private static void ResetStage(Actor actor) => actor.stage.transform.position = Vector3.up * NativeRhythmRules.GroundOffset;
        private static GameObject Node(Scene scene, string name)
        {
            var node = new GameObject(name) { hideFlags = HideFlags.DontSave }; SceneManager.MoveGameObjectToScene(node, scene); return node;
        }
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException("COURIER_HOP_REVIEW: " + message); }
    }
}
