using System.Collections;
using DancingCats.Clone;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RadioArchipelago.World
{
    /// <summary>Scene-local quality override; the project's existing render asset remains untouched.</summary>
    public sealed class IslandPilotEnvironment : MonoBehaviour
    {
        [SerializeField] private UniversalRenderPipelineAsset renderPipeline;
        [SerializeField] private OriginalPlayController controller;
        [SerializeField] private Light sunlight;
        [SerializeField] private Volume atmosphere;
        [SerializeField] private IslandQualityMode qualityMode = IslandQualityMode.Automatic;
        private RenderPipelineAsset previous;
        private UniversalRenderPipelineAsset runtimePipeline;
        private Camera budgetCamera;
        private UniversalAdditionalCameraData cameraData;
        private Bloom bloom;
        private float slowSeconds, activeSeconds;
        private int particleBudget;
        private bool applied;

        public enum IslandQualityMode { Automatic, Economy, Balanced, Rich }
        public IslandQualityMode ActiveQuality { get; private set; }
        public int ParticleBudgetPerSystem => particleBudget;

        public void Configure(UniversalRenderPipelineAsset pipeline, OriginalPlayController game)
        { renderPipeline = pipeline; controller = game; }

        public void ConfigureAtmosphere(Light sun, Volume volume) { sunlight = sun; atmosphere = volume; }

        private void Awake()
        {
            if (renderPipeline == null) return;
            previous = QualitySettings.renderPipeline;
            runtimePipeline = Instantiate(renderPipeline); runtimePipeline.name = "Island runtime quality";
            runtimePipeline.hideFlags = HideFlags.DontSave;
            QualitySettings.renderPipeline = runtimePipeline;
            applied = true;
            if (atmosphere != null) { atmosphere.profile.TryGet(out bloom); atmosphere.enabled = false; }
            var initial = qualityMode;
            if (initial == IslandQualityMode.Automatic)
                initial = !Application.isMobilePlatform ? IslandQualityMode.Rich :
                    SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize <= 3072 ? IslandQualityMode.Economy : IslandQualityMode.Balanced;
            ApplyQuality(initial);
        }

        private IEnumerator Start()
        {
            for (int frame = 0; frame < 120 && controller != null && controller.Selected == null; frame++) yield return null;
            string songId = controller != null && controller.IslandStage != null
                ? controller.IslandStage.Definition?.SongId : null;
            if (string.IsNullOrEmpty(songId) || controller.Selected?.id == songId || controller.Songs == null) yield break;
            foreach (var song in controller.Songs)
                if (song.id == songId)
                {
                    controller.SelectSongForPreview(song.catalogId);
                    break;
                }
        }

        private void OnDestroy()
        {
            if (applied && QualitySettings.renderPipeline == runtimePipeline) QualitySettings.renderPipeline = previous;
            if (runtimePipeline != null) Destroy(runtimePipeline);
        }

        private void Update()
        {
            var camera = controller != null && controller.IslandStage != null ? controller.IslandStage.Camera : null;
            if (camera != budgetCamera) cameraData = camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
            if (camera != budgetCamera || camera != null && Mathf.Abs(camera.farClipPlane - DesiredFarClip) > .01f)
            { budgetCamera = camera; ApplyViewDistance(); }
            DisablePostProcessing();
            if (qualityMode != IslandQualityMode.Automatic || !Application.isMobilePlatform || controller == null || controller.Phase != ClonePhase.Playing) return;
            float dt = Time.unscaledDeltaTime; activeSeconds += dt;
            if (activeSeconds < 10 || ActiveQuality == IslandQualityMode.Economy) return;
            slowSeconds = Mathf.Clamp(slowSeconds + (dt > 1f / 30 ? dt : -dt * .5f), 0, 7);
            if (slowSeconds < 6) return;
            ApplyQuality(ActiveQuality == IslandQualityMode.Rich ? IslandQualityMode.Balanced : IslandQualityMode.Economy);
            slowSeconds = 0; activeSeconds = 0;
        }

        private void ApplyQuality(IslandQualityMode quality)
        {
            ActiveQuality = quality;
            bool low = quality == IslandQualityMode.Economy, rich = quality == IslandQualityMode.Rich;
            runtimePipeline.renderScale = rich ? 1 : low ? .8f : .9f;
            runtimePipeline.msaaSampleCount = rich ? 4 : 2;
            runtimePipeline.mainLightShadowmapResolution = low ? 1024 : 2048;
            runtimePipeline.shadowCascadeCount = rich ? 4 : low ? 1 : 2;
            runtimePipeline.shadowDistance = rich ? 80 : low ? 30 : 50;
            if (sunlight != null) sunlight.shadows = low ? LightShadows.Hard : LightShadows.Soft;
            if (bloom != null) { bloom.intensity.Override(0); bloom.active = false; }
            particleBudget = rich ? 256 : low ? 48 : 128;
            ApplyViewDistance();
        }

        private void ApplyViewDistance()
        {
            bool low = ActiveQuality == IslandQualityMode.Economy, rich = ActiveQuality == IslandQualityMode.Rich;
            if (controller != null && controller.IslandStage != null && controller.IslandStage.Camera != null)
                controller.IslandStage.Camera.farClipPlane = DesiredFarClip;
            RenderSettings.fogStartDistance = rich ? 80 : low ? 45 : 70;
            RenderSettings.fogEndDistance = rich ? 240 : low ? 108 : 185;
        }

        private float DesiredFarClip => ActiveQuality == IslandQualityMode.Rich ? 350 : ActiveQuality == IslandQualityMode.Economy ? 110 : 190;

        private void LateUpdate() => DisablePostProcessing();
        private void DisablePostProcessing()
        {
            if (cameraData != null && cameraData.renderPostProcessing) cameraData.renderPostProcessing = false;
            if (atmosphere != null && atmosphere.enabled) atmosphere.enabled = false;
        }

        private void OnApplicationPause(bool paused) { slowSeconds = 0; activeSeconds = 0; }
    }
}
