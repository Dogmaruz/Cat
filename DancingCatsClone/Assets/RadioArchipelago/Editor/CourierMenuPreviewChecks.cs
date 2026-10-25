using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RadioArchipelago.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RadioArchipelago.Editor
{
    /// <summary>Checks the actual live menu camera, including all deformed skin vertices after UI layout.</summary>
    public static class CourierMenuPreviewChecks
    {
        private const float FrameMargin = .015f;

        /// <summary>Compares native skin bake spaces with direct bind-pose skinning without altering project assets.</summary>
        public static void ProbeBakeSpaces()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Probe skin bake spaces in Edit Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            if (catalog == null || catalog.entries == null) throw new InvalidOperationException("The courier catalog is missing.");
            var notes = new StringBuilder("Skin-space probe: rest pose, real imported weights/bindposes, disposable scene only.\n");
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject stage = null;
            Mesh bakedFalse = null, bakedTrue = null;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/RadioArchipelagoReview/menu-bake-spaces.txt"));
            try
            {
                stage = new GameObject("Owned skin-space probe"); SceneManager.MoveGameObjectToScene(stage, scene);
                bakedFalse = new Mesh { name = "Owned BakeMesh false probe", hideFlags = HideFlags.HideAndDontSave };
                bakedTrue = new Mesh { name = "Owned BakeMesh true probe", hideFlags = HideFlags.HideAndDontSave };
                foreach (var definition in catalog.entries)
                {
                    foreach (float position in new[] { 0f, 1000f })
                    foreach (float scale in new[] { 1f, 1.2f })
                    {
                        stage.transform.position = Vector3.one * position;
                        var actor = UnityEngine.Object.Instantiate(definition.prefab, stage.transform, false);
                        try
                        {
                            actor.transform.localScale = Vector3.one * scale;
                            actor.transform.localRotation = Quaternion.Euler(0, -24, 0);
                            var motion = actor.GetComponent<CourierMotion>();
                            if (motion == null) throw new InvalidOperationException("The courier motion is missing: " + definition.id);
                            motion.ManualSimulation = true; motion.ResetMotion();
                            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                                ProbeSkin(notes, definition.id, position, scale, skin, bakedFalse, bakedTrue);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(actor); }
                    }
                }
                notes.Insert(0, "PASS probe completed; compare numeric errors below.\n");
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, notes.ToString());
                Debug.Log("MENU_BAKE_SPACES PASS: " + output);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, "FAIL " + error + "\n" + notes);
                throw;
            }
            finally
            {
                if (bakedFalse != null) UnityEngine.Object.DestroyImmediate(bakedFalse);
                if (bakedTrue != null) UnityEngine.Object.DestroyImmediate(bakedTrue);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ProbeSkin(StringBuilder notes, string id, float stagePosition, float actorScale,
            SkinnedMeshRenderer skin, Mesh bakedFalse, Mesh bakedTrue)
        {
            var source = skin.sharedMesh;
            if (source == null) throw new InvalidOperationException("The skin mesh is missing: " + skin.name);
            var positions = source.vertices;
            var bindposes = source.bindposes;
            var bones = skin.bones;
            if (bindposes.Length != bones.Length) throw new InvalidOperationException("Skin bind poses do not match bones: " + skin.name);
            var matrices = new Matrix4x4[bones.Length];
            for (int bone = 0; bone < bones.Length; bone++)
                matrices[bone] = bones[bone].localToWorldMatrix * bindposes[bone];
            // These NativeArrays are borrowed views over the source mesh-owned bone-weight buffers.
            var counts = source.GetBonesPerVertex();
            var weights = source.GetAllBoneWeights();
            if (counts.Length != positions.Length) throw new InvalidOperationException("Skin vertex and weight counts differ: " + skin.name);
            var independent = new Vector3[positions.Length];
            float minSum = float.PositiveInfinity, maxSum = float.NegativeInfinity;
            float maxMissing = 0, maxExcess = 0;
            int offset = 0, tinyWeights = 0;
            var independentMin = Vector3.one * float.PositiveInfinity;
            var independentMax = Vector3.one * float.NegativeInfinity;
            for (int vertex = 0; vertex < positions.Length; vertex++)
            {
                Vector3 point = Vector3.zero; float sum = 0;
                for (int influence = 0; influence < counts[vertex]; influence++)
                {
                    var weight = weights[offset++];
                    sum += weight.weight;
                    if (weight.weight > 0 && weight.weight < .001f) tinyWeights++;
                    point += matrices[weight.boneIndex].MultiplyPoint3x4(positions[vertex]) * weight.weight;
                }
                independent[vertex] = point;
                independentMin = Vector3.Min(independentMin, point); independentMax = Vector3.Max(independentMax, point);
                minSum = Mathf.Min(minSum, sum); maxSum = Mathf.Max(maxSum, sum);
                maxMissing = Mathf.Max(maxMissing, 1 - sum); maxExcess = Mathf.Max(maxExcess, sum - 1);
            }
            if (offset != weights.Length) throw new InvalidOperationException("Skin has trailing weight data: " + skin.name);
            skin.BakeMesh(bakedFalse, false); skin.BakeMesh(bakedTrue, true);
            var pointsFalse = bakedFalse.vertices; var pointsTrue = bakedTrue.vertices;
            if (pointsFalse.Length != independent.Length || pointsTrue.Length != independent.Length)
                throw new InvalidOperationException("BakeMesh changed the vertex count: " + skin.name);
            float falseTransformError = 0, falseRotationError = 0, trueTransformError = 0, trueRotationError = 0;
            var falseLocalMin = Vector3.one * float.PositiveInfinity; var falseLocalMax = Vector3.one * float.NegativeInfinity;
            var trueLocalMin = Vector3.one * float.PositiveInfinity; var trueLocalMax = Vector3.one * float.NegativeInfinity;
            var falseWorldMin = Vector3.one * float.PositiveInfinity; var falseWorldMax = Vector3.one * float.NegativeInfinity;
            var trueWorldMin = Vector3.one * float.PositiveInfinity; var trueWorldMax = Vector3.one * float.NegativeInfinity;
            for (int vertex = 0; vertex < independent.Length; vertex++)
            {
                var transformedFalse = skin.transform.TransformPoint(pointsFalse[vertex]);
                var rotatedFalse = skin.transform.position + skin.transform.rotation * pointsFalse[vertex];
                var transformedTrue = skin.transform.TransformPoint(pointsTrue[vertex]);
                var rotatedTrue = skin.transform.position + skin.transform.rotation * pointsTrue[vertex];
                falseTransformError = Mathf.Max(falseTransformError, Vector3.Distance(transformedFalse, independent[vertex]));
                falseRotationError = Mathf.Max(falseRotationError, Vector3.Distance(rotatedFalse, independent[vertex]));
                trueTransformError = Mathf.Max(trueTransformError, Vector3.Distance(transformedTrue, independent[vertex]));
                trueRotationError = Mathf.Max(trueRotationError, Vector3.Distance(rotatedTrue, independent[vertex]));
                falseLocalMin = Vector3.Min(falseLocalMin, pointsFalse[vertex]); falseLocalMax = Vector3.Max(falseLocalMax, pointsFalse[vertex]);
                trueLocalMin = Vector3.Min(trueLocalMin, pointsTrue[vertex]); trueLocalMax = Vector3.Max(trueLocalMax, pointsTrue[vertex]);
                falseWorldMin = Vector3.Min(falseWorldMin, transformedFalse); falseWorldMax = Vector3.Max(falseWorldMax, transformedFalse);
                trueWorldMin = Vector3.Min(trueWorldMin, rotatedTrue); trueWorldMax = Vector3.Max(trueWorldMax, rotatedTrue);
            }
            notes.Append(id).Append('/').Append(skin.name).Append(" stage=").Append(Number(stagePosition))
                .Append(" actorScale=").Append(Number(actorScale)).Append(" skinLossy=").Append(Vector(skin.transform.lossyScale))
                .Append(" skinLocal=").Append(Vector(skin.transform.localScale)).Append(" vertices=").Append(independent.Length)
                .Append(" sumRange=[").Append(Number(minSum)).Append(',').Append(Number(maxSum)).Append(']')
                .Append(" maxMissing=").Append(Number(maxMissing)).Append(" maxExcess=").Append(Number(maxExcess))
                .Append(" retainedUnder001=").Append(tinyWeights).AppendLine()
                .Append(" independentWorld=").Append(Vector(independentMin)).Append("..").Append(Vector(independentMax)).AppendLine()
                .Append(" bakeFalseLocal=").Append(Vector(falseLocalMin)).Append("..").Append(Vector(falseLocalMax))
                .Append(" size=").Append(Vector(falseLocalMax - falseLocalMin)).AppendLine()
                .Append(" bakeTrueLocal=").Append(Vector(trueLocalMin)).Append("..").Append(Vector(trueLocalMax))
                .Append(" size=").Append(Vector(trueLocalMax - trueLocalMin)).AppendLine()
                .Append(" falseTransformWorld=").Append(Vector(falseWorldMin)).Append("..").Append(Vector(falseWorldMax)).AppendLine()
                .Append(" trueRotationWorld=").Append(Vector(trueWorldMin)).Append("..").Append(Vector(trueWorldMax)).AppendLine()
                .Append(" maxError falseTransform=").Append(Number(falseTransformError))
                .Append(" falseRotation=").Append(Number(falseRotationError))
                .Append(" trueTransform=").Append(Number(trueTransformError))
                .Append(" trueRotation=").Append(Number(trueRotationError)).AppendLine().AppendLine();
            var originalLocalBounds = skin.localBounds;
            int originalOutside = CountOutside(skin.bounds, independent);
            int rootSpaceOutside;
            var rootFrame = skin.rootBone != null ? skin.rootBone : skin.transform;
            var rootMin = Vector3.one * float.PositiveInfinity;
            var rootMax = Vector3.one * float.NegativeInfinity;
            var originalWorldBounds = skin.bounds;
            Bounds convertedWorldBounds;
            try
            {
                foreach (var localPoint in pointsTrue)
                {
                    var rootPoint = rootFrame.InverseTransformPoint(skin.transform.TransformPoint(localPoint));
                    rootMin = Vector3.Min(rootMin, rootPoint); rootMax = Vector3.Max(rootMax, rootPoint);
                }
                var converted = new Bounds(); converted.SetMinMax(rootMin, rootMax); converted.Expand(.004f);
                skin.localBounds = converted;
                convertedWorldBounds = skin.bounds;
                rootSpaceOutside = CountOutside(convertedWorldBounds, independent);
            }
            finally { skin.localBounds = originalLocalBounds; }
            notes.Append(" cullingFrame=").Append(rootFrame.name)
                .Append(" originalOutside=").Append(originalOutside).Append('/').Append(independent.Length)
                .Append(" originalWorldCenter=").Append(Vector(originalWorldBounds.center))
                .Append(" originalWorldSize=").Append(Vector(originalWorldBounds.size)).AppendLine()
                .Append(" rootSpaceOutside=").Append(rootSpaceOutside).Append('/').Append(independent.Length)
                .Append(" convertedWorldCenter=").Append(Vector(convertedWorldBounds.center))
                .Append(" convertedWorldSize=").Append(Vector(convertedWorldBounds.size)).AppendLine().AppendLine();
            if (!Finite(trueTransformError) || trueTransformError > .001f)
                throw new InvalidOperationException("BakeMesh(true) + TransformPoint differs from direct weighted skinning by " +
                    Number(trueTransformError) + " for " + id + ", stage=" + Number(stagePosition) + ", scale=" + Number(actorScale));
            if (rootSpaceOutside != 0)
                throw new InvalidOperationException("Root-bone-space culling bounds exclude " + rootSpaceOutside + "/" + independent.Length +
                    " actual vertices for " + id + ", stage=" + Number(stagePosition) + ", scale=" + Number(actorScale));
        }

        private static int CountOutside(Bounds bounds, Vector3[] points)
        {
            int count = 0;
            foreach (var point in points) if (!bounds.Contains(point)) count++;
            return count;
        }

        public static string CheckCurrent(Canvas canvas, string context)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            var previews = canvas.GetComponentsInChildren<CourierMenuPreview>(false)
                .Where(preview => preview.isActiveAndEnabled).ToArray();
            if (previews.Length != 1)
                throw new InvalidOperationException("MENU_PREVIEW FAIL " + context + ": expected one active menu preview, found " + previews.Length);
            var preview = previews[0];
            var camera = preview.PreviewCamera;
            if (camera == null || !camera.isActiveAndEnabled || camera.transform.parent == null)
                throw new InvalidOperationException("MENU_PREVIEW FAIL " + context + ": the actual preview camera is missing or inactive.");

            var stage = camera.transform.parent;
            var skins = stage.GetComponentsInChildren<SkinnedMeshRenderer>(false)
                .Where(skin => skin.enabled && skin.sharedMesh != null).ToArray();
            var renderers = stage.GetComponentsInChildren<Renderer>(false)
                .Where(renderer => renderer.enabled).ToArray();
            var details = new StringBuilder();
            details.Append(context).Append(" screen=").Append(Screen.width).Append('x').Append(Screen.height)
                .Append(" canvasScale=").Append(Number(canvas.scaleFactor))
                .Append(" canvasLossy=").Append(Vector(canvas.transform.lossyScale))
                .Append(" previewLossy=").Append(Vector(preview.transform.lossyScale))
                .Append(" stageLossy=").Append(Vector(stage.lossyScale))
                .Append(" cameraLossy=").Append(Vector(camera.transform.lossyScale))
                .Append(" cameraPos=").Append(Vector(camera.transform.position))
                .Append(" stagePos=").Append(Vector(stage.position))
                .Append(" cameraRect=").Append(Rectangle(camera.rect))
                .Append(" cameraPixelRect=").Append(Rectangle(camera.pixelRect))
                .Append(" aspect=").Append(Number(camera.aspect))
                .Append(" ortho=").Append(Number(camera.orthographicSize))
                .Append(" clip=[").Append(Number(camera.nearClipPlane)).Append(',').Append(Number(camera.farClipPlane)).Append(']');
            var image = preview.GetComponent<RawImage>();
            if (image != null)
                details.Append(" imageRect=").Append(Rectangle(image.rectTransform.rect))
                    .Append(" imageUV=").Append(Rectangle(image.uvRect));
            if (camera.targetTexture != null)
                details.Append(" target=").Append(camera.targetTexture.width).Append('x').Append(camera.targetTexture.height);

            var viewportMin = Vector3.one * float.PositiveInfinity;
            var viewportMax = Vector3.one * float.NegativeInfinity;
            int total = 0, outside = 0, nonFinite = 0;
            float bodyHeight = float.NaN, normalizedBodyHeight = float.NaN, squashScale = 1, footAnchorError = float.PositiveInfinity;
            Mesh sample = null;
            var vertices = new List<Vector3>();
            try
            {
                sample = new Mesh { name = "Owned live menu framing check", hideFlags = HideFlags.HideAndDontSave };
                foreach (var skin in skins)
                {
                    skin.BakeMesh(sample, true);
                    sample.GetVertices(vertices);
                    var skinMin = Vector3.one * float.PositiveInfinity;
                    var skinMax = Vector3.one * float.NegativeInfinity;
                    int skinOutside = 0;
                    foreach (var localPoint in vertices)
                    {
                        var viewport = camera.WorldToViewportPoint(skin.transform.TransformPoint(localPoint));
                        total++;
                        if (!Finite(viewport.x) || !Finite(viewport.y) || !Finite(viewport.z))
                        {
                            nonFinite++; outside++; skinOutside++; continue;
                        }
                        viewportMin = Vector3.Min(viewportMin, viewport);
                        viewportMax = Vector3.Max(viewportMax, viewport);
                        skinMin = Vector3.Min(skinMin, viewport);
                        skinMax = Vector3.Max(skinMax, viewport);
                        if (viewport.x < FrameMargin || viewport.x > 1 - FrameMargin ||
                            viewport.y < FrameMargin || viewport.y > 1 - FrameMargin ||
                            viewport.z < camera.nearClipPlane || viewport.z > camera.farClipPlane)
                        { outside++; skinOutside++; }
                    }
                    details.Append(" | skin=").Append(skin.name).Append(" vertices=").Append(vertices.Count)
                        .Append(" outside=").Append(skinOutside).Append(" viewport=").Append(Vector(skinMin)).Append("..").Append(Vector(skinMax))
                        .Append(" lossy=").Append(Vector(skin.transform.lossyScale))
                        .Append(" rendererCenter=").Append(Vector(skin.bounds.center))
                        .Append(" rendererSize=").Append(Vector(skin.bounds.size));
                }
                var bounds = CourierVisualBounds.Measure(renderers);
                details.Append(" | measuredCenter=").Append(Vector(bounds.center))
                    .Append(" measuredSize=").Append(Vector(bounds.size))
                    .Append(" centerViewport=").Append(Vector(camera.WorldToViewportPoint(bounds.center)))
                    .Append(" viewport=").Append(Vector(viewportMin)).Append("..").Append(Vector(viewportMax))
                    .Append(" vertices=").Append(total).Append(" outside=").Append(outside)
                    .Append(" nonFinite=").Append(nonFinite);
                bodyHeight = CourierVisualBounds.MeasureBody(renderers).size.y;
                // The menu normalizes the rest body to 2.1 units. Live squash is
                // intentional; compare the base size while still checking every
                // animated vertex and its actual foot anchor above/below.
                var motion = stage.GetComponentInChildren<CourierMotion>(false);
                if (motion != null)
                {
                    var part = typeof(CourierMotion).GetField("visual", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(motion);
                    if (part == null) throw new InvalidOperationException("The menu courier has no captured presentation pose.");
                    var node = (Transform)part.GetType().GetField("node").GetValue(part);
                    var restScale = (Vector3)part.GetType().GetField("scale").GetValue(part);
                    squashScale = node.localScale.y / restScale.y;
                }
                normalizedBodyHeight = bodyHeight / squashScale;
                details.Append(" bodyHeight=").Append(Number(bodyHeight))
                    .Append(" squashScale=").Append(Number(squashScale))
                    .Append(" normalizedBodyHeight=").Append(Number(normalizedBodyHeight));
                var background = canvas.GetComponentsInChildren<RectTransform>(false)
                    .SingleOrDefault(rect => rect.name == "SkyIslandsBackground");
                if (image != null && background != null)
                {
                    var footViewport = camera.WorldToViewportPoint(stage.position);
                    var uv = image.uvRect;
                    var imageRect = image.rectTransform.rect;
                    var footLocal = new Vector3(
                        Mathf.LerpUnclamped(imageRect.xMin, imageRect.xMax, (footViewport.x - uv.x) / uv.width),
                        Mathf.LerpUnclamped(imageRect.yMin, imageRect.yMax, (footViewport.y - uv.y) / uv.height), 0);
                    var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    var footScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, image.rectTransform.TransformPoint(footLocal));
                    var pad = CourierMenuPreview.LandingPadAnchor;
                    var backgroundRect = background.rect;
                    var padLocal = new Vector3(Mathf.LerpUnclamped(backgroundRect.xMin, backgroundRect.xMax, pad.x),
                        Mathf.LerpUnclamped(backgroundRect.yMin, backgroundRect.yMax, pad.y), 0);
                    var padScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, background.TransformPoint(padLocal));
                    footAnchorError = Vector2.Distance(footScreen, padScreen);
                    details.Append(" footScreen=").Append(Vector(footScreen))
                        .Append(" padScreen=").Append(Vector(padScreen))
                        .Append(" footAnchorErrorPixels=").Append(Number(footAnchorError));
                }
                else details.Append(" footAnchor=missing-background-or-image");
            }
            finally
            {
                if (sample != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(sample);
                    else UnityEngine.Object.DestroyImmediate(sample);
                }
            }
            if (total == 0 || outside != 0 || !Finite(bodyHeight) || !Finite(squashScale) || squashScale < .90f || squashScale > 1.10f ||
                !Finite(normalizedBodyHeight) || Mathf.Abs(normalizedBodyHeight - 2.1f) > .12f ||
                !Finite(footAnchorError) || footAnchorError > 1.5f)
                throw new InvalidOperationException("MENU_PREVIEW FAIL " + details);
            return "MENU_PREVIEW PASS " + details;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static string Number(float value) => value.ToString("G7", CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => "(" + Number(value.x) + "," + Number(value.y) + "," + Number(value.z) + ")";
        private static string Rectangle(Rect value) => "(" + Number(value.x) + "," + Number(value.y) + "," + Number(value.width) + "," + Number(value.height) + ")";
    }
}
