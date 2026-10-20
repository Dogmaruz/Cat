using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace DancingCat
{
    /// <summary>Runtime portrait interface for the new game. All actions use the real game controller.</summary>
    [DisallowMultipleComponent]
    public sealed class CatGameUI : MonoBehaviour
    {
        [SerializeField] private CatGameController m_controller;
        [SerializeField] private TMP_FontAsset m_titleFont = null;

        private static readonly Color Ink = new Color(.22f, .12f, .36f);
        private static readonly Color Muted = new Color(.48f, .40f, .59f);
        private static readonly Color Purple = new Color(.49f, .29f, .86f);
        private static readonly Color Pink = new Color(1f, .32f, .64f);
        private static readonly Color Gold = new Color(1f, .77f, .20f);
        private static readonly Color Paper = new Color(.98f, .96f, 1f);
        private readonly Dictionary<string, UnityEngine.UI.Button> _buttons = new Dictionary<string, UnityEngine.UI.Button>();
        private readonly List<SongRow> _songRows = new List<SongRow>();
        private readonly List<SkinRow> _skinRows = new List<SkinRow>();
        private readonly List<Sprite> _ownedSprites = new List<Sprite>();
        private readonly List<Texture2D> _ownedTextures = new List<Texture2D>();
        private TMP_FontAsset _font;
        private Canvas _canvas;
        private RectTransform _safe;
        private RectTransform _menu;
        private RectTransform _songsPanel;
        private RectTransform _catsPanel;
        private RectTransform _rewardsPanel;
        private RectTransform _hud;
        private RectTransform _ready;
        private RectTransform _pause;
        private RectTransform _failure;
        private RectTransform _results;
        private RectTransform _settings;
        private TMP_Text _coinText;
        private TMP_Text _previewName;
        private TMP_Text _scoreText;
        private TMP_Text _comboText;
        private TMP_Text _songText;
        private TMP_Text _progressText;
        private TMP_Text _feedbackText;
        private TMP_Text _readyTitle;
        private TMP_Text _readySong;
        private TMP_Text _failedStats;
        private TMP_Text _reviveLabel;
        private TMP_Text _resultTitle;
        private TMP_Text _resultStats;
        private TMP_Text _resultCoins;
        private TMP_Text _rewardText;
        private TMP_Text _rewardLabel;
        private TMP_Text _volumeText;
        private UnityEngine.UI.Image _progressFill;
        private UnityEngine.UI.Slider _volume;
        private readonly UnityEngine.UI.Image[] _resultStars = new UnityEngine.UI.Image[3];
        private Sprite _rounded;
        private Sprite _circle;
        private Sprite _triangle;
        private Sprite _star;
        private string _tab = "Songs";
        private CatGamePhase _shownPhase = (CatGamePhase)(-1);
        private bool _built;
        private bool _subscribed;
        private bool _settingsOpen;
        private bool _goPressed;
        private int _screenWidth;
        private int _screenHeight;
        private Rect _lastSafeArea;
        private string _lastFeedback;
        private int _lastFeedbackScore = -1;
        private float _feedbackAt;

        private sealed class SongRow
        {
            public int index;
            public CatSong song;
            public TMP_Text best;
            public UnityEngine.UI.Image[] stars;
        }

        private sealed class SkinRow
        {
            public int index;
            public TMP_Text status;
            public TMP_Text action;
            public UnityEngine.UI.Button button;
        }

        private void Awake()
        {
            if (m_controller == null) m_controller = GetComponent<CatGameController>();
        }

        private void OnEnable() { Subscribe(); }

        private void Start()
        {
            if (m_controller == null)
            {
                Debug.LogError("CatGameUI requires its CatGameController.", this);
                enabled = false;
                return;
            }
            Subscribe();
            Build();
            Refresh();
        }

        private void Subscribe()
        {
            if (_subscribed || m_controller == null) return;
            m_controller.Changed += Refresh;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed && m_controller != null) m_controller.Changed -= Refresh;
            _subscribed = false;
        }

        private void OnDestroy()
        {
            foreach (Sprite sprite in _ownedSprites) if (sprite != null) Destroy(sprite);
            foreach (Texture2D texture in _ownedTextures) if (texture != null) Destroy(texture);
        }

        private void Update()
        {
            if (!_built) return;
            ApplySafeArea();
            if (_shownPhase != m_controller.Phase) Refresh();
            if (m_controller.Phase == CatGamePhase.Failed)
            {
                _buttons["Revive"].interactable = m_controller.CanRevive;
                _reviveLabel.text = "REVIVE  ·  " + m_controller.ReviveCost + " COINS  ·  " + Mathf.CeilToInt(m_controller.ReviveRemaining) + "s";
            }
            if (_hud.gameObject.activeSelf)
            {
                _progressFill.fillAmount = Mathf.Clamp01(m_controller.Progress);
                _progressText.text = Mathf.RoundToInt(m_controller.Progress * 100f) + "%";
                _scoreText.SetText("{0}", m_controller.Score);
                _comboText.text = m_controller.Combo > 1 ? "x" + m_controller.Combo + " COMBO" : "FIND YOUR RHYTHM";
                string feedback = m_controller.Feedback ?? "";
                if (_lastFeedback != feedback || _lastFeedbackScore != m_controller.Score)
                {
                    _lastFeedback = feedback;
                    _lastFeedbackScore = m_controller.Score;
                    _feedbackAt = Time.unscaledTime;
                    _feedbackText.text = feedback;
                }
                float age = Time.unscaledTime - _feedbackAt;
                _feedbackText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, Mathf.Clamp01(age * 6f));
                _feedbackText.alpha = Mathf.Clamp01(1.7f - age);
                if (_ready.gameObject.activeSelf && _goPressed && !string.IsNullOrEmpty(feedback))
                    _readyTitle.text = feedback;
            }
        }

        private void Build()
        {
            _font = TMP_Settings.defaultFontAsset;
            if (_font == null) _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            _rounded = Shape("Rounded", 80, 80, (x, y) =>
            {
                float dx = Mathf.Max(Mathf.Abs(x - 39.5f) - 15.5f, 0);
                float dy = Mathf.Max(Mathf.Abs(y - 39.5f) - 15.5f, 0);
                return Mathf.Clamp01(24f - Mathf.Sqrt(dx * dx + dy * dy));
            }, new Vector4(26, 26, 26, 26));
            _circle = Shape("Circle", 80, 80, (x, y) => Mathf.Clamp01(39.5f - Vector2.Distance(new Vector2(x, y), new Vector2(39.5f, 39.5f))));
            _triangle = Shape("Ear", 64, 64, (x, y) => y >= 4 && y <= 59 && Mathf.Abs(x - 31.5f) <= (59 - y) * .52f ? 1 : 0);
            _star = Shape("Star", 80, 80, StarAlpha);

            var canvasObject = new GameObject("DancingCatCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.layer = LayerMask.NameToLayer("UI");
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0;
            _safe = Fill(canvasObject.transform, "SafeArea");
            ApplySafeArea();
            EnsureEventSystem();

            _menu = Fill(_safe, "MainMenu");
            BuildMenu();
            _hud = Fill(_safe, "GameplayHUD");
            BuildHud();
            BuildReady();
            BuildPause();
            BuildFailure();
            BuildResults();
            BuildSettings();
            _built = true;
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var system = new GameObject("DancingCatEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            system.transform.SetParent(transform, false);
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void ApplySafeArea()
        {
            if (_safe == null || Screen.width == 0 || Screen.height == 0) return;
            Rect area = Screen.safeArea;
            if (_screenWidth == Screen.width && _screenHeight == Screen.height && area == _lastSafeArea) return;
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _lastSafeArea = area;
            _safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            _safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }

        private void BuildMenu()
        {
            // The center remains transparent so the real 3D cat is the hero of the menu.
            var haze = Fill(_menu, "MenuTint");
            Image(haze, new Color(.32f, .16f, .58f, .12f));
            Disc(_menu, "PinkHalo", -150, 350, 380, new Color(1, .42f, .71f, .12f));
            Disc(_menu, "PurpleHalo", 880, 190, 340, new Color(.63f, .43f, 1, .15f));
            RectTransform coin = At(_menu, "CoinPill", 48, 38, 250, 112);
            Surface(coin, new Color(.99f, .97f, 1f, .96f));
            Disc(coin, "CoinIcon", 24, 24, 64, Gold);
            Label(coin, "CoinMark", "C", 32, Ink, 24, 24, 64, 64, TextAlignmentOptions.Center, true);
            _coinText = Label(coin, "CoinCount", "0", 39, Ink, 105, 18, 125, 76, TextAlignmentOptions.Left, true);
            Button(_menu, "OpenSettings", "=", 900, 30, 132, 132, new Color(1, 1, 1, .95f), Ink,
                () => { _settingsOpen = true; Refresh(); });
            TMP_Text title = Label(_menu, "GameTitle", "DANCING CAT", 100, Color.white, 75, 185, 930, 148, TextAlignmentOptions.Center, true);
            title.font = m_titleFont != null ? m_titleFont : _font;
            title.characterSpacing = 1.5f;
            var titleShadow = title.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            titleShadow.effectColor = new Color(.24f, .09f, .43f, .65f);
            titleShadow.effectDistance = new Vector2(0, -7);
            Label(_menu, "Tagline", "LITTLE PAWS. BIG BEATS.", 30, new Color(1, .86f, .97f), 150, 330, 780, 60, TextAlignmentOptions.Center, true);
            Label(_menu, "LeftMusicNote", "♪", 115, new Color(1, .78f, .92f, .8f), 115, 465, 140, 160, TextAlignmentOptions.Center, true);
            Label(_menu, "RightMusicNote", "♫", 105, new Color(.82f, .72f, 1, .85f), 850, 570, 140, 160, TextAlignmentOptions.Center, true);
            At(_menu, "CatPreviewSpace", 270, 390, 540, 350);
            RectTransform name = At(_menu, "CatNamePill", 355, 754, 370, 72);
            Surface(name, new Color(.25f, .12f, .44f, .72f));
            _previewName = Label(name, "CatName", "YOUR DANCE PARTNER", 28, Color.white, 10, 5, 350, 62, TextAlignmentOptions.Center, true);

            _songsPanel = Fill(_menu, "SongsPanel");
            _catsPanel = Fill(_menu, "CatsPanel");
            _rewardsPanel = Fill(_menu, "RewardsPanel");
            BuildSongList();
            BuildCatList();
            BuildRewards();
            RectTransform nav = Bottom(_menu, "BottomNavigation", 32, 25, 32, 155);
            Surface(nav, new Color(.20f, .10f, .35f, .97f));
            float width = (1080 - 64) / 3f;
            string[] names = { "Songs", "Cats", "Rewards" };
            for (int i = 0; i < names.Length; i++)
            {
                string tab = names[i];
                var button = Button(nav, "Tab_" + tab, tab.ToUpperInvariant(), 12 + width * i, 12,
                    width - 24, 131, new Color(0, 0, 0, 0), Color.white, () => { _tab = tab; Refresh(); });
                button.GetComponentInChildren<TMP_Text>().fontSize = 32;
            }
        }

        private void BuildSongList()
        {
            Label(_songsPanel, "SongsHeading", "PICK YOUR NEXT BEAT", 33, Color.white, 60, 868, 820, 65, TextAlignmentOptions.Left, true);
            RectTransform content = Scroll(_songsPanel, "SongScroll", 40, 947, 40, 210);
            CatSong[] songs = m_controller.Songs ?? Array.Empty<CatSong>();
            for (int i = 0; i < songs.Length; i++)
            {
                CatSong song = songs[i];
                if (song == null) continue;
                int index = i;
                RectTransform card = LayoutCard(content, "Song_" + i, 228);
                Surface(card, Paper, true);
                RectTransform art = At(card, "AlbumArt", 24, 28, 172, 172);
                Surface(art, Color.Lerp(song.theme, Color.white, .25f));
                Face(art, "AlbumCat", 25, 31, 122, new Color(1, .91f, .94f));
                Label(card, "SongTitle", song.title, 43, Ink, 220, 28, 520, 62, TextAlignmentOptions.Left, true);
                Label(card, "SongArtist", song.artist, 29, Muted, 222, 92, 490, 44);
                string difficulty = song.difficulty <= 1 ? "EASY" : song.difficulty == 2 ? "NORMAL" : "HARD";
                Label(card, "Difficulty", difficulty + "  ·  " + FormatTime(song.duration), 26, Purple, 222, 143, 300, 42, TextAlignmentOptions.Left, true);
                var row = new SongRow { index = index, song = song, stars = new UnityEngine.UI.Image[3] };
                row.best = Label(card, "BestScore", "BEST 0", 23, Muted, 222, 185, 300, 32);
                for (int s = 0; s < 3; s++) row.stars[s] = Star(card, "BestStar_" + s, 560 + s * 45, 155, 37, Gold);
                Button(card, "SongPlay_" + index, "PLAY", 768, 48, 184, 132, Pink, Color.white, () => m_controller.PlaySong(index));
                _songRows.Add(row);
            }
        }

        private void BuildCatList()
        {
            Label(_catsPanel, "CatsHeading", "MEET YOUR DANCE PARTNER", 33, Color.white, 60, 868, 900, 65, TextAlignmentOptions.Left, true);
            RectTransform content = Scroll(_catsPanel, "CatScroll", 40, 947, 40, 210);
            string[] names = m_controller.SkinNames ?? Array.Empty<string>();
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                RectTransform card = LayoutCard(content, "Skin_" + i, 208);
                Surface(card, Paper, true);
                Color color = m_controller.SkinColors != null && i < m_controller.SkinColors.Length ? m_controller.SkinColors[i] : Color.white;
                RectTransform portrait = At(card, "Portrait", 27, 26, 158, 158);
                Surface(portrait, new Color(.90f, .84f, .99f));
                Face(portrait, "CatFace", 23, 23, 113, color);
                Label(card, "SkinName", names[i], 42, Ink, 220, 38, 490, 62, TextAlignmentOptions.Left, true);
                var row = new SkinRow { index = i };
                row.status = Label(card, "SkinStatus", "", 28, Muted, 222, 112, 470, 55);
                row.button = Button(card, "SkinSelect_" + index, "SELECT", 737, 42, 215, 126, Purple, Color.white,
                    () => m_controller.SelectSkin(index));
                row.action = row.button.GetComponentInChildren<TMP_Text>();
                row.action.fontSize = 29;
                _skinRows.Add(row);
            }
        }

        private void BuildRewards()
        {
            Label(_rewardsPanel, "RewardsHeading", "A LITTLE DAILY TREAT", 33, Color.white, 60, 868, 900, 65, TextAlignmentOptions.Left, true);
            RectTransform card = TopStretch(_rewardsPanel, "DailyRewardCard", 40, 954, 40, 510);
            Surface(card, Paper, true);
            RectTransform gift = At(card, "Gift", 390, 46, 220, 172);
            Surface(gift, Pink);
            Image(At(gift, "RibbonVertical", 90, 0, 40, 172), Gold);
            Image(At(gift, "RibbonHorizontal", 0, 45, 220, 35), Gold);
            _rewardText = Label(card, "DailyMessage", "A gift for your next adventure.", 33, Ink, 45, 250, 910, 75, TextAlignmentOptions.Center, true);
            UnityEngine.UI.Button claim = Button(card, "ClaimDaily", "CLAIM GIFT", 205, 350, 590, 120, Purple, Color.white, m_controller.ClaimDaily);
            _rewardLabel = claim.GetComponentInChildren<TMP_Text>();
        }

        private void BuildHud()
        {
            Button(_hud, "PauseGame", "II", 45, 35, 132, 132, new Color(.20f, .10f, .35f, .9f), Color.white, m_controller.Pause);
            _songText = Label(_hud, "CurrentSong", "", 35, Color.white, 200, 48, 660, 72, TextAlignmentOptions.Center, true);
            _progressText = Label(_hud, "ProgressPercent", "0%", 31, Color.white, 880, 52, 155, 66, TextAlignmentOptions.Center, true);
            RectTransform track = TopStretch(_hud, "ProgressTrack", 62, 194, 62, 18);
            Surface(track, new Color(1, 1, 1, .22f));
            _progressFill = Image(Fill(track, "ProgressFill"), Gold);
            _progressFill.sprite = _rounded;
            _progressFill.type = UnityEngine.UI.Image.Type.Filled;
            _progressFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _progressFill.fillOrigin = 0;
            _scoreText = Label(_hud, "Score", "0", 116, Color.white, 170, 250, 740, 150, TextAlignmentOptions.Center, true);
            var scoreShadow = _scoreText.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            scoreShadow.effectColor = new Color(.16f, .06f, .30f, .65f);
            scoreShadow.effectDistance = new Vector2(0, -5);
            _comboText = Label(_hud, "Combo", "FIND YOUR RHYTHM", 38, new Color(1, .88f, .44f), 170, 404, 740, 72, TextAlignmentOptions.Center, true);
            RectTransform feedback = Bottom(_hud, "FeedbackArea", 95, 355, 95, 115);
            _feedbackText = Label(feedback, "HitFeedback", "", 63, Color.white, 0, 0, 890, 110, TextAlignmentOptions.Center, true);
        }

        private void BuildReady()
        {
            _ready = Fill(_safe, "ReadyPanel");
            RectTransform card = Bottom(_ready, "InstructionCard", 60, 105, 60, 570);
            Surface(card, new Color(.21f, .10f, .37f, .97f), true);
            _readyTitle = Label(card, "InstructionTitle", "HOLD & DRAG", 63, Color.white, 40, 48, 880, 104, TextAlignmentOptions.Center, true);
            Label(card, "Instruction", "Move left and right.\nLand on the beat. Keep the music going!", 34,
                new Color(.88f, .81f, 1f), 60, 164, 840, 116, TextAlignmentOptions.Center);
            _readySong = Label(card, "ReadySong", "", 30, Gold, 60, 290, 840, 64, TextAlignmentOptions.Center, true);
            Button(card, "BeginRun", "GO!", 220, 395, 520, 130, Pink, Color.white, () =>
            {
                _goPressed = true;
                _buttons["BeginRun"].gameObject.SetActive(false);
                m_controller.BeginRun();
            });
        }

        private void BuildPause()
        {
            _pause = Modal("PausePanel");
            RectTransform card = ModalCard(_pause, "PauseCard", 850);
            Label(card, "PauseTitle", "PAWS FOR A MOMENT", 58, Ink, 45, 72, 810, 132, TextAlignmentOptions.Center, true);
            Label(card, "PauseMessage", "Your next beat is waiting.", 33, Muted, 45, 222, 810, 72, TextAlignmentOptions.Center);
            Button(card, "ResumeGame", "KEEP DANCING", 105, 355, 690, 132, Pink, Color.white, m_controller.Resume);
            Button(card, "PauseRestart", "START AGAIN", 105, 520, 690, 120, Purple, Color.white, m_controller.RestartSong);
            Button(card, "PauseHome", "BACK TO SONGS", 105, 678, 690, 110, new Color(.90f, .85f, .98f), Ink, m_controller.Home);
        }

        private void BuildFailure()
        {
            _failure = Modal("FailedPanel");
            RectTransform card = ModalCard(_failure, "FailedCard", 1010);
            Face(card, "SadCatPortrait", 355, 48, 190, new Color(1, .85f, .90f));
            Label(card, "FailedTitle", "ONE MORE BEAT?", 62, Ink, 45, 270, 810, 110, TextAlignmentOptions.Center, true);
            _failedStats = Label(card, "FailedStats", "", 34, Muted, 45, 395, 810, 110, TextAlignmentOptions.Center);
            UnityEngine.UI.Button revive = Button(card, "Revive", "", 105, 550, 690, 128, Pink, Color.white, m_controller.Revive);
            _reviveLabel = revive.GetComponentInChildren<TMP_Text>();
            _reviveLabel.fontSize = 34;
            Button(card, "RetrySong", "TRY AGAIN", 105, 708, 690, 120, Purple, Color.white, m_controller.RestartSong);
            Button(card, "FailHome", "BACK TO SONGS", 105, 860, 690, 105, new Color(.90f, .85f, .98f), Ink, m_controller.Home);
        }

        private void BuildResults()
        {
            _results = Modal("ResultsPanel");
            RectTransform card = ModalCard(_results, "ResultsCard", 1080);
            _resultTitle = Label(card, "ResultTitle", "PURRFECT RUN!", 67, Ink, 45, 62, 810, 110, TextAlignmentOptions.Center, true);
            for (int i = 0; i < 3; i++) _resultStars[i] = Star(card, "ResultStar_" + i, 205 + 170 * i, i == 1 ? 195 : 224, 150, Gold);
            _resultStats = Label(card, "ResultStats", "", 42, Ink, 65, 445, 770, 120, TextAlignmentOptions.Center, true);
            _resultCoins = Label(card, "EarnedCoins", "", 38, Purple, 90, 587, 720, 72, TextAlignmentOptions.Center, true);
            Button(card, "NextSong", "NEXT SONG", 105, 706, 690, 128, Pink, Color.white, PlayNextSong);
            Button(card, "ResultRetry", "PLAY AGAIN", 105, 862, 330, 120, Purple, Color.white, m_controller.RestartSong);
            Button(card, "ResultHome", "SONGS", 465, 862, 330, 120, new Color(.90f, .85f, .98f), Ink, m_controller.Home);
        }

        private void BuildSettings()
        {
            _settings = Modal("SettingsPanel");
            RectTransform card = ModalCard(_settings, "SettingsCard", 770);
            Label(card, "SettingsTitle", "MAKE IT YOURS", 58, Ink, 45, 72, 810, 100, TextAlignmentOptions.Center, true);
            Label(card, "VolumeTitle", "MUSIC VOLUME", 32, Purple, 90, 250, 720, 60, TextAlignmentOptions.Left, true);
            _volumeText = Label(card, "VolumePercent", "100%", 32, Muted, 600, 250, 210, 60, TextAlignmentOptions.Right, true);
            RectTransform sliderRoot = At(card, "MusicVolume", 95, 352, 710, 112);
            _volume = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();
            Image(sliderRoot, new Color(0, 0, 0, 0), true);
            _volume.minValue = 0;
            _volume.maxValue = 1;
            RectTransform rail = At(sliderRoot, "Rail", 0, 42, 710, 28);
            Surface(rail, new Color(.84f, .77f, .95f));
            RectTransform handleArea = Fill(sliderRoot, "HandleSlideArea");
            handleArea.offsetMin = new Vector2(42, 0);
            handleArea.offsetMax = new Vector2(-42, 0);
            RectTransform handle = At(handleArea, "Handle", 0, 56, 84, 84);
            handle.anchorMin = handle.anchorMax = new Vector2(0, .5f);
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero;
            UnityEngine.UI.Image graphic = Image(handle, Pink, true);
            graphic.sprite = _circle;
            _volume.handleRect = handle;
            _volume.targetGraphic = graphic;
            _volume.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            _volume.onValueChanged.AddListener(value =>
            {
                m_controller.SetVolume(value);
                _volumeText.text = Mathf.RoundToInt(value * 100) + "%";
            });
            Button(card, "CloseSettings", "ALL SET!", 160, 562, 580, 128, Purple, Color.white,
                () => { _settingsOpen = false; Refresh(); });
        }

        private void PlayNextSong()
        {
            CatSong[] songs = m_controller.Songs;
            if (songs == null || songs.Length == 0) return;
            int index = Array.IndexOf(songs, m_controller.SelectedSong);
            m_controller.PlaySong((index + 1) % songs.Length);
        }

        private void Refresh()
        {
            if (!_built || m_controller == null) return;
            CatGamePhase phase = m_controller.Phase;
            if (phase != _shownPhase)
            {
                _shownPhase = phase;
                if (phase == CatGamePhase.Countdown)
                {
                    _goPressed = false;
                    _readyTitle.text = "HOLD & DRAG";
                    _buttons["BeginRun"].gameObject.SetActive(true);
                }
                if (phase != CatGamePhase.MainMenu) _settingsOpen = false;
            }
            bool menu = phase == CatGamePhase.MainMenu;
            _menu.gameObject.SetActive(menu);
            _songsPanel.gameObject.SetActive(_tab == "Songs");
            _catsPanel.gameObject.SetActive(_tab == "Cats");
            _rewardsPanel.gameObject.SetActive(_tab == "Rewards");
            _hud.gameObject.SetActive(phase == CatGamePhase.Playing || phase == CatGamePhase.Countdown || phase == CatGamePhase.Paused);
            _ready.gameObject.SetActive(phase == CatGamePhase.Countdown);
            _pause.gameObject.SetActive(phase == CatGamePhase.Paused);
            _failure.gameObject.SetActive(phase == CatGamePhase.Failed);
            _results.gameObject.SetActive(phase == CatGamePhase.Complete);
            _settings.gameObject.SetActive(_settingsOpen && menu);
            _buttons["PauseGame"].gameObject.SetActive(phase == CatGamePhase.Playing);
            _coinText.text = m_controller.Coins.ToString();
            string[] names = m_controller.SkinNames;
            int skin = m_controller.SelectedSkin;
            _previewName.text = names != null && skin >= 0 && skin < names.Length ? names[skin].ToUpperInvariant() : "YOUR DANCE PARTNER";
            _songText.text = m_controller.SelectedSong != null ? m_controller.SelectedSong.title : "";
            _readySong.text = _songText.text;
            _failedStats.text = "SCORE  " + m_controller.Score + "  ·  BEST x" + m_controller.BestCombo + "\nWALLET  " + m_controller.Coins + " COINS";
            _reviveLabel.text = "REVIVE  ·  " + m_controller.ReviveCost + " COINS";
            _buttons["Revive"].interactable = m_controller.CanRevive;
            _resultTitle.text = m_controller.Stars >= 3 ? "PURRFECT RUN!" : "NICE DANCING!";
            _resultStats.text = "SCORE  " + m_controller.Score + "\nBEST COMBO  x" + m_controller.BestCombo;
            _resultCoins.text = "+" + m_controller.EarnedCoins + " COINS EARNED";
            if (m_controller.SelectedSong != null && m_controller.Songs.Length > 0)
            {
                int next = (Array.IndexOf(m_controller.Songs, m_controller.SelectedSong) + 1) % m_controller.Songs.Length;
                bool unlocked = m_controller.IsSongUnlocked(next);
                _buttons["NextSong"].GetComponentInChildren<TMP_Text>().text = unlocked ? "NEXT SONG" : "UNLOCK NEXT  ·  " + m_controller.SongUnlockCost(next) + " COINS";
                _buttons["NextSong"].interactable = unlocked || m_controller.Coins >= m_controller.SongUnlockCost(next);
            }
            for (int i = 0; i < 3; i++) _resultStars[i].color = i < m_controller.Stars ? Gold : new Color(.84f, .79f, .90f);
            foreach (SongRow row in _songRows)
            {
                bool unlocked = m_controller.IsSongUnlocked(row.index);
                var play = _buttons["SongPlay_" + row.index];
                play.GetComponentInChildren<TMP_Text>().text = unlocked ? "PLAY" : "OPEN\n" + m_controller.SongUnlockCost(row.index) + " C";
                play.interactable = unlocked || m_controller.Coins >= m_controller.SongUnlockCost(row.index);
                row.best.text = "BEST " + m_controller.GetBestScore(row.song);
                int bestStars = m_controller.GetBestStars(row.song);
                for (int i = 0; i < 3; i++) row.stars[i].color = i < bestStars ? Gold : new Color(.82f, .77f, .89f);
            }
            foreach (SkinRow row in _skinRows)
            {
                bool owned = m_controller.IsSkinOwned(row.index);
                bool selected = skin == row.index;
                int price = m_controller.SkinPrices != null && row.index < m_controller.SkinPrices.Length ? m_controller.SkinPrices[row.index] : 0;
                row.status.text = selected ? "Ready to dance" : owned ? "In your cat family" : price + " coins to adopt";
                row.action.text = selected ? "SELECTED" : owned ? "SELECT" : "ADOPT " + price;
                row.button.interactable = !selected && (owned || m_controller.Coins >= price);
                row.button.GetComponent<UnityEngine.UI.Image>().color = selected ? new Color(.75f, .64f, .89f) : Purple;
            }
            bool daily = m_controller.CanClaimDaily;
            _buttons["ClaimDaily"].interactable = daily;
            _rewardText.text = daily ? "DAY " + (m_controller.DailyStreak % 7 + 1) + "  ·  +" + m_controller.DailyReward + " COINS" : "Today's treat is in your pocket.\nCome back tomorrow!";
            _rewardLabel.text = daily ? "CLAIM " + m_controller.DailyReward + " COINS" : "CLAIMED TODAY";
            _volume.SetValueWithoutNotify(m_controller.MusicVolume);
            _volumeText.text = Mathf.RoundToInt(m_controller.MusicVolume * 100) + "%";
            foreach (string tab in new[] { "Songs", "Cats", "Rewards" })
                _buttons["Tab_" + tab].GetComponent<UnityEngine.UI.Image>().color = _tab == tab ? Pink : new Color(0, 0, 0, 0);
        }

        private RectTransform Scroll(Transform parent, string name, float left, float top, float right, float bottom)
        {
            RectTransform root = Fill(parent, name);
            root.offsetMin = new Vector2(left, bottom);
            root.offsetMax = new Vector2(-right, -top);
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 38;
            scroll.decelerationRate = .12f;
            RectTransform viewport = Fill(root, "Viewport");
            UnityEngine.UI.Image viewportImage = Image(viewport, Color.white, true);
            viewportImage.sprite = _rounded;
            viewportImage.type = UnityEngine.UI.Image.Type.Sliced;
            var mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;
            RectTransform content = Fill(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 22;
            layout.padding = new RectOffset(0, 0, 0, 18);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        private RectTransform LayoutCard(Transform parent, string name, float height)
        {
            RectTransform card = Fill(parent, name);
            var element = card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0;
            return card;
        }

        private RectTransform Modal(string name)
        {
            RectTransform root = Fill(_safe, name);
            Image(root, new Color(.10f, .04f, .23f, .75f), true);
            return root;
        }

        private RectTransform ModalCard(Transform parent, string name, float height)
        {
            RectTransform card = At(parent, name, 0, 0, 900, height);
            card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
            card.pivot = new Vector2(.5f, .5f);
            card.anchoredPosition = Vector2.zero;
            Surface(card, Paper, true);
            return card;
        }

        private UnityEngine.UI.Button Button(Transform parent, string name, string text, float x, float y,
            float width, float height, Color background, Color foreground, Action action)
        {
            RectTransform rect = At(parent, name, x, y, width, height);
            UnityEngine.UI.Image image = Surface(rect, background);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, .94f, 1);
            colors.pressedColor = new Color(.83f, .72f, .92f);
            colors.disabledColor = new Color(.78f, .75f, .83f, .75f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() => action?.Invoke());
            TMP_Text label = Label(rect, "Label", text, 38, foreground, 8, 6, width - 16, height - 12,
                TextAlignmentOptions.Center, true);
            label.enableAutoSizing = true;
            label.fontSizeMin = 25;
            label.fontSizeMax = 38;
            _buttons[name] = button;
            return button;
        }

        private TMP_Text Label(Transform parent, string name, string value, float size, Color color,
            float x, float y, float width, float height, TextAlignmentOptions alignment = TextAlignmentOptions.Left, bool bold = false)
        {
            RectTransform rect = At(parent, name, x, y, width, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value ?? "";
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            return text;
        }

        private UnityEngine.UI.Image Surface(RectTransform rect, Color color, bool shadow = false)
        {
            UnityEngine.UI.Image image = Image(rect, color);
            image.sprite = _rounded;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            if (shadow)
            {
                var effect = rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();
                effect.effectColor = new Color(.12f, .04f, .25f, .20f);
                effect.effectDistance = new Vector2(0, -8);
            }
            return image;
        }

        private static UnityEngine.UI.Image Image(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private UnityEngine.UI.Image Disc(Transform parent, string name, float x, float y, float size, Color color)
        {
            UnityEngine.UI.Image image = Image(At(parent, name, x, y, size, size), color);
            image.sprite = _circle;
            return image;
        }

        private UnityEngine.UI.Image Star(Transform parent, string name, float x, float y, float size, Color color)
        {
            UnityEngine.UI.Image image = Image(At(parent, name, x, y, size, size), color);
            image.sprite = _star;
            return image;
        }

        private void Face(Transform parent, string name, float x, float y, float size, Color fur)
        {
            RectTransform face = At(parent, name, x, y, size, size);
            UnityEngine.UI.Image earLeft = Image(At(face, "LeftEar", 6, 0, size * .36f, size * .48f), fur);
            earLeft.sprite = _triangle;
            earLeft.rectTransform.localEulerAngles = new Vector3(0, 0, 14);
            UnityEngine.UI.Image earRight = Image(At(face, "RightEar", size * .60f, 0, size * .36f, size * .48f), fur);
            earRight.sprite = _triangle;
            earRight.rectTransform.localEulerAngles = new Vector3(0, 0, -14);
            var head = Image(At(face, "Head", 0, size * .21f, size, size * .79f), fur);
            head.sprite = _circle;
            Disc(face, "LeftEye", size * .27f, size * .54f, size * .083f, Ink);
            Disc(face, "RightEye", size * .66f, size * .54f, size * .083f, Ink);
            Disc(face, "Nose", size * .46f, size * .71f, size * .09f, Pink);
            Disc(face, "LeftCheek", size * .12f, size * .69f, size * .17f, new Color(1, .58f, .70f, .65f));
            Disc(face, "RightCheek", size * .75f, size * .69f, size * .17f, new Color(1, .58f, .70f, .65f));
        }

        private Sprite Shape(string name, int width, int height, Func<float, float, float> alpha, Vector4 border = default)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "CatUI_" + name, hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var colors = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) colors[y * width + x] = new Color(1, 1, 1, alpha(x, y));
            texture.SetPixels(colors);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSave;
            _ownedTextures.Add(texture);
            _ownedSprites.Add(sprite);
            return sprite;
        }

        private static float StarAlpha(float x, float y)
        {
            float dx = x - 39.5f;
            float dy = y - 39.5f;
            float angle = Mathf.Atan2(dy, dx) - Mathf.PI * .5f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float period = Mathf.PI * .4f;
            float sector = Mathf.Repeat(angle + period * .5f, period) - period * .5f;
            float folded = Mathf.Abs(sector);
            // A radial five-point polygon with a slightly softened edge.
            float inner = 17f;
            float outer = 37f;
            float a = period * .5f;
            float boundary = outer * inner * Mathf.Sin(a) /
                (inner * Mathf.Sin(a - folded) + outer * Mathf.Sin(folded));
            return Mathf.Clamp01(boundary - radius);
        }

        private static RectTransform Fill(Transform parent, string name)
        {
            RectTransform rect = NewRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform At(Transform parent, string name, float x, float y, float width, float height)
        {
            RectTransform rect = NewRect(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform TopStretch(Transform parent, string name, float left, float top, float right, float height)
        {
            RectTransform rect = Fill(parent, name);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        private static RectTransform Bottom(Transform parent, string name, float left, float bottom, float right, float height)
        {
            RectTransform rect = Fill(parent, name);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
            return rect;
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static string FormatTime(float seconds)
        {
            int value = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return value / 60 + ":" + (value % 60).ToString("00");
        }
    }
}
