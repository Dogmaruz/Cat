using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RadioArchipelago.Editor
{
    /// <summary>Targeted migration of seven existing lake/outlet/fall surfaces; never rebuilds an island.</summary>
    public static class IslandWaterfallRolloutJobs
    {
        public const string ShaderName = "RadioArchipelago/Amber Flow Water";
        public const string ReviewFolder = "Library/RadioArchipelago/UnifiedLakeWaterfalls";
        private const string Source = "../ArtSource/RadioArchipelago/Islands/UnifiedLakeWaterfalls20261102/STAGING/";
        private sealed class Island
        {
            public string Id, Stage, File, Before, Previous, After, Prefix, SourceFolder; public int SheetCount=2;
            public string Model => "Assets/RadioArchipelago/Imported/Islands/" + Id + "/" + File;
            public string Prefab => "Assets/RadioArchipelago/Content/Islands/" + Id + "/" + Id + ".prefab";
            public string Material => "Assets/RadioArchipelago/Content/Islands/" + Id + "/" + Prefix + "Waterfall.mat";
            public string Staged => (SourceFolder ?? Source + Stage) + "/" + File;
            public string Handoff => (SourceFolder ?? Source + Stage) + "/handoff.json";
        }
        private static readonly Island[] Islands = {
            new Island { Id="CoralOasis", Stage="Coral", File="coral_oasis_island.fbx", Prefix="RA_Coral_",
                SourceFolder="../ArtSource/RadioArchipelago/Islands/CoralVisibleDrains20261102/STAGING",
                Before="6fb98555cb33402282d55f41eeb7e064c4d812d72083a999d85d7c478c9da7b2", Previous="f64e9bd7b0e51ff365169112c89ab5d04cc54a1e341815abaf7573ea8c81170b", After="37e99875187a9e0e67a6405a042dba3919e8b96377111fc54ec627eaa1ccb4d0" },
            new Island { Id="MoonResonance", Stage="Moon", File="moon_resonance_island.fbx", Prefix="RA_Moon_",
                Before="c2407e489e3ec0e85322e66897e8379f3828b143d09111781df8944fc38912be", Previous="fba3247c094803b1a51b519e528afc3b3b1b139b40bf728320b4ef1941cd9c5f", After="d507cdcacc4a6288743f03513cda0b1de0496b6ffa9d7781465ced6d414a4331" },
            new Island { Id="AmberWinds", Stage="Amber", File="amber_winds_island.fbx", Prefix="RA_Amber_", SheetCount=3,
                Before="18cfb754ba06d63a01a95c069ff5d7dbc5142e7415b42c6f19bf48187e45305f", After="f49534c8e60ccc133e674860c5ef91042478f3327f1956c5ce79a791e075ca43" }
        };
        [Serializable] private sealed class Handoff { public Sheet[] sheets; public string[] consumed_pool_nodes; }
        [Serializable] private sealed class Sheet
        {
            public string node; public int triangles; public float[] uv_v, unity_head, unity_bottom;
            public float metric_start_v; public float[] metric_start_unity;
        }
        [Serializable] private sealed class Evidence
        {
            public bool passed; public string scope; public List<FallEvidence> falls = new List<FallEvidence>();
            public List<string> preservation = new List<string>();
        }
        [Serializable] private sealed class FallEvidence
        {
            public string island, node; public int vertices, triangles, components, indexedComponents, transitionTriangles, interpolatedMouthVertices;
            public string firstMetricRowSamples;
            public float length, metricLength, lakeVMinimum, lakeVMaximum; public int lakeVertices, channelVertices; public Bounds worldBounds;
        }
        private sealed class Snapshot
        {
            public Dictionary<string,string> Poses = new Dictionary<string,string>();
            public Dictionary<string,string> Renderers = new Dictionary<string,string>();
            public Dictionary<string,Material[]> Materials = new Dictionary<string,Material[]>();
        }

        public static void ConfigureMaterial(Material material, bool moon)
        {
            if (!material) throw new ArgumentNullException(nameof(material));
            var shader = Shader.Find(ShaderName);
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Accepted continuous water shader is missing or invalid.");
            material.shader = shader; material.enableInstancing = true;
            material.SetColor("_BaseColor", moon ? new Color(.17f,.41f,.63f,1) : new Color(.10f,.52f,.57f,1));
            material.SetColor("_FoamColor", moon ? new Color(.86f,.93f,1,1) : new Color(.86f,.97f,.94f,1));
            material.SetFloat("_UseLakeSurface", 1);
            material.SetColor("_LakeColor", moon ? new Color(.13f,.37f,.59f,1) : new Color(.08f,.48f,.51f,1));
            material.SetFloat("_CurrentSpeed", 4.8f); material.SetFloat("_FoamStrength", .92f);
            material.SetTexture("_RippleNormal", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RadioArchipelago/Art/WaterRippleNormal.png"));
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
        }

        public static void ConfigureAmberMaterial(Material material)
        {
            ConfigureMaterial(material, false);
            // Preserve the accepted Amber curtain palette, speed, foam and ripple normal.
            material.SetColor("_BaseColor", new Color(.12f,.48f,.50f,1));
            material.SetColor("_FoamColor", new Color(.86f,.97f,.94f,1));
            material.SetColor("_LakeColor", new Color(.12f,.48f,.50f,1));
            EditorUtility.SetDirty(material);
        }

        public static void Install() => InstallSelected(Islands);
        public static void InstallCoral() => InstallSelected(Islands.Where(i=>i.Stage=="Coral").ToArray());
        public static void InstallCoralAndMoon() => InstallSelected(Islands.Where(i=>i.Stage!="Amber").ToArray());
        private static void InstallSelected(Island[] selected)
        {
            IslandReliefJobs.RequireEditMode();
            var snapshots = new Dictionary<string,Snapshot>();
            var backups = new Dictionary<string,byte[]>();
            var guids = new Dictionary<string,string>();
            foreach (var island in selected)
            {
                Require(island.After.Length==64, "Final reviewed "+island.Id+" lake/waterfall export is not pinned yet; full seven-surface installation must wait.");
                Require(Hash(island.Staged) == island.After, "Reviewed staged FBX changed: " + island.Staged);
                string current = Hash(island.Model);
                Require(current == island.Before || current == island.Previous || current == island.After, "Unexpected installed FBX revision: " + island.Model);
                Require(ReadHandoff(island).sheets.Length == island.SheetCount, "Each source must contain its reviewed falls.");
                guids[island.Id] = AssetDatabase.AssetPathToGUID(island.Model);
                foreach (var path in new[] { island.Model, island.Model+".meta", island.Prefab, island.Material }) backups[path] = File.ReadAllBytes(path);
                var contents = PrefabUtility.LoadPrefabContents(island.Prefab);
                try { snapshots[island.Id] = Capture(contents, island); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            try
            {
                foreach (var island in selected)
                {
                    if (Hash(island.Model) != island.After)
                    {
                        File.Copy(island.Staged, island.Model, true);
                        AssetDatabase.ImportAsset(island.Model, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    }
                    Require(AssetDatabase.AssetPathToGUID(island.Model) == guids[island.Id], "Model GUID changed.");
                    var material = AssetDatabase.LoadAssetAtPath<Material>(island.Material);
                    if(island.Stage=="Amber") ConfigureAmberMaterial(material); else ConfigureMaterial(material, island.Stage == "Moon");
                    var contents = PrefabUtility.LoadPrefabContents(island.Prefab);
                    try
                    {
                        var falls = ReadHandoff(island).sheets.Select(s => s.node).ToHashSet();
                        foreach (var instance in contents.GetComponentsInChildren<Transform>(true))
                        {
                            if (!PrefabUtility.IsAnyPrefabInstanceRoot(instance.gameObject)) continue;
                            var modifications = PrefabUtility.GetPropertyModifications(instance.gameObject);
                            if (modifications != null && modifications.Any(m => !m.target))
                                PrefabUtility.SetPropertyModifications(instance.gameObject, modifications.Where(m => m.target).ToArray());
                        }
                        foreach (var renderer in contents.GetComponentsInChildren<Renderer>(true))
                        {
                            string key = PathOf(renderer.transform, contents.transform);
                            if (falls.Contains(renderer.name)) renderer.sharedMaterials = new[] { material };
                            // Reapply the existing explicit binding if FBX local IDs changed on reimport.
                            else if (snapshots[island.Id].Materials.TryGetValue(key, out var existing) && !renderer.sharedMaterials.SequenceEqual(existing))
                                renderer.sharedMaterials = existing;
                        }
                        VerifyPreserved(snapshots[island.Id], Capture(contents, island));
                        ValidateFalls(contents, island, new Evidence());
                        PrefabUtility.SaveAsPrefabAsset(contents, island.Prefab);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                    AssetDatabase.SaveAssetIfDirty(material);
                }
                CheckSelected(selected);
                var report = new Evidence { passed=true, scope=ScopeFor(selected) };
                foreach (var island in selected)
                {
                    var contents = PrefabUtility.LoadPrefabContents(island.Prefab);
                    try
                    {
                        var after = Capture(contents, island); VerifyPreserved(snapshots[island.Id], after);
                        report.preservation.Add(island.Id + ": " + after.Poses.Count + " local poses/flags and " + after.Renderers.Count + " unrelated renderer bindings/shadows preserved; GUID=" + guids[island.Id]);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                }
                Directory.CreateDirectory(FolderFor(selected));
                File.WriteAllText(FolderFor(selected)+"/installation-preservation.json", JsonUtility.ToJson(report,true));
                Debug.Log("ISLAND_WATERFALL_INSTALL PASS ["+report.scope+"]: exact reviewed FBX revisions, "+selected.Sum(i=>i.SheetCount)+" continuous lake/outlet/fall surfaces, unrelated prefab overrides preserved.");
            }
            catch
            {
                foreach (var backup in backups) File.WriteAllBytes(backup.Key, backup.Value);
                foreach (var island in selected)
                    foreach (var path in new[] { island.Model, island.Prefab, island.Material })
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                throw;
            }
        }

        public static void RunChecks() => CheckSelected(Islands);
        public static void RunCoralChecks() => CheckSelected(Islands.Where(i=>i.Stage=="Coral").ToArray());
        public static void RunCoralAndMoonChecks() => CheckSelected(Islands.Where(i=>i.Stage!="Amber").ToArray());
        private static void CheckSelected(Island[] selected)
        {
            IslandReliefJobs.RequireEditMode();
            var report = new Evidence { scope=ScopeFor(selected) };
            foreach (var island in selected)
            {
                Require(island.After.Length==64, "Final reviewed "+island.Id+" lake/waterfall export is not pinned yet.");
                Require(Hash(island.Model) == island.After, "Installed model is not the reviewed waterfall revision: " + island.Id);
                var contents = PrefabUtility.LoadPrefabContents(island.Prefab);
                try { ValidateFalls(contents, island, report); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            report.passed = report.falls.Count == selected.Sum(i=>i.SheetCount);
            Require(report.passed, "Unexpected installed fall count for "+report.scope);
            Directory.CreateDirectory(FolderFor(selected));
            File.WriteAllText(FolderFor(selected)+"/checks.json", JsonUtility.ToJson(report,true));
            Debug.Log("ISLAND_WATERFALL_CHECKS PASS ["+report.scope+"]: "+report.falls.Count+" connected lake/outlet/fall surfaces, native-clock shader, lake masks, downstream metric UVs, alpha fades, normals and transformed endpoints.");
        }
        private static string ScopeFor(Island[] selected) => selected.Length==1 && selected[0].Stage=="Coral" ? "Coral2" : selected.Length==2 ? "CoralMoon4" : "AllSeven";
        private static string FolderFor(Island[] selected) => ReviewFolder+"/"+ScopeFor(selected);

        private static void ValidateFalls(GameObject root, Island island, Evidence report)
        {
            var sheets = ReadHandoff(island).sheets;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var consumed in ReadHandoff(island).consumed_pool_nodes)
                Require(!renderers.Any(r=>r.name==consumed), "Consumed pool renderer remains below unified water: " + consumed);
            Require(renderers.Count(r => r.sharedMaterials.Any(m=>m && AssetDatabase.GetAssetPath(m)==island.Material)) == sheets.Length, "Unexpected waterfall material renderer outside the reviewed unified surfaces.");
            foreach (var sheet in sheets)
            {
                var matches = renderers.Where(r => r.name == sheet.node).ToArray();
                Require(matches.Length == 1, "Missing/duplicate fall: " + sheet.node);
                var renderer = matches[0]; var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Require(mesh && mesh.subMeshCount == 1 && renderer.sharedMaterials.Length == 1, "Lake/outlet/fall must be one material mesh.");
                var material = renderer.sharedMaterial;
                Require(material && material.shader && material.shader.name == ShaderName && !ShaderUtil.ShaderHasError(material.shader), "Wrong waterfall shader.");
                Require(Mathf.Abs(material.GetFloat("_CurrentSpeed")-4.8f)<.001f && Mathf.Abs(material.GetFloat("_FoamStrength")-.92f)<.001f, "Waterfall flow parameters drifted.");
                Require(material.HasProperty("_UseLakeSurface") && material.GetFloat("_UseLakeSurface")>.99f, "Unified lake shader variant is not enabled.");
                var vertices = mesh.vertices; var uv = mesh.uv; var region = mesh.uv2; var normals = mesh.normals; var colors = mesh.colors;
                Require(vertices.Length == uv.Length && region.Length == vertices.Length && normals.Length == vertices.Length && colors.Length == vertices.Length, "Missing lake/ribbon attributes.");
                var triangles = mesh.triangles;
                Require(triangles.Length/3 == sheet.triangles, "Reviewed topology mismatch: "+island.Id+"/"+sheet.node+" actualTriangles="+(triangles.Length/3)+" expectedTriangles="+sheet.triangles+" importedVertices="+vertices.Length);
                var parent = Enumerable.Range(0,vertices.Length).ToArray();
                var indexedParent = Enumerable.Range(0,vertices.Length).ToArray();
                var welds = new Dictionary<Vector3Int,int>();
                Func<int,int> find = null; find = i => parent[i] == i ? i : parent[i] = find(parent[i]);
                Action<int,int> join = (a,b) => parent[find(a)] = find(b);
                Func<int,int> indexedFind = null; indexedFind = i => indexedParent[i]==i ? i : indexedParent[i]=indexedFind(indexedParent[i]);
                var rows = new SortedDictionary<float,List<Vector4>>();
                var rowColumns = new SortedDictionary<float,Dictionary<int,Vector3>>();
                int lakeCount=0, channelCount=0; float lakeMin=float.PositiveInfinity, lakeMax=float.NegativeInfinity;
                for (int i=0;i<vertices.Length;i++)
                {
                    Require(Finite(vertices[i]) && Finite(normals[i]) && normals[i].sqrMagnitude>.5f && normals[i].sqrMagnitude<1.5f && float.IsFinite(uv[i].y), "Invalid ribbon vertex/normal/UV.");
                    Require(float.IsFinite(uv[i].x) && float.IsFinite(region[i].x) && region[i].x>=-.001f && region[i].x<=1.001f && colors[i].a>=0 && colors[i].a<=1, "Invalid lake/ribbon region, UV or alpha.");
                    if(region[i].x<.999f) Require(colors[i].a>.99f, "Lake/outlet retained a separate upstream ribbon alpha fade.");
                    var p = vertices[i]; var key = new Vector3Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000),Mathf.RoundToInt(p.z*10000));
                    if (welds.TryGetValue(key,out int previous)) join(i,previous); else welds[key]=i;
                    if(region[i].x<.001f) { lakeCount++; lakeMin=Mathf.Min(lakeMin,uv[i].y); lakeMax=Mathf.Max(lakeMax,uv[i].y); }
                    if(region[i].x<.999f) continue;
                    Require(uv[i].x>=-.001f && uv[i].x<=1.001f, "Ribbon width must use U0..1.");
                    channelCount++;
                    if(uv[i].y<sheet.metric_start_v-.0001f) continue;
                    float v = Mathf.Round(uv[i].y*10000)/10000;
                    if (!rows.ContainsKey(v)) { rows[v] = new List<Vector4>(); rowColumns[v] = new Dictionary<int,Vector3>(); }
                    var world = renderer.transform.TransformPoint(p);
                    rows[v].Add(new Vector4(world.x,world.y,world.z,uv[i].x));
                    // Unity may split a source vertex for corner normals/tangents. Count each authored U column once.
                    float u = uv[i].x; int column = Mathf.RoundToInt(u*32);
                    // CDT can insert another point along a mouth edge. Validate it after all authored columns are known.
                    if(column<0 || column>32 || u!=column/32f) continue;
                    if(rowColumns[v].TryGetValue(column,out var existingColumn))
                        Require(Vector3.Distance(existingColumn,world)<.00001f, "Split UV column positions disagree: "+island.Id+"/"+sheet.node+" V="+v.ToString("R")+" U="+u.ToString("R")+" column="+column);
                    else rowColumns[v][column] = world;
                }
                int transitionTriangles=0;
                for (int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    join(a,b); join(a,c);
                    indexedParent[indexedFind(a)]=indexedFind(b); indexedParent[indexedFind(a)]=indexedFind(c);
                    float low=Mathf.Min(region[a].x,region[b].x,region[c].x), high=Mathf.Max(region[a].x,region[b].x,region[c].x);
                    if(high-low>.01f) transitionTriangles++;
                }
                int components = Enumerable.Range(0,vertices.Length).Select(find).Distinct().Count();
                int indexedComponents = Enumerable.Range(0,vertices.Length).Select(indexedFind).Distinct().Count();
                Require(components == 1, "Lake/outlet/fall is disconnected.");
                Require(transitionTriangles>0, "Lake and outlet masks must share actual transition triangles.");
                Require(lakeCount>20 && channelCount>100 && lakeMin<-.1f && lakeMax-lakeMin>.1f && rows.Count>50, "Unified mesh must contain a broad lake and a genuine flowing channel/body.");
                int interpolatedMouthVertices=0;
                foreach(var row in rowColumns)
                {
                    Require(row.Value.Count==33, "Metric row must contain all33 authored U columns: "+island.Id+"/"+sheet.node+" V="+row.Key.ToString("R")+" authoredColumns="+row.Value.Count+" importedVertices="+rows[row.Key].Count+" samples="+RowSamples(rows[row.Key]));
                    foreach(var sample in rows[row.Key])
                    {
                        float u=sample.w; int column=Mathf.RoundToInt(u*32);
                        if(column>=0 && column<=32 && u==column/32f) continue;
                        int left=Mathf.FloorToInt(u*32);
                        Require(left>=0 && left<32, "Interpolated mouth U is outside its authored neighbors: "+u.ToString("R"));
                        Vector3 expected=Vector3.LerpUnclamped(row.Value[left],row.Value[left+1],u*32-left), actual=new Vector3(sample.x,sample.y,sample.z);
                        Require(Vector3.Distance(expected,actual)<.0001f, "Additional mouth vertex is not on its authored row edge: "+island.Id+"/"+sheet.node+" V="+row.Key.ToString("R")+" U="+u.ToString("R")+" actual="+actual.ToString("R")+" interpolated="+expected.ToString("R")+" samples="+RowSamples(rows[row.Key]));
                        interpolatedMouthVertices++;
                    }
                }
                var centers = rowColumns.Select(pair => new { V=pair.Key, Center=pair.Value.Values.Aggregate(Vector3.zero,(a,b)=>a+b)/pair.Value.Count }).ToArray();
                float length = centers.Last().V-centers.First().V, metric = 0;
                for (int i=1;i<centers.Length;i++) metric += Vector3.Distance(centers[i-1].Center,centers[i].Center);
                Require(Mathf.Abs(centers.Last().V-sheet.uv_v[1])<.02f && Mathf.Abs(length-metric)<.08f && Mathf.Abs(centers[0].V-sheet.metric_start_v)<.001f, "Channel/body UV.V must measure downstream metres.");
                Require(Vector3.Distance(centers.First().Center,V3(sheet.metric_start_unity))<.03f && Vector3.Distance(centers.Last().Center,V3(sheet.unity_bottom))<.03f,
                    "Transformed channel/body endpoints changed: island="+island.Id+" node="+sheet.node+" startV="+centers.First().V.ToString("R")+" expectedStartV="+sheet.metric_start_v.ToString("R")+
                    " actualFirstCenter="+centers.First().Center.ToString("R")+" expectedFirstCenter="+V3(sheet.metric_start_unity).ToString("R")+
                    " actualEndCenter="+centers.Last().Center.ToString("R")+" expectedEndCenter="+V3(sheet.unity_bottom).ToString("R")+
                    " rowCount="+centers.Length+" firstRowVertices="+rows.First().Value.Count+" firstRowColumns="+rowColumns.First().Value.Count+" lastRowVertices="+rows.Last().Value.Count+" lastRowColumns="+rowColumns.Last().Value.Count);
                Require(centers.First().Center.y-centers.Last().Center.y>1 && colors.Min(c=>c.a)<.01f && colors.Max(c=>c.a)>.99f, "Missing drop or endpoint fades.");
                report.falls.Add(new FallEvidence { island=island.Id,node=sheet.node,vertices=vertices.Length,triangles=triangles.Length/3,components=components,indexedComponents=indexedComponents,transitionTriangles=transitionTriangles,interpolatedMouthVertices=interpolatedMouthVertices,firstMetricRowSamples=RowSamples(rows.First().Value),length=length,metricLength=metric,worldBounds=renderer.bounds,lakeVertices=lakeCount,channelVertices=channelCount,lakeVMinimum=lakeMin,lakeVMaximum=lakeMax });
            }
            foreach (var renderer in renderers.Where(r=>r.name.Contains("Foam")))
                Require(renderer.sharedMaterials.All(m=>m && m.shader && m.shader.name=="RadioArchipelago/Coral Water" && m.HasProperty("_FoamLayer") && m.GetFloat("_FoamLayer")>.5f), "Legacy foam must retain its clipped legacy material.");
        }

        private static Snapshot Capture(GameObject root, Island island)
        {
            var result = new Snapshot();
            var excluded = ReadHandoff(island).sheets.Select(s=>s.node).Concat(ReadHandoff(island).consumed_pool_nodes).ToHashSet();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (excluded.Contains(t.name)) continue;
                result.Poses[PathOf(t,root.transform)] = t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+"/"+t.gameObject.activeSelf+"/"+t.gameObject.layer+"/"+t.gameObject.tag+"/"+(int)GameObjectUtility.GetStaticEditorFlags(t.gameObject);
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (excluded.Contains(r.name)) continue;
                string key = PathOf(r.transform,root.transform);
                result.Materials[key] = r.sharedMaterials;
                result.Renderers[key] = string.Join("|",r.sharedMaterials.Select(AssetId))+"/"+r.shadowCastingMode+"/"+r.receiveShadows+"/"+r.enabled+"/"+r.lightProbeUsage+"/"+r.reflectionProbeUsage+"/"+r.renderingLayerMask+"/"+r.sortingLayerID+"/"+r.sortingOrder+"/"+r.allowOcclusionWhenDynamic+"/"+r.motionVectorGenerationMode;
                if(r is ParticleSystemRenderer spray)
                    result.Renderers[key] += "/spray/"+AssetId(spray.trailMaterial)+"/"+spray.renderMode+"/"+spray.alignment+"/"+AssetId(spray.mesh)+"/"+spray.enableGPUInstancing+"/"+spray.pivot.ToString("R")+"/"+spray.flip.ToString("R")+"/"+spray.minParticleSize.ToString("R")+"/"+spray.maxParticleSize.ToString("R")+"/"+spray.lengthScale.ToString("R")+"/"+spray.velocityScale.ToString("R")+"/"+spray.cameraVelocityScale.ToString("R");
            }
            return result;
        }
        private static void VerifyPreserved(Snapshot before, Snapshot after)
        {
            foreach (var pair in new[] { (before.Poses,after.Poses), (before.Renderers,after.Renderers) })
            {
                Require(pair.Item1.Count==pair.Item2.Count, "Nonfall hierarchy/renderer count changed.");
                foreach (var entry in pair.Item1) Require(pair.Item2.TryGetValue(entry.Key,out var value) && value==entry.Value, "Existing prefab state changed: " + entry.Key);
            }
        }
        private static string AssetId(UnityEngine.Object material) => !material ? "null" : AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material,out string guid,out long id) ? guid+":"+id.ToString(CultureInfo.InvariantCulture) : material.name;
        private static string PathOf(Transform t, Transform root) => t==root ? "$" : PathOf(t.parent,root)+"/"+t.name;
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private static string RowSamples(IEnumerable<Vector4> row) => string.Join("; ", row.Select(p=>p.w.ToString("R",CultureInfo.InvariantCulture)+"=>"+new Vector3(p.x,p.y,p.z).ToString("R")).Distinct());
        private static Vector3 V3(float[] p) => new Vector3(p[0],p[1],p[2]);
        private static Handoff ReadHandoff(Island island) => JsonUtility.FromJson<Handoff>(File.ReadAllText(island.Handoff));
        private static string Hash(string path) { using (var sha=SHA256.Create()) using(var stream=File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant(); }
        private static void Require(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
