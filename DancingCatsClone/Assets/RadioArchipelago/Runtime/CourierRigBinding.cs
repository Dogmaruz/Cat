using System;
using UnityEngine;

namespace RadioArchipelago
{
    /// <summary>Explicit presentation controls for imported skinned character rigs.</summary>
    [Serializable]
    public sealed class CourierRigBinding
    {
        public Transform visualPivot;
        public Transform headPivot;
        public Transform leftArm, rightArm;
        public Transform leftFoot, rightFoot;
        public Transform[] antennaChain = Array.Empty<Transform>();
        public Transform[] crestChain = Array.Empty<Transform>();
        public Transform[] scarfChain = Array.Empty<Transform>();
        public Transform[] blinkEyes = Array.Empty<Transform>();
        public SkinnedMeshRenderer blinkRenderer;
        public string blinkShapeName = "Blink";
    }
}
