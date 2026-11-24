using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RadioArchipelago.World
{
    /// <summary>A glazed capsule support, bent in plan and held to one straight vertical grade.</summary>
    public sealed class IslandHoldSurface
    {
        private const int Layers = 6, CapSteps = 12;
        private readonly IslandRoute route;
        private readonly IslandStageDefinition definition;
        private readonly float start, end, lane, chartY, radius, startHeight, endHeight;
        private readonly int steps, contour;
        private readonly Vector3[] vertices;
        private readonly Vector2[] uv;
        private float progress;
        public Mesh Mesh { get; }
        public Vector3 Pivot { get; private set; }
        public Vector3 WorldCenter => Pivot + Mesh.bounds.center;
        public Vector3 LocalDrawnHeadCenter => vertices[contour * Layers + 2];
        public Vector3 LocalEndCenter => vertices[contour * Layers + 3];
        public float Start => start;
        public float DrawnStart => start;
        public float End => end;
        public float Radius => radius;
        public float ProgressChartZ => progress;

        public IslandHoldSurface(IslandRoute route, IslandStageDefinition definition,
            float start, float end, float lane, float chartY, float radius)
        {
            this.route = route; this.definition = definition; this.start = start;
            this.end = Mathf.Max(start + .05f, end); this.lane = lane * definition.LateralScale; this.chartY = chartY;
            this.radius = Mathf.Max(.15f, radius);
            steps = Mathf.Max(2, Mathf.CeilToInt((this.end - start) * definition.DistanceScale / .35f));
            contour = 2 * steps + 2 * CapSteps;
            vertices = new Vector3[contour * Layers + 4]; uv = new Vector2[vertices.Length];
            startHeight = route.Evaluate(definition.StartDistance + start * definition.DistanceScale).Position.y;
            endHeight = route.Evaluate(definition.StartDistance + this.end * definition.DistanceScale).Position.y;
            Mesh = new Mesh { name = "Island glazed rounded ride", indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            progress = start;
            WriteVertices(start);
            Mesh.vertices = vertices; Mesh.RecalculateBounds(); Pivot = Mesh.bounds.center;
            WriteVertices(start);
            Mesh.vertices = vertices; Mesh.uv = uv;
            var body = new List<int>(); var top = new List<int>(); var trim = new List<int>();
            for (int layer = 0; layer < Layers - 1; layer++)
                for (int i = 0; i < contour; i++)
                {
                    int n = (i + 1) % contour;
                    Quad(layer >= 3 ? trim : body, layer * contour + i, layer * contour + n,
                        (layer + 1) * contour + n, (layer + 1) * contour + i);
                }
            Face(top, (Layers - 1) * contour, contour * Layers + 2, contour * Layers + 3, false);
            Face(body, 0, contour * Layers, contour * Layers + 1, true);
            Mesh.subMeshCount = 3; Mesh.SetTriangles(body, 0); Mesh.SetTriangles(top, 1); Mesh.SetTriangles(trim, 2);
            Mesh.RecalculateNormals(); Mesh.RecalculateBounds();
        }

        public void Advance(float chartZ)
        {
            // The support is a whole ceramic object for its entire lifetime.
            // Only the native translucent band and radio cursor show progress.
            progress = Mathf.Clamp(chartZ, start, end);
        }

        private void WriteVertices(float begin)
        {
            float thickness = Mathf.Max(.05f, definition.TileThickness), bevel = Mathf.Min(.075f, thickness * .24f);
            for (int layer = 0; layer < Layers; layer++)
            {
                float scale = layer == 0 || layer == 5 ? .88f : layer == 1 || layer == 4 ? .965f : 1;
                float height = layer == 0 ? -thickness : layer == 1 ? -thickness + bevel / 3 :
                    layer == 2 ? -thickness + bevel : layer == 3 ? -bevel : layer == 4 ? -bevel / 3 : 0;
                float r = radius * scale;
                int index = layer * contour;
                for (int i = 0; i <= steps; i++) Put(index++, -r, Mathf.Lerp(begin, end, (float)i / steps), height);
                for (int i = 1; i <= CapSteps; i++)
                {
                    float angle = Mathf.PI - Mathf.PI * i / CapSteps;
                    Put(index++, Mathf.Cos(angle) * r, end + Mathf.Sin(angle) * r / definition.DistanceScale, height);
                }
                for (int i = steps - 1; i >= 0; i--) Put(index++, r, Mathf.Lerp(begin, end, (float)i / steps), height);
                for (int i = 1; i < CapSteps; i++)
                {
                    float angle = -Mathf.PI * i / CapSteps;
                    Put(index++, Mathf.Cos(angle) * r, begin + Mathf.Sin(angle) * r / definition.DistanceScale, height);
                }
            }
            Put(contour * Layers, 0, begin, -thickness); Put(contour * Layers + 1, 0, end, -thickness);
            Put(contour * Layers + 2, 0, begin, 0); Put(contour * Layers + 3, 0, end, 0);
        }

        private void Put(int index, float x, float z, float y)
        {
            var frame = route.Evaluate(definition.StartDistance + z * definition.DistanceScale);
            var center = frame.Position;
            center.y = Mathf.LerpUnclamped(startHeight, endHeight, (z - start) / (end - start));
            vertices[index] = center + frame.Right * (lane + x) + Vector3.up * (chartY + definition.TileElevation + y) - Pivot;
            uv[index] = new Vector2(x / (radius * 2) + .5f, (z - start) / Mathf.Max(.01f, end - start));
        }

        private void Face(List<int> triangles, int offset, int beginCenter, int endCenter, bool reverse)
        {
            for (int i = 0; i < steps; i++)
            {
                int left = offset + i, nextLeft = left + 1;
                int right = offset + 2 * steps + CapSteps - i, nextRight = right - 1;
                if (i == 0)
                {
                    Triangle(triangles, beginCenter, left, nextLeft, reverse);
                    Triangle(triangles, beginCenter, nextLeft, nextRight, reverse);
                    Triangle(triangles, beginCenter, nextRight, right, reverse);
                }
                else if (i == steps - 1)
                {
                    Triangle(triangles, endCenter, nextRight, right, reverse);
                    Triangle(triangles, endCenter, right, left, reverse);
                    Triangle(triangles, endCenter, left, nextLeft, reverse);
                }
                else if (reverse) Quad(triangles, left, right, nextRight, nextLeft);
                else Quad(triangles, left, nextLeft, nextRight, right);
            }
            for (int i = 0; i < CapSteps; i++)
            {
                Triangle(triangles, endCenter, offset + steps + i, offset + steps + i + 1, reverse);
                int a = offset + 2 * steps + CapSteps + i;
                int b = i == CapSteps - 1 ? offset : a + 1;
                Triangle(triangles, beginCenter, a, b, reverse);
            }
        }

        private static void Triangle(List<int> target, int a, int b, int c, bool reverse)
        { target.Add(a); target.Add(reverse ? c : b); target.Add(reverse ? b : c); }
        private static void Quad(List<int> target, int a, int b, int c, int d)
        { target.Add(a); target.Add(b); target.Add(c); target.Add(a); target.Add(c); target.Add(d); }
    }
}
