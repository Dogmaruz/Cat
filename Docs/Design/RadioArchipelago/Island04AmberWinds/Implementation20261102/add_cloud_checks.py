from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'AmberWindsIslandJobs.cs'
s=p.read_text(encoding='utf8')
s=s.replace('using System.Linq;','using System.Linq;\nusing System.Reflection;')
s=s.replace('private const int PreviewLayer = 29;','private const int PreviewLayer = 29;\n        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;')
s=s.replace('public int renderers, materials, movingTiles, holds;','public int renderers, materials, movingTiles, holds, clouds;\n            public float cloudMinimumGap, cloudMinimumClearance, cloudVolumeRatio;')
s=s.replace('!definition.SparseCloudSteps || definition.CloudStepRanges == null || !definition.CloudStepRanges.SequenceEqual(CloudRanges())','!definition.SparseCloudSteps || Mathf.Abs(definition.CloudBankScaleMultiplier - 1.6f) > .0001f || definition.CloudStepRanges == null || !definition.CloudStepRanges.SequenceEqual(CloudRanges())')
s=s.replace('CheckRooftops(definition);','CheckRooftops(definition);\n            CheckClouds(definition, route, report);')
needle='''        private static void CheckRooftops(IslandStageDefinition definition)
'''
method='''        private static void CheckClouds(IslandStageDefinition definition, IslandRoute route, Report report)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var owner = new GameObject("Isolated Amber cloud geometry");
            SceneManager.MoveGameObjectToScene(owner, preview);
            IslandLivingNature nature = null;
            try
            {
                nature = owner.AddComponent<IslandLivingNature>(); nature.Configure(definition, route);
                var entries = (Array)typeof(IslandLivingNature).GetField("bank", Hidden).GetValue(nature);
                report.clouds = entries.Length;
                if (entries.Length != definition.CloudStepRanges.Length)
                    throw new InvalidOperationException("Amber requires one substantial detached cloud per real air gap.");
                // Reuse the independent transformed-triangle proof, including full drift,
                // minimum physical separation, actual height clearance and visible volume variation.
                var proofType = typeof(SparseCloudStepChecks).GetNestedType("Report", BindingFlags.NonPublic);
                var proof = Activator.CreateInstance(proofType, true);
                try
                {
                    typeof(SparseCloudStepChecks).GetMethod("CheckSparse", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { nature, definition, route, definition.CloudStepRanges, proof });
                }
                catch (TargetInvocationException error)
                { throw new InvalidOperationException("Amber cloud geometry: " + error.InnerException?.Message, error.InnerException ?? error); }
                report.cloudMinimumGap = (float)proofType.GetField("minimumGap").GetValue(proof);
                report.cloudMinimumClearance = (float)proofType.GetField("minimumClearance").GetValue(proof);
                report.cloudVolumeRatio = (float)proofType.GetField("sizeRatio").GetValue(proof);
            }
            finally
            {
                nature?.Dispose(); Object.DestroyImmediate(owner);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

'''
assert needle in s
s=s.replace(needle,method+needle)
p.write_text(s,encoding='utf8')
print('Added real cloud drift, volume, gap and slope checks to new island job.')
