using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using DancingCat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.LowLevel;
using UnityEngine.Rendering;

/// <summary>
/// Real scene, real UI callbacks and real Input System mouse events. No game state is corrected.
/// Batch invocation must omit -quit; the runner exits after Play Mode and restores its save keys.
/// </summary>
[InitializeOnLoad]
public static class CatPlayValidation
{
    private const string Session = "DancingCat.PlayValidation.";
    private const string SavePrefix = "DancingCat.v2.";
    private const string ScenePath = "Assets/DancingCat/Scenes/DancingCat_Preview.unity";
    private sealed class BeforeGameUpdate { }
    private sealed class AfterGameUpdate { }

    [Serializable] private sealed class Pref
    {
        public string key;
        public string kind;
        public bool existed;
        public int integer;
        public float number;
        public string text;
    }
    [Serializable] private sealed class Preferences { public List<Pref> values = new List<Pref>(); }
    [Serializable] private sealed class Config
    {
        public string report;
        public string mode = "autoplay";
        public string captureFolder;
        public float seconds = 65;
        public int fps = 60;
        public int song;
        public int difficulty;
    }
    [Serializable] private sealed class Landing
    {
        public int run;
        public int frame;
        public int index;
        public string kind;
        public int score;
        public int combo;
        public float songTime;
        public float x;
        public float lane;
    }
    [Serializable] private sealed class Snapshot
    {
        public int frame;
        public string phase;
        public float realtime;
        public float songTime;
        public float x;
        public float y;
        public float expectedY;
        public float z;
        public int landings;
        public int score;
        public double dspTime;
        public double expectedSongTime;
        public double sourceTime;
        public double dspDrift;
        public double audioDrift;
    }
    [Serializable] private sealed class Report
    {
        public string unityVersion;
        public string scene = ScenePath;
        public string mode;
        public string songId;
        public string songTitle;
        public int requestedFps;
        public int frames;
        public float requestedSeconds;
        public float durationRealtime;
        public float actualAverageFps;
        public string startedUtc;
        public string finishedUtc;
        public string stopReason;
        public string finalPhase;
        public bool passed;
        public bool gameStarted;
        public bool completed;
        public bool failureObserved;
        public bool intentionalMissApplied;
        public bool pauseChecked;
        public bool pauseClockFrozen;
        public float pauseDrift;
        public bool resumeChecked;
        public bool reviveChecked;
        public int reviveCoinsSpent;
        public bool restartChecked;
        public bool homeChecked;
        public bool preferencesRestored;
        public bool extrasStarted;
        public bool dailyChecked;
        public bool dailyClaimOnce;
        public bool skinPurchaseChecked;
        public bool skinAnimatorChecked;
        public bool volumeChecked;
        public bool challengeOpened;
        public bool roomUpgraded;
        public bool trailChecked;
        public bool diceChecked;
        public bool modeChecked;
        public bool difficultyChecked;
        public bool latencyChecked;
        public bool vibrationChecked;
        public bool fpsChecked;
        public bool songUnlockChecked;
        public int totalNotes;
        public int expectedScore;
        public int totalLandings;
        public int duplicateLandings;
        public int shortLandings;
        public int holdLandings;
        public int slideLandings;
        public int completedLandings;
        public int completedScore;
        public int lastScore;
        public int maxCombo;
        public int endlessLaps;
        public bool scoreMonotonic = true;
        public int heightSamples;
        public float maximumHeightError;
        public float maximumZError;
        public double maximumDspDrift;
        public int scheduledPrerollFrames;
        public double maximumAudioDrift;
        public int audioSamples;
        public int tailBones;
        public float maximumTailLocalPositionError;
        public float maximumTailLengthRatio = 1;
        public float maximumTailAngularSpeed;
        public string worstTailBone;
        public int worstTailFrame;
        public float worstTailAngle;
        public float worstTailDeltaTime;
        public bool allTransformsFinite = true;
        public int runtimeErrors;
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> failedCriteria = new List<string>();
        public List<string> buttonsInvoked = new List<string>();
        public List<string> screenshots = new List<string>();
        public List<Landing> landings = new List<Landing>();
        public List<Snapshot> samples = new List<Snapshot>();
    }
    private sealed class TailBone
    {
        public Transform transform;
        public Vector3 localPosition;
        public Quaternion lastRotation;
        public int lastFrame = -1;
    }

    private static Config _config;
    private static Report _report;
    private static Preferences _preferences;
    private static CatGameController _controller;
    private static CatRunState _observedRun;
    private static Mouse _mouse;
    private static bool _ownedMouse;
    private static bool _loopInstalled;
    private static bool _stopping;
    private static bool _needAnchor = true;
    private static bool _wasPlaying;
    private static bool _gameCaptured;
    private static int _warmup;
    private static int _startStage;
    private static int _stageFrames;
    private static int _runNumber;
    private static int _flowStage;
    private static int _extrasStage;
    private static int _balanceAfterDaily;
    private static int _finishFrames;
    private static int _lastMeasuredFrame = -1;
    private static float _mouseAnchorX;
    private static float _mouseAnchorPointer;
    private static double _startedAt;
    private static double _editorStartedAt;
    private static double _pauseAt;
    private static float _pausedSongTime;
    private static double _nextSample;
    private static PlayerLoopSystem _originalLoop;
    private static readonly HashSet<string> Landings = new HashSet<string>();
    private static readonly List<TailBone> Tail = new List<TailBone>();
    private static readonly FieldInfo ScheduleField = typeof(CatGameController).GetField("_scheduledStart", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo OffsetField = typeof(CatGameController).GetField("_clockOffset", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo RateField = typeof(CatGameController).GetField("_rate", BindingFlags.Instance | BindingFlags.NonPublic);

    static CatPlayValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.update += Watchdog;
        Application.logMessageReceived += OnLog;
        if (!SessionState.GetBool(Session + "Active", false)) return;
        RestoreSession();
        if (SessionState.GetBool(Session + "Stopping", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.delayCall += Finish;
        else if (EditorApplication.isPlaying)
            EditorApplication.delayCall += ObservePlay;
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Session + "Active", false))
            throw new InvalidOperationException("Another Play Mode validation is active.");
        _config = Arguments();
        _report = new Report
        {
            unityVersion = Application.unityVersion, mode = _config.mode, requestedFps = _config.fps,
            requestedSeconds = _config.seconds, startedUtc = DateTime.UtcNow.ToString("O")
        };
        _preferences = BackupPreferences();
        Directory.CreateDirectory(Path.GetDirectoryName(_config.report));
        File.WriteAllText(_config.report + ".prefs-backup.json", JsonUtility.ToJson(_preferences, true));
        SessionState.SetString(Session + "Config", JsonUtility.ToJson(_config));
        SessionState.SetString(Session + "Report", JsonUtility.ToJson(_report));
        SessionState.SetString(Session + "Preferences", JsonUtility.ToJson(_preferences));
        SessionState.SetBool(Session + "Stopping", false);
        SessionState.SetBool(Session + "Active", true);
        _editorStartedAt = EditorApplication.timeSinceStartup;
        // Use a reproducible offline save fixture for unlocks and a paid revive.
        // Original values, including absent keys, are restored after Play Mode.
        PlayerPrefs.SetInt(SavePrefix + "Coins", 1000);
        PlayerPrefs.SetInt(SavePrefix + "Difficulty", _config.difficulty);
        PlayerPrefs.SetInt(SavePrefix + "Endless", _config.mode == "endless" ? 1 : 0);
        PlayerPrefs.SetFloat(SavePrefix + "LatencyMs", 0);
        PlayerPrefs.SetInt(SavePrefix + "Vibration", 0);
        PlayerPrefs.SetInt(SavePrefix + "TargetFps", _config.fps);
        PlayerPrefs.SetString(SavePrefix + "Meta", JsonUtility.ToJson(new CatMetaData
        {
            difficulty = _config.difficulty, endless = _config.mode == "endless", targetFps = _config.fps,
            vibration = false, latencyMs = 0
        }));
        if (_config.mode == "extras")
        {
            PlayerPrefs.SetInt(SavePrefix + "Skin", 0);
            PlayerPrefs.SetInt(SavePrefix + "Owned.1", 0);
            PlayerPrefs.SetInt(SavePrefix + "UnlockedSong.3", 0);
        }
        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception) { Error("Setup: " + exception); Stop("setup_error"); }
    }

    private static Config Arguments()
    {
        var config = new Config();
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (!key.StartsWith("-cat", StringComparison.Ordinal)) continue;
            if (++i >= args.Length) throw new ArgumentException("Missing value for " + key);
            string value = args[i];
            switch (key)
            {
                case "-catReport": config.report = value; break;
                case "-catMode": config.mode = value; break;
                case "-catFps": config.fps = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "-catSong": config.song = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "-catDifficulty": config.difficulty = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "-catSeconds": config.seconds = float.Parse(value, CultureInfo.InvariantCulture); break;
                case "-catCaptureFolder": config.captureFolder = value; break;
                case "-catScreenshot": config.captureFolder = Path.GetDirectoryName(value); break;
                default: throw new ArgumentException("Unknown validation argument: " + key);
            }
        }
        if (string.IsNullOrEmpty(config.report) || !Path.IsPathRooted(config.report)) throw new ArgumentException("-catReport must be absolute.");
        if (config.captureFolder != null && !Path.IsPathRooted(config.captureFolder)) throw new ArgumentException("-catCaptureFolder must be absolute.");
        if (config.mode != "autoplay" && config.mode != "miss" && config.mode != "flow" && config.mode != "endless" && config.mode != "extras") throw new ArgumentException("Mode must be autoplay, miss, flow, endless or extras.");
        if ((config.fps != 30 && config.fps != 60 && config.fps != 120) || config.song < 0 || config.seconds <= 0 || config.seconds > 600 || config.difficulty < 0 || config.difficulty > 2) throw new ArgumentOutOfRangeException("Invalid run parameters.");
        return config;
    }

    private static Preferences BackupPreferences()
    {
        var result = new Preferences();
        AddPref(result, SavePrefix + "Coins", "int");
        AddPref(result, SavePrefix + "Skin", "int");
        AddPref(result, SavePrefix + "Volume", "float");
        AddPref(result, SavePrefix + "Daily", "string");
        AddPref(result, SavePrefix + "Difficulty", "int");
        AddPref(result, SavePrefix + "Endless", "int");
        AddPref(result, SavePrefix + "LatencyMs", "float");
        AddPref(result, SavePrefix + "Vibration", "int");
        AddPref(result, SavePrefix + "TargetFps", "int");
        AddPref(result, SavePrefix + "Meta", "string");
        for (int i = 0; i < 5; i++)
        {
            AddPref(result, SavePrefix + "Owned." + i, "int");
            AddPref(result, SavePrefix + "UnlockedSong." + i, "int");
        }
        foreach (string guid in AssetDatabase.FindAssets("t:CatSong", new[] { "Assets/DancingCat" }))
        {
            CatSong song = AssetDatabase.LoadAssetAtPath<CatSong>(AssetDatabase.GUIDToAssetPath(guid));
            if (song == null) continue;
            AddPref(result, SavePrefix + "Best." + song.id, "int");
            AddPref(result, SavePrefix + "Stars." + song.id, "int");
        }
        return result;
    }

    private static void AddPref(Preferences preferences, string key, string kind)
    {
        if (preferences.values.Exists(value => value.key == key)) return;
        preferences.values.Add(new Pref
        {
            key = key, kind = kind, existed = PlayerPrefs.HasKey(key), integer = kind == "int" ? PlayerPrefs.GetInt(key) : 0,
            number = kind == "float" ? PlayerPrefs.GetFloat(key) : 0, text = kind == "string" ? PlayerPrefs.GetString(key) : ""
        });
    }

    private static void RestorePreferences(Preferences preferences)
    {
        foreach (Pref value in preferences.values)
        {
            if (!value.existed) PlayerPrefs.DeleteKey(value.key);
            else if (value.kind == "int") PlayerPrefs.SetInt(value.key, value.integer);
            else if (value.kind == "float") PlayerPrefs.SetFloat(value.key, value.number);
            else PlayerPrefs.SetString(value.key, value.text);
        }
        PlayerPrefs.Save();
    }

    // Recovery entry point if an external process kill prevented the normal cleanup.
    public static void RestoreSavedPreferences()
    {
        Config config = Arguments();
        RestorePreferences(JsonUtility.FromJson<Preferences>(File.ReadAllText(config.report + ".prefs-backup.json")));
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    private static void RestoreSession()
    {
        _config = JsonUtility.FromJson<Config>(SessionState.GetString(Session + "Config", ""));
        _report = JsonUtility.FromJson<Report>(SessionState.GetString(Session + "Report", ""));
        _preferences = JsonUtility.FromJson<Preferences>(SessionState.GetString(Session + "Preferences", ""));
        _editorStartedAt = EditorApplication.timeSinceStartup;
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Session + "Active", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) ObservePlay();
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            RemoveLoop();
            if (!SessionState.GetBool(Session + "Stopping", false)) Stop("play_stopped_early");
        }
        if (state == PlayModeStateChange.EnteredEditMode) Finish();
    }

    private static void ObservePlay()
    {
        if (_loopInstalled || !SessionState.GetBool(Session + "Active", false)) return;
        if (_config == null || _report == null) RestoreSession();
        _stopping = false;
        _warmup = _startStage = _stageFrames = _runNumber = _flowStage = _finishFrames = _extrasStage = 0;
        _lastMeasuredFrame = -1;
        _needAnchor = true;
        _wasPlaying = _gameCaptured = false;
        _nextSample = 0;
        Landings.Clear();
        Tail.Clear();
        Application.runInBackground = true;
        Application.targetFrameRate = _config.fps;
        QualitySettings.vSyncCount = 0;
        _mouse = Mouse.current;
        _ownedMouse = _mouse == null;
        if (_ownedMouse) _mouse = InputSystem.AddDevice<Mouse>();
        _originalLoop = PlayerLoop.GetCurrentPlayerLoop();
        PlayerLoopSystem loop = CopyLoop(_originalLoop);
        bool before = Insert(ref loop, typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate),
            new PlayerLoopSystem { type = typeof(BeforeGameUpdate), updateDelegate = BeforeUpdate }, false);
        bool after = Insert(ref loop, typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate),
            new PlayerLoopSystem { type = typeof(AfterGameUpdate), updateDelegate = AfterUpdate }, true);
        if (!before || !after) { Error("Update/LateUpdate stages were not found."); Stop("player_loop_error"); return; }
        PlayerLoop.SetPlayerLoop(loop);
        _loopInstalled = true;
    }

    private static void BeforeUpdate()
    {
        if (_stopping) return;
        try
        {
            if (_startStage == 0)
            {
                if (++_warmup < 8) return;
                _controller = UnityEngine.Object.FindAnyObjectByType<CatGameController>();
                if (_controller == null) throw new InvalidOperationException("New CatGameController is missing from the real preview scene.");
                if (_config.song >= _controller.Songs.Length) throw new ArgumentException("Requested song index does not exist.");
                _controller.TargetFps = _config.fps;
                Capture("menu");
                if (_config.mode == "extras")
                {
                    _startedAt = Time.realtimeSinceStartupAsDouble;
                    _report.extrasStarted = true;
                    _startStage = 3;
                    return;
                }
                Click("SongPlay_" + _config.song);
                if (_controller.Phase == CatGamePhase.MainMenu) Click("SongPlay_" + _config.song);
                BindRun();
                _report.songId = _controller.SelectedSong.id;
                _report.songTitle = _controller.SelectedSong.title;
                _report.totalNotes = _controller.SelectedSong.notes.Count;
                _report.expectedScore = _report.totalNotes * 10;
                foreach (CatNote note in _controller.SelectedSong.notes)
                    if (note.checkpoints != null)
                        foreach (float point in note.checkpoints)
                            if (point > 0 && point < note.duration) _report.expectedScore += 2;
                BindTail();
                _startStage = 1;
                return;
            }
            if (_config.mode == "extras") { RunExtras(); return; }
            if (_startStage == 1)
            {
                if (++_stageFrames < 3) return;
                Capture("ready");
                Click("BeginRun");
                _startedAt = Time.realtimeSinceStartupAsDouble;
                _report.gameStarted = true;
                _startStage = 2;
            }
            ProcessFlow();
            if (_config.mode == "endless" && !ReferenceEquals(_observedRun, _controller.Run))
            {
                _report.endlessLaps++;
                BindRun();
                // Keep the finger held at the previous lap's actual pointer position.
                // The controller must discard the old drag origin itself.
                _mouseAnchorX = _controller.Run.X;
                _mouseAnchorPointer = _mouse.position.ReadValue().x;
                _needAnchor = false;
                QueueMouse(true, _mouseAnchorPointer);
                _wasPlaying = true;
                return;
            }
            bool playing = _controller.Phase == CatGamePhase.Playing;
            if (playing)
            {
                if (!_wasPlaying) _needAnchor = true;
                DriveMouse();
            }
            else QueueMouse(false, Screen.width * .5f);
            _wasPlaying = playing;
        }
        catch (Exception exception) { Error("Driver: " + exception); Stop("driver_error"); }
    }

    private static void BindRun()
    {
        if (ReferenceEquals(_observedRun, _controller.Run)) return;
        if (_observedRun != null) _observedRun.Landed -= OnLanding;
        _observedRun = _controller.Run;
        if (_observedRun == null) return;
        _runNumber++;
        _observedRun.Landed += OnLanding;
    }

    private static void DriveMouse()
    {
        CatRunState run = _controller.Run;
        if (_needAnchor)
        {
            _mouseAnchorX = run.X;
            _mouseAnchorPointer = Screen.width * .5f;
            QueueMouse(true, _mouseAnchorPointer);
            _needAnchor = false;
            return;
        }
        float target = DesiredLane(_controller.SelectedSong, (float)ExpectedSongTime());
        bool miss = (_config.mode == "miss" && run.Landings >= 5) ||
                    (_config.mode == "flow" && _flowStage == 2 && run.Landings >= 10);
        if (miss)
        {
            float lane = run.Next != null ? run.Next.lane : run.X;
            target = lane >= 0 ? -1.7f : 1.7f;
            _report.intentionalMissApplied = true;
        }
        float pointer = _mouseAnchorPointer + (target - _mouseAnchorX) / 5.5f * Screen.width;
        QueueMouse(true, pointer);
    }

    private static float DesiredLane(CatSong song, float time)
    {
        if (song.notes.Count == 0) return 0;
        int current = 0;
        while (current + 1 < song.notes.Count && song.notes[current + 1].time <= time) current++;
        CatNote note = song.notes[current];
        float departure = note.time + note.duration;
        float end = note.kind == CatNoteKind.Slide ? note.endLane : note.lane;
        if (time <= departure)
        {
            if (note.kind != CatNoteKind.Slide || note.duration <= 0) return note.lane;
            float elapsed = time - note.time, previousTime = 0, previousLane = note.lane;
            foreach (var point in note.path ?? new List<CatPathPoint>())
            {
                if (elapsed <= point.time) return Mathf.Lerp(previousLane, point.lane, Mathf.InverseLerp(previousTime, point.time, elapsed));
                previousTime = point.time; previousLane = point.lane;
            }
            return Mathf.Lerp(previousLane, note.endLane, Mathf.InverseLerp(previousTime, note.duration, elapsed));
        }
        if (current + 1 >= song.notes.Count) return end;
        CatNote next = song.notes[current + 1];
        return Mathf.Lerp(end, next.lane, Mathf.InverseLerp(departure, next.time, time));
    }

    private static void QueueMouse(bool held, float x)
    {
        if (_mouse == null || !_mouse.added) return;
        _mouse.MakeCurrent();
        InputSystem.QueueStateEvent(_mouse, new MouseState
        {
            position = new Vector2(x, Screen.height * .43f),
            buttons = (ushort)(held ? 1 : 0)
        });
        InputSystem.Update();
    }

    private static void RunExtras()
    {
        CatMetaProgress meta = _controller.GetComponent<CatMetaProgress>();
        if (meta == null) throw new InvalidOperationException("Offline progression component is missing.");
        switch (_extrasStage++)
        {
            case 0:
                Click("Tab_Rewards");
                break;
            case 1:
            {
                int balance = _controller.Coins;
                int reward = meta.NextDailyReward;
                Click("ClaimDaily");
                _balanceAfterDaily = _controller.Coins;
                _report.dailyChecked = _balanceAfterDaily == balance + reward && !meta.CanClaimDaily;
                _report.dailyClaimOnce = !FindButton("ClaimDaily", false).interactable;
                Capture("daily");
                break;
            }
            case 2:
            {
                _report.dailyClaimOnce &= _controller.Coins == _balanceAfterDaily;
                Click("Tab_Cats");
                int balance = _controller.Coins;
                Click("SkinSelect_1");
                _report.skinPurchaseChecked = _controller.IsSkinOwned(1) && _controller.SelectedSkin == 1 &&
                    balance - _controller.Coins == _controller.SkinPrices[1];
                break;
            }
            case 3:
                foreach (Animator animator in _controller.Player.GetComponentsInChildren<Animator>(true))
                    if (animator.runtimeAnimatorController == _controller.CatAnimator && animator.enabled)
                        _report.skinAnimatorChecked = true;
                Capture("cats");
                Click("Tab_Songs");
                Click("OpenSettings");
                break;
            case 4:
                SetSlider("MusicVolume", .35f);
                _report.volumeChecked = Mathf.Abs(_controller.MusicVolume - .35f) < .001f &&
                    Mathf.Abs(_controller.Music.volume - .35f) < .001f;
                Capture("volume_settings");
                Click("CloseSettings");
                break;
            case 5:
                Click("ExtraMenuButton");
                Click("OpenExtra_Challenge");
                _report.challengeOpened = FindButton("ChallengePlay", false) != null;
                Capture("challenge");
                Click("ChallengeBack");
                break;
            case 6:
            {
                Click("OpenExtra_House");
                int balance = _controller.Coins;
                int level = meta.RoomLevel(0);
                int price = meta.RoomUpgradePrice(0);
                Click("RoomUpgrade_0");
                _report.roomUpgraded = meta.RoomLevel(0) == level + 1 && balance - _controller.Coins == price && price == 40;
                Capture("house");
                Click("HouseBack");
                break;
            }
            case 7:
            {
                Click("OpenExtra_Trails");
                int balance = _controller.Coins;
                Click("TrailSelect_1");
                _report.trailChecked = meta.IsTrailOwned(1) && meta.SelectedTrail == 1 && balance - _controller.Coins == 50;
                Capture("trails");
                Click("TrailsBack");
                break;
            }
            case 8:
            {
                Click("OpenExtra_Dice");
                int balance = _controller.Coins;
                int rolls = meta.Data.diceRolls;
                Click("DiceRoll");
                int reward = meta.Data.lastDiceReward;
                _report.diceChecked = meta.Data.diceRolls == rolls + 1 && Array.IndexOf(CatMetaRules.DiceRewards, reward) >= 0 &&
                    _controller.Coins == balance - 10 + reward;
                Capture("dice");
                Click("DiceBack");
                break;
            }
            case 9:
                Click("OpenExtra_Options");
                Click("ModeEndless");
                _report.modeChecked = _controller.Endless;
                Click("ModeNormal");
                _report.modeChecked &= !_controller.Endless;
                Click("Difficulty_2");
                _report.difficultyChecked = _controller.Difficulty == 2;
                Click("Difficulty_0");
                _report.difficultyChecked &= _controller.Difficulty == 0;
                break;
            case 10:
            {
                SetSlider("LatencySlider", 80);
                _report.latencyChecked = Mathf.Abs(_controller.AudioLatencyMs - 80) < .001f && Mathf.Abs(meta.Data.latencyMs - 80) < .001f;
                bool vibration = _controller.Vibration;
                Click("VibrationToggle");
                _report.vibrationChecked = _controller.Vibration != vibration;
                Click("VibrationToggle");
                _report.vibrationChecked &= _controller.Vibration == vibration;
                break;
            }
            case 11:
                _report.fpsChecked = true;
                foreach (int rate in new[] { 30, 60, 120 })
                {
                    Click("Fps_" + rate);
                    _report.fpsChecked &= _controller.TargetFps == rate && Application.targetFrameRate == rate;
                }
                Capture("options");
                Click("Fps_" + _config.fps);
                SetSlider("LatencySlider", 0);
                Click("OptionsBack");
                Click("HubBack");
                break;
            case 12:
            {
                Click("Tab_Songs");
                int balance = _controller.Coins;
                int cost = _controller.SongUnlockCost(3);
                Click("SongPlay_3");
                _report.songUnlockChecked = _controller.IsSongUnlocked(3) && _controller.Phase == CatGamePhase.MainMenu && balance - _controller.Coins == cost;
                Click("SongPlay_3");
                _report.songUnlockChecked &= _controller.Phase == CatGamePhase.Countdown && _controller.SelectedSong == _controller.Songs[3];
                Capture("unlocked_song_ready");
                Stop("extras_complete");
                break;
            }
        }
    }

    private static void SetSlider(string name, float value)
    {
        foreach (UnityEngine.UI.Slider slider in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Slider>(FindObjectsInactive.Include))
        {
            if (slider.name != name || !slider.gameObject.activeInHierarchy || !slider.interactable) continue;
            slider.value = value;
            return;
        }
        throw new InvalidOperationException("Real active UI slider is unavailable: " + name);
    }

    private static void ProcessFlow()
    {
        if (_config.mode != "flow") return;
        if (_flowStage == 0 && _controller.Phase == CatGamePhase.Playing && _controller.SongTime >= 3)
        {
            Click("PauseGame");
            _pausedSongTime = _controller.SongTime;
            _pauseAt = Time.realtimeSinceStartupAsDouble;
            _flowStage = 1;
        }
        else if (_flowStage == 1 && Time.realtimeSinceStartupAsDouble - _pauseAt >= .6)
        {
            _report.pauseChecked = true;
            _report.pauseDrift = Mathf.Abs(_controller.SongTime - _pausedSongTime);
            _report.pauseClockFrozen = _controller.Phase == CatGamePhase.Paused && _report.pauseDrift < .0001f;
            Click("ResumeGame");
            _report.resumeChecked = _controller.Phase == CatGamePhase.Playing;
            _flowStage = 2;
        }
        else if (_flowStage == 2 && _controller.Phase == CatGamePhase.Failed)
        {
            Capture("failed");
            int coins = _controller.Coins;
            Click("Revive");
            _report.reviveCoinsSpent = coins - _controller.Coins;
            _report.reviveChecked = _controller.Phase == CatGamePhase.Playing && _report.reviveCoinsSpent == _controller.ReviveCost;
            _needAnchor = true;
            _flowStage = 3;
        }
        else if (_flowStage == 4)
        {
            if (++_finishFrames < 4) return;
            Click("ResultRetry");
            BindRun();
            _report.restartChecked = _controller.Phase == CatGamePhase.Countdown && _controller.Score == 0 && _controller.Run.Landings == 0;
            Click("BeginRun");
            _needAnchor = true;
            _flowStage = 5;
        }
        else if (_flowStage == 5 && _controller.Phase == CatGamePhase.Playing && _controller.Run.Landings >= 2)
        {
            Click("PauseGame");
            Click("PauseHome");
            _report.homeChecked = _controller.Phase == CatGamePhase.MainMenu && _controller.Run == null;
            Capture("home");
            Stop("flow_complete");
        }
    }

    private static void AfterUpdate()
    {
        if (_stopping || (!_report.gameStarted && !_report.extrasStarted) || _lastMeasuredFrame == Time.frameCount) return;
        _lastMeasuredFrame = Time.frameCount;
        try
        {
            _report.frames++;
            _report.durationRealtime = (float)(Time.realtimeSinceStartupAsDouble - _startedAt);
            _report.finalPhase = _controller.Phase.ToString();
            if (_config.mode == "endless" && _controller.Score < _report.lastScore) _report.scoreMonotonic = false;
            _report.lastScore = _controller.Score;
            _report.maxCombo = Math.Max(_report.maxCombo, _controller.BestCombo);
            Measure();
            if (_report.runtimeErrors > 0) { Stop("runtime_error"); return; }
            if (_config.mode == "extras") return;
            if (_config.mode == "endless" && _report.endlessLaps >= 2) { Stop("endless_complete"); return; }
            if (!_gameCaptured && _controller.Phase == CatGamePhase.Playing && _controller.SongTime > 2)
            {
                Capture("game");
                _gameCaptured = true;
            }
            if (_controller.Phase == CatGamePhase.Failed)
            {
                _report.failureObserved = true;
                if (_config.mode != "flow" || _flowStage != 2)
                {
                    Capture("failed");
                    Stop("failed");
                    return;
                }
            }
            if (_controller.Phase == CatGamePhase.Complete)
            {
                if (!_report.completed)
                {
                    _report.completed = true;
                    _report.completedLandings = _controller.Run.Landings;
                    _report.completedScore = _controller.Score;
                    Capture("results");
                }
                if (_config.mode == "flow" && _flowStage == 3) { _flowStage = 4; _finishFrames = 0; }
                else if (_config.mode != "flow") { Stop("complete"); return; }
            }
            if (_report.durationRealtime >= _config.seconds) Stop("time_limit");
        }
        catch (Exception exception) { Error("Measurement: " + exception); Stop("measurement_error"); }
    }

    private static void Measure()
    {
        Transform player = _controller.Player;
        if (player == null || _controller.Run == null) return;
        CatRunState run = _controller.Run;
        Vector3 position = player.position;
        _report.allTransformsFinite &= Finite(position);
        if (_controller.Phase == CatGamePhase.Playing)
        {
            _report.heightSamples++;
            _report.maximumHeightError = Mathf.Max(_report.maximumHeightError, Mathf.Abs(position.y - .14f - run.Height));
            _report.maximumZError = Mathf.Max(_report.maximumZError, Mathf.Abs(position.z - run.Time * run.Song.speed));
            double expected = ExpectedSongTime();
            // Resume/revive intentionally hold the existing chart until a future DSP start.
            // Its raw extrapolated clock is earlier during that lead-in; it is not audio drift.
            if (AudioSettings.dspTime >= ScheduledStart())
                _report.maximumDspDrift = Math.Max(_report.maximumDspDrift, Math.Abs(run.Time - expected));
            else _report.scheduledPrerollFrames++;
            if (_controller.Music != null && _controller.Music.clip != null && _controller.Music.isPlaying)
            {
                double audio = (double)_controller.Music.timeSamples / _controller.Music.clip.frequency;
                // A scheduled source reports isPlaying before its scheduled start; compare only after it starts.
                if (AudioSettings.dspTime >= ScheduledStart() + .05)
                {
                    _report.audioSamples++;
                    _report.maximumAudioDrift = Math.Max(_report.maximumAudioDrift, Math.Abs(audio - run.Time));
                }
            }
        }
        foreach (TailBone bone in Tail)
        {
            if (bone.transform == null) continue;
            Vector3 local = bone.transform.localPosition;
            Quaternion rotation = bone.transform.localRotation;
            _report.allTransformsFinite &= Finite(local) && Finite(bone.transform.position) && Finite(rotation);
            _report.maximumTailLocalPositionError = Mathf.Max(_report.maximumTailLocalPositionError, Vector3.Distance(local, bone.localPosition));
            if (bone.localPosition.magnitude > .00001f)
                _report.maximumTailLengthRatio = Mathf.Max(_report.maximumTailLengthRatio, local.magnitude / bone.localPosition.magnitude);
            if (bone.lastFrame == Time.frameCount - 1 && Time.unscaledDeltaTime > .00001f)
            {
                float angle = Quaternion.Angle(rotation, bone.lastRotation);
                float speed = angle / Time.unscaledDeltaTime;
                if (speed > _report.maximumTailAngularSpeed)
                {
                    _report.maximumTailAngularSpeed = speed;
                    _report.worstTailBone = bone.transform.name;
                    _report.worstTailFrame = Time.frameCount;
                    _report.worstTailAngle = angle;
                    _report.worstTailDeltaTime = Time.unscaledDeltaTime;
                }
            }
            bone.lastRotation = rotation;
            bone.lastFrame = Time.frameCount;
        }
        if (_report.durationRealtime < _nextSample) return;
        _nextSample += .5;
        double source = _controller.Music != null && _controller.Music.clip != null
            ? (double)_controller.Music.timeSamples / _controller.Music.clip.frequency : 0;
        _report.samples.Add(new Snapshot
        {
            frame = Time.frameCount, phase = _controller.Phase.ToString(), realtime = _report.durationRealtime,
            songTime = run.Time, x = position.x, y = position.y, z = position.z, expectedY = .14f + run.Height,
            score = run.Score, landings = run.Landings, dspTime = AudioSettings.dspTime, expectedSongTime = ExpectedSongTime(),
            sourceTime = source, dspDrift = run.Time - ExpectedSongTime(), audioDrift = source - run.Time
        });
    }

    private static double ScheduledStart() => ScheduleField != null ? (double)ScheduleField.GetValue(_controller) : AudioSettings.dspTime;

    private static double ExpectedSongTime()
    {
        if (ScheduleField == null || OffsetField == null) return _controller.SongTime;
        float offset = (float)OffsetField.GetValue(_controller);
        float rate = RateField != null ? (float)RateField.GetValue(_controller) : 1;
        return Math.Max(0, offset + (AudioSettings.dspTime - ScheduledStart()) * rate - _controller.AudioLatencyMs / 1000);
    }

    private static void BindTail()
    {
        foreach (Transform bone in _controller.Player.GetComponentsInChildren<Transform>(true))
        {
            string name = bone.name.ToLowerInvariant();
            if (name != "tail" && !name.StartsWith("tail.") && !(name.StartsWith("bone") && bone.GetComponent<Rigidbody>() != null)) continue;
            if (name.Contains("end")) continue;
            Tail.Add(new TailBone { transform = bone, localPosition = bone.localPosition, lastRotation = bone.localRotation });
        }
        _report.tailBones = Tail.Count;
    }

    private static void OnLanding(int index)
    {
        CatRunState run = _controller.Run;
        if (!Landings.Add(_runNumber + ":" + index)) _report.duplicateLandings++;
        CatNote note = run.Song.notes[index];
        _report.landings.Add(new Landing
        {
            run = _runNumber, frame = Time.frameCount, index = index, kind = note.kind.ToString(), score = run.Score,
            combo = run.Combo, songTime = run.Time, x = run.X, lane = note.lane
        });
        _report.totalLandings++;
        if (note.kind == CatNoteKind.Short || note.kind == CatNoteKind.BigJump) _report.shortLandings++;
        else if (note.kind == CatNoteKind.Hold) _report.holdLandings++;
        else _report.slideLandings++;
    }

    private static void Click(string name)
    {
        UnityEngine.UI.Button button = FindButton(name, true);
        _report.buttonsInvoked.Add(name);
        button.onClick.Invoke();
    }

    private static UnityEngine.UI.Button FindButton(string name, bool requireInteractable)
    {
        foreach (UnityEngine.UI.Button button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include))
        {
            if (button.name != name || !button.gameObject.activeInHierarchy || (requireInteractable && !button.interactable)) continue;
            return button;
        }
        throw new InvalidOperationException("Real active UI button is unavailable: " + name);
    }

    private static void Capture(string name)
    {
        if (string.IsNullOrEmpty(_config.captureFolder)) return;
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            if (!_report.warnings.Contains("Screenshots skipped: null graphics device.")) _report.warnings.Add("Screenshots skipped: null graphics device.");
            return;
        }
        Camera camera = Camera.main;
        Canvas[] canvases = _controller.GetComponentsInChildren<Canvas>(true);
        if (camera == null || canvases.Length == 0) { _report.warnings.Add("Screenshot camera or Canvas missing: " + name); return; }
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        var modes = new RenderMode[canvases.Length];
        var oldCameras = new Camera[canvases.Length];
        var distances = new float[canvases.Length];
        var orders = new int[canvases.Length];
        var overrides = new bool[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            modes[i] = canvases[i].renderMode;
            oldCameras[i] = canvases[i].worldCamera;
            distances[i] = canvases[i].planeDistance;
            orders[i] = canvases[i].sortingOrder;
            overrides[i] = canvases[i].overrideSorting;
        }
        var target = new RenderTexture(540, 960, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(540, 960, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = target;
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            texture.Apply();
            Directory.CreateDirectory(_config.captureFolder);
            string path = Path.Combine(_config.captureFolder, name + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            _report.screenshots.Add(path);
        }
        catch (Exception exception) { _report.warnings.Add("Screenshot " + name + ": " + exception.Message); }
        finally
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = modes[i];
                canvases[i].worldCamera = oldCameras[i];
                canvases[i].planeDistance = distances[i];
                canvases[i].sortingOrder = orders[i];
                canvases[i].overrideSorting = overrides[i];
            }
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(target);
            Canvas.ForceUpdateCanvases();
        }
    }

    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Session + "Active", false) || SessionState.GetBool(Session + "Stopping", false)) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Error(type + ": " + message + "\n" + stack);
    }

    private static void Error(string message)
    {
        if (_report == null) return;
        _report.runtimeErrors++;
        if (_report.errors.Count < 60) _report.errors.Add(message);
    }

    private static void Watchdog()
    {
        if (!SessionState.GetBool(Session + "Active", false) || SessionState.GetBool(Session + "Stopping", false)) return;
        if (_config == null || _report == null) RestoreSession();
        if (EditorApplication.timeSinceStartup - _editorStartedAt < Math.Max(180, _config.seconds * 3 + 60)) return;
        Error("Editor watchdog timed out before a test outcome.");
        Stop("watchdog");
    }

    private static void Stop(string reason)
    {
        if (_stopping || SessionState.GetBool(Session + "Stopping", false)) return;
        _stopping = true;
        _report.stopReason = reason;
        _report.finishedUtc = DateTime.UtcNow.ToString("O");
        _report.finalPhase = _controller != null ? _controller.Phase.ToString() : "Unavailable";
        _report.actualAverageFps = _report.durationRealtime > 0 ? _report.frames / _report.durationRealtime : 0;
        if (_report.runtimeErrors > 0) _report.failedCriteria.Add("Runtime or harness errors occurred.");
        if (!_report.gameStarted && !_report.extrasStarted) _report.failedCriteria.Add("The real UI start flow was not reached.");
        if (!_report.allTransformsFinite) _report.failedCriteria.Add("A player/tail transform contained non-finite values.");
        if (_config.mode != "extras" && (_report.heightSamples == 0 || _report.maximumHeightError > .001f || _report.maximumZError > .001f))
            _report.failedCriteria.Add("The real player height or forward position disagreed with its run state.");
        if (_report.maximumTailLocalPositionError > .001f) _report.failedCriteria.Add("Tail local bone positions changed length.");
        if (_report.maximumTailAngularSpeed > 800) _report.failedCriteria.Add("Tail inter-frame rotation exceeded 800 degrees per second.");
        if (_report.duplicateLandings > 0) _report.failedCriteria.Add("A note was scored more than once in the same run.");
        if (_report.maximumDspDrift > .08) _report.failedCriteria.Add("DSP clock and observed song time diverged by more than 80 ms.");
        if (_config.mode == "extras")
        {
            if (!_report.dailyChecked || !_report.dailyClaimOnce || !_report.skinPurchaseChecked || !_report.skinAnimatorChecked ||
                !_report.volumeChecked || !_report.challengeOpened || !_report.roomUpgraded || !_report.trailChecked ||
                !_report.diceChecked || !_report.modeChecked || !_report.difficultyChecked || !_report.latencyChecked ||
                !_report.vibrationChecked || !_report.fpsChecked || !_report.songUnlockChecked)
                _report.failedCriteria.Add("One or more real offline progression/settings UI scenarios failed.");
            if (reason != "extras_complete") _report.failedCriteria.Add("Offline extras flow did not reach its final unlock/start check.");
        }
        else if (_config.mode == "miss")
        {
            if (!_report.failureObserved || !_report.intentionalMissApplied || _report.totalLandings < 5)
                _report.failedCriteria.Add("The intentional missed-input negative scenario did not produce a failure after five landings.");
        }
        else if (_config.mode == "endless")
        {
            if (_report.failureObserved || _report.endlessLaps < 2 || !_report.scoreMonotonic)
                _report.failedCriteria.Add("Two endless laps with monotonically increasing score were not observed.");
        }
        else
        {
            if (!_report.completed || _report.completedLandings != _report.totalNotes || _report.completedScore != _report.expectedScore)
                _report.failedCriteria.Add("The primary song did not complete with every landing and hold accent scored once.");
            if (_config.mode == "autoplay" && _report.failureObserved) _report.failedCriteria.Add("Autoplay unexpectedly failed.");
            if (_config.mode == "flow" && (!_report.pauseChecked || !_report.pauseClockFrozen || !_report.resumeChecked ||
                !_report.failureObserved || !_report.reviveChecked || !_report.restartChecked || !_report.homeChecked))
                _report.failedCriteria.Add("Pause, resume, miss, revive, result, retry or home flow was not verified.");
        }
        if (reason == "time_limit" || reason == "watchdog" || reason.EndsWith("error") || reason == "play_stopped_early")
            _report.failedCriteria.Add("The run stopped before its expected scenario outcome.");
        _report.passed = _report.failedCriteria.Count == 0;
        SessionState.SetString(Session + "Report", JsonUtility.ToJson(_report));
        SessionState.SetBool(Session + "Stopping", true);
        EditorApplication.delayCall += () =>
        {
            RemoveLoop();
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            else Finish();
        };
    }

    private static void Finish()
    {
        if (!SessionState.GetBool(Session + "Active", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        RestoreSession();
        SessionState.SetBool(Session + "Active", false);
        SessionState.SetBool(Session + "Stopping", false);
        int code = _report.passed ? 0 : 1;
        try
        {
            RestorePreferences(_preferences);
            _report.preferencesRestored = true;
            File.WriteAllText(_config.report, JsonUtility.ToJson(_report, true));
            Debug.Log("Cat real-scene validation " + (_report.passed ? "passed" : "failed") + ": " + _config.report);
        }
        catch (Exception exception) { Debug.LogException(exception); code = 2; }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    private static void RemoveLoop()
    {
        if (_loopInstalled) PlayerLoop.SetPlayerLoop(_originalLoop);
        _loopInstalled = false;
        if (_mouse != null && _mouse.added)
        {
            QueueMouse(false, Screen.width * .5f);
            if (_ownedMouse) InputSystem.RemoveDevice(_mouse);
        }
    }

    private static PlayerLoopSystem CopyLoop(PlayerLoopSystem source)
    {
        if (source.subSystemList == null) return source;
        var list = new PlayerLoopSystem[source.subSystemList.Length];
        for (int i = 0; i < list.Length; i++) list[i] = CopyLoop(source.subSystemList[i]);
        source.subSystemList = list;
        return source;
    }

    private static bool Insert(ref PlayerLoopSystem parent, Type stage, PlayerLoopSystem addition, bool after)
    {
        if (parent.subSystemList == null) return false;
        for (int i = 0; i < parent.subSystemList.Length; i++)
        {
            if (parent.subSystemList[i].type == stage)
            {
                var list = new List<PlayerLoopSystem>(parent.subSystemList);
                list.Insert(i + (after ? 1 : 0), addition);
                parent.subSystemList = list.ToArray();
                return true;
            }
            PlayerLoopSystem child = parent.subSystemList[i];
            if (!Insert(ref child, stage, addition, after)) continue;
            parent.subSystemList[i] = child;
            return true;
        }
        return false;
    }
}
