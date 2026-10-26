using System;
using System.Collections.Generic;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>Conservative moving supports from independently measured recurring melodic figures.</summary>
    public static class PcmMelodicFollowAnalysis
    {
        private sealed class Cell
        {
            public PcmInstrumentalEvidence.NoteEvent first, second;
            public double end, interval;
        }

        // The caller verifies the evidence's PCM, model, schema and digest before this
        // musical policy runs. Legacy supports retain priority independently of it.
        public static MusicalCue[] Analyze(PcmInstrumentalEvidence.Evidence evidence, MusicalCue[] protectedSupports, double beatPeriod = 0)
        {
            if (evidence?.events == null || evidence.events.Length < 3) return Array.Empty<MusicalCue>();
            var notes = evidence.events.Where(Qualifies).OrderBy(n => n.time).ThenBy(n => n.midi).ToArray();
            if (notes.Length < 6) return Array.Empty<MusicalCue>();
            var pitches = notes.Select(n => (double)n.midi).OrderBy(p => p).ToArray();
            double upperRegister = Quantile(pitches, .8);
            var attacks = evidence.events.Where(n => Get(n, "sourceAttack") >= .5).Select(n => n.time).Distinct().OrderBy(t => t).ToArray();
            var cells = new List<Cell>();
            foreach (var first in notes)
            {
                if (first.midi < upperRegister || Get(first, "sourceAttack") < .5) continue;
                foreach (var candidate in notes)
                {
                    double interval = candidate.time - first.time;
                    if (interval < .10 - 1e-7 || interval > .30 + 1e-7 || first.midi - candidate.midi < 2 || first.midi - candidate.midi > 5
                        || first.endTime < candidate.time - .10 - 1e-7 || Get(candidate, "harmonicNovelty") < .025
                        || ((Get(first, "harmonicNovelty") < .02 || Get(candidate, "sourceAttack") < .5) && first.midi - candidate.midi > 2)
                        || HasStrongerSemitone(candidate, evidence.events)) continue;
                    double end = End(candidate, attacks), duration = end - first.time;
                    if (duration < .25 - 1e-7 || duration > .7 + 1e-7) continue;
                var cell = new Cell { first = first, second = candidate, interval = interval, end = end };
                int at = Nearest(attacks, first.time), left = at, right = at;
                double connectedGap = Math.Max(.205, 1.2 * cell.interval);
                while (left > 0 && attacks[left] - attacks[left - 1] <= connectedGap + 1e-7) left--;
                while (right + 1 < attacks.Length && attacks[right + 1] - attacks[right] <= connectedGap + 1e-7) right++;
                // A pair embedded in an uninterrupted longer run is not truncated
                // into a short support. Its larger phrasing remains unresolved.
                if (attacks[right] - attacks[left] > 8 * cell.interval + 1e-7) continue;
                cells.Add(cell);
                }
            }
            var accepted = new List<Cell>();
            foreach (var cell in cells)
            {
                var peers = cells.Where(c => c.first.midi == cell.first.midi && c.second.midi == cell.second.midi
                    && Math.Abs(c.interval - cell.interval) < .04 + 1e-7).ToArray();
                int index = Array.IndexOf(peers, cell); bool recurrent = false;
                for (int first = Math.Max(0, index - 2); first <= Math.Min(index, peers.Length - 3); first++)
                {
                    double before = peers[first + 1].first.time - peers[first].first.time;
                    double after = peers[first + 2].first.time - peers[first + 1].first.time;
                    if (before >= 2.7 * cell.interval - 1e-7 && before <= 5 * cell.interval + 1e-7 && Math.Abs(before - after) <= .08 + 1e-7)
                        recurrent = true;
                }
                if (!recurrent || (protectedSupports ?? Array.Empty<MusicalCue>()).Any(s => s != null && (s.kind == "Hold" || s.kind == "Follow" || s.kind == "BigJump")
                    && cell.first.time < s.endTime + .078 && cell.end > s.time - .078)) continue;
                accepted.Add(cell);
            }
            var result = new List<MusicalCue>();
            foreach (var cell in accepted.OrderBy(c => c.first.time).ThenByDescending(c => Get(c.second, "neuralOnset") * Get(c.second, "harmonicBody")))
            {
                if (result.Count > 0 && cell.first.time < result[result.Count - 1].endTime + .078) continue;
                double start = cell.first.time, end = cell.end;
                float direction = (result.Count & 1) == 0 ? -1 : 1;
                var points = new List<MusicalPoint> { new MusicalPoint { time = start, lane = 0 } };
                // A brief two-note glide needs entry and exit, not an extra
                // scoring checkpoint on every internal pitch articulation.
                if (end - start >= .5 && cell.second.time - start >= .0624 && end - cell.second.time >= .0624)
                    points.Add(new MusicalPoint { time = cell.second.time, lane = (float)(direction * .8 * (cell.second.time - start) / (end - start)) });
                points.Add(new MusicalPoint { time = end, lane = direction * .8f });
                result.Add(new MusicalCue { kind = "Follow", time = start, endTime = end, lane = 0, points = points.ToArray(),
                    confidence = .5 * (Math.Min(Get(cell.first, "neuralOnset"), Get(cell.second, "neuralOnset"))
                        + Math.Min(1, Math.Min(Get(cell.first, "harmonicBody"), Get(cell.second, "harmonicBody")))),
                    phraseId = "mixed-recurrent-cell-" + result.Count,
                    reason = "Recurring source-confirmed upper two-note response; measured harmonic release or actual subsequent articulation ends the short support. Lateral direction is independent gameplay choreography." });
            }
            foreach (var cue in LongContours(evidence, attacks).OrderBy(c => c.time))
            {
                if ((protectedSupports ?? Array.Empty<MusicalCue>()).Concat(result).Any(s => s != null
                    && (s.kind == "Hold" || s.kind == "Follow" || s.kind == "BigJump")
                    && cue.time < s.endTime + .078 && cue.endTime > s.time - .078)) continue;
                result.Add(cue);
            }
            foreach (var cue in ChordalResponses(evidence).OrderBy(c => c.time))
            {
                if ((protectedSupports ?? Array.Empty<MusicalCue>()).Concat(result).Any(s => s != null
                    && (s.kind == "Hold" || s.kind == "Follow" || s.kind == "BigJump")
                    && cue.time < s.endTime + .078 && cue.endTime > s.time - .078)) continue;
                result.Add(cue);
            }
            result = result.OrderBy(c => c.time).ToList();
            FitNewPathCorridors(result, protectedSupports ?? Array.Empty<MusicalCue>());
            return result.ToArray();
        }

        private sealed class VoicedResponse
        {
            public double time, end, interval, duration, confidence;
            public int delta;
        }

        /// <summary>Recurring foreground rising bodies, measured before optional flight reservation.</summary>
        public static MusicalCue[] AnalyzeVoicedResponses(PcmInstrumentalEvidence.Evidence evidence,
            PcmVocalAnalysis.Evidence voice, MusicalCue[] protectedSupports)
        {
            if (evidence?.events == null || voice?.pitchTimes == null || voice.pitchTimes.Length < 2
                || voice.pitch == null || voice.pitchConfidence == null) return Array.Empty<MusicalCue>();
            var frameSteps = voice.pitchTimes.Zip(voice.pitchTimes.Skip(1), (a, b) => b - a).OrderBy(step => step).ToArray();
            double step = frameSteps[frameSteps.Length / 2];
            var events = evidence.events.Where(n => n != null).ToArray();
            var candidates = new List<VoicedResponse>();
            foreach (var note in events)
            {
                double time = note.time, duration = note.endTime - time, neural = Get(note, "neuralDuration");
                if (duration < .5 || duration > 1.2 || neural < .4 || duration / neural < .65 || duration / neural > 1.8
                    || !Tone(note, .62, .65, .025, 3) || !VoicedBody(voice, note, step, .08)) continue;
                if (events.Any(n => n.time > time + .12 && n.time < note.endTime - .12
                    && Tone(n, Math.Max(.55, .75 * Get(note, "neuralOnset")), .45, .025, 2))) continue;
                var pickup = events.Where(n => time - n.time >= .08 && time - n.time <= .4
                    && note.midi - n.midi >= 1 && note.midi - n.midi <= 5 && Tone(n, .5, .3, .015, 2)
                    && n.endTime >= time - .1 && Get(n, "neuralDuration") >= .08 && Get(n, "neuralDuration") <= .6
                    && VoicedBody(voice, n, step, .03)).OrderByDescending(n => n.time).FirstOrDefault();
                if (pickup == null) continue;
                double end = note.endTime;
                var exits = events.Where(n => n.time >= time + .6 * duration && n.time < end
                    && Get(n, "sourceAttack") >= .5 && n.midi != note.midi && Tone(n, .5, .15, .01, 2)).Select(n => n.time).ToArray();
                if (exits.Length > 0) end = exits.Min();
                if (end - pickup.time < .5 || candidates.Count > 0 && pickup.time - candidates.Last().time < .15) continue;
                candidates.Add(new VoicedResponse { time = pickup.time, end = end, interval = time - pickup.time,
                    delta = note.midi - pickup.midi, duration = duration, confidence = Get(note, "neuralOnset") });
            }
            var result = new List<MusicalCue>();
            foreach (var response in candidates.Where(c => candidates.Any(other => other != c
                && Math.Abs(c.time - other.time) > 1 && c.delta == other.delta
                && Math.Abs(c.interval - other.interval) < .04 && Math.Abs(c.duration - other.duration) < .18)))
            {
                if ((protectedSupports ?? Array.Empty<MusicalCue>()).Concat(result).Any(s => s != null
                    && (s.kind == "Hold" || s.kind == "Follow" || s.kind == "BigJump")
                    && response.time < s.endTime + .078 && response.end > s.time - .078)) continue;
                float direction = result.Count % 2 == 0 ? -1 : 1;
                result.Add(new MusicalCue { kind = "Follow", time = response.time, endTime = response.end, lane = 0,
                    points = new[] { new MusicalPoint { time = response.time, lane = 0 },
                        new MusicalPoint { time = response.end, lane = direction * .8f } },
                    confidence = response.confidence, phraseId = "mixed-voiced-rising-response-" + result.Count,
                    reason = "Recurring independently voiced rising pickup into a source-confirmed plateau; measured note-body release or following contrast ends the response. Lateral motion is independent gameplay choreography." });
            }
            FitNewPathCorridors(result, protectedSupports ?? Array.Empty<MusicalCue>());
            return result.ToArray();
        }

        private static bool VoicedBody(PcmVocalAnalysis.Evidence voice, PcmInstrumentalEvidence.NoteEvent note, double step, double minimum)
        {
            int confirmed = 0;
            for (int i = 0; i < voice.pitchTimes.Length; i++)
                if (voice.pitchTimes[i] >= note.time + .015 && voice.pitchTimes[i] < Math.Min(note.time + .2, note.endTime)
                    && voice.pitchConfidence[i] >= .5 && voice.pitch[i] == note.midi) confirmed++;
            return confirmed * step >= minimum;
        }

        private sealed class NoteGroup
        {
            public double time, strength;
            public PcmInstrumentalEvidence.NoteEvent[] notes;
        }
        private sealed class Contour
        {
            public string family, signature;
            public double period;
            public double[] times;
        }

        private static IEnumerable<MusicalCue> LongContours(PcmInstrumentalEvidence.Evidence evidence, double[] attacks)
        {
            var groups = Groups(evidence.events, false);
            var tonalGroups = Groups(evidence.events, true);
            var reliable = evidence.events.Where(Qualifies).Select(n => (double)n.midi).OrderBy(p => p).ToArray();
            if (reliable.Length < 8) return Array.Empty<MusicalCue>();
            double middleRegister = Quantile(reliable, .5), upperResponseRegister = Quantile(reliable, .6), upperRegister = Quantile(reliable, .8);
            var contours = new List<Contour>();
            int start = 0;
            for (int end = 1; end <= groups.Length; end++)
            {
                if (end < groups.Length && groups[end].time - groups[end - 1].time <= .265 + 1e-7) continue;
                var run = groups.Skip(start).Take(end - start).ToArray();
                double priorGap = start == 0 ? double.PositiveInfinity : groups[start].time - groups[start - 1].time;
                double afterGap = end == groups.Length ? double.PositiveInfinity : groups[end].time - groups[end - 1].time;
                start = end;
                if (run.Length < 7 || run.Length > 24 || !Regular(run, .12, .25, .13, out double period)
                    || priorGap < 1.7 * period - 1e-7 || afterGap < 1.7 * period - 1e-7) continue;
                if (run.Length == 7)
                {
                    var turn = BestPath(run, n => Tone(n, .68, .45, .025, 1), p =>
                    {
                        int i = p.Count - 1;
                        return (i != 2 || Step(p[1], p[2]) >= 1 && Step(p[1], p[2]) <= 3)
                            && (i != 3 || Same(p[0], p[3]) && Step(p[2], p[3]) >= 1 && Step(p[2], p[3]) <= 3)
                            && (i != 4 || Same(p[2], p[4]) && Step(p[3], p[4]) >= -3 && Step(p[3], p[4]) <= -1)
                            && (i != 5 || Same(p[1], p[5]) && Step(p[4], p[5]) >= -3 && Step(p[4], p[5]) <= -1)
                            && (i != 6 || Step(p[5], p[6]) >= -3 && Step(p[5], p[6]) <= -1);
                    });
                    if (turn != null) contours.Add(MakeContour("scalar-turn", run, turn, period));
                }
                var ascent = BestPath(run.Take(4).ToArray(), n => Tone(n, .8, .6, .05, 3), p => p.Count < 2
                    || p[p.Count - 1].midi - p[p.Count - 2].midi >= 1 && p[p.Count - 1].midi - p[p.Count - 2].midi <= 3);
                if (ascent == null || ascent[3].midi - ascent[0].midi < 5) continue;
                var phrase = run;
                if (run.Length > 9)
                {
                    // Repeated pedal responses distinguish the ornament's body
                    // from the separate cadence attack that follows it.
                    int lastPedal = -1, count = 0;
                    foreach (var pedal in run[5].notes.Where(n => Tone(n, .6, .55, .025, 2)))
                    {
                        int last = 3, repeats = 0;
                        for (int i = 5; i < run.Length; i += 2)
                        {
                            if (!run[i].notes.Any(n => Same(n, pedal) && Tone(n, .6, .55, .025, 2))) break;
                            repeats++; last = i;
                        }
                        if (repeats > count) { count = repeats; lastPedal = last; }
                    }
                    if (count < 3) continue;
                    phrase = run.Take(lastPedal + 1).ToArray();
                }
                contours.Add(MakeContour(run.Length > 9 ? "ascent-pedal" : "scalar-ascent", phrase, ascent, period));
            }
            for (int i = 0; i + 5 < groups.Length; i++)
            {
                var run = groups.Skip(i).Take(6).ToArray();
                if (!Regular(run, .12, .25, .12, out double period)) continue;
                var reply = BestPath(run, n => Tone(n, .68, .45, .025, 1), p =>
                {
                    int at = p.Count - 1;
                    return (at != 0 || p[0].midi >= middleRegister)
                        && (at != 1 || Step(p[0], p[1]) >= 2 && Step(p[0], p[1]) <= 5)
                        && (at != 2 || Same(p[0], p[2])) && (at != 3 || Same(p[1], p[3]))
                        && (at != 4 || Step(p[3], p[4]) >= -4 && Step(p[3], p[4]) <= -1)
                        && (at != 5 || Same(p[4], p[5]));
                });
                if (reply == null) continue;
                double release = attacks.FirstOrDefault(t => t > run[5].time + .078 && t < run[5].time + 1.4 * period);
                if (release <= 0) continue;
                var contour = MakeContour("broken-chord-reply", run, reply, period);
                var times = contour.times.Concat(new[] { release }).ToList();
                var pickup = evidence.events.Where(n => n != null && Math.Abs(n.time - (run[0].time - period)) < .04
                    && reply[0].midi - n.midi >= 1 && reply[0].midi - n.midi <= 2
                    && Get(n, "sourceAttack") >= .5 && Tone(n, .65, .2, .01, 2)
                    && !evidence.events.Any(o => o != null && Math.Abs(o.time - n.time) < 1e-7 && Math.Abs(o.midi - n.midi) == 12
                        && Get(o, "neuralOnset") > Get(n, "neuralOnset"))).OrderByDescending(n => Get(n, "neuralOnset")).FirstOrDefault();
                if (pickup != null) times.Insert(0, pickup.time);
                contour.times = times.ToArray(); contours.Add(contour);
            }
            for (int i = 0; i + 5 < tonalGroups.Length; i++)
            {
                var run = tonalGroups.Skip(i).Take(6).ToArray();
                if (!Regular(run, .13, .25, .12, out double period)) continue;
                var answer = BestPath(run, Qualifies, p =>
                {
                    int at = p.Count - 1;
                    return (at != 0 || p[0].midi >= upperResponseRegister && !run[0].notes.Any(n => n != p[0]
                            && Same(n, p[0]) && Get(n, "neuralOnset") > Get(p[0], "neuralOnset")))
                        && (at != 1 || Step(p[0], p[1]) >= -5 && Step(p[0], p[1]) <= -4)
                        && (at != 2 || Same(p[1], p[2]))
                        && (at != 3 || Step(p[2], p[3]) >= 1 && Step(p[2], p[3]) <= 3)
                        && (at != 4 || Same(p[1], p[4]))
                        && (at != 5 || Step(p[4], p[5]) >= -3 && Step(p[4], p[5]) <= -1);
                });
                if (answer != null) contours.Add(MakeContour("stepwise-answer", run, answer, period));
            }
            var upperNotes = evidence.events.Where(n => Qualifies(n) && n.midi >= upperRegister - 2 && !HasStrongerSemitone(n, evidence.events))
                .GroupBy(n => n.time).Select(g => g.OrderByDescending(n => Get(n, "neuralOnset") * Math.Pow(Get(n, "harmonicNovelty"), .2)).First()).ToArray();
            var trillNotes = new List<PcmInstrumentalEvidence.NoteEvent>();
            foreach (var note in upperNotes)
            {
                if (trillNotes.Count > 0 && note.time - trillNotes.Last().time < .11)
                {
                    if (Get(note, "neuralOnset") * Math.Pow(Get(note, "harmonicNovelty"), .2)
                        <= Get(trillNotes.Last(), "neuralOnset") * Math.Pow(Get(trillNotes.Last(), "harmonicNovelty"), .2)) continue;
                    trillNotes.RemoveAt(trillNotes.Count - 1);
                }
                trillNotes.Add(note);
            }
            for (int i = 0; i + 8 < trillNotes.Count; i++)
            {
                var first = trillNotes[i]; var second = trillNotes[i + 1];
                if (first.midi < upperRegister || first.midi - second.midi < 1 || first.midi - second.midi > 2) continue;
                int end = i + 1;
                while (end < trillNotes.Count && trillNotes[end].midi == ((end - i) % 2 == 0 ? first.midi : second.midi)
                    && trillNotes[end].time - trillNotes[end - 1].time >= .1 - 1e-7 && trillNotes[end].time - trillNotes[end - 1].time <= .3 + 1e-7) end++;
                if (end - i < 9) continue;
                int count = (end - i) % 2 == 0 ? end - i - 1 : end - i;
                var run = trillNotes.Skip(i).Take(count).Select(n => new NoteGroup { time = n.time, notes = new[] { n } }).ToArray();
                if (Regular(run, .1, .3, .18, out double period) && IndependentlyArticulated(run)) contours.Add(new Contour
                    { family = "continuous-neighbor-trill", signature = "", period = period, times = run.Select(n => n.time).ToArray() });
                i = end - 1;
            }
            // Wider tremolos require source articulation rather than merely
            // reusing a sustained neural activation at successive mixed peaks.
            var articulatedNotes = evidence.events.Where(n => Qualifies(n) && n.midi >= upperRegister - 5 && !HasStrongerSemitone(n, evidence.events))
                .GroupBy(n => n.time).Select(g => g.OrderByDescending(n => Get(n, "neuralOnset") * Math.Pow(Get(n, "harmonicNovelty"), .2)).First()).ToArray();
            var articulated = new List<PcmInstrumentalEvidence.NoteEvent>();
            foreach (var note in articulatedNotes)
            {
                if (articulated.Count > 0 && note.time - articulated.Last().time < .11)
                {
                    if (Get(note, "neuralOnset") * Math.Pow(Get(note, "harmonicNovelty"), .2)
                        <= Get(articulated.Last(), "neuralOnset") * Math.Pow(Get(articulated.Last(), "harmonicNovelty"), .2)) continue;
                    articulated.RemoveAt(articulated.Count - 1);
                }
                articulated.Add(note);
            }
            for (int i = 0; i + 6 < articulated.Count; i++)
            {
                var first = articulated[i]; var second = articulated[i + 1];
                if (first.midi < upperRegister || first.midi - second.midi < 1 || first.midi - second.midi > 5) continue;
                int end = i + 1;
                while (end < articulated.Count && articulated[end].midi == ((end - i) % 2 == 0 ? first.midi : second.midi)
                    && articulated[end].time - articulated[end - 1].time >= .1 - 1e-7 && articulated[end].time - articulated[end - 1].time <= .3 + 1e-7) end++;
                if (end - i < 7) continue;
                int count = (end - i) % 2 == 0 ? end - i - 1 : end - i;
                var run = articulated.Skip(i).Take(count).Select(n => new NoteGroup { time = n.time, notes = new[] { n } }).ToArray();
                if (Regular(run, .1, .3, .18, out double period) && Get(first, "neuralDuration") > 0
                    && Get(first, "neuralDuration") <= 1.5 * period && IndependentlyArticulated(run))
                {
                    var times = run.Select(n => n.time).ToList();
                    var pickup = evidence.events.Where(n => n != null && Math.Abs(n.time - (first.time - period)) < .04
                        && Step(n, first) >= 1 && Step(n, first) <= 2 && Tone(n, .65, .2, .01, 2)
                        && (Get(n, "sourceAttack") >= .5 || Tone(n, .85, .3, .05, 2))
                        && !evidence.events.Any(o => o != null && Math.Abs(o.time - n.time) < 1e-7 && Math.Abs(o.midi - n.midi) == 12
                            && Get(o, "neuralOnset") > Get(n, "neuralOnset"))).OrderByDescending(n => Get(n, "neuralOnset")).FirstOrDefault();
                    if (pickup != null) times.Insert(0, pickup.time);
                    contours.Add(new Contour { family = "articulated-tremolo", signature = "", period = period, times = times.ToArray() });
                }
                i = end - 1;
            }
            var accepted = contours.Where(c => c.family == "continuous-neighbor-trill" || c.family == "articulated-tremolo" || contours.Any(other => other != c
                && FamilyKey(other.family) == FamilyKey(c.family) && other.signature == c.signature && Math.Abs(other.period - c.period) < .04
                && Math.Abs(other.times[0] - c.times[0]) > .5)).OrderBy(c => c.times[0]).ToArray();
            var result = new List<MusicalCue>();
            foreach (var contour in accepted)
            {
                double begin = contour.times.First(), end = contour.times.Last();
                if (end - begin < .7 || end - begin > 6 || contour.times.Zip(contour.times.Skip(1), (a, b) => b - a >= .0624).Any(valid => !valid)) continue;
                if (result.Any(c => begin < c.endTime + .078 && end > c.time - .078)) continue;
                // The broader articulation family has its own choreography
                // stream, so discovering it does not mirror existing paths.
                int ordinal = result.Count(c => c.phraseId.StartsWith("mixed-articulated-tremolo-", StringComparison.Ordinal)
                    == (contour.family == "articulated-tremolo"));
                float direction = ordinal % 2 == 0 ? -1 : 1;
                var points = contour.times.Select((time, index) => new MusicalPoint { time = time,
                    lane = ContourLane(contour.family, index, contour.times.Length, direction) }).ToArray();
                result.Add(new MusicalCue { kind = "Follow", time = begin, endTime = end, lane = points[0].lane, points = points,
                    confidence = .8, phraseId = "mixed-" + contour.family + "-" + ordinal,
                    reason = "Measured recurring tonal " + contour.family + "; native markers follow actual articulations and the source-confirmed phrase boundary. Lateral choreography is independent of the reference map." });
            }
            return result;
        }

        private static float ContourLane(string family, int index, int count, float direction)
        {
            if (index == count - 1) return 0;
            if (family == "ascent-pedal") return index == 0 ? direction * .4f : (float)(.7 * Math.Sin((index - 1) * Math.PI / 4));
            double phase = (double)index / (count - 1);
            if (family == "scalar-turn" || family == "broken-chord-reply") return phase <= .34 ? direction * .8f : phase <= .68 ? -direction * .8f : 0;
            return index < 2 ? direction * .8f : (float)(direction * .8 * (1 - 2.0 * (index - 1) / (count - 3)));
        }
        private static string FamilyKey(string family) => family == "ascent-pedal" ? "scalar-ascent" : family;

        private static bool IndependentlyArticulated(NoteGroup[] run)
        {
            // Repeated mixed peaks can reuse a note activation until its next
            // same-pitch return. Require a majority of actual note releases to
            // resolve before that return, across the run rather than just at entry.
            int confirmed = 0;
            for (int i = 0; i + 1 < run.Length; i++)
            {
                double gap = run[i + 1].time - run[i].time;
                if (run[i].notes.Any(n => Get(n, "neuralDuration") > 0 && Get(n, "neuralDuration") <= 1.5 * gap + 1e-7)) confirmed++;
            }
            return confirmed >= Math.Ceiling(.6 * (run.Length - 1));
        }

        /// <summary>Veto a legacy irregular mixed-attack signature when independent tonal onsets reveal its omitted regular subdivisions.</summary>
        public static bool RejectsLegacyArticulation(PcmInstrumentalEvidence.Evidence evidence, MusicalCue cue)
        {
            if (evidence?.events == null || cue == null || cue.kind != "Follow") return false;
            var groups = Groups(evidence.events.Where(n => n != null && n.time >= cue.time - .08
                && n.time <= cue.endTime + .04 && Tone(n, .68, .45, .025, 2)).ToArray(), false);
            if (groups.Length < 6 || groups.Last().time - groups.First().time < .75 * (cue.endTime - cue.time)) return false;
            // This only challenges the legacy detector's five irregular attack
            // premise. A separate melodic recognizer may still justify a Follow.
            return Regular(groups, .12, .30, .18, out _);
        }

        private sealed class ChordalResponse
        {
            public PcmInstrumentalEvidence.NoteEvent lead;
            public double start, end, duration;
            public int[] signature;
        }

        private static IEnumerable<MusicalCue> ChordalResponses(PcmInstrumentalEvidence.Evidence evidence)
        {
            var events = evidence.events; var candidates = new List<ChordalResponse>();
            foreach (var group in events.Where(n => n != null).GroupBy(n => n.time).OrderBy(g => g.Key))
            foreach (var note in group)
            {
                double time = note.time, duration = note.endTime - time, neuralDuration = Get(note, "neuralDuration");
                if (duration < .55 || duration > 1.2 || neuralDuration < .4 || duration / neuralDuration < .65
                    || duration / neuralDuration > 1.8 || !Tone(note, .62, .65, .025, 3)) continue;
                var voices = group.Where(n => n.endTime - time >= .75 * duration && Tone(n, .3, .6, .02, 3)).ToArray();
                var signature = voices.Select(n => (n.midi - note.midi + 120) % 12).Distinct().OrderBy(p => p).ToArray();
                if (voices.Length < 2 || signature.Length < 2
                    || voices.Any(n => Get(n, "neuralOnset") > Get(note, "neuralOnset")
                        || n.midi > note.midi && Get(n, "neuralOnset") >= .95 * Get(note, "neuralOnset"))) continue;
                // Resonant accompaniment can remain harmonic while an independent
                // melody continues. Such a phrase needs an articulated contour;
                // it must not acquire this sustained multi-voice interpretation.
                if (events.Any(n => n != null && n.time > time + .12 && n.time < note.endTime - .12
                    && Tone(n, Math.Max(.55, .75 * Get(note, "neuralOnset")), .45, .025, 2))) continue;
                double start = time, end = note.endTime;
                var pickup = events.Where(n => n != null && time - n.time >= .08 && time - n.time <= .4
                    && Math.Abs(note.midi - n.midi) <= 7 && Tone(n, .5, .6, .025, 3) && n.endTime >= time - .1
                    && Get(n, "neuralDuration") >= .08 && Get(n, "neuralDuration") <= .6)
                    .OrderByDescending(n => n.time).FirstOrDefault();
                if (pickup != null) start = pickup.time;
                var exits = events.Where(n => n != null && n.time >= time + .6 * duration && n.time < end
                    && Get(n, "sourceAttack") >= .5 && n.midi != note.midi && Tone(n, .5, .15, .01, 2)).Select(n => n.time).ToArray();
                if (exits.Length > 0) end = exits.Min();
                var response = new ChordalResponse { lead = note, start = start, end = end, duration = duration, signature = signature };
                if (candidates.Count > 0 && start - candidates.Last().start < .15)
                {
                    var previous = candidates.Last();
                    if (Get(note, "neuralOnset") <= Get(previous.lead, "neuralOnset") && duration <= previous.duration) continue;
                    candidates.RemoveAt(candidates.Count - 1);
                }
                candidates.Add(response);
            }
            var result = new List<MusicalCue>();
            foreach (var response in candidates.Where(c => candidates.Any(other => other != c && Math.Abs(c.start - other.start) > 1
                && Math.Abs(c.duration - other.duration) < .18 && c.signature.Intersect(other.signature).Count() >= 2)))
            {
                double start = response.start, end = response.end;
                if (end - start < .5 || result.Any(c => start < c.endTime + .078 && end > c.time - .078)) continue;
                float direction = result.Count % 2 == 0 ? -1 : 1;
                var points = new List<MusicalPoint> { new MusicalPoint { time = start, lane = 0 } };
                if (response.lead.time - start >= .0624 && end - response.lead.time >= .0624)
                    points.Add(new MusicalPoint { time = response.lead.time, lane = (float)(direction * .8 * (response.lead.time - start) / (end - start)) });
                points.Add(new MusicalPoint { time = end, lane = direction * .8f });
                result.Add(new MusicalCue { kind = "Follow", time = start, endTime = end, lane = 0, points = points.ToArray(),
                    confidence = Get(response.lead, "neuralOnset"), phraseId = "mixed-chordal-response-" + result.Count,
                    reason = "Recurring foreground response with independent sustained chord voices; measured connected pickup and harmonic release or subsequent contrast delimit the phrase. Lateral movement is gameplay choreography." });
            }
            return result;
        }

        private static NoteGroup[] Groups(PcmInstrumentalEvidence.NoteEvent[] notes, bool tonal)
        {
            var result = new List<NoteGroup>();
            foreach (var group in notes.Where(n => n != null).GroupBy(n => n.time).OrderBy(g => g.Key))
            {
                var eligible = group.Where(n => tonal ? Qualifies(n) : Tone(n, .68, .25, .025, 1)).ToArray();
                if (eligible.Length == 0) continue;
                var item = new NoteGroup { time = group.Key, notes = group.ToArray(),
                    strength = eligible.Max(n => Get(n, "neuralOnset") * Math.Min(1, Get(n, "harmonicNovelty") / .1)) };
                if (!tonal && result.Count > 0 && item.time - result.Last().time < .11)
                {
                    if (item.strength <= result.Last().strength) continue;
                    result.RemoveAt(result.Count - 1);
                }
                result.Add(item);
            }
            return result.ToArray();
        }
        private static PcmInstrumentalEvidence.NoteEvent[] BestPath(NoteGroup[] groups, Func<PcmInstrumentalEvidence.NoteEvent, bool> eligible,
            Func<List<PcmInstrumentalEvidence.NoteEvent>, bool> validPrefix)
        {
            var options = groups.Select(g => g.notes.Where(eligible).ToArray()).ToArray();
            if (options.Any(p => p.Length == 0)) return null;
            var path = new List<PcmInstrumentalEvidence.NoteEvent>(); PcmInstrumentalEvidence.NoteEvent[] best = null;
            double bestScore = double.NegativeInfinity;
            void Search(int at, double score)
            {
                if (at == options.Length) { if (score > bestScore) { bestScore = score; best = path.ToArray(); } return; }
                foreach (var note in options[at])
                {
                    path.Add(note);
                    if (validPrefix(path)) Search(at + 1, score + Get(note, "neuralOnset"));
                    path.RemoveAt(path.Count - 1);
                }
            }
            Search(0, 0); return best;
        }
        private static bool Regular(NoteGroup[] groups, double minimum, double maximum, double variability, out double period)
        {
            var gaps = groups.Zip(groups.Skip(1), (a, b) => b.time - a.time).ToArray();
            period = gaps.Length == 0 ? 0 : Quantile(gaps.OrderBy(g => g).ToArray(), .5);
            if (period <= 0 || gaps.Any(g => g < minimum - 1e-7 || g > maximum + 1e-7)) return false;
            double mean = gaps.Average();
            return Math.Sqrt(gaps.Average(g => (g - mean) * (g - mean))) / period <= variability;
        }
        private static Contour MakeContour(string family, NoteGroup[] groups, PcmInstrumentalEvidence.NoteEvent[] notes, double period)
            => new Contour { family = family, period = period, times = groups.Select(g => g.time).ToArray(),
                signature = string.Join(",", notes.Select(n => (n.midi - notes[0].midi + 120) % 12)) };
        private static int Step(PcmInstrumentalEvidence.NoteEvent first, PcmInstrumentalEvidence.NoteEvent second) => (second.midi - first.midi + 126) % 12 - 6;
        private static bool Same(PcmInstrumentalEvidence.NoteEvent first, PcmInstrumentalEvidence.NoteEvent second) => (first.midi - second.midi) % 12 == 0;
        private static bool Tone(PcmInstrumentalEvidence.NoteEvent n, double neural, double body, double novelty, double partials)
            => n != null && Get(n, "neuralOnset") >= neural && Get(n, "harmonicBody") >= body
                && Get(n, "harmonicNovelty") >= novelty && Get(n, "harmonicPartials") >= partials;

        private sealed class PathSupport
        {
            public MusicalCue cue;
            public bool isNew;
            public float entry, exit;
        }

        private static void FitNewPathCorridors(List<MusicalCue> proposals, MusicalCue[] protectedSupports)
        {
            // These paths become fixed in ChartRouteComposer. Fit only our new
            // endpoints before compilation; never bend a confirmed legacy path.
            while (proposals.Count > 0)
            {
                var supports = protectedSupports.Where(c => c != null && c.kind == "Follow")
                    .Concat(proposals).OrderBy(c => c.time).Select(c => new PathSupport
                    {
                        cue = c, isNew = proposals.Contains(c),
                        entry = c.points != null && c.points.Length > 0 ? c.points.First().lane : c.lane,
                        exit = c.points != null && c.points.Length > 0 ? c.points.Last().lane : c.lane
                    }).ToArray();
                if (TryFitCorridors(supports)) return;
                // A source gesture is optional. Reject an infeasible proposal
                // rather than changing a protected path or emitting a broken map.
                proposals.RemoveAt(proposals.Count - 1);
            }
        }

        private static bool TryFitCorridors(PathSupport[] supports)
        {
            int count = supports.Length * 2;
            var lower = new double[count]; var upper = new double[count]; var desired = new double[count];
            var stepLower = new double[count - 1]; var stepUpper = new double[count - 1];
            for (int i = 0; i < supports.Length; i++)
            {
                var support = supports[i]; int entry = 2 * i, exit = entry + 1;
                lower[entry] = lower[exit] = -1; upper[entry] = upper[exit] = 1;
                desired[entry] = support.entry; desired[exit] = support.exit;
                if (support.isNew)
                {
                    bool right = support.exit > support.entry;
                    // A tiny numeric margin keeps the serialized float span at
                    // or above the compiler's meaningful-movement threshold.
                    double maximumSpan = Math.Max(.20001, Math.Abs(support.exit - support.entry));
                    stepLower[entry] = right ? .20001 : -maximumSpan;
                    stepUpper[entry] = right ? maximumSpan : -.20001;
                }
                else
                {
                    lower[entry] = upper[entry] = support.entry;
                    lower[exit] = upper[exit] = support.exit;
                    stepLower[entry] = -2; stepUpper[entry] = 2;
                }
                if (i + 1 == supports.Length) continue;
                double gap = supports[i + 1].cue.time - support.cue.endTime;
                if (gap < -.00001) return false;
                double shift = ChartRouteComposer.MaxLateralShift(Math.Max(0, gap));
                stepLower[exit] = -shift; stepUpper[exit] = shift;
                if (support.isNew && gap < .30 - .00001)
                    desired[exit] = Clamp(desired[exit], supports[i + 1].entry - shift, supports[i + 1].entry + shift);
            }
            // The constraint graph is a chain of entries and exits. Backward
            // reachable intervals followed by forward target projection solve
            // all neighboring endpoint corridors, including two legacy anchors.
            for (int i = count - 2; i >= 0; i--)
            {
                lower[i] = Math.Max(lower[i], lower[i + 1] - stepUpper[i]);
                upper[i] = Math.Min(upper[i], upper[i + 1] - stepLower[i]);
                if (lower[i] > upper[i] + 1e-7) return false;
            }
            var fitted = new double[count];
            for (int i = 0; i < count; i++)
            {
                double low = lower[i], high = upper[i];
                if (i > 0)
                {
                    low = Math.Max(low, fitted[i - 1] + stepLower[i - 1]);
                    high = Math.Min(high, fitted[i - 1] + stepUpper[i - 1]);
                }
                if (low > high + 1e-7) return false;
                fitted[i] = Clamp(desired[i], low, high);
            }
            for (int i = 0; i < supports.Length; i++)
            {
                var support = supports[i]; if (!support.isNew) continue;
                var cue = support.cue; float entry = (float)fitted[2 * i], exit = (float)fitted[2 * i + 1];
                cue.lane = entry;
                foreach (var point in cue.points)
                    point.lane = (float)Clamp(entry + (point.lane - support.entry) * (exit - entry) / (support.exit - support.entry), -1, 1);
            }
            return true;
        }

        private static double Clamp(double value, double lower, double upper) => Math.Max(lower, Math.Min(upper, value));

        private static bool Qualifies(PcmInstrumentalEvidence.NoteEvent n)
            => n != null && Get(n, "neuralOnset") >= .6 && Get(n, "harmonicPartials") >= 1
                && Get(n, "harmonicBody") >= .45 && Get(n, "harmonicNovelty") >= .015;
        private static double End(PcmInstrumentalEvidence.NoteEvent n, double[] attacks)
            => Math.Min(n.endTime, attacks.FirstOrDefault(t => t > n.time + .078) is double next && next > 0 ? next : n.endTime);
        private static bool HasStrongerSemitone(PcmInstrumentalEvidence.NoteEvent note, PcmInstrumentalEvidence.NoteEvent[] notes)
            => notes.Any(n => n != null && Math.Abs(n.time - note.time) < 1e-7 && Math.Abs(n.midi - note.midi) == 1
                && Get(n, "neuralOnset") >= .55 && Get(n, "harmonicPartials") >= 1 && Get(n, "harmonicBody") >= .4
                && Get(n, "harmonicNovelty") > 1.5 * Get(note, "harmonicNovelty"));
        private static double Get(PcmInstrumentalEvidence.NoteEvent n, string feature) => PcmInstrumentalEvidence.Get(n, feature);
        private static double Quantile(double[] sorted, double q)
        { double at = (sorted.Length - 1) * q; int lower = (int)at; return sorted[lower] + (sorted[Math.Min(lower + 1, sorted.Length - 1)] - sorted[lower]) * (at - lower); }
        private static int Nearest(double[] times, double time)
        {
            int index = Array.BinarySearch(times, time);
            if (index >= 0) return index;
            index = ~index;
            if (index >= times.Length) return times.Length - 1;
            return index > 0 && time - times[index - 1] < times[index] - time ? index - 1 : index;
        }
    }
}
