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
    /// <summary>Offline energy/onset based authoring tool. It does not identify vocals or musical pitch.</summary>
    public static class AudioChartGenerator
    {
        public const string AlwaysRememberId = "AlwaysRememberMeowCat";
        private const string Version = "pcm-onset-chart-v1";
        private const double MinGap = .32, MaxGap = 1.0;

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
        private sealed class Note
        {
            public double time, end;
            public float lane;
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
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded)
                throw new InvalidDataException("Audio PCM is unavailable. Use Decompress On Load and disable Load In Background on the music importer.");
            var pcm = new float[checked(clip.samples * clip.channels)];
            if (!clip.GetData(pcm, 0)) throw new InvalidDataException("AudioClip.GetData failed; import the song with Decompress On Load.");
            var map = GenerateFromSamples(pcm, clip.frequency, clip.channels, id, options);
            string assetPath = AssetDatabase.GetAssetPath(clip);
            string source = ProjectFile(assetPath);
            if (File.Exists(source))
            {
                string hash;
                using (var stream = File.OpenRead(source)) using (var sha = SHA256.Create()) hash = Hex(sha.ComputeHash(stream));
                map.audio.rawFile = assetPath;
                map.audio.rawSha256 = hash;
                map.audio.rawBytes = new FileInfo(source).Length;
                map.audio.sourceContainer = assetPath;
                map.audio.sourceContainerSha256 = hash;
                map.normalizationNotes = map.normalizationNotes.Concat(new[] { "Imported source SHA-256: " + hash }).ToArray();
            }
            map.audio.name = clip.name;
            return map;
        }

        /// <summary>Deterministic pure PCM entry point, also used by synthetic audio validation.</summary>
        public static MapTimeline GenerateFromSamples(float[] interleaved, int sampleRate, int channels, string id, Options options = null)
        {
            options = options ?? new Options();
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-')) throw new ArgumentException("Use an alphanumeric chart id.", "id");
            if (interleaved == null || sampleRate < 1000 || channels < 1 || interleaved.Length % channels != 0) throw new ArgumentException("Invalid interleaved PCM, channels or sample rate.");
            if (float.IsNaN(options.forwardSpeed) || float.IsInfinity(options.forwardSpeed) || float.IsNaN(options.delayTime) || float.IsInfinity(options.delayTime) || options.forwardSpeed <= 0 || options.delayTime < MinGap || options.delayTime > MaxGap) throw new ArgumentException("Positive speed and a .32 to 1 second preroll are required.");
            double duration = (double)(interleaved.Length / channels) / sampleRate;
            if (duration < 5) throw new InvalidDataException("A playable song must contain at least five seconds of decoded PCM.");
            var analysis = Analyze(interleaved, sampleRate, channels);
            double finishTime = Round(duration - Math.Max(1.1, 5 / options.forwardSpeed + .25));
            var times = new List<double> { 0 };
            foreach (double accent in analysis.accents)
            {
                if (accent < MinGap || accent > finishTime - MinGap || accent - times[times.Count - 1] < MinGap) continue;
                FillGap(times, accent, analysis.period);
                times.Add(Round(accent));
            }
            times.RemoveAll(t => t > finishTime - MinGap);
            FillGap(times, finishTime, analysis.period);
            times.Add(finishTime);
            var notes = new List<Note>();
            double nextHold = Math.Max(8, analysis.period * 8);
            // The repeating phrase lanes are deterministic; accents and hold lengths come from PCM.
            float[] phraseLanes = { 0, .5f, 0, -.5f, -.5f, 0, .5f, 0 };
            for (int i = 0; i < times.Count; i++)
            {
                double time = times[i], end = time;
                if (options.includeHolds && time >= nextHold && i + 3 < times.Count)
                {
                    int endIndex = i + 2;
                    double proposedEnd = times[endIndex];
                    if (proposedEnd - time >= .6 && proposedEnd - time <= 2.1 && Sustained(analysis, time, proposedEnd))
                    {
                        end = proposedEnd;
                        i = endIndex;
                        nextHold = end + analysis.period * 8;
                    }
                }
                int phraseBeat = (int)Math.Floor(Math.Max(0, time - analysis.phase) / analysis.period + .1);
                notes.Add(new Note { time = time, end = end, lane = phraseLanes[phraseBeat % phraseLanes.Length] });
            }
            notes[0].lane = 0;
            notes[notes.Count - 1].lane = 0;
            string pcmDigest = PcmHash(interleaved);
            var metadata = new OriginalSongMetadata
            {
                id = options.catalogId, musicName = options.title, artName = options.artist, musicPath = id,
                scene = "level1", builtin = true, disable = false, musicUnlockType = 0, baseSpeed = options.forwardSpeed,
                difficultyType = 1, speedAdd = Array.Empty<float>()
            };
            var map = new MapTimeline
            {
                schemaVersion = 1, id = id, sampleRate = sampleRate, mapAssetId = "custom:" + id,
                sourceMapBundle = Version, sourceMapBundleSha256 = pcmDigest, sourceMapUrl = "",
                forwardSpeed = options.forwardSpeed, delayTime = options.delayTime, catalog = metadata,
                audio = new OriginalAudioMetadata
                {
                    assetId = "custom:" + id, name = id, category = "Music", rawSha256 = pcmDigest,
                    rawBytes = (long)interleaved.Length * sizeof(float), frequency = sampleRate, channels = channels,
                    bitsPerSample = 32, declaredLengthSeconds = duration, files = Array.Empty<OriginalAudioFile>()
                },
                timing = new TimingDescription
                {
                    gameTime = "audioTime + delayTime", audioTime = "gameTime - delayTime", eventGameTime = "sample / sampleRate + delayTime",
                    warning = "Energy/onset heuristic with beat scaffolding; manual playability and listening QA remain required."
                },
                tempoSections = new[] { new TempoSection { sectionName = "Measured stable beat", startSample = (int)Math.Round(analysis.phase * sampleRate), samplesPerBeat = analysis.period * sampleRate, beatsPerMeasure = 4, bStartNewMeasure = 1 } },
                normalizationNotes = new[]
                {
                    "Generator: " + Version + "; decoded PCM float32 SHA-256: " + pcmDigest,
                    "20ms channel-energy windows / 10ms hop; positive RMS flux; normalized autocorrelation 55-180 BPM; measured onset phase.",
                    "Detected BPM: " + (60 / analysis.period).ToString("F4", CultureInfo.InvariantCulture) + "; phase seconds: " + analysis.phase.ToString("F6", CultureInfo.InvariantCulture) + "; accented peaks: " + analysis.accents.Count,
                    "Landings prioritize actual accents; beat scaffolding bounds .32-1.0 second arcs. Sustained RMS floor selects holds; this does not detect vocal pitch or words.",
                    "Native holds use negative se.y length and ext checkpoint Zs; outgoing distance begins at hold exit. Sparse moving jumps use native cycle phase zero at landing.",
                    "FinishJump launches five units to the original end platform while decoded audio is still playing."
                }
            };
            var elements = new List<MapElement>();
            var segments = new List<MapSegment>();
            elements.Add(Element(map, 0, "Start", -map.delayTime, -map.delayTime, 0, 0));
            segments.Add(Segment(elements[0], -map.delayTime, map));
            double nextMoving = 18;
            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                bool finish = i == notes.Count - 1, hold = note.end > note.time;
                string kind = finish ? "FinishJump" : hold ? "Hold" : "Jump";
                var element = Element(map, finish ? 1 : hold ? 2 : 0, kind, note.time, note.end, note.lane, elements.Count);
                if (options.includeMovingJumps && !finish && !hold && i > 0 && i + 1 < notes.Count && note.time >= nextMoving &&
                    note.time - notes[i - 1].end >= .6 && notes[i + 1].time - note.time >= .6 &&
                    Math.Abs(notes[i - 1].lane) <= .5f && Math.Abs(notes[i + 1].lane) <= .5f)
                {
                    element.updateType = 3;
                    element.pos.x = 0; // Native MoveCycleX(initialX, 0, direction) == 0.
                    element.msLeft = ((int)(note.time / 24)) % 2;
                    nextMoving = note.time + 24;
                }
                elements.Add(element);
                segments.Add(Segment(element, note.end, map));
            }
            for (int i = 0; i < elements.Count; i++)
            {
                elements[i].jumpDistance = i + 1 < elements.Count ? elements[i + 1].pos.z - segments[i].endZ : 5;
                segments[i].motion.jumpDistance = elements[i].jumpDistance;
            }
            var events = new List<KoreographyEvent>();
            foreach (var element in elements.Skip(1))
            {
                AddEvent(events, element.audioTime, element.kind == "FinishJump" ? 100 : 0, map);
                foreach (var tick in element.holdTicks.Skip(1)) AddEvent(events, tick.audioTime, 0, map);
            }
            map.elements = elements.ToArray(); map.segments = segments.ToArray(); map.events = events.OrderBy(e => e.mStartSample).ToArray();
            for (int i = 0; i < map.events.Length; i++) map.events[i].index = i;
            return map;
        }

        private static Analysis Analyze(float[] pcm, int rate, int channels)
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
            for (int n = 0; n < count; n++)
            {
                int start = n * hop, end = Math.Min(frames, start + window);
                double square = 0; for (int f = start; f < end; f++) square += powers[f];
                energy[n] = Math.Sqrt(square / (end - start));
                novelty[n] = Math.Max(0, energy[n] - (n > 0 ? energy[n - 1] : 0));
            }
            double maxNovelty = novelty.Max();
            var peaks = new List<double>();
            for (int n = 1; n + 1 < count; n++)
            {
                double mean = 0; int left = Math.Max(0, n - 40), right = Math.Min(count - 1, n + 40);
                for (int j = left; j <= right; j++) mean += novelty[j];
                mean /= right - left + 1;
                if (novelty[n] >= novelty[n - 1] && novelty[n] > novelty[n + 1] && novelty[n] > Math.Max(maxNovelty * .065, mean * 1.4))
                {
                    double time = (double)(n * hop + window / 2) / rate;
                    if (peaks.Count == 0 || time - peaks[peaks.Count - 1] >= .12) peaks.Add(Round(time));
                }
            }
            if (peaks.Count < 4 || maxNovelty < rms * .005) throw new InvalidDataException("Audio has insufficient rhythmic accents; author the chart manually or use a recording with a clear beat.");
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

        private static bool Sustained(Analysis analysis, double start, double end)
        {
            int first = Math.Max(0, (int)Math.Ceiling((start + .1) / analysis.hop));
            int last = Math.Min(analysis.energy.Length - 1, (int)((end - .1) / analysis.hop));
            if (last <= first) return false;
            int active = 0; double novelty = 0;
            for (int i = first; i <= last; i++) { if (analysis.energy[i] >= analysis.rms * .23) active++; novelty += analysis.novelty[i]; }
            return (double)active / (last - first + 1) >= .92 && novelty / (last - first + 1) < analysis.rms * .055;
        }
        private static void FillGap(List<double> times, double target, double period)
        {
            while (target - times[times.Count - 1] > MaxGap)
            {
                double last = times[times.Count - 1], step = Math.Min(.8, Math.Max(MinGap, period));
                if (target - last - step < MinGap) step = (target - last) / 2;
                times.Add(Round(last + step));
            }
        }
        private static MapElement Element(MapTimeline map, int id, string kind, double audio, double endAudio, float lane, int index)
        {
            double game = audio + map.delayTime;
            var ticks = new List<HoldTick>();
            if (endAudio > audio)
            {
                int count = Math.Max(1, (int)Math.Ceiling((endAudio - audio) / .5));
                for (int i = 0; i <= count; i++)
                {
                    double tickAudio = audio + (endAudio - audio) * i / count;
                    ticks.Add(new HoldTick { audioTime = tickAudio, gameTime = tickAudio + map.delayTime, z = (float)((tickAudio + map.delayTime) * map.forwardSpeed) });
                }
            }
            return new MapElement
            {
                id = id, name = kind, kind = kind, sourceIndex = index,
                gameTime = game, audioTime = audio, audioSampleFromPosition = audio * map.sampleRate,
                pos = new Vector3(lane, 0, (float)(game * map.forwardSpeed)), begin = 50, end = 5,
                se = endAudio > audio ? new Vector2(0, -(float)((endAudio - audio) * map.forwardSpeed)) : new Vector2(.775f, -.775f),
                ext = string.Join(",", ticks.Select(t => t.z.ToString("R", CultureInfo.InvariantCulture)).ToArray()), holdTicks = ticks.ToArray()
            };
        }
        private static MapSegment Segment(MapElement element, double endAudio, MapTimeline map)
        {
            double endGame = endAudio + map.delayTime;
            return new MapSegment
            {
                sourceIndex = element.sourceIndex, id = element.id, kind = element.kind, name = element.name,
                x = element.pos.x, z = element.pos.z, endZ = (float)(endGame * map.forwardSpeed),
                gameTime = element.gameTime, audioTime = element.audioTime, endGameTime = endGame, endAudioTime = endAudio,
                duration = endAudio - element.audioTime, holdTicks = element.holdTicks, elementIndices = new[] { element.sourceIndex }, path = Array.Empty<ChartPoint>(),
                motion = new MotionParameters { updateType = element.updateType, msLeft = element.msLeft, begin = element.begin, end = element.end, se = element.se, ext = element.ext }
            };
        }
        private static void AddEvent(List<KoreographyEvent> events, double audio, int command, MapTimeline map)
        {
            int sample = (int)Math.Round(audio * map.sampleRate);
            events.Add(new KoreographyEvent { index = events.Count, command = command, mStartSample = sample, mEndSample = sample, audioTime = (double)sample / map.sampleRate,
                endAudioTime = (double)sample / map.sampleRate, gameTime = (double)sample / map.sampleRate + map.delayTime, endGameTime = (double)sample / map.sampleRate + map.delayTime });
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
        private static void Save(MapTimeline map, AudioClip clip, Options options)
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
                holdTickCount = map.elements.Sum(e => e.holdTicks.Length), followPathPointCount = 0, finish = map.segments.Where(s => s.kind == "FinishJump").ToArray()
            });
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectFile(mapPath)));
            File.WriteAllText(ProjectFile(mapPath), JsonUtility.ToJson(map, true) + "\n");
            File.WriteAllText(ProjectFile(catalogPath), "[\n" + string.Join(",\n", songs.OrderBy(s => s.catalogId).Select(s => JsonUtility.ToJson(s, true)).ToArray()) + "\n]\n");
            AssetDatabase.ImportAsset(mapPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("AUDIO_CHART_GENERATED " + map.id + ": " + map.audio.declaredLengthSeconds.ToString("F3", CultureInfo.InvariantCulture) + " seconds, " + map.elements.Length + " elements, " + map.elements.Count(e => e.kind == "Hold") + " holds, " + map.elements.Count(e => e.updateType == 3) + " moving jumps; source SHA-256 " + map.audio.rawSha256);
        }
    }
}
