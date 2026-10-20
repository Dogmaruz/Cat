using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace DancingCat
{
    [DefaultExecutionOrder(50), DisallowMultipleComponent]
    [RequireComponent(typeof(CatGameController), typeof(CatMetaProgress))]
    public sealed class CatExtraUI : MonoBehaviour
    {
        private static readonly Color Purple = new Color(.49f, .29f, .86f);
        private static readonly Color Pink = new Color(1f, .32f, .64f);
        private static readonly Color Ink = new Color(.22f, .12f, .36f);
        private static readonly Color Muted = new Color(.48f, .4f, .59f);
        private static readonly Color Paper = new Color(.98f, .96f, 1);
        private static readonly Color Gold = new Color(1f, .77f, .2f);
        private readonly Dictionary<string, RectTransform> _panels = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, UnityEngine.UI.Button> _buttons = new Dictionary<string, UnityEngine.UI.Button>();
        private readonly Dictionary<string, TMP_Text> _labels = new Dictionary<string, TMP_Text>();
        private readonly UnityEngine.UI.Image[,] _roomSteps = new UnityEngine.UI.Image[4, 5];
        private CatGameController _controller;
        private CatMetaProgress _meta;
        private Canvas _canvas;
        private RectTransform _safe;
        private TMP_FontAsset _font;
        private Sprite _rounded;
        private Texture2D _texture;
        private UnityEngine.UI.Slider _latency;
        private string _page = "Hub";
        private bool _open;
        private bool _built;
        private Rect _safeArea;
        private Vector2Int _screen;
        private int _lastDiceRemaining = -1;

        private void Start()
        {
            _controller = GetComponent<CatGameController>();
            _meta = GetComponent<CatMetaProgress>();
            _font = TMP_Settings.defaultFontAsset;
            if (_font == null) _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            Build();
            _controller.Changed += Refresh;
            _meta.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.Changed -= Refresh;
            if (_meta != null) _meta.Changed -= Refresh;
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_rounded != null) Destroy(_rounded);
            if (_texture != null) Destroy(_texture);
        }

        private void Update() { if (_built) ApplySafeArea(); }

        private void Build()
        {
            MakeRoundedSprite();
            var root = new GameObject("DancingCatExtrasCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.layer = LayerMask.NameToLayer("UI");
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0;
            _safe = Fill(root.transform, "ExtrasSafeArea");
            ApplySafeArea();
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var system = new GameObject("DancingCatExtrasEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                system.transform.SetParent(transform, false);
                system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            Button(_safe, "ExtraMenuButton", "MORE", 640, 44, 220, 96, Purple, () => Open("Hub"));
            BuildHub();
            BuildChallenge();
            BuildHouse();
            BuildTrails();
            BuildDice();
            BuildOptions();
            _built = true;
        }

        public void Open(string page = "Hub")
        {
            if (_controller.Phase != CatGamePhase.MainMenu || !_panels.ContainsKey(page)) return;
            _page = page;
            _open = true;
            Refresh();
        }

        public void Close() { _open = false; Refresh(); }

        private void BuildHub()
        {
            var content = Panel("Hub", "MORE WAYS TO PURR", "Little goals. A cozier home. Your own sparkle.");
            string[] pages = { "Challenge", "House", "Trails", "Dice", "Options" };
            string[] titles = { "CHALLENGE", "CAT HOUSE", "TRAILS", "LUCKY DICE", "PLAY OPTIONS" };
            string[] details = { "Today's song and your seven-day streak", "Make four rooms feel like home", "Ten colors to leave a little magic", "Spend 10 coins, roll a local reward", "Mode, difficulty and audio calibration" };
            for (var index = 0; index < pages.Length; index++)
            {
                var page = pages[index];
                var card = Card(content, "ExtraHub_" + page, 220);
                Label(card, "HubTitle_" + page, titles[index], 44, Ink, 40, 30, 680, 60, true);
                Label(card, "HubDetail_" + page, details[index], 31, Muted, 40, 105, 680, 70);
                Button(card, "OpenExtra_" + page, ">", 800, 50, 140, 120, index % 2 == 0 ? Purple : Pink, () => Open(page));
            }
        }

        private void BuildChallenge()
        {
            var content = Panel("Challenge", "DAILY CHALLENGE", "Finish today's selected song, then collect 75 coins.");
            var daily = Card(content, "StreakCard", 310);
            Label(daily, "StreakTitle", "YOUR SEVEN-DAY STREAK", 40, Ink, 35, 25, 900, 60, true);
            Label(daily, "StreakStatus", "", 30, Muted, 35, 87, 900, 60);
            for (var index = 0; index < 7; index++)
            {
                var chip = Box(daily, "StreakDay_" + index, 35 + index * 133, 170, 120, 104, new Color(.9f, .83f, 1));
                Label(chip, "StreakDayLabel_" + index, "DAY " + (index + 1) + "\n" + CatMetaRules.DailyRewards[index], 27, Ink, 6, 9, 108, 86, true, true);
            }
            var challenge = Card(content, "ChallengeCard", 470);
            Label(challenge, "ChallengeSong", "", 48, Ink, 40, 40, 900, 100, true, true);
            Label(challenge, "ChallengeStatus", "", 34, Muted, 40, 160, 900, 100, false, true);
            Button(challenge, "ChallengePlay", "PLAY TODAY'S SONG", 40, 310, 550, 112, Purple, () =>
            {
                var index = Array.IndexOf(_controller.Songs, _meta.ChallengeSong);
                if (index < 0) return;
                if (!_controller.IsSongUnlocked(index)) { _controller.UnlockSong(index); Refresh(); return; }
                ApplyOption(() => _controller.Endless = false);
                Close();
                _controller.PlaySong(index);
            });
            Button(challenge, "ChallengeClaim", "CLAIM 75", 620, 310, 320, 112, Pink, () => _meta.ClaimChallenge());
        }

        private void BuildHouse()
        {
            var content = Panel("House", "CAT HOUSE", "Turn coins from your songs into a cozy place to nap.");
            string[] rooms = { "COZY LOUNGE", "PLAY ROOM", "MOON BEDROOM", "SUNNY KITCHEN" };
            for (var index = 0; index < 4; index++)
            {
                var room = index;
                var card = Card(content, "HouseRoom_" + room, 285);
                Label(card, "RoomTitle_" + room, rooms[room], 40, Ink, 35, 28, 650, 58, true);
                Label(card, "RoomStatus_" + room, "", 31, Muted, 35, 100, 600, 54);
                for (var step = 0; step < 5; step++)
                    _roomSteps[room, step] = Image(Box(card, "RoomDecoration_" + room + "_" + step,
                        35 + step * 113, 184, 95, 58, Purple));
                Button(card, "RoomUpgrade_" + room, "", 665, 130, 275, 112, Purple, () => _meta.TryUpgradeRoom(room));
            }
        }

        private void BuildTrails()
        {
            var content = Panel("Trails", "A LITTLE SPARKLE", "Unlock a color once, then wear it on every song.");
            for (var row = 0; row < 5; row++)
            {
                var card = Card(content, "TrailRow_" + row, 240);
                for (var column = 0; column < 2; column++)
                {
                    var trail = row * 2 + column;
                    var x = column * 500f;
                    Box(card, "TrailSwatch_" + trail, x + 30, 32, 75, 75, CatMetaProgress.TrailColors[trail]);
                    Label(card, "TrailName_" + trail, CatMetaProgress.TrailNames[trail], 31, Ink, x + 125, 32, 345, 75, true);
                    Button(card, "TrailSelect_" + trail, "", x + 30, 135, 440, 80, Purple, () =>
                    {
                        if (_meta.IsTrailOwned(trail) || _meta.TryUnlockTrail(trail)) _meta.SelectTrail(trail);
                    });
                }
            }
        }

        private void BuildDice()
        {
            var content = Panel("Dice", "LUCKY DICE", "Three rolls per day. Every roll costs 10 coins.");
            var card = Card(content, "LuckyDiceCard", 680);
            var face = Box(card, "DiceFace", 355, 50, 290, 290, new Color(.91f, .82f, 1));
            Label(face, "DiceFaceNumber", "?", 150, Purple, 15, 10, 260, 260, true, true);
            Label(card, "DiceResult", "Ready for a little luck?", 42, Ink, 40, 375, 920, 100, true, true);
            Label(card, "DiceAttempts", "", 30, Purple, 40, 475, 920, 45, true, true);
            Button(card, "DiceRoll", "ROLL - 10 COINS", 200, 525, 600, 112, Pink, () =>
            {
                var reward = _meta.RollDice();
                if (reward >= 0)
                {
                    _labels["DiceResult"].text = reward > 0 ? "+" + reward + " COINS!" : "No prize this time.";
                    _labels["DiceFaceNumber"].text = (Array.IndexOf(CatMetaRules.DiceRewards, reward) + 1).ToString();
                }
            });
            var info = Card(content, "DiceRewardsCard", 240);
            Label(info, "DiceRewards", "SIX EQUALLY LIKELY REWARDS\n0 / 5 / 10 / 15 / 25 / 50 COINS", 34, Ink, 40, 40, 920, 150, false, true);
        }

        private void BuildOptions()
        {
            var content = Panel("Options", "PLAY YOUR WAY", "Your timing, your challenge, your dance.");
            var mode = Card(content, "ModeCard", 220);
            Label(mode, "ModeTitle", "GAME MODE", 36, Ink, 35, 20, 900, 60, true);
            Button(mode, "ModeNormal", "NORMAL", 35, 105, 440, 80, Purple, () => ApplyOption(() => _controller.Endless = false));
            Button(mode, "ModeEndless", "ENDLESS", 525, 105, 440, 80, Purple, () => ApplyOption(() => _controller.Endless = true));
            var difficulty = Card(content, "DifficultyCard", 220);
            Label(difficulty, "DifficultyTitle", "CHALLENGE", 36, Ink, 35, 20, 900, 60, true);
            string[] names = { "NORMAL", "HARD", "EXPERT" };
            for (var index = 0; index < 3; index++)
            {
                var selected = index;
                Button(difficulty, "Difficulty_" + index, names[index], 35 + index * 318, 105, 295, 80,
                    Purple, () => ApplyOption(() => _controller.Difficulty = selected));
            }
            var latency = Card(content, "LatencyCard", 285);
            Label(latency, "LatencyTitle", "AUDIO CALIBRATION", 36, Ink, 35, 20, 900, 60, true);
            Label(latency, "LatencyValue", "", 32, Muted, 35, 83, 900, 55, false, true);
            var sliderRoot = At(latency, "LatencySlider", 70, 175, 860, 80);
            _latency = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();
            Box(sliderRoot, "LatencyRail", 0, 25, 860, 28, new Color(.83f, .73f, .96f));
            var handleArea = Fill(sliderRoot, "LatencyHandleArea");
            handleArea.offsetMin = new Vector2(40, 0);
            handleArea.offsetMax = new Vector2(-40, 0);
            var handle = At(handleArea, "LatencyHandle", 0, 0, 80, 80);
            handle.anchorMin = handle.anchorMax = new Vector2(0, .5f);
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero;
            var graphic = Surface(handle, Pink, true);
            _latency.targetGraphic = graphic;
            _latency.handleRect = handle;
            _latency.minValue = -200;
            _latency.maxValue = 200;
            _latency.wholeNumbers = true;
            _latency.onValueChanged.AddListener(value => ApplyOption(() => _controller.AudioLatencyMs = value));
            Surface(sliderRoot, new Color(0, 0, 0, 0), true);
            var feedback = Card(content, "FeedbackCard", 160);
            Label(feedback, "VibrationTitle", "TOUCH FEEDBACK", 34, Ink, 35, 45, 540, 70, true);
            Button(feedback, "VibrationToggle", "", 610, 35, 355, 90, Purple, () => ApplyOption(() => _controller.Vibration = !_controller.Vibration));
            var frames = Card(content, "FrameRateCard", 220);
            Label(frames, "FrameRateTitle", "FRAME RATE", 36, Ink, 35, 20, 900, 60, true);
            var rates = new[] { 30, 60, 120 };
            for (var index = 0; index < rates.Length; index++)
            {
                var rate = rates[index];
                Button(frames, "Fps_" + rate, rate + " FPS", 35 + index * 318, 105, 295, 80, Purple,
                    () => ApplyOption(() => _controller.TargetFps = rate));
            }
        }

        private void Refresh()
        {
            if (!_built) return;
            var menu = _controller.Phase == CatGamePhase.MainMenu;
            if (!menu) _open = false;
            _buttons["ExtraMenuButton"].gameObject.SetActive(menu && !_open);
            foreach (var panel in _panels) panel.Value.gameObject.SetActive(menu && _open && panel.Key == _page);
            foreach (var panel in _panels) _labels[panel.Key + "Wallet"].text = _controller.Coins + " COINS";
            _labels["StreakStatus"].text = "DAY " + _meta.DailyStreak + " / 7  -  NEXT GIFT " + _meta.NextDailyReward + " COINS";
            for (var index = 0; index < 7; index++)
                _labels["StreakDayLabel_" + index].transform.parent.GetComponent<UnityEngine.UI.Image>().color =
                    index < _meta.DailyStreak ? Gold : new Color(.9f, .83f, 1);
            _labels["ChallengeSong"].text = _meta.ChallengeSong != null ? _meta.ChallengeSong.title : "No song available";
            _labels["ChallengeStatus"].text = _meta.CanClaimChallenge ? "You did it! Your 75 coins are ready." :
                _meta.ChallengeComplete ? "Today's prize is already in your pocket." : "Complete this song today.\nPast personal bests do not count.";
            var challengeIndex = Array.IndexOf(_controller.Songs, _meta.ChallengeSong);
            var challengeUnlocked = challengeIndex >= 0 && _controller.IsSongUnlocked(challengeIndex);
            var challengePrice = challengeIndex >= 0 ? _controller.SongUnlockCost(challengeIndex) : 0;
            _labels["ChallengePlayLabel"].text = challengeUnlocked ? "PLAY TODAY'S SONG" : "UNLOCK " + challengePrice;
            _buttons["ChallengePlay"].interactable = challengeIndex >= 0 && (challengeUnlocked || _controller.Coins >= challengePrice);
            _buttons["ChallengeClaim"].interactable = _meta.CanClaimChallenge;
            for (var room = 0; room < 4; room++)
            {
                var level = _meta.RoomLevel(room);
                var price = _meta.RoomUpgradePrice(room);
                _labels["RoomStatus_" + room].text = "COZY LEVEL " + level + " / 5";
                _labels["RoomUpgrade_" + room + "Label"].text = level >= 5 ? "ALL COZY!" : "UPGRADE " + price;
                _buttons["RoomUpgrade_" + room].interactable = price > 0 && _controller.Coins >= price;
                for (var step = 0; step < 5; step++) _roomSteps[room, step].color = step < level ? Pink : new Color(.85f, .8f, .93f);
            }
            for (var trail = 0; trail < 10; trail++)
            {
                var owned = _meta.IsTrailOwned(trail);
                var selected = _meta.SelectedTrail == trail;
                _labels["TrailSelect_" + trail + "Label"].text = selected ? "SELECTED" : owned ? "WEAR IT" : "UNLOCK " + _meta.TrailPrice(trail);
                _buttons["TrailSelect_" + trail].interactable = !selected && (owned || _controller.Coins >= _meta.TrailPrice(trail));
                Tint("TrailSelect_" + trail, selected);
            }
            var remaining = _meta.DiceRollsRemaining;
            _labels["DiceAttempts"].text = remaining + " / 3 ROLLS LEFT TODAY";
            _labels["DiceRollLabel"].text = remaining > 0 ? "ROLL - 10 COINS" : "BACK TOMORROW";
            _buttons["DiceRoll"].interactable = remaining > 0 && _controller.Coins >= 10;
            if (remaining == 0) _labels["DiceResult"].text = "Today's dice are resting.\nCome back tomorrow!";
            else if (_lastDiceRemaining == 0)
            {
                _labels["DiceResult"].text = "Ready for a little luck?";
                _labels["DiceFaceNumber"].text = "?";
            }
            _lastDiceRemaining = remaining;
            _latency.SetValueWithoutNotify(_controller.AudioLatencyMs);
            var value = Mathf.RoundToInt(_controller.AudioLatencyMs);
            _labels["LatencyValue"].text = (value > 0 ? "+" : "") + value + " ms  (-200 to +200)";
            _labels["VibrationToggleLabel"].text = _controller.Vibration ? "VIBRATION ON" : "VIBRATION OFF";
            Tint("VibrationToggle", _controller.Vibration);
            Tint("ModeNormal", !_controller.Endless);
            Tint("ModeEndless", _controller.Endless);
            for (var index = 0; index < 3; index++) Tint("Difficulty_" + index, _controller.Difficulty == index);
            foreach (var fps in new[] { 30, 60, 120 }) Tint("Fps_" + fps, _controller.TargetFps == fps);
        }

        private void Tint(string name, bool selected) => _buttons[name].GetComponent<UnityEngine.UI.Image>().color = selected ? Pink : Purple;
        private void ApplyOption(Action change) { change(); _meta.SavePlayOptions(); Refresh(); }

        private RectTransform Panel(string page, string title, string subtitle)
        {
            var panelName = page == "Hub" ? "ExtraHubPanel" : page == "Challenge" ? "DailyChallengePanel" :
                page == "House" ? "CatHousePanel" : page == "Dice" ? "LuckyDicePanel" : page == "Options" ? "PlayOptionsPanel" : page + "Panel";
            var panel = Fill(_safe, panelName);
            Surface(panel, new Color(.19f, .09f, .35f, .985f), true);
            _panels.Add(page, panel);
            Button(panel, page + "Back", "< BACK", 50, 45, 235, 96, Purple, () => { if (page == "Hub") Close(); else Open("Hub"); });
            Label(panel, page + "Wallet", "", 34, Gold, 630, 60, 400, 70, true);
            Label(panel, page + "Title", title, 53, Color.white, 50, 175, 980, 90, true, true);
            Label(panel, page + "Subtitle", subtitle, 30, new Color(.84f, .76f, .95f), 70, 265, 940, 95, false, true);

            var scrollRoot = Fill(panel, page + "Scroll");
            scrollRoot.offsetMin = new Vector2(40, 55);
            scrollRoot.offsetMax = new Vector2(-40, -385);
            var scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            var viewport = Fill(scrollRoot, "Viewport");
            Surface(viewport, Color.white, true);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Fill(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 26;
            layout.padding = new RectOffset(0, 0, 0, 30);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        private RectTransform Card(Transform parent, string name, float height)
        {
            var card = Fill(parent, name);
            Surface(card, Paper);
            var size = card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.minHeight = height;
            size.preferredHeight = height;
            size.flexibleHeight = 0;
            return card;
        }

        private UnityEngine.UI.Button Button(Transform parent, string name, string text, float x, float y,
            float width, float height, Color color, Action action)
        {
            var rect = At(parent, name, x, y, width, height);
            var graphic = Surface(rect, color, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = graphic;
            var colors = button.colors;
            colors.highlightedColor = new Color(1, .92f, 1);
            colors.pressedColor = new Color(.8f, .7f, .9f);
            colors.disabledColor = new Color(.8f, .78f, .86f, .65f);
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() => action?.Invoke());
            Label(rect, name + "Label", text, 35, Color.white, 8, 5, width - 16, height - 10, true, true);
            _buttons.Add(name, button);
            return button;
        }

        private TMP_Text Label(Transform parent, string name, string value, float size, Color color,
            float x, float y, float width, float height, bool bold = false, bool center = false)
        {
            var text = At(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * .78f;
            text.fontSizeMax = size;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            _labels.Add(name, text);
            return text;
        }

        private RectTransform Box(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var rect = At(parent, name, x, y, width, height);
            Surface(rect, color);
            return rect;
        }

        private static UnityEngine.UI.Image Image(RectTransform rect) => rect.GetComponent<UnityEngine.UI.Image>();
        private UnityEngine.UI.Image Surface(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.sprite = _rounded;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.raycastTarget = raycast;
            return image;
        }

        private void ApplySafeArea()
        {
            if (_safe == null || Screen.width <= 0 || Screen.height <= 0) return;
            var area = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (area == _safeArea && screen == _screen) return;
            _safeArea = area;
            _screen = screen;
            _safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            _safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }

        private void MakeRoundedSprite()
        {
            _texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
                { name = "CatExtrasRounded", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[64 * 64];
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 64; x++)
                {
                    var dx = Mathf.Max(Mathf.Abs(x - 31.5f) - 15.5f, 0);
                    var dy = Mathf.Max(Mathf.Abs(y - 31.5f) - 15.5f, 0);
                    pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(16 - Mathf.Sqrt(dx * dx + dy * dy)));
                }
            _texture.SetPixels(pixels);
            _texture.Apply(false, true);
            _rounded = Sprite.Create(_texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
            _rounded.hideFlags = HideFlags.DontSave;
        }

        private static RectTransform Fill(Transform parent, string name)
        {
            var rect = NewRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static RectTransform At(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = NewRect(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        private static RectTransform NewRect(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
