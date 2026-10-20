using System;
using System.Globalization;
using UnityEngine;

namespace DancingCat
{
    [Serializable]
    public sealed class CatMetaData
    {
        public int version = 1;
        public string lastDailyClaim = "";
        public int dailyStreak;
        public string challengeDay = "";
        public string challengeSong = "";
        public bool challengeComplete;
        public bool challengeClaimed;
        public int[] roomLevels = new int[4];
        public int ownedTrails = 1;
        public int selectedTrail;
        public int diceRolls;
        public string diceDay = "";
        public int dailyDiceRolls;
        public int lastDiceReward = -1;
        public float latencyMs;
        public bool vibration = true;
        public int targetFps = 60;
        public int difficulty;
        public bool endless;
    }

    /// <summary>Local rules with explicit dates and wallet transactions, independent of scenes and PlayerPrefs.</summary>
    public sealed class CatMetaRules
    {
        public static readonly int[] DailyRewards = { 50, 75, 100, 125, 150, 200, 300 };
        public static readonly int[] DiceRewards = { 0, 5, 10, 15, 25, 50 };
        public CatMetaData Data { get; }

        public CatMetaRules(CatMetaData data)
        {
            Data = data ?? new CatMetaData();
            var rooms = new int[4];
            for (var index = 0; index < rooms.Length; index++)
                rooms[index] = Mathf.Clamp(Data.roomLevels != null && index < Data.roomLevels.Length ? Data.roomLevels[index] : 0, 0, 5);
            Data.roomLevels = rooms;
            Data.dailyStreak = Mathf.Clamp(Data.dailyStreak, 0, 7);
            Data.ownedTrails = (Data.ownedTrails & 1023) | 1;
            if (!IsTrailOwned(Data.selectedTrail)) Data.selectedTrail = 0;
            Data.latencyMs = float.IsNaN(Data.latencyMs) ? 0 : Mathf.Clamp(Data.latencyMs, -200, 200);
            if (Data.targetFps != 30 && Data.targetFps != 60 && Data.targetFps != 120) Data.targetFps = 60;
            Data.difficulty = Mathf.Clamp(Data.difficulty, 0, 2);
            Data.diceRolls = Math.Max(0, Data.diceRolls);
            Data.dailyDiceRolls = Mathf.Clamp(Data.dailyDiceRolls, 0, 3);
            if (Data.version < 1) { Data.vibration = true; Data.version = 1; }
        }

        public bool CanClaimDaily(DateTime date) => !LastClaim(out var last) || date.Date > last;

        public int NextDailyReward(DateTime date)
        {
            var continuing = LastClaim(out var last) && (date.Date == last || date.Date == last.AddDays(1));
            return DailyRewards[continuing ? Data.dailyStreak % 7 : 0];
        }

        public int StreakAt(DateTime date) => LastClaim(out var last) && date.Date <= last.AddDays(1) ? Data.dailyStreak : 0;

        public int ClaimDaily(DateTime date)
        {
            if (!CanClaimDaily(date)) return 0;
            var continuing = LastClaim(out var last) && date.Date == last.AddDays(1);
            Data.dailyStreak = continuing ? Data.dailyStreak % 7 + 1 : 1;
            Data.lastDailyClaim = Day(date);
            return DailyRewards[Data.dailyStreak - 1];
        }

        public int ChallengeIndex(DateTime date, int songCount) => songCount > 0 ? (int)(date.Date.Ticks / TimeSpan.TicksPerDay % songCount) : -1;

        public void EnsureChallenge(DateTime date, string songId)
        {
            var day = Day(date);
            if (Data.challengeDay == day && Data.challengeSong == songId) return;
            Data.challengeDay = day;
            Data.challengeSong = songId ?? "";
            Data.challengeComplete = false;
            Data.challengeClaimed = false;
        }

        public bool RecordCompletion(DateTime date, string completedSongId, string challengeSongId)
        {
            EnsureChallenge(date, challengeSongId);
            if (string.IsNullOrEmpty(challengeSongId) || completedSongId != challengeSongId || Data.challengeComplete) return false;
            Data.challengeComplete = true;
            return true;
        }

        public bool CanClaimChallenge(DateTime date, string songId)
        {
            EnsureChallenge(date, songId);
            return !string.IsNullOrEmpty(songId) && Data.challengeComplete && !Data.challengeClaimed;
        }

        public int ClaimChallenge(DateTime date, string songId)
        {
            if (!CanClaimChallenge(date, songId)) return 0;
            Data.challengeClaimed = true;
            return 75;
        }

        public int RoomLevel(int room) => room >= 0 && room < 4 ? Data.roomLevels[room] : 0;
        public int RoomUpgradePrice(int room) => room >= 0 && room < 4 && RoomLevel(room) < 5 ? 40 * (RoomLevel(room) + 1) : 0;

        public bool TryUpgradeRoom(int room, Func<int, bool> spend)
        {
            var price = RoomUpgradePrice(room);
            if (price <= 0 || spend == null || !spend(price)) return false;
            Data.roomLevels[room]++;
            return true;
        }

        public bool IsTrailOwned(int trail) => trail >= 0 && trail < 10 && (Data.ownedTrails & (1 << trail)) != 0;
        public int TrailPrice(int trail) => trail > 0 && trail < 10 ? 30 + trail * 20 : 0;

        public bool TryUnlockTrail(int trail, Func<int, bool> spend)
        {
            if (trail <= 0 || trail >= 10 || IsTrailOwned(trail) || spend == null || !spend(TrailPrice(trail))) return false;
            Data.ownedTrails |= 1 << trail;
            return true;
        }

        public bool SelectTrail(int trail)
        {
            if (!IsTrailOwned(trail)) return false;
            Data.selectedTrail = trail;
            return true;
        }

        public int DiceRollsRemaining(DateTime date)
        {
            if (!DateTime.TryParseExact(Data.diceDay, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day)) return 3;
            if (date.Date < day) return 0;
            return date.Date > day ? 3 : 3 - Data.dailyDiceRolls;
        }

        public int RollDice(int seed, Func<int, bool> spend, Action<int> add) => RollDice(DateTime.Today, seed, spend, add);

        public int RollDice(DateTime date, int seed, Func<int, bool> spend, Action<int> add)
        {
            if (DiceRollsRemaining(date) <= 0 || spend == null || add == null || !spend(10)) return -1;
            var day = Day(date);
            if (Data.diceDay != day) { Data.diceDay = day; Data.dailyDiceRolls = 0; }
            var reward = DiceRewards[new System.Random(seed).Next(DiceRewards.Length)];
            Data.lastDiceReward = reward;
            Data.diceRolls++;
            Data.dailyDiceRolls++;
            if (reward > 0) add(reward);
            return reward;
        }

        private bool LastClaim(out DateTime date) => DateTime.TryParseExact(Data.lastDailyClaim, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        private static string Day(DateTime date) => date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    [DefaultExecutionOrder(20), DisallowMultipleComponent]
    public sealed class CatMetaProgress : MonoBehaviour
    {
        public const string SaveKey = "DancingCat.v2.Meta";
        public static readonly string[] TrailNames = { "LILAC", "BUBBLEGUM", "MINT", "SUNSHINE", "PEACH", "SKY", "CORAL", "LIME", "LAVENDER", "STARLIGHT" };
        public static readonly Color[] TrailColors =
        {
            new Color(.68f,.45f,1), new Color(1,.35f,.69f), new Color(.3f,1,.76f), new Color(1,.84f,.25f),
            new Color(1,.66f,.43f), new Color(.3f,.77f,1), new Color(1,.4f,.42f), new Color(.7f,1,.28f),
            new Color(.8f,.65f,1), new Color(1,.96f,.8f)
        };

        public event Action Changed;
        public CatMetaData Data => _rules.Data;
        public bool CanClaimDaily => _rules.CanClaimDaily(DateTime.Today);
        public int NextDailyReward => _rules.NextDailyReward(DateTime.Today);
        public int DailyStreak => _rules.StreakAt(DateTime.Today);
        public int SelectedTrail => Data.selectedTrail;
        public int DiceRollsRemaining => _rules.DiceRollsRemaining(DateTime.Today);
        public CatSong ChallengeSong
        {
            get
            {
                var songs = _controller != null ? _controller.Songs : null;
                var index = _rules.ChallengeIndex(DateTime.Today, songs?.Length ?? 0);
                return index >= 0 ? songs[index] : null;
            }
        }
        public bool ChallengeComplete { get { EnsureToday(); return Data.challengeComplete; } }
        public bool CanClaimChallenge => _rules.CanClaimChallenge(DateTime.Today, ChallengeSong != null ? ChallengeSong.id : "");

        private CatGameController _controller;
        private CatMetaRules _rules;
        private CatGamePhase _previousPhase;
        private DateTime _day;
        private bool _started;
        private bool _subscribed;

        private void Awake()
        {
            _controller = GetComponent<CatGameController>();
            CatMetaData data = null;
            var json = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { data = JsonUtility.FromJson<CatMetaData>(json); }
                catch (ArgumentException) { Debug.LogWarning("The local cat extras save could not be read; using defaults.", this); }
            }
            _rules = new CatMetaRules(data);
            _day = DateTime.Today;
        }

        private void Start()
        {
            if (_controller == null) { enabled = false; return; }
            _controller.Difficulty = Data.difficulty;
            _controller.Endless = Data.endless;
            _controller.AudioLatencyMs = Data.latencyMs;
            _controller.Vibration = Data.vibration;
            _controller.TargetFps = Data.targetFps;
            _controller.SetTrail(TrailColors[SelectedTrail]);
            _previousPhase = _controller.Phase;
            _started = true;
            Subscribe();
            EnsureToday();
        }

        private void OnEnable() { if (_started) Subscribe(); }
        private void OnDisable()
        {
            if (_subscribed && _controller != null) _controller.Changed -= ControllerChanged;
            _subscribed = false;
        }
        private void Subscribe()
        {
            if (_subscribed || _controller == null) return;
            _controller.Changed += ControllerChanged;
            _subscribed = true;
        }

        private void Update()
        {
            if (_day == DateTime.Today) return;
            _day = DateTime.Today;
            EnsureToday();
            Publish();
        }

        private void ControllerChanged()
        {
            var changed = false;
            if (_controller.Phase == CatGamePhase.Complete && _previousPhase != CatGamePhase.Complete && _controller.SelectedSong != null)
                changed = _rules.RecordCompletion(DateTime.Today, _controller.SelectedSong.id, ChallengeSong != null ? ChallengeSong.id : "");
            _previousPhase = _controller.Phase;
            if (Data.difficulty != _controller.Difficulty || Data.endless != _controller.Endless ||
                Data.latencyMs != _controller.AudioLatencyMs || Data.vibration != _controller.Vibration || Data.targetFps != _controller.TargetFps)
            {
                Data.difficulty = _controller.Difficulty;
                Data.endless = _controller.Endless;
                Data.latencyMs = _controller.AudioLatencyMs;
                Data.vibration = _controller.Vibration;
                Data.targetFps = _controller.TargetFps;
                changed = true;
            }
            if (changed) Publish();
        }

        public int ClaimDaily()
        {
            var reward = _rules.ClaimDaily(DateTime.Today);
            if (reward > 0) { Save(); _controller.AddCoins(reward); Changed?.Invoke(); }
            return reward;
        }

        public int ClaimChallenge()
        {
            var reward = _rules.ClaimChallenge(DateTime.Today, ChallengeSong != null ? ChallengeSong.id : "");
            if (reward > 0) { Save(); _controller.AddCoins(reward); Changed?.Invoke(); }
            return reward;
        }

        public int RoomLevel(int room) => _rules.RoomLevel(room);
        public int RoomUpgradePrice(int room) => _rules.RoomUpgradePrice(room);
        public bool TryUpgradeRoom(int room)
        {
            var success = _rules.TryUpgradeRoom(room, _controller.SpendCoins);
            if (success) Publish();
            return success;
        }
        public bool IsTrailOwned(int trail) => _rules.IsTrailOwned(trail);
        public int TrailPrice(int trail) => _rules.TrailPrice(trail);
        public bool TryUnlockTrail(int trail)
        {
            var success = _rules.TryUnlockTrail(trail, _controller.SpendCoins);
            if (success) Publish();
            return success;
        }
        public bool SelectTrail(int trail)
        {
            if (!_rules.SelectTrail(trail)) return false;
            _controller.SetTrail(TrailColors[trail]);
            Publish();
            return true;
        }
        public int RollDice()
        {
            var reward = _rules.RollDice(Environment.TickCount ^ Data.diceRolls, _controller.SpendCoins, _controller.AddCoins);
            if (reward >= 0) Publish();
            return reward;
        }
        public void SavePlayOptions() => ControllerChanged();
        private void EnsureToday() => _rules.EnsureChallenge(DateTime.Today, ChallengeSong != null ? ChallengeSong.id : "");
        private void Save() { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data)); PlayerPrefs.Save(); }
        private void Publish() { Save(); Changed?.Invoke(); }
    }
}
