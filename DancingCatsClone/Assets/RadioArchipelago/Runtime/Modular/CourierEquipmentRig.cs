using System;
using UnityEngine;

namespace RadioArchipelago
{
    /// <summary>Authored movable parts and the harness fit, independent of the rigid case.</summary>
    [DisallowMultipleComponent]
    public sealed class CourierEquipmentRig : MonoBehaviour
    {
        public Transform topPivot, topRotor;
        public Vector3 spinAxis = Vector3.up;
        public Transform[] harness = Array.Empty<Transform>();
        public Transform[] harnessFrontControls = Array.Empty<Transform>();
        public Transform[] harnessUpperControls = Array.Empty<Transform>();
        public Transform[] antennaChain = Array.Empty<Transform>();
        public Transform[] scarfChain = Array.Empty<Transform>();
        public float referenceBodyWidth = 1, referenceBodyDepth = .6f;
        public Vector3 boundsMin, boundsMax;
        public Vector3 caseBoundsMin, caseBoundsMax;
        public float caseContactZ;
        public Material harnessMaterial, buckleMaterial;
        private Transform originalHarnessRoot;
        private GameObject fittedStand;
        private bool topOriginCaptured;
        private Vector3 topOriginLocal;
        public float AppliedTopLift { get; private set; }
        public float SweptTopMinimum { get; private set; }

        public void FitTop(float clearanceHeight, Transform visualFrame)
        {
            if (topPivot == null || topRotor == null || visualFrame == null) return;
            if (!topOriginCaptured) { topOriginLocal = topPivot.localPosition; topOriginCaptured = true; }
            Vector3 origin = visualFrame.InverseTransformPoint(topPivot.parent != null
                ? topPivot.parent.TransformPoint(topOriginLocal) : topOriginLocal);
            Vector3 pivot = visualFrame.InverseTransformPoint(topPivot.position);
            Vector3 axis = spinAxis.sqrMagnitude > .001f ? spinAxis.normalized : Vector3.up;
            float minimum = float.PositiveInfinity;
            foreach (var filter in topRotor.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled) continue;
                if (filter.sharedMesh.isReadable)
                    foreach (var vertex in filter.sharedMesh.vertices)
                        Accumulate(visualFrame.InverseTransformPoint(filter.transform.TransformPoint(vertex)) - pivot);
                else
                {
                    var bounds = renderer.bounds;
                    for (int corner = 0; corner < 8; corner++)
                        Accumulate(visualFrame.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))) - pivot);
                }
            }
            if (float.IsPositiveInfinity(minimum)) return;
            float lift = Mathf.Max(0, clearanceHeight - (pivot.y + minimum));
            topPivot.position += visualFrame.TransformVector(Vector3.up * lift);
            pivot.y += lift;
            AppliedTopLift = Mathf.Max(AppliedTopLift, pivot.y - origin.y);
            SweptTopMinimum = pivot.y + minimum;
            if (fittedStand != null) { fittedStand.SetActive(false); if (Application.isPlaying) Destroy(fittedStand); else DestroyImmediate(fittedStand); }
            // Tripo can leave the original bearing above the case. The support must
            // start inside the real case roof, not at that already floating bearing.
            Vector3 supportBase = origin;
            if (caseBoundsMax.y > caseBoundsMin.y)
            {
                Vector3 inEquipment = transform.InverseTransformPoint(visualFrame.TransformPoint(origin));
                inEquipment.y = Mathf.Min(inEquipment.y, caseBoundsMax.y - .015f);
                Vector3 roofPoint = visualFrame.InverseTransformPoint(transform.TransformPoint(inEquipment));
                if (roofPoint.y < supportBase.y) supportBase = roofPoint;
            }
            Vector3 end = new Vector3(origin.x, pivot.y, origin.z);
            float supportLength = Vector3.Distance(supportBase, end);
            if (supportLength < .015f) return;
            fittedStand = new GameObject("Fitted telescopic swivel stand");
            fittedStand.transform.SetParent(visualFrame, false);
            fittedStand.layer = gameObject.layer;
            // Move only the joint; all supplied rotor vertices and proportions remain unchanged.
            Cylinder("Lower telescope", supportBase, Vector3.Lerp(supportBase, end, .62f), .033f, harnessMaterial);
            Cylinder("Upper telescope", Vector3.Lerp(supportBase, end, .45f), end, .025f, harnessMaterial);
            float collarLength = Mathf.Min(.045f, supportLength * .18f);
            foreach (float section in new[] { .05f, .57f })
            {
                Vector3 middle = Vector3.Lerp(supportBase, end, section);
                Cylinder("Brass collar", middle - Vector3.up * collarLength * .5f, middle + Vector3.up * collarLength * .5f, .041f, buckleMaterial);
            }
            var hub = Primitive(PrimitiveType.Sphere, "Rounded swivel hub", buckleMaterial);
            hub.transform.localPosition = end; hub.transform.localScale = Vector3.one * .105f;
            fittedStand.transform.SetParent(transform, true);

            void Accumulate(Vector3 relative)
            {
                float dot = Vector3.Dot(relative, axis);
                float radialSquared = Mathf.Max(0, relative.sqrMagnitude - dot * dot);
                float spinMinimum = dot * axis.y - Mathf.Sqrt(Mathf.Max(0, (1 - axis.y * axis.y) * radialSquared));
                // Bound the extra vertical displacement of any point during a 15-degree inertial lean.
                float leanMargin = relative.magnitude * 2 * Mathf.Sin(15 * Mathf.Deg2Rad * .5f);
                minimum = Mathf.Min(minimum, spinMinimum - leanMargin);
            }
            GameObject Primitive(PrimitiveType type, string name, Material material)
            {
                var node = GameObject.CreatePrimitive(type); node.name = name;
                var collider = node.GetComponent<Collider>(); if (collider != null) DestroyImmediate(collider);
                node.transform.SetParent(fittedStand.transform, false); node.layer = gameObject.layer;
                var renderer = node.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material != null ? material : topRotor.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                renderer.receiveShadows = true; return node;
            }
            void Cylinder(string name, Vector3 from, Vector3 to, float radius, Material material)
            {
                var node = Primitive(PrimitiveType.Cylinder, name, material);
                node.transform.localPosition = (from + to) * .5f;
                node.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
                node.transform.localScale = new Vector3(radius * 2, Vector3.Distance(from, to) * .5f, radius * 2);
            }
        }

        public void FitOriginalHarness(float widthScale, float depthScale, float anchorZ = 0,
            float forwardOffset = 0, float outwardOffset = 0, float verticalOffset = 0, float upperDrop = 0, float lowerOutwardOffset = 0,
            float lowerDrop = 0)
        {
            foreach (var strap in harness) if (strap != null) strap.gameObject.SetActive(true);
            if (harnessFrontControls.Length == 2 && harnessFrontControls[0] != null && harnessFrontControls[1] != null)
            {
                harnessFrontControls[0].localPosition = new Vector3(-outwardOffset-lowerOutwardOffset, verticalOffset-lowerDrop, forwardOffset);
                harnessFrontControls[1].localPosition = new Vector3(outwardOffset+lowerOutwardOffset, verticalOffset-lowerDrop, forwardOffset);
                if(harnessUpperControls.Length==2 && harnessUpperControls[0]!=null && harnessUpperControls[1]!=null)
                {
                    harnessUpperControls[0].localPosition = new Vector3(-outwardOffset, verticalOffset-upperDrop, forwardOffset);
                    harnessUpperControls[1].localPosition = new Vector3(outwardOffset, verticalOffset-upperDrop, forwardOffset);
                }
                return;
            }
            widthScale = Mathf.Clamp(widthScale, .85f, 1.18f);
            depthScale = Mathf.Clamp(depthScale, .85f, 3f);
            if (originalHarnessRoot == null)
            {
                originalHarnessRoot = new GameObject("Original strap fitting").transform;
                originalHarnessRoot.SetParent(transform, false);
                originalHarnessRoot.gameObject.layer = gameObject.layer;
                originalHarnessRoot.localPosition = new Vector3(0, 0, anchorZ);
                foreach (var strap in harness) if (strap != null) strap.SetParent(originalHarnessRoot, true);
            }
            originalHarnessRoot.localScale = new Vector3(widthScale, 1, depthScale);
        }

        private void OnDestroy()
        {
            if (fittedStand != null)
            { fittedStand.SetActive(false); if (Application.isPlaying) Destroy(fittedStand); else DestroyImmediate(fittedStand); fittedStand = null; }
        }

    }
}
