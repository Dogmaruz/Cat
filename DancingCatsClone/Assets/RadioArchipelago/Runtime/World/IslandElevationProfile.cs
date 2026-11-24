using System;
using System.Collections.Generic;
using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>
    /// Immutable height over planar musical arc distance. Shape-preserving quintic
    /// Hermite spans share their first derivatives and have zero key curvature,
    /// giving C2 joins without stopping at intermediate samples on a hillside.
    /// </summary>
    public sealed class IslandElevationProfile
    {
        [Serializable] public struct Key
        {
            public float distance, height;

            public Key(float distance, float height)
            {
                this.distance = distance;
                this.height = height;
            }
        }

        [Serializable] public sealed class Document
        {
            public float length;
            public bool closed;
            public Key[] keys;
        }

        private readonly double[] _distances, _heights, _slopes;

        public float Length { get; }
        public bool Closed { get; }

        /// <summary>
        /// Keys must be finite, strictly increasing and include 0 and length.
        /// Closed profiles require equal endpoint heights. Caller data is copied.
        /// Open profiles clamp at their endpoints with zero endpoint grade.
        /// </summary>
        public IslandElevationProfile(float length, bool closed, IReadOnlyList<Key> keys)
        {
            if (!Finite(length) || length <= 0)
                throw new ArgumentException("Elevation length must be positive and finite.", nameof(length));
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count < 2)
                throw new ArgumentException("Elevation needs at least two endpoint keys.", nameof(keys));
            Length = length;
            Closed = closed;
            int count = keys.Count;
            _distances = new double[count];
            _heights = new double[count];
            _slopes = new double[count];
            for (int i = 0; i < count; i++)
            {
                Key key = keys[i];
                if (!Finite(key.distance) || !Finite(key.height) || key.distance < 0 || key.distance > length ||
                    (i > 0 && key.distance <= _distances[i - 1]))
                    throw new ArgumentException("Elevation keys must be finite and strictly increasing within its length.", nameof(keys));
                _distances[i] = key.distance;
                _heights[i] = key.height;
            }
            if (_distances[0] != 0 || _distances[count - 1] != length)
                throw new ArgumentException("Elevation keys must include distance 0 and its declared length.", nameof(keys));
            if (closed && _heights[0] != _heights[count - 1])
                throw new ArgumentException("Closed elevation endpoint heights must match exactly.", nameof(keys));

            var spans = new double[count - 1];
            var secants = new double[count - 1];
            for (int i = 0; i < count - 1; i++)
            {
                spans[i] = _distances[i + 1] - _distances[i];
                secants[i] = (_heights[i + 1] - _heights[i]) / spans[i];
            }
            for (int i = 1; i < count - 1; i++)
                _slopes[i] = SharedSlope(spans[i - 1], spans[i], secants[i - 1], secants[i]);
            if (closed)
                _slopes[0] = _slopes[count - 1] = SharedSlope(spans[count - 2], spans[0], secants[count - 2], secants[0]);

            // Nonnegative derivative Bernstein coefficients guarantee monotonic
            // height between each pair of keys. Limiting only reduces shared
            // slopes, so processing later spans cannot invalidate an earlier one.
            for (int i = 0; i < count - 1; i++)
            {
                double secant = secants[i];
                if (secant == 0)
                {
                    SetSlope(i, 0);
                    SetSlope(i + 1, 0);
                }
                else
                {
                    double total = (_slopes[i] + _slopes[i + 1]) / secant;
                    if (total > 2.5)
                    {
                        double scale = 2.5 / total;
                        SetSlope(i, _slopes[i] * scale);
                        SetSlope(i + 1, _slopes[i + 1] * scale);
                    }
                }
            }
            for (int i = 0; i < count - 1; i++)
            {
                double middleGrade = 5 * secants[i] - 2 * _slopes[i] - 2 * _slopes[i + 1];
                if (Math.Abs(_slopes[i]) > float.MaxValue || Math.Abs(_slopes[i + 1]) > float.MaxValue ||
                    Math.Abs(middleGrade) > float.MaxValue)
                    throw new ArgumentException("Elevation grade exceeds finite Unity world coordinates.", nameof(keys));
            }
        }

        public static IslandElevationProfile FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Elevation JSON must not be empty.", nameof(json));
            Document document = JsonUtility.FromJson<Document>(json);
            if (document == null)
                throw new ArgumentException("Elevation JSON must contain a profile document.", nameof(json));
            return new IslandElevationProfile(document.length, document.closed, document.keys);
        }

        public float Evaluate(float distance) => Evaluate((double)distance);
        public float Evaluate(double distance)
        {
            Sample(distance, out float height, out _);
            return height;
        }

        /// <summary>Analytic dh/ds in world metres per planar musical metre.</summary>
        public float Grade(float distance) => Grade((double)distance);
        public float Grade(double distance)
        {
            Sample(distance, out _, out float grade);
            return grade;
        }

        internal void Sample(double distance, out float height, out float grade)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance))
                throw new ArgumentOutOfRangeException(nameof(distance), "Elevation distance must be finite.");
            double wrapped = distance;
            if (Closed)
            {
                wrapped %= Length;
                if (wrapped < 0) wrapped += Length;
                if (wrapped >= Length) wrapped = 0;
            }
            else if (wrapped <= 0 || wrapped >= Length)
            {
                height = (float)_heights[wrapped <= 0 ? 0 : _heights.Length - 1];
                grade = 0;
                return;
            }

            int low = 0, high = _distances.Length - 2;
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (_distances[middle] <= wrapped) low = middle;
                else high = middle - 1;
            }
            double span = _distances[low + 1] - _distances[low];
            double t = (wrapped - _distances[low]) / span;
            double a = _heights[low], b = _heights[low + 1];
            double secant = (b - a) / span, m0 = _slopes[low], m1 = _slopes[low + 1];
            double c3 = 10 * secant - 6 * m0 - 4 * m1;
            double c4 = -15 * secant + 8 * m0 + 7 * m1;
            double c5 = 6 * secant - 3 * m0 - 3 * m1;
            double sampledHeight = a + span * t * (m0 + t * t * (c3 + t * (c4 + t * c5)));
            // Guard only floating point cancellation; the limited polynomial is monotone.
            height = (float)Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), sampledHeight));
            grade = (float)(m0 + t * t * (3 * c3 + t * (4 * c4 + t * 5 * c5)));
        }

        private void SetSlope(int index, double value)
        {
            _slopes[index] = value;
            if (Closed && (index == 0 || index == _slopes.Length - 1))
                _slopes[0] = _slopes[_slopes.Length - 1] = value;
        }

        private static double SharedSlope(double previousSpan, double nextSpan, double previous, double next)
        {
            if (previous == 0 || next == 0 || (previous > 0) != (next > 0)) return 0;
            double w1 = 2 * nextSpan + previousSpan, w2 = nextSpan + 2 * previousSpan;
            return (w1 + w2) / (w1 / previous + w2 / next);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
