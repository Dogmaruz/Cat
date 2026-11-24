using System;
using System.Collections.Generic;
using DancingCats.Clone;
using UnityEngine;
using UnityEngine.Rendering;

namespace RadioArchipelago.World
{
    /// <summary>
    /// Scene-owned decorative wildlife and clouds. The stage supplies its route
    /// distance and presentation delta; no gameplay transform or clock is read or
    /// changed. Meshes, materials and a small fixed pool are created only at setup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IslandLivingNature : MonoBehaviour
    {
        [SerializeField, Range(0, 24)] private int faunaCount = 16;
        [SerializeField, Range(0, 12)] private int skyCloudCount = 8;
        [SerializeField, Min(8)] private float faunaSpacing = 16;
        [SerializeField, Min(64)] private float skyCloudSpacing = 64;
        [SerializeField] private Vector2 cloudBankRange = new Vector2(432, 460);

        private enum Species { Butterfly, Dragonfly, Bird, Fairy }
        private sealed class Fauna
        {
            internal Transform root, leftWing, rightWing;
            internal Species species;
            internal int cell = int.MinValue;
            internal float anchor, phase, speed, lateral, height;
            internal IslandRoute.Frame frame;
        }
        private sealed class Cloud
        {
            internal Transform root;
            internal int cell = int.MinValue;
            internal int shapeIndex, sizeClass;
            internal float anchor, phase, lateral, height, horizontalRadius, bottomOffset, forwardReach;
            internal Vector3 scale;
            internal Quaternion rotationOffset;
            internal IslandRoute.Frame frame;
        }
        private sealed class Bank
        {
            internal Transform root;
            internal int shapeIndex, sizeClass;
            internal float distance, phase;
            internal Vector3 center, right;
            // Used only by the setup pass. The animated pool remains fixed.
            internal Vector3 scale;
            internal Quaternion rotation;
            internal Vector3[] vertices;
            internal float horizontalRadius;
        }

        private readonly struct CloudLanding
        {
            internal readonly float distance, lateral;
            internal CloudLanding(float distance, float lateral) { this.distance = distance; this.lateral = lateral; }
        }

        private readonly List<Mesh> meshes = new List<Mesh>(8);
        private readonly List<Material> materials = new List<Material>(2);
        private IslandStageDefinition definition;
        private IslandRoute route;
        private Transform poolRoot;
        private Fauna[] fauna = Array.Empty<Fauna>();
        private Cloud[] clouds = Array.Empty<Cloud>();
        private Bank[] bank = Array.Empty<Bank>();
        private Vector3[] cloudGroundSamples = Array.Empty<Vector3>();
        private float elapsed;
        private float skyFloor;
        private bool configured;

        public int PooledFaunaCount => fauna.Length;
        public int PooledSkyCloudCount => clouds.Length;
        public int CloudBankCount => bank.Length;
        public int ActiveFaunaCount { get; private set; }
        public int ActiveSkyCloudCount { get; private set; }
        public int ActiveCloudBankCount { get; private set; }
        public bool IsConfigured => configured && poolRoot != null && route != null;

        /// <summary>Idempotent for the same definition/route. All owned resources are disposable.</summary>
        public void Configure(IslandStageDefinition stage, IslandRoute islandRoute)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (islandRoute == null) throw new ArgumentNullException(nameof(islandRoute));
            if (definition == stage && route == islandRoute && IsConfigured) return;
            Dispose();
            definition = stage;
            route = islandRoute;
            // A sky cloud may overlap another part of the winding route in XZ.
            // Its whole bottom therefore clears the highest route, not only
            // the station underneath its own centre.
            skyFloor = float.MinValue;
            int elevationSamples = Mathf.Max(1, Mathf.CeilToInt(route.Length / .5f));
            for (int i = 0; i <= elevationSamples; i++)
                skyFloor = Mathf.Max(skyFloor, route.Evaluate(route.Length * i / elevationSamples).Position.y);
            poolRoot = new GameObject("Living island nature (visual only)").transform;
            poolRoot.SetParent(transform, false);
            poolRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Material wildlife = CreateMaterial("Island wildlife shared", Color.white);
            Material cloud = CreateMaterial("Island cloud puffs shared", new Color(.94f, .97f, 1));
            cloud.SetFloat("_Cull", 2);
            var bodies = new Mesh[4];
            var wings = new Mesh[3];
            for (int i = 0; i < bodies.Length; i++) bodies[i] = CreateBody((Species)i);
            for (int i = 0; i < wings.Length; i++) wings[i] = CreateWings((Species)i);
            var cloudShapes = new Mesh[5];
            for (int i = 0; i < cloudShapes.Length; i++) cloudShapes[i] = CreateSparseCloudMesh(i);
            fauna = new Fauna[Mathf.Clamp(faunaCount, 0, 24)];
            for (int i = 0; i < fauna.Length; i++)
            {
                Species species = SpeciesFor(i);
                var item = new Fauna { species = species, phase = Noise(i + 41) * Mathf.PI * 2 };
                item.speed = species == Species.Bird ? .65f : species == Species.Dragonfly ? 1.8f : 1.05f;
                item.root = NewRoot(species + " pool " + i);
                AddMesh(item.root, "Body", bodies[(int)species], wildlife);
                if (species != Species.Fairy)
                {
                    item.leftWing = AddMesh(item.root, "Left wings", wings[(int)species], wildlife);
                    item.rightWing = AddMesh(item.root, "Right wings", wings[(int)species], wildlife);
                    item.leftWing.localScale = new Vector3(-1, 1, 1);
                }
                float size = species == Species.Bird ? .95f : species == Species.Fairy ? .45f : .72f;
                item.root.localScale = Vector3.one * size * Mathf.Lerp(.85f, 1.15f, Noise(i + 113));
                fauna[i] = item;
            }
            clouds = new Cloud[Mathf.Clamp(skyCloudCount, 0, 12)];
            for (int i = 0; i < clouds.Length; i++)
            {
                var item = new Cloud { root = NewRoot("Drifting sky cloud " + i), phase = Noise(i + 31) * Mathf.PI * 2,
                    shapeIndex = i % cloudShapes.Length, sizeClass = CloudSizeClass(i) };
                AddMesh(item.root, "Soft puffs", cloudShapes[item.shapeIndex], cloud);
                item.scale = Vector3.one * (SkyCloudScale * CloudSizeClasses[item.sizeClass]);
                item.rotationOffset = Quaternion.Euler(0, Noise(i + 193) * 360, 0);
                Bounds bounds = cloudShapes[item.shapeIndex].bounds;
                Vector3 center = item.rotationOffset * Vector3.Scale(bounds.center, item.scale);
                Vector3 extents = Vector3.Scale(bounds.extents, item.scale);
                Vector3 right = item.rotationOffset * Vector3.right;
                Vector3 forward = item.rotationOffset * Vector3.forward;
                float lateralExtent = Mathf.Abs(right.x) * extents.x + Mathf.Abs(forward.x) * extents.z;
                float forwardExtent = Mathf.Abs(right.z) * extents.x + Mathf.Abs(forward.z) * extents.z;
                item.horizontalRadius = Mathf.Max(Mathf.Abs(center.x) + lateralExtent, Mathf.Abs(center.z) + forwardExtent);
                item.forwardReach = Mathf.Abs(center.z) + forwardExtent;
                item.bottomOffset = bounds.min.y * item.scale.y;
                item.root.localScale = item.scale;
                clouds[i] = item;
            }
            if (definition.SparseCloudSteps) CreateSparseCloudSteps(cloud, cloudShapes);
            else CreateCloudBank(cloudShapes, cloud);
            configured = true;
        }

        /// <summary>
        /// Tick once with the stage's already-computed unwrapped world route
        /// distance. A zero delta freezes wings and drift. No per-frame creation,
        /// allocations, camera search, mesh modification or physics is used.
        /// </summary>
        public void UpdatePresentation(float routeDistance, float deltaTime)
        {
            if (!IsConfigured || !isActiveAndEnabled) return;
            elapsed += Mathf.Clamp(deltaTime, 0, .1f);
            ActiveFaunaCount = ActiveSkyCloudCount = ActiveCloudBankCount = 0;
            int firstFaunaCell = Mathf.FloorToInt(routeDistance / Mathf.Max(8, faunaSpacing)) - 2;
            for (int i = 0; i < fauna.Length; i++)
            {
                Fauna item = fauna[i];
                int cell = firstFaunaCell + Mod(i - firstFaunaCell, fauna.Length);
                if (cell != item.cell)
                {
                    item.cell = cell;
                    item.anchor = (cell + .4f + Noise(cell + 67) * .2f) * Mathf.Max(8, faunaSpacing);
                    item.frame = route.Evaluate(item.anchor);
                    float side = (cell & 1) == 0 ? -1 : 1;
                    bool bird = item.species == Species.Bird;
                    item.lateral = side * (bird ? Mathf.Lerp(8.5f, 12, Noise(cell + 7)) : Mathf.Lerp(3.7f, 5.2f, Noise(cell + 7)));
                    item.height = definition.TileElevation + (bird ? Mathf.Lerp(5, 8, Noise(cell + 11)) :
                        item.species == Species.Dragonfly ? 1.45f : item.species == Species.Fairy ? 2.1f : .95f);
                }
                float ahead = item.anchor - routeDistance;
                bool active = ahead >= -5 && ahead <= Mathf.Min(42, definition.VisibleAhead + 5);
                SetActive(item.root, active);
                if (!active) continue;
                ActiveFaunaCount++;
                AnimateFauna(item);
            }
            float cloudSpacing = Mathf.Max(SkyCloudSpacing, skyCloudSpacing);
            int firstCloudCell = Mathf.FloorToInt(routeDistance / cloudSpacing) - 2;
            for (int i = 0; i < clouds.Length; i++)
            {
                Cloud item = clouds[i];
                int cell = firstCloudCell + Mod(i - firstCloudCell, clouds.Length);
                if (cell != item.cell)
                {
                    item.cell = cell;
                    item.anchor = (cell + .5f) * cloudSpacing;
                    item.frame = route.Evaluate(item.anchor);
                    item.lateral = ((cell & 1) == 0 ? -1 : 1) *
                        (item.horizontalRadius + Mathf.Lerp(14, 24, Noise(cell + 103)) + 2.6f);
                    item.height = skyFloor + definition.TileElevation +
                        Mathf.Lerp(14, 16, Noise(cell + 107)) - item.bottomOffset + .3f;
                }
                float ahead = item.anchor - routeDistance;
                bool active = ahead + item.forwardReach >= -60 && ahead - item.forwardReach <= 180;
                SetActive(item.root, active);
                if (!active) continue;
                ActiveSkyCloudCount++;
                float drift = elapsed * .045f + item.phase;
                Vector3 center = item.frame.Position + item.frame.Right * (item.lateral + Mathf.Sin(drift) * 2.6f)
                    + item.frame.Tangent * (Mathf.Cos(drift * .7f) * 1.9f)
                    + Vector3.up * (item.height - item.frame.Position.y + Mathf.Sin(drift * .6f) * .3f);
                item.root.SetPositionAndRotation(center, item.frame.Rotation * item.rotationOffset);
            }
            for (int i = 0; i < bank.Length; i++)
            {
                Bank item = bank[i];
                float ahead = LoopDelta(item.distance, routeDistance, route.Length);
                bool active = ahead >= -definition.VisibleBehind - 9 && ahead <= definition.VisibleAhead + 12;
                SetActive(item.root, active);
                if (!active) continue;
                ActiveCloudBankCount++;
                float drift = elapsed * .15f + item.phase;
                item.root.position = item.center + item.right * (Mathf.Sin(drift) * .12f)
                    + Vector3.up * (Mathf.Cos(drift * .7f) * .035f);
            }
        }

        private void AnimateFauna(Fauna item)
        {
            bool bird = item.species == Species.Bird;
            float phase = elapsed * item.speed + item.phase;
            float orbit = bird ? 2.7f : item.species == Species.Fairy ? .35f : .7f;
            Vector3 center = item.frame.Position + item.frame.Right * (item.lateral + Mathf.Sin(phase) * orbit)
                + item.frame.Tangent * (Mathf.Cos(phase * .8f) * (bird ? 3.4f : .9f))
                + Vector3.up * (item.height + Mathf.Sin(phase * 1.4f) * (bird ? .6f : .22f));
            if (item.species == Species.Dragonfly) center.y += Mathf.Sin(phase * 17) * .025f;
            Vector3 heading = bird ? item.frame.Right * Mathf.Cos(phase) - item.frame.Tangent * Mathf.Sin(phase * .8f) :
                item.frame.Tangent + item.frame.Right * (Mathf.Cos(phase) * .45f);
            if (heading.sqrMagnitude < .01f) heading = item.frame.Tangent;
            item.root.SetPositionAndRotation(center, Quaternion.LookRotation(heading, Vector3.up));
            if (item.leftWing == null) return;
            float frequency = item.species == Species.Dragonfly ? 51 : bird ? 9 : 22;
            float amplitude = item.species == Species.Dragonfly ? 13 : bird ? 24 : 55;
            float wing = Mathf.Sin(elapsed * frequency + item.phase) * amplitude;
            item.leftWing.localRotation = Quaternion.AngleAxis(-wing, Vector3.forward);
            item.rightWing.localRotation = Quaternion.AngleAxis(wing, Vector3.forward);
        }

        private void CreateCloudBank(Mesh[] shapes, Material material)
        {
            // Existing stages receive the same large, separated volumes without
            // rewriting their serialized definition or any musical chart.
            CreateSparseCloudSteps(material, shapes);
        }

        private const float CloudDrift = .12f;
        private const float CloudAirGap = 1.5f;
        private const float CloudBob = .035f;
        private const float SkyCloudScale = 20;
        private const float SkyCloudSpacing = 64;
        private const float CloudBankScale = 5;
        private const float CloudBankSpacing = 14;
        private static readonly float[] CloudSizeClasses = { .72f, .88f, 1.04f, 1.22f, 1.44f };
        private static int CloudSizeClass(int ordinal) => (ordinal * 2 + 1 + ordinal / 5) % 5;
        private static readonly int[] BankShapeOrder = { 1, 0, 3, 2, 4 };
        private static readonly int[] BankSizeOrder = { 3, 1, 0, 2, 4 };

        private void CreateSparseCloudSteps(Material material, Mesh[] shapes)
        {
            List<Vector2> ranges = SparseCloudRanges();
            if (ranges.Count == 0) { bank = Array.Empty<Bank>(); return; }
            cloudGroundSamples = CloudGroundSamples(ranges);
            var vertices = new Vector3[shapes.Length][];
            for (int i = 0; i < shapes.Length; i++)
                vertices[i] = shapes[i].vertices;
            List<CloudLanding> landings = CloudLandings();
            var pool = new List<Bank>(16);
            for (int r = 0; r < ranges.Count && pool.Count < 32; r++)
            {
                Vector2 range = ranges[r];
                float intervals = (range.y - range.x) / (CloudBankSpacing * CloudBankMultiplier);
                int count = Mathf.Clamp(CloudBankMultiplier > 1 ? Mathf.RoundToInt(intervals) : Mathf.CeilToInt(intervals), 1, 32 - pool.Count);
                int rangeStart = pool.Count;
                for (int slot = 0; slot < count; slot++)
                {
                    float cellStart = Mathf.Lerp(range.x, range.y, (float)slot / count);
                    float cellEnd = Mathf.Lerp(range.x, range.y, (float)(slot + 1) / count);
                    Vector2 cell = new Vector2(cellStart + (slot > 0 ? CloudAirGap * .5f : 0),
                        cellEnd - (slot + 1 < count ? CloudAirGap * .5f : 0));
                    float target = (cellStart + cellEnd) * .5f;
                    int index = pool.Count, shape = BankShapeOrder[index % BankShapeOrder.Length];
                    Bank item = NewCloudBank(index, vertices);
                    float size = item.scale.x;
                    const float yaw = 0;
                    // Prefer a real stationary landing in this cell. Moving and
                    // held supports retain their gameplay motion and native art.
                    bool placed = false;
                    float best = float.MaxValue;
                    int chosen = -1;
                    for (int l = 0; l < landings.Count; l++)
                    {
                        CloudLanding landing = landings[l];
                        if (landing.distance < cellStart || landing.distance > cellEnd) continue;
                        float difference = Mathf.Abs(landing.distance - target);
                        if (difference >= best || !PlaceSparseCloud(item, landing.distance, landing.lateral, yaw, cell, pool)) continue;
                        best = difference; chosen = l;
                    }
                    if (chosen >= 0)
                    {
                        CloudLanding landing = landings[chosen];
                        placed = PlaceSparseCloud(item, landing.distance, landing.lateral, yaw, cell, pool);
                    }
                    // Fit the entire large volume inside its window. Recentring
                    // its asymmetric footprint preserves size and air gaps.
                    if (!placed)
                    {
                        float side = Mathf.Lerp(-.45f, .45f, Noise(index + 269));
                        Bounds bounds = shapes[shape].bounds;
                        float halfDepth = bounds.extents.z * size + CloudDrift;
                        float footprintCenter = Mathf.Clamp(target, cell.x + halfDepth, cell.y - halfDepth);
                        float station = footprintCenter - bounds.center.z * size;
                        placed = PlaceSparseCloud(item, station, side, yaw, cell, pool);
                    }
                    if (!placed)
                    {
                        // A route's smoothed heading can differ from its actual
                        // displacement. Wide forms may need more spacing along
                        // the centre-to-centre axis than a cell's local Z size.
                        // Reflow this window before creating any scene objects.
                        pool.RemoveRange(rangeStart, pool.Count - rangeStart);
                        if (!PlaceCloudRange(range, count, rangeStart, shapes, vertices, pool))
                            throw new InvalidOperationException("The large clouds do not fit authored window " + range +
                                " with their complete footprints and air gaps; cloud size is never silently reduced.");
                        break;
                    }
                    pool.Add(item);
                }
            }
            bank = pool.ToArray();
            for (int i = 0; i < bank.Length; i++)
            {
                Bank item = bank[i];
                item.root = NewRoot("High sky cloud bank " + i);
                item.root.localScale = item.scale;
                AddMesh(item.root, "Detached cloud stepping scenery", shapes[item.shapeIndex], material);
                item.root.SetPositionAndRotation(item.center, item.rotation);
            }
        }

        private float CloudBankMultiplier => Finite(definition.CloudBankScaleMultiplier)
            ? Mathf.Clamp(definition.CloudBankScaleMultiplier, 1, 2.5f) : 1;

        private Bank NewCloudBank(int index, Vector3[][] vertices)
        {
            int shape = BankShapeOrder[index % BankShapeOrder.Length];
            int sizeClass = BankSizeOrder[index % BankSizeOrder.Length];
            float size = CloudBankScale * CloudSizeClasses[sizeClass] * CloudBankMultiplier;
            float radiusSquared = 0;
            for (int i = 0; i < vertices[shape].Length; i++)
            {
                Vector3 vertex = vertices[shape][i];
                radiusSquared = Mathf.Max(radiusSquared, vertex.x * vertex.x + vertex.z * vertex.z);
            }
            return new Bank { phase = index * .93f, scale = Vector3.one * size,
                horizontalRadius = Mathf.Sqrt(radiusSquared) * size,
                vertices = vertices[shape], shapeIndex = shape, sizeClass = sizeClass };
        }

        private bool PlaceCloudRange(Vector2 range, int count, int rangeStart, Mesh[] shapes, Vector3[][] vertices, List<Bank> pool)
        {
            // Bounded, setup-only forward packing. The exact same vertex gate
            // validates every candidate, including all previously placed ranges.
            const int searchSteps = 64;
            for (int slot = 0; slot < count; slot++)
            {
                int index = rangeStart + slot;
                Bank item = NewCloudBank(index, vertices);
                Bounds bounds = shapes[item.shapeIndex].bounds;
                float low = range.x + CloudDrift + .001f - bounds.min.z * item.scale.z;
                float high = range.y - CloudDrift - .001f - bounds.max.z * item.scale.z;
                if (low > high) return false;
                bool placed = false;
                float side = Mathf.Lerp(-.45f, .45f, Noise(index + 269));
                for (int sample = 0; sample <= searchSteps; sample++)
                {
                    float fraction = (float)sample / searchSteps;
                    if (count == 1) fraction = .5f + ((sample & 1) == 0 ? -1 : 1) * ((sample + 1) / 2) / (float)searchSteps;
                    float distance = Mathf.Lerp(low, high, fraction);
                    if (!PlaceSparseCloud(item, distance, side, 0, range, pool)) continue;
                    placed = true;
                    break;
                }
                if (!placed) return false;
                pool.Add(item);
            }
            return true;
        }

        private Vector3[] CloudGroundSamples(List<Vector2> ranges)
        {
            // The route between separate air windows visits solid islands.
            // Tangent projection alone cannot protect these visits on a bend.
            var result = new List<Vector3>();
            for (int r = 1; r < ranges.Count; r++)
            {
                float low = ranges[r - 1].y + .25f, high = ranges[r].x - .25f;
                if (low > high) continue;
                int samples = Mathf.Max(1, Mathf.CeilToInt((high - low) / .25f));
                for (int i = 0; i <= samples; i++)
                    result.Add(route.Evaluate(Mathf.Lerp(low, high, (float)i / samples)).Position);
            }
            return result.ToArray();
        }

        private List<Vector2> SparseCloudRanges()
        {
            Vector2[] authored = definition.CloudStepRanges;
            bool fallback = authored == null || authored.Length == 0;
            int count = fallback ? 1 : authored.Length;
            var ranges = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 value = fallback ? cloudBankRange : authored[i];
                if (!Finite(value.x) || !Finite(value.y) || value.x < 0 || value.y <= value.x || value.y > route.Length) continue;
                ranges.Add(value);
            }
            ranges.Sort((a, b) => a.x.CompareTo(b.x));
            for (int i = 1; i < ranges.Count;)
            {
                Vector2 previous = ranges[i - 1], current = ranges[i];
                if (current.x > previous.y) { i++; continue; }
                ranges[i - 1] = new Vector2(previous.x, Mathf.Max(previous.y, current.y));
                ranges.RemoveAt(i);
            }
            return ranges;
        }

        private List<CloudLanding> CloudLandings()
        {
            var result = new List<CloudLanding>();
            if (definition.Chart == null || !Finite(definition.StartDistance) || !Finite(definition.DistanceScale) ||
                definition.DistanceScale <= 0 || !Finite(definition.LateralScale)) return result;
            MapTimeline map;
            try { map = JsonUtility.FromJson<MapTimeline>(definition.Chart.text); }
            catch (ArgumentException) { return result; }
            if (map?.elements == null) return result;
            for (int i = 0; i < map.elements.Length; i++)
            {
                MapElement element = map.elements[i];
                if (element == null || element.updateType != 0 || (element.id != 0 && element.id != 1 && element.id != 4) ||
                    !Finite(element.pos.x) || !Finite(element.pos.z)) continue;
                float distance = definition.StartDistance + element.pos.z * definition.DistanceScale;
                float lateral = element.pos.x * definition.LateralScale;
                if (Finite(distance) && Finite(lateral)) result.Add(new CloudLanding(distance, lateral));
            }
            return result;
        }

        private bool PlaceSparseCloud(Bank item, float distance, float lateral, float yaw, Vector2 range, List<Bank> placed)
        {
            IslandRoute.Frame frame = route.Evaluate(distance);
            item.distance = distance; item.right = frame.Right;
            item.rotation = frame.Rotation * Quaternion.Euler(0, yaw, 0);
            item.center = frame.Position + frame.Right * lateral;
            float minOffset = float.MaxValue, maxOffset = float.MinValue, topOffset = float.MinValue;
            for (int i = 0; i < item.vertices.Length; i++)
            {
                Vector3 offset = item.rotation * Vector3.Scale(item.vertices[i], item.scale);
                float along = Vector3.Dot(offset, frame.Tangent);
                minOffset = Mathf.Min(minOffset, along); maxOffset = Mathf.Max(maxOffset, along);
                topOffset = Mathf.Max(topOffset, offset.y);
            }
            float low = distance + minOffset - CloudDrift, high = distance + maxOffset + CloudDrift;
            if (low < range.x || high > range.y) return false;
            for (int i = 0; i < cloudGroundSamples.Length; i++)
            {
                Vector3 axis = cloudGroundSamples[i] - item.center; axis.y = 0;
                float separation = axis.magnitude;
                if (separation > item.horizontalRadius + CloudDrift) continue;
                if (separation < .0001f) return false;
                axis /= separation;
                float extent = float.MinValue;
                for (int v = 0; v < item.vertices.Length; v++)
                    extent = Mathf.Max(extent, Vector3.Dot(item.rotation * Vector3.Scale(item.vertices[v], item.scale), axis));
                if (separation - extent <= CloudDrift) return false;
            }
            for (int i = 0; i < placed.Count; i++)
            {
                Bank other = placed[i];
                Vector3 axis = item.center - other.center; axis.y = 0;
                if (axis.sqrMagnitude < .0001f) return false;
                axis.Normalize();
                float near = float.MaxValue, far = float.MinValue;
                for (int v = 0; v < item.vertices.Length; v++)
                    near = Mathf.Min(near, Vector3.Dot(item.center + item.rotation * Vector3.Scale(item.vertices[v], item.scale), axis));
                for (int v = 0; v < other.vertices.Length; v++)
                    far = Mathf.Max(far, Vector3.Dot(other.center + other.rotation * Vector3.Scale(other.vertices[v], other.scale), axis));
                // Reserve each cloud's entire horizontal drift, even where the
                // route bends and a lateral drift projects onto this gap axis.
                if (near - far < CloudAirGap + CloudDrift * 2) return false;
            }
            float floor = frame.Position.y;
            int samples = Mathf.Max(1, Mathf.CeilToInt((high - low) / .05f));
            for (int i = 0; i <= samples; i++)
                floor = Mathf.Min(floor, route.Evaluate(Mathf.Lerp(low, high, (float)i / samples)).Position.y);
            item.center.y = floor + definition.TileElevation - .76f - CloudBob - topOffset;
            return true;
        }

        public void Dispose()
        {
            configured = false;
            if (poolRoot != null) Release(poolRoot.gameObject);
            poolRoot = null;
            for (int i = 0; i < meshes.Count; i++) Release(meshes[i]);
            for (int i = 0; i < materials.Count; i++) Release(materials[i]);
            meshes.Clear(); materials.Clear();
            fauna = Array.Empty<Fauna>(); clouds = Array.Empty<Cloud>(); bank = Array.Empty<Bank>();
            cloudGroundSamples = Array.Empty<Vector3>();
            definition = null; route = null; elapsed = 0;
            ActiveFaunaCount = ActiveSkyCloudCount = ActiveCloudBankCount = 0;
        }

        private void OnDestroy() => Dispose();

        private Transform NewRoot(string label)
        {
            var root = new GameObject(label).transform;
            root.SetParent(poolRoot, false);
            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform AddMesh(Transform parent, string label, Mesh mesh, Material material)
        {
            var node = new GameObject(label);
            node.transform.SetParent(parent, false);
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return node.transform;
        }

        private Material CreateMaterial(string label, Color tint)
        {
            Shader shader = Shader.Find("RadioArchipelago/Island Surface");
            if (shader == null) throw new InvalidOperationException("Living nature requires the island's shared lit surface shader.");
            var material = new Material(shader) { name = label, hideFlags = HideFlags.DontSave, enableInstancing = true };
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", .06f);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Cull", 0);
            materials.Add(material);
            return material;
        }

        private Mesh CreateBody(Species species)
        {
            var builder = new MeshBuilder();
            if (species == Species.Fairy)
            {
                Color warm = new Color(1, .83f, .4f);
                builder.Ellipsoid(Vector3.zero, new Vector3(.10f, .18f, .10f), warm, 6, 3);
                builder.Triangle(new Vector3(-.22f, 0, 0), new Vector3(0, .045f, 0), new Vector3(0, -.045f, 0), warm);
                builder.Triangle(new Vector3(.22f, 0, 0), new Vector3(0, -.045f, 0), new Vector3(0, .045f, 0), warm);
            }
            else if (species == Species.Bird)
            {
                Color blue = new Color(.28f, .44f, .56f);
                builder.Ellipsoid(Vector3.zero, new Vector3(.12f, .10f, .32f), blue);
                builder.Ellipsoid(new Vector3(0, .05f, .29f), new Vector3(.10f, .095f, .12f), new Color(.87f, .92f, .88f));
                builder.Triangle(new Vector3(-.065f, .045f, .35f), new Vector3(.065f, .045f, .35f), new Vector3(0, .015f, .49f), new Color(1, .68f, .22f));
                builder.Triangle(new Vector3(-.18f, 0, -.47f), new Vector3(.18f, 0, -.47f), new Vector3(0, 0, -.18f), blue);
            }
            else
            {
                Color body = species == Species.Dragonfly ? new Color(.08f, .46f, .47f) : new Color(.18f, .21f, .29f);
                builder.Ellipsoid(Vector3.zero, new Vector3(.045f, .055f, species == Species.Dragonfly ? .52f : .25f), body);
                builder.Ellipsoid(new Vector3(0, .015f, .24f), new Vector3(.075f, .07f, .08f), body);
                if (species == Species.Butterfly)
                {
                    builder.Triangle(new Vector3(-.025f, .025f, .26f), new Vector3(-.12f, .07f, .42f), new Vector3(-.09f, .07f, .41f), body);
                    builder.Triangle(new Vector3(.025f, .025f, .26f), new Vector3(.09f, .07f, .41f), new Vector3(.12f, .07f, .42f), body);
                }
            }
            return Own(builder.Finish("Island " + species + " shared body"));
        }

        private Mesh CreateWings(Species species)
        {
            var builder = new MeshBuilder();
            Color edge = species == Species.Bird ? new Color(.32f, .49f, .59f) : new Color(.11f, .37f, .42f);
            Color face = species == Species.Butterfly ? new Color(1, .59f, .35f) :
                species == Species.Dragonfly ? new Color(.63f, .91f, .89f) : new Color(.89f, .93f, .88f);
            if (species == Species.Butterfly)
            {
                builder.Triangle(new Vector3(.02f, 0, .10f), new Vector3(.49f, 0, .40f), new Vector3(.60f, 0, .12f), face, face, edge);
                builder.Triangle(new Vector3(.02f, 0, .10f), new Vector3(.60f, 0, .12f), new Vector3(.29f, 0, -.10f), face, edge, face);
                builder.Triangle(new Vector3(.02f, 0, -.10f), new Vector3(.29f, 0, -.10f), new Vector3(.44f, 0, -.36f), face, face, edge);
                builder.Triangle(new Vector3(.02f, 0, -.10f), new Vector3(.44f, 0, -.36f), new Vector3(.08f, 0, -.28f), face, edge, face);
                builder.Triangle(new Vector3(.19f, .003f, .17f), new Vector3(.38f, .003f, .27f), new Vector3(.38f, .003f, .10f), new Color(1, .88f, .43f));
            }
            else if (species == Species.Dragonfly)
            {
                builder.Triangle(new Vector3(.02f, 0, .20f), new Vector3(.82f, 0, .39f), new Vector3(.72f, 0, .13f), face, face, edge);
                builder.Triangle(new Vector3(.02f, 0, .20f), new Vector3(.72f, 0, .13f), new Vector3(.10f, 0, .10f), face, edge, face);
                builder.Triangle(new Vector3(.02f, 0, -.02f), new Vector3(.72f, 0, -.04f), new Vector3(.69f, 0, -.28f), face, face, edge);
                builder.Triangle(new Vector3(.02f, 0, -.02f), new Vector3(.69f, 0, -.28f), new Vector3(.08f, 0, -.12f), face, edge, face);
            }
            else
            {
                builder.Triangle(new Vector3(.03f, 0, .12f), new Vector3(.46f, 0, .18f), new Vector3(.87f, 0, -.20f), face, face, edge);
                builder.Triangle(new Vector3(.03f, 0, .12f), new Vector3(.87f, 0, -.20f), new Vector3(.39f, 0, -.32f), face, edge, edge);
                builder.Triangle(new Vector3(.03f, 0, .12f), new Vector3(.39f, 0, -.32f), new Vector3(.10f, 0, -.18f), face, edge, face);
            }
            return Own(builder.Finish("Island " + species + " shared wings"));
        }

        private readonly struct DensityLobe
        {
            internal readonly Vector3 center, radius, inverse;
            internal readonly float minimumRadius;
            internal DensityLobe(Vector3 center, Vector3 radius)
            {
                this.center = center; this.radius = radius;
                inverse = new Vector3(1 / radius.x, 1 / radius.y, 1 / radius.z);
                minimumRadius = Mathf.Min(radius.x, Mathf.Min(radius.y, radius.z));
            }
        }

        private Mesh CreateSparseCloudMesh(int shape)
        {
            DensityLobe[] lobes = CloudDensityLobes(shape);
            var domain = new Bounds(lobes[0].center, lobes[0].radius * 2);
            for (int i = 1; i < lobes.Length; i++) domain.Encapsulate(new Bounds(lobes[i].center, lobes[i].radius * 2));
            domain.Expand(.5f);
            List<Vector3> positions = null;
            List<int> triangles = null;
            // A welded implicit surface exposes many unequal billows without
            // retaining any internal, intersecting ellipsoid surfaces. Its small
            // grid is sampled only once at setup; mesh budget is explicit.
            for (int resolution = 34; resolution >= 24; resolution -= 2)
            {
                PolygonizeCloud(lobes, domain, resolution, out positions, out triangles);
                if (triangles.Count / 3 <= 16000) break;
            }
            if (triangles == null || triangles.Count == 0 || triangles.Count / 3 > 16000)
                throw new InvalidOperationException("Cloud density surface exceeded its bounded triangle budget.");
            Bounds target = shape == 1 ? new Bounds(new Vector3(-.025f, .17f, .13f), new Vector3(1.87f, 1.92f, 1.52f)) :
                shape == 2 ? new Bounds(new Vector3(-.03f, .165f, .205f), new Vector3(2.50f, 2.43f, 1.91f)) :
                new Bounds(new Vector3(-.02f, .04f, .19f), new Vector3(2.34f, 1.96f, 1.82f));
            var raw = new Bounds(positions[0], Vector3.zero);
            for (int i = 1; i < positions.Count; i++) raw.Encapsulate(positions[i]);
            // Fit the whole cloud uniformly. Stretching its height to fill the
            // old safety envelope turns round billows into vertical rock faces.
            // A smaller rounded underside is fine; keep the same safe top cap.
            float fit = Mathf.Min(target.size.x / raw.size.x, Mathf.Min(target.size.y / raw.size.y, target.size.z / raw.size.z));
            Vector3 center = new Vector3(target.center.x, target.max.y - raw.extents.y * fit, target.center.z);
            var colors = new Color[positions.Count];
            var smoothNormals = new Vector3[positions.Count];
            Color underside = new Color(.73f, .84f, .95f), shoulder = new Color(.94f, .975f, 1), crown = Color.white;
            for (int i = 0; i < positions.Count; i++)
            {
                smoothNormals[i] = CloudGradient(positions[i], lobes).normalized;
                Vector3 p = positions[i];
                p = new Vector3(Mathf.InverseLerp(raw.min.x, raw.max.x, p.x), Mathf.InverseLerp(raw.min.y, raw.max.y, p.y),
                    Mathf.InverseLerp(raw.min.z, raw.max.z, p.z));
                float tint = Mathf.SmoothStep(0, 1, Mathf.Clamp01(p.y * 1.3f));
                colors[i] = Color.Lerp(Color.Lerp(underside, shoulder, tint), crown, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 1, p.y)));
                positions[i] = center + (positions[i] - raw.center) * fit;
            }
            var mesh = new Mesh { name = "Island coherent billow shared " + shape, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(positions); mesh.colors = colors;
            mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            ApplyCloudNormals(mesh, smoothNormals, triangles);
            return Own(mesh);
        }

        private static DensityLobe[] CloudDensityLobes(int shape)
        {
            var lobes = new List<DensityLobe>(64);
            switch (shape)
            {
                case 0: // Long, low bank: a distributed ridge above a broad base.
                    Lobe(lobes, new Vector3(0, -.14f, 0), new Vector3(1.55f, .25f, .50f));
                    for (int i = 0; i < 7; i++)
                    {
                        float size = Mathf.Lerp(.34f, .44f, Noise(i + 811));
                        Lobe(lobes, new Vector3(-1.26f + i * .42f, Mathf.Lerp(.02f, .13f, Noise(i + 821)), (i % 2 == 0 ? -.13f : .15f)),
                            new Vector3(size * 1.15f, size * .83f, size));
                    }
                    for (int i = 0; i < 9; i++)
                    {
                        float size = Mathf.Lerp(.20f, .29f, Noise(i + 827));
                        Lobe(lobes, new Vector3(-1.28f + i * .32f, Mathf.Lerp(.20f, .34f, Noise(i + 829)), Mathf.Lerp(-.24f, .24f, Noise(i + 839))), Vector3.one * size);
                    }
                    BillowRing(lobes, Vector3.zero, new Vector2(1.48f, .52f), -.08f, .10f, .19f, .27f, 14, 853, .10f);
                    Lobe(lobes, new Vector3(1.74f, -.13f, .12f), new Vector3(.20f, .11f, .15f));
                    Lobe(lobes, new Vector3(-1.70f, -.14f, -.10f), new Vector3(.20f, .11f, .15f));
                    break;
                case 1: // Upright cumulus: a rounded tower and a high uneven cap.
                    Lobe(lobes, new Vector3(0, -.40f, 0), new Vector3(.76f, .32f, .61f));
                    Lobe(lobes, new Vector3(-.28f, -.02f, .03f), new Vector3(.61f, .49f, .55f));
                    Lobe(lobes, new Vector3(.07f, .48f, -.02f), new Vector3(.61f, .65f, .55f));
                    Lobe(lobes, new Vector3(.10f, 1.10f, -.09f), new Vector3(.47f, .53f, .44f));
                    Lobe(lobes, new Vector3(-.37f, .74f, .10f), new Vector3(.35f, .39f, .36f));
                    BillowRing(lobes, Vector3.zero, new Vector2(.66f, .52f), -.23f, .30f, .21f, .31f, 12, 877, .17f);
                    BillowRing(lobes, Vector3.zero, new Vector2(.50f, .43f), .50f, .88f, .21f, .29f, 10, 887, .31f);
                    BillowRing(lobes, new Vector3(.08f, 0, -.07f), new Vector2(.30f, .28f), 1.10f, 1.30f, .21f, .30f, 7, 907, .13f);
                    break;
                case 2: // Two clearly separated crowns with a low central saddle.
                    Lobe(lobes, new Vector3(0, -.27f, 0), new Vector3(1.34f, .31f, .57f));
                    Lobe(lobes, new Vector3(-.72f, .24f, .03f), new Vector3(.61f, .62f, .52f));
                    Lobe(lobes, new Vector3(.72f, .32f, -.06f), new Vector3(.60f, .69f, .51f));
                    Lobe(lobes, new Vector3(-.18f, -.08f, .44f), new Vector3(.56f, .29f, .36f));
                    Lobe(lobes, new Vector3(.04f, -.08f, -.44f), new Vector3(.56f, .29f, .36f));
                    BillowRing(lobes, new Vector3(-.72f, 0, .03f), new Vector2(.29f, .25f), .58f, .74f, .21f, .30f, 6, 919, .14f);
                    BillowRing(lobes, new Vector3(.72f, 0, -.06f), new Vector2(.28f, .25f), .69f, .86f, .21f, .30f, 6, 929, .40f);
                    BillowRing(lobes, Vector3.zero, new Vector2(1.27f, .57f), -.12f, .10f, .20f, .29f, 14, 937, .12f);
                    BillowRing(lobes, Vector3.zero, new Vector2(1.00f, .63f), .08f, .30f, .18f, .26f, 10, 947, .26f);
                    break;
                case 3: // Broad round footprint: low domes spread in every direction.
                    Lobe(lobes, new Vector3(0, -.22f, 0), new Vector3(.93f, .28f, .93f));
                    Lobe(lobes, new Vector3(.03f, .06f, .02f), new Vector3(.78f, .40f, .77f));
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = (i + .5f) * Mathf.PI * .5f;
                        Lobe(lobes, new Vector3(Mathf.Cos(angle) * .61f, -.02f, Mathf.Sin(angle) * .61f), new Vector3(.53f, .35f, .53f));
                    }
                    BillowRing(lobes, Vector3.zero, new Vector2(.48f, .48f), .20f, .36f, .25f, .35f, 9, 967, .21f);
                    BillowRing(lobes, Vector3.zero, new Vector2(.94f, .94f), -.13f, .09f, .20f, .29f, 15, 977, .14f);
                    BillowRing(lobes, Vector3.zero, new Vector2(.82f, .82f), .08f, .22f, .19f, .27f, 11, 983, .38f);
                    break;
                default: // Left-heavy body with unequal wisps trailing far to the right.
                    Lobe(lobes, new Vector3(-.45f, -.19f, -.02f), new Vector3(.95f, .29f, .53f));
                    Lobe(lobes, new Vector3(-.87f, .06f, -.08f), new Vector3(.56f, .43f, .49f));
                    Lobe(lobes, new Vector3(-.23f, .16f, .10f), new Vector3(.60f, .49f, .48f));
                    Lobe(lobes, new Vector3(.47f, -.02f, .04f), new Vector3(.49f, .32f, .37f));
                    BillowRing(lobes, new Vector3(-.45f, 0, 0), new Vector2(.58f, .35f), .24f, .42f, .21f, .31f, 8, 997, .12f);
                    BillowRing(lobes, new Vector3(-.45f, 0, 0), new Vector2(.95f, .56f), -.09f, .14f, .18f, .28f, 13, 1009, .31f);
                    Lobe(lobes, new Vector3(.88f, -.10f, .04f), new Vector3(.40f, .24f, .28f));
                    Lobe(lobes, new Vector3(1.27f, -.16f, -.02f), new Vector3(.31f, .18f, .22f));
                    Lobe(lobes, new Vector3(1.60f, -.18f, .12f), new Vector3(.25f, .14f, .18f));
                    Lobe(lobes, new Vector3(1.86f, -.20f, .01f), new Vector3(.17f, .10f, .13f));
                    Lobe(lobes, new Vector3(2.02f, -.22f, .08f), new Vector3(.11f, .07f, .08f));
                    Lobe(lobes, new Vector3(-1.42f, -.15f, .04f), new Vector3(.25f, .16f, .21f));
                    Lobe(lobes, new Vector3(-1.62f, -.20f, 0), new Vector3(.14f, .10f, .13f));
                    break;
            }
            return lobes.ToArray();
        }

        private static void Lobe(List<DensityLobe> lobes, Vector3 center, Vector3 radius) => lobes.Add(new DensityLobe(center, radius));

        private static void BillowRing(List<DensityLobe> lobes, Vector3 center, Vector2 ellipse, float lowY, float highY,
            float lowSize, float highSize, int count, int seed, float phase)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count + phase;
                float size = Mathf.Lerp(lowSize, highSize, Noise(seed + i * 7));
                Vector3 position = center + new Vector3(Mathf.Cos(angle) * ellipse.x,
                    Mathf.Lerp(lowY, highY, Noise(seed + i * 7 + 1)), Mathf.Sin(angle) * ellipse.y);
                Lobe(lobes, position, new Vector3(size, size * Mathf.Lerp(.91f, 1.10f, Noise(seed + i * 7 + 2)),
                    size * Mathf.Lerp(.94f, 1.06f, Noise(seed + i * 7 + 3))));
            }
        }
        private const float CloudBlend = .15f;
        private static float CloudDistance(Vector3 point, DensityLobe[] lobes)
        {
            float distance = float.MaxValue;
            for (int i = 0; i < lobes.Length; i++)
            {
                DensityLobe lobe = lobes[i];
                float next = (Vector3.Scale(point - lobe.center, lobe.inverse).magnitude - 1) * lobe.minimumRadius;
                float h = Mathf.Max(CloudBlend - Mathf.Abs(distance - next), 0) / CloudBlend;
                distance = Mathf.Min(distance, next) - h * h * CloudBlend * .25f;
            }
            return distance;
        }

        private static Vector3 CloudGradient(Vector3 point, DensityLobe[] lobes)
        {
            float distance = 0; Vector3 gradient = Vector3.zero;
            for (int i = 0; i < lobes.Length; i++)
            {
                DensityLobe lobe = lobes[i];
                Vector3 q = Vector3.Scale(point - lobe.center, lobe.inverse);
                float length = Mathf.Max(q.magnitude, .000001f);
                float next = (length - 1) * lobe.minimumRadius;
                Vector3 nextGradient = Vector3.Scale(q, lobe.inverse) * (lobe.minimumRadius / length);
                if (i == 0) { distance = next; gradient = nextGradient; continue; }
                float h = Mathf.Clamp01(.5f + .5f * (next - distance) / CloudBlend);
                gradient = gradient * h + nextGradient * (1 - h);
                distance = Mathf.Lerp(next, distance, h) - CloudBlend * h * (1 - h);
            }
            return gradient;
        }

        private static void ApplyCloudNormals(Mesh mesh, Vector3[] smooth, List<int> triangles)
        {
            Vector3[] area = mesh.normals, positions = mesh.vertices;
            for (int i = 0; i < smooth.Length; i++)
                if (smooth[i].sqrMagnitude < .9f || Vector3.Dot(smooth[i], area[i]) < .25f) smooth[i] = area[i];
            // The density gradient shades complete round billows smoothly.
            // A handful of coarse-grid concave/sliver faces need their local
            // area normals so interpolated shading still follows real winding.
            for (int pass = 0; pass < 4; pass++)
            {
                bool corrected = false;
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    Vector3 face = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                    float length = Mathf.Sqrt(face.sqrMagnitude);
                    if (length <= 0) continue;
                    if (Vector3.Dot(face / length, (smooth[a] + smooth[b] + smooth[c]).normalized) >= .30f &&
                        Vector3.Dot(smooth[a], smooth[b]) > -.20f && Vector3.Dot(smooth[b], smooth[c]) > -.20f &&
                        Vector3.Dot(smooth[c], smooth[a]) > -.20f) continue;
                    smooth[a] = area[a]; smooth[b] = area[b]; smooth[c] = area[c]; corrected = true;
                }
                if (!corrected) break;
            }
            mesh.normals = smooth;
        }

        private static void PolygonizeCloud(DensityLobe[] lobes, Bounds domain, int resolution, out List<Vector3> vertices, out List<int> triangles)
        {
            float step = Mathf.Max(domain.size.x, Mathf.Max(domain.size.y, domain.size.z)) / resolution;
            int nx = Mathf.CeilToInt(domain.size.x / step), ny = Mathf.CeilToInt(domain.size.y / step), nz = Mathf.CeilToInt(domain.size.z / step);
            int sx = nx + 1, sz = sx * (ny + 1), total = sz * (nz + 1);
            var grid = new Vector3[total]; var values = new float[total];
            for (int z = 0; z <= nz; z++) for (int y = 0; y <= ny; y++) for (int x = 0; x <= nx; x++)
            {
                int index = x + y * sx + z * sz;
                grid[index] = domain.min + new Vector3(domain.size.x * x / nx, domain.size.y * y / ny, domain.size.z * z / nz);
                values[index] = CloudDistance(grid[index], lobes);
                if (Mathf.Abs(values[index]) < .00001f) values[index] = values[index] < 0 ? -.00001f : .00001f;
            }
            vertices = new List<Vector3>(6000); triangles = new List<int>(36000);
            var edges = new Dictionary<ulong, int>(9000);
            int[] cube = new int[8], tetra = new int[4], inside = new int[4], outside = new int[4];
            int[] decomposition = { 0, 5, 1, 6, 0, 1, 2, 6, 0, 2, 3, 6, 0, 3, 7, 6, 0, 7, 4, 6, 0, 4, 5, 6 };
            for (int z = 0; z < nz; z++) for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                int index = x + y * sx + z * sz;
                cube[0] = index; cube[1] = index + 1; cube[2] = index + 1 + sx; cube[3] = index + sx;
                cube[4] = index + sz; cube[5] = index + 1 + sz; cube[6] = index + 1 + sx + sz; cube[7] = index + sx + sz;
                bool anyInside = false, anyOutside = false;
                for (int i = 0; i < 8; i++) { anyInside |= values[cube[i]] < 0; anyOutside |= values[cube[i]] >= 0; }
                if (!anyInside || !anyOutside) continue;
                for (int t = 0; t < decomposition.Length; t += 4)
                {
                    int ni = 0, no = 0; Vector3 innerCenter = Vector3.zero, outerCenter = Vector3.zero;
                    for (int i = 0; i < 4; i++)
                    {
                        int vertex = tetra[i] = cube[decomposition[t + i]];
                        if (values[vertex] < 0) { inside[ni++] = vertex; innerCenter += grid[vertex]; }
                        else { outside[no++] = vertex; outerCenter += grid[vertex]; }
                    }
                    if (ni == 0 || no == 0) continue;
                    Vector3 outward = outerCenter / no - innerCenter / ni;
                    if (ni == 1 || no == 1)
                    {
                        int pivot = ni == 1 ? inside[0] : outside[0];
                        int[] others = ni == 1 ? outside : inside;
                        int a = CloudEdge(pivot, others[0], grid, values, edges, vertices);
                        int b = CloudEdge(pivot, others[1], grid, values, edges, vertices);
                        int c = CloudEdge(pivot, others[2], grid, values, edges, vertices);
                        CloudTriangle(a, b, c, outward, vertices, triangles);
                    }
                    else
                    {
                        int ac = CloudEdge(inside[0], outside[0], grid, values, edges, vertices);
                        int ad = CloudEdge(inside[0], outside[1], grid, values, edges, vertices);
                        int bc = CloudEdge(inside[1], outside[0], grid, values, edges, vertices);
                        int bd = CloudEdge(inside[1], outside[1], grid, values, edges, vertices);
                        CloudTriangle(ac, ad, bc, outward, vertices, triangles);
                        CloudTriangle(ad, bd, bc, outward, vertices, triangles);
                    }
                }
            }
        }

        private static int CloudEdge(int a, int b, Vector3[] grid, float[] values, Dictionary<ulong, int> edges, List<Vector3> vertices)
        {
            if (a > b) { int swap = a; a = b; b = swap; }
            ulong key = ((ulong)(uint)a << 32) | (uint)b;
            if (edges.TryGetValue(key, out int existing)) return existing;
            float t = values[a] / (values[a] - values[b]);
            int index = vertices.Count; vertices.Add(Vector3.LerpUnclamped(grid[a], grid[b], t)); edges.Add(key, index);
            return index;
        }

        private static void CloudTriangle(int a, int b, int c, Vector3 outward, List<Vector3> vertices, List<int> triangles)
        {
            Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(cross, outward) < 0) { int swap = b; b = c; c = swap; }
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }
        private Mesh Own(Mesh mesh) { meshes.Add(mesh); return mesh; }
        private static Species SpeciesFor(int index)
        {
            switch (index % 8)
            {
                case 1: case 6: return Species.Dragonfly;
                case 3: case 7: return Species.Bird;
                case 4: return Species.Fairy;
                default: return Species.Butterfly;
            }
        }
        private static int Mod(int value, int divisor) { int r = value % divisor; return r < 0 ? r + divisor : r; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float LoopDelta(float distance, float observer, float length)
            => Mathf.Repeat(distance - observer + length * .5f, length) - length * .5f;
        private static float Noise(int seed)
        {
            uint value = unchecked((uint)seed * 747796405u + 2891336453u);
            value = ((value >> (int)((value >> 28) + 4)) ^ value) * 277803737u;
            return ((value >> 22) ^ value) / 4294967295f;
        }
        private static void SetActive(Transform target, bool active)
        { if (target.gameObject.activeSelf != active) target.gameObject.SetActive(active); }
        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (value is GameObject node) node.SetActive(false);
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();

            internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color) => Triangle(a, b, c, color, color, color);
            internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
            {
                int first = vertices.Count;
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                colors.Add(ca); colors.Add(cb); colors.Add(cc);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            }

            internal void Ellipsoid(Vector3 center, Vector3 scale, Color color, int segments = 8, int rings = 4)
            {
                int first = vertices.Count;
                for (int y = 0; y <= rings; y++)
                {
                    float latitude = (float)y / rings * Mathf.PI;
                    float radius = Mathf.Sin(latitude), height = Mathf.Cos(latitude);
                    for (int x = 0; x <= segments; x++)
                    {
                        float longitude = (float)x / segments * Mathf.PI * 2;
                        var unit = new Vector3(Mathf.Cos(longitude) * radius, height, Mathf.Sin(longitude) * radius);
                        vertices.Add(center + Vector3.Scale(unit, scale));
                        normals.Add(new Vector3(unit.x / scale.x, unit.y / scale.y, unit.z / scale.z).normalized);
                        colors.Add(color);
                    }
                }
                for (int y = 0; y < rings; y++)
                    for (int x = 0; x < segments; x++)
                    {
                        int a = first + y * (segments + 1) + x, b = a + segments + 1;
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                        triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                    }
            }

            internal Mesh Finish(string label)
            {
                var mesh = new Mesh { name = label, hideFlags = HideFlags.DontSave };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
