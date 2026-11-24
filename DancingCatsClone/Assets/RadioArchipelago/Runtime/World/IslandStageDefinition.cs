using UnityEngine;

namespace RadioArchipelago.World
{
    [CreateAssetMenu(menuName = "Radio Archipelago/Island stage")]
    public sealed class IslandStageDefinition : ScriptableObject
    {
        public string SongId = "AlwaysRememberMeowCat";
        public GameObject Prefab;
        [Tooltip("Authored rigid rooftop heads, with explicit node bindings, pivots and rotation speeds.")]
        public TextAsset RooftopMotionJson;
        public TextAsset RouteJson;
        [Tooltip("Optional height over the same planar route distance. Omit to retain the flat pilot.")]
        public TextAsset ElevationJson;
        [Tooltip("Optional pilot chart copy. The source song timeline remains unchanged.")]
        public TextAsset Chart;
        [Tooltip("Native FX meshes paired with readable pilot copies. Original imported assets remain unchanged.")]
        public Mesh[] FxSourceMeshes = System.Array.Empty<Mesh>();
        public Mesh[] FxReadableMeshes = System.Array.Empty<Mesh>();
        [Tooltip("Explicit Player dependency for the dynamically created mint Hold materials.")]
        public Shader HoldBodyShader;
        [Tooltip("Transparent native tracer shader, also used for the soft white ride overlay.")]
        public Shader HoldTracerShader;
        [Range(0, 1)] public float HoldBandOpacity = .24f;
        [Range(0, 1)] public float HoldLineOpacity = 1f;
        public float StartDistance;
        [Min(.01f)] public float DistanceScale = 1.18f;
        [Tooltip("World spacing between native lanes. Tile sizes and musical timing stay independent.")]
        [Range(1, 3)] public float LateralScale = 1f;
        [Range(.5f, 1f)] public float TileElevation = 1f;
        [Min(.05f)] public float TileThickness = .30f;
        [Min(0)] public float GlowEmission = 0f;
        [Min(.01f)] public float CourierScale = .64f;
        [Min(1)] public float RoadWidth = 6f;
        [Min(0)] public float LateralLimit = 1.30f;
        [Min(1)] public float VisibleAhead = 35f;
        [Min(0)] public float VisibleBehind = 4f;
        [Min(1)] public float CameraHeight = 7.8f;
        [Min(1)] public float CameraBackDistance = 11.5f;
        [Min(0)] public float CameraLookAhead = 8.5f;
        [Range(20, 90)] public float CameraFieldOfView = 48f;
        [Min(.01f)] public float CameraSmoothing = 7f;
        [Tooltip("A close route rig with a constant offset; never dollies to fit a turn.")]
        public bool UseCloseCamera;
        [Tooltip("How much of the courier's lateral motion the fixed camera anchor follows.")]
        [Range(0, 1)] public float CameraLateralFollow = 1f;
        [Min(0)] public float CameraFocusHeight = 3.6f;
        [Min(0)] public float CameraHeadingLead = 3.2f;
        [Tooltip("Use the same glazed ceramic and brass shape for round and long supports.")]
        public bool UnifiedHoldTiles;
        [Tooltip("Keep every ordinary landing at its native .65m radius. The authored route must provide full-size clearance throughout motion.")]
        public bool UniformRoundTiles;
        [Range(1, 2.5f)] public float JumpHeightScale = 1f;
        public bool LivingNatureEnabled;
        [Tooltip("Land on a meadow for the final collection run instead of a floating finish disc.")]
        public bool EnableGroundFinish;
        [Tooltip("The finish ground and batched decoration belong to this island's world. Zero preserves the original Emerald meadow.")]
        public IslandFinishTheme FinishTheme = IslandFinishTheme.EmeraldMeadow;
        [Tooltip("Finale exit heading relative to the last musical support; existing islands retain their established meadow exit.")]
        [Range(-90, 90)] public float FinishExitYaw = IslandGroundFinishPath.MeadowExitYaw;
        [Tooltip("Optional island reward presentation; existing colliders and saved currency remain authoritative.")]
        public SignalSparkVisual FinishCurrencyPrefab;
        public Color SkyColor = new Color(.67f, .79f, .85f, 1f);
        [Tooltip("Use detached, varied volumetric cloud steps. Omit to retain the original cloud bank exactly.")]
        public bool SparseCloudSteps;
        [Tooltip("Optional route-distance windows for detached clouds between small islands. Empty uses the original cloud crossing window.")]
        public Vector2[] CloudStepRanges = System.Array.Empty<Vector2>();
        [Tooltip("Uniform cloud-step volume multiplier. Existing islands retain their current size at1.")]
        [Range(1, 2.5f)] public float CloudBankScaleMultiplier = 1f;
    }
}
