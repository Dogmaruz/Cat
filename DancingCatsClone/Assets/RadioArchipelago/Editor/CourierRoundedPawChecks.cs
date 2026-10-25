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
    /// <summary>Checks the requested double-rounded shape on the actual imported paw surface, not just bone motion.</summary>
    public static class CourierRoundedPawChecks
    {
        [Serializable] private sealed class Result
        {
            public string courier, shoulder;
            public int points;
            public float length, middleRadius, lowerTipRadiusRatio, upperTipRadiusRatio, mirrorMean, mirror95;
            public bool roundedShape;
        }
        [Serializable] private sealed class Report { public Result[] paws; }

        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Check rounded paws in Edit Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            if (catalog == null || catalog.entries == null || catalog.entries.Length != 3)
                throw new InvalidOperationException("Three imported couriers are required for six-paw validation.");
            var scene = EditorSceneManager.NewPreviewScene(); var results = new List<Result>();
            try
            {
                foreach (var entry in catalog.entries)
                {
                    var actor = UnityEngine.Object.Instantiate(entry.prefab); SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        var motion = actor.GetComponentInChildren<CourierMotion>(); motion.ManualSimulation = true; motion.ResetMotion();
                        var rig = (CourierRigBinding)typeof(CourierMotion).GetField("rig", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(motion);
                        foreach (var arm in new[] { rig.leftArm, rig.rightArm }) results.Add(Measure(entry.id, arm, Points(actor, rig.visualPivot, arm)));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
                string folder = Path.GetFullPath("Library/RadioRigging/SmoothCourierReview/Unity"); Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "paw-shape-checks.json"), JsonUtility.ToJson(new Report { paws = results.ToArray() }, true));
                if (results.Count != 6) throw new InvalidOperationException("Six imported paws are required.");
                var failures = results.Where(result => !result.roundedShape).ToArray();
                if (failures.Length > 0) throw new InvalidOperationException("ROUNDED_PAWS FAIL: asymmetric or flat-ended imported paw surface: "
                    + string.Join("; ", failures.Select(result => result.courier + "/" + result.shoulder + " mirror95=" + result.mirror95.ToString("F4")
                        + ", tip radii=" + result.lowerTipRadiusRatio.ToString("F3") + "/" + result.upperTipRadiusRatio.ToString("F3"))));
                Debug.Log("ROUNDED_PAWS PASS: all six actual paw surfaces have matching rounded ends and elongated proportions. Visual surface/strap review is still required.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Vector3[] Points(GameObject actor, Transform frame, Transform arm)
        {
            var result = new Dictionary<Vector3Int, Vector3>();
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var scratch = new Mesh();
                try
                {
                    skin.BakeMesh(scratch, true); var vertices = scratch.vertices;
                    var counts = skin.sharedMesh.GetBonesPerVertex(); var weights = skin.sharedMesh.GetAllBoneWeights();
                    int at = 0;
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        float influence = 0;
                        for (int count = 0; count < counts[vertex]; count++)
                        {
                            var weight = weights[at++]; if (skin.bones[weight.boneIndex] == arm) influence += weight.weight;
                        }
                        if (influence < .999f) continue;
                        Vector3 point = frame.InverseTransformPoint(skin.transform.TransformPoint(vertices[vertex]));
                        var key = new Vector3Int(Mathf.RoundToInt(point.x * 1000000), Mathf.RoundToInt(point.y * 1000000), Mathf.RoundToInt(point.z * 1000000));
                        if (!result.ContainsKey(key)) result.Add(key, point);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(scratch); }
            }
            if (result.Count < 24) throw new InvalidDataException("A rounded paw needs a meaningful imported surface: " + arm.name);
            return result.Values.ToArray();
        }

        private static Result Measure(string courier, Transform arm, Vector3[] points)
        {
            Vector3 mean = Vector3.zero; foreach (var point in points) mean += point; mean /= points.Length;
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
            Vector3 axis = bounds.size.x > bounds.size.y && bounds.size.x > bounds.size.z ? Vector3.right
                : bounds.size.y > bounds.size.z ? Vector3.up : Vector3.forward;
            var covariance = new float[3, 3];
            foreach (var point in points)
            {
                Vector3 delta = point - mean;
                for (int row = 0; row < 3; row++) for (int column = 0; column < 3; column++) covariance[row, column] += delta[row] * delta[column];
            }
            for (int iteration = 0; iteration < 40; iteration++)
            {
                var next = new Vector3();
                for (int row = 0; row < 3; row++) for (int column = 0; column < 3; column++) next[row] += covariance[row, column] * axis[column];
                axis = next.normalized;
            }
            var along = points.Select(point => Vector3.Dot(point - mean, axis)).ToArray();
            float first = along.Min(), last = along.Max(), length = last - first, middle = (first + last) * .5f;
            Vector3 center = mean + axis * middle;
            float radius = 0, lower = 0, upper = 0;
            var errors = new float[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                float position = along[index] - middle;
                Vector3 radial = points[index] - center - axis * position;
                if (Mathf.Abs(position) <= length * .2f) radius = Mathf.Max(radius, radial.magnitude);
                if (along[index] <= first + length * .06f) lower = Mathf.Max(lower, radial.magnitude);
                if (along[index] >= last - length * .06f) upper = Mathf.Max(upper, radial.magnitude);
                Vector3 reflected = points[index] - axis * (2 * position); float nearest = float.PositiveInfinity;
                foreach (var other in points) nearest = Mathf.Min(nearest, (reflected - other).sqrMagnitude);
                errors[index] = Mathf.Sqrt(nearest) / Mathf.Max(.000001f, length);
            }
            Array.Sort(errors);
            var result = new Result { courier = courier, shoulder = arm.name, points = points.Length, length = length, middleRadius = radius,
                lowerTipRadiusRatio = lower / Mathf.Max(.000001f, radius), upperTipRadiusRatio = upper / Mathf.Max(.000001f, radius),
                mirrorMean = errors.Average(), mirror95 = errors[Mathf.Min(errors.Length - 1, (int)(errors.Length * .95f))] };
            result.roundedShape = radius > .0001f && length > radius * 2.5f && result.mirrorMean < .008f && result.mirror95 < .02f
                && result.lowerTipRadiusRatio < .85f && result.upperTipRadiusRatio < .85f;
            return result;
        }
    }
}
