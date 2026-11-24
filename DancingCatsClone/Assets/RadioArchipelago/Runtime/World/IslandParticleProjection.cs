using System;
using System.Collections.Generic;
using DancingCats.Clone;
using Unity.Profiling;
using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>
    /// Displays authoritative native tile effects on the curved island. Hidden
    /// logical systems keep running; mirrors never emit or advance simulation.
    /// Pools/buffers are retained across hits. Bind/quality changes may allocate;
    /// a previously unseen concurrency peak can grow a pool once, never per hit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IslandParticleProjection : MonoBehaviour, IDisposable
    {
        private sealed class Source
        {
            internal OriginalTile owner;
            internal ParticleSystem system;
            internal ParticleSystemRenderer renderer;
            internal ParticleSystemCullingMode originalCulling;
            internal bool musicalNotes;
            internal bool collectibleMarker;
            internal float finishLaneAnchor;
            internal Pool pool;
            internal Mirror mirror;
        }

        private sealed class Pool
        {
            internal ParticleSystem prototype;
            internal int sourceCount;
            internal readonly List<Mirror> mirrors = new List<Mirror>();
        }

        private sealed class Mirror
        {
            internal ParticleSystem system;
            internal ParticleSystemRenderer renderer;
            internal ParticleSystem.Particle[] particles;
            internal MaterialPropertyBlock properties = new MaterialPropertyBlock();
            internal bool busy;
            internal int readyFrame;
        }

        private sealed class Trail
        {
            internal OriginalTile owner;
            internal TrailRenderer source;
            internal LineRenderer view;
            internal Vector3[] points;
            internal MaterialPropertyBlock properties = new MaterialPropertyBlock();
        }

        private sealed class EffectMesh
        {
            internal OriginalTile owner;
            internal Renderer source;
            internal SkinnedMeshRenderer skin;
            internal Mesh sourceMesh, baked, viewMesh;
            internal MeshRenderer view;
            internal MeshFilter filter;
            internal bool originalUpdateWhenOffscreen;
            internal bool ivoryHoldBody;
            internal bool whiteHoldTracer;
            internal bool whiteHoldBand;
            internal bool collectibleMarker;
            internal float tracerOpacity;
            internal float finishLaneAnchor;
            internal Vector3[] vertices, originalVertices;
            internal Vector3[] normals, originalNormals;
            internal Vector4[] tangents, originalTangents;
            internal VertexSample[] samples;
            internal int[] triangles;
            internal float[] chartZ;
            internal List<Vector3> bakedVertices;
            internal List<Vector3> bakedNormals;
            internal List<Vector4> bakedTangents;
            internal Vector3[] cachedBakedVertices, cachedBakedNormals;
            internal Vector4[] cachedBakedTangents;
            internal bool geometryCached;
            internal Matrix4x4 cachedSourceMatrix;
            internal Vector3 cachedPivot;
            internal float cachedOffset, cachedScale, cachedStartDistance, cachedDistanceScale, cachedTileElevation, cachedLateralScale, cachedLaneAnchor;
            internal MaterialPropertyBlock properties = new MaterialPropertyBlock();
        }

        private struct VertexSample
        {
            internal int a, b, c;
            internal Vector3 weights;
        }

        private struct AnimationState
        {
            internal Animator animator;
            internal AnimatorCullingMode culling;
        }

        private readonly List<Source> sources = new List<Source>();
        private readonly Dictionary<string, Pool> pools = new Dictionary<string, Pool>();
        private readonly List<Trail> trails = new List<Trail>();
        private readonly List<EffectMesh> meshes = new List<EffectMesh>();
        private readonly List<AnimationState> animations = new List<AnimationState>();
        private readonly Dictionary<Material, Material> holdMaterials = new Dictionary<Material, Material>();
        private readonly Dictionary<Material, Material> tracerMaterials = new Dictionary<Material, Material>();
        private readonly Dictionary<Material, bool> noteMaterials = new Dictionary<Material, bool>();
        public const float NoteParticleSizeMultiplier = 2f;
        private static readonly Color HoldIvory = new Color(.48f, .80f, .79f, 1f);
        private static readonly Color HoldRim = new Color(.08f, .075f, .065f, 1f);
        private static readonly int MainColorId = Shader.PropertyToID("MainColor"), ColorId = Shader.PropertyToID("_Color"), BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FreColorId = Shader.PropertyToID("FreColor"), FresnelColorId = Shader.PropertyToID("_FresnelColor");
        private static readonly int HighlightColorId = Shader.PropertyToID("HLColor");
        private static readonly int TracerAlphaId = Shader.PropertyToID("Alpha");
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("Island.NativeFX.Projection"), ParticleMarker = new ProfilerMarker("Island.NativeFX.Particles"), TrailMarker = new ProfilerMarker("Island.NativeFX.Trails"), MeshMarker = new ProfilerMarker("Island.NativeFX.Meshes");
        private static readonly double TimestampToMilliseconds = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        private double totalCpuMilliseconds;
        private double totalParticleCpuMilliseconds, totalTrailCpuMilliseconds, totalMeshCpuMilliseconds;
        private IslandRoute route;
        private IslandStageDefinition definition;
        private IslandStagePresentation stage;
        private Transform staging;
        private int budget = 512;

        public int DebugActiveParticleCount { get; private set; }
        public int DebugActiveNoteParticleCount { get; private set; }
        public int MirroredCollectibleMarkerCount { get; private set; }
        public int DebugVisibleCollectibleMarkerCount { get; private set; }
        public int SuppressedHoldFlashSystemCount { get; private set; }
        public int debugActiveParticleCount => DebugActiveParticleCount;
        public int MirroredSystemCount => sources.Count;
        public int MirroredTrailCount => trails.Count;
        public int MirroredEffectMeshCount => meshes.Count;
        public int UnsupportedParticleTrailSystemCount { get; private set; }
        public int PoolGrowthCount { get; private set; }
        public int ReadableReplacementMeshCount { get; private set; }
        public int DebugVisibleEffectMeshCount { get; private set; }
        public int DebugVisibleEffectVertexCount { get; private set; }
        public int ProjectedEffectVertexCount { get; private set; }
        public int ProjectedEffectTriangleCount { get; private set; }
        public float MaxHoldTessellationChartEdge { get; private set; }
        public int ProjectionFrameCount { get; private set; }
        public int IvoryHoldBodyCount { get; private set; }
        public int MintHoldBodyCount => IvoryHoldBodyCount;
        public int WhiteHoldTracerCount { get; private set; }
        public float DebugMaxVisibleBandOpacity { get; private set; }
        public int DebugAdvancedHoldBandCount { get; private set; }
        public float DebugMaxHoldBandBehindHead { get; private set; }
        /// <summary>Raw chart extent outside the full support's spine interval, before its rounded caps.</summary>
        public float DebugMaxHoldBandOutsideSupport { get; private set; }
        /// <summary>Actual retained visible band vertices, without copying a mesh or advancing its source.</summary>
        public bool TryGetHoldBandReviewState(OriginalTile tile, out float minChartZ, out float maxChartZ, out int visibleViews)
        {
            minChartZ = float.PositiveInfinity; maxChartZ = float.NegativeInfinity; visibleViews = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                var effect = meshes[i];
                if (effect.owner != tile || !effect.whiteHoldBand || effect.view == null || !effect.view.enabled ||
                    !effect.view.gameObject.activeInHierarchy) continue;
                visibleViews++;
                for (int p = 0; p < effect.chartZ.Length; p++)
                {
                    minChartZ = Mathf.Min(minChartZ, effect.chartZ[p]);
                    maxChartZ = Mathf.Max(maxChartZ, effect.chartZ[p]);
                }
            }
            if (visibleViews == 0) { minChartZ = maxChartZ = 0; return false; }
            return true;
        }
        public int DebugVisibleNativeHoldLineCount { get; private set; }
        public int CpuSamples { get; private set; }
        public float LastCpuMilliseconds { get; private set; }
        public float PeakCpuMilliseconds { get; private set; }
        public float MeanCpuMilliseconds => CpuSamples > 0 ? (float)(totalCpuMilliseconds / CpuSamples) : 0;
        public float LastParticleCpuMilliseconds { get; private set; }
        public float PeakParticleCpuMilliseconds { get; private set; }
        public float MeanParticleCpuMilliseconds => CpuSamples > 0 ? (float)(totalParticleCpuMilliseconds / CpuSamples) : 0;
        public float LastTrailCpuMilliseconds { get; private set; }
        public float PeakTrailCpuMilliseconds { get; private set; }
        public float MeanTrailCpuMilliseconds => CpuSamples > 0 ? (float)(totalTrailCpuMilliseconds / CpuSamples) : 0;
        public float LastMeshCpuMilliseconds { get; private set; }
        public float PeakMeshCpuMilliseconds { get; private set; }
        public float MeanMeshCpuMilliseconds => CpuSamples > 0 ? (float)(totalMeshCpuMilliseconds / CpuSamples) : 0;
        public int GeometryRebuildCount { get; private set; }
        public int GeometryReuseCount { get; private set; }
        public int ProjectedVerticesThisFrame { get; private set; }
        public int ReusedVerticesThisFrame { get; private set; }
        public long GeometryProjectedVertexCount { get; private set; }
        public long GeometryReusedVertexCount { get; private set; }
        /// <summary>Diagnostic reference deformation on cache hits; leave disabled for CPU measurements.</summary>
        public bool ValidateGeometryCacheParity { get; set; }
        public int GeometryParityChecks { get; private set; }
        public int GeometryParityFailures { get; private set; }

        public void Initialize(IslandRoute islandRoute, IslandStageDefinition stageDefinition)
        {
            if (islandRoute == null) throw new ArgumentNullException(nameof(islandRoute));
            if (stageDefinition == null) throw new ArgumentNullException(nameof(stageDefinition));
            Clear(); route = islandRoute; definition = stageDefinition; stage = GetComponentInParent<IslandStagePresentation>();
            if (staging == null)
            {
                var node = new GameObject("Inactive native FX cloning buffer");
                node.SetActive(false); node.layer = 0; node.transform.SetParent(transform, false); staging = node.transform;
            }
        }

        public void Bind(IReadOnlyList<OriginalTile> tiles)
        {
            if (route == null || definition == null) throw new InvalidOperationException("Initialize the particle projection first.");
            ClearBindings();
            if (tiles == null) return;
            var seen = new HashSet<ParticleSystem>();
            var seenTrails = new HashSet<TrailRenderer>();
            var seenMeshes = new HashSet<Renderer>();
            var seenAnimations = new HashSet<Animator>();
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i]; if (tile == null) continue;
                foreach (var animator in tile.GetComponentsInChildren<Animator>(true))
                    if (seenAnimations.Add(animator))
                    { animations.Add(new AnimationState { animator = animator, culling = animator.cullingMode }); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
                foreach (var system in tile.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!seen.Add(system)) continue;
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    if (renderer == null) continue;
                    var owner = NativeOwner(system.transform, tile);
                    // These two native mesh flashes are stretched several
                    // metres and cut straight across a curved ride. They are
                    // decorative only: omit their view copies, preserving the
                    // native source, rings, note bursts and ride overlays.
                    if (IsHoldCrossFlash(system, renderer, owner)) { SuppressedHoldFlashSystemCount++; continue; }
                    var main = system.main;
                    string key = system.name + "/" + (system.transform.parent != null ? system.transform.parent.name : "") + "/"
                        + (renderer.sharedMaterial != null ? renderer.sharedMaterial.GetHashCode() : 0) + "/" + (int)renderer.renderMode + "/" + (int)renderer.alignment
                        + "/" + (renderer.mesh != null ? renderer.mesh.GetHashCode() : 0) + "/" + main.maxParticles;
                    Pool pool;
                    if (!pools.TryGetValue(key, out pool)) { pool = new Pool { prototype = system }; pools.Add(key, pool); }
                    pool.sourceCount++;
                    bool collectible = IsCollectibleMarkerSystem(system, owner);
                    sources.Add(new Source { owner = owner, system = system, renderer = renderer, pool = pool,
                        originalCulling = main.cullingMode, musicalNotes = UsesMusicalNoteTexture(renderer.sharedMaterial), collectibleMarker = collectible,
                        finishLaneAnchor = FinishLaneAnchor(system.transform, owner) });
                    if (collectible) MirroredCollectibleMarkerCount++;
                    // Camera-hidden original systems must not stop authoritative
                    // looping/hold particles through automatic render culling.
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    if (system.trails.enabled) UnsupportedParticleTrailSystemCount++;
                }
                foreach (var trail in tile.GetComponentsInChildren<TrailRenderer>(true))
                    if (seenTrails.Add(trail)) trails.Add(CreateTrail(trail, NativeOwner(trail.transform, tile)));
                foreach (var renderer in tile.GetComponentsInChildren<Renderer>(true))
                    if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && seenMeshes.Add(renderer))
                    {
                        var owner = NativeOwner(renderer.transform, tile);
                        if (IsNativeBody(owner) || IsEffectMesh(renderer.transform, owner.transform))
                        { var mesh = CreateEffectMesh(renderer, owner); if (mesh != null) meshes.Add(mesh); }
                    }
            }
            // Prewarm configurations, not every one of hundreds of tile emitters.
            // Eight overlapping instances cover ordinary native burst lifetimes.
            foreach (var pool in pools.Values)
                for (int i = 0; i < Mathf.Min(8, pool.sourceCount); i++) pool.mirrors.Add(CreateMirror(pool.prototype));
            PoolGrowthCount = 0;
        }

        public void SetParticleBudgetPerSystem(int value)
        {
            int next = Mathf.Clamp(value, 0, 4096);
            if (next == budget) return;
            budget = next;
            foreach (var pool in pools.Values)
                for (int i = 0; i < pool.mirrors.Count; i++) Resize(pool.mirrors[i], pool.prototype);
            for (int i = 0; i < trails.Count; i++) trails[i].points = new Vector3[Mathf.Max(1, Mathf.Min(512, budget))];
        }

        public void Tick()
        {
            long begin = System.Diagnostics.Stopwatch.GetTimestamp();
            using (TickMarker.Auto()) { TickProjection(); }
            LastCpuMilliseconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - begin) * TimestampToMilliseconds);
            PeakCpuMilliseconds = Mathf.Max(PeakCpuMilliseconds, LastCpuMilliseconds);
            totalCpuMilliseconds += LastCpuMilliseconds; CpuSamples++;
            totalParticleCpuMilliseconds += LastParticleCpuMilliseconds;
            totalTrailCpuMilliseconds += LastTrailCpuMilliseconds;
            totalMeshCpuMilliseconds += LastMeshCpuMilliseconds;
            PeakParticleCpuMilliseconds = Mathf.Max(PeakParticleCpuMilliseconds, LastParticleCpuMilliseconds);
            PeakTrailCpuMilliseconds = Mathf.Max(PeakTrailCpuMilliseconds, LastTrailCpuMilliseconds);
            PeakMeshCpuMilliseconds = Mathf.Max(PeakMeshCpuMilliseconds, LastMeshCpuMilliseconds);
        }

        private void TickProjection()
        {
            DebugActiveParticleCount = 0;
            DebugActiveNoteParticleCount = 0;
            DebugVisibleCollectibleMarkerCount = 0;
            LastParticleCpuMilliseconds = LastTrailCpuMilliseconds = LastMeshCpuMilliseconds = 0;
            if (route == null || definition == null) return;
            ProjectionFrameCount++;
            long passBegin = System.Diagnostics.Stopwatch.GetTimestamp();
            using (ParticleMarker.Auto())
            {
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                bool visible = budget > 0 && source.system != null && source.renderer != null && source.system.gameObject.activeInHierarchy && source.renderer.enabled && InChartWindow(source.owner);
                if (!visible || source.system.particleCount == 0) { ReturnMirror(source); continue; }
                if (source.mirror == null) source.mirror = Acquire(source.pool);
                var mirror = source.mirror;
                if (Time.frameCount < mirror.readyFrame) continue;
                if (!mirror.system.gameObject.activeSelf)
                {
                    mirror.system.gameObject.SetActive(true);
                    mirror.system.Play(false); mirror.system.Pause(false);
                }
                int count = source.system.GetParticles(mirror.particles);
                var main = source.system.main;
                Transform space = main.simulationSpace == ParticleSystemSimulationSpace.Local ? source.system.transform
                    : main.simulationSpace == ParticleSystemSimulationSpace.Custom ? main.customSimulationSpace : null;
                Vector3 origin = source.system.transform.position;
                float laneAnchor = stage != null && stage.GroundFinishPath != null && source.owner != null && source.owner.element.id == 10
                    ? source.finishLaneAnchor : source.owner != null ? source.owner.CenterX : origin.x;
                var originFrame = EvaluateFrame(origin.z);
                mirror.system.transform.position = MapGeometry(origin, originFrame, laneAnchor);
                mirror.system.transform.rotation = Quaternion.identity;
                mirror.system.transform.localScale = main.scalingMode == ParticleSystemScalingMode.Local ? source.system.transform.localScale
                    : main.scalingMode == ParticleSystemScalingMode.Hierarchy ? source.system.transform.lossyScale : Vector3.one;
                for (int p = 0; p < count; p++)
                {
                    var particle = mirror.particles[p];
                    Vector3 logical = space != null ? space.TransformPoint(particle.position) : particle.position;
                    Vector3 velocity = space != null ? space.TransformVector(particle.velocity) : particle.velocity;
                    var frame = EvaluateFrame(logical.z);
                    particle.startColor = particle.GetCurrentColor(source.system);
                    // Only native musical-note glyphs need greater phone-screen
                    // readability; rings, sparkles and reward emission stay native.
                    particle.startSize3D = particle.GetCurrentSize3D(source.system) * (source.musicalNotes ? NoteParticleSizeMultiplier : 1f);
                    // Enlarge the emitter's lane, not its burst spread, note
                    // glyph dimensions or locally authored particle velocity.
                    particle.position = MapGeometry(logical, frame, laneAnchor);
                    particle.velocity = frame.Right * velocity.x + Vector3.up * velocity.y +
                        (frame.Tangent + Vector3.up * frame.Grade) * (velocity.z * definition.DistanceScale);
                    if (source.renderer.renderMode == ParticleSystemRenderMode.Mesh || source.renderer.alignment == ParticleSystemRenderSpace.World || source.renderer.alignment == ParticleSystemRenderSpace.Local)
                    {
                        Quaternion rotation = Quaternion.Euler(particle.rotation3D);
                        if (space != null) rotation = space.rotation * rotation;
                        particle.rotation3D = (frame.Rotation * rotation).eulerAngles;
                    }
                    mirror.particles[p] = particle;
                }
                mirror.system.time = source.system.time;
                mirror.system.SetParticles(mirror.particles, count);
                mirror.renderer.enabled = source.renderer.enabled;
                mirror.renderer.sharedMaterial = source.renderer.sharedMaterial;
                source.renderer.GetPropertyBlock(mirror.properties); mirror.renderer.SetPropertyBlock(mirror.properties);
                DebugActiveParticleCount += count;
                if (source.musicalNotes) DebugActiveNoteParticleCount += count;
                if (source.collectibleMarker && count > 0) DebugVisibleCollectibleMarkerCount++;
            }
            }
            LastParticleCpuMilliseconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - passBegin) * TimestampToMilliseconds);
            passBegin = System.Diagnostics.Stopwatch.GetTimestamp();
            using (TrailMarker.Auto()) { TickTrails(); }
            LastTrailCpuMilliseconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - passBegin) * TimestampToMilliseconds);
            passBegin = System.Diagnostics.Stopwatch.GetTimestamp();
            using (MeshMarker.Auto()) { TickEffectMeshes(); }
            LastMeshCpuMilliseconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - passBegin) * TimestampToMilliseconds);
        }

        private Mirror Acquire(Pool pool)
        {
            for (int i = 0; i < pool.mirrors.Count; i++) if (!pool.mirrors[i].busy) { pool.mirrors[i].busy = true; return pool.mirrors[i]; }
            // Preserve actual effects if a song exceeds the prewarmed overlap;
            // retain the new instance for later hits and expose the cold growth.
            var mirror = CreateMirror(pool.prototype); mirror.busy = true; pool.mirrors.Add(mirror); PoolGrowthCount++; return mirror;
        }

        private Mirror CreateMirror(ParticleSystem prototype)
        {
            var clone = Instantiate(prototype.gameObject, staging, false);
            clone.name = "Projected native FX " + prototype.name; clone.SetActive(false); clone.layer = 0;
            for (int i = clone.transform.childCount - 1; i >= 0; i--) { var child = clone.transform.GetChild(i).gameObject; child.SetActive(false); Release(child); }
            var system = clone.GetComponent<ParticleSystem>();
            var renderer = clone.GetComponent<ParticleSystemRenderer>();
            foreach (var component in clone.GetComponents<Component>())
                if (!(component is Transform) && component != system && component != renderer)
                { var behaviour = component as Behaviour; if (behaviour != null) behaviour.enabled = false; var collider = component as Collider; if (collider != null) collider.enabled = false; Release(component); }
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.simulationSpeed = 0; main.gravityModifier = 0; main.stopAction = ParticleSystemStopAction.None;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var sub = system.subEmitters; sub.enabled = false;
            var velocity = system.velocityOverLifetime; velocity.enabled = false;
            var limit = system.limitVelocityOverLifetime; limit.enabled = false;
            var force = system.forceOverLifetime; force.enabled = false;
            var inherit = system.inheritVelocity; inherit.enabled = false;
            var external = system.externalForces; external.enabled = false;
            var noise = system.noise; noise.enabled = false;
            var collision = system.collision; collision.enabled = false;
            var trigger = system.trigger; trigger.enabled = false;
            var color = system.colorOverLifetime; color.enabled = false;
            var colorSpeed = system.colorBySpeed; colorSpeed.enabled = false;
            var size = system.sizeOverLifetime; size.enabled = false;
            var sizeSpeed = system.sizeBySpeed; sizeSpeed.enabled = false;
            var rotation = system.rotationOverLifetime; rotation.enabled = false;
            var rotationSpeed = system.rotationBySpeed; rotationSpeed.enabled = false;
            var lights = system.lights; lights.enabled = false;
            var particleTrails = system.trails; particleTrails.enabled = false;
            clone.transform.SetParent(transform, false);
            var result = new Mirror { system = system, renderer = renderer, readyFrame = Time.frameCount + 1 };
            Resize(result, prototype); return result;
        }

        private void Resize(Mirror mirror, ParticleSystem prototype)
        {
            int count = Mathf.Max(1, Mathf.Min(budget, prototype.main.maxParticles));
            mirror.particles = new ParticleSystem.Particle[count];
            var main = mirror.system.main; main.maxParticles = count;
        }

        private void ReturnMirror(Source source)
        {
            if (source.mirror == null) return;
            source.mirror.system.Clear(false); source.mirror.renderer.enabled = false; source.mirror.busy = false; source.mirror = null;
        }

        private Trail CreateTrail(TrailRenderer source, OriginalTile owner)
        {
            var node = new GameObject("Projected native trail " + source.name); node.layer = 0; node.transform.SetParent(transform, false);
            var line = node.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.loop = false;
            line.sharedMaterials = source.sharedMaterials; line.widthCurve = source.widthCurve; line.widthMultiplier = source.widthMultiplier;
            line.colorGradient = source.colorGradient; line.textureMode = source.textureMode; line.alignment = source.alignment;
            line.numCornerVertices = source.numCornerVertices; line.numCapVertices = source.numCapVertices;
            line.generateLightingData = source.generateLightingData; line.shadowCastingMode = source.shadowCastingMode;
            line.receiveShadows = source.receiveShadows; line.sortingLayerID = source.sortingLayerID; line.sortingOrder = source.sortingOrder;
            line.positionCount = 0;
            return new Trail { owner = owner, source = source, view = line, points = new Vector3[Mathf.Max(1, Mathf.Min(512, budget))] };
        }

        private void TickTrails()
        {
            for (int i = 0; i < trails.Count; i++)
            {
                var trail = trails[i]; var source = trail.source;
                float offset, scale; Vector3 pivot;
                if (budget == 0 || source == null || !source.enabled || !source.gameObject.activeInHierarchy || !VisualState(trail.owner, out offset, out scale, out pivot)) { trail.view.positionCount = 0; continue; }
                int count = Mathf.Min(source.positionCount, trail.points.Length);
                int sampleOffset = source.positionCount - count;
                if (sampleOffset == 0) source.GetPositions(trail.points);
                else for (int p = 0; p < count; p++) trail.points[p] = source.GetPosition(sampleOffset + p);
                trail.view.positionCount = count; trail.view.widthMultiplier = source.widthMultiplier;
                for (int p = 0; p < count; p++) trail.view.SetPosition(p, ApplyVisualState(Map(trail.points[p]), offset, scale, pivot));
                source.GetPropertyBlock(trail.properties); trail.view.SetPropertyBlock(trail.properties);
            }
        }

        private static bool IsEffectMesh(Transform node, Transform tile)
        {
            for (var current = node; current != null && current != tile; current = current.parent)
            {
                string name = current.name.ToLowerInvariant();
                if (name.Contains("effect") || name.Contains("hold_light") || name.Contains("trail") || name.Contains("note") || name.Contains("lizi")) return true;
            }
            return false;
        }

        private EffectMesh CreateEffectMesh(Renderer source, OriginalTile owner)
        {
            // The ceramic body and radio tracer replace these two view copies.
            // Keep the authoritative native sources and all other hit effects.
            if (definition.UnifiedHoldTiles && owner.element != null && (owner.element.id == 2 || owner.element.id == 3) &&
                (string.Equals(source.name.Trim(), "Hold", StringComparison.Ordinal) ||
                 string.Equals(source.name.Trim(), "Hold_Line", StringComparison.Ordinal))) return null;
            var skin = source as SkinnedMeshRenderer;
            var filter = source.GetComponent<MeshFilter>();
            Mesh shared = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
            if (shared == null) return null;
            Mesh readable = skin == null ? ReadableMesh(shared, owner) : null;
            Mesh baked = null;
            if (skin != null)
            {
                // BakeMesh can read an imported skinned surface without enabling
                // CPU access on its original shared asset.
                baked = new Mesh { name = "Native FX bake buffer" };
                skin.BakeMesh(baked, false);
            }
            var node = new GameObject("Projected native effect mesh " + source.name); node.layer = 0; node.transform.SetParent(transform, false);
            var template = baked != null ? baked : readable;
            var effect = new EffectMesh { owner = owner, source = source, skin = skin, sourceMesh = shared,
                finishLaneAnchor = FinishLaneAnchor(source.transform, owner),
                ivoryHoldBody = owner.element != null && (owner.element.id == 2 || owner.element.id == 3) && string.Equals(source.name.Trim(), "Hold", StringComparison.Ordinal),
                whiteHoldTracer = owner.element != null && (owner.element.id == 2 || owner.element.id == 3) && (string.Equals(source.name.Trim(), "Hold_Line", StringComparison.Ordinal) || string.Equals(source.name.Trim(), "Hold_Light", StringComparison.Ordinal)),
                whiteHoldBand = string.Equals(source.name.Trim(), "Hold_Light", StringComparison.Ordinal),
                collectibleMarker = owner.element != null &&
                    (owner.element.id == 5 && string.Equals(source.name, "Sphere", StringComparison.Ordinal) ||
                     owner.element.id == 9 && string.Equals(source.name, "Point", StringComparison.Ordinal)),
                tracerOpacity = string.Equals(source.name.Trim(), "Hold_Line", StringComparison.Ordinal) ? definition.HoldLineOpacity : definition.HoldBandOpacity,
                originalVertices = skin == null ? readable.vertices : null, baked = baked,
                bakedVertices = skin != null ? new List<Vector3>(template.vertexCount) : null,
                originalNormals = skin == null ? readable.normals : null, originalTangents = skin == null ? readable.tangents : null,
                bakedNormals = skin != null ? new List<Vector3>(template.vertexCount) : null, bakedTangents = skin != null ? new List<Vector4>(template.vertexCount) : null,
                originalUpdateWhenOffscreen = skin != null && skin.updateWhenOffscreen };
            if (skin != null) skin.updateWhenOffscreen = true;
            var mesh = BuildProjectedMesh(effect, template);
            if (skin != null)
            {
                effect.cachedBakedVertices = new Vector3[template.vertexCount];
                if (effect.normals != null) effect.cachedBakedNormals = new Vector3[template.vertexCount];
                if (effect.tangents != null) effect.cachedBakedTangents = new Vector4[template.vertexCount];
            }
            var viewFilter = node.AddComponent<MeshFilter>(); viewFilter.sharedMesh = mesh;
            var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterials = source.sharedMaterials;
            if (effect.ivoryHoldBody) { renderer.sharedMaterial = IvoryMaterial(source.sharedMaterial); IvoryHoldBodyCount++; }
            else if (effect.whiteHoldTracer) { renderer.sharedMaterial = WhiteTracerMaterial(source.sharedMaterial); WhiteHoldTracerCount++; }
            renderer.shadowCastingMode = source.shadowCastingMode; renderer.receiveShadows = source.receiveShadows;
            renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
            effect.viewMesh = mesh; effect.view = renderer; effect.filter = viewFilter;
            if (effect.collectibleMarker) MirroredCollectibleMarkerCount++;
            return effect;
        }

        private void TickEffectMeshes()
        {
            DebugVisibleEffectMeshCount = DebugVisibleEffectVertexCount = 0;
            ProjectedVerticesThisFrame = ReusedVerticesThisFrame = 0;
            DebugMaxVisibleBandOpacity = 0;
            DebugAdvancedHoldBandCount = DebugVisibleNativeHoldLineCount = 0;
            DebugMaxHoldBandBehindHead = DebugMaxHoldBandOutsideSupport = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                var effect = meshes[i]; var source = effect.source;
                float offset = 0, scale = 1; Vector3 pivot = Vector3.zero;
                bool visible = budget > 0 && source != null && source.enabled && source.gameObject.activeInHierarchy && VisualState(effect.owner, out offset, out scale, out pivot);
                effect.view.enabled = visible; if (!visible) continue;
                if (string.Equals(source.name.Trim(), "Hold_Line", StringComparison.Ordinal)) DebugVisibleNativeHoldLineCount++;
                if (effect.collectibleMarker) DebugVisibleCollectibleMarkerCount++;
                DebugVisibleEffectMeshCount++; DebugVisibleEffectVertexCount += effect.vertices.Length;
                if (effect.skin != null)
                {
                    effect.skin.BakeMesh(effect.baked, false); effect.baked.GetVertices(effect.bakedVertices);
                    effect.baked.GetNormals(effect.bakedNormals); effect.baked.GetTangents(effect.bakedTangents);
                    if (effect.bakedVertices.Count != effect.sourceMesh.vertexCount)
                        throw new InvalidOperationException("Native skinned FX topology changed after binding: " + source.name);
                }
                if (CanReuseGeometry(effect, offset, scale, pivot))
                {
                    GeometryReuseCount++; ReusedVerticesThisFrame += effect.vertices.Length; GeometryReusedVertexCount += effect.vertices.Length;
                    if (ValidateGeometryCacheParity) { ProjectEffectGeometry(effect, offset, scale, pivot, true); GeometryParityChecks++; }
                }
                else
                {
                    ProjectEffectGeometry(effect, offset, scale, pivot, false);
                    GeometryRebuildCount++; ProjectedVerticesThisFrame += effect.vertices.Length; GeometryProjectedVertexCount += effect.vertices.Length;
                    if (effect.owner.element != null && (effect.owner.element.id == 2 || effect.owner.element.id == 3))
                        for (int t = 0; t < effect.triangles.Length; t += 3)
                        {
                            float a = effect.chartZ[effect.triangles[t]], b = effect.chartZ[effect.triangles[t + 1]], c = effect.chartZ[effect.triangles[t + 2]];
                            MaxHoldTessellationChartEdge = Mathf.Max(Mathf.Max(MaxHoldTessellationChartEdge, Mathf.Abs(a - b)), Mathf.Max(Mathf.Abs(b - c), Mathf.Abs(c - a)));
                        }
                    effect.viewMesh.vertices = effect.vertices; effect.viewMesh.RecalculateBounds();
                    if (effect.normals != null) effect.viewMesh.normals = effect.normals; else effect.viewMesh.RecalculateNormals();
                    if (effect.tangents != null) effect.viewMesh.tangents = effect.tangents;
                    RememberGeometry(effect, offset, scale, pivot);
                }
                if (definition.UnifiedHoldTiles && effect.whiteHoldBand && effect.owner.consumed && stage != null &&
                    stage.TryGetHoldDrawRange(effect.owner, out float bandStart, out float bandEnd))
                {
                    DebugAdvancedHoldBandCount++;
                    for (int p = 0; p < effect.chartZ.Length; p++)
                        DebugMaxHoldBandOutsideSupport = Mathf.Max(DebugMaxHoldBandOutsideSupport,
                            Mathf.Max(bandStart - effect.chartZ[p], effect.chartZ[p] - bandEnd));
                }
                var material = effect.ivoryHoldBody ? IvoryMaterial(source.sharedMaterial) : effect.whiteHoldTracer ? WhiteTracerMaterial(source.sharedMaterial) : source.sharedMaterial;
                if (effect.view.sharedMaterial != material) effect.view.sharedMaterial = material;
                source.GetPropertyBlock(effect.properties);
                if (effect.ivoryHoldBody) SetIvoryOverrides(effect.properties);
                else if (effect.whiteHoldTracer)
                {
                    float alpha = SetWhiteTracerOverrides(effect.properties, source.sharedMaterial, effect.tracerOpacity);
                    if (effect.whiteHoldBand) DebugMaxVisibleBandOpacity = Mathf.Max(DebugMaxVisibleBandOpacity, alpha);
                }
                effect.view.SetPropertyBlock(effect.properties);
            }
        }

        private bool CanReuseGeometry(EffectMesh effect, float offset, float scale, Vector3 pivot)
        {
            // Unity's vector/matrix == operators are approximate. A cache hit
            // requires identical deformation inputs, including tiny animation.
            if (!effect.geometryCached || !effect.source.transform.localToWorldMatrix.Equals(effect.cachedSourceMatrix) ||
                !offset.Equals(effect.cachedOffset) || !scale.Equals(effect.cachedScale) || !pivot.Equals(effect.cachedPivot) ||
                !definition.StartDistance.Equals(effect.cachedStartDistance) || !definition.DistanceScale.Equals(effect.cachedDistanceScale) ||
                !definition.TileElevation.Equals(effect.cachedTileElevation) ||
                !definition.LateralScale.Equals(effect.cachedLateralScale) ||
                definition.LateralScale != 1 && !LaneAnchor(effect).Equals(effect.cachedLaneAnchor)) return false;
            return effect.skin == null || SameNativeData(effect.bakedVertices, effect.cachedBakedVertices) &&
                (effect.normals == null || SameNativeData(effect.bakedNormals, effect.cachedBakedNormals)) &&
                (effect.tangents == null || SameNativeData(effect.bakedTangents, effect.cachedBakedTangents));
        }

        private static bool SameNativeData<T>(List<T> current, T[] previous) where T : struct, IEquatable<T>
        {
            if (previous == null || current.Count != previous.Length) return false;
            for (int i = 0; i < previous.Length; i++) if (!current[i].Equals(previous[i])) return false;
            return true;
        }

        private void RememberGeometry(EffectMesh effect, float offset, float scale, Vector3 pivot)
        {
            effect.cachedSourceMatrix = effect.source.transform.localToWorldMatrix;
            effect.cachedOffset = offset; effect.cachedScale = scale; effect.cachedPivot = pivot;
            effect.cachedStartDistance = definition.StartDistance; effect.cachedDistanceScale = definition.DistanceScale; effect.cachedTileElevation = definition.TileElevation;
            effect.cachedLateralScale = definition.LateralScale; effect.cachedLaneAnchor = LaneAnchor(effect);
            if (effect.skin != null)
            {
                effect.bakedVertices.CopyTo(effect.cachedBakedVertices);
                if (effect.normals != null) effect.bakedNormals.CopyTo(effect.cachedBakedNormals);
                if (effect.tangents != null) effect.bakedTangents.CopyTo(effect.cachedBakedTangents);
            }
            effect.geometryCached = true;
        }

        private void ProjectEffectGeometry(EffectMesh effect, float offset, float scale, Vector3 pivot, bool validate)
        {
            var source = effect.source;
            var normalMatrix = source.transform.localToWorldMatrix.inverse.transpose;
            for (int p = 0; p < effect.vertices.Length; p++)
            {
                var sample = effect.samples[p];
                Vector3 a = effect.skin != null ? effect.bakedVertices[sample.a] : effect.originalVertices[sample.a];
                Vector3 b = effect.skin != null ? effect.bakedVertices[sample.b] : effect.originalVertices[sample.b];
                Vector3 c = effect.skin != null ? effect.bakedVertices[sample.c] : effect.originalVertices[sample.c];
                Vector3 local = a * sample.weights.x + b * sample.weights.y + c * sample.weights.z;
                Vector3 logical = source.transform.TransformPoint(local);
                // Keep the native Follow-bone animation: its translucent band
                // grows from the entry to the courier on a full-length support.
                var frame = EvaluateFrame(logical.z);
                bool wideFinish = effect.owner != null && effect.owner.element != null && effect.owner.element.id == 10 &&
                    (stage == null || stage.GroundFinishPath == null);
                Vector3 vertex = ApplyVisualState(wideFinish ? Map(logical, frame) : MapGeometry(logical, frame, LaneAnchor(effect)), offset, scale, pivot);
                if (validate) RequireGeometryParity(effect, vertex.Equals(effect.vertices[p]) && logical.z.Equals(effect.chartZ[p]), "position", p);
                else { effect.vertices[p] = vertex; effect.chartZ[p] = logical.z; }
                if (effect.normals != null)
                {
                    Vector3 na = effect.skin != null ? effect.bakedNormals[sample.a] : effect.originalNormals[sample.a];
                    Vector3 nb = effect.skin != null ? effect.bakedNormals[sample.b] : effect.originalNormals[sample.b];
                    Vector3 nc = effect.skin != null ? effect.bakedNormals[sample.c] : effect.originalNormals[sample.c];
                    Vector3 n = normalMatrix.MultiplyVector(na * sample.weights.x + nb * sample.weights.y + nc * sample.weights.z);
                    if (wideFinish) n.x /= definition.LateralScale;
                    Vector3 normal = definition.ElevationJson == null
                        ? (frame.Right * n.x + Vector3.up * n.y + frame.Tangent * n.z).normalized
                        : (frame.Right * n.x + Vector3.up * n.y + frame.Tangent * (n.z / definition.DistanceScale - frame.Grade * n.y)).normalized;
                    if (validate) RequireGeometryParity(effect, normal.Equals(effect.normals[p]), "normal", p); else effect.normals[p] = normal;
                }
                if (effect.tangents != null)
                {
                    Vector4 ta = effect.skin != null ? effect.bakedTangents[sample.a] : effect.originalTangents[sample.a];
                    Vector4 tb = effect.skin != null ? effect.bakedTangents[sample.b] : effect.originalTangents[sample.b];
                    Vector4 tc = effect.skin != null ? effect.bakedTangents[sample.c] : effect.originalTangents[sample.c];
                    Vector4 t = ta * sample.weights.x + tb * sample.weights.y + tc * sample.weights.z;
                    Vector3 direction = source.transform.TransformVector(new Vector3(t.x, t.y, t.z));
                    if (wideFinish) direction.x *= definition.LateralScale;
                    direction = definition.ElevationJson == null
                        ? (frame.Right * direction.x + Vector3.up * direction.y + frame.Tangent * direction.z).normalized
                        : (frame.Right * direction.x + Vector3.up * direction.y +
                            (frame.Tangent + Vector3.up * frame.Grade) * (direction.z * definition.DistanceScale)).normalized;
                    var tangent = new Vector4(direction.x, direction.y, direction.z, t.w < 0 ? -1 : 1);
                    if (validate) RequireGeometryParity(effect, tangent.Equals(effect.tangents[p]), "tangent", p); else effect.tangents[p] = tangent;
                }
            }
        }

        private void RequireGeometryParity(EffectMesh effect, bool equal, string channel, int vertex)
        {
            if (equal) return;
            GeometryParityFailures++;
            throw new InvalidOperationException("Cached native FX " + channel + " differs from reference deformation: " + effect.source.name + " vertex " + vertex);
        }

        private Mesh ReadableMesh(Mesh original, OriginalTile owner)
        {
            if (original.isReadable) return original;
            var from = definition.FxSourceMeshes; var to = definition.FxReadableMeshes;
            if (from != null && to != null)
                for (int i = 0; i < Mathf.Min(from.Length, to.Length); i++)
                    if (from[i] == original && to[i] != null && to[i].isReadable)
                    { ReadableReplacementMeshCount++; return to[i]; }
            throw new InvalidOperationException("The island needs a readable pilot FX mesh copy for '" + original.name + "' on " + owner.name + ". Populate IslandStageDefinition.FxSourceMeshes/FxReadableMeshes; original assets must remain unchanged.");
        }

        private Mesh BuildProjectedMesh(EffectMesh effect, Mesh template)
        {
            var native = template.vertices;
            var samples = new List<VertexSample>();
            var indices = new List<int>[template.subMeshCount];
            bool hold = effect.owner.element != null && (effect.owner.element.id == 2 || effect.owner.element.id == 3);
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            for (int i = 0; i < native.Length; i++) { float z = effect.source.transform.TransformPoint(native[i]).z; minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z); }
            float span = Mathf.Max(0, OwnerEnd(effect.owner) - effect.owner.initialZ);
            float stretch = hold && effect.skin != null && maxZ - minZ < span * .6f ? span / Mathf.Max(.05f, maxZ - minZ) : 1;
            for (int sub = 0; sub < indices.Length; sub++)
            {
                indices[sub] = new List<int>(); var triangles = template.GetTriangles(sub);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    float az = effect.source.transform.TransformPoint(native[a]).z, bz = effect.source.transform.TransformPoint(native[b]).z, cz = effect.source.transform.TransformPoint(native[c]).z;
                    int n = hold ? Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(Mathf.Abs(az - bz), Mathf.Abs(bz - cz), Mathf.Abs(cz - az)) * stretch / .25f), 1, 128) : 1;
                    // At binding the native filled-progress band can be almost
                    // collapsed between Start and Follow. Reserve enough rows
                    // for its later full native Follow fill, not that tiny pose.
                    if (definition.UnifiedHoldTiles && effect.whiteHoldBand)
                        n = Mathf.Max(n, Mathf.Clamp(Mathf.CeilToInt(span / .5f), 1, 128));
                    int first = samples.Count;
                    for (int row = 0; row <= n; row++) for (int col = 0; col <= n - row; col++)
                        samples.Add(new VertexSample { a = a, b = b, c = c, weights = new Vector3(1f - (float)(row + col) / n, (float)row / n, (float)col / n) });
                    for (int row = 0; row < n; row++) for (int col = 0; col < n - row; col++)
                    {
                        int p = first + GridRow(row, n) + col, q = first + GridRow(row + 1, n) + col;
                        indices[sub].Add(p); indices[sub].Add(q); indices[sub].Add(p + 1);
                        if (col < n - row - 1) { indices[sub].Add(p + 1); indices[sub].Add(q); indices[sub].Add(q + 1); }
                    }
                }
            }
            effect.samples = samples.ToArray(); effect.vertices = new Vector3[samples.Count]; effect.chartZ = new float[samples.Count];
            if (template.normals.Length == native.Length) effect.normals = new Vector3[samples.Count];
            if (template.tangents.Length == native.Length) effect.tangents = new Vector4[samples.Count];
            var allTriangles = new List<int>(); for (int sub = 0; sub < indices.Length; sub++) allTriangles.AddRange(indices[sub]); effect.triangles = allTriangles.ToArray();
            ProjectedEffectVertexCount += samples.Count; ProjectedEffectTriangleCount += allTriangles.Count / 3;
            var mesh = new Mesh { name = "Curved native " + effect.source.name, indexFormat = samples.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.MarkDynamic(); mesh.vertices = effect.vertices; mesh.subMeshCount = indices.Length;
            for (int sub = 0; sub < indices.Length; sub++) mesh.SetTriangles(indices[sub], sub, false);
            for (int channel = 0; channel < 8; channel++)
            {
                var uv = new List<Vector4>(); template.GetUVs(channel, uv); if (uv.Count != native.Length) continue;
                var mapped = new List<Vector4>(samples.Count);
                for (int i = 0; i < samples.Count; i++) { var s = samples[i]; mapped.Add(uv[s.a] * s.weights.x + uv[s.b] * s.weights.y + uv[s.c] * s.weights.z); }
                mesh.SetUVs(channel, mapped);
            }
            var colors = template.colors;
            if (colors.Length == native.Length)
            {
                var mapped = new Color[samples.Count];
                for (int i = 0; i < mapped.Length; i++) { var s = samples[i]; mapped[i] = colors[s.a] * s.weights.x + colors[s.b] * s.weights.y + colors[s.c] * s.weights.z; }
                mesh.colors = mapped;
            }
            return mesh;
        }

        private static int GridRow(int row, int n) => row * (n + 1) - row * (row - 1) / 2;
        private static OriginalTile NativeOwner(Transform child, OriginalTile fallback) => child.GetComponentInParent<OriginalTile>() ?? fallback;
        private static bool IsNativeBody(OriginalTile owner) => owner != null && owner.element != null && (owner.element.id == 2 || owner.element.id == 3 || owner.element.id == 5 || owner.element.id == 9);
        private static float OwnerEnd(OriginalTile owner) => owner.element != null && (owner.element.id == 2 || owner.element.id == 3 || owner.element.id == 4)
            ? Mathf.Max(owner.initialZ, owner.segment != null ? owner.segment.endZ : owner.initialZ + owner.element.mfendPos) : owner.initialZ;

        private bool InChartWindow(OriginalTile owner)
        {
            if (owner == null || owner.owner == null || owner.owner.Actor == null) return false;
            float z = owner.owner.Actor.position.z, scale = Mathf.Max(.01f, definition.DistanceScale);
            return OwnerEnd(owner) >= z - definition.VisibleBehind / scale && owner.initialZ <= z + definition.VisibleAhead / scale;
        }

        private bool VisualState(OriginalTile owner, out float offset, out float scale, out Vector3 pivot)
        {
            offset = 0; scale = 1; pivot = Vector3.zero;
            if (!InChartWindow(owner)) return false;
            if (owner.element != null && (owner.element.id == 6 || owner.element.id == 8) && !owner.consumed) return false;
            bool visible;
            if (stage != null && stage.TryGetTileVisualState(owner, out visible, out offset, out scale))
            { stage.TryGetTileVisualPivot(owner, out pivot); return visible; }
            float speed = owner.owner.Map != null ? owner.owner.Map.forwardSpeed * owner.owner.PlaybackRate : 1;
            float grace = stage != null && owner.element != null && (owner.element.id == 2 || owner.element.id == 3) ? stage.HoldExitGrace : Mathf.Max(.01f, speed) * .5f;
            return !owner.consumed || owner.owner.Actor.position.z <= OwnerEnd(owner) + grace;
        }

        private static Vector3 ApplyVisualState(Vector3 point, float offset, float scale, Vector3 pivot)
        { point.x = pivot.x + (point.x - pivot.x) * scale; point.z = pivot.z + (point.z - pivot.z) * scale; point.y += offset; return point; }

        private Material IvoryMaterial(Material original)
        {
            if (original == null) return null;
            Material copy;
            if (holdMaterials.TryGetValue(original, out copy)) return copy;
            var ivoryShader = definition.HoldBodyShader;
            if (ivoryShader == null) throw new InvalidOperationException("The island definition must reference the Hold body shader for Player builds.");
            copy = new Material(original) { name = original.name + " (island mint Hold body)", shader = ivoryShader };
            // The pilot shader neutralizes baked blue hue while retaining the
            // original atlas brightness, bevel shading, textures and UVs.
            SetColor(copy, MainColorId, HoldIvory); SetColor(copy, ColorId, HoldIvory); SetColor(copy, BaseColorId, HoldIvory);
            SetColor(copy, FreColorId, HoldRim); SetColor(copy, FresnelColorId, HoldRim);
            holdMaterials.Add(original, copy); return copy;
        }

        private static void SetIvoryOverrides(MaterialPropertyBlock properties)
        {
            properties.SetColor(MainColorId, HoldIvory); properties.SetColor(ColorId, HoldIvory); properties.SetColor(BaseColorId, HoldIvory);
            properties.SetColor(FreColorId, HoldRim); properties.SetColor(FresnelColorId, HoldRim);
        }

        private Material WhiteTracerMaterial(Material original)
        {
            if (original == null) return null;
            Material copy;
            if (tracerMaterials.TryGetValue(original, out copy)) return copy;
            // Native Hold_Light is opaque Ecolor; changing color alpha cannot
            // make it transparent. Reuse the native alpha-blended line shader
            // on view-only copies, leaving the original material untouched.
            if (definition.HoldTracerShader == null) throw new InvalidOperationException("The island definition must reference the transparent Hold tracer shader.");
            copy = new Material(original) { name = original.name + " (island translucent white native tracer)", shader = definition.HoldTracerShader };
            copy.renderQueue = Mathf.Max(3001, original.renderQueue);
            copy.SetFloat("Alpha", original.HasProperty("Alpha") ? original.GetFloat("Alpha") : 1);
            SetColor(copy, ColorId, Color.white); SetColor(copy, MainColorId, Color.white); SetColor(copy, BaseColorId, Color.white);
            SetColor(copy, HighlightColorId, Color.white); SetColor(copy, FreColorId, Color.white); SetColor(copy, FresnelColorId, Color.white);
            tracerMaterials.Add(original, copy); return copy;
        }

        private static float SetWhiteTracerOverrides(MaterialPropertyBlock properties, Material original, float opacity)
        {
            // Animation can drive Alpha in the source property block. Multiply
            // its current value rather than replacing the native end fade.
            float alpha = properties.HasFloat(TracerAlphaId) ? properties.GetFloat(TracerAlphaId) :
                original != null && original.HasProperty(TracerAlphaId) ? original.GetFloat(TracerAlphaId) : 1;
            properties.SetColor(ColorId, Color.white); properties.SetColor(MainColorId, Color.white); properties.SetColor(BaseColorId, Color.white);
            properties.SetColor(HighlightColorId, Color.white); properties.SetColor(FreColorId, Color.white); properties.SetColor(FresnelColorId, Color.white);
            properties.SetFloat(TracerAlphaId, Mathf.Clamp01(alpha) * opacity);
            return Mathf.Clamp01(alpha) * opacity;
        }

        private static void SetColor(Material material, int property, Color color) { if (material.HasProperty(property)) material.SetColor(property, color); }

        private bool UsesMusicalNoteTexture(Material material)
        {
            if (material == null) return false;
            if (noteMaterials.TryGetValue(material, out bool musicalNotes)) return musicalNotes;
            // Imported shader variants expose this texture through different
            // property names. Inspect the actual atlas once while binding.
            foreach (string property in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(property);
                if (texture != null && string.Equals(texture.name, "Lizi_Yinfu", StringComparison.Ordinal))
                { musicalNotes = true; break; }
            }
            noteMaterials.Add(material, musicalNotes); return musicalNotes;
        }

        private static bool IsCollectibleMarkerSystem(ParticleSystem system, OriginalTile owner)
        {
            if (owner == null || owner.element == null || owner.element.id != 5) return false;
            var main = system.main;
            if (!main.loop || !main.playOnAwake) return false;
            for (var parent = system.transform; parent != null && parent != owner.transform; parent = parent.parent)
                if (string.Equals(parent.name, "Sphere", StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsHoldCrossFlash(ParticleSystem system, ParticleSystemRenderer renderer, OriginalTile owner)
        {
            if (owner == null || owner.element == null || renderer.renderMode != ParticleSystemRenderMode.Mesh ||
                (owner.element.id != 2 && owner.element.id != 3 && owner.element.id != 9) ||
                (!string.Equals(system.name, "Light1", StringComparison.Ordinal) && !string.Equals(system.name, "Light2", StringComparison.Ordinal))) return false;
            for (var parent = system.transform.parent; parent != null && parent != owner.transform; parent = parent.parent)
                if (parent.name.StartsWith("Effect_Point_", StringComparison.Ordinal)) return true;
            return false;
        }

        private IslandRoute.Frame EvaluateFrame(float chartZ) => stage != null && stage.GroundFinishPath != null ?
            stage.EvaluatePresentationFrame(definition.StartDistance + chartZ * definition.DistanceScale) :
            route.Evaluate(definition.StartDistance + chartZ * definition.DistanceScale);
        private Vector3 Map(Vector3 logical) => Map(logical, EvaluateFrame(logical.z));
        // Trails contain trajectory centers; their LineRenderer width remains
        // native. Meshes and particle bursts instead keep their local X shape.
        private Vector3 Map(Vector3 logical, IslandRoute.Frame frame) => frame.Position + frame.Right * (logical.x * definition.LateralScale) + Vector3.up * (logical.y + definition.TileElevation);
        private Vector3 MapGeometry(Vector3 logical, IslandRoute.Frame frame, float laneAnchor)
            => frame.Position + frame.Right * (logical.x + laneAnchor * (definition.LateralScale - 1)) + Vector3.up * (logical.y + definition.TileElevation);
        private float LaneAnchor(EffectMesh effect) => stage != null && stage.GroundFinishPath != null &&
            effect.owner != null && effect.owner.element.id == 10 ? effect.finishLaneAnchor :
            effect.owner != null ? effect.owner.CenterX : effect.source.transform.position.x;

        private static float FinishLaneAnchor(Transform source, OriginalTile owner)
        {
            if (owner == null || owner.element == null || owner.element.id != 10) return 0;
            for (var current = source; current != null && current != owner.transform; current = current.parent)
                if (current.name.StartsWith("Pickable", StringComparison.Ordinal) && current.GetComponent<Collider>() != null)
                    return current.position.x;
            return owner.CenterX;
        }

        private void ClearBindings()
        {
            for (int i = 0; i < sources.Count; i++)
                if (sources[i].system != null) { var main = sources[i].system.main; main.cullingMode = sources[i].originalCulling; }
            sources.Clear();
            foreach (var pool in pools.Values) for (int i = 0; i < pool.mirrors.Count; i++) Release(pool.mirrors[i].system.gameObject);
            pools.Clear();
            for (int i = 0; i < trails.Count; i++) Release(trails[i].view.gameObject); trails.Clear();
            for (int i = 0; i < meshes.Count; i++) { if (meshes[i].skin != null) meshes[i].skin.updateWhenOffscreen = meshes[i].originalUpdateWhenOffscreen; Release(meshes[i].view.gameObject); Release(meshes[i].viewMesh); Release(meshes[i].baked); } meshes.Clear();
            for (int i = 0; i < animations.Count; i++) if (animations[i].animator != null) animations[i].animator.cullingMode = animations[i].culling; animations.Clear();
            foreach (var material in holdMaterials.Values) Release(material); holdMaterials.Clear();
            foreach (var material in tracerMaterials.Values) Release(material); tracerMaterials.Clear();
            noteMaterials.Clear(); DebugActiveNoteParticleCount = 0;
            MirroredCollectibleMarkerCount = DebugVisibleCollectibleMarkerCount = 0;
            SuppressedHoldFlashSystemCount = 0;
            DebugActiveParticleCount = UnsupportedParticleTrailSystemCount = PoolGrowthCount = ReadableReplacementMeshCount = DebugVisibleEffectMeshCount = DebugVisibleEffectVertexCount = 0;
            ProjectedEffectVertexCount = ProjectedEffectTriangleCount = ProjectionFrameCount = IvoryHoldBodyCount = 0; MaxHoldTessellationChartEdge = 0;
            WhiteHoldTracerCount = CpuSamples = 0; LastCpuMilliseconds = PeakCpuMilliseconds = 0; totalCpuMilliseconds = 0;
            LastParticleCpuMilliseconds = PeakParticleCpuMilliseconds = LastTrailCpuMilliseconds = PeakTrailCpuMilliseconds = LastMeshCpuMilliseconds = PeakMeshCpuMilliseconds = 0;
            totalParticleCpuMilliseconds = totalTrailCpuMilliseconds = totalMeshCpuMilliseconds = 0;
            GeometryRebuildCount = GeometryReuseCount = ProjectedVerticesThisFrame = ReusedVerticesThisFrame = GeometryParityChecks = GeometryParityFailures = 0;
            GeometryProjectedVertexCount = GeometryReusedVertexCount = 0;
            DebugMaxVisibleBandOpacity = 0;
            DebugAdvancedHoldBandCount = DebugVisibleNativeHoldLineCount = 0;
            DebugMaxHoldBandBehindHead = DebugMaxHoldBandOutsideSupport = 0;
        }

        public void Clear() { ClearBindings(); }
        public void Dispose() { Clear(); if (staging != null) Release(staging.gameObject); staging = null; route = null; definition = null; }
        private void OnDestroy() { Dispose(); }
        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            var node = value as GameObject; if (node != null) node.SetActive(false);
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
