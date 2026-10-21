using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class MeowLevelValidation
    {
        public static void RunChecks()
        {
            var failures = new List<string>();
            foreach (var name in new[] { "AudioChartGeneratorValidation", "MusicalChartCompilerValidation", "CustomContentValidation", "IntegratedPlayValidation" })
            {
                try
                {
                    if (name == "CustomContentValidation") Invoke(name, "RunChecks", true);
                    else if (name == "IntegratedPlayValidation") Invoke(name, "RunReviewSafetyChecks");
                    else Invoke(name, "RunChecks");
                }
                catch (Exception error)
                {
                    string message = name + ": " + (error.InnerException ?? error).Message;
                    Debug.LogError("MEOW_LEVEL FAIL: " + message);
                    failures.Add(message);
                }
            }
            if (failures.Count != 0) throw new InvalidOperationException(string.Join("\n", failures));
            Debug.Log("MEOW_LEVEL PASS: audio generator, musical compiler, native custom geometry and open-Editor review safety checks");
        }

        public static void GenerateAndVerify()
        {
            Invoke("AudioChartGenerator", "GenerateAlwaysRemember");
            AssetDatabase.Refresh();
            RunChecks();
            ClonePlayerBuild.Verify();
        }

        private static void Invoke(string typeName, string methodName, params object[] arguments)
        {
            var type = typeof(MeowLevelValidation).Assembly.GetType("DancingCats.Clone.Editor." + typeName, true);
            var types = Array.ConvertAll(arguments, value => value.GetType());
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, types, null);
            if (method == null) throw new MissingMethodException(typeName, methodName);
            var result = method.Invoke(null, arguments);
            if (result is int failures && failures != 0)
                throw new InvalidOperationException(typeName + " reported " + failures + " failed checks.");
        }
    }
}
