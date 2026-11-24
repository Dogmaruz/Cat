using System;
using System.Collections.Generic;
using System.Linq;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Conservative full-body clearance from the actual ceramic mesh projections, including curved caps.</summary>
    public static class IslandSpanSupportSpacing
    {
        public const float NativeHoldRadius = .70f;
        [Serializable] public sealed class Violation
        {
            public int first, second;
            public string firstKind, secondKind;
            public float playerZ, playerBegin, playerEnd, planarGap, requiredGap;
        }
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int supports, spans, coexistingSpanPairs;
            public Violation[] violations;
        }
        private sealed class Footprint
        {
            public MapElement element;
            public MapSegment segment;
            public bool span, sweptFollow;
            public float start, end;
            public double speed;
            public Vector3[] vertices;
            public int[] triangles;
            public Vector3 min, max;
        }

        public static Report Audit(MapTimeline map, IslandRoute route, IslandDynamicSupportSpacing.Settings settings,
            float tileThickness = .30f, float tileElevation = 1)
        {
            var definition = ScriptableObject.CreateInstance<IslandStageDefinition>();
            definition.StartDistance = settings.StartDistance; definition.DistanceScale = settings.DistanceScale;
            definition.LateralScale = settings.LateralScale; definition.TileThickness = tileThickness; definition.TileElevation = tileElevation;
            var supports = new List<Footprint>();
            try
            {
                foreach (MapSegment segment in map.segments.Where(s => s != null && s.kind != "Theme"))
                {
                    MapElement e = map.elements[segment.sourceIndex];
                    if (e.id < 0 || e.id > 4) continue;
                    var support = new Footprint { element = e, segment = segment, span = e.id == 2 || e.id == 3,
                        sweptFollow = e.id == 4 && e.updateType == 2, start = e.pos.z,
                        end = e.id == 2 || e.id == 3 ? segment.endZ : e.id == 4 ? e.pos.z + Math.Max(0, e.mfendPos) : e.pos.z };
                    if (support.span)
                    {
                        var body = new IslandHoldSurface(route, definition, support.start, support.end, e.pos.x, e.pos.y, NativeHoldRadius);
                        Mesh mesh = body.Mesh;
                        try
                        {
                            support.vertices = mesh.vertices.Select(v => v + body.Pivot).ToArray();
                            var triangles = new List<int>();
                            for (int i = 0; i < mesh.subMeshCount; i++) triangles.AddRange(mesh.GetTriangles(i));
                            support.triangles = triangles.ToArray();
                        }
                        finally { UnityEngine.Object.DestroyImmediate(mesh); }
                    }
                    else if (support.sweptFollow)
                    {
                        // An actor-driven Follow may freeze at any release point.
                        // A rectangle encloses every reached route station and
                        // the complete native lane, including changing headings.
                        float length = (support.end - support.start) * settings.DistanceScale;
                        int steps = Math.Max(1, (int)Math.Ceiling(length / .1f));
                        var stations = new Vector3[steps + 1];
                        for (int i = 0; i <= steps; i++) stations[i] = route.Evaluate(settings.StartDistance +
                            (support.start + (support.end - support.start) * i / steps) * settings.DistanceScale).Position;
                        float padding = NativeRhythmRules.HorizontalLimit * settings.LateralScale + IslandSupportSpacing.NativeJumpRadius + .1f;
                        float minX = stations.Min(p => p.x) - padding, maxX = stations.Max(p => p.x) + padding;
                        float minZ = stations.Min(p => p.z) - padding, maxZ = stations.Max(p => p.z) + padding;
                        support.vertices = new[] { new Vector3(minX, -1e20f, minZ), new Vector3(minX, 1e20f, maxZ),
                            new Vector3(maxX, -1e20f, maxZ), new Vector3(maxX, 1e20f, minZ) };
                        support.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                    }
                    else
                    {
                        support.vertices = RoundVertices(route, settings, e, e.pos.z, tileThickness, tileElevation);
                        support.triangles = RoundTriangles();
                        if (e.updateType == 3) support.speed = 2 * Math.PI / 30 * settings.LateralScale;
                        else if (e.updateType == 1)
                        {
                            if (!(e.begin > e.end)) throw new ArgumentException("Native linear supports require begin > end.");
                            double dx = (e.pos.x - e.mstartPos.x) * settings.LateralScale, dy = e.pos.y - e.mstartPos.y;
                            support.speed = Math.Sqrt(dx * dx + dy * dy) / (e.begin - e.end);
                        }
                    }
                    Bounds(support.vertices, out support.min, out support.max);
                    if (!support.span && !support.sweptFollow && e.updateType == 3)
                    {
                        var frame = route.Evaluate(settings.StartDistance + e.pos.z * settings.DistanceScale);
                        Vector3 offset = frame.Right * settings.LateralScale;
                        var envelope = RoundVertices(route, settings, e, e.pos.z, tileThickness, tileElevation);
                        Vector3 contact = Center(route, settings, e, e.pos.z, tileElevation);
                        Vector3 origin = frame.Position + Vector3.up * (e.pos.y + tileElevation);
                        Bounds(envelope.Select(v => v - contact + origin - offset).Concat(
                            envelope.Select(v => v - contact + origin + offset)).ToArray(), out support.min, out support.max);
                    }
                    else if (!support.span && !support.sweptFollow && e.updateType == 1)
                    {
                        Bounds(RoundVertices(route, settings, e, e.pos.z - e.begin, tileThickness, tileElevation).Concat(
                            RoundVertices(route, settings, e, e.pos.z - e.end, tileThickness, tileElevation)).ToArray(), out support.min, out support.max);
                    }
                    supports.Add(support);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }

            var violations = new List<Violation>();
            var report = new Report { supports = supports.Count, spans = supports.Count(s => s.span) };
            double ahead = settings.VisibleAhead / settings.DistanceScale;
            double behind = Math.Max(settings.VisibleBehind / settings.DistanceScale, .55 * settings.ForwardSpeed);
            for (int i = 0; i < supports.Count; i++)
                for (int j = i + 1; j < supports.Count; j++)
                {
                    Footprint a = supports[i], b = supports[j];
                    if (!a.span && !b.span) continue; // Round/Follow pairs have the existing dynamic gate.
                    double low = Math.Max(a.start, b.start) - ahead, high = Math.Min(a.end, b.end) + behind;
                    if (high < low) continue;
                    report.coexistingSpanPairs++;
                    int steps = a.speed + b.speed == 0 ? 0 : Math.Max(1, (int)Math.Ceiling((high - low) /
                        (.005 / Math.Max(1, settings.LateralScale))));
                    double reserve = steps == 0 ? 0 : (a.speed + b.speed) * (high - low) / steps * .5;
                    float gap = (float)(IslandSupportSpacing.MinimumGap + IslandDynamicSupportSpacing.MotionMargin + reserve);
                    if (AxisGap(a.min.y, a.max.y, b.min.y, b.max.y) >= IslandDynamicSupportSpacing.SpringDifference + gap ||
                        BoundsPlanarGap(a, b) >= gap) continue;
                    for (int sample = 0; sample <= steps; sample++)
                    {
                        float playerZ = (float)(steps == 0 ? low : low + (high - low) * sample / steps);
                        Vector3[] av = a.span || a.sweptFollow ? a.vertices : RoundVertices(route, settings, a.element, playerZ, tileThickness, tileElevation);
                        Vector3[] bv = b.span || b.sweptFollow ? b.vertices : RoundVertices(route, settings, b.element, playerZ, tileThickness, tileElevation);
                        float actual = ProjectedMeshGap(av, a.triangles, bv, b.triangles, gap);
                        if (actual >= gap) continue;
                        violations.Add(new Violation { first = a.element.sourceIndex, second = b.element.sourceIndex,
                            firstKind = a.segment.kind, secondKind = b.segment.kind, playerZ = playerZ,
                            playerBegin = (float)low, playerEnd = (float)high, planarGap = actual, requiredGap = gap });
                        break;
                    }
                }
            report.violations = violations.ToArray(); report.passed = violations.Count == 0;
            return report;
        }

        private static Vector3 Center(IslandRoute route, IslandDynamicSupportSpacing.Settings settings, MapElement e, float playerZ, float elevation)
        {
            var frame = route.Evaluate(settings.StartDistance + e.pos.z * settings.DistanceScale);
            float x = e.pos.x, y = e.pos.y;
            if (e.updateType == 3) x = NativeRhythmRules.MoveCycleX(e.pos.x, e.pos.z - playerZ, e.msLeft != 0);
            else if (e.updateType == 1)
            {
                float t = NativeRhythmRules.MovePercent(e.pos.z - playerZ, e.begin, e.end);
                x = Mathf.Lerp(e.mstartPos.x, e.pos.x, t); y = Mathf.Lerp(e.mstartPos.y, e.pos.y, t);
            }
            return frame.Position + frame.Right * (x * settings.LateralScale) + Vector3.up * (y + elevation);
        }
        private static Vector3[] RoundVertices(IslandRoute route, IslandDynamicSupportSpacing.Settings settings,
            MapElement e, float playerZ, float thickness, float elevation)
        {
            var frame = route.Evaluate(settings.StartDistance + e.pos.z * settings.DistanceScale);
            Vector3 center = Center(route, settings, e, playerZ, elevation);
            var vertices = new Vector3[42]; vertices[40] = center; vertices[41] = center - Vector3.up * thickness;
            for (int i = 0; i < 40; i++)
            {
                float angle = i * Mathf.PI * 2 / 40;
                vertices[i] = center + (frame.Right * Mathf.Cos(angle) + frame.Tangent * Mathf.Sin(angle)) * IslandSupportSpacing.NativeJumpRadius;
            }
            return vertices;
        }
        private static int[] RoundTriangles()
        {
            var triangles = new int[120];
            for (int i = 0; i < 40; i++) { triangles[i * 3] = 40; triangles[i * 3 + 1] = i; triangles[i * 3 + 2] = (i + 1) % 40; }
            return triangles;
        }
        private static void Bounds(Vector3[] points, out Vector3 min, out Vector3 max)
        {
            min = max = points[0];
            foreach (Vector3 p in points)
            {
                min = new Vector3(Math.Min(min.x, p.x), Math.Min(min.y, p.y), Math.Min(min.z, p.z));
                max = new Vector3(Math.Max(max.x, p.x), Math.Max(max.y, p.y), Math.Max(max.z, p.z));
            }
        }
        private static float AxisGap(float minA, float maxA, float minB, float maxB) => Math.Max(0, Math.Max(minA - maxB, minB - maxA));
        private static float BoundsPlanarGap(Footprint a, Footprint b)
        {
            float x = AxisGap(a.min.x, a.max.x, b.min.x, b.max.x), z = AxisGap(a.min.z, a.max.z, b.min.z, b.max.z);
            return Mathf.Sqrt(x * x + z * z);
        }
        private static float ProjectedMeshGap(Vector3[] av, int[] at, Vector3[] bv, int[] bt, float cap)
        {
            double best = cap * cap;
            for (int i = 0; i < at.Length; i += 3)
            {
                Vector3 a = av[at[i]], b = av[at[i + 1]], c = av[at[i + 2]];
                float minX = Math.Min(a.x, Math.Min(b.x, c.x)), maxX = Math.Max(a.x, Math.Max(b.x, c.x));
                float minZ = Math.Min(a.z, Math.Min(b.z, c.z)), maxZ = Math.Max(a.z, Math.Max(b.z, c.z));
                for (int j = 0; j < bt.Length; j += 3)
                {
                    Vector3 d = bv[bt[j]], e = bv[bt[j + 1]], f = bv[bt[j + 2]];
                    double dx = AxisGap(minX, maxX, Math.Min(d.x, Math.Min(e.x, f.x)), Math.Max(d.x, Math.Max(e.x, f.x)));
                    double dz = AxisGap(minZ, maxZ, Math.Min(d.z, Math.Min(e.z, f.z)), Math.Max(d.z, Math.Max(e.z, f.z)));
                    if (dx * dx + dz * dz >= best) continue;
                    if (Inside(a, d, e, f) || Inside(d, a, b, c)) return 0;
                    Vector3[] first = { a, b, c }, second = { d, e, f };
                    for (int m = 0; m < 3; m++) for (int n = 0; n < 3; n++)
                        best = Math.Min(best, SegmentSquared(first[m], first[(m + 1) % 3], second[n], second[(n + 1) % 3]));
                    if (best <= 1e-18) return 0;
                }
            }
            return (float)Math.Sqrt(best);
        }
        private static double Cross(Vector3 a, Vector3 b, Vector3 c) => ((double)b.x - a.x) * (c.z - a.z) - ((double)b.z - a.z) * (c.x - a.x);
        private static bool Inside(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            if (Math.Abs(Cross(a, b, c)) < 1e-12) return false;
            double x = Cross(a, b, p), y = Cross(b, c, p), z = Cross(c, a, p);
            return x >= -1e-10 && y >= -1e-10 && z >= -1e-10 || x <= 1e-10 && y <= 1e-10 && z <= 1e-10;
        }
        private static double SegmentSquared(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            double abx = b.x - a.x, abz = b.z - a.z, cdx = d.x - c.x, cdz = d.z - c.z;
            double determinant = abx * cdz - abz * cdx;
            if (Math.Abs(determinant) > 1e-18)
            {
                double x = c.x - a.x, z = c.z - a.z;
                double u = (x * cdz - z * cdx) / determinant, v = (x * abz - z * abx) / determinant;
                if (u >= 0 && u <= 1 && v >= 0 && v <= 1) return 0;
            }
            return Math.Min(Math.Min(PointSquared(a, c, d), PointSquared(b, c, d)), Math.Min(PointSquared(c, a, b), PointSquared(d, a, b)));
        }
        private static double PointSquared(Vector3 p, Vector3 a, Vector3 b)
        {
            double x = b.x - a.x, z = b.z - a.z, squared = x * x + z * z;
            double t = squared <= 1e-20 ? 0 : Math.Max(0, Math.Min(1, ((p.x - a.x) * x + (p.z - a.z) * z) / squared));
            double dx = p.x - a.x - x * t, dz = p.z - a.z - z * t;
            return dx * dx + dz * dz;
        }
    }
}
