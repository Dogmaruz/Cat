using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RhythmGame.Services
{
    public sealed class SceneRenderSettings
    {
        private readonly Volume[] volumes;
        private Camera camera;
        private UniversalAdditionalCameraData cameraData;
        private bool? applied;

        public SceneRenderSettings(Volume[] volumes)
        {
            this.volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
        }

        public void Apply(Camera target, bool enabled)
        {
            bool cameraChanged = camera != target;
            if (cameraChanged)
            {
                camera = target;
                cameraData = target != null ? target.GetComponent<UniversalAdditionalCameraData>() : null;
            }
            if (!cameraChanged && applied == enabled) return;
            if (cameraData != null) cameraData.renderPostProcessing = enabled;
            foreach (var volume in volumes) if (volume != null) volume.enabled = enabled;
            applied = enabled;
        }
    }
}
