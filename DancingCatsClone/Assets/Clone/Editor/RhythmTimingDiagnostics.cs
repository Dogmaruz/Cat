using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RadioArchipelago;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DancingCats.Clone.Editor
{
    /// <summary>Passive Editor recorder. It does not start a game, move input, change settings or seek audio.</summary>
    [InitializeOnLoad]
    public static class RhythmTimingDiagnostics
    {
        private const string Key = "DancingCats.TimingDiagnostics";
        private static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ChartGenerationV12/Timing"));
        private static readonly List<ContactSample> Contacts = new List<ContactSample>(2048);
        private static readonly List<FrameSample> Frames = new List<FrameSample>(8192);
        private static readonly FieldInfo RigField = typeof(CourierMotion).GetField("rig", BindingFlags.NonPublic | BindingFlags.Instance);
        private static OriginalPlayController bound;
        private static CourierRigBinding rig;
        private static CourierMotion motion;
        private static int lastFrame = -1;
        private static bool subscribed;

        [Serializable] public sealed class SettingsSnapshot
        {
            public string utc, unityVersion, platform, selectedSong;
            public bool isPlaying, savedProfileExists, savedProfileParsed, liveGameExists;
            public float savedRhythmLineHeight, savedCalibrationOffsetSeconds, liveRhythmLineHeight, liveCalibrationOffsetSeconds;
            public int outputSampleRate, dspBufferFrames, dspBufferCount;
            public bool profilerEnabled;
        }
        [Serializable] public sealed class ContactSample
        {
            public string stage, resource, kind;
            public int frame, sourceIndex, tileId, musicSamples, musicFrequency;
            public bool hasTile;
            public double realTime, dspTime, gameTime, nominalGameTime, nominalAudioTime, storedAudioTime, clockOffset, dspStart;
            public double musicCursor, dspGameTime, contactMinusNominalMs, substepBacklogMs, nominalDsp, submitMinusNominalDspMs;
            public float userDelay, rate, lapOffset, jumpDistance;
            public double effectiveDelay;
            public Vector3 actorPosition, jumpBegin, tilePosition;
        }
        [Serializable] public sealed class FrameSample
        {
            public int frame, musicSamples;
            public double realTime, dspTime, gameTime, musicCursor;
            public float frameDelta;
            public bool hasRig;
            public Vector3 actorPosition, visualWorld, leftFootControlWorld, rightFootControlWorld;
            public CourierMotion.EditorTimingPose pose;
        }
        [Serializable] public sealed class Capture
        {
            public string schema = "editor-contact-clock-v1", songId, utc, endedBecause;
            public SettingsSnapshot settings;
            public ContactSample[] contacts;
            public FrameSample[] frames;
            public string limitations = "DSP/timeSamples and PlayOneShot submission are software observations. No physical audio onset, output-device latency or original-app audible latency is measured. Foot controls are rig transforms, not measured mesh soles. Recorder performs no file IO until exit.";
        }

        static RhythmTimingDiagnostics()
        {
            EditorApplication.playModeStateChanged += OnMode;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            if (SessionState.GetBool(Key + ".armed", false)) Subscribe();
        }

        public static void InspectLiveTiming()
        {
            var snapshot = Inspect();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "live-settings.json"), JsonUtility.ToJson(snapshot, true));
            Debug.Log("RHYTHM_TIMING_INSPECT " + JsonUtility.ToJson(snapshot));
        }

        private static SettingsSnapshot Inspect()
        {
            AudioSettings.GetDSPBufferSize(out int buffer, out int count);
            var snapshot = new SettingsSnapshot
            {
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(), isPlaying = EditorApplication.isPlaying,
                savedProfileExists = PlayerPrefs.HasKey(OriginalProgression.SaveKey),
                outputSampleRate = AudioSettings.outputSampleRate, dspBufferFrames = buffer,
                dspBufferCount = count, profilerEnabled = UnityEngine.Profiling.Profiler.enabled
            };
            if (snapshot.savedProfileExists)
            {
                // Parse directly: constructing OriginalProgression would normalize the user's save.
                var state = JsonUtility.FromJson<OriginalProgressionState>(PlayerPrefs.GetString(OriginalProgression.SaveKey));
                snapshot.savedProfileParsed = state != null && state.settings != null;
                if (snapshot.savedProfileParsed)
                {
                    snapshot.savedRhythmLineHeight = state.settings.rhythmLineHeight;
                    snapshot.savedCalibrationOffsetSeconds = state.settings.calibrationOffsetSeconds;
                    snapshot.selectedSong = state.selectedSong;
                }
            }
            var game = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            snapshot.liveGameExists = game != null && game.Progression != null;
            if (snapshot.liveGameExists)
            {
                snapshot.liveRhythmLineHeight = game.Progression.State.settings.rhythmLineHeight;
                snapshot.liveCalibrationOffsetSeconds = game.Progression.State.settings.calibrationOffsetSeconds;
                snapshot.selectedSong = game.Selected?.id;
            }
            return snapshot;
        }

        public static void BeginOriginalJingle() => Begin("JingleBells", "jingle-original-before.json");
        public static void BeginV11Jingle() => Begin("JingleBellsGeneratorTestV11", "jingle-v11-before.json");
        public static void BeginV11Jingle512() => Begin("JingleBellsGeneratorTestV11", "jingle-v11-buffer512.json");
        public static void BeginV11Jingle512Pose() => Begin("JingleBellsGeneratorTestV11", "jingle-v11-buffer512-pose.json");

        [Serializable] private sealed class BufferComparison
        {
            public BufferConfiguration before, requested, observed;
            public SettingsSnapshot live;
            public bool resetAccepted;
        }
        [Serializable] private sealed class NativeConfigurationContainer { public AudioConfiguration before; }
        [Serializable] private sealed class BufferConfiguration
        {
            public int dspBufferSize, sampleRate, numRealVoices, numVirtualVoices;
            public AudioSpeakerMode speakerMode;
            public static BufferConfiguration Read(AudioConfiguration c) => new BufferConfiguration
            {
                dspBufferSize = c.dspBufferSize, sampleRate = c.sampleRate,
                numRealVoices = c.numRealVoices, numVirtualVoices = c.numVirtualVoices, speakerMode = c.speakerMode
            };
            public AudioConfiguration Value => new AudioConfiguration
            {
                dspBufferSize = dspBufferSize, sampleRate = sampleRate,
                numRealVoices = numRealVoices, numVirtualVoices = numVirtualVoices, speakerMode = speakerMode
            };
        }
        private static string SerializeConfiguration(AudioConfiguration config) => JsonUtility.ToJson(BufferConfiguration.Read(config));

        public static void RunSerializationChecks()
        {
            var source = AudioSettings.GetConfiguration(); source.dspBufferSize = 512;
            string json = SerializeConfiguration(source);
            var copy = JsonUtility.FromJson<BufferConfiguration>(json).Value;
            if (copy.dspBufferSize != source.dspBufferSize || copy.sampleRate != source.sampleRate
                || copy.speakerMode != source.speakerMode || copy.numRealVoices != source.numRealVoices
                || copy.numVirtualVoices != source.numVirtualVoices)
                throw new InvalidOperationException("RHYTHM_TIMING_SERIALIZATION FAIL: audio DTO loses a configuration field.");
            var pose = new CourierMotion.EditorTimingPose { landingTime = .04f, scaleYRatio = .84f, impactCompression = .16f };
            string poseJson = JsonUtility.ToJson(pose);
            var poseCopy = JsonUtility.FromJson<CourierMotion.EditorTimingPose>(poseJson);
            if (Math.Abs(poseCopy.landingTime - .04f) > .00001 || Math.Abs(poseCopy.scaleYRatio - .84f) > .00001
                || Math.Abs(poseCopy.impactCompression - .16f) > .00001)
                throw new InvalidOperationException("RHYTHM_TIMING_SERIALIZATION FAIL: pose fields are missing.");
            var container = new BufferComparison { before = BufferConfiguration.Read(source) };
            var containerCopy = JsonUtility.FromJson<BufferComparison>(JsonUtility.ToJson(container));
            if (containerCopy.before == null || containerCopy.before.dspBufferSize != 512)
                throw new InvalidOperationException("RHYTHM_TIMING_SERIALIZATION FAIL: nested report omits its configuration DTO.");
            // Root serialization and nested-field serialization are distinct Unity rules.
            // The first report omitted native nested fields; its separate root snapshot was valid.
            string rootNativeJson = JsonUtility.ToJson(source);
            string nestedNativeJson = JsonUtility.ToJson(new NativeConfigurationContainer { before = source });
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "serialization-checks.txt"), "PASS root/nested audio DTO all five fields and rendered pose roundtrip.\n"
                + "Root native struct (valid saved snapshot): " + rootNativeJson
                + "\nNested native struct counterexample (incomplete original report): " + nestedNativeJson
                + "\nExplicit DTO: " + json + "\nPose: " + poseJson);
            Debug.Log("RHYTHM_TIMING_SERIALIZATION PASS root/nested audio-five-fields and visible-pose; native nested=" + nestedNativeJson);
        }
        public static void Apply512ForComparison()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Change the comparison audio configuration in Edit Mode.");
            if (!string.IsNullOrEmpty(SessionState.GetString(Key + ".bufferOriginal", "")))
                throw new InvalidOperationException("A buffer comparison is already active; restore it first.");
            var before = AudioSettings.GetConfiguration();
            SessionState.SetString(Key + ".bufferOriginal", SerializeConfiguration(before));
            var requested = before; requested.dspBufferSize = 512;
            bool accepted = AudioSettings.Reset(requested);
            var observed = AudioSettings.GetConfiguration();
            var comparison = new BufferComparison { before = BufferConfiguration.Read(before), requested = BufferConfiguration.Read(requested), observed = BufferConfiguration.Read(observed),
                resetAccepted = accepted, live = Inspect() };
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "buffer512-configuration.json"), JsonUtility.ToJson(comparison, true));
            if (!accepted || observed.dspBufferSize != 512)
            {
                RestoreBufferConfiguration();
                throw new InvalidOperationException("The audio backend did not accept a 512-frame buffer; original configuration restored.");
            }
            Debug.Log("RHYTHM_TIMING_BUFFER512_APPLIED " + JsonUtility.ToJson(comparison));
        }
        public static void RestoreBufferConfiguration()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Restore the comparison audio configuration in Edit Mode.");
            string saved = SessionState.GetString(Key + ".bufferOriginal", "");
            if (string.IsNullOrEmpty(saved)) throw new InvalidOperationException("No comparison audio configuration is saved.");
            var original = JsonUtility.FromJson<BufferConfiguration>(saved).Value;
            if (original.dspBufferSize == 0)
            {
                // Recover the first diagnostic's missing-property snapshot. Its only assignment was
                // dspBufferSize=512; all other configuration values remain the unchanged copied baseline.
                // The actual pre-comparison 1024 frames are retained in both before captures/live-settings-before.
                var retainedCurrent = AudioSettings.GetConfiguration();
                original = retainedCurrent; original.dspBufferSize = 1024;
                File.WriteAllText(Path.Combine(Output, "legacy-buffer-snapshot-recovery.json"),
                    "{\"faultySavedSnapshot\":" + saved + ",\"recovery\":\"Restore known recorded buffer1024 only; preserve other CURRENT fields. Exact all-five-field original snapshot was unavailable.\",\"retainedCurrent\":"
                    + SerializeConfiguration(retainedCurrent) + ",\"requestedRecovery\":" + SerializeConfiguration(original) + "}");
                Debug.Log("RHYTHM_TIMING_BUFFER_RECOVER_LEGACY_SNAPSHOT baseline=1024 otherFields=unchanged-current");
            }
            bool accepted = AudioSettings.Reset(original);
            var observed = AudioSettings.GetConfiguration();
            if (!accepted || observed.dspBufferSize != original.dspBufferSize || observed.sampleRate != original.sampleRate
                || observed.speakerMode != original.speakerMode || observed.numRealVoices != original.numRealVoices
                || observed.numVirtualVoices != original.numVirtualVoices)
                throw new InvalidOperationException("Audio configuration restoration was not confirmed; saved baseline retained for retry.");
            SessionState.EraseString(Key + ".bufferOriginal");
            File.WriteAllText(Path.Combine(Output, "buffer-restored.json"), JsonUtility.ToJson(Inspect(), true));
            Debug.Log("RHYTHM_TIMING_BUFFER_RESTORED " + observed.dspBufferSize);
        }

        public static void ApplyProjectBufferSettings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply configured audio in Edit Mode.");
            string settings = File.ReadAllText(Path.Combine(Application.dataPath, "../ProjectSettings/AudioManager.asset"));
            if (!settings.Contains("m_DSPBufferSize: 512") || !settings.Contains("m_RequestedDSPBufferSize: 512"))
                throw new InvalidOperationException("The saved project must explicitly match the shipped 512-frame audio configuration first.");
            if (!string.IsNullOrEmpty(SessionState.GetString(Key + ".bufferOriginal", "")))
                throw new InvalidOperationException("Restore the temporary comparison before applying the saved project configuration.");
            var config = AudioSettings.GetConfiguration(); config.dspBufferSize = 512;
            if (!AudioSettings.Reset(config) || AudioSettings.GetConfiguration().dspBufferSize != 512)
                throw new InvalidOperationException("Audio backend did not apply the project's 512-frame buffer.");
            File.WriteAllText(Path.Combine(Output, "project-buffer-applied.json"), SerializeConfiguration(AudioSettings.GetConfiguration()));
            Debug.Log("RHYTHM_TIMING_PROJECT_BUFFER_APPLIED 512");
        }

        public static void Begin(string songId, string filename)
        {
            if (string.IsNullOrEmpty(songId) || string.IsNullOrEmpty(filename) || Path.GetFileName(filename) != filename)
                throw new ArgumentException("Timing capture requires a song id and a plain output filename.");
            if (SessionState.GetBool(Key + ".armed", false)) throw new InvalidOperationException("A timing capture is already armed.");
            Contacts.Clear(); Frames.Clear(); bound = null; rig = null; motion = null; lastFrame = -1;
            Directory.CreateDirectory(Output);
            SessionState.SetString(Key + ".song", songId);
            SessionState.SetString(Key + ".path", Path.Combine(Output, filename));
            SessionState.SetString(Key + ".settings", JsonUtility.ToJson(Inspect()));
            SessionState.SetBool(Key + ".armed", true);
            Subscribe();
            Debug.Log("RHYTHM_TIMING_ARMED " + songId + " -> " + filename);
        }

        private static void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            OriginalPlayController.EditorTimingObserved += Observe;
            Camera.onPreCull += BeforeCamera;
            RenderPipelineManager.beginCameraRendering += BeforePipelineCamera;
        }

        private static void Unsubscribe()
        {
            OriginalPlayController.EditorTimingObserved -= Observe;
            Camera.onPreCull -= BeforeCamera;
            RenderPipelineManager.beginCameraRendering -= BeforePipelineCamera;
            subscribed = false;
        }

        private static void Observe(OriginalPlayController.EditorTimingSample sample)
        {
            if (sample.game.Selected?.id != SessionState.GetString(Key + ".song", "")) return;
            if (bound != sample.game)
            {
                bound = sample.game;
                motion = bound.Actor.GetComponentInChildren<CourierMotion>();
                rig = motion != null ? RigField.GetValue(motion) as CourierRigBinding : null;
            }
            var element = sample.tile?.element;
            double nominalGameTime = element?.gameTime ?? 0;
            double nominalDsp = sample.dspStart + (nominalGameTime - sample.clockOffset) / sample.rate;
            Contacts.Add(new ContactSample
            {
                stage = sample.stage, resource = sample.resource, hasTile = element != null,
                kind = element?.kind, sourceIndex = element?.sourceIndex ?? -1, tileId = element?.id ?? -1,
                frame = sample.frame, realTime = sample.realTime, dspTime = sample.dspTime,
                gameTime = sample.gameTime, nominalGameTime = nominalGameTime,
                // Synthesized HoldPoint wrappers carry gameTime but omit audioTime; retain both axes explicitly.
                nominalAudioTime = nominalGameTime - sample.lapOffset / sample.game.Map.forwardSpeed - sample.game.Map.delayTime,
                storedAudioTime = element?.audioTime ?? 0,
                musicSamples = sample.musicSamples, musicFrequency = sample.musicFrequency,
                musicCursor = sample.musicFrequency > 0 ? sample.musicSamples / (double)sample.musicFrequency : 0,
                clockOffset = sample.clockOffset, dspStart = sample.dspStart,
                dspGameTime = sample.clockOffset + Math.Max(0, sample.dspTime - sample.dspStart) * sample.rate,
                contactMinusNominalMs = (sample.gameTime - nominalGameTime) * 1000 / sample.rate,
                substepBacklogMs = ((sample.clockOffset + Math.Max(0, sample.dspTime - sample.dspStart) * sample.rate) - sample.gameTime) * 1000 / sample.rate,
                nominalDsp = nominalDsp, submitMinusNominalDspMs = (sample.dspTime - nominalDsp) * 1000,
                userDelay = sample.userDelay, rate = sample.rate, lapOffset = sample.lapOffset,
                jumpDistance = sample.jumpDistance, effectiveDelay = sample.effectiveDelay,
                actorPosition = sample.actorPosition, jumpBegin = sample.jumpBegin,
                tilePosition = sample.tile != null ? sample.tile.transform.position : Vector3.zero
            });
        }

        private static void BeforePipelineCamera(ScriptableRenderContext context, Camera camera) => BeforeCamera(camera);
        private static void BeforeCamera(Camera camera)
        {
            if (bound == null || bound.Phase != ClonePhase.Playing || camera.cameraType != CameraType.Game
                || camera.gameObject.scene != bound.gameObject.scene || lastFrame == Time.frameCount || Frames.Count >= 20000) return;
            lastFrame = Time.frameCount;
            int frequency = bound.Music.clip != null ? bound.Music.clip.frequency : 0;
            int samples = bound.Music.timeSamples;
            bool hasRig = rig?.visualPivot != null && rig.leftFoot != null && rig.rightFoot != null;
            Frames.Add(new FrameSample
            {
                frame = lastFrame, realTime = Time.realtimeSinceStartupAsDouble, dspTime = AudioSettings.dspTime,
                gameTime = bound.GameTime, frameDelta = Time.unscaledDeltaTime,
                musicSamples = samples, musicCursor = frequency > 0 ? samples / (double)frequency : 0,
                actorPosition = bound.Actor.position, hasRig = hasRig,
                visualWorld = hasRig ? rig.visualPivot.position : Vector3.zero,
                leftFootControlWorld = hasRig ? rig.leftFoot.position : Vector3.zero,
                rightFootControlWorld = hasRig ? rig.rightFoot.position : Vector3.zero,
                pose = motion != null ? motion.ReadEditorTimingPose() : default
            });
        }

        private static void OnMode(PlayModeStateChange mode)
        {
            if (mode == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Key + ".armed", false)) Flush("play-exit");
        }
        private static void BeforeReload()
        {
            // The initial Edit->Play reload keeps the arm. A reload during a run preserves its partial evidence.
            if (Contacts.Count > 0 && SessionState.GetBool(Key + ".armed", false)) Flush("assembly-reload");
            Unsubscribe();
        }
        public static void StopRecording() => Flush("explicit-stop");
        private static void Flush(string reason)
        {
            if (!SessionState.GetBool(Key + ".armed", false)) return;
            var capture = new Capture
            {
                songId = SessionState.GetString(Key + ".song", ""), utc = DateTime.UtcNow.ToString("O"), endedBecause = reason,
                settings = JsonUtility.FromJson<SettingsSnapshot>(SessionState.GetString(Key + ".settings", "{}")),
                contacts = Contacts.ToArray(), frames = Frames.ToArray()
            };
            SessionState.SetBool(Key + ".armed", false);
            Unsubscribe();
            File.WriteAllText(SessionState.GetString(Key + ".path", ""), JsonUtility.ToJson(capture, true));
            Debug.Log("RHYTHM_TIMING_SAVED " + capture.songId + " contacts=" + Contacts.Count(x => x.stage == "score-contact")
                + " frames=" + Frames.Count + " reason=" + reason);
            Contacts.Clear(); Frames.Clear(); bound = null; rig = null; motion = null;
        }
    }
}
