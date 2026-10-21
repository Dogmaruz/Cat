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
                    case "inspect": break;
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
