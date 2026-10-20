using System;
using System.IO;
using UnityEngine;

namespace DancingCats.Clone
{
    [Serializable]
    public sealed class SongRecord
    {
        public string id, title, artist, audioResource, timelineResource, previewAudioResource;
        public string audioFile, audioAssetId, timeline;
        public int catalogId, sampleRate, elementCount, segmentCount, eventCount, holdTickCount, followPathPointCount;
        public float forwardSpeed, delayTime;
        public double audioLengthSeconds;
        public MapSegment[] finish;
        public OriginalSongMetadata catalog;
        public AudioClip LoadAudio() { return Resources.Load<AudioClip>(audioResource); }
    }

    [Serializable]
    public sealed class MapTimeline
    {
        public int schemaVersion, sampleRate;
        public string id, mapAssetId, sourceMapBundle, sourceMapBundleSha256, sourceMapUrl;
        public float forwardSpeed, delayTime;
        public OriginalSongMetadata catalog;
        public OriginalAudioMetadata audio;
        public TimingDescription timing;
        public TempoSection[] tempoSections;
        public MapElement[] elements;
        public MapSegment[] segments;
        public KoreographyEvent[] events;
        public string[] normalizationNotes;
    }

    [Serializable]
    public sealed class MapElement
    {
        public int id, sourceIndex, updateType, msLeft;
        public string name, kind, ext;
        public Vector3 pos, mstartPos;
        public Vector2 se;
        public float begin, end, mfendPos, jumpDistance;
        public double gameTime, audioTime, audioSampleFromPosition;
        public HoldTick[] holdTicks;
    }

    [Serializable]
    public sealed class MapSegment
    {
        public int sourceIndex, id;
        public string kind, name;
        public float x, z, endZ;
        public double gameTime, audioTime, endGameTime, endAudioTime, duration;
        public int[] elementIndices;
        public HoldTick[] holdTicks;
        public ChartPoint[] path;
        public MotionParameters motion;
    }

    [Serializable]
    public sealed class ChartPoint
    {
        public int sourceIndex, updateType, msLeft;
        public string kind;
        public float x, z;
        public double gameTime, audioTime, relativeTime;
    }

    [Serializable]
    public sealed class HoldTick
    {
        public float z;
        public double gameTime, audioTime;
    }

    [Serializable]
    public sealed class MotionParameters
    {
        public int updateType, msLeft;
        public float begin, end, mfendPos, jumpDistance;
        public Vector3 mstartPos;
        public Vector2 se;
        public string ext;
    }

    [Serializable]
    public sealed class KoreographyEvent
    {
        public int mStartSample, mEndSample, index, command;
        public double audioTime, endAudioTime, gameTime, endGameTime;
    }

    [Serializable]
    public sealed class OriginalSongMetadata
    {
        public string musicName, musicPath, headSprite, scene, artName;
        public int id, endOrder, labelType, group, gold, musicUnlockType, difficultyType, musicType, ad;
        public bool builtin, disable;
        public float baseSpeed;
        public float[] speedAdd;
    }

    [Serializable]
    public sealed class TimingDescription
    {
        public string gameTime, audioTime, eventGameTime, warning;
    }

    [Serializable]
    public sealed class TempoSection
    {
        public string sectionName;
        public int startSample, beatsPerMeasure, bStartNewMeasure;
        public double samplesPerBeat;
    }

    [Serializable]
    public sealed class OriginalAudioMetadata
    {
        public string assetId, name, category, sourceContainer, sourceContainerSha256, rawFile, rawSha256;
        public long pathId, rawBytes;
        public int frequency, channels, bitsPerSample, compressionFormat;
        public double declaredLengthSeconds;
        public OriginalAudioResource resource;
        public OriginalAudioFile[] files;
    }

    [Serializable]
    public sealed class OriginalAudioResource
    {
        public string m_Source;
        public long m_Offset, m_Size;
    }

    [Serializable]
    public sealed class OriginalAudioFile
    {
        public string file, sha256;
        public int bytes, frames, frequency, channels;
        public double lengthSeconds;
    }

    public static class RecoveredCatalog
    {
        [Serializable] private sealed class JsonArray<T> { public T[] items; }

        public static SongRecord[] Load()
        {
            var json = RequireText("OriginalData/songs").text;
            var songs = FromJsonArray<SongRecord>(json);
            if (songs == null || songs.Length == 0) throw new InvalidDataException("Original song catalog is empty.");
            return songs;
        }

        public static MapTimeline LoadMap(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || Array.Exists(id.ToCharArray(), c => !char.IsLetterOrDigit(c) && c != '_' && c != '-'))
                throw new ArgumentException("Invalid recovered map identifier.", "id");
            var map = JsonUtility.FromJson<MapTimeline>(RequireText("OriginalData/Maps/" + id + ".timeline").text);
            if (map == null || map.id != id || map.schemaVersion != 1 || map.elements == null || map.segments == null || map.events == null)
                throw new InvalidDataException("Invalid recovered map: " + id);
            return map;
        }

        public static T[] FromJsonArray<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("[", StringComparison.Ordinal))
                throw new ArgumentException("A JSON root array is required.", "json");
            return JsonUtility.FromJson<JsonArray<T>>("{\"items\":" + json + "}").items;
        }

        private static TextAsset RequireText(string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null) throw new FileNotFoundException("Original data resource is missing: " + resource);
            return asset;
        }
    }
}
