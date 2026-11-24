using System;
using RhythmGame.Services;

namespace RadioArchipelago
{
    public sealed class CourierSelection
    {
        public const string PreferenceKey = "RadioArchipelago.v1.courier";
        private readonly CourierCatalog catalog;
        private readonly IPlayerPreferences preferences;
        public int SelectedIndex { get; private set; }
        public string SelectedId => SelectedDefinition.id;
        public CourierDefinition SelectedDefinition => catalog.entries[SelectedIndex];
        public CourierLightingProfile Lighting => catalog.lighting;
        public CourierCatalog Catalog => catalog;
        public event Action Changed;

        public CourierSelection(CourierCatalog catalog, IPlayerPreferences preferences)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            if (catalog.entries == null || catalog.entries.Length == 0)
                throw new ArgumentException("The courier catalog is empty.", nameof(catalog));
            int saved = preferences.GetInt(PreferenceKey, 0);
            SelectedIndex = saved >= 0 && saved < catalog.entries.Length ? saved : 0;
        }

        public void Select(int index)
        {
            if (index < 0 || index >= catalog.entries.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (index == SelectedIndex) return;
            SelectedIndex = index;
            preferences.SetInt(PreferenceKey, index);
            Changed?.Invoke();
        }

        public CourierEquipmentDefinition SelectedEquipment(CourierEquipmentKind kind)
        {
            string key = OutfitKey(SelectedId, kind);
            int token = preferences.GetInt(key, int.MinValue);
            if (token == 0) return null;
            if (token == int.MinValue)
            {
                string initial = kind == CourierEquipmentKind.Backpack ? SelectedDefinition.defaultBackpackId
                    : kind == CourierEquipmentKind.Scarf ? SelectedDefinition.defaultScarfId : null;
                return catalog.FindEquipment(initial, kind);
            }
            foreach (var entry in catalog.equipment)
                if (entry != null && entry.kind == kind && StableToken(entry.id) == token) return entry;
            return null;
        }

        public void Equip(CourierEquipmentKind kind, string id)
        {
            if ((int)kind < 0 || (int)kind > 2) throw new ArgumentOutOfRangeException(nameof(kind));
            var definition = string.IsNullOrEmpty(id) ? null : catalog.FindEquipment(id, kind);
            if (!string.IsNullOrEmpty(id) && definition == null) throw new ArgumentException("Unknown equipment: " + id, nameof(id));
            var selected = SelectedEquipment(kind);
            if (selected == definition) return;
            preferences.SetInt(OutfitKey(SelectedId, kind), definition == null ? 0 : StableToken(definition.id));
            Changed?.Invoke();
        }

        public void ApplyTo(UnityEngine.GameObject actor)
        {
            if (actor != null && actor.TryGetComponent<CourierWardrobeRig>(out var wardrobe))
                wardrobe.Apply(SelectedEquipment(CourierEquipmentKind.Backpack), SelectedEquipment(CourierEquipmentKind.Hat),
                    SelectedEquipment(CourierEquipmentKind.Scarf));
        }

        private static string OutfitKey(string courierId, CourierEquipmentKind kind) =>
            "RadioArchipelago.v1.outfit." + courierId + "." + kind;

        // Stable across catalog reordering and platform runtimes; never use string.GetHashCode for save data.
        public static int StableToken(string id)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char value in id) { hash ^= value; hash *= 16777619; }
                return (int)(hash % 2147483646) + 1;
            }
        }
    }
}
