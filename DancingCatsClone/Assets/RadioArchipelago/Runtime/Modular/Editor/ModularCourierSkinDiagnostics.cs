#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    public static class ModularCourierSkinDiagnostics
    {
        [Serializable] private sealed class Evidence { public Sample[] samples; }
        [Serializable] private sealed class Sample
        {
            public string hero, pose, skin, sourceIndexFormat, bakedIndexFormat;
            public int sourceVertices, bakedVertices, sourceIndices, bakedIndices, sourceMaxIndex, bakedMaxIndex, nonfinite;
            public float cpuBakeTrueMaxError, cpuBakeFalseMaxError, scaleTrueToFalseMaxDifference;
            public Vector3 sourceLossyScale, visualScale, footLeft, footRight;
            public Bounds cpuBounds, bakedBounds, rendererBounds, sourceMeshBounds;
            public int lowerRightVertices, lowerRightBadVertices, cpuDegenerateTriangles, bakedDegenerateTriangles;
        }

        public static void Run()
        {
            string folder = Path.GetFullPath("../Docs/Design/RadioArchipelago/ModularCast20261104/SkinDiagnostics");
            Directory.CreateDirectory(folder);
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraNode = new GameObject("Owned skin diagnosis camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraNode, scene);
            var camera = cameraNode.GetComponent<Camera>(); camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 2.05f; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.89f, .88f, .82f); camera.nearClipPlane = .01f; camera.farClipPlane = 40;
            camera.transform.position = new Vector3(0, 1.55f, 6); camera.transform.LookAt(new Vector3(0, 1.45f, 0));
            var lightNode = new GameObject("Owned diagnosis key", typeof(Light)); SceneManager.MoveGameObjectToScene(lightNode, scene);
            lightNode.transform.rotation = Quaternion.Euler(35, -25, 0); var light = lightNode.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f;
            var light2Node = new GameObject("Owned diagnosis rim", typeof(Light)); SceneManager.MoveGameObjectToScene(light2Node, scene);
            light2Node.transform.rotation = Quaternion.Euler(25, 145, 0); var light2 = light2Node.GetComponent<Light>(); light2.type = LightType.Directional; light2.intensity = .9f;
            var target = RenderTexture.GetTemporary(640, 800, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target;
            var all = new List<Sample>();
            try
            {
                foreach (var hero in catalog.entries.Take(3))
                {
                    var actor = UnityEngine.Object.Instantiate(hero.prefab); SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                        var motion = actor.GetComponent<CourierMotion>(); motion.ManualSimulation = true;
                        wardrobe.Apply(catalog.FindEquipment(hero.defaultBackpackId, CourierEquipmentKind.Backpack), null,
                            catalog.FindEquipment(hero.defaultScarfId, CourierEquipmentKind.Scarf));
                        var binding = wardrobe.bodyRig;
                        Vector3 restVisual = binding.visualPivot.localScale;
                        Vector3 restFootL = binding.leftFoot.localPosition, restFootR = binding.rightFoot.localPosition;
                        foreach (string pose in new[] { "Reset", "Idle08", "Idle08NoScale", "Idle08NoFeet", "FlightHalf" })
                        {
                            motion.ResetMotion();
                            if (pose.StartsWith("Idle", StringComparison.Ordinal)) { motion.PlayIdle(); motion.SimulateSprings(.08f); }
                            if (pose == "Idle08NoScale") binding.visualPivot.localScale = restVisual;
                            if (pose == "Idle08NoFeet") { binding.leftFoot.localPosition = restFootL; binding.rightFoot.localPosition = restFootR; }
                            if (pose == "FlightHalf")
                            {
                                motion.PlayJump(1.4f);
                                for (int i = 0; i < 30; i++) { motion.SetFlightProgress(i / 60f); motion.SimulateSprings(1f / 60); }
                            }
                            var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                            foreach (var skin in skins) all.Add(Measure(hero.assetId, pose, skin, binding));
                            using (var bake = new CourierPreviewSkinBake(actor))
                            {
                                bake.Refresh(); camera.Render(); Save(camera, folder + "/" + hero.assetId + "_" + pose + "_baked.png");
                            }
                            RenderIndependent(camera, actor, scene, folder + "/" + hero.assetId + "_" + pose + "_cpu.png");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
                File.WriteAllText(folder + "/report.json", JsonUtility.ToJson(new Evidence { samples = all.ToArray() }, true));
                Debug.Log("MODULAR_SKIN_DIAGNOSTICS COMPLETE: " + all.Count + " skin/pose samples, independent CPU comparison and paired native renders. " + folder);
            }
            finally { camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Sample Measure(string hero, string pose, SkinnedMeshRenderer skin, CourierRigBinding binding)
        {
            var source = skin.sharedMesh;
            var cpu = Independent(skin);
            var bakeTrue = new Mesh { indexFormat = source.indexFormat };
            var bakeFalse = new Mesh { indexFormat = source.indexFormat };
            try
            {
                skin.BakeMesh(bakeTrue, true); skin.BakeMesh(bakeFalse, false);
                var a = bakeTrue.vertices; var b = bakeFalse.vertices;
                var data = new Sample
                {
                    hero = hero, pose = pose, skin = skin.name, sourceVertices = source.vertexCount, bakedVertices = bakeTrue.vertexCount,
                    sourceIndexFormat = source.indexFormat.ToString(), bakedIndexFormat = bakeTrue.indexFormat.ToString(),
                    sourceIndices = source.triangles.Length, bakedIndices = bakeTrue.triangles.Length,
                    sourceMaxIndex = source.triangles.Max(), bakedMaxIndex = bakeTrue.triangles.Max(),
                    sourceLossyScale = skin.transform.lossyScale, visualScale = binding.visualPivot.localScale,
                    footLeft = binding.leftFoot.localPosition, footRight = binding.rightFoot.localPosition,
                    rendererBounds = skin.bounds, sourceMeshBounds = source.bounds,
                    cpuBounds = BoundsOf(cpu), bakedBounds = BoundsOf(a.Select(skin.transform.TransformPoint).ToArray()),
                    cpuDegenerateTriangles = Degenerates(cpu, source.triangles),
                    bakedDegenerateTriangles = Degenerates(a, bakeTrue.triangles)
                };
                for (int i = 0; i < a.Length; i++)
                {
                    Vector3 trueWorld = skin.transform.TransformPoint(a[i]);
                    Vector3 falseWorld = skin.transform.TransformPoint(b[i]);
                    float error = Vector3.Distance(trueWorld, cpu[i]);
                    data.cpuBakeTrueMaxError = Mathf.Max(data.cpuBakeTrueMaxError, error);
                    data.cpuBakeFalseMaxError = Mathf.Max(data.cpuBakeFalseMaxError, Vector3.Distance(falseWorld, cpu[i]));
                    data.scaleTrueToFalseMaxDifference = Mathf.Max(data.scaleTrueToFalseMaxDifference, Vector3.Distance(a[i], b[i]));
                    if (float.IsNaN(a[i].sqrMagnitude) || float.IsInfinity(a[i].sqrMagnitude)) data.nonfinite++;
                    if (cpu[i].x > data.cpuBounds.center.x && cpu[i].y < data.cpuBounds.min.y + data.cpuBounds.size.y * .30f)
                    { data.lowerRightVertices++; if (error > .001f) data.lowerRightBadVertices++; }
                }
                return data;
            }
            finally { UnityEngine.Object.DestroyImmediate(bakeTrue); UnityEngine.Object.DestroyImmediate(bakeFalse); }
        }

        private static Vector3[] Independent(SkinnedMeshRenderer skin)
        {
            var mesh = skin.sharedMesh;
            var points = mesh.vertices; var bindposes = mesh.bindposes; var bones = skin.bones;
            var weights = mesh.GetAllBoneWeights(); var counts = mesh.GetBonesPerVertex();
            var matrices = new Matrix4x4[bones.Length];
            for (int i = 0; i < matrices.Length; i++) matrices[i] = bones[i].localToWorldMatrix * bindposes[i];
            var result = new Vector3[points.Length]; int cursor = 0;
            for (int i = 0; i < result.Length; i++)
                for (int j = 0; j < counts[i]; j++)
                { var influence = weights[cursor++]; result[i] += matrices[influence.boneIndex].MultiplyPoint3x4(points[i]) * influence.weight; }
            return result;
        }

        private static void RenderIndependent(Camera camera, GameObject actor, Scene scene, string output)
        {
            var proxies = new List<GameObject>(); var meshes = new List<Mesh>();
            var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(false).Where(x => x.enabled).ToArray();
            try
            {
                foreach (var skin in skins)
                {
                    var node = new GameObject("Owned independent CPU skin", typeof(MeshFilter), typeof(MeshRenderer)); SceneManager.MoveGameObjectToScene(node, scene);
                    proxies.Add(node);
                    var mesh = UnityEngine.Object.Instantiate(skin.sharedMesh); meshes.Add(mesh);
                    mesh.indexFormat = IndexFormat.UInt32;
                    mesh.vertices = Independent(skin); mesh.triangles = skin.sharedMesh.triangles;
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    node.GetComponent<MeshFilter>().sharedMesh = mesh; node.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                }
                camera.Render(); Save(camera, output);
            }
            finally
            {
                foreach (var skin in skins) skin.enabled = true;
                foreach (var node in proxies) UnityEngine.Object.DestroyImmediate(node);
                foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static void Save(Camera camera, string output)
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = camera.targetTexture; texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply();
                File.WriteAllBytes(output, texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        }
        private static Bounds BoundsOf(Vector3[] values)
        { var bounds = new Bounds(values[0], Vector3.zero); foreach (var point in values) bounds.Encapsulate(point); return bounds; }
        private static int Degenerates(Vector3[] points, int[] triangles)
        { int count = 0; for (int i = 0; i < triangles.Length; i += 3) if (Vector3.Cross(points[triangles[i + 1]] - points[triangles[i]], points[triangles[i + 2]] - points[triangles[i]]).sqrMagnitude < 1e-16f) count++; return count; }
    }
}
#endif
