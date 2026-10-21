using System;

namespace DancingCats.Clone.Editor
{
    /// <summary>Portable musical annotation. Times are decoded-audio seconds; lanes are native X coordinates.</summary>
    [Serializable]
    public sealed class MusicalScore
    {
        public int schemaVersion = 1;
        public string id, sourceSha256;
        public double audioDuration;
        public int sampleRate;
        public double bpm;
        public double[] beats = Array.Empty<double>();
        public MusicalPhrase[] phrases = Array.Empty<MusicalPhrase>();
        public MusicalNote[] notes = Array.Empty<MusicalNote>();
        public MusicalCue[] cues = Array.Empty<MusicalCue>();
    }
    [Serializable]
    public sealed class MusicalPhrase
    {
        public double start, end, confidence;
        public string repeatKey, role;
    }
    [Serializable]
    public sealed class MusicalNote
    {
        public double start, end, midi, confidence, strength;
    }
    [Serializable]
    public sealed class MusicalCue
    {
        public double time, endTime, confidence;
        public string kind, reason, phraseId;
        public float lane;
        public MusicalPoint[] points = Array.Empty<MusicalPoint>();
    }
    [Serializable]
    public sealed class MusicalPoint
    {
        public double time;
        public float lane;
    }
}
