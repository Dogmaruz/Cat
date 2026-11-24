using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RadioArchipelago.World
{
    /// <summary>
    /// Own radio guide for a styled Hold: fine white wire, two signal arcs,
    /// brass ring and a small mint receiver. Mesh slots are white/brass/mint.
    /// The caller owns the mesh and parents its renderer to the Hold's fixed
    /// pivot, so the body's spring, scale and lifetime are shared exactly.
    /// </summary>
    public sealed class IslandRadioHoldTracer
    {
        private const float LineHalfWidth = .0125f, Lift = .025f;
        private readonly IslandRoute route;
        private readonly IslandStageDefinition definition;
        private readonly float start, end, lane, chartY, startHeight, endHeight, grade, glyphScale;
        private readonly Vector3 pivot;
        private readonly int steps, lineVertices, glyphAnchorIndex;
        private readonly Vector3[] vertices, normals, glyphPositions, glyphNormals;
        private readonly Vector2[] uv;

        public Mesh Mesh { get; }
        public float DrawnStart => start;
        public float GlyphChartZ { get; private set; }
        public Vector3 LocalLineStartCenter => (vertices[0] + vertices[1]) * .5f;
        public Vector3 LocalLineEndCenter => (vertices[steps * 2] + vertices[steps * 2 + 1]) * .5f;
        public Vector3 LocalGlyphCenter => vertices[glyphAnchorIndex];

        public IslandRadioHoldTracer(IslandRoute route, IslandStageDefinition definition,
            float start, float end, float lane, float chartY, Vector3 fixedPivot, float halfWidth = .65f)
        {
            this.route = route ?? throw new ArgumentNullException(nameof(route));
            this.definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            this.start = start; this.end = Mathf.Max(start + .05f, end);
            this.lane = lane * definition.LateralScale; this.chartY = chartY; pivot = fixedPivot;
            glyphScale = Mathf.Min(1, Mathf.Max(.15f, halfWidth) / .38f);
            float distanceScale = Mathf.Max(.01f, definition.DistanceScale);
            startHeight = route.Evaluate(definition.StartDistance + start * distanceScale).Position.y;
            endHeight = route.Evaluate(definition.StartDistance + this.end * distanceScale).Position.y;
            grade = (endHeight - startHeight) / ((this.end - start) * distanceScale);
            steps = Mathf.Max(2, Mathf.CeilToInt((this.end - start) * distanceScale / .35f));
            lineVertices = (steps + 1) * 2;

            var glyph = new GlyphBuilder(lineVertices);
            glyph.Torus(1, .143f, .014f, .039f, 0, Mathf.PI * 2, 32, false);
            glyph.Torus(0, .220f, .011f, .038f, -70 * Mathf.Deg2Rad, 70 * Mathf.Deg2Rad, 20, true);
            glyph.Torus(0, .290f, .011f, .038f, -70 * Mathf.Deg2Rad, 70 * Mathf.Deg2Rad, 24, true);
            glyph.Receiver();
            glyphAnchorIndex = glyph.receiverAnchorIndex;
            glyphPositions = glyph.positions.ToArray(); glyphNormals = glyph.normals.ToArray();
            vertices = new Vector3[lineVertices + glyphPositions.Length]; normals = new Vector3[vertices.Length];
            uv = new Vector2[vertices.Length];
            for (int i = 0; i <= steps; i++)
            {
                uv[i * 2] = new Vector2(0, (float)i / steps);
                uv[i * 2 + 1] = new Vector2(1, (float)i / steps);
            }
            for (int i = 0; i < glyphPositions.Length; i++)
                uv[lineVertices + i] = new Vector2(glyphPositions[i].x / .65f + .5f, glyphPositions[i].z / .65f + .5f);
            var white = new List<int>(steps * 6 + glyph.indices[0].Count);
            for (int i = 0; i < steps; i++)
            {
                int left = i * 2;
                Quad(white, left, left + 2, left + 3, left + 1);
            }
            white.AddRange(glyph.indices[0]);
            Mesh = new Mesh { name = "Island radio Hold wire and receiver",
                indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            Mesh.MarkDynamic();
            GlyphChartZ = start; WriteVertices(start);
            Mesh.vertices = vertices; Mesh.normals = normals; Mesh.uv = uv;
            Mesh.subMeshCount = 3;
            Mesh.SetTriangles(white, 0); Mesh.SetTriangles(glyph.indices[1], 1); Mesh.SetTriangles(glyph.indices[2], 2);
            Mesh.RecalculateBounds();
        }

        /// <summary>The cursor is the exact endpoint of the progress wire. No arrays or mesh are created here.</summary>
        public void Advance(float chartZ)
        {
            float progress = Mathf.Clamp(chartZ, start, end);
            if (Mathf.Abs(progress - GlyphChartZ) < .0001f) return;
            WriteVertices(progress);
            Mesh.vertices = vertices; Mesh.normals = normals; Mesh.RecalculateBounds();
        }

        private void WriteVertices(float progress)
        {
            float distanceScale = Mathf.Max(.01f, definition.DistanceScale);
            GlyphChartZ = progress;
            for (int i = 0; i <= steps; i++)
            {
                float z = Mathf.Lerp(start, progress, (float)i / steps);
                Put(i * 2, -LineHalfWidth, z, Lift, Vector3.up);
                Put(i * 2 + 1, LineHalfWidth, z, Lift, Vector3.up);
            }
            for (int i = 0; i < glyphPositions.Length; i++)
            {
                Vector3 local = glyphPositions[i];
                Put(lineVertices + i, local.x * glyphScale, GlyphChartZ + local.z * glyphScale / distanceScale,
                    Lift + (local.y - Lift) * glyphScale, glyphNormals[i]);
            }
        }

        private void Put(int index, float x, float z, float y, Vector3 localNormal)
        {
            var frame = route.Evaluate(definition.StartDistance + z * definition.DistanceScale);
            Vector3 center = frame.Position;
            // One straight vertical grade, exactly like IslandHoldSurface.
            center.y = Mathf.LerpUnclamped(startHeight, endHeight, (z - start) / (end - start));
            vertices[index] = center + frame.Right * (lane + x) + Vector3.up * (chartY + definition.TileElevation + y) - pivot;
            normals[index] = (frame.Right * localNormal.x + Vector3.up * localNormal.y +
                frame.Tangent * (localNormal.z - grade * localNormal.y)).normalized;
        }

        private static void Quad(List<int> triangles, int a, int b, int c, int d)
        { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(a); triangles.Add(c); triangles.Add(d); }

        private sealed class GlyphBuilder
        {
            internal readonly List<Vector3> positions = new List<Vector3>();
            internal readonly List<Vector3> normals = new List<Vector3>();
            internal readonly List<int>[] indices = { new List<int>(), new List<int>(), new List<int>() };
            internal int receiverAnchorIndex;
            private readonly int offset;
            internal GlyphBuilder(int offset) { this.offset = offset; }
            private int Vertex(Vector3 position, Vector3 normal)
            { int index = offset + positions.Count; positions.Add(position); normals.Add(normal); return index; }

            internal void Torus(int material, float radius, float tube, float height, float from, float to, int segments, bool cap)
            {
                const int cross = 6;
                int first = offset + positions.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = Mathf.Lerp(from, to, (float)i / segments);
                    var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    for (int j = 0; j <= cross; j++)
                    {
                        float around = (float)j / cross * Mathf.PI * 2;
                        Vector3 normal = radial * Mathf.Cos(around) + Vector3.up * Mathf.Sin(around);
                        Vertex(radial * radius + Vector3.up * height + normal * tube, normal);
                    }
                }
                for (int i = 0; i < segments; i++)
                    for (int j = 0; j < cross; j++)
                    {
                        int a = first + i * (cross + 1) + j, b = a + cross + 1;
                        Quad(indices[material], a, b, b + 1, a + 1);
                    }
                if (!cap) return;
                TubeCap(material, radius, tube, height, from, false, cross);
                TubeCap(material, radius, tube, height, to, true, cross);
            }

            private void TubeCap(int material, float radius, float tube, float height, float angle, bool end, int cross)
            {
                var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                var tangent = new Vector3(Mathf.Cos(angle), 0, -Mathf.Sin(angle));
                Vector3 normal = tangent * (end ? 1 : -1), center = radial * radius + Vector3.up * height;
                int middle = Vertex(center, normal), first = offset + positions.Count;
                for (int j = 0; j <= cross; j++)
                {
                    float around = (float)j / cross * Mathf.PI * 2;
                    Vertex(center + (radial * Mathf.Cos(around) + Vector3.up * Mathf.Sin(around)) * tube, normal);
                }
                for (int j = 0; j < cross; j++)
                {
                    indices[material].Add(middle);
                    indices[material].Add(first + j + (end ? 1 : 0));
                    indices[material].Add(first + j + (end ? 0 : 1));
                }
            }

            internal void Receiver()
            {
                const int sides = 32;
                float[] radii = { .090f, .115f, .098f }, heights = { Lift, .040f, .063f };
                float[] tilt = { -.56f, 0, .57f };
                int first = offset + positions.Count;
                for (int ring = 0; ring < 3; ring++)
                    for (int i = 0; i <= sides; i++)
                    {
                        float angle = (float)i / sides * Mathf.PI * 2;
                        var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                        Vertex(radial * radii[ring] + Vector3.up * heights[ring],
                            (radial + Vector3.up * tilt[ring]).normalized);
                    }
                for (int ring = 0; ring < 2; ring++)
                    for (int i = 0; i < sides; i++)
                    {
                        int a = first + ring * (sides + 1) + i;
                        Quad(indices[2], a, a + 1, a + sides + 2, a + sides + 1);
                    }
                Disc(.098f, .063f, true, sides);
                receiverAnchorIndex = Disc(.090f, Lift, false, sides);
            }

            private int Disc(float radius, float height, bool top, int sides)
            {
                Vector3 normal = Vector3.up * (top ? 1 : -1);
                int center = Vertex(Vector3.up * height, normal), first = offset + positions.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float angle = (float)i / sides * Mathf.PI * 2;
                    Vertex(new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius), normal);
                }
                for (int i = 0; i < sides; i++)
                {
                    indices[2].Add(center);
                    indices[2].Add(first + i + (top ? 0 : 1));
                    indices[2].Add(first + i + (top ? 1 : 0));
                }
                return center;
            }
        }
    }
}
