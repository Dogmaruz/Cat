from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'AmberWindsIslandJobs.cs'
s=p.read_text(encoding='utf8')
needle='''        private static void CheckRooftops(IslandStageDefinition definition)
'''
s=s.replace(needle,'''        [Serializable] private sealed class RotorPreviewReport
        {
            public bool passed, zeroDeltaFreezes, disposeRestores;
            public string[] nodes;
            public float[] angleAfterOneSecond;
        }

'''+needle,1)
old='''                motion.Configure(owner.transform, owner.transform, definition.RooftopMotionJson);
                if (motion.Count < 3) throw new InvalidOperationException("A prominent rotating windmill is required.");
                motion.Tick(.1f);'''
new='''                motion.Configure(owner.transform, owner.transform, definition.RooftopMotionJson);
                var document = JsonUtility.FromJson<IslandRooftopMotion.Document>(definition.RooftopMotionJson.text);
                var nodes = document.mechanisms.Select(entry => owner.GetComponentsInChildren<Transform>(true)
                    .Single(node => node.name == entry.nodeName)).ToArray();
                if (motion.Count < 3) throw new InvalidOperationException("Three prominent rotating windmills are required.");
                var rest = nodes.Select(node => node.localRotation).ToArray();
                for (int tick = 0; tick < 10; tick++) motion.Tick(.1f);
                var moved = nodes.Select(node => node.localRotation).ToArray();
                var report = new RotorPreviewReport { nodes = nodes.Select(node => node.name).ToArray(),
                    angleAfterOneSecond = nodes.Select((node,index) => Quaternion.Angle(rest[index],node.localRotation)).ToArray() };
                if (report.angleAfterOneSecond.Any(angle => angle < 20))
                    throw new InvalidOperationException("Authored windmill sails did not visibly rotate during their one-second native helper tick.");
                motion.Tick(0);
                report.zeroDeltaFreezes = nodes.Select((node,index) => Quaternion.Angle(node.localRotation,moved[index]) < .001f).All(value => value);
                motion.Dispose();
                report.disposeRestores = nodes.Select((node,index) => Quaternion.Angle(node.localRotation,rest[index]) < .001f).All(value => value);
                if (!report.zeroDeltaFreezes || !report.disposeRestores)
                    throw new InvalidOperationException("Windmill zero-delta pause or cleanup did not preserve its authored rest pose.");
                report.passed = true; Directory.CreateDirectory(ReviewFolder);
                File.WriteAllText(ReviewFolder + "/rooftop-motion-preview.json", JsonUtility.ToJson(report,true));'''
assert old in s
s=s.replace(old,new,1)
p.write_text(s,encoding='utf8')
print('Windmill proof now records actual native rotation, zero-delta pause and rest-pose cleanup.')
