using System;
using System.Collections.Generic;
using System.Reflection;
using DancingCat;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CatChartValidation
{
    public static void Run()
    {
        var failures = new List<string>();
        Check("onsets create tiles at audible grid cells", AudibleCells, failures);
        Check("identical mono and stereo audio make identical charts", MonoStereo, failures);
        Check("source sample rate preserves the chart", SourceSampleRate, failures);
        Check("same seed makes the same chart", DeterministicSeed, failures);
        Check("dense chart stays ordered and reachable", DenseChart, failures);
        Check("sustained sections produce holds and follow variation", SustainedSections, failures);
        Check("partial final grid cell stays inside the clip", PartialFinalCell, failures);
        Check("first beat offset stays on the absolute audio timeline", AbsoluteOffset, failures);
        if (failures.Count != 0)
            throw new Exception("Cat chart validation failed: " + string.Join("; ", failures));
        Debug.Log("CAT_CHART_VALIDATION PASS: 8 checks.");
    }

    private static void AudibleCells()
    {
        using (var fixture = new Fixture(1000, 1, 4f))
        {
            fixture.Pulse(.5f, .05f);
            fixture.Pulse(1f, .05f);
            fixture.Pulse(2.5f, .05f);
            Analyze(fixture.Song);
            Require(HasTime(fixture.Song, .5f), "Missing the attack at 0.5 seconds");
            Require(HasTime(fixture.Song, 1f), "Missing the attack at 1 second");
            Require(HasTime(fixture.Song, 2.5f), "Missing the attack at 2.5 seconds");
            Require(!HasTime(fixture.Song, 1.5f), "Silence must not create an attack");
            ValidateChart(fixture.Song);
        }
    }

    private static void MonoStereo()
    {
        using (var mono = Pattern(1000, 1))
        using (var stereo = Pattern(1000, 2))
        {
            Analyze(mono.Song, 17);
            Analyze(stereo.Song, 17);
            SameChart(mono.Song, stereo.Song);
        }
    }

    private static void SourceSampleRate()
    {
        using (var low = Pattern(1000, 1))
        using (var high = Pattern(2000, 1))
        {
            Analyze(low.Song, 17);
            Analyze(high.Song, 17);
            SameChart(low.Song, high.Song);
        }
    }

    private static void DeterministicSeed()
    {
        using (var first = Pattern(1000, 1))
        using (var second = Pattern(1000, 1))
        {
            Analyze(first.Song, 91);
            Analyze(second.Song, 91);
            SameChart(first.Song, second.Song);
        }
    }

    private static void DenseChart()
    {
        using (var fixture = new Fixture(2000, 2, 8f))
        {
            fixture.Song.bpm = 360;
            fixture.Song.difficulty = 4;
            for (var index = 1; index < 90; index++)
                fixture.Pulse(index / 12f, .025f);
            Analyze(fixture.Song, 3);
            Require(fixture.Song.notes.Count >= 8, "A dense source should produce a playable chart");
            ValidateChart(fixture.Song);
        }
    }

    private static void SustainedSections()
    {
        using (var fixture = new Fixture(1000, 1, 10f))
        {
            fixture.Song.difficulty = 3;
            fixture.Pulse(.5f, 1f);
            fixture.Pulse(2.5f, 1f);
            fixture.Pulse(4.5f, 1f);
            fixture.Pulse(6.5f, 1f);
            fixture.Pulse(8.5f, 1f);
            Analyze(fixture.Song, 7);
            Require(fixture.Song.notes.Exists(note => note.kind == CatNoteKind.Hold && note.duration >= .5f),
                "Stable sustained audio should produce a hold");
            Require(fixture.Song.notes.Exists(note => note.kind == CatNoteKind.Slide && note.duration >= .5f),
                "Repeated sustained phrases should include occasional follow variation");
            ValidateChart(fixture.Song);
        }
    }

    private static void PartialFinalCell()
    {
        using (var fixture = new Fixture(1000, 2, 1.07f))
        {
            fixture.Pulse(1f, .07f);
            Analyze(fixture.Song);
            Require(HasTime(fixture.Song, 1f), "The final 70 ms of audible data must not disappear");
            Require(fixture.Song.duration <= fixture.Clip.length + .0001f, "Song exceeds its source clip");
            ValidateChart(fixture.Song);
        }
    }

    private static void AbsoluteOffset()
    {
        using (var fixture = new Fixture(1000, 1, 4f))
        {
            fixture.Song.offset = .125f;
            fixture.Song.duration = 2.2f;
            fixture.Pulse(.625f, .05f);
            fixture.Pulse(2.375f, .05f);
            Analyze(fixture.Song);
            Require(HasTime(fixture.Song, .625f), "Offset was omitted or applied twice");
            Require(!HasTime(fixture.Song, 2.375f), "Audio outside the requested window entered the chart");
            Near(2.2f, fixture.Song.duration, "Requested song window");
            ValidateChart(fixture.Song);
        }
    }

    private static Fixture Pattern(int rate, int channels)
    {
        var fixture = new Fixture(rate, channels, 6f);
        fixture.Song.difficulty = 3;
        fixture.Pulse(.5f, .05f);
        fixture.Pulse(1.25f, .05f);
        fixture.Pulse(2f, 1f);
        fixture.Pulse(4f, .05f);
        fixture.Pulse(5f, .05f);
        return fixture;
    }

    private static void Analyze(CatSong song, int seed = 0)
    {
        var type = typeof(CatSong).Assembly.GetType("DancingCat.CatChartAnalyzer");
        Require(type != null, "CatChartAnalyzer is not implemented yet");
        var method = type.GetMethod("Analyze", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(CatSong), typeof(int), typeof(float) }, null);
        Require(method != null, "Missing Analyze(CatSong, int, float) entry point");
        method.Invoke(null, new object[] { song, seed, 1.35f });
    }

    private static bool HasTime(CatSong song, float time)
    {
        return song.notes.Exists(note => Mathf.Abs(note.time - time) < .002f);
    }

    private static void ValidateChart(CatSong song)
    {
        Require(song.notes.Count > 0, "Chart is empty");
        Near(0, song.notes[0].time, "Initial time");
        Near(0, song.notes[0].lane, "Initial lane");
        for (var index = 0; index < song.notes.Count; index++)
        {
            var note = song.notes[index];
            Require(!float.IsNaN(note.time) && note.time >= 0, "Invalid note time");
            Require(note.duration >= 0, "Negative duration");
            Require(note.time + note.duration <= song.duration + .0001f, "Note extends past the song");
            Require(Mathf.Abs(note.lane) <= 1.5001f && Mathf.Abs(note.endLane) <= 1.5001f, "Lane out of bounds");
            if (note.kind == CatNoteKind.Short) Near(0, note.duration, "Short note duration");
            if (index == 0) continue;
            var previous = song.notes[index - 1];
            var gap = note.time - previous.time - previous.duration;
            Require(gap >= .1799f, "Overlapping note or insufficient landing gap at " + index);
            Require(Mathf.Abs(note.lane - previous.endLane) <= gap * 6f + .0001f,
                "Unreachable lateral jump at " + index);
        }
    }

    private static void SameChart(CatSong first, CatSong second)
    {
        Require(first.notes.Count == second.notes.Count, "Note counts differ");
        for (var index = 0; index < first.notes.Count; index++)
        {
            var left = first.notes[index];
            var right = second.notes[index];
            Near(left.time, right.time, "Time " + index);
            Near(left.duration, right.duration, "Duration " + index);
            Near(left.lane, right.lane, "Lane " + index);
            Near(left.endLane, right.endLane, "End lane " + index);
            Require(left.kind == right.kind && left.coin == right.coin, "Note features differ at " + index);
        }
    }

    private static void Check(string name, Action test, List<string> failures)
    {
        try
        {
            test();
            Debug.Log("CAT_CHART_VALIDATION PASS: " + name);
        }
        catch (Exception exception)
        {
            var cause = exception.InnerException ?? exception;
            var message = name + ": " + cause.Message;
            failures.Add(message);
            Debug.LogError("CAT_CHART_VALIDATION FAIL: " + message);
        }
    }

    private static void Near(float expected, float actual, string name)
    {
        Require(!float.IsNaN(actual) && Mathf.Abs(expected - actual) < .002f,
            name + ": expected " + expected + ", actual " + actual);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly AudioClip Clip;
        public readonly CatSong Song;
        private readonly float[] samples;
        private readonly int rate;
        private readonly int channels;

        public Fixture(int rate, int channels, float duration)
        {
            this.rate = rate;
            this.channels = channels;
            var frames = Mathf.RoundToInt(rate * duration);
            samples = new float[frames * channels];
            Clip = AudioClip.Create("CatChartSynthetic", frames, channels, rate, false);
            Song = ScriptableObject.CreateInstance<CatSong>();
            Song.clip = Clip;
            Song.bpm = 120;
            Song.duration = duration;
        }

        public void Pulse(float start, float duration)
        {
            var first = Mathf.RoundToInt(start * rate);
            var end = Mathf.Min(Clip.samples, Mathf.RoundToInt((start + duration) * rate));
            for (var frame = first; frame < end; frame++)
                for (var channel = 0; channel < channels; channel++)
                    samples[frame * channels + channel] = channel % 2 == 0 ? .8f : -.8f;
            Require(Clip.SetData(samples, 0), "Cannot populate synthetic clip");
        }

        public void Dispose()
        {
            Object.DestroyImmediate(Song);
            Object.DestroyImmediate(Clip);
        }
    }
}
