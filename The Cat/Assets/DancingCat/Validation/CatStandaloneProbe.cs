#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DancingCat.Validation
{
    // Opt-in development-player verification; clicks and steering use device events.
    [DefaultExecutionOrder(-1000)]
    public sealed class CatStandaloneProbe : MonoBehaviour
    {
        private const string Prefix = "DancingCat.v2.";
        private string _reportPath;
        private double _startedAt;
        private bool _finished, _steering;
        private Mouse _mouse, _previousMouse;
        private CatGameController _game;
        private float _anchorLane;
        private Vector2 _anchorPointer;
        private readonly List<Pref> _preferences = new List<Pref>();
        private readonly Result _result = new Result();
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo Schedule = typeof(CatGameController).GetField("_scheduledStart", Private);
        private static readonly FieldInfo Offset = typeof(CatGameController).GetField("_clockOffset", Private);
        private static readonly FieldInfo Rate = typeof(CatGameController).GetField("_rate", Private);

        [Serializable] private sealed class Pref
        {
            public string key, kind, text;
            public bool existed;
            public int integer;
            public float number;
        }
        [Serializable] private sealed class PrefBackup { public List<Pref> values; }
        [Serializable] private sealed class ClickResult
        {
            public string button, topRaycast, resultingPhase;
            public float x, y;
            public bool raycastMatched, phaseMatched;
        }
        [Serializable] private sealed class CaptureResult
        {
            public string name, path;
            public int width, height, samples;
            public float nonblackFraction, luminanceVariance;
            public bool nonblack;
        }
        [Serializable] private sealed class Result
        {
            public bool passed, countdownChecked, playingChecked, preferencesRestored;
            public string scene, phase, stopReason, exception;
            public int songs, buttons, errors, width, height, landings;
            public float songTime;
            public double elapsedSeconds;
            public string screenshot, menuScreenshot, gameScreenshot;
            public string captureMethod = "offscreen camera RenderTexture with ScreenSpaceCamera canvases";
            public List<string> messages = new List<string>();
            public List<ClickResult> clicks = new List<ClickResult>();
            public List<CaptureResult> captures = new List<CaptureResult>();
        }

        // Capture before controller Awake/Start write option defaults.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-catSmokeReport")
                {
                    var runner = new GameObject("DevelopmentPointerProbe").AddComponent<CatStandaloneProbe>();
                    DontDestroyOnLoad(runner.gameObject);
                    runner._reportPath = Path.GetFullPath(args[i + 1]);
                    runner._startedAt = Time.realtimeSinceStartupAsDouble;
                    runner.BackupPreferences();
                    Directory.CreateDirectory(Path.GetDirectoryName(runner._reportPath));
                    File.WriteAllText(runner._reportPath + ".prefs-backup.json",
                        JsonUtility.ToJson(new PrefBackup { values = runner._preferences }, true));
                    return;
                }
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string trace, LogType type)
        {
            if (_finished || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            _result.errors++;
            if (_result.messages.Count < 20) _result.messages.Add(type + ": " + message + "\n" + trace);
        }

        private void Update()
        {
            if (_finished || string.IsNullOrEmpty(_reportPath)) return;
            if (Time.realtimeSinceStartupAsDouble - _startedAt > 35) Finish("timeout");
            else if (_result.errors > 0) Finish("runtime_error");
        }

        private IEnumerator Start() => Guard(Run());

        // Handle nested iterator exceptions so UI/capture failures always produce JSON.
        private IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (!_finished && stack.Count > 0)
            {
                object yielded = null;
                bool moved = false;
                try { moved = stack.Peek().MoveNext(); if (moved) yielded = stack.Peek().Current; }
                catch (Exception exception) { _result.exception = exception.ToString(); Finish("probe_error"); }
                if (_finished) yield break;
                if (!moved) { stack.Pop(); continue; }
                if (yielded is IEnumerator nested) stack.Push(nested);
                else yield return yielded;
            }
        }

        private IEnumerator Run()
        {
            Application.runInBackground = true;
            for (int i = 0; i < 45; i++) yield return null;
            _game = UnityEngine.Object.FindAnyObjectByType<CatGameController>();
            Require(_game != null && _game.Phase == CatGamePhase.MainMenu, "The new game's main menu is unavailable.");
            Require(_game.Songs != null && _game.Songs.Length == 5, "Expected five preview songs.");
            Require(EventSystem.current != null && EventSystem.current.currentInputModule is InputSystemUIInputModule,
                "The active EventSystem is not using InputSystemUIInputModule.");
            foreach (CatSong song in _game.Songs)
            {
                AddPref("Best." + song.id, "int");
                AddPref("Stars." + song.id, "int");
            }
            _previousMouse = Mouse.current;
            _mouse = InputSystem.AddDevice<Mouse>();
            _mouse.MakeCurrent();
            yield return Capture("menu");
            yield return Click("SongPlay_0", CatGamePhase.Countdown);
            _result.countdownChecked = true;
            yield return Click("BeginRun", CatGamePhase.Playing);
            _result.playingChecked = true;
            Require(Schedule != null && Offset != null && Rate != null, "The DSP steering clock is unavailable.");
            _anchorLane = _game.Run.X;
            _anchorPointer = new Vector2(Screen.width * .5f, Screen.height * .43f);
            _steering = true;
            InputSystem.onBeforeUpdate += SteerBeforeInput;
            while (_game.SongTime < 3)
            {
                Require(_game.Phase == CatGamePhase.Playing, "The short pointer-driven run unexpectedly left Playing.");
                yield return null;
            }
            Require(_game.Phase == CatGamePhase.Playing && _game.Run.Landings >= 3,
                "Pointer steering did not sustain the early gameplay notes.");
            yield return Capture("game");
            Finish("pointer_smoke_complete");
        }

        private IEnumerator Click(string name, CatGamePhase expected)
        {
            Canvas.ForceUpdateCanvases();
            Button target = null;
            foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude))
                if (button.name == name && button.IsActive() && button.IsInteractable()) { target = button; break; }
            Require(target != null, "The active UI button is unavailable: " + name);
            var rect = (RectTransform)target.transform;
            var canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            Require(point.x >= 0 && point.x < Screen.width && point.y >= 0 && point.y < Screen.height,
                "Button center is outside the player window: " + name);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Button hitButton = hits.Count > 0 ? hits[0].gameObject.GetComponentInParent<Button>() : null;
            var click = new ClickResult { button = name, x = point.x, y = point.y,
                topRaycast = hits.Count > 0 ? hits[0].gameObject.name : "none", raycastMatched = hitButton == target };
            _result.clicks.Add(click);
            Require(click.raycastMatched, "Button center is occluded or excluded from raycasting: " + name);
            QueueMouse(false, point);
            yield return null;
            yield return null;
            QueueMouse(true, point);
            yield return null;
            yield return null;
            QueueMouse(false, point);
            double deadline = Time.realtimeSinceStartupAsDouble + 2;
            while (_game.Phase != expected && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            click.resultingPhase = _game.Phase.ToString();
            click.phaseMatched = _game.Phase == expected;
            Require(click.phaseMatched, "Pointer click did not reach " + expected + ": " + name);
            yield return null;
        }

        private void QueueMouse(bool held, Vector2 position)
        {
            _mouse.MakeCurrent();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position, buttons = (ushort)(held ? 1 : 0) });
        }

        private void SteerBeforeInput()
        {
            if (!_steering || _finished || _game == null || _game.Phase != CatGamePhase.Playing) return;
            try
            {
                float offset = (float)Offset.GetValue(_game), rate = (float)Rate.GetValue(_game);
                double scheduled = (double)Schedule.GetValue(_game);
                float time = Mathf.Max(0, offset + (float)(AudioSettings.dspTime - scheduled) * rate - _game.AudioLatencyMs / 1000f);
                float lane = DesiredLane(_game.SelectedSong, time);
                QueueMouse(true, _anchorPointer + Vector2.right * ((lane - _anchorLane) / 5.5f * Screen.width));
            }
            catch (Exception exception) { _result.exception = exception.ToString(); Finish("steering_error"); }
        }

        private static float DesiredLane(CatSong song, float time)
        {
            int current = 0;
            while (current + 1 < song.notes.Count && song.notes[current + 1].time <= time) current++;
            CatNote note = song.notes[current];
            float departure = note.time + note.duration;
            float end = note.kind == CatNoteKind.Slide ? note.endLane : note.lane;
            if (time <= departure)
            {
                if (note.kind != CatNoteKind.Slide || note.duration <= 0) return note.lane;
                float elapsed = time - note.time, previousTime = 0, previousLane = note.lane;
                foreach (var point in note.path)
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

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(Path.GetDirectoryName(_reportPath), Path.GetFileNameWithoutExtension(_reportPath) + "-" + name + ".png");
            Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null,
                "Offscreen capture needs a graphics device: " + name);
            Camera camera = Camera.main;
            Canvas[] canvases = _game.GetComponentsInChildren<Canvas>(true);
            Require(camera != null && canvases.Length > 0, "Capture camera or UI canvases are missing: " + name);
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
            int width = Mathf.Max(16, Screen.width), height = Mathf.Max(16, Screen.height);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Require(target.Create(), "RenderTexture creation failed: " + name);
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
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                var capture = InspectPixels(name, path, texture);
                _result.captures.Add(capture);
                Require(capture.nonblack, "Capture is black or lacks scene/UI detail: " + name +
                    ", nonblack=" + capture.nonblackFraction + ", variance=" + capture.luminanceVariance);
            }
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
                target.Release();
                Destroy(target);
                Destroy(texture);
                Canvas.ForceUpdateCanvases();
            }
            if (name == "menu") { _result.menuScreenshot = path; _result.screenshot = path; }
            else _result.gameScreenshot = path;
        }

        private static CaptureResult InspectPixels(string name, string path, Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int step = Mathf.Max(1, pixels.Length / 4096), samples = 0, nonblack = 0;
            double sum = 0, squares = 0;
            for (int i = 0; i < pixels.Length; i += step)
            {
                Color32 pixel = pixels[i];
                if (Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) >= 16) nonblack++;
                double luminance = pixel.r * .2126 + pixel.g * .7152 + pixel.b * .0722;
                samples++; sum += luminance; squares += luminance * luminance;
            }
            float fraction = samples > 0 ? (float)nonblack / samples : 0;
            float variance = samples > 0 ? (float)Math.Max(0, squares / samples - (sum / samples) * (sum / samples)) : 0;
            return new CaptureResult { name = name, path = path, width = texture.width, height = texture.height,
                samples = samples, nonblackFraction = fraction, luminanceVariance = variance,
                nonblack = fraction >= .02f && variance >= 9 };
        }

        private void BackupPreferences()
        {
            foreach (string key in new[] { "Coins", "Difficulty", "Endless", "Vibration", "TargetFps", "Skin" }) AddPref(key, "int");
            foreach (string key in new[] { "LatencyMs", "Volume" }) AddPref(key, "float");
            AddPref("Meta", "string"); AddPref("Daily", "string");
            for (int i = 0; i < 5; i++)
            {
                AddPref("Owned." + i, "int"); AddPref("UnlockedSong." + i, "int");
                AddPref("Best.session-" + (i + 1), "int"); AddPref("Stars.session-" + (i + 1), "int");
            }
        }

        private void AddPref(string suffix, string kind)
        {
            string key = Prefix + suffix;
            if (_preferences.Exists(value => value.key == key)) return;
            _preferences.Add(new Pref { key = key, kind = kind, existed = PlayerPrefs.HasKey(key),
                integer = kind == "int" ? PlayerPrefs.GetInt(key) : 0,
                number = kind == "float" ? PlayerPrefs.GetFloat(key) : 0,
                text = kind == "string" ? PlayerPrefs.GetString(key) : "" });
        }

        private void RestorePreferences()
        {
            foreach (Pref value in _preferences)
            {
                if (!value.existed) PlayerPrefs.DeleteKey(value.key);
                else if (value.kind == "int") PlayerPrefs.SetInt(value.key, value.integer);
                else if (value.kind == "float") PlayerPrefs.SetFloat(value.key, value.number);
                else PlayerPrefs.SetString(value.key, value.text);
            }
            PlayerPrefs.Save();
            foreach (Pref value in _preferences)
                Require(PlayerPrefs.HasKey(value.key) == value.existed && (!value.existed ||
                    (value.kind == "int" ? PlayerPrefs.GetInt(value.key) == value.integer :
                     value.kind == "float" ? PlayerPrefs.GetFloat(value.key).Equals(value.number) :
                     PlayerPrefs.GetString(value.key) == value.text)), "Preference restoration failed: " + value.key);
            _result.preferencesRestored = true;
        }

        private void Finish(string reason)
        {
            if (_finished) return;
            _finished = true; _steering = false;
            InputSystem.onBeforeUpdate -= SteerBeforeInput;
            _result.stopReason = reason;
            _result.scene = SceneManager.GetActiveScene().path;
            _result.songs = _game != null ? _game.Songs.Length : 0;
            _result.buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Length;
            _result.width = Screen.width; _result.height = Screen.height;
            _result.phase = _game != null ? _game.Phase.ToString() : "Missing";
            _result.songTime = _game != null ? _game.SongTime : 0;
            _result.landings = _game != null && _game.Run != null ? _game.Run.Landings : 0;
            _result.elapsedSeconds = Time.realtimeSinceStartupAsDouble - _startedAt;
            // No finish/failure/claim/revive/purchase/option action is requested. Restore early
            // collectible coins and startup default writes before quitting.
            if (_game != null)
            {
                _game.enabled = false;
                var meta = _game.GetComponent<CatMetaProgress>();
                if (meta != null) meta.enabled = false;
                if (_game.Music != null) _game.Music.Stop();
            }
            try { RestorePreferences(); }
            catch (Exception exception) { _result.errors++; _result.exception += "\n" + exception; }
            try
            {
                if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
                if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
            }
            catch (Exception exception) { _result.errors++; _result.exception += "\n" + exception; }
            _result.passed = reason == "pointer_smoke_complete" && _result.errors == 0 &&
                _result.countdownChecked && _result.playingChecked && _result.preferencesRestored &&
                !string.IsNullOrEmpty(_result.menuScreenshot) && !string.IsNullOrEmpty(_result.gameScreenshot);
            try { File.WriteAllText(_reportPath, JsonUtility.ToJson(_result, true)); }
            catch (Exception exception) { Debug.LogException(exception); _result.passed = false; }
            Application.Quit(_result.passed ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif

