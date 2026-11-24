using System;
using System.Collections.Generic;
using DancingCats.Clone;

namespace RadioArchipelago.UI
{
    /// <summary>The current public archipelago. Historical catalog entries stay available to saves and authoring.</summary>
    public static class RadioIslandMenuPolicy
    {
        public const int IslandCount = 4;
        public static string SongId(int index)
        {
            switch (index)
            {
                case 0: return "LumiLai";
                case 1: return "TikoTa";
                case 2: return "NoriNo";
                case 3: return "RadioTrio";
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < IslandCount; i++) if (string.Equals(SongId(i), id, StringComparison.Ordinal)) return i;
            return -1;
        }

        public static string IslandName(int index)
        {
            switch (index)
            {
                case 0: return "Изумрудная бездна";
                case 1: return "Коралловый оазис";
                case 2: return "Лунный резонанс";
                case 3: return "Янтарные ветра";
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static SongRecord[] VisibleSongs(SongRecord[] catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var result = new SongRecord[IslandCount];
            for (int i = 0; i < catalog.Length; i++)
            {
                SongRecord song = catalog[i];
                int index = song == null ? -1 : IndexOf(song.id);
                if (index < 0) continue;
                if (result[index] != null) throw new InvalidOperationException("Duplicate public island song: " + song.id);
                result[index] = song;
            }
            for (int i = 0; i < result.Length; i++)
                if (result[i] == null) throw new InvalidOperationException("Missing public island song: " + SongId(i));
            return result;
        }

        public static SongRecord ResolveSelection(SongRecord[] visible, string selectedId)
        {
            if (visible == null || visible.Length != IslandCount) throw new ArgumentException("The ordered public island songs are required.", nameof(visible));
            int index = IndexOf(selectedId);
            return visible[index < 0 ? 0 : index];
        }

        public static SongRecord Next(SongRecord[] visible, string selectedId, int direction, Func<string, bool> unlocked)
        {
            if (visible == null || visible.Length != IslandCount) throw new ArgumentException("The ordered public island songs are required.", nameof(visible));
            if (unlocked == null) throw new ArgumentNullException(nameof(unlocked));
            int index = IndexOf(selectedId);
            if (index < 0) return unlocked(visible[0].id) ? visible[0] : null;
            int stepDirection = direction < 0 ? -1 : 1;
            for (int step = 1; step < visible.Length; step++)
            {
                var next = visible[(index + stepDirection * step + visible.Length) % visible.Length];
                if (unlocked(next.id)) return next;
            }
            return null;
        }
    }
}
