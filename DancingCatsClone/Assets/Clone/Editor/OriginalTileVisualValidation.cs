using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class OriginalTileVisualValidation
    {
        public static void Run()
        {
            var type = typeof(RecoveredCatalog).Assembly.GetType("DancingCats.Clone.OriginalTileVisuals");
            Require(type != null, "original tile visuals are not implemented");
            for (int theme = 0; theme < 7; theme++)
            {
                string suffix = theme == 0 ? "" : "_" + (theme - 1);
                var jump = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Recovered/GameObject/Jump1" + suffix + ".prefab"));
                try
                {
                    Invoke(type, "Configure", jump, new MapElement { id = 0 }, null);
                    var jumpAnimator = jump.GetComponent<Animator>();
                    Invoke(type, "OnHit", jump, true, false, 4);
                    jumpAnimator.Update(.02f);
                    Require(jumpAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Great"),
                        "original normal jump uses Great feedback for the first four perfect timings");
                    Invoke(type, "OnHit", jump, true, false, 5);
                    jumpAnimator.Update(.02f);
                    Require(jumpAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Perfect"),
                        "original normal jump enhanced Perfect feedback starts at streak five");
                }
                finally { UnityEngine.Object.DestroyImmediate(jump); }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recovered/GameObject/Hold" + suffix + ".prefab");
                var pointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recovered/GameObject/Hold_Light" + suffix + ".prefab");
                Require(prefab != null && pointPrefab != null, "original hold and checkpoint prefabs exist for theme " + theme);
                var hold = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    hold.transform.position = new Vector3(.5f, 0, 10f);
                    var element = new MapElement { id = 2, pos = hold.transform.position, se = new Vector2(0, -4), ext = "10,12,14", jumpDistance = 3 };
                    var segment = new MapSegment { id = 2, kind = "Hold", x = .5f, z = 10, endZ = 14 };
                    Invoke(type, "RegisterHoldPointTemplate", hold, pointPrefab);
                    Invoke(type, "Configure", hold, element, segment);
                    var start = hold.transform.Find("Hold/Dummy002/Start");
                    var end = hold.transform.Find("Hold/Dummy002/End");
                    Require(start != null && end != null, "original skin end bones exist");
                    Near(start.position.z, 10, "start bone retains first checkpoint Z");
                    Near(end.position.z, 14, "negative local Y stretches the original mesh forward");
                    Near(hold.transform.Find("Jump").position.z, 14, "unscored exit jump follows the actual end bone");
                    Require((GameObject)Invoke(type, "GetHoldPointTemplate", hold) == pointPrefab, "checkpoint uses the original external prefab");
                    var point = (GameObject)Invoke(type, "SpawnHoldPoint", hold, new Vector3(.5f, 0, 12));
                    Require(point != null && point.GetComponentsInChildren<Renderer>(true).Length > 0,
                        "checkpoint retains its recovered mesh and FX renderers");
                    Near(point.transform.position.z, 12, "checkpoint is placed at authored world Z");
                    Invoke(type, "OnHit", point, true, true, 6);
                    Require(point.transform.Find("Point") != null && !point.transform.Find("Point").gameObject.activeSelf,
                        "checkpoint hit hides only its authored Point mesh");
                    Invoke(type, "OnHit", hold, true, true, 6);
                    var animator = hold.GetComponent<Animator>();
                    animator.Update(.02f);
                    Require(animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Loop"),
                        "hold contact plays original Loop animation");
                    Invoke(type, "AdvanceHold", hold, 15f);
                    animator.Update(.02f);
                    Require(animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("End"),
                        "hold completion plays original End animation");
                    Invoke(type, "RestartHold", hold, 10.5f);
                    animator.Update(.02f);
                    Require(animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Loop"),
                        "reviving a completed hold restarts its original Loop animation");
                    var follow = Array.Find(hold.GetComponentsInChildren<Transform>(true), child => child.name == "Follow");
                    Near(follow.position.z, 10.5f, "revive restarts glow tracking at the restored player position");
                    Require(!point.transform.Find("Point").gameObject.activeSelf,
                        "revive keeps previously scored checkpoint graphics consumed");
                    Invoke(type, "AdvanceHold", hold, 11.5f);
                    Near(follow.position.z, 11.5f, "revived glow continues to follow forward travel");
                    Invoke(type, "AdvanceHold", hold, 15f);
                    animator.Update(.02f);
                    Require(animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("End"),
                        "revived hold reaches its original End animation again");
                }
                finally { UnityEngine.Object.DestroyImmediate(hold); }
            }
            Debug.Log("ORIGINAL_TILE_VISUALS PASS: all seven authored holds, original checkpoint graphics, native skin length and hit states");
        }

        private static object Invoke(Type type, string name, params object[] values)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Require(method != null, "tile visual interface exists: " + name);
            return method.Invoke(null, values);
        }
        private static void Near(float value, float expected, string message) => Require(Mathf.Abs(value - expected) < .0002f, message);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("ORIGINAL_TILE_VISUALS RED/FAIL: " + message);
        }
    }
}
