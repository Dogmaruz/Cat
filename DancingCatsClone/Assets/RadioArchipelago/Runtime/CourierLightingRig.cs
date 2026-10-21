using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RadioArchipelago
{
    /// <summary>Three local studio lights and a shared static reflection map, owned by one presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class CourierLightingRig : MonoBehaviour
    {
        public const uint RenderingLayer = 1u << 7;
        private bool initialized;

        public void Initialize(CourierLightingProfile profile, GameObject subject, Vector3 frontDirection, int cullingMask)
        {
            if (initialized) throw new InvalidOperationException("Courier lighting is already initialized.");
            if (profile == null || profile.reflections == null || subject == null)
                throw new ArgumentException("Courier lighting requires its profile, studio reflections and model.");
            initialized = true;
            var renderers = subject.GetComponentsInChildren<Renderer>(true);
            var body = CourierVisualBounds.MeasureBody(renderers);
            float height = Mathf.Max(.1f, body.size.y);
            transform.position = new Vector3(body.center.x, body.min.y, body.center.z);
            frontDirection.y = 0;
            transform.rotation = Quaternion.LookRotation(frontDirection.sqrMagnitude > .001f ? frontDirection.normalized : Vector3.forward);
            float intensityScale = height / Mathf.Max(.1f, profile.referenceHeight);
            intensityScale *= intensityScale;
            AddLight("Warm key", profile.keyPosition * height, profile.keyColor, profile.keyIntensity * intensityScale, height * 3.8f, cullingMask);
            AddLight("Sky fill", profile.fillPosition * height, profile.fillColor, profile.fillIntensity * intensityScale, height * 3.8f, cullingMask);
            AddLight("Soft rim", profile.rimPosition * height, profile.rimColor, profile.rimIntensity * intensityScale, height * 3.4f, cullingMask);

            var node = new GameObject("Studio reflections", typeof(ReflectionProbe));
            node.transform.SetParent(transform, false);
            var probe = node.GetComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.customBakedTexture = profile.reflections;
            probe.intensity = profile.reflectionIntensity;
            probe.size = Vector3.one * height * 5;
            probe.center = Vector3.up * height * .75f;
            probe.blendDistance = height * .15f;
            probe.importance = 100;
            probe.boxProjection = false;
            foreach (var renderer in renderers)
            {
                renderer.renderingLayerMask |= RenderingLayer;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
                renderer.probeAnchor = transform;
            }
        }

        private void AddLight(string name, Vector3 position, Color color, float intensity, float range, int mask)
        {
            var node = new GameObject(name, typeof(Light));
            node.transform.SetParent(transform, false); node.transform.localPosition = position;
            var light = node.GetComponent<Light>();
            light.type = LightType.Point; light.color = color; light.intensity = intensity;
            light.range = range; light.shadows = LightShadows.None; light.cullingMask = mask;
            light.GetUniversalAdditionalLightData().renderingLayers = RenderingLayer;
        }
    }
}
