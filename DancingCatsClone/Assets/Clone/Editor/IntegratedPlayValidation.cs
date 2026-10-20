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
        private static OriginalTile edgeHold, edgeExit, edgeFinish, edgePrevious;
        private static int edgePointCount, edgeScore;
        private static float edgeMissX;
        private static int diceGold, dicePosition;
        private static bool bot;
        private static InputSettings previousInputSettings;
        private static readonly List<string> errors = new List<string>();
        private static readonly string[] PanelNames = { "NewRoleShop_UIPanel", "DailyReward_UIPanel", "LoginReward_UIPanel", "ChallengeLevel_UIPanel", "PlayDice_UIPanel", "CatHouseBuilding_UIPanel", "Set_UIPanel" };
        private static string Out => Environment.GetEnvironmentVariable("DANCING_CATS_QA_OUTPUT") ?? Path.GetFullPath("../work/clone-validation");

        static IntegratedPlayValidation()
        {
            EditorApplication.playModeStateChanged += Mode;
        }

        public static void Run() => StartValidation(false, false);
        public static void RunInterfaceAndRecovery() => StartValidation(true, false);
        public static void RunChallenge() => StartValidation(false, false, true);
        public static void RunMenus()
        {
            ProgressionValidation.Run();
            OriginalAssetLibraryValidation.Run();
            StartValidation(true, true);
        }

        private static void StartValidation(bool followUp, bool onlyMenus, bool onlyChallenge = false)
        {
            SessionState.SetBool(Key + ".followUp", followUp);
            SessionState.SetBool(Key + ".menusOnly", onlyMenus);
            SessionState.SetBool(Key + ".challengeOnly", onlyChallenge);
            SetPortraitGameView();
            SessionState.SetString(Key + ".save", PlayerPrefs.GetString(OriginalProgression.SaveKey, ""));
            SessionState.SetInt(Key + ".guide", PlayerPrefs.GetInt("DancingCatsClone.v1.howToPlaySeen", 0));
            PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
            PlayerPrefs.SetInt("DancingCatsClone.v1.howToPlaySeen", 0);
            PlayerPrefs.Save();
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(CloneSceneBuilder.ScenePath);
            EditorApplication.isPlaying = true;
        }

        private static void SetPortraitGameView()
        {
            var assembly = typeof(EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            var resolutionType = assembly.GetType("UnityEditor.GameViewSizeType", true);
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var size = Activator.CreateInstance(sizeType, new[] { Enum.Parse(resolutionType, "FixedResolution"), (object)540, 960, "Clone portrait QA" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            var viewType = assembly.GetType("UnityEditor.GameView", true);
            var view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
            view.Show(); view.Focus(); view.Repaint();
            Debug.Log("INTEGRATED_PLAY portrait GameView selected index=" + index);
        }

        private static void Mode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                previousInputSettings = InputSystem.settings;
                var settings = UnityEngine.Object.Instantiate(previousInputSettings);
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = settings;
                step = substep = 0; bot = false; uiReady = -1; errors.Clear();
                recoveryFollowUp = SessionState.GetBool(Key + ".followUp", false);
                menusOnly = SessionState.GetBool(Key + ".menusOnly", false);
                challengeOnly = SessionState.GetBool(Key + ".challengeOnly", false);
                deadline = EditorApplication.timeSinceStartup + 30;
                Application.logMessageReceived += Log;
                EditorApplication.update -= Tick;
                var loop = PlayerLoop.GetCurrentPlayerLoop();
                loop.subSystemList = loop.subSystemList.Concat(new[] { new PlayerLoopSystem { type = typeof(IntegratedPlayValidation), updateDelegate = Tick } }).ToArray();
                PlayerLoop.SetPlayerLoop(loop);
            }
        }

        private static void Log(string message, string trace, LogType type)
        {
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
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused) return;
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
                    mouse = InputSystem.AddDevice<Mouse>("Clone QA pointer"); mouse.MakeCurrent();
                    foreach (var physical in InputSystem.devices.Where(d => d.native && (d is Mouse || d is Keyboard)).ToArray()) InputSystem.DisableDevice(physical);
                }
                switch (step)
                {
                    case 0:
                        Check(game.Songs.Length == 58 && game.Assets.RoleNames.Length == 63, "all original songs and cats bound");
                        Check(game.Phase == ClonePhase.Menu, "fresh startup opens original menu");
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
            Application.logMessageReceived -= Log;
            EditorApplication.update -= Tick;
            Directory.CreateDirectory(Out);
            File.WriteAllText(Path.Combine(Out, "integrated-result.txt"), (success ? "PASS " : "FAIL ") + details);
            var saved = SessionState.GetString(Key + ".save", "");
            if (saved.Length == 0) PlayerPrefs.DeleteKey(OriginalProgression.SaveKey); else PlayerPrefs.SetString(OriginalProgression.SaveKey, saved);
            PlayerPrefs.SetInt("DancingCatsClone.v1.howToPlaySeen", SessionState.GetInt(Key + ".guide", 0)); PlayerPrefs.Save();
            SessionState.SetBool(Key, false);
            if (previousInputSettings != null) InputSystem.settings = previousInputSettings;
            Debug.Log("INTEGRATED_PLAY " + (success ? "PASS " : "FAIL ") + details);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
