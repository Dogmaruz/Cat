#if UNITY_EDITOR
using System;
using UnityEngine;

namespace DancingCats.Clone
{
    public sealed partial class OriginalPlayController
    {
        // Temporary diagnostics: never present in a player and no sample work without a subscriber.
        public struct EditorTimingSample
        {
            public OriginalPlayController game;
            public OriginalTile tile;
            public string stage, resource;
            public int frame, musicSamples, musicFrequency;
            public double realTime, dspTime, gameTime, clockOffset, dspStart, effectiveDelay;
            public float userDelay, rate, lapOffset, jumpDistance;
            public Vector3 actorPosition, jumpBegin;
        }

        public static event Action<EditorTimingSample> EditorTimingObserved;
        private OriginalTile _editorTimingTile;

        private void ObserveEditorTiming(string stage, OriginalTile tile, string resource = null)
        {
            var observe = EditorTimingObserved;
            if (observe == null) return;
            var sample = new EditorTimingSample
            {
                game = this, tile = tile, stage = stage, resource = resource,
                realTime = Time.realtimeSinceStartupAsDouble, dspTime = _audioClock.DspTime,
                frame = Time.frameCount, gameTime = GameTime, clockOffset = _clockOffset,
                dspStart = _dspStart, effectiveDelay = EffectiveDelay, userDelay = UserDelay,
                rate = _rate, lapOffset = _lapOffset, jumpDistance = _jumpDistance,
                actorPosition = Actor.position, jumpBegin = _jumpBegin,
                musicSamples = _music.timeSamples, musicFrequency = _music.clip != null ? _music.clip.frequency : 0
            };
            observe(sample);
        }
    }
}
#endif
