using DancingCats.Clone;
using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>Presentation-only island height for the authoritative native jump.</summary>
    public static class IslandFlightElevation
    {
        private const int ClearanceSamples = 32;
        private const float RidgeClearance = .06f;

        /// <summary>
        /// Returns the island contribution to the courier's world Y. Add the
        /// unchanged native chart Y and tile/surface offsets separately. While
        /// airborne, replace the route frame's ground Y with this value: following
        /// the ground under the courier would turn every hill into a second jump.
        /// The endpoint chord lands at the original chart distance; an optional
        /// lift shares the native arc's exact zero-height endpoints. No clocks,
        /// chart coordinates, physics state or managed allocations are involved.
        /// </summary>
        public static float Sample(IslandElevationProfile profile, float routeStartDistance,
            float worldDistanceScale, Vector3 jumpStart, float jumpDistance, float currentChartZ)
        {
            if (profile == null) return 0;
            if (jumpDistance <= .0001f)
                return profile.Evaluate((double)routeStartDistance + (double)currentChartZ * worldDistanceScale);

            double start = (double)routeStartDistance + (double)jumpStart.z * worldDistanceScale;
            double span = (double)jumpDistance * worldDistanceScale;
            float startHeight = profile.Evaluate(start);
            float endHeight = profile.Evaluate(start + span);
            float t = Mathf.Clamp01((currentChartZ - jumpStart.z) / jumpDistance);
            // Preserve exact contact heights, including after a missed landing.
            // Native JumpY remains free to fall beyond the end of its own arc.
            if (t <= 0) return startHeight;
            if (t >= 1) return endHeight;

            float nativeHeight = NativeRhythmRules.HeightForDistance(jumpDistance);
            float extraHeight = 0;
            for (int i = 1; i < ClearanceSamples; i++)
            {
                float sampleT = (float)i / ClearanceSamples;
                float shape = ArcShape(sampleT);
                float chord = Mathf.Lerp(startHeight, endHeight, sampleT);
                float ridge = profile.Evaluate(start + span * sampleT);
                float deficit = ridge - chord - nativeHeight * shape;
                // A safety margin is activated only by a real intersection.
                // Flat routes and ordinary up/down slopes retain the native arc.
                if (deficit > 0)
                    extraHeight = Mathf.Max(extraHeight, (deficit + RidgeClearance) / shape);
            }
            return Mathf.Lerp(startHeight, endHeight, t) + extraHeight * ArcShape(t);
        }

        private static float ArcShape(float progress) => NativeRhythmRules.JumpY(0, 1, progress);

        /// <summary>Extra visible flight height, with exactly unchanged contact times and endpoints.</summary>
        public static float Boost(float jumpDistance, float startZ, float currentZ, float heightScale)
        {
            if (jumpDistance <= .0001f || heightScale <= 1) return 0;
            float t = (currentZ - startZ) / jumpDistance;
            if (t <= 0 || t >= 1) return 0;
            return (heightScale - 1) * NativeRhythmRules.HeightForDistance(jumpDistance) * ArcShape(t);
        }
    }
}
