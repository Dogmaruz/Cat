using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Compiles musical decisions to the recovered native tile contract, without changing playback or movement.</summary>
    public static class MusicalChartCompiler
    {
        public static MapTimeline Compile(MusicalScore score, AudioChartGenerator.Options options, int sampleRate, double duration)
        {
            if (score == null) throw new ArgumentNullException("score");
            options = options ?? new AudioChartGenerator.Options();
            AudioChartGenerator.ValidateOptions(score.id, options);
            if (score.schemaVersion != 1 || !Finite(duration) || duration < 5 || sampleRate < 1000 || score.sampleRate != sampleRate || !Finite(score.audioDuration) || Math.Abs(score.audioDuration - duration) > .1)
                throw new InvalidDataException("Musical score version, duration or decoded sample rate is invalid.");
            if (!Finite(score.bpm) || score.bpm <= 0 || score.cues == null || score.cues.Length == 0)
                throw new InvalidDataException("Musical score needs measured tempo and authored cues.");
            var beats = score.beats ?? Array.Empty<double>();
            if (beats.Any(b => !Finite(b) || b < 0 || b > duration) || !beats.SequenceEqual(beats.OrderBy(b => b).Distinct()))
                throw new InvalidDataException("Musical beats must be finite, unique and increasing inside the audio.");
            var map = new MapTimeline
            {
                schemaVersion = 1, id = score.id, sampleRate = sampleRate, mapAssetId = "custom:" + score.id,
                sourceMapBundle = "musical-score-v1", sourceMapBundleSha256 = score.sourceSha256, sourceMapUrl = "",
                forwardSpeed = options.forwardSpeed, delayTime = options.delayTime,
                catalog = new OriginalSongMetadata { id = options.catalogId, musicName = options.title, artName = options.artist, musicPath = score.id, scene = "level1", builtin = true, baseSpeed = options.forwardSpeed, difficultyType = 1, speedAdd = Array.Empty<float>() },
                audio = new OriginalAudioMetadata { assetId = "custom:" + score.id, name = score.id, category = "Music", frequency = sampleRate, declaredLengthSeconds = duration, rawSha256 = score.sourceSha256, files = Array.Empty<OriginalAudioFile>() },
                timing = new TimingDescription { gameTime = "audioTime + delayTime", audioTime = "gameTime - delayTime", eventGameTime = "sample / sampleRate + delayTime", warning = "Musical score annotations compile to native timing. Pitch estimates are evidence, not isolated vocals." },
                tempoSections = new[] { new TempoSection { sectionName = "Measured musical pulse", startSample = beats.Length > 0 ? (int)Math.Round(beats[0] * sampleRate) : 0, samplesPerBeat = 60 / score.bpm * sampleRate, beatsPerMeasure = 4, bStartNewMeasure = 1 } },
                normalizationNotes = new[] { "Generator: musical-score-v1; source SHA-256: " + score.sourceSha256, "Authored accents, sustained notes and pitch contours; no periodic filler, oscillation timer or runtime movement changes.", "Hold checkpoints use measured beat times; Follow uses native moving entry, stationary path markers and exit.", "Rest annotations add no scoring events. Outgoing arcs start at sustained support exits." }
            };
            var elements = new List<MapElement>();
            var segments = new List<MapSegment>();
            var start = Element(map, 0, "Start", -map.delayTime, 0, 0);
            elements.Add(start); segments.Add(Segment(map, start, start.audioTime));
            double finishTime = Math.Round(duration - Math.Max(1.1, 5 / options.forwardSpeed + .25), 6);
            double priorEnd = -map.delayTime;
            if (score.cues.Any(c => c == null)) throw new InvalidDataException("Musical cues cannot contain null entries.");
            var ordered = score.cues.OrderBy(c => c.time).ToArray();
            foreach (var cue in ordered)
            {
                double cueLimit = cue.kind == "Rest" ? duration : finishTime;
                if (!Finite(cue.time) || !Finite(cue.endTime) || cue.time < 0 || cue.time > cueLimit || cue.endTime < cue.time || cue.endTime > cueLimit || !Finite(cue.lane) || Math.Abs(cue.lane) > 1)
                    throw new InvalidDataException("Musical cue has invalid time, lane or overlaps the finish: " + cue.kind);
                if (cue.kind == "Rest") continue;
                if (cue.time < priorEnd - .00001) throw new InvalidDataException("Musical playable cues overlap: " + cue.time);
                if (cue.kind == "Follow")
                {
                    if (cue.endTime - cue.time < 1 || cue.endTime - cue.time > 6) throw new InvalidDataException("Follow contours must span one to six seconds.");
                    var sourcePoints = cue.points ?? Array.Empty<MusicalPoint>();
                    if (sourcePoints.Any(p => p == null)) throw new InvalidDataException("Follow points cannot contain null entries.");
                    var points = sourcePoints.OrderBy(p => p.time).ToList();
                    if (points.Count == 0 || Math.Abs(points[0].time - cue.time) > .00001) points.Insert(0, new MusicalPoint { time = cue.time, lane = cue.lane });
                    if (Math.Abs(points[points.Count - 1].time - cue.endTime) > .00001) points.Add(new MusicalPoint { time = cue.endTime, lane = points[points.Count - 1].lane });
                    if (points.Any(p => !Finite(p.time) || !Finite(p.lane) || Math.Abs(p.lane) > 1 || p.time < cue.time || p.time > cue.endTime) || points.Select(p => p.time).Distinct().Count() != points.Count)
                        throw new InvalidDataException("Follow points must be unique and inside their cue.");
                    if (points.Max(p => p.lane) - points.Min(p => p.lane) < .2f) throw new InvalidDataException("A Follow must describe meaningful lateral musical movement; use Hold for a stable note.");
                    // Conservative custom-authoring envelope measured from the recovered
                    // entry/mid pairs. This is not a claimed engine physics limit.
                    for (int p = 1; p < points.Count; p++)
                    {
                        double interval = points[p].time - points[p - 1].time;
                        if (interval < .0624 || Math.Abs(points[p].lane - points[p - 1].lane) > 12.01 * interval)
                            throw new InvalidDataException("Follow points exceed the custom authoring interval/lateral envelope.");
                    }
                    int first = elements.Count;
                    foreach (var point in points)
                    {
                        int id = elements.Count == first ? 4 : point == points[points.Count - 1] ? 6 : 5;
                        var element = Element(map, id, id == 4 ? "Follow" : id == 5 ? "FollowMid" : "FollowEnd", point.time, point.lane, elements.Count);
                        element.updateType = id == 4 ? 2 : 0;
                        if (id == 4) element.mfendPos = (float)((cue.endTime - cue.time) * map.forwardSpeed);
                        elements.Add(element);
                    }
                    var segment = Segment(map, elements[first], cue.endTime);
                    segment.elementIndices = Enumerable.Range(first, points.Count).ToArray();
                    segment.path = segment.elementIndices.Select(i => new ChartPoint { sourceIndex = i, kind = elements[i].kind, x = elements[i].pos.x, z = elements[i].pos.z, gameTime = elements[i].gameTime, audioTime = elements[i].audioTime, relativeTime = elements[i].audioTime - cue.time, updateType = elements[i].updateType }).ToArray();
                    segments.Add(segment);
                }
                else if (cue.kind == "Jump" || cue.kind == "BigJump" || cue.kind == "Hold")
                {
                    bool hold = cue.kind == "Hold";
                    if (hold && cue.endTime - cue.time < .18) throw new InvalidDataException("Hold needs a sustained interval.");
                    var element = Element(map, hold ? 2 : cue.kind == "BigJump" ? 1 : 0, cue.kind, cue.time, cue.lane, elements.Count);
                    if (hold)
                    {
                        element.se = new Vector2(0, -(float)((cue.endTime - cue.time) * map.forwardSpeed));
                        element.holdTicks = new[] { cue.time }.Concat(beats.Where(b => b > cue.time + .00001 && b < cue.endTime - .00001)).Concat(new[] { cue.endTime }).Select(t => new HoldTick { audioTime = t, gameTime = t + map.delayTime, z = (float)((t + map.delayTime) * map.forwardSpeed) }).ToArray();
                        element.ext = string.Join(",", element.holdTicks.Select(t => t.z.ToString("R", CultureInfo.InvariantCulture)).ToArray());
                    }
                    elements.Add(element); segments.Add(Segment(map, element, hold ? cue.endTime : cue.time));
                }
                else throw new InvalidDataException("Unknown musical cue kind: " + cue.kind);
                priorEnd = cue.kind == "Jump" || cue.kind == "BigJump" ? cue.time : cue.endTime;
            }
            if (priorEnd > finishTime - .078) throw new InvalidDataException("The last authored cue leaves no finish arc.");
            var finish = Element(map, 1, "FinishJump", finishTime, 0, elements.Count);
            elements.Add(finish); segments.Add(Segment(map, finish, finishTime));
            foreach (var segment in segments)
            {
                var outgoing = elements[segment.elementIndices.Last()];
                int next = outgoing.sourceIndex + 1;
                outgoing.jumpDistance = next < elements.Count ? elements[next].pos.z - segment.endZ : 5;
                segment.motion.jumpDistance = segment.kind == "Follow" ? 0 : outgoing.jumpDistance;
            }
            var events = new List<KoreographyEvent>();
            foreach (var element in elements.Skip(1))
            {
                AddEvent(events, map, element.audioTime, element.kind == "FinishJump" ? 100 : 0);
                foreach (var tick in element.holdTicks.Skip(1)) AddEvent(events, map, tick.audioTime, 0);
            }
            map.elements = elements.ToArray(); map.segments = segments.ToArray();
            map.events = events.OrderBy(e => e.mStartSample).ToArray();
            for (int i = 0; i < map.events.Length; i++) map.events[i].index = i;
            return map;
        }
        private static MapElement Element(MapTimeline map, int id, string kind, double time, float lane, int index)
        {
            return new MapElement { id = id, kind = kind, name = kind, sourceIndex = index, audioTime = time, gameTime = time + map.delayTime, audioSampleFromPosition = time * map.sampleRate, pos = new Vector3(lane, 0, (float)((time + map.delayTime) * map.forwardSpeed)), begin = 50, end = 5, se = new Vector2(.775f, -.775f), ext = "", holdTicks = Array.Empty<HoldTick>() };
        }
        private static MapSegment Segment(MapTimeline map, MapElement e, double end)
        {
            return new MapSegment { sourceIndex = e.sourceIndex, id = e.id, kind = e.kind, name = e.name, x = e.pos.x, z = e.pos.z, endZ = (float)((end + map.delayTime) * map.forwardSpeed), audioTime = e.audioTime, gameTime = e.gameTime, endAudioTime = end, endGameTime = end + map.delayTime, duration = end - e.audioTime, holdTicks = e.holdTicks, elementIndices = new[] { e.sourceIndex }, path = Array.Empty<ChartPoint>(), motion = new MotionParameters { updateType = e.updateType, begin = e.begin, end = e.end, se = e.se, ext = e.ext, mfendPos = e.mfendPos } };
        }
        private static void AddEvent(List<KoreographyEvent> events, MapTimeline map, double time, int command)
        {
            int sample = (int)Math.Round(time * map.sampleRate);
            double audio = (double)sample / map.sampleRate;
            events.Add(new KoreographyEvent { command = command, mStartSample = sample, mEndSample = sample, audioTime = audio, endAudioTime = audio, gameTime = audio + map.delayTime, endGameTime = audio + map.delayTime });
        }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
