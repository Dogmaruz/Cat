using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using RhythmGame.Services;

namespace DancingCats.Clone
{
    public enum OriginalRoleUnlock { None, Gold, Advertising, GameNumber, Reward, Lottery, Login, Monopoly, PlayDice }
    public enum OriginalMusicUnlock { None, Gold, Advertising, GoldOrAdvertising, DiamondOrAdvertising, Diamond }
    public enum OriginalLandRewardKind { None, Diamond, Role, Music, Free, DiamondAndRole }
    public enum OriginalWheelRewardKind { Gold, Diamond, MusicItem, GoldAddDiamond, GoldAddMusicItem, DiamondAddMusicItem, GoldDiamondAddMusicItem, Role }

    [Serializable] public sealed class OriginalAssetReference { public int m_FileID; public long m_PathID; }
    [Serializable]
    public sealed class OriginalRoleDefinition
    {
        public string Name, headName, prefabName;
        public OriginalAssetReference Main, ShopMain, Head;
        public int UnlockType, GoldNumber, RoleAttributeType, HP, SettlementId, AdNumber, roleType;
        public float CoinPercent;
        public OriginalAssetReference[] catAudio;
    }
    [Serializable]
    public sealed class OriginalWheelReward
    {
        public int rweardType, goldNumber, diamondNumber, probability;
        public string roleId, headName;
        public OriginalAssetReference head;
    }
    [Serializable]
    public sealed class OriginalLoginReward
    {
        public int id, landRweardType, diamondNumber;
        public string roleIdName, musicName, headName;
        public OriginalAssetReference head;
    }
    [Serializable]
    public sealed class OriginalLoginSet
    {
        public OriginalLoginReward OneDay, TwoDay, ThreeDay, FourDay, FiveDay, SixDay, SevenDay;
        public OriginalLoginReward[] Days { get { return new[] { OneDay, TwoDay, ThreeDay, FourDay, FiveDay, SixDay, SevenDay }; } }
    }
    [Serializable] public sealed class OriginalChallengeReward { public int id, landRweardType, goldNumber; }
    [Serializable]
    public sealed class OriginalDiceSpace
    {
        public int landRweardType, goldNumber;
        public string RoleName;
        public OriginalAssetReference roleHead, goldHead;
    }
    [Serializable]
    public sealed class OriginalHouseDefinition
    {
        public int id, floor, floorUnlock, selectableConditions;
        public string type;
        public int[] unlockNumber;
        public string[] icon;
    }
    [Serializable]
    public sealed class OriginalConstants
    {
        public float InputScale, ScoreSingle, ConverTimeForward, ConverCoinsPercent, ResurrectionTime, CameraDamp, InvincibleTime, SlideSpeed;
        public int ReviveHP, IntHP, GoldUINumber, id;
        public int[] NextPriority;
        public string TokenName;
        public string[] LocalCountryCodes;
    }
    [Serializable] public sealed class OriginalAppointedScene { public string musicName; public int sceneNumber; }
    [Serializable]
    public sealed class OriginalProgressionConfig
    {
        public int schemaVersion;
        public string sourceAssetId, guideMusicName, trialRoleName;
        public float trialDuration;
        public OriginalRoleDefinition[] roles, trails;
        public OriginalWheelReward[] dailyRewards;
        public OriginalLoginSet[] loginRewardSets;
        public OriginalChallengeReward[] challengeRewards;
        public OriginalDiceSpace[] diceSpaces;
        public OriginalHouseDefinition[] houseRows;
        public int[] settlementGold;
        public OriginalConstants constants;
        public OriginalAppointedScene[] appointedScenes;

        public static OriginalProgressionConfig Load()
            => LoadFrom(new UnityGameResources());

        public static OriginalProgressionConfig LoadFrom(IGameResources resources)
        {
            var asset = resources.Load<TextAsset>("OriginalData/progression.catalog");
            if (asset == null) throw new FileNotFoundException("Original progression configuration is missing.");
            var result = JsonUtility.FromJson<OriginalProgressionConfig>(asset.text);
            if (result == null || result.schemaVersion != 1 || result.roles == null || result.trails == null || result.houseRows == null)
                throw new InvalidDataException("Original progression configuration is invalid.");
            return result;
        }
    }

    [Serializable] public sealed class OriginalUnlockProgress { public string id; public int count; }
    [Serializable]
    public sealed class OriginalSongProgress
    {
        public string id;
        public int normalStars, endlessStars, normalScore, endlessScore, completions, starts;
        public bool favorite;
    }
    [Serializable]
    public sealed class OriginalHouseProgress
    {
        public int id, rewardProgress, selected = -1;
        public List<int> owned = new List<int>();
    }
    [Serializable]
    public sealed class OriginalCatSlotProgress
    {
        public int floor, slot, rewardProgress;
        public bool unlocked;
        public string role;
    }
    [Serializable]
    public sealed class OriginalSettings
    {
        public bool sound = true, vibration = true, postProcessing = true;
        public int language;
        public float calibrationOffsetSeconds, rhythmLineHeight;
    }
    [Serializable]
    public sealed class OriginalProgressionState
    {
        public int schemaVersion = 1, gold, diamonds;
        public string selectedRole = "1", selectedTrail = "1", selectedSong = "UnityPop054";
        public List<string> unlockedRoles = new List<string>(), unlockedTrails = new List<string>(), unlockedSongs = new List<string>();
        public List<OriginalUnlockProgress> roleProgress = new List<OriginalUnlockProgress>(), trailProgress = new List<OriginalUnlockProgress>();
        public List<OriginalSongProgress> songs = new List<OriginalSongProgress>();
        public List<OriginalHouseProgress> house = new List<OriginalHouseProgress>();
        public List<OriginalCatSlotProgress> catSlots = new List<OriginalCatSlotProgress>();
        public OriginalSettings settings = new OriginalSettings();
        public string loginDay = "", dailyDay = "", dailySession = "", diceDay = "", diceStartedDay = "", challengeDay = "", challengeSong = "";
        public int loginClaims, loginContinuous, dailyFreeClaims, dailyRewardClaims, diceFreeRolls, diceRewardRolls, diceBonusRolls, dicePosition, challengeStars;
        public bool diceComplete, firstDailyRewardClaimed;
        public List<int> claimedDiceSpaces = new List<int>(), claimedChallengeTiers = new List<int>();
        public List<int> dailyDeck = new List<int>(), dailyCards = new List<int>();
        public List<OriginalSettlementQuote> settlements = new List<OriginalSettlementQuote>();
    }
    [Serializable]
    public sealed class OriginalSettlementQuote
    {
        public string runId, roleId;
        public int earnedStars, baseGold, collectedCoins, roleBonus, totalGold;
        public bool eligible, claimed;
    }
    [Serializable]
    public sealed class OriginalRewardReceipt
    {
        public bool success;
        public int gold, diamonds, extraRolls, position = -1, face;
        public string role, music, message;
        public List<string> roles = new List<string>(), musicUnlocks = new List<string>();
        public static OriginalRewardReceipt Refused(string message) { return new OriginalRewardReceipt { message = message }; }
    }

    public sealed class OriginalProgression
    {
        public const string SaveKey = "DancingCatsClone.v1.progression";
        private string dailySession;
        public OriginalProgressionConfig Config { get; private set; }
        public OriginalProgressionState State { get; private set; }
        public int Gold { get { return State.gold; } }
        public int Diamonds { get { return State.diamonds; } }
        public string SelectedRole { get { return State.selectedRole; } }
        public string SelectedTrail { get { return State.selectedTrail; } }
        public event Action Changed;
        private readonly bool autoSave;
        private readonly ISongCatalog songCatalog;
        private readonly IProfileStore profileStore;
        private SongRecord[] SongCatalog => songCatalog.Songs;

        public OriginalProgression(OriginalProgressionConfig config, OriginalProgressionState state,
            ISongCatalog catalog, IProfileStore store, bool autoSave = false, ApplicationSession session = null)
        {
            if (config == null) throw new ArgumentNullException("config");
            songCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            profileStore = store ?? throw new ArgumentNullException(nameof(store));
            if (catalog.Songs == null || catalog.Songs.Length == 0)
                throw new ArgumentException("Progression requires a nonempty song catalog.", nameof(catalog));
            dailySession = (session ?? new ApplicationSession()).Id;
            this.autoSave = autoSave;
            Config = config;
            State = state ?? new OriginalProgressionState();
            Normalize();
        }

        public static OriginalProgression FromJson(OriginalProgressionConfig config, string json,
            ISongCatalog catalog, IProfileStore store, bool autoSave = false, ApplicationSession session = null)
        {
            OriginalProgressionState state = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try { state = JsonUtility.FromJson<OriginalProgressionState>(json); }
                catch (ArgumentException) { }
            }
            return new OriginalProgression(config, state, catalog, store, autoSave, session);
        }

        public string ToJson() { return JsonUtility.ToJson(State); }
        public void Save() { profileStore.Write(ToJson()); }
        private void Commit() { if (autoSave) Save(); if (Changed != null) Changed(); }

        private void Normalize()
        {
            State.schemaVersion = 1;
            State.gold = Math.Max(0, State.gold); State.diamonds = Math.Max(0, State.diamonds);
            if (State.unlockedRoles == null) State.unlockedRoles = new List<string>();
            if (State.unlockedTrails == null) State.unlockedTrails = new List<string>();
            if (State.unlockedSongs == null) State.unlockedSongs = new List<string>();
            if (State.roleProgress == null) State.roleProgress = new List<OriginalUnlockProgress>();
            if (State.trailProgress == null) State.trailProgress = new List<OriginalUnlockProgress>();
            if (State.songs == null) State.songs = new List<OriginalSongProgress>();
            if (State.house == null) State.house = new List<OriginalHouseProgress>();
            if (State.catSlots == null) State.catSlots = new List<OriginalCatSlotProgress>();
            if (State.dailyDeck == null) State.dailyDeck = new List<int>();
            if (State.dailyCards == null) State.dailyCards = new List<int>();
            if (State.claimedDiceSpaces == null) State.claimedDiceSpaces = new List<int>();
            if (State.claimedChallengeTiers == null) State.claimedChallengeTiers = new List<int>();
            if (State.settings == null) State.settings = new OriginalSettings();
            State.settlements = (State.settlements ?? new List<OriginalSettlementQuote>()).Where(q => q != null && !string.IsNullOrEmpty(q.runId)).GroupBy(q => q.runId).Select(g => g.OrderByDescending(q => q.claimed).First()).ToList();
            State.unlockedRoles = State.unlockedRoles.Where(id => Config.roles.Any(r => r.Name == id)).Distinct().ToList();
            State.unlockedTrails = State.unlockedTrails.Where(id => Config.trails.Any(r => r.Name == id)).Distinct().ToList();
            State.roleProgress = State.roleProgress.Where(p => p != null && Role(p.id) != null).GroupBy(p => p.id).Select(g => new OriginalUnlockProgress { id = g.Key, count = Mathf.Clamp(g.Max(p => p.count), 0, Math.Max(1, Role(g.Key).AdNumber)) }).ToList();
            State.trailProgress = State.trailProgress.Where(p => p != null && Trail(p.id) != null).GroupBy(p => p.id).Select(g => new OriginalUnlockProgress { id = g.Key, count = Mathf.Clamp(g.Max(p => p.count), 0, Math.Max(1, Trail(g.Key).AdNumber)) }).ToList();
            State.songs.RemoveAll(s => s == null || string.IsNullOrEmpty(s.id));
            foreach (var song in State.songs) song.starts = Math.Max(0, song.starts);
            State.house.RemoveAll(h => h == null || Config.houseRows.All(d => d.id != h.id));
            foreach (var house in State.house)
            {
                var definition = Config.houseRows.First(d => d.id == house.id);
                house.owned = (house.owned ?? new List<int>()).Where(i => i >= 0 && i < definition.unlockNumber.Length).Distinct().ToList();
                if (!house.owned.Contains(house.selected)) house.selected = house.owned.Count == 0 ? -1 : house.owned[0];
            }
            foreach (var role in Config.roles.Where(r => r.UnlockType == 0)) AddUnique(State.unlockedRoles, role.Name);
            foreach (var trail in Config.trails.Where(r => r.UnlockType == 0)) AddUnique(State.unlockedTrails, trail.Name);
            if (!IsRoleUnlocked(State.selectedRole)) State.selectedRole = Config.roles.First(r => r.UnlockType == 0).Name;
            if (!IsTrailUnlocked(State.selectedTrail)) State.selectedTrail = Config.trails.First(r => r.UnlockType == 0).Name;
            State.loginClaims = Math.Max(0, State.loginClaims);
            State.dailyFreeClaims = Math.Max(0, State.dailyFreeClaims); State.dailyRewardClaims = Math.Max(0, State.dailyRewardClaims);
            State.diceFreeRolls = Math.Max(0, State.diceFreeRolls); State.diceRewardRolls = Math.Max(0, State.diceRewardRolls);
            State.diceBonusRolls = Math.Max(0, State.diceBonusRolls);
            State.dicePosition = Mathf.Clamp(State.dicePosition, 0, Config.diceSpaces.Length);
            State.diceComplete = State.dicePosition >= Config.diceSpaces.Length;
            State.challengeStars = Mathf.Clamp(State.challengeStars, 0, 9);
        }

        private static bool AddUnique<T>(List<T> list, T value)
        {
            if (list.Contains(value)) return false;
            list.Add(value); return true;
        }
        public OriginalRoleDefinition Role(string id) { return Config.roles.FirstOrDefault(r => r.Name == id); }
        public OriginalRoleDefinition Trail(string id) { return Config.trails.FirstOrDefault(r => r.Name == id); }
        public bool IsRoleUnlocked(string id) { return !string.IsNullOrEmpty(id) && State.unlockedRoles.Contains(id); }
        public bool IsTrailUnlocked(string id) { return !string.IsNullOrEmpty(id) && State.unlockedTrails.Contains(id); }
        public bool IsSongUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (State.unlockedSongs.Contains(id)) return true;
            var song = SongCatalog.FirstOrDefault(s => s.id == id);
            return song != null && song.catalog.musicUnlockType == 0;
        }

        public bool TryBuyRole(string id) { return BuyRoleDefinition(Role(id), State.unlockedRoles); }
        public bool TryBuyTrail(string id) { return BuyRoleDefinition(Trail(id), State.unlockedTrails); }
        private bool BuyRoleDefinition(OriginalRoleDefinition item, List<string> unlocked)
        {
            if (item == null || item.UnlockType != (int)OriginalRoleUnlock.Gold || item.GoldNumber <= 0 || unlocked.Contains(item.Name) || State.gold < item.GoldNumber)
                return false;
            State.gold -= item.GoldNumber; unlocked.Add(item.Name); Commit(); return true;
        }

        public bool TryBuySong(SongRecord song)
        {
            if (song == null || song.catalog == null || IsSongUnlocked(song.id) || song.catalog.gold <= 0) return false;
            int kind = song.catalog.musicUnlockType;
            bool diamond = kind == (int)OriginalMusicUnlock.Diamond || kind == (int)OriginalMusicUnlock.DiamondOrAdvertising;
            if (!diamond && kind != (int)OriginalMusicUnlock.Gold && kind != (int)OriginalMusicUnlock.GoldOrAdvertising) return false;
            int balance = diamond ? State.diamonds : State.gold;
            if (balance < song.catalog.gold) return false;
            if (diamond) State.diamonds -= song.catalog.gold; else State.gold -= song.catalog.gold;
            AddUnique(State.unlockedSongs, song.id); Commit(); return true;
        }

        public int RoleUnlockProgress(string id) { return Progress(State.roleProgress, id); }
        public int TrailUnlockProgress(string id) { return Progress(State.trailProgress, id); }
        private static int Progress(List<OriginalUnlockProgress> entries, string id) { var entry = entries.FirstOrDefault(e => e.id == id); return entry == null ? 0 : entry.count; }
        public bool RewardRoleUnlock(string id) { return RewardUnlock(Role(id), State.unlockedRoles, State.roleProgress); }
        public bool RewardTrailUnlock(string id) { return RewardUnlock(Trail(id), State.unlockedTrails, State.trailProgress); }
        private bool RewardUnlock(OriginalRoleDefinition item, List<string> unlocked, List<OriginalUnlockProgress> entries)
        {
            if (item == null || item.UnlockType != (int)OriginalRoleUnlock.Advertising || unlocked.Contains(item.Name)) return false;
            var progress = entries.FirstOrDefault(e => e.id == item.Name);
            if (progress == null) { progress = new OriginalUnlockProgress { id = item.Name }; entries.Add(progress); }
            progress.count = Math.Min(Math.Max(1, item.AdNumber), progress.count + 1);
            bool complete = progress.count >= Math.Max(1, item.AdNumber);
            if (complete) AddUnique(unlocked, item.Name);
            Commit(); return complete;
        }
        public bool RewardSongUnlock(SongRecord song)
        {
            if (song == null || song.catalog == null || IsSongUnlocked(song.id)) return false;
            int kind = song.catalog.musicUnlockType;
            if (kind != 2 && kind != 3 && kind != 4) return false;
            AddUnique(State.unlockedSongs, song.id); Commit(); return true;
        }
        public bool SelectRole(string id)
        {
            if (!IsRoleUnlocked(id)) return false;
            State.selectedRole = id; Commit(); return true;
        }
        public bool SelectTrail(string id)
        {
            if (!IsTrailUnlocked(id)) return false;
            State.selectedTrail = id; Commit(); return true;
        }
        public void AddGold(int amount) { if (amount < 0) throw new ArgumentOutOfRangeException("amount"); State.gold = SaturatingAdd(State.gold, amount); Commit(); }
        public void AddDiamonds(int amount) { if (amount < 0) throw new ArgumentOutOfRangeException("amount"); State.diamonds = SaturatingAdd(State.diamonds, amount); Commit(); }
        private static int SaturatingAdd(int balance, int amount) { return (int)Math.Min(int.MaxValue, (long)balance + Math.Max(0, amount)); }

        private static string Day(DateTime now) { return now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        private static bool ReadDay(string value, out DateTime result)
        { return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result); }
        private static bool LaterDay(string saved, DateTime now)
        { DateTime previous; return !ReadDay(saved, out previous) || now.Date > previous; }

        public bool CanClaimLogin(DateTime now) { return LaterDay(State.loginDay, now); }
        public int LoginDayNumber { get { return State.loginClaims % 7 + 1; } }
        public OriginalLoginReward NextLoginReward
        {
            get { return Config.loginRewardSets[Math.Min(Config.loginRewardSets.Length - 1, State.loginClaims / 7)].Days[State.loginClaims % 7]; }
        }
        public OriginalRewardReceipt ClaimLogin(DateTime now, bool offlineDoubleReward = false)
        {
            if (!CanClaimLogin(now)) return OriginalRewardReceipt.Refused("Today's login reward is already claimed.");
            var reward = NextLoginReward;
            var receipt = new OriginalRewardReceipt { success = true };
            // Native RewardButtonClick credits PlayerGold for enum1 despite the historical field/enum names.
            ApplyLandReward(reward.landRweardType, reward.diamondNumber * (offlineDoubleReward ? 2 : 1), reward.roleIdName, reward.musicName, receipt, true);
            DateTime previous;
            State.loginContinuous = ReadDay(State.loginDay, out previous) && (now.Date - previous).Days == 1 ? State.loginContinuous + 1 : 0;
            State.loginDay = Day(now); State.loginClaims++;
            Commit(); return receipt;
        }

        public OriginalWheelReward SelectWheelReward(int ticket)
        {
            int total = Config.dailyRewards.Sum(r => r.probability);
            if (ticket < 0 || ticket >= total) throw new ArgumentOutOfRangeException("ticket");
            foreach (var reward in Config.dailyRewards)
            { if (ticket < reward.probability) return reward; ticket -= reward.probability; }
            throw new InvalidDataException("The original reward weights are invalid.");
        }

        public void BeginDailySession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentException("A session identifier is required.", "sessionId");
            dailySession = sessionId;
            if (State.dailySession == sessionId) return;
            // Native DailyRewardUIPanel keeps its nine-card page in panel-instance fields, while
            // FirstDailyReward is saved. Persisting the current session ledger also prevents duplicate callbacks.
            State.dailySession = sessionId; State.dailyFreeClaims = State.dailyRewardClaims = 0;
            State.dailyDeck.Clear(); State.dailyCards.Clear();
            Commit();
        }
        private void DailyDate(DateTime now) { BeginDailySession(dailySession); }
        public int WheelClaimsRemaining(DateTime now)
        { DailyDate(now); return Math.Max(0, 9 - State.dailyFreeClaims - State.dailyRewardClaims); }
        public OriginalRewardReceipt SpinWheel(DateTime now, int ticket, bool offlineReward = false)
        {
            DailyDate(now);
            int card = Enumerable.Range(0, 9).FirstOrDefault(i => !State.dailyCards.Contains(i));
            return FlipDailyCard(now, card, ticket, offlineReward);
        }
        public bool DailyCardClaimed(int card) { return State.dailyCards.Contains(card); }
        public OriginalRewardReceipt FlipDailyCard(DateTime now, int card, int seed, bool offlineReward = false)
        {
            DailyDate(now);
            if (card < 0 || card >= 9 || State.dailyCards.Contains(card)) return OriginalRewardReceipt.Refused("This reward card is already claimed.");
            if (WheelClaimsRemaining(now) == 0) return OriginalRewardReceipt.Refused("All daily reward cards are claimed.");
            if (State.dailyFreeClaims > 0 && !offlineReward) return OriginalRewardReceipt.Refused("Use the offline reward action for another card.");
            var random = new System.Random(seed);
            var gold = Enumerable.Range(0, Config.dailyRewards.Length).Where(i => Config.dailyRewards[i].rweardType == 0).ToArray();
            if (State.dailyDeck.Count == 0 && State.dailyCards.Count == 0)
            {
                // RewradData samples gold entries uniformly; the serialized probability field is not used here.
                if (State.firstDailyRewardClaimed)
                    State.dailyDeck.AddRange(Enumerable.Range(0, Config.dailyRewards.Length).Where(i => Config.dailyRewards[i].rweardType == 2 || Config.dailyRewards[i].rweardType == 7));
                int goldCount = State.firstDailyRewardClaimed ? 7 : 8;
                for (int i = 0; i < goldCount; i++) State.dailyDeck.Add(gold[random.Next(gold.Length)]);
            }
            OriginalWheelReward reward;
            if (State.dailyFreeClaims == 0)
            {
                if (!State.firstDailyRewardClaimed) reward = Config.dailyRewards.First(r => r.rweardType == 7);
                else
                {
                    int maximum = gold.Max(i => Config.dailyRewards[i].goldNumber);
                    var free = gold.Where(i => Config.dailyRewards[i].goldNumber < maximum).ToArray();
                    reward = Config.dailyRewards[free[random.Next(free.Length)]];
                }
                State.firstDailyRewardClaimed = true;
            }
            else
            {
                int index = random.Next(State.dailyDeck.Count);
                reward = Config.dailyRewards[State.dailyDeck[index]]; State.dailyDeck.RemoveAt(index);
            }
            if (State.dailyFreeClaims == 0) State.dailyFreeClaims++; else State.dailyRewardClaims++;
            State.dailyCards.Add(card);
            var result = new OriginalRewardReceipt { success = true };
            int kind = reward.rweardType;
            if (kind == 0 || kind == 3 || kind == 4 || kind == 6) CreditGold(reward.goldNumber, result);
            if (kind == 1 || kind == 3 || kind == 5 || kind == 6) CreditDiamonds(reward.diamondNumber, result);
            if (kind == 2 || kind == 4 || kind == 5 || kind == 6)
            {
                var available = SongCatalog.Where(s => !IsSongUnlocked(s.id)).ToArray();
                if (available.Length > 0) GrantMusic(available[random.Next(available.Length)].id, result);
            }
            if (kind == 7)
            {
                var role = !IsRoleUnlocked(reward.roleId) ? Role(reward.roleId) : Config.roles.FirstOrDefault(r => r.UnlockType == 4 && !IsRoleUnlocked(r.Name));
                if (role != null) GrantRole(role.Name, result, false);
                else CreditGold(Config.dailyRewards[Config.dailyRewards.Length - 3].goldNumber, result);
            }
            Commit(); return result;
        }

        public void StartDiceEvent(DateTime now)
        {
            if (!string.IsNullOrEmpty(State.diceStartedDay)) return;
            State.diceStartedDay = Day(now); Commit();
        }
        private void DiceDate(DateTime now)
        {
            if (string.IsNullOrEmpty(State.diceStartedDay)) State.diceStartedDay = Day(now);
            if (!LaterDay(State.diceDay, now)) return;
            State.diceDay = Day(now); State.diceFreeRolls = State.diceRewardRolls = 0;
        }
        private int DiceAge(DateTime now)
        { DateTime started; return ReadDay(State.diceStartedDay, out started) ? Math.Max(0, (now.Date - started).Days) : 0; }
        public int DiceRollsRemaining(DateTime now)
        {
            DiceDate(now);
            if (State.diceComplete) return 0;
            return State.diceBonusRolls > 0 || (DiceAge(now) < 7 && State.diceFreeRolls == 0) ? 1 : 0;
        }
        public int DiceRewardActionsRemaining(DateTime now)
        {
            DiceDate(now);
            if (State.diceComplete) return 0;
            return DiceAge(now) >= 7 ? int.MaxValue : Math.Max(0, 1 - State.diceRewardRolls);
        }
        public int NextDiceFace(DateTime now, int seed, bool offlineReward = false)
        {
            DiceDate(now);
            return new System.Random(seed).Next(State.dicePosition == 0 ? 3 : 1, State.dicePosition == 0 ? 6 : 7);
        }
        public OriginalRewardReceipt RollDice(DateTime now, int face, bool offlineReward = false)
        {
            DiceDate(now);
            // The Free space resets entitlement; it does not set the otherwise-unwritten native isFree flag.
            int min = State.dicePosition == 0 ? 3 : 1;
            int max = State.dicePosition == 0 ? 5 : 6;
            if (face < min || face > max) return OriginalRewardReceipt.Refused("This face is outside the original roll range.");
            if (offlineReward ? DiceRewardActionsRemaining(now) == 0 : DiceRollsRemaining(now) == 0)
                return OriginalRewardReceipt.Refused("No roll is available.");
            if (offlineReward) State.diceRewardRolls++;
            else { State.diceFreeRolls = 1; State.diceBonusRolls = 0; }
            var result = new OriginalRewardReceipt { success = true, face = face };
            int target = Math.Min(Config.diceSpaces.Length, State.dicePosition + face);
            // SetDatas awards every traversed entry, including rewards passed between start and destination.
            for (int index = State.dicePosition; index < target; index++)
            {
                if (!AddUnique(State.claimedDiceSpaces, index)) continue;
                var entry = Config.diceSpaces[index];
                ApplyLandReward(entry.landRweardType, entry.goldNumber, entry.RoleName, "", result, false);
                if (entry.landRweardType == 4)
                { State.diceBonusRolls = 1; State.diceFreeRolls = 0; result.extraRolls = 1; }
            }
            State.dicePosition = target; State.diceComplete = target == Config.diceSpaces.Length;
            result.position = target;
            Commit(); return result;
        }

        private void CreditGold(int amount, OriginalRewardReceipt receipt)
        { if (amount <= 0) return; State.gold = SaturatingAdd(State.gold, amount); receipt.gold = SaturatingAdd(receipt.gold, amount); }
        private void CreditDiamonds(int amount, OriginalRewardReceipt receipt)
        { if (amount <= 0) return; State.diamonds = SaturatingAdd(State.diamonds, amount); receipt.diamonds = SaturatingAdd(receipt.diamonds, amount); }
        private void GrantRole(string id, OriginalRewardReceipt receipt, bool select)
        {
            if (Role(id) == null) return;
            if (AddUnique(State.unlockedRoles, id)) { receipt.roles.Add(id); receipt.role = id; }
            if (select) State.selectedRole = id;
        }
        private void GrantMusic(string id, OriginalRewardReceipt receipt)
        {
            if (string.IsNullOrEmpty(id) || SongCatalog.All(s => s.id != id)) return;
            if (AddUnique(State.unlockedSongs, id)) { receipt.musicUnlocks.Add(id); receipt.music = id; }
        }
        private void ApplyLandReward(int kind, int amount, string role, string music, OriginalRewardReceipt result, bool selectRole)
        {
            // Native login/dice paths use PlayerGoldUpdate for historical Diamond/ DiamondAndRole enum values.
            if (kind == 1 || kind == 5) CreditGold(amount, result);
            if (kind == 2 || kind == 5) GrantRole(role, result, selectRole);
            if (kind == 3) GrantMusic(music, result);
        }

        public OriginalSongProgress SongProgress(string id)
        { return State.songs.FirstOrDefault(s => s.id == id); }
        private OriginalSongProgress EnsureSong(string id)
        {
            var value = SongProgress(id);
            if (value == null) { value = new OriginalSongProgress { id = id }; State.songs.Add(value); }
            return value;
        }
        public void RecordSongStart(string id)
        {
            if (SongCatalog.All(s => s.id != id)) return;
            var entry = EnsureSong(id);
            entry.starts = SaturatingAdd(entry.starts, 1); Commit();
        }

        // Native GetNextPlaySongId1489338 uses the shared best star/crown count in every mode.
        // The mode parameter is retained for callers; it does not change the original priority.
        public string GetNextPlaySong(bool hasUnlock = true, bool endless = false, int seed = 0)
        {
            var candidates = SongCatalog.Where(s => s.catalog != null && s.catalogId < 900 && !s.catalog.disable).OrderBy(s => s.catalogId).ToArray();
            var unlocked = candidates.Where(s => IsSongUnlocked(s.id)).ToArray();
            var next = unlocked.FirstOrDefault(s => SongProgress(s.id) == null || SongProgress(s.id).starts == 0);
            if (next != null) return next.id;
            if (hasUnlock)
            {
                next = candidates.FirstOrDefault(s => !IsSongUnlocked(s.id));
                if (next != null) return next.id;
            }
            next = unlocked.FirstOrDefault(s => SongProgress(s.id) == null || Math.Max(SongProgress(s.id).normalStars, SongProgress(s.id).endlessStars) < 3);
            if (next != null) return next.id;
            var other = unlocked.Where(s => s.id != State.selectedSong).ToArray();
            if (other.Length > 0) return other[new System.Random(seed).Next(other.Length)].id;
            return unlocked.Length == 0 ? null : unlocked[0].id;
        }

        public bool ToggleFavorite(string id)
        {
            if (SongCatalog.All(s => s.id != id)) return false;
            var value = EnsureSong(id); value.favorite = !value.favorite; Commit(); return value.favorite;
        }
        public void RecordSongResult(string id, int stars, int score, bool endless, bool completed, DateTime now, bool challenge = false)
        {
            if (SongCatalog.All(s => s.id != id)) return;
            var entry = EnsureSong(id); stars = Mathf.Clamp(stars, 0, endless ? 9 : 3); score = Math.Max(0, score);
            if (endless) { entry.endlessStars = Math.Max(entry.endlessStars, stars); entry.endlessScore = Math.Max(entry.endlessScore, score); }
            else { entry.normalStars = Math.Max(entry.normalStars, stars); entry.normalScore = Math.Max(entry.normalScore, score); }
            if (completed) entry.completions++;
            EnsureChallenge(now);
            // Native Settlement.Show updates the daily maximum only for ChallengeLevelPattern1,
            // including failed partial runs; SetChallengeLevelMusicStar1493EBC stores max(old, marks).
            if (challenge && id == State.challengeSong) State.challengeStars = Math.Max(State.challengeStars, stars);
            Commit();
        }
        public OriginalSettlementQuote GetSettlementQuote(string runId)
        { return State.settlements.FirstOrDefault(q => q.runId == runId); }

        // Native Show14CE1F4 randomizes ScoreNoConver in inclusive settlementGold[0..1].
        // HeadShow14CF16C applies CoinPercent only for Gold attributes; FlyCoin14CFA94 truncates the bonus.
        public static int CalculateSettlementGold(int baseGold, int collectedCoins, float coinPercent)
        {
            if (baseGold < 0 || collectedCoins < 0 || float.IsNaN(coinPercent) || float.IsInfinity(coinPercent) || coinPercent < 0)
                throw new ArgumentOutOfRangeException("Settlement amounts must be nonnegative finite values.");
            int subtotal = SaturatingAdd(baseGold, collectedCoins);
            float rawBonus = subtotal * coinPercent;
            int bonus = rawBonus >= int.MaxValue ? int.MaxValue : (int)rawBonus;
            return SaturatingAdd(subtotal, bonus);
        }

        public OriginalSettlementQuote PrepareSettlement(string runId, int earnedStars, int collectedCoins, int seed, string roleId = null)
        {
            if (string.IsNullOrEmpty(runId)) throw new ArgumentException("A settlement requires a unique run ID.", "runId");
            if (collectedCoins < 0) throw new ArgumentOutOfRangeException("collectedCoins");
            var existing = GetSettlementQuote(runId);
            if (existing != null) return existing;
            if (Config.settlementGold == null || Config.settlementGold.Length < 2 || Config.settlementGold[0] < 0 || Config.settlementGold[1] < Config.settlementGold[0] || Config.settlementGold[1] == int.MaxValue)
                throw new InvalidDataException("Original settlement range is invalid.");
            var role = Role(roleId ?? SelectedRole);
            int randomBase = new System.Random(seed).Next(Config.settlementGold[0], Config.settlementGold[1] + 1);
            float percent = role != null && role.RoleAttributeType == 1 ? role.CoinPercent : 0;
            int total = CalculateSettlementGold(randomBase, collectedCoins, percent);
            int marks = Mathf.Clamp(earnedStars, 0, 9);
            var quote = new OriginalSettlementQuote {
                runId = runId, roleId = role == null ? "" : role.Name, earnedStars = marks,
                baseGold = randomBase, collectedCoins = collectedCoins,
                roleBonus = total - SaturatingAdd(randomBase, collectedCoins), totalGold = total,
                eligible = marks == 3 || marks == 6 || marks == 9
            };
            State.settlements.Add(quote); Commit(); return quote;
        }

        // Native TimerThree14D1E64 invokes FlyCoin after its first showTime wait.
        // One/two-mark animations and Home/Hide/Next do not grant this reward.
        public OriginalRewardReceipt AutoRewardSettlement(string runId)
        {
            var quote = GetSettlementQuote(runId);
            if (quote == null || !quote.eligible || quote.claimed) return OriginalRewardReceipt.Refused("This run has no unclaimed full-tier reward.");
            quote.claimed = true;
            var receipt = new OriginalRewardReceipt { success = true };
            CreditGold(quote.totalGold, receipt); Commit(); return receipt;
        }

        public string ChallengeSong(DateTime now) { EnsureChallenge(now); return State.challengeSong; }
        private void EnsureChallenge(DateTime now)
        {
            if (!LaterDay(State.challengeDay, now)) return;
            State.challengeDay = Day(now); State.challengeStars = 0; State.claimedChallengeTiers.Clear();
            // The original stores an appointed challenge song. Its remote selection policy is not serialized.
            // This explicit offline fallback selects a stable active song per local date, independent of process RNG.
            int ordinal = (int)(now.Date - new DateTime(2000, 1, 1)).TotalDays;
            State.challengeSong = SongCatalog[(ordinal % SongCatalog.Length + SongCatalog.Length) % SongCatalog.Length].id;
        }
        public bool CanClaimChallenge(DateTime now, int tier)
        {
            EnsureChallenge(now);
            return tier >= 0 && tier < Config.challengeRewards.Length && State.challengeStars >= (tier + 1) * 3 && !State.claimedChallengeTiers.Contains(tier);
        }
        public OriginalRewardReceipt ClaimChallenge(DateTime now, int tier)
        {
            if (!CanClaimChallenge(now, tier)) return OriginalRewardReceipt.Refused("Challenge reward is not available.");
            var reward = Config.challengeRewards[tier]; var result = new OriginalRewardReceipt { success = true };
            ApplyLandReward(reward.landRweardType, reward.goldNumber, "", "", result, false);
            State.claimedChallengeTiers.Add(tier); Commit(); return result;
        }

        // Native GetThreeStarNumber counts active songs with MusicStarCrownCount > 2, including later crowns.
        public int TotalStars { get { return 3 * SongCatalog.Count(song => State.songs.Any(s => s.id == song.id && Math.Max(s.normalStars, s.endlessStars) >= 3)); } }
        public OriginalHouseProgress HouseProgress(int id)
        { return State.house.FirstOrDefault(h => h.id == id); }
        public bool IsFloorUnlocked(int floor)
        {
            var item = Config.houseRows.FirstOrDefault(h => h.floor == floor);
            return item != null && TotalStars >= item.floorUnlock;
        }
        public int BuiltInFloor(int floor)
        {
            var ids = Config.houseRows.Where(h => h.floor == floor && h.type != "1").Select(h => h.id).ToArray();
            return State.house.Count(h => ids.Contains(h.id) && h.selected >= 0);
        }
        public bool TryBuyHouseVariant(int id, int variant, bool offlineReward = false)
        {
            var definition = Config.houseRows.FirstOrDefault(h => h.id == id);
            if (definition == null || definition.type == "1" || variant < 0 || variant >= definition.unlockNumber.Length || !IsFloorUnlocked(definition.floor)) return false;
            var existing = HouseProgress(id);
            if (existing != null && existing.owned.Contains(variant)) return SelectHouseVariant(id, variant);
            if (BuiltInFloor(definition.floor) < definition.selectableConditions) return false;
            int cost = definition.unlockNumber[variant];
            if (cost < 0 && !offlineReward || cost > State.gold) return false;
            if (existing == null) { existing = new OriginalHouseProgress { id = id }; State.house.Add(existing); }
            if (cost < 0)
            {
                existing.rewardProgress = Math.Min(-cost, existing.rewardProgress + 1);
                if (existing.rewardProgress < -cost) { Commit(); return false; }
            }
            if (cost > 0) State.gold -= cost;
            existing.owned.Add(variant); existing.selected = variant;
            Commit(); return true;
        }
        public bool SelectHouseVariant(int id, int variant)
        {
            var value = HouseProgress(id);
            if (value == null || !value.owned.Contains(variant)) return false;
            value.selected = variant; Commit(); return true;
        }
        public OriginalCatSlotProgress CatSlot(int floor, int slot)
        { return State.catSlots.FirstOrDefault(s => s.floor == floor && s.slot == slot); }
        public bool TryUnlockCatSlot(int floor, int slot, bool offlineReward = false)
        {
            var row = Config.houseRows.FirstOrDefault(r => r.floor == floor && r.type == "1");
            if (row == null || slot < 0 || slot >= row.unlockNumber.Length || !IsFloorUnlocked(floor)) return false;
            var state = CatSlot(floor, slot);
            if (state != null && state.unlocked) return true;
            int cost = row.unlockNumber[slot];
            if (cost < 0 && !offlineReward || cost > State.gold) return false;
            if (state == null) { state = new OriginalCatSlotProgress { floor = floor, slot = slot }; State.catSlots.Add(state); }
            if (cost < 0)
            {
                state.rewardProgress = Math.Min(-cost, state.rewardProgress + 1);
                if (state.rewardProgress < -cost) { Commit(); return false; }
            }
            if (cost > 0) State.gold -= cost;
            state.unlocked = true; Commit(); return true;
        }
        public bool AssignCatToSlot(int floor, int slot, string role, bool moveExisting = false)
        {
            if (!IsRoleUnlocked(role)) return false;
            var target = CatSlot(floor, slot);
            if (target == null || !target.unlocked) return false;
            var existing = State.catSlots.FirstOrDefault(s => s.role == role && s != target);
            if (existing != null && !moveExisting) return false;
            if (existing != null) existing.role = "";
            target.role = role; Commit(); return true;
        }
        public void SetSettings(bool sound, bool vibration, bool postProcessing, int language, float calibrationOffsetSeconds)
        {
            if (float.IsNaN(calibrationOffsetSeconds) || float.IsInfinity(calibrationOffsetSeconds)) throw new ArgumentOutOfRangeException("calibrationOffsetSeconds");
            State.settings.sound = sound; State.settings.vibration = vibration; State.settings.postProcessing = postProcessing;
            State.settings.language = Mathf.Clamp(language, 0, 13); State.settings.calibrationOffsetSeconds = calibrationOffsetSeconds;
            Commit();
        }
        public void SetRhythmLineHeight(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException("value");
            State.settings.rhythmLineHeight = Mathf.Clamp(value, -1f, 1f); Commit();
        }
        public void AdjustCalibration(int direction)
        { SetRhythmLineHeight(Mathf.Round((State.settings.rhythmLineHeight + Math.Sign(direction) * .01f) * 100f) / 100f); }
    }
}
