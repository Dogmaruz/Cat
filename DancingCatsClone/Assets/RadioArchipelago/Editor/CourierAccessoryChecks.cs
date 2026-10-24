using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Exercises the imported bones and actual deformed skin, without inspecting solver state.</summary>
    public static class CourierAccessoryChecks
    {
        private const float Dt = 1f / 120;
        private const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        private static readonly string Output = Path.GetFullPath("Library/RadioRigging/AccessoryReview/Unity");

        [MenuItem("Radio Archipelago/Imported couriers/Check accessory motion")]
        public static void RunChecks()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Run accessory checks in Edit Mode after compilation.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            Require(catalog != null && catalog.entries != null && catalog.entries.Length == 3, "Missing three-courier catalog.");
            var notes = new List<string>();
            var scene = EditorSceneManager.NewPreviewScene();
            Directory.CreateDirectory(Output);
            try
            {
                foreach (var definition in catalog.entries)
                {
                    var actor = UnityEngine.Object.Instantiate(definition.prefab);
                    SceneManager.MoveGameObjectToScene(actor, scene);
                    try { CheckCourier(definition.id, actor, notes); }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
                File.WriteAllText(Path.Combine(Output, "accessory-checks.txt"), "PASS\n" + string.Join("\n", notes) + "\n");
                Debug.Log("RADIO_ACCESSORIES PASS: real imported skins, hinged turns, scarf tips, flight rotation, opposite world ride rotation, pause/reset and hitch stability. " + Output);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Output, "accessory-checks.txt"), "FAIL\n" + string.Join("\n", notes) + "\n" + error);
                throw;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void CheckCourier(string id, GameObject actor, List<string> notes)
        {
            var motion = actor.GetComponentInChildren<CourierMotion>(true);
            Require(motion != null, id + ": no actual CourierMotion.");
            var binding = typeof(CourierMotion).GetField("rig", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(motion) as CourierRigBinding;
            Require(binding != null && binding.visualPivot != null, id + ": no explicit imported rig binding.");
            var yaw = binding.antennaYawPivot;
            Require(yaw != null && yaw.name == "antenna.yaw" && yaw.IsChildOf(binding.visualPivot), id + ": rotating assembly needs its actual mounting yaw bone.");
            Require(binding.antennaChain != null && binding.antennaChain.Length >= 2
                && binding.antennaChain.All(bone => bone != null && bone.IsChildOf(yaw)), id + ": flexible antenna bones must follow the mounting hinge.");
            motion.ManualSimulation = true; motion.ResetMotion();
            var nodes = actor.GetComponentsInChildren<Transform>(true);
            var rest = Snapshot(nodes);
            var roots = ChainRoots(binding.scarfChain);
            var tips = ChainTips(binding.scarfChain);
            if (id == "sail") Require(tips.Length == 0, "Lumen must retain its supplied design without an invented scarf.");
            else Require(roots.Length >= 2 && tips.Length >= 2, id + ": scarf needs independently deformable tails.");
            using (var skin = new SkinSamples(actor, binding, tips))
            {
                float height = Mathf.Max(.1f, skin.Height);
                Quaternion hingeRest = yaw.localRotation;
                motion.PlayIdle(); motion.SetFacing(55); Step(motion, .12f);
                float firstHinge = Quaternion.Angle(hingeRest, yaw.localRotation);
                motion.SetFacing(-55); Step(motion, .18f);
                float oppositeHinge = Quaternion.Angle(hingeRest, yaw.localRotation);
                Require(Mathf.Max(firstHinge, oppositeHinge) > .6f, id + ": turning does not move the mounting hinge.");
                skin.CheckGeometry(id + " turning");

                float tipResponse = 0, flightTipExcursion = 0, lateralTipResponse = 0;
                if (tips.Length > 0)
                {
                    motion.ResetMotion(); motion.PlayIdle(); motion.SetFacing(55); Step(motion, .14f);
                    var positive = skin.TipCentroids(binding.visualPivot);
                    motion.ResetMotion(); motion.PlayIdle(); motion.SetFacing(-55); Step(motion, .14f);
                    var negative = skin.TipCentroids(binding.visualPivot);
                    tipResponse = MaxDistance(positive, negative);
                    Require(tipResponse > height * .003f, id + ": the actual painted scarf tips do not respond to opposite turns.");
                    motion.SetFacing(55);
                    var before = skin.TipCentroids(binding.visualPivot);
                    Step(motion, Dt); var first = skin.TipCentroids(binding.visualPivot);
                    Step(motion, .13f); var later = skin.TipCentroids(binding.visualPivot);
                    Require(MaxDistance(before, first) < height * .04f && MaxDistance(first, later) > height * .001f,
                        id + ": scarf turn response should develop over time instead of snapping instantly.");

                    motion.ResetMotion();
                    var restTips = skin.TipCentroids(binding.visualPivot);
                    motion.PlayJump(2.4f);
                    for (int frame = 0; frame < 240; frame++)
                    {
                        motion.SetFlightProgress(frame / 239f); motion.SimulateSprings(.01f);
                        if (frame % 10 == 0)
                        {
                            flightTipExcursion = Mathf.Max(flightTipExcursion, MaxDistance(restTips, skin.TipCentroids(binding.visualPivot)));
                            skin.CheckGeometry(id + " free scarf flight " + frame);
                        }
                    }
                    Require(flightTipExcursion > height * .04f, id + ": scarf ends need a conspicuous flight response, actual=" + Number(flightTipExcursion));

                    Vector3 rootPosition = motion.transform.position;
                    Vector3[] MovingTips(float direction)
                    {
                        motion.transform.position = rootPosition; motion.ResetMotion(); motion.PlayIdle();
                        for (int frame = 0; frame < 22; frame++)
                        {
                            // The real controller repeats this call every frame.
                            motion.SetPaused(false);
                            motion.transform.position += motion.transform.right * (direction * .025f);
                            motion.SimulateSprings(Dt);
                        }
                        return skin.TipCentroids(binding.visualPivot);
                    }
                    try
                    {
                        var movingRight = MovingTips(1); var movingLeft = MovingTips(-1);
                        lateralTipResponse = MaxDistance(movingRight, movingLeft);
                        Require(lateralTipResponse > height * .01f, id + ": real lateral movement must affect the scarf even at a fixed facing.");
                    }
                    finally { motion.transform.position = rootPosition; motion.ResetMotion(); }
                }

                motion.ResetMotion(); motion.PlayJump(.42f);
                var shortYaw = TrackYaw(motion, binding.visualPivot, yaw, .42f, .42f, skin, id + " short jump");
                Require(Mathf.Abs(shortYaw.rotor) < 40, id + ": a short jump made the antenna perform a full rotor gesture.");
                motion.ResetMotion(); motion.PlayJump(2.4f);
                var flight = TrackYaw(motion, binding.visualPivot, yaw, 2.4f, 2.4f, skin, id + " long flight");
                Require(Mathf.Abs(flight.rotor) > 60 && Mathf.Abs(flight.rotor) < 650 && flight.maximumRate < 300,
                    id + ": long flight needs visible, controlled rotor rotation; measured " + Number(flight.rotor) + " degrees, peak " + Number(flight.maximumRate) + " deg/s.");
                Quaternion beforeExit = yaw.rotation;
                motion.PlayIdle(); motion.PlayLanding(); Step(motion, Dt);
                Require(Quaternion.Angle(beforeExit, yaw.rotation) < 8, id + ": leaving flight snaps the rotating assembly.");
                Step(motion, 1.2f); skin.CheckGeometry(id + " settling");

                var rideNotes = new List<string>();
                foreach (bool left in new[] { false, true })
                {
                    motion.ResetMotion(); motion.PlayHold(left, 2.6f, true);
                    var ride = TrackYaw(motion, binding.visualPivot, yaw, 2.6f, 0, skin, id + " ride " + left);
                    Require(Mathf.Abs(ride.body) > 250 && Mathf.Abs(ride.rotor) > 45 && ride.body * ride.rotor < 0,
                        id + ": assembly must turn opposite to the hero in WORLD space; body=" + Number(ride.body) + ", rotor=" + Number(ride.rotor));
                    rideNotes.Add((left ? "left" : "right") + " body=" + Number(ride.body) + "/rotor=" + Number(ride.rotor));
                }

                motion.ResetMotion(); motion.PlayJump(2.4f); Step(motion, .7f);
                motion.SetPaused(true); var paused = Snapshot(nodes); var pausedVertices = skin.AllVertices();
                Step(motion, .7f); Same(paused, nodes, id + " paused transforms");
                Require(MaxDistance(pausedVertices, skin.AllVertices()) < .00001f, id + ": paused skin keeps moving.");
                motion.SetPaused(false); motion.SimulateSprings(.25f); motion.SimulateSprings(float.NaN); motion.SimulateSprings(float.PositiveInfinity);
                skin.CheckGeometry(id + " hitch");
                foreach (var root in roots)
                {
                    int index = Array.IndexOf(nodes, root);
                    Require((root.localPosition - rest[index].position).sqrMagnitude < .00000001f,
                        id + ": scarf attachment translated away from its source mounting point.");
                }
                motion.ResetMotion(); Same(rest, nodes, id + " reset");
                notes.Add("PASS " + id + ": hinge=" + Number(Mathf.Max(firstHinge, oppositeHinge))
                    + "deg; painted scarf turn response=" + Number(tipResponse) + "; short rotor=" + Number(shortYaw.rotor) + "deg"
                    + "; scarf flight excursion=" + Number(flightTipExcursion) + "; real lateral response=" + Number(lateralTipResponse)
                    + "; flight rotor=" + Number(flight.rotor) + "deg, peak=" + Number(flight.maximumRate)
                    + "deg/s; ride [" + string.Join("; ", rideNotes) + "]; max accessory edge strain=" + Number(skin.MaximumStrain)
                    + "; finite skin, fixed attachments, pause/reset/hitch PASS.");
            }
        }

        private struct YawTrace { public float body, rotor, maximumRate; }
        private static YawTrace TrackYaw(CourierMotion motion, Transform visual, Transform rotor, float duration, float flightDuration, SkinSamples skin, string context)
        {
            // Markers express character forward in each actual bone's initial local axes.
            Vector3 bodyMarker = visual.InverseTransformDirection(motion.transform.forward);
            Vector3 rotorMarker = rotor.InverseTransformDirection(motion.transform.forward);
            float previousBody = Heading(visual.TransformDirection(bodyMarker));
            float previousRotor = Heading(rotor.TransformDirection(rotorMarker));
            var result = new YawTrace();
            int count = Mathf.CeilToInt(duration / Dt);
            for (int frame = 0; frame < count; frame++)
            {
                float dt = Mathf.Min(Dt, duration - frame * Dt);
                if (dt <= .000001f) break;
                if (flightDuration > 0) motion.SetFlightProgress(Mathf.Clamp01((frame * Dt + dt) / flightDuration));
                motion.SimulateSprings(dt);
                float body = Heading(visual.TransformDirection(bodyMarker)), value = Heading(rotor.TransformDirection(rotorMarker));
                float change = Mathf.DeltaAngle(previousRotor, value);
                result.body += Mathf.DeltaAngle(previousBody, body); result.rotor += change;
                result.maximumRate = Mathf.Max(result.maximumRate, Mathf.Abs(change) / dt);
                previousBody = body; previousRotor = value;
                if (frame % 20 == 0 || frame == count - 1) skin.CheckGeometry(context + " frame " + frame);
            }
            return result;
        }

        private static float Heading(Vector3 direction)
        {
            Require(Finite(direction.x) && Finite(direction.z) && direction.x * direction.x + direction.z * direction.z > .01f,
                "Rotating assembly lost a finite horizontal heading.");
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        private sealed class SkinSamples : IDisposable
        {
            private sealed class Skin
            {
                public SkinnedMeshRenderer renderer;
                public Mesh scratch;
                public Vector3[] rest;
                public int[][] tips;
                public Vector2Int[] edges;
            }
            private readonly Transform actor;
            private readonly Skin[] skins;
            public float Height { get; }
            public float MaximumStrain { get; private set; } = 1;
            public SkinSamples(GameObject subject, CourierRigBinding binding, Transform[] tips)
            {
                actor = subject.transform;
                var renderers = subject.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                Require(renderers.Length > 0, "Accessory checks require actual imported skins.");
                Height = CourierVisualBounds.MeasureBody(renderers).size.y;
                var accessory = new HashSet<Transform>((binding.antennaChain ?? Array.Empty<Transform>()).Concat(binding.scarfChain ?? Array.Empty<Transform>())) { binding.antennaYawPivot };
                skins = new Skin[renderers.Length];
                for (int index = 0; index < renderers.Length; index++)
                {
                    var renderer = renderers[index]; var mesh = renderer.sharedMesh;
                    var item = new Skin { renderer = renderer, scratch = new Mesh { name = "Owned accessory skin sample" }, tips = new int[tips.Length][] };
                    skins[index] = item;
                    item.rest = Bake(item);
                    var bones = renderer.bones;
                    var counts = mesh.GetBonesPerVertex(); var weights = mesh.GetAllBoneWeights();
                    var selected = new bool[mesh.vertexCount];
                    var tipSamples = tips.Select(_ => new List<int>()).ToArray(); int at = 0;
                    for (int vertex = 0; vertex < counts.Length; vertex++)
                    {
                        float accessoryWeight = 0;
                        for (int influence = 0; influence < counts[vertex]; influence++)
                        {
                            var weight = weights[at++]; var bone = bones[weight.boneIndex];
                            if (accessory.Contains(bone)) accessoryWeight += weight.weight;
                            for (int t = 0; t < tips.Length; t++) if (bone == tips[t] && weight.weight >= .5f) tipSamples[t].Add(vertex);
                        }
                        selected[vertex] = accessoryWeight > .05f;
                    }
                    for (int t = 0; t < tips.Length; t++) item.tips[t] = tipSamples[t].ToArray();
                    var edges = new HashSet<ulong>();
                    foreach (int[] triangle in Enumerable.Range(0, mesh.subMeshCount).Select(sub => mesh.GetTriangles(sub)))
                        for (int t = 0; t < triangle.Length; t += 3)
                            for (int e = 0; e < 3; e++)
                            {
                                int a = triangle[t + e], b = triangle[t + (e + 1) % 3];
                                if (!selected[a] && !selected[b]) continue;
                                int first = Mathf.Min(a, b), second = Mathf.Max(a, b);
                                if ((item.rest[first] - item.rest[second]).magnitude < Height * .0001f) continue;
                                edges.Add(((ulong)(uint)first << 32) | (uint)second);
                            }
                    item.edges = edges.Select(value => new Vector2Int((int)(value >> 32), (int)(value & uint.MaxValue))).ToArray();
                }
                for (int t = 0; t < tips.Length; t++) Require(skins.Sum(item => item.tips[t].Length) >= 3,
                    "Scarf tip bone does not control actual painted geometry: " + tips[t].name);
            }

            private Vector3[] Bake(Skin skin)
            {
                skin.renderer.BakeMesh(skin.scratch, true);
                var values = skin.scratch.vertices;
                for (int i = 0; i < values.Length; i++) values[i] = actor.InverseTransformPoint(skin.renderer.transform.TransformPoint(values[i]));
                return values;
            }
            public Vector3[] AllVertices() => skins.SelectMany(Bake).ToArray();
            public Vector3[] TipCentroids(Transform visual)
            {
                int count = skins[0].tips.Length; var sums = new Vector3[count]; var counts = new int[count];
                foreach (var item in skins)
                {
                    var values = Bake(item);
                    for (int t = 0; t < count; t++) foreach (int vertex in item.tips[t])
                    { sums[t] += visual.InverseTransformPoint(actor.TransformPoint(values[vertex])); counts[t]++; }
                }
                for (int t = 0; t < count; t++) { Require(counts[t] > 0, "A scarf has no weighted tip samples."); sums[t] /= counts[t]; }
                return sums;
            }
            public void CheckGeometry(string context)
            {
                foreach (var item in skins)
                {
                    var values = Bake(item);
                    foreach (var point in values) Require(Finite(point.x) && Finite(point.y) && Finite(point.z) && point.magnitude < Height * 8,
                        context + ": deformed vertex is non-finite or escaped the character.");
                    foreach (var edge in item.edges)
                    {
                        float ratio = (values[edge.x] - values[edge.y]).magnitude / (item.rest[edge.x] - item.rest[edge.y]).magnitude;
                        MaximumStrain = Mathf.Max(MaximumStrain, ratio);
                        Require(ratio < 10, context + ": accessory edge stretched into a tear; vertices=" + edge.x + "/" + edge.y + ", ratio=" + Number(ratio));
                    }
                }
            }
            public void Dispose() { foreach (var item in skins) if (item?.scratch != null) UnityEngine.Object.DestroyImmediate(item.scratch); }
        }

        private struct LocalPose { public Vector3 position, scale; public Quaternion rotation; }
        private static LocalPose[] Snapshot(Transform[] nodes) => nodes.Select(node => new LocalPose { position = node.localPosition, rotation = node.localRotation, scale = node.localScale }).ToArray();
        private static void Same(LocalPose[] before, Transform[] nodes, string context)
        {
            for (int i = 0; i < nodes.Length; i++) Require((nodes[i].localPosition - before[i].position).sqrMagnitude < .00000001f
                && (nodes[i].localScale - before[i].scale).sqrMagnitude < .00000001f && Quaternion.Angle(nodes[i].localRotation, before[i].rotation) < .025f,
                context + ": changed " + nodes[i].name);
        }
        private static Transform[] ChainRoots(Transform[] chain)
        {
            var set = new HashSet<Transform>(chain ?? Array.Empty<Transform>());
            return set.Where(node => node != null && !set.Contains(node.parent)).ToArray();
        }
        private static Transform[] ChainTips(Transform[] chain)
        {
            var values = chain ?? Array.Empty<Transform>();
            return values.Where(node => node != null && !values.Any(child => child != null && child.parent == node)).ToArray();
        }
        private static float MaxDistance(Vector3[] first, Vector3[] second)
        {
            Require(first.Length == second.Length, "A geometry sample changed topology.");
            float value = 0; for (int i = 0; i < first.Length; i++) value = Mathf.Max(value, Vector3.Distance(first[i], second[i])); return value;
        }
        private static void Step(CourierMotion motion, float seconds)
        { while (seconds > .000001f) { float dt = Mathf.Min(Dt, seconds); motion.SimulateSprings(dt); seconds -= dt; } }
        private static string Number(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("RADIO_ACCESSORIES FAIL: " + message); }
    }
}
