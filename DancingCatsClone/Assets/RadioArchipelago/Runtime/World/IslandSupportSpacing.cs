using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>Visual footprint policy. Never changes native timing or colliders.</summary>
    public static class IslandSupportSpacing
    {
        public const float NativeJumpRadius = .65f;
        public const float MinimumGap = .08f;

        /// <summary>Two equally capped circles cannot intersect across this gap.</summary>
        public static float LimitRadius(float preferred, float centerDistance)
            => Mathf.Min(preferred, Mathf.Max(0, (centerDistance - MinimumGap) * .5f));

        public static void ResolveRadii(Vector3[] centers, float[] chartDistances, float maximumChartSeparation, float[] preferred, float[] output)
        {
            if (centers == null || chartDistances == null || preferred == null || output == null || centers.Length != preferred.Length || output.Length != centers.Length || chartDistances.Length != centers.Length)
                throw new System.ArgumentException("Disc centers and radii must have equal lengths.");
            for (int i = 0; i < output.Length; i++) output[i] = preferred[i];
            for (int i = 0; i < centers.Length; i++)
                for (int j = i + 1; j < centers.Length; j++)
                {
                    // A closed island is reused by later musical laps. Those
                    // surfaces never coexist in the unwrapped visibility window.
                    if (Mathf.Abs(chartDistances[i] - chartDistances[j]) > maximumChartSeparation) continue;
                    float distance = Vector3.Distance(centers[i], centers[j]);
                    if (distance >= preferred[i] + preferred[j] + MinimumGap) continue;
                    output[i] = Mathf.Min(output[i], LimitRadius(preferred[i], distance));
                    output[j] = Mathf.Min(output[j], LimitRadius(preferred[j], distance));
                }
        }
    }
}
