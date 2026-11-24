using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Opt-in physical phrasing of measured ornaments. Keeps every scored attack on one native continuous support.</summary>
    public static class PcmPlayablePhraseGrouping
    {
        public const string Version = "playable-ornament-holds-v2-connected-caps";
        private const string ReportPrefix = "Playable ornament report: ";

        [Serializable] public sealed class Settings
        {
            public float worldDistanceScale = 1.18f, lateralScale = 1.8f, minimumSeparation = 1.40f;
            public float minimumHoldSeparation = 1.50f;
        }

        [Serializable] public sealed class Group
        {
            public int[] sourceIndices;
            public double[] onsetTimes;
            public double start, end, minimumPossiblePairDistance;
            public int resultSourceIndex;
            public float nativeEarlyHandoffChartZ, terminalCheckpointChartZ;
            public bool partOfPhysicalMerge;
        }

        [Serializable] public sealed class PhysicalMerge
        {
            public int resultSourceIndex;
            public int[] sourceSupportIndices;
            public int[] originalHoldIndices;
            public string[] originalHoldStartBits, originalHoldEndBits;
            public double[] sourceStarts, sourceEnds, checkpointTimes, originalHoldStarts, originalHoldEnds;
            public double gap, laneEnvelopeDistance, minimumGapVocalRms, localVocalPeak, minimumGapVocalRatio;
        }

        [Serializable] public sealed class Report
        {
            public string version = Version;
            public Settings settings;
            public int sourceSupports, resultSupports, sourceScoringEvents, resultScoringEvents, groupedAttacks;
            public bool scoredEventSamplesPreserved;
            public Group[] groups = Array.Empty<Group>();
            public PhysicalMerge[] physicalMerges = Array.Empty<PhysicalMerge>();
        }

        public sealed class Result { public MapTimeline Map; public Report Report; }

        /// <summary>Does not mutate the input. Existing Hold/Follow/BigJump decisions are protected.</summary>
        public static Result Apply(MapTimeline source, Settings settings)
            => Apply(source, settings, null);

        public static Result Apply(MapTimeline source, Settings settings, PcmVocalAnalysis.Evidence vocalEvidence)
        {
            if (source?.elements == null || source.segments == null || source.events == null)
                throw new ArgumentException("A compiled musical timeline is required.", nameof(source));
            ValidateSettings(settings);
            var map = CopyForAuthoring(source);
            var segments = map.segments.OrderBy(s => s.audioTime).ToArray();
            var grouped = new Dictionary<int, Group>();
            for (int first = 0; first < segments.Length;)
            {
                if (!Ordinary(map, segments[first])) { first++; continue; }
                int end = first + 1;
                while (end < segments.Length && Ordinary(map, segments[end])
                    && segments[end].audioTime - segments[end - 1].endAudioTime < .18 - .00001) end++;
                double smallest = double.PositiveInfinity;
                for (int i = first + 1; i < end; i++)
                    smallest = Math.Min(smallest, PossibleDistance(map.forwardSpeed,
                        segments[i].audioTime - segments[i - 1].endAudioTime, settings));
                if (end - first >= 2 && smallest < settings.minimumSeparation - .000001)
                {
                    grouped.Add(first, new Group
                    {
                        sourceIndices = segments.Skip(first).Take(end - first).Select(s => s.sourceIndex).ToArray(),
                        onsetTimes = segments.Skip(first).Take(end - first).Select(s => s.audioTime).ToArray(),
                        start = segments[first].audioTime, end = segments[end - 1].audioTime,
                        minimumPossiblePairDistance = smallest
                    });
                }
                first = end;
            }

            var keptElements = new List<MapElement>(); var keptSegments = new List<MapSegment>();
            var oldToNew = new Dictionary<int, int>();
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (grouped.TryGetValue(i, out Group group))
                {
                    var entry = map.elements[segment.sourceIndex];
                    var last = map.elements[group.sourceIndices.Last()];
                    entry.id = 2; entry.kind = entry.name = "Hold"; entry.updateType = entry.msLeft = 0;
                    entry.mstartPos = Vector3.zero; entry.mfendPos = 0;
                    entry.se = new Vector2(0, -(float)((group.end - group.start) * map.forwardSpeed));
                    entry.holdTicks = group.sourceIndices.Select(index => map.elements[index]).Select(e =>
                        new HoldTick { audioTime = e.audioTime, gameTime = e.gameTime, z = e.pos.z }).ToArray();
                    entry.ext = string.Join(",", entry.holdTicks.Select(t => t.z.ToString("R", CultureInfo.InvariantCulture)));
                    segment.id = 2; segment.kind = segment.name = "Hold";
                    segment.endAudioTime = last.audioTime; segment.endGameTime = last.gameTime;
                    segment.endZ = last.pos.z; segment.duration = group.end - group.start;
                    segment.holdTicks = entry.holdTicks; segment.path = Array.Empty<ChartPoint>();
                    segment.elementIndices = new[] { segment.sourceIndex };
                    segment.motion = new MotionParameters { updateType = 0, begin = entry.begin, end = entry.end,
                        se = entry.se, ext = entry.ext, mstartPos = Vector3.zero };
                    group.resultSourceIndex = keptElements.Count;
                    group.nativeEarlyHandoffChartZ = segment.endZ - .75f;
                    group.terminalCheckpointChartZ = segment.endZ;
                    i += group.sourceIndices.Length - 1;
                }
                foreach (int index in segment.elementIndices)
                {
                    oldToNew.Add(index, keptElements.Count);
                    keptElements.Add(map.elements[index]);
                }
                keptSegments.Add(segment);
            }
            var physicalMerges = JoinConnectedHoldCaps(source, map, keptElements, keptSegments, grouped.Values.ToArray(), settings, vocalEvidence);
            oldToNew.Clear();
            for (int i = 0; i < keptElements.Count; i++) oldToNew.Add(keptElements[i].sourceIndex, i);
            for (int i = 0; i < keptElements.Count; i++) keptElements[i].sourceIndex = i;
            foreach (var segment in keptSegments)
            {
                segment.sourceIndex = oldToNew[segment.sourceIndex];
                segment.elementIndices = segment.elementIndices.Select(index => oldToNew[index]).ToArray();
                foreach (var point in segment.path ?? Array.Empty<ChartPoint>()) point.sourceIndex = oldToNew[point.sourceIndex];
                var exit = keptElements[segment.elementIndices.Last()];
                exit.jumpDistance = exit.sourceIndex + 1 < keptElements.Count
                    ? keptElements[exit.sourceIndex + 1].pos.z - segment.endZ : 5;
                segment.motion.jumpDistance = segment.kind == "Follow" ? 0 : exit.jumpDistance;
            }
            map.elements = keptElements.ToArray(); map.segments = keptSegments.ToArray();
            foreach (var group in grouped.Values)
            {
                var support = map.segments.Single(s => s.kind == "Hold" && s.audioTime <= group.start && s.endAudioTime >= group.end);
                group.resultSourceIndex = support.sourceIndex;
                group.partOfPhysicalMerge = physicalMerges.Any(m => m.checkpointTimes.Contains(group.start));
                group.nativeEarlyHandoffChartZ = support.endZ - .75f;
                group.terminalCheckpointChartZ = support.endZ;
            }
            foreach (var merge in physicalMerges)
                merge.resultSourceIndex = map.segments.Single(s => s.audioTime <= merge.sourceStarts.First() && s.endAudioTime >= merge.sourceEnds.Last()).sourceIndex;
            var report = new Report
            {
                settings = settings, sourceSupports = source.segments.Length, resultSupports = map.segments.Length,
                sourceScoringEvents = source.events.Length, resultScoringEvents = map.events.Length,
                groups = grouped.Values.OrderBy(g => g.start).ToArray(),
                groupedAttacks = grouped.Values.Sum(g => g.onsetTimes.Length),
                physicalMerges = physicalMerges.ToArray(),
                scoredEventSamplesPreserved = EventsExactlyEqual(source.events, map.events)
            };
            if (!report.scoredEventSamplesPreserved) throw new InvalidDataException("Ornament grouping changed a measured scoring event.");
            map.normalizationNotes = (map.normalizationNotes ?? Array.Empty<string>())
                .Where(n => n == null || !n.StartsWith(ReportPrefix, StringComparison.Ordinal))
                .Concat(new[] { "Playable ornaments: " + Version + "; only rapid ordinary-attack runs containing a physically infeasible separate pair become one full-size native Hold. Every measured attack remains a checkpoint; no periodic beats or new musical offsets. This is gameplay phrasing, not a claim of a sustained vocal note.",
                    "Connected Hold caps: a rapid adjacent original Hold/new ornament may share one support only when the full caps cannot clear the existing lane budget and separated-vocal RMS confirms audible continuity through the gap. Original real-Hold intervals and every scored entry/tick remain covered; physical support topology changes are listed separately.",
                    ReportPrefix + JsonUtility.ToJson(report) }).ToArray();
            return new Result { Map = map, Report = report };
        }

        public static Report ReadReport(MapTimeline map)
        {
            string note = (map.normalizationNotes ?? Array.Empty<string>()).LastOrDefault(n => n != null && n.StartsWith(ReportPrefix, StringComparison.Ordinal));
            return note == null ? null : JsonUtility.FromJson<Report>(note.Substring(ReportPrefix.Length));
        }

        internal static double PossibleDistance(float speed, double gap, Settings settings)
        {
            double forward = gap * speed * settings.worldDistanceScale;
            double lateral = ChartRouteComposer.MaxLateralShift(gap) * settings.lateralScale;
            return Math.Sqrt(forward * forward + lateral * lateral);
        }

        private static List<PhysicalMerge> JoinConnectedHoldCaps(MapTimeline source, MapTimeline map,
            List<MapElement> elements, List<MapSegment> segments, Group[] ornaments, Settings settings, PcmVocalAnalysis.Evidence voice)
        {
            var merges = new List<PhysicalMerge>();
            for (int i = 1; i < segments.Count; i++)
            {
                var a = segments[i - 1]; var b = segments[i];
                if (a.kind != "Hold" || b.kind != "Hold" || !ornaments.Any(g =>
                    g.start >= a.audioTime && g.end <= a.endAudioTime || g.start >= b.audioTime && g.end <= b.endAudioTime)) continue;
                double gap = b.audioTime - a.endAudioTime;
                if (gap < 0 || gap >= .18 - .00001) continue;
                double maximumDistance = PossibleDistance(map.forwardSpeed, gap, settings);
                if (maximumDistance >= settings.minimumHoldSeparation - .000001
                    || !AudibleGap(voice, a.audioTime, a.endAudioTime, b.audioTime, b.endAudioTime, out double minimumRms, out double peak)) continue;
                var ticks = a.holdTicks.Concat(b.holdTicks).ToArray();
                if (!ticks.Select(t => t.audioTime).SequenceEqual(ticks.Select(t => t.audioTime).Distinct().OrderBy(t => t)))
                    throw new InvalidDataException("Connected Hold checkpoints must stay unique and in their measured order.");
                var originalHolds = source.segments.Where(s => s.kind == "Hold" && s.audioTime >= a.audioTime && s.endAudioTime <= b.endAudioTime).ToArray();
                merges.Add(new PhysicalMerge
                {
                    sourceSupportIndices = new[] { a.sourceIndex, b.sourceIndex }, sourceStarts = new[] { a.audioTime, b.audioTime },
                    sourceEnds = new[] { a.endAudioTime, b.endAudioTime }, checkpointTimes = ticks.Select(t => t.audioTime).ToArray(),
                    originalHoldStarts = originalHolds.Select(s => s.audioTime).ToArray(), originalHoldEnds = originalHolds.Select(s => s.endAudioTime).ToArray(),
                    originalHoldIndices = originalHolds.Select(s => s.sourceIndex).ToArray(),
                    originalHoldStartBits = originalHolds.Select(s => BitConverter.DoubleToInt64Bits(s.audioTime).ToString("X16")).ToArray(),
                    originalHoldEndBits = originalHolds.Select(s => BitConverter.DoubleToInt64Bits(s.endAudioTime).ToString("X16")).ToArray(),
                    gap = gap, laneEnvelopeDistance = maximumDistance, minimumGapVocalRms = minimumRms, localVocalPeak = peak, minimumGapVocalRatio = minimumRms / peak
                });
                var entry = map.elements[a.sourceIndex];
                a.endAudioTime = b.endAudioTime; a.endGameTime = b.endGameTime; a.endZ = b.endZ;
                a.duration = a.endAudioTime - a.audioTime; a.holdTicks = ticks;
                entry.holdTicks = ticks; entry.se = new Vector2(0, -(float)(a.duration * map.forwardSpeed));
                entry.ext = string.Join(",", ticks.Select(t => t.z.ToString("R", CultureInfo.InvariantCulture)));
                a.motion.se = entry.se; a.motion.ext = entry.ext;
                elements.Remove(map.elements[b.sourceIndex]); segments.RemoveAt(i); i--;
            }
            return merges;
        }

        private static bool AudibleGap(PcmVocalAnalysis.Evidence voice, double start, double gapStart, double gapEnd, double end,
            out double minimumRms, out double peak)
        {
            minimumRms = peak = 0;
            if (voice?.rms == null || voice.rmsStep <= 0) return false;
            int first = (int)Math.Floor(start / voice.rmsStep), last = (int)Math.Ceiling(end / voice.rmsStep);
            int gapFirst = (int)Math.Ceiling(gapStart / voice.rmsStep), gapLast = (int)Math.Ceiling(gapEnd / voice.rmsStep);
            if (first < 0 || last > voice.rms.Length || gapFirst >= gapLast || first >= last) return false;
            for (int i = first; i < last; i++) peak = Math.Max(peak, voice.rms[i]);
            if (peak < .005) return false;
            minimumRms = double.PositiveInfinity;
            for (int i = gapFirst; i < gapLast; i++) minimumRms = Math.Min(minimumRms, voice.rms[i]);
            return minimumRms >= peak * .10;
        }

        // A JSON round trip is unsuitable for an in-memory authoring copy: Unity's
        // serializer can change a double's low bits, including sample-derived event
        // times. Copy all mutable native records directly. Metadata is read-only in
        // this pass and is retained verbatim, including every provenance string.
        private static MapTimeline CopyForAuthoring(MapTimeline source)
        {
            return new MapTimeline
            {
                schemaVersion = source.schemaVersion, sampleRate = source.sampleRate, id = source.id,
                mapAssetId = source.mapAssetId, sourceMapBundle = source.sourceMapBundle,
                sourceMapBundleSha256 = source.sourceMapBundleSha256, sourceMapUrl = source.sourceMapUrl,
                forwardSpeed = source.forwardSpeed, delayTime = source.delayTime,
                catalog = source.catalog, audio = source.audio, timing = source.timing, tempoSections = source.tempoSections,
                normalizationNotes = source.normalizationNotes?.ToArray(),
                events = source.events.Select(e => new KoreographyEvent
                {
                    mStartSample = e.mStartSample, mEndSample = e.mEndSample, index = e.index, command = e.command,
                    audioTime = e.audioTime, endAudioTime = e.endAudioTime, gameTime = e.gameTime, endGameTime = e.endGameTime
                }).ToArray(),
                elements = source.elements.Select(e => new MapElement
                {
                    id = e.id, sourceIndex = e.sourceIndex, updateType = e.updateType, msLeft = e.msLeft,
                    name = e.name, kind = e.kind, ext = e.ext, pos = e.pos, mstartPos = e.mstartPos, se = e.se,
                    begin = e.begin, end = e.end, mfendPos = e.mfendPos, jumpDistance = e.jumpDistance,
                    gameTime = e.gameTime, audioTime = e.audioTime, audioSampleFromPosition = e.audioSampleFromPosition,
                    holdTicks = CopyTicks(e.holdTicks)
                }).ToArray(),
                segments = source.segments.Select(s => new MapSegment
                {
                    sourceIndex = s.sourceIndex, id = s.id, kind = s.kind, name = s.name, x = s.x, z = s.z, endZ = s.endZ,
                    gameTime = s.gameTime, audioTime = s.audioTime, endGameTime = s.endGameTime,
                    endAudioTime = s.endAudioTime, duration = s.duration,
                    elementIndices = s.elementIndices?.ToArray(), holdTicks = CopyTicks(s.holdTicks),
                    path = s.path?.Select(p => new ChartPoint
                    {
                        sourceIndex = p.sourceIndex, updateType = p.updateType, msLeft = p.msLeft, kind = p.kind,
                        x = p.x, z = p.z, gameTime = p.gameTime, audioTime = p.audioTime, relativeTime = p.relativeTime
                    }).ToArray(),
                    motion = s.motion == null ? null : new MotionParameters
                    {
                        updateType = s.motion.updateType, msLeft = s.motion.msLeft, begin = s.motion.begin,
                        end = s.motion.end, mfendPos = s.motion.mfendPos, jumpDistance = s.motion.jumpDistance,
                        mstartPos = s.motion.mstartPos, se = s.motion.se, ext = s.motion.ext
                    }
                }).ToArray()
            };
        }

        private static HoldTick[] CopyTicks(HoldTick[] ticks)
            => ticks?.Select(t => new HoldTick { z = t.z, gameTime = t.gameTime, audioTime = t.audioTime }).ToArray();

        internal static bool EventsExactlyEqual(KoreographyEvent[] before, KoreographyEvent[] after)
        {
            if (before.Length != after.Length) return false;
            for (int i = 0; i < before.Length; i++)
            {
                var a = before[i]; var b = after[i];
                if (a.mStartSample != b.mStartSample || a.mEndSample != b.mEndSample || a.index != b.index || a.command != b.command
                    || !SameDouble(a.audioTime, b.audioTime) || !SameDouble(a.endAudioTime, b.endAudioTime)
                    || !SameDouble(a.gameTime, b.gameTime) || !SameDouble(a.endGameTime, b.endGameTime)) return false;
            }
            return true;
        }

        private static bool SameDouble(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        private static bool Ordinary(MapTimeline map, MapSegment s)
            => s.kind == "Jump" && s.elementIndices.Length == 1 && map.elements[s.sourceIndex].id == 0;

        private static void ValidateSettings(Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            foreach (float value in new[] { settings.worldDistanceScale, settings.lateralScale, settings.minimumSeparation, settings.minimumHoldSeparation })
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                    throw new ArgumentException("Playable ornament geometry must be finite and positive.", nameof(settings));
        }
    }
}
