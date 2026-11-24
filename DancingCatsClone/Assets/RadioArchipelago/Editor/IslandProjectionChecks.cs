using System;
using System.Collections;
using System.Reflection;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Isolated real-mesh checks; no gameplay scene, saved assets or preferences are changed.</summary>
    public static class IslandProjectionChecks
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run projection checks in Edit Mode.");
            using (var fixture = new Fixture(false))
            {
                fixture.Tick();
                var first = fixture.ViewMesh.vertices;
                Equal(new[] { new Vector3(-.4f, 1, 1), new Vector3(.4f, 1, 1), new Vector3(0, 1, 2) }, first, "Straight-route projection changed its known positions.");
                fixture.Tick();
                Require(fixture.Counter("GeometryReuseCount") == 1, "Unchanged geometry was rebuilt instead of reused.");
                Equal(first, fixture.ViewMesh.vertices, "An unchanged mesh moved.");
                Require(fixture.Counter("ProjectedVerticesThisFrame") == 0, "A reused mesh repeated per-vertex route work.");
                fixture.EnableParity();
                fixture.Source.transform.position += new Vector3(.25f, 0, .5f);
                fixture.ExpectRebuild("Source translation did not invalidate geometry.");
                fixture.Source.transform.rotation = Quaternion.Euler(0, 18, 0);
                fixture.ExpectRebuild("Source rotation did not invalidate geometry.");
                fixture.Source.transform.localScale = new Vector3(1.2f, .8f, .9f);
                fixture.ExpectRebuild("Source scale did not invalidate geometry.");
                fixture.SetState("offset", -.08f); fixture.ExpectRebuild("Support offset did not invalidate geometry.");
                fixture.SetState("scale", .8f); fixture.ExpectRebuild("Support scale did not invalidate geometry.");
                fixture.Pivot.position = new Vector3(.5f, 0, 0); fixture.ExpectRebuild("Spring pivot did not invalidate geometry.");
                fixture.Mapping.StartDistance += .5f; fixture.ExpectRebuild("Route start distance did not invalidate geometry.");
                fixture.Mapping.DistanceScale = 1.1f; fixture.ExpectRebuild("Route distance scale did not invalidate geometry.");
                fixture.Mapping.TileElevation = .9f; fixture.ExpectRebuild("Tile elevation did not invalidate geometry.");
                fixture.Tick();
                Require(fixture.Counter("GeometryParityChecks") > 0, "Cached geometry was not compared with the reference deformation.");
                var properties = new MaterialPropertyBlock(); properties.SetFloat("_ProjectionTest", .37f);
                fixture.Source.SetPropertyBlock(properties);
                int rebuilds = fixture.Counter("GeometryRebuildCount");
                fixture.Tick();
                var drawn = new MaterialPropertyBlock(); fixture.View.GetPropertyBlock(drawn);
                Require(drawn.GetFloat("_ProjectionTest").Equals(.37f), "Cached geometry froze animated material properties.");
                fixture.Source.sharedMaterial = fixture.AlternateMaterial;
                fixture.Tick();
                Require(fixture.View.sharedMaterial == fixture.AlternateMaterial, "Cached geometry froze source material changes.");
                Require(fixture.Counter("GeometryRebuildCount") == rebuilds, "Material-only changes repeated geometry work.");
                fixture.Source.enabled = false; fixture.Tick(); Require(!fixture.View.enabled, "A hidden source retained its projected view.");
                fixture.Source.enabled = true; fixture.Tick(); Require(fixture.View.enabled, "A re-enabled source did not regain its projected view.");
                Require(fixture.Counter("GeometryParityFailures") == 0, "Cached static geometry differed from reference deformation.");
            }
            using (var fixture = new Fixture(true))
            {
                fixture.EnableParity(); fixture.Tick(); fixture.Tick();
                Require(fixture.Counter("GeometryReuseCount") == 1, "An unchanged skinned pose was rebuilt instead of reused.");
                fixture.Bone.localPosition = new Vector3(.2f, .1f, 0);
                fixture.ExpectRebuild("A changed native bone pose did not invalidate geometry.");
                fixture.Tick();
                fixture.Bone.localRotation = Quaternion.Euler(0, 0, 12);
                fixture.ExpectRebuild("Native bone rotation did not invalidate geometry."); fixture.Tick();
                fixture.Skin.SetBlendShapeWeight(0, 50);
                fixture.ExpectRebuild("A native blend shape did not invalidate geometry.");
                fixture.Tick();
                fixture.ChangeNativeNormal(); fixture.ExpectRebuild("Native normal-only changes did not invalidate geometry."); fixture.Tick();
                fixture.ChangeNativeTangent(); fixture.ExpectRebuild("Native tangent-only changes did not invalidate geometry."); fixture.Tick();
                Require(fixture.Counter("GeometryParityFailures") == 0, "Cached skinned geometry differed from reference deformation.");
                Require(fixture.Counter("GeometryParityChecks") >= 3, "Skinned cached frames were not reference-checked.");
            }
            using (var fixture = new Fixture(false))
            {
                fixture.Owner.transform.position = Vector3.right;
                fixture.EnableParity(); fixture.Tick();
                var original = fixture.ViewMesh.vertices;
                fixture.Mapping.LateralScale = 2.1f;
                fixture.ExpectRebuild("Changing lane spread did not invalidate cached effect geometry.");
                var widened = fixture.ViewMesh.vertices;
                for (int i = 0; i < original.Length; i++)
                    Require(Vector3.Distance(widened[i], original[i] + Vector3.right * 1.1f) < .00001f,
                        "Lane widening stretched a native effect's local mesh instead of translating its center.");
                Require(Mathf.Abs((widened[1].x - widened[0].x) - .8f) < .00001f, "Lane widening changed effect width.");
                fixture.Tick();
                Require(fixture.Counter("GeometryParityFailures") == 0, "Wide-lane cached geometry differed from a fresh projection.");
                fixture.Mapping.LateralScale = 1;
                fixture.ExpectRebuild("Restoring legacy lane spread did not invalidate the cache.");
                Equal(original, fixture.ViewMesh.vertices, "Default spread changed original effect geometry.");
            }
            CheckNativeProgressHoldBands();
            CheckWholeHoldAndAttachedRadioCursor();
            Debug.Log("ISLAND_PROJECTION_CACHE_PASS static/skin reuse; translation, rotation, spring and bone/blend-shape invalidation; live materials/visibility; exact geometry parity.");
        }

        private static void CheckNativeProgressHoldBands()
        {
            using (var legacy = new Fixture(false, true, false))
            {
                legacy.AdvanceHold(4); legacy.Tick(); legacy.RequireBandRange(1, 2);
                Require(legacy.Counter("DebugAdvancedHoldBandCount") == 0, "A legacy Hold band was remapped.");
                legacy.AdvanceNativeFill(4); legacy.Tick(); legacy.RequireBandRange(0, 4);
            }
            foreach (bool skinned in new[] { false, true })
                using (var fixture = new Fixture(skinned, true, true))
                {
                    fixture.EnableParity(); fixture.Tick(); fixture.RequireBandRange(1, 2);
                    int before = fixture.Counter("GeometryRebuildCount");
                    fixture.AdvanceHold(4); fixture.Tick(); fixture.RequireBandRange(1, 2);
                    Require(fixture.Counter("GeometryRebuildCount") == before,
                        "Body progress rewrote a native band whose source pose did not change.");
                    Require(fixture.Counter("DebugAdvancedHoldBandCount") == 1, "The native progress band was not observed.");
                    foreach (float progress in new[] { 3f, 7f, 9.5f, 10f })
                    {
                        fixture.AdvanceNativeFill(progress);
                        fixture.ExpectRebuild("The native Follow fill pose failed to invalidate its projected band.");
                        fixture.RequireBandRange(0, progress);
                        fixture.RequireReviewBandRange(0, progress);
                        Require(fixture.Metric("DebugMaxHoldBandOutsideSupport") < .00001f,
                            "The native filled prefix left its full ceramic support.");
                    }
                    int reused = fixture.Counter("GeometryReuseCount");
                    fixture.Tick(); Require(fixture.Counter("GeometryReuseCount") == reused + 1, "A stationary Hold band failed to reuse geometry.");
                    fixture.AdvanceHold(11); fixture.Tick(); fixture.RequireBandRange(0, 10);
                    fixture.Pivot.gameObject.SetActive(false); fixture.Tick();
                    Require(!fixture.View.enabled && fixture.Counter("DebugAdvancedHoldBandCount") == 0,
                        "An expired ceramic support left its band visible.");
                    fixture.RequireNoReviewBand();
                    fixture.RequireNativeLineSuppressed();
                    Require(fixture.Counter("GeometryParityFailures") == 0, "Native Hold geometry failed cached/reference parity.");
                }
            using (var collapsed = new Fixture(false, true, true, .00001f))
            {
                collapsed.AdvanceHold(4); collapsed.Tick(); collapsed.RequireBandRange(1, 1.00001f);
                Require(collapsed.Metric("MaxHoldTessellationChartEdge") <= .75f,
                    "A tiny native fill pose expanded into coarse triangles.");
            }
            Debug.Log("ISLAND_HOLD_BAND_PASS static/skinned native Follow fill preserved; prefix advances to player; stationary reuse; legacy unchanged; expired support hidden; native fish line omitted.");
        }

        private static void CheckWholeHoldAndAttachedRadioCursor()
        {
            var definition = ScriptableObject.CreateInstance<IslandStageDefinition>();
            definition.DistanceScale = 1.18f; definition.TileElevation = 1; definition.LateralScale = 2.1f;
            var points = new[] { Vector3.zero, new Vector3(0, 0, 5), new Vector3(2, 0, 10),
                new Vector3(6, 0, 15), new Vector3(12, 0, 20), new Vector3(20, 0, 15), new Vector3(20, 0, 0) };
            float length = new IslandRoute(points).Length;
            var elevation = new IslandElevationProfile(length, true, new[] {
                new IslandElevationProfile.Key(0, 0), new IslandElevationProfile.Key(length * .2f, 4),
                new IslandElevationProfile.Key(length * .5f, 2), new IslandElevationProfile.Key(length, 0) });
            var route = new IslandRoute(points, elevation);
            try
            {
                foreach (float halfWidth in new[] { .15f, .65f })
                {
                    var body = new IslandHoldSurface(route, definition, 3, 14, .6f, 0, halfWidth);
                    var tracer = new IslandRadioHoldTracer(route, definition, 3, 14, .6f, 0, body.Pivot, halfWidth);
                    try
                    {
                        Vector3[] original = body.Mesh.vertices;
                        Bounds originalBounds = body.Mesh.bounds;
                        Vector3 originalPivot = body.Pivot, head = body.LocalDrawnHeadCenter, tail = body.LocalEndCenter;
                        Vector3 wireStart = tracer.LocalLineStartCenter;
                        foreach (float percent in new[] { -.2f, 0, .3f, .7f, .95f, 1, 1.2f })
                        {
                            float z = Mathf.LerpUnclamped(body.Start, body.End, percent);
                            body.Advance(z); tracer.Advance(z);
                            Equal(original, body.Mesh.vertices, "Ride progress shortened or otherwise deformed the ceramic Hold.");
                            Require(body.Pivot.Equals(originalPivot) && body.Mesh.bounds.center.Equals(originalBounds.center) &&
                                body.Mesh.bounds.extents.Equals(originalBounds.extents), "Ride progress changed ceramic bounds or pivot.");
                            Require(body.LocalDrawnHeadCenter.Equals(head) && body.LocalEndCenter.Equals(tail) && body.DrawnStart == body.Start,
                                "Ride progress moved a full support's entry/exit spine centers.");
                            float expectedProgress = Mathf.Clamp(z, body.Start, body.End);
                            Require(Mathf.Abs(body.ProgressChartZ - expectedProgress) < .00001f &&
                                Mathf.Abs(tracer.GlyphChartZ - expectedProgress) < .00001f, "Body and radio cursor disagree on progress.");
                            Require(Vector3.Distance(tracer.LocalGlyphCenter, tracer.LocalLineEndCenter) < .00001f,
                                "The radio receiver is detached from the exact wire endpoint.");
                            Require(Vector3.Distance(wireStart, tracer.LocalLineStartCenter) < .00001f,
                                "The progressive white wire moved its fixed entry endpoint.");
                            var frame = route.Evaluate(definition.StartDistance + expectedProgress * definition.DistanceScale);
                            Vector3 actual = body.Pivot + tracer.LocalGlyphCenter;
                            float lateralError = Vector3.Dot(actual - frame.Position, frame.Right) - .6f * definition.LateralScale;
                            Require(Mathf.Abs(lateralError) < .00001f, "The receiver has an independent side offset from its centerline.");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(tracer.Mesh); UnityEngine.Object.DestroyImmediate(body.Mesh); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
            Debug.Log("ISLAND_WHOLE_HOLD_PASS fixed ceramic vertices/bounds/pivot; .0/.3/.7/.95 progress; centered radio exactly attached to native progress-wire endpoint on curved elevated route.");
        }

        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene;
            private readonly GameObject root;
            private readonly IslandParticleProjection projection;
            private readonly IslandStageDefinition definition;
            private readonly Mesh native;
            private readonly Material material;
            private readonly object state, effect;
            internal readonly Material AlternateMaterial;
            internal readonly Renderer Source;
            internal readonly Transform Pivot, Bone;
            internal readonly SkinnedMeshRenderer Skin;
            internal readonly Transform Actor;
            internal readonly OriginalTile Owner;
            private readonly IslandHoldSurface styled;
            internal IslandStageDefinition Mapping => definition;
            internal Mesh ViewMesh => (Mesh)Get(effect, "viewMesh");
            internal MeshRenderer View => (MeshRenderer)Get(effect, "view");

            internal Fixture(bool skinned, bool hold = false, bool unified = false, float nativeBandSpan = 1)
            {
                scene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Projection cache check"); SceneManager.MoveGameObjectToScene(root, scene);
                var stage = root.AddComponent<IslandStagePresentation>();
                var game = root.AddComponent<OriginalPlayController>();
                Actor = Child("Actor");
                typeof(OriginalPlayController).GetProperty("Actor", Members).SetValue(game, Actor);
                definition = ScriptableObject.CreateInstance<IslandStageDefinition>();
                definition.StartDistance = 10; definition.DistanceScale = 1;
                definition.UnifiedHoldTiles = unified;
                definition.HoldTracerShader = Shader.Find("DancingCatsClone/Recovered/Shader Graphs/Cats_Ecolor_A");
                projection = root.AddComponent<IslandParticleProjection>();
                projection.Initialize(new IslandRoute(new[] { new Vector3(0, 0, -10), Vector3.zero, new Vector3(0, 0, 10), new Vector3(0, 0, 20), new Vector3(20, 0, 20), new Vector3(20, 0, -10) }), definition);
                var owner = Child("Native tile").gameObject.AddComponent<OriginalTile>();
                Owner = owner;
                owner.owner = game; owner.element = new MapElement { id = hold ? 2 : 0, mfendPos = hold ? 10 : 0 }; owner.initialZ = 0;
                if (hold) owner.segment = new MapSegment { endZ = 10 };
                Pivot = Child("Spring proxy");
                var stateType = typeof(IslandStagePresentation).GetNestedType("TileProxy", BindingFlags.NonPublic);
                state = Activator.CreateInstance(stateType, true); Set(state, "source", owner); Set(state, "view", Pivot); Set(state, "scale", 1f);
                if (hold && unified)
                {
                    var route = new IslandRoute(new[] { new Vector3(0, 0, -10), Vector3.zero, new Vector3(0, 0, 10), new Vector3(0, 0, 20), new Vector3(20, 0, 20), new Vector3(20, 0, -10) });
                    styled = new IslandHoldSurface(route, definition, 0, 10, 0, 0, .65f);
                    Set(state, "styledSpan", styled); Set(state, "span", true);
                }
                ((IDictionary)Get(stage, "proxyBySource")).Add(owner, state);
                native = new Mesh { name = "Projection check triangle" };
                native.vertices = new[] { new Vector3(-.4f, 0, 1), new Vector3(.4f, 0, 1), new Vector3(0, 0, 1 + nativeBandSpan) };
                native.triangles = new[] { 0, 2, 1 };
                native.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
                native.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader");
                material = new Material(shader); AlternateMaterial = new Material(shader);
                var source = Child(hold ? "Hold_Light" : "Effect_Test"); source.SetParent(owner.transform, false);
                if (skinned)
                {
                    Bone = Child("Native bone");
                    native.bindposes = new[] { Matrix4x4.identity };
                    var weight = new BoneWeight { boneIndex0 = 0, weight0 = 1 };
                    native.boneWeights = new[] { weight, weight, weight };
                    native.AddBlendShapeFrame("Bend", 100, new[] { Vector3.zero, Vector3.zero, new Vector3(.15f, .1f, 0) }, new Vector3[3], new Vector3[3]);
                    Skin = source.gameObject.AddComponent<SkinnedMeshRenderer>(); Skin.sharedMesh = native; Skin.bones = new[] { Bone }; Skin.rootBone = Bone; Source = Skin;
                }
                else
                { source.gameObject.AddComponent<MeshFilter>().sharedMesh = native; Source = source.gameObject.AddComponent<MeshRenderer>(); }
                Source.sharedMaterial = material;
                effect = typeof(IslandParticleProjection).GetMethod("CreateEffectMesh", Members).Invoke(projection, new object[] { Source, owner });
                ((IList)Get(projection, "meshes")).Add(effect);
            }

            private Transform Child(string name) { var child = new GameObject(name).transform; child.SetParent(root.transform, false); return child; }
            internal int Counter(string name) => (int)(typeof(IslandParticleProjection).GetProperty(name, Members)?.GetValue(projection) ?? 0);
            internal float Metric(string name) => (float)typeof(IslandParticleProjection).GetProperty(name, Members).GetValue(projection);
            internal void AdvanceHold(float z)
            {
                Owner.consumed = true; Actor.position = Vector3.forward * z; styled?.Advance(z);
            }
            internal void AdvanceNativeFill(float z)
            {
                AdvanceHold(z);
                // Native Hold_Light is skinned between its fixed entry and a
                // Follow bone moved to the player. These equivalent source
                // poses exercise both supported mesh projection paths.
                var animated = Skin != null ? Bone : Source.transform;
                float progress = Mathf.Clamp(z, 0, 10);
                animated.localScale = new Vector3(1, 1, progress);
                animated.localPosition = Vector3.back * progress;
            }
            internal void RequireReviewBandRange(float start, float end)
            {
                Require(projection.TryGetHoldBandReviewState(Owner, out float min, out float max, out int count) && count == 1 &&
                    Mathf.Abs(min - start) < .00001f && Mathf.Abs(max - end) < .00001f,
                    "Native band review state disagrees with its real visible vertices.");
            }
            internal void RequireNoReviewBand() => Require(!projection.TryGetHoldBandReviewState(Owner, out _, out _, out int count) && count == 0,
                "Expired support retained a visible native band in review state.");
            internal void RequireBandRange(float start, float end)
            {
                var z = (float[])Get(effect, "chartZ"); float min = float.PositiveInfinity, max = float.NegativeInfinity;
                foreach (float value in z) { min = Mathf.Min(min, value); max = Mathf.Max(max, value); }
                Require(Mathf.Abs(min - start) < .00001f && Mathf.Abs(max - end) < .00001f,
                    "Projected band has stale/wrong range: " + min + ".." + max + "; expected " + start + ".." + end);
            }
            internal void RequireNativeLineSuppressed()
            {
                string name = Source.name; Source.name = "Hold_Line";
                object skipped = typeof(IslandParticleProjection).GetMethod("CreateEffectMesh", Members).Invoke(projection, new object[] { Source, Owner });
                Source.name = name;
                Require(skipped == null, "The unified Hold still copied its native fish-shaped line.");
            }
            internal void EnableParity() { typeof(IslandParticleProjection).GetProperty("ValidateGeometryCacheParity", Members)?.SetValue(projection, true); }
            internal void SetState(string name, object value) => Set(state, name, value);
            internal void ChangeNativeNormal() { var normals = native.normals; normals[0] = Vector3.forward; native.normals = normals; }
            internal void ChangeNativeTangent() { var tangents = native.tangents; tangents[0] = new Vector4(0, 0, 1, -1); native.tangents = tangents; }
            internal void Tick()
            {
                try { typeof(IslandParticleProjection).GetMethod("TickEffectMeshes", Members).Invoke(projection, null); }
                catch (TargetInvocationException error) { throw error.InnerException ?? error; }
            }
            internal void ExpectRebuild(string message) { int before = Counter("GeometryRebuildCount"); Tick(); Require(Counter("GeometryRebuildCount") == before + 1, message); }
            public void Dispose()
            {
                projection.Dispose(); EditorSceneManager.ClosePreviewScene(scene);
                if (styled != null) UnityEngine.Object.DestroyImmediate(styled.Mesh);
                UnityEngine.Object.DestroyImmediate(native); UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(AlternateMaterial); UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private static void Equal(Vector3[] a, Vector3[] b, string message) { Require(a.Length == b.Length, message); for (int i = 0; i < a.Length; i++) Require(a[i].Equals(b[i]), message); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
