using System.Collections.Generic;
using UnityEngine;

namespace RadioArchipelago
{
    /// <summary>Fits preview cameras to visible geometry, rather than padded skin culling boxes.</summary>
    public static class CourierVisualBounds
    {
        public static Bounds Measure(Renderer[] renderers)
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
                        skin.BakeMesh(scratch); scratch.GetVertices(vertices);
                        foreach (var vertex in vertices)
                        {
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
