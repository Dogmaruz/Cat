using System;
using System.IO;
using DancingCats.Clone;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class IslandSteeringChecks
    {
        public static void RunChecks()
        {
            float reference = Run(60);
            foreach (int fps in new[] { 20, 30, 60, 120 })
                if (Mathf.Abs(Run(fps) - reference) > .00001f)
                    throw new InvalidOperationException("Steering response depends on render frame rate: " + fps);
            if (Mathf.Abs(NativeRhythmRules.SteerLateral(0, 1, 1f / 60) - .25f) > .000001f)
                throw new InvalidOperationException("The original 60 Hz control response changed.");
            if (NativeRhythmRules.SteerLateral(.6f, -1, 0) != .6f || NativeRhythmRules.SteerLateral(0, 5, 2) > 1.5f)
                throw new InvalidOperationException("Steering zero-time or lane limit failed.");
            CheckTouchDrag();
            Directory.CreateDirectory(IslandPilotJobs.ReviewPath);
            File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "steering-checks.json"),
                "{\"passed\":true,\"fps\":[20,30,60,120],\"sameResponseAfter100ms\":true,\"native60HzGainPreserved\":true," +
                "\"touchFullSpanScreenFraction\":0.24,\"touchResolutionIndependent\":true,\"touchEdgeReversalImmediate\":true,\"touchAnchorPreserved\":true,\"desktopDragPreserved\":true}");
        }

        private static void CheckTouchDrag()
        {
            foreach (float width in new[] { 320f, 540f, 1080f, 1440f })
            {
                float pointerOrigin = width * .2f, dragOrigin = -1.5f;
                float target = NativeRhythmRules.DragTargetX(pointerOrigin + width * .24f, width, true, ref pointerOrigin, ref dragOrigin);
                Close(target, 1.5f, "A full touch lane crossing must fit within 24% of screen width.");
                pointerOrigin = width * .4f; dragOrigin = .7f;
                Close(NativeRhythmRules.DragTargetX(pointerOrigin, width, true, ref pointerOrigin, ref dragOrigin), .7f,
                    "Touch-down moved the hero before the finger moved.");
                pointerOrigin = 0; dragOrigin = 0;
                Close(NativeRhythmRules.DragTargetX(width * .08f, width, true, ref pointerOrigin, ref dragOrigin), 1f,
                    "Equal fractional finger travel must produce equal lateral displacement at every resolution.");
                pointerOrigin = 0; dragOrigin = 0;
                Close(NativeRhythmRules.DragTargetX(width * .6f, width, true, ref pointerOrigin, ref dragOrigin), 1.5f,
                    "Touch overscroll must stop at the lane boundary.");
                Close(NativeRhythmRules.DragTargetX(width * .58f, width, true, ref pointerOrigin, ref dragOrigin), 1.25f,
                    "Reversing after right overscroll must move immediately, without unwinding invisible travel.");
                pointerOrigin = width; dragOrigin = 0;
                Close(NativeRhythmRules.DragTargetX(width * .4f, width, true, ref pointerOrigin, ref dragOrigin), -1.5f,
                    "Left touch overscroll must stop at the lane boundary.");
                Close(NativeRhythmRules.DragTargetX(width * .42f, width, true, ref pointerOrigin, ref dragOrigin), -1.25f,
                    "Reversing after left overscroll must move immediately.");
                pointerOrigin = width * .2f; dragOrigin = .3f;
                Close(NativeRhythmRules.DragTargetX(width * .3f, width, false, ref pointerOrigin, ref dragOrigin), .96f,
                    "Desktop pointer sensitivity changed.");
                Close(pointerOrigin, width * .2f, "Desktop pointer origin changed.");
                Close(dragOrigin, .3f, "Desktop drag origin changed.");
            }
        }

        private static void Close(float actual, float expected, string message)
        {
            if (Mathf.Abs(actual - expected) > .00001f) throw new InvalidOperationException(message + " actual=" + actual + ", expected=" + expected);
        }

        private static float Run(int fps)
        {
            float value = 0;
            for (int i = 0; i < fps / 10; i++) value = NativeRhythmRules.SteerLateral(value, 1, 1f / fps);
            return value;
        }
    }
}
