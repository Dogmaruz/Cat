using System;
using System.Reflection;
using UnityEngine;

namespace DancingCat.Editor
{
    public static class CatControllerValidation
    {
        public static void Run()
        {
            const string walletKey = "DancingCat.v2.Coins";
            bool hadWallet = PlayerPrefs.HasKey(walletKey);
            int oldWallet = PlayerPrefs.GetInt(walletKey);
            string id = "validation-" + Guid.NewGuid().ToString("N");
            var song = ScriptableObject.CreateInstance<CatSong>();
            var root = new GameObject("RewardRegression");
            root.SetActive(false);
            try
            {
                song.id = id; song.duration = 1;
                song.notes.Add(new CatNote { time = 0 });
                song.notes.Add(new CatNote { time = .5f });
                var controller = root.AddComponent<CatGameController>();
                var state = new CatRunState(song);
                Set(controller, "Run", state);
                Set(controller, "SelectedSong", song);
                Set(controller, "Coins", 0);
                state.Advance(0, 0); state.Advance(.5f, 1.5f);
                Set(controller, "Phase", CatGamePhase.Failed);
                Pay(controller, false);
                state.Revive(); state.Advance(1, 0);
                Set(controller, "Phase", CatGamePhase.Complete);
                Pay(controller, true);
                if (controller.GetBestStars(song) != 3 || controller.GetBestScore(song) != 20 || controller.Coins != 25)
                    throw new Exception($"CAT_CONTROLLER FAIL: completion after revive must save 3 stars/score20 and pay total25; got {controller.GetBestStars(song)}/{controller.GetBestScore(song)}/{controller.Coins}");
                Pay(controller, true);
                if (controller.Coins != 25) throw new Exception("CAT_CONTROLLER FAIL: completing twice paid duplicate coins");
                Debug.Log("CAT_CONTROLLER PASS: revived completion persists score/stars and pays reward once");
                ValidateCalibration(controller, song, root);
                ValidateHeldRestart(controller);
            }
            finally
            {
                if (hadWallet) PlayerPrefs.SetInt(walletKey, oldWallet); else PlayerPrefs.DeleteKey(walletKey);
                PlayerPrefs.DeleteKey("DancingCat.v2.Best." + id);
                PlayerPrefs.DeleteKey("DancingCat.v2.Stars." + id);
                PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(song);
            }
        }

        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static void ValidateHeldRestart(CatGameController controller)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CatGameController).GetField("_dragging", flags).SetValue(controller, true);
            var method = typeof(CatGameController).GetMethod("ResetSteering", flags);
            if (method == null) throw new Exception("CAT_CONTROLLER FAIL: endless restart needs a shared drag reset");
            method.Invoke(controller, new object[] { 0f });
            if ((bool)typeof(CatGameController).GetField("_dragging", flags).GetValue(controller))
                throw new Exception("CAT_CONTROLLER FAIL: held drag retains the previous lap anchor");
            Debug.Log("CAT_CONTROLLER PASS: endless restart re-anchors a held drag");
        }
        private static void Pay(CatGameController controller, bool complete) => typeof(CatGameController).GetMethod("PayReward", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, new object[] { complete });

        private static void ValidateCalibration(CatGameController controller, CatSong song, GameObject root)
        {
            var audioObject = new GameObject("CalibrationAudio");
            var source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            var clip = AudioClip.Create("CalibrationFixture", 480000, 1, 48000, false);
            song.clip = clip; song.duration = 10;
            song.notes.Clear(); song.notes.Add(new CatNote { time = 0 }); song.notes.Add(new CatNote { time = 8 });
            var player = new GameObject("Player").transform; player.SetParent(root.transform);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CatGameController).GetField("_player", flags).SetValue(controller, player);
            typeof(CatGameController).GetField("_music", flags).SetValue(controller, source);
            try
            {
                foreach (float latency in new[] { 200f, -200f })
                {
                    typeof(CatGameController).GetField("_latency", flags).SetValue(controller, latency);
                    typeof(CatGameController).GetField("_pauseTime", flags).SetValue(controller, 3f);
                    var state = new CatRunState(song); state.Advance(0, 0); state.Advance(3, 0);
                    Set(controller, "Run", state); Set(controller, "Phase", CatGamePhase.Paused);
                    controller.Resume();
                    float wantedAudioTime = 3 + latency / 1000f;
                    if (Mathf.Abs(source.timeSamples / 48000f - wantedAudioTime) > .001f)
                        throw new Exception($"CAT_CONTROLLER FAIL: calibrated resume {latency}ms must seek audio to {wantedAudioTime}, got {source.timeSamples / 48000f}");
                    typeof(CatGameController).GetMethod("Update", flags).Invoke(controller, null);
                    if (Mathf.Abs(state.Time - 3) > .001f)
                        throw new Exception("CAT_CONTROLLER FAIL: resume changed chart time before scheduled audio started");
                    source.Stop();
                }
                Debug.Log("CAT_CONTROLLER PASS: +/-200ms calibration survives pause/resume without a clock jump");
            }
            finally { source.Stop(); UnityEngine.Object.DestroyImmediate(audioObject); UnityEngine.Object.DestroyImmediate(clip); }
        }
    }
}
