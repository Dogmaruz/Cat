using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class ChartStudyPrecisionDiagnostics
    {
        [Serializable] private sealed class Difference { public string path, left, right, type; public double absoluteError; }
        [Serializable] private sealed class Report
        {
            public string sourceId;
            public int totalDifferences, nonNumericDifferences;
            public double maxDoubleError, maxFloatError;
            public Difference[] examples;
        }
        public static void Run()
        {
            var manifest = ChartGeneratorStudy.LoadManifest();
            foreach (string id in manifest.publishSourceIds)
            {
                var record = RecoveredCatalog.LoadOriginal().Single(s => s.id == id);
                var report = new Report { sourceId = id };
                var differences = new List<Difference>();
                var actual = AudioChartGenerator.Generate(record.LoadAudio(), id + manifest.testSuffix,
                    new AudioChartGenerator.Options { catalogId = manifest.catalogIdStart, title = record.title,
                        artist = record.artist, forwardSpeed = record.forwardSpeed, delayTime = manifest.preroll,
                        preferAuthoredScore = false, includeExperimentalShortSupports = true });
                var expected = JsonUtility.FromJson<MapTimeline>(File.ReadAllText(manifest.studyDirectory + "/benchmark/" + id + "/candidate.generated.timeline.json"));
                expected = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(expected));
                actual = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(actual));
                foreach (string field in new[] { "schemaVersion", "sampleRate", "forwardSpeed", "delayTime", "tempoSections", "events", "elements", "segments" })
                {
                    var member = typeof(MapTimeline).GetField(field);
                    Compare(field, member.GetValue(expected), member.GetValue(actual), report, differences);
                }
                report.examples = differences.ToArray();
                File.WriteAllText(manifest.studyDirectory + "/precision-" + id + ".json", JsonUtility.ToJson(report, true));
                Debug.Log("CHART_PRECISION " + id + ": differences=" + report.totalDifferences + "; nonnumeric=" + report.nonNumericDifferences
                    + "; maxDouble=" + report.maxDoubleError.ToString("R", CultureInfo.InvariantCulture) + "; maxFloat=" + report.maxFloatError.ToString("R", CultureInfo.InvariantCulture));
            }
        }
        private static void Compare(string path, object a, object b, Report report, List<Difference> examples)
        {
            if (a == null && b == null) return;
            if (a != null && b != null && a.GetType() == b.GetType())
            {
                if (a is double d) { if (d.Equals((double)b)) return; Record(path, a, b, Math.Abs(d-(double)b), report, examples); return; }
                if (a is float f) { if (f.Equals((float)b)) return; Record(path, a, b, Math.Abs(f-(float)b), report, examples); return; }
                if (a is Array array)
                {
                    var other = (Array)b;
                    if (array.Length != other.Length) { Record(path + ".Length", array.Length, other.Length, 0, report, examples); return; }
                    for (int i=0; i<array.Length; i++) Compare(path+"["+i+"]",array.GetValue(i),other.GetValue(i),report,examples);
                    return;
                }
                var type=a.GetType();
                if (!type.IsPrimitive && !type.IsEnum && !(a is string))
                {
                    foreach (var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public)) Compare(path+"."+field.Name,field.GetValue(a),field.GetValue(b),report,examples);
                    return;
                }
                if (a.Equals(b)) return;
            }
            Record(path,a,b,0,report,examples);
        }
        private static void Record(string path, object a, object b, double error, Report report, List<Difference> examples)
        {
            report.totalDifferences++;
            if (a is double) report.maxDoubleError=Math.Max(report.maxDoubleError,error);
            else if (a is float) report.maxFloatError=Math.Max(report.maxFloatError,error);
            else report.nonNumericDifferences++;
            if (examples.Count<30) examples.Add(new Difference {path=path,left=Convert.ToString(a,CultureInfo.InvariantCulture),right=Convert.ToString(b,CultureInfo.InvariantCulture),absoluteError=error,type=a?.GetType().Name??"null"});
        }
    }
}
