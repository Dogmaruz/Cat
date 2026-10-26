using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DancingCats.Clone;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RadioArchipelago.Editor
{
    /// <summary>Runs only in the open Editor. Uses an isolated profile and real Input System pointer events.</summary>
    [InitializeOnLoad]
    public static class RadioEditorValidation
    {
        private const string Key = "RadioArchipelago.EditorReview";
        private const string ScenePath = "Assets/Clone/Scenes/DancingCats.unity";
        private const string CourierKey = "RadioArchipelago.v1.courier";
        private const string GuideKey = "DancingCatsClone.v1.howToPlaySeen";
        private const string SongId = "AlwaysRememberMeowCat";
        private const string ComparisonId = "UnstoppableGeneratorTest";
        private static string ReviewSongId => SessionState.GetString(Key + ".song", SongId);
        private static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/RadioArchipelagoReview"));
        private static readonly List<string> Notes = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static readonly List<InputDevice> DisabledDevices = new List<InputDevice>();
        private static OriginalPlayController game;
        private static CourierCatalog couriers;
        private static Mouse pointer, priorMouse;
        private static InputSettings priorInput, reviewInput;
        private static int step, frames, pressedFrame, scoreAtPause, samplesAtPause, selectedSong;
        private static float dragOrigin, songLength;
        private static double deadline, readyAt, pauseClock, pauseDsp, startDsp, lastClock, lastDsp;
        private static bool bot, capturedHold, capturedGlide, capturedSideways;
        private static readonly Dictionary<int, Vector2> MovingRanges = new Dictionary<int, Vector2>();
        private static LocalPose[] pausedMovingTiles;
        private static readonly Vector2Int[] MenuSizes = { new Vector2Int(540, 960), new Vector2Int(390, 844), new Vector2Int(1080, 1920) };
        private static int menuSize, menuCourier;
        private static LocalPose[] pausedCourierPose;
        private static BlendPose[] pausedCourierBlends;

        private struct LocalPose
        {
            public Transform node;
            public Vector3 position, scale;
            public Quaternion rotation;
        }
        private struct BlendPose { public SkinnedMeshRenderer renderer; public int index; public float weight; }

        [Serializable] private sealed class SavedScene { public string path; public bool loaded, active; }
        [Serializable] private sealed class Snapshot
        {
            public SavedScene[] scenes;
            public string[] selection;
            public string activeSelection;
            public bool hadProfile, hadGuide, hadCourier;
            public string profile;
            public int guide, courier;
        }

        static RadioEditorValidation()
        {
            EditorApplication.playModeStateChanged += OnMode;
            if (SessionState.GetBool(Key, false)) Application.logMessageReceived += OnLog;
        }

        [MenuItem("Radio Archipelago/Review/Check assets")]
        public static void RunChecks()
        {
            Notes.Clear();
            try
            {
                Check(!EditorApplication.isCompiling && !EditorApplication.isUpdating, "Editor imports and compilation have finished");
                Check(TMPro.TMP_Settings.LoadDefaultSettings() != null, "official TextMeshPro essential resources are imported");
                var catalog = LoadCatalog();
                Check(catalog.lighting != null && catalog.lighting.reflections != null, "couriers share their studio lighting profile and static HDR reflections");
                Check(catalog.entries != null && catalog.entries.Length == 3, "three original courier definitions are present");
                Check(catalog.entries.Select(entry => entry.id).Distinct(StringComparer.Ordinal).Count() == 3, "courier identifiers are unique");
                foreach (var entry in catalog.entries)
                {
                    Check(!string.IsNullOrWhiteSpace(entry.id) && !string.IsNullOrWhiteSpace(entry.displayName), "courier " + entry.id + " has an identifier and display name");
                    Check(entry.prefab != null && entry.icon != null, "courier " + entry.id + " has its prefab and portrait");
                    Check(entry.prefab.GetComponentInChildren<CourierMotion>(true) != null, "courier " + entry.id + " has independent motion");
                    Check(CourierMeshes(entry.prefab, true).Any(), "courier " + entry.id + " contains real 3D mesh geometry");
                    Check(entry.prefab.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.sharedMaterials.All(material => material != null && material.shader != null && !material.shader.name.Contains("InternalError"))), "courier " + entry.id + " has valid rendering materials");
                    var renderers = entry.prefab.GetComponentsInChildren<Renderer>(true);
                    var bounds = new Bounds(renderers[0].bounds.center, Vector3.zero);
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Check(bounds.size.y > .2f && bounds.size.y < 8 && bounds.size.x > .1f && bounds.size.x < 8, "courier " + entry.id + " has finite, usable preview bounds");
                }
                var art = AssetDatabase.FindAssets("t:RadioArtAssets").Select(AssetDatabase.GUIDToAssetPath)
                    .Select(path => AssetDatabase.LoadMainAssetAtPath(path)).FirstOrDefault();
                Check(art != null, "the menu art catalog is present");
                Check(ReadMember(art, "menuBackground") is Sprite, "the menu has its own background sprite");
                Check(ReadMember(art, "font") is Font, "the menu font reference is assigned");
                foreach (var field in art.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).Where(field => field.FieldType == typeof(Sprite)))
                    Check(field.GetValue(art) is Sprite, "the original interface sprite " + field.Name + " is assigned");
                foreach (string dependency in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(art), true))
                    Check(!dependency.StartsWith("Assets/Recovered/", StringComparison.Ordinal) && !dependency.StartsWith("Assets/Resources/", StringComparison.Ordinal), "the menu art has no recovered dependency: " + dependency);
                var builder = FindType("CourierAssetBuilder");
                var validate = builder?.GetMethod("Validate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                Check(validate != null, "courier asset builder exposes a read-only Validate check");
                validate.Invoke(null, null);
                Check(File.Exists(ScenePath), "the playable scene remains available");
                WriteResult("checks.txt", true, "Asset references, independent courier geometry/materials/motion, art and builder validation passed.");
            }
            catch (Exception error)
            {
                WriteResult("checks.txt", false, error.ToString());
                throw;
            }
        }

        [MenuItem("Radio Archipelago/Review/Play menu and full song")]
        public static void RunPlaytest() => StartReview(false);

        public static void RunComparisonPlaytest() => StartReview(false, ComparisonId);
        public static void RunNamedPlaytest(string songId) => StartReview(false, songId);
        public static void RunV9DisconnectPlaytest() => StartReview(false, "DisconnectPopGeneratorTestV9");
        public static void RunV9CanCanPlaytest() => StartReview(false, "CanCanPianoGeneratorTestV9");
        public static void RunV9DancePlaytest() => StartReview(false, "IJustWannaDancePopGeneratorTestV9");
        public static void RunV9CloudsPlaytest() => StartReview(false, "MoonlightCloudsPianoGeneratorTestV9");
        public static void RunV10DisconnectPlaytest() => StartReview(false, "DisconnectPopGeneratorTestV10");
        public static void RunV10CanCanPlaytest() => StartReview(false, "CanCanPianoGeneratorTestV10");
        public static void RunV10DancePlaytest() => StartReview(false, "IJustWannaDancePopGeneratorTestV10");
        public static void RunV10MoonlightPlaytest() => StartReview(false, "MoonlightPopGeneratorTestV10");
        public static void RunV10ElisePlaytest() => StartReview(false, "FurElisePianoGeneratorTestV10");
        public static void RunV10JinglePlaytest() => StartReview(false, "JingleBellsGeneratorTestV10");
        public static void RunV11CanCanPlaytest() => StartReview(false, "CanCanPianoGeneratorTestV11");
        public static void RunV11ElisePlaytest() => StartReview(false, "FurElisePianoGeneratorTestV11");
        public static void RunV11JinglePlaytest() => StartReview(false, "JingleBellsGeneratorTestV11");
        public static void RunV11OdePlaytest() => StartReview(false, "OdetoJoyPianoGeneratorTestV11");
        public static void RunV11BajoPlaytest() => StartReview(false, "BajoElSolPopGeneratorTestV11");
        public static void RunV11BabyPlaytest() => StartReview(false, "BabySharkGeneratorTestV11");

        [MenuItem("Radio Archipelago/Review/Check live menu preview framing")]
        public static void RunMenuPreviewReview() => StartReview(true);

        private static void StartReview(bool menuOnly, string songId = SongId)
        {
            if (SessionState.GetBool(Key, false)) throw new InvalidOperationException("A Radio Archipelago review is already running.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Start the review in Edit Mode after imports and compilation have finished.");
            var open = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            if (open.Any(scene => scene.isDirty || string.IsNullOrEmpty(scene.path)))
                throw new InvalidOperationException("Review refused: every open scene must already be saved and clean. No scene was saved or discarded.");
            var snapshot = new Snapshot
            {
                scenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => new SavedScene { path = scene.path, loaded = scene.isLoaded, active = scene.isActive }).ToArray(),
                selection = Selection.objects.Select(value => GlobalObjectId.GetGlobalObjectIdSlow(value).ToString()).ToArray(),
                activeSelection = Selection.activeObject != null ? GlobalObjectId.GetGlobalObjectIdSlow(Selection.activeObject).ToString() : "",
                hadProfile = PlayerPrefs.HasKey(OriginalProgression.SaveKey), profile = PlayerPrefs.GetString(OriginalProgression.SaveKey, ""),
                hadGuide = PlayerPrefs.HasKey(GuideKey), guide = PlayerPrefs.GetInt(GuideKey, 0),
                hadCourier = PlayerPrefs.HasKey(CourierKey), courier = PlayerPrefs.GetInt(CourierKey, 0)
            };
            SessionState.SetString(Key + ".snapshot", JsonUtility.ToJson(snapshot));
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".menuOnly", menuOnly);
            SessionState.SetString(Key + ".song", songId);
            SessionState.SetBool(Key + ".finishing", false);
            SessionState.SetBool(Key + ".viewCaptured", false);
            SessionState.SetString(Key + ".viewSizes", "");
            Notes.Clear(); Errors.Clear();
            try
            {
                PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
                PlayerPrefs.SetInt(GuideKey, 1); PlayerPrefs.SetInt(CourierKey, 0); PlayerPrefs.Save();
                SelectGameViewSize(540, 960);
                EditorSceneManager.OpenScene(ScenePath);
                Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
                EditorApplication.isPlaying = true;
            }
            catch (Exception error)
            {
                Finish(false, "Unable to enter review: " + error);
                throw;
            }
        }

        private static void OnMode(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                priorInput = InputSystem.settings; reviewInput = UnityEngine.Object.Instantiate(priorInput);
                reviewInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                reviewInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = reviewInput;
                priorMouse = Mouse.current; pointer = InputSystem.AddDevice<Mouse>("Radio Archipelago QA pointer"); pointer.MakeCurrent();
                foreach (var device in InputSystem.devices.Where(device => device.native && device.enabled && (device is Mouse || device is Keyboard)).ToArray())
                { DisabledDevices.Add(device); InputSystem.DisableDevice(device); }
                game = null; couriers = null; bot = false; capturedHold = capturedGlide = capturedSideways = false;
                MovingRanges.Clear();
                step = frames = 0; readyAt = -1; deadline = EditorApplication.timeSinceStartup + 40;
                // VContainer can start entry points before EnteredPlayMode. Preserve faults
                // captured by the InitializeOnLoad log hook even when no UI was created.
                Notes.Clear();
                Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
                var loop = PlayerLoop.GetCurrentPlayerLoop();
                loop.subSystemList = (loop.subSystemList ?? Array.Empty<PlayerLoopSystem>()).Where(system => system.type != typeof(RadioEditorValidation))
                    .Concat(new[] { new PlayerLoopSystem { type = typeof(RadioEditorValidation), updateDelegate = Tick } }).ToArray();
                PlayerLoop.SetPlayerLoop(loop);
            }
            if (mode == PlayModeStateChange.ExitingPlayMode) CleanupInput();
            if (mode == PlayModeStateChange.EnteredEditMode)
            {
                if (!SessionState.GetBool(Key + ".finishing", false))
                {
                    SessionState.SetBool(Key + ".success", false);
                    SessionState.SetString(Key + ".details", "Play Mode stopped before the review completed.");
                }
                RestoreEditor();
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || SessionState.GetBool(Key + ".finishing", false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if (Errors.Count != 0) throw new InvalidOperationException("Runtime error: " + string.Join(" | ", Errors.Take(3)));
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Step " + step + ", phase=" + game?.Phase + ", song clock=" + game?.GameTime);
                if (game == null) game = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
                if (game == null || game.Presenter == null || game.Presenter.UICanvas == null) return;
                if (readyAt < 0) readyAt = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - readyAt < 1.5) return;
                frames++;
                if (SessionState.GetBool(Key + ".menuOnly", false)) { TickMenuPreview(); return; }
                switch (step)
                {
                    case 0:
                        couriers = LoadCatalog();
                        Check(game.Presenter.GetType().FullName == "RadioArchipelago.UI.RadioMenuPresenter", "the injected presentation is Radio Archipelago");
                        Check(!UnityEngine.Object.FindObjectsByType<OriginalUIPresenter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any(presenter => presenter.enabled), "the legacy UI presenter is inactive");
                        Check(game.Phase == ClonePhase.Menu, "startup opens the new home screen");
                        Check(game.Songs.Any(song => song.id == ReviewSongId), "the reviewed recording remains in the catalog");
                        Capture("01-home-540x960.png", 540, 960);
                        InvokeButton("CourierButton_1"); Next(1); break;
                    case 1:
                        if (frames < 4) return;
                        CheckActor(1); Capture("02-courier-two.png", 540, 960);
                        InvokeButton("CourierButton_2"); Next(2); break;
                    case 2:
                        if (frames < 4) return;
                        CheckActor(2); Capture("03-courier-three.png", 540, 960);
                        InvokeButton("CourierButton_0"); Next(3); break;
                    case 3:
                        if (frames < 4) return;
                        CheckActor(0); selectedSong = game.Selected.catalogId;
                        InvokeButton("NextSongButton"); Next(4); break;
                    case 4:
                        if (frames < 3) return;
                        Check(game.Selected.catalogId != selectedSong, "next-song control changes the active song");
                        InvokeButton("PreviousSongButton"); Next(5); break;
                    case 5:
                        if (frames < 3) return;
                        Check(game.Selected.catalogId == selectedSong, "previous-song control restores the previous selection");
                        InvokeButton("SongLibraryButton"); Next(6); break;
                    case 6:
                        if (frames < 3) return;
                        Check(game.Presenter.IsBlockingInput, "the song library blocks gameplay pointer input");
                        Capture("04-song-library.png", 540, 960);
                        InvokeButton("ClosePanelButton_Songs"); Next(7); break;
                    case 7:
                        if (frames < 3) return;
                        InvokeButton("SettingsButton"); Next(8); break;
                    case 8:
                        if (frames < 3) return;
                        Check(game.Presenter.IsBlockingInput, "settings block gameplay pointer input");
                        bool sound = game.Progression.State.settings.sound;
                        InvokeButton("SoundToggle");
                        Check(game.Progression.State.settings.sound != sound, "the new sound setting updates the actual profile");
                        InvokeButton("SoundToggle");
                        Check(game.Progression.State.settings.sound == sound, "the sound setting can be restored through the same control");
                        float calibration = game.Progression.State.settings.rhythmLineHeight;
                        InvokeButton("CalibrationPlusButton");
                        Check(game.Progression.State.settings.rhythmLineHeight > calibration, "the calibration control updates the playback offset");
                        InvokeButton("CalibrationMinusButton");
                        Check(Mathf.Abs(game.Progression.State.settings.rhythmLineHeight - calibration) < .0001f, "calibration controls preserve the original offset after a round trip");
                        Capture("05-settings.png", 540, 960);
                        InvokeButton("ClosePanelButton_Settings"); InvokeButton("Navigation_Atlas"); Next(9); break;
                    case 9:
                        if (frames < 3) return;
                        Capture("06-atlas.png", 540, 960);
                        InvokeButton("Navigation_Workshop"); Next(10); break;
                    case 10:
                        if (frames < 3) return;
                        Capture("07-workshop.png", 540, 960);
                        InvokeButton("WorkshopCourierButton_1"); Next(11); break;
                    case 11:
                        if (frames < 3) return;
                        CheckActor(1); InvokeButton("Navigation_Play"); Next(12); break;
                    case 12:
                        if (frames < 3) return;
                        SelectGameViewSize(390, 844); Next(13); break;
                    case 13:
                        if (frames < 12 || Screen.width != 390 || Screen.height != 844) return;
                        Capture("08-home-390x844.png", 390, 844);
                        Check(Button("PlayButton") != null && Button("PlayButton").gameObject.activeInHierarchy, "primary start control remains available at 390x844");
                        SelectGameViewSize(540, 960); Next(14); break;
                    case 14:
                        if (frames < 12 || Screen.width != 540 || Screen.height != 960) return;
                        var song = game.Songs.Single(record => record.id == ReviewSongId);
                        // Original reference songs may be locked in the fresh disposable test
                        // profile. The user's complete profile is restored by FinishReview.
                        if (!game.Progression.IsSongUnlocked(song.id))
                        {
                            game.Progression.State.unlockedSongs.Add(song.id);
                            game.Progression.Save();
                            Notes.Add("SETUP unlocked the reference song in the disposable review profile only");
                        }
                        Check(game.SelectSong(song.catalogId), "the reviewed song can be selected for the new menu");
                        game.Endless = false; Next(15); break;
                    case 15:
                        if (frames < 3) return;
                        Capture("09-custom-song-menu.png", 540, 960);
                        BeginClick(Button("PlayButton")); Next(16); break;
                    case 16:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release(); Next(17); break;
                    case 17:
                        if (frames < 3 || game.Phase != ClonePhase.Playing) return;
                        Check(game.Music != null && game.Music.clip != null, "start uses the real scheduled song AudioSource");
                        songLength = game.Music.clip.length; startDsp = AudioSettings.dspTime;
                        Check(songLength > 5 && Math.Abs(songLength - game.Selected.audioLengthSeconds) < .002
                            && Math.Abs(songLength - game.Map.audio.declaredLengthSeconds) < .002,
                            "the complete source recording is unchanged in duration");
                        CheckActor(1); dragOrigin = game.Actor.position.x; bot = true; Hold(dragOrigin);
                        Next(18, songLength + 65); break;
                    case 18:
                        Bot();
                        if (game.GameTime < game.Map.delayTime + 4) return;
                        Check(game.Score > 0 && game.Phase == ClonePhase.Playing && game.Music.isPlaying, "real pointer input scores actual landings while the recording plays");
                        Capture("10-gameplay.png", 540, 960);
                        bot = false; Release(); pressedFrame = Time.frameCount; Next(19); break;
                    case 19:
                        if (Time.frameCount <= pressedFrame + 1) return;
                        BeginClick(Button("PauseButton")); Next(20); break;
                    case 20:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        if (game.Phase != ClonePhase.Paused) return;
                        pauseClock = game.GameTime; pauseDsp = AudioSettings.dspTime; scoreAtPause = game.Score; samplesAtPause = game.Music.timeSamples;
                        pausedCourierPose = SnapshotCourier();
                        pausedMovingTiles = game.Tiles.Where(tile => tile.element.updateType == 3)
                            .Select(tile => new LocalPose { node = tile.transform, position = tile.transform.localPosition }).ToArray();
                        pausedCourierBlends = game.Actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .Where(renderer => renderer.sharedMesh != null)
                            .SelectMany(renderer => Enumerable.Range(0, renderer.sharedMesh.blendShapeCount)
                                .Select(index => new BlendPose { renderer = renderer, index = index, weight = renderer.GetBlendShapeWeight(index) })).ToArray();
                        Next(21); break;
                    case 21:
                        if (AudioSettings.dspTime - pauseDsp < .8) return;
                        Check(game.Phase == ClonePhase.Paused && Math.Abs(game.GameTime - pauseClock) < .0001 && game.Score == scoreAtPause &&
                            !game.Music.isPlaying && game.Music.timeSamples == samplesAtPause, "pause freezes the chart, score and audio position");
                        Check(pausedCourierPose != null && pausedCourierPose.All(pose => pose.node != null &&
                            (pose.node.localPosition - pose.position).sqrMagnitude < 1e-9f &&
                            (pose.node.localScale - pose.scale).sqrMagnitude < 1e-9f &&
                            Quaternion.Angle(pose.node.localRotation, pose.rotation) < .02f),
                            "pause freezes actual courier bones, squash, feet, blinking and secondary animation transforms");
                        Check(pausedCourierBlends.All(pose => pose.renderer != null && Mathf.Abs(pose.renderer.GetBlendShapeWeight(pose.index) - pose.weight) < .001f),
                            "pause freezes any actual courier blink blend shapes");
                        Check(pausedMovingTiles.All(pose => pose.node != null && (pose.node.localPosition - pose.position).sqrMagnitude < 1e-9f),
                            "pause freezes actual sideways moving tile transforms");
                        Capture("11-pause.png", 540, 960); BeginClick(Button("ResumeButton")); Next(22); break;
                    case 22:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        if (game.Phase != ClonePhase.Playing) return;
                        Check(game.GameTime >= pauseClock && game.GameTime - pauseClock < .3, "resume continues from the preserved song position");
                        pressedFrame = Time.frameCount; Next(23); break;
                    case 23:
                        if (Time.frameCount <= pressedFrame + 1) return;
                        dragOrigin = game.Actor.position.x; Hold(dragOrigin); bot = true;
                        lastClock = game.GameTime; lastDsp = AudioSettings.dspTime; Next(24, songLength + 60); break;
                    case 24:
                        if (game.Phase == ClonePhase.Playing)
                        {
                            Bot();
                            double dsp = AudioSettings.dspTime;
                            if (!game.WinSequence && Math.Abs((game.GameTime - lastClock) - (dsp - lastDsp) * game.PlaybackRate) > .15)
                                throw new InvalidOperationException("The song clock diverged from DSP playback after pause/resume.");
                            lastClock = game.GameTime; lastDsp = dsp;
                            if (!capturedHold && game.Support != null && (game.Support.element.id == 2 || game.Support.element.id == 3))
                            { Capture("12-sustain.png", 540, 960); capturedHold = true; }
                            if (!capturedGlide && game.Support != null && game.Support.element.id == 4)
                            { Capture("13-glide.png", 540, 960); capturedGlide = true; }
                            return;
                        }
                        Release(); bot = false;
                        Check(game.Phase == ClonePhase.Results && game.WinSequence, "the new courier completes the full recording through real tile and final-platform contacts");
                        Check(game.GameTime - game.Map.delayTime >= songLength - .25 && AudioSettings.dspTime - startDsp >= (songLength - 1) / game.PlaybackRate,
                            "the complete recording was exercised without seeking, teleporting or shortening the chart");
                        Check(game.Score > 0 && game.RunStars == 3, "the result retains the earned score and three-star completion");
                        var sustainTiles = game.Tiles.Where(tile => tile.element.id == 2 || tile.element.id == 3 || tile.element.id == 8 || tile.element.id == 9).ToArray();
                        if (sustainTiles.Length > 0) Check(sustainTiles.All(tile => tile.consumed), "sustains, checkpoints and their exits were crossed through actual contacts");
                        else Notes.Add("N/A: the reviewed chart contains no sustain tiles.");
                        var glideTiles = game.Tiles.Where(tile => tile.element.id == 4 || tile.element.id == 5 || tile.element.id == 6).ToArray();
                        if (glideTiles.Length > 0) Check(glideTiles.All(tile => tile.consumed), "glide entries, scored midpoints and exits were crossed through actual contacts");
                        else Notes.Add("N/A: the reviewed chart contains no Follow/glide tiles.");
                        var sideways = game.Tiles.Where(tile => tile.element.updateType == 3 && (tile.element.id == 0 || tile.element.id == 1)).ToArray();
                        if (sideways.Length > 0)
                        {
                            Check(sideways.All(tile => tile.consumed), "every sideways moving target was reached through actual tile contacts");
                            Check(sideways.All(tile => MovingRanges.TryGetValue(tile.element.sourceIndex, out var range) && range.y - range.x > .15f),
                                "native sideways target transforms moved visibly during actual playback");
                            if (game.Map.elements.Any(e => e.updateType == 3 && e.msLeft == 0)
                                && game.Map.elements.Any(e => e.updateType == 3 && e.msLeft != 0))
                                Check(sideways.Any(tile => tile.element.msLeft == 0) && sideways.Any(tile => tile.element.msLeft != 0),
                                    "the reviewed chart exercised both authored sideways movement directions");
                            else Notes.Add("N/A: this source route authors one sideways movement direction only; the opposite flag is covered by native motion fixtures.");
                        }
                        Next(25); break;
                    case 25:
                        if (frames < 30) return;
                        Capture("14-results.png", 540, 960); InvokeButton("RetryButton"); Next(26); break;
                    case 26:
                        if (frames < 4) return;
                        Check(game.Phase == ClonePhase.Ready || game.Phase == ClonePhase.Playing, "the new result retry control prepares or starts another attempt");
                        if (game.Phase == ClonePhase.Ready) InvokeButton("ReadyPlayButton");
                        Next(28); break;
                    case 28:
                        if (frames < 3) return;
                        Check(game.Phase == ClonePhase.Playing, "retry starts through the ready-screen control");
                        InvokeButton("PauseButton"); Next(29); break;
                    case 29:
                        if (frames < 3) return;
                        Check(game.Phase == ClonePhase.Paused, "the retried attempt can be paused");
                        InvokeButton("HomeButton"); Next(27); break;
                    case 27:
                        if (frames < 4) return;
                        Check(game.Phase == ClonePhase.Menu && Button("PlayButton").gameObject.activeInHierarchy, "returning home restores the new menu");
                        Capture("15-return-home.png", 540, 960);
                        Check(Errors.Count == 0, "the reviewed flow produced no runtime errors");
                        Finish(true, "New home, all three couriers, song navigation/library, settings, atlas, workshop, 540x960 and 390x844, actual pointer Start/Pause/Resume, full " +
                            songLength.ToString("F3") + "s custom song, DSP continuity, actual tile contacts, result and retry passed. No player build was requested or created."); break;
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void TickMenuPreview()
        {
            if (step == 0)
            {
                couriers = LoadCatalog(); menuSize = menuCourier = 0;
                InvokeButton("CourierButton_0"); Next(1); return;
            }
            var size = MenuSizes[menuSize];
            if (frames < 20 || Screen.width != size.x || Screen.height != size.y) return;
            string context = couriers.entries[menuCourier].id + "-" + size.x + "x" + size.y;
            string result = CourierMenuPreviewChecks.CheckCurrent(game.Presenter.UICanvas, context);
            if (frames < 45) return;
            Capture("menu-" + context + ".png", size.x, size.y);
            Notes.Add(result);
            Notes.Add(CourierLightingChecks.CheckMenu(game.Presenter.UICanvas));
            if (++menuCourier < couriers.entries.Length)
            {
                InvokeButton("CourierButton_" + menuCourier); Next(1); return;
            }
            menuCourier = 0;
            if (++menuSize < MenuSizes.Length)
            {
                SelectGameViewSize(MenuSizes[menuSize].x, MenuSizes[menuSize].y);
                InvokeButton("CourierButton_0"); Next(1); return;
            }
            Check(Errors.Count == 0, "menu preview switching and resizing produced no runtime errors");
            Finish(true, "All three live menu courier previews fit their camera after layout and during animation at 540x960, 390x844 and 1080x1920. No player build was created.");
        }

        private static void CheckActor(int index)
        {
            var definition = couriers.entries[index];
            var motion = game.Actor.GetComponentsInChildren<CourierMotion>(false).SingleOrDefault();
            Check(motion != null && motion.transform.IsChildOf(game.Actor), "the active actor uses courier motion " + definition.id);
            Check(game.Actor.Cast<Transform>().Any(child => child.gameObject.activeInHierarchy && child.name == definition.id), "selected courier " + definition.id + " is installed on the real gameplay actor");
            Check(PlayerPrefs.GetInt(CourierKey, -1) == index, "courier selection " + index + " is persisted independently");
            var expectedMeshes = new HashSet<Mesh>(CourierMeshes(definition.prefab, true));
            var activeMeshes = CourierMeshes(motion.gameObject, false).ToArray();
            Check(activeMeshes.Length > 0 && activeMeshes.All(expectedMeshes.Contains), "the installed courier uses the selected definition's actual 3D meshes");
            Notes.Add(CourierLightingChecks.CheckActor(game.Actor));
        }

        private static IEnumerable<Mesh> CourierMeshes(GameObject root, bool includeInactive) =>
            root.GetComponentsInChildren<MeshFilter>(includeInactive).Select(filter => filter.sharedMesh)
                .Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive).Select(renderer => renderer.sharedMesh))
                .Where(mesh => mesh != null);

        private static LocalPose[] SnapshotCourier() => game.Actor.GetComponentsInChildren<CourierMotion>(false)
            .SelectMany(motion => motion.GetComponentsInChildren<Transform>(true)).Distinct()
            .Select(node => new LocalPose { node = node, position = node.localPosition, rotation = node.localRotation, scale = node.localScale }).ToArray();

        private static void Bot()
        {
            if (!bot || game.Phase != ClonePhase.Playing) return;
            float z = game.Actor.position.z;
            foreach (var tile in game.Tiles.Where(tile => tile.element.updateType == 3))
            {
                int index = tile.element.sourceIndex;
                var range = MovingRanges.TryGetValue(index, out var observed) ? observed : new Vector2(tile.CenterX, tile.CenterX);
                MovingRanges[index] = new Vector2(Mathf.Min(range.x, tile.CenterX), Mathf.Max(range.y, tile.CenterX));
                if (!capturedSideways && !tile.consumed && tile.initialZ - z > 1 && tile.initialZ - z < 12)
                { Capture("16-sideways.png", 540, 960); capturedSideways = true; }
            }
            float target;
            if (game.Support != null && game.Support.element.id != 4) target = game.Support.CenterX;
            else
            {
                var next = game.Tiles.Where(tile => !tile.consumed && tile.element.id != 7 && tile.element.id != 8 && tile.initialZ > z - .1f)
                    .OrderBy(tile => tile.initialZ).FirstOrDefault();
                target = next == null ? 0 : next.CenterX;
            }
            Hold(target);
        }

        private static void Hold(float worldX)
        {
            var screen = new Vector2(Screen.width * .5f + (worldX - dragOrigin) * Screen.width / 6.6f, Screen.height * .28f);
            InputSystem.QueueStateEvent(pointer, new MouseState { position = screen }.WithButton(MouseButton.Left));
            UpdatePointer();
        }

        private static void Release()
        {
            if (pointer == null || !pointer.added) return;
            InputSystem.QueueStateEvent(pointer, new MouseState { position = pointer.position.ReadValue() }); UpdatePointer();
        }

        private static void UpdatePointer() => typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null)
            .Invoke(null, new object[] { InputUpdateType.Dynamic });

        private static Button Button(string name) => game.Presenter.UICanvas.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button.name == name && button.gameObject.activeInHierarchy);

        private static void InvokeButton(string name)
        {
            var button = Button(name);
            Check(button != null && button.interactable, "the active UI exposes " + name);
            button.onClick.Invoke();
        }

        private static void BeginClick(Button button)
        {
            Check(button != null && button.interactable, "an active pointer target is available");
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "UI raycasting reaches " + button.name);
            InputSystem.QueueStateEvent(pointer, new MouseState { position = position }.WithButton(MouseButton.Left));
            pressedFrame = Time.frameCount; UpdatePointer();
        }

        private static void Next(int next, double seconds = 20) { step = next; frames = 0; deadline = EditorApplication.timeSinceStartup + seconds; }
        private static CourierCatalog LoadCatalog() => AssetDatabase.FindAssets("t:CourierCatalog").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CourierCatalog>).FirstOrDefault(catalog => catalog != null) ?? throw new InvalidOperationException("Courier catalog is missing.");
        private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly =>
        { try { return assembly.GetTypes(); } catch (ReflectionTypeLoadException error) { return error.Types.Where(type => type != null); } }).FirstOrDefault(type => type.Name == name);
        private static object ReadMember(object target, string name) => target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) ??
            target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("RADIO_REVIEW FAIL: " + description);
            Notes.Add("PASS " + description); Debug.Log("RADIO_REVIEW PASS: " + description);
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            Errors.Add(message + "\n" + trace);
        }

        private static void SelectGameViewSize(int width, int height)
        {
            var assembly = typeof(EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var groupKind = sizesType.GetProperty("currentGroupType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(sizes);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupKind });
            var viewType = assembly.GetType("UnityEditor.GameView", true);
            bool wasOpen = Resources.FindObjectsOfTypeAll(viewType).Length != 0;
            var view = EditorWindow.GetWindow(viewType);
            var selected = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (!SessionState.GetBool(Key + ".viewCaptured", false))
            {
                SessionState.SetInt(Key + ".viewGroup", Convert.ToInt32(groupKind));
                SessionState.SetInt(Key + ".viewSelected", (int)selected.GetValue(view));
                SessionState.SetEntityId(Key + ".viewWindow", view.GetEntityId());
                SessionState.SetBool(Key + ".viewWasOpen", wasOpen);
                SessionState.SetBool(Key + ".viewCaptured", true);
            }
            var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null), index = -1;
            for (int i = 0; i < count; i++)
            {
                var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)sizeType.GetProperty("width").GetValue(size) == width && (int)sizeType.GetProperty("height").GetValue(size) == height &&
                    sizeType.GetProperty("sizeType").GetValue(size).ToString() == "FixedResolution") { index = i; break; }
            }
            if (index < 0)
            {
                string label = "Radio QA " + Guid.NewGuid().ToString("N");
                SessionState.SetString(Key + ".viewSizes", SessionState.GetString(Key + ".viewSizes", "") + label + "\n");
                var resolutionType = assembly.GetType("UnityEditor.GameViewSizeType", true);
                var size = Activator.CreateInstance(sizeType, new[] { Enum.Parse(resolutionType, "FixedResolution"), (object)width, height, label });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size }); index = count;
            }
            selected.SetValue(view, index); view.Show(); view.Focus(); view.Repaint();
        }

        private static void Capture(string filename, int width, int height)
        {
            Directory.CreateDirectory(Output);
            var viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
            var view = Resources.FindObjectsOfTypeAll(viewType).FirstOrDefault();
            for (var type = viewType; view != null && type != null; type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!typeof(RenderTexture).IsAssignableFrom(field.FieldType)) continue;
                    var frame = field.GetValue(view) as RenderTexture;
                    if (frame == null || frame.width != width || frame.height != height) continue;
                    var prior = RenderTexture.active;
                    var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = frame; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                        if (SystemInfo.graphicsUVStartsAtTop)
                        {
                            var colors = pixels.GetPixels32();
                            for (int y = 0; y < height / 2; y++) for (int x = 0; x < width; x++)
                            { int a = y * width + x, b = (height - y - 1) * width + x; var swap = colors[a]; colors[a] = colors[b]; colors[b] = swap; }
                            pixels.SetPixels32(colors); pixels.Apply();
                        }
                        File.WriteAllBytes(Path.Combine(Output, filename), pixels.EncodeToPNG());
                        Notes.Add("CAPTURE " + filename + " actual GameView " + width + "x" + height); return;
                    }
                    finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(pixels); }
                }
            throw new InvalidOperationException("The actual GameView framebuffer " + width + "x" + height + " is not available for capture.");
        }

        private static void Finish(bool success, string details)
        {
            if (SessionState.GetBool(Key + ".finishing", false)) return;
            SessionState.SetBool(Key + ".finishing", true); SessionState.SetBool(Key + ".success", success);
            SessionState.SetString(Key + ".details", details); SessionState.SetString(Key + ".notes", string.Join("\n", Notes));
            CleanupInput();
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPaused = false; EditorApplication.isPlaying = false; }
            else RestoreEditor();
        }

        private static void CleanupInput()
        {
            bot = false; Application.logMessageReceived -= OnLog;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (loop.subSystemList != null && loop.subSystemList.Any(system => system.type == typeof(RadioEditorValidation)))
            { loop.subSystemList = loop.subSystemList.Where(system => system.type != typeof(RadioEditorValidation)).ToArray(); PlayerLoop.SetPlayerLoop(loop); }
            foreach (var device in DisabledDevices) if (device != null && device.added && !device.enabled) InputSystem.EnableDevice(device);
            DisabledDevices.Clear();
            if (pointer != null && pointer.added) InputSystem.RemoveDevice(pointer); pointer = null;
            if (priorMouse != null && priorMouse.added) priorMouse.MakeCurrent(); priorMouse = null;
            if (priorInput != null) InputSystem.settings = priorInput;
            if (reviewInput != null && reviewInput != priorInput) UnityEngine.Object.DestroyImmediate(reviewInput);
            reviewInput = priorInput = null;
        }

        private static void RestoreEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool success = SessionState.GetBool(Key + ".success", false);
            string details = SessionState.GetString(Key + ".details", "Review interrupted.");
            try
            {
                var snapshot = JsonUtility.FromJson<Snapshot>(SessionState.GetString(Key + ".snapshot", ""));
                if (snapshot?.scenes == null) throw new InvalidOperationException("The prior Editor snapshot is missing.");
                if (snapshot.hadProfile) PlayerPrefs.SetString(OriginalProgression.SaveKey, snapshot.profile); else PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
                if (snapshot.hadGuide) PlayerPrefs.SetInt(GuideKey, snapshot.guide); else PlayerPrefs.DeleteKey(GuideKey);
                if (snapshot.hadCourier) PlayerPrefs.SetInt(CourierKey, snapshot.courier); else PlayerPrefs.DeleteKey(CourierKey);
                PlayerPrefs.Save();
                if (Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(scene => scene.isDirty))
                    throw new InvalidOperationException("An Edit Mode scene is now dirty; review refused to save or discard it.");
                var setup = snapshot.scenes.Select(scene => new SceneSetup { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray();
                var actual = EditorSceneManager.GetSceneManagerSetup();
                if (actual.Length != setup.Length || actual.Where((scene, i) => scene.path != setup[i].path || scene.isLoaded != setup[i].isLoaded || scene.isActive != setup[i].isActive).Any())
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                UnityEngine.Object Resolve(string value) => !string.IsNullOrEmpty(value) && GlobalObjectId.TryParse(value, out var id) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) : null;
                Selection.objects = (snapshot.selection ?? Array.Empty<string>()).Select(Resolve).Where(value => value != null).ToArray();
                var selected = Resolve(snapshot.activeSelection); if (selected != null) Selection.activeObject = selected;
                details += " Restored the original profile, courier, guide, clean scenes, selection and input; returned to Edit Mode.";
            }
            catch (Exception error) { success = false; details += " Editor restoration failed: " + error; }
            finally
            {
                try { RestoreGameView(); } catch (Exception error) { success = false; details += " GameView restoration failed: " + error; }
                SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".finishing", false);
                Notes.Clear(); Notes.AddRange(SessionState.GetString(Key + ".notes", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            }
            WriteResult(SessionState.GetBool(Key + ".menuOnly", false) ? "menu-result.txt"
                : ReviewSongId == ComparisonId ? "comparison-result.txt" : "result.txt", success, details);
        }

        private static void RestoreGameView()
        {
            if (!SessionState.GetBool(Key + ".viewCaptured", false)) return;
            var assembly = typeof(EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
            int priorGroup = SessionState.GetInt(Key + ".viewGroup", 0), priorSelection = SessionState.GetInt(Key + ".viewSelected", 0);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.ToObject(groupType, priorGroup) });
            var viewType = assembly.GetType("UnityEditor.GameView", true);
            var view = EditorUtility.EntityIdToObject(SessionState.GetEntityId(Key + ".viewWindow", EntityId.None)) as EditorWindow;
            if (view != null)
            {
                int currentGroup = Convert.ToInt32(sizesType.GetProperty("currentGroupType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(sizes));
                if (currentGroup == priorGroup) viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, priorSelection);
                else
                {
                    var field = viewType.GetField("m_SelectedSizes", BindingFlags.Instance | BindingFlags.NonPublic);
                    var indices = field?.GetValue(view) as int[];
                    if (indices == null || priorGroup >= indices.Length) throw new InvalidOperationException("Unable to restore the prior GameView group selection.");
                    indices[priorGroup] = priorSelection; field.SetValue(view, indices);
                }
            }
            var created = SessionState.GetString(Key + ".viewSizes", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            for (int i = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1; i >= builtins; i--)
            {
                var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if (created.Contains((string)size.GetType().GetProperty("baseText").GetValue(size))) group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { i });
            }
            if (view != null) { if (SessionState.GetBool(Key + ".viewWasOpen", true)) view.Repaint(); else view.Close(); }
            SessionState.SetBool(Key + ".viewCaptured", false); SessionState.SetString(Key + ".viewSizes", "");
        }

        private static void WriteResult(string filename, bool success, string details)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, filename), (success ? "PASS " : "FAIL ") + details + "\n\n" + string.Join("\n", Notes));
            Debug.Log("RADIO_REVIEW " + (success ? "PASS " : "FAIL ") + details);
        }
    }
}
