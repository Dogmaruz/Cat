using System;
using UnityEngine;

namespace RadioArchipelago
{
    [CreateAssetMenu(menuName = "Radio Archipelago/Courier catalog")]
    public sealed class CourierCatalog : ScriptableObject
    {
        public CourierLightingProfile lighting;
        public CourierDefinition[] entries = Array.Empty<CourierDefinition>();

        public CourierDefinition Find(string id)
        {
            foreach (var entry in entries)
                if (entry != null && string.Equals(entry.id, id, StringComparison.Ordinal)) return entry;
            return null;
        }
    }
}
