using System;
using UnityEngine;

namespace RadioArchipelago
{
    public enum CourierEquipmentKind { Backpack, Hat, Scarf }

    [Serializable]
    public sealed class CourierEquipmentDefinition
    {
        public string id;
        public string displayName;
        public CourierEquipmentKind kind;
        public GameObject prefab;
        public Sprite icon;
    }

    [Serializable]
    public sealed class CourierEquipmentFit
    {
        public string equipmentId;
        public Vector3 position, euler;
        public float scale = 1;
    }

    [Serializable]
    public sealed class CourierBodySection
    {
        public float height, halfWidth, frontZ, backZ;
    }

    [Serializable]
    public sealed class CourierMountProfile
    {
        public float bodyWidth = 1, bodyDepth = .6f, bodyHeight = 2.1f, headWidth = 1, neckWidth = .8f;
        public float shoulderHeight = 1.25f, strapBottomHeight = .65f;
        public CourierBodySection[] bodySections = Array.Empty<CourierBodySection>();
        public Vector3[] leftHarnessPath = Array.Empty<Vector3>(), rightHarnessPath = Array.Empty<Vector3>();
        public Vector3[] leftHarnessNormals = Array.Empty<Vector3>(), rightHarnessNormals = Array.Empty<Vector3>();
        // All poses are expressed in the visual pivot's coordinate frame, before bone animation.
        public Vector3 backpackPosition, backpackEuler;
        public float backpackScale = 1;
        public Vector3 hatPosition, hatEuler = new Vector3(-12, 0, 0);
        public float hatScale = 1;
        public Vector3 scarfPosition, scarfEuler;
        public float scarfScale = 1;
        public CourierEquipmentFit[] equipmentFits = Array.Empty<CourierEquipmentFit>();

        public CourierEquipmentFit FindFit(string id)
        {
            foreach (var fit in equipmentFits)
                if (fit != null && string.Equals(fit.equipmentId, id, StringComparison.Ordinal)) return fit;
            return null;
        }
    }
}
