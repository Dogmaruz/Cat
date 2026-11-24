using System;
using System.Collections.Generic;
using DancingCats.Clone;
using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>One-time visual radius fitting across the native supports' full coexistence and motion windows.</summary>
    public static class IslandDynamicSupportSpacing
    {
        public const float MotionMargin = .02f;
        public const float SpringDifference = .21f;
        private const double CycleRate = 2 * Math.PI / 30;
        private const double SampleStep = .005;

        public readonly struct Settings
        {
            public readonly float StartDistance, DistanceScale, VisibleAhead, VisibleBehind, ForwardSpeed, LateralScale;
            public Settings(float startDistance, float distanceScale, float visibleAhead, float visibleBehind, float forwardSpeed, float lateralScale = 1)
            {
                StartDistance = startDistance; DistanceScale = distanceScale; VisibleAhead = visibleAhead;
                VisibleBehind = visibleBehind; ForwardSpeed = forwardSpeed; LateralScale = lateralScale;
            }
        }

        public struct RadiusLimit
        {
            public int otherIndex;
            public float distance, playerBegin, playerEnd;
            public bool sweptFollow;
        }

        private readonly struct Support
        {
            public readonly MapElement Element;
            public readonly IslandRoute.Frame Frame;
            public readonly float MinX, MaxX, MinY, MaxY, EndZ;
            public readonly double Speed;
            public readonly float LateralScale;
            public bool Follow => Element.id == 4 && Element.updateType == 2;

            public Support(MapElement e, IslandRoute route, Settings settings)
            {
                Element = e; Frame = route.Evaluate(settings.StartDistance + (double)e.pos.z * settings.DistanceScale);
                LateralScale = settings.LateralScale;
                MinX = MaxX = e.pos.x; MinY = MaxY = e.pos.y; EndZ = e.pos.z; Speed = 0;
                if (e.updateType == 3) { MinX = -1; MaxX = 1; Speed = CycleRate * settings.LateralScale; }
                else if (e.updateType == 1)
                {
                    if (!(e.begin > e.end)) throw new ArgumentException("Native moving supports require begin > end.");
                    MinX = Math.Min(e.mstartPos.x, e.pos.x); MaxX = Math.Max(e.mstartPos.x, e.pos.x);
                    MinY = Math.Min(e.mstartPos.y, e.pos.y); MaxY = Math.Max(e.mstartPos.y, e.pos.y);
                    double x = ((double)e.pos.x - e.mstartPos.x) * settings.LateralScale, y = (double)e.pos.y - e.mstartPos.y;
                    Speed = Math.Sqrt(x * x + y * y) / (e.begin - e.end);
                }
                else if (e.id == 4 && e.updateType == 2)
                {
                    MinX = -NativeRhythmRules.HorizontalLimit; MaxX = NativeRhythmRules.HorizontalLimit;
                    EndZ = e.pos.z + Math.Max(0, e.mfendPos);
                    // Route motion and changing lateral basis are covered by a
                    // wide swept-lane reserve below, rather than guessing actor X.
                    Speed = 0;
                }
            }
        }

        public static void Resolve(IslandRoute route, IslandStageDefinition definition,
            IReadOnlyList<MapElement> elements, float[] preferred, float[] radii, float forwardSpeed = 6.5f)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Resolve(route, new Settings(definition.StartDistance, definition.DistanceScale,
                definition.VisibleAhead, definition.VisibleBehind, forwardSpeed, definition.LateralScale), elements, preferred, radii);
        }

        /// <summary>Authoring gate using the same lifetime, sweep and spring reserve as runtime fitting.</summary>
        public static bool CanKeepRadius(IslandRoute route, Settings settings,
            IReadOnlyList<MapElement> elements, int targetIndex, float radius)
        {
            if (route == null || elements == null || targetIndex < 0 || targetIndex >= elements.Count ||
                !Finite(radius) || radius < 0) throw new ArgumentException("A route, support index and finite radius are required.");
            Support target = new Support(elements[targetIndex], route, settings);
            double ahead = settings.VisibleAhead / settings.DistanceScale;
            double behind = Math.Max(settings.VisibleBehind / settings.DistanceScale, .52 * settings.ForwardSpeed);
            float budget = radius * 2 + IslandSupportSpacing.MinimumGap;
            for (int i = 0; i < elements.Count; i++)
            {
                if (i == targetIndex || !Round(elements[i])) continue;
                Support other = new Support(elements[i], route, settings);
                double low = Math.Max(target.Element.pos.z, other.Element.pos.z) - ahead;
                double high = Math.Min(target.EndZ, other.EndZ) + behind;
                if (high < low || !target.Follow && !other.Follow && ProjectionBound(target, other) >= budget + MotionMargin) continue;
                if (MinimumDistance(route, settings, target, other, low, high, budget) < budget) return false;
            }
            return true;
        }

        /// <summary>Scalar overload shared by authoring checks without constructing scene objects.</summary>
        public static void Resolve(IslandRoute route, Settings settings,
            IReadOnlyList<MapElement> elements, float[] preferred, float[] radii, RadiusLimit[] limits = null)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (elements == null || preferred == null || radii == null || elements.Count != preferred.Length || radii.Length != preferred.Length)
                throw new ArgumentException("Elements and visual radius arrays must have equal lengths.");
            if (limits != null && limits.Length != elements.Count) throw new ArgumentException("Radius diagnostic arrays must match elements.");
            if (!Finite(settings.StartDistance) || !Finite(settings.DistanceScale) || settings.DistanceScale <= 0 ||
                !Finite(settings.VisibleAhead) || settings.VisibleAhead < 0 || !Finite(settings.VisibleBehind) || settings.VisibleBehind < 0 ||
                !Finite(settings.ForwardSpeed) || settings.ForwardSpeed < 0 || !Finite(settings.LateralScale) || settings.LateralScale <= 0)
                throw new ArgumentException("Dynamic spacing requires finite route coordinates and visibility windows.");
            var supports = new Support[elements.Count];
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i] == null || !Finite(preferred[i]) || preferred[i] < 0)
                    throw new ArgumentException("Support elements and finite nonnegative radii are required.");
                supports[i] = new Support(elements[i], route, settings);
                radii[i] = preferred[i];
            }
            double ahead = settings.VisibleAhead / settings.DistanceScale;
            double behind = Math.Max(settings.VisibleBehind / settings.DistanceScale, .52 * settings.ForwardSpeed);
            for (int i = 0; i < supports.Length; i++)
            {
                if (!Round(supports[i].Element)) continue;
                for (int j = i + 1; j < supports.Length; j++)
                {
                    if (!Round(supports[j].Element)) continue;
                    Support a = supports[i], b = supports[j];
                    double low = Math.Max(a.Element.pos.z, b.Element.pos.z) - ahead;
                    double high = Math.Min(a.EndZ, b.EndZ) + behind;
                    if (high < low) continue; // Different musical laps never shrink one another.
                    float budget = preferred[i] + preferred[j] + IslandSupportSpacing.MinimumGap;
                    if (!a.Follow && !b.Follow && ProjectionBound(a, b) >= budget + MotionMargin) continue;
                    double minimum = MinimumDistance(route, settings, a, b, low, high, budget);
                    float radius = Math.Max(0, (float)((minimum - IslandSupportSpacing.MinimumGap) * .5));
                    if (limits != null)
                    {
                        var limit = new RadiusLimit { distance = (float)minimum, playerBegin = (float)low, playerEnd = (float)high,
                            sweptFollow = a.Follow && high >= a.Element.pos.z || b.Follow && high >= b.Element.pos.z };
                        if (radius < radii[i]) { limit.otherIndex = j; limits[i] = limit; }
                        if (radius < radii[j]) { limit.otherIndex = i; limits[j] = limit; }
                    }
                    radii[i] = Math.Min(radii[i], radius); radii[j] = Math.Min(radii[j], radius);
                }
            }
        }

        private static double MinimumDistance(IslandRoute route, Settings settings, Support a, Support b,
            double low, double high, float budget)
        {
            if (a.Follow && high >= a.Element.pos.z || b.Follow && high >= b.Element.pos.z)
                return FollowSweepDistance(route, settings, a, b, high);
            if (a.Speed == 0 && b.Speed == 0)
            {
                Pose(route, settings, a, (float)low, out Vector3 pa, out Vector3 ra, out float ha);
                Pose(route, settings, b, (float)low, out Vector3 pb, out Vector3 rb, out float hb);
                double horizontal = HorizontalSegmentDistance(pa, ra, ha, pb, rb, hb);
                double vertical = Math.Max(0, Math.Abs((double)pa.y - pb.y) - SpringDifference);
                return Math.Max(0, Math.Sqrt(horizontal * horizontal + vertical * vertical) - MotionMargin);
            }
            double minimum = double.MaxValue;
            // Keep the maximum world-space mover advance per sample constant
            // when lanes widen. Otherwise a larger derivative reserve alone
            // can unnecessarily shrink an already safely separated pair.
            double sampleStep = SampleStep / Math.Max(1, settings.LateralScale);
            int steps = Math.Max(1, (int)Math.Ceiling((high - low) / sampleStep));
            double actualStep = (high - low) / steps;
            // For fixed-XZ supports, native sine/linear derivative bounds prove
            // the distance cannot drop farther than this between samples.
            double betweenSamples = (a.Speed + b.Speed) * actualStep * .5;
            for (int i = 0; i <= steps; i++)
            {
                float z = (float)(low + (high - low) * i / steps);
                Pose(route, settings, a, z, out Vector3 pa, out Vector3 ra, out float ha);
                Pose(route, settings, b, z, out Vector3 pb, out Vector3 rb, out float hb);
                double horizontal = HorizontalSegmentDistance(pa, ra, ha, pb, rb, hb);
                double vertical = Math.Max(0, Math.Abs((double)pa.y - pb.y) - SpringDifference);
                minimum = Math.Min(minimum, Math.Sqrt(horizontal * horizontal + vertical * vertical));
            }
            return Math.Max(0, minimum - betweenSamples - MotionMargin);
        }

        private static double FollowSweepDistance(IslandRoute route, Settings settings, Support a, Support b, double high)
        {
            // A ridden Follow can freeze at its release point. Cover its entire
            // already reachable path and lane, rather than assuming it always
            // reaches the end or predicting the player's input. Other movers'
            // complete native lane/Y ranges make this deliberately conservative.
            double endA = a.Follow ? Math.Min(a.EndZ, Math.Max(a.Element.pos.z, high)) : a.Element.pos.z;
            double endB = b.Follow ? Math.Min(b.EndZ, Math.Max(b.Element.pos.z, high)) : b.Element.pos.z;
            int stepsA = Math.Max(1, (int)Math.Ceiling((endA - a.Element.pos.z) / .02));
            int stepsB = Math.Max(1, (int)Math.Ceiling((endB - b.Element.pos.z) / .02));
            double minimum = double.MaxValue;
            for (int i = 0; i <= stepsA; i++)
            {
                SweptLane(route, settings, a, a.Element.pos.z + (endA - a.Element.pos.z) * i / stepsA, high,
                    out Vector3 pa, out Vector3 ra, out float ha, out float ya);
                for (int j = 0; j <= stepsB; j++)
                {
                    SweptLane(route, settings, b, b.Element.pos.z + (endB - b.Element.pos.z) * j / stepsB, high,
                        out Vector3 pb, out Vector3 rb, out float hb, out float yb);
                    double horizontal = HorizontalSegmentDistance(pa, ra, ha, pb, rb, hb);
                    double vertical = Math.Max(0, Math.Abs((double)pa.y - pb.y) - ya - yb - SpringDifference);
                    minimum = Math.Min(minimum, Math.Sqrt(horizontal * horizontal + vertical * vertical));
                }
            }
            // Covers swept-route/basis interpolation between 2cm path samples.
            return Math.Max(0, minimum - .10 - MotionMargin);
        }

        private static void SweptLane(IslandRoute route, Settings settings, Support support, double z, double high,
            out Vector3 position, out Vector3 right, out float halfLane, out float halfHeight)
        {
            IslandRoute.Frame frame = support.Follow ? route.Evaluate(settings.StartDistance + z * settings.DistanceScale) : support.Frame;
            bool movingFollow = support.Follow && high >= support.Element.pos.z;
            float minX = support.Follow ? movingFollow ? -NativeRhythmRules.HorizontalLimit : support.Element.pos.x : support.MinX;
            float maxX = support.Follow ? movingFollow ? NativeRhythmRules.HorizontalLimit : support.Element.pos.x : support.MaxX;
            position = frame.Position + frame.Right * ((minX + maxX) * .5f * settings.LateralScale) + Vector3.up * ((support.MinY + support.MaxY) * .5f);
            right = frame.Right; halfLane = (maxX - minX) * .5f * settings.LateralScale; halfHeight = (support.MaxY - support.MinY) * .5f;
        }

        private static void Pose(IslandRoute route, Settings settings, Support support, float playerZ,
            out Vector3 position, out Vector3 right, out float halfLane)
        {
            MapElement e = support.Element;
            IslandRoute.Frame frame = support.Frame;
            float x = e.pos.x, y = e.pos.y;
            halfLane = 0;
            if (support.Follow && playerZ >= e.pos.z)
            {
                float z = e.pos.z + Mathf.Clamp(playerZ - e.pos.z, 0, Math.Max(0, e.mfendPos));
                frame = route.Evaluate(settings.StartDistance + (double)z * settings.DistanceScale);
                x = 0; halfLane = NativeRhythmRules.HorizontalLimit * settings.LateralScale;
            }
            else if (e.updateType == 3)
                x = NativeRhythmRules.MoveCycleX(e.pos.x, e.pos.z - playerZ, e.msLeft != 0);
            else if (e.updateType == 1)
            {
                float progress = NativeRhythmRules.MovePercent(e.pos.z - playerZ, e.begin, e.end);
                x = Mathf.Lerp(e.mstartPos.x, e.pos.x, progress); y = Mathf.Lerp(e.mstartPos.y, e.pos.y, progress);
            }
            position = frame.Position + frame.Right * (x * settings.LateralScale) + Vector3.up * y;
            right = frame.Right;
        }

        private static double ProjectionBound(Support a, Support b)
        {
            Vector3 axis = a.Frame.Tangent + b.Frame.Tangent;
            double magnitude = Math.Sqrt((double)axis.x * axis.x + (double)axis.z * axis.z);
            if (magnitude < .00001) return 0;
            axis = new Vector3((float)(axis.x / magnitude), 0, (float)(axis.z / magnitude));
            double center = Vector3.Dot(b.Frame.Position - a.Frame.Position, axis);
            double ca = Vector3.Dot(a.Frame.Right, axis) * a.LateralScale, cb = Vector3.Dot(b.Frame.Right, axis) * b.LateralScale;
            double low = center + Math.Min(cb * b.MinX, cb * b.MaxX) - Math.Max(ca * a.MinX, ca * a.MaxX);
            double high = center + Math.Max(cb * b.MinX, cb * b.MaxX) - Math.Min(ca * a.MinX, ca * a.MaxX);
            return low > 0 ? low : high < 0 ? -high : 0;
        }

        private static double HorizontalSegmentDistance(Vector3 a, Vector3 ar, float ah, Vector3 b, Vector3 br, float bh)
        {
            double ax = a.x - ar.x * ah, az = a.z - ar.z * ah, adx = ar.x * ah * 2, adz = ar.z * ah * 2;
            double bx = b.x - br.x * bh, bz = b.z - br.z * bh, bdx = br.x * bh * 2, bdz = br.z * bh * 2;
            double determinant = adx * bdz - adz * bdx;
            if (Math.Abs(determinant) > 1e-12)
            {
                double x = bx - ax, z = bz - az;
                double u = (x * bdz - z * bdx) / determinant, v = (x * adz - z * adx) / determinant;
                if (u >= 0 && u <= 1 && v >= 0 && v <= 1) return 0;
            }
            return Math.Sqrt(Math.Min(Math.Min(PointSegmentSquared(ax, az, bx, bz, bdx, bdz),
                PointSegmentSquared(ax + adx, az + adz, bx, bz, bdx, bdz)),
                Math.Min(PointSegmentSquared(bx, bz, ax, az, adx, adz), PointSegmentSquared(bx + bdx, bz + bdz, ax, az, adx, adz))));
        }

        private static double PointSegmentSquared(double x, double z, double a, double b, double dx, double dz)
        {
            double squared = dx * dx + dz * dz;
            double t = squared > 1e-20 ? Math.Max(0, Math.Min(1, ((x - a) * dx + (z - b) * dz) / squared)) : 0;
            x -= a + dx * t; z -= b + dz * t;
            return x * x + z * z;
        }

        private static bool Round(MapElement e) => e.id == 0 || e.id == 1 || e.id == 4;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
