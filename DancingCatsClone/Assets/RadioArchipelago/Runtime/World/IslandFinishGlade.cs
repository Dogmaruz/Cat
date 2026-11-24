using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RadioArchipelago.World
{
    /// <summary>
    /// Scene-owned regional ground for the native finish collection run. The central
    /// nine metres are exactly planar. No collider, simulation or clock is used.
    /// Supplied vertex-colour materials are borrowed; only generated meshes and
    /// the generated root are owned and disposed by this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class IslandFinishGlade : MonoBehaviour
    {
        private const int EdgeSegments = 4, InnerSegments = 16;
        private const int Columns = InnerSegments + EdgeSegments * 2 + 1;
        private const float FlatHalfWidth = 4.5f, BackMargin = 3.5f, EndMargin = 4f;
        private readonly List<Mesh> meshes = new List<Mesh>(4);
        private Transform generatedRoot;
        private float phase;
        private int rows;
        private MeshData land;

        public bool IsConfigured => generatedRoot != null;
        public float Length { get; private set; }
        public float Width { get; private set; }
        public float GroundHeight { get; private set; }
        public float ClearHalfWidth => FlatHalfWidth;
        public float Depth { get; private set; }
        public int GeneratedTriangleCount { get; private set; }
        public int GeneratedRendererCount { get; private set; }
        public int CornflowerCount { get; private set; }
        public int DaisyCount { get; private set; }
        public int DandelionCount { get; private set; }
        public int LowCorridorFlowerCount { get; private set; }
        public float MaxCorridorPlantHeight { get; private set; }
        public IReadOnlyList<Mesh> OwnedMeshes => meshes;
        public Transform GeneratedRoot => generatedRoot;

        /// <param name="anchor">World XZ centre of the landing/start of the coin run.</param>
        /// <param name="forward">World horizontal direction of the straight coin run.</param>
        /// <param name="groundHeight">Exact absolute world Y of the coin run surface.</param>
        /// <param name="length">World metres from landing through the end of the run.</param>
        /// <param name="width">Nominal meadow width; at least 12m including rolling edges.</param>
        /// <remarks>
        /// Top covers -3.5m through length+4m. Use the existing island Terrain,
        /// Cliffs, Grass and Foliage materials (vertex RGB, alpha wind weight).
        /// Terrain/Cliffs must already protect structural surfaces from cutaway.
        /// All material references stay borrowed and unmodified.
        /// </remarks>
        public void Configure(Vector3 anchor, Vector3 forward, float groundHeight,
            float length, float width, Material ground, Material cliffs, Material grass,
            Material flowers, int seed, IslandFinishTheme theme = IslandFinishTheme.EmeraldMeadow,
            Material accent = null)
        {
            if (!Finite(anchor) || !Finite(forward) || !Finite(groundHeight) ||
                !Finite(length) || !Finite(width) || length <= 0)
                throw new ArgumentException("Finish glade dimensions and frame must be finite.");
            forward.y = 0;
            if (forward.sqrMagnitude < .0001f) throw new ArgumentException("Finish glade needs a horizontal heading.");
            if (ground == null || cliffs == null || grass == null || flowers == null)
                throw new ArgumentNullException(nameof(ground), "Finish glade needs four borrowed island materials.");
            if (!Enum.IsDefined(typeof(IslandFinishTheme),theme)) throw new ArgumentOutOfRangeException(nameof(theme));
            if (theme == IslandFinishTheme.MoonCrystalGarden && accent == null)
                throw new ArgumentNullException(nameof(accent),"The lunar finish needs its island's borrowed crystal material.");
            Dispose();
            Theme = theme;
            Length = Mathf.Clamp(length, 4, 120);
            Width = Mathf.Clamp(width, 12, 32);
            GroundHeight = groundHeight;
            Depth = Mathf.Clamp(Width * .9f + Length * .13f, 12, 20);
            var random = new System.Random(seed);
            phase = Range(random, 0, Mathf.PI * 2);
            generatedRoot = new GameObject(theme == IslandFinishTheme.EmeraldMeadow ? "Natural finish meadow (visual ground)" : theme + " finish ground").transform;
            generatedRoot.SetParent(transform, false);
            generatedRoot.SetPositionAndRotation(new Vector3(anchor.x, groundHeight, anchor.z),
                Quaternion.LookRotation(forward.normalized, Vector3.up));
            land = BuildLand();
            AddMesh(theme == IslandFinishTheme.EmeraldMeadow ? "Continuous grass and deep rock body" : "Continuous " + theme + " ground and rock body", land, new[] { ground, cliffs });
            var rocks = new MeshData(1);
            BuildRockPiles(rocks, random);
            AddMesh("Overlapping cliff masses and edge boulders", rocks, new[] { cliffs });
            var blades = new MeshData(1);
            if (theme == IslandFinishTheme.EmeraldMeadow) BuildGrass(blades, random);
            else BuildThemedPlants(blades,random);
            AddMesh(theme == IslandFinishTheme.EmeraldMeadow ? "Grounded meadow grass" : theme + " regional plants", blades, new[] { grass });
            var blooms = new MeshData(1);
            if (theme == IslandFinishTheme.EmeraldMeadow) BuildFlowers(blooms, random);
            else BuildThemedAccents(blooms,random);
            AddMesh(theme == IslandFinishTheme.EmeraldMeadow ? "Cornflowers, daisies and golden dandelions" : theme + " regional accents", blooms,
                new[] { theme == IslandFinishTheme.MoonCrystalGarden ? accent : flowers });
        }

        /// <summary>Sample the actual top triangles in world space; useful for presentation checks.</summary>
        public bool TrySampleGround(Vector3 world, out float height)
        {
            height = GroundHeight;
            if (!IsConfigured || land == null) return false;
            Vector3 local = generatedRoot.InverseTransformPoint(world);
            if (!SampleTop(local.x, local.z, out float y)) return false;
            height = generatedRoot.TransformPoint(new Vector3(local.x, y, local.z)).y;
            return true;
        }

        private MeshData BuildLand()
        {
            var data = new MeshData(2);
            rows = Mathf.CeilToInt((Length + BackMargin + EndMargin) / 1.25f) + 1;
            for (int row = 0; row < rows; row++)
            {
                float t = (float)row / (rows - 1);
                float z0 = Mathf.Lerp(-BackMargin, Length + EndMargin, t);
                float half = HalfWidth(z0);
                for (int col = 0; col < Columns; col++)
                {
                    float x = col <= EdgeSegments ? Mathf.Lerp(-half, -FlatHalfWidth, (float)col / EdgeSegments) :
                        col <= EdgeSegments + InnerSegments ? Mathf.Lerp(-FlatHalfWidth, FlatHalfWidth, (float)(col - EdgeSegments) / InnerSegments) :
                        Mathf.Lerp(FlatHalfWidth, half, (float)(col - EdgeSegments - InnerSegments) / EdgeSegments);
                    float u = x / half;
                    // Rounded, uneven corners without an elliptical platform silhouette.
                    float corner = Mathf.Pow(Mathf.Abs(u), 4) *
                        (1.4f + .25f * Mathf.Sin(phase + u * 3)) * (Mathf.Pow(1 - t, 8) - Mathf.Pow(t, 8));
                    float z = z0 + corner;
                    float y = TopHeight(x, z);
                    float variation = .5f + .25f * Mathf.Sin(x * .72f + phase) + .25f * Mathf.Cos(z * .43f - x * .31f);
                    Color color = Linear(ThemeGround(Color.Lerp(new Color(.29f, .43f, .19f), new Color(.42f, .55f, .26f), variation),variation));
                    data.Vertex(new Vector3(x, y, z), color, new Vector2(x, z));
                }
            }
            for (int r = 0; r < rows - 1; r++)
                for (int c = 0; c < Columns - 1; c++)
                {
                    int i = r * Columns + c;
                    data.Quad(i, i + Columns, i + Columns + 1, i + 1, 0);
                }
            var boundary = new List<int>(rows * 2 + Columns * 2 - 4);
            for (int c = Columns - 1; c >= 0; c--) boundary.Add(c);
            for (int r = 1; r < rows; r++) boundary.Add(r * Columns);
            for (int c = 1; c < Columns; c++) boundary.Add((rows - 1) * Columns + c);
            for (int r = rows - 2; r > 0; r--) boundary.Add(r * Columns + Columns - 1);
            int count = boundary.Count;
            var previous = boundary.ToArray();
            float center = Length * .5f;
            float[] heights = { -.7f, -1.8f, -5.4f, -10.2f, -Depth };
            float[] radii = { 1.025f, 1.0f, .88f, .64f, .18f };
            for (int ring = 0; ring < heights.Length; ring++)
            {
                var next = new int[count];
                for (int j = 0; j < count; j++)
                {
                    Vector3 q = data.Vertices[boundary[j]];
                    float wobble = .035f * Mathf.Sin(j * 1.83f + ring * 1.4f + phase);
                    float radius = radii[ring] + wobble;
                    float x = q.x * radius + Mathf.Sin(ring * 1.7f + phase) * .35f;
                    float z = center + (q.z - center) * radius + Mathf.Cos(ring * 1.3f + phase) * .55f;
                    float y = heights[ring] + (ring == 0 ? q.y * .35f : .28f * Mathf.Sin(j * .79f + phase + ring));
                    float shade = .85f + .15f * Mathf.Sin(j * .6f + ring * 2.1f);
                    Color color = Linear(ThemeRock(new Color(.38f * shade, .43f * shade, .42f * shade)));
                    next[j] = data.Vertex(new Vector3(x, y, z), color, new Vector2(x, z));
                }
                for (int j = 0; j < count; j++)
                    data.Quad(previous[j], next[j], next[(j + 1) % count], previous[(j + 1) % count], 1);
                previous = next;
            }
            int tip = data.Vertex(new Vector3(.4f, -Depth - 1.3f, center + .7f), Linear(ThemeRock(new Color(.25f, .31f, .32f))), Vector2.zero);
            for (int j = 0; j < count; j++) data.Triangle(tip, previous[(j + 1) % count], previous[j], 1);
            return data;
        }

        private float HalfWidth(float z) => Mathf.Max(FlatHalfWidth + 1.35f,
            Width * .5f * (.94f + .045f * Mathf.Sin(z * .31f + phase) + .035f * Mathf.Sin(z * .79f + phase * 1.7f)));

        private float TopHeight(float x, float z)
        {
            float t = Mathf.Clamp01((Mathf.Abs(x) - FlatHalfWidth) / Mathf.Max(.1f, HalfWidth(z) - FlatHalfWidth));
            float edge = t * t * (3 - 2 * t);
            return edge * (.23f + .18f * Mathf.Sin(z * .35f + phase) +
                .16f * Mathf.Cos(x * .8f + z * .5f + phase)) + .12f * edge * edge;
        }

        private bool SampleTop(float x, float z, out float y)
        {
            var triangles = land.Indices[0];
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = land.Vertices[triangles[i]], b = land.Vertices[triangles[i + 1]], c = land.Vertices[triangles[i + 2]];
                float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) < .000001f) continue;
                float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / denominator;
                float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / denominator;
                if (u < -.00001f || v < -.00001f || u + v > 1.00001f) continue;
                y = a.y * u + b.y * v + c.y * (1 - u - v);
                return true;
            }
            y = 0;
            return false;
        }

        private void BuildRockPiles(MeshData data, System.Random random)
        {
            // Large overlapping masses cover the solid core and break its whole silhouette.
            int bands = 3, around = Mathf.Clamp(Mathf.CeilToInt(Length / 4.5f), 5, 14);
            for (int band = 0; band < bands; band++)
                for (int i = 0; i < around; i++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float z = Mathf.Lerp(-1, Length + 1, (i + .5f + Range(random, -.25f, .25f)) / around);
                        float taper = 1 - band * .25f;
                        float x = HalfWidth(z) * taper * side;
                        Vector3 center = new Vector3(x, -2.6f - band * 4.9f, Length * .5f + (z - Length * .5f) * taper);
                        var size = new Vector3(Range(random, 1.8f, 2.8f), Range(random, 2.2f, 3.3f), Range(random, 2.4f, 3.8f));
                        AddRock(data, center, size, Quaternion.Euler(Range(random, -23, 23), Range(random, 0, 360), Range(random, -24, 24)), random, true);
                    }
            for (int end = 0; end < 2; end++)
                for (int i = 0; i < 3; i++)
                    AddRock(data, new Vector3((i - 1) * 2.7f, -3.7f, end == 0 ? -2.4f : Length + 2.5f),
                        new Vector3(2.7f, 3.6f, 2.8f), Quaternion.Euler(11, Range(random, 0, 360), -16), random, true);
            for (int i = 0; i < 4; i++)
            {
                float z = Length * (.14f + i * .22f), x = (i % 2 == 0 ? -1 : 1) * (HalfWidth(z) - .45f);
                if (!SampleTop(x, z, out float y)) continue;
                AddRock(data, new Vector3(x, y + .26f, z), new Vector3(.58f, .65f, .85f), Quaternion.Euler(12, i * 73, -9), random, false);
            }
        }

        private void AddRock(MeshData data, Vector3 center, Vector3 scale, Quaternion rotation, System.Random random, bool belowGround)
        {
            const int sides = 9;
            int bottom = data.Vertex(center + rotation * Vector3.Scale(new Vector3(.10f, -.92f, -.06f), scale), Linear(ThemeRock(new Color(.30f, .36f, .36f))), Vector2.zero);
            int first = data.Vertices.Count;
            float spin = Range(random, 0, Mathf.PI * 2);
            float[] radii = { .65f, 1, .92f, .55f }, levels = { -.66f, -.25f, .35f, .72f };
            for (int r = 0; r < radii.Length; r++)
                for (int i = 0; i < sides; i++)
                {
                    float angle = spin + i * Mathf.PI * 2 / sides;
                    float radius = radii[r] * Range(random, .86f, 1.13f);
                    Vector3 q = center + rotation * Vector3.Scale(new Vector3(Mathf.Cos(angle) * radius, levels[r] + Range(random, -.06f, .06f), Mathf.Sin(angle) * radius), scale);
                    if (belowGround) q.y = Mathf.Min(q.y, -.18f);
                    float shade = Range(random, .85f, 1.10f);
                    data.Vertex(q, Linear(ThemeRock(new Color(.39f * shade, .44f * shade, .43f * shade))), new Vector2(q.x, q.z));
                }
            for (int i = 0; i < sides; i++) data.Triangle(bottom, first + i, first + (i + 1) % sides, 0);
            for (int r = 0; r < radii.Length - 1; r++)
                for (int i = 0; i < sides; i++)
                {
                    int a = first + r * sides + i, b = first + r * sides + (i + 1) % sides;
                    data.Quad(a, a + sides, b + sides, b, 0);
                }
            Vector3 top = center + rotation * Vector3.Scale(new Vector3(-.08f, .94f, .09f), scale);
            if (belowGround) top.y = Mathf.Min(top.y, -.18f);
            int peak = data.Vertex(top, Linear(ThemeRock(new Color(.43f, .47f, .44f))), Vector2.zero);
            int last = first + (radii.Length - 1) * sides;
            for (int i = 0; i < sides; i++) data.Triangle(peak, last + (i + 1) % sides, last + i, 0);
        }

        private void BuildGrass(MeshData data, System.Random random)
        {
            int attempts = Mathf.CeilToInt((Length + 3) * 13);
            for (int i = 0; i < attempts; i++)
            {
                float z = Range(random, -1.6f, Length + 2), side = i % 2 == 0 ? -1 : 1;
                float x = side * Range(random, FlatHalfWidth + .32f, HalfWidth(z) - .18f);
                if (!SampleTop(x, z, out float y)) continue;
                int blades = 4 + random.Next(3);
                for (int j = 0; j < blades; j++)
                {
                    float bx = x + Range(random, -.13f, .13f), bz = z + Range(random, -.13f, .13f);
                    if (!SampleTop(bx, bz, out float by)) continue;
                    AddBlade(data, new Vector3(bx, by - .012f, bz), Range(random, .17f, .57f), Range(random, .018f, .036f), Range(random, 0, Mathf.PI * 2), random);
                }
            }
            // A mown-looking low centre keeps the hovering feet and glowing
            // rewards readable while connecting the outer flower beds.
            int lowTufts = Mathf.CeilToInt((Length + 3) * 6);
            for (int i = 0; i < lowTufts; i++)
            {
                float x = Range(random, -FlatHalfWidth, FlatHalfWidth);
                float z = Range(random, -1.5f, Length + 2);
                for (int blade = 0; blade < 3; blade++)
                {
                    float bx = x + Range(random, -.07f, .07f), bz = z + Range(random, -.07f, .07f);
                    if (!SampleTop(bx, bz, out float y)) continue;
                    float height = Range(random, .06f, .16f);
                    AddBlade(data, new Vector3(bx, y - .012f, bz), height, Range(random, .012f, .022f),
                        Range(random, 0, Mathf.PI * 2), random);
                    MaxCorridorPlantHeight = Mathf.Max(MaxCorridorPlantHeight, height);
                }
            }
        }

        private static void AddBlade(MeshData data, Vector3 root, float height, float width, float angle, System.Random random)
        {
            Vector3 side = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)), lean = new Vector3(-side.z, 0, side.x) * Range(random, .05f, .18f);
            int start = data.Vertices.Count;
            Color green = Linear(Color.Lerp(new Color(.23f, .39f, .13f), new Color(.40f, .53f, .22f), Range(random, 0, 1)));
            for (int ring = 0; ring < 2; ring++)
            {
                float v = ring == 0 ? 0 : .55f, r = ring == 0 ? 1 : .68f;
                Vector3 center = root + Vector3.up * (height * v) + lean * v;
                Color color = green; color.a = v;
                data.Vertex(center - side * width * r - lean.normalized * width * .12f, color, new Vector2(0, v));
                data.Vertex(center + side * width * r - lean.normalized * width * .12f, color, new Vector2(1, v));
                data.Vertex(center + lean.normalized * width * .14f * r, color, new Vector2(.5f, v));
            }
            Color tipColor = green; tipColor.a = 1;
            data.Vertex(root + lean + Vector3.up * height, tipColor, new Vector2(.5f, 1));
            data.Triangle(start, start + 1, start + 2, 0);
            data.Quad(start, start + 3, start + 4, start + 1, 0);
            data.Quad(start + 1, start + 4, start + 5, start + 2, 0);
            data.Quad(start + 2, start + 5, start + 3, start, 0);
            data.Triangle(start + 3, start + 6, start + 4, 0);
            data.Triangle(start + 4, start + 6, start + 5, 0);
            data.Triangle(start + 5, start + 6, start + 3, 0);
        }

        private void BuildFlowers(MeshData data, System.Random random)
        {
            // Irregular overlapping beds, close enough to the reward lane to
            // read as flowers in the portrait camera rather than distant dots.
            int beds = Mathf.CeilToInt((Length + 3) / 2.1f);
            for (int bed = 0; bed < beds; bed++)
            {
                float z = Mathf.Lerp(-1.3f, Length + 1.5f, (bed + .5f) / beds);
                for (int side = -1; side <= 1; side += 2)
                {
                    float centreX = side * (3.7f + .37f * Mathf.Sin(bed * 1.43f + phase));
                    int count = 5 + random.Next(3);
                    for (int flower = 0; flower < count; flower++)
                    {
                        float x = centreX + Range(random, -.64f, .64f), zz = z + Range(random, -.70f, .70f);
                        if (Mathf.Abs(x) < 3.35f || Mathf.Abs(x) > HalfWidth(zz) - .45f ||
                            !SampleTop(x, zz, out float y)) continue;
                        AddFlower(data, new Vector3(x, y, zz), (bed + flower + (side == 1 ? 1 : 0)) % 3,
                            Range(random, .43f, .73f), Range(random, .23f, .34f), random, false);
                    }
                }
            }
            // Small, isolated lawn blooms beneath the floating trail. Their
            // complete geometry, including petal crowns, stays below 0.30 m.
            int lowBlooms = Mathf.CeilToInt((Length + 3) * 1.7f);
            for (int i = 0; i < lowBlooms; i++)
            {
                float x = Range(random, -2.3f, 2.3f), z = Range(random, -1.5f, Length + 1.8f);
                if (!SampleTop(x, z, out float y)) continue;
                AddFlower(data, new Vector3(x, y, z), i % 3,
                    Range(random, .13f, .22f), Range(random, .12f, .18f), random, true);
            }
        }

        private void AddFlower(MeshData data, Vector3 root, int species, float height,
            float radius, System.Random random, bool low)
        {
            float spin = Range(random, 0, Mathf.PI * 2);
            Vector3 bend = new Vector3(Mathf.Cos(spin), 0, Mathf.Sin(spin)) * (height * .12f);
            Vector3 joint = root + Vector3.up * height * .55f + bend * .28f;
            Vector3 top = root + Vector3.up * height + bend;
            Color stem = Linear(new Color(.24f, .43f, .14f));
            AddStem(data, root - Vector3.up * .008f, joint, low ? .009f : .013f, stem);
            AddStem(data, joint, top, low ? .007f : .010f, stem);
            for (int leaf = 0; leaf < 2; leaf++)
            {
                float angle = spin + leaf * 2.8f;
                Vector3 along = new Vector3(Mathf.Cos(angle), .35f, Mathf.Sin(angle)).normalized;
                AddPetal(data, root + Vector3.up * height * (.22f + leaf * .20f), along,
                    radius * .80f, radius * .17f, radius * .11f,
                    Linear(new Color(.25f, .46f, .15f)), Linear(new Color(.38f, .55f, .22f)));
            }
            if (species == 0)
            {
                CornflowerCount++;
                AddPetalRing(data, top, 10, radius, radius * .23f, radius * .13f, spin,
                    Linear(new Color(.16f, .34f, .88f)), Linear(new Color(.39f, .61f, 1.0f)));
                AddPetalRing(data, top + Vector3.up * radius * .10f, 8, radius * .62f,
                    radius * .14f, radius * .18f, spin + .23f,
                    Linear(new Color(.17f, .27f, .66f)), Linear(new Color(.49f, .65f, .99f)));
                AddFlowerCentre(data, top + Vector3.up * radius * .09f, radius * .20f,
                    radius * .23f, Linear(new Color(.17f, .16f, .42f)), Linear(new Color(.46f, .45f, .77f)));
            }
            else if (species == 1)
            {
                DaisyCount++;
                AddPetalRing(data, top, 12, radius, radius * .20f, radius * .06f, spin,
                    Linear(new Color(.87f, .85f, .76f)), Linear(new Color(.99f, .98f, .92f)));
                AddFlowerCentre(data, top + Vector3.up * radius * .045f, radius * .29f,
                    radius * .19f, Linear(new Color(.86f, .48f, .045f)), Linear(new Color(1, .79f, .16f)));
            }
            else
            {
                DandelionCount++;
                AddPetalRing(data, top, 16, radius, radius * .115f, radius * .09f, spin,
                    Linear(new Color(.89f, .54f, .02f)), Linear(new Color(1, .87f, .11f)));
                AddPetalRing(data, top + Vector3.up * radius * .075f, 11, radius * .67f,
                    radius * .10f, radius * .15f, spin + .16f,
                    Linear(new Color(.98f, .66f, .035f)), Linear(new Color(1, .91f, .24f)));
                AddFlowerCentre(data, top + Vector3.up * radius * .07f, radius * .20f,
                    radius * .20f, Linear(new Color(.94f, .61f, .04f)), Linear(new Color(1, .83f, .13f)));
            }
            if (low)
            {
                LowCorridorFlowerCount++;
                // The most elevated part is the second cornflower crown:
                // top .10r plus its .23r domed centre.
                MaxCorridorPlantHeight = Mathf.Max(MaxCorridorPlantHeight, height + radius * .34f);
            }
        }

        private static void AddPetalRing(MeshData data, Vector3 centre, int petals,
            float radius, float width, float crown, float spin, Color root, Color tip)
        {
            for (int i = 0; i < petals; i++)
            {
                float angle = spin + i * Mathf.PI * 2 / petals;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                AddPetal(data, centre + outward * radius * .11f, outward,
                    radius * (.89f + .045f * Mathf.Sin(i * 2.7f)), width, crown, root, tip);
            }
        }

        /// <summary>A closed, gently cupped petal, with a curved crown and shaded underside.</summary>
        private static void AddPetal(MeshData data, Vector3 root, Vector3 direction,
            float length, float width, float crown, Color baseColor, Color tipColor)
        {
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            Vector3 normal = Vector3.Cross(direction, side).normalized;
            int first = data.Vertices.Count;
            data.Vertex(root, baseColor, new Vector2(.5f, 0));
            data.Vertex(root + direction * length * .36f + side * width * .74f + normal * crown * .22f,
                tipColor, new Vector2(0, .36f));
            data.Vertex(root + direction * length * .76f + side * width + normal * crown * .68f,
                tipColor, new Vector2(0, .76f));
            data.Vertex(root + direction * length + normal * crown,
                tipColor, new Vector2(.5f, 1));
            data.Vertex(root + direction * length * .76f - side * width + normal * crown * .68f,
                tipColor, new Vector2(1, .76f));
            data.Vertex(root + direction * length * .36f - side * width * .74f + normal * crown * .22f,
                tipColor, new Vector2(1, .36f));
            int ridge = data.Vertex(root + direction * length * .50f + normal * (crown * .80f + width * .18f),
                tipColor, new Vector2(.5f, .5f));
            Color underside = Color.Lerp(baseColor, tipColor, .35f);
            int bottom = data.Vertex(root + direction * length * .49f - normal * width * .08f,
                underside, new Vector2(.5f, .5f));
            for (int i = 0; i < 6; i++)
            {
                int a = first + i, b = first + (i + 1) % 6;
                data.Triangle(ridge, b, a, 0);
                data.Triangle(bottom, a, b, 0);
            }
        }

        /// <summary>Small raised flower disc; no billboard or transparent card is used.</summary>
        private static void AddFlowerCentre(MeshData data, Vector3 centre, float radius,
            float height, Color rim, Color crown)
        {
            const int sides = 10;
            int first = data.Vertices.Count;
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    float r = ring == 0 ? radius : radius * .70f;
                    data.Vertex(centre + new Vector3(Mathf.Cos(a) * r, ring * height * .65f,
                        Mathf.Sin(a) * r), ring == 0 ? rim : crown, new Vector2((float)i / sides, ring));
                }
            int top = data.Vertex(centre + Vector3.up * height, crown, new Vector2(.5f, 1));
            int bottom = data.Vertex(centre - Vector3.up * height * .15f, rim, new Vector2(.5f, 0));
            for (int i = 0; i < sides; i++)
            {
                int a = first + i, b = first + (i + 1) % sides;
                data.Quad(a, a + sides, b + sides, b, 0);
                data.Triangle(top, b + sides, a + sides, 0);
                data.Triangle(bottom, a, b, 0);
            }
        }

        private static void AddStem(MeshData data, Vector3 root, Vector3 tip, float radius, Color color)
        {
            const int sides = 5;
            int first = data.Vertices.Count;
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    data.Vertex((ring == 0 ? root : tip) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * (ring == 0 ? 1 : .6f), color, new Vector2((float)i / sides, ring));
                }
            for (int i = 0; i < sides; i++)
                data.Quad(first + i, first + sides + i, first + sides + (i + 1) % sides, first + (i + 1) % sides, 0);
            for (int i = 1; i < sides - 1; i++)
            {
                data.Triangle(first, first + i, first + i + 1, 0);
                data.Triangle(first + sides, first + sides + i + 1, first + sides + i, 0);
            }
        }

        private void AddMesh(string label, MeshData data, Material[] materials)
        {
            var mesh = new Mesh { name = "Finish glade / " + label, hideFlags = HideFlags.DontSave,
                indexFormat = data.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(data.Vertices); mesh.SetColors(data.Colors); mesh.SetUVs(0, data.Uv);
            mesh.subMeshCount = data.Indices.Length;
            for (int i = 0; i < data.Indices.Length; i++)
            { mesh.SetTriangles(data.Indices[i], i); GeneratedTriangleCount += data.Indices[i].Count / 3; }
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var node = new GameObject(label); node.layer = gameObject.layer; node.transform.SetParent(generatedRoot, false);
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            GeneratedRendererCount++;
        }

        public void Dispose()
        {
            if (generatedRoot != null) { generatedRoot.gameObject.SetActive(false); Release(generatedRoot.gameObject); }
            generatedRoot = null;
            for (int i = 0; i < meshes.Count; i++) Release(meshes[i]);
            meshes.Clear(); land = null;
            GeneratedTriangleCount = GeneratedRendererCount = 0;
            CornflowerCount = DaisyCount = DandelionCount = LowCorridorFlowerCount = 0;
            CoralClusterCount = ShellCount = CrystalClusterCount = CrystalCount = AutumnFlowerCount = FallenLeafCount = 0;
            Theme = IslandFinishTheme.EmeraldMeadow;
            MaxCorridorPlantHeight = 0;
            Length = Width = Depth = GroundHeight = 0;
        }

        private void OnDestroy() => Dispose();
        private static void Release(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static float Range(System.Random random, float low, float high) => low + (high - low) * (float)random.NextDouble();
        private static Color Linear(Color color) => new Color(Gamma(color.r), Gamma(color.g), Gamma(color.b), color.a);
        private static float Gamma(float value) => value <= .04045f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);

        private sealed class MeshData
        {
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<Color> Colors = new List<Color>();
            internal readonly List<Vector2> Uv = new List<Vector2>();
            internal readonly List<int>[] Indices;
            internal MeshData(int submeshes)
            { Indices = new List<int>[submeshes]; for (int i = 0; i < submeshes; i++) Indices[i] = new List<int>(); }
            internal int Vertex(Vector3 v, Color color, Vector2 uv)
            { int index = Vertices.Count; Vertices.Add(v); Colors.Add(color); Uv.Add(uv); return index; }
            internal void Triangle(int a, int b, int c, int submesh)
            { Indices[submesh].Add(a); Indices[submesh].Add(b); Indices[submesh].Add(c); }
            internal void Quad(int a, int b, int c, int d, int submesh)
            { Triangle(a, b, c, submesh); Triangle(a, c, d, submesh); }
        }
    }
}
