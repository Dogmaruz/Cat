using System;
using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>
    /// View-only departure from a repeated island route onto its final glade.
    /// Chart distances, native contacts, coin colliders and rewards stay native.
    /// </summary>
    public sealed class IslandGroundFinishPath
    {
        public const float CourierHoverHeight = .42f;
        public const float SparkGroundClearance = .50f;
        public const float MeadowExitYaw = -53f;
        private readonly IslandRoute route;
        private readonly float startDistance, distanceScale, tileElevation;
        private readonly float takeoffDistance, landingDistance;
        private readonly IslandRoute.Frame takeoff;
        private readonly Vector3 landingPosition;
        private readonly Vector3 exitHeading;
        private readonly float exitYaw;
        private readonly Vector3[] arcPositions;
        private const int ArcStations = 64;

        public float TakeoffChartZ { get; }
        public float LandingChartZ { get; }
        public float EndChartZ { get; }
        public float GroundHeight => landingPosition.y + tileElevation;
        public float CoinRunLength => (EndChartZ - LandingChartZ) * distanceScale;
        public IslandRoute.Frame LandingFrame => new IslandRoute.Frame(landingPosition, exitHeading);

        /// <summary>Unwrapped song distance keeps a repeated island's earlier laps free of the finale ground.</summary>
        public bool ShouldPresentGlade(float chartZ, float visibleAheadWorld) =>
            chartZ >= TakeoffChartZ - Mathf.Max(0, visibleAheadWorld) / distanceScale;

        public IslandGroundFinishPath(IslandRoute route, float startDistance, float distanceScale,
            float tileElevation, float takeoffChartZ, float landingChartZ, float endChartZ, float exitYaw = 0)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (!Finite(startDistance) || !Finite(distanceScale) || distanceScale <= 0 || !Finite(tileElevation) ||
                !Finite(takeoffChartZ) || !Finite(landingChartZ) || !Finite(endChartZ) || !Finite(exitYaw) ||
                landingChartZ <= takeoffChartZ || endChartZ <= landingChartZ)
                throw new ArgumentException("A ground finish requires finite increasing chart distances and a positive world scale.");
            this.route = route; this.startDistance = startDistance; this.distanceScale = distanceScale;
            this.tileElevation = tileElevation;
            TakeoffChartZ = takeoffChartZ; LandingChartZ = landingChartZ; EndChartZ = endChartZ;
            takeoffDistance = startDistance + takeoffChartZ * distanceScale;
            landingDistance = startDistance + landingChartZ * distanceScale;
            takeoff = route.Evaluate(takeoffDistance);
            this.exitYaw = exitYaw;
            exitHeading = Quaternion.Euler(0, exitYaw, 0) * takeoff.Tangent;
            landingPosition = takeoff.Position + exitHeading * (landingDistance - takeoffDistance);
            if (Mathf.Abs(exitYaw) > .0001f)
            {
                // Integrate a gradually turning unit heading. Unlike blending two
                // displaced paths, this cannot overshoot the exit yaw then turn back.
                arcPositions = new Vector3[ArcStations + 1];
                arcPositions[0] = takeoff.Position;
                float step = (landingDistance - takeoffDistance) / ArcStations;
                for (int i = 0; i < ArcStations; i++)
                {
                    float a = (float)i / ArcStations, b = (i + 1f) / ArcStations;
                    arcPositions[i + 1] = arcPositions[i] + step / 6 *
                        (ArcHeading(a) + 4 * ArcHeading((a + b) * .5f) + ArcHeading(b));
                }
                landingPosition = arcPositions[ArcStations];
            }
            // Retain the authored landing altitude, then make the reward run flat.
            landingPosition.y = route.Evaluate(landingDistance).Position.y;
        }

        /// <summary>
        /// Before the last jump this is the unchanged island. During that jump,
        /// a gradually turning arc joins it to the clear meadow exit without a
        /// camera-heading overshoot. The zero-yaw legacy exit retains its original
        /// quintic blend. The meadow continues straight at constant elevation.
        /// </summary>
        public IslandRoute.Frame Evaluate(float routeDistance)
        {
            if (!Finite(routeDistance)) throw new ArgumentOutOfRangeException(nameof(routeDistance));
            if (routeDistance <= takeoffDistance) return route.Evaluate(routeDistance);
            if (routeDistance >= landingDistance)
                return new IslandRoute.Frame(landingPosition + exitHeading * (routeDistance - landingDistance), exitHeading);
            float span = landingDistance - takeoffDistance;
            float t = (routeDistance - takeoffDistance) / span;
            if (arcPositions != null)
            {
                float station = t * ArcStations;
                int i = Mathf.Min(ArcStations - 1, Mathf.FloorToInt(station));
                float u = station - i, u2 = u * u, u3 = u2 * u, step = span / ArcStations;
                Vector3 arcPosition = (2 * u3 - 3 * u2 + 1) * arcPositions[i] +
                    (u3 - 2 * u2 + u) * step * ArcHeading((float)i / ArcStations) +
                    (-2 * u3 + 3 * u2) * arcPositions[i + 1] +
                    (u3 - u2) * step * ArcHeading((i + 1f) / ArcStations);
                // Match the route's entry grade and the flat meadow's exit grade.
                float y0 = takeoff.Position.y, y1 = landingPosition.y;
                arcPosition.y = (2 * t * t * t - 3 * t * t + 1) * y0 +
                    (t * t * t - 2 * t * t + t) * span * takeoff.Grade +
                    (-2 * t * t * t + 3 * t * t) * y1;
                float arcGrade = (6 * t * t - 6 * t) * y0 / span +
                    (3 * t * t - 4 * t + 1) * takeoff.Grade + (-6 * t * t + 6 * t) * y1 / span;
                return new IslandRoute.Frame(arcPosition, ArcHeading(t), arcGrade);
            }
            float weight = t * t * t * (10 + t * (-15 + 6 * t));
            float derivative = 30 * t * t * (1 - t) * (1 - t) / span;
            var original = route.Evaluate(routeDistance);
            Vector3 line = takeoff.Position + exitHeading * (routeDistance - takeoffDistance);
            line.y = landingPosition.y;
            Vector3 position = Vector3.LerpUnclamped(original.Position, line, weight);
            Vector3 heading = Vector3.LerpUnclamped(original.Tangent, exitHeading, weight) +
                              (line - original.Position) * derivative;
            heading.y = 0;
            if (heading.sqrMagnitude < .000001f) heading = exitHeading;
            float grade = original.Grade * (1 - weight) + (line.y - original.Position.y) * derivative;
            return new IslandRoute.Frame(position, heading.normalized, grade);
        }

        private Vector3 ArcHeading(float fraction)
        {
            float t = Mathf.Clamp01(fraction);
            float eased = t * t * t * (10 + t * (-15 + 6 * t));
            return Quaternion.Euler(0, exitYaw * eased, 0) * takeoff.Tangent;
        }

        public float SurfaceY(float chartZ) => Evaluate(startDistance + chartZ * distanceScale).Position.y + tileElevation;

        /// <summary>Presentation-only clearance, joined continuously over the last genuine jump.</summary>
        public float CourierHoverOffset(float chartZ)
        {
            float t = Mathf.Clamp01((chartZ - TakeoffChartZ) / (LandingChartZ - TakeoffChartZ));
            return CourierHoverHeight * t * t * t * (10 + t * (-15 + 6 * t));
        }

        public bool IsFinalFlight(Vector3 jumpStart, float jumpDistance) =>
            jumpStart.z >= TakeoffChartZ - .001f && jumpStart.z < LandingChartZ &&
            jumpStart.z + jumpDistance >= LandingChartZ - .001f;

        /// <summary>The final airborne feet follow the contact-height chord, not the terrain underneath.</summary>
        public float FlightSurfaceY(Vector3 jumpStart, float jumpDistance, float chartZ)
        {
            float t = Mathf.Clamp01((chartZ - jumpStart.z) / Mathf.Max(.0001f, jumpDistance));
            return Mathf.Lerp(SurfaceY(jumpStart.z), SurfaceY(jumpStart.z + jumpDistance), t) + CourierHoverOffset(chartZ);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
