using System;
using System.Collections.Generic;
using UnityEngine;

namespace DancingCat
{
    /// <summary>
    /// Offline half-beat chart suggestion. Review the result while listening before shipping a song.
    /// Holds use sustained source energy; occasional slides are a gameplay arrangement heuristic.
    /// This does not reproduce another game's private charting algorithm.
    /// </summary>
    public static class CatChartAnalyzer
    {
        public const float MinimumLandingGap = .18f;

        private struct Cell
        {
            public float time;
            public float energy;
            public float duty;
        }

        public static void Analyze(CatSong song, int seed = 0, float onsetThreshold = 1.35f)
        {
            if (song == null) throw new ArgumentNullException(nameof(song));
            if (song.clip == null) throw new ArgumentException("The song needs an AudioClip.", nameof(song));
            if (!(song.bpm > 0) || float.IsInfinity(song.bpm))
                throw new ArgumentException("BPM must be a finite positive number.", nameof(song));
            if (float.IsNaN(song.offset) || float.IsInfinity(song.offset))
                throw new ArgumentException("The first beat offset must be finite.", nameof(song));
            if (!(onsetThreshold > 1) || float.IsInfinity(onsetThreshold))
                throw new ArgumentException("Onset sensitivity ratio must be greater than one.", nameof(onsetThreshold));

            var clip = song.clip;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
                throw new InvalidOperationException("Cannot read audio samples. Set the clip's Load Type to Decompress On Load.");

            var duration = Mathf.Min(song.duration > 0 ? song.duration : clip.length, clip.length);
            var grid = 30.0 / song.bpm;
            var offset = Math.Max(0.0, song.offset);
            var count = Math.Max(0, (int)Math.Ceiling((duration - offset) / grid));
            var cells = new Cell[count];
            var totalEnergy = 0f;
            for (var index = 0; index < count; index++)
            {
                var time = offset + index * grid;
                var end = Math.Min(duration, offset + (index + 1) * grid);
                var firstFrame = (int)Math.Round(time * clip.frequency);
                var endFrame = Math.Min(clip.samples, (int)Math.Round(end * clip.frequency));
                cells[index] = ReadCell(samples, clip.channels, firstFrame, endFrame, (float)time);
                totalEnergy += cells[index].energy;
            }

            var floor = Mathf.Max(.008f, count == 0 ? 0 : totalEnergy / count * .18f);
            var notes = new List<CatNote> { new CatNote { time = 0, lane = 0, endLane = 0 } };
            var random = new System.Random(seed);
            var difficulty = Mathf.Clamp(song.difficulty, 1, 4);
            var sustainedCount = 0;
            var minimumHoldCells = Math.Max(3, (int)Math.Ceiling(.5 / grid));
            var maximumHoldCells = Math.Max(minimumHoldCells, (int)Math.Floor(2.0 / grid));

            for (var index = 0; index < count; index++)
            {
                var cell = cells[index];
                var previous = notes[notes.Count - 1];
                var gap = cell.time - previous.time - previous.duration;
                if (gap < MinimumLandingGap - .0001f || cell.energy < floor) continue;

                var steadyCells = 0;
                while (index + steadyCells < count && steadyCells < maximumHoldCells)
                {
                    var candidate = cells[index + steadyCells];
                    if (candidate.duty < .8f || candidate.energy < floor ||
                        candidate.energy > cell.energy * onsetThreshold ||
                        candidate.energy * onsetThreshold < cell.energy) break;
                    steadyCells++;
                }

                var before = index == 0 ? 0 : cells[index - 1].energy;
                var after = index + 1 == count ? 0 : cells[index + 1].energy;
                var onset = before < floor || cell.energy >= before * onsetThreshold ||
                    (cell.energy > before * 1.05f && cell.energy >= after) || cell.duty < .8f;
                if (steadyCells < minimumHoldCells && !onset) continue;

                var note = new CatNote
                {
                    time = cell.time,
                    lane = ChooseLane(random, previous.endLane, gap, difficulty),
                    kind = CatNoteKind.Short,
                    coin = random.NextDouble() < .3
                };
                note.endLane = note.lane;

                if (steadyCells >= minimumHoldCells)
                {
                    note.duration = Mathf.Min((float)(steadyCells * grid), duration - note.time);
                    note.kind = CatNoteKind.Hold;
                    for (float tick = (float)grid; tick < note.duration - .01f; tick += (float)grid)
                        note.checkpoints.Add(tick);
                    sustainedCount++;
                    // Follow-like movement is an authored arrangement rule, not an audio feature detector.
                    if (difficulty >= 2 && sustainedCount % 4 == 0)
                    {
                        note.kind = CatNoteKind.Slide;
                        note.endLane = ChooseLane(random, note.lane, note.duration, difficulty, true);
                        note.path.Add(new CatPathPoint { time = note.duration * .5f,
                            lane = Mathf.Clamp((note.lane + note.endLane) * .5f + (random.Next(2) == 0 ? -.35f : .35f), -1.5f, 1.5f) });
                    }
                    index += steadyCells - 1;
                }

                if (note.kind == CatNoteKind.Short && gap > .8f) note.kind = CatNoteKind.BigJump;

                notes.Add(note);
            }

            song.duration = duration;
            song.notes = notes;
        }

        private static Cell ReadCell(float[] samples, int channels, int firstFrame, int endFrame, float time)
        {
            double squares = 0;
            var activeFrames = 0;
            for (var frame = firstFrame; frame < endFrame; frame++)
            {
                var absolute = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    var sample = samples[frame * channels + channel];
                    squares += sample * sample;
                    absolute += Mathf.Abs(sample);
                }
                if (absolute / channels > .01f) activeFrames++;
            }

            var frames = endFrame - firstFrame;
            return new Cell
            {
                time = time,
                energy = frames > 0 ? (float)Math.Sqrt(squares / (frames * channels)) : 0,
                duty = frames > 0 ? (float)activeFrames / frames : 0
            };
        }

        private static float ChooseLane(System.Random random, float origin, float seconds, int difficulty,
            bool preferChange = false)
        {
            var maximumChange = Mathf.Min(1f + difficulty * .25f, seconds * (3f + difficulty * .6f));
            var candidates = new List<float>(7);
            for (var index = -3; index <= 3; index++)
            {
                var lane = index * .5f;
                if (Mathf.Abs(lane - origin) <= maximumChange + .0001f &&
                    (!preferChange || Mathf.Abs(lane - origin) > .01f)) candidates.Add(lane);
            }
            return candidates.Count == 0 ? origin : candidates[random.Next(candidates.Count)];
        }
    }
}
