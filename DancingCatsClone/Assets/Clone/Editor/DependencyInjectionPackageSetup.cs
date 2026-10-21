using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class DependencyInjectionPackageSetup
    {
        public const string PackageUrl = "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#5401e5a7ebc4980a2b82141ffc26391a6547edd7";
        private static AddRequest request;
        private static double deadline;

        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before installing VContainer.");
            if (request != null) throw new InvalidOperationException("The DI package request is already running.");
            request = Client.Add(PackageUrl);
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += Poll;
            Debug.Log("DI_PACKAGE: installing jp.hadashikick.vcontainer 1.19.0 at pinned revision");
        }

        private static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            if (!request.IsCompleted) Debug.LogError("DI_PACKAGE: package resolution timed out");
            else if (request.Status != StatusCode.Success) Debug.LogError("DI_PACKAGE: " + request.Error.message);
            else Debug.Log("DI_PACKAGE PASS: " + request.Result.name + " " + request.Result.version);
            request = null;
        }
    }
}
