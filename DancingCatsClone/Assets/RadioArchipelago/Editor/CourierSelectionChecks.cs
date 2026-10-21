using System;
using System.Collections.Generic;
using RhythmGame.Services;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    public static class CourierSelectionChecks
    {
        public static void Run()
        {
            var catalog = ScriptableObject.CreateInstance<CourierCatalog>();
            catalog.entries = new[]
            {
                new CourierDefinition { id = "signal", displayName = "Тико" },
                new CourierDefinition { id = "sail", displayName = "Люмен" },
                new CourierDefinition { id = "orbit", displayName = "Нори" }
            };
            try
            {
                var firstStore = new MemoryPreferences();
                var secondStore = new MemoryPreferences();
                var first = new CourierSelection(catalog, firstStore);
                var second = new CourierSelection(catalog, secondStore);
                int changes = 0;
                first.Changed += () => changes++;
                first.Select(2);
                Require(first.SelectedId == "orbit" && second.SelectedId == "signal", "Avatar choice must belong to its own profile");
                Require(firstStore.GetInt(CourierSelection.PreferenceKey) == 2 && changes == 1, "A changed choice must save and notify once");
                first.Select(2);
                Require(changes == 1, "Reselecting the current courier must not rebuild its model");
                var restored = new CourierSelection(catalog, firstStore);
                Require(restored.SelectedId == "orbit", "Avatar choice must survive service recreation");
                firstStore.SetInt(CourierSelection.PreferenceKey, 999);
                Require(new CourierSelection(catalog, firstStore).SelectedId == "signal", "Invalid saved avatar indices must fall back safely");
                bool refused = false;
                try { first.Select(-1); } catch (ArgumentOutOfRangeException) { refused = true; }
                Require(refused && first.SelectedId == "orbit", "An invalid UI request must not corrupt the current choice");
                Debug.Log("RADIO_SELECTION PASS: independent profiles, persistence, change notification and invalid-save recovery");
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private sealed class MemoryPreferences : IPlayerPreferences
        {
            private readonly Dictionary<string, int> values = new Dictionary<string, int>();
            public int GetInt(string key, int defaultValue = 0) => values.TryGetValue(key, out int value) ? value : defaultValue;
            public void SetInt(string key, int value) => values[key] = value;
        }
    }
}
