using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DancingCat.Editor
{
    public static class CatPreviewBuilder
    {
        public const string ScenePath = "Assets/DancingCat/Scenes/DancingCat_Preview.unity";

        public static void ValidateAndPrepare()
        {
            CatSimulationValidation.Run();
            global::CatChartValidation.Run();
            CatControllerValidation.Run();
            CatPresentationValidation.Run();
            global::CatMetaValidation.Run();
            BuildPreview();
        }

        [MenuItem("Dancing Cat/Build playable preview")]
        public static void BuildPreview()
        {
            Directory.CreateDirectory("Assets/DancingCat/Songs");
            Directory.CreateDirectory("Assets/DancingCat/Scenes");
            AssetDatabase.Refresh();
            var songs = new List<CatSong>();
            string[] titles = { "MEOW MELODY", "NEON PAWS", "MOONLIGHT DANCE", "PINK PARADE", "STARLIGHT CAT" };
            float[] tempos = { 170, 80, 115, 127, 128 };
            Color[] themes = { new Color(.48f, .2f, .83f), new Color(.13f, .48f, .7f), new Color(.17f, .23f, .6f), new Color(.73f, .24f, .49f), new Color(.12f, .53f, .5f) };
            for (int i = 0; i < titles.Length; i++)
            {
                string path = i == 0 ? "Assets/Sounds/Level_01/Cat_Dancing.ogg" : $"Assets/Sounds/Level_{i + 1:D2}/Level_{i + 1:D2}.wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) throw new Exception("Missing existing project audio: " + path);
                string assetPath = $"Assets/DancingCat/Songs/Session_{i + 1:D2}.asset";
                var song = AssetDatabase.LoadAssetAtPath<CatSong>(assetPath);
                if (song == null)
                {
                    song = ScriptableObject.CreateInstance<CatSong>();
                    AssetDatabase.CreateAsset(song, assetPath);
                }
                song.id = "session-" + (i + 1);
                song.title = titles[i];
                song.artist = "Cat sessions · " + (i == 0 ? "Meow mix" : "Project track " + (i + 1));
                song.clip = clip;
                song.bpm = tempos[i];
                song.speed = 6.5f;
                song.offset = 0;
                song.duration = Mathf.Min(60, clip.length);
                song.difficulty = i < 2 ? 1 : i < 4 ? 2 : 3;
                song.theme = themes[i];
                CatChartAnalyzer.Analyze(song, 117 + i * 31);
                EditorUtility.SetDirty(song);
                songs.Add(song);
                int holds = song.notes.FindAll(note => note.kind == CatNoteKind.Hold).Count;
                int slides = song.notes.FindAll(note => note.kind == CatNoteKind.Slide).Count;
                Debug.Log($"CAT_PREVIEW chart {song.title}: {song.duration:F2}s, {song.notes.Count} notes, {holds} holds, {slides} follows");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.19f, .08f, .38f);
            camera.fieldOfView = 48;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 180;
            camera.transform.position = new Vector3(0, 3.5f, -6);
            camera.transform.LookAt(new Vector3(0, .65f, 0));
            var key = new GameObject("Soft Key", typeof(Light)).GetComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.3f;
            key.color = new Color(1, .88f, .91f);
            key.transform.rotation = Quaternion.Euler(40, -30, 0);
            var fill = new GameObject("Blue Fill", typeof(Light)).GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = .55f;
            fill.color = new Color(.55f, .72f, 1);
            fill.transform.rotation = Quaternion.Euler(30, 150, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .48f, .7f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 50;
            RenderSettings.fogEndDistance = 120;

            var game = new GameObject("Dancing Cat").AddComponent<CatGameController>();
            game.Songs = songs.ToArray();
            string[] modelPaths = {
                "Assets/Prefabs/Orange_Cat_001.prefab",
                "Assets/Lowpoly Humanoid Cat/Prefab/Shiro.prefab",
                "Assets/Lowpoly Humanoid Cat/Prefab/Kuroneko.prefab",
                "Assets/Lowpoly Humanoid Cat/Prefab/Mikeneko.prefab",
                "Assets/Lowpoly Humanoid Cat/Prefab/Silver.prefab"
            };
            game.CatPrefabs = new GameObject[modelPaths.Length];
            for (int i = 0; i < modelPaths.Length; i++)
            {
                game.CatPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(modelPaths[i]);
                if (game.CatPrefabs[i] == null) throw new Exception("Missing cat model: " + modelPaths[i]);
            }
            game.CatAnimator = MakeController("HumanoidCat", FindClip("Assets/Lowpoly Humanoid Cat/Model_Animation/catwait.fbx", "catwait"),
                FindClip("Assets/Lowpoly Humanoid Cat/Model_Animation/catjump.fbx", "catjump"),
                FindClip("Assets/Lowpoly Humanoid Cat/Model_Animation/catjump.fbx", "catrun"));
            game.GingerAnimator = MakeController("GingerCat", FindClip("Assets/Objects/Cat_001.fbx", "Idel_Pose"),
                FindClip("Assets/Objects/Cat_001.fbx", "Jump_Pose"), FindClip("Assets/Objects/Cat_001.fbx", "Idel_Pose"));
            game.gameObject.AddComponent<CatGameUI>();
            game.gameObject.AddComponent<CatMetaProgress>();
            game.gameObject.AddComponent<CatExtraUI>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var buildScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var existing in EditorBuildSettings.scenes)
                if (existing.path != ScenePath) buildScenes.Add(new EditorBuildSettingsScene(existing.path, false));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
            Debug.Log("CAT_PREVIEW READY: " + ScenePath);
        }

        public static void ValidateAndBuildWindows()
        {
            ValidateAndPrepare();
            BuildWindows();
        }

        public static void BuildWindows()
        {
            string output = Argument("-catBuild", "");
            if (string.IsNullOrEmpty(output)) throw new ArgumentException("Pass -catBuild with the output executable path");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("Windows preview build failed: " + report.summary.result);
            Debug.Log("CAT_PREVIEW BUILD PASS: " + output);
        }

        private static AnimationClip FindClip(string path, string token)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .First(clip => !clip.name.StartsWith("__preview__") && clip.name.Contains(token));
        }

        private static AnimatorController MakeController(string name, AnimationClip idle, AnimationClip jump, AnimationClip hold)
        {
            Directory.CreateDirectory("Assets/DancingCat/Animation");
            string path = "Assets/DancingCat/Animation/" + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null) return controller;
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            string[] states = { "Idle", "Jump", "Hold", "End" };
            AnimationClip[] clips = { idle, jump, hold, idle };
            for (int i = 0; i < states.Length; i++)
            {
                var state = machine.AddState(states[i]);
                state.motion = clips[i];
                if (i == 0) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static string Argument(string key, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return fallback;
        }
    }
}
