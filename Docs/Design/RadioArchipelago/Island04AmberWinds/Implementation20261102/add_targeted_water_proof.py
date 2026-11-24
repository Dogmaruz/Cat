from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'LivingWaterReview.cs'; s=p.read_text(encoding='utf8')
s=s.replace('''        private static string LiveTag => AmberLive ? (SessionState.GetInt(PhaseKey,0) == 0 ? "stream" : "waterfall") : "live";''','''        private static string LiveTag => AmberLive ? AmberPhaseTags[SessionState.GetInt(PhaseKey,0)] : "live";
        private static readonly string[] AmberPhaseTags = { "stream", "roll210", "side-roll", "waterfall" };
        private const string SideCascadePrefix = "RA_Amber_SideCascade_";
        private static readonly Vector3[] SideCascadeTargets = { new Vector3(47,28,-96),new Vector3(49,20,-96),new Vector3(49,12,-96) };''')
s=s.replace('''internal View(IslandStageDefinition definition, bool waterfall, bool preview, string measuredKind = null)''','''internal View(IslandStageDefinition definition, bool waterfall, bool preview, string measuredKind = null, string measuredNodePrefix = null)''')
s=s.replace('''                if (waters.Length == 0) throw new InvalidOperationException''','''                if (!string.IsNullOrEmpty(measuredNodePrefix))
                    waters = waters.Where(r => r.name.StartsWith(measuredNodePrefix,StringComparison.Ordinal)).ToArray();
                if (waters.Length == 0) throw new InvalidOperationException''',1)
s=s.replace('''            internal int width = 960, height = 640;''','''            internal int width = 960, height = 640;
            internal float selectedStation;''')
s=s.replace('''            internal void UseGameplayCamera(IslandStageDefinition definition)''','''            internal void UseGameplayCamera(IslandStageDefinition definition, float fixedStation = -1)''')
s=s.replace('''                var candidates = new List<(float score, IslandCloseCameraRig.Pose pose)>();''','''                if (fixedStation >= 0)
                {
                    var fixedPose = rig.Step(route,fixedStation,0,definition.CameraBackDistance,definition.CameraHeight,
                        definition.CameraLookAhead,definition.CameraFocusHeight,definition.CameraHeadingLead,definition.CameraSmoothing,0,true);
                    camera.transform.SetPositionAndRotation(fixedPose.Position,fixedPose.Rotation);
                    selectedStation = fixedStation;
                    return;
                }
                var candidates = new List<(float score, float station, IslandCloseCameraRig.Pose pose)>();''')
s=s.replace('''                    candidates.Add((score,pose));''','''                    candidates.Add((score,station,pose));''')
s=s.replace('''                var shortlist = candidates.OrderBy(c => c.score).Take(8).Select(c => c.pose).ToList();''','''                var shortlist = candidates.OrderBy(c => c.score).Take(8).Select(c => (c.station,c.pose)).ToList();''')
s=s.replace('''                    shortlist.Add(rig.Step(route,route.Length*i/32,0,definition.CameraBackDistance,definition.CameraHeight,
                        definition.CameraLookAhead,definition.CameraFocusHeight,definition.CameraHeadingLead,
                        definition.CameraSmoothing,0,true));''','''                    shortlist.Add((route.Length*i/32,rig.Step(route,route.Length*i/32,0,definition.CameraBackDistance,definition.CameraHeight,
                        definition.CameraLookAhead,definition.CameraFocusHeight,definition.CameraHeadingLead,
                        definition.CameraSmoothing,0,true)));''')
s=s.replace('''                    var pose = shortlist[i];''','''                    var pose = shortlist[i].pose;''')
s=s.replace('''                var chosen = shortlist[best];
                camera.transform.SetPositionAndRotation(chosen.Position,chosen.Rotation);''','''                var chosen = shortlist[best]; selectedStation = chosen.station;
                camera.transform.SetPositionAndRotation(chosen.pose.Position,chosen.pose.Rotation);''')
s=s.replace('''public float firstTime, lastTime;''','''public float firstTime, lastTime, cameraStation;''',1)
s=s.replace('''                        bool fall = AmberLive && SessionState.GetInt(PhaseKey,0) == 1;
                        live = new View(definition,fall,false,AmberLive ? (fall ? "Waterfall" : "Stream") : null);
                        live.UseGameplayCamera(definition);''','''                        int phase = SessionState.GetInt(PhaseKey,0);
                        bool fall = AmberLive && phase == AmberPhaseTags.Length-1;
                        live = new View(definition,fall,false,AmberLive ? (fall ? "Waterfall" : "Stream") : null,
                            AmberLive && phase >= 2 ? SideCascadePrefix : null);
                        if (AmberLive && phase == 1) live.UseGameplayCamera(definition,210);
                        else live.UseGameplayCamera(definition);
                        if (AmberLive && fall) CaptureSideCascadeVisibility(definition,live);''')
s=s.replace('''firstTime = firstLiveTime, lastTime = Time.time, movingPixels''','''firstTime = firstLiveTime, lastTime = Time.time, cameraStation = live.selectedStation, movingPixels''',1)
s=s.replace('''                if (AmberLive && SessionState.GetInt(PhaseKey,0) == 0)
                {
                    SessionState.SetInt(PhaseKey,1); liveFrame = 0;''','''                if (AmberLive && SessionState.GetInt(PhaseKey,0) < AmberPhaseTags.Length-1)
                {
                    SessionState.SetInt(PhaseKey,SessionState.GetInt(PhaseKey,0)+1); liveFrame = 0;''')
s=s.replace('''                    var stream = JsonUtility.FromJson<LiveReport>(File.ReadAllText(LiveFolder + "/stream-clock.json"));
                    File.WriteAllText(LiveFolder + "/live-clock.json",JsonUtility.ToJson(new AmberLiveReport {
                        pass = stream.pass && report.pass,sourceSceneryOnly = true,features = new[] { stream,report } },true));''','''                    var features = AmberPhaseTags.Select(tag => JsonUtility.FromJson<LiveReport>(File.ReadAllText(LiveFolder + "/" + tag + "-clock.json"))).ToArray();
                    File.WriteAllText(LiveFolder + "/live-clock.json",JsonUtility.ToJson(new AmberLiveReport {
                        pass = features.All(feature => feature.pass),sourceSceneryOnly = true,features = features },true));''')
needle='''        private static void Tick()
'''
method='''        [Serializable] private sealed class CascadeVisibility
        {
            public float station;
            public int visibleWaterPixels;
            public Vector3[] projectedTopMidBase;
            public bool allTargetsInsidePortrait;
        }
        [Serializable] private sealed class CascadeVisibilityReport
        {
            public string scope = "Explicit RA_Amber_SideCascade_ waterfall nodes. Actual fixed route camera at155/170/185m; full scenery remains enabled and the category-specific visible-water mask includes occlusion. Projected landmarks use artist-provided actual Unity top/mid/base, not water-only renders.";
            public CascadeVisibility[] stations;
        }
        private static void CaptureSideCascadeVisibility(IslandStageDefinition definition, View view)
        {
            float previous = view.selectedStation;
            var proofs = new List<CascadeVisibility>();
            foreach (float station in new[] {155f,170f,185f})
            {
                view.UseGameplayCamera(definition,station);
                view.Capture(LiveFolder + "/side-cascade-route-" + station.ToString("000",System.Globalization.CultureInfo.InvariantCulture) + ".png");
                var mask = view.VisibleWaterMask();
                var projected = SideCascadeTargets.Select(point => view.camera.WorldToViewportPoint(point)).ToArray();
                proofs.Add(new CascadeVisibility {station = station,visibleWaterPixels = mask.Count(pixel => pixel.r > 0),
                    projectedTopMidBase = projected,allTargetsInsidePortrait = projected.All(point => point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1)});
            }
            view.UseGameplayCamera(definition,previous);
            File.WriteAllText(LiveFolder + "/side-cascade-visibility.json",JsonUtility.ToJson(new CascadeVisibilityReport {stations = proofs.ToArray()},true));
            if (proofs.Any(proof => proof.visibleWaterPixels < 500))
                throw new InvalidOperationException("The explicit side waterfall is not clearly visible through full scenery from all three representative normal route cameras.");
        }

'''
assert needle in s;s=s.replace(needle,method+needle,1)
p.write_text(s,encoding='utf8')
p=Path(__file__).with_name('UnityStaging')/'AmberWindsIslandJobs.cs'; s=p.read_text(encoding='utf8').replace('new[] { 172f,185f','new[] { 155f,170f,172f,185f'); p.write_text(s,encoding='utf8')
print('Staged explicit side-cascade masks/landmarks at155/170/185 and4 native temporal phases including old210 roll.')
