using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RadioArchipelago.Editor
{
    public static partial class IslandFinishThemeJobs
    {
        private static readonly string[] Definitions = {
            IslandAbyssJobs.DefinitionPath,CoralOasisIslandJobs.DefinitionPath,
            MoonResonanceIslandJobs.DefinitionPath,AmberWindsIslandJobs.DefinitionPath };
        [Serializable] private sealed class ThemeReport
        {
            public string definition,theme,meshSha256;
            public int triangles,renderers,flatSamples,coralClusters,shells,crystalClusters,crystals,autumnFlowers,fallenLeaves;
            public float maximumGroundError,maximumCentralDecorationHeight,decorationCheckHalfWidth;
            public bool noColliders,completeDisposal,borrowedMaterialsUnchanged;
        }
        [Serializable] private sealed class ThemeCheckReport
        {
            public bool passed,emeraldMeshExactlyPreserved,allThemesStructurallyDifferent;
            public string scope = "Actual runtime generators with four explicit themes and own island materials. Original Emerald vertex/color/UV/normal/index SHA compared to the native pre-change generator. Rotated actual-triangle ground samples and decoration-corridor clearance. No gameplay timing, route, colliders or material ownership changes.";
            public List<ThemeReport> themes = new List<ThemeReport>();
        }

        [MenuItem("Radio Archipelago/Finish worlds/Install island themes")]
        public static void Install()
        {
            RequireEditMode();
            // Never save/rewrite the first definition just to serialize a zero.
            for (int i=1;i<Definitions.Length;i++)
            {
                var definition=AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(Definitions[i]);
                if (!definition) throw new InvalidOperationException("Missing finish theme definition: "+Definitions[i]);
                var theme=(IslandFinishTheme)i;
                if (definition.FinishTheme==theme) continue;
                definition.FinishTheme=theme;
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
            }
            Debug.Log("FINISH_WORLD_THEMES installed: Coral shore, Moon crystal garden, Amber autumn; Emerald untouched.");
        }

        [MenuItem("Radio Archipelago/Finish worlds/Check island themes")]
        public static void RunChecks()
        {
            RequireEditMode();
            string baselinePath=ReviewFolder+"/emerald-before.json";
            if (!File.Exists(baselinePath)) throw new InvalidOperationException("Capture the original Emerald finish mesh before replacing the generator.");
            var baseline=JsonUtility.FromJson<Baseline>(File.ReadAllText(baselinePath));
            var report=new ThemeCheckReport();
            var hashes=new HashSet<string>();
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                for (int i=0;i<Definitions.Length;i++)
                {
                    var definition=AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(Definitions[i]);
                    if (!definition || definition.FinishTheme!=(IslandFinishTheme)i)
                        throw new InvalidOperationException("The island definition must explicitly select its own finish world: "+Definitions[i]);
                    var node=new GameObject("Finish world actual runtime validation");
                    SceneManager.MoveGameObjectToScene(node,scene);
                    try
                    {
                        var glade=node.AddComponent<IslandFinishGlade>();
                        Material[] borrowed={Borrow(definition,"Terrain"),Borrow(definition,"Cliffs"),Borrow(definition,"Grass"),Borrow(definition,"Foliage")};
                        var accent=definition.FinishTheme==IslandFinishTheme.MoonCrystalGarden ? Borrow(definition,"Crystal") : null;
                        var all=accent ? borrowed.Concat(new[] {accent}).ToArray() : borrowed;
                        var before=all.Select(material=>EditorJsonUtility.ToJson(material)).ToArray();
                        glade.Configure(new Vector3(21,0,-8),Quaternion.Euler(0,37,0)*Vector3.forward,5.2f,34,14,
                            borrowed[0],borrowed[1],borrowed[2],borrowed[3],71237,definition.FinishTheme,accent);
                        var item=new ThemeReport {definition=Definitions[i],theme=glade.Theme.ToString(),meshSha256=MeshFingerprint(glade),
                            triangles=glade.GeneratedTriangleCount,renderers=glade.GeneratedRendererCount,coralClusters=glade.CoralClusterCount,
                            shells=glade.ShellCount,crystalClusters=glade.CrystalClusterCount,crystals=glade.CrystalCount,
                            autumnFlowers=glade.AutumnFlowerCount,fallenLeaves=glade.FallenLeafCount};
                        report.themes.Add(item); hashes.Add(item.meshSha256);
                        if (i==0)
                        {
                            report.emeraldMeshExactlyPreserved=item.meshSha256==baseline.meshSha256;
                            Require(report.emeraldMeshExactlyPreserved,"Emerald finish mesh bytes changed from the native original baseline.");
                        }
                        Require(item.renderers==4 && item.triangles<180000,"Each finish world needs four bounded batched renderers.");
                        item.noColliders=node.GetComponentsInChildren<Collider>(true).Length==0;
                        Require(item.noColliders,"Decorative finish world must not add gameplay colliders.");
                        foreach (Mesh mesh in glade.OwnedMeshes)
                            foreach (Vector3 vertex in mesh.vertices) Require(Finite(vertex),"A themed mesh contains a nonfinite vertex.");
                        for (int zi=0;zi<=38;zi++) for (int xi=-8;xi<=8;xi++)
                        {
                            Vector3 point=glade.GeneratedRoot.TransformPoint(new Vector3(xi*.5f,0,zi-2));
                            Require(glade.TrySampleGround(point,out float height),"The full coin run needs actual ground beneath it.");
                            item.maximumGroundError=Mathf.Max(item.maximumGroundError,Mathf.Abs(height-5.2f)); item.flatSamples++;
                        }
                        Require(item.maximumGroundError<.0001f,"Theme changes must keep the same exactly flat collection ground.");
                        // Emerald's original tall flower beds deliberately sit
                        // at X3.35..4.5 on the planar ground. Its central low
                        // blooms are X±2.3. Do not misclassify those preserved
                        // side beds as new obstacles: check the actual central
                        // trail plus .2m margin, and retain its original metric.
                        // New worlds have a wider, independently strict 9m
                        // decoration clearance and are tested over all of it.
                        item.decorationCheckHalfWidth=i==0 ? 2.5f : 4.5f;
                        for (int meshIndex=2;meshIndex<glade.OwnedMeshes.Count;meshIndex++)
                            foreach (Vector3 vertex in glade.OwnedMeshes[meshIndex].vertices)
                                if (Mathf.Abs(vertex.x)<item.decorationCheckHalfWidth)
                                    item.maximumCentralDecorationHeight=Mathf.Max(item.maximumCentralDecorationHeight,vertex.y);
                        Require(item.maximumCentralDecorationHeight<=.30f,"High plants/crystals must stay outside the flat collection corridor.");
                        if (i==0)
                        {
                            Require(glade.MaxCorridorPlantHeight<=.30f,"Original low Emerald flowers must retain the established hover clearance.");
                        }
                        else if (i==1) Require(item.coralClusters>=30 && item.shells>=10 && item.crystals==0,"Coral needs actual coral/shell geometry, not reskinned meadow flowers.");
                        else if (i==2) Require(item.crystalClusters>=30 && item.crystals>=100 && item.coralClusters==0,"Moon needs multiple actual hexagonal crystal druses.");
                        else Require(item.autumnFlowers>=90 && item.fallenLeaves>=400 && item.crystals==0,"Amber needs autumn blooms and a visibly leaf-strewn ochre floor.");
                        item.borrowedMaterialsUnchanged=before.SequenceEqual(all.Select(material=>EditorJsonUtility.ToJson(material)));
                        Require(item.borrowedMaterialsUnchanged,"Finish generation mutated borrowed island materials.");
                        Mesh[] owned=glade.OwnedMeshes.ToArray(); glade.Dispose();
                        item.completeDisposal=glade.GeneratedRoot==null && glade.OwnedMeshes.Count==0 && owned.All(mesh=>mesh==null);
                        Require(item.completeDisposal,"All finish meshes must be released on teardown.");
                    }
                    finally { Object.DestroyImmediate(node); }
                }
                report.allThemesStructurallyDifferent=hashes.Count==4;
                Require(report.allThemesStructurallyDifferent,"The four finish worlds must have different actual generated mesh data.");
                report.passed=true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene); Directory.CreateDirectory(ReviewFolder);
                File.WriteAllText(ReviewFolder+"/theme-checks.json",JsonUtility.ToJson(report,true));
            }
            Debug.Log("FINISH_WORLD_THEMES PASS: four distinct worlds, exact Emerald preservation, flat unobstructed collection surface and complete cleanup.");
        }

        private static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
        private static void Require(bool ok,string message) { if (!ok) throw new InvalidOperationException(message); }
    }
}
