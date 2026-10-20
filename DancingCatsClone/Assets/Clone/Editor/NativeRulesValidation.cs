using System;
using System.Reflection;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class NativeRulesValidation
    {
        public static void Run()
        {
            var rules = typeof(RecoveredCatalog).Assembly.GetType("DancingCats.Clone.NativeRhythmRules");
            if (rules == null) throw new Exception("NATIVE_RULES RED: original rhythm rules are not implemented");
            var warp = rules.GetMethod("WarpPercent", BindingFlags.Public | BindingFlags.Static);
            var height = rules.GetMethod("HeightForDistance", BindingFlags.Public | BindingFlags.Static);
            var jump = rules.GetMethod("JumpY", BindingFlags.Public | BindingFlags.Static);
            var perfect = rules.GetMethod("IsPerfect", BindingFlags.Public | BindingFlags.Static);
            Check(warp != null && height != null && jump != null && perfect != null, "native rule interfaces exist");
            Near((float)warp.Invoke(null, new object[] { .5f }), .41108018f, "authored original Hermite curve midpoint");
            Near((float)height.Invoke(null, new object[] { 3f }), 1.2f, "short jump height comes from original distance parameter");
            Near((float)height.Invoke(null, new object[] { 8f }), 2f, "long jump height uses original cap");
            Near((float)jump.Invoke(null, new object[] { .3f, 2f, .5f }), 2.236746f, "warped original parabola at half longitudinal progress");
            Near((float)jump.Invoke(null, new object[] { .3f, 2f, 1f }), .3f, "arc ends at the landing plane");
            Check((bool)perfect.Invoke(null, new object[] { .249f, false, false }) &&
                  !(bool)perfect.Invoke(null, new object[] { .251f, false, false }), "normal Perfect includes native +0.05 margin");
            Check(!(bool)perfect.Invoke(null, new object[] { .21f, true, false }) &&
                   (bool)perfect.Invoke(null, new object[] { .21f, true, true }), "slider threshold and forced FollowEnd Perfect differ");
            var scoreType = typeof(RecoveredCatalog).Assembly.GetType("DancingCats.Clone.OriginalScore");
            Check(scoreType != null, "original score accumulator exists");
            var score = Activator.CreateInstance(scoreType);
            var apply = scoreType.GetMethod("Apply");
            foreach (bool hit in new[] { true, true, true, false, true }) apply.Invoke(score, new object[] { hit });
            Check((int)scoreType.GetProperty("Value").GetValue(score) == 8 &&
                  (int)scoreType.GetProperty("Streak").GetValue(score) == 1,
                  "native Perfect streak grows 1,2,3; Great adds one and resets the streak");
            Debug.Log("NATIVE_RULES PASS: recovered arc, height, accuracy and scoring behaviors");
        }

        private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .0001f, message);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("NATIVE_RULES FAIL: " + message);
            Debug.Log("NATIVE_RULES PASS: " + message);
        }
    }
}
