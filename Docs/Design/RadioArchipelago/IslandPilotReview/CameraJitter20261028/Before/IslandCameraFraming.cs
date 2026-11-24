using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>
    /// Fits a small gameplay envelope into a perspective camera's safe viewport.
    /// Keeps the chosen heading/FOV; only pans and, when necessary, retreats.
    /// Operates after damping so a slow frame cannot postpone a safety correction.
    /// </summary>
    public static class IslandCameraFraming
    {
        public static void Fit(Camera camera, Vector3[] points, int count, Rect safeViewport)
        {
            if (camera == null || count == 0) return;
            var rotation = camera.transform.rotation;
            var inverse = Quaternion.Inverse(rotation);
            var origin = camera.transform.position;
            float tanY = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            float tanX = tanY * camera.aspect;
            float lowX = (safeViewport.xMin * 2 - 1) * tanX;
            float highX = (safeViewport.xMax * 2 - 1) * tanX;
            float lowY = (safeViewport.yMin * 2 - 1) * tanY;
            float highY = (safeViewport.yMax * 2 - 1) * tanY;
            // Each point supplies an interval for the camera's right/up pan.
            // Their intersections widen monotonically as the camera retreats.
            float retreat = 0;
            for (int i = 0; i < count; i++)
                retreat = Mathf.Max(retreat, camera.nearClipPlane + .1f - (inverse * (points[i] - origin)).z);
            if (!Intervals(points, count, origin, inverse, retreat, lowX, highX, lowY, highY, out _, out _))
            {
                float lower = retreat, upper = Mathf.Max(1, retreat);
                while (!Intervals(points, count, origin, inverse, upper, lowX, highX, lowY, highY, out _, out _))
                    upper *= 2;
                for (int iteration = 0; iteration < 14; iteration++)
                {
                    float middle = (lower + upper) * .5f;
                    if (Intervals(points, count, origin, inverse, middle, lowX, highX, lowY, highY, out _, out _)) upper = middle;
                    else lower = middle;
                }
                retreat = upper + .001f;
            }
            Intervals(points, count, origin, inverse, retreat, lowX, highX, lowY, highY, out Vector2 x, out Vector2 y);
            camera.transform.position = origin + rotation * new Vector3(Mathf.Clamp(0, x.x, x.y), Mathf.Clamp(0, y.x, y.y), -retreat);
        }

        private static bool Intervals(Vector3[] points, int count, Vector3 origin, Quaternion inverse, float retreat,
            float lowX, float highX, float lowY, float highY, out Vector2 x, out Vector2 y)
        {
            x = y = new Vector2(float.NegativeInfinity, float.PositiveInfinity);
            for (int i = 0; i < count; i++)
            {
                Vector3 point = inverse * (points[i] - origin);
                float depth = point.z + retreat;
                x.x = Mathf.Max(x.x, point.x - highX * depth);
                x.y = Mathf.Min(x.y, point.x - lowX * depth);
                y.x = Mathf.Max(y.x, point.y - highY * depth);
                y.y = Mathf.Min(y.y, point.y - lowY * depth);
            }
            return x.x <= x.y && y.x <= y.y;
        }
    }
}
