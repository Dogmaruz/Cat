using System;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Physical separation and scored-onset regression checks for opt-in ornament grouping.</summary>
    public static class PcmPlayablePhraseGroupingChecks
    {
        public static void RunBaseline()
        {
            var map = JsonUtility.FromJson<MapTimeline>(File.ReadAllText("Assets/Resources/CustomData/Maps/LumiLai.timeline.json"));
            RequireNoInfeasibleRounds(map, 1.18f, 1.8f, 1.40f);
        }

        public static void RunAdjacentHoldBaseline()
        {
            var map = JsonUtility.FromJson<MapTimeline>(File.ReadAllText("Assets/Resources/CustomData/Maps/LumiLai.timeline.json"));
            Require(map.segments.Any(s => s.kind == "Hold" && s.audioTime <= 63.272744 && s.endAudioTime >= 64.350294),
                "the audible connected 139.683ms Hold/ornament transition requires one full-size support, not intersecting cap meshes");
        }

        public static void RunChecks()
        {
            var source = Compile(new[] { Jump(1), Jump(1.089796), Jump(1.179592), Jump(1.6), Jump(2.4),
                new MusicalCue { kind = "Hold", time = 3.2, endTime = 3.5 },
                new MusicalCue { kind = "Follow", time = 4, endTime = 4.4, points = new[] {
                    new MusicalPoint { time = 4, lane = 0 }, new MusicalPoint { time = 4.4, lane = .5f } } } });
            string original = JsonUtility.ToJson(source);
            RecordJsonCloneDiagnostics(source);
            var result = PcmPlayablePhraseGrouping.Apply(source, new PcmPlayablePhraseGrouping.Settings());
            var group = result.Report.groups.Single();
            var held = result.Map.segments.Single(s => s.sourceIndex == group.resultSourceIndex);
            var entry = result.Map.elements[held.sourceIndex];
            Require(JsonUtility.ToJson(source) == original, "grouping must not mutate its measured source");
            Require(group.onsetTimes.SequenceEqual(new[] { 1.0, 1.089796, 1.179592 }), "the complete 89.796ms ornament retains every exact onset");
            Require(held.kind == "Hold" && held.duration < .18 && held.duration > .1795,
                "the measured 179.592ms span uses a native continuous support without padding its musical end");
            Require(entry.holdTicks.Select(t => t.audioTime).SequenceEqual(group.onsetTimes),
                "every ornament onset must become a real native checkpoint, including entry and terminal attack");
            Require(PcmPlayablePhraseGrouping.EventsExactlyEqual(source.events, result.Map.events), "scored event fields, exact double bits and multiplicities are unchanged");
            var corrupted = result.Map.events.Select(e => new KoreographyEvent { mStartSample = e.mStartSample,
                mEndSample = e.mEndSample, index = e.index, command = e.command, audioTime = e.audioTime,
                endAudioTime = e.endAudioTime, gameTime = e.gameTime, endGameTime = e.endGameTime }).ToArray();
            corrupted[0].gameTime = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(corrupted[0].gameTime) + 1);
            Require(!PcmPlayablePhraseGrouping.EventsExactlyEqual(source.events, corrupted), "even a one-ULP event-time mutation must fail the strict preservation guard");
            Require(NativeTargetSamples(result.Map).SequenceEqual(source.events.Select(e => e.mStartSample).OrderBy(t => t)),
                "native entry/checkpoint/marker targets must account for every scored sample exactly once");
            Require(Math.Abs(-entry.se.y - (held.endZ - held.z)) < .00001,
                "continuous support bones span the full measured ornament");
            foreach (string kind in new[] { "Hold", "Follow" })
            {
                var before = source.segments.Single(s => s.kind == kind);
                var after = result.Map.segments.Single(s => s.kind == kind && Math.Abs(s.audioTime - before.audioTime) < .000001);
                Require(before.audioTime == after.audioTime && before.endAudioTime == after.endAudioTime
                    && before.holdTicks.Select(t => JsonUtility.ToJson(t)).SequenceEqual(after.holdTicks.Select(t => JsonUtility.ToJson(t))), "existing " + kind + " musical timing stays protected");
            }
            // The exit box can fire before the terminal tick. The native arc is anchored
            // at endZ, and its clamped negative progress keeps all remaining ticks at
            // ground height. Actual collider contacts and steering still require Play QA.
            float earlyHandoff = held.endZ - .75f;
            Require(entry.holdTicks[1].z - .255f < earlyHandoff, "the short ornament's interior tick can contact before early exit handoff");
            foreach (var tick in entry.holdTicks.Where(t => t.z >= earlyHandoff))
            {
                float contactZ = tick.z - .255f;
                float percent = (contactZ - held.endZ) / entry.jumpDistance;
                Require(Math.Abs(NativeRhythmRules.JumpY(0, NativeRhythmRules.HeightForDistance(entry.jumpDistance), percent)) < .000001,
                    "native early handoff must not lift the actor above a remaining ornament checkpoint");
            }
            AudioChartGeneratorValidation.ValidateMap(result.Map);
            RequireNoInfeasibleRounds(result.Map, 1.18f, 1.8f, 1.40f);

            foreach (double interval in new[] { .15, .179592, .18, .22 })
            {
                var feasible = Compile(new[] { Jump(1), Jump(1 + interval), Jump(1 + 2 * interval), Jump(2.4), Jump(3.6) });
                var unchanged = PcmPlayablePhraseGrouping.Apply(feasible, new PcmPlayablePhraseGrouping.Settings());
                Require(unchanged.Report.groups.Length == 0 && unchanged.Map.elements.Length == feasible.elements.Length,
                    "feasible " + interval + "s subdivisions must retain their separate normal supports");
            }
            bool rejected = false;
            try { PcmPlayablePhraseGrouping.Apply(source, new PcmPlayablePhraseGrouping.Settings { minimumSeparation = float.NaN }); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "non-finite geometry settings are rejected before authoring");
            RunConnectedCapChecks();
            Debug.Log("PCM_PLAYABLE_ORNAMENTS PASS: exact event bits and native checkpoint samples, source immutability, 179.592ms support and early-handoff guard; audible connected-cap joins retain original Hold coverage, silence and feasible gaps stay separate. Actual physical scoring remains a Play Mode requirement.");
        }

        public static void ValidateGenerated(MapTimeline source, MapTimeline grouped)
        {
            var report = PcmPlayablePhraseGrouping.ReadReport(grouped);
            Require(report != null && report.scoredEventSamplesPreserved, "enabled grouping stores its measured report");
            Require(PcmPlayablePhraseGrouping.EventsExactlyEqual(source.events, grouped.events), "full-recording scored event fields, exact double bits and multiplicities stay unchanged");
            Require(NativeTargetSamples(grouped).SequenceEqual(source.events.Select(e => e.mStartSample).OrderBy(t => t)),
                "full-recording native scoring targets cover all measured samples once");
            RecordCoverageReportDiagnostics(source, grouped, report);
            foreach (var original in source.segments.Where(s => s.kind == "Hold"))
                Require(grouped.segments.Any(s => s.kind == "Hold" && CoversOriginalHold(original, s)),
                    "every original real-Hold interval stays fully covered on the immutable source clock: " + original.sourceIndex);
            foreach (var group in report.groups)
            {
                var segment = grouped.segments.Single(s => s.sourceIndex == group.resultSourceIndex);
                var nativeSamples = segment.holdTicks.Select(t => (int)Math.Round(t.audioTime * grouped.sampleRate)).ToArray();
                var reportedSamples = group.onsetTimes.Select(t => (int)Math.Round(t * grouped.sampleRate)).ToArray();
                Require(segment.kind == "Hold" && (group.partOfPhysicalMerge
                    ? reportedSamples.All(t => nativeSamples.Contains(t)) : nativeSamples.SequenceEqual(reportedSamples)),
                    "reported group onsets are actual native Hold checkpoint targets");
            }
            foreach (var merge in report.physicalMerges)
            {
                var segment = grouped.segments.Single(s => s.sourceIndex == merge.resultSourceIndex);
                Require(merge.checkpointTimes.All(t => segment.holdTicks.Any(p => Math.Round(p.audioTime * grouped.sampleRate) == Math.Round(t * grouped.sampleRate))),
                    "every connected original/ornament checkpoint remains an actual native target");
                for (int i = 0; i < merge.originalHoldIndices.Length; i++)
                {
                    var original = source.segments.Single(s => s.sourceIndex == merge.originalHoldIndices[i] && s.kind == "Hold");
                    Require(merge.originalHoldStartBits[i] == Bits(original.audioTime) && merge.originalHoldEndBits[i] == Bits(original.endAudioTime),
                        "physical merge report retains exact immutable source boundary bits");
                    Require(CoversOriginalHold(original, segment), "combined support retains full original real-Hold coverage: source="
                        + original.sourceIndex + " start=" + Bits(original.audioTime) + " end=" + Bits(original.endAudioTime)
                        + " resultStart=" + Bits(segment.audioTime) + " resultEnd=" + Bits(segment.endAudioTime));
                }
            }
            RequireNoInfeasibleRounds(grouped, report.settings.worldDistanceScale, report.settings.lateralScale, report.settings.minimumSeparation);
            AudioChartGeneratorValidation.ValidateMap(grouped);
        }

        private static void RunConnectedCapChecks()
        {
            MapTimeline Source(double gap, bool chain = false)
            {
                double a = 1.2 + gap;
                return Compile(new[] { new MusicalCue { kind = "Hold", time = 1, endTime = 1.2 },
                    Jump(a), Jump(a + .089796), Jump(a + .179592),
                    chain ? new MusicalCue { kind = "Hold", time = a + .179592 + gap, endTime = a + .379592 + gap } : Jump(2.3),
                    Jump(3.5), Jump(4.3) });
            }
            var voice = new PcmVocalAnalysis.Evidence { rmsStep = .01, rms = Enumerable.Repeat(.1, 600).ToArray() };
            var source = Source(.139683);
            var connected = PcmPlayablePhraseGrouping.Apply(source, new PcmPlayablePhraseGrouping.Settings(), voice);
            var merge = connected.Report.physicalMerges.Single();
            var support = connected.Map.segments.Single(s => s.sourceIndex == merge.resultSourceIndex);
            Require(support.audioTime == 1 && support.endAudioTime == 1.2 + .139683 + .179592,
                "an audible infeasible adjacent real Hold/ornament becomes one full-size support at its existing boundaries");
            Require(support.holdTicks.Select(t => t.audioTime).SequenceEqual(new[] { 1.0, 1.2, 1.339683, 1.429479, 1.519275 }),
                "the former real-Hold entry/release and every ornament attack remain native checkpoints");
            Require(merge.originalHoldStarts.SequenceEqual(new[] { 1.0 }) && merge.originalHoldEnds.SequenceEqual(new[] { 1.2 }),
                "physical topology changes explicitly retain the original real-Hold coverage");
            var realHold = source.segments.Single(s => s.kind == "Hold");
            Require(!CoversOriginalHold(realHold, new MapSegment { audioTime = realHold.audioTime,
                endAudioTime = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(realHold.endAudioTime) - 1) }),
                "losing even one ULP of an original real-Hold boundary must fail coverage, without a timing tolerance");
            ValidateGenerated(source, connected.Map);
            Require(PcmPlayablePhraseGrouping.Apply(source, new PcmPlayablePhraseGrouping.Settings()).Report.physicalMerges.Length == 0,
                "missing vocal continuity cannot justify a physical Hold join");
            var silent = new PcmVocalAnalysis.Evidence { rmsStep = .01, rms = voice.rms.ToArray() };
            Array.Clear(silent.rms, 120, 14);
            Require(PcmPlayablePhraseGrouping.Apply(source, new PcmPlayablePhraseGrouping.Settings(), silent).Report.physicalMerges.Length == 0,
                "a true silent gap must remain separate despite close cap geometry");
            foreach (double feasibleGap in new[] { .179592, .22 })
                Require(PcmPlayablePhraseGrouping.Apply(Source(feasibleGap), new PcmPlayablePhraseGrouping.Settings(), voice).Report.physicalMerges.Length == 0,
                    "a feasible or nonrapid Hold transition retains its original separate support");
            var chainSource = Source(.139683, true);
            var chain = PcmPlayablePhraseGrouping.Apply(chainSource, new PcmPlayablePhraseGrouping.Settings(), voice);
            Require(chain.Report.physicalMerges.Length == 2 && chain.Report.physicalMerges.Select(m => m.resultSourceIndex).Distinct().Count() == 1,
                "connected-cap chains resolve every report to the actual combined support");
            ValidateGenerated(chainSource, chain.Map);
        }

        private static int[] NativeTargetSamples(MapTimeline map)
            => map.elements.Where(e => e.kind != "Start").SelectMany(e => e.kind == "Hold"
                ? e.holdTicks.Select(t => t.audioTime) : new[] { e.audioTime })
                .Select(t => (int)Math.Round(t * map.sampleRate)).OrderBy(t => t).ToArray();

        private static bool CoversOriginalHold(MapSegment original, MapSegment result)
            => result.audioTime <= original.audioTime && result.endAudioTime >= original.endAudioTime;

        private static string Bits(double value) => BitConverter.DoubleToInt64Bits(value).ToString("X16");

        [Serializable] private sealed class CoverageReportDiagnostic { public CoverageBoundaryDiagnostic[] boundaries; }
        [Serializable] private sealed class CoverageBoundaryDiagnostic
        {
            public int sourceIndex, resultSourceIndex;
            public string sourceStartBits, sourceEndBits, numericReportStartBits, numericReportEndBits, resultStartBits, resultEndBits;
            public bool numericReportBitsExact, immutableSourceCovered;
        }

        private static void RecordCoverageReportDiagnostics(MapTimeline source, MapTimeline grouped, PcmPlayablePhraseGrouping.Report report)
        {
            var records = new System.Collections.Generic.List<CoverageBoundaryDiagnostic>();
            foreach (var merge in report.physicalMerges)
            for (int i = 0; i < merge.originalHoldIndices.Length; i++)
            {
                var original = source.segments.Single(s => s.sourceIndex == merge.originalHoldIndices[i] && s.kind == "Hold");
                var result = grouped.segments.Single(s => s.sourceIndex == merge.resultSourceIndex);
                records.Add(new CoverageBoundaryDiagnostic { sourceIndex = original.sourceIndex, resultSourceIndex = result.sourceIndex,
                    sourceStartBits = Bits(original.audioTime), sourceEndBits = Bits(original.endAudioTime),
                    numericReportStartBits = Bits(merge.originalHoldStarts[i]), numericReportEndBits = Bits(merge.originalHoldEnds[i]),
                    resultStartBits = Bits(result.audioTime), resultEndBits = Bits(result.endAudioTime),
                    numericReportBitsExact = Bits(original.audioTime) == Bits(merge.originalHoldStarts[i]) && Bits(original.endAudioTime) == Bits(merge.originalHoldEnds[i]),
                    immutableSourceCovered = CoversOriginalHold(original, result) });
            }
            Directory.CreateDirectory("Library/LumiDensePhraseChecks");
            File.WriteAllText("Library/LumiDensePhraseChecks/coverage-report-diagnostic.json",
                JsonUtility.ToJson(new CoverageReportDiagnostic { boundaries = records.ToArray() }, true));
        }

        [Serializable] private sealed class JsonCloneDiagnostic
        {
            public bool jsonRoundTripEventsExact;
            public int firstChangedEvent = -1;
            public string originalEvent, clonedEvent, originalAudioBits, clonedAudioBits, originalGameBits, clonedGameBits;
        }

        private static void RecordJsonCloneDiagnostics(MapTimeline source)
        {
            var roundTrip = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(source));
            var report = new JsonCloneDiagnostic { jsonRoundTripEventsExact = PcmPlayablePhraseGrouping.EventsExactlyEqual(source.events, roundTrip.events) };
            for (int i = 0; i < source.events.Length && i < roundTrip.events.Length; i++)
            {
                var a = source.events[i]; var b = roundTrip.events[i];
                if (PcmPlayablePhraseGrouping.EventsExactlyEqual(new[] { a }, new[] { b })) continue;
                report.firstChangedEvent = i; report.originalEvent = JsonUtility.ToJson(a); report.clonedEvent = JsonUtility.ToJson(b);
                report.originalAudioBits = BitConverter.DoubleToInt64Bits(a.audioTime).ToString("X16");
                report.clonedAudioBits = BitConverter.DoubleToInt64Bits(b.audioTime).ToString("X16");
                report.originalGameBits = BitConverter.DoubleToInt64Bits(a.gameTime).ToString("X16");
                report.clonedGameBits = BitConverter.DoubleToInt64Bits(b.gameTime).ToString("X16");
                break;
            }
            Directory.CreateDirectory("Library/LumiDensePhraseChecks");
            File.WriteAllText("Library/LumiDensePhraseChecks/json-clone-diagnostic.json", JsonUtility.ToJson(report, true));
        }

        private static MusicalCue Jump(double time) => new MusicalCue { kind = "Jump", time = time, endTime = time };
        private static MapTimeline Compile(MusicalCue[] cues)
            => MusicalChartCompiler.Compile(new MusicalScore { id = "PlayableOrnamentCheck", audioDuration = 6, sampleRate = 44100,
                bpm = 112, cues = cues }, new AudioChartGenerator.Options(), 44100, 6);

        internal static void RequireNoInfeasibleRounds(MapTimeline map, float distanceScale, float lateralScale, float separation)
        {
            var segments = map.segments.OrderBy(s => s.audioTime).ToArray();
            for (int i = 1; i < segments.Length; i++)
            {
                var a = segments[i - 1]; var b = segments[i];
                if (a.kind != "Jump" || b.kind != "Jump") continue;
                double gap = b.audioTime - a.endAudioTime;
                double forward = gap * map.forwardSpeed * distanceScale;
                double lateral = ChartRouteComposer.MaxLateralShift(gap) * lateralScale;
                Require(forward * forward + lateral * lateral >= separation * separation - .000001,
                    "separate normal supports cannot fit within the existing lane budget: " + a.sourceIndex + " -> " + b.sourceIndex);
            }
        }

        private static void Require(bool pass, string message)
        { if (!pass) throw new InvalidDataException("Playable ornament regression: " + message); }
    }
}
