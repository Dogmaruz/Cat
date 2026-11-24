using System;
using System.Collections.Generic;
using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>
    /// Immutable arc-length sampler for an exported closed Unity XZ centreline.
    /// XZ stays exactly on its authored polyline; optional height never changes
    /// musical distance. Heading and input remain yaw-only on elevated terrain.
    /// One distance/lateral/height unit is one world metre. No scene or clock state.
    /// </summary>
    public sealed class IslandRoute
    {
        private const double DuplicateDistance = .00001;
        private readonly Vector3[] _points, _directions, _tangents;
        private readonly double[] _distances;
        private readonly double _length;
        private readonly IslandElevationProfile _elevation;

        public float Length => (float)_length;

        /// <summary>Use the unrounded perimeter when constructing very large repeated-lap distances.</summary>
        public double ExactLength => _length;

        public readonly struct Frame
        {
            public Vector3 Position { get; }
            public Vector3 Tangent { get; }
            public Vector3 Right { get; }
            public Quaternion Rotation { get; }
            public float Grade { get; }
            public Vector3 SurfaceTangent { get; }
            public Vector3 SurfaceUp { get; }

            internal Frame(Vector3 position, Vector3 tangent, float grade = 0)
            {
                Position = position;
                Tangent = tangent;
                Right = new Vector3(tangent.z, 0, -tangent.x); // Cross(up, forward).
                double halfYaw = Math.Atan2(tangent.x, tangent.z) * .5;
                Rotation = new Quaternion(0, (float)Math.Sin(halfYaw), 0, (float)Math.Cos(halfYaw));
                Grade = grade;
                double normalizer = 1 / Math.Sqrt(1 + (double)grade * grade);
                SurfaceTangent = new Vector3((float)(tangent.x * normalizer), (float)(grade * normalizer),
                    (float)(tangent.z * normalizer));
                SurfaceUp = new Vector3((float)(-tangent.x * grade * normalizer), (float)normalizer,
                    (float)(-tangent.z * grade * normalizer));
            }
        }

        /// <summary>
        /// Copies finite planar points, discarding consecutive and repeated closing
        /// endpoints within 0.01 mm. Closing the final edge is implicit. Exact
        /// reversals are invalid because they have no continuous forward frame.
        /// </summary>
        public IslandRoute(IReadOnlyList<Vector3> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            var clean = new List<Vector3>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z))
                    throw new ArgumentException("Route points must contain only finite coordinates.", nameof(points));
                if (Math.Abs(point.y) > .0001f)
                    throw new ArgumentException("The island route must lie on Unity Y=0; chart Y supplies jump height.", nameof(points));
                point.y = 0;
                if (clean.Count == 0 || Distance(clean[clean.Count - 1], point) > DuplicateDistance)
                    clean.Add(point);
            }
            if (clean.Count > 1 && Distance(clean[clean.Count - 1], clean[0]) <= DuplicateDistance)
                clean.RemoveAt(clean.Count - 1);
            if (clean.Count < 3)
                throw new ArgumentException("A closed route needs at least three distinct points.", nameof(points));

            _points = clean.ToArray();
            int count = _points.Length;
            _distances = new double[count + 1];
            _directions = new Vector3[count];
            _tangents = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 a = _points[i], b = _points[(i + 1) % count];
                double distance = Distance(a, b);
                _distances[i + 1] = _distances[i] + distance;
                _directions[i] = new Vector3((float)(((double)b.x - a.x) / distance), 0,
                    (float)(((double)b.z - a.z) / distance));
            }
            _length = _distances[count];
            if (double.IsInfinity(_length) || _length > float.MaxValue)
                throw new ArgumentException("The route perimeter exceeds finite Unity world distance.", nameof(points));
            for (int i = 0; i < count; i++)
            {
                Vector3 sum = _directions[(i + count - 1) % count] + _directions[i];
                if ((double)sum.x * sum.x + (double)sum.z * sum.z <= 1e-10)
                    throw new ArgumentException("The route cannot reverse direction exactly at an authored point.", nameof(points));
                _tangents[i] = Normalize(sum);
            }
        }

        /// <summary>
        /// Keep the same planar perimeter and input frame, adding only surface Y.
        /// Profile length must agree within 1 cm with the exported XZ perimeter.
        /// Null retains the original flat behaviour.
        /// </summary>
        public IslandRoute(IReadOnlyList<Vector3> points, IslandElevationProfile elevation) : this(points)
        {
            if (elevation != null && (!elevation.Closed || Math.Abs(elevation.Length - _length) > .01))
                throw new ArgumentException("Route elevation must be closed and match the planar perimeter within 1 cm.", nameof(elevation));
            _elevation = elevation;
        }

        public Frame Evaluate(float distance) => Evaluate((double)distance);

        /// <summary>
        /// Wrap any finite signed distance, including repeated laps. Position uses
        /// linear arc-length interpolation; averaged vertex headings interpolate
        /// continuously over each span and across the seam. Allocates no memory.
        /// </summary>
        public Frame Evaluate(double distance)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance))
                throw new ArgumentOutOfRangeException(nameof(distance), "Route distance must be finite.");
            double wrapped = distance % _length;
            if (wrapped < 0) wrapped += _length;
            if (wrapped >= _length) wrapped = 0; // Rounding when adding a tiny negative remainder.
            int low = 0, high = _points.Length - 1;
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (_distances[middle] <= wrapped) low = middle;
                else high = middle - 1;
            }
            int next = (low + 1) % _points.Length;
            double t = (wrapped - _distances[low]) / (_distances[low + 1] - _distances[low]);
            Vector3 a = _points[low], b = _points[next];
            var position = new Vector3((float)(a.x + ((double)b.x - a.x) * t), 0,
                (float)(a.z + ((double)b.z - a.z) * t));
            a = _tangents[low]; b = _tangents[next];
            var tangent = new Vector3((float)(a.x + ((double)b.x - a.x) * t), 0,
                (float)(a.z + ((double)b.z - a.z) * t));
            tangent = tangent.sqrMagnitude > 1e-12f ? Normalize(tangent) : _directions[low];
            float grade = 0;
            if (_elevation != null)
                _elevation.Sample(wrapped, out position.y, out grade);
            return new Frame(position, tangent, grade);
        }

        /// <summary>
        /// Convert logical (lateral X, height Y, distance Z) into world space.
        /// Apply the presentation's start offset here OR before calling Evaluate,
        /// once. Lateral input is never clamped and chart/DSP coordinates are not
        /// mutated. Any optional presentation distance scale belongs to its caller.
        /// </summary>
        public Vector3 Map(Vector3 chartPosition, float startDistance = 0)
        {
            if (!Finite(chartPosition.x) || !Finite(chartPosition.y) || !Finite(chartPosition.z))
                throw new ArgumentException("Chart coordinates must be finite.", nameof(chartPosition));
            if (!Finite(startDistance))
                throw new ArgumentException("Start distance must be finite.", nameof(startDistance));
            Frame frame = Evaluate((double)chartPosition.z + startDistance);
            return frame.Position + frame.Right * chartPosition.x + Vector3.up * chartPosition.y;
        }

        private static Vector3 Normalize(Vector3 vector)
        {
            double length = Math.Sqrt((double)vector.x * vector.x + (double)vector.z * vector.z);
            return new Vector3((float)(vector.x / length), 0, (float)(vector.z / length));
        }

        private static double Distance(Vector3 a, Vector3 b)
        {
            double x = (double)b.x - a.x, z = (double)b.z - a.z;
            return Math.Sqrt(x * x + z * z);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
