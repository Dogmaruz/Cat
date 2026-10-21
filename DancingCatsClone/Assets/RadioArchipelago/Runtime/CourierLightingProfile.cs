using UnityEngine;

namespace RadioArchipelago
{
    [CreateAssetMenu(menuName = "Radio Archipelago/Courier lighting")]
    public sealed class CourierLightingProfile : ScriptableObject
    {
        public Cubemap reflections;
        public Color keyColor = new Color(1, .96f, .89f);
        public Color fillColor = new Color(.88f, .95f, 1);
        public Color rimColor = new Color(1, .98f, .93f);
        public Vector3 keyPosition = new Vector3(-.95f, 1.52f, 1.52f);
        public Vector3 fillPosition = new Vector3(1.24f, .71f, 1.43f);
        public Vector3 rimPosition = new Vector3(.24f, 1.43f, -1.14f);
        [Min(.1f)] public float referenceHeight = 2.1f;
        [Min(0)] public float keyIntensity = 32, fillIntensity = 18, rimIntensity = 24;
        [Min(0)] public float reflectionIntensity = .7f;
    }
}
