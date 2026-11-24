using System;
using UnityEngine;

namespace RadioArchipelago
{
    /// <summary>Owns the three equipped objects; all fitting happens only when an outfit changes.</summary>
    [DisallowMultipleComponent]
    public sealed class CourierWardrobeRig : MonoBehaviour
    {
        public CourierMotion motion;
        public CourierRigBinding bodyRig;
        public Transform bodyAnchor, headAnchor;
        public CourierMountProfile profile = new CourierMountProfile();
        private readonly GameObject[] equipped = new GameObject[3];
        private GameObject bodyHarness, harnessSource;
        public GameObject BodyHarness => bodyHarness;
        public GameObject Equipped(CourierEquipmentKind kind) => equipped[(int)kind];

        public void Configure(CourierMotion presentation, CourierRigBinding binding, Transform body, Transform head)
        {
            motion = presentation; bodyRig = binding; bodyAnchor = body; headAnchor = head;
        }

        public void Apply(CourierEquipmentDefinition backpack, CourierEquipmentDefinition hat, CourierEquipmentDefinition scarf)
        {
            if (motion == null || bodyRig == null || bodyRig.visualPivot == null)
                throw new InvalidOperationException("The modular courier has no authored presentation binding.");
            // Remove the previous bindings before destroying their nodes.
            bodyRig.antennaYawPivot = null;
            bodyRig.antennaChain = Array.Empty<Transform>();
            bodyRig.scarfChain = Array.Empty<Transform>();
            motion.ConfigureRig(bodyRig);
            for (int i = 0; i < equipped.Length; i++)
            {
                if (equipped[i] == null) continue;
                equipped[i].SetActive(false);
                if (Application.isPlaying) Destroy(equipped[i]); else DestroyImmediate(equipped[i]);
                equipped[i] = null;
            }
            var pack = Attach(backpack, CourierEquipmentKind.Backpack, bodyAnchor,
                profile.backpackPosition, profile.backpackEuler, profile.backpackScale);
            Attach(hat, CourierEquipmentKind.Hat, headAnchor, profile.hatPosition, profile.hatEuler, profile.hatScale);
            var cloth = Attach(scarf, CourierEquipmentKind.Scarf, headAnchor, profile.scarfPosition, profile.scarfEuler, profile.scarfScale);
            ApplyBodyHarness(pack != null);
            if (pack != null)
            {
                float headTop = profile.bodyHeight;
                var equippedHat = equipped[(int)CourierEquipmentKind.Hat];
                if (equippedHat != null)
                    foreach (var renderer in equippedHat.GetComponentsInChildren<Renderer>(false))
                    {
                        var bounds = renderer.bounds;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            var point = bounds.center + Vector3.Scale(bounds.extents,
                                new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                            headTop = Mathf.Max(headTop, bodyRig.visualPivot.InverseTransformPoint(point).y);
                        }
                    }
                pack.FitTop(headTop + .12f, bodyRig.visualPivot);
                bodyRig.antennaYawPivot = pack.topPivot;
                bodyRig.antennaSpinAxis = pack.spinAxis.sqrMagnitude > .001f ? pack.spinAxis.normalized : Vector3.up;
                bodyRig.antennaChain = pack.antennaChain != null && pack.antennaChain.Length > 0 ? pack.antennaChain
                    : pack.topRotor != null ? new[] { pack.topRotor } : Array.Empty<Transform>();
                bodyRig.antennaBendLimit = pack.antennaChain != null && pack.antennaChain.Length > 0 ? 40 : 15;
            }
            if (cloth != null) bodyRig.scarfChain = cloth.scarfChain;
            motion.ConfigureRig(bodyRig);
        }

        private void ApplyBodyHarness(bool visible)
        {
            if (harnessSource != profile.bodyHarnessPrefab)
            {
                if (bodyHarness != null)
                {
                    bodyHarness.SetActive(false);
                    if (Application.isPlaying) Destroy(bodyHarness); else DestroyImmediate(bodyHarness);
                }
                bodyHarness = null;
                harnessSource = profile.bodyHarnessPrefab;
            }
            if (visible && bodyHarness == null && harnessSource != null)
            {
                bodyHarness = Instantiate(harnessSource, bodyRig.visualPivot, false);
                bodyHarness.name = "Tailored body harness";
                if (bodyAnchor != null) bodyHarness.transform.SetParent(bodyAnchor, true);
                foreach (var node in bodyHarness.GetComponentsInChildren<Transform>(true))
                    node.gameObject.layer = gameObject.layer;
            }
            if (bodyHarness != null) bodyHarness.SetActive(visible);
        }

        private CourierEquipmentRig Attach(CourierEquipmentDefinition definition, CourierEquipmentKind kind,
            Transform anchor, Vector3 position, Vector3 euler, float scale)
        {
            if (definition == null || definition.prefab == null) return null;
            if (definition.kind != kind) throw new InvalidOperationException("Equipment kind does not match its socket.");
            var fit = profile.FindFit(definition.id);
            if (fit != null) { position = fit.position; euler = fit.euler; scale = fit.scale; }
            var prefab = kind == CourierEquipmentKind.Hat && fit != null && fit.prefabOverride != null
                ? fit.prefabOverride : definition.prefab;
            var instance = Instantiate(prefab, bodyRig.visualPivot, false);
            instance.name = "Equipped_" + definition.id;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(euler);
            instance.transform.localScale *= Mathf.Max(.01f, scale);
            if (kind == CourierEquipmentKind.Hat && fit != null)
                instance.transform.localScale = Vector3.Scale(instance.transform.localScale, HatAxisScale(fit.axisScale));
            if (anchor != null) instance.transform.SetParent(anchor, true);
            foreach (var node in instance.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = gameObject.layer;
            equipped[(int)kind] = instance;
            return instance.GetComponent<CourierEquipmentRig>();
        }

        private static Vector3 HatAxisScale(Vector3 value)
        {
            // Missing legacy data may deserialize as all zero. Authoring rejects
            // invalid axes; a damaged saved fit still keeps the original hat size.
            if (!(value.x > 0 && value.y > 0 && value.z > 0) ||
                float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z))
                return Vector3.one;
            return value;
        }
    }
}
