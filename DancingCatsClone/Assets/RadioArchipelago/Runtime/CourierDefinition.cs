using System;
using UnityEngine;

namespace RadioArchipelago
{
    [Serializable]
    public sealed class CourierDefinition
    {
        public string id;
        public string displayName;
        public GameObject prefab;
        public Sprite icon;
    }
}
