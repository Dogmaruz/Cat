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
    }
}
