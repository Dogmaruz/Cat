using System;
using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>Fixed orbit about route ground and the supplied lateral anchor; only yaw is damped.</summary>
    public sealed class IslandCloseCameraRig
    {
        public readonly struct Pose
        {
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Anchor { get; }
            public Quaternion Heading { get; }

            internal Pose(Vector3 position, Quaternion rotation, Vector3 anchor, Quaternion heading)
            { Position = position; Rotation = rotation; Anchor = anchor; Heading = heading; }
        }

        private bool initialized;
        private Quaternion heading;

        public void Reset() => initialized = false;

        /// <summary>
        /// No camera, parent transform, courier animation/bounds, native jump or
        /// FOV is an input. Back/height stay exact while terrain changes beneath
        /// the travel anchor. Look height is relative to that same anchor, never
        /// a future terrain sample that could tilt the feet out of view uphill.
        /// Lateral is the caller's world-space anchor offset, including its
        /// chosen lateral-follow fraction; actor width never changes the orbit.
        /// </summary>
        public Pose Step(IslandRoute route, float distance, float lateral, float back, float height,
            float lookAhead, float focusHeight, float headingLead, float smoothing, float deltaTime, bool immediate)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            return Step(route.Evaluate(distance), route.Evaluate(distance + headingLead), lateral, back, height,
                lookAhead, focusHeight, smoothing, deltaTime, immediate);
        }

        /// <summary>Uses the same fixed orbit with an explicitly sampled finale path.</summary>
        public Pose Step(IslandRoute.Frame frame, IslandRoute.Frame leadFrame, float lateral, float back, float height,
            float lookAhead, float focusHeight, float smoothing, float deltaTime, bool immediate)
        {
            Vector3 forward = leadFrame.Tangent;
            forward.y = 0;
            Quaternion desired = Quaternion.LookRotation(forward, Vector3.up);
            if (!initialized || immediate)
            {
                heading = desired;
                initialized = true;
            }
            else
            {
                float blend = 1 - Mathf.Exp(-Mathf.Max(0, smoothing) * Mathf.Max(0, deltaTime));
                heading = Quaternion.Slerp(heading, desired, blend);
            }
            Vector3 anchor = frame.Position + frame.Right * lateral;
            forward = heading * Vector3.forward;
            Vector3 position = anchor - forward * back + Vector3.up * height;
            Vector3 look = anchor + forward * lookAhead + Vector3.up * focusHeight;
            return new Pose(position, Quaternion.LookRotation(look - position, Vector3.up), anchor, heading);
        }
    }
}
