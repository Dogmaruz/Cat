using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace DancingCats.Clone
{
    /// <summary>Behavior adapter for the recovered original UI prefabs. Art, fonts and item layouts stay authored.</summary>
    public sealed class OriginalUIPresenter : MonoBehaviour
    {
        private OriginalPlayController game;
        private readonly Dictionary<string, GameObject> panels = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly List<SongRow> songRows = new List<SongRow>();
        private readonly List<SongRow> recommendedRows = new List<SongRow>();
        private readonly List<RoleRow> roleRows = new List<RoleRow>();
        private readonly List<GameObject> houseRoleRows = new List<GameObject>();
        private readonly HashSet<int> dailyPicked = new HashSet<int>();
        private bool dailyChoosing;
        private Canvas canvas;
        private GameObject menu, hud, tokens, death, results, stop;
        private string popup, songFilter = "ALL", previewRole, pendingRole;
        private bool built, refreshing, trails, deadResults;
        private ClonePhase lastPhase = (ClonePhase)(-1);
        private OriginalHouseDefinition furniture;
        private int houseFloor = 1, houseSlot;
        private AudioSource clickAudio;
        private bool menuMusicPaused;
        private Coroutine settlementAnimation;
        private readonly List<Animator> hudMilestones = new List<Animator>();
        private int hudTier = -1, hudEarned = -1;

        private sealed class SongRow { public SongRecord Song; public GameObject Root; }
        private sealed class RoleRow { public OriginalRoleDefinition Role; public GameObject Root; public bool Trail; }
        public Canvas UICanvas => canvas;
        public bool IsBlockingInput => !string.IsNullOrEmpty(popup) || menu != null && Active(menu, "GuideInternalStart_UIPanel");

        public void Bind(OriginalPlayController controller)
        {
            if (game == controller && built) return;
            if (built) throw new InvalidOperationException("Original UI is already bound.");
            game = controller;
            TryBuild();
        }

        private void Update()
        {
            if (!built) { TryBuild(); return; }
            if (lastPhase != game.Phase) Refresh();
            if (game.Phase == ClonePhase.Playing)
            {
                Text(hud, "Node/ProgressScore_UIPanel/BG/ScoreTextBG/ScoreText", game.Score.ToString());
                Text(hud, "Node/ProgressScore_UIPanel/BG/ScoreTextBG/ScoreText/ScoreText (1)", game.Score.ToString());
                foreach (string name in new[] { "Prefect", "Prefect2", "Prefect3" })
                    Text(hud, "Node/Combo/Animation/" + name + "/Text", "X" + game.Streak);
                Set(hud, "Node/Combo", game.Streak > 1);
                Text(hud, "Coins/Image/Animation/Text", Progress.Gold.ToString());
                var fill = Component<UnityEngine.UI.Image>(hud, "Node/ProgressScore_UIPanel/BG/ProgressBG/Progress");
                if (fill != null) fill.fillAmount = game.Progress;
                UpdateHudMilestones();
            }
            if (game.Phase == ClonePhase.Dead && !deadResults)
            {
                Text(death, "BG/Continue/CountdownText", Mathf.CeilToInt(game.ReviveSeconds).ToString());
                Interactable(death, "BG/Continue/ContinueButton", game.CanRevive);
            }
        }

        private OriginalProgression Progress => game.Progression;

        private void UpdateHudMilestones()
        {
            if (!hud.activeInHierarchy) return;
            float progress = game.Progress;
            int count = progress >= 1 ? 3 : progress >= .65f ? 2 : progress >= .3f ? 1 : 0;
            int tier = game.Endless ? Mathf.Clamp((game.RunStars - count) / 3, 0, 2) : 0;
            if (tier != hudTier)
            {
                const string path = "Node/ProgressScore_UIPanel/BG/";
                var parent = Find(hud, path + "Parent");
                var progressRect = Find(hud, path + "ProgressBG") as RectTransform;
                string[] types = { "Star", "Crown", "Trophy" };
                var template = Find(hud, path + types[tier] + "BG");
                if (parent == null || progressRect == null || template == null)
                    throw new InvalidOperationException("Original HUD milestone hierarchy is missing.");
                foreach (Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                hudMilestones.Clear();
                Canvas.ForceUpdateCanvases();
                float[] thresholds = { .3f, .65f, 1 };
                for (int i = 0; i < thresholds.Length; i++)
                {
                    var marker = Instantiate(template.gameObject, parent, false);
                    marker.name = types[tier] + "BG_" + (i + 1);
                    marker.transform.localScale = Vector3.one;
                    var rect = marker.transform as RectTransform;
                    if (rect != null) rect.anchoredPosition = progressRect.anchoredPosition + new Vector2(progressRect.rect.width * thresholds[i], 0);
                    marker.SetActive(true);
                    var animator = marker.transform.Find(types[tier])?.GetComponent<Animator>();
                    hudMilestones.Add(animator);
                    if (animator != null) { animator.Play(i < count ? "StarBG_Loop" : "Normal", 0, 0); animator.Update(0); }
                }
                hudTier = tier; hudEarned = count;
            }
            else if (count != hudEarned)
            {
                for (int i = 0; i < hudMilestones.Count; i++)
                {
                    if ((i < count) == (i < hudEarned)) continue;
                    var animator = hudMilestones[i];
                    if (animator != null) animator.Play(i < count ? "StarBG" : "Normal", 0, 0);
                }
                hudEarned = count;
            }
        }

        private Sprite SongIcon(SongRecord song) => song != null ? Resources.Load<Sprite>("OriginalIcons/" + song.catalogId) : null;
        private Sprite SongIcon(string id) => SongIcon(game.Songs.FirstOrDefault(song => song.id == id));

        private void TryBuild()
        {
            if (built || game == null || game.Assets == null || game.Songs == null || game.Songs.Length == 0 || game.Progression == null) return;
            var root = new GameObject("OriginalUI", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("OriginalEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                events.transform.SetParent(transform, false);
            }
            menu = Panel("InternalStart_UIPanel");
            hud = Panel("Internal_UIPanel");
            death = Panel("Death_UIPanel");
            results = Panel("Settlement_UIPanel");
            stop = Panel("Stop_UIPanel");
            tokens = Panel("Tokens_UIPanel");
            var audio = Panel("Audio_UIPanel");
            clickAudio = Component<AudioSource>(audio, "ButtonClickAudio");
            // This original audio-only root is permanent; it is not a modal panel.
            audio.SetActive(true);
            WireMenu(); WireGame();
            Button(tokens, "Coins/Image/Animation/Button/Button", () => OpenPanel("DailyReward_UIPanel"));
            game.Changed += Refresh;
            Progress.Changed += Refresh;
            built = true;
            Refresh();
        }

        private GameObject Panel(string name)
        {
            if (panels.TryGetValue(name, out var existing)) return existing;
            var source = name == "MusicSettings" ? Resources.Load<GameObject>("ui/MusicSettings") : game.Assets.Prefab(name);
            if (source == null) throw new InvalidOperationException("Original UI prefab is missing: " + name);
            var panel = Instantiate(source, canvas.transform, false);
            panel.name = name;
            OriginalUILocalization.Bind(panel, name, Progress.State.settings.language);
            if (panel.transform is RectTransform rect)
            {
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            }
            foreach (var text in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (text.text == "Button") text.gameObject.SetActive(false);
            foreach (var audioSource in panel.GetComponentsInChildren<AudioSource>(true)) audioSource.playOnAwake = false;
            panels.Add(name, panel);
            panel.SetActive(false);
            return panel;
        }

        private void WireMenu()
        {
            var start = Find(menu, "StartBtn");
            if (start == null) throw new InvalidOperationException("Original pointer-down StartBtn is missing.");
            var startGraphic = start.GetComponent<UnityEngine.UI.Graphic>();
            if (startGraphic != null) startGraphic.raycastTarget = true;
            (start.GetComponent<OriginalStartPointer>() ?? start.gameObject.AddComponent<OriginalStartPointer>()).Bind(StartPlaying);
            Button(menu, "Animation/Return", () => game.Home());
            Button(menu, "L/MusicButton", () => OpenPanel("MusicList_UIPanel"));
            Button(menu, "R/ShopButton", () => OpenPanel("NewRoleShop_UIPanel"));
            Button(menu, "L/SetButton", () => OpenPanel("Set_UIPanel"));
            Button(menu, "L/BuildingButton/BuildingButton", () => OpenPanel("CatHouseBuilding_UIPanel"));
            Button(menu, "L/ActivitesButton", () => OpenPanel("ChallengeLevel_UIPanel"));
            Button(menu, "R/LoginRewardButton", () => OpenPanel("LoginReward_UIPanel"));
            Button(menu, "R/ChallengeLevelButton", () => OpenPanel("ChallengeLevel_UIPanel"));
            Button(menu, "R/PlayDiceButton", () => OpenPanel("PlayDice_UIPanel"));
            Button(menu, "R/PlayVideoButton", () => OpenPanel("DailyReward_UIPanel"));
            Button(menu, "L/UIToggleRoot_Pattern/Normal", () => game.Endless = false);
            Button(menu, "L/UIToggleRoot_Pattern/Endless", () => game.Endless = true);
            Action dismiss = () =>
            {
                Set(menu, "GuideInternalStart_UIPanel", false);
                PlayerPrefs.SetInt("DancingCatsClone.v1.howToPlaySeen", 1); PlayerPrefs.Save();
            };
            Button(menu, "GuideInternalStart_UIPanel/Image/GuideButton", dismiss);
            Button(menu, "GuideInternalStart_UIPanel/GuideCloseButton", dismiss);
            Set(menu, "GuideInternalStart_UIPanel", PlayerPrefs.GetInt("DancingCatsClone.v1.howToPlaySeen", 0) == 0);
            Set(menu, "L/ActivitesButton/Tips", false);
            Set(menu, "L/BuildingButton/Lock", false);
            Set(menu, "R/PlayVideoButton/Tap", false);
            Set(menu, "R/Role", false);
            Set(menu, "R/ChallengeLevelButton", true);
            Set(menu, "R/PlayDiceButton", true);
            Text(menu, "R/PlayVideoButton/Image/Text", "FREE");
        }

        private void WireGame()
        {
            Button(hud, "StopButton", () => game.Pause());
            Button(hud, "ReturnButton", () => game.Pause());
            Set(hud, "StopButton", true); Set(hud, "ReturnButton", false);
            Set(hud, "InvincibleTime", false);
            Set(hud, "Coins/Image/Animation/Button", false);
            Button(stop, "StopWindow/Resume", () => game.Resume());
            Button(stop, "StopWindow/Quit", () => ShowQuitPrompt(true));
            Button(stop, "PromptWindow/BG/No", () => ShowQuitPrompt(false));
            Button(stop, "PromptWindow/BG/Yes", () => game.Home());
            ShowQuitPrompt(false);
            Button(death, "BG/Continue/ContinueButton", () => game.Revive());
            Button(death, "BG/Continue", () => game.Revive());
            Button(death, "BG/NoThanksButton", () => game.AcceptFailure());
            Set(death, "BG/Guide", false); Set(death, "BG/NoThanksButton", true);
            Text(death, "BG/Continue/ContinueButton/Text (1)", "OFFLINE CONTINUE");
            Set(death, "BG/Continue/ContinueButton/ContinueTips", false);
            Button(results, "BtnHome", () => game.Home());
            Button(results, "BtnNext", () => game.NextSong());
            Button(results, "BtnNextRight", () => game.NextSong());
            Button(results, "BtnAdNext", () => game.UnlockNextOffline());
            Button(results, "BtnAdNextRight", () => game.UnlockNextOffline());
            Button(results, "BtnReplay", () => game.Retry());
            Button(results, "BtnRetry", () => game.Retry());
            Set(results, "BtnAdNext", false); Set(results, "BtnAdNextRight", false);
            Set(results, "BtnRetry", false); Set(results, "BtnNextRight", false);
            Set(results, "Button_AD", false); Set(results, "Button", false);
            Set(results, "Button_1", false);
            Set(results, "UIListView", true);
        }

        private void ShowQuitPrompt(bool visible)
        {
            Set(stop, "PromptWindow", visible);
            Set(stop, "StopWindow", !visible);
        }

        private void StartPlaying()
        {
            if (IsBlockingInput) return;
            if (game.Phase != ClonePhase.Menu && game.Phase != ClonePhase.Ready) return;
            if (game.Selected == null)
            {
                var song = game.Songs.FirstOrDefault(s => s.id == Progress.State.selectedSong) ?? game.Songs[0];
                game.SelectSong(song.catalogId);
            }
            game.BeginRun();
        }

        public void OpenPanel(string name)
        {
            bool challengeResults = name == "ChallengeLevel_UIPanel" && game.Phase == ClonePhase.Results && game.ChallengeRun;
            if (!built || game.Phase != ClonePhase.Menu && game.Phase != ClonePhase.Ready && !challengeResults) return;
            if (name == "Audio_UIPanel") name = "Set_UIPanel";
            bool first = !panels.ContainsKey(name);
            var panel = Panel(name);
            StopSongPreview();
            if (first)
            {
                switch (name)
                {
                    case "MusicList_UIPanel": WireMusic(panel); break;
                    case "NewRoleShop_UIPanel": WireRoles(panel); break;
                    case "Set_UIPanel": WireSettings(panel); break;
                    case "MusicSettings": WireCalibration(panel); break;
                    case "DailyReward_UIPanel": WireDaily(panel); break;
                    case "LoginReward_UIPanel": WireLogin(panel); break;
                    case "ChallengeLevel_UIPanel": WireChallenge(panel); break;
                    case "PlayDice_UIPanel": WireDice(panel); break;
                    case "CatHouseBuilding_UIPanel": WireHouse(panel); break;
                    case "NewSkin_UIPanel": WireNewSkin(panel); break;
                }
            }
            if (popup != null && panels.TryGetValue(popup, out var old))
            {
                foreach (var player in old.GetComponentsInChildren<AudioSource>(true)) player.Stop();
                old.SetActive(false);
            }
            popup = name;
            if (name == "DailyReward_UIPanel") dailyChoosing = Progress.State.dailyFreeClaims == 0;
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            if (name == "DailyReward_UIPanel")
            {
                var cards = Find(panel, "Animation/BG/Parent");
                for (int index = 0; cards != null && index < cards.childCount; index++)
                    if (Progress.DailyCardClaimed(index)) PresentDailyCard(cards.GetChild(index).gameObject, 1f);
            }
            if (name == "NewRoleShop_UIPanel" && roleRows.Count == 0) BuildRoles(panel);
            if (name == "PlayDice_UIPanel") Progress.StartDiceEvent(DateTime.Today);
            if (name == "MusicSettings")
            {
                PauseMenuMusic();
                var player = panel.GetComponentInChildren<AudioSource>(true);
                if (player != null && player.clip != null) { player.loop = true; player.Play(); }
            }
            Refresh();
            Canvas.ForceUpdateCanvases();
        }

        public void ClosePopup()
        {
            if (popup != null && panels.TryGetValue(popup, out var panel))
            {
                foreach (var player in panel.GetComponentsInChildren<AudioSource>(true)) player.Stop();
                panel.SetActive(false);
            }
            StopSongPreview();
            popup = null;
            Refresh();
        }

        private void WireMusic(GameObject panel)
        {
            Button(panel, "Hide", ClosePopup);
            var scroll = Component<UnityEngine.UI.ScrollRect>(panel, "Scroller");
            Transform template = Find(panel, "Scroller/placeholder/MusicLevelItem");
            if (template == null || scroll == null) throw new InvalidOperationException("Original song list template/scroller is missing.");
            var content = template.parent as RectTransform;
            template.gameObject.SetActive(false);
            SetupScroll(scroll, content);
            foreach (var song in game.Songs.OrderBy(s => s.id == "AlwaysRememberMeowCat" ? 0 : 1).ThenBy(s => s.catalog.endOrder).ThenBy(s => s.catalogId))
            {
                var item = Instantiate(game.Assets.Prefab("MusicLevelItem"), content, false);
                item.name = "MusicLevelItem_" + song.id;
                OriginalUILocalization.Bind(item, "MusicLevelItem", Progress.State.settings.language);
                item.SetActive(true);
                CleanGraphics(item);
                var row = new SongRow { Song = song, Root = item }; songRows.Add(row);
                Button(item, "Unlock/Play", () => SelectSong(song));
                Button(item, "Level_Icon/Play", () => PreviewSong(song));
                Button(item, "Lcok/Coin", () => Progress.TryBuySong(song));
                Button(item, "Lcok/AD", () => Progress.RewardSongUnlock(song));
                Button(item, "Unlock/XIn", () => Progress.ToggleFavorite(song.id));
                Text(item, "MusicName", song.title); Text(item, "ArtName", song.artist);
                Sprite(item, "Level_Icon/Icon", SongIcon(song));
                Set(item, "Level_Icon/Music_Play", false); Set(item, "Level_Icon/Dowload", false);
                foreach (string debug in new[] { "1star", "2star", "3star", "next", "unlock" }) Set(item, debug, false);
                Set(item, "Lcok/AD/Tips", false); Set(item, "Lcok/Coin/Tips (1)", false);
            }
            Transform filters = Find(panel, "UIListView__MusicList/Content/UIToggleRoot_MusicList");
            if (filters != null)
                foreach (Transform filter in filters)
                {
                    var label = filter.GetComponentInChildren<UnityEngine.UI.Text>(true);
                    string category = label != null ? label.text.Trim().ToUpperInvariant() : "ALL";
                    Button(filter.gameObject, "", () => { songFilter = category; LayoutSongs(panel); });
                }
            LayoutSongs(panel);
        }

        private AudioSource previewAudio;
        private void PreviewSong(SongRecord song)
        {
            PauseMenuMusic();
            if (previewAudio == null) { previewAudio = gameObject.AddComponent<AudioSource>(); previewAudio.playOnAwake = false; }
            previewAudio.Stop();
            previewAudio.clip = Resources.Load<AudioClip>(string.IsNullOrEmpty(song.previewAudioResource) ? song.audioResource : song.previewAudioResource);
            previewAudio.volume = Progress.State.settings.sound ? .6f : 0;
            if (previewAudio.clip != null) previewAudio.Play();
        }

        private void PauseMenuMusic()
        {
            if (game.Phase != ClonePhase.Menu || menuMusicPaused || game.Music == null || !game.Music.isPlaying) return;
            game.Music.Pause(); menuMusicPaused = true;
        }

        private void StopSongPreview()
        {
            if (previewAudio != null) previewAudio.Stop();
            if (menuMusicPaused && game.Phase == ClonePhase.Menu && game.Music != null) game.Music.UnPause();
            menuMusicPaused = false;
        }

        private void SelectSong(SongRecord song)
        {
            if (!Progress.IsSongUnlocked(song.id)) return;
            if (previewAudio != null) previewAudio.Stop();
            game.SelectSong(song.catalogId);
            ClosePopup();
        }

        private void LayoutSongs(GameObject panel)
        {
            int index = 0;
            foreach (SongRow row in songRows)
            {
                bool show = SongMatches(row.Song); row.Root.SetActive(show);
                if (show) Place(row.Root.transform as RectTransform, index++, 1, 230, 1080);
            }
            var scroll = Component<UnityEngine.UI.ScrollRect>(panel, "Scroller");
            if (scroll != null) { scroll.content.sizeDelta = new Vector2(0, index * 230); scroll.verticalNormalizedPosition = 1; }
        }

        private void BuildRecommendations()
        {
            foreach (var row in recommendedRows) { row.Root.SetActive(false); Destroy(row.Root); }
            recommendedRows.Clear();
            var list = Find(results, "UIListView");
            var template = Find(results, "UIListView/Cell");
            var viewport = Find(results, "UIListView/Viewport") as RectTransform;
            var content = Find(results, "UIListView/Viewport/Content") as RectTransform;
            if (list == null || template == null || viewport == null || content == null)
                throw new InvalidOperationException("Original settlement song list hierarchy is missing.");
            template.gameObject.SetActive(false);
            var scroll = list.GetComponent<UnityEngine.UI.ScrollRect>() ?? list.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.onValueChanged.RemoveAllListeners();
            scroll.viewport = viewport;
            SetupScroll(scroll, content, true);
            int index = 0;
            foreach (var song in RecommendedSongs())
            {
                var item = Instantiate(template.gameObject, content, false);
                item.name = "Recommendation_" + index + "_" + song.id;
                OriginalUILocalization.Bind(item, "MusicLevelItem", Progress.State.settings.language);
                item.SetActive(true); CleanGraphics(item);
                Place(item.transform as RectTransform, index, 1, 225, 1080);
                var rect = item.transform as RectTransform;
                if (rect != null) rect.anchoredPosition = new Vector2(0, -820 - index * 230);
                var row = new SongRow { Song = song, Root = item }; recommendedRows.Add(row);
                Button(item, "Unlock/Play", () => SelectSong(song));
                Button(item, "Level_Icon/Play", () => PreviewSong(song));
                Button(item, "Lcok/Coin", () => { if (Progress.TryBuySong(song)) SelectSong(song); });
                Button(item, "Lcok/AD", () => { if (Progress.RewardSongUnlock(song)) SelectSong(song); });
                Text(item, "MusicName", song.title); Text(item, "ArtName", song.artist);
                Sprite(item, "Level_Icon/Icon", SongIcon(song));
                Set(item, "Level_Icon/Music_Play", false); Set(item, "Level_Icon/Dowload", false);
                foreach (string debug in new[] { "1star", "2star", "3star", "next", "unlock" }) Set(item, debug, false);
                Set(item, "Lcok/AD/Tips", false); Set(item, "Lcok/Coin/Tips (1)", false);
                RefreshSong(row);
                index++;
            }
            content.sizeDelta = new Vector2(content.sizeDelta.x, 820 + index * 225 + Mathf.Max(0, index - 1) * 5);
            content.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
            scroll.onValueChanged.AddListener(_ => UpdateSettlementHeader(content));
            UpdateSettlementHeader(content);
        }

        private void UpdateSettlementHeader(RectTransform content)
        {
            if (game.Phase != ClonePhase.Results || recommendedRows.Count == 0) return;
            var move = Find(results, "MoveContens") as RectTransform;
            var top = Find(results, "MoveContens/Image") as RectTransform;
            var first = recommendedRows[0].Root.transform as RectTransform;
            if (move == null || top == null || first == null || move.rect.height <= 0) return;
            // Native OnScrollPositionChanged reads the visible cell's position in the
            // viewport. ScrollRect moves Content instead of OSA's recycled cells.
            float cellY = first.anchoredPosition.y + content.anchoredPosition.y;
            float fraction = Mathf.Clamp01((-top.rect.height - cellY) / move.rect.height);
            float offset = (1 - fraction) * move.rect.height;
            move.anchoredPosition = new Vector2(move.anchoredPosition.x, offset);
            var wallet = tokens.transform as RectTransform;
            if (wallet != null) wallet.anchoredPosition = new Vector2(0, offset);
        }

        private IEnumerable<SongRecord> RecommendedSongs()
        {
            // Native GetNextTenSongIds: active catalog by ID, locked event music
            // 10032..10041 first, then locked songs, then never-started songs.
            var candidates = game.Songs.Where(song => !song.catalog.disable &&
                (song.catalogId < 901 || song.catalogId >= 10000 || Progress.IsSongUnlocked(song.id))).OrderBy(song => song.catalogId).ToArray();
            var rows = new List<SongRecord>();
            var added = new HashSet<string>(StringComparer.Ordinal);
            Action<SongRecord> add = song => { if (rows.Count < 10) { rows.Add(song); added.Add(song.id); } };
            foreach (var song in candidates.Where(song => song.catalogId >= 10032 && song.catalogId <= 10041 && !Progress.IsSongUnlocked(song.id))) add(song);
            foreach (var song in candidates.Where(song => !Progress.IsSongUnlocked(song.id))) add(song);
            foreach (var song in candidates.Where(song => (Progress.SongProgress(song.id)?.starts ?? 0) == 0)) add(song);
            if (rows.Count == 0)
            {
                int current = Array.FindIndex(candidates, song => game.Selected != null && song.id == game.Selected.id);
                foreach (var song in candidates.Skip(current + 1)) add(song);
            }
            foreach (var song in candidates.Where(song => !added.Contains(song.id))) add(song);
            return rows;
        }

        private bool SongMatches(SongRecord song)
        {
            switch (songFilter)
            {
                case "MEOW": return song.catalog.musicType == 0;
                case "PIANO": return song.catalog.musicType == 1;
                case "POP": return song.catalog.musicType == 2;
                case "HOT": return song.catalog.labelType == 1;
                case "NEW": return song.catalog.labelType == 2;
                case "ACTIVITES": return song.catalog.ad != 0;
                default: return true;
            }
        }

        private void RefreshSong(SongRow row)
        {
            var song = row.Song; var item = row.Root;
            bool unlocked = Progress.IsSongUnlocked(song.id);
            Set(item, "Lcok", !unlocked); Set(item, "Unlock", unlocked);
            // Native CellViewRow.SetData uses one authored button position: a nonzero
            // token price selects Coin; a zero token price with ad metadata selects AD.
            bool gold = song.catalog.gold > 0 && (song.catalog.musicUnlockType == 1 || song.catalog.musicUnlockType >= 3);
            bool offline = !gold && song.catalog.ad != 0 && song.catalog.musicUnlockType >= 2 && song.catalog.musicUnlockType <= 4;
            Set(item, "Lcok/Coin", !unlocked && gold); Set(item, "Lcok/AD", !unlocked && offline);
            Text(item, "Lcok/Coin/Text", song.catalog.gold.ToString());
            Text(item, "Unlock/Score/ScroeText", game.GetSongBest(song.catalogId).ToString());
            Set(item, "Unlock/XIn", false);
            Set(item, "Select", game.Selected != null && game.Selected.id == song.id);
            Set(item, "Tap/New", song.catalog.labelType == 2); Set(item, "Tap/Hot", song.catalog.labelType == 1);
            Text(item, "Tap/New/Text (1)", "NEW"); Text(item, "Tap/Hot/Text (1)", "HOT");
            Set(item, "Difficulty/Hard", song.catalog.difficultyType == 1);
            Set(item, "Difficulty/Expert", song.catalog.difficultyType >= 2);
            int stars = game.GetSongStars(song.catalogId);
            int tier = stars <= 0 ? 0 : Mathf.Clamp((stars - 1) / 3, 0, 2);
            int count = Mathf.Clamp(stars - tier * 3, 0, 3);
            string[] groups = { "Star", "Crown", "Trophy" };
            for (int group = 0; group < groups.Length; group++)
            {
                Set(item, "Unlock/" + groups[group], group == tier);
                for (int i = 1; i <= 3; i++) Set(item, "Unlock/" + groups[group] + "/Star_BG" + i + "/Star", count >= i);
            }
        }

        private void WireRoles(GameObject panel)
        {
            Button(panel, "BG/CloseButton", ClosePopup);
            Button(panel, "BG/UIToggleRoot_RoleList/UIToggle_Role", () => { trails = false; BuildRoles(panel); });
            Button(panel, "BG/UIToggleRoot_RoleList/UIToggle_Trailing", () => { trails = true; BuildRoles(panel); });
            BuildRoles(panel);
        }

        private void BuildRoles(GameObject panel)
        {
            foreach (var row in roleRows) { row.Root.SetActive(false); Destroy(row.Root); }
            roleRows.Clear();
            var template = Find(panel, "BG/GridView_TopToBottom/Viewport/Content/RoleItem");
            var scroll = Component<UnityEngine.UI.ScrollRect>(panel, "BG/GridView_TopToBottom");
            if (template == null || scroll == null) throw new InvalidOperationException("Original role shop item/scroller is missing.");
            var content = template.parent as RectTransform; template.gameObject.SetActive(false);
            SetupScroll(scroll, content);
            var definitions = trails ? Progress.Config.trails : Progress.Config.roles;
            for (int index = 0; index < definitions.Length; index++)
            {
                OriginalRoleDefinition definition = definitions[index]; bool trail = trails;
                var item = Instantiate(template.gameObject, content, false); item.name = "RoleItem_" + definition.Name;
                item.SetActive(true); CleanGraphics(item); PlaceRole(item.transform as RectTransform, index);
                roleRows.Add(new RoleRow { Role = definition, Root = item, Trail = trail });
                Button(item, "", () => PreviewRole(definition.Name, trail));
                Button(item, "SelectButton", () => Wear(definition.Name, trail));
                Button(item, "GoldButton", () => UnlockRole(definition.Name, trail, false));
                Button(item, "AdButton", () => UnlockRole(definition.Name, trail, true));
                Sprite head = RoleHead(definition.Name, trail);
                Sprite(item, "Head", head); Sprite(item, "goHead/Head2", head);
                Text(item, "NameText", definition.Name); Set(item, "Red", false); Set(item, "Text (2)", false);
                Set(item, "GoldButton/Tips", false); Set(item, "AdButton/Tips (1)", false);
            }
            int rows = Mathf.CeilToInt(definitions.Length / 3f);
            content.sizeDelta = new Vector2(0, rows * 340 + Mathf.Max(0, rows - 1) * 30 + 30);
            scroll.verticalNormalizedPosition = 1;
            PreviewRole(trails ? Progress.SelectedTrail : Progress.SelectedRole, trails);
            Refresh();
        }

        private void Wear(string id, bool trail)
        {
            if (trail) Progress.SelectTrail(id); else Progress.SelectRole(id);
            game.RefreshRole();
            if (panels.TryGetValue("NewRoleShop_UIPanel", out var panel)) PreviewRole(id, trail);
        }

        private void UnlockRole(string id, bool trail, bool offline)
        {
            bool owned = trail ? Progress.IsTrailUnlocked(id) : Progress.IsRoleUnlocked(id);
            if (owned) return;
            if (offline) { if (trail) Progress.RewardTrailUnlock(id); else Progress.RewardRoleUnlock(id); }
            else { if (trail) Progress.TryBuyTrail(id); else Progress.TryBuyRole(id); }
            bool unlocked = trail ? Progress.IsTrailUnlocked(id) : Progress.IsRoleUnlocked(id);
            if (!unlocked) return;
            Wear(id, trail);
            if (!trail)
            {
                OpenPanel("NewSkin_UIPanel");
                RolePreview(panels["NewSkin_UIPanel"], "RawImage", "Camera", "Camera/Parent", id, false);
            }
        }

        private void PreviewRole(string id, bool trail)
        {
            previewRole = id;
            if (!panels.TryGetValue("NewRoleShop_UIPanel", out var panel)) return;
            RolePreview(panel, "BG/ButtonLeft/RawImage_Left", "Camera_Left", "Camera_Left/RoleParent", id, trail);
            Set(panel, "BG/ButtonLeft/Select_Left", trail ? Progress.SelectedTrail == id : Progress.SelectedRole == id);
            var unusedCamera = Component<Camera>(panel, "Camera_Right"); if (unusedCamera != null) unusedCamera.enabled = false;
        }

        private Sprite RoleHead(string id, bool trail)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var definition = trail ? Progress.Trail(id) : Progress.Role(id);
            if (definition != null && !string.IsNullOrEmpty(definition.headName)) return game.Assets.Sprite(definition.headName);
            if (trail)
            {
                int number; if (!int.TryParse(id, out number) || number <= 1) return game.Assets.Sprite("NoTrail");
                return game.Assets.Sprite("Icon" + (number - 1).ToString("D2"));
            }
            string name = game.Assets.RoleNames.FirstOrDefault(n => n.StartsWith("Role" + id + "_", StringComparison.Ordinal));
            return game.Assets.Sprite(name ?? "Role" + id + "_main");
        }

        private void RefreshRole(RoleRow row)
        {
            var item = row.Root; var definition = row.Role;
            bool owned = row.Trail ? Progress.IsTrailUnlocked(definition.Name) : Progress.IsRoleUnlocked(definition.Name);
            bool selected = (row.Trail ? Progress.SelectedTrail : Progress.SelectedRole) == definition.Name;
            Set(item, "SelectButton", owned && !selected); Set(item, "Use", selected);
            Set(item, "BGUse", selected); Set(item, "Bg", !selected); Set(item, "Unlock", !owned);
            Set(item, "GoldButton", !owned && definition.UnlockType == 1);
            Set(item, "AdButton", !owned && definition.UnlockType == 2);
            Text(item, "GoldButton/Text", definition.GoldNumber.ToString());
            Text(item, "AdButton/Text", (row.Trail ? Progress.TrailUnlockProgress(definition.Name) : Progress.RoleUnlockProgress(definition.Name)) + "/" + Math.Max(1, definition.AdNumber));
            bool bonus = definition.CoinPercent > 0; Set(item, "Gold", bonus);
            Text(item, "Gold/Text", "+" + Mathf.RoundToInt(definition.CoinPercent * 100) + "%");
        }

        private void WireSettings(GameObject panel)
        {
            const string prefix = "BG/BG/GameObject/";
            Button(panel, "BG/BG/HideButton", ClosePopup);
            Button(panel, prefix + "VoiceButton", () => Settings(!Progress.State.settings.sound, null, null, null));
            Button(panel, prefix + "ShockButton", () => Settings(null, !Progress.State.settings.vibration, null, null));
            Button(panel, prefix + "PostButton", () => Settings(null, null, !Progress.State.settings.postProcessing, null));
            Button(panel, prefix + "MusicButton", () => OpenPanel("MusicSettings"));
            Text(panel, prefix + "MusicButton/Icon/Text", "Adjust Music");
            Set(panel, "BG/AADebug", false); Set(panel, "BG/Debug", false);
            Set(panel, prefix + "OrganicToggle", false); Set(panel, "BG/Normal", false);
            var language = Component<UnityEngine.UI.Dropdown>(panel, prefix + "Language/Dropdown");
            if (language != null)
            {
                if (language.targetGraphic != null) language.targetGraphic.raycastTarget = true;
                if (language.template != null)
                {
                    var templateGraphic = language.template.GetComponent<UnityEngine.UI.Graphic>();
                    if (templateGraphic != null) templateGraphic.raycastTarget = true;
                    foreach (var selectable in language.template.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                        if (selectable.targetGraphic != null) selectable.targetGraphic.raycastTarget = true;
                }
                language.onValueChanged.RemoveAllListeners();
                language.onValueChanged.AddListener(value =>
                {
                    var s = Progress.State.settings;
                    Progress.SetSettings(s.sound, s.vibration, s.postProcessing, value, s.calibrationOffsetSeconds);
                });
            }
        }

        private void WireCalibration(GameObject panel)
        {
            Button(panel, "Ctrl/Plus", () => Progress.AdjustCalibration(1));
            Button(panel, "Ctrl/Reduce", () => Progress.AdjustCalibration(-1));
            Button(panel, "Ctrl/Back", () => OpenPanel("Set_UIPanel"));
        }

        private void Settings(bool? sound, bool? vibration, bool? graphics, float? offset)
        {
            var s = Progress.State.settings;
            Progress.SetSettings(sound ?? s.sound, vibration ?? s.vibration, graphics ?? s.postProcessing, s.language, offset ?? s.calibrationOffsetSeconds);
            game.SetVolume(Progress.State.settings.sound ? 1 : 0);
            game.NotifyChanged();
        }

        private void WireDaily(GameObject panel)
        {
            // Native Show hides goTimer before enabling card selection. Its authored transparent
            // fullscreen Image otherwise consumes every pointer event above the nine cards.
            Set(panel, "BGTime", false);
            Button(panel, "Animation/BG/CloseButton", ClosePopup);
            var parent = Find(panel, "Animation/BG/Parent");
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                int index = i; var card = parent.GetChild(i).gameObject;
                if (Progress.DailyCardClaimed(i)) dailyPicked.Add(i);
                Set(card, "Animation/Win", false);
                Button(card, "", () => ClaimCard(card, index));
            }
            Button(panel, "Animation/BG/PlayVideoButton", () =>
            {
                dailyChoosing = true;
                Refresh();
            });
            Text(panel, "Animation/BG/Title/Text (1)", "Lucky Gift");
            Set(panel, "Animation/BG/PlayVideoButton/Tips", false);
            Set(panel, "Animation/BG/PlayVideoButton/Image (3)", false);
        }

        private void ClaimCard(GameObject card, int index)
        {
            if (!dailyChoosing || dailyPicked.Contains(index)) return;
            var receipt = Progress.FlipDailyCard(DateTime.Today, index, Environment.TickCount, true);
            if (!receipt.success) return;
            dailyPicked.Add(index);
            dailyChoosing = false;
            Set(card, "Animation/Win", true); Set(card, "Animation/BG", false);
            Text(card, "Animation/Win/Text", receipt.gold > 0 ? "X" + receipt.gold : receipt.diamonds > 0 ? "X" + receipt.diamonds : "");
            bool role = !string.IsNullOrEmpty(receipt.role), music = !string.IsNullOrEmpty(receipt.music);
            Set(card, "Animation/Win/Role", role); Set(card, "Animation/Win/Music", music);
            Set(card, "Animation/Win/Icon", !role && !music);
            var rewardArt = Progress.Config.dailyRewards.FirstOrDefault(r => r.goldNumber == receipt.gold && !string.IsNullOrEmpty(r.headName));
            if (rewardArt != null) Sprite(card, "Animation/Win/Icon", game.Assets.Sprite(rewardArt.headName));
            Sprite(card, "Animation/Win/Role", role ? RoleHead(receipt.role, false) : null);
            Sprite(card, "Animation/Win/Music/Head", music ? SongIcon(receipt.music) : null);
            PresentDailyCard(card, 0f);
            Refresh();
        }

        private void PresentDailyCard(GameObject card, float normalizedTime)
        {
            var animator = Component<Animator>(card, "Animation");
            if (animator == null) throw new InvalidOperationException("The original daily card Animator is missing.");
            if (!animator.isInitialized) { animator.Rebind(); animator.Update(0f); }
            animator.Play("DailyReward_Pick", 0, normalizedTime);
            // Disabling the popup resets this Animator. Restore only the visual pose on reopen;
            // the session ledger already owns the receipt and no reward action runs here.
            if (normalizedTime >= 1f) animator.Update(0f);
        }

        private void WireLogin(GameObject panel)
        {
            Button(panel, "RewardButton", () => Progress.ClaimLogin(DateTime.Today));
            Button(panel, "DoubleRewardButton", () => Progress.ClaimLogin(DateTime.Today, true));
            Button(panel, "CloseButton", ClosePopup);
            Text(panel, "Title/Text (3)", "7-Day Reward");
            Set(panel, "DoubleRewardButton/Tips", false);
        }

        private void RefreshLogin(GameObject panel)
        {
            bool can = Progress.CanClaimLogin(DateTime.Today);
            Set(panel, "RewardButton", can);
            Set(panel, "DoubleRewardButton", can && Progress.NextLoginReward.landRweardType == 1);
            Set(panel, "CloseButton", !can);
            int claims = Progress.State.loginClaims;
            int displayClaim = Mathf.Max(0, claims - (can ? 0 : 1));
            int claimedInWeek = can || claims == 0 ? claims % 7 : (claims - 1) % 7 + 1;
            int set = Mathf.Min(Progress.Config.loginRewardSets.Length - 1, displayClaim / 7);
            var rewards = Progress.Config.loginRewardSets[set].Days;
            bool specialSeventh = rewards[6].landRweardType == 2;
            Set(panel, "LoginRewardItem_Role", specialSeventh);
            Set(panel, "LoginRewardItem (6)", !specialSeventh);
            for (int i = 0; i < 7; i++)
            {
                string path = i == 0 ? "Reward/LoginRewardItem" : i < 6 ? "Reward/LoginRewardItem (" + i + ")" : specialSeventh ? "LoginRewardItem_Role" : "LoginRewardItem (6)";
                var item = Find(panel, path);
                if (item == null) continue;
                var reward = rewards[i]; bool role = reward.landRweardType == 2;
                bool special = i == 6 && specialSeventh;
                // Native cards contain duplicate Image names; the final direct Image is goOne,
                // the claimed dimmer, while earlier siblings carry the authored reward artwork.
                var directImages = item.Cast<Transform>().Where(t => t.name == "Image").ToArray();
                Set(item.gameObject, "Gold", !role); Set(item.gameObject, "Role", role && !special);
                Text(item.gameObject, "Gold/Dowm/Text", "X" + reward.diamondNumber);
                if (role && !special) Text(item.gameObject, "Role/Dowm/Text", "New Character");
                if (special)
                {
                    if (directImages.Length > 1) Text(directImages[1].gameObject, "Text", "Day7");
                    if (directImages.Length > 0) Text(directImages[0].gameObject, "Text (1)", "Day7");
                }
                Text(item.gameObject, "Gold/Image/DayText", "Day" + (i + 1));
                Text(item.gameObject, "Role/Image/DayText", "Day" + (i + 1));
                Text(item.gameObject, "Gold/Image/Text", "Day" + (i + 1));
                Text(item.gameObject, "Role/Image/Text", "Day" + (i + 1));
                if (role && !special) Sprite(item.gameObject, "Role/Head", RoleHead(reward.roleIdName, false));
                else if (!string.IsNullOrEmpty(reward.headName)) Sprite(item.gameObject, "Gold/Head/Head", game.Assets.Sprite(reward.headName));
                Set(item.gameObject, "Complete", i < claimedInWeek);
                if (directImages.Length > 0) directImages[directImages.Length - 1].gameObject.SetActive(i < claimedInWeek);
                Set(item.gameObject, "Select", can && i == Progress.LoginDayNumber - 1);
            }
        }

        private void WireChallenge(GameObject panel)
        {
            Button(panel, "BG/Start/CloseButton", ClosePopup);
            Action play = () =>
            {
                string id = Progress.ChallengeSong(DateTime.Today);
                var song = game.Songs.FirstOrDefault(s => s.id == id);
                if (song == null) return;
                if (!Progress.IsSongUnlocked(id)) Progress.RewardSongUnlock(song);
                if (!Progress.IsSongUnlocked(id)) return;
                SelectSong(song);
                game.Endless = true;
                game.ChallengeRun = true;
                game.NotifyChanged();
            };
            Button(panel, "BG/Start/AdButton", play); Button(panel, "BG/Start/Start", play);
            Button(panel, "BG/Win/CloseButton (1)", ClaimChallenge);
            Button(panel, "BG/Win/AdButton (1)", ClaimChallenge);
            Button(panel, "BG/Win/OverCloseButton", ClosePopup);
            Set(panel, "BG/Win/AdButton (1)", false);
            Set(panel, "GET", false);
            Set(panel, "BG/Start/AdButton/Tips", false);
            Text(panel, "BG/Title/Title/Text (1)", "Daily Challenge");
            Text(panel, "BG/Title/TitleOver/Text (1)", "Challenge Reward");
        }

        private void ClaimChallenge()
        {
            for (int tier = 0; tier < 3; tier++)
                if (Progress.CanClaimChallenge(DateTime.Today, tier)) Progress.ClaimChallenge(DateTime.Today, tier);
            Refresh();
        }

        private void WireDice(GameObject panel)
        {
            Button(panel, "CloseButton", ClosePopup);
            Button(panel, "Button_Bg/FreeButton", () => RollDice(false));
            Button(panel, "Button_Bg/AdButton", () => RollDice(true));
            Button(panel, "RoleUI/Button", () => Set(panel, "RoleUI", false));
            Button(panel, "Coins/Button", () => Set(panel, "Coins", false));
            foreach (string path in new[] { "RoleUI/BG", "Coins/BG" })
            {
                var background = Component<UnityEngine.UI.Graphic>(panel, path);
                if (background != null) background.raycastTarget = true;
            }
            Set(panel, "RoleUI", false); Set(panel, "Coins", false); Set(panel, "SieveButton", false);
            Set(panel, "Button_Bg/AdButton/Tips", false);
            Text(panel, "CloseButton/Text", "");
            Text(panel, "Button_Bg/FreeButton/Text", "");
            Text(panel, "Button_Bg/FreeButton/Image (1)/Text (2)", "Roll Dice");
            Text(panel, "Button_Bg/AdButton/Text", "");
            Text(panel, "Button_Bg/AdButton/Roll/Text (2)", "OFFLINE ROLL");
            BuildDiceBoard(panel);
        }

        private void BuildDiceBoard(GameObject panel)
        {
            var track = panel.GetComponent<OriginalDicePresentation>() ?? panel.AddComponent<OriginalDicePresentation>();
            track.Bind(Progress, id => RoleHead(id, false));
        }

        private void RollDice(bool offline)
        {
            var panel = panels["PlayDice_UIPanel"];
            var track = panel.GetComponent<OriginalDicePresentation>();
            if (track != null && track.IsRolling || Active(panel, "RoleUI") || Active(panel, "Coins")) return;
            int face = Progress.NextDiceFace(DateTime.Today, Environment.TickCount, offline);
            track?.BeginRoll(face);
            var receipt = Progress.RollDice(DateTime.Today, face, offline);
            if (!receipt.success) { track?.CancelRoll(); return; }
            Action showReward = () =>
            {
                if (!string.IsNullOrEmpty(receipt.role))
                {
                    Set(panel, "RoleUI", true); Sprite(panel, "RoleUI/BG/RoleHead", RoleHead(receipt.role, false));
                }
                else if (receipt.gold > 0)
                {
                    Set(panel, "Coins", true); Text(panel, "Coins/BG/Image (3)/Text", "Coins X" + receipt.gold);
                }
                Refresh();
            };
            if (track == null) showReward(); else track.FinishRoll(receipt.position, showReward);
            Refresh();
        }

        private void WireHouse(GameObject panel)
        {
            Button(panel, "CloseButton", ClosePopup);
            Button(panel, "Down/Image/CloseDowmButton", () => Set(panel, "Down", false));
            Button(panel, "RoleList/Image/CloseRoleListButton", () => Set(panel, "RoleList", false));
            Set(panel, "Down", false); Set(panel, "RoleList", false); Set(panel, "Tips", false);
            var confirmationBackground = Component<UnityEngine.UI.Graphic>(panel, "Tips/BG");
            if (confirmationBackground != null) confirmationBackground.raycastTarget = true;
            foreach (var row in Progress.Config.houseRows.Where(r => r.type != "1"))
            {
                var item = row;
                Button(panel, "UIListView_CatHouseBuilding/Content/" + row.floor + "/Build/" + row.type, () => OpenFurniture(item));
            }
            for (int floor = 1; floor <= 4; floor++)
                for (int slot = 0; slot < 3; slot++)
                {
                    int room = floor, index = slot;
                    Button(panel, "UIListView_CatHouseBuilding/Content/" + floor + "/Cats/Cat_" + (slot + 1) + "/Button", () => OpenCatSlot(room, index));
                }
            for (int variant = 0; variant < 3; variant++)
            {
                int index = variant;
                string item = "Down/Image/Parent/DownItem_" + (variant + 1);
                Button(panel, item + "/Head", () => HouseVariant(index, false));
                Button(panel, item + "/FreeButton", () => HouseVariant(index, false));
                Button(panel, item + "/GoldButton", () => HouseVariant(index, false));
                Button(panel, item + "/AdButton", () => HouseVariant(index, true));
                Set(panel, item + "/GoldButton/GoldTips", false); Set(panel, item + "/AdButton/AdTips", false);
            }
            Button(panel, "RoleList/Image/Lock/GoldUnlockButton", () => { Progress.TryUnlockCatSlot(houseFloor, houseSlot); RefreshCatSlot(); });
            Button(panel, "RoleList/Image/Lock/AdUnlockButton", () => { Progress.TryUnlockCatSlot(houseFloor, houseSlot, true); RefreshCatSlot(); });
            Button(panel, "Tips/BG/Image/NoButton", () => Set(panel, "Tips", false));
            Button(panel, "Tips/BG/Image/YesButton", () =>
            {
                Progress.AssignCatToSlot(houseFloor, houseSlot, pendingRole, true);
                Set(panel, "Tips", false); RefreshCatSlot();
            });
            var scroll = Component<UnityEngine.UI.ScrollRect>(panel, "UIListView_CatHouseBuilding");
            if (scroll != null) SetupScroll(scroll, scroll.content, true);
        }

        private void OpenFurniture(OriginalHouseDefinition row)
        {
            furniture = row;
            var panel = panels["CatHouseBuilding_UIPanel"];
            Set(panel, "Down", true); RefreshFurniture();
        }

        private void HouseVariant(int variant, bool offline)
        {
            if (furniture == null) return;
            if (!Progress.SelectHouseVariant(furniture.id, variant)) Progress.TryBuyHouseVariant(furniture.id, variant, offline);
            Progress.SelectHouseVariant(furniture.id, variant); RefreshFurniture(); Refresh();
        }

        private void RefreshFurniture()
        {
            if (furniture == null || !panels.TryGetValue("CatHouseBuilding_UIPanel", out var panel)) return;
            var state = Progress.HouseProgress(furniture.id);
            for (int index = 0; index < 3; index++)
            {
                string path = "Down/Image/Parent/DownItem_" + (index + 1);
                bool owned = state != null && state.owned.Contains(index);
                int price = index < furniture.unlockNumber.Length ? furniture.unlockNumber[index] : 0;
                Set(panel, path + "/FreeButton", owned);
                Set(panel, path + "/GoldButton", !owned && price >= 0);
                Set(panel, path + "/AdButton", !owned && price < 0);
                Text(panel, path + "/GoldButton/Text", price.ToString());
                Text(panel, path + "/AdButton/Text", "OFFLINE " + Math.Abs(price));
                Text(panel, path + "/FreeButton/Text", state != null && state.selected == index ? "SELECTED" : "SELECT");
                if (furniture.icon != null && index < furniture.icon.Length)
                    Sprite(panel, path + "/Head", game.Assets.Sprite(furniture.icon[index]));
            }
        }

        private void OpenCatSlot(int floor, int slot)
        {
            houseFloor = floor; houseSlot = slot;
            var panel = panels["CatHouseBuilding_UIPanel"];
            Set(panel, "RoleList", true);
            var template = Find(panel, "RoleList/Image/UIListView__RoleList/Content/CatsItem");
            var scroll = Component<UnityEngine.UI.ScrollRect>(panel, "RoleList/Image/UIListView__RoleList");
            if (template != null && scroll != null && houseRoleRows.Count == 0)
            {
                template.gameObject.SetActive(false); SetupScroll(scroll, template.parent as RectTransform);
                int index = 0;
                foreach (var definition in Progress.Config.roles)
                {
                    string id = definition.Name;
                    var item = Instantiate(template.gameObject, template.parent, false); item.name = "CatsItem_" + id;
                    item.SetActive(true); CleanGraphics(item); Place(item.transform as RectTransform, index++, 3, 340, 300);
                    houseRoleRows.Add(item);
                    Sprite(item, "Button/Head", RoleHead(id, false));
                    Button(item, "SelectButton", () => AssignCat(id));
                    Button(item, "Button", () => AssignCat(id));
                }
                scroll.content.sizeDelta = new Vector2(0, Mathf.CeilToInt(index / 3f) * 340);
            }
            RefreshCatSlot();
        }

        private void AssignCat(string id)
        {
            var panel = panels["CatHouseBuilding_UIPanel"];
            if (!Progress.IsRoleUnlocked(id)) return;
            if (!Progress.AssignCatToSlot(houseFloor, houseSlot, id))
            {
                pendingRole = id; Set(panel, "Tips", true);
            }
            RefreshCatSlot();
        }

        private void RefreshCatSlot()
        {
            if (!panels.TryGetValue("CatHouseBuilding_UIPanel", out var panel)) return;
            var slot = Progress.CatSlot(houseFloor, houseSlot);
            bool unlocked = slot != null && slot.unlocked;
            Set(panel, "RoleList/Image/Lock", !unlocked);
            var definition = Progress.Config.houseRows.FirstOrDefault(r => r.floor == houseFloor && r.type == "1");
            int price = definition != null && houseSlot < definition.unlockNumber.Length ? definition.unlockNumber[houseSlot] : 0;
            Set(panel, "RoleList/Image/Lock/GoldUnlockButton", !unlocked && price >= 0);
            Set(panel, "RoleList/Image/Lock/AdUnlockButton", !unlocked && price < 0);
            Text(panel, "RoleList/Image/Lock/GoldUnlockButton/Text", price.ToString());
            Text(panel, "RoleList/Image/Lock/AdUnlockButton/Text", "OFFLINE " + Math.Abs(price));
            foreach (var item in houseRoleRows)
            {
                string id = item.name.Substring("CatsItem_".Length);
                bool owned = Progress.IsRoleUnlocked(id);
                Interactable(item, "SelectButton", unlocked && owned);
                Interactable(item, "Button", unlocked && owned);
                Set(item, "Lock", !owned);
                Set(item, "AdButton", false); Set(item, "GoldButton", false);
                Text(item, "SelectButton/Text", !owned ? "LOCKED" : slot != null && slot.role == id ? "SELECTED" : "SELECT");
            }
        }

        private void WireNewSkin(GameObject panel)
        {
            Button(panel, "NextLevel", ClosePopup); Button(panel, "NoThanks", ClosePopup);
            Button(panel, "Unlock", ClosePopup);
            Set(panel, "Unlock", false); Set(panel, "AD", false); Set(panel, "NoThanks", false);
            Text(panel, "NextLevel/Text", "CONTINUE");
            RolePreview(panel, "RawImage", "Camera", "Camera/Parent", Progress.SelectedRole, false);
        }

        private void Refresh()
        {
            if (!built || refreshing) return;
            refreshing = true;
            try
            {
                bool changedPhase = lastPhase != game.Phase;
                if (changedPhase)
                {
                    if (lastPhase == ClonePhase.Results) StopSongPreview();
                    if (game.Phase == ClonePhase.Playing && lastPhase != ClonePhase.Paused && lastPhase != ClonePhase.Dead)
                        hudTier = -1;
                    if (settlementAnimation != null) { StopCoroutine(settlementAnimation); settlementAnimation = null; }
                    if (game.Phase != ClonePhase.Menu && game.Phase != ClonePhase.Ready)
                    {
                        if (popup != null && panels.TryGetValue(popup, out var open)) open.SetActive(false);
                        popup = null;
                        StopSongPreview();
                    }
                    if (game.Phase != ClonePhase.Dead) deadResults = false;
                    if (game.Phase == ClonePhase.Paused) ShowQuitPrompt(false);
                    lastPhase = game.Phase;
                }
                bool start = game.Phase == ClonePhase.Menu || game.Phase == ClonePhase.Ready;
                if (start && tokens.transform is RectTransform wallet) wallet.anchoredPosition = Vector2.zero;
                menu.SetActive(start); hud.SetActive((game.Phase == ClonePhase.Playing || game.Phase == ClonePhase.Paused) && !game.WinSequence);
                death.SetActive(game.Phase == ClonePhase.Dead && !deadResults);
                results.SetActive(game.Phase == ClonePhase.Results || game.Phase == ClonePhase.Dead && deadResults);
                if (changedPhase && game.Phase == ClonePhase.Results)
                {
                    BuildRecommendations();
                    settlementAnimation = StartCoroutine(PresentSettlement());
                    if (game.ChallengeRun) OpenPanel("ChallengeLevel_UIPanel");
                }
                stop.SetActive(game.Phase == ClonePhase.Paused);
                tokens.SetActive(start && !Active(menu, "GuideInternalStart_UIPanel"));
                Text(tokens, "Coins/Image/Animation/Text", Progress.Gold.ToString());
                Text(tokens, "Star/Image/Text", Progress.TotalStars.ToString());
                if (game.Selected != null)
                {
                    Text(menu, "Animation/BG_ 2/NameText", game.Selected.title);
                    Text(menu, "Animation/BG_ 2/ArtText", game.Selected.artist);
                    Text(menu, "Animation/BG_ 2/HighestScore/Text", game.GetSongBest(game.Selected.catalogId).ToString());
                    Text(results, "MoveContens/MusicName", game.Selected.title);
                    Text(results, "MoveContens/Image/Best/Image/MaxScores", game.GetSongBest(game.Selected.catalogId).ToString());
                }
                Text(results, "MoveContens/Image/Scores", game.Score.ToString());
                bool offlineNext = !game.CanNext && game.CanOfflineUnlockNext;
                Set(results, "BtnNext", !offlineNext); Set(results, "BtnAdNext", offlineNext);
                Text(results, "BtnNext/Next", game.CanNext ? "NEXT" : "SONGS");
                Text(results, "BtnAdNext/Next", "OFFLINE NEXT");
                Set(results, "BtnAdNext/AdTap", false); Set(results, "BtnAdNextRight/AdTap", false);
                if (game.SettlementQuote != null) Text(results, "Button_AD/Text", game.SettlementQuote.totalGold.ToString());
                Set(menu, "L/UIToggleRoot_Pattern/Normal/Background/Checkmark", !game.Endless);
                Set(menu, "L/UIToggleRoot_Pattern/Endless/Background/Checkmark", game.Endless);
                Text(menu, "Text", "Lv." + (1 + Progress.TotalStars / 3));
                var challengeSong = game.Songs.FirstOrDefault(s => s.id == Progress.ChallengeSong(DateTime.Today));
                if (challengeSong != null)
                {
                    Sprite(menu, "L/ActivitesButton/AD_Level/Animation/BG/Mask/Image", SongIcon(challengeSong));
                    Text(menu, "L/ActivitesButton/AD_Level/Animation/MusicName", challengeSong.title);
                }
                int difficulty = game.Selected != null && game.Selected.catalog != null ? game.Selected.catalog.difficultyType : 0;
                foreach (string key in new[] { "Normal", "Hard", "Expert" })
                    Set(menu, "Animation/BG_ 2/Image (1)/" + key, key == (difficulty <= 0 ? "Normal" : difficulty == 1 ? "Hard" : "Expert"));
                if (popup == "MusicList_UIPanel") foreach (var row in songRows) RefreshSong(row);
                if (game.Phase == ClonePhase.Results) foreach (var row in recommendedRows) RefreshSong(row);
                if (popup == "NewRoleShop_UIPanel") foreach (var row in roleRows) RefreshRole(row);
                if (popup == "Set_UIPanel") RefreshSettings(panels[popup]);
                if (popup == "MusicSettings") RefreshCalibration(panels[popup]);
                if (popup == "LoginReward_UIPanel") RefreshLogin(panels[popup]);
                if (popup == "DailyReward_UIPanel") RefreshDaily(panels[popup]);
                if (popup == "ChallengeLevel_UIPanel") RefreshChallenge(panels[popup]);
                if (popup == "PlayDice_UIPanel") RefreshDice(panels[popup]);
                if (popup == "CatHouseBuilding_UIPanel") RefreshHouse(panels[popup]);
                OriginalUILocalization.Apply(canvas.gameObject, Progress.State.settings.language);
            }
            finally { refreshing = false; }
        }

        private IEnumerator PresentSettlement()
        {
            int earned = Mathf.Clamp(game.RunStars, 0, 9);
            int tier = earned == 0 ? 0 : (earned - 1) / 3;
            int count = earned - tier * 3;
            string[] groups = { "Star", "Crown", "Trophy" };
            for (int group = 0; group < groups.Length; group++)
            {
                string path = "MoveContens/TheStar/" + groups[group];
                Set(results, path, group == tier);
                for (int number = 1; number <= 3; number++)
                    Set(results, path + "/" + groups[group] + "_" + number + "/Animation", false);
            }
            Set(results, "Particle", false);
            // Native SettlementUIPanel.showTime is .5; the three-icon timer pays FlyCoin
            // at its first wait and then starts each original Win_Star animation in order.
            for (int number = 1; number <= count; number++)
            {
                yield return new WaitForSeconds(.5f);
                if (game.Phase != ClonePhase.Results) yield break;
                if (number == 1 && game.SettlementQuote != null && game.SettlementQuote.eligible)
                {
                    game.PaySettlement();
                    Set(results, "Particle", true);
                    var applause = Component<AudioSource>(results, "Particle");
                    if (applause != null && Progress.State.settings.sound)
                    {
                        // The recovered Particle source is disabled until its reward cue.
                        applause.enabled = true;
                        applause.Play();
                    }
                }
                string path = "MoveContens/TheStar/" + groups[tier] + "/" + groups[tier] + "_" + number + "/Animation";
                Set(results, path, true);
                var animator = Component<Animator>(results, path);
                if (animator != null)
                {
                    animator.enabled = true;
                    if (animator.HasState(0, Animator.StringToHash("Win_Star"))) animator.Play("Win_Star", 0, 0f);
                }
                var sound = Component<AudioSource>(results, path);
                if (sound != null && Progress.State.settings.sound) sound.Play();
            }
            settlementAnimation = null;
        }

        private void RefreshSettings(GameObject panel)
        {
            const string prefix = "BG/BG/GameObject/";
            var s = Progress.State.settings;
            Set(panel, prefix + "VoiceButton/On", s.sound); Set(panel, prefix + "VoiceButton/Off", !s.sound);
            Set(panel, prefix + "ShockButton/On", s.vibration); Set(panel, prefix + "ShockButton/Off", !s.vibration);
            Set(panel, prefix + "PostButton/On", s.postProcessing); Set(panel, prefix + "PostButton/Off", !s.postProcessing);
            Text(panel, prefix + "MusicButton/Icon/Text", "Adjust Music");
            var language = Component<UnityEngine.UI.Dropdown>(panel, prefix + "Language/Dropdown");
            if (language != null) language.SetValueWithoutNotify(s.language);
        }

        private void RefreshCalibration(GameObject panel)
        {
            Text(panel, "Ctrl/Icon/Text", Progress.State.settings.rhythmLineHeight.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void RefreshDaily(GameObject panel)
        {
            int remaining = Progress.WheelClaimsRemaining(DateTime.Today);
            dailyPicked.Clear();
            for (int index = 0; index < 9; index++)
                if (Progress.DailyCardClaimed(index)) dailyPicked.Add(index);
            bool choosing = dailyChoosing && remaining > 0;
            Set(panel, "Animation/BG/PlayVideoButton", !choosing && remaining > 0);
            Set(panel, "Animation/BG/CloseButton", !choosing);
            Set(panel, "Animation/BG/Text (1)", choosing);
            Interactable(panel, "Animation/BG/PlayVideoButton", remaining > 0);
            Text(panel, "Animation/BG/PlayVideoButton/Text", Progress.State.dailyFreeClaims == 0 ? "FREE" : "OFFLINE REWARD");
            Set(panel, "Animation/BG/OverText", remaining == 0);
            Text(panel, "Animation/BG/Text (1)", "Choose a card for your reward!");
            var parent = Find(panel, "Animation/BG/Parent");
            for (int i = 0; parent != null && i < parent.childCount; i++)
                Interactable(parent.GetChild(i).gameObject, "", choosing && !dailyPicked.Contains(i));
        }

        private void RefreshChallenge(GameObject panel)
        {
            string id = Progress.ChallengeSong(DateTime.Today);
            var song = game.Songs.FirstOrDefault(s => s.id == id);
            Text(panel, "BG/MusicNameText", song != null ? song.title : "");
            bool claim = Enumerable.Range(0, 3).Any(i => Progress.CanClaimChallenge(DateTime.Today, i));
            bool result = game.Phase == ClonePhase.Results && game.ChallengeRun;
            bool rewards = result || claim;
            Set(panel, "BG/Title/Title", !result); Set(panel, "BG/Title/TitleOver", result);
            Set(panel, "BG/Text (1)", !result); Set(panel, "BG/MusicNameText", !result);
            Set(panel, "BG/Win", rewards); Set(panel, "BG/Start", !rewards);
            Set(panel, "BG/Win/CloseButton (1)", claim);
            Set(panel, "BG/Win/OverCloseButton", !claim);
            Set(panel, "BG/Start/AdButton", false); Set(panel, "BG/Start/Start", true);
            int marks = Progress.State.challengeStars;
            int pendingGold = Enumerable.Range(0, 3).Where(i => Progress.CanClaimChallenge(DateTime.Today, i)).Sum(i => Progress.Config.challengeRewards[i].goldNumber);
            Set(panel, "BG/GoldShowBG", rewards && pendingGold > 0);
            Text(panel, "BG/GoldShowBG/Text", pendingGold.ToString());
            var progress = Component<UnityEngine.UI.Image>(panel, "BG/Progress/ProgressBG/ProgressImage");
            if (progress != null) progress.fillAmount = marks >= 9 ? 1 : marks >= 7 ? .75f : marks >= 6 ? .5f : marks >= 4 ? .25f : 0;
            string[] checks = { "Image/GoldImage/Image", "Image (1)/GoldImage/Image (1)", "Image (2)/Image (2)" };
            for (int i = 0; i < 3; i++)
            {
                string path = "BG/Progress/" + (i == 0 ? "Image" : "Image (" + i + ")");
                var milestone = Component<UnityEngine.UI.Image>(panel, path);
                if (milestone != null) milestone.enabled = marks >= (i + 1) * 3;
                bool claimed = Progress.State.claimedChallengeTiers.Contains(i);
                Set(panel, "BG/Progress/" + checks[i], claimed);
                Set(panel, path + "/GoldImage/GoldText_" + (i + 1), !claimed);
                Text(panel, path + "/ScoresText_" + (i + 1), ((i + 1) * 3).ToString());
            }
        }

        private void RefreshDice(GameObject panel)
        {
            bool free = Progress.DiceRollsRemaining(DateTime.Today) > 0;
            bool extra = Progress.DiceRewardActionsRemaining(DateTime.Today) > 0;
            Set(panel, "Button_Bg/FreeButton", free); Set(panel, "Button_Bg/AdButton", !free && extra);
            Set(panel, "Button_Bg/Text", !free && !extra);
            var track = panel.GetComponent<OriginalDicePresentation>();
            bool rolling = track != null && track.IsRolling || Active(panel, "RoleUI") || Active(panel, "Coins");
            Interactable(panel, "Button_Bg/FreeButton", !rolling); Interactable(panel, "Button_Bg/AdButton", !rolling);
            if (track != null) track.Sync();
        }

        private void RefreshHouse(GameObject panel)
        {
            for (int floor = 1; floor <= 4; floor++)
            {
                string prefix = "UIListView_CatHouseBuilding/Content/" + floor;
                bool unlocked = Progress.IsFloorUnlocked(floor);
                Set(panel, prefix + "/Lock", !unlocked);
                foreach (var row in Progress.Config.houseRows.Where(r => r.floor == floor && r.type != "1"))
                {
                    var state = Progress.HouseProgress(row.id);
                    string part = prefix + "/Build/" + row.type;
                    bool placed = state != null && state.selected >= 0;
                    Set(panel, part + "/Head", placed);
                    Set(panel, part + "/Build", !placed && unlocked);
                    Set(panel, part + "/SelectedImage", furniture != null && furniture.id == row.id && Active(panel, "Down"));
                    if (state != null && row.icon != null && state.selected >= 0 && state.selected < row.icon.Length)
                        Sprite(panel, part + "/Head", game.Assets.Sprite(row.icon[state.selected]));
                }
                for (int index = 0; index < 3; index++)
                {
                    var slot = Progress.CatSlot(floor, index);
                    string path = prefix + "/Cats/Cat_" + (index + 1) + "/Button";
                    bool assigned = slot != null && !string.IsNullOrEmpty(slot.role);
                    Set(panel, path + "/Head", assigned);
                    Set(panel, path + "/Lock", slot == null || !slot.unlocked);
                    Set(panel, path + "/Increase", slot != null && slot.unlocked && !assigned);
                    if (assigned) Sprite(panel, path + "/Head", RoleHead(slot.role, false));
                }
            }
            RefreshFurniture();
            if (Active(panel, "RoleList")) RefreshCatSlot();
        }

        private static Transform Find(GameObject root, string path)
        {
            if (root == null) return null;
            if (string.IsNullOrEmpty(path)) return root.transform;
            return root.transform.Find(path);
        }
        private static T Component<T>(GameObject root, string path) where T : Component => Find(root, path)?.GetComponent<T>();
        private static bool Active(GameObject root, string path) { var node = Find(root, path); return node != null && node.gameObject.activeSelf; }
        private static void Set(GameObject root, string path, bool active) { var node = Find(root, path); if (node != null) node.gameObject.SetActive(active); }
        private static void Text(GameObject root, string path, string value)
        {
            var text = Component<UnityEngine.UI.Text>(root, path); if (text != null) OriginalUILocalization.SetText(text, value);
        }
        private static void Sprite(GameObject root, string path, Sprite sprite)
        {
            var image = Component<UnityEngine.UI.Image>(root, path);
            if (image != null && sprite != null) { image.sprite = sprite; image.preserveAspect = true; }
        }
        private void RolePreview(GameObject root, string path, string cameraPath, string parentPath, string id, bool trail)
        {
            var raw = Component<UnityEngine.UI.RawImage>(root, path);
            var camera = Component<Camera>(root, cameraPath);
            var parent = Find(root, parentPath);
            if (raw == null || camera == null || parent == null) throw new InvalidOperationException("Original 3D preview hierarchy is missing.");
            var preview = raw.GetComponent<OriginalRolePreview>() ?? raw.gameObject.AddComponent<OriginalRolePreview>();
            preview.Bind(game.Assets, Progress.Role(trail ? Progress.SelectedRole : id),
                trail ? Progress.Trail(id) : null, camera, parent);
        }
        private static void Interactable(GameObject root, string path, bool value)
        {
            var button = Component<UnityEngine.UI.Button>(root, path); if (button != null) button.interactable = value;
        }
        private void Button(GameObject root, string path, Action action)
        {
            var node = Find(root, path); if (node == null) return;
            var button = node.GetComponent<UnityEngine.UI.Button>() ?? node.gameObject.AddComponent<UnityEngine.UI.Button>();
            var image = node.GetComponent<UnityEngine.UI.Graphic>() ?? node.GetComponentInChildren<UnityEngine.UI.Graphic>(true);
            if (image != null) { image.raycastTarget = true; button.targetGraphic = image; }
            var navigation = button.navigation; navigation.mode = UnityEngine.UI.Navigation.Mode.None; button.navigation = navigation;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { if (clickAudio != null && Progress.State.settings.sound) clickAudio.Play(); action(); Refresh(); });
        }
        private static void CleanGraphics(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true)) if (text.text == "Button") text.gameObject.SetActive(false);
        }
        private static void SetupScroll(UnityEngine.UI.ScrollRect scroll, RectTransform content, bool preserveLayout = false)
        {
            if (content == null) throw new InvalidOperationException("Recovered scroll content is missing.");
            scroll.content = content;
            if (scroll.viewport == null) scroll.viewport = scroll.transform as RectTransform;
            var viewport = scroll.viewport;
            if (viewport.GetComponent<UnityEngine.UI.Mask>() == null && viewport.GetComponent<UnityEngine.UI.RectMask2D>() == null)
                viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var graphic = viewport.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic == null)
            {
                var hit = viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = Color.clear; graphic = hit;
            }
            graphic.raycastTarget = true;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            if (preserveLayout) return;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            foreach (var layout in content.GetComponents<UnityEngine.UI.LayoutGroup>()) layout.enabled = false;
            var fitter = content.GetComponent<UnityEngine.UI.ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
        }
        private static void PlaceRole(RectTransform rect, int index)
        {
            if (rect == null) return;
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(278, 340);
            rect.anchoredPosition = new Vector2(30 + index % 3 * 326, -(index / 3) * 370);
        }
        private static void Place(RectTransform rect, int index, int columns, float height, float width)
        {
            if (rect == null) return;
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            if (columns == 1)
            {
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
                rect.sizeDelta = new Vector2(0, height); rect.anchoredPosition = new Vector2(0, -(index / columns) * height);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(width - 20, height - 20);
                rect.anchoredPosition = new Vector2(index % columns * width + 20, -(index / columns) * height);
            }
        }
        private void OnDestroy()
        {
            if (game != null) game.Changed -= Refresh;
            if (game != null && game.Progression != null) game.Progression.Changed -= Refresh;
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}

