using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Offline score compiler with identity-free spectral attacks and harmonic sustain evidence. It does not isolate vocals.</summary>
    public static class AudioChartGenerator
    {
        public const string AlwaysRememberId = "AlwaysRememberMeowCat";
        private const string Version = "pcm-phrase-chart-v4";
        private const string VocalVersion = "pcm-vocal-phrase-chart-v6";
        private const double MinGap = .18, MaxGap = 1.95;

        [Serializable]
        public sealed class Options
        {
            public int catalogId = 20001;
            public string title = "Always Remember Meow", artist = "Meow Cat";
            public float forwardSpeed = 6.5f, delayTime = .6f;
            public bool includeHolds = true, includeMovingJumps = true;
        }
        private sealed class Analysis
        {
            public double hop, period, phase, rms;
            public double[] energy, novelty;
            public List<double> accents;
        }
        [MenuItem("Dancing Cats/Generate Always Remember Meow Chart")]
        public static void GenerateAlwaysRemember()
        {
            var clip = Resources.Load<AudioClip>("CustomAudio/Music/" + AlwaysRememberId);
            if (clip == null) throw new FileNotFoundException("Import the complete song at Resources/CustomAudio/Music/" + AlwaysRememberId + " first.");
            var options = new Options();
            Save(Generate(clip, AlwaysRememberId, options), clip, options);
        }

        [MenuItem("Assets/Dancing Cats/Generate Chart From Selected Audio")]
        public static void GenerateSelectedAudio()
        {
            var clip = Selection.activeObject as AudioClip;
            if (clip == null) throw new InvalidOperationException("Select an imported AudioClip inside a Resources folder.");
            string id = new string(clip.name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            var options = new Options { title = clip.name, catalogId = ResolveCatalogId(id, RecoveredCatalog.LoadOriginal(), LoadCustomSongs(), 0) };
            Save(Generate(clip, id, options), clip, options);
        }

        /// <summary>Generate in memory. Saving is explicit, so reusable analysis does not mutate assets.</summary>
        public static MapTimeline Generate(AudioClip clip, string id, Options options = null)
        {
            if (clip == null) throw new ArgumentNullException("clip");
            options = options ?? new Options();
            ValidateOptions(id, options);
            string assetPath = AssetDatabase.GetAssetPath(clip);
            string source = ProjectFile(assetPath);
            string hash = "";
            if (File.Exists(source))
                using (var stream = File.OpenRead(source)) using (var sha = SHA256.Create()) hash = Hex(sha.ComputeHash(stream));
            string scorePath = ProjectFile("Assets/Clone/Editor/MusicalScores/" + id + ".score.json");
            MapTimeline map;
            if (File.Exists(scorePath))
            {
                var score = JsonUtility.FromJson<MusicalScore>(File.ReadAllText(scorePath));
                ValidateScoreSource(score, id, hash);
                map = MusicalChartCompiler.Compile(score, options, clip.frequency, (double)clip.samples / clip.frequency);
            }
            else
            {
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                if (clip.loadState != AudioDataLoadState.Loaded)
                    throw new InvalidDataException("Audio PCM is unavailable. Use Decompress On Load and disable Load In Background on the music importer.");
                var pcm = new float[checked(clip.samples * clip.channels)];
                if (!clip.GetData(pcm, 0)) throw new InvalidDataException("AudioClip.GetData failed; import the song with Decompress On Load.");
                map = GenerateFromSamples(pcm, clip.frequency, clip.channels, id, options, PcmVocalAnalysis.LoadForClip(clip));
            }
            map.audio.channels = clip.channels;
            if (File.Exists(source))
            {
                map.audio.rawFile = assetPath; map.audio.rawSha256 = hash;
                map.audio.rawBytes = new FileInfo(source).Length;
                map.audio.sourceContainer = assetPath; map.audio.sourceContainerSha256 = hash;
                map.normalizationNotes = map.normalizationNotes.Concat(new[] { "Imported source SHA-256: " + hash }).ToArray();
            }
            map.audio.name = clip.name;
            return map;
        }

        /// <summary>Deterministic pure PCM entry point, also used by synthetic audio validation.</summary>
        public static MapTimeline GenerateFromSamples(float[] interleaved, int sampleRate, int channels, string id, Options options = null,
            PcmVocalAnalysis.Evidence vocalEvidence = null)
        {
            options = options ?? new Options();
            ValidateOptions(id, options);
            if (interleaved == null || sampleRate < 1000 || channels < 1 || interleaved.Length % channels != 0) throw new ArgumentException("Invalid interleaved PCM, channels or sample rate.");
            double duration = (double)(interleaved.Length / channels) / sampleRate;
            if (duration < 5) throw new InvalidDataException("A playable song must contain at least five seconds of decoded PCM.");
            var analysis = Analyze(interleaved, sampleRate, channels, out var spectral);
            string digest = PcmHash(interleaved);
            if (vocalEvidence != null) PcmVocalAnalysis.Validate(vocalEvidence, digest, sampleRate, channels, interleaved.Length / channels);
            if (spectral.accents.Length >= 4) analysis.accents = spectral.accents.ToList();
            double finishTime = Round(duration - Math.Max(1.1, 5 / options.forwardSpeed + .25));
            // The native Start is already support. Its next tile follows the first real attack,
            // rather than an extra invented landing at decoded-audio time zero.
            var cues = new List<MusicalCue>();
            var movingPhrases = options.includeMovingJumps ? spectral.follows.Where(cue => cue.time >= 0 && cue.endTime <= finishTime - MinGap).ToArray()
                : Array.Empty<MusicalCue>();
            var vocalHolds = vocalEvidence != null && options.includeHolds ? PcmVocalAnalysis.Analyze(vocalEvidence, analysis.accents.ToArray(), analysis.period)
                .Where(cue => cue.time >= 0 && cue.endTime <= finishTime - MinGap
                    && !movingPhrases.Any(phrase => cue.time < phrase.endTime + .078 && cue.endTime > phrase.time - .078)).ToArray()
                : Array.Empty<MusicalCue>();
            var confirmedSupports = movingPhrases.Concat(vocalHolds).OrderBy(cue => cue.time).ToArray();
            int nextSupport = 0;
            double supportedUntil = double.NegativeInfinity;
            // A separated vocal can reveal an attack hidden by the accompaniment. Its
            // measured entry must participate even when the mixed-audio peaks missed it.
            foreach (double accent in analysis.accents.Concat(confirmedSupports.Select(cue => cue.time)).Distinct().OrderBy(time => time))
            {
                // Phrase entry is refined from the attack's rising edge and may precede its
                // flux peak. Consume the whole phrase once, preserving its measured end.
                while (nextSupport < confirmedSupports.Length && confirmedSupports[nextSupport].time <= accent + .08)
                {
                    var phrase = confirmedSupports[nextSupport++];
                    if (phrase.time <= supportedUntil + .078) continue;
                    cues.Add(phrase); supportedUntil = phrase.endTime;
                }
                if (accent < 0 || accent > finishTime - MinGap || accent <= supportedUntil + .078) continue;
                var note = options.includeHolds && vocalEvidence == null ? spectral.sustained.FirstOrDefault(candidate => candidate.start <= accent + .08
                    && candidate.start >= accent - .35 && candidate.end - accent >= .45 && candidate.confidence >= .65) : null;
                // A new independent articulation shortly afterwards is evidence for another
                // jump, even when a backing instrument keeps the same harmonic pitch.
                double nextAttack = analysis.accents.FirstOrDefault(time => time > accent + .078);
                if (nextAttack > 0 && nextAttack - accent < .42) note = null;
                // A verified recurrent glide takes priority over a weaker background note.
                // Reject the ambiguous hold instead of inventing a release at the glide start.
                if (note != null && nextSupport < confirmedSupports.Length && note.end >= confirmedSupports[nextSupport].time - .078) note = null;
                double end = accent;
                if (note != null)
                {
                    end = Math.Min(finishTime - MinGap, note.end - .03);
                    // A measured release/rearticulation near the harmonic note boundary makes
                    // the support end, not a duplicate jump on the same musical accent.
                    var releases = analysis.accents.Where(time => time > accent + .4 && Math.Abs(time - note.end) <= .1).ToArray();
                    if (releases.Length > 0) end = releases.OrderBy(time => Math.Abs(time - note.end)).First();
                    end = Math.Min(end, finishTime - MinGap);
                    if (end - accent < .18) { note = null; end = accent; }
                }
                cues.Add(new MusicalCue { kind = note == null ? "Jump" : "Hold", time = Round(accent), endTime = Round(end),
                    lane = 0, confidence = note?.confidence ?? .7,
                    reason = note == null ? "Measured log spectral attack" : "Stable multi-partial pitch track; not inferred from RMS energy" });
                supportedUntil = end;
            }
            // Automatic long notes score their measured attack and release only. Pulse-grid
            // checkpoints remain available to explicitly authored scores through Generate.
            var map = MusicalChartCompiler.Compile(new MusicalScore { id = id, audioDuration = duration, sampleRate = sampleRate,
                bpm = 60 / analysis.period, beats = Array.Empty<double>(), cues = cues.ToArray() }, options, sampleRate, duration);
            map.tempoSections[0].startSample = (int)Math.Round(analysis.phase * sampleRate);
            string version = vocalEvidence == null ? Version : VocalVersion;
            map.sourceMapBundle = version; map.sourceMapBundleSha256 = digest;
            map.audio.rawSha256 = digest; map.audio.channels = channels; map.audio.bitsPerSample = 32; map.audio.rawBytes = (long)interleaved.Length * sizeof(float);
            map.timing.warning = "Mixed-audio attacks and conservative sustained-note estimates do not isolate a singer. Recurrent continuous phrases can form glides; their lateral paths use an authored palette. Long rests are preserved.";
            map.normalizationNotes = new[] { "Generator: " + version + "; decoded PCM float32 SHA-256: " + digest,
                "1024-sample Hann spectrum near 11kHz / 10ms hop; independent stereo magnitude combination; log spectral flux with local threshold and strength-ranked 160ms non-maximum suppression.",
                "Detected BPM: " + (60 / analysis.period).ToString("F4", CultureInfo.InvariantCulture) + "; phase seconds: " + analysis.phase.ToString("F6", CultureInfo.InvariantCulture),
                "Mixed-audio hold proposals require a stable pitch ridge with at least three harmonic partials and measured attack/release; a sustained single-tone RMS bed is insufficient.",
                "Follow proposals require repeated irregular articulation patterns, near-identical normalized spectra and continuous tonal energy. Boundaries come from measured attacks; six route samples use an independent authored lane palette.",
                "No source identity, reference cues, arbitrary gap filler or periodic moving jumps. A musical score takes priority when its source hash matches." };
            if (vocalEvidence != null)
            {
                map.timing.warning = "Locally separated vocal evidence drives legato/plateau holds and bounded musical support policies; mixed-audio recurrence drives glides. Lateral paths use an authored palette. This does not reproduce every designer-authored gesture.";
                map.normalizationNotes = map.normalizationNotes.Concat(new[]
                {
                    "Vocal evidence: " + vocalEvidence.analysisVersion + "; verified decoded PCM and feature SHA-256: " + vocalEvidence.featureSha256,
                    "Vocal holds use measured plateaus, returning ornaments, register-relative pickups and descending legato. Pitch-confidence gaps require continuing vocal energy; real pauses remain separate.",
                    "Musical support policies may extend an upper-register cadence through a bounded rest or end a glide before the next confirmed hold, reserving a tempo-relative flight. These endpoints express gameplay phrasing, not literal voice offsets. Voice features take priority over mixed-pitch sustain guesses.",
                    "Local separator model SHA-256: " + vocalEvidence.separatorModelSha256 + "; note model: " + vocalEvidence.noteModelSha256 + "; no audio upload."
                }).ToArray();
            }
            return map;
        }

        public static void ValidateOptions(string id, Options options)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-')) throw new ArgumentException("Use an alphanumeric chart id.", "id");
            if (options == null || float.IsNaN(options.forwardSpeed) || float.IsInfinity(options.forwardSpeed) || float.IsNaN(options.delayTime) || float.IsInfinity(options.delayTime) || options.forwardSpeed <= 0 || options.delayTime < MinGap || options.delayTime > MaxGap)
                throw new ArgumentException("Positive finite speed and a .18 to 1.95 second preroll are required.");
        }
        public static void ValidateScoreSource(MusicalScore score, string id, string actualHash)
        {
            if (score == null || !string.Equals(score.id, id, StringComparison.Ordinal) || string.IsNullOrEmpty(actualHash) || !string.Equals(score.sourceSha256, actualHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Musical score identity or SHA-256 does not match the imported source audio. Re-analyze that source before generation.");
        }
        /// <summary>Choose strong onsets before weaker neighbours, then restore chronological order.</summary>
        public static double[] SelectAccentTimes(double[] times, double[] strengths, double minimumGap)
        {
            if (times == null || strengths == null || times.Length != strengths.Length || minimumGap <= 0) throw new ArgumentException("Accent times and strengths must have equal lengths and a positive gap.");
            var selected = new List<int>();
            foreach (int i in Enumerable.Range(0, times.Length).OrderByDescending(i => strengths[i]).ThenBy(i => times[i]))
            {
                if (double.IsNaN(times[i]) || double.IsInfinity(times[i]) || double.IsNaN(strengths[i]) || double.IsInfinity(strengths[i])) throw new ArgumentException("Accent values must be finite.");
                if (selected.All(j => Math.Abs(times[j] - times[i]) >= minimumGap - .000001)) selected.Add(i);
            }
            return selected.Select(i => times[i]).OrderBy(t => t).ToArray();
        }

        private static Analysis Analyze(float[] pcm, int rate, int channels, out PcmSpectralAnalysis.Result spectral)
        {
            int frames = pcm.Length / channels, hop = Math.Max(1, rate / 100), window = hop * 2;
            int count = (frames + hop - 1) / hop;
            var energy = new double[count]; var novelty = new double[count];
            double totalSquare = 0;
            // Per-frame channel powers avoid stereo phase cancellation and keep analysis O(PCM length).
            var powers = new double[frames];
            for (int f = 0; f < frames; f++)
            {
                double power = 0;
                for (int c = 0; c < channels; c++)
                {
                    float sample = pcm[f * channels + c];
                    if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new InvalidDataException("PCM contains non-finite samples.");
                    power += (double)sample * sample;
                }
                powers[f] = power / channels; totalSquare += powers[f];
            }
            double rms = Math.Sqrt(totalSquare / frames);
            if (rms < .00001) throw new InvalidDataException("Audio is silent; no playable rhythm can be generated.");
            spectral = PcmSpectralAnalysis.Analyze(pcm, rate, channels);
            for (int n = 0; n < count; n++)
            {
                int start = n * hop, end = Math.Min(frames, start + window);
                double square = 0; for (int f = start; f < end; f++) square += powers[f];
                energy[n] = Math.Sqrt(square / (end - start));
                novelty[n] = Math.Max(0, energy[n] - (n > 0 ? energy[n - 1] : 0));
            }
            double maxNovelty = novelty.Max();
            var peaks = new List<double>(); var strengths = new List<double>();
            for (int n = 1; n + 1 < count; n++)
            {
                double mean = 0; int left = Math.Max(0, n - 40), right = Math.Min(count - 1, n + 40);
                for (int j = left; j <= right; j++) mean += novelty[j];
                mean /= right - left + 1;
                if (novelty[n] >= novelty[n - 1] && novelty[n] > novelty[n + 1] && novelty[n] > Math.Max(maxNovelty * .065, mean * 1.4))
                {
                    double time = (double)(n * hop + window / 2) / rate;
                    peaks.Add(Round(time)); strengths.Add(novelty[n]);
                }
            }
            peaks = SelectAccentTimes(peaks.ToArray(), strengths.ToArray(), MinGap).ToList();
            if (peaks.Count < 4 || maxNovelty < rms * .005)
            {
                if (spectral.accents.Length < 4) throw new InvalidDataException("Audio has insufficient rhythmic accents; author the chart manually or use a recording with a clear beat.");
                // Pitch/timbre attacks can retain constant RMS. Use their measured times
                // as pulse evidence only when the original energy route has no usable attacks.
                peaks = spectral.accents.ToList();
                Array.Clear(novelty, 0, novelty.Length);
                foreach (double accent in peaks)
                {
                    int index = Math.Max(0, Math.Min(count - 1, (int)Math.Round(accent * rate / hop)));
                    novelty[index] = 1;
                }
            }
            double hopSeconds = (double)hop / rate, bestScore = double.MinValue; int bestLag = 0;
            int minLag = (int)Math.Ceiling(60 / 180.0 / hopSeconds), maxLag = Math.Min(count / 3, (int)Math.Floor(60 / 55.0 / hopSeconds));
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                double dot = 0, aa = 0, bb = 0;
                for (int i = lag; i < count; i++) { dot += novelty[i] * novelty[i - lag]; aa += novelty[i] * novelty[i]; bb += novelty[i - lag] * novelty[i - lag]; }
                double score = dot / Math.Sqrt(Math.Max(1e-30, aa * bb));
                // A small slow-beat prior resolves exact half/double ambiguities without imposing a fixed BPM.
                score *= 1 + .03 * lag / maxLag;
                if (score > bestScore) { bestScore = score; bestLag = lag; }
            }
            double period = bestLag * hopSeconds;
            if (bestLag == 0 || bestScore < .025) throw new InvalidDataException("No stable rhythmic pulse found in the decoded recording.");
            // Refine autocorrelation's 10ms lag using intervals between measured peaks near one beat.
            var beatIntervals = new List<double>();
            for (int i = 0; i < peaks.Count; i++)
                for (int j = i + 1; j < peaks.Count && peaks[j] - peaks[i] <= period * 1.12; j++)
                    if (peaks[j] - peaks[i] >= period * .88) beatIntervals.Add(peaks[j] - peaks[i]);
            if (beatIntervals.Count > 3) { beatIntervals.Sort(); period = beatIntervals[beatIntervals.Count / 2]; }
            double phase = 0, bestPhaseScore = -1;
            for (int p = 0; p * hopSeconds < period; p++)
            {
                double candidate = p * hopSeconds, score = 0;
                foreach (double peak in peaks)
                {
                    double residual = Math.Abs((peak - candidate) / period - Math.Round((peak - candidate) / period)) * period;
                    if (residual < .065) score += 1 - residual / .065;
                }
                if (score > bestPhaseScore) { bestPhaseScore = score; phase = candidate; }
            }
            return new Analysis { hop = hopSeconds, energy = energy, novelty = novelty, period = period, phase = phase, rms = rms, accents = peaks };
        }

        private static double Round(double value) { return Math.Round(value, 6, MidpointRounding.AwayFromZero); }
        private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
        private static string PcmHash(float[] pcm)
        {
            using (var sha = SHA256.Create())
            {
                var buffer = new byte[32768];
                for (int offset = 0; offset < pcm.Length; offset += buffer.Length / sizeof(float))
                {
                    int bytes = Math.Min(buffer.Length / sizeof(float), pcm.Length - offset) * sizeof(float);
                    Buffer.BlockCopy(pcm, offset * sizeof(float), buffer, 0, bytes);
                    if (!BitConverter.IsLittleEndian) for (int b = 0; b < bytes; b += 4) Array.Reverse(buffer, b, 4);
                    sha.TransformBlock(buffer, 0, bytes, buffer, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return Hex(sha.Hash);
            }
        }
        private static string ProjectFile(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        }
        /// <summary>Resolve selected-audio identities and reject all original/custom collisions before writing.</summary>
        public static int ResolveCatalogId(string id, SongRecord[] originals, SongRecord[] customs, int requestedId)
        {
            originals = originals ?? Array.Empty<SongRecord>();
            customs = customs ?? Array.Empty<SongRecord>();
            if (originals.Any(s => string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Generated song id collides with the original catalog: " + id);
            if (customs.Any(s => string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase) && !string.Equals(s.id, id, StringComparison.Ordinal)))
                throw new InvalidDataException("Generated song id differs only by case from an existing custom song: " + id);
            var existing = customs.FirstOrDefault(s => string.Equals(s.id, id, StringComparison.Ordinal));
            int resolved = requestedId > 0 ? requestedId : existing != null ? existing.catalogId : 20001;
            if (requestedId == 0 && existing == null)
                while (originals.Any(s => s.catalogId == resolved) || customs.Any(s => s.catalogId == resolved)) resolved++;
            if (resolved < 20001 || originals.Any(s => s.catalogId == resolved) || customs.Any(s => s.id != id && s.catalogId == resolved))
                throw new InvalidDataException("Generated catalog id collides with an existing song or reserved original range: " + resolved);
            return resolved;
        }
        private static SongRecord[] LoadCustomSongs()
        {
            string path = ProjectFile("Assets/Resources/CustomData/songs.json");
            return File.Exists(path) ? RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(path)) : Array.Empty<SongRecord>();
        }
        public static void Save(MapTimeline map, AudioClip clip, Options options)
        {
            AudioChartGeneratorValidation.ValidateMap(map);
            string assetPath = AssetDatabase.GetAssetPath(clip).Replace('\\', '/');
            int resourceIndex = assetPath.LastIndexOf("/Resources/", StringComparison.Ordinal);
            if (resourceIndex < 0) throw new InvalidDataException("The audio must be inside a Resources directory for offline loading.");
            string audioResource = assetPath.Substring(resourceIndex + "/Resources/".Length);
            audioResource = audioResource.Substring(0, audioResource.Length - Path.GetExtension(audioResource).Length);
            string mapPath = "Assets/Resources/CustomData/Maps/" + map.id + ".timeline.json";
            string catalogPath = "Assets/Resources/CustomData/songs.json";
            var songs = LoadCustomSongs().ToList();
            ResolveCatalogId(map.id, RecoveredCatalog.LoadOriginal(), songs.ToArray(), options.catalogId);
            songs.RemoveAll(s => s.id == map.id);
            songs.Add(new SongRecord
            {
                id = map.id, catalogId = options.catalogId, title = options.title, artist = options.artist, catalog = map.catalog,
                audioResource = audioResource, previewAudioResource = audioResource, timelineResource = "CustomData/Maps/" + map.id + ".timeline",
                timeline = mapPath, audioFile = assetPath, audioAssetId = map.audio.assetId, sampleRate = map.sampleRate, audioLengthSeconds = map.audio.declaredLengthSeconds,
                forwardSpeed = map.forwardSpeed, delayTime = map.delayTime, elementCount = map.elements.Length, segmentCount = map.segments.Length, eventCount = map.events.Length,
                holdTickCount = map.elements.Sum(e => e.holdTicks.Length), followPathPointCount = map.segments.Sum(s => s.path.Length), finish = map.segments.Where(s => s.kind == "FinishJump").ToArray()
            });
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectFile(mapPath)));
            File.WriteAllText(ProjectFile(mapPath), JsonUtility.ToJson(map, true) + "\n");
            File.WriteAllText(ProjectFile(catalogPath), "[\n" + string.Join(",\n", songs.OrderBy(s => s.catalogId).Select(s => JsonUtility.ToJson(s, true)).ToArray()) + "\n]\n");
            AssetDatabase.ImportAsset(mapPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("AUDIO_CHART_GENERATED " + map.id + ": " + map.audio.declaredLengthSeconds.ToString("F3", CultureInfo.InvariantCulture) + " seconds, " + map.elements.Length + " elements, " + map.elements.Count(e => e.kind == "Hold") + " holds, " + map.elements.Count(e => e.updateType == 3) + " moving jumps, " + map.segments.Count(s => s.kind == "Follow") + " glides; source SHA-256 " + map.audio.rawSha256);
        }
    }
}
