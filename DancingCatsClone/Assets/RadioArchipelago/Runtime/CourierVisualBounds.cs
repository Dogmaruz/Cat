using System.Collections.Generic;
using UnityEngine;

namespace RadioArchipelago
{
    /// <summary>Fits preview cameras to visible geometry, rather than padded skin culling boxes.</summary>
    public static class CourierVisualBounds
    {
        public static Bounds Measure(Renderer[] renderers) => Measure(renderers, false);

        /// <summary>Feet-to-head presentation size; antennas, crest and scarf tails do not set the scale.</summary>
        public static Bounds MeasureBody(Renderer[] renderers) => Measure(renderers, true);

        private static bool IsBodyBone(string name) => name == "root" || name == "body" || name == "head"
            || name == "arm.L" || name == "arm.R" || name == "foot.L" || name == "foot.R";

        private static Bounds Measure(Renderer[] renderers, bool bodyOnly)
        {
            var bounds = new Bounds(); bool found = false;
            Mesh scratch = null; List<Vector3> vertices = null;
            try
            {
                foreach (var renderer in renderers)
                {
                    if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                    {
                        if (scratch == null) { scratch = new Mesh { name = "Courier bounds sample" }; vertices = new List<Vector3>(); }
                        // Renderer-local vertices: TransformPoint below applies its scale once.
                        skin.BakeMesh(scratch, true); scratch.GetVertices(vertices);
                        var bones = skin.bones;
                        var counts = skin.sharedMesh.GetBonesPerVertex();
                        var weights = skin.sharedMesh.GetAllBoneWeights();
                        int cursor = 0;
                        for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
                        {
                            if (bodyOnly)
                            {
                                float bodyWeight = 0;
                                for (int i = 0; i < counts[vertexIndex]; i++)
                                {
                                    var weight = weights[cursor++];
                                    if (bones[weight.boneIndex] != null && IsBodyBone(bones[weight.boneIndex].name)) bodyWeight += weight.weight;
                                }
                                if (bodyWeight < .5f) continue;
                            }
                            var vertex = vertices[vertexIndex];
                            var point = skin.transform.TransformPoint(vertex);
                            if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                            else bounds.Encapsulate(point);
                        }
                    }
                    else
                    {
                        if (!found) { bounds = renderer.bounds; found = true; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                }
                return bounds;
            }
            finally
            {
                if (scratch != null)
                {
                    if (Application.isPlaying) Object.Destroy(scratch); else Object.DestroyImmediate(scratch);
                }
            }
        }
    }
}
