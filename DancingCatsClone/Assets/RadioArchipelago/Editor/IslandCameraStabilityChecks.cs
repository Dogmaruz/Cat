using System;
using System.IO;
using System.Reflection;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>A stationary route must not make the camera follow the courier's animated culling bounds.</summary>
    public static class IslandCameraStabilityChecks
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float PositionTolerance = .01f;

        [Serializable] private sealed class Sample
        {
            public int holdDegrees;
            public Vector3 scale, boundsExtents, cameraPosition;
            public float translation, rotationDegrees, fieldOfViewDelta;
        }

        [Serializable] private sealed class CarrierSample
        {
            public int step;
            public Vector3 carrierPosition, cameraPosition;
            public float translation;
        }

        [Serializable] private sealed class Report
        {
            public bool passed;
            public string failure;
            public int frames;
            public float aspect = 590f / 1280f, positionTolerance = PositionTolerance;
            public float maximumTranslation, maximumRotationDegrees, maximumFieldOfViewDelta, maximumBoundsChange;
            public Vector3 referenceCameraPosition;
            public Quaternion referenceCameraRotation;
            public float referenceFieldOfView;
            public Sample[] samples = new Sample[360];
            public float maxCarrierTranslation, carrierPositionTolerance = .001f;
            public Vector3 carrierReferenceCameraPosition;
            public CarrierSample[] carrierSamples = new CarrierSample[60];
        }

        [MenuItem("Radio Archipelago/Island pilot/Check camera animation stability")]
        public static void RunChecks()
        {
            var report = new Report();
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Run camera stability checks in Edit Mode.");
                using (var fixture = new Fixture())
                {
                    // Warm up fitting at a fixed animation pose before measuring animation-induced drift.
                    for (int i = 0; i < 4; i++) fixture.Tick();
                    report.referenceCameraPosition = fixture.Camera.transform.position;
                    report.referenceCameraRotation = fixture.Camera.transform.rotation;
                    report.referenceFieldOfView = fixture.Camera.fieldOfView;
                    Vector3 initialExtents = fixture.Courier.bounds.extents;
                    for (int frame = 1; frame <= 360; frame++)
                    {
                        float phase = frame * Mathf.Deg2Rad;
                        fixture.Actor.localRotation = Quaternion.Euler(0, frame, 0);
                        fixture.Actor.localScale = .64f * new Vector3(1 + .08f * Mathf.Sin(phase * 2),
                            1 - .12f * Mathf.Sin(phase * 2), 1 + .06f * Mathf.Sin(phase * 2));
                        fixture.Tick();
                        var sample = new Sample
                        {
                            holdDegrees = frame,
                            scale = fixture.Actor.localScale,
                            boundsExtents = fixture.Courier.bounds.extents,
                            cameraPosition = fixture.Camera.transform.position,
                            translation = Vector3.Distance(report.referenceCameraPosition, fixture.Camera.transform.position),
                            rotationDegrees = Quaternion.Angle(report.referenceCameraRotation, fixture.Camera.transform.rotation),
                            fieldOfViewDelta = Mathf.Abs(report.referenceFieldOfView - fixture.Camera.fieldOfView)
                        };
                        report.samples[frame - 1] = sample;
                        report.frames++;
                        report.maximumTranslation = Mathf.Max(report.maximumTranslation, sample.translation);
                        report.maximumRotationDegrees = Mathf.Max(report.maximumRotationDegrees, sample.rotationDegrees);
                        report.maximumFieldOfViewDelta = Mathf.Max(report.maximumFieldOfViewDelta, sample.fieldOfViewDelta);
                        report.maximumBoundsChange = Mathf.Max(report.maximumBoundsChange, Vector3.Distance(initialExtents, sample.boundsExtents));
                    }
                    // Native finish animation moves the camera's parent railway. The island camera
                    // must retain its own settled world pose rather than damping from that inherited motion.
                    for (int i = 0; i < 4; i++) fixture.Tick();
                    report.carrierReferenceCameraPosition = fixture.Camera.transform.position;
                    for (int step = 0; step < report.carrierSamples.Length; step++)
                    {
                        fixture.Carrier.position += Vector3.forward * .1f;
                        fixture.Tick(false);
                        var sample = new CarrierSample
                        {
                            step = step + 1,
                            carrierPosition = fixture.Carrier.position,
                            cameraPosition = fixture.Camera.transform.position,
                            translation = Vector3.Distance(report.carrierReferenceCameraPosition, fixture.Camera.transform.position)
                        };
                        report.carrierSamples[step] = sample;
                        report.maxCarrierTranslation = Mathf.Max(report.maxCarrierTranslation, sample.translation);
                    }
                }
                Require(report.maximumBoundsChange > .1f, "Fixture did not exercise rotating and squashing renderer bounds.");
                Require(report.maximumTranslation <= PositionTolerance && report.maximumRotationDegrees <= .01f &&
                    report.maximumFieldOfViewDelta <= .001f,
                    "Stationary-route camera moved with courier animation: translation=" + report.maximumTranslation.ToString("F5") +
                    "m (limit .01m), rotation=" + report.maximumRotationDegrees.ToString("F5") +
                    " degrees, FOV delta=" + report.maximumFieldOfViewDelta.ToString("F5") + ".");
                Require(report.maxCarrierTranslation <= report.carrierPositionTolerance,
                    "Stationary-route camera inherited movement from its parent carrier: translation=" +
                    report.maxCarrierTranslation.ToString("F5") + "m (limit .001m).");
                report.passed = true;
                Debug.Log("ISLAND_CAMERA_STABILITY_PASS frames=" + report.frames + " maximumTranslation=" + report.maximumTranslation +
                    " maxCarrierTranslation=" + report.maxCarrierTranslation);
            }
            catch (Exception error)
            {
                report.failure = error.ToString();
                throw;
            }
            finally
            {
                string reviewPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/IslandPilotReview"));
                Directory.CreateDirectory(reviewPath);
                File.WriteAllText(Path.Combine(reviewPath, "camera-stability-checks.json"), JsonUtility.ToJson(report, true));
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene;
            private readonly IslandStagePresentation stage;
            private readonly IslandStageDefinition definition;
            private readonly Mesh mesh;
            private readonly Vector4 savedClearViewCamera;
            internal readonly Camera Camera;
            internal readonly Transform Actor, Carrier;
            internal readonly MeshRenderer Courier;

            internal Fixture()
            {
                savedClearViewCamera = Shader.GetGlobalVector("_RA_ClearViewCamera");
                scene = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("Camera animation stability check") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                stage = root.AddComponent<IslandStagePresentation>();
                Carrier = Child(root.transform, "Native camera parent carrier");
                Camera = Child(Carrier, "Disabled camera").gameObject.AddComponent<Camera>();
                Camera.enabled = false; Camera.aspect = 590f / 1280f; Camera.fieldOfView = 48;
                Camera.nearClipPlane = .08f;
                definition = ScriptableObject.CreateInstance<IslandStageDefinition>();
                definition.hideFlags = HideFlags.HideAndDontSave;
                definition.CameraFieldOfView = 48;
                var anchor = Child(root.transform, "Stationary actor anchor");
                anchor.position = new Vector3(0, 1, 0);
                Actor = Child(anchor, "Animated courier");
                Actor.localScale = Vector3.one * .64f;
                mesh = new Mesh { name = "Courier with padded animation culling bounds", hideFlags = HideFlags.HideAndDontSave };
                mesh.vertices = new[]
                {
                    new Vector3(-.4f, 0, -.3f), new Vector3(.4f, 0, -.3f), new Vector3(.4f, 1.4f, -.3f), new Vector3(-.4f, 1.4f, -.3f),
                    new Vector3(-.4f, 0, .3f), new Vector3(.4f, 0, .3f), new Vector3(.4f, 1.4f, .3f), new Vector3(-.4f, 1.4f, .3f)
                };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2, 0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5 };
                // Imported courier bounds include animation padding; world AABB size changes during Hold rotation.
                mesh.bounds = new Bounds(new Vector3(0, 1.605f, 0), new Vector3(4.986f, 3.21f, 4.986f));
                Actor.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                Courier = Actor.gameObject.AddComponent<MeshRenderer>();
                Set(stage, "stageCamera", Camera); Set(stage, "definition", definition); Set(stage, "actorAnchor", anchor);
                Set(stage, "courierRenderers", new Renderer[] { Courier });
                Vector3 desiredPosition = new Vector3(0, 7.8f, -11.5f);
                Quaternion desiredRotation = Quaternion.LookRotation(new Vector3(0, 1, 8.5f) - desiredPosition, Vector3.up);
                Camera.transform.SetPositionAndRotation(desiredPosition, desiredRotation);
                Set(stage, "desiredCameraPosition", desiredPosition); Set(stage, "desiredCameraRotation", desiredRotation);
                Set(stage, "cameraReady", true);
                var envelope = (Vector3[])typeof(IslandStagePresentation).GetField("cameraEnvelope", Members).GetValue(stage);
                int count = 0;
                // Fixed straight-route volume, matching the authored silhouette and both preview lanes.
                for (int side = -1; side <= 1; side += 2)
                    for (int front = -1; front <= 1; front += 2)
                        for (int top = 0; top <= 1; top++)
                            envelope[count++] = anchor.position + new Vector3(side * .85f, top == 0 ? -.1f : 2.6f, front * .65f);
                for (int sample = 0; sample <= 7; sample++)
                    for (int side = -1; side <= 1; side += 2)
                        envelope[count++] = new Vector3(side * 2.2f, 1, 12.5f * sample / 7);
                Set(stage, "cameraEnvelopeCount", count);
            }

            internal void Tick(bool immediate = true)
            {
                Set(stage, "cameraPoseRequested", true);
                try { typeof(IslandStagePresentation).GetMethod("ApplyCamera", Members).Invoke(stage, new object[] { immediate }); }
                catch (TargetInvocationException error) { throw error.InnerException ?? error; }
            }

            public void Dispose()
            {
                try
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                    UnityEngine.Object.DestroyImmediate(mesh);
                    UnityEngine.Object.DestroyImmediate(definition);
                }
                finally { Shader.SetGlobalVector("_RA_ClearViewCamera", savedClearViewCamera); }
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }.transform;
            child.SetParent(parent, false); return child;
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
