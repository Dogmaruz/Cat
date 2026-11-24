using System;
using System.Collections.Generic;
using DancingCats.Clone;
using UnityEngine;
using UnityEngine.Rendering;

namespace RadioArchipelago.World
{
    /// <summary>
    /// Optional island view of the unchanged chart-space simulation. No collider,
    /// chart position, timing, input, score or musical rule is moved by this view.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class IslandStagePresentation : MonoBehaviour
    {
        public const int HiddenLogicalLayer = 30;
        [SerializeField] private OriginalPlayController game;
        [SerializeField] private IslandStageDefinition definition;
        private readonly Dictionary<GameObject, int> hiddenLayers = new Dictionary<GameObject, int>();
        private readonly Dictionary<Light, bool> hiddenLights = new Dictionary<Light, bool>();
        private readonly List<TileProxy> proxies = new List<TileProxy>();
        private readonly List<Mesh> spanMeshes = new List<Mesh>();
        private readonly List<CoinProxy> finishCoins = new List<CoinProxy>();
        private Transform finishCoinRoot;
        private Transform islandModel;
        private IslandFinishGlade finishGlade;
        private Transform viewRoot, proxyRoot, actorAnchor;
        private Camera stageCamera;
        private CameraState cameraState;
        private ChildBinding courierBinding, lightingBinding;
        private CourierMotion courierMotion;
        private readonly IslandFinishDeparture finishDeparture = new IslandFinishDeparture();
        private int departureStartFrame = -1;
        private bool departureCourierHidden, departureCourierWasActive;
        private Vector3 departureCourierBaseScale = Vector3.one;
        private Vector3 departureCameraPosition, departureOffset;
        private Quaternion departureCameraRotation;
        private Mesh discMesh;
        private Material stone, brass, blue, coral, glow;
        private Material[] jumpMaterials, followMaterials, pointMaterials;
        private Material radioWhite;
        private Material[] radioMaterials;
        private bool cameraReady;
        private bool configured;
        private bool cameraPoseRequested;
        private Vector3 desiredCameraPosition;
        private Quaternion desiredCameraRotation;
        private Vector3 framedCameraTarget;
        private Vector3 smoothedCameraPosition;
        private Quaternion smoothedCameraRotation;
        private readonly Vector3[] cameraEnvelope = new Vector3[40];
        private int cameraEnvelopeCount;
        private Renderer[] courierRenderers = Array.Empty<Renderer>();
        private Vector4 clearViewActor, clearViewLanding;
        private static readonly int ClearViewCameraId = Shader.PropertyToID("_RA_ClearViewCamera");
        private static readonly int ClearViewActorId = Shader.PropertyToID("_RA_ClearViewActor");
        private static readonly int ClearViewLandingId = Shader.PropertyToID("_RA_ClearViewLanding");
        private static readonly int ClearViewFloorId = Shader.PropertyToID("_RA_ClearViewFloor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private IslandParticleProjection particleProjection;
        private IslandLivingNature livingNature;
        private IslandRooftopMotion rooftopMotion;
        private readonly IslandCloseCameraRig closeCamera = new IslandCloseCameraRig();
        private float cameraRouteDistance, cameraLateral;
        public Vector3 CameraRigAnchor { get; private set; }
        public bool CameraUsesFixedRig => definition != null && definition.UseCloseCamera;
        public IslandLivingNature LivingNature => livingNature;
        public int VisibleUnifiedHoldBodies { get; private set; }
        public int VisibleRadioHoldTracers { get; private set; }
        public float VisualJumpBoost { get; private set; }
        public float VisualFinishHoverOffset { get; private set; }
        public bool FinishDepartureStarted => finishDeparture.Started;
        public bool FinishDepartureComplete => finishDeparture.Complete;
        public int FinishDepartureStartCount => finishDeparture.StartCount;
        public int FinishDepartureCollectedCoinsAtStart => finishDeparture.CollectedCoinsAtStart;
        public float FinishDepartureProgress => finishDeparture.Progress;
        public float FinishDepartureVisualScale => finishDeparture.VisualScale;
        public Vector3 FinishDepartureOffset => departureOffset;
        public Vector3 FinishDepartureStartPosition => finishDeparture.StartPosition;
        public bool FinishDepartureCourierHidden => departureCourierHidden;
        private IslandPilotEnvironment quality;
        private int appliedParticleBudget = -1;
        private readonly Dictionary<OriginalTile, TileProxy> proxyBySource = new Dictionary<OriginalTile, TileProxy>();
        private readonly List<Vector2> visibleHoldChartRanges = new List<Vector2>();
        private readonly List<TileProxy> roundSupports = new List<TileProxy>();
        private readonly List<TileProxy> visibleRoundSupports = new List<TileProxy>();
        private int animationFrame = -1;

        public IslandStageDefinition Definition => definition;
        public IslandRoute Route { get; private set; }
        public IslandElevationProfile Elevation { get; private set; }
        public IslandGroundFinishPath GroundFinishPath { get; private set; }
        public IslandFinishGlade FinishGlade => finishGlade;
        public Camera Camera => stageCamera;
        public Vector3 WorldActorPosition => actorAnchor != null ? actorAnchor.position : Vector3.zero;
        public IslandParticleProjection ParticleProjection => particleProjection;
        public IslandParticleProjection Projection => particleProjection;
        public int ParticleCount => particleProjection != null ? particleProjection.DebugActiveParticleCount : 0;
        public int MirroredSystemCount => particleProjection != null ? particleProjection.MirroredSystemCount : 0;
        public int MirroredTrailCount => particleProjection != null ? particleProjection.MirroredTrailCount : 0;
        public int MirroredEffectMeshCount => particleProjection != null ? particleProjection.MirroredEffectMeshCount : 0;
        public float ActorSurfaceOffset { get; private set; }
        public float VisualContactOffset => ActorSurfaceOffset;
        /// <summary>Visible Hold intervals in unwrapped, authoritative chart coordinates.</summary>
        public IReadOnlyList<Vector2> VisibleHoldChartRanges => visibleHoldChartRanges;
        public int VisibleHoldCount => visibleHoldChartRanges.Count;
        public int VisibleExpiredHoldCount { get; private set; }
        public int BoundFinishCoinCount => finishCoins.Count;
        public int BoundSignalSparkCount { get; private set; }
        public int VisibleSignalSparkCount { get; private set; }
        public int VisibleConsumedSignalSparkCount { get; private set; }
        public int VisibleFinishCoinCount { get; private set; }
        public int VisibleGroundedFinishCoinCount { get; private set; }
        public float MinVisibleFinishCoinGroundClearance { get; private set; }
        public float MaxVisibleFinishCoinGroundClearance { get; private set; }
        public int VisibleFinishPlatformCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < proxies.Count; i++)
                {
                    var proxy = proxies[i];
                    if (proxy.source != null && proxy.source.element != null && proxy.source.element.id == 10 &&
                        proxy.renderer != null && proxy.renderer.enabled && proxy.renderer.gameObject.activeInHierarchy) count++;
                }
                return count;
            }
        }
        public int UnmappedFinishCoinCount { get; private set; }
        public int VisibleRoundOverlapCount { get; private set; }
        public float MinimumRoundSupportRadius { get; private set; }
        public int VisibleLandingOverlapCount => VisibleRoundOverlapCount;
        public float MinVisibleLandingRadius { get; private set; }
        /// <summary>Unwrapped chart metres allowed for native End/release animation.</summary>
        public float HoldExitGrace => Mathf.Min(4f, (game != null && game.Map != null ? Mathf.Max(0, game.Map.forwardSpeed) : 6.5f) * .55f);
        public int VisibleExitDiscCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < proxies.Count; i++)
                {
                    var proxy = proxies[i];
                    if (proxy.source != null && proxy.source.element != null && (proxy.source.element.id == 6 || proxy.source.element.id == 8) &&
                        proxy.view != null && proxy.view.gameObject.activeInHierarchy && proxy.renderer != null && proxy.renderer.enabled) count++;
                }
                return count;
            }
        }

        [Serializable]
        private sealed class RouteDocument
        {
            public Vector3[] points;
            public bool closed;
            public float roadWidth;
        }

        private sealed class TileProxy
        {
            internal OriginalTile source;
            internal Transform view;
            internal float start, end;
            internal bool span;
            internal MeshRenderer renderer;
            internal bool wasConsumed;
            internal Vector3 baseScale, spanOrigin;
            internal TileProxy parentSpan;
            internal SpringMode spring;
            internal bool wasSupported;
            internal bool round;
            internal IslandHoldSurface styledSpan;
            internal IslandRadioHoldTracer radioTracer;
            internal MeshRenderer radioRenderer;
            internal Vector3 drawnPosition;
            internal float drawnRadius;
            internal float elapsed, offset, releaseFrom, scale = 1f;
        }

        private sealed class CoinProxy
        {
            internal Collider source;
            internal Transform art;
            internal CoinPart[] parts;
            internal SignalSparkVisual spark;
            internal float phaseSeed;
        }

        private sealed class CoinPart
        {
            internal Transform source, view;
            internal MeshRenderer sourceRenderer, renderer;
            internal MaterialPropertyBlock properties = new MaterialPropertyBlock();
        }

        private enum SpringMode { Rest, Hit, Held, Release, Finished }

        private struct ChildBinding
        {
            internal Transform child, parent;
            internal Vector3 localPosition, localScale;
            internal Quaternion localRotation;
        }

        private struct CameraState
        {
            internal Camera camera;
            internal Vector3 position;
            internal Quaternion rotation;
            internal int cullingMask;
            internal float fieldOfView, nearClip, farClip;
            internal CameraClearFlags clearFlags;
            internal Color backgroundColor;
        }

        public void Configure(OriginalPlayController owner, IslandStageDefinition stage)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            Deactivate();
            game = owner;
            definition = stage;
            quality = GetComponent<IslandPilotEnvironment>();
            LoadRoute();
            configured = true;
        }

        private void LoadRoute()
        {
            if (definition == null || definition.RouteJson == null || definition.Prefab == null)
                throw new InvalidOperationException("Island stage requires its definition, prefab and route JSON.");
            var document = JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text);
            if (document == null || document.points == null || !document.closed)
                throw new InvalidOperationException("Island route JSON needs points [{x,y,z}] and closed:true.");
            if (document.roadWidth > 0 && Mathf.Abs(document.roadWidth - definition.RoadWidth) > .05f)
                throw new InvalidOperationException("Island definition road width disagrees with its route.");
            Elevation = definition.ElevationJson != null ? IslandElevationProfile.FromJson(definition.ElevationJson.text) : null;
            Route = new IslandRoute(document.points, Elevation);
        }

        public bool IsActiveFor(string songId)
        {
            return isActiveAndEnabled && definition != null &&
                   !string.IsNullOrEmpty(songId) && string.Equals(definition.SongId, songId, StringComparison.Ordinal);
        }

        public void PrepareEnvironment(Transform railway, Camera camera)
        {
            if (game == null || !IsActiveFor(game.Selected?.id)) { Deactivate(); return; }
            if (!configured || Route == null) { LoadRoute(); configured = true; }
            quality = GetComponent<IslandPilotEnvironment>();
            // Restore source simulation settings before BuildTrack disposes the
            // previous authoritative chart hierarchy on a retry/song change.
            if (particleProjection != null) particleProjection.Clear();
            RestoreHiddenObjects();
            RestoreCamera();
            EnsureView();
            HideAdditionalLogical(railway);
            stageCamera = camera;
            if (camera != null)
            {
                cameraState = new CameraState
                {
                    camera = camera, position = camera.transform.position, rotation = camera.transform.rotation,
                    cullingMask = camera.cullingMask, fieldOfView = camera.fieldOfView,
                    nearClip = camera.nearClipPlane, farClip = camera.farClipPlane,
                    clearFlags = camera.clearFlags, backgroundColor = camera.backgroundColor
                };
                camera.cullingMask &= ~(1 << HiddenLogicalLayer);
                camera.fieldOfView = definition.CameraFieldOfView;
                camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, .08f);
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 350f);
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = definition.SkyColor;
            }
            cameraReady = false;
            closeCamera.Reset();
        }

        private void EnsureView()
        {
            if (viewRoot != null) return;
            viewRoot = new GameObject("Island stage world view").transform;
            viewRoot.SetParent(transform, false);
            viewRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var island = Instantiate(definition.Prefab, viewRoot, false);
            island.name = definition.Prefab.name;
            islandModel = island.transform;
            rooftopMotion = new IslandRooftopMotion();
            rooftopMotion.Configure(islandModel, viewRoot, definition.RooftopMotionJson);
            proxyRoot = new GameObject("Route tile visual proxies").transform;
            proxyRoot.SetParent(viewRoot, false);
            actorAnchor = new GameObject("Courier route pose").transform;
            actorAnchor.SetParent(viewRoot, false);
            CreateMaterials();
            discMesh = CreateDiscMesh(definition.TileThickness);
            particleProjection = viewRoot.gameObject.AddComponent<IslandParticleProjection>();
            particleProjection.Initialize(Route, definition);
            if (definition.LivingNatureEnabled)
            {
                livingNature = viewRoot.gameObject.AddComponent<IslandLivingNature>();
                livingNature.Configure(definition, Route);
            }
            ApplyParticleBudget();
        }

        public void BindTrack(IReadOnlyList<OriginalTile> tiles)
        {
            if (game == null || !IsActiveFor(game.Selected?.id)) return;
            EnsureView();
            ClearProxies();
            if (tiles == null) return;
            ConfigureGroundFinish(tiles);
            for (int i = 0; i < tiles.Count; i++)
            {
                var source = tiles[i];
                if (source == null || source.element == null) continue;
                HideRenderers(source.transform);
                // Hold exits and FollowEnd are logical triggers. Adding discs
                // to those positions invents landing surfaces ahead of a ride.
                if (source.element.id == 7 || source.element.id == 6 || source.element.id == 8) continue;
                bool span = (source.element.id == 2 || source.element.id == 3) && source.segment != null;
                float start = source.element.pos.z;
                float end = span || source.element.id == 4
                    ? Mathf.Max(start, source.segment != null ? source.segment.endZ : start + source.element.mfendPos)
                    : start;
                var node = new GameObject("Island " + source.element.kind + " " + source.element.sourceIndex);
                node.transform.SetParent(proxyRoot, false);
                var filter = node.AddComponent<MeshFilter>();
                var renderer = node.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                // Preserve the recovered white Hold body, line and checkpoint
                // models through IslandParticleProjection. These references
                // only own spring/contact state and never draw over that art.
                renderer.enabled = (!span || definition.UnifiedHoldTiles) && source.element.id != 9 && source.element.id != 5 &&
                    !(GroundFinishPath != null && source.element.id == 10);
                var proxyMaterials = source.element.id == 4 || source.element.id == 6 ? followMaterials :
                    source.checkpoint || source.element.id == 5 || source.element.id == 9 ? pointMaterials : jumpMaterials;
                renderer.sharedMaterials = proxyMaterials;
                IslandHoldSurface styled = null;
                IslandRadioHoldTracer radioTracer = null;
                MeshRenderer radioRenderer = null;
                if (span)
                {
                    if (definition.UnifiedHoldTiles)
                        styled = new IslandHoldSurface(Route, definition, start, end, source.element.pos.x,
                            source.element.pos.y, MeasureHalfWidth(source));
                    var mesh = styled != null ? styled.Mesh : CreateSpanMesh(source, MeasureHalfWidth(source));
                    Vector3 pivot = mesh.bounds.center;
                    if (styled != null) pivot = styled.Pivot;
                    else
                    {
                        var localVertices = mesh.vertices;
                        for (int v = 0; v < localVertices.Length; v++) localVertices[v] -= pivot;
                        mesh.vertices = localVertices; mesh.RecalculateBounds();
                    }
                    node.transform.position = pivot;
                    spanMeshes.Add(mesh);
                    filter.sharedMesh = mesh;
                    if (styled != null)
                    {
                        radioTracer = new IslandRadioHoldTracer(Route, definition, start, end, source.element.pos.x,
                            source.element.pos.y, styled.Pivot, MeasureHalfWidth(source));
                        spanMeshes.Add(radioTracer.Mesh);
                        var guide = new GameObject("Island radio Hold tracer");
                        guide.transform.SetParent(node.transform, false);
                        guide.AddComponent<MeshFilter>().sharedMesh = radioTracer.Mesh;
                        radioRenderer = guide.AddComponent<MeshRenderer>();
                        radioRenderer.sharedMaterials = radioMaterials;
                        radioRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        radioRenderer.receiveShadows = false;
                    }
                }
                else
                {
                    filter.sharedMesh = discMesh;
                    float radius = source.checkpoint || source.element.id == 5 || source.element.id == 9 ? .26f :
                        definition.UniformRoundTiles && IsOrdinaryRound(source.element) ? IslandSupportSpacing.NativeJumpRadius : MeasureRadius(source);
                    node.transform.localScale = new Vector3(radius, 1, radius);
                    // The finish accepts the entire corridor. Its arrival
                    // platform widens with the lanes; ordinary discs do not.
                    if (source.element.id == 10 && definition.LateralScale != 1)
                        node.transform.localScale = new Vector3(Mathf.Max(radius, NativeRhythmRules.HorizontalLimit) * definition.LateralScale, 1, radius);
                }
                node.SetActive(false);
                var proxy = new TileProxy { source = source, view = node.transform, start = start, end = end, span = span,
                    renderer = renderer, wasConsumed = source.consumed, baseScale = node.transform.localScale,
                    spanOrigin = node.transform.position, round = renderer.enabled && !span && !source.checkpoint &&
                        (source.element.id != 10 || definition.LateralScale == 1), styledSpan = styled,
                    radioTracer = radioTracer, radioRenderer = radioRenderer };
                proxies.Add(proxy); proxyBySource[source] = proxy;
                if (proxy.round) roundSupports.Add(proxy);
            }
            FitRoundSupports();
            if (visibleHoldChartRanges.Capacity < proxies.Count) visibleHoldChartRanges.Capacity = proxies.Count;
            for (int i = 0; i < proxies.Count; i++)
            {
                var parent = proxies[i].source.transform.parent;
                var owner = parent != null ? parent.GetComponentInParent<OriginalTile>() : null;
                if (owner != null && proxyBySource.TryGetValue(owner, out var span) && span.span) proxies[i].parentSpan = span;
            }
            if (particleProjection != null) particleProjection.Bind(tiles);
        }

        private void ConfigureGroundFinish(IReadOnlyList<OriginalTile> tiles)
        {
            if (!definition.EnableGroundFinish) return;
            OriginalTile takeoff = null, landing = null;
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (tile == null || tile.element == null) continue;
                if (tile.element.kind == "FinishJump") takeoff = tile;
                if (tile.element.id == 10) landing = tile;
            }
            if (takeoff == null || landing == null) return;
            float end = landing.element.pos.z + 29;
            foreach (var child in landing.GetComponentsInChildren<Transform>(true))
                if (child.name == "FinalTile01") end = Mathf.Max(end, child.position.z + 2);
            GroundFinishPath = new IslandGroundFinishPath(Route, definition.StartDistance, definition.DistanceScale,
                definition.TileElevation, takeoff.element.pos.z, landing.element.pos.z, end, definition.FinishExitYaw);
            var node = new GameObject("Radioarchipelago final ground glade");
            node.transform.SetParent(viewRoot, false);
            // Its world footprint overlaps early laps. Construct it once, but
            // reveal it only when the unwrapped song reaches the real finale.
            node.SetActive(false);
            finishGlade = node.AddComponent<IslandFinishGlade>();
            var frame = GroundFinishPath.LandingFrame;
            finishGlade.Configure(frame.Position, frame.Tangent, GroundFinishPath.GroundHeight,
                Mathf.Max(34, GroundFinishPath.CoinRunLength), 14,
                FindIslandMaterial("Terrain"), FindIslandMaterial("Cliffs"),
                FindIslandMaterial("Grass"), FindIslandMaterial("Foliage"), 71237, definition.FinishTheme,
                definition.FinishTheme == IslandFinishTheme.MoonCrystalGarden ? FindIslandMaterial("Crystal") : null);
        }

        private Material FindIslandMaterial(string suffix)
        {
            if (islandModel != null)
                foreach (var renderer in islandModel.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null && material.name.IndexOf("_" + suffix, StringComparison.Ordinal) >= 0) return material;
            throw new InvalidOperationException("Ground finish needs the island's shared " + suffix + " material.");
        }

        /// <summary>One world frame for the courier, final coins, FX and camera; musical distances remain unchanged.</summary>
        public IslandRoute.Frame EvaluatePresentationFrame(float routeDistance) =>
            GroundFinishPath != null ? GroundFinishPath.Evaluate(routeDistance) : Route.Evaluate(routeDistance);

        /// <summary>Visible support surface, including the floating tile/ground presentation elevation.</summary>
        public float GetPresentationSurfaceY(float chartZ) =>
            EvaluatePresentationFrame(definition.StartDistance + chartZ * definition.DistanceScale).Position.y + definition.TileElevation;

        /// <summary>Airborne contact-height contribution, including TileElevation but excluding the native jump arc.</summary>
        public float GetFlightSurfaceY(Vector3 jumpStart, float jumpDistance, float chartZ)
        {
            if (GroundFinishPath != null && GroundFinishPath.IsFinalFlight(jumpStart, jumpDistance))
                return GroundFinishPath.FlightSurfaceY(jumpStart, jumpDistance, chartZ);
            return IslandFlightElevation.Sample(Elevation, definition.StartDistance, definition.DistanceScale,
                jumpStart, jumpDistance, chartZ) + definition.TileElevation;
        }

        private void FitRoundSupports()
        {
            var centers = new Vector3[roundSupports.Count];
            var chartDistances = new float[roundSupports.Count];
            var preferred = new float[roundSupports.Count];
            var radii = new float[roundSupports.Count];
            for (int i = 0; i < roundSupports.Count; i++)
            {
                var proxy = roundSupports[i];
                Vector3 logical = proxy.source.ContactPosition;
                var frame = Route.Evaluate(definition.StartDistance + logical.z * definition.DistanceScale);
                centers[i] = frame.Position + frame.Right * (logical.x * definition.LateralScale) + Vector3.up * logical.y;
                preferred[i] = proxy.baseScale.x;
                chartDistances[i] = logical.z;
            }
            if (definition.UnifiedHoldTiles || definition.UniformRoundTiles)
            {
                var elements = new MapElement[roundSupports.Count];
                for (int i = 0; i < elements.Length; i++) elements[i] = roundSupports[i].source.element;
                IslandDynamicSupportSpacing.Resolve(Route, definition, elements, preferred, radii, game.Map.forwardSpeed);
            }
            else IslandSupportSpacing.ResolveRadii(centers, chartDistances,
                (definition.VisibleAhead + definition.VisibleBehind) / definition.DistanceScale, preferred, radii);
            MinimumRoundSupportRadius = float.PositiveInfinity;
            for (int i = 0; i < roundSupports.Count; i++)
            {
                var proxy = roundSupports[i];
                if (definition.UniformRoundTiles && IsOrdinaryRound(proxy.source.element) &&
                    radii[i] < IslandSupportSpacing.NativeJumpRadius - .0001f)
                    throw new InvalidOperationException("Uniform round support " + proxy.source.element.sourceIndex +
                        " lacks full-size motion clearance; repair its authored lane instead of shrinking the tile.");
                float radius = definition.UniformRoundTiles && IsOrdinaryRound(proxy.source.element) ? IslandSupportSpacing.NativeJumpRadius : radii[i];
                proxy.baseScale.x = proxy.baseScale.z = radius;
                MinimumRoundSupportRadius = Mathf.Min(MinimumRoundSupportRadius, radius);
            }
        }

        private static bool IsOrdinaryRound(MapElement element) => element.id == 0 || element.id == 1 || element.id == 4;

        /// <summary>View-only copies of the exact native coins whose colliders award rewards.</summary>
        public void BindFinishCoins(IReadOnlyList<Collider> sourceCoins)
        {
            ClearFinishCoins();
            if (game == null || !IsActiveFor(game.Selected?.id) || sourceCoins == null) return;
            EnsureView();
            finishCoinRoot = new GameObject("Finish signal sparks").transform;
            finishCoinRoot.SetParent(viewRoot, false);
            var seen = new HashSet<Collider>();
            for (int i = 0; i < sourceCoins.Count; i++)
            {
                var source = sourceCoins[i];
                if (source == null || !seen.Add(source)) continue;
                // This is the same Art root deactivated by the authoritative
                // collection path in OriginalPlayController.
                var art = source.transform.Find("Node1/Pickable/Art") ?? source.transform.Find("Art");
                if (art == null) { UnmappedFinishCoinCount++; continue; }
                if (definition.FinishCurrencyPrefab != null)
                {
                    var spark = Instantiate(definition.FinishCurrencyPrefab, finishCoinRoot, false);
                    spark.name = "Signal spark " + i;
                    spark.SetCamera(stageCamera);
                    spark.gameObject.SetActive(false);
                    finishCoins.Add(new CoinProxy { source = source, art = art,
                        parts = Array.Empty<CoinPart>(), spark = spark, phaseSeed = i * 2.399963f });
                    BoundSignalSparkCount++;
                    continue;
                }
                var filters = art.GetComponentsInChildren<MeshFilter>(true);
                var parts = new List<CoinPart>(filters.Length);
                for (int p = 0; p < filters.Length; p++)
                {
                    var sourceRenderer = filters[p].GetComponent<MeshRenderer>();
                    if (filters[p].sharedMesh == null || sourceRenderer == null) continue;
                    var node = new GameObject("Native reward coin " + i + " " + p);
                    node.layer = 0;
                    node.transform.SetParent(finishCoinRoot, false);
                    node.AddComponent<MeshFilter>().sharedMesh = filters[p].sharedMesh;
                    var renderer = node.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                    renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                    renderer.receiveShadows = sourceRenderer.receiveShadows;
                    node.SetActive(false);
                    parts.Add(new CoinPart { source = filters[p].transform, sourceRenderer = sourceRenderer,
                        view = node.transform, renderer = renderer });
                }
                if (parts.Count == 0) { UnmappedFinishCoinCount++; continue; }
                finishCoins.Add(new CoinProxy { source = source, art = art, parts = parts.ToArray() });
            }
            TickFinishCoins();
        }

        private void TickFinishCoins()
        {
            VisibleFinishCoinCount = 0;
            VisibleSignalSparkCount = VisibleConsumedSignalSparkCount = 0;
            VisibleGroundedFinishCoinCount = 0;
            MinVisibleFinishCoinGroundClearance = float.PositiveInfinity;
            MaxVisibleFinishCoinGroundClearance = float.NegativeInfinity;
            if (game == null || game.Actor == null || Route == null || definition == null) return;
            float ahead = game.Actor.position.z + definition.VisibleAhead / Mathf.Max(.01f, definition.DistanceScale);
            float behind = game.Actor.position.z - definition.VisibleBehind / Mathf.Max(.01f, definition.DistanceScale);
            for (int i = 0; i < finishCoins.Count; i++)
            {
                var coin = finishCoins[i];
                bool visible = coin.source != null && coin.source.enabled && coin.source.gameObject.activeInHierarchy &&
                    coin.art != null && coin.art.gameObject.activeInHierarchy && coin.source.transform.position.z <= ahead && coin.source.transform.position.z >= behind;
                bool drewCoin = false;
                float coinMinY = float.PositiveInfinity;
                if (coin.spark != null)
                {
                    if (coin.spark.gameObject.activeSelf != visible) coin.spark.gameObject.SetActive(visible);
                    if (visible)
                    {
                        Vector3 logical = coin.source.transform.position;
                        var frame = EvaluatePresentationFrame(definition.StartDistance + logical.z * definition.DistanceScale);
                        float surface = GetPresentationSurfaceY(logical.z);
                        float clearance = GroundFinishPath != null ? IslandGroundFinishPath.SparkGroundClearance : .10f;
                        float centerY = surface + clearance - coin.spark.LocalVisualBounds.min.y;
                        coin.spark.transform.SetPositionAndRotation(new Vector3(frame.Position.x, centerY, frame.Position.z) +
                            frame.Right * (logical.x * definition.LateralScale), frame.Rotation);
                        coin.spark.Tick(Time.time, coin.phaseSeed);
                        coinMinY = coin.spark.WorldBottomY;
                        drewCoin = true;
                        VisibleSignalSparkCount++;
                    }
                    if (coin.spark.gameObject.activeInHierarchy &&
                        (coin.art == null || !coin.art.gameObject.activeInHierarchy)) VisibleConsumedSignalSparkCount++;
                }
                for (int p = 0; p < coin.parts.Length; p++)
                {
                    var part = coin.parts[p];
                    bool drawPart = visible && part.source != null && part.sourceRenderer != null &&
                        part.sourceRenderer.enabled && part.source.gameObject.activeInHierarchy;
                    if (part.view.gameObject.activeSelf != drawPart) part.view.gameObject.SetActive(drawPart);
                    if (!drawPart) continue;
                    Vector3 logical = part.source.position;
                    var frame = EvaluatePresentationFrame(definition.StartDistance + logical.z * definition.DistanceScale);
                    // Expand the coin's lane, retaining the art's local shape.
                    float laneX = logical.x + coin.source.transform.position.x * (definition.LateralScale - 1);
                    part.view.SetPositionAndRotation(frame.Position + frame.Right * laneX + Vector3.up * (logical.y + definition.TileElevation),
                        frame.Rotation * part.source.rotation);
                    part.view.localScale = part.source.lossyScale;
                    part.sourceRenderer.GetPropertyBlock(part.properties);
                    part.renderer.SetPropertyBlock(part.properties);
                    coinMinY = Mathf.Min(coinMinY, part.renderer.bounds.min.y);
                    drewCoin = true;
                }
                if (drewCoin)
                {
                    VisibleFinishCoinCount++;
                    if (GroundFinishPath != null && coin.source.transform.position.z >= GroundFinishPath.LandingChartZ)
                    {
                        VisibleGroundedFinishCoinCount++;
                        float clearance = coinMinY - GroundFinishPath.GroundHeight;
                        MinVisibleFinishCoinGroundClearance = Mathf.Min(MinVisibleFinishCoinGroundClearance, clearance);
                        MaxVisibleFinishCoinGroundClearance = Mathf.Max(MaxVisibleFinishCoinGroundClearance, clearance);
                    }
                }
            }
            if (VisibleGroundedFinishCoinCount == 0)
                MinVisibleFinishCoinGroundClearance = MaxVisibleFinishCoinGroundClearance = 0;
        }

        private void ClearFinishCoins()
        {
            if (finishCoinRoot != null) Release(finishCoinRoot.gameObject);
            finishCoinRoot = null;
            finishCoins.Clear();
            BoundSignalSparkCount = VisibleSignalSparkCount = VisibleConsumedSignalSparkCount = 0;
            VisibleFinishCoinCount = UnmappedFinishCoinCount = 0;
            VisibleGroundedFinishCoinCount = 0;
            MinVisibleFinishCoinGroundClearance = MaxVisibleFinishCoinGroundClearance = 0;
        }

        /// <summary>Shared pose/lifetime for native meshes, independent of the reference renderer.</summary>
        public bool TryGetVisualState(OriginalTile tile, out float offset, out float planarScale, out bool visible)
        {
            if (tile != null && proxyBySource.TryGetValue(tile, out var proxy) && proxy.view != null)
            {
                offset = proxy.offset + (proxy.parentSpan != null ? proxy.parentSpan.offset : 0);
                planarScale = proxy.scale * (proxy.parentSpan != null ? proxy.parentSpan.scale : 1);
                visible = proxy.view.gameObject.activeInHierarchy && planarScale > 0;
                return true;
            }
            offset = 0; planarScale = 1; visible = false;
            return false;
        }

        public bool TryGetTileVisualState(OriginalTile tile, out bool visible, out float surfaceOffset, out float planarScale)
            => TryGetVisualState(tile, out surfaceOffset, out planarScale, out visible);

        public bool TryGetTileVisualPivot(OriginalTile tile, out Vector3 worldPivot)
        {
            if (tile != null && proxyBySource.TryGetValue(tile, out var proxy) && proxy.view != null)
            {
                worldPivot = proxy.styledSpan != null ? proxy.styledSpan.WorldCenter : proxy.span ? proxy.spanOrigin : proxy.view.position;
                return true;
            }
            worldPivot = Vector3.zero;
            return false;
        }

        /// <summary>The complete fixed ceramic spine; ride progress never shortens its mesh.</summary>
        public bool TryGetHoldDrawRange(OriginalTile tile, out float begin, out float end)
        {
            if (tile != null && proxyBySource.TryGetValue(tile, out var proxy) && proxy.styledSpan != null)
            {
                begin = proxy.styledSpan.DrawnStart; end = proxy.styledSpan.End;
                return true;
            }
            begin = end = 0;
            return false;
        }

        public bool TryGetHoldSupportRange(OriginalTile tile, out float start, out float end, out float capRadius)
        {
            if (tile != null && proxyBySource.TryGetValue(tile, out var proxy) && proxy.styledSpan != null)
            {
                start = proxy.styledSpan.Start; end = proxy.styledSpan.End; capRadius = proxy.styledSpan.Radius;
                return true;
            }
            start = end = capRadius = 0;
            return false;
        }

        public bool TryGetHoldTracerGeometry(OriginalTile tile, out Vector3 lineStart, out Vector3 lineEnd,
            out Vector3 markerCenter, out float progressChartZ, out bool visible)
        {
            if (tile != null && proxyBySource.TryGetValue(tile, out var proxy) && proxy.radioTracer != null && proxy.radioRenderer != null)
            {
                var pose = proxy.radioRenderer.transform;
                lineStart = pose.TransformPoint(proxy.radioTracer.LocalLineStartCenter);
                lineEnd = pose.TransformPoint(proxy.radioTracer.LocalLineEndCenter);
                markerCenter = pose.TransformPoint(proxy.radioTracer.LocalGlyphCenter);
                progressChartZ = proxy.radioTracer.GlyphChartZ;
                visible = proxy.radioRenderer.enabled && proxy.radioRenderer.gameObject.activeInHierarchy;
                return true;
            }
            lineStart = lineEnd = markerCenter = Vector3.zero; progressChartZ = 0; visible = false;
            return false;
        }

        /// <summary>Actual retained mesh center vertices, without allocating a Mesh.vertices copy.</summary>
        public bool TryGetHoldGeometryCenters(OriginalTile tile, out Vector3 head, out Vector3 tail)
        {
            if (tile != null && proxyBySource.TryGetValue(tile, out var proxy) && proxy.styledSpan != null && proxy.view != null)
            {
                head = proxy.view.TransformPoint(proxy.styledSpan.LocalDrawnHeadCenter);
                tail = proxy.view.TransformPoint(proxy.styledSpan.LocalEndCenter);
                return true;
            }
            head = tail = Vector3.zero;
            return false;
        }

        private void HideRenderers(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var node = renderers[i].gameObject;
                if (!hiddenLayers.ContainsKey(node)) hiddenLayers.Add(node, node.layer);
                node.layer = HiddenLogicalLayer;
            }
        }

        public void HideAdditionalLogical(Transform root)
        {
            if (root == null || game == null || !IsActiveFor(game.Selected?.id)) return;
            HideRenderers(root);
            var lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (!hiddenLights.ContainsKey(lights[i])) hiddenLights.Add(lights[i], lights[i].enabled);
                lights[i].enabled = false;
            }
        }

        private static float MeasureRadius(OriginalTile tile)
        {
            var colliders = tile.GetComponentsInChildren<Collider>(true);
            float radius = IslandSupportSpacing.NativeJumpRadius;
            for (int i = 0; i < colliders.Length; i++)
                radius = Mathf.Max(radius, Mathf.Min(1.05f, Mathf.Max(colliders[i].bounds.extents.x, colliders[i].bounds.extents.z)));
            return radius;
        }

        private static float MeasureHalfWidth(OriginalTile tile)
        {
            var colliders = tile.GetComponentsInChildren<Collider>(true);
            float halfWidth = .62f;
            for (int i = 0; i < colliders.Length; i++) halfWidth = Mathf.Max(halfWidth, Mathf.Min(.82f, colliders[i].bounds.extents.x));
            return halfWidth;
        }

        public void BindCourier(Transform courier, Transform lighting)
        {
            if (game == null || !IsActiveFor(game.Selected?.id) || courier == null) return;
            EnsureView();
            if (courierBinding.child != courier)
            {
                ResetFinishDeparture();
                RestoreBinding(ref courierBinding, game.Actor);
                courierBinding = Capture(courier);
                courierRenderers = courier.GetComponentsInChildren<Renderer>(true);
                courierMotion = courier.GetComponent<CourierMotion>();
            }
            courier.SetParent(actorAnchor, false);
            ApplyLocal(courierBinding);
            if (lighting != null)
            {
                if (lightingBinding.child != lighting)
                {
                    RestoreBinding(ref lightingBinding, game.Actor);
                    lightingBinding = Capture(lighting);
                }
                lighting.SetParent(actorAnchor, false);
                ApplyLocal(lightingBinding);
            }
        }

        private static ChildBinding Capture(Transform child)
        {
            return new ChildBinding { child = child, parent = child.parent, localPosition = child.localPosition,
                localRotation = child.localRotation, localScale = child.localScale };
        }

        private static void ApplyLocal(ChildBinding binding)
        {
            if (binding.child == null) return;
            binding.child.localPosition = binding.localPosition;
            binding.child.localRotation = binding.localRotation;
            binding.child.localScale = binding.localScale;
        }

        private static void RestoreBinding(ref ChildBinding binding, Transform fallback)
        {
            if (binding.child != null)
            {
                binding.child.SetParent(fallback != null ? fallback : binding.parent, false);
                ApplyLocal(binding);
            }
            binding = default;
        }

        public void RestoreCourierParent(Transform actor)
        {
            ResetFinishDeparture();
            RestoreBinding(ref courierBinding, actor);
            RestoreBinding(ref lightingBinding, actor);
            courierMotion = null;
        }

        private void ResetFinishDeparture()
        {
            if (courierBinding.child != null)
            {
                if (finishDeparture.Started) courierBinding.child.localScale = departureCourierBaseScale;
                if (departureCourierHidden && departureCourierWasActive) courierBinding.child.gameObject.SetActive(true);
            }
            finishDeparture.Reset();
            departureStartFrame = -1;
            departureCourierHidden = departureCourierWasActive = false;
            departureCourierBaseScale = Vector3.one;
            departureOffset = Vector3.zero;
        }

        private void AdvanceFinishDeparture(bool nativeFinishReached, int collectedCoins, Vector3 start,
            Quaternion heading, float deltaTime, int frameNumber)
        {
            if (finishDeparture.TryBegin(nativeFinishReached, collectedCoins, start, heading))
            {
                departureStartFrame = frameNumber;
                departureCourierWasActive = courierBinding.child != null && courierBinding.child.gameObject.activeSelf;
                // Binding preserves the native restoration scale before the
                // game applies its island scale. Depart from the actual current
                // bound presentation, not that earlier native snapshot.
                departureCourierBaseScale = courierBinding.child != null ? courierBinding.child.localScale : Vector3.one;
                if (stageCamera != null)
                {
                    departureCameraPosition = stageCamera.transform.position;
                    departureCameraRotation = stageCamera.transform.rotation;
                }
                courierMotion?.PlayFinishDeparture();
            }
            // The trigger-frame delta elapsed before takeoff. Exclude that
            // entire frame, including any second UpdatePresentation call, then
            // retain normal hitch-aware advancement on subsequent frames.
            if (frameNumber != departureStartFrame) finishDeparture.Tick(deltaTime);
        }

        private void ApplyFinishDepartureCourier()
        {
            if (courierBinding.child == null) return;
            courierBinding.child.localScale = departureCourierBaseScale * finishDeparture.VisualScale;
            if (finishDeparture.Complete && !departureCourierHidden)
            {
                courierBinding.child.gameObject.SetActive(false);
                departureCourierHidden = true;
            }
        }

        public void UpdatePresentation(Vector3 position, bool immediate)
        {
            if (game == null || !IsActiveFor(game.Selected?.id) || viewRoot == null || Route == null) return;
            float distance = definition.StartDistance + position.z * definition.DistanceScale;
            if (finishGlade != null && GroundFinishPath != null)
            {
                bool showGlade = GroundFinishPath.ShouldPresentGlade(position.z, definition.VisibleAhead);
                if (finishGlade.gameObject.activeSelf != showGlade) finishGlade.gameObject.SetActive(showGlade);
            }
            var frame = EvaluatePresentationFrame(distance);
            float lateral = position.x * definition.LateralScale;
            float animationDt = 0;
            if (animationFrame != Time.frameCount)
            {
                animationFrame = Time.frameCount;
                if (game.Phase != ClonePhase.Paused) animationDt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            }
            if (livingNature != null) livingNature.UpdatePresentation(distance, animationDt);
            float ahead = position.z + definition.VisibleAhead / Mathf.Max(.01f, definition.DistanceScale);
            float behind = position.z - definition.VisibleBehind / Mathf.Max(.01f, definition.DistanceScale);
            float holdExitGrace = HoldExitGrace;
            visibleHoldChartRanges.Clear();
            visibleRoundSupports.Clear();
            VisibleExpiredHoldCount = 0;
            VisibleUnifiedHoldBodies = 0;
            VisibleRadioHoldTracers = 0;
            for (int i = 0; i < proxies.Count; i++)
            {
                var proxy = proxies[i];
                if (proxy.source == null || proxy.view == null) continue;
                TickSpring(proxy, animationDt);
                bool visible = proxy.start <= ahead && proxy.end >= behind || game.Support == proxy.source;
                bool completing = proxy.spring == SpringMode.Hit || proxy.spring == SpringMode.Release;
                if (completing && proxy.start <= ahead) visible = true;
                if (proxy.spring == SpringMode.Finished) visible = false;
                if (proxy.source.consumed && proxy.end < position.z - .5f && game.Support != proxy.source && !completing) visible = false;
                // The release spring is a brief tail, never another visible
                // course on a later geometric lap, even after a slow frame.
                if (proxy.span && proxy.source.consumed && proxy.end + holdExitGrace < position.z && game.Support != proxy.source) visible = false;
                if (proxy.view.gameObject.activeSelf != visible) proxy.view.gameObject.SetActive(visible);
                if (proxy.span && visible)
                {
                    if (proxy.styledSpan != null) VisibleUnifiedHoldBodies++;
                    if (proxy.radioTracer != null && proxy.radioRenderer != null && proxy.radioRenderer.enabled) VisibleRadioHoldTracers++;
                    visibleHoldChartRanges.Add(new Vector2(proxy.start, proxy.end));
                    if (proxy.end + holdExitGrace < position.z && game.Support != proxy.source) VisibleExpiredHoldCount++;
                }
                float surface = proxy.offset + (proxy.parentSpan != null ? proxy.parentSpan.offset : 0);
                proxy.view.localScale = new Vector3(proxy.baseScale.x * proxy.scale, proxy.baseScale.y, proxy.baseScale.z * proxy.scale);
                if (proxy.span)
                {
                    if (visible && proxy.styledSpan != null && proxy.source.consumed)
                    {
                        proxy.styledSpan.Advance(position.z);
                        if (proxy.radioTracer != null) proxy.radioTracer.Advance(position.z);
                    }
                    Vector3 scaleCorrection = proxy.styledSpan != null ?
                        (proxy.styledSpan.WorldCenter - proxy.spanOrigin) * (1 - proxy.scale) : Vector3.zero;
                    scaleCorrection.y = 0;
                    proxy.view.position = proxy.spanOrigin + Vector3.up * surface + scaleCorrection;
                    continue;
                }
                if (!visible) continue;
                var chart = proxy.source.transform.position;
                var pose = EvaluatePresentationFrame(definition.StartDistance + chart.z * definition.DistanceScale);
                float x = chart.x * definition.LateralScale;
                var worldPosition = pose.Position + pose.Right * x + Vector3.up * (chart.y + definition.TileElevation + surface);
                proxy.view.SetPositionAndRotation(worldPosition, pose.Rotation);
                if (proxy.round)
                {
                    proxy.drawnPosition = worldPosition; proxy.drawnRadius = proxy.baseScale.x * proxy.scale;
                    visibleRoundSupports.Add(proxy);
                }
            }
            ActorSurfaceOffset = game.Support != null && proxyBySource.TryGetValue(game.Support, out var supportProxy) ? supportProxy.offset : 0;
            ObserveRoundOverlap();
            Vector3 actorBase = frame.Position;
            VisualFinishHoverOffset = GroundFinishPath != null ? GroundFinishPath.CourierHoverOffset(position.z) : 0;
            if (game.IslandIsInFlight)
                actorBase.y = GetFlightSurfaceY(game.IslandJumpStart, game.IslandJumpDistance, position.z) - definition.TileElevation;
            else actorBase.y += VisualFinishHoverOffset;
            float jumpBoost = game.IslandIsInFlight ? IslandFlightElevation.Boost(game.IslandJumpDistance,
                game.IslandJumpStart.z, position.z, definition.JumpHeightScale) : 0;
            VisualJumpBoost = jumpBoost;
            actorAnchor.SetPositionAndRotation(actorBase + frame.Right * lateral + Vector3.up * (position.y + jumpBoost + definition.TileElevation + ActorSurfaceOffset), frame.Rotation);
            AdvanceFinishDeparture(GroundFinishPath != null && game.WinSequence, game.CollectedCoins,
                actorAnchor.position, frame.Rotation, animationDt > 0 ? Time.unscaledDeltaTime : 0, Time.frameCount);
            if (finishDeparture.Started)
            {
                departureOffset = finishDeparture.Position - actorAnchor.position;
                actorAnchor.SetPositionAndRotation(finishDeparture.Position, finishDeparture.Rotation);
                ApplyFinishDepartureCourier();
                // The hero moves away from this final camera pose rather than
                // dragging the camera with its presentation-only departure.
                if (stageCamera != null) stageCamera.transform.SetPositionAndRotation(departureCameraPosition, departureCameraRotation);
                cameraPoseRequested = false;
                Vector3 departureCenter = actorAnchor.position + Vector3.up;
                clearViewActor = new Vector4(departureCenter.x, departureCenter.y, departureCenter.z,
                    departureCourierHidden ? 0 : 1.8f);
                return;
            }
            if (stageCamera == null) return;
            var lookFrame = EvaluatePresentationFrame(distance + definition.CameraLookAhead);
            cameraRouteDistance = distance; cameraLateral = lateral * definition.CameraLateralFollow;
            desiredCameraPosition = frame.Position - frame.Tangent * definition.CameraBackDistance + Vector3.up * definition.CameraHeight;
            Vector3 target = lookFrame.Position + Vector3.up * 1.0f;
            desiredCameraRotation = Quaternion.LookRotation((target - desiredCameraPosition).normalized, Vector3.up);
            // A fixed swept volume, independent of the live courier pose or
            // which tile was consumed. Jumping, squash and Hold spins must not
            // reframe the entire world, even on a perfectly straight road.
            cameraEnvelopeCount = 0;
            for (int side = -1; side <= 1; side += 2)
                for (int front = -1; front <= 1; front += 2)
                    for (int top = 0; top <= 1; top++)
                        cameraEnvelope[cameraEnvelopeCount++] = frame.Position + frame.Right * (side * (3.4f + NativeRhythmRules.HorizontalLimit * (definition.LateralScale - 1))) +
                            frame.Tangent * (front * .75f) + Vector3.up * (definition.TileElevation + (top == 0 ? -.1f : 5.25f));
            float preview = Mathf.Max(18.4f, definition.CameraLookAhead);
            for (int sample = 0; sample <= 7; sample++)
            {
                var corridor = EvaluatePresentationFrame(distance + preview * sample / 7);
                for (int side = -1; side <= 1; side += 2)
                    cameraEnvelope[cameraEnvelopeCount++] = corridor.Position + corridor.Right * (side * (2.2f + NativeRhythmRules.HorizontalLimit * (definition.LateralScale - 1))) + Vector3.up * definition.TileElevation;
            }
            TileProxy next = null;
            for (int i = 0; i < proxies.Count; i++)
            {
                var candidate = proxies[i];
                if (candidate.source == null || candidate.source.consumed || candidate.start <= position.z + .05f ||
                    candidate.source.element.id > 4 || next != null && candidate.start >= next.start) continue;
                next = candidate;
            }
            clearViewLanding = Vector4.zero;
            if (next != null && next.start - position.z <= 15f)
            {
                Vector3 contact = next.source.ContactPosition;
                var landing = EvaluatePresentationFrame(definition.StartDistance + contact.z * definition.DistanceScale);
                Vector3 landingCenter = landing.Position + landing.Right * (contact.x * definition.LateralScale) + Vector3.up * (contact.y + definition.TileElevation);
                clearViewLanding = new Vector4(landingCenter.x, landingCenter.y, landingCenter.z, 1.05f);
            }
            cameraPoseRequested = true;
            if (immediate || !cameraReady) ApplyCamera(true);
        }

        private void ObserveRoundOverlap()
        {
            VisibleRoundOverlapCount = 0;
            MinVisibleLandingRadius = float.PositiveInfinity;
            for (int i = 0; i < visibleRoundSupports.Count; i++)
            {
                var a = visibleRoundSupports[i];
                if (!a.source.consumed) MinVisibleLandingRadius = Mathf.Min(MinVisibleLandingRadius, a.baseScale.x);
                for (int j = i + 1; j < visibleRoundSupports.Count; j++)
                {
                    var b = visibleRoundSupports[j];
                    float radius = a.drawnRadius + b.drawnRadius;
                    if (Vector3.Distance(a.drawnPosition, b.drawnPosition) < radius - .002f) VisibleRoundOverlapCount++;
                }
            }
            if (float.IsPositiveInfinity(MinVisibleLandingRadius)) MinVisibleLandingRadius = 0;
        }

        private void ApplyCamera(bool immediate)
        {
            if (stageCamera == null || !cameraPoseRequested) return;
            float blend = immediate || !cameraReady ? 1f : 1f - Mathf.Exp(-definition.CameraSmoothing * Time.unscaledDeltaTime);
            stageCamera.fieldOfView = definition.CameraFieldOfView;
            if (definition.UseCloseCamera)
            {
                var pose = closeCamera.Step(EvaluatePresentationFrame(cameraRouteDistance),
                    EvaluatePresentationFrame(cameraRouteDistance + definition.CameraHeadingLead), cameraLateral, definition.CameraBackDistance,
                    definition.CameraHeight, definition.CameraLookAhead, definition.CameraFocusHeight,
                    definition.CameraSmoothing,
                    game != null && game.Phase == ClonePhase.Paused ? 0 : Time.unscaledDeltaTime, immediate || !cameraReady);
                CameraRigAnchor = pose.Anchor;
                framedCameraTarget = smoothedCameraPosition = pose.Position;
                desiredCameraRotation = smoothedCameraRotation = pose.Rotation;
            }
            else
            {
                framedCameraTarget = IslandCameraFraming.ResolvePosition(desiredCameraPosition, desiredCameraRotation,
                stageCamera.fieldOfView, stageCamera.aspect, stageCamera.nearClipPlane, cameraEnvelope, cameraEnvelopeCount,
                new Rect(.10f, .12f, .80f, .72f));
            // The native railway parent moves during its finish animation.
            // Keep independent world-space state instead of feeding inherited
            // transform motion back into the island camera's damping.
            smoothedCameraPosition = immediate || !cameraReady ? framedCameraTarget :
                Vector3.Lerp(smoothedCameraPosition, framedCameraTarget, blend);
            smoothedCameraRotation = immediate || !cameraReady ? desiredCameraRotation :
                Quaternion.Slerp(smoothedCameraRotation, desiredCameraRotation, blend);
            }
            stageCamera.transform.SetPositionAndRotation(smoothedCameraPosition, smoothedCameraRotation);
            // Sample the actual animated silhouette after CourierMotion's
            // LateUpdate, including its antenna, scarf and airborne arms.
            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < courierRenderers.Length; i++)
            {
                var renderer = courierRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (hasBounds) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; hasBounds = true; }
            }
            Vector3 center = hasBounds ? bounds.center : actorAnchor.position + Vector3.up;
            clearViewActor = new Vector4(center.x, center.y, center.z, hasBounds ? Mathf.Max(1.25f, bounds.extents.magnitude + .15f) : 1.8f);
            cameraReady = true;
            cameraPoseRequested = false;
        }

        private void LateUpdate()
        {
            if (rooftopMotion != null)
                rooftopMotion.Tick(game != null && game.Phase == ClonePhase.Paused ? 0 : Mathf.Min(Time.unscaledDeltaTime, .1f));
            if (game != null && game.Actor != null && (game.Phase == ClonePhase.Dead || game.WinSequence))
                UpdatePresentation(game.Actor.position, false);
            ApplyCamera(false);
            UpdateCameraReveal();
            TickFinishCoins();
            if (particleProjection != null)
            {
                ApplyParticleBudget();
                particleProjection.Tick();
            }
        }

        private void UpdateCameraReveal()
        {
            if (stageCamera == null || actorAnchor == null) return;
            Vector3 camera = stageCamera.transform.position;
            Shader.SetGlobalVector(ClearViewCameraId, new Vector4(camera.x, camera.y, camera.z, 1));
            Shader.SetGlobalVector(ClearViewActorId, clearViewActor);
            Shader.SetGlobalVector(ClearViewLandingId, clearViewLanding);
            // The ground and cobbled road always remain opaque. Only scenery
            // above them can obscure the courier or the next support.
            Shader.SetGlobalFloat(ClearViewFloorId, .35f);
        }

        private void ApplyParticleBudget()
        {
            if (particleProjection == null || quality == null) return;
            int budget = quality.ParticleBudgetPerSystem;
            if (appliedParticleBudget == budget) return;
            particleProjection.SetParticleBudgetPerSystem(budget);
            appliedParticleBudget = budget;
        }

        private void TickSpring(TileProxy proxy, float dt)
        {
            // The native end corridor is logical support beneath real ground,
            // which must not inherit the spring of a floating landing disc.
            if (GroundFinishPath != null && proxy.source.element.id == 10)
            { proxy.offset = 0; proxy.scale = 1; return; }
            bool consumed = proxy.source.consumed;
            bool missedMarker = proxy.source.element.id == 5 && game.Actor != null &&
                Mathf.Abs(game.Actor.position.z - proxy.source.initialZ) >= .8f;
            bool hit = consumed && !proxy.wasConsumed && !missedMarker;
            bool supported = game.Support == proxy.source;
            if (supported && !proxy.wasSupported)
            { proxy.spring = SpringMode.Held; proxy.elapsed = 0; }
            else if (proxy.wasSupported && !supported)
            { proxy.spring = SpringMode.Release; proxy.elapsed = 0; proxy.releaseFrom = proxy.offset; }
            else if (hit && !supported)
            { proxy.spring = SpringMode.Hit; proxy.elapsed = 0; }
            proxy.wasConsumed = consumed; proxy.wasSupported = supported;
            proxy.elapsed += dt;
            float t = proxy.elapsed;
            if (proxy.spring == SpringMode.Held)
            { proxy.offset = -.08f * Mathf.SmoothStep(0, 1, t / .07f); proxy.scale = 1; }
            else if (proxy.spring == SpringMode.Hit)
            {
                float dip = proxy.source.checkpoint || proxy.source.element.id == 5 || proxy.source.element.id == 9 ? .09f : .15f;
                proxy.offset = t < .07f ? Mathf.Lerp(0, -dip, Mathf.SmoothStep(0, 1, t / .07f)) :
                    t < .16f ? Mathf.Lerp(-dip, .04f, Mathf.SmoothStep(0, 1, (t - .07f) / .09f)) :
                    Mathf.Lerp(.04f, 0, Mathf.SmoothStep(0, 1, (t - .16f) / .10f));
                proxy.scale = 1 - Mathf.SmoothStep(0, 1, (t - .28f) / .24f);
                if (t >= .52f) { proxy.spring = SpringMode.Finished; proxy.scale = 0; }
            }
            else if (proxy.spring == SpringMode.Release)
            {
                proxy.offset = t < .09f ? Mathf.Lerp(proxy.releaseFrom, .04f, Mathf.SmoothStep(0, 1, t / .09f)) :
                    Mathf.Lerp(.04f, 0, Mathf.SmoothStep(0, 1, (t - .09f) / .13f));
                proxy.scale = 1 - Mathf.SmoothStep(0, 1, (t - .24f) / .24f);
                if (t >= .48f) { proxy.spring = SpringMode.Finished; proxy.scale = 0; }
            }
        }

        private Mesh CreateSpanMesh(OriginalTile source, float halfWidth)
        {
            float start = source.element.pos.z;
            float end = Mathf.Max(start + .1f, source.segment.endZ);
            int steps = Mathf.Max(1, Mathf.CeilToInt((end - start) * definition.DistanceScale / .45f));
            int capStart = (steps + 1) * 8, capEnd = capStart + 1;
            var vertices = new Vector3[capEnd + 1];
            var uv = new Vector2[vertices.Length];
            var surface = new List<int>(steps * 12);
            var trim = new List<int>(steps * 24);
            var body = new List<int>(steps * 24);
            float x = source.element.pos.x * definition.LateralScale;
            float thickness = Mathf.Max(.05f, definition.TileThickness);
            float bevel = Mathf.Min(.075f, thickness * .24f);
            for (int i = 0; i <= steps; i++)
            {
                float z = Mathf.Lerp(start, end, (float)i / steps);
                var frame = Route.Evaluate(definition.StartDistance + z * definition.DistanceScale);
                Vector3 center = frame.Position + frame.Right * x + Vector3.up * (source.element.pos.y + definition.TileElevation);
                for (int j = 0; j < 8; j++)
                {
                    float width = j == 0 || j == 5 ? -halfWidth * .90f : j == 1 || j == 4 ? halfWidth * .90f :
                        j == 2 || j == 3 ? halfWidth : -halfWidth;
                    float height = j <= 1 ? 0 : j == 2 || j == 7 ? -bevel : j == 3 || j == 6 ? -thickness + bevel : -thickness;
                    vertices[i * 8 + j] = center + frame.Right * width + Vector3.up * height;
                    uv[i * 8 + j] = new Vector2((float)i / steps, (float)j / 7);
                }
            }
            for (int i = 0; i < steps; i++)
            {
                int a = i * 8, b = (i + 1) * 8;
                for (int j = 0; j < 8; j++)
                    AddQuad(j == 0 ? surface : j == 1 || j == 7 ? trim : body,
                        a + j, b + j, b + (j + 1) % 8, a + (j + 1) % 8);
            }
            vertices[capStart] = (vertices[0] + vertices[4]) * .5f;
            vertices[capEnd] = (vertices[steps * 8] + vertices[steps * 8 + 4]) * .5f;
            for (int j = 0; j < 8; j++)
            {
                body.Add(capStart); body.Add(j); body.Add((j + 1) % 8);
                body.Add(capEnd); body.Add(steps * 8 + (j + 1) % 8); body.Add(steps * 8 + j);
            }
            var mesh = new Mesh { name = "Curved hold visual " + source.element.sourceIndex };
            if (vertices.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices; mesh.uv = uv; mesh.subMeshCount = 3;
            mesh.SetTriangles(body, 0); mesh.SetTriangles(surface, 1); mesh.SetTriangles(trim, 2);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(List<int> target, int a, int b, int c, int d)
        {
            target.Add(a); target.Add(b); target.Add(c); target.Add(a); target.Add(c); target.Add(d);
        }

        private static Mesh CreateDiscMesh(float requestedThickness)
        {
            const int sides = 40, rings = 6;
            float thickness = Mathf.Max(.05f, requestedThickness);
            float bevel = Mathf.Min(.075f, thickness * .24f);
            var vertices = new Vector3[sides * rings + 2];
            var uv = new Vector2[vertices.Length];
            float[] radii = { .88f, .965f, 1f, 1f, .965f, .88f };
            float[] heights = { -thickness, -thickness + bevel * .33f, -thickness + bevel, -bevel, -bevel * .33f, 0f };
            var body = new List<int>(sides * 18);
            var top = new List<int>(sides * 3);
            var trim = new List<int>(sides * 6);
            for (int ring = 0; ring < rings; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    int index = ring * sides + i;
                    vertices[index] = new Vector3(Mathf.Cos(a) * radii[ring], heights[ring], Mathf.Sin(a) * radii[ring]);
                    uv[index] = new Vector2(vertices[index].x * .5f + .5f, vertices[index].z * .5f + .5f);
                }
            int bottom = sides * rings, center = bottom + 1;
            vertices[bottom] = Vector3.down * thickness;
            vertices[center] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                for (int ring = 0; ring < rings - 1; ring++)
                    AddQuad(ring >= rings - 3 ? trim : body, ring * sides + i, (ring + 1) * sides + i, (ring + 1) * sides + next, ring * sides + next);
                body.Add(bottom); body.Add(i); body.Add(next);
                top.Add(center); top.Add((rings - 1) * sides + next); top.Add((rings - 1) * sides + i);
            }
            var mesh = new Mesh { name = "Island beveled circular support" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.subMeshCount = 3;
            mesh.SetTriangles(body, 0); mesh.SetTriangles(top, 1); mesh.SetTriangles(trim, 2);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateMaterials()
        {
            stone = CreateMaterial("Island tile ceramic", new Color(.35f, .48f, .47f), .15f);
            brass = CreateMaterial("Island tile brass rim", new Color(.81f, .63f, .30f), .55f);
            blue = CreateMaterial("Island jump glaze", new Color(.48f, .80f, .79f), .08f);
            coral = CreateMaterial("Island glide glaze", new Color(.92f, .58f, .42f), .08f);
            glow = CreateMaterial("Island checkpoint pearl", new Color(.96f, .88f, .65f), .18f);
            jumpMaterials = new[] { stone, blue, brass };
            followMaterials = new[] { stone, coral, brass };
            pointMaterials = new[] { brass, glow, brass };
            if (definition.UnifiedHoldTiles)
            {
                if (definition.HoldTracerShader == null) throw new InvalidOperationException("Styled Hold radio guides require the transparent tracer shader.");
                radioWhite = new Material(definition.HoldTracerShader)
                { name = "Island radio Hold white wire", hideFlags = HideFlags.DontSave, renderQueue = 3001 };
                radioWhite.SetColor("_Color", Color.white);
                radioWhite.SetFloat("Alpha", definition.HoldLineOpacity);
                radioMaterials = new[] { radioWhite, brass, blue };
            }
        }

        private Material CreateMaterial(string name, Color color, float metallic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Island tile supports require a lit shader.");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .48f);
            if (material.HasProperty("_EmissionColor"))
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, Color.black);
            }
            return material;
        }

        private void ClearProxies()
        {
            ResetFinishDeparture();
            ClearFinishCoins();
            if (particleProjection != null) particleProjection.Clear();
            if (finishGlade != null) Release(finishGlade.gameObject);
            finishGlade = null; GroundFinishPath = null;
            for (int i = 0; i < proxies.Count; i++) if (proxies[i].view != null) Release(proxies[i].view.gameObject);
            proxies.Clear();
            roundSupports.Clear(); MinimumRoundSupportRadius = VisibleRoundOverlapCount = 0;
            visibleRoundSupports.Clear(); MinVisibleLandingRadius = 0;
            proxyBySource.Clear();
            visibleHoldChartRanges.Clear();
            VisibleExpiredHoldCount = 0;
            VisibleRadioHoldTracers = 0;
            ActorSurfaceOffset = 0;
            animationFrame = -1;
            for (int i = 0; i < spanMeshes.Count; i++) Release(spanMeshes[i]);
            spanMeshes.Clear();
        }

        private void RestoreHiddenObjects()
        {
            foreach (var item in hiddenLayers) if (item.Key != null) item.Key.layer = item.Value;
            hiddenLayers.Clear();
            foreach (var item in hiddenLights) if (item.Key != null) item.Key.enabled = item.Value;
            hiddenLights.Clear();
        }

        private void RestoreCamera()
        {
            if (cameraState.camera != null)
            {
                var camera = cameraState.camera;
                camera.cullingMask = cameraState.cullingMask;
                camera.fieldOfView = cameraState.fieldOfView;
                camera.nearClipPlane = cameraState.nearClip;
                camera.farClipPlane = cameraState.farClip;
                camera.clearFlags = cameraState.clearFlags;
                camera.backgroundColor = cameraState.backgroundColor;
                camera.transform.SetPositionAndRotation(cameraState.position, cameraState.rotation);
            }
            cameraState = default;
            stageCamera = null;
            cameraReady = false;
            cameraPoseRequested = false;
        }

        public void Deactivate()
        {
            Shader.SetGlobalVector(ClearViewCameraId, Vector4.zero);
            RestoreCourierParent(game != null ? game.Actor : null);
            RestoreHiddenObjects();
            RestoreCamera();
            ClearProxies();
            if (particleProjection != null) particleProjection.Dispose();
            particleProjection = null;
            livingNature = null;
            rooftopMotion?.Dispose();
            rooftopMotion = null;
            closeCamera.Reset();
            appliedParticleBudget = -1;
            if (viewRoot != null) Release(viewRoot.gameObject);
            viewRoot = proxyRoot = actorAnchor = islandModel = null;
            courierRenderers = Array.Empty<Renderer>();
            Release(discMesh); discMesh = null;
            Release(stone); Release(brass); Release(blue); Release(coral); Release(glow);
            Release(radioWhite); radioWhite = null; radioMaterials = null;
            stone = brass = blue = coral = glow = null;
            jumpMaterials = followMaterials = pointMaterials = null;
        }

        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (value is GameObject node) node.SetActive(false);
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDisable() => Deactivate();
        private void OnDestroy() => Deactivate();
    }
}
