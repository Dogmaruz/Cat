using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Actual URP color/depth rendering of a disposable foreground wall and rear actor surrogate.</summary>
    public static class IslandOcclusionChecks
    {
        private const int Size = 128, Layer = 29;
        private static readonly int CameraId = Shader.PropertyToID("_RA_ClearViewCamera"), ActorId = Shader.PropertyToID("_RA_ClearViewActor"),
            LandingId = Shader.PropertyToID("_RA_ClearViewLanding"), FloorId = Shader.PropertyToID("_RA_ClearViewFloor");

        [Serializable] private sealed class Report
        {
            public string runId, unityVersion, pipeline, details, beforePng, afterPng, floorPng, otherCameraPng;
            public bool passed, baselineBlocksActor, enabledRevealsActor, floorPreservesWall, otherCameraPreservesWall, globalsRestored;
            public bool elevatedGroundPreservesWall;
            public int width = Size, height = Size, baselineRedPixels, clearedRedPixels, floorRedPixels, otherCameraRedPixels,
                baselineGreenPixels, floorChangedPixels, otherCameraChangedPixels;
            public Color baselineCenter, clearedCenter, floorCenter, otherCameraCenter;
            public Vector3 cameraPosition, actorPosition;
            public float actorRadius = 1.5f, protectedFloor = .35f;
        }

        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run occlusion checks in Edit Mode.");
            var previousCamera = Shader.GetGlobalVector(CameraId); var previousActor = Shader.GetGlobalVector(ActorId);
            var previousLanding = Shader.GetGlobalVector(LandingId); float previousFloor = Shader.GetGlobalFloat(FloorId);
            var previousTarget = RenderTexture.active;
            string runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string folder = Path.GetFullPath(Path.Combine("Library/IslandPilotReview/OcclusionChecks", runId));
            Directory.CreateDirectory(folder);
            var report = new Report { runId = runId, unityVersion = Application.unityVersion,
                pipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "none",
                beforePng = Path.Combine(folder, "before.png"), afterPng = Path.Combine(folder, "after.png"),
                floorPng = Path.Combine(folder, "floor-protected.png"), otherCameraPng = Path.Combine(folder, "other-camera.png") };
            Scene scene = default;
            Camera camera = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            Mesh whiteCube = null;
            Material wallMaterial = null, actorMaterial = null;
            try
            {
                Require(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "Occlusion checks require the project's active URP renderer.");
                var islandShader = Shader.Find("RadioArchipelago/Island Surface");
                var actorShader = Shader.Find("Universal Render Pipeline/Unlit");
                Require(islandShader != null && actorShader != null, "The real island and URP Unlit shaders must be available.");
                scene = EditorSceneManager.NewPreviewScene();
                camera = Node(scene, "Occlusion check camera").AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.cullingMask = 1 << Layer;
                camera.transform.position = new Vector3(0, 2, -6); camera.transform.rotation = Quaternion.identity;
                camera.fieldOfView = 40; camera.aspect = 1; camera.nearClipPlane = .1f; camera.farClipPlane = 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.allowHDR = false; camera.allowMSAA = false; camera.useOcclusionCulling = false;
                var cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing = false; cameraData.renderShadows = false; cameraData.volumeLayerMask = 0;
                cameraData.requiresDepthTexture = false; cameraData.requiresColorTexture = false;
                cameraData.antialiasing = AntialiasingMode.None; cameraData.allowXRRendering = false;
                var light = Node(scene, "Occlusion check sunlight").AddComponent<Light>();
                light.type = LightType.Directional; light.color = Color.white; light.intensity = 1.5f; light.shadows = LightShadows.None;
                light.cullingMask = 1 << Layer; light.transform.rotation = Quaternion.Euler(25, 0, 0);
                wallMaterial = new Material(islandShader) { name = "Disposable green island wall" };
                wallMaterial.SetColor("_BaseColor", Color.green); wallMaterial.SetFloat("_Metallic", 0); wallMaterial.SetFloat("_Smoothness", 0);
                wallMaterial.SetFloat("_DetailStrength", 0); wallMaterial.SetFloat("_Road", 0);
                actorMaterial = new Material(actorShader) { name = "Disposable red actor surrogate" };
                actorMaterial.SetColor("_BaseColor", Color.red);
                var wall = Cube(scene, "Foreground island wall", new Vector3(0, 2, -1), new Vector3(4, 4, .8f), wallMaterial);
                whiteCube = UnityEngine.Object.Instantiate(wall.GetComponent<MeshFilter>().sharedMesh); whiteCube.name = "Disposable white vertex-color cube";
                var colors = new Color[whiteCube.vertexCount]; for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                whiteCube.colors = colors; wall.GetComponent<MeshFilter>().sharedMesh = whiteCube;
                var actor = Cube(scene, "Rear actor surrogate", new Vector3(0, 2, 2), Vector3.one, actorMaterial);
                report.cameraPosition = camera.transform.position; report.actorPosition = actor.transform.position;
                target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = "Disposable occlusion check target", antiAliasing = 1 };
                target.Create();
                pixels = new Texture2D(Size, Size, TextureFormat.RGBA32, false);

                SetGlobals(report, false, .35f, false);
                var before = Capture(camera, target, pixels, report.beforePng);
                SetGlobals(report, true, .35f, false);
                var after = Capture(camera, target, pixels, report.afterPng);
                SetGlobals(report, true, 100, false);
                var floor = Capture(camera, target, pixels, report.floorPng);
                SetGlobals(report, true, .35f, true);
                var other = Capture(camera, target, pixels, report.otherCameraPng);
                SetGlobals(report, true, .35f, false);
                wallMaterial.SetFloat("_ProtectGameplaySurface", 1);
                var raisedGround = Capture(camera, target, pixels, Path.Combine(folder, "elevated-ground.png"));
                wallMaterial.SetFloat("_ProtectGameplaySurface", 0);
                report.elevatedGroundPreservesWall = Changed(before, raisedGround) == 0;
                report.baselineCenter = before[Size / 2 * Size + Size / 2]; report.clearedCenter = after[Size / 2 * Size + Size / 2];
                report.floorCenter = floor[Size / 2 * Size + Size / 2]; report.otherCameraCenter = other[Size / 2 * Size + Size / 2];
                report.baselineRedPixels = CountCenter(before, true); report.baselineGreenPixels = CountCenter(before, false);
                report.clearedRedPixels = CountCenter(after, true); report.floorRedPixels = CountCenter(floor, true); report.otherCameraRedPixels = CountCenter(other, true);
                report.floorChangedPixels = Changed(before, floor); report.otherCameraChangedPixels = Changed(before, other);
                report.baselineBlocksActor = report.baselineRedPixels == 0 && report.baselineGreenPixels > 400 && Green(report.baselineCenter);
                report.enabledRevealsActor = report.clearedRedPixels > 100 && Red(report.clearedCenter);
                report.floorPreservesWall = report.floorChangedPixels == 0 && report.floorRedPixels == 0;
                report.otherCameraPreservesWall = report.otherCameraChangedPixels == 0 && report.otherCameraRedPixels == 0;
                Require(report.baselineBlocksActor, "The actual foreground island wall did not block the rear red actor; inspect the baseline render.");
                Require(report.enabledRevealsActor, "Clear-view enabled: the actual foreground wall still blocks the rear red actor.");
                Require(report.floorPreservesWall, "The protected floor changed the baseline wall instead of preserving it.");
                Require(report.otherCameraPreservesWall, "Mismatching camera globals changed a camera that should retain the complete island.");
                Require(report.elevatedGroundPreservesWall, "Elevated walkable ground was clipped by scenery cutaway.");
                report.passed = true;
                report.details = "Actual URP SubmitRenderRequest pixels: disabled wall occludes actor; enabled cutaway reveals actor; protected floor and other camera reproduce baseline exactly.";
            }
            catch (Exception error) { report.details = error.ToString(); throw; }
            finally
            {
                Shader.SetGlobalVector(CameraId, previousCamera); Shader.SetGlobalVector(ActorId, previousActor);
                Shader.SetGlobalVector(LandingId, previousLanding); Shader.SetGlobalFloat(FloorId, previousFloor);
                RenderTexture.active = previousTarget;
                if (camera != null) camera.targetTexture = null;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (whiteCube != null) UnityEngine.Object.DestroyImmediate(whiteCube);
                if (wallMaterial != null) UnityEngine.Object.DestroyImmediate(wallMaterial);
                if (actorMaterial != null) UnityEngine.Object.DestroyImmediate(actorMaterial);
                report.globalsRestored = Shader.GetGlobalVector(CameraId).Equals(previousCamera) && Shader.GetGlobalVector(ActorId).Equals(previousActor) &&
                    Shader.GetGlobalVector(LandingId).Equals(previousLanding) && Shader.GetGlobalFloat(FloorId).Equals(previousFloor) && RenderTexture.active == previousTarget;
                if (!report.globalsRestored) { report.passed = false; report.details += " Shader globals or active render target were not restored."; }
                string json = JsonUtility.ToJson(report, true);
                File.WriteAllText(Path.Combine(folder, "report.json"), json);
                File.WriteAllText(Path.GetFullPath("Library/IslandPilotReview/occlusion-checks.json"), json);
            }
            Require(report.globalsRestored, "Occlusion checks did not restore global rendering state.");
            Debug.Log("ISLAND_OCCLUSION_PASS red center pixels " + report.baselineRedPixels + " -> " + report.clearedRedPixels + "; floor/other camera unchanged; evidence=" + folder);
        }

        private static void SetGlobals(Report report, bool enabled, float floor, bool otherCamera)
        {
            Vector3 at = report.cameraPosition + (otherCamera ? Vector3.right * 10 : Vector3.zero);
            Shader.SetGlobalVector(CameraId, new Vector4(at.x, at.y, at.z, enabled ? 1 : 0));
            Shader.SetGlobalVector(ActorId, new Vector4(report.actorPosition.x, report.actorPosition.y, report.actorPosition.z, report.actorRadius));
            Shader.SetGlobalVector(LandingId, Vector4.zero); Shader.SetGlobalFloat(FloorId, floor);
        }

        private static Color32[] Capture(Camera camera, RenderTexture target, Texture2D pixels, string path)
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); pixels.Apply(false, false);
            var frame = pixels.GetPixels32(); File.WriteAllBytes(path, pixels.EncodeToPNG()); return frame;
        }

        private static GameObject Node(Scene scene, string name)
        { var node = new GameObject(name) { layer = Layer }; SceneManager.MoveGameObjectToScene(node, scene); return node; }
        private static GameObject Cube(Scene scene, string name, Vector3 position, Vector3 scale, Material material)
        {
            var node = GameObject.CreatePrimitive(PrimitiveType.Cube); node.name = name; node.layer = Layer; SceneManager.MoveGameObjectToScene(node, scene);
            UnityEngine.Object.DestroyImmediate(node.GetComponent<Collider>()); node.transform.position = position; node.transform.localScale = scale;
            var renderer = node.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return node;
        }
        private static bool Red(Color color) => color.r > .6f && color.r > color.g * 2 && color.r > color.b * 2;
        private static bool Green(Color color) => color.g > .15f && color.g > color.r * 1.5f && color.g > color.b * 1.5f;
        private static int CountCenter(Color32[] pixels, bool red)
        { int count = 0; for (int y = Size / 2 - 16; y < Size / 2 + 16; y++) for (int x = Size / 2 - 16; x < Size / 2 + 16; x++) if (red ? Red(pixels[y * Size + x]) : Green(pixels[y * Size + x])) count++; return count; }
        private static int Changed(Color32[] a, Color32[] b)
        { int count = 0; for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) count++; return count; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
