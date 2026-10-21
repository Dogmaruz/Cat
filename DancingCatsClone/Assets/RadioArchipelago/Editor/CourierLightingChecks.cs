using System;
using System.Linq;
using RadioArchipelago.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RadioArchipelago.Editor
{
    /// <summary>Reads the live presentation and its actual light, skin and material components.</summary>
    public static class CourierLightingChecks
    {
        public static string CheckMenu(Canvas canvas)
        {
            Require(canvas != null, "menu", "The live menu Canvas is missing.");
            var previews = canvas.GetComponentsInChildren<CourierMenuPreview>(false)
                .Where(preview => preview.isActiveAndEnabled).ToArray();
            Require(previews.Length == 1, "menu", "Expected one active courier menu preview; found " + previews.Length + ".");
            var camera = previews[0].PreviewCamera;
            Require(camera != null && camera.isActiveAndEnabled && camera.transform.parent != null,
                "menu", "The live courier preview camera and its presentation stage must be active.");
            return Run(camera.transform.parent, "menu", camera);
        }

        public static string CheckActor(Transform actor)
        {
            Require(actor != null && actor.gameObject.activeInHierarchy, "actor", "The actual gameplay actor is missing or inactive.");
            return Run(actor, "actor", null);
        }

        private static string Run(Transform presentation, string label, Camera camera)
        {
            const uint layer = CourierLightingRig.RenderingLayer;
            uint defined = RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue();
            Require((defined & layer) != 0, label, "The studio rendering layer is undefined and URP would remove its light mask before GPU upload: 0x" + defined.ToString("X"));
            var rigs = presentation.GetComponentsInChildren<CourierLightingRig>(false)
                .Where(rig => rig.isActiveAndEnabled).ToArray();
            Require(rigs.Length == 1, label, "Expected one active lighting rig in the live presentation; found " + rigs.Length + ".");
            var rig = rigs[0];

            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Require(pipeline != null && pipeline.useRenderingLayers, label,
                "The active URP asset must enable rendering layers for the local lights to remain isolated.");
            Require(pipeline.additionalLightsRenderingMode == LightRenderingMode.PerPixel && pipeline.maxAdditionalLightsCount >= 3,
                label, "The active URP asset must render at least three additional lights per object with per-pixel shading.");

            var lights = presentation.GetComponentsInChildren<Light>(false).Where(light => light.isActiveAndEnabled).ToArray();
            Require(lights.Length == 3, label, "Expected exactly three enabled local lights, without leftover preview lights; found " + lights.Length + ".");
            foreach (var light in lights)
            {
                Require(light.transform.IsChildOf(rig.transform), label, "Light is outside the presentation's lighting rig: " + light.name);
                Require(light.type == LightType.Point && Positive(light.intensity) && Positive(light.range), label,
                    "A local point light needs finite positive intensity and range: " + light.name);
                Require(light.shadows == LightShadows.None, label, "Courier studio lights must not render shadow maps: " + light.name);
                var data = light.GetComponent<UniversalAdditionalLightData>();
                Require(data != null, label, "A live light has no URP additional data: " + light.name);
                uint mask = data.renderingLayers;
                Require((mask & layer) != 0 && (mask & ~layer) == 0, label,
                    "A studio light must address only the courier rendering layer: " + light.name + " mask=0x" + mask.ToString("X"));
            }

            var probes = presentation.GetComponentsInChildren<ReflectionProbe>(false).Where(probe => probe.isActiveAndEnabled).ToArray();
            Require(probes.Length == 1, label, "Expected exactly one enabled custom reflection probe; found " + probes.Length + ".");
            var probe = probes[0];
            Require(probe.transform.IsChildOf(rig.transform) && probe.mode == ReflectionProbeMode.Custom, label,
                "Courier reflections must use a custom probe owned by the local rig, without a realtime capture probe.");
            Require(Positive(probe.intensity), label, "The custom reflection probe must contribute positive finite intensity.");
            var reflections = probe.customBakedTexture as Cubemap;
            Require(reflections != null && reflections.width == 128 && reflections.height == 128 && reflections.mipmapCount == 8,
                label, "The live custom probe needs its 128-pixel Cubemap with the full eight-mip chain.");
            Require(IsHdr(reflections.format), label, "The live reflection Cubemap must preserve HDR values; format=" + reflections.format);

            var motions = presentation.GetComponentsInChildren<CourierMotion>(false).Where(motion => motion.isActiveAndEnabled).ToArray();
            Require(motions.Length == 1, label, "Expected one actual active courier motion hierarchy; found " + motions.Length + ".");
            var motion = motions[0];
            Require(!motion.transform.IsChildOf(rig.transform), label, "The lighting rig and the animated subject must have independent hierarchies.");
            var skins = motion.GetComponentsInChildren<SkinnedMeshRenderer>(false).Where(skin => skin.enabled).ToArray();
            Require(skins.Length > 0, label, "The active courier has no enabled deformable skinned geometry.");
            var materials = skins.SelectMany(skin => skin.sharedMaterials).Distinct().ToArray();
            foreach (var skin in skins)
            {
                var mesh = skin.sharedMesh;
                Require(mesh != null && mesh.vertexCount >= 3 && skin.bones != null && skin.bones.Length > 0
                    && skin.bones.All(bone => bone != null && bone.IsChildOf(motion.transform))
                    && mesh.bindposes.Length == skin.bones.Length, label, "The lit courier needs a real bound skin: " + skin.name);
                Require(mesh.GetAllBoneWeights().Length > 0, label, "The lit mesh has no actual deformation weights: " + skin.name);
                Require((skin.renderingLayerMask & layer) != 0, label, "The actual courier skin does not receive its studio light layer: " + skin.name);
                Require(skin.sharedMaterials.Length >= mesh.subMeshCount && skin.sharedMaterials.Length > 0,
                    label, "The lit skin has missing material slots: " + skin.name);
                Require(skin.reflectionProbeUsage != ReflectionProbeUsage.Off && skin.probeAnchor != null
                    && skin.probeAnchor.IsChildOf(rig.transform) && probe.bounds.Contains(skin.probeAnchor.position), label,
                    "The actual courier skin must sample the local custom reflection volume: " + skin.name);
                foreach (var light in lights)
                    Require((light.cullingMask & (1 << skin.gameObject.layer)) != 0, label,
                        "A studio light's culling mask excludes the actual courier skin: " + light.name + " / " + skin.name);
                if (camera != null)
                    Require((camera.cullingMask & (1 << skin.gameObject.layer)) != 0, label,
                        "The live preview camera excludes the lit courier skin: " + skin.name);
            }
            foreach (var material in materials)
            {
                Require(material != null && material.shader != null && material.shader.name == "Universal Render Pipeline/Lit",
                    label, "The actual courier skin needs a valid URP Lit material.");
                foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                    Require(material.HasProperty(property) && material.GetTexture(property) != null,
                        label, "The active courier material is missing its texture " + property + ": " + material.name);
                Require(material.IsKeywordEnabled("_NORMALMAP") && material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),
                    label, "The active material must actually use its normal and metallic/smoothness maps: " + material.name);
                Require(material.HasProperty("_SpecularHighlights") && Mathf.Approximately(material.GetFloat("_SpecularHighlights"), 1)
                    && material.HasProperty("_EnvironmentReflections") && Mathf.Approximately(material.GetFloat("_EnvironmentReflections"), 1)
                    && !material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF") && !material.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"),
                    label, "The live material must enable specular highlights and environment reflections: " + material.name);
            }

            string sources = string.Join(", ", lights.Select(light => light.name + " " + light.intensity.ToString("F1") + "/r" + light.range.ToString("F1")));
            return "PASS " + label + ": one live rig; three shadowless point lights [" + sources + "]; isolated layer 0x"
                + layer.ToString("X") + "; custom HDR " + reflections.width + "px/" + reflections.mipmapCount + " mips (" + reflections.format
                + "); " + skins.Length + " weighted skin(s), " + materials.Length + " URP Lit material(s) with normal, metallic and specular reflections.";
        }

        private static bool IsHdr(TextureFormat format) => format == TextureFormat.RGBAHalf || format == TextureFormat.RGBAFloat
            || format == TextureFormat.RGB9e5Float || format == TextureFormat.BC6H;
        private static bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        private static void Require(bool value, string label, string message)
        {
            if (!value) throw new InvalidOperationException("RADIO_COURIER_LIGHTING FAIL (" + label + "): " + message);
        }
    }
}
