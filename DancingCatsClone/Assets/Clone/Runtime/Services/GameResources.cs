using System;
using DancingCats.Clone;
using UnityEngine;

namespace RhythmGame.Services
{
    public interface IGameResources
    {
        T Load<T>(string path) where T : UnityEngine.Object;
    }

    public sealed class UnityGameResources : IGameResources
    {
        public T Load<T>(string path) where T : UnityEngine.Object => Resources.Load<T>(path);
    }

    public interface IAudioClock
    {
        double DspTime { get; }
    }

    public sealed class UnityAudioClock : IAudioClock
    {
        public double DspTime => AudioSettings.dspTime;
    }

    public interface ISongCatalog
    {
        SongRecord[] Songs { get; }
        MapTimeline LoadMap(string id);
        AudioClip LoadAudio(SongRecord song, bool preview = false);
        Sprite LoadIcon(SongRecord song);
    }

    public sealed class ResourceSongCatalog : ISongCatalog
    {
        private readonly IGameResources resources;
        public SongRecord[] Songs { get; }

        public ResourceSongCatalog(IGameResources resources)
        {
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            Songs = RecoveredCatalog.LoadFrom(resources);
        }

        // A run offsets its timeline in place; sharing deserialized maps would corrupt later runs.
        public MapTimeline LoadMap(string id) => RecoveredCatalog.LoadMapFrom(id, resources);

        public AudioClip LoadAudio(SongRecord song, bool preview = false)
        {
            if (song == null) throw new ArgumentNullException(nameof(song));
            string path = preview && !string.IsNullOrEmpty(song.previewAudioResource)
                ? song.previewAudioResource : song.audioResource;
            return resources.Load<AudioClip>(path);
        }

        public Sprite LoadIcon(SongRecord song) => song == null ? null : resources.Load<Sprite>("OriginalIcons/" + song.catalogId);
    }
}
