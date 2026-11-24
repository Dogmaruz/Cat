using System;
using System.IO;
using System.Reflection;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RadioArchipelago.Editor
{
    /// <summary>Final-trigger ordering, finite departure, pausing and real binding restoration.</summary>
    public static class IslandFinishDepartureChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed, waitsForNativeFinish, startsOnce, boundedFlight, pauseFreezes, hitchCompletes;
            public bool forwardFacingCollection, restoredOnRebind, restoredOnDeactivate, triggerFrameTimeExcluded, currentPresentationScalePreserved;
            public int frameRatesChecked;
            public long warmedTickAllocatedBytes;
            public string checkedUtc;
        }

        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Finish departure checks require stable Edit Mode.");
            CourierFinishHoverChecks.Run();
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("O"), forwardFacingCollection = true };
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var departure = new IslandFinishDeparture();
                Vector3 origin = new Vector3(11, 4, -7);
                Quaternion heading = Quaternion.Euler(0, -53, 0);
                Vector3 forward = heading * Vector3.forward;
                Require(!departure.TryBegin(false, 20, origin, heading),
                    "Collecting or hiding a spark alone must not start the departure before the native final trigger.");
                departure.Tick(10);
                Require(!departure.Started && departure.Progress == 0 && departure.StartCount == 0,
                    "Waiting for the actual native finish must not advance a visual flight clock.");
                report.waitsForNativeFinish = true;
                Require(departure.TryBegin(true, 20, origin, heading), "The real final trigger must start the departure.");
                Require(departure.Position == origin && departure.VisualScale == 1 && departure.CollectedCoinsAtStart == 20,
                    "The departure must begin at the actual presentation position, after native collections, without a scale pop.");
                Require(!departure.TryBegin(true, 20, origin + Vector3.one * 100, Quaternion.identity) && departure.StartCount == 1,
                    "Repeated final-trigger samples must not restart the flight or change its direction.");
                report.startsOnce = true;
                for (int pause = 0; pause < 20; pause++) departure.Tick(0);
                Require(departure.Position == origin && departure.Progress == 0, "A paused departure must preserve its exact pose.");
                report.pauseFreezes = true;
                float previousHeight = 0, previousForward = 0, previousProgress = 0, previousScale = 1;
                int steps = Mathf.CeilToInt(IslandFinishDeparture.Duration * fps) + 2;
                for (int frame = 0; frame < steps; frame++)
                {
                    departure.Tick(1f / fps);
                    Vector3 offset = departure.Position - origin;
                    float along = Vector3.Dot(offset, forward);
                    Require(offset.y + .0001f >= previousHeight && along + .0001f >= previousForward &&
                        departure.Progress >= previousProgress && departure.VisualScale <= previousScale + .0001f,
                        "The final leap must climb and fly away continuously, shrinking only as it recedes.");
                    Require(Mathf.Abs(Vector3.Dot(offset, heading * Vector3.right)) < .001f &&
                        Quaternion.Angle(departure.Rotation, heading) < .01f,
                        "The departure must retain the native finish heading without sideways drift or an axial spin.");
                    Require(!float.IsNaN(offset.y) && !float.IsInfinity(offset.y) && departure.VisualScale > 0,
                        "The flight must retain finite positions and nonzero visual scale until hidden.");
                    previousHeight = offset.y; previousForward = along;
                    previousProgress = departure.Progress; previousScale = departure.VisualScale;
                }
                Require(departure.Complete && departure.Progress == 1 && Mathf.Abs(previousHeight - 18) < .001f && Mathf.Abs(previousForward - 100) < .001f &&
                    departure.VisualScale <= .11f && IslandFinishDeparture.Duration < 1.9f,
                    "The hero must visibly leap high, recede and finish disappearing inside the existing native two-second result delay.");
                Vector3 end = departure.Position;
                departure.Tick(100);
                Require(departure.Position == end, "A completed flight must not keep moving after the hero disappears.");
                report.boundedFlight = true;
                departure.Reset();
                Require(!departure.Started && !departure.Complete && departure.StartCount == 0 && departure.VisualScale == 1,
                    "Reset must remove all previous finish state before retry or another song.");
                departure.TryBegin(true, 20, origin, heading);
                departure.Tick(4);
                Require(departure.Complete, "A single render hitch must not leave the hero visible after the native results delay.");
                report.hitchCompletes = true;
                report.frameRatesChecked++;
            }
            CheckRestoration(false); report.restoredOnRebind = true;
            CheckRestoration(true); report.restoredOnDeactivate = true;
            CheckTriggerFrameHitch(); report.triggerFrameTimeExcluded = true;
            CheckCurrentPresentationScale(); report.currentPresentationScalePreserved = true;
            var warm = new IslandFinishDeparture();
            warm.TryBegin(true, 20, Vector3.zero, Quaternion.identity); warm.Tick(.001f);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 10000; frame++) warm.Tick(.00001f);
            report.warmedTickAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Require(report.warmedTickAllocatedBytes == 0, "Departure presentation ticks must not allocate managed memory.");
            report.passed = true;
            string folder = "Library/RadioArchipelago/FinishDeparture";
            Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/checks.json", JsonUtility.ToJson(report, true));
            Debug.Log("ISLAND_FINISH_DEPARTURE PASS: forward-facing collection, native final trigger, one finite pausable ascent, late disappearance and complete binding restoration.");
        }

        private static void CheckTriggerFrameHitch()
        {
            var root = new GameObject("Isolated native finish frame boundary") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var stage = root.AddComponent<IslandStagePresentation>();
                var advance = typeof(IslandStagePresentation).GetMethod("AdvanceFinishDeparture", BindingFlags.NonPublic | BindingFlags.Instance);
                Require(advance != null, "The stage must exclude frame time preceding the native finish trigger.");
                Vector3 origin = new Vector3(4, 6, 8);
                Quaternion heading = Quaternion.Euler(0, -53, 0);
                object[] sample = { false, 19, origin, heading, 1.6f, 100 };
                advance.Invoke(stage, sample);
                Require(!stage.FinishDepartureStarted, "A hitch before the final native trigger must not start the departure.");
                sample[0] = true; sample[1] = 20; sample[5] = 101;
                advance.Invoke(stage, sample);
                Require(stage.FinishDepartureStarted && !stage.FinishDepartureComplete && stage.FinishDepartureProgress == 0 &&
                    stage.FinishDepartureVisualScale == 1 && stage.FinishDepartureCollectedCoinsAtStart == 20,
                    "The final-trigger frame's1.6s delta happened before takeoff; the new flight must remain at its complete starting pose.");
                advance.Invoke(stage, sample);
                Require(stage.FinishDepartureProgress == 0 && stage.FinishDepartureStartCount == 1,
                    "A second presentation call on the trigger frame must not consume that old delta or restart the flight.");
                sample[4] = 0f; sample[5] = 102;
                advance.Invoke(stage, sample);
                Require(stage.FinishDepartureProgress == 0, "A paused first departure frame must remain at takeoff.");
                sample[4] = .02f; sample[5] = 103;
                advance.Invoke(stage, sample);
                Require(stage.FinishDepartureProgress > 0 && stage.FinishDepartureProgress < .02f && !stage.FinishDepartureComplete,
                    "The first actual post-trigger frame must advance by its own small delta only.");
                sample[4] = 1.6f; sample[5] = 104;
                advance.Invoke(stage, sample);
                Require(stage.FinishDepartureComplete && stage.FinishDepartureStartCount == 1,
                    "A later hitch belongs to the already started flight and must still complete it inside the native result delay.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CheckCurrentPresentationScale()
        {
            var root = new GameObject("Isolated native versus bound courier scale") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var actor = new GameObject("Native actor").transform; actor.SetParent(root.transform, false);
                var anchor = new GameObject("Island anchor").transform; anchor.SetParent(root.transform, false);
                var courier = new GameObject("Courier visual").transform; courier.SetParent(actor, false);
                courier.localPosition = new Vector3(0, -.25f, 0);
                courier.localRotation = Quaternion.Euler(0, 17, 0);
                courier.localScale = Vector3.one * .47f;
                var stage = root.AddComponent<IslandStagePresentation>();
                const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
                object binding = typeof(IslandStagePresentation).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { courier });
                typeof(IslandStagePresentation).GetField("courierBinding", hidden).SetValue(stage, binding);
                typeof(IslandStagePresentation).GetField("actorAnchor", hidden).SetValue(stage, anchor);
                // Cold launch captures native restoration state first; the game
                // applies the currently selected island's larger size afterward.
                courier.SetParent(anchor, false); courier.localScale = Vector3.one * .64f;
                var advance = typeof(IslandStagePresentation).GetMethod("AdvanceFinishDeparture", hidden);
                var apply = typeof(IslandStagePresentation).GetMethod("ApplyFinishDepartureCourier", hidden);
                object[] sample = { true, 20, Vector3.zero, Quaternion.identity, 0f, 200 };
                advance.Invoke(stage, sample); apply.Invoke(stage, null);
                Require(stage.FinishDepartureProgress == 0 && courier.localScale == Vector3.one * .64f,
                    "Departure takeoff must preserve the actual bound.64 scale, not pop to the old native.47 restoration snapshot.");
                sample[4] = .9f; sample[5] = 201;
                advance.Invoke(stage, sample); apply.Invoke(stage, null);
                Vector3 expected = Vector3.one * (.64f * stage.FinishDepartureVisualScale);
                Require(stage.FinishDepartureVisualScale < 1 && (courier.localScale - expected).sqrMagnitude < .00000001f,
                    "Flight recession must scale relative to the current island presentation size.");
                sample[4] = 0f;
                advance.Invoke(stage, sample); apply.Invoke(stage, null);
                Require((courier.localScale - expected).sqrMagnitude < .00000001f,
                    "A second presentation sample must apply an absolute scale instead of compounding shrinkage.");
                sample[4] = 2f; sample[5] = 202;
                advance.Invoke(stage, sample); apply.Invoke(stage, null);
                Require(stage.FinishDepartureComplete && !courier.gameObject.activeSelf,
                    "The mixed-scale real presentation path must still hide its completed courier.");
                typeof(IslandStagePresentation).GetMethod("ResetFinishDeparture", hidden).Invoke(stage, null);
                Require(courier.gameObject.activeSelf && courier.parent == anchor && courier.localScale == Vector3.one * .64f &&
                    !stage.FinishDepartureStarted,
                    "Reset while bound must restore the island's current scale and visibility without restoring native parent state early.");
                stage.RestoreCourierParent(actor);
                Require(courier.parent == actor && courier.gameObject.activeSelf && courier.localScale == Vector3.one * .47f &&
                    courier.localPosition == new Vector3(0, -.25f, 0) &&
                    Quaternion.Angle(courier.localRotation, Quaternion.Euler(0, 17, 0)) < .01f,
                    "Leaving the stage must separately restore the original native.47 scale, parent and complete local pose.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CheckRestoration(bool deactivate)
        {
            var root = new GameObject("Isolated finish binding restoration") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var actor = new GameObject("Gameplay actor").transform; actor.SetParent(root.transform, false);
                var anchor = new GameObject("Presentation anchor").transform; anchor.SetParent(root.transform, false);
                var courier = new GameObject("Courier visual").transform; courier.SetParent(actor, false);
                courier.localPosition = new Vector3(0, -.25f, 0);
                courier.localRotation = Quaternion.Euler(0, 17, 0);
                courier.localScale = Vector3.one * .64f;
                var stage = root.AddComponent<IslandStagePresentation>();
                const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
                object binding = typeof(IslandStagePresentation).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { courier });
                typeof(IslandStagePresentation).GetField("courierBinding", hidden).SetValue(stage, binding);
                typeof(IslandStagePresentation).GetField("actorAnchor", hidden).SetValue(stage, anchor);
                courier.SetParent(anchor, false);
                var advance = typeof(IslandStagePresentation).GetMethod("AdvanceFinishDeparture", hidden);
                object[] sample = { true, 20, Vector3.zero, Quaternion.identity, 0f, 100 };
                advance.Invoke(stage, sample);
                sample[4] = 2f; sample[5] = 101; advance.Invoke(stage, sample);
                typeof(IslandStagePresentation).GetMethod("ApplyFinishDepartureCourier", hidden).Invoke(stage, null);
                Require(!courier.gameObject.activeSelf && stage.FinishDepartureCourierHidden,
                    "The completed real presentation path must hide the courier before restoration is tested.");
                if (deactivate) stage.Deactivate(); else stage.RestoreCourierParent(actor);
                Require(courier.gameObject.activeSelf && courier.parent == actor &&
                    courier.localPosition == new Vector3(0, -.25f, 0) && courier.localScale == Vector3.one * .64f &&
                    Quaternion.Angle(courier.localRotation, Quaternion.Euler(0, 17, 0)) < .01f,
                    "Rebind/deactivation must restore the hidden shrunken courier's original visibility, parent and complete local pose.");
                Require(!stage.FinishDepartureStarted && stage.FinishDepartureStartCount == 0 &&
                    stage.FinishDepartureOffset == Vector3.zero && !stage.FinishDepartureCourierHidden,
                    "Rebind/deactivation must clear its old departure state.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
