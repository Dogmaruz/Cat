using System;
using System.IO;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Isolated Edit Mode pool/lifecycle check; never opens or modifies a scene.</summary>
    public static class IslandLivingNatureChecks
    {
        [Serializable] private sealed class Report
        {
            public bool pass, sameConfigurationIsIdempotent, noGameplayComponents, cleanupComplete, pauseFreezesPose;
            public int pooledFauna, pooledSkyClouds, cloudBankSections, maximumActiveFauna, maximumActiveSkyClouds;
            public long warmedTickAllocatedBytes;
        }

        [MenuItem("Tools/Radio Archipelago/Check island living nature")]
        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Check living nature in Edit Mode.");
            var owner = new GameObject("Isolated living nature check") { hideFlags = HideFlags.HideAndDontSave };
            var definition = ScriptableObject.CreateInstance<IslandStageDefinition>();
            var report = new Report();
            try
            {
                var route = new IslandRoute(new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 125),
                    new Vector3(125, 0, 125), new Vector3(125, 0, 0) });
                var nature = owner.AddComponent<IslandLivingNature>();
                nature.Configure(definition, route);
                report.pooledFauna = nature.PooledFaunaCount;
                report.pooledSkyClouds = nature.PooledSkyCloudCount;
                report.cloudBankSections = nature.CloudBankCount;
                int transforms = owner.GetComponentsInChildren<Transform>(true).Length;
                nature.Configure(definition, route);
                report.sameConfigurationIsIdempotent = transforms == owner.GetComponentsInChildren<Transform>(true).Length;
                report.noGameplayComponents = owner.GetComponentsInChildren<Collider>(true).Length == 0 &&
                    owner.GetComponentsInChildren<Rigidbody>(true).Length == 0 && owner.GetComponentsInChildren<AudioSource>(true).Length == 0;
                for (int i = 0; i < 1000; i++)
                {
                    nature.UpdatePresentation(i * 1.11f - 30, .016f);
                    report.maximumActiveFauna = Mathf.Max(report.maximumActiveFauna, nature.ActiveFaunaCount);
                    report.maximumActiveSkyClouds = Mathf.Max(report.maximumActiveSkyClouds, nature.ActiveSkyCloudCount);
                }
                nature.UpdatePresentation(440, .016f);
                var active = owner.GetComponentsInChildren<MeshRenderer>();
                var poses = new Vector3[active.Length];
                for (int i = 0; i < active.Length; i++) poses[i] = active[i].transform.position;
                nature.UpdatePresentation(440, 0);
                report.pauseFreezesPose = true;
                for (int i = 0; i < active.Length; i++)
                    report.pauseFreezesPose &= (poses[i] - active[i].transform.position).sqrMagnitude < .00000001f;
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 2000; i++) nature.UpdatePresentation(400 + i * .023f, .016f);
                report.warmedTickAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(owner.GetComponentsInChildren<Transform>(true).Length == transforms, "Tick created new objects.");
                nature.Dispose();
                report.cleanupComplete = !nature.IsConfigured && owner.transform.childCount == 0 &&
                    nature.PooledFaunaCount == 0 && nature.PooledSkyCloudCount == 0 && nature.CloudBankCount == 0;
                report.pass = report.sameConfigurationIsIdempotent && report.noGameplayComponents && report.cleanupComplete &&
                    report.pauseFreezesPose && report.pooledFauna == 16 && report.pooledSkyClouds == 8 && report.cloudBankSections == 13 &&
                    report.maximumActiveFauna <= 4 && report.maximumActiveSkyClouds <= 6 && report.warmedTickAllocatedBytes == 0;
                string folder = Path.GetFullPath("Library/RadioArchipelago/IslandNature");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "living-nature-checks.json"), JsonUtility.ToJson(report, true));
                Require(report.pass, "ISLAND_LIVING_NATURE FAIL: " + JsonUtility.ToJson(report));
                Debug.Log("ISLAND_LIVING_NATURE PASS: fixed pools, bounded visible wildlife/clouds, pause, zero tick allocations and cleanup.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
