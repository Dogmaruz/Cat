using System;
using System.Collections.Generic;
using System.Reflection;
using DancingCat;
using UnityEngine;

public static class CatMetaValidation
{
    private static readonly DateTime FirstDay = new DateTime(2026, 10, 1);

    public static void Run()
    {
        var failures = new List<string>();
        Check("seven daily rewards escalate and cycle without duplicate claims", DailyCycle, failures);
        Check("missing a day resets the streak and clock rollback cannot reclaim", DailyMissedDay, failures);
        Check("daily challenge requires today's actual completion", DailyChallenge, failures);
        Check("room upgrades spend the real wallet and cap at five", Rooms, failures);
        Check("trails require ownership before selection", Trails, failures);
        Check("seeded dice spend ten and award the rolled amount", Dice, failures);
        Check("dice allows three daily rolls and restores them tomorrow", DailyDiceLimit, failures);
        Check("JSON round trip preserves meta progress and settings", Persistence, failures);
        Check("invalid saved values normalize to playable bounds", Defaults, failures);
        if (failures.Count != 0) throw new Exception("Cat meta validation failed: " + string.Join("; ", failures));
        Debug.Log("CAT_META_VALIDATION PASS: 9 checks.");
    }

    private static void DailyCycle()
    {
        var rules = NewRules();
        var expected = new[] { 50, 75, 100, 125, 150, 200, 300 };
        for (var day = 0; day < 7; day++)
        {
            Equal(expected[day], Call<int>(rules, "ClaimDaily", FirstDay.AddDays(day)), "Daily reward");
            Equal(0, Call<int>(rules, "ClaimDaily", FirstDay.AddDays(day)), "Duplicate daily claim");
        }
        Equal(50, Call<int>(rules, "ClaimDaily", FirstDay.AddDays(7)), "New reward cycle");
        Equal(1, Field<int>(Data(rules), "dailyStreak"), "New cycle day");
    }

    private static void DailyMissedDay()
    {
        var rules = NewRules();
        Call<int>(rules, "ClaimDaily", FirstDay);
        Equal(50, Call<int>(rules, "ClaimDaily", FirstDay.AddDays(2)), "Missed-day reward reset");
        Equal(1, Field<int>(Data(rules), "dailyStreak"), "Missed-day streak reset");
        Equal(0, Call<int>(rules, "ClaimDaily", FirstDay.AddDays(1)), "Clock rollback claim");
    }

    private static void DailyChallenge()
    {
        var rules = NewRules();
        Call<bool>(rules, "RecordCompletion", FirstDay, "song-a", "song-a");
        Equal(0, Call<int>(rules, "ClaimChallenge", FirstDay.AddDays(1), "song-a"), "Yesterday's completion");
        Require(!Call<bool>(rules, "RecordCompletion", FirstDay.AddDays(1), "song-b", "song-a"), "Wrong song completed challenge");
        Require(Call<bool>(rules, "RecordCompletion", FirstDay.AddDays(1), "song-a", "song-a"), "Today's completion was ignored");
        Equal(75, Call<int>(rules, "ClaimChallenge", FirstDay.AddDays(1), "song-a"), "Challenge reward");
        Equal(0, Call<int>(rules, "ClaimChallenge", FirstDay.AddDays(1), "song-a"), "Duplicate challenge claim");
        var index = Call<int>(rules, "ChallengeIndex", FirstDay, 3);
        Require(index >= 0 && index < 3, "Daily song index out of bounds");
        Equal((index + 1) % 3, Call<int>(rules, "ChallengeIndex", FirstDay.AddDays(1), 3), "Daily song rotation");
    }

    private static void Rooms()
    {
        var rules = NewRules();
        var wallet = new Wallet(1000);
        for (var level = 0; level < 5; level++)
            Require(Call<bool>(rules, "TryUpgradeRoom", 0, wallet.Spend), "Affordable room upgrade rejected");
        Equal(5, Call<int>(rules, "RoomLevel", 0), "Room level cap");
        Equal(400, wallet.Coins, "Five upgrades must cost 40+80+120+160+200");
        Require(!Call<bool>(rules, "TryUpgradeRoom", 0, wallet.Spend), "Maxed room upgraded again");
        Equal(400, wallet.Coins, "Maxed upgrade spent coins");
        Require(!Call<bool>(rules, "TryUpgradeRoom", -1, wallet.Spend), "Invalid room upgraded");
        Require(!Call<bool>(rules, "TryUpgradeRoom", 1, new Wallet(39).Spend), "Unaffordable room upgraded");
        Equal(0, Call<int>(rules, "RoomLevel", 1), "Failed purchase changed room level");
    }

    private static void Trails()
    {
        var rules = NewRules();
        var wallet = new Wallet(500);
        Require(Call<bool>(rules, "IsTrailOwned", 0), "Default trail missing");
        Require(!Call<bool>(rules, "SelectTrail", 9), "Locked trail selected");
        Require(Call<bool>(rules, "TryUnlockTrail", 9, wallet.Spend), "Affordable trail rejected");
        Equal(290, wallet.Coins, "Trail price");
        Require(Call<bool>(rules, "SelectTrail", 9), "Owned trail rejected");
        Equal(9, Field<int>(Data(rules), "selectedTrail"), "Selected trail");
        Require(!Call<bool>(rules, "TryUnlockTrail", 9, wallet.Spend), "Owned trail charged twice");
        Equal(290, wallet.Coins, "Duplicate trail cost");
    }

    private static void Dice()
    {
        var first = new Wallet(100);
        var second = new Wallet(100);
        var reward = Call<int>(NewRules(), "RollDice", 123, first.Spend, first.Add);
        var repeated = Call<int>(NewRules(), "RollDice", 123, second.Spend, second.Add);
        Require(Array.IndexOf(new[] { 0, 5, 10, 15, 25, 50 }, reward) >= 0, "Impossible dice reward");
        Equal(reward, repeated, "Seeded dice result");
        Equal(90 + reward, first.Coins, "Dice wallet transaction");
        var poor = new Wallet(9);
        Equal(-1, Call<int>(NewRules(), "RollDice", 1, poor.Spend, poor.Add), "Unaffordable dice roll");
        Equal(9, poor.Coins, "Failed dice roll changed wallet");
    }

    private static void Persistence()
    {
        var rules = NewRules();
        Call<int>(rules, "ClaimDaily", FirstDay);
        Call<bool>(rules, "TryUpgradeRoom", 2, new Wallet(100).Spend);
        var data = Data(rules);
        Set(data, "latencyMs", -137f);
        Set(data, "vibration", false);
        Set(data, "targetFps", 120);
        Set(data, "difficulty", 2);
        Set(data, "endless", true);
        var restoredData = JsonUtility.FromJson(JsonUtility.ToJson(data), data.GetType());
        var restored = NewRules(restoredData);
        Equal(1, Call<int>(restored, "RoomLevel", 2), "Saved room level");
        Equal(0, Call<int>(restored, "ClaimDaily", FirstDay), "Saved daily claim");
        Require(Field<float>(Data(restored), "latencyMs") == -137f, "Latency not restored");
        Require(!Field<bool>(Data(restored), "vibration") && Field<bool>(Data(restored), "endless"), "Options not restored");
        Equal(120, Field<int>(Data(restored), "targetFps"), "Saved frame rate");
    }

    private static void DailyDiceLimit()
    {
        var rules = NewRules();
        var wallet = new Wallet(100);
        for (var roll = 0; roll < 3; roll++)
            Require(RollAt(rules, FirstDay, roll, wallet) >= 0, "A permitted daily roll was rejected");
        var balanceAfterThree = wallet.Coins;
        var saved = Data(rules);
        rules = NewRules(JsonUtility.FromJson(JsonUtility.ToJson(saved), saved.GetType()));
        Equal(-1, RollAt(rules, FirstDay, 4, wallet), "Fourth daily dice roll");
        Equal(balanceAfterThree, wallet.Coins, "A refused roll charged coins");
        Equal(0, Call<int>(rules, "DiceRollsRemaining", FirstDay), "Exhausted daily rolls");
        Require(RollAt(rules, FirstDay.AddDays(1), 5, wallet) >= 0, "Tomorrow's first roll was rejected");
        Equal(2, Call<int>(rules, "DiceRollsRemaining", FirstDay.AddDays(1)), "Tomorrow's remaining rolls");
    }

    private static int RollAt(object rules, DateTime day, int seed, Wallet wallet)
    {
        var method = rules.GetType().GetMethod("RollDice", new[]
            { typeof(DateTime), typeof(int), typeof(Func<int, bool>), typeof(Action<int>) });
        // Exercise the existing unlimited behavior for RED before the dated overload exists.
        return method != null ? (int)method.Invoke(rules, new object[] { day, seed, wallet.Spend, wallet.Add }) :
            Call<int>(rules, "RollDice", seed, wallet.Spend, wallet.Add);
    }

    private static void Defaults()
    {
        var rules = NewRules();
        var data = Data(rules);
        Set(data, "latencyMs", 999f);
        Set(data, "targetFps", 999);
        Set(data, "difficulty", 99);
        Set(data, "roomLevels", new[] { -5, 999 });
        Set(data, "ownedTrails", 0);
        Set(data, "selectedTrail", 9);
        var normalized = NewRules(data);
        Require(Field<float>(Data(normalized), "latencyMs") == 200f, "Latency bound");
        Equal(60, Field<int>(Data(normalized), "targetFps"), "Unsupported frame rate fallback");
        Equal(2, Field<int>(Data(normalized), "difficulty"), "Difficulty bound");
        Equal(0, Call<int>(normalized, "RoomLevel", 0), "Room lower bound");
        Equal(5, Call<int>(normalized, "RoomLevel", 1), "Room upper bound");
        Equal(0, Field<int>(Data(normalized), "selectedTrail"), "Locked saved trail fallback");
        Require(Call<bool>(normalized, "IsTrailOwned", 0), "Default trail restore");
    }

    private static object NewRules(object data = null)
    {
        var assembly = typeof(CatSong).Assembly;
        var dataType = assembly.GetType("DancingCat.CatMetaData");
        var ruleType = assembly.GetType("DancingCat.CatMetaRules");
        Require(dataType != null && ruleType != null, "CatMetaRules is not implemented yet");
        return Activator.CreateInstance(ruleType, new[] { data ?? Activator.CreateInstance(dataType) });
    }

    private static object Data(object rules) => rules.GetType().GetProperty("Data").GetValue(rules);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    private static T Call<T>(object target, string name, params object[] arguments)
    {
        var types = Array.ConvertAll(arguments, value => value.GetType());
        var method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, types, null);
        Require(method != null, "Missing rule method " + name);
        return (T)method.Invoke(target, arguments);
    }

    private static void Check(string name, Action test, List<string> failures)
    {
        try { test(); Debug.Log("CAT_META_VALIDATION PASS: " + name); }
        catch (Exception exception)
        {
            var cause = exception.InnerException ?? exception;
            failures.Add(name + ": " + cause.Message);
            Debug.LogError("CAT_META_VALIDATION FAIL: " + name + ": " + cause.Message);
        }
    }

    private static void Equal(int expected, int actual, string message) =>
        Require(expected == actual, message + ": expected " + expected + ", actual " + actual);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    private sealed class Wallet
    {
        public int Coins;
        public readonly Func<int, bool> Spend;
        public readonly Action<int> Add;
        public Wallet(int initial)
        {
            Coins = initial;
            Spend = amount => { if (amount < 0 || Coins < amount) return false; Coins -= amount; return true; };
            Add = amount => Coins += amount;
        }
    }
}
