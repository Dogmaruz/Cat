using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RhythmGame.Services;
using RhythmGame.Sessions;
using VContainer;

namespace DancingCats.Clone
{
    public sealed class OriginalPlayController : MonoBehaviour
    {
        public OriginalAssets Assets;
        public SongRecord[] Songs { get; private set; }
        public SongRecord Selected { get; private set; }
        public MapTimeline Map { get; private set; }
        public OriginalProgression Progression { get; private set; }
        public ClonePhase Phase { get; private set; }
        public Transform Actor { get; private set; }
        public OriginalTile Support { get; private set; }
        public int Score => _score.Value;
        public int Streak => _score.Streak;
        public double GameTime { get; private set; }
        public float Progress => _localFinishZ > 0 ? Mathf.Clamp01(((float)GameTime * Map.forwardSpeed - _lapOffset) / _localFinishZ) : 0;
        public int RunStars => (Endless ? Mathf.Min(_lap, 2) * 3 : 0) + (Progress >= 1 ? 3 : Progress >= .65f ? 2 : Progress >= .3f ? 1 : 0);
        public int CollectedCoins { get; private set; }
        public OriginalSettlementQuote SettlementQuote => Progression?.GetSettlementQuote(_runId);
        public bool WinSequence => _won;
        public SongRecord NextSongRecord
        {
            get
            {
                if (Phase == ClonePhase.Results && _nextSongEvaluated) return _nextSong;
                string id = Progression?.GetNextPlaySong(true, Endless, UnityEngine.Random.Range(0, int.MaxValue));
                var song = Songs?.FirstOrDefault(s => s.id == id);
                if (Phase == ClonePhase.Results) { _nextSong = song; _nextSongEvaluated = true; }
                return song;
            }
        }
        public bool CanNext => NextSongRecord != null && Progression.IsSongUnlocked(NextSongRecord.id);
        public bool CanOfflineUnlockNext => NextSongRecord != null && new[] { 2, 3, 4 }.Contains(NextSongRecord.catalog.musicUnlockType);
        public string Feedback { get; private set; } = "";
        public event Action Changed;
        public bool Endless { get; set; }
        public bool ChallengeRun { get; set; }
        public int ChallengePreviousStars { get; private set; }
        public int Difficulty { get; set; }
        public bool CanRevive => Phase == ClonePhase.Dead && !_revived && ReviveSeconds > 0;
        public float ReviveSeconds => Mathf.Max(0, 5 - (Time.unscaledTime - _deathTime));
        public AudioSource Music => _music;
        public OriginalUIPresenter Presenter { get; private set; }
        public IReadOnlyList<OriginalTile> Tiles => _tiles;
        public float PlaybackRate => _rate;
        public bool Descending => _jumping && JumpPercent > .5f;
        public float JumpPercent => _jumpDistance > .0001f ? (Actor.position.z - _jumpBegin.z) / _jumpDistance : 0;
        private readonly List<OriginalTile> _tiles = new List<OriginalTile>();
        private readonly List<Collider> _coins = new List<Collider>();
        private OriginalScore _score => _runSessions.Current.Score;
        private OriginalRoleMotion _motion;
        private Transform _track, _railway, _endPlatform;
        private AudioSource _music, _effects;
        private Camera _camera;
        private Vector3 _jumpBegin;
        private float _jumpDistance, _height, _finishZ, _localFinishZ, _lapOffset, _destinationX, _pointerOrigin, _dragOrigin, _deathTime, _invincibleUntil, _winStarted;
        private float _rate = 1, _volume = 1;
        private double _dspStart, _clockOffset;
        private float UserDelay => Progression.State.settings.rhythmLineHeight;
        private double EffectiveDelay => _rate * (Map.delayTime * (2 - _rate) + Mathf.Max(UserDelay, 0));
        private bool _dragging, _jumping, _revived, _finishing, _settled, _endRun, _won, _invincibleGrounded;
        private int _lap, _theme;
        private OriginalTile _lastLanding;
        private OriginalThemeVisuals _themeVisuals;
        private Collider _finishCollider;
        private string _runId;
        private string _boundRole, _boundTrail;
        private SongRecord _nextSong;
        private bool _nextSongEvaluated;
        private ISongCatalog _catalog;
        private IGameResources _resources;
        private IAudioClock _audioClock;
        private RoleMotionCatalog _roleCatalog;
        private ThemeCatalog _themeCatalog;
        private SceneRenderSettings _renderSettings;
        private bool _initialized;
        private RunSessionFactory _runSessions;
        private static readonly string[][] MidShotSources = {
            Array.Empty<string>(), new[] { "MidShotItem" }, new[] { "MidShotItem01" }, new[] { "MidShotItem01_0" },
            new[] { "MidShotItem01_1", "MidShotItem02" }, new[] { "MidShotItem01_2", "MidShotItem02_0" },
            new[] { "MidShotItem1", "MidShotItem2", "MidShotItem3" }, new[] { "MidShotItem01_3", "MidShotItem02_1" }
        };

        [Inject]
        public void Construct(OriginalAssets assets, ISongCatalog catalog, OriginalProgression progression,
            IGameResources resources, IAudioClock audioClock, RoleMotionCatalog roleCatalog,
            ThemeCatalog themeCatalog, SceneRenderSettings renderSettings, RunSessionFactory runSessions)
        {
            Assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _audioClock = audioClock ?? throw new ArgumentNullException(nameof(audioClock));
            _roleCatalog = roleCatalog ?? throw new ArgumentNullException(nameof(roleCatalog));
            _themeCatalog = themeCatalog ?? throw new ArgumentNullException(nameof(themeCatalog));
            _renderSettings = renderSettings ?? throw new ArgumentNullException(nameof(renderSettings));
            _runSessions = runSessions ?? throw new ArgumentNullException(nameof(runSessions));
        }

        public void Initialize()
        {
            if (_initialized) throw new InvalidOperationException("The game has already started.");
            if (_catalog == null) throw new InvalidOperationException("The game must be composed before startup.");
            _initialized = true;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Songs = _catalog.Songs;
            Progression.Changed += NotifyChanged;
            _music = gameObject.AddComponent<AudioSource>(); _music.playOnAwake = false;
            _effects = gameObject.AddComponent<AudioSource>(); _effects.playOnAwake = false;
            _track = new GameObject("Original authored map").transform;
            _track.SetParent(transform, false);
            Actor = new GameObject("Original Cat Actor").transform;
            Actor.SetParent(transform, false);
            var body = Actor.gameObject.AddComponent<Rigidbody>();
            body.useGravity = false; body.isKinematic = true;
            var sphere = Actor.gameObject.AddComponent<SphereCollider>();
            sphere.radius = .25f; sphere.isTrigger = true;
            Actor.gameObject.AddComponent<OriginalActorTrigger>().owner = this;
            _motion = Actor.gameObject.AddComponent<OriginalRoleMotion>();
            RefreshRole();
            var selected = Songs.FirstOrDefault(s => s.id == Progression.State.selectedSong) ?? Songs[0];
            Prepare(selected, ClonePhase.Menu, true);
            Debug.Log("DANCING_CATS_READY songs=" + Songs.Length + " cats=" + Assets.RoleNames.Length + " sprites=" + Assets.sprites.Length + " song=" + Selected.id + " elements=" + Map.elements.Length);
        }

        public void AttachPresenter(OriginalUIPresenter presenter)
        {
            if (Presenter != null) throw new InvalidOperationException("A presenter is already attached.");
            Presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        }

        public bool SelectSong(int catalogId)
        {
            var song = Songs.FirstOrDefault(s => s.catalogId == catalogId);
            if (song == null) return false;
            if (!Progression.IsSongUnlocked(song.id)) return false;
            PaySettlement();
            ChallengeRun = false;
            Progression.State.selectedSong = song.id; Progression.Save();
            Prepare(song, ClonePhase.Menu, true);
            return true;
        }

        private void Prepare(SongRecord song, ClonePhase phase, bool resetScore)
        {
            float retainedX = Actor.position.x;
            int retainedPalette = _themeVisuals != null ? _themeVisuals.Index : 0;
            Vector3 retainedCamera = _camera != null ? _camera.transform.position : Vector3.zero;
            _music.Stop();
            Selected = song; Map = _catalog.LoadMap(song.id);
            if (resetScore) { _runId = _runSessions.Begin().Id; _lap = 0; _settled = _revived = false; CollectedCoins = 0; _nextSong = null; _nextSongEvaluated = false; }
            _rate = 1 + (song.catalog?.speedAdd != null && song.catalog.speedAdd.Length > 0 ? song.catalog.speedAdd[Mathf.Min(_lap, song.catalog.speedAdd.Length - 1)] : 0);
            _localFinishZ = Map.elements.First(e => e.kind == "FinishJump").pos.z;
            float delayLength = Mathf.Min(0, UserDelay) * Map.forwardSpeed;
            _lapOffset = _lap * (_localFinishZ + 5 - delayLength);
            OffsetMap(_lapOffset);
            var firstTile = Map.elements.First(e => e.kind == "Start");
            firstTile.pos.z += delayLength; firstTile.jumpDistance -= delayLength; firstTile.gameTime += delayLength / Map.forwardSpeed;
            GameTime = firstTile.gameTime;
            if (resetScore) { _dragging = false; _destinationX = 0; }
            _finishing = _endRun = _won = _invincibleGrounded = false; _invincibleUntil = -1;
            _lastLanding = Support = null;
            InstallEnvironment();
            BuildTrack();
            Actor.position = new Vector3(resetScore ? 0 : retainedX, .25f, firstTile.pos.z);
            if (!resetScore)
            {
                _themeVisuals.SetIndex(retainedPalette, Actor.position.z);
                if (_camera != null) _camera.transform.position = retainedCamera;
            }
            if (resetScore) _motion.ResetMotion();
            _motion.PlayIdle();
            Phase = phase;
            FollowCamera(resetScore);
            if (phase == ClonePhase.Menu) PlayPreview();
            NotifyChanged();
        }

        private void OffsetMap(float offset)
        {
            if (offset == 0) return;
            double time = offset / Map.forwardSpeed;
            foreach (var element in Map.elements)
            {
                element.pos.z += offset; element.mstartPos.z += offset; element.gameTime += time;
                foreach (var tick in element.holdTicks ?? Array.Empty<HoldTick>()) { tick.z += offset; tick.gameTime += time; }
            }
            foreach (var segment in Map.segments)
            {
                segment.z += offset; segment.endZ += offset; segment.gameTime += time; segment.endGameTime += time;
                foreach (var tick in segment.holdTicks ?? Array.Empty<HoldTick>()) { tick.z += offset; tick.gameTime += time; }
                foreach (var point in segment.path ?? Array.Empty<ChartPoint>()) { point.z += offset; point.gameTime += time; }
            }
        }

        private void InstallEnvironment()
        {
            if (_railway != null) { _railway.gameObject.SetActive(false); Destroy(_railway.gameObject); }
            if (_endPlatform != null) { _endPlatform.gameObject.SetActive(false); Destroy(_endPlatform.gameObject); }
            var appointed = Progression.Config.appointedScenes.FirstOrDefault(s => s.musicName == Selected.id);
            _theme = appointed != null ? appointed.sceneNumber : 1;
            var environment = Assets.prefabs.FirstOrDefault(p => p.name == "Environment" + _theme);
            if (environment == null) throw new InvalidOperationException("Original environment unavailable: " + _theme);
            _railway = Instantiate(environment.asset).transform;
            _railway.SetParent(transform, true);
            _railway.name = "Original railway " + _theme;
            var cameras = _railway.GetComponentsInChildren<Camera>(true);
            _camera = cameras.FirstOrDefault(c => c.gameObject.activeInHierarchy) ?? cameras.FirstOrDefault();
            foreach (var camera in cameras) camera.enabled = camera == _camera;
            foreach (var canvas in _railway.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            var platform = Assets.prefabs.FirstOrDefault(p => p.name == "EndPlatform" + _theme);
            if (platform != null)
            {
                _endPlatform = Instantiate(platform.asset).transform;
                _endPlatform.SetParent(transform, true);
                _endPlatform.name = "Original end platform";
                _endPlatform.gameObject.SetActive(true);
            }
        }

        public void RefreshRole()
        {
            if (Actor == null || Progression == null) return;
            foreach (Transform child in Actor) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            string id = Progression.SelectedRole;
            string name = Progression.Role(id)?.prefabName;
            if (name == null) throw new InvalidOperationException("Recovered original cat is unavailable: " + id);
            var instance = Instantiate(Assets.Prefab(name), Actor, false);
            instance.name = name; instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) { collider.enabled = false; Destroy(collider); }
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; Destroy(body); }
            _motion.Bind(instance, name, _roleCatalog);
            string trailName = Progression.Trail(Progression.SelectedTrail)?.prefabName;
            var trailEntry = Assets.prefabs.FirstOrDefault(p => p.name == trailName);
            if (trailEntry?.asset != null)
            {
                var trail = Instantiate(trailEntry.asset, Actor, false);
                trail.name = "Original selected trail";
            }
            _boundRole = Progression.SelectedRole; _boundTrail = Progression.SelectedTrail;
            NotifyChanged();
        }

        private void BuildTrack()
        {
            foreach (Transform child in _track) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _tiles.Clear();
            _coins.Clear(); _finishCollider = null;
            int previousMidShot = -1;
            _finishZ = Map.elements.Where(e => e.kind == "FinishJump").Select(e => e.pos.z).DefaultIfEmpty((float)Selected.audioLengthSeconds * Map.forwardSpeed).First();
            foreach (var element in Map.elements.OrderBy(e => e.pos.z).ThenBy(e => e.sourceIndex))
            {
                string stem = element.id == 0 ? "Jump1" : element.id == 1 ? "Jump2" : element.id == 2 || element.id == 3 ? "Hold" : element.id == 4 ? "Follow" : element.id == 5 ? "FollowMid" : element.id == 6 ? "FollowEnd" : "MidShotItem";
                // level0 is the original bootstrap. Seven gameplay scenes use the base skin followed by _0.._5.
                string suffix = _theme == 1 ? "" : "_" + (_theme - 2);
                var entry = Assets.prefabs.FirstOrDefault(p => p.name == stem + suffix) ?? Assets.prefabs.FirstOrDefault(p => p.name == stem);
                if (element.id == 7)
                {
                    var sources = MidShotSources[_theme];
                    var choices = Enumerable.Range(0, sources.Length).Where(i => sources.Length == 1 || i != previousMidShot).ToArray();
                    previousMidShot = choices[UnityEngine.Random.Range(0, choices.Length)];
                    entry = Assets.prefabs.First(p => p.name == sources[previousMidShot]);
                }
                if (entry?.asset == null) throw new InvalidOperationException("Original tile prefab is missing: " + stem);
                var instance = Instantiate(entry.asset, _track, false);
                instance.name = entry.name;
                instance.transform.position = element.pos;
                var segment = Map.segments.FirstOrDefault(s => s.sourceIndex == element.sourceIndex);
                var tile = instance.AddComponent<OriginalTile>();
                tile.Initialize(this, element, segment, element.id == 5);
                if ((element.id == 2 || element.id == 3) && segment != null)
                {
                    var box = instance.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault();
                    if (box != null)
                    {
                        float length = segment.endZ - segment.z;
                        var size = box.size; size.z = length + 1.2f; box.size = size;
                        var center = box.center; center.z = length * .5f; box.center = center;
                    }
                }
                if ((element.id == 2 || element.id == 3) && segment != null)
                {
                    OriginalTileVisuals.RegisterHoldPointTemplate(instance, Assets.Prefab("Hold_Light" + suffix));
                    OriginalTileVisuals.Configure(instance, element, segment);
                    var ticks = segment.holdTicks ?? Array.Empty<HoldTick>();
                    foreach (var tick in ticks.Where(t => t.z > element.pos.z + .01f))
                    {
                        var point = OriginalTileVisuals.SpawnHoldPoint(instance, new Vector3(element.pos.x, element.pos.y, tick.z));
                        if (point == null) throw new InvalidOperationException("Original HoldPoint prefab could not be spawned.");
                        var pointElement = new MapElement { id = 9, kind = "HoldPoint", pos = point.transform.position, sourceIndex = element.sourceIndex, gameTime = tick.gameTime };
                        var binding = point.AddComponent<OriginalTile>(); binding.Initialize(this, pointElement, segment, true);
                        _tiles.Add(binding);
                    }
                    var exit = instance.transform.Find("Jump");
                    if (exit == null) throw new InvalidOperationException("Original Hold exit jump is missing.");
                    var exitElement = new MapElement { id = 8, kind = "HoldExit", pos = exit.position, jumpDistance = element.jumpDistance, sourceIndex = element.sourceIndex };
                    var exitBinding = exit.gameObject.AddComponent<OriginalTile>(); exitBinding.Initialize(this, exitElement, segment, true);
                    _tiles.Add(exitBinding);
                }
                else if (element.id != 7) OriginalTileVisuals.Configure(instance, element, segment);
                _tiles.Add(tile);
            }
            _tiles.Sort((a, b) => a.initialZ.CompareTo(b.initialZ));
            if (_endPlatform != null)
            {
                _endPlatform.position = Vector3.forward * (_finishZ + 5);
                _endPlatform.gameObject.SetActive(!Endless || _lap >= 2);
                var endElement = new MapElement { id = 10, kind = "EndPlatform", pos = _endPlatform.position, sourceIndex = int.MaxValue };
                var end = _endPlatform.gameObject.AddComponent<OriginalTile>(); end.Initialize(this, endElement);
                _tiles.Add(end);
                foreach (Transform node in _endPlatform.GetComponentsInChildren<Transform>(true))
                {
                    if (node.name.StartsWith("Pickable", StringComparison.Ordinal) && node.TryGetComponent<Collider>(out var collider)) _coins.Add(collider);
                    if (node.name == "FinalTile01") _finishCollider = node.GetComponent<Collider>();
                }
            }
            if (_themeVisuals == null) _themeVisuals = gameObject.AddComponent<OriginalThemeVisuals>();
            _themeVisuals.Bind(_railway.gameObject, _tiles, Assets.themeMaterials, _theme, _themeCatalog);
        }

        public void BeginRun()
        {
            if (Phase != ClonePhase.Menu && Phase != ClonePhase.Ready) return;
            var start = _tiles.First(t => t.element.kind == "Start");
            if (_lap == 0) Progression.RecordSongStart(Selected.id);
            if (_endPlatform != null) _endPlatform.gameObject.SetActive(!Endless || _lap >= 2);
            _lastLanding = start; start.MarkConsumed();
            BeginArc(start.element.pos, start.element.jumpDistance);
            Schedule(start.element.gameTime);
            Phase = ClonePhase.Playing;
            NotifyChanged();
        }

        private void Schedule(double gameTime)
        {
            _music.Stop(); _music.clip = _catalog.LoadAudio(Selected); _music.loop = false;
            _music.pitch = _rate; _music.volume = Progression.State.settings.sound ? _volume : 0;
            double audioTime = gameTime - _lapOffset / Map.forwardSpeed - Mathf.Min(UserDelay, 0) - EffectiveDelay;
            _clockOffset = gameTime; _dspStart = _audioClock.DspTime;
            _music.timeSamples = Mathf.Clamp((int)Math.Round(Math.Max(0, audioTime) * _music.clip.frequency), 0, _music.clip.samples - 1);
            if (audioTime < _music.clip.length) _music.PlayScheduled(_dspStart + Math.Max(0, -audioTime) / _rate);
        }

        private void PlayPreview()
        {
            _music.clip = _catalog.LoadAudio(Selected, true);
            _music.pitch = 1; _music.loop = true; _music.volume = Progression.State.settings.sound ? _volume : 0;
            if (_music.clip != null) _music.Play();
        }

        private void BeginArc(Vector3 point, float distance)
        {
            _invincibleGrounded = false;
            Support = null; _jumpBegin = point; _jumpDistance = Mathf.Max(.01f, distance);
            _height = NativeRhythmRules.HeightForDistance(_jumpDistance); _jumping = true;
            _motion.PlayJump();
        }

        private void Update()
        {
            if (Actor == null) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                if (Phase == ClonePhase.Playing) Pause();
                else if (Phase == ClonePhase.Paused) Resume();
                else Home();
            }
            if (Phase == ClonePhase.Playing)
            {
                if (_won)
                {
                    float elapsed = Time.unscaledTime - _winStarted;
                    var at = Actor.position; at.x = Mathf.Lerp(at.x, 0, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8)); Actor.position = at;
                    if (elapsed < 1 && _railway != null) _railway.position += Vector3.forward * Map.forwardSpeed * _rate * Mathf.Cos(elapsed * Mathf.PI * .5f) * Time.unscaledDeltaTime;
                    if (elapsed >= 2) Complete();
                    return;
                }
                ReadPointer();
                double previousTime = GameTime;
                double targetTime = _clockOffset + Math.Max(0, _audioClock.DspTime - _dspStart) * _rate;
                var previousPosition = Actor.position;
                float targetX = Mathf.Clamp(previousPosition.x + (_destinationX - previousPosition.x) * .25f, -1.5f, 1.5f);
                int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs((float)targetTime * Map.forwardSpeed - previousPosition.z) / .12f), 1, 1024);
                // Preserve the native once-per-frame lateral damping while sampling the real arc
                // densely enough that authored thin checkpoint/finish colliders survive a render hitch.
                for (int sample = 1; sample <= steps; sample++)
                {
                float fraction = (float)sample / steps;
                GameTime = previousTime + (targetTime - previousTime) * fraction;
                float z = (float)GameTime * Map.forwardSpeed;
                foreach (var tile in _tiles) tile.Tick(z);
                var position = Actor.position;
                position.x = Mathf.Lerp(previousPosition.x, targetX, fraction);
                position.z = z;
                if (Support != null)
                {
                    position.y = Support.TopY + .25f;
                }
                if (_jumping) position.y = NativeRhythmRules.JumpY(_jumpBegin.y, _height, (z - _jumpBegin.z) / _jumpDistance) + .25f;
                Actor.position = position;
                Physics.SyncTransforms();
                foreach (var tile in _tiles)
                    if (!tile.consumed && Mathf.Abs(tile.transform.position.z - z) < 2 && tile.ContainsSphere(position, .25f)) TouchTile(tile);
                foreach (var tile in _tiles)
                    if (!tile.consumed && tile.element.id == 5 && z > tile.initialZ + 1)
                    { tile.MarkConsumed(); _score.BreakStreak(); NotifyChanged(); }
                foreach (var hold in _tiles)
                    if (hold.consumed && (hold.element.id == 2 || hold.element.id == 3)) OriginalTileVisuals.AdvanceHold(hold.gameObject, z);
                if (Support != null && Support.element.id != 10 && !Support.ContainsSphere(Actor.position, .25f))
                {
                    if (Time.unscaledTime < _invincibleUntil) GroundDuringInvincibility();
                    else { Die(); FollowCamera(false); return; }
                }
                if (_jumping && Actor.position.y < -1 && Time.unscaledTime < _invincibleUntil)
                    GroundDuringInvincibility();
                if (Actor.position.y < -2 || _invincibleGrounded && Time.unscaledTime >= _invincibleUntil)
                {
                    Die();
                    FollowCamera(false); return;
                }
                if (_endRun)
                {
                    foreach (var coin in _coins)
                        if (coin != null && coin.enabled && coin.gameObject.activeInHierarchy && (coin.ClosestPoint(Actor.position) - Actor.position).sqrMagnitude <= .06251f)
                        {
                            coin.enabled = false;
                            var art = coin.transform.Find("Node1/Pickable/Art"); if (art != null) art.gameObject.SetActive(false);
                            var effect = coin.transform.Find("Node1/Pickable/Effect");
                            if (effect != null) { effect.gameObject.SetActive(true); foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true)) particle.Play(true); }
                            CollectedCoins++; PlayEffect("lantern2__985"); NotifyChanged();
                        }
                    if (_finishCollider != null && (_finishCollider.ClosestPoint(Actor.position) - Actor.position).sqrMagnitude <= .06251f)
                    {
                        _won = true; _winStarted = Time.unscaledTime;
                        NotifyChanged();
                        var animation = _finishCollider.GetComponentInChildren<Animator>();
                        if (animation != null && animation.HasState(0, Animator.StringToHash("Win"))) animation.Play("Win");
                        break;
                    }
                }
                if (_finishing && Endless && _lap < 2 && z >= _finishZ + 5)
                {
                    Progression.RecordSongResult(Selected.id, RunStars, Score, true, false, DateTime.Now);
                    _lap++; Prepare(Selected, ClonePhase.Ready, false); BeginRun(); return;
                }
                }
                FollowCamera(false);
            }
            else if (Phase == ClonePhase.Dead)
            {
                Actor.position += Vector3.down * Time.unscaledDeltaTime * 5;
                if (ReviveSeconds <= 0) AcceptFailure();
            }
        }

        private void ReadPointer()
        {
            if (Presenter != null && Presenter.IsBlockingInput) { _dragging = false; return; }
            bool held = false; float pointer = 0;
            if (Touchscreen.current?.primaryTouch.press.isPressed == true) { held = true; pointer = Touchscreen.current.primaryTouch.position.ReadValue().x; }
            else if (Mouse.current?.leftButton.isPressed == true) { held = true; pointer = Mouse.current.position.ReadValue().x; }
            if (held)
            {
                if (!_dragging) { _pointerOrigin = pointer; _dragOrigin = Actor.position.x; }
                _destinationX = _dragOrigin + (pointer - _pointerOrigin) * 6.6f / Mathf.Max(1, Screen.width);
            }
            _dragging = held;
            if (Keyboard.current != null)
            {
                float axis = (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed ? 1 : 0);
                _destinationX = Mathf.Clamp(_destinationX + axis * Time.unscaledDeltaTime * 6.6f, -1.5f, 1.5f);
            }
        }

        public void TouchTile(OriginalTile tile)
        {
            if (Phase != ClonePhase.Playing || tile.consumed || tile.element == null) return;
            if (tile.element.id == 7) return;
            if (tile.element.id == 10)
            {
                if (_jumping && !Descending) return;
                tile.MarkConsumed(); Support = tile; _jumping = false; _invincibleGrounded = false; _endRun = true;
                _motion.PlayEnd(); _themeVisuals.Finish(); return;
            }
            if (tile.element.id == 8)
            {
                if (Support == null || Support.element.sourceIndex != tile.element.sourceIndex) return;
                tile.MarkConsumed();
                BeginArc(tile.element.pos, tile.element.jumpDistance);
                return;
            }
            if (tile.element.id == 5 || tile.checkpoint)
            {
                ScoreHit(tile, tile.element.id == 5 || NativeRhythmRules.IsPerfect(Actor.position.x - tile.CenterX, true, false));
                tile.MarkConsumed(); return;
            }
            if (_jumping && !Descending) return;
            if (_lastLanding != null && tile.element.pos.z <= _lastLanding.element.pos.z) return;
            tile.MarkConsumed(); _lastLanding = tile;
            bool slider = tile.element.id == 2 || tile.element.id == 3 || tile.element.id == 4;
            ScoreHit(tile, tile.element.id == 4 || NativeRhythmRules.IsPerfect(Actor.position.x - tile.CenterX, slider, tile.element.id == 6));
            if (slider)
            {
                _jumping = false; _invincibleGrounded = false; Support = tile;
                Actor.position = new Vector3(Actor.position.x, tile.TopY + .25f, Actor.position.z);
                _motion.PlayHold(tile.element.id == 4);
            }
            else BeginArc(tile.transform.position, tile.element.jumpDistance);
            if (tile.element.kind == "Theme") _themeVisuals.Advance(Actor.position.z);
            if (tile.element.kind == "FinishJump") _finishing = true;
        }

        private void ScoreHit(OriginalTile tile, bool perfect)
        {
            _score.Apply(perfect); Feedback = perfect ? "Perfect" : "Great";
            PlayEffect("lantern2__985"); _themeVisuals.ShowHit(perfect, Streak);
            OriginalTileVisuals.OnHit(tile.gameObject, perfect, tile.element.id == 2 || tile.element.id == 3, Streak);
#if UNITY_ANDROID || UNITY_IOS
            if (Progression.State.settings.vibration) Handheld.Vibrate();
#endif
            NotifyChanged();
        }

        public void LeaveTile(OriginalTile tile)
        {
            if (Phase != ClonePhase.Playing || Support != tile) return;
            if (tile.segment != null && Actor.position.z >= tile.segment.endZ - .1f)
                BeginArc(new Vector3(tile.CenterX, tile.TopY, tile.segment.endZ), tile.element.jumpDistance);
        }

        private void GroundDuringInvincibility()
        {
            // Native invincibility resets Y at the current chart position. It neither seeks
            // the song to another note nor declares a finish without the actual final trigger.
            Support = null; _jumping = false; _invincibleGrounded = true;
            var position = Actor.position; position.y = .25f; Actor.position = position;
        }

        private void Die()
        {
            if (Phase != ClonePhase.Playing) return;
            _music.Stop(); _score.BreakStreak(); _deathTime = Time.unscaledTime;
            PlayEffect("03_death__983");
            Phase = ClonePhase.Dead; Feedback = "";
            NotifyChanged();
        }

        public void Pause() { if (Phase != ClonePhase.Playing || _won) return; _music.Stop(); Phase = ClonePhase.Paused; _dragging = false; NotifyChanged(); }
        public void Resume() { if (Phase != ClonePhase.Paused) return; Schedule(GameTime); Phase = ClonePhase.Playing; NotifyChanged(); }
        public void Retry() { PaySettlement(); Prepare(Selected, ClonePhase.Ready, true); }
        public void Home() { PaySettlement(); ChallengeRun = false; Prepare(Selected, ClonePhase.Menu, true); }
        public void NextSong()
        {
            var next = NextSongRecord;
            if (next != null && SelectSong(next.catalogId)) return;
            Home(); Presenter.OpenPanel("MusicList_UIPanel");
        }
        public void UnlockNextOffline()
        {
            var next = NextSongRecord;
            if (next == null || !CanOfflineUnlockNext) return;
            if (Progression.RewardSongUnlock(next)) SelectSong(next.catalogId);
        }
        public void Revive()
        {
            if (!CanRevive) return;
            _revived = true;
            var previous = _lastLanding;
            GameTime = previous != null ? previous.element.gameTime : 0;
            _invincibleUntil = Time.unscaledTime + 3;
            _invincibleGrounded = false;
            Actor.position = previous != null ? previous.element.pos + Vector3.up * .25f : Vector3.up * .25f;
            _destinationX = Actor.position.x; _dragging = false;
            if (previous != null && (previous.element.id == 2 || previous.element.id == 3 || previous.element.id == 4))
            {
                Support = previous; _jumping = false; _motion.PlayHold(previous.element.id == 4);
                if (previous.element.id == 2 || previous.element.id == 3)
                {
                    foreach (var exit in _tiles.Where(t => t.element.id == 8 && t.element.sourceIndex == previous.element.sourceIndex)) exit.consumed = false;
                    OriginalTileVisuals.RestartHold(previous.gameObject, Actor.position.z);
                }
            }
            else BeginArc(previous != null ? previous.element.pos : Vector3.zero, previous?.element.jumpDistance ?? 3.25f);
            Schedule(GameTime); Phase = ClonePhase.Playing; NotifyChanged();
        }

        private void Complete()
        {
            if (Phase != ClonePhase.Playing) return;
            _music.Stop(); _motion.PlayEnd();
            Phase = ClonePhase.Results;
            if (!_settled)
            {
                _settled = true;
                ChallengePreviousStars = Progression.State.challengeStars;
                Progression.RecordSongResult(Selected.id, RunStars, Score, Endless, true, DateTime.Now, ChallengeRun);
                Progression.PrepareSettlement(_runId, RunStars, CollectedCoins, UnityEngine.Random.Range(0, int.MaxValue));
            }
            NotifyChanged();
        }

        public void AcceptFailure()
        {
            if (Phase != ClonePhase.Dead) return;
            Phase = ClonePhase.Results;
            if (!_settled)
            {
                _settled = true;
                ChallengePreviousStars = Progression.State.challengeStars;
                Progression.RecordSongResult(Selected.id, RunStars, Score, Endless, false, DateTime.Now, ChallengeRun);
                Progression.PrepareSettlement(_runId, RunStars, CollectedCoins, UnityEngine.Random.Range(0, int.MaxValue));
            }
            NotifyChanged();
        }

        public void PaySettlement() { if (!string.IsNullOrEmpty(_runId)) Progression?.AutoRewardSettlement(_runId); }
        private void PlayEffect(string resource)
        {
            var clip = _resources.Load<AudioClip>("OriginalAudio/Sfx/" + resource);
            if (clip != null && Progression.State.settings.sound) _effects.PlayOneShot(clip);
        }

        private void FollowCamera(bool immediate)
        {
            if (_railway != null) _railway.position = Vector3.forward * Actor.position.z;
            if (_camera == null) return;
            float x = immediate ? Actor.position.x : Mathf.Lerp(_camera.transform.position.x, Actor.position.x, 1 - Mathf.Exp(-4.60517f * Time.unscaledDeltaTime / 5));
            _camera.transform.SetPositionAndRotation(new Vector3(x, 3.85f, Actor.position.z - 7.9f), Quaternion.Euler(20, 0, 0));
            _camera.fieldOfView = 45;
        }

        public void SetVolume(float volume) { _volume = Mathf.Clamp01(volume); _music.volume = _volume; }
        public int GetSongBest(int id)
        {
            string key = Songs.FirstOrDefault(s => s.catalogId == id)?.id;
            var record = Progression.State.songs.FirstOrDefault(s => s.id == key);
            return record == null ? 0 : Endless ? record.endlessScore : record.normalScore;
        }
        public int GetSongStars(int id)
        {
            string key = Songs.FirstOrDefault(s => s.catalogId == id)?.id;
            var record = Progression.State.songs.FirstOrDefault(s => s.id == key);
            return record == null ? 0 : Endless ? record.endlessStars : record.normalStars;
        }
        public void NotifyChanged()
        {
            if (Progression != null)
            {
                if (Actor != null && (_boundRole != Progression.SelectedRole || _boundTrail != Progression.SelectedTrail)) RefreshRole();
                var settings = Progression.State.settings;
                if (_music != null) _music.volume = settings.sound ? _volume : 0;
                if (_effects != null) _effects.volume = settings.sound ? _volume : 0;
                _renderSettings.Apply(_camera, settings.postProcessing);
            }
            Changed?.Invoke();
        }
        private void OnApplicationPause(bool paused) { if (paused) Pause(); }
        private void OnDestroy()
        {
            if (Progression != null) Progression.Changed -= NotifyChanged;
            if (_music != null) _music.Stop();
            if (_effects != null) _effects.Stop();
        }
    }
}
