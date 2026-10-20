using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DancingCat
{
    public sealed class CatGameController : MonoBehaviour
    {
        public CatSong[] Songs = Array.Empty<CatSong>();
        public GameObject[] CatPrefabs = Array.Empty<GameObject>();
        public RuntimeAnimatorController CatAnimator;
        public RuntimeAnimatorController GingerAnimator;
        public event Action Changed;
        public CatSong SelectedSong { get; private set; }
        public CatGamePhase Phase { get; private set; } = CatGamePhase.MainMenu;
        public CatRunState Run { get; private set; }
        public int Score => _lapScore + (Run?.Score ?? 0);
        public int Combo => Run?.Combo ?? 0;
        public int BestCombo => Mathf.Max(_lapBest, Run?.BestCombo ?? 0);
        public int Coins { get; private set; }
        public int Stars => Phase == CatGamePhase.Complete || _lap > 0 ? 3 : Mathf.Clamp(Mathf.FloorToInt(Progress * 3), 0, 2);
        public int SelectedSkin { get; private set; }
        public int EarnedCoins { get; private set; }
        public int ReviveCost => 25;
        public float ReviveRemaining => Mathf.Max(0, 5 - (Time.unscaledTime - _failedAt));
        public bool CanRevive => Phase == CatGamePhase.Failed && !_revived && Coins >= ReviveCost && ReviveRemaining > 0;
        public float Progress => Phase == CatGamePhase.Complete ? 1 : SelectedSong == null ? 0 : Mathf.Clamp01(SongTime / SelectedSong.duration);
        public float SongTime => Run?.Time ?? 0;
        public float MusicVolume { get; private set; }
        public int Difficulty { get => _difficulty; set { _difficulty = Mathf.Clamp(value, 0, 2); PlayerPrefs.SetInt(Key + "Difficulty", _difficulty); Changed?.Invoke(); } }
        public bool Endless { get => _endless; set { _endless = value; PlayerPrefs.SetInt(Key + "Endless", value ? 1 : 0); Changed?.Invoke(); } }
        public float AudioLatencyMs { get => _latency; set { _latency = Mathf.Clamp(value, -200, 200); PlayerPrefs.SetFloat(Key + "LatencyMs", _latency); } }
        public bool Vibration { get => _vibration; set { _vibration = value; PlayerPrefs.SetInt(Key + "Vibration", value ? 1 : 0); } }
        public int TargetFps { get => _targetFps; set { _targetFps = value <= 30 ? 30 : value <= 60 ? 60 : 120; Application.targetFrameRate = _targetFps; PlayerPrefs.SetInt(Key + "TargetFps", _targetFps); } }
        public string Feedback => Run?.Feedback ?? "";
        public bool CanClaimDaily => GetComponent<CatMetaProgress>() != null ? GetComponent<CatMetaProgress>().CanClaimDaily : PlayerPrefs.GetString(Key + "Daily", "") != DateTime.UtcNow.ToString("yyyy-MM-dd");
        public int DailyReward => GetComponent<CatMetaProgress>() != null ? GetComponent<CatMetaProgress>().NextDailyReward : 50;
        public int DailyStreak => GetComponent<CatMetaProgress>() != null ? GetComponent<CatMetaProgress>().DailyStreak : 0;
        public string[] SkinNames { get; } = new[] { "GINGER", "MOCHI", "MIDNIGHT", "CALICO", "SILVER" };
        public Color[] SkinColors { get; } = new[] { new Color(1, .66f, .27f), new Color(1, .91f, .83f), new Color(.25f, .24f, .4f), new Color(.92f, .61f, .45f), new Color(.65f, .76f, .84f) };
        public int[] SkinPrices { get; } = new[] { 0, 80, 150, 220, 300 };
        public Transform Player => _player;
        public AudioSource Music => _music;
        private const string Key = "DancingCat.v2.";
        private readonly List<Transform> _tiles = new List<Transform>();
        private readonly List<Transform> _coins = new List<Transform>();
        private readonly List<Material> _materials = new List<Material>();
        private Transform _world;
        private Transform _player;
        private Transform _visual;
        private Camera _camera;
        private AudioSource _music;
        private CatVisualMotion _motion;
        private Material _tileMaterial, _holdMaterial, _slideMaterial, _hitMaterial, _goldMaterial;
        private double _scheduledStart;
        private float _clockOffset;
        private float _pauseTime;
        private float _x;
        private float _pointerStart, _dragStartX;
        private bool _dragging, _revived;
        private int _paidReward;
        private bool _endless, _vibration;
        private int _difficulty, _targetFps = 60;
        private float _latency, _rate = 1;
        private int _lap, _lapScore, _lapBest;
        private TrailRenderer _trail;
        private int _songIndex, _lastCoinCount;
        private float _failedAt;

        private void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Coins = PlayerPrefs.GetInt(Key + "Coins", 100);
            SelectedSkin = Mathf.Clamp(PlayerPrefs.GetInt(Key + "Skin", 0), 0, SkinNames.Length - 1);
            MusicVolume = PlayerPrefs.GetFloat(Key + "Volume", .8f);
            _difficulty = Mathf.Clamp(PlayerPrefs.GetInt(Key + "Difficulty", 0), 0, 2);
            _endless = PlayerPrefs.GetInt(Key + "Endless", 0) == 1;
            _latency = PlayerPrefs.GetFloat(Key + "LatencyMs", 0);
            _vibration = PlayerPrefs.GetInt(Key + "Vibration", 0) == 1;
            TargetFps = PlayerPrefs.GetInt(Key + "TargetFps", 60);
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = false;
            _camera = Camera.main;
            _world = new GameObject("RhythmWorld").transform;
            _player = new GameObject("CatPlayer").transform;
            _visual = new GameObject("CatVisual").transform;
            _visual.SetParent(_player, false);
            _tileMaterial = MakeMaterial(new Color(.32f, .91f, .99f));
            _holdMaterial = MakeMaterial(new Color(.99f, .52f, .79f));
            _slideMaterial = MakeMaterial(new Color(.65f, .48f, 1));
            _hitMaterial = MakeMaterial(new Color(1, .83f, .32f));
            _goldMaterial = MakeMaterial(new Color(1, .76f, .16f));
            RefreshCat();
            Home();
        }

        private Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            material.SetFloat("_Smoothness", .3f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * .15f);
            _materials.Add(material);
            return material;
        }

        public void PlaySong(int index)
        {
            if (index < 0 || index >= Songs.Length || Songs[index] == null) return;
            if (!IsSongUnlocked(index)) { UnlockSong(index); return; }
            _songIndex = index;
            SelectedSong = Songs[index];
            _music.Stop();
            Run = new CatRunState(SelectedSong);
            Run.Landed += OnLanded;
            ResetSteering(SelectedSong.notes.Count > 0 ? SelectedSong.notes[0].lane : 0);
            _revived = false;
            _paidReward = 0;
            _lastCoinCount = 0;
            _lap = _lapScore = _lapBest = 0;
            _rate = 1 + _difficulty * .12f;
            EarnedCoins = 0;
            _dragging = false;
            if (_trail != null) _trail.Clear();
            BuildTrack();
            SetPlayer(0, 0);
            Phase = CatGamePhase.Countdown;
            Changed?.Invoke();
        }

        public void BeginRun()
        {
            if (Phase != CatGamePhase.Countdown) return;
            if (SelectedSong == null || SelectedSong.clip == null || SelectedSong.notes.Count == 0)
                throw new InvalidOperationException("A song needs audio and a chart before playing.");
            ScheduleAt(0, true);
            Phase = CatGamePhase.Playing;
            Run.Advance(0, _x);
            Changed?.Invoke();
        }

        private void ScheduleAt(float time, bool fromStart = false)
        {
            _music.Stop();
            _music.clip = SelectedSong.clip;
            _music.volume = MusicVolume;
            _music.pitch = _rate;
            float audioTime = fromStart ? 0 : Mathf.Max(0, time + _latency / 1000f);
            _music.timeSamples = Mathf.Clamp(Mathf.RoundToInt(audioTime * _music.clip.frequency), 0, _music.clip.samples - 1);
            _clockOffset = audioTime;
            _scheduledStart = AudioSettings.dspTime + .25;
            _music.PlayScheduled(_scheduledStart);
        }

        public void RestartSong() => PlaySong(_songIndex);

        public void Home()
        {
            _music.Stop();
            Phase = CatGamePhase.MainMenu;
            Run = null;
            SelectedSong = null;
            ClearTrack();
            _x = 0;
            _lapScore = _lapBest = _lap = 0;
            if (_trail != null) _trail.Clear();
            _player.position = Vector3.zero;
            if (_motion != null) _motion.Present(null, 0);
            if (_camera != null)
            {
                _camera.transform.position = new Vector3(0, 3.5f, -6);
                _camera.transform.LookAt(new Vector3(0, -.85f, 0));
                _camera.backgroundColor = new Color(.19f, .08f, .38f);
            }
            Changed?.Invoke();
        }

        public void Pause()
        {
            if (Phase != CatGamePhase.Playing) return;
            _pauseTime = SongTime;
            _music.Pause();
            Phase = CatGamePhase.Paused;
            _dragging = false;
            Changed?.Invoke();
        }

        public void Resume()
        {
            if (Phase != CatGamePhase.Paused) return;
            ScheduleAt(_pauseTime);
            Phase = CatGamePhase.Playing;
            Changed?.Invoke();
        }

        public void Revive()
        {
            if (Phase != CatGamePhase.Failed || !CanRevive) return;
            Coins -= ReviveCost;
            SaveCoins();
            _revived = true;
            Run.Revive();
            _x = Run.X;
            _dragging = false;
            ScheduleAt(Run.Time);
            SetPlayer(Run.Time, 0);
            Phase = CatGamePhase.Playing;
            Changed?.Invoke();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (Phase == CatGamePhase.Playing) Pause();
                else if (Phase == CatGamePhase.Paused) Resume();
                else Home();
            }
            if (Phase == CatGamePhase.Playing)
            {
                ReadSteering();
                float songTime = Mathf.Max(0, _clockOffset + (float)(AudioSettings.dspTime - _scheduledStart) * _rate - _latency / 1000f);
                Run.Advance(songTime, _x);
                if (Run.Failed) _x = Run.X;
                SetPlayer(Run.Time, Run.Height);
                CollectCoins();
                if (Run.Failed)
                {
#if UNITY_ANDROID || UNITY_IOS
                    if (_vibration) Handheld.Vibrate();
#endif
                    _failedAt = Time.unscaledTime;
                    _music.Stop();
                    Phase = CatGamePhase.Failed;
                    PayReward(false);
                    Changed?.Invoke();
                }
                else if (Run.Finished)
                {
                    if (_endless)
                    {
                        if (Score > GetBestScore(SelectedSong)) PlayerPrefs.SetInt(Key + "Best." + SelectedSong.id, Score);
                        PlayerPrefs.SetInt(Key + "Stars." + SelectedSong.id, 3);
                        PlayerPrefs.Save();
                        _lap++;
                        _lapScore += Run.Score;
                        _lapBest = Mathf.Max(_lapBest, Run.BestCombo);
                        _rate = Mathf.Min(1.75f, _rate + .06f);
                        Run = new CatRunState(SelectedSong);
                        Run.Landed += OnLanded;
                        _lastCoinCount = 0;
                        ResetSteering(SelectedSong.notes[0].lane);
                        BuildTrack();
                        ScheduleAt(0, true);
                        Run.Advance(0, _x);
                        SetPlayer(0, 0);
                        Changed?.Invoke();
                        return;
                    }
                    _music.Stop();
                    Phase = CatGamePhase.Complete;
                    PayReward(true);
                    Changed?.Invoke();
                }
            }
            else if (Phase == CatGamePhase.Failed)
            {
                Vector3 position = _player.position;
                position.y = -Mathf.Min(4, (Time.unscaledTime - _failedAt) * 2.5f);
                _player.position = position;
            }
            UpdateTrack();
        }

        private void ResetSteering(float lane)
        {
            _x = lane;
            _dragging = false;
        }

        private void ReadSteering()
        {
            bool held = false;
            float position = 0;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                held = true;
                position = Touchscreen.current.primaryTouch.position.ReadValue().x;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                held = true;
                position = Mouse.current.position.ReadValue().x;
            }
            if (held)
            {
                if (!_dragging) { _pointerStart = position; _dragStartX = _x; }
                _x = _dragStartX + (position - _pointerStart) / Mathf.Max(1, Screen.width) * 5.5f;
            }
            _dragging = held;
            if (Keyboard.current != null)
            {
                float axis = (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed ? 1 : 0)
                    - (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed ? 1 : 0);
                _x += axis * Time.unscaledDeltaTime * 4;
            }
            _x = Mathf.Clamp(_x, -1.7f, 1.7f);
        }

        private void SetPlayer(float time, float height)
        {
            _player.position = new Vector3(_x, height + .14f, time * SelectedSong.speed);
            if (_motion != null) _motion.Present(Run, _x);
            if (_camera != null)
            {
                var position = new Vector3(_x * .13f, 5.4f, _player.position.z - 7.8f);
                _camera.transform.position = position;
                _camera.transform.LookAt(new Vector3(0, .2f, _player.position.z + 6));
                _camera.backgroundColor = Color.Lerp(SelectedSong.theme * .25f, SelectedSong.theme * .55f, .5f + .5f * Mathf.Sin(time * .15f));
            }
        }

        private void BuildTrack()
        {
            ClearTrack();
            for (int i = 0; i < SelectedSong.notes.Count; i++)
            {
                CatNote note = SelectedSong.notes[i];
                var tile = new GameObject();
                tile.name = $"Tile_{i:D3}_{note.kind}";
                tile.transform.SetParent(_world, false);
                Vector3 start = new Vector3(note.lane, -.12f, note.time * SelectedSong.speed);
                Vector3 end = new Vector3(note.kind == CatNoteKind.Slide ? note.endLane : note.lane, -.12f, (note.time + note.duration) * SelectedSong.speed);
                tile.transform.position = start;
                var material = note.kind == CatNoteKind.Hold ? _holdMaterial : note.kind == CatNoteKind.Slide ? _slideMaterial : _tileMaterial;
                Vector3 previous = start;
                if (note.kind == CatNoteKind.Slide && note.path != null)
                {
                    foreach (var point in note.path)
                    {
                        var next = new Vector3(point.lane, -.12f, (note.time + point.time) * SelectedSong.speed);
                        MakeTileSegment(tile.transform, previous, next, material);
                        previous = next;
                    }
                }
                MakeTileSegment(tile.transform, previous, end, material, note.kind == CatNoteKind.BigJump ? 1.3f : 1.04f);
                if (note.checkpoints != null)
                {
                    foreach (float accent in note.checkpoints)
                    {
                        float lane = NoteLane(note, accent);
                        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        marker.name = "HoldAccent";
                        marker.transform.SetParent(tile.transform, false);
                        marker.transform.position = new Vector3(lane, .025f, (note.time + accent) * SelectedSong.speed);
                        marker.transform.localScale = new Vector3(.2f, .04f, .08f);
                        marker.GetComponent<Renderer>().sharedMaterial = _goldMaterial;
                        Destroy(marker.GetComponent<Collider>());
                    }
                }
                _tiles.Add(tile.transform);
                Transform coin = null;
                if (note.coin)
                {
                    var pickup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pickup.name = "Coin_" + i;
                    Destroy(pickup.GetComponent<Collider>());
                    pickup.transform.SetParent(_world, false);
                    pickup.transform.position = start + Vector3.up * .62f;
                    pickup.transform.localScale = new Vector3(.22f, .045f, .22f);
                    pickup.transform.rotation = Quaternion.Euler(90, 0, 0);
                    pickup.GetComponent<Renderer>().sharedMaterial = _goldMaterial;
                    coin = pickup.transform;
                }
                _coins.Add(coin);
            }
            var finish = GameObject.CreatePrimitive(PrimitiveType.Cube);
            finish.name = "FinishPlatform";
            finish.transform.SetParent(_world, false);
            var lastNote = SelectedSong.notes[SelectedSong.notes.Count - 1];
            finish.transform.position = new Vector3(lastNote.endLane, -.25f,
                (lastNote.time + lastNote.duration) * SelectedSong.speed + 2);
            finish.transform.localScale = new Vector3(4.5f, .3f, 5);
            finish.GetComponent<Renderer>().sharedMaterial = _hitMaterial;
            Destroy(finish.GetComponent<Collider>());
        }

        private void UpdateTrack()
        {
            if (SelectedSong == null) return;
            float z = _player.position.z;
            for (int i = 0; i < _tiles.Count; i++)
            {
                Transform tile = _tiles[i];
                CatNote note = SelectedSong.notes[i];
                bool visible = (note.time + note.duration) * SelectedSong.speed >= z - 10 && note.time * SelectedSong.speed < z + 100;
                tile.gameObject.SetActive(visible);
                if (_coins[i] != null && _coins[i].gameObject.activeSelf)
                    _coins[i].Rotate(0, 0, 95 * Time.unscaledDeltaTime, Space.Self);
            }
        }

        private void OnLanded(int index)
        {
            if (index < _tiles.Count)
                foreach (Renderer renderer in _tiles[index].GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = _hitMaterial;
            if (index < _coins.Count && _coins[index] != null) _coins[index].gameObject.SetActive(false);
            if (_motion != null) _motion.Land();
        }

        private void CollectCoins()
        {
            if (Run.Coins <= _lastCoinCount) return;
            int amount = Run.Coins - _lastCoinCount;
            _lastCoinCount = Run.Coins;
            Coins += amount;
            EarnedCoins += amount;
            SaveCoins();
        }

        private void MakeTileSegment(Transform root, Vector3 start, Vector3 end, Material material, float width = 1.04f)
        {
            var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = "Surface";
            segment.transform.SetParent(root, false);
            segment.transform.position = (start + end) * .5f;
            float length = Vector3.Distance(start, end);
            if (length > .01f) segment.transform.rotation = Quaternion.LookRotation(end - start);
            segment.transform.localScale = new Vector3(width, .22f, length + .82f);
            segment.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(segment.GetComponent<Collider>());
        }

        private static float NoteLane(CatNote note, float elapsed)
        {
            if (note.kind != CatNoteKind.Slide) return note.lane;
            float time = 0, lane = note.lane;
            if (note.path != null)
                foreach (var point in note.path)
                {
                    if (elapsed <= point.time) return Mathf.Lerp(lane, point.lane, Mathf.InverseLerp(time, point.time, elapsed));
                    time = point.time; lane = point.lane;
                }
            return Mathf.Lerp(lane, note.endLane, Mathf.InverseLerp(time, note.duration, elapsed));
        }

        private void PayReward(bool completed)
        {
            int totalReward = completed ? 25 + BestCombo / 3 : Run.Landings / 5;
            int reward = Mathf.Max(0, totalReward - _paidReward);
            _paidReward += reward;
            Coins += reward; EarnedCoins += reward;
            if (Score > GetBestScore(SelectedSong)) PlayerPrefs.SetInt(Key + "Best." + SelectedSong.id, Score);
            int stars = completed ? 3 : Stars;
            if (stars > GetBestStars(SelectedSong)) PlayerPrefs.SetInt(Key + "Stars." + SelectedSong.id, stars);
            SaveCoins();
        }

        public int GetBestScore(CatSong song) => PlayerPrefs.GetInt(Key + "Best." + song.id, 0);
        public int GetBestStars(CatSong song) => PlayerPrefs.GetInt(Key + "Stars." + song.id, 0);
        public bool IsSongUnlocked(int index) => index < 3 || PlayerPrefs.GetInt(Key + "UnlockedSong." + index, 0) == 1;
        public int SongUnlockCost(int index) => 50 + index * 25;
        public void UnlockSong(int index)
        {
            if (IsSongUnlocked(index) || index < 0 || index >= Songs.Length || !SpendCoins(SongUnlockCost(index))) return;
            PlayerPrefs.SetInt(Key + "UnlockedSong." + index, 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
        public bool SpendCoins(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            Coins -= amount; SaveCoins(); Changed?.Invoke(); return true;
        }
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount; SaveCoins(); Changed?.Invoke();
        }
        public void SetTrail(Color color)
        {
            if (_trail == null)
            {
                _trail = _player.gameObject.AddComponent<TrailRenderer>();
                _trail.time = .32f; _trail.startWidth = .18f; _trail.endWidth = .01f;
                _trail.minVertexDistance = .04f;
                _trail.material = MakeMaterial(color);
            }
            _trail.startColor = color; _trail.endColor = new Color(color.r, color.g, color.b, 0);
            _trail.sharedMaterial.color = color;
            _trail.sharedMaterial.SetColor("_EmissionColor", color * .2f);
            _trail.Clear();
        }
        public bool IsSkinOwned(int index) => index == 0 || PlayerPrefs.GetInt(Key + "Owned." + index, 0) == 1;

        public void SelectSkin(int index)
        {
            if (index < 0 || index >= SkinNames.Length) return;
            if (!IsSkinOwned(index))
            {
                if (Coins < SkinPrices[index]) return;
                Coins -= SkinPrices[index];
                PlayerPrefs.SetInt(Key + "Owned." + index, 1);
                SaveCoins();
            }
            SelectedSkin = index;
            PlayerPrefs.SetInt(Key + "Skin", index);
            PlayerPrefs.Save();
            RefreshCat();
            Changed?.Invoke();
        }

        public void ClaimDaily()
        {
            var meta = GetComponent<CatMetaProgress>();
            if (meta != null) { meta.ClaimDaily(); Changed?.Invoke(); return; }
            if (!CanClaimDaily) return;
            Coins += 50;
            PlayerPrefs.SetString(Key + "Daily", DateTime.UtcNow.ToString("yyyy-MM-dd"));
            SaveCoins();
            Changed?.Invoke();
        }

        public void SetVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            _music.volume = MusicVolume;
            PlayerPrefs.SetFloat(Key + "Volume", MusicVolume);
            PlayerPrefs.Save();
        }

        private void SaveCoins() { PlayerPrefs.SetInt(Key + "Coins", Coins); PlayerPrefs.Save(); }

        private void RefreshCat()
        {
            foreach (Transform child in _visual) Destroy(child.gameObject);
            if (_motion != null) Destroy(_motion);
            if (CatPrefabs.Length == 0) return;
            GameObject prefab = CatPrefabs[Mathf.Min(SelectedSkin, CatPrefabs.Length - 1)];
            if (prefab == null) return;
            var model = Instantiate(prefab, _visual);
            model.name = "CatModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>()) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (Collider collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (Animator animator in model.GetComponentsInChildren<Animator>())
            {
                animator.applyRootMotion = false;
                var controller = SelectedSkin == 0 ? GingerAnimator : CatAnimator;
                if (controller != null) animator.runtimeAnimatorController = controller;
            }
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float scale = 1.3f / Mathf.Max(.1f, bounds.size.y);
                model.transform.localScale *= scale;
                model.transform.localPosition = new Vector3(0, -bounds.min.y * scale + _visual.position.y * (scale - 1), 0);
            }
            _motion = _visual.gameObject.AddComponent<CatVisualMotion>();
        }

        private void ClearTrack()
        {
            foreach (Transform child in _world) Destroy(child.gameObject);
            _tiles.Clear(); _coins.Clear();
        }

        private void OnApplicationPause(bool paused) { if (paused && Phase == CatGamePhase.Playing) Pause(); }

        private void OnDestroy()
        {
            foreach (Material material in _materials) Destroy(material);
            if (_world != null) Destroy(_world.gameObject);
            if (_player != null) Destroy(_player.gameObject);
        }
    }
}
