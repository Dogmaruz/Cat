using UnityEngine;

namespace DancingCats.Clone
{
    /// <summary>Scalar rules recovered from the original 0.5.8 ARM64 build and authored prefab values.</summary>
    public static class NativeRhythmRules
    {
        public const float GroundOffset = .25f;
        public const float DragWorldRange = 6.6f;
        public const float HorizontalLimit = 1.5f;
        public const float HorizontalLerp = .25f;
        private const float StartTangent = .770636796951294f;
        private const float EndTangent = 1.4819953441619873f;

        // Ball.Jumping applies the authored AnimationCurve only up to the end of the arc.
        // Progress beyond one keeps falling; clamping it would keep a missed cat suspended.
        public static float WarpPercent(float percent)
        {
            float p = Mathf.Max(0f, percent);
            if (p > 1f) return p;
            return ((StartTangent + EndTangent - 2f) * p + (3f - 2f * StartTangent - EndTangent)) * p * p + StartTangent * p;
        }

        public static float HeightForDistance(float distance) => Mathf.Min(distance * .4f, 2f);

        public static float JumpY(float beginY, float height, float percent)
        {
            float p = WarpPercent(percent);
            return beginY + 4f * height * p * (1f - p);
        }

        public static bool IsPerfect(float error, bool slider, bool followEnd)
            => followEnd || Mathf.Abs(error) <= (slider ? .2f : .25f);

        public static float MoveCycleX(float initialX, float distanceAhead, bool moveLeft)
            => Mathf.Sin(2f * Mathf.PI * (initialX / 5f + (moveLeft ? -distanceAhead : distanceAhead) / 30f));

        public static float MovePercent(float distanceAhead, float begin, float end)
        {
            if (distanceAhead > begin) return 0f;
            if (distanceAhead < end) return 1f;
            return (begin - distanceAhead) / (begin - end);
        }
    }

    public sealed class OriginalScore
    {
        public int Value { get; private set; }
        public int Streak { get; private set; }
        public bool EnhancedPerfect => Streak > 4;

        public int Apply(bool perfect)
        {
            Streak = perfect ? Streak + 1 : 0;
            int awarded = perfect ? Streak : 1;
            Value += awarded;
            return awarded;
        }

        public void BreakStreak() => Streak = 0;
        public void Reset() { Value = 0; Streak = 0; }
    }
}
