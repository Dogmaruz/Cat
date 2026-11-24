from pathlib import Path
root=Path.cwd(); folder=Path(__file__).parent; stage=folder/'UnityStaging'
p=root/'DancingCatsClone/Assets/RadioArchipelago/Editor/LivingWaterReview.cs'
s=p.read_text(encoding='utf8'); (folder/'Before/LivingWaterReview.before-amber-scenery.cs.txt').write_text(s,encoding='utf8')
s=s.replace('''        private const string SceneKey = ClockKey + ".PreviousScene";''','''        private const string SceneKey = ClockKey + ".PreviousScene";
        private const string AmberKey = ClockKey + ".AmberSourceScenery", PhaseKey = ClockKey + ".Phase";
        private static bool AmberLive => SessionState.GetBool(AmberKey,false);
        private static string LiveFolder => AmberLive ? AmberWindsIslandJobs.ReviewFolder + "/WaterNative" : Folder;
        private static string LiveTag => AmberLive ? (SessionState.GetInt(PhaseKey,0) == 0 ? "stream" : "waterfall") : "live";''')
s=s.replace('''        [Serializable] private sealed class LiveReport { public bool pass; public string state, scope; public float firstTime, lastTime; public int movingPixels, coveredPixels; public bool defaultClock, originalSceneRestored; }''','''        [Serializable] private sealed class FlowProperties { public string material; public float useFlowUv,currentSpeed,flowSpeed,fall,previewTime; }
        [Serializable] private sealed class LiveReport { public bool pass; public string state, scope, feature; public float firstTime, lastTime; public int movingPixels, coveredPixels; public bool defaultClock, originalSceneRestored; public FlowProperties[] materialProperties; }
        [Serializable] private sealed class AmberLiveReport { public bool pass, sourceSceneryOnly, originalSceneRestored; public LiveReport[] features; }''')
s=s.replace('''internal View(IslandStageDefinition definition, bool waterfall, bool preview)''','''internal View(IslandStageDefinition definition, bool waterfall, bool preview, string measuredKind = null)''')
s=s.replace('''                if (waters.Length == 0) throw new InvalidOperationException("The actual island prefab has no living water: " + definition.name);''','''                if (!string.IsNullOrEmpty(measuredKind))
                    waters = waters.Where(r => r.sharedMaterials.Any(m => m && m.name.IndexOf(measuredKind,StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
                if (waters.Length == 0) throw new InvalidOperationException("The actual island prefab has no living water: " + definition.name + " / " + measuredKind);''')
start=s.index('''        [MenuItem("Tools/Radio Archipelago/Verify native water clock")]''')
new='''        [MenuItem("Tools/Radio Archipelago/Verify native water clock")]
        public static void StartLiveProof() => BeginLiveProof(false);

        [MenuItem("Tools/Radio Archipelago/Verify Amber source-scenery water clock")]
        public static void StartAmberSceneryProof() => BeginLiveProof(true);

        private static void BeginLiveProof(bool amber)
        {
            IslandReliefJobs.RequireEditMode();
            if (SceneManager.GetActiveScene().isDirty || SessionState.GetBool(ClockKey,false))
                throw new InvalidOperationException("Native water proof requires a clean current scene and no active water review.");
            SessionState.SetBool(AmberKey,amber); SessionState.SetInt(PhaseKey,0);
            Directory.CreateDirectory(LiveFolder); SessionState.SetString(SceneKey,SceneManager.GetActiveScene().path);
            SessionState.SetBool(ClockKey,true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ClockKey,false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) { started = EditorApplication.timeSinceStartup; firstLive = null; liveFrame = 0; }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                string folder = LiveFolder; bool amber = AmberLive;
                SessionState.SetBool(ClockKey,false);
                string previous = SessionState.GetString(SceneKey,"");
                if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous,OpenSceneMode.Single);
                string path = folder + "/live-clock.json";
                if (File.Exists(path))
                {
                    if (amber)
                    {
                        var report = JsonUtility.FromJson<AmberLiveReport>(File.ReadAllText(path));
                        report.originalSceneRestored = true; File.WriteAllText(path,JsonUtility.ToJson(report,true));
                    }
                    else
                    {
                        var report = JsonUtility.FromJson<LiveReport>(File.ReadAllText(path));
                        report.originalSceneRestored = true; File.WriteAllText(path,JsonUtility.ToJson(report,true));
                    }
                }
                SessionState.EraseBool(AmberKey); SessionState.EraseInt(PhaseKey); SessionState.EraseString(SceneKey);
            }
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(ClockKey,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 60) throw new TimeoutException("Native water clock proof timed out.");
                if (Time.timeSinceLevelLoad < .25f) return;
                if (live == null)
                {
                    IslandStageDefinition definition = AmberLive ? AmberWindsIslandJobs.CreateSceneryDefinition() :
                        AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(CoralOasisIslandJobs.DefinitionPath);
                    try
                    {
                        if (AmberLive && (definition.Chart != null || !string.IsNullOrEmpty(definition.SongId)))
                            throw new InvalidOperationException("Amber water art proof must not bind a song or gameplay chart.");
                        bool fall = AmberLive && SessionState.GetInt(PhaseKey,0) == 1;
                        live = new View(definition,fall,false,AmberLive ? (fall ? "Waterfall" : "Stream") : null);
                        live.UseGameplayCamera(definition);
                        if (live.waters.SelectMany(r => r.sharedMaterials).Any(m => m.GetFloat(PreviewTime) >= 0))
                            throw new InvalidOperationException("A saved material overrides the native water clock.");
                        if (AmberLive)
                            foreach (var material in live.waters.SelectMany(r => r.sharedMaterials).Distinct())
                            {
                                if ((!fall && (Mathf.Abs(material.GetFloat("_UseFlowUV")-1) > .001f || Mathf.Abs(material.GetFloat("_CurrentSpeed")-4.2f) > .001f)) ||
                                    (fall && material.GetFloat("_Fall") < .5f))
                                    throw new InvalidOperationException("Amber native stream/fall flow configuration differs from its source UV contract.");
                            }
                    }
                    finally { if (AmberLive) Object.DestroyImmediate(definition); }
                    firstLive = live.Capture(LiveFolder + "/" + LiveTag + "-clock-early.png"); firstLiveTime = Time.time;
                }
                if (Time.time-firstLiveTime >= liveFrame*.10f && liveFrame < 14)
                {
                    live.Capture(LiveFolder + "/" + LiveTag + "-flow-" + liveFrame.ToString("00") + ".png"); liveFrame++;
                }
                if (Time.time - firstLiveTime < 1.3f) return;
                var last = live.Capture(LiveFolder + "/" + LiveTag + "-clock-late.png"); var mask = live.VisibleWaterMask();
                Changed(firstLive,last,mask,out int moving,out int covered);
                var report = new LiveReport { pass = moving > 100 && covered > 500, state = "complete", feature = LiveTag,
                    firstTime = firstLiveTime, lastTime = Time.time, movingPixels = moving, coveredPixels = covered, defaultClock = true,
                    scope = "Real native Play Mode _Time, untouched saved materials, no preview override. Real route camera rig/FOV in portrait; isolated scenery-only scene, no game/profile/audio controllers. Visible-water mask measures only the selected water category and excludes terrain/foliage occlusion.",
                    materialProperties = live.waters.SelectMany(r => r.sharedMaterials).Distinct().Select(m => new FlowProperties {
                        material = m.name,useFlowUv = m.GetFloat("_UseFlowUV"),currentSpeed = m.GetFloat("_CurrentSpeed"),
                        flowSpeed = m.GetFloat("_FlowSpeed"),fall = m.GetFloat("_Fall"),previewTime = m.GetFloat(PreviewTime) }).ToArray() };
                File.WriteAllText(LiveFolder + "/" + (AmberLive ? LiveTag + "-clock.json" : "live-clock.json"),JsonUtility.ToJson(report,true));
                live.Dispose(); live = null; firstLive = null;
                if (AmberLive && SessionState.GetInt(PhaseKey,0) == 0)
                {
                    SessionState.SetInt(PhaseKey,1); liveFrame = 0;
                    return;
                }
                if (AmberLive)
                {
                    var stream = JsonUtility.FromJson<LiveReport>(File.ReadAllText(LiveFolder + "/stream-clock.json"));
                    File.WriteAllText(LiveFolder + "/live-clock.json",JsonUtility.ToJson(new AmberLiveReport {
                        pass = stream.pass && report.pass,sourceSceneryOnly = true,features = new[] { stream,report } },true));
                }
                EditorApplication.ExitPlaymode();
            }
            catch (Exception error)
            {
                if (live != null) { live.Dispose(); live = null; }
                if (AmberLive)
                    File.WriteAllText(LiveFolder + "/live-clock.json",JsonUtility.ToJson(new AmberLiveReport { sourceSceneryOnly = true,
                        features = new[] { new LiveReport { state = "failed",feature = LiveTag,scope = error.ToString() } } },true));
                else File.WriteAllText(LiveFolder + "/live-clock.json",JsonUtility.ToJson(new LiveReport { state = "failed", scope = error.ToString() },true));
                EditorApplication.ExitPlaymode(); Debug.LogException(error);
            }
        }
    }
}
'''
s=s[:start]+new
(stage/'LivingWaterReview.cs').write_text(s,encoding='utf8')
p=stage/'AmberWindsIslandJobs.cs'; s=p.read_text(encoding='utf8').replace('private static IslandStageDefinition CreateSceneryDefinition()','internal static IslandStageDefinition CreateSceneryDefinition()'); p.write_text(s,encoding='utf8')
print('Staged shared native-clock helper with explicit Amber source-only stream/fall phases and Coral default retained.')
