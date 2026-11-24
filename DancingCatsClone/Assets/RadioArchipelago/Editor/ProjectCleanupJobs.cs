using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Explicit project housekeeping. Never runs at import or in a player.</summary>
    public static class ProjectCleanupJobs
    {
        private const string Templates = "Assets/Editor/SceneTemplates/";
        [Serializable] private sealed class Entry
        {
            public string source, destination, sha256, metaSha256, guid;
            public bool preserved;
        }
        [Serializable] private sealed class Report
        {
            public bool passed, currentScenePreserved;
            public string projectPath, playableScene, archivePath;
            public int workingScenes, templateScenes, archivedScenes, missingScripts;
            public Entry[] entries;
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        private static string RepoRoot => Directory.GetParent(ProjectRoot).FullName;
        private static string ArchiveRoot => Path.Combine(Directory.GetParent(RepoRoot).FullName,
            "_Archive", "RadioArchipelago", "2026-10-28", "Scenes");
        private static string ReportPath => Path.Combine(RepoRoot, "Docs", "Maintenance", "UnityCleanup20261028", "scene-cleanup.json");

        [MenuItem("Radio Archipelago/Project/Open current island")]
        public static void OpenCurrent()
        {
            RequireCleanEditor();
            if (EditorSceneManager.GetActiveScene().path != IslandAbyssJobs.ScenePath)
                EditorSceneManager.OpenScene(IslandAbyssJobs.ScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(IslandAbyssJobs.ScenePath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Radio Archipelago/Project/Organize legacy scenes")]
        public static void Apply()
        {
            RequireCleanEditor();
            if (EditorSceneManager.GetActiveScene().path != IslandAbyssJobs.ScenePath)
                EditorSceneManager.OpenScene(IslandAbyssJobs.ScenePath);
            string currentHash = Hash(IslandAbyssJobs.ScenePath);
            var entries = new List<Entry>();
            MoveTemplate("Assets/Clone/Scenes/DancingCats.unity", Templates + "Base/DancingCats.unity", entries);
            MoveTemplate("Assets/RadioArchipelago/Scenes/EmeraldIslandPilot.unity", Templates + "Prototypes/EmeraldIslandPilot.unity", entries);
            MoveTemplate("Assets/RadioArchipelago/Scenes/EmeraldReliefPilot.unity", Templates + "Prototypes/EmeraldReliefPilot.unity", entries);
            for (int i = 0; i < 8; i++)
                MoveTemplate("Assets/Recovered/Scenes/level" + i + ".unity", Templates + "Recovered/level" + i + ".unity", entries);
            ArchiveScene("Assets/Scenes/SampleScene.unity", entries);
            ArchiveScene("Assets/_Recovery/0.unity", entries);
            foreach (string emptyFolder in new[] { "Assets/Scenes", "Assets/_Recovery", "Assets/Clone/Scenes" })
                if (Directory.Exists(emptyFolder) && !Directory.EnumerateFileSystemEntries(emptyFolder).Any())
                    AssetDatabase.DeleteAsset(emptyFolder);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(IslandAbyssJobs.ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var report = Inspect();
            report.currentScenePreserved = currentHash == Hash(IslandAbyssJobs.ScenePath);
            report.entries = entries.ToArray();
            report.archivedScenes = entries.Count(e => e.destination.StartsWith(ArchiveRoot, StringComparison.OrdinalIgnoreCase));
            report.passed &= report.currentScenePreserved && entries.All(e => e.preserved) && report.archivedScenes == 2;
            Write(report);
            if (!report.passed) throw new InvalidOperationException("Project cleanup checks failed; see " + ReportPath);
            OpenCurrent();
            Debug.Log("RADIO_PROJECT_CLEANUP_PASS working=1 templates=11 archived=2; current island unchanged.");
        }

        [MenuItem("Radio Archipelago/Project/Check organized scenes")]
        public static void RunChecks()
        {
            RequireCleanEditor();
            var report = Inspect();
            if (!report.passed) throw new InvalidOperationException("Unexpected working scenes, missing templates, build entry or missing scripts.");
            Debug.Log("RADIO_PROJECT_CLEANUP_CHECKS_PASS working=" + report.workingScenes + " templates=" + report.templateScenes);
        }

        private static Report Inspect()
        {
            string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            string[] working = scenes.Where(p => !p.StartsWith(Templates, StringComparison.Ordinal)).ToArray();
            var report = new Report
            {
                projectPath = ProjectRoot, playableScene = IslandAbyssJobs.ScenePath, archivePath = ArchiveRoot,
                workingScenes = working.Length, templateScenes = scenes.Length - working.Length
            };
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                    report.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject);
            var buildScenes = EditorBuildSettings.scenes;
            report.passed = working.Length == 1 && working[0] == IslandAbyssJobs.ScenePath && report.templateScenes == 11 &&
                scene.path == IslandAbyssJobs.ScenePath && report.missingScripts == 0 && buildScenes.Length == 1 &&
                buildScenes[0].enabled && buildScenes[0].path == IslandAbyssJobs.ScenePath;
            return report;
        }

        private static void MoveTemplate(string source, string destination, List<Entry> entries)
        {
            bool sourceExists = File.Exists(source);
            if (!sourceExists && !File.Exists(destination)) throw new FileNotFoundException("Scene template missing", source);
            if (sourceExists && File.Exists(destination)) throw new IOException("Both scene paths already exist: " + destination);
            string existing = sourceExists ? source : destination;
            var entry = Snapshot(source, destination, existing);
            if (sourceExists)
            {
                EnsureAssetFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
                string error = AssetDatabase.MoveAsset(source, destination);
                if (!string.IsNullOrEmpty(error)) throw new IOException(error);
            }
            entry.preserved = entry.sha256 == Hash(destination) && entry.metaSha256 == Hash(destination + ".meta") &&
                entry.guid == AssetDatabase.AssetPathToGUID(destination);
            entries.Add(entry);
        }

        private static void ArchiveScene(string source, List<Entry> entries)
        {
            string destination = Path.GetFullPath(Path.Combine(ArchiveRoot, source.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(Path.GetFullPath(ArchiveRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Archive target escaped its directory.");
            if (!File.Exists(source) && !File.Exists(destination)) throw new FileNotFoundException("Scene to archive missing", source);
            string existing = File.Exists(source) ? source : destination;
            var entry = Snapshot(source, destination, existing);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (File.Exists(source))
            {
                foreach (string suffix in new[] { "", ".meta" })
                {
                    if (!File.Exists(destination + suffix)) File.Copy(source + suffix, destination + suffix);
                    if (Hash(source + suffix) != Hash(destination + suffix)) throw new IOException("Archive collision: " + destination + suffix);
                }
                if (!AssetDatabase.DeleteAsset(source)) throw new IOException("Unable to remove archived scene: " + source);
            }
            entry.preserved = entry.sha256 == Hash(destination) && entry.metaSha256 == Hash(destination + ".meta");
            entries.Add(entry);
        }

        private static Entry Snapshot(string source, string destination, string existing) => new Entry
        {
            source = source, destination = destination, sha256 = Hash(existing), metaSha256 = Hash(existing + ".meta"),
            guid = File.ReadLines(existing + ".meta").First(l => l.StartsWith("guid: ")).Substring(6)
        };

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            EnsureAssetFolder(folder.Substring(0, folder.LastIndexOf('/')));
            AssetDatabase.CreateFolder(folder.Substring(0, folder.LastIndexOf('/')), Path.GetFileName(folder));
        }

        private static void Write(Report report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        }

        private static void RequireCleanEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Finish Play Mode, compilation and unsaved scene edits before housekeeping.");
        }
    }
}
