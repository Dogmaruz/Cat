using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RadioArchipelago.World;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DancingCats.Clone.Editor
{
    /// <summary>Actual Editor/DSP play review using only real Input System mouse events.</summary>
    [InitializeOnLoad]
    public static class IslandPilotPlayReview
    {
        private const string Key = "RadioArchipelago.IslandPilotPlayReview";
        private const string OtherReview = "RadioArchipelago.EditorReview";
        private const string GuideKey = "DancingCatsClone.v1.howToPlaySeen";
        private const string CourierKey = "RadioArchipelago.v1.courier";
        private const string LegacySongId = "AlwaysRememberMeowCat";
        private static string ReviewSongId => SessionState.GetString(Key + ".song", LegacySongId);
        private static bool IsCoralReview => SessionState.GetString(Key + ".scenePath", "") == RadioArchipelago.Editor.CoralOasisIslandJobs.ScenePath;
        private static bool IsMoonReview => SessionState.GetString(Key + ".scenePath", "") == RadioArchipelago.Editor.MoonResonanceIslandJobs.ScenePath;
        private static bool IsAmberReview => SessionState.GetString(Key + ".scenePath", "") == RadioArchipelago.Editor.AmberWindsIslandJobs.ScenePath;
        private static bool IsIndependentIslandReview => IsCoralReview || IsMoonReview || IsAmberReview;
        private static string ReviewFolder => IsAmberReview ? RadioArchipelago.Editor.AmberWindsIslandJobs.ReviewFolder + "/PlayReview" : IsMoonReview ? RadioArchipelago.Editor.MoonResonanceIslandJobs.ReviewFolder + "/PlayReview" : IsCoralReview
            ? RadioArchipelago.Editor.CoralOasisIslandJobs.ReviewFolder + "/PlayReview" : IslandPilotJobs.ReviewPath;
        private static readonly List<string> Errors = new List<string>(), Notes = new List<string>(), Images = new List<string>();
        private static readonly List<InputDevice> Disabled = new List<InputDevice>();
        private static readonly HashSet<OriginalTile> Consumed = new HashSet<OriginalTile>();
        private static readonly HashSet<OriginalTile> ScoredContacts = new HashSet<OriginalTile>();
        private static readonly HashSet<OriginalTile> ScoredHoldPoints = new HashSet<OriginalTile>();
        private static readonly HashSet<OriginalTile> FullRangePlatforms = new HashSet<OriginalTile>();
        private static readonly Dictionary<OriginalTile, Vector2> Moving = new Dictionary<OriginalTile, Vector2>();
        private static readonly Dictionary<OriginalTile, HoldLifetime> HoldLifetimes = new Dictionary<OriginalTile, HoldLifetime>();
        private static readonly List<HoldEvidence> HoldSamples = new List<HoldEvidence>(48);
        private static readonly List<HoldHandoffEvidence> HoldHandoffs = new List<HoldHandoffEvidence>(16);
        private static readonly List<TerrainFrameEvidence> TerrainFrames = new List<TerrainFrameEvidence>(16);
        private static readonly object[] DynamicUpdate = { InputUpdateType.Dynamic };
        private static readonly MethodInfo UpdateInput = typeof(InputSystem).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(InputUpdateType) }, null);
        private const BindingFlags NativePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo NativeDestination = typeof(OriginalPlayController).GetField("_destinationX", NativePrivate);
        private static readonly FieldInfo NativePointerOrigin = typeof(OriginalPlayController).GetField("_pointerOrigin", NativePrivate);
        private static readonly FieldInfo NativeDragOrigin = typeof(OriginalPlayController).GetField("_dragOrigin", NativePrivate);
        private static readonly FieldInfo NativeDragging = typeof(OriginalPlayController).GetField("_dragging", NativePrivate);
        private static readonly FieldInfo NativeLastLanding = typeof(OriginalPlayController).GetField("_lastLanding", NativePrivate);
        private static readonly FieldInfo NativeLocalFinishZ = typeof(OriginalPlayController).GetField("_localFinishZ", NativePrivate);
        private static readonly FieldInfo NativeLapOffset = typeof(OriginalPlayController).GetField("_lapOffset", NativePrivate);
        private static readonly FieldInfo StageProxyMap = typeof(IslandStagePresentation).GetField("proxyBySource", NativePrivate);
        private static readonly FieldInfo ProxyRenderer = typeof(IslandStagePresentation).GetNestedType("TileProxy", BindingFlags.NonPublic)?.GetField("renderer", NativePrivate);
        private static readonly TraceFrame[] TraceFrames = new TraceFrame[240];
        private static readonly TraceContact[] TraceContacts = new TraceContact[96];
        private static int traceFrameCount, traceFrameWrite, traceContactCount, traceContactWrite;
        private static float traceScreenWidth;
        private static OriginalPlayController game;
        private static IslandStagePresentation stage;
        private static UniversalAdditionalCameraData stageCameraData;
        private static Renderer[] courierRenderers;
        private static RadioArchipelago.CourierMotion finishMotion;
        private static Transform finishVisual;
        private static bool finishHeadingReady;
        private static float previousFinishHeading;
        private static Vector3 departureCameraStart;
        private static Quaternion departureCameraStartRotation;
        private static float previousDepartureProgress;
        private static bool departureSeen, departureEarlyCaptured, departureMidCaptured, departureLateCaptured, departureHiddenCaptured;
        private static Renderer[] natureRenderers;
        private static Mesh geometryScratch;
        private static readonly List<Vector3> GeometryVertices = new List<Vector3>(8192);
        private static readonly List<BoundsEvidence> BoundsSamples = new List<BoundsEvidence>(32);
        private static readonly FieldInfo CameraTarget = typeof(IslandStagePresentation).GetField("framedCameraTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CameraHeading = typeof(IslandStagePresentation).GetField("desiredCameraRotation", BindingFlags.Instance | BindingFlags.NonPublic);
        private static Vector3 previousCameraPosition;
        private static Quaternion previousCameraRotation;
        private static int previousCameraFrame = -1;
        private static Mouse pointer, previousMouse;
        private static InputSettings previousSettings, reviewSettings;
        private static Report report;
        private static int step, pressedFrame, lastFrame, captureIndex, previousLap, armPhase, armFrame;
        private static float dragOrigin, previousYaw;
        private static float fixedCameraFieldOfView;
        private static int pauseScore, pauseSamples;
        private static Vector3 pauseActor, pauseWorld, previousWorld, previousChart;
        private static Quaternion pauseCamera;
        private static double deadline, readyAt, startedDsp, pausedDsp, pauseClock, lastDsp, lastClock, lastWrite;
        private static bool started, bigLaunch, holdCaptured, glideCaptured, bigCaptured, movingCaptured, turnCaptured;
        private static bool holdRideCaptured, markerCaptured, radioMidCaptured, radioLateCaptured;
        private static bool faunaCaptured, cloudsCaptured, cloudBankCaptured;
        private static bool groundJumpCaptured, groundLandingCaptured, groundCoinsCaptured;
        private static bool holdBeforeCaptured, holdBoundaryCaptured, holdAfterCaptured;
        private static int terrainCaptureIndex;
        private static readonly float[] HoldProgressSamples = { .3f, .7f, .95f };
        private static readonly float[] TerrainCaptureClocks = { 21, 21.5f, 22, 22.5f, 23, 23.5f, 24, 62.5f, 63, 63.5f, 64, 64.5f, 65, 65.5f, 66, 66.5f };
        private static readonly float[] CoralTerrainCaptureFractions = { .08f, .22f, .38f, .52f, .68f, .82f, .94f };
        private static readonly float[] CaptureTimes = { 2, 25, 35, 55, 73, 90, 130, 150, 154 };
        private static readonly float[] LivingCaptureTimes = { 3, 18, 35, 55, 70 };

        [Serializable] private sealed class Snapshot
        {
            public bool hadProfile, hadGuide, hadCourier;
            public string profile;
            public int guide, courier;
        }

        [Serializable] private struct TraceFrame
        {
            public int frame, reviewStep, score, streak, lastLandingSource, supportSource;
            public ClonePhase phase;
            public double chartClock, dspTime;
            public Vector3 actor, jumpStart;
            public float jumpDistance, jumpPercent;
            public int nextTargetSource, nextTargetId, nextTargetUpdateType;
            public float nextTargetCurrentX, nextTargetContactX, nextTargetZ, nextTargetAhead;
            public float worldLateral, nextTargetCurrentWorldLateral, nextTargetContactWorldLateral;
            public Vector2 pointer;
            public float qaDragOrigin, qaMappedTargetX, nativeDestinationX, nativePointerOrigin, nativeDragOrigin;
            public bool pointerHeld, nativeDragging, presenterBlocksInput;
        }

        [Serializable] private struct TraceContact
        {
            public int frame, reviewStep, scoreBefore, sourceIndex, id, updateType;
            public double chartClock, dspTime;
            public float currentX;
            public Vector3 actor, contact, jumpStart;
        }

        [Serializable] private sealed class TraceReport
        {
            public string runId;
            public float screenWidth;
            public bool nativeBindingsComplete;
            public string scope = "Read-only QA telemetry: latest 240 actual PlayerLoop frames and latest 96 real score-contact events in chronological order. nextTarget is the current Bot candidate; qaMappedTargetX reconstructs the last pointer command. No input/controller state is changed by this trace.";
            public TraceFrame[] frames;
            public TraceContact[] sourceContacts;
        }

        [Serializable] private sealed class Report
        {
            public string state = "running", runId, song = ReviewSongId, details;
            public string songAudioResource, reviewMapId;
            public int recordingSamples, recordingFrequency;
            public string traceFile;
            public string scene;
            public bool coralOasisReview, expectedRouteSeam;
            public int requiredTerrainCaptures;
            public bool boundedReview, fixedCamera, unifiedHoldTiles, livingNatureEnabled, livingFaunaSeen, cloudsSeen, cloudBankSeen;
            public float boundedSeconds, higherJumpBoost, maxCameraRigDistanceError, maxCameraRigPoseError, maxCameraRigRotationError, fixedFovDelta;
            public int cameraRigSamples, maxVisibleHoldBodies, maxVisibleFaunaBodies, maxVisibleClouds;
            public int maxVisibleRadioHoldTracers, advancedHoldBandFrames, maxNativeFishLineViews;
            public float maxHoldBandOutsideSupport, maxHoldBandOutsideRoundedSupport, maxHoldBandBeyondProgress, maxHoldBandPrefixEndpointError;
            public float maxHeldBodyScaleError, maxHoldBodyRangeError, maxHoldMarkerEndpointError, maxHoldProgressError, maxHoldLinePoseError;
            public int fullHoldSamples30, fullHoldSamples70, fullHoldSamples95, holdReleaseChecks, holdLifetimeChecks;
            public HoldEvidence[] fullHoldSamples;
            public bool staticFullHold95Verified;
            public string staticFullHold95CheckedUtc;
            public string staticFullHold95Scope = "Fresh Edit Mode synthetic geometry fixture checks full ceramic vertices/bounds/pivot and exact attached cursor at 0/.3/.7/.95/1; this is separate from native held-state progress.";
            public int nativeEarlyHandoffs, nativeEndBefore95Checks;
            public float maxNativeHeldProgress, minNativeHeldProgress = 1;
            public HoldHandoffEvidence[] nativeHoldHandoffs;
            public TerrainFrameEvidence[] terrainFrames;
            public string terrainClearanceReport, terrainClearanceCheckedUtc, terrainClearanceDefinitionHash;
            public bool terrainTriangleReportAvailable, terrainTriangleReportFresh, terrainTriangleClearancePassed;
            public int terrainCorridorSamples, terrainTileSamples, terrainCorridorViolations, terrainTileViolations;
            public float minTerrainCorridorClearance, minTerrainTileClearance;
            public string terrainEvidenceScope = "Terrain clearance is read from the independent imported-triangle diagnostic. Native collision success and screenshots do not establish that tiles are above terrain.";
            public float lateralScale, cameraLateralFollow, fullTravelWidth, fullRangePlatformThreshold;
            public float minWorldLateral = float.MaxValue, maxWorldLateral = float.MinValue;
            public float minPlatformWorldLateral = float.MaxValue, maxPlatformWorldLateral = float.MinValue;
            public int fullRangePlatforms, fullRangeLeftPlatforms, fullRangeRightPlatforms, platformPoseSamples, movingAlignmentSamples, holdAlignmentSamples;
            public float maxPlatformPoseError, maxMovingAlignmentError, maxHoldAlignmentError, maxCameraRigAnchorError;
            public bool reliefStage, reliefBridgeSeen, reliefStairsSeen, reliefHillSeen, reliefDescentSeen, reliefSkySeen;
            public float minRouteHeight, maxRouteHeight, maxRouteGrade, maxFlightElevationCorrection;
            public int ascendingFrames, descendingFrames;
            public bool pass, completedRecording, won, pauseVerified, profileRestored, inputRestored;
            public bool postProcessingChecked, postProcessingSeen, courierScaleChecked;
            public float courierScale;
            public float maxHoldBandOpacity;
            public bool touchInputVerified;
            public int maxLandingOverlaps, maxNoteParticles;
            public int mappedCollectibleMarkers, maxVisibleCollectibleMarkers;
            public int suppressedHoldFlashes;
            public float noteSizeMultiplier;
            public float minLandingRadius = float.MaxValue;
            public bool holdSeen, glideSeen, bigJumpSeen, movingTileSeen, seamSeen, particlesSeen, coinsSeen;
            public int observedFrames, worldPoseSamples, seamCrossings, movingTilesConsumed, score;
            public int maxParticles, MappedSystems;
            public int maxExpiredHoldVisuals, visibleExitDiscs, readableEffectReplacements, projectedEffectVertices;
            public float maxHoldTriangleChartEdge;
            public int peakVisibleProjectedVertices, framesOver50Ms, coinsBound, maxVisibleCoins, coinsCollected, unmappedCoins;
            public bool signalSparkCurrencySelected, signalSparksSeen, signalSparkLineCaptured;
            public int boundSignalSparks, maxVisibleSignalSparks, maxVisibleConsumedSignalSparks, signalSparkVisibleFrames, signalSparkDisappearanceSamples;
            public float meanProjectionCpuMs, peakProjectionCpuMs, meanObservedFrameMs, maxObservedFrameMs;
            public float meanParticleCpuMs, meanTrailCpuMs, meanMeshCpuMs, peakMeshCpuMs;
            public int geometryRebuilds, geometryReuses, geometryParityChecks, geometryParityFailures, poolGrowth;
            public long projectedVertices, reusedVertices;
            public int requiredHolds, requiredLongHolds, requiredGlides, requiredCollectibleMarkers, requiredBigJumps, requiredMovingTiles;
            public int requiredScoreContacts, scoredScoreContacts, requiredHoldPoints, scoredHoldPoints;
            public int preservedMusicalEvents;
            public string[] missingScoreContacts;
            public bool uniformRoundTiles;
            public float normalRoundRadius;
            public float songSeconds, routeLength, roadWidth, distanceScale;
            public float maxWorldPoseError, cameraYawTravel, maxSeamWorldStep, maxJumpY, maxClockDspDrift;
            public int actorOffscreenFrames, nextLandingOffscreenFrames;
            public int actorBoundsOffscreenFrames, nextLandingRimOffscreenFrames;
            public int coarseActorBoundsFrames, orientedActorBoundsFrames, coarseBoundsFalsePositives, geometryCheckFrames, actualGeometryClips;
            public BoundsEvidence[] boundsEvidence;
            public bool groundFinishEnabled, groundFinishConfigured, groundFinalJumpSeen, groundLandingSeen, groundCoinRunSeen;
            public float groundFinishTakeoffZ, groundFinishLandingZ, groundHeight, maxGroundActorError;
            public float minGroundFootClearance = float.MaxValue, maxGroundFootClearance = float.MinValue;
            public float minGroundCoinClearance = float.MaxValue, maxGroundCoinClearance = float.MinValue;
            public int groundFootSamples, maxGroundedFinishCoins, maxVisibleFinishPlatforms, coinsAtGroundLanding, coinsCollectedOnGround;
            public double groundLandingClock;
            public float finishHoverHeight, finishRotationDegrees;
            public int finishHoverRotationSamples;
            public int forwardFinishSamples, departureSamples, departureHiddenFrames, departureCollectedCoinsAtStart;
            public bool departureStarted, departureComplete, departureAfterNativeWin, departureCourierHidden;
            public float maxFinishCollectionYawError, maxDepartureCameraPositionError, maxDepartureCameraRotationError;
            public float maxDepartureRise, maxDepartureForward, minimumDepartureScale = 1;
            public int gladePolicyChecks, gladePolicyViolations, preFinishActiveViolations, hiddenEarlyGladeFrames, activeFinalGladeFrames;
            public string gladeOverlapScope = "Generated-glade samples describe potential geometry intersections. visibleGladeOverlap additionally requires an active glade and a drawn tile; inactive generated geometry is not a rendered obstruction.";
            public int courierIndex, cameraSmoothingSamples;
            public float maxCameraSmoothingError, maxCameraRotationSmoothingError;
            public float viewportAspect, minActorViewportX = 1, maxActorViewportX, minLandingViewportX = 1, maxLandingViewportX;
            public double chartClock, realDspSeconds, pauseDspSeconds;
            public string[] errors, notes, screenshots;
            public string scope = "Actual Editor GameView and full native recording; InputSystem pointer only. No actor warps, invincibility, audio seeking or shortened chart. Frame/CPU timings describe this instrumented Editor run, including framebuffer captures.";
        }

        [Serializable] private sealed class BoundsEvidence
        {
            public int frame;
            public double chartClock;
            public Vector3 nativeActor, worldActor, cameraPosition, coarseViewport, geometryViewport;
            public Quaternion cameraRotation;
            public Bounds worldBounds;
            public string coarseAxis, geometryAxis, renderer;
            public bool orientedOutside, geometryOutside;
        }

        private sealed class HoldLifetime
        {
            public float start, end, releaseElapsed, lastHeldProgress, maxHeldProgress, lastHeldChartZ;
            public int samples, lastHeldFrame, lastGeometryFrame, heldFrames, geometryFrames;
            public bool released, complete, earlyHandoffVerified;
            public double releasedClock, lastHeldClock;
        }

        [Serializable] private sealed class HoldHandoffEvidence
        {
            public int sourceIndex, lastHeldFrame, firstHandoffFrame, heldFrames, fullGeometryFrames;
            public float lastHeldProgress, maxHeldProgress, lastHeldChartZ, handoffChartZ, nativeArcStartZ, originalEndZ;
            public double lastHeldClock, handoffClock;
            public bool nativeEndHandoff, fullGeometryThroughLastHeld, before95;
        }

        [Serializable] private sealed class HoldEvidence
        {
            public int sourceIndex, bandViews;
            public double chartClock;
            public float requestedProgress, observedProgress, start, end, markerChartZ, bandMinChartZ, bandMaxChartZ, planarScale;
            public Vector3 bodyStart, bodyEnd, lineStart, lineEnd, markerCenter;
        }

        [Serializable] private sealed class TerrainFrameEvidence
        {
            public string screenshot;
            public double chartClock;
            public float audioTime, requestedChartClock, routeDistance;
            public float nativeProgress, localFinishZ, forwardSpeed, lapOffset, progressDerivedChartClock, progressClockError;
            public Vector3 nativeActor, worldActor;
            public int supportSourceIndex, nextSourceIndex;
            public bool proxyReflectionAvailable, finishGladeConfigured, finishGladeActive, finishGladeExpectedActive;
            public Bounds finishGladeBounds;
            public int finishGladeRenderers, finishGladeTriangles;
            public float finishGladeGroundHeight;
            public TerrainProxyEvidence[] nearbySources;
        }

        [Serializable] private sealed class TerrainProxyEvidence
        {
            public int sourceIndex, id, meshVertices;
            public string renderer, mesh;
            public bool consumed, sourceIsSupport, hasProxy, proxyVisible, rendererEnabled, rendererActive, gladeSample, potentialGladeOverlap, visibleGladeOverlap;
            public float surfaceOffset, planarScale, meshLocalMinY, meshWorldMinY, meshWorldMaxY;
            public float gladeSurfaceY, gladeAboveTileBottom, gladeAboveTileTop;
            public Vector3 nativeCurrent, nativeContact, proxyPosition, expectedPosition, viewport;
            public Bounds rendererBounds;
        }

        [Serializable] private sealed class TriangleClearanceReport
        {
            public bool passed = false;
            public string checkedUtc = "", definitionDependencyHash = "", prefabDependencyHash = "";
            public bool anchorChecksPassed = false, floorRegressionPassed = false, structureRegressionPassed = false;
            public int structureSamples = 0, structureViolations = 0, actualFinishFloorSamples = 0;
            public int corridorSamples = 0, tileSamples = 0, corridorViolations = 0, tileViolations = 0;
            public float minCorridorClearance = 0, minTileClearance = 0;
        }

        static IslandPilotPlayReview()
        {
            EditorApplication.playModeStateChanged += OnMode;
            if (SessionState.GetBool(Key, false)) Application.logMessageReceived += OnLog;
        }

        [MenuItem("Radio Archipelago/Island pilot/Review full song")]
        public static void Begin() => BeginCourier(0);

        [MenuItem("Radio Archipelago/Island pilot/Review full song with Lumen")]
        public static void BeginLumen() => BeginCourier(1);

        [MenuItem("Radio Archipelago/Relief experiment/Review full song")]
        public static void BeginRelief() => BeginCourier(1, RadioArchipelago.Editor.IslandReliefJobs.ScenePath);

        [MenuItem("Radio Archipelago/Floating archipelago/Review living stage (78 seconds)")]
        public static void BeginLivingPass() => BeginCourier(1, RadioArchipelago.Editor.IslandAbyssJobs.ScenePath, 78);

        public static void BeginWideIsland() => BeginCourier(1, RadioArchipelago.Editor.IslandAbyssJobs.ScenePath);

        public static void BeginNoriAbyssBaseline() => BeginCourier(2, RadioArchipelago.Editor.IslandAbyssJobs.ScenePath, 30);

        public static void BeginNoriAbyss() => BeginCourier(2, RadioArchipelago.Editor.IslandAbyssJobs.ScenePath);

        [MenuItem("Radio Archipelago/Coral oasis/Review full song")]
        public static void BeginCoralOasis() => BeginCourier(0, RadioArchipelago.Editor.CoralOasisIslandJobs.ScenePath);

        [MenuItem("Radio Archipelago/Moon Resonance/Review full song with Nori")]
        public static void BeginMoonResonance() => BeginCourier(2, RadioArchipelago.Editor.MoonResonanceIslandJobs.ScenePath);

        [MenuItem("Radio Archipelago/Amber Winds/Review full song")]
        public static void BeginAmberWinds() => BeginCourier(0, RadioArchipelago.Editor.AmberWindsIslandJobs.ScenePath);

        private static void BeginCourier(int courierIndex, string scenePath = IslandPilotJobs.ScenePath, float boundedSeconds = 0)
        {
            RequireCleanEditor();
            if (!File.Exists(scenePath)) throw new FileNotFoundException("Create the requested saved island scene first.", scenePath);
            if (SessionState.GetBool(Key, false) || SessionState.GetBool(OtherReview, false))
                throw new InvalidOperationException("Another play review is already running.");
            if (SessionState.GetBool(OtherReview + ".viewCaptured", false))
                throw new InvalidOperationException("Restore the previous Radio Editor review's GameView before beginning this review.");
            // Reset before checking so a prior run can never certify this one.
            SessionState.SetBool(Key + ".staticFullHold95Verified", false);
            SessionState.SetString(Key + ".staticFullHold95CheckedUtc", "");
            if (scenePath == RadioArchipelago.Editor.IslandAbyssJobs.ScenePath || scenePath == RadioArchipelago.Editor.CoralOasisIslandJobs.ScenePath ||
                scenePath == RadioArchipelago.Editor.MoonResonanceIslandJobs.ScenePath || scenePath == RadioArchipelago.Editor.AmberWindsIslandJobs.ScenePath)
            {
                RadioArchipelago.Editor.IslandProjectionChecks.RunChecks();
                SessionState.SetBool(Key + ".staticFullHold95Verified", true);
                SessionState.SetString(Key + ".staticFullHold95CheckedUtc", DateTime.UtcNow.ToString("O"));
            }
            var snapshot = new Snapshot
            {
                hadProfile = PlayerPrefs.HasKey(OriginalProgression.SaveKey), profile = PlayerPrefs.GetString(OriginalProgression.SaveKey, ""),
                hadGuide = PlayerPrefs.HasKey(GuideKey), guide = PlayerPrefs.GetInt(GuideKey, 0),
                hadCourier = PlayerPrefs.HasKey(CourierKey), courier = PlayerPrefs.GetInt(CourierKey, 0)
            };
            SessionState.SetString(Key + ".snapshot", JsonUtility.ToJson(snapshot));
            SessionState.SetString(Key + ".runId", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            SessionState.SetString(Key + ".scenePath", scenePath);
            SessionState.SetString(Key + ".song", LegacySongId);
            SessionState.SetFloat(Key + ".boundedSeconds", Mathf.Max(0, boundedSeconds));
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".finishing", false);
            SessionState.SetString(Key + ".earlyErrors", "");
            report = new Report { runId = SessionState.GetString(Key + ".runId", "") };
            traceFrameCount = traceFrameWrite = traceContactCount = traceContactWrite = 0; traceScreenWidth = 0;
            ConfigureScope();
            Errors.Clear(); Notes.Clear(); Images.Clear();
            Persist();
            try
            {
                PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
                PlayerPrefs.SetInt(GuideKey, 1); PlayerPrefs.SetInt(CourierKey, courierIndex); PlayerPrefs.Save();
                // Match the narrow phone recording that exposed the turn-framing bug.
                GameViewHelper("SelectGameViewSize", new object[] { 590, 1280 });
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var sceneStage = UnityEngine.Object.FindFirstObjectByType<IslandStagePresentation>();
                string songId = sceneStage != null && sceneStage.Definition != null && !string.IsNullOrEmpty(sceneStage.Definition.SongId)
                    ? sceneStage.Definition.SongId : LegacySongId;
                SessionState.SetString(Key + ".song", songId);
                report.song = songId;
                Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
                EditorApplication.isPlaying = true;
                Debug.Log("ISLAND_PILOT_PLAY_REVIEW_STARTED: real song playback, " +
                    (boundedSeconds > 0 ? "focused " + boundedSeconds + " second review" : "full native recording") + ". Progress: " + ResultPath);
            }
            catch (Exception error) { Finish(false, "Review startup failed: " + error); throw; }
        }

        public static void OpenForUser()
        {
            RequireCleanEditor();
            if (SessionState.GetBool(Key, false)) throw new InvalidOperationException("The play review is still running.");
            string scenePath = RadioArchipelago.Editor.IslandAbyssJobs.ScenePath;
            if (!File.Exists(scenePath)) throw new FileNotFoundException("Create the current archipelago scene first.", scenePath);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true)).Show();
        }

        private static string ResultPath => Path.Combine(ReviewFolder, "play-review.json");

        private static void ConfigureScope()
        {
            report.coralOasisReview = IsCoralReview;
            if (IsIndependentIslandReview) report.minRouteHeight = float.MaxValue;
            report.requiredTerrainCaptures = IsIndependentIslandReview ? CoralTerrainCaptureFractions.Length : TerrainCaptureClocks.Length;
            report.staticFullHold95Verified = SessionState.GetBool(Key + ".staticFullHold95Verified", false);
            report.staticFullHold95CheckedUtc = SessionState.GetString(Key + ".staticFullHold95CheckedUtc", "");
            report.boundedSeconds = SessionState.GetFloat(Key + ".boundedSeconds", 0);
            report.boundedReview = report.boundedSeconds > 0;
            if (report.boundedReview)
                report.scope = "Focused living-stage review of the first " + report.boundedSeconds +
                    " audio seconds in the actual Editor GameView. Full source recording loaded and played at 1x; normal InputSystem pointer only, no actor warps, invincibility or audio seeking. Does not certify full-song completion, finish coins or settlement. Captures and instrumented Editor timings are recorded separately.";
        }

        private static void RequireCleanEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Start in Edit Mode after script compilation and imports finish.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException("Save open scenes first; the review never saves or discards user scene edits.");
            }
        }

        private static void OnMode(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                report = new Report { runId = SessionState.GetString(Key + ".runId", ""), scene = SessionState.GetString(Key + ".scenePath", IslandPilotJobs.ScenePath) };
                ConfigureScope();
                Errors.Clear(); Notes.Clear(); Images.Clear(); Consumed.Clear(); Moving.Clear(); FullRangePlatforms.Clear();
                ScoredContacts.Clear(); ScoredHoldPoints.Clear();
                HoldLifetimes.Clear(); HoldSamples.Clear(); HoldHandoffs.Clear(); TerrainFrames.Clear(); terrainCaptureIndex = 0;
                BoundsSamples.Clear();
                traceFrameCount = traceFrameWrite = traceContactCount = traceContactWrite = 0; traceScreenWidth = 0;
                OriginalPlayController.EditorTimingObserved -= OnScoreContact;
                OriginalPlayController.EditorTimingObserved += OnScoreContact;
                string early = SessionState.GetString(Key + ".earlyErrors", "");
                if (!string.IsNullOrEmpty(early)) Errors.Add(early);
                previousSettings = InputSystem.settings; reviewSettings = UnityEngine.Object.Instantiate(previousSettings);
                reviewSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                reviewSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = reviewSettings;
                previousMouse = Mouse.current; pointer = InputSystem.AddDevice<Mouse>("Island pilot QA pointer"); pointer.MakeCurrent();
                foreach (var device in InputSystem.devices.ToArray())
                    if (device.native && device.enabled && (device is Mouse || device is Keyboard))
                    { Disabled.Add(device); InputSystem.DisableDevice(device); }
                game = null; stage = null; stageCameraData = null; started = bigLaunch = false;
                natureRenderers = null;
                previousCameraFrame = -1;
                holdCaptured = glideCaptured = bigCaptured = movingCaptured = turnCaptured = false;
                holdRideCaptured = markerCaptured = radioMidCaptured = radioLateCaptured = false;
                faunaCaptured = cloudsCaptured = cloudBankCaptured = false;
                groundJumpCaptured = groundLandingCaptured = groundCoinsCaptured = false;
                departureSeen = departureEarlyCaptured = departureMidCaptured = departureLateCaptured = departureHiddenCaptured = false;
                previousDepartureProgress = 0;
                holdBeforeCaptured = holdBoundaryCaptured = holdAfterCaptured = false;
                step = captureIndex = 0; lastFrame = -1; readyAt = EditorApplication.timeSinceStartup + 1.5;
                armPhase = 0;
                deadline = EditorApplication.timeSinceStartup + 40; lastDsp = lastClock = lastWrite = 0;
                Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
                var loop = PlayerLoop.GetCurrentPlayerLoop();
                loop.subSystemList = (loop.subSystemList ?? Array.Empty<PlayerLoopSystem>()).Where(s => s.type != typeof(IslandPilotPlayReview))
                    .Concat(new[] { new PlayerLoopSystem { type = typeof(IslandPilotPlayReview), updateDelegate = Tick } }).ToArray();
                PlayerLoop.SetPlayerLoop(loop);
            }
            if (mode == PlayModeStateChange.ExitingPlayMode)
            {
                if (!SessionState.GetBool(Key + ".finishing", false))
                {
                    if (report == null) report = new Report { runId = SessionState.GetString(Key + ".runId", "") };
                    Errors.Add("Play Mode stopped before the review completed."); report.state = "failed"; report.pass = false;
                    Persist(); SessionState.SetBool(Key + ".finishing", true);
                }
                try { CleanupInput(); }
                catch (Exception error) { Errors.Add("Input cleanup: " + error); report.pass = report.inputRestored = false; }
                Persist();
            }
            if (mode == PlayModeStateChange.EnteredEditMode) Restore();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || SessionState.GetBool(Key + ".finishing", false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                if (Errors.Count > 0) { Finish(false, "Runtime errors were observed."); return; }
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Review phase " + step + ", game phase " + (game == null ? "missing" : game.Phase.ToString()));
                if (EditorApplication.timeSinceStartup < readyAt) return;
                if (game == null) game = UnityEngine.Object.FindAnyObjectByType<OriginalPlayController>();
                if (stage == null) stage = UnityEngine.Object.FindAnyObjectByType<IslandStagePresentation>();
                if (game == null || stage == null || game.Presenter == null || game.Presenter.UICanvas == null) return;
                if (started) RecordTrace();
                if (started && game.Phase == ClonePhase.Dead) { Capture("death"); Finish(false, "The real pointer bot died; no revive or invincibility was used."); return; }
                if (started && game.Phase == ClonePhase.Results) { Complete(); return; }
                if (started && game.Phase == ClonePhase.Playing)
                {
                    Observe();
                    if (report.boundedReview && step >= 11 && game.GameTime - game.Map.delayTime >= report.boundedSeconds)
                    { Complete(); return; }
                }
                switch (step)
                {
                    case 0:
                        if (SessionState.GetString(Key + ".scenePath", "") == RadioArchipelago.Editor.IslandAbyssJobs.ScenePath || IsIndependentIslandReview)
                        {
                            Require(game.Selected != null && stage.Definition != null &&
                                game.Selected.id == ReviewSongId && stage.Definition.SongId == ReviewSongId,
                                "The current island did not automatically select its configured song before review input.");
                            Notes.Add("Configured island song selected automatically before review input: " + game.Selected.id);
                        }
                        var song = game.Songs.FirstOrDefault(s => s.id == ReviewSongId);
                        Require(song != null, "The full target song is missing.");
                        if (!game.Progression.IsSongUnlocked(ReviewSongId)) { game.Progression.State.unlockedSongs.Add(ReviewSongId); game.Progression.Save(); }
                        Require(game.SelectSong(song.catalogId), "Target song could not be selected.");
                        IslandTouchInputProbe.Verify(game); report.touchInputVerified = true;
                        VerifyCourierScale();
                        game.Endless = false; Capture("menu"); pressedFrame = Time.frameCount; step = 1; break;
                    case 1:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Click("PlayButton"); step = 2; break;
                    case 2:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release(); step = 3; break;
                    case 3:
                        if (game.Phase != ClonePhase.Playing) return;
                        Require(stage.IsActiveFor(ReviewSongId) && stage.Route != null && stage.Camera != null, "Pilot island presentation is inactive.");
                        Require(game.Music != null && game.Music.clip != null, "Actual song AudioSource is missing.");
                        report.songSeconds = game.Music.clip.length;
                        report.songAudioResource = game.Selected.audioResource; report.reviewMapId = game.Map.id;
                        report.recordingSamples = game.Music.clip.samples; report.recordingFrequency = game.Music.clip.frequency;
                        Require(game.Selected.id == ReviewSongId && game.Map.id == ReviewSongId && game.Map.audio != null &&
                            game.Music.clip == game.Selected.LoadAudio() && report.songSeconds > 0 && report.recordingFrequency > 0 &&
                            Math.Abs(report.songSeconds - game.Selected.audioLengthSeconds) < .01 &&
                            Math.Abs((double)report.recordingSamples / report.recordingFrequency - game.Selected.audioLengthSeconds) < .01 &&
                            Math.Abs(game.Map.audio.declaredLengthSeconds - game.Selected.audioLengthSeconds) < .01,
                            "The reviewed chart and complete decoded source clip must match the selected song and its full duration.");
                        Require(Mathf.Abs(game.PlaybackRate - 1) < .0001f && Mathf.Abs(game.Music.pitch - 1) < .0001f, "Playback must remain at its native rate.");
                        report.routeLength = stage.Route.Length; report.roadWidth = stage.Definition.RoadWidth; report.distanceScale = stage.Definition.DistanceScale;
                        float lastRouteZ = stage.GroundFinishPath != null ? stage.GroundFinishPath.TakeoffChartZ :
                            (float)(game.Map.delayTime + report.songSeconds) * game.Map.forwardSpeed;
                        report.expectedRouteSeam = Lap(lastRouteZ) > Lap(0);
                        report.reliefStage = stage.Elevation != null;
                        report.fixedCamera = stage.CameraUsesFixedRig;
                        report.unifiedHoldTiles = stage.Definition.UnifiedHoldTiles;
                        report.livingNatureEnabled = stage.Definition.LivingNatureEnabled;
                        report.groundFinishEnabled = stage.Definition.EnableGroundFinish;
                        report.groundFinishConfigured = stage.GroundFinishPath != null;
                        report.signalSparkCurrencySelected = stage.Definition.FinishCurrencyPrefab != null;
                        report.lateralScale = stage.Definition.LateralScale;
                        report.uniformRoundTiles = stage.Definition.UniformRoundTiles;
                        report.normalRoundRadius = stage.MinimumRoundSupportRadius;
                        if (IsMoonReview || IsAmberReview)
                            Require(report.uniformRoundTiles && Mathf.Abs(report.normalRoundRadius - .65f) < .001f,
                                "This independent island requires full-size .65m ordinary landing supports throughout native playback.");
                        report.cameraLateralFollow = stage.Definition.CameraLateralFollow;
                        report.fullTravelWidth = 2 * NativeRhythmRules.HorizontalLimit * report.lateralScale;
                        report.fullRangePlatformThreshold = Mathf.Max(0, stage.Definition.LateralLimit * report.lateralScale - .01f);
                        ReadTriangleClearanceReport();
                        fixedCameraFieldOfView = stage.Camera.fieldOfView;
                        var nature = stage.LivingNature;
                        natureRenderers = nature != null ? nature.GetComponentsInChildren<Renderer>(true).Where(IsNatureRenderer).ToArray() : Array.Empty<Renderer>();
                        foreach (var tile in game.Tiles)
                        {
                            if (tile.element.kind != "Start" && (tile.element.id >= 0 && tile.element.id <= 6 || tile.element.id == 9)) report.requiredScoreContacts++;
                            if (tile.element.id == 9) report.requiredHoldPoints++;
                            if (tile.consumed) Consumed.Add(tile);
                            if (tile.element.id == 2 || tile.element.id == 3)
                            {
                                report.requiredHolds++;
                                float holdEnd = tile.segment != null ? tile.segment.endZ : tile.initialZ + tile.element.mfendPos;
                                if (holdEnd - tile.initialZ >= 5) report.requiredLongHolds++;
                            }
                            if (tile.element.id == 4) report.requiredGlides++;
                            if (tile.element.id == 5) report.requiredCollectibleMarkers++;
                            if (tile.element.id == 1) report.requiredBigJumps++;
                            if (tile.element.updateType == 3) report.requiredMovingTiles++;
                        }
                        var playablePhrases = PcmPlayablePhraseGrouping.ReadReport(game.Map);
                        if (playablePhrases != null)
                        {
                            report.preservedMusicalEvents = playablePhrases.sourceScoringEvents;
                            Require(report.requiredScoreContacts == report.preservedMusicalEvents,
                                "The native track does not expose every preserved measured scoring event exactly once.");
                        }
                        started = true; startedDsp = AudioSettings.dspTime;
                        previousChart = game.Actor.position; previousWorld = stage.WorldActorPosition;
                        previousLap = Lap(previousChart.z); previousYaw = stage.Camera.transform.eulerAngles.y;
                        deadline = EditorApplication.timeSinceStartup + (report.boundedReview ? report.boundedSeconds + 25 : report.songSeconds + 45);
                        ArmPointer(); step = 4; break;
                    case 4:
                        if (!CompleteArm()) return;
                        step = 5; break;
                    case 5:
                        Bot();
                        if (game.GameTime < game.Map.delayTime + 8 || game.WinSequence) return;
                        Release(); pressedFrame = Time.frameCount; step = 6; break;
                    case 6:
                        if (Time.frameCount <= pressedFrame + 1) return;
                        Click("PauseButton"); step = 7; break;
                    case 7:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release(); if (game.Phase != ClonePhase.Paused) return;
                        pauseClock = game.GameTime; pausedDsp = AudioSettings.dspTime; pauseScore = game.Score; pauseSamples = game.Music.timeSamples;
                        pauseActor = game.Actor.position; pauseWorld = stage.WorldActorPosition; pauseCamera = stage.Camera.transform.rotation;
                        Capture("pause"); step = 8; break;
                    case 8:
                        if (AudioSettings.dspTime - pausedDsp < 1) return;
                        Require(game.Phase == ClonePhase.Paused && Math.Abs(game.GameTime - pauseClock) < .0001 && game.Score == pauseScore, "Pause did not freeze chart and score.");
                        Require(!game.Music.isPlaying && game.Music.timeSamples == pauseSamples, "Pause did not freeze audio position.");
                        Require(Vector3.Distance(game.Actor.position, pauseActor) < .0001f && Vector3.Distance(stage.WorldActorPosition, pauseWorld) < .0001f && Quaternion.Angle(stage.Camera.transform.rotation, pauseCamera) < .02f, "Pause did not freeze logical/world courier and camera.");
                        report.pauseVerified = true; report.pauseDspSeconds = AudioSettings.dspTime - pausedDsp;
                        Click("ResumeButton"); step = 9; break;
                    case 9:
                        if (Time.frameCount <= pressedFrame + 3) return;
                        Release(); if (game.Phase != ClonePhase.Playing) return;
                        Require(game.GameTime >= pauseClock - .0001 && game.GameTime - pauseClock < .5, "Resume skipped the preserved chart position.");
                        lastDsp = 0; ArmPointer(); step = 10; break;
                    case 10:
                        if (!CompleteArm()) return;
                        step = 11; break;
                    case 11: Bot(); break;
                }
                if (EditorApplication.timeSinceStartup - lastWrite > 5) { Persist(); lastWrite = EditorApplication.timeSinceStartup; }
            }
            catch (Exception error) { Errors.Add(error.ToString()); Finish(false, "Review assertion failed."); }
        }

        private static void VerifyCourierScale()
        {
            var courier = stage.GetComponentInChildren<RadioArchipelago.CourierMotion>(true);
            Require(courier != null, "The displayed courier is missing from the island stage.");
            if (finishMotion != courier || finishVisual != courier.EditorVisualPivot)
            {
                finishMotion = courier;
                finishVisual = courier.EditorVisualPivot;
                finishHeadingReady = false;
            }
            courierRenderers = courier.GetComponentsInChildren<Renderer>(true);
            report.courierIndex = PlayerPrefs.GetInt(CourierKey, 0);
            float departureScale = stage.FinishDepartureStarted ? stage.FinishDepartureVisualScale : 1;
            report.courierScale = stage.Definition.CourierScale;
            Require(Vector3.Distance(courier.transform.localScale, Vector3.one * (stage.Definition.CourierScale * departureScale)) < .0001f,
                "The displayed courier did not receive the island scale after song preparation: " + courier.transform.localScale);
            report.courierScaleChecked = true;
        }

        private static void Observe()
        {
            // A framebuffer capture can block this callback for hundreds of
            // milliseconds. Sample both clocks before any captures so the
            // review does not count its own readback cost as gameplay drift.
            double observedDsp = AudioSettings.dspTime, observedClock = game.GameTime;
            report.observedFrames++; report.chartClock = observedClock; report.realDspSeconds = observedDsp - startedDsp; report.score = game.Score;
            if (report.uniformRoundTiles)
                Require(Mathf.Abs(stage.MinimumRoundSupportRadius - IslandSupportSpacing.NativeJumpRadius) < .001f,
                    "A normal-size island landing was reduced during the native run.");
            if (report.observedFrames >= 20)
            {
                if (stageCameraData == null) stageCameraData = stage.Camera.GetComponent<UniversalAdditionalCameraData>();
                Require(stageCameraData != null, "The pilot camera's URP data is missing.");
                report.postProcessingChecked = true; report.postProcessingSeen |= stageCameraData.renderPostProcessing;
                Require(!stageCameraData.renderPostProcessing, "Post-processing remained enabled on the pilot camera.");
                VerifyCourierScale();
            }
            float frameMs = Time.unscaledDeltaTime * 1000f;
            report.meanObservedFrameMs += (frameMs - report.meanObservedFrameMs) / report.observedFrames;
            report.maxObservedFrameMs = Mathf.Max(report.maxObservedFrameMs, frameMs); if (frameMs > 50) report.framesOver50Ms++;
            report.coinsBound = Math.Max(report.coinsBound, stage.BoundFinishCoinCount);
            report.maxVisibleCoins = Math.Max(report.maxVisibleCoins, stage.VisibleFinishCoinCount);
            report.coinsCollected = Math.Max(report.coinsCollected, game.CollectedCoins);
            report.unmappedCoins = Math.Max(report.unmappedCoins, stage.UnmappedFinishCoinCount);
            ObserveSignalSparks();
            report.maxLandingOverlaps = Math.Max(report.maxLandingOverlaps, stage.VisibleLandingOverlapCount);
            report.minLandingRadius = Mathf.Min(report.minLandingRadius, stage.MinVisibleLandingRadius);
            Require(stage.VisibleLandingOverlapCount == 0, "Visible round landing platforms intersected.");
            Require(report.unmappedCoins == 0, "A native finish coin could not be projected.");
            if (!report.coinsSeen && stage.VisibleFinishCoinCount > 0) { report.coinsSeen = true; Capture("finish-coins"); }
            var chart = game.Actor.position; var definition = stage.Definition;
            var frame = stage.EvaluatePresentationFrame(definition.StartDistance + chart.z * definition.DistanceScale);
            var expected = frame.Position + frame.Right * (chart.x * definition.LateralScale) + Vector3.up * (chart.y + definition.TileElevation + stage.ActorSurfaceOffset);
            float worldLateral = Vector3.Dot(stage.WorldActorPosition - frame.Position, frame.Right);
            report.minWorldLateral = Mathf.Min(report.minWorldLateral, worldLateral);
            report.maxWorldLateral = Mathf.Max(report.maxWorldLateral, worldLateral);
            float boost = game.IslandIsInFlight ? IslandFlightElevation.Boost(game.IslandJumpDistance,
                game.IslandJumpStart.z, chart.z, definition.JumpHeightScale) : 0;
            expected.y += boost;
            if (!game.IslandIsInFlight) expected.y += stage.VisualFinishHoverOffset;
            report.higherJumpBoost = Mathf.Max(report.higherJumpBoost, boost);
            Require(Mathf.Abs(stage.VisualJumpBoost - boost) < .001f, "The displayed higher jump boost disagrees with native flight progress.");
            if (stage.Elevation != null && game.IslandIsInFlight)
            {
                float flight = stage.GroundFinishPath != null && stage.GroundFinishPath.IsFinalFlight(game.IslandJumpStart, game.IslandJumpDistance)
                    ? stage.GroundFinishPath.FlightSurfaceY(game.IslandJumpStart, game.IslandJumpDistance, chart.z) - definition.TileElevation
                    : IslandFlightElevation.Sample(stage.Elevation, definition.StartDistance, definition.DistanceScale,
                        game.IslandJumpStart, game.IslandJumpDistance, chart.z);
                expected.y += flight - frame.Position.y;
                report.maxFlightElevationCorrection = Mathf.Max(report.maxFlightElevationCorrection, Mathf.Abs(flight - frame.Position.y));
            }
            if (stage.Elevation != null)
            {
                report.minRouteHeight = Mathf.Min(report.minRouteHeight, frame.Position.y);
                report.maxRouteHeight = Mathf.Max(report.maxRouteHeight, frame.Position.y);
                report.maxRouteGrade = Mathf.Max(report.maxRouteGrade, Mathf.Abs(frame.Grade));
                if (frame.Grade > .03f) report.ascendingFrames++;
                if (frame.Grade < -.03f) report.descendingFrames++;
                if (!report.boundedReview && !IsIndependentIslandReview)
                {
                double s = (definition.StartDistance + chart.z * definition.DistanceScale) % stage.Route.ExactLength;
                if (!report.reliefBridgeSeen && s > 86 && s < 96) { report.reliefBridgeSeen = true; Capture("relief-stone-bridge"); }
                if (!report.reliefStairsSeen && s > 246 && s < 263) { report.reliefStairsSeen = true; Capture("relief-wood-stairs"); }
                if (!report.reliefHillSeen && s > 282 && s < 315) { report.reliefHillSeen = true; Capture("relief-hilltop-house"); }
                if (!report.reliefDescentSeen && s > 349 && s < 374) { report.reliefDescentSeen = true; Capture("relief-descent"); }
                if (!report.reliefSkySeen && s > 442 && s < 453) { report.reliefSkySeen = true; Capture("relief-canopy-flight"); }
                }
            }
            expected += stage.FinishDepartureOffset;
            report.maxWorldPoseError = Mathf.Max(report.maxWorldPoseError, Vector3.Distance(expected, stage.WorldActorPosition)); report.worldPoseSamples++;
            Require(report.maxWorldPoseError < .025f, "The displayed courier disagrees with the real logical route pose.");
            var projection = stage.ParticleProjection;
            report.maxExpiredHoldVisuals = Math.Max(report.maxExpiredHoldVisuals, stage.VisibleExpiredHoldCount);
            report.visibleExitDiscs = Math.Max(report.visibleExitDiscs, stage.VisibleExitDiscCount);
            Require(stage.VisibleExpiredHoldCount == 0, "A consumed Hold strip remained visible after its release window.");
            Require(stage.VisibleExitDiscCount == 0, "A logical Hold/Follow exit has an invented visible disc.");
            if (projection != null)
            {
                report.maxNoteParticles = Math.Max(report.maxNoteParticles, projection.DebugActiveNoteParticleCount);
                report.suppressedHoldFlashes = Math.Max(report.suppressedHoldFlashes, projection.SuppressedHoldFlashSystemCount);
                report.noteSizeMultiplier = IslandParticleProjection.NoteParticleSizeMultiplier;
                report.mappedCollectibleMarkers = Math.Max(report.mappedCollectibleMarkers, projection.MirroredCollectibleMarkerCount);
                report.maxVisibleCollectibleMarkers = Math.Max(report.maxVisibleCollectibleMarkers, projection.DebugVisibleCollectibleMarkerCount);
                report.maxHoldBandOpacity = Mathf.Max(report.maxHoldBandOpacity, projection.DebugMaxVisibleBandOpacity);
                Require(projection.DebugMaxVisibleBandOpacity <= .241f, "The white Hold band became an opaque overlay.");
                report.MappedSystems = Math.Max(report.MappedSystems, projection.MirroredSystemCount);
                report.maxParticles = Math.Max(report.maxParticles, projection.DebugActiveParticleCount);
                report.readableEffectReplacements = Math.Max(report.readableEffectReplacements, projection.ReadableReplacementMeshCount);
                report.projectedEffectVertices = Math.Max(report.projectedEffectVertices, projection.ProjectedEffectVertexCount);
                report.maxHoldTriangleChartEdge = Mathf.Max(report.maxHoldTriangleChartEdge, projection.MaxHoldTessellationChartEdge);
                report.peakVisibleProjectedVertices = Math.Max(report.peakVisibleProjectedVertices, projection.DebugVisibleEffectVertexCount);
                report.meanProjectionCpuMs = projection.MeanCpuMilliseconds;
                report.meanParticleCpuMs = projection.MeanParticleCpuMilliseconds;
                report.meanTrailCpuMs = projection.MeanTrailCpuMilliseconds;
                report.meanMeshCpuMs = projection.MeanMeshCpuMilliseconds;
                report.peakMeshCpuMs = projection.PeakMeshCpuMilliseconds;
                report.geometryRebuilds = projection.GeometryRebuildCount;
                report.geometryReuses = projection.GeometryReuseCount;
                report.projectedVertices = projection.GeometryProjectedVertexCount;
                report.reusedVertices = projection.GeometryReusedVertexCount;
                report.geometryParityChecks = projection.GeometryParityChecks;
                report.geometryParityFailures = projection.GeometryParityFailures;
                report.poolGrowth = projection.PoolGrowthCount;
                if (projection.DebugAdvancedHoldBandCount > 0) report.advancedHoldBandFrames++;
                report.maxHoldBandOutsideSupport = Mathf.Max(report.maxHoldBandOutsideSupport, projection.DebugMaxHoldBandOutsideSupport);
                report.maxNativeFishLineViews = Math.Max(report.maxNativeFishLineViews, projection.DebugVisibleNativeHoldLineCount);
                if (report.unifiedHoldTiles)
                {
                    Require(projection.DebugVisibleNativeHoldLineCount == 0, "The old native fish-shaped Hold tracer remains visible.");
                }
                // Reference-check occasional live cache hits without paying the
                // old per-vertex cost on every frame of this timing capture.
                projection.ValidateGeometryCacheParity = Time.frameCount % 120 == 0;
                report.peakProjectionCpuMs = Mathf.Max(report.peakProjectionCpuMs, projection.PeakCpuMilliseconds);
                Require(report.maxHoldTriangleChartEdge <= .75f, "Native Hold geometry has long straight edges instead of following the road.");
                if (!report.particlesSeen && projection.DebugActiveParticleCount > 0)
                { report.particlesSeen = true; Capture("projected-native-effects"); }
            }
            report.maxJumpY = Mathf.Max(report.maxJumpY, chart.y);
            report.maxVisibleHoldBodies = Math.Max(report.maxVisibleHoldBodies, stage.VisibleUnifiedHoldBodies);
            report.maxVisibleRadioHoldTracers = Math.Max(report.maxVisibleRadioHoldTracers, stage.VisibleRadioHoldTracers);
            ObserveLivingNature();
            ObserveGroundFinish();
            ObserveCamera();
            float yaw = stage.Camera.transform.eulerAngles.y;
            report.cameraYawTravel += Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw)); previousYaw = yaw;
            int lap = IsIndependentIslandReview && stage.GroundFinishPath != null && chart.z >= stage.GroundFinishPath.TakeoffChartZ
                ? previousLap : Lap(chart.z);
            if (lap > previousLap)
            {
                report.seamCrossings += lap - previousLap; report.seamSeen = true;
                float worldStep = Vector3.Distance(previousWorld, stage.WorldActorPosition);
                report.maxSeamWorldStep = Mathf.Max(report.maxSeamWorldStep, worldStep);
                Require(worldStep < Vector3.Distance(previousChart, chart) * 2 + 1, "World courier teleported across the route seam.");
                Capture("seam-" + report.seamCrossings);
            }
            previousLap = lap; previousWorld = stage.WorldActorPosition; previousChart = chart;
            if (!turnCaptured && report.cameraYawTravel > 65) { Capture("turn"); turnCaptured = true; }
            double dsp = observedDsp;
            if (lastDsp > 0 && !game.WinSequence && step != 9 && step != 10)
            {
                float drift = (float)Math.Abs((observedClock - lastClock) - (dsp - lastDsp) * game.PlaybackRate);
                report.maxClockDspDrift = Mathf.Max(report.maxClockDspDrift, drift);
                Require(drift < .15f, "Chart clock diverged from unmodified DSP playback.");
            }
            lastDsp = dsp; lastClock = observedClock;
            foreach (var tile in game.Tiles)
            {
                ObserveHoldLifetime(tile);
                ObservePlatformPose(tile);
                if (tile.element.updateType == 3)
                {
                    Vector2 range; if (!Moving.TryGetValue(tile, out range)) range = new Vector2(tile.CenterX, tile.CenterX);
                    range.x = Mathf.Min(range.x, tile.CenterX); range.y = Mathf.Max(range.y, tile.CenterX); Moving[tile] = range;
                    if (range.y - range.x > .04f) report.movingTileSeen = true;
                    if (!movingCaptured && !tile.consumed && tile.initialZ - chart.z > 1 && tile.initialZ - chart.z < 12) { Capture("moving-tile"); movingCaptured = true; }
                }
                if (tile.consumed && Consumed.Add(tile))
                {
                    if (tile.element.updateType == 3) report.movingTilesConsumed++;
                    if (tile.element.id == 0 || tile.element.id == 1 || tile.element.id == 4 || tile.element.id == 6 || tile.element.id == 8) bigLaunch = tile.element.id == 1;
                }
            }
            if (game.Support != null && (game.Support.element.id == 2 || game.Support.element.id == 3))
            {
                if (report.unifiedHoldTiles)
                {
                    Require(stage.VisibleUnifiedHoldBodies > 0, "The held tile has no visible styled Hold body.");
                    Require(stage.VisibleRadioHoldTracers > 0, "The held tile has no visible Radio Archipelago tracer.");
                }
                report.holdSeen = true; if (!holdCaptured) { Capture("hold"); holdCaptured = true; }
                float end = game.Support.segment != null ? game.Support.segment.endZ : game.Support.initialZ + game.Support.element.mfendPos;
                if (!holdRideCaptured && Mathf.InverseLerp(game.Support.initialZ, end, game.Actor.position.z) > .22f)
                { Capture("hold-riding"); holdRideCaptured = true; }
            }
            if (game.Support != null && game.Support.element.id == 4)
            {
                report.glideSeen = true; if (!glideCaptured) { Capture("glide"); glideCaptured = true; }
                if (!markerCaptured && projection != null && projection.DebugVisibleCollectibleMarkerCount > 0)
                { Capture("collectible-markers"); markerCaptured = true; }
            }
            if (bigLaunch && game.Support == null && game.JumpPercent > .15f && game.JumpPercent < .85f)
            { report.bigJumpSeen = true; if (!bigCaptured) { Capture("big-jump"); bigCaptured = true; } }
            float audioTime = (float)game.GameTime - game.Map.delayTime;
            CaptureTerrainMoment();
            float[] captureTimes = report.boundedReview ? LivingCaptureTimes : CaptureTimes;
            if (captureIndex < captureTimes.Length && audioTime >= captureTimes[captureIndex])
            { Capture((report.boundedReview ? "living-" : "song-") + captureTimes[captureIndex]); captureIndex++; }
        }

        private static void ObservePlatformPose(OriginalTile tile)
        {
            int id = tile.element.id;
            if (id < 0 || id > 4) return;
            bool state = stage.TryGetTileVisualState(tile, out bool visible, out float surface, out float scale);
            if (report.unifiedHoldTiles && (id == 2 || id == 3) && game.Support == tile)
            {
                report.maxHeldBodyScaleError = Mathf.Max(report.maxHeldBodyScaleError, Mathf.Abs(scale - 1));
                Require(state && visible && Mathf.Abs(scale - 1) <= .001f, "The ceramic Hold body disappeared or shrank while still supporting the courier.");
            }
            if (!state || !visible || scale < .999f) return;
            var definition = stage.Definition;
            if (id == 2 || id == 3)
            {
                // Test actual retained cap-centre vertices while the unscaled
                // ride is held. Release animation is covered by the FX checks.
                if (game.Support != tile || !stage.TryGetHoldDrawRange(tile, out float begin, out float end) ||
                    !stage.TryGetHoldGeometryCenters(tile, out Vector3 head, out Vector3 tail)) return;
                float originalEnd = tile.segment != null ? tile.segment.endZ : tile.initialZ + tile.element.mfendPos;
                float rangeError = Mathf.Max(Mathf.Abs(begin - tile.initialZ), Mathf.Abs(end - originalEnd));
                report.maxHoldBodyRangeError = Mathf.Max(report.maxHoldBodyRangeError, rangeError);
                Require(rangeError <= .001f, "The ceramic Hold body no longer covers its complete original start/end interval.");
                var headFrame = stage.Route.Evaluate(definition.StartDistance + begin * definition.DistanceScale);
                var tailFrame = stage.Route.Evaluate(definition.StartDistance + end * definition.DistanceScale);
                float startHeight = stage.Route.Evaluate(definition.StartDistance + tile.initialZ * definition.DistanceScale).Position.y;
                float headHeight = Mathf.LerpUnclamped(startHeight, tailFrame.Position.y, (begin - tile.initialZ) / Mathf.Max(.05f, end - tile.initialZ));
                Vector3 expectedHead = headFrame.Position + headFrame.Right * (tile.element.pos.x * definition.LateralScale);
                Vector3 expectedTail = tailFrame.Position + tailFrame.Right * (tile.element.pos.x * definition.LateralScale);
                expectedHead.y = headHeight + tile.element.pos.y + definition.TileElevation + surface;
                expectedTail.y = tailFrame.Position.y + tile.element.pos.y + definition.TileElevation + surface;
                float error = Mathf.Max(Vector3.Distance(head, expectedHead), Vector3.Distance(tail, expectedTail));
                report.maxHoldAlignmentError = Mathf.Max(report.maxHoldAlignmentError, error);
                report.holdAlignmentSamples++;
                Require(error < .025f, "The held ceramic body's actual head/tail disagree with its widened route lane or straight grade.");
                RecordPlatformWidth(tile, Vector3.Dot(head - headFrame.Position, headFrame.Right));
                ObserveFullHold(tile, begin, end, head, tail, surface, scale);
                return;
            }
            if (!stage.TryGetTileVisualPivot(tile, out Vector3 pivot)) return;
            Vector3 chart = tile.transform.position;
            var frame = stage.EvaluatePresentationFrame(definition.StartDistance + chart.z * definition.DistanceScale);
            Vector3 expected = frame.Position + frame.Right * (chart.x * definition.LateralScale) +
                Vector3.up * (chart.y + definition.TileElevation + surface);
            float poseError = Vector3.Distance(pivot, expected);
            report.maxPlatformPoseError = Mathf.Max(report.maxPlatformPoseError, poseError);
            report.platformPoseSamples++;
            Require(poseError < .025f, "A visible landing platform disagrees with its live widened route position.");
            if (tile.element.updateType == 3)
            {
                report.movingAlignmentSamples++;
                report.maxMovingAlignmentError = Mathf.Max(report.maxMovingAlignmentError, poseError);
            }
            RecordPlatformWidth(tile, Vector3.Dot(pivot - frame.Position, frame.Right));
        }

        private static void RecordPlatformWidth(OriginalTile tile, float worldLateral)
        {
            report.minPlatformWorldLateral = Mathf.Min(report.minPlatformWorldLateral, worldLateral);
            report.maxPlatformWorldLateral = Mathf.Max(report.maxPlatformWorldLateral, worldLateral);
            if (Mathf.Abs(worldLateral) < report.fullRangePlatformThreshold || !FullRangePlatforms.Add(tile)) return;
            report.fullRangePlatforms++;
            if (worldLateral < 0) report.fullRangeLeftPlatforms++; else report.fullRangeRightPlatforms++;
        }

        private static void ObserveHoldLifetime(OriginalTile tile)
        {
            if (!report.unifiedHoldTiles || tile.element == null || (tile.element.id != 2 && tile.element.id != 3)) return;
            float end = tile.segment != null ? tile.segment.endZ : tile.initialZ + tile.element.mfendPos;
            if (!holdBeforeCaptured && !tile.consumed && end - tile.initialZ >= 5 &&
                tile.initialZ - game.Actor.position.z > .6f && tile.initialZ - game.Actor.position.z < 4 &&
                stage.TryGetTileVisualState(tile, out bool beforeVisible, out _, out _) && beforeVisible)
            { Capture("hold-full-before"); holdBeforeCaptured = true; }
            if (game.Support == tile)
            {
                if (!HoldLifetimes.TryGetValue(tile, out var held))
                {
                    held = new HoldLifetime { start = tile.initialZ, end = end };
                    HoldLifetimes.Add(tile, held);
                }
                held.lastHeldProgress = Mathf.InverseLerp(held.start, held.end, game.Actor.position.z);
                held.maxHeldProgress = Mathf.Max(held.maxHeldProgress, held.lastHeldProgress);
                held.lastHeldChartZ = game.Actor.position.z; held.lastHeldClock = game.GameTime;
                held.lastHeldFrame = Time.frameCount; held.heldFrames++;
                report.maxNativeHeldProgress = Mathf.Max(report.maxNativeHeldProgress, held.maxHeldProgress);
                return;
            }
            if (!HoldLifetimes.TryGetValue(tile, out var lifetime) || lifetime.complete) return;
            if (!lifetime.released)
            {
                lifetime.released = true; lifetime.releasedClock = game.GameTime; report.holdReleaseChecks++;
                bool nativeEnd = game.IslandIsInFlight && Mathf.Abs(game.IslandJumpStart.z - lifetime.end) <= .001f;
                bool fullThroughHandoff = lifetime.heldFrames > 0 && lifetime.geometryFrames == lifetime.heldFrames &&
                    lifetime.lastGeometryFrame == lifetime.lastHeldFrame;
                Require(nativeEnd && fullThroughHandoff,
                    "The Hold did not reach its native End handoff with full geometry verified through the last supported frame.");
                bool early = lifetime.maxHeldProgress < .95f;
                if (early)
                {
                    report.nativeEarlyHandoffs++;
                    Require(lifetime.lastHeldProgress < .95f,
                        "An early native handoff was incorrectly classified as a held 95-percent observation.");
                    lifetime.earlyHandoffVerified = true; report.nativeEndBefore95Checks++;
                }
                report.minNativeHeldProgress = Mathf.Min(report.minNativeHeldProgress, lifetime.maxHeldProgress);
                HoldHandoffs.Add(new HoldHandoffEvidence { sourceIndex = tile.element.sourceIndex,
                    lastHeldFrame = lifetime.lastHeldFrame, firstHandoffFrame = Time.frameCount,
                    heldFrames = lifetime.heldFrames, fullGeometryFrames = lifetime.geometryFrames,
                    lastHeldProgress = lifetime.lastHeldProgress, maxHeldProgress = lifetime.maxHeldProgress,
                    lastHeldChartZ = lifetime.lastHeldChartZ, handoffChartZ = game.Actor.position.z,
                    nativeArcStartZ = game.IslandJumpStart.z, originalEndZ = lifetime.end,
                    lastHeldClock = lifetime.lastHeldClock, handoffClock = game.GameTime,
                    nativeEndHandoff = nativeEnd, fullGeometryThroughLastHeld = fullThroughHandoff, before95 = early });
                if (!holdBoundaryCaptured && (lifetime.samples & 3) == 3)
                { Capture("radio-ride-native-handoff"); holdBoundaryCaptured = true; }
            }
            // Match the existing capped presentation-animation delta, including
            // capture stalls and pause, rather than treating DSP time as a spring clock.
            lifetime.releaseElapsed += Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (lifetime.releaseElapsed <= .55f) return;
            stage.TryGetTileVisualState(tile, out bool bodyVisible, out _, out _);
            stage.TryGetHoldTracerGeometry(tile, out _, out _, out _, out _, out bool tracerVisible);
            int bands = 0;
            if (stage.ParticleProjection != null) stage.ParticleProjection.TryGetHoldBandReviewState(tile, out _, out _, out bands);
            Require(!bodyVisible && !tracerVisible && bands == 0, "A released Hold body, progress wire or native translucent band outlived its normal release.");
            lifetime.complete = true; report.holdLifetimeChecks++;
            bool boundaryVerified = (lifetime.samples & 4) != 0 ||
                report.staticFullHold95Verified && lifetime.earlyHandoffVerified;
            if (!holdAfterCaptured && (lifetime.samples & 3) == 3 && boundaryVerified)
            { Capture("hold-full-after-release"); holdAfterCaptured = true; }
        }

        private static void ObserveFullHold(OriginalTile tile, float start, float end, Vector3 bodyStart, Vector3 bodyEnd, float surface, float scale)
        {
            Require(stage.TryGetHoldSupportRange(tile, out float originalStart, out float originalEnd, out float capRadius),
                "The full Hold support range is unavailable.");
            Require(stage.TryGetHoldTracerGeometry(tile, out Vector3 lineStart, out Vector3 lineEnd, out Vector3 marker,
                out float markerChartZ, out bool tracerVisible) && tracerVisible, "The held progress wire/marker is not visible.");
            float progress = Mathf.Clamp(game.Actor.position.z, start, end);
            float fraction = Mathf.InverseLerp(start, end, progress);
            float markerError = Vector3.Distance(marker, lineEnd), progressError = Mathf.Abs(markerChartZ - progress);
            var frame = stage.Route.Evaluate(stage.Definition.StartDistance + progress * stage.Definition.DistanceScale);
            Vector3 expectedEnd = frame.Position + frame.Right * (tile.element.pos.x * stage.Definition.LateralScale);
            expectedEnd.y = Mathf.Lerp(bodyStart.y, bodyEnd.y, fraction) + .025f;
            float lineError = Mathf.Max(Vector3.Distance(lineStart, bodyStart + Vector3.up * .025f), Vector3.Distance(lineEnd, expectedEnd));
            report.maxHoldMarkerEndpointError = Mathf.Max(report.maxHoldMarkerEndpointError, markerError);
            report.maxHoldProgressError = Mathf.Max(report.maxHoldProgressError, progressError);
            report.maxHoldLinePoseError = Mathf.Max(report.maxHoldLinePoseError, lineError);
            Require(markerError <= .001f && progressError <= .002f && lineError <= .003f,
                "The radio marker is offset from the exact centreline progress endpoint.");
            float bandMin = 0, bandMax = 0; int bandViews = 0;
            var projection = stage.ParticleProjection;
            if (projection != null && projection.TryGetHoldBandReviewState(tile, out bandMin, out bandMax, out bandViews))
            {
                float cap = capRadius / stage.Definition.DistanceScale;
                float outside = Mathf.Max(0, Mathf.Max(originalStart - cap - bandMin, bandMax - originalEnd - cap));
                float beyondProgress = Mathf.Max(0, bandMax - progress - cap);
                report.maxHoldBandOutsideRoundedSupport = Mathf.Max(report.maxHoldBandOutsideRoundedSupport, outside);
                report.maxHoldBandBeyondProgress = Mathf.Max(report.maxHoldBandBeyondProgress, beyondProgress);
                report.maxHoldBandPrefixEndpointError = Mathf.Max(report.maxHoldBandPrefixEndpointError, Mathf.Abs(bandMax - progress));
                Require(outside <= .002f && beyondProgress <= .002f && bandMin <= originalStart + cap + .002f && progress - bandMax <= cap + .002f,
                    "The native translucent Hold prefix extends beyond the physical support/progress or no longer begins at the original start.");
            }
            Require(HoldLifetimes.TryGetValue(tile, out var lifetime), "No native held-state observation exists for this full Hold frame.");
            lifetime.lastGeometryFrame = Time.frameCount; lifetime.geometryFrames++;
            if (end - start < 5) return;
            for (int sample = 0; sample < HoldProgressSamples.Length; sample++)
            {
                int bit = 1 << sample;
                if ((lifetime.samples & bit) != 0 || fraction < HoldProgressSamples[sample]) continue;
                Require(bandViews > 0, "No actual native translucent prefix is visible at a required Hold progress sample.");
                lifetime.samples |= bit;
                if (sample == 0) report.fullHoldSamples30++;
                else if (sample == 1) report.fullHoldSamples70++; else report.fullHoldSamples95++;
                if (HoldSamples.Count < 48) HoldSamples.Add(new HoldEvidence { sourceIndex = tile.element.sourceIndex,
                    chartClock = game.GameTime, requestedProgress = HoldProgressSamples[sample], observedProgress = fraction,
                    start = start, end = end, markerChartZ = markerChartZ, bandMinChartZ = bandMin, bandMaxChartZ = bandMax,
                    bandViews = bandViews, planarScale = scale, bodyStart = bodyStart, bodyEnd = bodyEnd,
                    lineStart = lineStart, lineEnd = lineEnd, markerCenter = marker });
                if (sample == 0 && !radioMidCaptured) { Capture("radio-ride-middle"); radioMidCaptured = true; }
                if (sample == 1 && !radioLateCaptured) { Capture("radio-ride-late"); radioLateCaptured = true; }
                if (sample == 2 && !holdBoundaryCaptured) { Capture("radio-ride-end-95"); holdBoundaryCaptured = true; }
            }
        }

        private static void CaptureTerrainMoment()
        {
            if ((!IsIndependentIslandReview && report.scene != RadioArchipelago.Editor.IslandAbyssJobs.ScenePath) ||
                terrainCaptureIndex >= report.requiredTerrainCaptures) return;
            float requested = IsIndependentIslandReview ? (float)game.Map.delayTime + report.songSeconds * CoralTerrainCaptureFractions[terrainCaptureIndex]
                : TerrainCaptureClocks[terrainCaptureIndex];
            if (game.GameTime < requested) return;
            terrainCaptureIndex++;
            string label = "terrain-review-clock-" + requested.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            OriginalTile next = null;
            foreach (var tile in game.Tiles)
                if (!tile.consumed && tile.initialZ > game.Actor.position.z && (next == null || tile.initialZ < next.initialZ)) next = tile;
            float localFinishZ = NativeLocalFinishZ != null ? (float)NativeLocalFinishZ.GetValue(game) : 0;
            float lapOffset = NativeLapOffset != null ? (float)NativeLapOffset.GetValue(game) : 0;
            float clockFromProgress = (game.Progress * localFinishZ + lapOffset) / Mathf.Max(.01f, game.Map.forwardSpeed);
            var evidence = new TerrainFrameEvidence { screenshot = "play-" + report.runId + "-" + label + ".png",
                chartClock = game.GameTime, requestedChartClock = requested, audioTime = (float)game.GameTime - game.Map.delayTime,
                routeDistance = stage.Definition.StartDistance + game.Actor.position.z * stage.Definition.DistanceScale,
                nativeActor = game.Actor.position, worldActor = stage.WorldActorPosition,
                nativeProgress = game.Progress, localFinishZ = localFinishZ, forwardSpeed = game.Map.forwardSpeed, lapOffset = lapOffset,
                progressDerivedChartClock = clockFromProgress, progressClockError = Mathf.Abs(clockFromProgress - (float)game.GameTime),
                supportSourceIndex = game.Support != null ? game.Support.element.sourceIndex : -1,
                nextSourceIndex = next != null ? next.element.sourceIndex : -1 };
            CaptureTerrainProxyState(evidence);
            TerrainFrames.Add(evidence);
            Capture(label);
        }

        private static void CaptureTerrainProxyState(TerrainFrameEvidence frame)
        {
            var glade = stage.FinishGlade;
            if (glade != null)
            {
                frame.finishGladeConfigured = glade.IsConfigured;
                frame.finishGladeExpectedActive = stage.GroundFinishPath != null &&
                    stage.GroundFinishPath.ShouldPresentGlade(game.Actor.position.z, stage.Definition.VisibleAhead);
                frame.finishGladeActive = glade.gameObject.activeInHierarchy &&
                    glade.GeneratedRoot != null && glade.GeneratedRoot.gameObject.activeInHierarchy;
                frame.finishGladeTriangles = glade.GeneratedTriangleCount;
                frame.finishGladeGroundHeight = glade.GroundHeight;
                bool boundsFound = false;
                foreach (var renderer in glade.GetComponentsInChildren<Renderer>(true))
                {
                    frame.finishGladeRenderers++;
                    if (boundsFound) frame.finishGladeBounds.Encapsulate(renderer.bounds);
                    else { frame.finishGladeBounds = renderer.bounds; boundsFound = true; }
                }
            }
            var proxies = StageProxyMap != null ? StageProxyMap.GetValue(stage) as System.Collections.IDictionary : null;
            frame.proxyReflectionAvailable = proxies != null && ProxyRenderer != null;
            var samples = new List<TerrainProxyEvidence>(9);
            int beginSource = frame.chartClock < 30 ? 60 : 146, endSource = frame.chartClock < 30 ? 68 : 151;
            IEnumerable<OriginalTile> sources = IsIndependentIslandReview
                ? game.Tiles.Where(tile => tile.element != null).OrderBy(tile => Mathf.Abs(tile.initialZ - game.Actor.position.z)).Take(12)
                : game.Tiles;
            foreach (var tile in sources)
            {
                if (tile.element == null || !IsIndependentIslandReview && (tile.element.sourceIndex < beginSource || tile.element.sourceIndex > endSource)) continue;
                var sample = new TerrainProxyEvidence { sourceIndex = tile.element.sourceIndex, id = tile.element.id,
                    consumed = tile.consumed, sourceIsSupport = game.Support == tile,
                    nativeCurrent = tile.transform.position, nativeContact = tile.ContactPosition };
                sample.hasProxy = stage.TryGetTileVisualPivot(tile, out Vector3 pivot);
                sample.proxyPosition = pivot;
                stage.TryGetTileVisualState(tile, out sample.proxyVisible, out sample.surfaceOffset, out sample.planarScale);
                var routeFrame = stage.EvaluatePresentationFrame(stage.Definition.StartDistance +
                    sample.nativeCurrent.z * stage.Definition.DistanceScale);
                sample.expectedPosition = routeFrame.Position + routeFrame.Right * (sample.nativeCurrent.x * stage.Definition.LateralScale) +
                    Vector3.up * (sample.nativeCurrent.y + stage.Definition.TileElevation + sample.surfaceOffset);
                sample.viewport = stage.Camera.WorldToViewportPoint(pivot);
                object proxy = proxies != null && proxies.Contains(tile) ? proxies[tile] : null;
                var renderer = proxy != null && ProxyRenderer != null ? ProxyRenderer.GetValue(proxy) as Renderer : null;
                if (renderer != null)
                {
                    sample.renderer = renderer.name; sample.rendererEnabled = renderer.enabled;
                    sample.rendererActive = renderer.gameObject.activeInHierarchy; sample.rendererBounds = renderer.bounds;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                    {
                        Mesh mesh = filter.sharedMesh; Bounds bounds = mesh.bounds;
                        sample.mesh = mesh.name; sample.meshVertices = mesh.vertexCount; sample.meshLocalMinY = bounds.min.y;
                        sample.meshWorldMinY = float.PositiveInfinity; sample.meshWorldMaxY = float.NegativeInfinity;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            Vector3 p = bounds.center + Vector3.Scale(bounds.extents,
                                new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                            float y = renderer.transform.TransformPoint(p).y;
                            sample.meshWorldMinY = Mathf.Min(sample.meshWorldMinY, y);
                            sample.meshWorldMaxY = Mathf.Max(sample.meshWorldMaxY, y);
                        }
                    }
                }
                if (glade != null && sample.hasProxy && glade.TrySampleGround(pivot, out float yGround))
                {
                    sample.gladeSample = true; sample.gladeSurfaceY = yGround;
                    sample.gladeAboveTileBottom = yGround - sample.meshWorldMinY;
                    sample.gladeAboveTileTop = yGround - sample.meshWorldMaxY;
                    sample.potentialGladeOverlap = sample.meshVertices > 0 && sample.gladeAboveTileBottom > 0;
                    sample.visibleGladeOverlap = sample.potentialGladeOverlap && frame.finishGladeActive &&
                        sample.proxyVisible && sample.rendererEnabled && sample.rendererActive;
                }
                samples.Add(sample);
            }
            frame.nearbySources = samples.ToArray();
        }

        private static void ReadTriangleClearanceReport()
        {
            if (!IsIndependentIslandReview && report.scene != RadioArchipelago.Editor.IslandAbyssJobs.ScenePath) return;
            report.terrainClearanceReport = IsAmberReview ? RadioArchipelago.Editor.AmberWindsIslandJobs.TerrainReportPath : IsMoonReview ? RadioArchipelago.Editor.MoonResonanceIslandJobs.TerrainReportPath : IsCoralReview ? RadioArchipelago.Editor.CoralOasisIslandJobs.TerrainReportPath :
                "Library/RadioArchipelago/TerrainClearance/terrain-clearance-baseline.json";
            report.terrainTriangleReportAvailable = report.terrainTriangleReportFresh = report.terrainTriangleClearancePassed = false;
            if (!File.Exists(report.terrainClearanceReport)) return;
            try
            {
                var evidence = JsonUtility.FromJson<TriangleClearanceReport>(File.ReadAllText(report.terrainClearanceReport));
                if (evidence == null) return;
                report.terrainTriangleReportAvailable = true; report.terrainTriangleClearancePassed = evidence.passed;
                report.terrainClearanceCheckedUtc = evidence.checkedUtc; report.terrainClearanceDefinitionHash = evidence.definitionDependencyHash;
                string path = AssetDatabase.GetAssetPath(stage.Definition);
                report.terrainTriangleReportFresh = !string.IsNullOrEmpty(path) &&
                    AssetDatabase.GetAssetDependencyHash(path).ToString() == evidence.definitionDependencyHash;
                if (IsMoonReview || IsAmberReview)
                {
                    string prefabPath = AssetDatabase.GetAssetPath(stage.Definition.Prefab);
                    report.terrainTriangleReportFresh &= !string.IsNullOrEmpty(prefabPath) &&
                        AssetDatabase.GetAssetDependencyHash(prefabPath).ToString() == evidence.prefabDependencyHash;
                    report.terrainTriangleClearancePassed &= evidence.anchorChecksPassed && evidence.floorRegressionPassed &&
                        evidence.structureRegressionPassed && evidence.structureSamples > 1000 && evidence.structureViolations == 0 &&
                        evidence.actualFinishFloorSamples > 100;
                }
                report.terrainCorridorSamples = evidence.corridorSamples; report.terrainTileSamples = evidence.tileSamples;
                report.terrainCorridorViolations = evidence.corridorViolations; report.terrainTileViolations = evidence.tileViolations;
                report.minTerrainCorridorClearance = evidence.minCorridorClearance; report.minTerrainTileClearance = evidence.minTileClearance;
            }
            catch (Exception error) { Notes.Add("Independent terrain diagnostic could not be read: " + error.Message); }
        }

        private static void ObserveLivingNature()
        {
            if (!report.livingNatureEnabled || natureRenderers == null) return;
            int fauna = 0, clouds = 0, bank = 0;
            foreach (var renderer in natureRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.transform.parent == null) continue;
                Vector3 visible = stage.Camera.WorldToViewportPoint(renderer.bounds.center);
                if (visible.z <= 0 || visible.x < .03f || visible.x > .97f || visible.y < .04f || visible.y > .88f) continue;
                string parent = renderer.transform.parent.name;
                if (renderer.name == "Body" && parent.Contains(" pool ")) fauna++;
                else if (parent.StartsWith("Drifting sky cloud ", StringComparison.Ordinal)) clouds++;
                else if (parent.StartsWith("High sky cloud bank ", StringComparison.Ordinal)) bank++;
            }
            report.maxVisibleFaunaBodies = Math.Max(report.maxVisibleFaunaBodies, fauna);
            report.maxVisibleClouds = Math.Max(report.maxVisibleClouds, clouds + bank);
            report.livingFaunaSeen |= fauna > 0;
            report.cloudsSeen |= clouds + bank > 0;
            report.cloudBankSeen |= bank > 0;
            if (fauna > 0 && !faunaCaptured) { faunaCaptured = true; Capture("living-fauna"); }
            if (clouds > 0 && !cloudsCaptured) { cloudsCaptured = true; Capture("drifting-clouds"); }
            if (bank > 0 && !cloudBankCaptured) { cloudBankCaptured = true; Capture("cloud-crossing"); }
        }

        private static bool IsNatureRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.transform.parent == null) return false;
            string parent = renderer.transform.parent.name;
            return renderer.name == "Body" && parent.Contains(" pool ") ||
                parent.StartsWith("Drifting sky cloud ", StringComparison.Ordinal) ||
                parent.StartsWith("High sky cloud bank ", StringComparison.Ordinal);
        }

        private static int Lap(float chartZ) => (int)Math.Floor((stage.Definition.StartDistance + chartZ * stage.Definition.DistanceScale) / stage.Route.ExactLength);

        private static void ObserveSignalSparks()
        {
            report.boundSignalSparks = Math.Max(report.boundSignalSparks, stage.BoundSignalSparkCount);
            report.maxVisibleSignalSparks = Math.Max(report.maxVisibleSignalSparks, stage.VisibleSignalSparkCount);
            report.maxVisibleConsumedSignalSparks = Math.Max(report.maxVisibleConsumedSignalSparks, stage.VisibleConsumedSignalSparkCount);
            if (!report.signalSparkCurrencySelected) return;
            Require(stage.BoundFinishCoinCount == 60 && stage.BoundSignalSparkCount == stage.BoundFinishCoinCount,
                "The selected Signal Spark currency did not replace all 60 native finish-coin views.");
            Require(stage.VisibleConsumedSignalSparkCount == 0, "A collected native coin still has a visible Signal Spark.");
            if (game.CollectedCoins > 0) report.signalSparkDisappearanceSamples++;
            if (stage.VisibleSignalSparkCount > 0)
            {
                report.signalSparksSeen = true;
                report.signalSparkVisibleFrames++;
            }
        }

        private static void ObserveGroundFinish()
        {
            if (!report.groundFinishEnabled) return;
            var path = stage.GroundFinishPath;
            Require(path != null, "Ground finish is enabled without its final approach path.");
            var glade = stage.FinishGlade;
            Require(glade != null && glade.IsConfigured, "The ground finish glade is not configured.");
            bool expectedGlade = path.ShouldPresentGlade(game.Actor.position.z, stage.Definition.VisibleAhead);
            bool actualGlade = glade.gameObject.activeInHierarchy && glade.GeneratedRoot != null &&
                glade.GeneratedRoot.gameObject.activeInHierarchy;
            report.gladePolicyChecks++;
            if (!expectedGlade && !actualGlade) report.hiddenEarlyGladeFrames++;
            if (expectedGlade && actualGlade) report.activeFinalGladeFrames++;
            if (actualGlade != expectedGlade)
            {
                report.gladePolicyViolations++;
                if (!expectedGlade) report.preFinishActiveViolations++;
            }
            Require(actualGlade == expectedGlade, "The finish glade's actual visibility disagrees with its final-approach lifetime policy.");
            report.groundFinishTakeoffZ = path.TakeoffChartZ; report.groundFinishLandingZ = path.LandingChartZ;
            report.groundHeight = path.GroundHeight;
            report.maxVisibleFinishPlatforms = Math.Max(report.maxVisibleFinishPlatforms, stage.VisibleFinishPlatformCount);
            Require(stage.VisibleFinishPlatformCount == 0, "An oval finish tile is visible over the final ground.");
            float z = game.Actor.position.z;
            if (game.IslandIsInFlight && path.IsFinalFlight(game.IslandJumpStart, game.IslandJumpDistance))
            {
                report.groundFinalJumpSeen = true;
                if (!groundJumpCaptured && game.JumpPercent > .3f && game.JumpPercent < .8f)
                { Capture("final-jump-to-ground"); groundJumpCaptured = true; }
            }
            if (z < path.LandingChartZ || game.IslandIsInFlight) return;
            if (!report.groundLandingSeen)
            {
                report.groundLandingSeen = true; report.groundLandingClock = game.GameTime;
                report.coinsAtGroundLanding = game.CollectedCoins;
            }
            report.coinsCollectedOnGround = Math.Max(report.coinsCollectedOnGround, game.CollectedCoins - report.coinsAtGroundLanding);
            // The ordinary fixed camera is now near the first few pickups.
            // Capture the selected currency without changing input or framing.
            if (report.signalSparkCurrencySelected && !report.signalSparkLineCaptured &&
                stage.VisibleSignalSparkCount > 0 && z >= path.LandingChartZ + 6 && z <= path.LandingChartZ + 14)
            { Capture("signal-spark-ground-line"); report.signalSparkLineCaptured = true; }
            report.maxGroundedFinishCoins = Math.Max(report.maxGroundedFinishCoins, stage.VisibleGroundedFinishCoinCount);
            if (stage.VisibleGroundedFinishCoinCount > 0)
            {
                report.minGroundCoinClearance = Mathf.Min(report.minGroundCoinClearance, stage.MinVisibleFinishCoinGroundClearance);
                report.maxGroundCoinClearance = Mathf.Max(report.maxGroundCoinClearance, stage.MaxVisibleFinishCoinGroundClearance);
                Require(stage.MinVisibleFinishCoinGroundClearance >= .44f && stage.MaxVisibleFinishCoinGroundClearance <= .58f,
                    "Visible Signal Sparks must float above the flowers at the intended ground clearance.");
                report.groundCoinRunSeen = true;
                if (!groundCoinsCaptured && z > path.LandingChartZ + 3)
                { Capture("grounded-finish-coins"); groundCoinsCaptured = true; }
            }
            if (stage.FinishDepartureStarted)
            {
                ObserveFinishDeparture();
                return;
            }
            Require(finishMotion != null && finishMotion.IsFinishHover && finishVisual != null,
                "The actual native final contact did not start the forward hovering collection pose.");
            report.finishHoverHeight = stage.VisualFinishHoverOffset;
            float heading = finishVisual.localEulerAngles.y;
            if (finishHeadingReady) report.finishRotationDegrees += Mathf.DeltaAngle(previousFinishHeading, heading);
            previousFinishHeading = heading; finishHeadingReady = true;
            report.finishHoverRotationSamples++;
            if (game.GameTime - report.groundLandingClock >= .5)
            {
                Vector3 facing = Quaternion.Inverse(finishMotion.EditorVisualRestRotation) * finishVisual.localRotation * Vector3.forward;
                float error = Mathf.Abs(Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg);
                report.maxFinishCollectionYawError = Mathf.Max(report.maxFinishCollectionYawError, error);
                report.forwardFinishSamples++;
                Require(error < 2, "The collecting courier must face along the path with its back toward the camera, without axial spin.");
            }
            // Sample the actual skinned feet after the final touchdown squash.
            if (game.GameTime - report.groundLandingClock < .6) return;
            float originError = Mathf.Abs(stage.WorldActorPosition.y - path.GroundHeight - NativeRhythmRules.GroundOffset - IslandGroundFinishPath.CourierHoverHeight);
            report.maxGroundActorError = Mathf.Max(report.maxGroundActorError, originError);
            Require(originError < .025f, "The hovering courier origin disagrees with the intended meadow clearance.");
            if (Time.frameCount % 6 != 0) return;
            float feet = float.PositiveInfinity;
            foreach (var renderer in courierRenderers)
            {
                if (!DrawableCourier(renderer)) continue;
                ReadGeometryVertices(renderer);
                for (int i = 0; i < GeometryVertices.Count; i++)
                    feet = Mathf.Min(feet, renderer.transform.TransformPoint(GeometryVertices[i]).y);
            }
            Require(!float.IsPositiveInfinity(feet), "No actual courier geometry was available for the ground-foot check.");
            float clearance = feet - path.GroundHeight;
            report.minGroundFootClearance = Mathf.Min(report.minGroundFootClearance, clearance);
            report.maxGroundFootClearance = Mathf.Max(report.maxGroundFootClearance, clearance); report.groundFootSamples++;
            Require(clearance >= .25f && clearance <= .65f, "The courier's actual foot geometry must hover clearly above the flower meadow.");
            if (!groundLandingCaptured) { Capture("landed-on-finish-ground"); groundLandingCaptured = true; }
        }

        private static void ObserveFinishDeparture()
        {
            report.departureStarted = true;
            report.departureAfterNativeWin |= game.WinSequence;
            Require(game.WinSequence && stage.FinishDepartureStartCount == 1,
                "Departure must start exactly once, following the real native final trigger.");
            report.departureCollectedCoinsAtStart = stage.FinishDepartureCollectedCoinsAtStart;
            Require(report.departureCollectedCoinsAtStart >= 20 && game.CollectedCoins == report.departureCollectedCoinsAtStart,
                "The full reward line must have been collected before the courier departs; departure cannot alter rewards.");
            if (!departureSeen)
            {
                departureSeen = true;
                departureCameraStart = stage.Camera.transform.position;
                departureCameraStartRotation = stage.Camera.transform.rotation;
            }
            float progress = stage.FinishDepartureProgress;
            Require(progress >= previousDepartureProgress && progress <= 1, "Departure progress must be finite, monotone and bounded.");
            previousDepartureProgress = progress;
            report.departureSamples++;
            report.maxDepartureCameraPositionError = Mathf.Max(report.maxDepartureCameraPositionError,
                Vector3.Distance(departureCameraStart, stage.Camera.transform.position));
            report.maxDepartureCameraRotationError = Mathf.Max(report.maxDepartureCameraRotationError,
                Quaternion.Angle(departureCameraStartRotation, stage.Camera.transform.rotation));
            Require(report.maxDepartureCameraPositionError < .002f && report.maxDepartureCameraRotationError < .02f,
                "The final camera must remain still while the courier flies into the distance.");
            Vector3 travel = stage.WorldActorPosition - stage.FinishDepartureStartPosition;
            Vector3 forward = stage.GroundFinishPath.LandingFrame.Tangent; forward.y = 0; forward.Normalize();
            report.maxDepartureRise = Mathf.Max(report.maxDepartureRise, travel.y);
            report.maxDepartureForward = Mathf.Max(report.maxDepartureForward, Vector3.Dot(travel, forward));
            report.minimumDepartureScale = Mathf.Min(report.minimumDepartureScale, stage.FinishDepartureVisualScale);
            if (!departureEarlyCaptured && progress >= .08f)
            { departureEarlyCaptured = true; Capture("finish-departure-leap"); }
            if (!departureMidCaptured && progress >= .45f)
            { departureMidCaptured = true; Capture("finish-departure-distance"); }
            if (!departureLateCaptured && progress >= .85f)
            { departureLateCaptured = true; Capture("finish-departure-far"); }
            if (!stage.FinishDepartureComplete)
                Require(!stage.FinishDepartureCourierHidden, "The courier must remain visible during its final flight.");
            else
            {
                report.departureComplete = true;
                report.departureCourierHidden |= stage.FinishDepartureCourierHidden;
                int visibleRenderers = 0;
                foreach (var renderer in courierRenderers) if (DrawableCourier(renderer)) visibleRenderers++;
                Require(stage.FinishDepartureCourierHidden && visibleRenderers == 0,
                    "The complete courier must disappear after the flight and before results.");
                report.departureHiddenFrames++;
                if (!departureHiddenCaptured)
                { departureHiddenCaptured = true; Capture("finish-departure-disappeared"); }
            }
        }

        private static void ObserveCamera()
        {
            var camera = stage.Camera;
            if (stage.CameraUsesFixedRig)
            {
                var definition = stage.Definition;
                var anchorFrame = stage.EvaluatePresentationFrame(definition.StartDistance + game.Actor.position.z * definition.DistanceScale);
                Vector3 expectedAnchor = anchorFrame.Position + anchorFrame.Right *
                    (game.Actor.position.x * definition.LateralScale * definition.CameraLateralFollow);
                if (!stage.FinishDepartureStarted)
                    report.maxCameraRigAnchorError = Mathf.Max(report.maxCameraRigAnchorError,
                        Vector3.Distance(stage.CameraRigAnchor, expectedAnchor));
                float expectedRadius = Mathf.Sqrt(stage.Definition.CameraBackDistance * stage.Definition.CameraBackDistance +
                    stage.Definition.CameraHeight * stage.Definition.CameraHeight);
                report.maxCameraRigDistanceError = Mathf.Max(report.maxCameraRigDistanceError,
                    Mathf.Abs(Vector3.Distance(camera.transform.position, stage.CameraRigAnchor) - expectedRadius));
                report.fixedFovDelta = Mathf.Max(report.fixedFovDelta, Mathf.Abs(camera.fieldOfView - fixedCameraFieldOfView));
                report.fixedFovDelta = Mathf.Max(report.fixedFovDelta, Mathf.Abs(camera.fieldOfView - stage.Definition.CameraFieldOfView));
                // ApplyCamera stores its own world pose. Any movement inherited
                // afterwards from the native railway parent is an actual error.
                report.maxCameraRigPoseError = Mathf.Max(report.maxCameraRigPoseError,
                    Vector3.Distance(camera.transform.position, (Vector3)CameraTarget.GetValue(stage)));
                report.maxCameraRigRotationError = Mathf.Max(report.maxCameraRigRotationError,
                    Quaternion.Angle(camera.transform.rotation, (Quaternion)CameraHeading.GetValue(stage)));
                report.cameraRigSamples++;
            }
            else if (previousCameraFrame == Time.frameCount - 1 && report.observedFrames > 20)
            {
                float blend = 1 - Mathf.Exp(-stage.Definition.CameraSmoothing * Time.unscaledDeltaTime);
                Vector3 target = (Vector3)CameraTarget.GetValue(stage);
                float error = Vector3.Distance(camera.transform.position, Vector3.Lerp(previousCameraPosition, target, blend));
                float rotationError = Quaternion.Angle(camera.transform.rotation, Quaternion.Slerp(previousCameraRotation,
                    (Quaternion)CameraHeading.GetValue(stage), blend));
                report.maxCameraSmoothingError = Mathf.Max(report.maxCameraSmoothingError, error);
                report.maxCameraRotationSmoothingError = Mathf.Max(report.maxCameraRotationSmoothingError, rotationError);
                report.cameraSmoothingSamples++;
            }
            previousCameraPosition = camera.transform.position; previousCameraRotation = camera.transform.rotation; previousCameraFrame = Time.frameCount;
            report.viewportAspect = camera.aspect;
            Vector3 actor = camera.WorldToViewportPoint(stage.WorldActorPosition + Vector3.up * .65f);
            report.minActorViewportX = Mathf.Min(report.minActorViewportX, actor.x);
            report.maxActorViewportX = Mathf.Max(report.maxActorViewportX, actor.x);
            if (!stage.FinishDepartureCourierHidden && (actor.z <= 0 || actor.x < .04f || actor.x > .96f || actor.y < .04f || actor.y > .9f))
            {
                if (report.actorOffscreenFrames++ == 0) Capture("actor-out-of-frame");
            }
            Bounds bounds = default; bool hasBounds = false;
            foreach (var renderer in courierRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (hasBounds) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; hasBounds = true; }
            }
            bool clipped = false;
            if (hasBounds)
                for (int corner = 0; corner < 8; corner++)
                    clipped |= Outside(camera, bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
            if (clipped) CheckSuspectCourierBounds(camera, bounds);
            OriginalTile next = null;
            float z = game.Actor.position.z, nearest = float.PositiveInfinity;
            foreach (var tile in game.Tiles)
            {
                int id = tile.element.id;
                if (tile.consumed || (id != 0 && id != 1 && id != 2 && id != 3 && id != 4 && !(stage.GroundFinishPath != null && id == 10)) ||
                    tile.initialZ <= z + .05f || tile.initialZ >= nearest) continue;
                nearest = tile.initialZ; next = tile;
            }
            if (next == null || nearest - z > 15) return;
            Vector3 contact = next.ContactPosition;
            var frame = stage.EvaluatePresentationFrame(stage.Definition.StartDistance + contact.z * stage.Definition.DistanceScale);
            Vector3 landing = frame.Position + frame.Right * (contact.x * stage.Definition.LateralScale) + Vector3.up * (contact.y + stage.Definition.TileElevation);
            Vector3 point = camera.WorldToViewportPoint(landing);
            report.minLandingViewportX = Mathf.Min(report.minLandingViewportX, point.x);
            report.maxLandingViewportX = Mathf.Max(report.maxLandingViewportX, point.x);
            if (point.z <= 0 || point.x < .04f || point.x > .96f || point.y < .04f || point.y > .9f)
            {
                if (report.nextLandingOffscreenFrames++ == 0) Capture("landing-out-of-frame");
            }
            clipped = false;
            for (int side = -1; side <= 1; side += 2)
                for (int fore = -1; fore <= 1; fore += 2)
                    clipped |= Outside(camera, landing + frame.Right * (side * .7f) + frame.Tangent * (fore * .7f));
            if (clipped && report.nextLandingRimOffscreenFrames++ == 0) Capture("landing-rim-out-of-frame");
        }

        private static bool Outside(Camera camera, Vector3 world)
        {
            Vector3 v = camera.WorldToViewportPoint(world);
            return v.z <= 0 || v.x < .04f || v.x > .96f || v.y < .04f || v.y > .9f;
        }

        private static void CheckSuspectCourierBounds(Camera camera, Bounds worldBounds)
        {
            report.coarseActorBoundsFrames++;
            var evidence = new BoundsEvidence { frame = Time.frameCount, chartClock = game.GameTime,
                nativeActor = game.Actor.position, worldActor = stage.WorldActorPosition,
                cameraPosition = camera.transform.position, cameraRotation = camera.transform.rotation, worldBounds = worldBounds };
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 viewport = camera.WorldToViewportPoint(worldBounds.center + Vector3.Scale(worldBounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                string axis = OutsideAxis(viewport);
                if (axis.Length != 0) { evidence.coarseViewport = viewport; evidence.coarseAxis = axis; break; }
            }
            foreach (var renderer in courierRenderers)
            {
                if (!DrawableCourier(renderer)) continue;
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    if (Outside(camera, renderer.localToWorldMatrix.MultiplyPoint3x4(point))) evidence.orientedOutside = true;
                }
            }
            if (evidence.orientedOutside) report.orientedActorBoundsFrames++;
            // Check every suspect world box against current geometry. The
            // full rotor culling envelope remains only a cheap coarse filter.
            {
                report.geometryCheckFrames++;
                foreach (var renderer in courierRenderers)
                {
                    if (!DrawableCourier(renderer)) continue;
                    ReadGeometryVertices(renderer);
                    for (int i = 0; i < GeometryVertices.Count; i++)
                    {
                        Vector3 viewport = camera.WorldToViewportPoint(renderer.transform.TransformPoint(GeometryVertices[i]));
                        string axis = OutsideAxis(viewport);
                        if (axis.Length == 0) continue;
                        evidence.geometryOutside = true; evidence.geometryViewport = viewport;
                        evidence.geometryAxis = axis; evidence.renderer = renderer.name; break;
                    }
                    if (evidence.geometryOutside) break;
                }
            }
            if (evidence.geometryOutside)
            {
                report.actualGeometryClips++;
                if (report.actorBoundsOffscreenFrames++ == 0) Capture("courier-geometry-out-of-frame");
            }
            else report.coarseBoundsFalsePositives++;
            if (BoundsSamples.Count < 32) BoundsSamples.Add(evidence);
            else if (evidence.geometryOutside) BoundsSamples[BoundsSamples.Count - 1] = evidence;
        }

        private static bool DrawableCourier(Renderer renderer) => renderer != null && renderer.enabled &&
            renderer.gameObject.activeInHierarchy && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer);

        private static string OutsideAxis(Vector3 viewport) => viewport.z <= 0 ? "behind" : viewport.x < .04f ? "left" :
            viewport.x > .96f ? "right" : viewport.y < .04f ? "bottom" : viewport.y > .9f ? "top" : "";

        private static void ReadGeometryVertices(Renderer renderer)
        {
            GeometryVertices.Clear();
            if (renderer is SkinnedMeshRenderer skin)
            {
                if (geometryScratch == null) geometryScratch = new Mesh { name = "Island review suspect geometry", hideFlags = HideFlags.HideAndDontSave };
                skin.BakeMesh(geometryScratch, true);
                geometryScratch.GetVertices(GeometryVertices);
                return;
            }
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            Mesh mesh = filter.sharedMesh;
            if (mesh.isReadable) { mesh.GetVertices(GeometryVertices); return; }
            // Editor read-only data leaves the imported asset's Read/Write flag untouched.
            using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
            using (var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                data[0].GetVertices(vertices);
                for (int i = 0; i < vertices.Length; i++) GeometryVertices.Add(vertices[i]);
            }
        }

        private static void Bot()
        {
            if (game.Phase != ClonePhase.Playing || game.WinSequence) return;
            float target = 0;
            if (game.Support != null && game.Support.element.id != 4) target = game.Support.CenterX;
            else
            {
                float nearest = float.PositiveInfinity, z = game.Actor.position.z;
                foreach (var tile in game.Tiles)
                    if (!tile.consumed && tile.element.id != 7 && tile.element.id != 8 && tile.initialZ > z - .1f && tile.initialZ < nearest)
                    { nearest = tile.initialZ; target = tile.CenterX; }
            }
            Hold(target);
        }

        private static void RecordTrace()
        {
            OriginalTile target = game.Support != null && game.Support.element.id != 4 ? game.Support : null;
            if (target == null)
            {
                float nearest = float.PositiveInfinity, z = game.Actor.position.z;
                foreach (var tile in game.Tiles)
                    if (!tile.consumed && tile.element.id != 7 && tile.element.id != 8 && tile.initialZ > z - .1f && tile.initialZ < nearest)
                    { nearest = tile.initialZ; target = tile; }
            }
            var lastLanding = NativeLastLanding != null ? NativeLastLanding.GetValue(game) as OriginalTile : null;
            Vector2 position = pointer != null && pointer.added ? pointer.position.ReadValue() : Vector2.zero;
            traceScreenWidth = Screen.width;
            var sample = new TraceFrame
            {
                frame = Time.frameCount, reviewStep = step, score = game.Score, streak = game.Streak, phase = game.Phase,
                chartClock = game.GameTime, dspTime = AudioSettings.dspTime, actor = game.Actor.position,
                jumpStart = game.IslandJumpStart, jumpDistance = game.IslandJumpDistance, jumpPercent = game.JumpPercent,
                lastLandingSource = lastLanding != null ? lastLanding.element.sourceIndex : -1,
                supportSource = game.Support != null ? game.Support.element.sourceIndex : -1,
                nextTargetSource = target != null ? target.element.sourceIndex : -1,
                nextTargetId = target != null ? target.element.id : -1,
                nextTargetUpdateType = target != null ? target.element.updateType : -1,
                nextTargetCurrentX = target != null ? target.CenterX : 0,
                nextTargetContactX = target != null ? target.ContactPosition.x : 0,
                worldLateral = game.Actor.position.x * (stage != null ? stage.Definition.LateralScale : 1),
                nextTargetCurrentWorldLateral = target != null ? target.CenterX * (stage != null ? stage.Definition.LateralScale : 1) : 0,
                nextTargetContactWorldLateral = target != null ? target.ContactPosition.x * (stage != null ? stage.Definition.LateralScale : 1) : 0,
                nextTargetZ = target != null ? target.initialZ : 0,
                nextTargetAhead = target != null ? target.initialZ - game.Actor.position.z : 0,
                pointer = position, pointerHeld = pointer != null && pointer.added && pointer.leftButton.isPressed,
                qaDragOrigin = dragOrigin,
                qaMappedTargetX = dragOrigin + (position.x - Screen.width * .5f) * 6.6f / Mathf.Max(1, Screen.width),
                nativeDestinationX = NativeDestination != null ? (float)NativeDestination.GetValue(game) : 0,
                nativePointerOrigin = NativePointerOrigin != null ? (float)NativePointerOrigin.GetValue(game) : 0,
                nativeDragOrigin = NativeDragOrigin != null ? (float)NativeDragOrigin.GetValue(game) : 0,
                nativeDragging = NativeDragging != null && (bool)NativeDragging.GetValue(game),
                presenterBlocksInput = game.Presenter != null && game.Presenter.IsBlockingInput
            };
            TraceFrames[traceFrameWrite] = sample;
            traceFrameWrite = (traceFrameWrite + 1) % TraceFrames.Length;
            traceFrameCount = Math.Min(traceFrameCount + 1, TraceFrames.Length);
        }

        private static void OnScoreContact(OriginalPlayController.EditorTimingSample observed)
        {
            if (observed.game != game || observed.stage != "score-contact" || observed.tile == null) return;
            OriginalTile tile = observed.tile;
            if (ScoredContacts.Add(tile)) report.scoredScoreContacts = ScoredContacts.Count;
            if (tile.element.id == 9 && ScoredHoldPoints.Add(tile)) report.scoredHoldPoints = ScoredHoldPoints.Count;
            TraceContacts[traceContactWrite] = new TraceContact
            {
                frame = observed.frame, reviewStep = step, scoreBefore = game.Score, sourceIndex = tile.element.sourceIndex,
                id = tile.element.id, updateType = tile.element.updateType, chartClock = observed.gameTime, dspTime = observed.dspTime,
                currentX = tile.CenterX, contact = tile.ContactPosition, actor = observed.actorPosition, jumpStart = observed.jumpBegin
            };
            traceContactWrite = (traceContactWrite + 1) % TraceContacts.Length;
            traceContactCount = Math.Min(traceContactCount + 1, TraceContacts.Length);
        }

        private static void PersistTrace()
        {
            // Edit Mode restoration may run after a domain reload has cleared
            // these in-memory rings. Preserve the trace written at Finish.
            if (traceFrameCount == 0 && traceContactCount == 0) return;
            var trace = new TraceReport
            {
                runId = report.runId, screenWidth = traceScreenWidth,
                nativeBindingsComplete = NativeDestination != null && NativePointerOrigin != null && NativeDragOrigin != null &&
                    NativeDragging != null && NativeLastLanding != null,
                frames = CopyTraceRing(TraceFrames, traceFrameCount, traceFrameWrite),
                sourceContacts = CopyTraceRing(TraceContacts, traceContactCount, traceContactWrite)
            };
            File.WriteAllText(Path.Combine(ReviewFolder, report.traceFile), JsonUtility.ToJson(trace, true));
        }

        private static T[] CopyTraceRing<T>(T[] source, int count, int nextWrite)
        {
            var ordered = new T[count];
            int first = count == source.Length ? nextWrite : 0;
            for (int i = 0; i < count; i++) ordered[i] = source[(first + i) % source.Length];
            return ordered;
        }

        private static void ArmPointer()
        {
            // ReadPointer latches its origin only on a sampled up-to-down edge.
            // Keep UP across a real controller Update before centering DOWN.
            Release(); armPhase = 1; armFrame = pressedFrame = Time.frameCount;
        }

        private static bool CompleteArm()
        {
            if (Time.frameCount <= armFrame) return false;
            Require(NativeDragging != null && NativePointerOrigin != null && NativeDragOrigin != null && NativeDestination != null,
                "Native drag-origin bindings are required to verify the QA pointer gesture.");
            if (armPhase == 1)
            {
                Require(!(bool)NativeDragging.GetValue(game), "The controller did not sample the QA pointer's released frame.");
                if (game.Presenter != null && game.Presenter.IsBlockingInput) return false;
                dragOrigin = game.Actor.position.x;
                Send(new Vector2(Screen.width * .5f, Screen.height * .28f), true);
                armPhase = 2; armFrame = Time.frameCount;
                return false;
            }
            Require(armPhase == 2, "The QA pointer arm sequence is not active.");
            Require((bool)NativeDragging.GetValue(game) && pointer != null && pointer.leftButton.isPressed,
                "The controller did not sample the QA pointer's centered down frame.");
            Require(Mathf.Abs((float)NativePointerOrigin.GetValue(game) - Screen.width * .5f) < .01f &&
                Mathf.Abs((float)NativeDragOrigin.GetValue(game) - dragOrigin) < .001f &&
                Mathf.Abs((float)NativeDestination.GetValue(game) - dragOrigin) < .001f,
                "The QA pointer's centered down frame did not rebase the native drag origin and destination.");
            armPhase = 0;
            return true;
        }
        private static void Hold(float x) => Send(new Vector2(Screen.width * .5f + (x - dragOrigin) * Screen.width / 6.6f, Screen.height * .28f), true);
        private static void Release() { if (pointer != null && pointer.added) Send(pointer.position.ReadValue(), false); }
        private static void Send(Vector2 position, bool held)
        {
            if (pointer == null || !pointer.added) return;
            var state = new MouseState { position = position };
            InputSystem.QueueStateEvent(pointer, held ? state.WithButton(MouseButton.Left) : state);
            if (UpdateInput == null) throw new MissingMethodException("Input System dynamic update hook is unavailable.");
            UpdateInput.Invoke(null, DynamicUpdate);
        }

        private static void Click(string name)
        {
            var button = game.Presenter.UICanvas.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy && b.interactable);
            Require(button != null, "Active UI button is unavailable: " + name);
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Send(RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), true); pressedFrame = Time.frameCount;
        }

        private static void Capture(string label)
        {
            string filename = "play-" + report.runId + "-" + label + ".png";
            try
            {
                var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
                var view = Resources.FindObjectsOfTypeAll(type).FirstOrDefault();
                Require(view != null, "Actual GameView is unavailable.");
                for (var parent = type; parent != null; parent = parent.BaseType)
                    foreach (var field in parent.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (!typeof(RenderTexture).IsAssignableFrom(field.FieldType)) continue;
                        var frame = field.GetValue(view) as RenderTexture;
                        if (frame == null || frame.width != Screen.width || frame.height != Screen.height || frame.height < 100) continue;
                        Require(Math.Abs((float)frame.width / frame.height - 590f / 1280) < .01, "GameView must match narrow portrait phone framing.");
                        var previous = RenderTexture.active; var image = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
                        try
                        {
                            RenderTexture.active = frame; image.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); image.Apply();
                            if (SystemInfo.graphicsUVStartsAtTop)
                            {
                                var pixels = image.GetPixels32();
                                for (int y = 0; y < frame.height / 2; y++) for (int x = 0; x < frame.width; x++)
                                { int a = y * frame.width + x, b = (frame.height - 1 - y) * frame.width + x; var temp = pixels[a]; pixels[a] = pixels[b]; pixels[b] = temp; }
                                image.SetPixels32(pixels); image.Apply();
                            }
                            Directory.CreateDirectory(ReviewFolder); File.WriteAllBytes(Path.Combine(ReviewFolder, filename), image.EncodeToPNG()); Images.Add(filename); return;
                        }
                        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
                    }
                throw new InvalidOperationException("No actual GameView framebuffer matches current screen dimensions.");
            }
            catch (Exception error) { Errors.Add("Capture " + label + ": " + error.Message); }
        }

        private static void Complete()
        {
            ReadTriangleClearanceReport();
            report.won = game.WinSequence; report.score = game.Score; report.chartClock = game.GameTime;
            report.coinsCollected = Math.Max(report.coinsCollected, game.CollectedCoins);
            report.realDspSeconds = AudioSettings.dspTime - startedDsp;
            report.completedRecording = game.WinSequence && game.GameTime - game.Map.delayTime >= report.songSeconds - .3 && report.realDspSeconds - report.pauseDspSeconds >= report.songSeconds - 1;
            report.missingScoreContacts = game.Tiles.Where(tile => tile.element.kind != "Start" &&
                (tile.element.id >= 0 && tile.element.id <= 6 || tile.element.id == 9) && !ScoredContacts.Contains(tile))
                .Select(tile => "source=" + tile.element.sourceIndex + ", id=" + tile.element.id + ", kind=" + tile.element.kind +
                    ", z=" + tile.initialZ.ToString("R") + ", gameTime=" + tile.element.gameTime.ToString("R")).ToArray();
            if (!report.boundedReview && !report.completedRecording) Errors.Add("The complete recording was not exercised through real completion.");
            if (!report.boundedReview && report.scoredScoreContacts != report.requiredScoreContacts)
                Errors.Add("Native scoring contacts were missed: " + report.scoredScoreContacts + "/" + report.requiredScoreContacts + ".");
            if (!report.boundedReview && report.scoredHoldPoints != report.requiredHoldPoints)
                Errors.Add("Native HoldPoint scoring was incomplete: " + report.scoredHoldPoints + "/" + report.requiredHoldPoints + ".");
            if (!report.boundedReview && report.reliefStage && !IsIndependentIslandReview && (report.maxRouteHeight < 11 || report.ascendingFrames < 50 || report.descendingFrames < 50 ||
                !report.reliefBridgeSeen || !report.reliefStairsSeen || !report.reliefHillSeen || !report.reliefDescentSeen || !report.reliefSkySeen))
                Errors.Add("The relief course's bridge, stairs, hill, descent and canopy passage were not all exercised.");
            if (!report.boundedReview && IsCoralReview && (!report.reliefStage || report.maxRouteHeight - report.minRouteHeight < 6 ||
                report.ascendingFrames < 50 || report.descendingFrames < 50 || report.maxRouteGrade > .751f))
                Errors.Add("The Coral Oasis course did not exercise its actual elevated ascent/descent and safe route grades.");
            if (!report.boundedReview && (IsMoonReview || IsAmberReview) && (!report.reliefStage || report.maxRouteHeight - report.minRouteHeight < 6 ||
                report.ascendingFrames < 50 || report.descendingFrames < 50 || report.maxRouteGrade > .751f))
                Errors.Add("The independent island course did not exercise its actual elevated ascent/descent and safe route grades.");
            if (!report.pauseVerified) Errors.Add("Pause/resume was not verified.");
            if (!report.postProcessingChecked || report.postProcessingSeen) Errors.Add("Disabled pilot-camera post-processing was not verified.");
            if (!report.courierScaleChecked) Errors.Add("Actual courier visual scale was not verified.");
            if (!report.touchInputVerified) Errors.Add("The real touch input branch was not verified.");
            if (report.maxNoteParticles == 0) Errors.Add("No native musical note particles were observed.");
            if (report.requiredHolds > 0 && report.suppressedHoldFlashes == 0) Errors.Add("The transverse Hold flash exclusion was not verified.");
            if (report.requiredCollectibleMarkers > 0 && (report.mappedCollectibleMarkers == 0 || report.maxVisibleCollectibleMarkers == 0)) Errors.Add("Native collectible markers were not mapped and displayed.");
            if (report.requiredHolds > 0 && (report.unifiedHoldTiles ? report.maxVisibleHoldBodies == 0 : report.maxHoldBandOpacity <= 0))
                Errors.Add(report.unifiedHoldTiles ? "No visible styled Hold tile body was observed." : "No translucent Hold band was observed.");
            if (report.geometryParityChecks == 0 || report.geometryParityFailures > 0 || report.geometryReuses == 0)
                Errors.Add("Live geometry-cache parity or reuse was not verified.");
            if (report.worldPoseSamples < 100 || report.cameraYawTravel < 30) Errors.Add("Insufficient route-pose/camera-turn observations.");
            if (report.actorOffscreenFrames > 0 || report.nextLandingOffscreenFrames > 0)
                Errors.Add("Camera lost the courier or the immediate landing in narrow portrait framing: actor=" + report.actorOffscreenFrames + ", landing=" + report.nextLandingOffscreenFrames);
            if (report.actorBoundsOffscreenFrames > 0 || report.nextLandingRimOffscreenFrames > 0)
                Errors.Add("Camera clipped courier geometry or landing rim: courier=" + report.actorBoundsOffscreenFrames + ", rim=" + report.nextLandingRimOffscreenFrames);
            if (report.fixedCamera)
            {
                if (report.cameraRigSamples < 100 || report.maxCameraRigDistanceError >= .001f || report.fixedFovDelta >= .001f ||
                    report.maxCameraRigPoseError > .005f || report.maxCameraRigRotationError > .05f || report.maxCameraRigAnchorError >= .001f)
                    Errors.Add("Fixed camera changed radius/FOV or inherited parent motion: radius=" + report.maxCameraRigDistanceError +
                        ", FOV=" + report.fixedFovDelta + ", position=" + report.maxCameraRigPoseError + ", rotation=" + report.maxCameraRigRotationError +
                        ", anchor=" + report.maxCameraRigAnchorError);
            }
            else if (report.cameraSmoothingSamples < 100 || report.maxCameraSmoothingError > .005f || report.maxCameraRotationSmoothingError > .05f)
                Errors.Add("Camera motion bypassed damping: position=" + report.maxCameraSmoothingError + ", rotation=" + report.maxCameraRotationSmoothingError);
            if (!report.seamSeen && (!IsIndependentIslandReview || report.expectedRouteSeam)) Errors.Add("No required repeated-lap seam was actually observed.");
            if (report.requiredHolds > 0 && !report.holdSeen) Errors.Add("Authored hold support was not observed.");
            if (report.requiredGlides > 0 && !report.glideSeen) Errors.Add("Authored glide support was not observed.");
            if (report.requiredBigJumps > 0 && !report.bigJumpSeen) Errors.Add("Authored big-jump flight was not observed.");
            if (report.requiredMovingTiles > 0 && (!report.movingTileSeen || report.movingTilesConsumed == 0)) Errors.Add("Authored moving tile motion/contact was not observed.");
            if (report.MappedSystems > 0 && !report.particlesSeen) Errors.Add("No live native particles were observed in the island projection.");
            if (!report.boundedReview && (report.coinsBound == 0 || !report.coinsSeen || report.coinsCollected == 0))
                Errors.Add("Native finish coin binding, visibility and real collection were not all observed.");
            if (!report.boundedReview && report.signalSparkCurrencySelected && (report.coinsBound != 60 || report.boundSignalSparks != 60 ||
                !report.signalSparksSeen || report.maxVisibleSignalSparks == 0 || report.maxVisibleConsumedSignalSparks != 0 ||
                report.signalSparkDisappearanceSamples == 0 || report.coinsCollected < 20 || !report.signalSparkLineCaptured))
                Errors.Add("Signal Spark binding to all 60 sources, live visibility, disappearance after collection, close ground-line capture and 20 native collections were not all verified.");
            if (report.groundFinishEnabled && (report.gladePolicyChecks == 0 || report.gladePolicyViolations != 0 ||
                report.preFinishActiveViolations != 0 || report.hiddenEarlyGladeFrames <= 100 ||
                (!report.boundedReview && report.activeFinalGladeFrames == 0)))
                Errors.Add("The finish glade was not verified hidden during early laps and active only for the final approach.");
            if (!report.boundedReview && report.groundFinishEnabled && (!report.groundFinishConfigured || !report.groundFinalJumpSeen ||
                !report.groundLandingSeen || !report.groundCoinRunSeen || report.groundFootSamples < 3 || report.maxGroundedFinishCoins == 0 ||
                report.coinsCollected < 20 || report.coinsCollectedOnGround < 15 || report.maxVisibleFinishPlatforms != 0 ||
                !groundJumpCaptured || !groundLandingCaptured || !groundCoinsCaptured))
                Errors.Add("The genuine final jump, grounded footsteps/coins, hidden finish oval and native 20-coin reward run were not all verified.");
            if (!report.boundedReview && report.groundFinishEnabled && (report.forwardFinishSamples < 30 || report.maxFinishCollectionYawError >= 2))
                Errors.Add("The final reward run did not maintain a stable forward-facing hovering courier.");
            if (!report.boundedReview && report.groundFinishEnabled && (!report.departureStarted || !report.departureAfterNativeWin ||
                !report.departureComplete || !report.departureCourierHidden || report.departureHiddenFrames == 0 ||
                report.maxDepartureRise < 17.9f || report.maxDepartureForward < 99.9f || report.minimumDepartureScale > .11f ||
                !departureEarlyCaptured || !departureMidCaptured || !departureLateCaptured || !departureHiddenCaptured))
                Errors.Add("The genuine post-collection final leap, flight into the distance, fixed camera and disappearance were not all verified.");
            if (report.lateralScale > 1.001f && (report.fullRangeLeftPlatforms == 0 || report.fullRangeRightPlatforms == 0 ||
                report.minWorldLateral > -report.fullRangePlatformThreshold + .1f || report.maxWorldLateral < report.fullRangePlatformThreshold - .1f ||
                (report.requiredMovingTiles > 0 && report.movingAlignmentSamples == 0) || (report.requiredHolds > 0 && report.holdAlignmentSamples == 0)))
                Errors.Add("The widened course's actual left/right travel, full-range platforms, moving landings and held geometry were not all verified.");
            bool boundary95Verified = report.fullHoldSamples95 > 0 ||
                report.staticFullHold95Verified && report.nativeEarlyHandoffs > 0 &&
                report.nativeEndBefore95Checks >= report.nativeEarlyHandoffs;
            if (report.unifiedHoldTiles && report.requiredLongHolds > 0 && report.fullHoldSamples95 == 0 && boundary95Verified)
                Notes.Add("Live held-state 95-percent samples remain zero: all observed native End handoffs occurred earlier. " +
                    "Full body/marker geometry was checked on every supported frame through the handoff; " +
                    "the fresh synthetic Edit Mode fixture independently verified exact .95 and 1 progress. " +
                    "Observed terminal held progress range: " + report.minNativeHeldProgress.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) +
                    ".." + report.maxNativeHeldProgress.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + ".");
            if (report.unifiedHoldTiles && report.requiredHolds > 0 &&
                (report.maxVisibleRadioHoldTracers == 0 || report.holdLifetimeChecks == 0))
                Errors.Add("The authored Hold body's live progress tracer and normal release lifetime were not verified.");
            if (report.unifiedHoldTiles && report.requiredLongHolds > 0 && (report.maxVisibleRadioHoldTracers == 0 || report.advancedHoldBandFrames < 10 ||
                report.maxNativeFishLineViews != 0 || report.maxHoldBandOutsideRoundedSupport > .002f || report.maxHoldBandBeyondProgress > .002f ||
                report.fullHoldSamples30 == 0 || report.fullHoldSamples70 == 0 || !boundary95Verified || report.holdLifetimeChecks == 0 ||
                !radioMidCaptured || !radioLateCaptured || !holdBeforeCaptured || !holdBoundaryCaptured || !holdAfterCaptured))
                Errors.Add("The complete constant Hold body, start-to-player prefix, exact marker, live 30/70 observations, native handoff plus independent 95-percent boundary geometry, and normal release lifetime were not all verified.");
            if ((report.scene == RadioArchipelago.Editor.IslandAbyssJobs.ScenePath || IsIndependentIslandReview) &&
                (!report.terrainTriangleReportAvailable || !report.terrainTriangleReportFresh || !report.terrainTriangleClearancePassed ||
                    report.terrainCorridorViolations != 0 || report.terrainTileViolations != 0 || TerrainFrames.Count < report.requiredTerrainCaptures))
                Errors.Add(IsAmberReview ? "A current passing imported-triangle Amber Winds report and all seven distributed gameplay captures are required; native collision success does not certify terrain clearance." : IsMoonReview ? "A current passing imported-triangle Moon Resonance report and all seven distributed gameplay captures are required; native collision success does not certify terrain clearance." :
                    IsCoralReview ? "A current passing imported-triangle Coral Oasis report and all seven distributed gameplay captures are required; native collision success does not certify terrain clearance." :
                    "A current passing imported-triangle terrain-clearance report and the 21–24s / 62.5–66.5s gameplay captures are required; collision success does not certify visibility above terrain.");
            if (report.boundedReview)
            {
                if (game.GameTime - game.Map.delayTime < report.boundedSeconds - .1)
                    Errors.Add("The focused review ended before its required native audio duration.");
                if (!report.fixedCamera || !report.unifiedHoldTiles || !report.livingNatureEnabled)
                    Errors.Add("The focused review did not exercise the living stage's fixed camera, unified Hold tiles and nature flags.");
                if (!report.livingFaunaSeen || !report.cloudsSeen || !report.cloudBankSeen)
                    Errors.Add("Living fauna, drifting clouds and the cloud crossing were not all observed inside the actual viewport.");
                if (report.higherJumpBoost <= .1f) Errors.Add("No increased visual jump height was observed.");
                if (!report.reliefStage || report.maxRouteHeight < 11.95f || report.ascendingFrames < 50 || report.descendingFrames < 50)
                    Errors.Add("The living stage's elevated ascent/descent did not reach the required 12m height.");
                if (captureIndex < LivingCaptureTimes.Length) Errors.Add("The focused gameplay capture schedule was incomplete.");
            }
            if (Images.Count < 5) Errors.Add("Insufficient actual GameView captures.");
            Capture(report.boundedReview ? "living-review-end" : "result");
            Finish(Errors.Count == 0, report.boundedReview ?
                "Focused review ended after real native playback of the requested prefix; full-song completion and finish coins are outside this report's scope." :
                "Actual recording run ended; observations, failures and framebuffer captures are recorded independently.");
        }

        private static void Finish(bool passed, string details)
        {
            if (SessionState.GetBool(Key + ".finishing", false)) return;
            SessionState.SetBool(Key + ".finishing", true);
            if (report == null) report = new Report { runId = SessionState.GetString(Key + ".runId", "") };
            report.pass = passed && Errors.Count == 0; report.state = report.pass ? "passed" : "failed"; report.details = details;
            if (started) report.realDspSeconds = AudioSettings.dspTime - startedDsp;
            try { CleanupInput(); }
            catch (Exception error) { Errors.Add("Input cleanup: " + error); report.pass = report.inputRestored = false; report.state = "failed"; }
            Persist();
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPaused = false; EditorApplication.isPlaying = false; }
            else Restore();
        }

        private static void CleanupInput()
        {
            Application.logMessageReceived -= OnLog;
            OriginalPlayController.EditorTimingObserved -= OnScoreContact;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (loop.subSystemList != null)
            { loop.subSystemList = loop.subSystemList.Where(s => s.type != typeof(IslandPilotPlayReview)).ToArray(); PlayerLoop.SetPlayerLoop(loop); }
            foreach (var device in Disabled) if (device != null && device.added && !device.enabled) InputSystem.EnableDevice(device);
            Disabled.Clear();
            if (pointer != null && pointer.added) InputSystem.RemoveDevice(pointer); pointer = null;
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent(); previousMouse = null;
            if (previousSettings != null) InputSystem.settings = previousSettings;
            if (reviewSettings != null && reviewSettings != previousSettings) UnityEngine.Object.DestroyImmediate(reviewSettings);
            reviewSettings = previousSettings = null;
            if (geometryScratch != null) UnityEngine.Object.DestroyImmediate(geometryScratch);
            geometryScratch = null; GeometryVertices.Clear();
            if (report != null) report.inputRestored = true;
        }

        private static void Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            report = JsonUtility.FromJson<Report>(SessionState.GetString(Key + ".report", "")) ?? new Report();
            Errors.Clear(); Notes.Clear(); Images.Clear();
            if (report.errors != null) Errors.AddRange(report.errors); if (report.notes != null) Notes.AddRange(report.notes); if (report.screenshots != null) Images.AddRange(report.screenshots);
            try
            {
                var snapshot = JsonUtility.FromJson<Snapshot>(SessionState.GetString(Key + ".snapshot", ""));
                Require(snapshot != null, "Original profile snapshot is missing.");
                if (snapshot.hadProfile) PlayerPrefs.SetString(OriginalProgression.SaveKey, snapshot.profile); else PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
                if (snapshot.hadGuide) PlayerPrefs.SetInt(GuideKey, snapshot.guide); else PlayerPrefs.DeleteKey(GuideKey);
                if (snapshot.hadCourier) PlayerPrefs.SetInt(CourierKey, snapshot.courier); else PlayerPrefs.DeleteKey(CourierKey);
                PlayerPrefs.Save();
                report.profileRestored = PlayerPrefs.HasKey(OriginalProgression.SaveKey) == snapshot.hadProfile && (!snapshot.hadProfile || PlayerPrefs.GetString(OriginalProgression.SaveKey) == snapshot.profile)
                    && PlayerPrefs.HasKey(GuideKey) == snapshot.hadGuide && (!snapshot.hadGuide || PlayerPrefs.GetInt(GuideKey) == snapshot.guide)
                    && PlayerPrefs.HasKey(CourierKey) == snapshot.hadCourier && (!snapshot.hadCourier || PlayerPrefs.GetInt(CourierKey) == snapshot.courier);
                Require(report.profileRestored, "Exact profile/guide/courier restoration failed.");
                GameViewHelper("RestoreGameView", null);
                Notes.Add("Original PlayerPrefs values and absent-key cases restored; input restored; pilot scene left open in Edit Mode.");
            }
            catch (Exception error) { Errors.Add("Restoration: " + error); }
            report.pass &= Errors.Count == 0 && report.profileRestored && report.inputRestored; report.state = report.pass ? "passed" : "failed";
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".finishing", false); Persist();
            Debug.Log("ISLAND_PILOT_PLAY_REVIEW_" + (report.pass ? "PASS" : "FAIL") + " " + ResultPath);
        }

        private static void Persist()
        {
            if (report == null) return;
            report.traceFile = "play-" + report.runId + "-trace.json";
            report.errors = Errors.ToArray(); report.notes = Notes.ToArray(); report.screenshots = Images.ToArray();
            if (BoundsSamples.Count > 0) report.boundsEvidence = BoundsSamples.ToArray();
            if (HoldSamples.Count > 0) report.fullHoldSamples = HoldSamples.ToArray();
            if (HoldHandoffs.Count > 0) report.nativeHoldHandoffs = HoldHandoffs.ToArray();
            if (TerrainFrames.Count > 0) report.terrainFrames = TerrainFrames.ToArray();
            string json = JsonUtility.ToJson(report, true); SessionState.SetString(Key + ".report", json);
            Directory.CreateDirectory(ReviewFolder); File.WriteAllText(ResultPath, json);
            PersistTrace();
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string text = message + "\n" + trace; Errors.Add(text);
            SessionState.SetString(Key + ".earlyErrors", SessionState.GetString(Key + ".earlyErrors", "") + text + "\n");
        }

        private static void GameViewHelper(string method, object[] arguments)
        {
            var member = typeof(RadioArchipelago.Editor.RadioEditorValidation).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
            if (member == null) throw new MissingMethodException("Existing GameView helper is unavailable: " + method);
            member.Invoke(null, arguments);
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
