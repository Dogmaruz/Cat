using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DancingCats.Clone.Editor
{
    // A local, allowlisted authoring job queue for an already open Editor.
    // No network endpoint, arbitrary code evaluation or player build commands.
    // Package setup is restricted to the explicit, pinned DI installation action.
    [InitializeOnLoad]
    public static class MusicalChartEditorSession
    {
        [Serializable] private sealed class Request { public string action; }
        [Serializable] private sealed class Response
        {
            public string action, message, scenePath, selectedSong, phase;
            public bool success, playing, paused, compiling, sceneDirty;
            public double clock;
        }

        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MusicalChartReview"));
        private static double nextPoll;

        static MusicalChartEditorSession()
        {
            EditorApplication.update += Poll;
            EditorApplication.delayCall += () => WriteResponse("inspect", true, "Editor authoring session available");
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            string path = Path.Combine(Folder, "request.json");
            if (!File.Exists(path)) return;
            string action = "unknown";
            try
            {
                var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
                File.Delete(path);
                action = request?.action;
                switch (action)
                {
                    case "hat-fitting-user-preflight":
                        RadioArchipelago.Editor.UserHatFitImport20261106.Preflight(); break;
                    case "hat-fitting-user-apply":
                        RadioArchipelago.Editor.UserHatFitImport20261106.Apply(); break;
                    case "hat-fitting-user-verify":
                        RadioArchipelago.Editor.UserHatFitImport20261106.Verify(); break;
                    case "hat-fitting-open":
                        RadioArchipelago.Editor.HatFittingWorkbench.CreateDefault(); break;
                    case "hat-fitting-roundtrip":
                        RadioArchipelago.Editor.HatFittingWorkbench.RoundTripProbe(); break;
                    case "hat-fitting-inventory":
                        RadioArchipelago.Editor.HatFittingWorkbench.ProbeStationInventory(); break;
                    case "hat-fitting-export":
                        RadioArchipelago.Editor.HatFittingWorkbench.ExportCurrent(); break;
                    case "hat-fitting-reopen-check":
                        RadioArchipelago.Editor.HatFittingPreparation.ReopenAndVerify(); break;
                    case "hat-fitting-retain-game-poses":
                        RadioArchipelago.Editor.HatFittingPreparation.RestorePriorGameHats(); break;
                    case "hat-fitting-axis-checks":
                        RadioArchipelago.Editor.HatFittingPreparation.CheckAxisScale(); break;
                    case "wardrobe-remove-diadem":
                        RadioArchipelago.Editor.WardrobeCatalogCuration20261105.RemoveDiadem(); break;
                    case "wardrobe-fit-before":
                        RadioArchipelago.Editor.WardrobeFitWorkshopReview.BeforePilot(); break;
                    case "wardrobe-fit-pilot-review":
                        RadioArchipelago.Editor.WardrobeFitWorkshopReview.AfterPilot(); break;
                    case "wardrobe-fit-all-review":
                        RadioArchipelago.Editor.WardrobeFitWorkshopReview.AfterAll(); break;
                    case "wardrobe-open-workshop":
                        RadioArchipelago.Editor.WardrobeFitWorkshopReview.OpenForUser(); break;
                    case "wardrobe-workshop-status":
                        RadioArchipelago.Editor.WardrobeFitWorkshopReview.WriteLiveStatus(); break;
                    case "wardrobe-curated-pilot":
                        RadioArchipelago.Editor.CuratedHatPoseAuthoring.ApplyPilot(); break;
                    case "wardrobe-curated-all":
                        RadioArchipelago.Editor.CuratedHatPoseAuthoring.ApplyAll(); break;
                    case "backpack-proportions-measure":
                        RadioArchipelago.Editor.BackpackProportionCorrection20261105.Measure(); break;
                    case "backpack-proportions-apply":
                        RadioArchipelago.Editor.BackpackProportionCorrection20261105.Run(); break;
                    case "backpack-icons-refresh":
                        RadioArchipelago.Editor.ModularCourierAssetBuilder.RefreshBackpackIcons(); break;
                    case "courier-motion-quick":
                        RadioArchipelago.Editor.CourierMotionShowcase.Quick(); break;
                    case "courier-motion-capture":
                        RadioArchipelago.Editor.CourierMotionShowcase.Capture(); break;
                    case "courier-motion-cancel":
                        RadioArchipelago.Editor.CourierMotionShowcase.Cancel(); break;
                    case "courier-hats-before":
                        RadioArchipelago.Editor.CourierHatReview.Baseline(); break;
                    case "courier-hats-after":
                        RadioArchipelago.Editor.CourierHatReview.Capture(); break;
                    case "courier-hats-quick":
                        RadioArchipelago.Editor.CourierHatReview.Quick(); break;
                    case "courier-hats-fit":
                        RadioArchipelago.Editor.ModularHatFitRepair.Run(); break;
                    case "courier-hat-scale-probe":
                        RadioArchipelago.Editor.ModularHatFitRepair.ProbeSkinScale(); break;
                    case "courier-hats-checks-before":
                        RadioArchipelago.Editor.CourierHatChecks.Baseline(); break;
                    case "courier-hats-checks":
                        RadioArchipelago.Editor.CourierHatChecks.Run(); break;
                    case "body-harness-install":
                        RadioArchipelago.Editor.BodyHarnessMigration.Install(); break;
                    case "body-harness-pilot":
                        RadioArchipelago.Editor.BodyHarnessMigration.Pilot(); break;
                    case "body-harness-author":
                        RadioArchipelago.Editor.BodyHarnessAuthoring.Run(); break;
                    case "body-harness-checks":
                        RadioArchipelago.Editor.BodyHarnessMigration.Validate(); break;
                    case "body-harness-preview":
                        RadioArchipelago.Editor.BodyHarnessMigration.Capture(); break;
                    case "body-harness-quick":
                        RadioArchipelago.Editor.BodyHarnessMigration.Quick(); break;
                    case "body-harness-only":
                        RadioArchipelago.Editor.BodyHarnessReview.HarnessOnly(); break;
                    case "body-harness-play":
                        RadioArchipelago.Editor.ModularWardrobePlayReview.RunInOpenEditor(); break;
                    case "finish-theme-baseline":
                        RadioArchipelago.Editor.IslandFinishThemeJobs.CaptureLegacyBaseline();
                        break;
                    case "finish-theme-install":
                        RadioArchipelago.Editor.IslandFinishThemeJobs.Install();
                        break;
                    case "finish-theme-checks":
                        RadioArchipelago.Editor.IslandFinishThemeJobs.RunChecks();
                        break;
                    case "waterfall-rollout-install":
                        RadioArchipelago.Editor.IslandWaterfallRolloutJobs.Install();
                        break;
                    case "waterfall-rollout-checks":
                        RadioArchipelago.Editor.IslandWaterfallRolloutJobs.RunChecks();
                        break;
                    case "lake-waterfall-coral-moon-install":
                        RadioArchipelago.Editor.IslandWaterfallRolloutJobs.InstallCoralAndMoon();
                        break;
                    case "lake-waterfall-coral-moon-checks":
                        RadioArchipelago.Editor.IslandWaterfallRolloutJobs.RunCoralAndMoonChecks();
                        break;
                    case "coral-lake-waterfall-install":
                        RadioArchipelago.Editor.IslandWaterfallRolloutJobs.InstallCoral();
                        break;
                    case "coral-lake-waterfall-checks":
                        RadioArchipelago.Editor.IslandWaterfallRolloutJobs.RunCoralChecks();
                        break;
                    case "coral-lake-join-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginIslandLakes(RadioArchipelago.Editor.CoralOasisIslandJobs.DefinitionPath,"coral-lakes");
                        break;
                    case "coral-lake-reference-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginReference(RadioArchipelago.Editor.CoralOasisIslandJobs.DefinitionPath,"coral-lake-reference","../Docs/Design/RadioArchipelago/IslandFinishesAndWaterfalls20261102/NativeWater/coral-lakes-20261102-104322-3306603/report.json");
                        break;
                    case "moon-lake-reference-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginReference(RadioArchipelago.Editor.MoonResonanceIslandJobs.DefinitionPath,"moon-lake-reference","../Docs/Design/RadioArchipelago/IslandFinishesAndWaterfalls20261102/NativeWater/moon-lakes-20261102-103712-4289039/report.json");
                        break;
                    case "amber-lake-reference-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginReference(RadioArchipelago.Editor.AmberWindsIslandJobs.DefinitionPath,"amber-lake-reference","../Docs/Design/RadioArchipelago/IslandFinishesAndWaterfalls20261102/NativeWater/amber-lakes-20261102-103740-4869623/report.json");
                        break;
                    case "amber-waterfall-reference-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginReference(RadioArchipelago.Editor.AmberWindsIslandJobs.DefinitionPath,"amber-body-reference","../Docs/Design/RadioArchipelago/IslandFinishesAndWaterfalls20261102/NativeWater/amber-art-20261102-102130-3018003/report.json");
                        break;
                    case "moon-lake-join-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginIslandLakes(RadioArchipelago.Editor.MoonResonanceIslandJobs.DefinitionPath,"moon-lakes");
                        break;
                    case "amber-lake-join-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginIslandLakes(RadioArchipelago.Editor.AmberWindsIslandJobs.DefinitionPath,"amber-lakes");
                        break;
                    case "coral-waterfall-visual-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginIsland(RadioArchipelago.Editor.CoralOasisIslandJobs.DefinitionPath,"coral");
                        break;
                    case "moon-waterfall-visual-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginIsland(RadioArchipelago.Editor.MoonResonanceIslandJobs.DefinitionPath,"moon");
                        break;
                    case "coral-lake-waterfall-art-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginIslandArt(RadioArchipelago.Editor.CoralOasisIslandJobs.DefinitionPath,"coral-art");
                        break;
                    case "moon-lake-waterfall-art-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginIslandArt(RadioArchipelago.Editor.MoonResonanceIslandJobs.DefinitionPath,"moon-art");
                        break;
                    case "amber-lake-waterfall-art-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.BeginIslandArt(RadioArchipelago.Editor.AmberWindsIslandJobs.DefinitionPath,"amber-art");
                        break;
                    case "radio-trio-publish-approved":
                        RadioArchipelago.Editor.RadioTrioChartAuthoring.PublishApproved();
                        break;
                    case "amber-create":
                        RadioArchipelago.Editor.AmberWindsIslandJobs.Prepare();
                        break;
                    case "amber-checks":
                        RadioArchipelago.Editor.AmberWindsIslandJobs.RunChecks();
                        break;
                    case "amber-open":
                        RadioArchipelago.Editor.AmberWindsIslandJobs.Open();
                        break;
                    case "amber-capture":
                        RadioArchipelago.Editor.AmberWindsIslandJobs.CaptureViews();
                        break;
                    case "amber-playtest":
                        IslandPilotPlayReview.BeginAmberWinds();
                        break;
                    case "amber-waterfall-visual-before":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.Begin("before");
                        break;
                    case "amber-waterfall-visual-after":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.Begin("after");
                        break;
                    case "amber-waterfall-visual-live":
                        RadioArchipelago.Editor.AmberWaterfallVisualReview.Begin();
                        break;
                    case "amber-roll-alpha-compare":
                        RadioArchipelago.Editor.LivingWaterReview.CaptureAmberAlphaComparison();
                        break;
                    case "amber-scenery-water-live":
                        RadioArchipelago.Editor.LivingWaterReview.StartAmberSceneryProof();
                        break;
                    case "amber-scenery-create":
                        RadioArchipelago.Editor.AmberWindsIslandJobs.PrepareScenery();
                        break;
                    case "amber-scenery-capture":
                        RadioArchipelago.Editor.AmberWindsIslandJobs.CaptureScenery();
                        break;
                    case "radio-trio-pcm":
                        RadioArchipelago.Editor.RadioTrioChartAuthoring.ExportPcm();
                        break;
                    case "radio-trio-candidate":
                        RadioArchipelago.Editor.RadioTrioChartAuthoring.GenerateBase();
                        break;
                    case "nori-no-pcm":
                        RadioArchipelago.Editor.NoriNoChartAuthoring.ExportPcm();
                        break;
                    case "nori-no-generate":
                        RadioArchipelago.Editor.NoriNoChartAuthoring.GenerateBase();
                        break;
                    case "moon-create":
                        RadioArchipelago.Editor.MoonResonanceIslandJobs.Prepare();
                        break;
                    case "moon-profile-diagnose":
                        RadioArchipelago.Editor.MoonResonanceIslandJobs.DiagnoseHeldProfile();
                        break;
                    case "moon-height-checks":
                        RadioArchipelago.Editor.MoonHeightPrecisionChecks.RunChecks();
                        break;
                    case "sparse-cloud-checks":
                        RadioArchipelago.Editor.SparseCloudStepChecks.RunChecks();
                        break;
                    case "coherent-cloud-checks":
                        RadioArchipelago.Editor.CoherentCloudShapeChecks.RunChecks();
                        break;
                    case "coherent-cloud-preview":
                        RadioArchipelago.Editor.CoherentCloudShapeChecks.CapturePreview();
                        break;
                    case "finish-departure-checks":
                        RadioArchipelago.Editor.IslandFinishDepartureChecks.RunChecks();
                        break;
                    case "island-menu-prepare":
                        RadioArchipelago.Editor.RadioIslandMenuJobs.Prepare();
                        break;
                    case "island-menu-checks":
                        RadioArchipelago.Editor.RadioIslandMenuJobs.RunChecks();
                        break;
                    case "island-menu-playtest":
                        RadioArchipelago.Editor.RadioIslandMenuLiveChecks.BeginReview();
                        break;
                    case "coral-sparse-clouds":
                        RadioArchipelago.Editor.MoonResonanceIslandJobs.ApplyCoralSparseClouds();
                        break;
                    case "full-support-lane-checks":
                        RadioArchipelago.Editor.IslandFullSupportLaneRepairChecks.RunChecks();
                        break;
                    case "island-cloud-stage-checks":
                        RadioArchipelago.Editor.IslandCloudStageChecks.RunChecks();
                        break;
                    case "moon-checks":
                        RadioArchipelago.Editor.MoonResonanceIslandJobs.RunChecks();
                        break;
                    case "rooftop-before":
                        RadioArchipelago.Editor.IslandRooftopJobs.CaptureBefore();
                        break;
                    case "rooftop-empty-bindings":
                        RadioArchipelago.Editor.IslandRooftopJobs.CaptureEmptyBindings();
                        break;
                    case "rooftop-install":
                        RadioArchipelago.Editor.IslandRooftopJobs.Install();
                        break;
                    case "rooftop-review":
                        RadioArchipelago.Editor.IslandRooftopJobs.Review();
                        break;
                    case "moon-capture":
                        RadioArchipelago.Editor.MoonResonanceIslandJobs.CaptureViews();
                        break;
                    case "moon-open":
                        RadioArchipelago.Editor.MoonResonanceIslandJobs.Open();
                        break;
                    case "moon-playtest":
                        IslandPilotPlayReview.BeginMoonResonance();
                        break;
                    case "measured-gap-checks":
                        RequireEditMode();
                        Invoke("PcmMeasuredGapRecoveryValidation", "RunChecks");
                        break;
                    case "tiko-ta-generator-checks":
                        RequireEditMode();
                        Invoke("PcmMeasuredGapRecoveryValidation", "RunChecks");
                        Invoke("PcmSparsePhraseAnalysisValidation", "RunChecks");
                        Invoke("AudioChartGeneratorValidation", "RunChecks");
                        break;
                    case "tiko-ta-pcm":
                        RadioArchipelago.Editor.TikoTaChartAuthoring.ExportPcm();
                        WriteResponse(action, true, "Tiko-Ta source PCM exported");
                        break;
                    case "tiko-ta-generate":
                        RadioArchipelago.Editor.TikoTaChartAuthoring.GenerateBase();
                        WriteResponse(action, true, "Tiko-Ta base chart generated");
                        break;
                    case "coral-tiko-ta":
                        RadioArchipelago.Editor.CoralOasisIslandJobs.InstallTikoTa();
                        WriteResponse(action, true, "Tiko-Ta installed on Coral Oasis");
                        break;
                    case "living-water-review":
                        RadioArchipelago.Editor.LivingWaterReview.Capture(); break;
                    case "living-water-live":
                        RadioArchipelago.Editor.LivingWaterReview.StartLiveProof(); break;
                    case "inspect": break;
                    case "lumi-export-pcm":
                        RequireEditMode();
                        RadioArchipelago.Editor.LumiLaiChartAuthoring.ExportPcm();
                        break;
                    case "lumi-generate-base":
                        RequireEditMode();
                        RadioArchipelago.Editor.LumiLaiChartAuthoring.GenerateBase();
                        break;
                    case "lumi-install-island":
                        RequireEditMode();
                        RadioArchipelago.Editor.LumiLaiIslandJobs.Install();
                        break;
                    case "lumi-island-checks":
                        RequireEditMode();
                        RadioArchipelago.Editor.LumiLaiIslandJobs.RunChecks();
                        break;
                    case "lumi-playability-checks":
                        RequireEditMode();
                        PcmPlayablePhraseGroupingChecks.RunChecks();
                        RadioArchipelago.Editor.IslandOpeningChecks.RunChecks();
                        RadioArchipelago.Editor.IslandAbyssChartAuthoringChecks.RunChecks();
                        RadioArchipelago.Editor.IslandDynamicSupportSpacingChecks.RunChecks();
                        break;
                    case "project-cleanup":
                        RequireEditMode();
                        RadioArchipelago.Editor.ProjectCleanupJobs.Apply();
                        break;
                    case "project-cleanup-checks":
                        RequireEditMode();
                        RadioArchipelago.Editor.ProjectCleanupJobs.RunChecks();
                        break;
                    case "open-current-island":
                        RequireEditMode();
                        RadioArchipelago.Editor.ProjectCleanupJobs.OpenCurrent();
                        break;
                    case "island-create":
                        RequireEditMode();
                        Invoke("IslandPilotJobs", "Create");
                        break;
                    case "island-fx-assets":
                        RequireEditMode();
                        Invoke("IslandPilotJobs", "PrepareReadableEffects");
                        break;
                    case "island-checks":
                        RequireEditMode();
                        Invoke("IslandPilotJobs", "RunChecks");
                        break;
                    case "island-projection-checks":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandProjectionChecks.RunChecks();
                        break;
                    case "island-wide-checks":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandWideInputChecks.RunChecks();
                        RadioArchipelago.Editor.IslandProjectionChecks.RunChecks();
                        RadioArchipelago.Editor.IslandLivingPassChecks.RunChecks();
                        RadioArchipelago.Editor.ProjectCleanupJobs.RunChecks();
                        break;
                    case "island-terrain-baseline":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandTerrainClearanceDiagnostics.RunBaseline();
                        break;
                    case "island-follow-sightlines":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandTerrainClearanceDiagnostics.RunSightlines();
                        break;
                    case "island-glade-overlap":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandFinishGladeOverlapChecks.RunGladeOverlap();
                        break;
                    case "island-wide-playtest":
                        RequireEditMode();
                        IslandPilotPlayReview.BeginWideIsland();
                        break;
                    case "island-nori-baseline":
                        RequireEditMode();
                        IslandPilotPlayReview.BeginNoriAbyssBaseline();
                        break;
                    case "island-nori-playtest":
                        RequireEditMode();
                        IslandPilotPlayReview.BeginNoriAbyss();
                        break;
                    case "island-restore-interrupted-review":
                        RequireEditMode();
                        // Recover the saved user preferences before restarting
                        // an Editor whose temporary input state was interrupted.
                        typeof(IslandPilotPlayReview).GetMethod("Restore", BindingFlags.Static | BindingFlags.NonPublic)
                            .Invoke(null, null);
                        break;
                    case "signal-spark-assets":
                        RequireEditMode();
                        RadioArchipelago.Editor.SignalSparkAuthoring.EnsureAssets();
                        RadioArchipelago.Editor.SignalSparkAuthoring.RunChecks();
                        RadioArchipelago.Editor.SignalSparkAuthoring.CapturePreview();
                        break;
                    case "island-finish-survey":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandFinishPresentationChecks.Survey();
                        break;
                    case "island-finish-checks":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandFinishPresentationChecks.RunChecks();
                        RadioArchipelago.Editor.IslandFinishFlowerChecks.Run();
                        RadioArchipelago.Editor.CourierFinishHoverChecks.Run();
                        break;
                    case "coral-create":
                        RequireEditMode();
                        RadioArchipelago.Editor.CoralOasisIslandJobs.Prepare();
                        break;
                    case "coral-checks":
                        RequireEditMode();
                        RadioArchipelago.Editor.CoralOasisIslandJobs.RunChecks();
                        break;
                    case "coral-capture":
                        RequireEditMode();
                        RadioArchipelago.Editor.CoralOasisIslandJobs.CaptureViews();
                        break;
                    case "coral-open":
                        RequireEditMode();
                        RadioArchipelago.Editor.CoralOasisIslandJobs.Open();
                        break;
                    case "coral-playtest":
                        RequireEditMode();
                        IslandPilotPlayReview.BeginCoralOasis();
                        break;
                    case "island-camera-stability":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandCameraStabilityChecks.RunChecks();
                        break;
                    case "island-lighting-inspect":
                        RequireEditMode();
                        Invoke("IslandLightingDiagnostics", "InspectAssets");
                        break;
                    case "island-playtest":
                        RequireEditMode();
                        Invoke("IslandPilotPlayReview", "Begin");
                        break;
                    case "island-playtest-lumen":
                        RequireEditMode();
                        IslandPilotPlayReview.BeginLumen();
                        break;
                    case "island-relief-create":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandReliefJobs.Create();
                        break;
                    case "island-relief-checks":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandReliefJobs.RunChecks();
                        break;
                    case "island-relief-playtest":
                        RequireEditMode();
                        IslandPilotPlayReview.BeginRelief();
                        break;
                    case "island-abyss-create":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandAbyssJobs.Create();
                        break;
                    case "island-abyss-checks":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandAbyssJobs.RunChecks();
                        break;
                    case "island-abyss-capture":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandAbyssJobs.CaptureViews();
                        break;
                    case "island-living-checks":
                        RequireEditMode();
                        RadioArchipelago.Editor.IslandLivingPassChecks.RunChecks();
                        break;
                    case "island-living-playtest":
                        RequireEditMode();
                        Invoke("IslandPilotPlayReview", "BeginLivingPass");
                        break;
                    case "island-fx-probe":
                        RadioArchipelago.Editor.IslandHoldFxProbe.Begin();
                        break;
                    case "radio-hop-baseline": Invoke("RadioArchipelagoJobs", "HopBaseline"); break;
                    case "radio-hop-candidate": Invoke("RadioArchipelagoJobs", "HopCandidate"); break;
                    case "radio-hop-checks": Invoke("RadioArchipelagoJobs", "HopChecks"); break;
                    case "refresh":
                        RequireEditMode();
                        AssetDatabase.Refresh();
                        break;
                    case "stop-play":
                        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                        break;
                    case "generate":
                        RequireEditMode();
                        Invoke("MeowLevelValidation", "GenerateAndVerify");
                        break;
                    case "validate":
                        RequireEditMode();
                        Invoke("MeowLevelValidation", "RunChecks");
                        break;
                    case "playtest":
                        RequireEditMode();
                        Invoke("IntegratedPlayValidation", "RunCustomSongInOpenEditor");
                        break;
                    case "install-di":
                        RequireEditMode();
                        Invoke("DependencyInjectionPackageSetup", "Install");
                        break;
                    case "di-migrate":
                        RequireEditMode();
                        Invoke("DependencyInjectionValidation", "MigrateScene");
                        break;
                    case "di-checks":
                        RequireEditMode();
                        Invoke("DependencyInjectionValidation", "RunChecks");
                        break;
                    case "di-menus":
                        RequireEditMode();
                        Invoke("IntegratedPlayValidation", "RunDependencyInjectionMenusInOpenEditor");
                        break;
                    case "radio-assets":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "CreateAssets");
                        break;
                    case "radio-migrate":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "MigrateScene");
                        break;
                    case "radio-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RunChecks");
                        break;
                    case "radio-playtest":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "Playtest");
                        break;
                    case "radio-menu-preview-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "MenuPreviewChecks");
                        break;
                    case "radio-menu-bake-probe":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "MenuBakeProbe");
                        break;
                    case "radio-lighting-assets":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "LightingAssets");
                        break;
                    case "radio-lighting-reflections":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RefreshLightingReflections");
                        break;
                    case "radio-motion-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "MotionChecks");
                        break;
                    case "radio-import-couriers":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "ImportCouriers");
                        break;
                    case "radio-rig-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RigChecks");
                        break;
                    case "radio-rigid-paw-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RigidPawChecks");
                        break;
                    case "radio-surface-baseline":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "SurfaceBaseline");
                        break;
                    case "radio-surface-candidate":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "SurfaceCandidate");
                        break;
                    case "radio-rounded-paw-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RoundedPawChecks");
                        break;
                    case "radio-joint-baseline":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "JointBaseline");
                        break;
                    case "radio-joint-candidate":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "JointCandidate");
                        break;
                    case "radio-shoulder-trials":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "ShoulderTrials");
                        break;
                    case "radio-accessory-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "AccessoryChecks");
                        break;
                    case "radio-original-paw-study":
                        RequireEditMode();
                        Invoke("OriginalPawMotionStudy", "Run");
                        break;
                    case "chart-original-baseline":
                        RequireEditMode();
                        Invoke("OriginalChartCalibration", "RunBaseline");
                        break;
                    case "chart-original-checks":
                        RequireEditMode();
                        Invoke("OriginalChartCalibration", "RunChecks");
                        break;
                    case "chart-original-measure":
                        RequireEditMode();
                        Invoke("OriginalChartCalibration", "RunCandidate");
                        break;
                    case "chart-original-export-pcm":
                        RequireEditMode();
                        Invoke("OriginalChartCalibration", "ExportPcmForAnalysis");
                        break;
                    case "chart-custom-export-pcm":
                        RequireEditMode();
                        Invoke("AudioChartGenerator", "ExportAlwaysRememberPcm");
                        break;
                    case "chart-custom-publish":
                        RequireEditMode();
                        Invoke("AudioChartGenerator", "RegenerateAlwaysRememberFromAudio");
                        break;
                    case "chart-flight-checks":
                        RequireEditMode();
                        Invoke("PcmFlightAnalysisValidation", "RunChecks");
                        break;
                    case "chart-sparse-checks":
                        RequireEditMode();
                        Invoke("PcmSparsePhraseAnalysisValidation", "RunChecks");
                        break;
                    case "chart-custom-spectral":
                        RequireEditMode();
                        Invoke("AudioChartGenerator", "ExportAlwaysRememberSpectralEvidence");
                        break;
                    case "chart-benchmark-export-calibration":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "ExportCalibrationPcm");
                        break;
                    case "chart-benchmark-export-validation":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "ExportValidationPcm");
                        break;
                    case "chart-benchmark-baseline":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "RunCalibrationBaseline");
                        break;
                    case "chart-evidence-checks":
                        RequireEditMode();
                        Invoke("PcmVocalEvidenceValidation", "RunChecks");
                        break;
                    case "chart-foreground-checks":
                        RequireEditMode();
                        Invoke("PcmForegroundAnalysisValidation", "RunChecks");
                        break;
                    case "chart-rapid-checks":
                        RequireEditMode();
                        Invoke("PcmRapidArticulationAnalysisValidation", "RunChecks");
                        break;
                    case "chart-benchmark-candidate":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "RunCalibrationCandidate");
                        break;
                    case "chart-validation-baseline":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "RunFrozenBaseline");
                        break;
                    case "chart-validation-candidate":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "RunFrozenCandidate");
                        break;
                    case "chart-original-publish":
                        RequireEditMode();
                        Invoke("OriginalChartCalibration", "GenerateUnstoppableComparison");
                        break;
                    case "chart-route-checks":
                        RequireEditMode();
                        Invoke("ChartRouteComposerValidation", "RunChecks");
                        break;
                    case "chart-v9-export-calibration":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "ExportCalibrationPcm");
                        break;
                    case "chart-v9-export-frozen":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "ExportFrozenPcm");
                        break;
                    case "chart-v9-baseline-calibration":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunCalibrationBaseline");
                        break;
                    case "chart-v9-baseline-frozen":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunFrozenBaseline");
                        break;
                    case "chart-v9-baseline-regression":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunRegressionBaseline");
                        break;
                    case "chart-v9-calibration":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunCalibrationCandidate");
                        break;
                    case "chart-v9-frozen":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunFrozenCandidate");
                        break;
                    case "chart-v9-regression":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunRegressionCandidate");
                        break;
                    case "chart-v9-metric-checks":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunMetricChecks");
                        break;
                    case "chart-short-native-checks":
                        RequireEditMode();
                        Invoke("ShortNativeSupportValidation", "RunChecks");
                        break;
                    case "chart-instrument-evidence-checks":
                        RequireEditMode();
                        Invoke("PcmInstrumentalEvidenceValidation", "RunChecks");
                        break;
                    case "chart-melodic-follow-checks":
                        RequireEditMode();
                        Invoke("PcmMelodicFollowValidation", "RunChecks");
                        break;
                    case "chart-short-support-checks":
                        RequireEditMode();
                        Invoke("PcmShortSupportAnalysisValidation", "RunChecks");
                        break;
                    case "chart-instrument-integration":
                        RequireEditMode();
                        Invoke("InstrumentalChartIntegrationValidation", "RunChecks");
                        break;
                    case "chart-v9-publication-guards":
                        RequireEditMode();
                        Invoke("ChartTestPublicationV9", "RunGuardChecks");
                        break;
                    case "chart-v9-publish-tests":
                        RequireEditMode();
                        Invoke("ChartTestPublicationV9", "PublishTests");
                        break;
                    case "chart-v9-play-disconnect":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "V9DisconnectPlaytest");
                        break;
                    case "chart-v9-play-cancan":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "V9CanCanPlaytest");
                        break;
                    case "chart-v9-play-dance":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "V9DancePlaytest");
                        break;
                    case "chart-v9-play-clouds":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "V9CloudsPlaytest");
                        break;
                    case "chart-route-integration":
                        RequireEditMode();
                        Invoke("ChartRouteIntegrationValidation", "RunChecks");
                        break;
                    case "chart-route-publish":
                        RequireEditMode();
                        Invoke("ChartRouteJobs", "ApplyAllCustomLevels");
                        break;
                    case "chart-route-real-audio":
                        RequireEditMode();
                        Invoke("ChartRouteIntegrationValidation", "CheckPublishedGeneration");
                        break;
                    case "chart-native-moving-checks":
                        RequireEditMode();
                        Invoke("NativeMovingTileValidation", "RunChecks");
                        break;
                    case "chart-original-playtest":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "ComparisonPlaytest");
                        break;
                    case "radio-rig-render":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RigRender");
                        break;
                    case "radio-animation-preview":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "OpenAnimationPreview");
                        break;
                    case "radio-animation-reel":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "AnimationReel");
                        break;
                    case "radio-first-courier":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "FirstCourierProbe");
                        break;
                    case "radio-open-scene":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "OpenGameScene");
                        break;
                    case "radio-show-menu":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "ShowMainMenu");
                        break;
                    case "radio-select-custom-song":
                        Invoke("RadioArchipelagoJobs", "SelectCustomSongPreview");
                        break;
                    case "chart-v9-preview-cancan":
                        Invoke("RadioArchipelagoJobs", "SelectV9CanCanPreview");
                        break;
                    case "chart-v10-export": Invoke("PcmChartStudyV10", "ExportNewPcm"); break;
                    case "chart-v10-freeze-checks": Invoke("PcmChartStudyV10", "RunFreezeChecks"); break;
                    case "chart-v10-baseline-known": Invoke("PcmChartStudyV10", "BaselineKnown"); break;
                    case "chart-v10-baseline-heldout": Invoke("PcmChartStudyV10", "BaselineHeldout"); break;
                    case "chart-v10-known": Invoke("PcmChartStudyV10", "CandidateKnown"); break;
                    case "chart-v10-heldout": Invoke("PcmChartStudyV10", "CandidateHeldout"); break;
                    case "chart-v10-publication-guards": Invoke("ChartTestPublicationV10", "RunGuardChecks"); break;
                    case "chart-v10-publish-tests": Invoke("ChartTestPublicationV10", "PublishTests"); break;
                    case "chart-v10-play-disconnect": Invoke("RadioArchipelagoJobs", "V10DisconnectPlaytest"); break;
                    case "chart-v10-play-cancan": Invoke("RadioArchipelagoJobs", "V10CanCanPlaytest"); break;
                    case "chart-v10-play-dance": Invoke("RadioArchipelagoJobs", "V10DancePlaytest"); break;
                    case "chart-v10-play-moonlight": Invoke("RadioArchipelagoJobs", "V10MoonlightPlaytest"); break;
                    case "chart-v10-play-elise": Invoke("RadioArchipelagoJobs", "V10ElisePlaytest"); break;
                    case "chart-v10-play-jingle": Invoke("RadioArchipelagoJobs", "V10JinglePlaytest"); break;
                    case "chart-v10-preview-cancan": Invoke("RadioArchipelagoJobs", "SelectV10CanCanPreview"); break;
                    case "chart-study-export": Invoke("ChartGeneratorStudy", "ExportPcm"); break;
                    case "chart-study-baseline-known": Invoke("ChartGeneratorStudy", "BaselineKnown"); break;
                    case "chart-study-baseline-heldout": Invoke("ChartGeneratorStudy", "BaselineHeldout"); break;
                    case "chart-study-known": Invoke("ChartGeneratorStudy", "CandidateKnown"); break;
                    case "chart-study-heldout": Invoke("ChartGeneratorStudy", "CandidateHeldout"); break;
                    case "chart-study-publish": Invoke("ChartGeneratorStudy", "PublishTests"); break;
                    case "chart-study-checks": Invoke("ChartGeneratorStudy", "RunChecks"); break;
                    case "chart-study-precision": Invoke("ChartStudyPrecisionDiagnostics", "Run"); break;
                    case "rhythm-timing-inspect": Invoke("RhythmTimingDiagnostics", "InspectLiveTiming"); break;
                    case "rhythm-timing-original": Invoke("RhythmTimingDiagnostics", "BeginOriginalJingle"); break;
                    case "rhythm-timing-v11": Invoke("RhythmTimingDiagnostics", "BeginV11Jingle"); break;
                    case "rhythm-timing-stop": Invoke("RhythmTimingDiagnostics", "StopRecording"); break;
                    case "rhythm-timing-512": Invoke("RhythmTimingDiagnostics", "Apply512ForComparison"); break;
                    case "rhythm-timing-v11-512": Invoke("RhythmTimingDiagnostics", "BeginV11Jingle512"); break;
                    case "rhythm-timing-restore": Invoke("RhythmTimingDiagnostics", "RestoreBufferConfiguration"); break;
                    case "rhythm-timing-serialization": Invoke("RhythmTimingDiagnostics", "RunSerializationChecks"); break;
                    case "rhythm-timing-v11-512-pose": Invoke("RhythmTimingDiagnostics", "BeginV11Jingle512Pose"); break;
                    case "rhythm-timing-apply-project": Invoke("RhythmTimingDiagnostics", "ApplyProjectBufferSettings"); break;
                    case "chart-legacy-support-checks": Invoke("PcmLegacySupportValidation", "RunChecks"); break;
                    case "chart-voiced-response-checks": Invoke("PcmVoicedResponseValidation", "RunChecks"); break;
                    case "chart-short-motif-checks": Invoke("PcmShortMotifValidation", "RunChecks"); break;
                    case string studyAction when studyAction.StartsWith("chart-study-play:", StringComparison.Ordinal):
                        InvokeString("RadioArchipelagoJobs", "StudyPlaytest", studyAction.Substring("chart-study-play:".Length)); break;
                    case string studyAction when studyAction.StartsWith("chart-study-reference:", StringComparison.Ordinal):
                        InvokeString("RadioArchipelagoJobs", "StudyReferencePlaytest", studyAction.Substring("chart-study-reference:".Length)); break;
                    case string studyAction when studyAction.StartsWith("chart-study-preview:", StringComparison.Ordinal):
                        InvokeString("RadioArchipelagoJobs", "StudyPreview", studyAction.Substring("chart-study-preview:".Length)); break;
                    case "chart-v11-play-cancan": Invoke("RadioArchipelagoJobs", "V11CanCanPlaytest"); break;
                    case "chart-v11-play-elise": Invoke("RadioArchipelagoJobs", "V11ElisePlaytest"); break;
                    case "chart-v11-play-jingle": Invoke("RadioArchipelagoJobs", "V11JinglePlaytest"); break;
                    case "chart-v11-play-ode": Invoke("RadioArchipelagoJobs", "V11OdePlaytest"); break;
                    case "chart-v11-play-bajo": Invoke("RadioArchipelagoJobs", "V11BajoPlaytest"); break;
                    case "chart-v11-play-baby": Invoke("RadioArchipelagoJobs", "V11BabyPlaytest"); break;
                    case "chart-v11-preview-jingle": Invoke("RadioArchipelagoJobs", "SelectV11JinglePreview"); break;
                    default: throw new ArgumentException("Unknown musical authoring job: " + action);
                }
                WriteResponse(action, true, "Completed authoring job");
            }
            catch (Exception error)
            {
                WriteResponse(action, false, (error.InnerException ?? error).ToString());
                Debug.LogError("MUSICAL_CHART_EDITOR: " + (error.InnerException ?? error).Message);
            }
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before changing or validating the authored map.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; save it before running authoring jobs.");
        }

        private static void Invoke(string typeName, string methodName)
        {
            var type = typeof(MusicalChartEditorSession).Assembly.GetType("DancingCats.Clone.Editor." + typeName, true);
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method == null) throw new MissingMethodException(typeName, methodName);
            method.Invoke(null, null);
        }

        private static void InvokeString(string typeName, string methodName, string value)
        {
            var type = typeof(MusicalChartEditorSession).Assembly.GetType("DancingCats.Clone.Editor." + typeName, true);
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (method == null) throw new MissingMethodException(typeName, methodName);
            method.Invoke(null, new object[] { value });
        }

        private static void WriteResponse(string action, bool success, string message)
        {
            var scene = SceneManager.GetActiveScene();
            var controller = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            var response = new Response
            {
                action = action, success = success, message = message,
                playing = EditorApplication.isPlaying, paused = EditorApplication.isPaused,
                compiling = EditorApplication.isCompiling, scenePath = scene.path, sceneDirty = scene.isDirty,
                selectedSong = controller != null ? controller.Selected?.id : "",
                phase = controller != null ? controller.Phase.ToString() : "", clock = controller != null ? controller.GameTime : 0
            };
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "response.json"), JsonUtility.ToJson(response, true));
        }
    }
}
