using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem.UI;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using RhythmGame.Services;

namespace DancingCats.Clone.Editor
{
    [InitializeOnLoad]
    public static class IntegratedPlayValidation
    {
        private const string Key = "DancingCatsClone.QA.running";
        private static OriginalPlayController game;
        private static Mouse mouse;
        private static int step, substep, lastScore;
        private static double deadline, pauseClock, pauseDsp;
        private static float beginX;
        private static float uiReady = -1;
        private static float panelOpened;
        private static int pressedFrame;
        private static int observedLap;
        private static bool recoveryFollowUp;
        private static bool menusOnly;
        private static bool challengeOnly;
        private static bool customSongOnly;
        private const string CustomSongId = "AlwaysRememberMeowCat";
        private const int CustomSongCatalogId = 20001;
        private static double customDeadline, customStartDsp, customLastDsp, customLastClock, customResumeDsp, customResumeClock;
        private static float customAudioLength;
        private static int customGold, customPausedSamples;
        private static bool customGameplayCaptured, customHoldCaptured, customMovingCaptured;
        private static OriginalTile edgeHold, edgeExit, edgeFinish, edgePrevious;
        private static int edgePointCount, edgeScore;
        private static float edgeMissX;
        private static int diceGold, dicePosition;
        private static bool bot;
        private static InputSettings previousInputSettings;
        private static InputSettings qaInputSettings;
        private static Mouse previousMouse;
        private static readonly List<InputDevice> disabledPhysicalDevices = new List<InputDevice>();
        [Serializable] private sealed class ReviewScene { public string path; public bool loaded, active; }
        [Serializable] private sealed class ReviewSnapshot { public ReviewScene[] scenes; public string[] selection; public string activeSelection; }
        private static readonly List<string> errors = new List<string>();
        private static readonly string[] PanelNames = { "NewRoleShop_UIPanel", "DailyReward_UIPanel", "LoginReward_UIPanel", "ChallengeLevel_UIPanel", "PlayDice_UIPanel", "CatHouseBuilding_UIPanel", "Set_UIPanel" };
        private static string Out
        {
            get
            {
                string session = SessionState.GetString(Key + ".output", "");
                if (!string.IsNullOrWhiteSpace(session)) return session;
                string environment = Environment.GetEnvironmentVariable("DANCING_CATS_QA_OUTPUT");
                return !string.IsNullOrWhiteSpace(environment) ? environment : Path.GetFullPath("../work/clone-validation");
            }
        }

        static IntegratedPlayValidation()
        {
            EditorApplication.playModeStateChanged += Mode;
        }

        public static void Run() => StartValidation(false, false);
        public static void RunInterfaceAndRecovery() => StartValidation(true, false);
        public static void RunChallenge() => StartValidation(false, false, true);
        public static void RunCustomSong() => StartValidation(false, false, false, true);
        public static void RunCustomSongInOpenEditor() => StartValidation(false, false, false, true, true,
            Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MusicalChartReview/playtest")));
        public static void RunDependencyInjectionMenusInOpenEditor() => StartValidation(true, true, false, false, true,
            Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/DependencyInjectionValidation/menus")));
        public static void RunReviewSafetyChecks()
        {
            var preflight = typeof(IntegratedPlayValidation).GetMethod("ValidateEditorPreflight", BindingFlags.NonPublic | BindingFlags.Static);
            Check(preflight != null, "open-Editor review has a preflight before any scene or save changes");
            preflight.Invoke(null, new object[] { false, false, false, false, Array.Empty<string>(), Array.Empty<string>() });
            foreach (var values in new[]
            {
                new object[] { true, false, false, false, Array.Empty<string>(), Array.Empty<string>() },
                new object[] { false, true, false, false, Array.Empty<string>(), Array.Empty<string>() },
                new object[] { false, false, true, false, Array.Empty<string>(), Array.Empty<string>() },
                new object[] { false, false, false, true, Array.Empty<string>(), Array.Empty<string>() },
                new object[] { false, false, false, false, new[] { "Assets/UnsavedUserScene.unity" }, Array.Empty<string>() },
                new object[] { false, false, false, false, Array.Empty<string>(), new[] { "Untitled" } }
            })
            {
                bool rejected = false;
                try { preflight.Invoke(null, values); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
                Check(rejected, "unsafe open-Editor review is refused before opening or saving a scene");
            }
        }
        public static void RunMenus()
        {
            ProgressionValidation.Run();
            OriginalAssetLibraryValidation.Run();
            StartValidation(true, true);
        }

        private static void ValidateEditorPreflight(bool changingPlayMode, bool compiling, bool updating, bool running, string[] dirtyScenes, string[] unsavedScenes)
        {
            if (changingPlayMode) throw new InvalidOperationException("Stop Play Mode before starting an Editor review.");
            if (compiling || updating) throw new InvalidOperationException("Wait for Editor compilation/imports before starting a review.");
            if (running) throw new InvalidOperationException("An integrated review is already running.");
            if (dirtyScenes.Length != 0) throw new InvalidOperationException("Review refused: open scenes have unsaved changes: " + string.Join(", ", dirtyScenes));
            if (unsavedScenes.Length != 0) throw new InvalidOperationException("Review refused: an untitled scene cannot be restored without saving it first: " + string.Join(", ", unsavedScenes));
        }

        private static void CaptureEditorState()
        {
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            ValidateEditorPreflight(EditorApplication.isPlayingOrWillChangePlaymode, EditorApplication.isCompiling, EditorApplication.isUpdating,
                SessionState.GetBool(Key, false), scenes.Where(scene => scene.isDirty).Select(scene => string.IsNullOrEmpty(scene.path) ? scene.name : scene.path).ToArray(),
                scenes.Where(scene => string.IsNullOrEmpty(scene.path)).Select(scene => scene.name).ToArray());
            var snapshot = new ReviewSnapshot
            {
                scenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => new ReviewScene { path = scene.path, loaded = scene.isLoaded, active = scene.isActive }).ToArray(),
                selection = Selection.objects.Select(value => GlobalObjectId.GetGlobalObjectIdSlow(value).ToString()).ToArray(),
                activeSelection = Selection.activeObject != null ? GlobalObjectId.GetGlobalObjectIdSlow(Selection.activeObject).ToString() : ""
            };
            SessionState.SetString(Key + ".editor", JsonUtility.ToJson(snapshot));
        }

        private static void StartValidation(bool followUp, bool onlyMenus, bool onlyChallenge = false, bool onlyCustomSong = false, bool keepEditor = false, string output = null)
        {
            // Interactive entry points always preserve the user's Editor. Only batch children may exit.
            keepEditor |= !Application.isBatchMode;
            if (keepEditor) CaptureEditorState();
            else if (SessionState.GetBool(Key, false)) throw new InvalidOperationException("An integrated review is already running.");
            SessionState.SetBool(Key + ".keepEditor", keepEditor);
            SessionState.SetBool(Key + ".finishing", false);
            SessionState.SetBool(Key + ".gameViewCaptured", false);
            SessionState.SetString(Key + ".gameViewCreatedSize", "");
            SessionState.SetString(Key + ".output", output ?? "");
            SessionState.SetBool(Key + ".followUp", followUp);
            SessionState.SetBool(Key + ".menusOnly", onlyMenus);
            SessionState.SetBool(Key + ".challengeOnly", onlyChallenge);
            SessionState.SetBool(Key + ".customSongOnly", onlyCustomSong);
            SessionState.SetBool(Key + ".hadSave", PlayerPrefs.HasKey(OriginalProgression.SaveKey));
            SessionState.SetBool(Key + ".hadGuide", PlayerPrefs.HasKey("DancingCatsClone.v1.howToPlaySeen"));
            SessionState.SetString(Key + ".save", PlayerPrefs.GetString(OriginalProgression.SaveKey, ""));
            SessionState.SetInt(Key + ".guide", PlayerPrefs.GetInt("DancingCatsClone.v1.howToPlaySeen", 0));
            PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
            PlayerPrefs.SetInt("DancingCatsClone.v1.howToPlaySeen", 0);
            PlayerPrefs.Save();
            SessionState.SetBool(Key, true);
            try
            {
                SetPortraitGameView();
                EditorSceneManager.OpenScene(CloneSceneBuilder.ScenePath);
                EditorApplication.isPlaying = true;
            }
            catch (Exception error)
            {
                Finish(false, "Unable to start integrated review: " + error);
                throw;
            }
        }

        private static void SetPortraitGameView()
        {
            var assembly = typeof(EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var groupKind = sizesType.GetProperty("currentGroupType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(sizes);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupKind });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            var resolutionType = assembly.GetType("UnityEditor.GameViewSizeType", true);
            var viewType = assembly.GetType("UnityEditor.GameView", true);
            bool wasOpen = Resources.FindObjectsOfTypeAll(viewType).Length != 0;
            var view = EditorWindow.GetWindow(viewType);
            var selected = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            SessionState.SetInt(Key + ".gameViewGroup", Convert.ToInt32(groupKind));
            SessionState.SetInt(Key + ".gameViewSelected", (int)selected.GetValue(view));
            SessionState.SetEntityId(Key + ".gameViewWindow", view.GetEntityId());
            SessionState.SetBool(Key + ".gameViewWasOpen", wasOpen);
            SessionState.SetBool(Key + ".gameViewCaptured", true);
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < count; i++)
            {
                var candidate = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)sizeType.GetProperty("width").GetValue(candidate) == 540 &&
                    (int)sizeType.GetProperty("height").GetValue(candidate) == 960 &&
                    sizeType.GetProperty("sizeType").GetValue(candidate).ToString() == "FixedResolution")
                { index = i; break; }
            }
            if (index < 0)
            {
                string label = "Clone portrait QA " + Guid.NewGuid().ToString("N");
                SessionState.SetString(Key + ".gameViewCreatedSize", label);
                var size = Activator.CreateInstance(sizeType, new[] { Enum.Parse(resolutionType, "FixedResolution"), (object)540, 960, label });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = count;
            }
            selected.SetValue(view, index);
            view.Show(); view.Focus(); view.Repaint();
            Debug.Log("INTEGRATED_PLAY portrait GameView selected index=" + index);
        }

        private static void RestoreGameViewState()
        {
            if (!SessionState.GetBool(Key + ".gameViewCaptured", false)) return;
            var assembly = typeof(EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
            int originalGroup = SessionState.GetInt(Key + ".gameViewGroup", 0);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.ToObject(groupType, originalGroup) });
            var viewType = assembly.GetType("UnityEditor.GameView", true);
            var view = EditorUtility.EntityIdToObject(SessionState.GetEntityId(Key + ".gameViewWindow", EntityId.None)) as EditorWindow;
            int originalSelection = SessionState.GetInt(Key + ".gameViewSelected", 0);
            if (view != null)
            {
                int currentGroup = Convert.ToInt32(sizesType.GetProperty("currentGroupType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(sizes));
                if (currentGroup == originalGroup)
                    viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, originalSelection);
                else
                {
                    // The group follows the build target. Restore its saved selection without changing that target.
                    var field = viewType.GetField("m_SelectedSizes", BindingFlags.Instance | BindingFlags.NonPublic);
                    var indices = field?.GetValue(view) as int[];
                    if (indices == null || originalGroup >= indices.Length) throw new InvalidOperationException("Unable to restore the prior GameView group selection.");
                    indices[originalGroup] = originalSelection; field.SetValue(view, indices);
                }
            }
            string created = SessionState.GetString(Key + ".gameViewCreatedSize", "");
            if (created.Length != 0)
            {
                int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
                int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                for (int i = builtins; i < count; i++)
                {
                    var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    if ((string)size.GetType().GetProperty("baseText").GetValue(size) != created) continue;
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { i });
                    break;
                }
            }
            if (view != null)
            {
                if (SessionState.GetBool(Key + ".gameViewWasOpen", true)) view.Repaint();
                else view.Close(); // Only the GameView created by this review; never the Editor process.
            }
            SessionState.SetBool(Key + ".gameViewCaptured", false);
            SessionState.SetString(Key + ".gameViewCreatedSize", "");
        }

        private static void Mode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                bot = false;
                Application.logMessageReceived -= Log;
                EditorApplication.update -= Tick;
                CleanupPlayInput();
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + ".keepEditor", false))
            {
                if (!SessionState.GetBool(Key + ".finishing", false))
                {
                    SessionState.SetBool(Key + ".resultSuccess", false);
                    SessionState.SetString(Key + ".resultDetails", "Play Mode was stopped before the integrated review completed.");
                }
                CompleteOpenEditorReview();
                return;
            }
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                previousInputSettings = InputSystem.settings;
                qaInputSettings = UnityEngine.Object.Instantiate(previousInputSettings);
                qaInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                qaInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = qaInputSettings;
                game = null; mouse = null; previousMouse = null; disabledPhysicalDevices.Clear();
                step = substep = 0; bot = false; uiReady = -1; errors.Clear();
                recoveryFollowUp = SessionState.GetBool(Key + ".followUp", false);
                menusOnly = SessionState.GetBool(Key + ".menusOnly", false);
                challengeOnly = SessionState.GetBool(Key + ".challengeOnly", false);
                customSongOnly = SessionState.GetBool(Key + ".customSongOnly", false);
                customDeadline = 0;
                customGameplayCaptured = customHoldCaptured = customMovingCaptured = false;
                deadline = EditorApplication.timeSinceStartup + 30;
                Application.logMessageReceived += Log;
                EditorApplication.update -= Tick;
                var loop = PlayerLoop.GetCurrentPlayerLoop();
                loop.subSystemList = loop.subSystemList.Where(system => system.type != typeof(IntegratedPlayValidation))
                    .Concat(new[] { new PlayerLoopSystem { type = typeof(IntegratedPlayValidation), updateDelegate = Tick } }).ToArray();
                PlayerLoop.SetPlayerLoop(loop);
            }
        }

        private static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Warning && message.Contains("Can not play a disabled audio source"))
            {
                errors.Add(message + "\n" + trace);
                return;
            }
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (trace.Contains("UnityEditor.Search.SearchDatabase"))
            {
                Debug.LogWarning("INTEGRATED_PLAY EDITOR_ONLY: Unity Search startup index failure, outside the player assemblies: " + message);
                return;
            }
            errors.Add(message + "\n" + trace);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || SessionState.GetBool(Key + ".finishing", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused) return;
            try
            {
                if (errors.Count > 0) throw new Exception("runtime errors: " + string.Join(" | ", errors.Take(4)));
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout at step " + step + ", substep " + substep + ", phase " + game?.Phase);
                if (game == null) game = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
                if (game == null || game.Presenter == null || game.Presenter.UICanvas == null) return;
                if (uiReady < 0) uiReady = Time.time;
                if (Time.time - uiReady < 1.7f) return; // Original GuideButton fades on scaled Animation time.
                if (mouse == null)
                {
                    previousMouse = Mouse.current;
                    mouse = InputSystem.AddDevice<Mouse>("Clone QA pointer"); mouse.MakeCurrent();
                    foreach (var physical in InputSystem.devices.Where(d => d.native && d.enabled && (d is Mouse || d is Keyboard)).ToArray())
                    { disabledPhysicalDevices.Add(physical); InputSystem.DisableDevice(physical); }
                }
                if (customSongOnly && step >= 60) { TickCustomSong(); return; }
                switch (step)
                {
                    case 0:
                        Check(RecoveredCatalog.LoadOriginal().Length == 58 && game.Songs.Length >= 58 && game.Assets.RoleNames.Length == 63,
                            "all 58 original songs and 63 cats remain bound alongside optional custom songs");
                        Check(game.Phase == ClonePhase.Menu, "fresh startup opens original menu");
                        var clickAudio = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .FirstOrDefault(source => source.name == "ButtonClickAudio");
                        Check(clickAudio != null && clickAudio.isActiveAndEnabled && clickAudio.clip != null,
                            "original UI click sound remains active with its original clip");
                        Capture("01-original-how-to-play.png");
                        Next(11, 10); break;
                    case 11:
                        if (substep++ < 3) return;
                        var guide = FindButton("GuideInternalStart_UIPanel/Image/GuideButton");
                        if (guide == null) guide = FindButton("GuideInternalStart_UIPanel/GuideCloseButton");
                        Check(guide != null, "original tutorial button exists");
                        BeginClick(guide); Next(1, 10); break;
                    case 1:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        if (substep++ == 0)
                        {
                            var module = EventSystem.current.currentInputModule as InputSystemUIInputModule;
                            Debug.Log("INTEGRATED_PLAY INPUT focus=" + Application.isFocused + " mouse=" + mouse.enabled + " held=" + mouse.leftButton.isPressed +
                                " action=" + module?.leftClick.action.enabled + " click=" + module?.leftClick.action.ReadValue<float>() +
                                " point=" + module?.point.action.ReadValue<Vector2>() + " window=" + EditorWindow.focusedWindow?.GetType().Name);
                        }
                        Release();
                        if (game.Presenter.IsBlockingInput) return;
                        Next(12, 10); break;
                    case 12:
                        if (substep++ < 3) return;
                        Capture("02-original-main-menu.png");
                        if (customSongOnly)
                        {
                            var custom = game.Songs.Single(s => s.id == CustomSongId);
                            Check(custom.catalogId == CustomSongCatalogId && game.Progression.IsSongUnlocked(CustomSongId),
                                "custom song 20001 is available unlocked on a fresh save");
                            BeginClick(FindButton("L/MusicButton")); panelOpened = Time.unscaledTime; Next(60, 15); break;
                        }
                        BeginClick(FindButton("L/MusicButton")); Next(2, 10); break;
                    case 2:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        var list = GameObject.Find("MusicList_UIPanel");
                        if (list == null || !list.activeInHierarchy) return;
                        if (substep++ < 3) return;
                        Check(list.GetComponentsInChildren<Button>(true).Length > 58, "original song rows populated");
                        Capture("03-original-song-list.png");
                        game.Presenter.ClosePopup();
                        Next(18, 10); break;
                    case 18:
                        if (substep++ < 3) return;
                        BeginStartPress(); Next(17, 10); break;
                    case 17:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Check(game.Phase == ClonePhase.Playing && mouse.leftButton.isPressed, "original StartBtn starts on pointer DOWN while still held");
                        bot = true; beginX = game.Actor.position.x; Hold(beginX); Next(3, 80); break;
                    case 3:
                        Bot();
                        if (game.GameTime < 4) return;
                        Check(game.Score > 0 && game.Phase == ClonePhase.Playing, "native arcs land on actual original tile colliders");
                        lastScore = game.Score; pauseClock = game.GameTime; pauseDsp = AudioSettings.dspTime;
                        game.Pause(); bot = false; Release();
                        Next(4, 10); break;
                    case 4:
                        if (AudioSettings.dspTime - pauseDsp < .6) return;
                        Check(game.Phase == ClonePhase.Paused && game.Score == lastScore && Math.Abs(game.GameTime - pauseClock) < .0001, "pause freezes chart and score");
                        Capture("04-original-pause.png");
                        if (menusOnly) { BeginClick(FindButton("Stop_UIPanel/StopWindow/Quit")); Next(31, 10); break; }
                        game.Resume(); beginX = game.Actor.position.x; Hold(beginX); bot = true; Next(5, 80); break;
                    case 5:
                        Bot();
                        if (game.GameTime < 8) return;
                        Check(Math.Abs(game.Music.time - (game.GameTime - game.Map.delayTime)) < .15, "scheduled music stays aligned after pause/resume");
                        Capture("05-original-gameplay.png");
                        if (challengeOnly)
                        {
                            Release(); game.Home(); game.Progression.ChallengeSong(DateTime.Today);
                            game.Progression.State.challengeSong = "FlyingPop"; game.Progression.Save();
                            game.Presenter.OpenPanel("ChallengeLevel_UIPanel"); panelOpened = Time.unscaledTime;
                            Next(43, 15); break;
                        }
                        if (menusOnly) { Release(); game.Home(); Next(10, 45); break; }
                        if (!recoveryFollowUp) { Next(6, 80); break; }
                        StartSong("AllAlonePop");
                        edgeHold = game.Tiles.First(t => t.element.id == 2 || t.element.id == 3);
                        edgeExit = game.Tiles.First(t => t.element.id == 8 && t.element.sourceIndex == edgeHold.element.sourceIndex);
                        Next(20, 25); break;
                    case 20:
                        Bot();
                        if (!edgeExit.consumed) return;
                        Hold(1.5f); bot = false; Next(21, 10); break;
                    case 21:
                        Hold(1.5f);
                        if (game.Phase == ClonePhase.Playing) return;
                        Check(game.Phase == ClonePhase.Dead, "deliberate miss occurs after the consumed Hold exit");
                        edgePointCount = game.Tiles.Count(t => t.checkpoint && t.element.id == 9 && t.element.sourceIndex == edgeHold.element.sourceIndex && t.consumed);
                        edgeScore = game.Score;
                        game.Revive();
                        Check(game.Support == edgeHold && !edgeExit.consumed, "Hold revive rearms its exit without rearming scored points");
                        beginX = game.Actor.position.x; Hold(beginX); bot = true; Next(22, 10); break;
                    case 22:
                        Bot();
                        Check(game.Phase == ClonePhase.Playing, "revived Hold continues through actual contact");
                        if (!edgeExit.consumed || game.Actor.position.z <= edgeExit.initialZ + .1f) return;
                        Check(game.Support == null && game.Actor.position.y > .25f && game.Score == edgeScore,
                            "replayed Hold exit launches a real arc without duplicate point scores");
                        Check(game.Tiles.Count(t => t.checkpoint && t.element.id == 9 && t.element.sourceIndex == edgeHold.element.sourceIndex && t.consumed) == edgePointCount,
                            "Hold point consumption survives revive");
                        StartSong("FlyingPop");
                        edgeFinish = game.Tiles.First(t => t.element.kind == "FinishJump");
                        edgePrevious = game.Tiles.Where(t => t.element.id < 7 && t.initialZ < edgeFinish.initialZ).OrderBy(t => t.initialZ).Last();
                        edgeMissX = edgeFinish.CenterX >= 0 ? -1.5f : 1.5f;
                        Next(23, 50); break;
                    case 23:
                        Bot();
                        if (!edgePrevious.consumed) return;
                        bot = false; Hold(edgeMissX); Next(24, 15); break;
                    case 24:
                        Hold(edgeMissX);
                        if (game.Phase == ClonePhase.Playing) return;
                        Check(game.Phase == ClonePhase.Dead && !edgeFinish.consumed, "deliberate miss leaves the final jump unconsumed");
                        game.Revive(); beginX = game.Actor.position.x; Hold(beginX); Next(25, 15); break;
                    case 25:
                        if (substep++ < 3) return;
                        Hold(edgeMissX);
                        Check(game.Phase == ClonePhase.Playing, "invincible miss cannot bypass the real final platform");
                        if (game.Actor.position.z <= edgeFinish.initialZ + 2) return;
                        Check(!edgeFinish.consumed && game.Actor.position.z < edgeFinish.initialZ + 33,
                            "protected recovery keeps chart position and does not claim an early completion");
                        bot = true; Next(26, 20); break;
                    case 26:
                        if (game.Phase == ClonePhase.Playing) { Bot(); return; }
                        Check(game.Phase == ClonePhase.Results && game.Actor.position.z >= edgeFinish.initialZ + 33,
                            "protected final miss still needs the actual final trigger and win sequence");
                        if (!ResultAnimationReady()) return;
                        Capture("10-protected-finish-settlement.png"); Release(); game.Home(); Next(10, 45); break;
                    case 6:
                        if (game.Phase == ClonePhase.Playing) { Bot(); return; }
                        if (game.Phase != ClonePhase.Results) throw new Exception("starter map ended in " + game.Phase);
                        if (!ResultAnimationReady()) return;
                        Check(game.Phase == ClonePhase.Results, "original starter map reaches settlement through collider landings");
                        float finishZ = game.Map.elements.First(e => e.kind == "FinishJump").pos.z;
                        Check(game.Actor.position.z >= finishZ + 33, "settlement follows the real final collider after the collectible platform");
                        Check(game.GetSongStars(game.Selected.catalogId) == 3, "completed original map stores three stars");
                        Capture("06-original-settlement.png");
                        game.Retry(); game.BeginRun(); beginX = game.Actor.position.x; Hold(beginX); bot = false;
                        Next(16, 15); break;
                    case 16:
                        if (substep++ < 3) return;
                        Hold(1.5f); Next(7, 15); break;
                    case 7:
                        Hold(1.5f);
                        if (game.Phase == ClonePhase.Playing) return;
                        Check(game.Phase == ClonePhase.Dead && game.Actor.position.y < -2, "missing an actual tile falls below original death threshold");
                        if (substep++ < 3) return;
                        Capture("07-original-revive.png");
                        game.Revive(); Check(game.Phase == ClonePhase.Playing, "offline revive resumes native chart");
                        beginX = game.Actor.position.x; Hold(beginX); bot = true; Next(8, 15); break;
                    case 8:
                        Bot();
                        if (game.GameTime < 4) return;
                        Check(game.Phase == ClonePhase.Playing, "revived playback and collision recovery continue");
                        game.Home();
                        var varied = game.Songs.First(s => s.id == "AllAlonePop");
                        game.Progression.State.unlockedSongs.Add(varied.id);
                        game.SelectSong(varied.catalogId);
                        game.BeginRun(); beginX = game.Actor.position.x; Hold(beginX); bot = true; Next(9, 100); break;
                    case 9:
                        if (game.Phase == ClonePhase.Playing) { Bot(); return; }
                        if (game.Phase != ClonePhase.Results) throw new Exception("mixed map ended in " + game.Phase);
                        if (!ResultAnimationReady()) return;
                        Check(game.Phase == ClonePhase.Results, "original map with Hold and Follow completes with real drag input");
                        Capture("08-hold-follow-settlement.png");
                        game.Home();
                        var endless = game.Songs.First(s => s.id == "FlyingPop");
                        game.Progression.State.unlockedSongs.Add(endless.id);
                        game.Endless = true; game.SelectSong(endless.catalogId);
                        game.BeginRun(); beginX = game.Actor.position.x; Hold(0); bot = true; observedLap = 0;
                        Next(15, 130); break;
                    case 15:
                        if (game.Phase == ClonePhase.Playing)
                        {
                            Bot();
                            int lap = Mathf.Min(2, game.RunStars / 3);
                            if (lap > observedLap && game.Progress < .8f)
                            {
                                observedLap = lap;
                                float expected = 1 + game.Selected.catalog.speedAdd[Mathf.Min(lap, game.Selected.catalog.speedAdd.Length - 1)];
                                Check(Mathf.Abs(game.PlaybackRate - expected) < .0001f, "endless lap " + (lap + 1) + " keeps native speed and pitch");
                                Check(game.GetSongStars(game.Selected.catalogId) >= lap * 3, "endless tier is persisted at the lap boundary");
                                Capture("endless-lap-" + (lap + 1) + ".png");
                            }
                            return;
                        }
                        if (game.Phase != ClonePhase.Results) throw new Exception("endless map ended in " + game.Phase);
                        if (!ResultAnimationReady()) return;
                        Check(observedLap == 2 && game.RunStars == 9, "all three endless laps reach the real final platform and nine-star result");
                        Capture("09-endless-settlement.png");
                        game.Home(); game.Endless = false;
                        Next(10, 45); break;
                    case 10:
                        if (substep >= PanelNames.Length * 2)
                        {
                            if (recoveryFollowUp)
                            {
                                Release();
                                game.Presenter.OpenPanel("PlayDice_UIPanel"); panelOpened = Time.unscaledTime;
                                diceGold = game.Progression.Gold; dicePosition = game.Progression.State.dicePosition;
                                Next(27, 15); break;
                            }
                            Check(errors.Count == 0, "all gameplay/UI steps free of runtime errors");
                            Finish(true, "real pointer routing, starter and mixed original charts, miss, pause, revive, settlement and offline panels"); break;
                        }
                        string panel = PanelNames[substep / 2];
                        if (substep % 2 == 0) { Release(); game.Presenter.OpenPanel(panel); panelOpened = Time.unscaledTime; substep++; return; }
                        if (Time.unscaledTime - panelOpened < 1.7f) return;
                        Check(GameObject.Find(panel) != null, "restored original panel opens: " + panel);
                        Capture("panel-" + panel + ".png"); game.Presenter.ClosePopup(); substep++; break;
                    case 27:
                        if (Time.unscaledTime - panelOpened < 1.7f) return;
                        BeginClick(FindButton("PlayDice_UIPanel/Button_Bg/FreeButton")); Next(28, 15); break;
                    case 28:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        if (game.Progression.State.dicePosition == dicePosition) return;
                        panelOpened = Time.unscaledTime; Next(29, 15); break;
                    case 29:
                        if (Time.unscaledTime - panelOpened < 6) return;
                        Check(game.Progression.Gold > diceGold && game.Progression.State.dicePosition >= 3 && game.Progression.State.dicePosition <= 5,
                            "real dice button animates the native first roll and awards traversed cells offline");
                        var coins = game.Presenter.UICanvas.transform.Find("PlayDice_UIPanel/Coins");
                        Check(coins != null && coins.gameObject.activeInHierarchy && !FindButton("PlayDice_UIPanel/Button_Bg/AdButton").interactable,
                            "dice reward is modal and blocks another roll");
                        Capture("11-dice-reward.png");
                        BeginClick(FindButton("PlayDice_UIPanel/Coins/Button")); Next(30, 10); break;
                    case 30:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        var modal = game.Presenter.UICanvas.transform.Find("PlayDice_UIPanel/Coins");
                        if (modal.gameObject.activeInHierarchy) return;
                        Check(errors.Count == 0, "pointer-down start, recovery edges, original panels and dice actions have no runtime errors");
                        game.Presenter.ClosePopup(); game.Presenter.OpenPanel("DailyReward_UIPanel"); panelOpened = Time.unscaledTime;
                        Next(39, 15); break;
                    case 31:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        var prompt = game.Presenter.UICanvas.transform.Find("Stop_UIPanel/PromptWindow");
                        if (!prompt.gameObject.activeInHierarchy) return;
                        if (substep++ == 0) panelOpened = Time.unscaledTime;
                        if (Time.unscaledTime - panelOpened < 1) return;
                        var underlyingResume = FindButton("Stop_UIPanel/StopWindow/Resume");
                        Check(!underlyingResume.gameObject.activeInHierarchy || !underlyingResume.interactable, "quit confirmation blocks the underlying resume action");
                        Capture("12-quit-confirmation.png");
                        BeginClick(FindButton("Stop_UIPanel/PromptWindow/BG/No")); Next(32, 10); break;
                    case 32:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        if (game.Presenter.UICanvas.transform.Find("Stop_UIPanel/PromptWindow").gameObject.activeInHierarchy) return;
                        BeginClick(FindButton("Stop_UIPanel/StopWindow/Resume")); Next(33, 10); break;
                    case 33:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        if (game.Phase == ClonePhase.Paused) return;
                        Check(game.Phase == ClonePhase.Playing, "cancel quit restores the actual Resume button");
                        beginX = game.Actor.position.x; Hold(beginX); bot = true; Next(5, 80); break;
                    case 39:
                        if (Time.unscaledTime - panelOpened < 1.7f) return;
                        var cards = game.Presenter.UICanvas.transform.Find("DailyReward_UIPanel/Animation/BG/Parent");
                        BeginClick(cards.GetChild(0).GetComponent<Button>()); Next(40, 15); break;
                    case 40:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        if (!game.Progression.DailyCardClaimed(0)) return;
                        if (substep++ == 0) panelOpened = Time.unscaledTime;
                        if (Time.unscaledTime - panelOpened < .8f) return;
                        Check(game.Progression.IsRoleUnlocked("9") && game.Progression.SelectedRole == "1",
                            "actual first daily card unlocks the native gift cat while retaining the equipped cat");
                        var picked = game.Presenter.UICanvas.transform.Find("DailyReward_UIPanel/Animation/BG/Parent").GetChild(0).Find("Animation");
                        Check(picked.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("DailyReward_Pick") && picked.Find("Win").gameObject.activeInHierarchy,
                            "daily card reveals its credited reward through the original Pick animation");
                        Capture("14-daily-card-reward.png");
                        game.Presenter.ClosePopup(); game.Presenter.OpenPanel("NewRoleShop_UIPanel");
                        panelOpened = Time.unscaledTime; Next(48, 15); break;
                    case 41:
                        if (Time.unscaledTime - panelOpened < 1.7f) return;
                        BeginClick(FindButton("LoginReward_UIPanel/RewardButton")); Next(42, 15); break;
                    case 42:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        if (game.Progression.State.loginClaims == 0) return;
                        Check(game.Progression.Gold == diceGold + 100 && !game.Progression.CanClaimLogin(DateTime.Today), "actual login claim pays native day-one reward exactly once");
                        Check(errors.Count == 0, "all tested menu actions remain free of runtime errors");
                        Finish(true, menusOnly ? "native pointer-down start, modal pause/cancel/resume, seven original panels, actual dice reward, daily card and login claim" : "recovery edges, seven original panels, dice reward, daily card and login claim"); break;
                    case 43:
                        if (Time.unscaledTime - panelOpened < 1.7f) return;
                        BeginClick(FindButton("ChallengeLevel_UIPanel/BG/Start/Start")); Next(44, 15); break;
                    case 44:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        if (game.Presenter.IsBlockingInput) return;
                        Check(game.ChallengeRun && game.Endless && game.Selected.id == "FlyingPop", "actual challenge button selects the native endless challenge route");
                        if (substep++ < 3) return;
                        BeginStartPress(); Next(45, 15); break;
                    case 45:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Check(game.Phase == ClonePhase.Playing && game.ChallengeRun, "challenge starts through the original pointer-down control");
                        beginX = game.Actor.position.x; Hold(beginX); bot = true; Next(46, 130); break;
                    case 46:
                        if (game.Phase == ClonePhase.Playing) { Bot(); return; }
                        Release();
                        Check(game.Phase == ClonePhase.Results && game.RunStars == 9 && game.Progression.State.challengeStars == 9,
                            "native challenge records all nine marks through three actual laps");
                        if (!ResultAnimationReady()) return;
                        Check(Enumerable.Range(0, 3).All(i => game.Progression.CanClaimChallenge(DateTime.Today, i)), "all native 3/6/9 challenge reward checkpoints are available");
                        Capture("13-challenge-nine-marks.png"); diceGold = game.Progression.Gold;
                        BeginClick(FindButton("ChallengeLevel_UIPanel/BG/Win/CloseButton (1)")); Next(47, 15); break;
                    case 47:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        if (game.Progression.State.claimedChallengeTiers.Count < 3) return;
                        Check(game.Progression.Gold == diceGold + 3000 && !Enumerable.Range(0, 3).Any(i => game.Progression.CanClaimChallenge(DateTime.Today, i)),
                            "actual challenge claim pays 500+1000+1500 once and preserves ordinary settlement below");
                        Check(errors.Count == 0, "full native challenge flow is free of runtime errors");
                        Finish(true, "actual challenge selection, native pointer-down start, three full endless laps, nine-mark overlay and all 3/6/9 rewards claimed once"); break;
                    case 48:
                        if (Time.unscaledTime - panelOpened < 1.7f) return;
                        var wearButton = FindButton("RoleItem_9/SelectButton");
                        var wearRect = (RectTransform)wearButton.transform;
                        var shopScroll = wearButton.GetComponentInParent<ScrollRect>();
                        var viewport = shopScroll.viewport != null ? shopScroll.viewport : (RectTransform)shopScroll.transform;
                        if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, RectTransformUtility.WorldToScreenPoint(null, wearRect.TransformPoint(wearRect.rect.center))))
                        {
                            // Navigation setup is equivalent to scroll-into-view; the equip itself
                            // still goes through the real pointer, UI raycast and button event.
                            var local = viewport.InverseTransformPoint(wearRect.TransformPoint(wearRect.rect.center));
                            shopScroll.StopMovement(); shopScroll.content.anchoredPosition += Vector2.up * (viewport.rect.center.y - local.y);
                            Canvas.ForceUpdateCanvases(); panelOpened = Time.unscaledTime; return;
                        }
                        BeginClick(wearButton); Next(49, 15); break;
                    case 49:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release();
                        if (game.Progression.SelectedRole != "9") return;
                        Check(game.Actor.Cast<Transform>().Any(t => t.gameObject.activeInHierarchy && t.name == game.Progression.Role("9").prefabName),
                            "actual shop equip applies the unlocked native rig to the live actor");
                        game.Presenter.ClosePopup(); game.Presenter.OpenPanel("LoginReward_UIPanel");
                        diceGold = game.Progression.Gold; panelOpened = Time.unscaledTime; Next(41, 15); break;
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void TickCustomSong()
        {
            if (game.Phase == ClonePhase.Dead)
                throw new Exception("custom song died at chart time " + game.GameTime + ", actor " + game.Actor.position);
            switch (step)
            {
                case 60:
                    if (Time.frameCount <= pressedFrame + 3) return;
                    Release();
                    var list = GameObject.Find("MusicList_UIPanel");
                    if (list == null || !list.activeInHierarchy || Time.unscaledTime - panelOpened < 1.7f) return;
                    var play = FindButton("MusicLevelItem_" + CustomSongId + "/Unlock/Play");
                    Check(play != null && play.gameObject.activeInHierarchy && play.interactable, "custom song has an unlocked playable native list row");
                    var row = play.transform.parent.parent;
                    var rows = row.parent.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy && t.name.StartsWith("MusicLevelItem_", StringComparison.Ordinal)).ToArray();
                    Check(Array.IndexOf(rows, row) >= 0 && Array.IndexOf(rows, row) < 3, "custom song appears in the first three native list rows");
                    var scroll = play.GetComponentInParent<ScrollRect>();
                    var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
                    var rect = (RectTransform)play.transform;
                    Check(RectTransformUtility.RectangleContainsScreenPoint(viewport, RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))),
                        "custom song is visible immediately without scrolling");
                    Capture("20-custom-song-list.png");
                    BeginClick(play); panelOpened = Time.unscaledTime; NextCustom(61); break;
                case 61:
                    if (Time.frameCount <= pressedFrame + 3) return;
                    Release();
                    if (game.Presenter.IsBlockingInput || Time.unscaledTime - panelOpened < .5f) return;
                    Check(game.Selected.id == CustomSongId && game.Selected.catalogId == CustomSongCatalogId && game.Phase == ClonePhase.Menu,
                        "native custom row selects the custom song and returns to its menu");
                    var clip = game.Selected.LoadAudio();
                    Check(clip != null && clip.length > 150 && Math.Abs(clip.length - game.Selected.audioLengthSeconds) < .1,
                        "custom song binds the complete decoded recording and matching catalog duration");
                    customAudioLength = clip.length;
                    var finish = game.Map.elements.First(e => e.kind == "FinishJump");
                    double platformAudioTime = finish.audioTime + 5 / game.Map.forwardSpeed;
                    Check(platformAudioTime >= customAudioLength - 1 && platformAudioTime <= customAudioLength + .05,
                        "custom end-platform landing covers the final second of the complete audio timeline");
                    Check(game.Tiles.Any(t => t.element.id == 2 || t.element.id == 3) &&
                        game.Tiles.Any(t => t.element.id == 4 && t.element.updateType == 2 && t.element.mfendPos > 0) &&
                        game.Tiles.Any(t => t.element.id == 5) && game.Tiles.Any(t => t.element.id == 6),
                        "actual custom track contains holds and a native moving Follow route with markers and exit");
                    game.Endless = false;
                    customGold = game.Progression.Gold;
                    Check(game.Progression.State.settlements.Count == 0, "fresh custom validation has no previous settlement");
                    Capture("21-custom-menu.png");
                    BeginStartPress(); NextCustom(62); break;
                case 62:
                    if (Time.frameCount <= pressedFrame + 3) return;
                    Check(game.Phase == ClonePhase.Playing && mouse.leftButton.isPressed, "custom run starts through the actual StartBtn pointer DOWN");
                    customStartDsp = AudioSettings.dspTime;
                    customDeadline = EditorApplication.timeSinceStartup + customAudioLength + 60;
                    beginX = game.Actor.position.x; Hold(beginX); bot = true;
                    NextCustom(63); break;
                case 63:
                    ObserveCustomPlayback();
                    if (game.GameTime < game.Map.delayTime + 4) return;
                    Check(game.Score > 0 && game.Music.isPlaying, "custom audio plays while real input scores actual collider landings");
                    bot = false; Release(); pressedFrame = Time.frameCount; NextCustom(70); break;
                case 70:
                    // The UI module must observe a released frame between gameplay drag
                    // and the new pause-button press. Bot stays disabled until resume.
                    if (Time.frameCount <= pressedFrame + 1) return;
                    BeginClick(FindButton("Internal_UIPanel/StopButton")); NextCustom(64); break;
                case 64:
                    if (Time.frameCount <= pressedFrame + 3) return;
                    Release();
                    if (game.Phase != ClonePhase.Paused) return;
                    bot = false;
                    pauseClock = game.GameTime; pauseDsp = AudioSettings.dspTime; lastScore = game.Score;
                    customPausedSamples = game.Music.timeSamples;
                    NextCustom(65); break;
                case 65:
                    if (AudioSettings.dspTime - pauseDsp < .8) return;
                    Check(game.Phase == ClonePhase.Paused && Math.Abs(game.GameTime - pauseClock) < .0001 && game.Score == lastScore &&
                        !game.Music.isPlaying && game.Music.timeSamples == customPausedSamples,
                        "native pause freezes chart, score and audio position while the system DSP clock advances");
                    Capture("22-custom-pause.png");
                    BeginClick(FindButton("Stop_UIPanel/StopWindow/Resume")); NextCustom(66); break;
                case 66:
                    if (Time.frameCount <= pressedFrame + 3) return;
                    Release();
                    if (game.Phase != ClonePhase.Playing) return;
                    Check(game.GameTime >= pauseClock && game.GameTime - pauseClock < .3, "resume continues from the paused chart position without a time skip");
                    customResumeClock = customLastClock = game.GameTime;
                    customResumeDsp = customLastDsp = AudioSettings.dspTime;
                    // Let runtime ReadPointer observe one released frame after the UI click,
                    // then establish a new drag origin at the actor's current lane.
                    pressedFrame = Time.frameCount; NextCustom(69); break;
                case 69:
                    if (Time.frameCount <= pressedFrame + 1) return;
                    beginX = game.Actor.position.x; Hold(beginX); bot = true;
                    NextCustom(67); break;
                case 67:
                    if (game.Phase == ClonePhase.Playing)
                    {
                        ObserveCustomPlayback();
                        double dsp = AudioSettings.dspTime;
                        if (!game.WinSequence && Math.Abs((game.GameTime - customLastClock) - (dsp - customLastDsp) * game.PlaybackRate) > .1)
                            throw new Exception("custom chart clock is discontinuous with DSP playback");
                        customLastClock = game.GameTime; customLastDsp = dsp;
                        if (substep == 0 && dsp - customResumeDsp > 1)
                        {
                            Check(Math.Abs((game.GameTime - customResumeClock) - (dsp - customResumeDsp) * game.PlaybackRate) < .1 &&
                                Math.Abs(game.Music.time - (game.GameTime - game.Map.delayTime)) < .15,
                                "resumed custom music and chart remain aligned with the DSP clock");
                            substep = 1;
                        }
                        return;
                    }
                    Release(); bot = false;
                    Check(game.Phase == ClonePhase.Results && game.WinSequence, "custom song reaches results through the real win sequence without death or revival");
                    Check(game.Tiles.Any(t => t.element.id == 10 && t.consumed) &&
                        game.Actor.position.z >= game.Map.elements.First(e => e.kind == "FinishJump").pos.z + 33,
                        "custom actor lands on the actual end platform and reaches its final collider");
                    Check(game.GameTime - game.Map.delayTime >= customAudioLength - .25 &&
                        AudioSettings.dspTime - customStartDsp >= (customAudioLength - .25) / game.PlaybackRate,
                        "full custom recording completes in real playback time without teleporting or seeking");
                    Check(game.Tiles.Where(t => t.element.id == 2 || t.element.id == 3 || t.element.id == 8).All(t => t.consumed) &&
                        game.Tiles.Any(t => t.element.id == 9) && game.Tiles.Where(t => t.element.id == 9).All(t => t.consumed),
                        "custom holds, every scored hold point and hold exits are crossed through actual contacts");
                    Check(game.Tiles.Where(t => t.element.id < 7 && t.element.updateType != 0).All(t => t.consumed),
                        "custom moving tiles are crossed through actual contacts");
                    Check(game.Tiles.Where(t => t.element.id == 4 || t.element.id == 5 || t.element.id == 6).All(t => t.consumed),
                        "every custom Follow entry, scored midpoint and outgoing exit is crossed through actual contacts");
                    NextCustom(68); break;
                case 68:
                    if (!ResultAnimationReady()) return;
                    var quote = game.SettlementQuote;
                    var persisted = OriginalProgression.FromJson(game.Progression.Config, PlayerPrefs.GetString(OriginalProgression.SaveKey, ""),
                        new ResourceSongCatalog(new UnityGameResources()), new PlayerPrefsProfileStore());
                    var progress = persisted.SongProgress(CustomSongId);
                    Check(game.Score > 0 && game.RunStars == 3 && progress != null && progress.normalStars == 3 && progress.normalScore == game.Score &&
                        progress.starts == 1 && progress.completions == 1, "custom three-star result and best score survive save deserialization exactly once");
                    Check(quote != null && quote.eligible && quote.claimed && game.Progression.State.settlements.Count == 1 &&
                        game.Progression.Gold == customGold + quote.totalGold, "native custom settlement pays exactly one full-tier reward");
                    game.PaySettlement(); game.PaySettlement();
                    persisted = OriginalProgression.FromJson(game.Progression.Config, PlayerPrefs.GetString(OriginalProgression.SaveKey, ""),
                        new ResourceSongCatalog(new UnityGameResources()), new PlayerPrefsProfileStore());
                    Check(game.Progression.Gold == customGold + quote.totalGold && persisted.Gold == game.Progression.Gold &&
                        persisted.State.settlements.Count == 1 && persisted.State.settlements[0].claimed && persisted.SongProgress(CustomSongId).completions == 1,
                        "repeated settlement requests preserve the saved wallet and single completion");
                    Capture("25-custom-results.png");
                    Check(errors.Count == 0, "full custom song route is free of runtime errors");
                    Finish(true, "custom " + CustomSongId + ", audio=" + customAudioLength.ToString("F3") +
                        "s, actual list selection and StartBtn, pause/resume, full real-time input playthrough, all hold points and Follow markers/exits, final platform, saved three stars/best, one settlement" +
                        ", captures gameplay=" + customGameplayCaptured + " hold=" + customHoldCaptured + " moving=" + customMovingCaptured); break;
            }
        }

        private static void NextCustom(int number)
        {
            Next(number, 15);
            if (customDeadline > 0) deadline = customDeadline;
        }

        private static void ObserveCustomPlayback()
        {
            CheckCustomPlaying();
            Bot();
            if (!customGameplayCaptured && game.GameTime >= game.Map.delayTime + 2)
            { Capture("23-custom-gameplay.png"); customGameplayCaptured = true; }
            if (!customHoldCaptured && game.Support != null && (game.Support.element.id == 2 || game.Support.element.id == 3))
            { Capture("24-custom-hold.png"); customHoldCaptured = true; }
            if (!customMovingCaptured && game.Support != null && game.Support.element.id == 4)
            { Capture("24-custom-follow.png"); customMovingCaptured = true; }
        }

        private static void CheckCustomPlaying()
        {
            if (game.Phase != ClonePhase.Playing) throw new Exception("custom playback unexpectedly entered " + game.Phase + " at " + game.GameTime);
        }

        private static void Bot()
        {
            if (!bot || game.Phase != ClonePhase.Playing) return;
            float z = game.Actor.position.z, target;
            if (game.Support != null && game.Support.element.id != 4) target = game.Support.CenterX;
            else
            {
                var next = game.Tiles.Where(t => !t.consumed && t.element.id != 7 && t.element.id != 8 && t.initialZ > z - .1f)
                    .OrderBy(t => t.initialZ).FirstOrDefault();
                target = next == null ? 0 : next.CenterX;
            }
            Hold(target);
        }

        private static void StartSong(string id)
        {
            game.Home(); game.Endless = false;
            var song = game.Songs.First(s => s.id == id);
            if (!game.Progression.State.unlockedSongs.Contains(id)) game.Progression.State.unlockedSongs.Add(id);
            game.SelectSong(song.catalogId); game.BeginRun();
            beginX = game.Actor.position.x; Hold(beginX); bot = true;
        }

        private static void BeginStartPress()
        {
            var target = game.Presenter.UICanvas.transform.Find("InternalStart_UIPanel/StartBtn") as RectTransform;
            Check(target != null && target.gameObject.activeInHierarchy, "authored StartBtn is active");
            var position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Check(hits.Count > 0 && hits[0].gameObject == target.gameObject, "original transparent StartBtn receives pointer input");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            pressedFrame = Time.frameCount; UpdatePointer();
        }

        private static void Hold(float worldX)
        {
            var pos = new Vector2(Screen.width * .5f + (worldX - beginX) * Screen.width / 6.6f, Screen.height * .28f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left));
            UpdatePointer();
        }
        private static void Release() { InputSystem.QueueStateEvent(mouse, new MouseState { position = mouse.position.ReadValue() }); UpdatePointer(); }
        private static void UpdatePointer() => typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null)
            .Invoke(null, new object[] { InputUpdateType.Dynamic });
        private static void BeginClick(Button button)
        {
            Check(button != null && button.gameObject.activeInHierarchy && button.interactable, "click target is an active original button");
            var rect = (RectTransform)button.transform;
            var pos = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(EventSystem.current) { position = pos };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            var canvas = button.GetComponentInParent<Canvas>();
            Debug.Log("INTEGRATED_PLAY CLICK screen=" + Screen.width + "x" + Screen.height + " pointer=" + pos + " rect=" + rect.rect +
                " canvas=" + canvas.renderMode + " pixels=" + canvas.pixelRect + " scale=" + canvas.scaleFactor +
                " graphic=" + button.targetGraphic?.name + " alpha=" + button.targetGraphic?.color.a + " cull=" + button.targetGraphic?.canvasRenderer.cull +
                " depth=" + button.targetGraphic?.depth + " raycast=" + button.targetGraphic?.raycastTarget +
                " raycaster=" + canvas.GetComponent<GraphicRaycaster>()?.enabled);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
                "original UI raycast reaches " + button.name + " (top=" + (hits.Count > 0 ? hits[0].gameObject.name : "none") + ")");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left));
            pressedFrame = Time.frameCount;
            UpdatePointer();
        }
        private static Button FindButton(string ending) => game.Presenter.UICanvas.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(b => PathOf(b.transform).EndsWith(ending, StringComparison.Ordinal));
        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        private static void Next(int number, int seconds) { step = number; substep = 0; deadline = EditorApplication.timeSinceStartup + seconds; }
        private static bool ResultAnimationReady()
        {
            if (substep == 0) { panelOpened = Time.unscaledTime; substep = 1; }
            return Time.unscaledTime - panelOpened >= 2.2f;
        }
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception("INTEGRATED_PLAY FAIL: " + message);
            Debug.Log("INTEGRATED_PLAY PASS: " + message);
        }
        private static void Capture(string file)
        {
            Directory.CreateDirectory(Out);
            var viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var view = Resources.FindObjectsOfTypeAll(viewType).FirstOrDefault();
            for (var type = viewType; view != null && type != null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!typeof(RenderTexture).IsAssignableFrom(field.FieldType)) continue;
                    var frame = field.GetValue(view) as RenderTexture;
                    if (frame == null || frame.width != 540 || frame.height != 960) continue;
                    var previous = RenderTexture.active;
                    var pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = frame; pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply();
                        if (SystemInfo.graphicsUVStartsAtTop)
                        {
                            var colors = pixels.GetPixels32();
                            for (int y = 0; y < frame.height / 2; y++)
                                for (int x = 0; x < frame.width; x++)
                                {
                                    int a = y * frame.width + x, b = (frame.height - y - 1) * frame.width + x;
                                    var swap = colors[a]; colors[a] = colors[b]; colors[b] = swap;
                                }
                            pixels.SetPixels32(colors); pixels.Apply();
                        }
                        File.WriteAllBytes(Path.Combine(Out, file), pixels.EncodeToPNG());
                        Debug.Log("INTEGRATED_PLAY captured actual GameView " + field.Name); return;
                    }
                    finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
                }
            }
            var cam = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null);
            var canvas = game.Presenter.UICanvas;
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance;
            var rt = new RenderTexture(540, 960, 24); var old = RenderTexture.active;
            var tex = new Texture2D(540, 960, TextureFormat.RGB24, false);
            var target = cam.targetTexture;
            var uiObject = new GameObject("QA original UI overlay camera");
            var uiCamera = uiObject.AddComponent<Camera>();
            uiCamera.cullingMask = 1 << 5; uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.nearClipPlane = .01f; uiCamera.farClipPlane = 100;
            var uiData = uiCamera.GetUniversalAdditionalCameraData();
            uiData.renderType = CameraRenderType.Overlay; uiData.renderPostProcessing = false;
            var stack = cam.GetUniversalAdditionalCameraData().cameraStack;
            try
            {
                cam.targetTexture = rt; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera; canvas.planeDistance = 1;
                stack.Add(uiCamera);
                Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 540, 960), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(Out, file), tex.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = target; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane;
                stack.Remove(uiCamera); UnityEngine.Object.DestroyImmediate(uiObject);
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        private static void Finish(bool success, string details)
        {
            if (SessionState.GetBool(Key + ".finishing", false)) return;
            SessionState.SetBool(Key + ".finishing", true);
            SessionState.SetBool(Key + ".resultSuccess", success);
            SessionState.SetString(Key + ".resultDetails", details);
            bot = false;
            Application.logMessageReceived -= Log;
            EditorApplication.update -= Tick;
            CleanupPlayInput();
            if (SessionState.GetBool(Key + ".keepEditor", false) || !Application.isBatchMode)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.isPaused = false;
                    EditorApplication.isPlaying = false;
                }
                else CompleteOpenEditorReview();
                return;
            }
            RestoreSave();
            RestoreGameViewState();
            SessionState.SetBool(Key, false);
            SessionState.SetBool(Key + ".keepEditor", false);
            SessionState.SetBool(Key + ".finishing", false);
            WriteResult(success, details);
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static void RestoreSave()
        {
            var saved = SessionState.GetString(Key + ".save", "");
            if (SessionState.GetBool(Key + ".hadSave", false)) PlayerPrefs.SetString(OriginalProgression.SaveKey, saved);
            else PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
            if (SessionState.GetBool(Key + ".hadGuide", false)) PlayerPrefs.SetInt("DancingCatsClone.v1.howToPlaySeen", SessionState.GetInt(Key + ".guide", 0));
            else PlayerPrefs.DeleteKey("DancingCatsClone.v1.howToPlaySeen");
            PlayerPrefs.Save();
        }

        private static void CleanupPlayInput()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (loop.subSystemList != null && loop.subSystemList.Any(system => system.type == typeof(IntegratedPlayValidation)))
            {
                loop.subSystemList = loop.subSystemList.Where(system => system.type != typeof(IntegratedPlayValidation)).ToArray();
                PlayerLoop.SetPlayerLoop(loop);
            }
            foreach (var device in disabledPhysicalDevices)
                if (device != null && device.added && !device.enabled) InputSystem.EnableDevice(device);
            disabledPhysicalDevices.Clear();
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            mouse = null;
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            previousMouse = null;
            if (previousInputSettings != null) InputSystem.settings = previousInputSettings;
            if (qaInputSettings != null && qaInputSettings != previousInputSettings) UnityEngine.Object.DestroyImmediate(qaInputSettings);
            qaInputSettings = null; previousInputSettings = null;
        }

        private static void CompleteOpenEditorReview()
        {
            // Scene reopening is deferred until Unity has safely restored Edit Mode.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool success = SessionState.GetBool(Key + ".resultSuccess", false);
            string details = SessionState.GetString(Key + ".resultDetails", "Review did not complete.");
            try
            {
                RestoreSave();
                var snapshot = JsonUtility.FromJson<ReviewSnapshot>(SessionState.GetString(Key + ".editor", ""));
                if (snapshot == null || snapshot.scenes == null) throw new InvalidOperationException("Prior Editor scene snapshot is missing.");
                var current = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
                if (current.Any(scene => scene.isDirty)) throw new InvalidOperationException("Review stopped, but scene restoration was refused because the current Editor scene has unsaved changes. No scene was saved or discarded.");
                var setup = snapshot.scenes.Select(scene => new SceneSetup { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray();
                var actual = EditorSceneManager.GetSceneManagerSetup();
                if (actual.Length != setup.Length || actual.Where((scene, i) => scene.path != setup[i].path || scene.isLoaded != setup[i].isLoaded || scene.isActive != setup[i].isActive).Any())
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                UnityEngine.Object Resolve(string value) => !string.IsNullOrEmpty(value) && GlobalObjectId.TryParse(value, out var id) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) : null;
                Selection.objects = (snapshot.selection ?? Array.Empty<string>()).Select(Resolve).Where(value => value != null).ToArray();
                var active = Resolve(snapshot.activeSelection);
                if (active != null) Selection.activeObject = active;
                details += "; returned to Edit Mode, restored prior clean scenes, selection, input and saved progress";
            }
            catch (Exception error) { success = false; details += "; Editor restoration failed: " + error.Message; }
            finally
            {
                try { RestoreGameViewState(); }
                catch (Exception error) { success = false; details += "; GameView restoration failed: " + error.Message; }
                SessionState.SetBool(Key, false);
                SessionState.SetBool(Key + ".keepEditor", false);
                SessionState.SetBool(Key + ".finishing", false);
            }
            WriteResult(success, details);
        }

        private static void WriteResult(bool success, string details)
        {
            Directory.CreateDirectory(Out);
            File.WriteAllText(Path.Combine(Out, "integrated-result.txt"), (success ? "PASS " : "FAIL ") + details);
            Debug.Log("INTEGRATED_PLAY " + (success ? "PASS " : "FAIL ") + details);
        }
    }
}
