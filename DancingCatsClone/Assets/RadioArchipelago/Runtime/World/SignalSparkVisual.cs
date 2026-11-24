using UnityEngine;

namespace RadioArchipelago.World
{
    /// <summary>
    /// Presentation of one signal-spark reward. The owner supplies the clock and
    /// visibility; native reward colliders and balance remain authoritative.
    /// All instances share meshes/materials and have no individual Update loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SignalSparkVisual : MonoBehaviour
    {
        [SerializeField] private Transform spiral, nucleus, orbit, halo;
        [SerializeField] private MeshRenderer haloRenderer;
        [SerializeField] private Renderer[] modelRenderers;
        [SerializeField] private Bounds localVisualBounds;
        [SerializeField] private Quaternion spiralRest, orbitRest;
        [SerializeField] private Vector3 spiralAxis = Vector3.up, orbitAxis = Vector3.up;
        [SerializeField] private Vector3 nucleusRestScale;
        private Camera viewCamera;
        private MaterialPropertyBlock haloProperties;
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        public Bounds LocalVisualBounds => localVisualBounds;
        public Renderer[] ModelRenderers => modelRenderers;
        public float WorldBottomY
        {
            get
            {
                float bottom = float.PositiveInfinity;
                for (int i = 0; i < modelRenderers.Length; i++)
                    if (modelRenderers[i] != null && modelRenderers[i].enabled)
                        bottom = Mathf.Min(bottom, modelRenderers[i].bounds.min.y);
                return float.IsPositiveInfinity(bottom) ? transform.position.y : bottom;
            }
        }

        /// <summary>Called only by asset authoring, with explicit imported nodes.</summary>
        public void Configure(Transform spiralNode, Transform coreNode, Transform orbitNode,
            Transform haloNode, MeshRenderer glowRenderer, Renderer[] visibleModel, Bounds stableLocalBounds)
        {
            spiral = spiralNode; nucleus = coreNode; orbit = orbitNode;
            halo = haloNode; haloRenderer = glowRenderer;
            modelRenderers = visibleModel; localVisualBounds = stableLocalBounds;
            spiralRest = spiral != null ? spiral.localRotation : Quaternion.identity;
            orbitRest = orbit != null ? orbit.localRotation : Quaternion.identity;
            // FBX can retain Blender's Z-up local axes below its conversion root.
            spiralAxis = spiral != null ? spiral.InverseTransformDirection(transform.up).normalized : Vector3.up;
            orbitAxis = orbit != null ? orbit.InverseTransformDirection(transform.up).normalized : Vector3.up;
            nucleusRestScale = nucleus != null ? nucleus.localScale : Vector3.one;
        }

        public void SetCamera(Camera camera) => viewCamera = camera;

        public void Tick(float clock, float phaseSeed)
        {
            // Bounded angles keep a long-running session numerically stable.
            float orbitAngle = Mathf.Repeat(clock * 26f + phaseSeed * Mathf.Rad2Deg, 360f);
            if (orbit != null) orbit.localRotation = orbitRest * Quaternion.AngleAxis(orbitAngle, orbitAxis);
            // The authored waveform is part of the spiral mesh. A gentle sway
            // keeps that radio signature facing the player rather than orbiting
            // behind the opaque nucleus with a continuously rotating helix.
            if (spiral != null) spiral.localRotation = spiralRest * Quaternion.AngleAxis(5f * Mathf.Sin(clock * .65f + phaseSeed), spiralAxis);
            float pulse = .5f + .5f * Mathf.Sin(clock * 2.6f + phaseSeed);
            if (nucleus != null) nucleus.localScale = nucleusRestScale * (.97f + .06f * pulse);
            if (halo != null && viewCamera != null) halo.rotation = viewCamera.transform.rotation;
            if (haloRenderer == null) return;
            if (haloProperties == null) haloProperties = new MaterialPropertyBlock();
            haloProperties.SetFloat(IntensityId, .87f + .16f * pulse);
            haloRenderer.SetPropertyBlock(haloProperties);
        }
    }
}
