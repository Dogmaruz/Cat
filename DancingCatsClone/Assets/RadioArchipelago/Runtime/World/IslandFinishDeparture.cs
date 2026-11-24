using System;
using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>View-only final leap after the native final trigger, inside its existing results delay.</summary>
    public sealed class IslandFinishDeparture
    {
        public const float Duration = 1.55f;
        private Vector3 origin, forward;
        private Quaternion heading = Quaternion.identity;
        private float elapsed;

        public bool Started { get; private set; }
        public bool Complete => Started && elapsed >= Duration;
        public int StartCount { get; private set; }
        public int CollectedCoinsAtStart { get; private set; }
        public float Progress => Started ? Mathf.Clamp01(elapsed / Duration) : 0;
        public Vector3 StartPosition => origin;
        public Quaternion Rotation => heading;
        public Vector3 Position => origin + Offset;
        public Vector3 Offset
        {
            get
            {
                float t = Progress;
                // A brisk upward release becomes a slower climb while forward
                // acceleration carries the courier away from the fixed camera.
                // Clear the forest canopy before accelerating into the distance.
                // The longer forward travel keeps the high arc framed by the fixed camera.
                float rise = 15 * (1 - Mathf.Pow(1 - t, 3)) + 3 * t * t;
                return forward * (100 * t * t * (2 - t)) + Vector3.up * rise;
            }
        }
        public float VisualScale => Mathf.Lerp(1, .10f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.4f, 1, Progress)));

        /// <summary>The game checks real spark collections before raising its native final trigger.</summary>
        public bool TryBegin(bool nativeFinishReached, int collectedCoins, Vector3 start, Quaternion rotation)
        {
            if (!nativeFinishReached || Started) return false;
            if (!Finite(start.x) || !Finite(start.y) || !Finite(start.z) || collectedCoins < 0)
                throw new ArgumentException("Departure needs a finite final presentation position and native collection count.");
            Vector3 direction = rotation * Vector3.forward;
            direction.y = 0;
            if (!Finite(direction.x) || !Finite(direction.z) || direction.sqrMagnitude < .00001f)
                throw new ArgumentException("Departure needs a finite horizontal finish heading.");
            forward = direction.normalized;
            origin = start;
            heading = Quaternion.LookRotation(forward, Vector3.up);
            elapsed = 0;
            Started = true;
            StartCount++;
            CollectedCoinsAtStart = collectedCoins;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!Started || Complete || !Finite(deltaTime) || deltaTime <= 0) return;
            elapsed = Mathf.Min(Duration, elapsed + deltaTime);
        }

        public void Reset()
        {
            Started = false; elapsed = 0; StartCount = 0; CollectedCoinsAtStart = 0;
            origin = forward = Vector3.zero; heading = Quaternion.identity;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
