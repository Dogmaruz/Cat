using System;
using System.Collections.Generic;
using DancingCats.Clone;
using RhythmGame.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace RadioArchipelago.UI
{
    /// <summary>
    /// Native menu and session views for Radio Archipelago. View references are cached,
    /// catalog rows are recycled, and controller notifications only update visible data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RadioMenuPresenter : MonoBehaviour, IGamePresentation
    {
        private static readonly Color Ink = new Color32(27,49,83,255);
        private static readonly Color Muted = new Color32(103,123,143,255);
        private static readonly Color Cream = new Color32(251,242,225,255);
        private static readonly Color Paper = new Color32(255,250,240,255);
        private static readonly Color Mint = new Color32(80,193,169,255);
        private static readonly Color Coral = new Color32(245,108,82,255);
        private static readonly Color LineColor = new Color32(218,209,193,255);
        private const float SongRowHeight = 104;
        private const int SongRowPoolSize = 16;

        private OriginalPlayController game;
        private ISongCatalog songs;
        private OriginalProgression profile;
        private RadioArtAssets art;
        private CourierCatalog couriers;
        private CourierSelection courierSelection;
        private Canvas canvas;
        private TMP_FontAsset font;
        private RectTransform safeArea;
        private GameObject backdrop, home, navigation, hud, overlay;
        private readonly Dictionary<string, GameObject> screens = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, TMP_Text> navLabels = new Dictionary<string, TMP_Text>();
        private readonly Dictionary<string, Graphic> navMarks = new Dictionary<string, Graphic>();
        private readonly List<UnityEngine.UI.Image> courierCards = new List<UnityEngine.UI.Image>();
        private readonly List<UnityEngine.UI.Image> workshopCards = new List<UnityEngine.UI.Image>();
        private readonly List<TMP_Text> workshopStates = new List<TMP_Text>();
        private readonly List<SongRowView> songRows = new List<SongRowView>();
        private readonly List<TMP_Text> stationStates = new List<TMP_Text>();
        private readonly List<UnityEngine.UI.Image> stationMarks = new List<UnityEngine.UI.Image>();
        private TMP_Text songTitle, songDetails, songStars, courierName, hudScore, hudCombo, hudProgress, modeLabel;
        private TMP_Text overlayTitle, overlayDetail, overlayScore, overlayReward, atlasSummary, catalogBalance, catalogMessage;
        private TMP_Text soundValue, vibrationValue, effectsValue, calibrationValue;
        private UnityEngine.UI.Image hudFill;
        private UnityEngine.UI.Button resumeButton, nextButton, reviveButton;
        private TMP_Text reviveLabel;
        private GameObject readyPrompt;
        private RectTransform songContent;
        private ScrollRect songScroll;
        private string view = "Play";
        private string modal;
        private ClonePhase lastPhase = (ClonePhase)(-1);
        private Rect lastSafeArea;
        private int lastWidth, lastHeight;
        private int lastScore = -1, lastStreak = -1;
        private int lastProgressPercent = -1;
        private string lastSongId;
        private bool initialized;
        private bool initializing;
        private int lastReviveSeconds = -1;

        private sealed class SongRowView
        {
            public RectTransform root;
            public TMP_Text title, detail, action;
            public UnityEngine.UI.Image accent;
            public UnityEngine.UI.Button button;
            public int index = -1;
        }

        public Canvas UICanvas => canvas;
        public bool IsBlockingInput => !initialized || modal != null || game != null && game.Phase != ClonePhase.Playing && game.Phase != ClonePhase.Ready;
        public string CurrentView => modal ?? view;

        [Inject]
        public void Construct(OriginalPlayController controller, ISongCatalog songCatalog, OriginalProgression progression,
            RadioArtAssets presentationArt, CourierCatalog courierCatalog, CourierSelection selection)
        {
            game = controller ?? throw new ArgumentNullException(nameof(controller));
            songs = songCatalog ?? throw new ArgumentNullException(nameof(songCatalog));
            profile = progression ?? throw new ArgumentNullException(nameof(progression));
            art = presentationArt ?? throw new ArgumentNullException(nameof(presentationArt));
            couriers = courierCatalog ?? throw new ArgumentNullException(nameof(courierCatalog));
            courierSelection = selection ?? throw new ArgumentNullException(nameof(selection));
        }

        public void Initialize()
        {
            if (initialized || initializing) throw new InvalidOperationException("The radio presentation is already initialized or initializing.");
            if (game == null) throw new InvalidOperationException("The presentation must be composed before initialization.");
            initializing = true;
            try
            {
                CreateFont();
                BuildCanvas();
                BuildHome();
                BuildNavigation();
                BuildAtlas();
                BuildWorkshop();
                BuildSongs();
                BuildSettings();
                BuildHud();
                BuildSessionOverlay();
                game.Changed += Refresh;
                courierSelection.Changed += RefreshCouriers;
                RefreshCouriers();
                ApplySafeArea();
                initialized = true;
                Refresh();
            }
            catch
            {
                initialized = false;
                ReleasePresentation();
                throw;
            }
            finally { initializing = false; }
        }

        private void CreateFont()
        {
            // The builder installs persistent package settings; runtime never creates or
            // patches TMP's global configuration or opens an import dialog.
            if (TMP_Settings.LoadDefaultSettings() == null)
                throw new InvalidOperationException("TextMesh Pro settings are missing. Run Radio Archipelago's interface asset builder before play.");
            Font source = art.font != null ? art.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font = TMP_FontAsset.CreateFontAsset(source, 48, 6,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            if (font == null) throw new InvalidOperationException("The interface font needs Include Font Data enabled in its import settings.");
            font.name = "RadioArchipelago_RuntimeCyrillic";
            font.isMultiAtlasTexturesEnabled = true;
            font.TryAddCharacters("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789—–·:.,!?+−/()%");
        }

        private void BuildCanvas()
        {
            var root = new GameObject("RadioArchipelagoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 1168);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            backdrop = Rect("SkyIslandsBackground", root.transform, Vector2.zero, Vector2.one).gameObject;
            var background = backdrop.AddComponent<UnityEngine.UI.Image>();
            background.color = art.menuBackground != null ? Color.white : new Color(.70f,.85f,.88f);
            background.sprite = art.menuBackground; background.raycastTarget = false;
            safeArea = Rect("SafeArea", root.transform, Vector2.zero, Vector2.one);
        }

        private void BuildHome()
        {
            home = Rect("Home", safeArea, Vector2.zero, Vector2.one).gameObject;
            var header = Rect("Header", home.transform, new Vector2(.04f,.925f), new Vector2(.96f,.985f));
            Icon("BrandMark", header, new Vector2(0,.05f), new Vector2(.105f,.95f), RadioGlyph.Symbol.Signal, Color.white, art.brandLogo);
            Label("BrandName", header, "Радиоархипелаг", 25, Ink, new Vector2(.13f,0),new Vector2(.88f,1),TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            IconButton("SettingsButton", header, new Vector2(.91f,.12f),new Vector2(1,.88f), RadioGlyph.Symbol.Settings, art.settingsIcon, () => Navigate("Settings"), false);

            var previewRect = Rect("CourierHero", home.transform, new Vector2(.03f,.61f), new Vector2(.79f,.94f));
            var previewImage = Rect("Preview",previewRect,Vector2.zero,Vector2.one);
            var fit = previewImage.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio=1;
            var hero = previewImage.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            hero.raycastTarget = false;
            previewImage.gameObject.AddComponent<CourierMenuPreview>().Initialize(courierSelection);
            Label("Tagline",home.transform,"Поймай\nсвой ритм", 27,Ink,new Vector2(.055f,.625f),new Vector2(.50f,.715f),TextAlignmentOptions.MidlineLeft,FontStyles.Italic);
            var underline = Panel("TaglineUnderline",home.transform,new Vector2(.055f,.620f),new Vector2(.31f,.624f),Mint);
            underline.transform.localRotation = Quaternion.Euler(0,0,8);

            Panel("SongDock",home.transform,new Vector2(0,.066f),new Vector2(1,.449f),new Color(Cream.r,Cream.g,Cream.b,.97f));
            Label("OnAir",home.transform,"С Е Й Ч А С  В  Э Ф И Р Е",14,Muted,new Vector2(.18f,.407f),new Vector2(.82f,.431f));
            var dot = Panel("OnAirIndicator",home.transform,new Vector2(.18f,.414f),new Vector2(.195f,.421f),Mint,true);
            songTitle = Label("SelectedSongTitle",home.transform,"",27,Ink,new Vector2(.15f,.369f),new Vector2(.85f,.408f),TextAlignmentOptions.Center,FontStyles.Bold);
            songTitle.enableAutoSizing = true; songTitle.fontSizeMin = 19; songTitle.fontSizeMax = 27;
            songDetails = Label("SelectedSongDetails",home.transform,"",16,Muted,new Vector2(.17f,.344f),new Vector2(.83f,.371f));
            songStars = Label("SelectedSongProgress",home.transform,"",13,Muted,new Vector2(.23f,.316f),new Vector2(.77f,.339f));
            IconButton("PreviousSongButton",home.transform,new Vector2(.045f,.35f),new Vector2(.14f,.403f),RadioGlyph.Symbol.Previous,art.previousIcon,() => CycleSong(-1));
            IconButton("NextSongButton",home.transform,new Vector2(.86f,.35f),new Vector2(.955f,.403f),RadioGlyph.Symbol.Next,art.nextIcon,() => CycleSong(1));
            Button("SongLibraryButton",home.transform,"Все треки",14,Ink,new Vector2(.32f,.287f),new Vector2(.68f,.313f),new Color(1,1,1,0),() => Navigate("Songs"));
            Panel("DockDivider",home.transform,new Vector2(.065f,.278f),new Vector2(.935f,.279f),LineColor);

            for (int i=0;i<couriers.entries.Length;i++)
            {
                int index=i;
                float left=.13f+i*.25f;
                var card = Button("CourierButton_"+i,home.transform,"",16,Ink,new Vector2(left,.169f),new Vector2(left+.23f,.272f),Color.clear,() => SelectCourier(index));
                var highlight=Panel("Selection",card.transform,new Vector2(.15f,0),new Vector2(.85f,.035f),i==courierSelection.SelectedIndex?Mint:Color.clear,true);
                courierCards.Add(highlight);
                var icon=Panel("Portrait",card.transform,new Vector2(.06f,.07f),new Vector2(.94f,.97f),Color.white);
                icon.sprite = couriers.entries[i].icon; icon.preserveAspect=true;
                if(icon.sprite==null) icon.color=Color.clear;
            }
            courierName=Label("SelectedCourierName",home.transform,"",12,Muted,new Vector2(.15f,.151f),new Vector2(.85f,.170f));
            var play=Button("PlayButton",home.transform,"ИГРАТЬ",31,Color.white,new Vector2(.07f,.078f),new Vector2(.93f,.146f),Coral,StartSelectedSong);
            LabelInButton(play).fontStyle=FontStyles.Bold;
            Icon("PlayPulse",play.transform,new Vector2(.21f,.27f),new Vector2(.29f,.73f),RadioGlyph.Symbol.Play,Color.white,art.radioIcon);
            var playText=LabelInButton(play); SetAnchors(playText.rectTransform,new Vector2(.30f,0),new Vector2(.90f,1));
            modeLabel=Label("RunMode",home.transform,"Обычный эфир",12,Muted,new Vector2(.23f,.056f),new Vector2(.77f,.075f));
            Button("ModeButton",home.transform,"",12,Ink,new Vector2(.23f,.056f),new Vector2(.77f,.076f),Color.clear,() => {game.Endless=!game.Endless;RefreshMenu();});
        }

        private void BuildNavigation()
        {
            navigation=Rect("Navigation",safeArea,new Vector2(0,0),new Vector2(1,.067f)).gameObject;
            Panel("NavigationSurface",navigation.transform,Vector2.zero,Vector2.one,Paper);
            Panel("NavigationDivider",navigation.transform,new Vector2(0,.988f),Vector2.one,LineColor);
            string[] keys={"Play","Atlas","Workshop"};
            string[] titles={"Играть","Атлас","Мастерская"};
            RadioGlyph.Symbol[] shapes={RadioGlyph.Symbol.Play,RadioGlyph.Symbol.Map,RadioGlyph.Symbol.Workshop};
            Sprite[] sprites={art.playIcon,art.mapIcon,art.workshopIcon};
            for(int i=0;i<keys.Length;i++)
            {
                string key=keys[i]; float left=i/3f;
                var button=Button("Navigation_"+key,navigation.transform,"",16,Muted,new Vector2(left,0),new Vector2(left+1/3f,1),Color.clear,() => Navigate(key));
                navMarks.Add(key,Icon("Mark",button.transform,new Vector2(.4f,.43f),new Vector2(.6f,.90f),shapes[i],Muted,sprites[i]));
                navLabels.Add(key,Label("Title",button.transform,titles[i],16,Muted,new Vector2(.02f,.08f),new Vector2(.98f,.45f)));
            }
        }

        private GameObject CreateScreen(string key,string title,string subtitle)
        {
            var screen=Rect(key+"Screen",safeArea,new Vector2(0,.067f),new Vector2(1,1)).gameObject;
            Panel("Surface",screen.transform,Vector2.zero,Vector2.one,Cream);
            Label("Title",screen.transform,title,32,Ink,new Vector2(.07f,.91f),new Vector2(.85f,.975f),TextAlignmentOptions.MidlineLeft,FontStyles.Bold);
            Label("Subtitle",screen.transform,subtitle,16,Muted,new Vector2(.07f,.858f),new Vector2(.93f,.915f),TextAlignmentOptions.TopLeft);
            screens.Add(key,screen);
            screen.SetActive(false);
            return screen;
        }

        private void BuildAtlas()
        {
            var screen=CreateScreen("Atlas","Атлас сигналов","Каждая мелодия зажигает новый маяк.");
            atlasSummary=Label("AtlasSummary",screen.transform,"",21,Ink,new Vector2(.07f,.77f),new Vector2(.93f,.845f),TextAlignmentOptions.MidlineLeft,FontStyles.Bold);
            string[] names={"Тихая гавань","Ветряные сады","Облачная почта","Лунная станция","Дальний эфир"};
            string[] descriptions={"Первый сигнал над океаном","Маршруты становятся смелее","Мелодии соединяют острова","Ночной эфир ждёт слушателей","Слышно даже за горизонтом"};
            for(int i=0;i<names.Length;i++)
            {
                float top=.75f-i*.128f;
                var card=Panel("Station_"+i,screen.transform,new Vector2(.07f,top-.112f),new Vector2(.93f,top),Paper,true);
                stationMarks.Add(Panel("Beacon",card.transform,new Vector2(.04f,.28f),new Vector2(.14f,.72f),Mint,true));
                Icon("Signal",card.transform,new Vector2(.055f,.36f),new Vector2(.125f,.64f),RadioGlyph.Symbol.Signal,Ink,art.radioIcon);
                Label("Name",card.transform,names[i],20,Ink,new Vector2(.19f,.51f),new Vector2(.95f,.89f),TextAlignmentOptions.MidlineLeft,FontStyles.Bold);
                Label("Description",card.transform,descriptions[i],13,Muted,new Vector2(.19f,.23f),new Vector2(.95f,.52f),TextAlignmentOptions.MidlineLeft);
                stationStates.Add(Label("Progress",card.transform,"",12,Muted,new Vector2(.19f,.02f),new Vector2(.95f,.25f),TextAlignmentOptions.MidlineLeft));
            }
            Button("AtlasPlayButton",screen.transform,"Поймать следующий сигнал",18,Color.white,new Vector2(.07f,.035f),new Vector2(.93f,.095f),Coral,() => Navigate("Play"));
        }

        private void BuildWorkshop()
        {
            var screen=CreateScreen("Workshop","Мастерская","Выбери своего курьера. Каждый умеет слушать.");
            string[] descriptions={"Тёплый шарф, ясный сигнал.\nДля первого полёта и любимых мелодий.","Ловит ритм на солнечных частотах.\nЛюбит быстрые и звонкие маршруты.","Спокойный голос ночного эфира.\nНесёт музыку сквозь облака."};
            for(int i=0;i<couriers.entries.Length;i++)
            {
                int index=i; float top=.82f-i*.235f;
                var button=Button("WorkshopCourierButton_"+i,screen.transform,"",18,Ink,new Vector2(.07f,top-.21f),new Vector2(.93f,top),Paper,() => SelectCourier(index));
                workshopCards.Add(button.GetComponent<UnityEngine.UI.Image>());
                var portrait=Panel("Portrait",button.transform,new Vector2(.015f,.03f),new Vector2(.36f,.98f),Color.white);
                portrait.sprite=couriers.entries[i].icon; portrait.preserveAspect=true;
                if(portrait.sprite==null) portrait.color=Color.clear;
                Label("Name",button.transform,couriers.entries[i].displayName,24,Ink,new Vector2(.39f,.68f),new Vector2(.94f,.91f),TextAlignmentOptions.MidlineLeft,FontStyles.Bold);
                Label("Description",button.transform,descriptions[Mathf.Min(i,descriptions.Length-1)],14,Muted,new Vector2(.39f,.30f),new Vector2(.94f,.66f),TextAlignmentOptions.MidlineLeft);
                workshopStates.Add(Label("SelectionState",button.transform,"",14,Ink,new Vector2(.39f,.09f),new Vector2(.94f,.30f),TextAlignmentOptions.MidlineLeft,FontStyles.Bold));
            }
            Label("CosmeticNote",screen.transform,"Курьеры отличаются внешностью.\nТочность зависит от твоего ритма.",15,Muted,new Vector2(.09f,.035f),new Vector2(.91f,.11f));
        }

        private void BuildSongs()
        {
            var screen=CreateScreen("Songs","Библиотека эфира","Все мелодии твоего архипелага");
            IconButton("ClosePanelButton_Songs",screen.transform,new Vector2(.87f,.93f),new Vector2(.95f,.973f),RadioGlyph.Symbol.Close,art.closeIcon,ClosePopup,false);
            catalogBalance=Label("CatalogBalance",screen.transform,"",14,Muted,new Vector2(.07f,.81f),new Vector2(.93f,.855f),TextAlignmentOptions.MidlineLeft);
            catalogMessage=Label("CatalogMessage",screen.transform,"",13,Coral,new Vector2(.07f,.01f),new Vector2(.93f,.055f));
            var scrollRoot=Rect("SongScroll",screen.transform,new Vector2(.055f,.07f),new Vector2(.945f,.805f));
            songScroll=scrollRoot.gameObject.AddComponent<ScrollRect>();
            songScroll.horizontal=false; songScroll.vertical=true;
            songScroll.movementType=ScrollRect.MovementType.Clamped;
            songScroll.scrollSensitivity=30;
            var viewport=Rect("Viewport",scrollRoot,Vector2.zero,Vector2.one);
            var maskImage=viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); maskImage.color=Color.white;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            songContent=Rect("Content",viewport,new Vector2(0,1),new Vector2(1,1));
            songContent.pivot=new Vector2(.5f,1);
            songContent.sizeDelta=new Vector2(0,songs.Songs.Length*SongRowHeight);
            songScroll.viewport=viewport; songScroll.content=songContent;
            for(int i=0;i<SongRowPoolSize;i++)
            {
                var row=new SongRowView();
                row.button=Button("SongRow_"+i,songContent,"",16,Ink,Vector2.zero,Vector2.one,Paper,() => ActivateSongRow(row));
                row.root=(RectTransform)row.button.transform;
                row.root.anchorMin=new Vector2(0,1); row.root.anchorMax=Vector2.one; row.root.pivot=new Vector2(.5f,1); row.root.sizeDelta=new Vector2(0,SongRowHeight-9);
                row.accent=Panel("Accent",row.root,new Vector2(.025f,.18f),new Vector2(.04f,.82f),Mint,true);
                row.title=Label("Title",row.root,"",20,Ink,new Vector2(.075f,.49f),new Vector2(.72f,.90f),TextAlignmentOptions.MidlineLeft,FontStyles.Bold);
                row.title.enableAutoSizing=true; row.title.fontSizeMin=14; row.title.fontSizeMax=20;
                row.detail=Label("Detail",row.root,"",13,Muted,new Vector2(.075f,.13f),new Vector2(.72f,.49f),TextAlignmentOptions.MidlineLeft);
                row.action=Label("Action",row.root,"",12,Ink,new Vector2(.73f,.12f),new Vector2(.975f,.87f),TextAlignmentOptions.Center,FontStyles.Bold);
                songRows.Add(row);
            }
            songScroll.onValueChanged.AddListener(_ => RefreshSongRows(false));
        }

        private void BuildSettings()
        {
            var screen=CreateScreen("Settings","Настройки","Пусть ритм ощущается правильно");
            IconButton("ClosePanelButton_Settings",screen.transform,new Vector2(.87f,.93f),new Vector2(.95f,.973f),RadioGlyph.Symbol.Close,art.closeIcon,ClosePopup,false);
            soundValue=SettingButton(screen.transform,"SoundToggle","Звук",.75f,() => ChangeSetting(0));
            vibrationValue=SettingButton(screen.transform,"VibrationToggle","Вибрация",.64f,() => ChangeSetting(1));
            effectsValue=SettingButton(screen.transform,"EffectsToggle","Визуальные эффекты",.53f,() => ChangeSetting(2));
            var calibration=Panel("Calibration",screen.transform,new Vector2(.07f,.22f),new Vector2(.93f,.49f),Paper,true);
            Label("Title",calibration.transform,"Задержка звука",22,Ink,new Vector2(.06f,.72f),new Vector2(.94f,.96f),TextAlignmentOptions.MidlineLeft,FontStyles.Bold);
            Label("Hint",calibration.transform,"Если музыка и прыжки расходятся,\nподстрой задержку по 10 мс.",14,Muted,new Vector2(.06f,.44f),new Vector2(.94f,.74f),TextAlignmentOptions.MidlineLeft);
            Button("CalibrationMinusButton",calibration.transform,"−10 мс",17,Ink,new Vector2(.06f,.13f),new Vector2(.29f,.39f),Cream,() => AdjustCalibration(-1));
            calibrationValue=Label("CalibrationValue",calibration.transform,"",22,Ink,new Vector2(.30f,.13f),new Vector2(.70f,.39f),TextAlignmentOptions.Center,FontStyles.Bold);
            Button("CalibrationPlusButton",calibration.transform,"+10 мс",17,Ink,new Vector2(.71f,.13f),new Vector2(.94f,.39f),Cream,() => AdjustCalibration(1));
            Button("CalibrationResetButton",screen.transform,"Сбросить задержку",15,Muted,new Vector2(.20f,.155f),new Vector2(.80f,.205f),Color.clear,() => {profile.SetRhythmLineHeight(0);RefreshSettings();});
            Label("OfflineNote",screen.transform,"Сигнал доступен без подключения.\nНастройки сохраняются на этом устройстве.",14,Muted,new Vector2(.10f,.045f),new Vector2(.90f,.125f));
        }

        private TMP_Text SettingButton(Transform parent,string name,string title,float bottom,Action action)
        {
            var button=Button(name,parent,"",18,Ink,new Vector2(.07f,bottom),new Vector2(.93f,bottom+.085f),Paper,action);
            Label("Title",button.transform,title,20,Ink,new Vector2(.055f,.12f),new Vector2(.75f,.88f),TextAlignmentOptions.MidlineLeft);
            return Label("Value",button.transform,"",15,Ink,new Vector2(.77f,.12f),new Vector2(.95f,.88f),TextAlignmentOptions.Center,FontStyles.Bold);
        }

        private void BuildHud()
        {
            hud=Rect("PlayingHud",safeArea,Vector2.zero,Vector2.one).gameObject;
            var top=Panel("ScoreSurface",hud.transform,new Vector2(.05f,.915f),new Vector2(.95f,.985f),new Color(Paper.r,Paper.g,Paper.b,.94f),true);
            IconButton("PauseButton",top.transform,new Vector2(.02f,.13f),new Vector2(.12f,.87f),RadioGlyph.Symbol.Pause,art.pauseIcon,() => game.Pause(),false);
            hudScore=Label("Score",top.transform,"0",27,Ink,new Vector2(.16f,.28f),new Vector2(.60f,.93f),TextAlignmentOptions.MidlineLeft,FontStyles.Bold);
            hudCombo=Label("Combo",top.transform,"",13,Muted,new Vector2(.17f,.02f),new Vector2(.74f,.34f),TextAlignmentOptions.MidlineLeft);
            hudProgress=Label("Progress",top.transform,"0%",15,Muted,new Vector2(.76f,.20f),new Vector2(.95f,.8f),TextAlignmentOptions.MidlineRight);
            Panel("ProgressTrack",hud.transform,new Vector2(.08f,.902f),new Vector2(.92f,.907f),new Color(Ink.r,Ink.g,Ink.b,.12f),true);
            hudFill=Panel("ProgressFill",hud.transform,new Vector2(.08f,.902f),new Vector2(.08f,.907f),Mint,true);
            readyPrompt=Rect("ReadyPrompt",hud.transform,new Vector2(.08f,.13f),new Vector2(.92f,.27f)).gameObject;
            Panel("Surface",readyPrompt.transform,Vector2.zero,Vector2.one,new Color(Paper.r,Paper.g,Paper.b,.94f),true);
            Label("Hint",readyPrompt.transform,"Держи и веди курьера\nв ритме мелодии",21,Ink,new Vector2(.04f,.35f),new Vector2(.96f,.96f));
            Button("ReadyPlayButton",readyPrompt.transform,"В эфир",19,Color.white,new Vector2(.18f,.07f),new Vector2(.82f,.36f),Coral,StartSelectedSong);
        }

        private void BuildSessionOverlay()
        {
            overlay=Rect("SessionOverlay",safeArea,Vector2.zero,Vector2.one).gameObject;
            Panel("Dim",overlay.transform,Vector2.zero,Vector2.one,new Color(Ink.r,Ink.g,Ink.b,.48f));
            var card=Panel("SessionCard",overlay.transform,new Vector2(.08f,.19f),new Vector2(.92f,.79f),Cream,true);
            Icon("Signal",card.transform,new Vector2(.40f,.82f),new Vector2(.60f,.96f),RadioGlyph.Symbol.Signal,Mint,art.radioIcon);
            overlayTitle=Label("Title",card.transform,"",31,Ink,new Vector2(.06f,.68f),new Vector2(.94f,.84f),TextAlignmentOptions.Center,FontStyles.Bold);
            overlayDetail=Label("Detail",card.transform,"",17,Muted,new Vector2(.07f,.58f),new Vector2(.93f,.70f));
            overlayScore=Label("Score",card.transform,"",47,Ink,new Vector2(.08f,.41f),new Vector2(.92f,.59f),TextAlignmentOptions.Center,FontStyles.Bold);
            overlayReward=Label("Reward",card.transform,"",15,Muted,new Vector2(.07f,.34f),new Vector2(.93f,.43f));
            resumeButton=Button("ResumeButton",card.transform,"Продолжить",21,Color.white,new Vector2(.07f,.24f),new Vector2(.93f,.34f),Coral,() => game.Resume());
            reviveButton=Button("ReviveButton",card.transform,"Вернуться в эфир",20,Color.white,new Vector2(.07f,.24f),new Vector2(.93f,.34f),Coral,() => {if(game.CanRevive) game.Revive();});
            reviveLabel=LabelInButton(reviveButton);
            Button("RetryButton",card.transform,"Ещё раз",20,Ink,new Vector2(.07f,.13f),new Vector2(.48f,.23f),Paper,Retry);
            Button("HomeButton",card.transform,"В меню",20,Ink,new Vector2(.52f,.13f),new Vector2(.93f,.23f),Paper,GoHome);
            nextButton=Button("NextTrackButton",card.transform,"Следующая мелодия",17,Ink,new Vector2(.07f,.02f),new Vector2(.93f,.12f),Color.clear,() => {game.NextSong();ClosePopup();});
        }

        public void StartSelectedSong()
        {
            if(!initialized) return;
            ClosePopup();
            if(game.Phase==ClonePhase.Menu || game.Phase==ClonePhase.Ready) game.BeginRun();
        }

        public void SelectCourier(int index) => courierSelection.Select(index);

        public bool SelectSong(int catalogId)
        {
            if(!game.SelectSong(catalogId)) return false;
            view="Play"; ClosePopup(); Refresh();
            return true;
        }

        private void CycleSong(int direction)
        {
            var entries=songs.Songs;
            int current=0;
            for(int i=0;i<entries.Length;i++) if(entries[i].id==game.Selected.id) {current=i;break;}
            for(int step=1;step<entries.Length;step++)
            {
                int index=(current+direction*step+entries.Length*2)%entries.Length;
                if(profile.IsSongUnlocked(entries[index].id)) {SelectSong(entries[index].catalogId);return;}
            }
        }

        public void Navigate(string target)
        {
            if(!initialized) return;
            if(target=="Songs" || target=="Settings")
            {
                modal=target;
                if(target=="Songs") {catalogMessage.text="";RefreshSongRows(true);}
                else RefreshSettings();
            }
            else
            {
                modal=null;
                view=target=="Atlas" || target=="Workshop" ? target : "Play";
            }
            ApplyScreens();
            if(view=="Atlas") RefreshAtlas();
        }

        public void OpenPanel(string name)
        {
            if(name=="MusicList_UIPanel" || name=="Songs") Navigate("Songs");
            else if(name=="Set_UIPanel" || name=="Settings") Navigate("Settings");
            else if(name=="NewRoleShop_UIPanel" || name=="Workshop") Navigate("Workshop");
            else Navigate("Atlas");
        }

        public void ClosePopup() {modal=null;ApplyScreens();}

        private void Retry()
        {
            if(game.Phase==ClonePhase.Dead) game.AcceptFailure();
            game.Retry(); modal=null; view="Play"; Refresh();
        }

        private void GoHome()
        {
            if(game.Phase==ClonePhase.Dead) game.AcceptFailure();
            game.Home(); view="Play"; modal=null; Refresh();
        }

        private void Refresh()
        {
            if(!initialized) return;
            bool phaseChanged=lastPhase!=game.Phase;
            if(phaseChanged)
            {
                lastPhase=game.Phase;
                if(game.Phase!=ClonePhase.Menu) modal=null;
                if(game.Phase==ClonePhase.Menu) view="Play";
                ApplyScreens();
                if(game.Phase==ClonePhase.Dead || game.Phase==ClonePhase.Results || game.Phase==ClonePhase.Paused) RefreshOverlay();
            }
            if(game.Phase==ClonePhase.Menu || game.Phase==ClonePhase.Ready)
            {
                RefreshMenu();
                if(view=="Atlas") RefreshAtlas();
                if(modal=="Songs") RefreshSongRows(true);
                if(modal=="Settings") RefreshSettings();
            }
            if(game.Score!=lastScore) {lastScore=game.Score;hudScore.text=lastScore.ToString("N0");}
            if(game.Streak!=lastStreak) {lastStreak=game.Streak;hudCombo.text=lastStreak>1?"Серия "+lastStreak:"Слушай и веди";}
        }

        private void ApplyScreens()
        {
            if(!initialized || canvas==null) return;
            bool isMenu=game.Phase==ClonePhase.Menu;
            bool isReady=game.Phase==ClonePhase.Ready;
            backdrop.SetActive(isMenu);
            home.SetActive(isMenu && view=="Play" && modal==null);
            navigation.SetActive(isMenu);
            foreach(var entry in screens) entry.Value.SetActive(isMenu && (modal!=null?entry.Key==modal:entry.Key==view));
            hud.SetActive(game.Phase==ClonePhase.Playing || isReady);
            readyPrompt.SetActive(isReady);
            overlay.SetActive(game.Phase==ClonePhase.Paused || game.Phase==ClonePhase.Dead || game.Phase==ClonePhase.Results);
            foreach(var label in navLabels) {label.Value.color=label.Key==view?Coral:Muted;label.Value.fontStyle=label.Key==view?FontStyles.Bold:FontStyles.Normal;}
            foreach(var mark in navMarks) mark.Value.color=mark.Key==view?Coral:Muted;
        }

        private void RefreshMenu()
        {
            if(game.Selected==null) return;
            if(lastSongId!=game.Selected.id)
            {
                lastSongId=game.Selected.id;
                songTitle.text=game.Selected.title;
                songDetails.text=game.Selected.artist+" · "+Duration(game.Selected.audioLengthSeconds);
            }
            var progress=profile.SongProgress(game.Selected.id);
            int best=progress==null?0:game.Endless?progress.endlessScore:progress.normalScore;
            int stars=progress==null?0:game.Endless?progress.endlessStars:progress.normalStars;
            songStars.text=best>0?"Лучший сигнал: "+best.ToString("N0")+" · Маяки "+stars+(game.Endless?"/9":"/3"):"Новая мелодия · Поймай первый сигнал";
            modeLabel.text=game.Endless?"Бесконечный эфир · сменить":"Обычный эфир · сменить";
        }

        private void RefreshCouriers()
        {
            if(courierName!=null) courierName.text=courierSelection.SelectedDefinition.displayName;
            for(int i=0;i<courierCards.Count;i++) courierCards[i].color=i==courierSelection.SelectedIndex?Mint:Color.clear;
            for(int i=0;i<workshopCards.Count;i++)
            {
                bool selected=i==courierSelection.SelectedIndex;
                workshopCards[i].color=selected?new Color(.87f,.96f,.90f):Paper;
                workshopStates[i].text=selected?"В ЭФИРЕ":"Выбрать курьера";
                workshopStates[i].color=selected?new Color(.16f,.48f,.40f):Ink;
            }
        }

        private void RefreshAtlas()
        {
            int completed=0;
            for(int i=0;i<profile.State.songs.Count;i++) if(profile.State.songs[i].completions>0) completed++;
            int[] thresholds={0,1,3,6,10};
            atlasSummary.text="Зажжено мелодий: "+completed+" / "+songs.Songs.Length;
            for(int i=0;i<stationStates.Count;i++)
            {
                bool unlocked=completed>=thresholds[i];
                stationMarks[i].color=unlocked?Mint:new Color(.83f,.83f,.79f);
                stationStates[i].text=unlocked?"Сигнал принят":"До открытия: "+Mathf.Max(0,thresholds[i]-completed)+" новых мелодий";
            }
        }

        private void RefreshSongRows(bool force)
        {
            if(songContent==null) return;
            catalogBalance.text=songs.Songs.Length+" треков · Ноты: "+profile.Gold+" · Кристаллы: "+profile.Diamonds;
            int first=Mathf.Clamp(Mathf.FloorToInt(songContent.anchoredPosition.y/SongRowHeight)-1,0,Mathf.Max(0,songs.Songs.Length-1));
            for(int i=0;i<songRows.Count;i++)
            {
                var row=songRows[i]; int index=first+i;
                bool active=index<songs.Songs.Length;
                row.root.gameObject.SetActive(active);
                if(!active || !force && row.index==index) continue;
                row.index=index; row.root.anchoredPosition=new Vector2(0,-index*SongRowHeight);
                var song=songs.Songs[index]; bool unlocked=profile.IsSongUnlocked(song.id);
                row.title.text=song.title;
                row.detail.text=song.artist+" · "+Duration(song.audioLengthSeconds);
                row.accent.color=unlocked?Mint:LineColor;
                if(unlocked) row.action.text=game.Selected.id==song.id?"В ЭФИРЕ":"ВЫБРАТЬ";
                else if(song.catalog!=null && (song.catalog.musicUnlockType==2 || song.catalog.musicUnlockType==3 || song.catalog.musicUnlockType==4)) row.action.text="ОТКРЫТЬ\nЛОКАЛЬНО";
                else if(song.catalog!=null && song.catalog.gold>0) row.action.text="ОТКРЫТЬ\n"+song.catalog.gold+(song.catalog.musicUnlockType==5?" крист.":" нот");
                else row.action.text="ЗАКРЫТО";
            }
        }

        private void ActivateSongRow(SongRowView row)
        {
            if(row.index<0 || row.index>=songs.Songs.Length) return;
            var song=songs.Songs[row.index];
            if(!profile.IsSongUnlocked(song.id))
            {
                bool localUnlock=song.catalog!=null && (song.catalog.musicUnlockType==2 || song.catalog.musicUnlockType==3 || song.catalog.musicUnlockType==4);
                bool unlocked=localUnlock?profile.RewardSongUnlock(song):profile.TryBuySong(song);
                if(!unlocked) {catalogMessage.text="Пока недостаточно наград для этой мелодии.";return;}
            }
            SelectSong(song.catalogId);
        }

        private void ChangeSetting(int index)
        {
            var settings=profile.State.settings;
            profile.SetSettings(index==0?!settings.sound:settings.sound,index==1?!settings.vibration:settings.vibration,
                index==2?!settings.postProcessing:settings.postProcessing,settings.language,settings.calibrationOffsetSeconds);
            RefreshSettings();
        }

        private void AdjustCalibration(int direction) {profile.AdjustCalibration(direction);RefreshSettings();}
        private void RefreshSettings()
        {
            var settings=profile.State.settings;
            soundValue.text=settings.sound?"ВКЛ":"ВЫКЛ"; soundValue.color=settings.sound?new Color(.16f,.48f,.40f):Muted;
            vibrationValue.text=settings.vibration?"ВКЛ":"ВЫКЛ"; vibrationValue.color=settings.vibration?new Color(.16f,.48f,.40f):Muted;
            effectsValue.text=settings.postProcessing?"ВКЛ":"ВЫКЛ"; effectsValue.color=settings.postProcessing?new Color(.16f,.48f,.40f):Muted;
            int milliseconds=Mathf.RoundToInt(settings.rhythmLineHeight*1000);
            calibrationValue.text=(milliseconds>0?"+":"")+milliseconds+" мс";
        }

        private void RefreshOverlay()
        {
            bool paused=game.Phase==ClonePhase.Paused;
            bool dead=game.Phase==ClonePhase.Dead;
            overlayTitle.text=paused?"Эфир на паузе":dead?"Попробуем ещё?":game.WinSequence?"Сигнал принят!":"Эфир завершён";
            overlayDetail.text=game.Selected.title;
            overlayScore.text=game.Score.ToString("N0");
            overlayReward.text=paused?"Продолжим с того же места":dead?(game.CanRevive?"Один возврат за полёт · бесплатно":"Можно повторить полёт с начала"):
                "Маяки: "+game.RunStars+(game.Endless?"/9":"/3")+" · Собрано нот: "+game.CollectedCoins;
            resumeButton.gameObject.SetActive(paused);
            reviveButton.gameObject.SetActive(dead && game.CanRevive);
            reviveButton.interactable=dead && game.CanRevive;
            lastReviveSeconds=-1;
            nextButton.gameObject.SetActive(!paused && !dead && game.CanNext);
        }

        private void Update()
        {
            if(!initialized || canvas==null || safeArea==null) return;
            if(Screen.width!=lastWidth || Screen.height!=lastHeight || Screen.safeArea!=lastSafeArea) ApplySafeArea();
            if(game.Phase==ClonePhase.Playing || game.Phase==ClonePhase.Ready)
            {
                float progress=Mathf.Clamp01(game.Progress);
                var fill=(RectTransform)hudFill.transform;
                fill.anchorMax=new Vector2(.08f+.84f*progress,.907f);
                int percent=Mathf.FloorToInt(progress*100);
                if(percent!=lastProgressPercent) {lastProgressPercent=percent;hudProgress.text=percent+"%";}
            }
            else if(game.Phase==ClonePhase.Dead)
            {
                bool allowed=game.CanRevive;
                if(reviveButton.gameObject.activeSelf!=allowed) reviveButton.gameObject.SetActive(allowed);
                reviveButton.interactable=allowed;
                int seconds=Mathf.CeilToInt(game.ReviveSeconds);
                if(allowed && seconds!=lastReviveSeconds)
                {
                    lastReviveSeconds=seconds;
                    reviveLabel.text="Вернуться в эфир · "+seconds+" с";
                }
            }
        }

        private void ApplySafeArea()
        {
            if(safeArea==null) return;
            lastWidth=Screen.width;lastHeight=Screen.height;lastSafeArea=Screen.safeArea;
            if(lastWidth<1 || lastHeight<1) return;
            safeArea.anchorMin=new Vector2(lastSafeArea.xMin/lastWidth,lastSafeArea.yMin/lastHeight);
            safeArea.anchorMax=new Vector2(lastSafeArea.xMax/lastWidth,lastSafeArea.yMax/lastHeight);
            safeArea.offsetMin=Vector2.zero;safeArea.offsetMax=Vector2.zero;
        }

        private static string Duration(double seconds)
        {
            int total=Math.Max(0,(int)Math.Round(seconds)); return (total/60)+":"+(total%60).ToString("00");
        }

        private RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var node=new GameObject(name,typeof(RectTransform));
            node.transform.SetParent(parent,false);
            var rect=(RectTransform)node.transform; SetAnchors(rect,min,max);return rect;
        }
        private static void SetAnchors(RectTransform rect,Vector2 min,Vector2 max)
        {rect.anchorMin=min;rect.anchorMax=max;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;}

        private UnityEngine.UI.Image Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color,bool rounded=false)
        {
            var rect=Rect(name,parent,min,max);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color=color;image.raycastTarget=false;
            if(rounded && art.roundedPanel!=null) {image.sprite=art.roundedPanel;image.type=UnityEngine.UI.Image.Type.Sliced;}
            return image;
        }
        private TMP_Text Label(string name,Transform parent,string text,float size,Color color,Vector2 min,Vector2 max,
            TextAlignmentOptions alignment=TextAlignmentOptions.Center,FontStyles style=FontStyles.Normal)
        {
            var rect=Rect(name,parent,min,max);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font=font;label.text=text;label.fontSize=size;label.color=color;label.alignment=alignment;label.fontStyle=style;
            label.raycastTarget=false;label.richText=false;label.textWrappingMode=TextWrappingModes.Normal;
            label.overflowMode=TextOverflowModes.Ellipsis;label.margin=new Vector4(2,0,2,0);return label;
        }
        private UnityEngine.UI.Button Button(string name,Transform parent,string text,float size,Color textColor,Vector2 min,Vector2 max,Color color,Action action)
        {
            var image=Panel(name,parent,min,max,color,true);image.raycastTarget=true;
            if(art.roundedButton!=null) {image.sprite=art.roundedButton;image.type=UnityEngine.UI.Image.Type.Sliced;}
            var button=image.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
            var states=button.colors;states.normalColor=Color.white;states.highlightedColor=new Color(.97f,.97f,.97f);states.pressedColor=new Color(.87f,.89f,.88f);states.selectedColor=Color.white;states.fadeDuration=.08f;button.colors=states;
            button.navigation=new Navigation {mode=Navigation.Mode.None};
            if(action!=null) button.onClick.AddListener(() => action());
            Label("Label",button.transform,text,size,textColor,new Vector2(.035f,.03f),new Vector2(.965f,.97f));return button;
        }
        private static TMP_Text LabelInButton(UnityEngine.UI.Button button) => button.transform.GetChild(0).GetComponent<TMP_Text>();
        private Graphic Icon(string name,Transform parent,Vector2 min,Vector2 max,RadioGlyph.Symbol symbol,Color color,Sprite sprite=null)
        {
            var rect=Rect(name,parent,min,max);
            if(sprite!=null) {var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.color=color;image.preserveAspect=true;image.raycastTarget=false;return image;}
            var mark=rect.gameObject.AddComponent<RadioGlyph>();mark.shape=symbol;mark.color=color;mark.raycastTarget=false;return mark;
        }
        private UnityEngine.UI.Button IconButton(string name,Transform parent,Vector2 min,Vector2 max,RadioGlyph.Symbol symbol,Sprite sprite,Action action,bool surface=true)
        {
            var button=Button(name,parent,"",12,Ink,min,max,surface?new Color(1,1,1,.17f):Color.clear,action);
            Icon("Icon",button.transform,new Vector2(.20f,.20f),new Vector2(.80f,.80f),symbol,Ink,sprite);return button;
        }

        private void OnDestroy()
        {
            initialized=false;
            ReleasePresentation();
        }

        private void ReleasePresentation()
        {
            if(game!=null) game.Changed-=Refresh;
            if(courierSelection!=null) courierSelection.Changed-=RefreshCouriers;
            if(canvas!=null)
            {
                canvas.gameObject.SetActive(false);
                Destroy(canvas.gameObject);
                canvas=null;
            }
            safeArea=null;
            lastPhase=(ClonePhase)(-1);lastScore=lastStreak=lastProgressPercent=-1;lastSongId=null;
            screens.Clear(); navLabels.Clear(); navMarks.Clear(); courierCards.Clear();
            workshopCards.Clear(); workshopStates.Clear(); songRows.Clear(); stationStates.Clear(); stationMarks.Clear();
            if(font!=null)
            {
                var textures=font.atlasTextures;
                if(textures!=null) for(int i=0;i<textures.Length;i++) if(textures[i]!=null) Destroy(textures[i]);
                if(font.material!=null) Destroy(font.material);
                Destroy(font);
                font=null;
            }
        }
    }
}
