from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'LivingWaterReview.cs'; s=p.read_text(encoding='utf8')
s=s.replace('''private const string AmberKey = ClockKey + ".AmberSourceScenery", PhaseKey = ClockKey + ".Phase";''','''private const string AmberKey = ClockKey + ".AmberSourceScenery", PhaseKey = ClockKey + ".Phase", RunKey = ClockKey + ".RunId";
        private const int ExpectedLiveFrames = 14;''')
s=s.replace('''public string state, scope, feature;''','''public string state, scope, feature, runId;''',1).replace('''public int movingPixels, coveredPixels;''','''public int movingPixels, coveredPixels, frames;''',1)
s=s.replace('''            SessionState.SetBool(AmberKey,amber); SessionState.SetInt(PhaseKey,0);''','''            SessionState.SetBool(AmberKey,amber); SessionState.SetInt(PhaseKey,0);
            SessionState.SetString(RunKey,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));''',1)
s=s.replace('''SessionState.EraseString(SceneKey);''','''SessionState.EraseString(SceneKey); SessionState.EraseString(RunKey);''',1)
s=s.replace('''                    LiveFrameTimes.Clear();''','''                    foreach (string stale in Directory.GetFiles(LiveFolder,LiveTag + "-flow-*.png",SearchOption.TopDirectoryOnly)) File.Delete(stale);
                    LiveFrameTimes.Clear();''',1)
s=s.replace('''liveFrame < 14''','''liveFrame < ExpectedLiveFrames''')
s=s.replace('''                if (Time.time - firstLiveTime < 1.3f) return;''','''                if (liveFrame < ExpectedLiveFrames || Time.time - firstLiveTime < 1.3f) return;''',1)
s=s.replace('''var report = new LiveReport { pass = moving > 100 && covered > 500, state = "complete", feature = LiveTag,''','''var report = new LiveReport { pass = moving > 100 && covered > 500 && liveFrame == ExpectedLiveFrames && LiveFrameTimes.Count == ExpectedLiveFrames,
                    state = "complete", feature = LiveTag,runId = SessionState.GetString(RunKey,""),frames = liveFrame,''',1)
s=s.replace('''pass = features.All(feature => feature.pass),sourceSceneryOnly''','''pass = features.All(feature => feature.pass && feature.frames == ExpectedLiveFrames && feature.runId == SessionState.GetString(RunKey,"")),sourceSceneryOnly''',1)
p.write_text(s,encoding='utf8')
print('Native proof now requires all14 actually timestamped frames + minelapsed; phase-owned stale frames cleared and run IDs prevent stale report reuse.')
