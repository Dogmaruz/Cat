using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using RhythmGame.Services;

namespace DancingCats.Clone.Editor
{
    public static class ProgressionValidation
    {
        public static void Run()
        {
            CheckInjectedProfileIsolation();
            var configType = Find("OriginalProgressionConfig");
            var config = configType.GetMethod("Load").Invoke(null,null);
            var roles = Rows(config,"roles");
            var trails = Rows(config,"trails");
            Check(roles.Length==63 && roles.Select(r=>S(r,"Name")).Distinct().Count()==63,"63 unique original cats");
            Check(trails.Length==10,"10 original trails");
            Check(N(roles.Single(r=>S(r,"Name")=="4"),"GoldNumber")==3000 && N(roles.Single(r=>S(r,"Name")=="32"),"GoldNumber")==12000,"original cat prices");
            Check(N(roles.Single(r=>S(r,"Name")=="12"),"UnlockType")==6,"login-only cat remains login-only");
            Check(trails.Skip(1).Select(r=>N(r,"AdNumber")).SequenceEqual(new double[]{1,1,1,2,2,2,3,3,3}),"original trail unlock progress counts");
            var wheel=Rows(config,"dailyRewards");
            Check(wheel.Select(r=>N(r,"probability")).SequenceEqual(new double[]{25,25,15,10,10,5,5,5}),"original weighted wheel entries");
            var sets=Rows(config,"loginRewardSets");
            Check(sets.Length==2 && S(F(sets[0],"TwoDay"),"roleIdName")=="12" && S(F(sets[0],"SevenDay"),"roleIdName")=="13","two original seven-day sets and original login cats");
            Check(Rows(config,"challengeRewards").Select(r=>N(r,"goldNumber")).SequenceEqual(new double[]{500,1000,1500}),"three original challenge tiers");
            var dice=Rows(config,"diceSpaces");
            Check(dice.Length==38 && S(dice[5],"RoleName")=="22" && S(dice[37],"RoleName")=="21" && N(dice[37],"goldNumber")==2000,"original dice board rewards and terminal space");
            var house=Rows(config,"houseRows");
            Check(house.Length==33 && house.Select(r=>N(r,"floor")).Distinct().Count()==4,"33 original house entries across four floors");
            Check(((int[])F(house.Single(r=>N(r,"id")==1),"unlockNumber")).SequenceEqual(new[]{0,400,-1}),"original house variant costs retain sentinel values");

            // An explicit fixture balance tests atomicity without asserting an unknown original default wallet.
            var stateType=Find("OriginalProgressionState");
            var state=Activator.CreateInstance(stateType);
            stateType.GetField("gold").SetValue(state,10000);
            stateType.GetField("diamonds").SetValue(state,123);
            var progressionType=Find("OriginalProgression");
            var progression=new OriginalProgression((OriginalProgressionConfig)config,(OriginalProgressionState)state,
                new FixtureCatalog(RecoveredCatalog.Load()),new MemoryProfileStore());
            var buy=progressionType.GetMethod("TryBuyRole");
            Check((bool)buy.Invoke(progression,new object[]{"4"}),"coin-priced cat can be purchased");
            Check(N(state,"gold")==7000 && N(state,"diamonds")==123,"purchase debits only its actual currency");
            Check(!(bool)buy.Invoke(progression,new object[]{"4"}) && N(state,"gold")==7000,"duplicate purchase does not debit wallet");
            Check(!(bool)buy.Invoke(progression,new object[]{"12"}) && N(state,"gold")==7000,"zero-price login reward cannot be purchased as a free shop item");
            Check(!(bool)buy.Invoke(progression,new object[]{"32"}) && N(state,"gold")==7000,"insufficient wallet does not partially debit");
            CheckClaimsAndPersistence();
            CheckOfflineUnlockProgress();
            CheckOriginalDiceBoard();
            CheckNativeChallengeTiers();
            CheckChallengeAndHouse();
            CheckRunSettlement();
            CheckStartsAndNextSong();
            Debug.Log("PROGRESSION PASS: recovered config and atomic currency transactions");
        }

        private static OriginalProgression Fresh(int gold=0)
        { return new OriginalProgression(OriginalProgressionConfig.Load(),new OriginalProgressionState { gold=gold },
            new FixtureCatalog(RecoveredCatalog.Load()),new MemoryProfileStore()); }

        private static OriginalProgression Restore(OriginalProgression service)
        {
            var session=string.IsNullOrEmpty(service.State.dailySession) ? new ApplicationSession() : new ApplicationSession(service.State.dailySession);
            return OriginalProgression.FromJson(service.Config,service.ToJson(),new FixtureCatalog(RecoveredCatalog.Load()),new MemoryProfileStore(),session:session);
        }

        private static void CheckInjectedProfileIsolation()
        {
            var config=OriginalProgressionConfig.Load();
            var records=RecoveredCatalog.Load();
            var catalog=new FixtureCatalog(records.Take(2).ToArray());
            var firstStore=new MemoryProfileStore();
            var secondStore=new MemoryProfileStore();
            var session=new ApplicationSession("profile-fixture-session");
            var calendar=new FixtureCalendar(new DateTime(2026,10,20,12,0,0));
            var first=new ProfileServiceFactory(config,catalog,firstStore,session,calendar).Create();
            var second=new ProfileServiceFactory(config,catalog,secondStore,new ApplicationSession("other-fixture-session"),calendar).Create();
            Check(first.State.diceStartedDay=="2026-10-20","profile startup uses the injected calendar");
            first.RecordSongStart(records[0].id);
            first.RecordSongStart(records[2].id);
            Check(first.SongProgress(records[0].id)?.starts==1 && first.SongProgress(records[2].id)==null,
                "progression accepts only songs from its injected catalog");
            Check(second.SongProgress(records[0].id)==null,"separate injected profile stores do not share progress");
            var firstClaim=first.FlipDailyCard(calendar.Today,4,19);
            Check(firstClaim.success && firstClaim.role=="9","injected profile retains the first card reward");
            var reopened=new ProfileServiceFactory(config,catalog,firstStore,session,calendar).Create();
            Check(reopened.SongProgress(records[0].id)?.starts==1 && reopened.DailyCardClaimed(4) &&
                !reopened.FlipDailyCard(calendar.Today,4,19,true).success,
                "same application session and store preserve progress and duplicate claim guards");
            var nextSession=new ProfileServiceFactory(config,catalog,firstStore,new ApplicationSession("next-fixture-session"),calendar).Create();
            Check(!nextSession.DailyCardClaimed(4) && nextSession.State.firstDailyRewardClaimed && nextSession.IsRoleUnlocked("9"),
                "new application session resets the card page while preserving earned rewards");
            var restored=OriginalProgression.FromJson(config,firstStore.Read(),catalog,new MemoryProfileStore(),session:session);
            Check(restored.SongProgress(records[0].id)?.starts==1 && restored.IsRoleUnlocked("9"),
                "injected store keeps the existing JSON save contract");
            Check(secondStore.Read()!=firstStore.Read() && second.Gold==0 && !second.IsRoleUnlocked("9"),
                "writes and earned rewards remain isolated between stores");
        }

        private sealed class MemoryProfileStore : IProfileStore
        {
            private string json="";
            public string Read() => json;
            public void Write(string value) { json=value; }
        }

        private sealed class FixtureCalendar : ICalendar
        {
            public FixtureCalendar(DateTime now) { Now=now; }
            public DateTime Today => Now.Date;
            public DateTime Now { get; }
        }

        private sealed class FixtureCatalog : ISongCatalog
        {
            public FixtureCatalog(SongRecord[] songs) { Songs=songs; }
            public SongRecord[] Songs { get; }
            public MapTimeline LoadMap(string id) => throw new NotSupportedException();
            public AudioClip LoadAudio(SongRecord song,bool preview=false) => throw new NotSupportedException();
            public Sprite LoadIcon(SongRecord song) => throw new NotSupportedException();
        }

        private static void CheckClaimsAndPersistence()
        {
            var date=new DateTime(2026,10,20,12,0,0);
            var service=Fresh();
            var first=service.ClaimLogin(date);
            Check(first.success && first.gold==100 && service.Gold==100 && service.Diamonds==0,"native login enum1 credits Gold, not Diamond");
            Check(!service.ClaimLogin(date).success && service.Gold==100,"login claim is atomic and once per local date");
            service=Restore(service);
            Check(!service.ClaimLogin(date).success,"same-day login ledger survives JSON restart");
            var role=service.ClaimLogin(date.AddDays(1));
            Check(role.success && role.role=="12" && service.IsRoleUnlocked("12") && service.SelectedRole=="12","day2 grants and selects original cat12");
            service.ClaimLogin(date.AddDays(4));
            Check(service.LoginDayNumber==4 && service.State.loginContinuous==0,"missed dates reset continuous counter while reward-day progress continues");
            Check(service.SelectWheelReward(24).goldNumber==100 && service.SelectWheelReward(25).goldNumber==300 && service.SelectWheelReward(99).roleId=="9","original weighted reward boundary tickets");
            var cards=Fresh();
            Check(cards.FlipDailyCard(date,4,19).role=="9" && cards.IsRoleUnlocked("9"),"original first-ever free card guarantees cat9 rather than a weighted gold draw");
            Check(!cards.FlipDailyCard(date,4,19,true).success,"same card cannot be claimed twice");
            for(int i=0;i<9;i++)if(i!=4)Check(cards.FlipDailyCard(date,i,i,true).success,"remaining original cards can use explicit offline reward actions");
            Check(cards.WheelClaimsRemaining(date)==0 && !cards.SpinWheel(date,10,true).success,"nine-card reward page has a finite claim ledger");
            service.SetSettings(false,false,true,6,.037f);
            var restored=Restore(service);
            Check(!restored.State.settings.sound && !restored.State.settings.vibration && restored.State.settings.language==6 && Mathf.Abs(restored.State.settings.calibrationOffsetSeconds-.037f)<.000001f,"settings persist independently of original PlayerPrefs");
            Check(OriginalProgression.SaveKey.StartsWith("DancingCatsClone.v1.",StringComparison.Ordinal),"new progression uses an isolated save namespace");
            restored.SetRhythmLineHeight(.99f);restored.AdjustCalibration(1);restored.AdjustCalibration(1);
            Check(restored.State.settings.rhythmLineHeight==1f,"original calibration advances .01 and clamps at1");
        }

        private static void CheckOfflineUnlockProgress()
        {
            var service=Fresh();
            for(int i=0;i<4;i++) Check(!service.RewardRoleUnlock("20") && !service.IsRoleUnlocked("20"),"five-action original cat remains locked before final action");
            Check(service.RoleUnlockProgress("20")==4 && service.RewardRoleUnlock("20") && service.IsRoleUnlocked("20"),"fifth explicit offline reward action unlocks original cat20");
            Check(!service.RewardRoleUnlock("20") && service.RoleUnlockProgress("20")==5 && service.Gold==0,"repeated unlock cannot farm wallet or counter");
            Check(!service.RewardRoleUnlock("12"),"login-only cats cannot use advertising progress");
            Check(!service.RewardTrailUnlock("10") && !service.RewardTrailUnlock("10") && service.RewardTrailUnlock("10"),"original trail10 uses its actual three-action requirement");
            var restored=Restore(service);
            Check(restored.IsTrailUnlocked("10") && restored.SelectTrail("10") && !restored.SelectTrail("9"),"trail unlock and selection survive restart without selecting locked trails");
        }

        private static void CheckOriginalDiceBoard()
        {
            var date=new DateTime(2026,10,20);
            var service=Fresh();service.StartDiceEvent(date);
            Check(!service.RollDice(date,6).success,"first original non-bonus die face is restricted to3..5");
            var first=service.RollDice(date,5);
            Check(first.success && first.position==5 && first.gold==100 && !service.IsRoleUnlocked("22"),"first roll collects every passed space and retains step-based position");
            Check(!service.RollDice(date,1).success,"one free roll per event date in first seven days");
            var next=service.RollDice(date,1,true);
            Check(next.success && next.position==6 && service.IsRoleUnlocked("22"),"crossing arrayindex5 grants original cat22");
            Check(!service.RollDice(date,1,true).success,"one reward-action roll per date in first seven days");
            var restored=Restore(service);
            Check(restored.DiceRollsRemaining(date)==0 && restored.DiceRewardActionsRemaining(date)==0,"both dice entitlements persist across restarts");
            Check(restored.RollDice(date.AddDays(1),6).gold==600,"dice credits gold from all intermediate prize spaces");
            Check(restored.RollDice(date.AddDays(1),3,true).extraRolls==1 && restored.DiceRollsRemaining(date.AddDays(1))==1,"native Free cell restores one free entitlement");
            Check(restored.NextDiceFace(date.AddDays(1),41)<=6,"restored free entitlement retains the normal noninitial die range");
            var traversed=Fresh();traversed.State.dicePosition=35;traversed.State.claimedDiceSpaces=Enumerable.Range(0,35).ToList();
            Check(traversed.RollDice(date,6).gold==3000,"sourceindex35Gold1000 plus terminalindex37Gold2000 are both collected");
            var end=Fresh();end.State.dicePosition=37;end.State.claimedDiceSpaces=Enumerable.Range(0,37).ToList();
            var finish=end.RollDice(date,1);
            Check(finish.success && finish.position==38 && finish.gold==2000 && end.IsRoleUnlocked("21") && end.State.diceComplete,"terminal board collects exact original2000Gold and cat21");
            Check(!end.RollDice(date.AddDays(1),6,true).success,"finished finite board cannot be farmed the next day");
        }

        private static void CheckChallengeAndHouse()
        {
            var date=new DateTime(2026,10,20);var service=Fresh(1000);
            var song=service.ChallengeSong(date);
            service.RecordSongResult(song,3,120,false,false,date);
            Check(!service.CanClaimChallenge(date,0),"an ordinary run does not set the native challenge route flag");
            RecordChallenge(service,song,3,120,false,date);
            Check(service.ClaimChallenge(date,0).gold==500 && !service.ClaimChallenge(date,0).success,"challenge uses original tier reward and refuses duplicate claims");
            Check(!service.CanClaimChallenge(date.AddDays(1),0),"yesterday's result does not complete today's challenge");
            Check(service.TryBuyHouseVariant(1,0) && service.TryBuyHouseVariant(1,1) && service.Gold==1100,"house uses original row1 free variant and400Gold price");
            Check(service.TryBuyHouseVariant(1,1) && service.Gold==1100,"owned furniture selection never charges again");
            Check(!service.TryBuyHouseVariant(1,2) && service.TryBuyHouseVariant(1,2,true),"house sentinel-1 requires an explicit offline reward action");
            var restored=Restore(service);
            Check(restored.HouseProgress(1).selected==2 && restored.HouseProgress(1).owned.Count==3,"all purchased original furniture variants persist");
            Check(service.TryUnlockCatSlot(1,0) && service.TryUnlockCatSlot(1,1),"first floor's original two free cat slots");
            Check(service.AssignCatToSlot(1,0,"1") && !service.AssignCatToSlot(1,1,"1"),"one original cat cannot occupy two slots accidentally");
            Check(service.AssignCatToSlot(1,1,"1",true) && string.IsNullOrEmpty(service.CatSlot(1,0).role),"explicit cat move clears the previous slot");
            var partial=Fresh();var songs=RecoveredCatalog.Load();
            partial.RecordSongResult(songs[0].id,2,80,false,false,date);partial.RecordSongResult(songs[1].id,1,40,false,false,date);
            Check(partial.TotalStars==0 && !partial.IsFloorUnlocked(1),"native house does not add partial stars from different songs");
            partial.RecordSongResult(songs[0].id,6,300,true,false,date);
            Check(partial.TotalStars==3 && partial.IsFloorUnlocked(1),"one song with at least three stars counts as exactly three house stars even with later crowns");
        }

        private static void CheckRunSettlement()
        {
            var service=Fresh();var date=new DateTime(2026,10,20);var song=RecoveredCatalog.Load()[0].id;
            service.RecordSongResult(song,9,1234,true,false,date);
            service.RecordSongResult(song,2,90,true,false,date);
            service.RecordSongResult(song,3,120,false,true,date);
            var restored=Restore(service);
            Check(restored.SongProgress(song).endlessStars==9 && restored.SongProgress(song).normalStars==3 && restored.TotalStars==3,"native nine endless marks persist independently of normal stars and count once for house");
            Check(restored.Gold==0,"recording a score does not credit a settlement without its run ledger");
            var quote=restored.PrepareSettlement("run-one",3,13,77,"2");
            Check(quote.baseGold>=350 && quote.baseGold<=500 && quote.collectedCoins==13 && quote.eligible && !quote.claimed,"native settlement snapshots configured random350..500 and actual collected coins");
            var again=restored.PrepareSettlement("run-one",9,999,999,"1");
            Check(again.totalGold==quote.totalGold && again.baseGold==quote.baseGold && again.roleId=="2" && again.collectedCoins==13,"same run cannot reroll or replace the snapshotted coin bonus");
            var paid=restored.AutoRewardSettlement("run-one");
            Check(paid.success && paid.gold==quote.totalGold && restored.Gold==quote.totalGold,"native full tier automatically credits its exact quote");
            restored=Restore(restored);
            Check(!restored.AutoRewardSettlement("run-one").success && restored.Gold==quote.totalGold,"duplicate callback after JSON restart cannot pay the same run twice");
            var partial=restored.PrepareSettlement("run-partial",2,200,77,"2");
            Check(!partial.eligible && !restored.AutoRewardSettlement("run-partial").success,"native one or two marks do not invoke the three-mark automatic gold reward");
            Check(OriginalProgression.CalculateSettlementGold(350,13,.3f)==471 && OriginalProgression.CalculateSettlementGold(500,0,0)==500,"native settlement truncates the role bonus after summing randomized base and coins");
            var role=restored.Config.roles.Single(r=>r.Name=="2");var oldKind=role.RoleAttributeType;role.RoleAttributeType=2;
            var hp=restored.PrepareSettlement("run-hp",6,0,77,"2");
            Check(hp.roleBonus==0 && hp.totalGold==hp.baseGold,"native HP attribute does not apply the dormant CoinPercent field");role.RoleAttributeType=oldKind;
        }

        private static void CheckStartsAndNextSong()
        {
            var service=Fresh();var date=new DateTime(2026,10,20);
            var catalog=RecoveredCatalog.Load().Where(s=>s.catalogId<900 && !s.catalog.disable).OrderBy(s=>s.catalogId).ToArray();
            var free=catalog.Where(s=>s.catalog.musicUnlockType==0).ToArray();
            Check(free.Length>=3,"original active catalog contains at least three initially unlocked songs");
            Check(service.GetNextPlaySong()==free[0].id,"native Next first chooses the earliest unlocked unstarted song");
            service.RecordSongStart(free[0].id);
            Check(service.SongProgress(free[0].id).starts==1 && service.SongProgress(free[0].id).completions==0,"abandoned run counts as a start independently of completions");
            service=Restore(service);
            Check(service.SongProgress(free[0].id).starts==1 && service.GetNextPlaySong()==free[1].id,"starts survive restart and Next advances to the next unlocked unstarted song");
            foreach(var song in free.Skip(1))service.RecordSongStart(song.id);
            var locked=catalog.First(s=>!service.IsSongUnlocked(s.id));
            Check(service.GetNextPlaySong(true)==locked.id,"after unlocked starts native Next offers the earliest locked song when permitted");
            Check(service.GetNextPlaySong(false)==free[0].id,"without locked recommendations native Next chooses the earliest song below three best marks");
            service.RecordSongResult(free[0].id,3,100,false,true,date);
            service.RecordSongResult(free[1].id,3,100,true,false,date);
            Check(service.SongProgress(free[0].id).starts==1 && service.GetNextPlaySong(false)==free[2].id,"settlement does not double-count starts and combined normal/endless best drives native Next");
            foreach(var song in free.Skip(2))service.RecordSongResult(song.id,3,100,false,true,date);
            service.State.selectedSong=free[0].id;
            var next=service.GetNextPlaySong(false,false,81);
            Check(next!=free[0].id && free.Any(s=>s.id==next) && next==service.GetNextPlaySong(false,true,81),"native final fallback chooses another unlocked song deterministically and has no mode-dependent priority");
            service.RecordSongStart("not-in-catalog");
            Check(service.SongProgress("not-in-catalog")==null,"invalid song cannot create start progress");
        }

        private static void CheckNativeChallengeTiers()
        {
            var date=new DateTime(2026,10,20);var service=Fresh();var song=service.ChallengeSong(date);
            service.RecordSongResult(song,9,1200,true,true,date);
            Check(service.State.challengeStars==0 && !service.CanClaimChallenge(date,0),"an ordinary endless best is not a daily challenge result");
            RecordChallenge(service,song,2,90,false,date);
            Check(service.State.challengeStars==2 && !service.CanClaimChallenge(date,0),"native challenge retains partial failure marks but first reward requires three");
            RecordChallenge(service,song,3,120,false,date);
            Check(service.CanClaimChallenge(date,0) && !service.CanClaimChallenge(date,1) && !service.CanClaimChallenge(date,2),"three marks unlock only the native first checkpoint");
            Check(service.ClaimChallenge(date,0).gold==500 && !service.ClaimChallenge(date,0).success,"native first checkpoint pays500 once");
            RecordChallenge(service,song,5,220,false,date);
            RecordChallenge(service,song,4,200,false,date);
            service=Restore(service);
            Check(service.State.challengeStars==5 && !service.CanClaimChallenge(date,1),"native challenge stores a maximum rather than adding attempts and retains it after restart");
            RecordChallenge(service,song,6,260,false,date);
            Check(service.CanClaimChallenge(date,1) && !service.CanClaimChallenge(date,2) && service.ClaimChallenge(date,1).gold==1000,"six marks unlock the native1000Gold second checkpoint");
            RecordChallenge(service,song,8,340,false,date);
            Check(!service.CanClaimChallenge(date,2),"eight marks remain below the final native checkpoint");
            RecordChallenge(service,song,9,400,false,date);
            Check(service.CanClaimChallenge(date,2) && service.ClaimChallenge(date,2).gold==1500 && service.Gold==3000,"nine marks unlock the native1500Gold final checkpoint and total3000");
            service=Restore(service);
            Check(service.State.challengeStars==9 && !Enumerable.Range(0,3).Any(tier=>service.CanClaimChallenge(date,tier)),"native nine-mark maximum and all three claim guards survive restart");
            Check(!service.CanClaimChallenge(date.AddDays(1),0) && service.State.challengeStars==0 && service.State.claimedChallengeTiers.Count==0,"native next-local-date reset clears challenge marks and checkpoint claims");
        }

        private static void RecordChallenge(OriginalProgression service,string id,int stars,int score,bool completed,DateTime now)
        {
            var method=typeof(OriginalProgression).GetMethods().FirstOrDefault(m=>m.Name=="RecordSongResult" && m.GetParameters().Length==7);
            if(method!=null)method.Invoke(service,new object[]{id,stars,score,true,completed,now,true});
            else service.RecordSongResult(id,stars,score,true,completed,now);
        }

        private static Type Find(string name)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("DancingCats.Clone."+name)).FirstOrDefault(t=>t!=null);
            Check(type!=null,"RED: "+name+" is not implemented yet");return type;
        }
        private static object F(object value,string name)
        {
            var field=value.GetType().GetField(name);
            Check(field!=null,value.GetType().Name+"."+name+" is missing");return field.GetValue(value);
        }
        private static object[] Rows(object value,string field) { return ((IEnumerable)F(value,field)).Cast<object>().ToArray(); }
        private static double N(object value,string field) { return Convert.ToDouble(F(value,field)); }
        private static string S(object value,string field) { return (string)F(value,field); }
        private static void Check(bool condition,string message) { if(!condition)throw new Exception("PROGRESSION FAIL: "+message); }
    }
}
